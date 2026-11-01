"""Automated checks: budget, skinning, shape keys, clips and limb/body interpenetration per clip."""

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from . import config as C
from .animation import solo, unsolo
from .clips import CLIPS, HOP_SPEED_MPS, WALK_SPEED_MPS
from .geometry import AUX_ATTR, PART_ATTR

TRI_BUDGET = (15000, 30000)
PENETRATION_SAMPLE_STEP = 2
# Arm vertices closer than this to the shoulder pivot live inside the body by design (ball joint).
ARM_HIDDEN_AXIAL = 0.11
PENETRATION_TOLERANCE = 0.004   # metres; shading-level contact is fine, visible clipping is not
# Think: the mitten presses the cheek and the inner forearm runs up inside the body's side (the cheek
# is ~25 cm in front of the shoulder, beyond what the stubby arm reaches from outside). Seen from the
# side, the forearm emerges from the blob like a stub limb; no mitten or tip pokes through the face.
PENETRATION_TOLERANCE_BY_CLIP = {"Think": 0.032}
LOCOMOTION = {"Walk": WALK_SPEED_MPS, "Hop": HOP_SPEED_MPS}


def _face_parts(mesh):
    parts = np.empty(len(mesh.polygons), dtype=np.int32)
    mesh.attributes[PART_ATTR].data.foreach_get("value", parts)
    return parts


def static_report(obj, arm_obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    tris = len(mesh.loop_triangles)
    weighted = np.zeros(len(mesh.vertices), dtype=bool)
    max_influences = 0
    bad_sum = 0
    for v in mesh.vertices:
        ws = [g.weight for g in v.groups if g.weight > 1e-6]
        if ws:
            weighted[v.index] = True
        max_influences = max(max_influences, len(ws))
        if ws and abs(sum(ws) - 1.0) > 1e-3:
            bad_sum += 1
    bones = [b.name for b in arm_obj.data.bones]
    unknown_groups = [g.name for g in obj.vertex_groups if g.name not in bones]
    report = {
        "triangles": tris,
        "vertices": len(mesh.vertices),
        "tri_budget_ok": TRI_BUDGET[0] <= tris <= TRI_BUDGET[1],
        "bones": len(bones),
        "unweighted_vertices": int((~weighted).sum()),
        "max_influences": max_influences,
        "non_normalised_vertices": bad_sum,
        "groups_without_bone": unknown_groups,
        "shape_keys": [k.name for k in mesh.shape_keys.key_blocks[1:]],
        "materials": [m.name for m in mesh.materials],
        "clips": [t.name for t in arm_obj.animation_data.nla_tracks],
    }
    return report


def _vertex_sets(obj):
    mesh = obj.data
    parts = _face_parts(mesh)
    aux = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.attributes[AUX_ATTR].data.foreach_get("vector", aux)
    aux = aux.reshape(-1, 3)
    body_faces = [list(p.vertices) for p in mesh.polygons if parts[p.index] == C.PART_BODY]
    limb = {}
    for p in mesh.polygons:
        part = parts[p.index]
        for vi in p.vertices:
            if part == C.PART_ARM and aux[vi][0] > ARM_HIDDEN_AXIAL:
                limb.setdefault("arm_" + ("L" if mesh.vertices[vi].co.x > 0 else "R"), set()).add(vi)
            elif part == C.PART_THUMB:
                limb.setdefault("arm_" + ("L" if mesh.vertices[vi].co.x > 0 else "R"), set()).add(vi)
            elif part == C.PART_FOOT:
                limb.setdefault("foot_" + ("L" if mesh.vertices[vi].co.x > 0 else "R"), set()).add(vi)
    foot_faces = {side: [list(p.vertices) for p in mesh.polygons
                         if parts[p.index] == C.PART_FOOT and (mesh.vertices[p.vertices[0]].co.x > 0) == (side == "L")]
                  for side in "LR"}
    return body_faces, {k: sorted(v) for k, v in limb.items()}, foot_faces


def _max_depth(bvh, points):
    worst = 0.0
    for p in points:
        loc, normal, _i, dist = bvh.find_nearest(p)
        if loc is not None and (p - loc).dot(normal) < 0.0:
            worst = max(worst, dist)
    return worst


def penetration_report(obj, arm_obj):
    """Per clip: deepest limb vertex inside the body shell, and foot-vs-foot overlap."""
    body_faces, limbs, foot_faces = _vertex_sets(obj)
    scene = bpy.context.scene
    out = {}
    for clip in CLIPS:
        start = solo(arm_obj, clip.name)
        worst = {"arms_in_body": 0.0, "feet_overlap": 0.0, "worst_frame": 0, "worst_arm": None}
        for f in range(0, clip.length + 1, PENETRATION_SAMPLE_STEP):
            scene.frame_set(start + f)
            deps = bpy.context.evaluated_depsgraph_get()
            ev = obj.evaluated_get(deps)
            co = [v.co.copy() for v in ev.data.vertices]
            body = BVHTree.FromPolygons(co, body_faces)
            depths = {k: _max_depth(body, [co[i] for i in limbs.get(k, [])]) for k in ("arm_L", "arm_R")}
            arm_key = max(depths, key=depths.get)
            d_arm = depths[arm_key]
            foot_l = BVHTree.FromPolygons(co, foot_faces["L"])
            d_feet = _max_depth(foot_l, [co[i] for i in limbs.get("foot_R", [])[::3]])
            if d_arm > worst["arms_in_body"]:
                worst["arms_in_body"], worst["worst_frame"], worst["worst_arm"] = d_arm, f, arm_key
            worst["feet_overlap"] = max(worst["feet_overlap"], d_feet)
        tol = PENETRATION_TOLERANCE_BY_CLIP.get(clip.name, PENETRATION_TOLERANCE)
        worst["ok"] = worst["arms_in_body"] <= tol and worst["feet_overlap"] <= tol
        out[clip.name] = {k: (round(v, 4) if isinstance(v, float) else v) for k, v in worst.items()}
    unsolo(arm_obj)
    return out


def blink_report(obj):
    """Closed-eye height relative to open height for each blink key (smaller = more closed)."""
    mesh = obj.data
    parts = _face_parts(mesh)
    eye_verts = sorted({vi for p in mesh.polygons if parts[p.index] == C.PART_EYE for vi in p.vertices})
    keys = mesh.shape_keys.key_blocks
    out = {}
    for name, side in (("Blink_L", "L"), ("Blink_R", "R")):
        idx = [i for i in eye_verts if (mesh.vertices[i].co.x > 0) == (side == "L")]
        open_h = np.ptp([keys["Basis"].data[i].co.z for i in idx])
        closed_h = np.ptp([keys[name].data[i].co.z for i in idx])
        out[name] = round(float(closed_h / open_h), 3)
    return out


def locomotion_report(arm_obj):
    """Per gait clip: design ground speed at 1x, measured planted-foot slide speed, and the worst gap
    between a foot target and where the leg IK actually put the ankle (> a few mm = unreachable = slide)."""
    clips = {c.name: c for c in CLIPS}
    scene = bpy.context.scene
    out = {}
    for name, speed in LOCOMOTION.items():
        clip = clips[name]
        start = solo(arm_obj, name)
        ik_err = 0.0
        planted = []
        for f in range(clip.length + 1):
            scene.frame_set(start + f)
            spec = clip.pose(float(f))
            for side in "LR":
                off, _deg = spec.foot[side]
                target = arm_obj.data.bones[f"Foot_{side}"].head_local + Vector(off)
                got = arm_obj.pose.bones[f"Foot_{side}"].head  # armature space == character space
                ik_err = max(ik_err, (got - target).length)
            planted.append(spec.foot["L"][0])
        slide = [(b[1] - a[1]) * C.FPS for a, b in zip(planted, planted[1:]) if a[2] == 0.0 and b[2] == 0.0]
        out[name] = {"design_speed_mps": round(speed, 3),
                     "planted_foot_speed_mps": round(max(slide), 3) if slide else None,
                     "max_foot_ik_error_mm": round(ik_err * 1000.0, 2),
                     "ok": ik_err <= 0.003}
    unsolo(arm_obj)
    return out
