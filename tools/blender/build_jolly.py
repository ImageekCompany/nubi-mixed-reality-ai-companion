"""Builds a LOCAL-ONLY "Jolly-like" demo skin from a scan mesh the user supplies, as a drop-in for Nubi.

IP constraint: the source mesh and everything derived from it (FBX, textures, .blend, renders, report)
are third-party character IP. They are written only to git-ignored folders and are never committed or
published. This script holds no mesh, texture or image data; the body comes from --src at build time, and
the few landmark constants below (cut heights, eye and shoulder positions) were measured on that print.

Usage (from the repo root):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
        --python tools/blender/build_jolly.py -- --src <mesh.glb|mesh.stl> [--no-render] [--clips Walk,Wave]

Outputs (all git-ignored):
    JollyXR/Assets/Jolly/Art/Private/JollyDemo/Jolly.fbx, Jolly_FacePresets.json, build_report.json
    JollyXR/Assets/Jolly/Art/Private/JollyDemo/Textures/T_Jolly_Albedo.png, T_Jolly_Normal.png
    tools/blender/out/jolly/Jolly.blend and jolly_*.png renders

Drop-in contract: the same 22 bones, 13 shape keys and 15 clips as Nubi (docs/art/nubi/build_report.json).
The Nubi package is reused as is. Its modules read their design constants from module globals at call
time, so `apply_profile` swaps in Jolly's values before anything is built; the Nubi build never runs
in the same process, so nothing leaks back.

What comes from the scan and what is generated:
  scan     body, hood and face plate (arms deflated into the torso, legs flattened, voxel remeshed,
           fur relief softened, decimated)
  generated arms and mittens, legs and feet (separate shells so they can swing without tearing the body),
           eyes, mouth, UVs, albedo + fur normal map, rig, weights, shape keys and clips (Nubi's)
"""

import argparse
import json
import os
import sys
from math import atan2, cos, pi, radians, sin
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

sys.path.insert(0, str(Path(__file__).resolve().parent))

from nubi import config as C  # noqa: E402
from nubi import geometry as G  # noqa: E402
from nubi import animation, clips, export, faces, previews, rig, shapes, texture, validate  # noqa: E402
from nubi import render as R  # noqa: E402
from nubi.clips import CLIPS  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
DEFAULT_OUT_DIR = REPO / "JollyXR" / "Assets" / "Jolly" / "Art" / "Private" / "JollyDemo"
DEFAULT_RENDER_DIR = REPO / "tools" / "blender" / "out" / "jolly"

# ------------------------------------------------------------- scan fit ----
# Fractions and lengths below were measured on the "v2 Whole" print scaled to HEIGHT (cross-sections and
# front ray casts); they describe where to cut, not what the character looks like.
HEIGHT = 0.86                  # metres; Nubi's body height without the sprout, so the gait clips fit
WELD_DIST = 1e-6               # fraction of height
# The print's stub legs and coat hem are flattened into the body bottom at LEG_CUT_Z and pulled inside
# LEG_TUCK of the body's footprint just above (a cut at the hem line, 0.10, left a thin flange).
LEG_CUT_Z = 0.125
LEG_TUCK = 0.85
# Its fused arms are deflated between these heights, fading out towards the top so part of the print's
# shoulder stays as a pad the generated arm tucks under (a fully deflated side left the arm floating).
ARM_ZONE_Z = (0.110, 0.400, 0.520)
ARM_ZONE_THETA = (-40.0, 50.0)      # section angles (deg, 0 = +X, -90 = front) bracketing the arm bump
ARM_GROOVE = 0.022              # the deflated side is pressed in a little more, so the hanging arm clears it
VOXEL_SIZE = 0.007
SMOOTH_ITERATIONS = 4           # Laplacian passes: sculpted fur grooves -> soft plush surface
SMOOTH_LAMBDA = 0.8
# The print's dot eyes and the tiny mouth bump between them are sanded off the plate within this radius
# (x, z centres); the generated eyes and mouth replace them.
FACE_FEATURES = ((0.072, 0.668), (-0.072, 0.668), (0.0, 0.640))
FEATURE_RADIUS = 0.035
FEATURE_PASSES = 30
BODY_TRIS = 15600               # body share of the ~25k budget (limbs, eyes and mouth take the rest)

# Face plate: classified on the raw print by vertex density (the smooth plate is a few large triangles,
# the sculpted fur is dense), then transferred to the remesh as a soft mask. Only the front of the hood
# can be face plate.
DENSITY_RADIUS = 0.012
DENSITY_PLATE, DENSITY_FUR = 4, 12   # raw vertices within DENSITY_RADIUS
PLATE_MIN_Z = 0.48
PLATE_MASK_BLUR = 3

# ------------------------------------------------------------ the profile ----
# Values Jolly overrides in the shared Nubi modules (see apply_profile). Lengths in metres, character space:
# +X = character's left, -Y = forward, +Z = up.
ARM_SCALE = 0.330 / C.ARM_LENGTH
ARM_RADIUS_SCALE = 1.07
LEG_RADIUS_SCALE = 1.35

PROFILE = {
    C: dict(
        SHOULDER=(0.290, 0.010, 0.475),
        ARM_DIRECTION=(0.15, -0.02, -0.99),
        ARM_LENGTH=0.330,
        ARM_PROFILE=tuple((a * ARM_SCALE, r * ARM_RADIUS_SCALE) for a, r in C.ARM_PROFILE),
        HAND_START=C.HAND_START * ARM_SCALE,
        THUMB_OFFSET=(C.THUMB_OFFSET[0] * ARM_SCALE, C.THUMB_OFFSET[1]),
        LEG_PROFILE=tuple((a, r * LEG_RADIUS_SCALE) for a, r in C.LEG_PROFILE),
        FOOT_RADII=(0.090, 0.115, 0.056),
        FACE_EYE_X=0.072,
        FACE_EYE_Z=0.668,
        EYE_RADIUS_W=0.020,
        EYE_RADIUS_H=0.025,
        EYE_DEPTH=0.012,
        EYE_EMBED=0.40,
        EYE_PIVOT_DEPTH=0.30,
        EYE_SEGMENTS=16,
        EYE_RINGS=8,
        EYE_TILT_DEG=0.0,
        MOUTH_CENTER_Z=0.628,
        MOUTH_COLUMNS=20,
        MOUTH_ROWS=5,
        CHEEK_X=0.112,
        CHEEK_Z=0.636,
        CHEEK_RADIUS=0.030,
        MESH_NAME="Jolly_Mesh",
        # Kept as Nubi's: Animator bindings are "<armature>/Root/...", so with the same name Nubi's controller
        # (through an override controller) and its upper-body mask bind to this rig unchanged.
        ARMATURE_NAME=C.ARMATURE_NAME,
        MATERIAL_SKIN="M_Jolly_Fur",
        MATERIAL_GLOSS="M_Jolly_Gloss",
        TEXTURE_NAME="T_Jolly_Albedo",
        COLOR_EYE=(0.075, 0.068, 0.082),
        COLOR_EYE_LOW=(0.150, 0.130, 0.170),
        COLOR_MOUTH=(0.290, 0.090, 0.130),
        COLOR_TONGUE=(0.910, 0.500, 0.560),
        ROUGHNESS_SKIN=0.85,
    ),
    rig: dict(
        # The hood is the head: it starts where the shoulders end, with a wide ramp so a nod bends the
        # whole upper blob instead of creasing at one line.
        BODY_SPLITS=(("Hips", None), ("Spine", (0.225, 0.060)), ("Chest", (0.300, 0.055)),
                     ("Neck", (0.380, 0.045)), ("Head", (0.465, 0.090))),
        SPINE_HEADS={"Hips": 0.170, "Spine": 0.235, "Chest": 0.310, "Neck": 0.390, "Head": 0.460},
        HEAD_TAIL_Z=0.850,
    ),
    clips: dict(
        REST_ARM_DIR_R=None,  # filled in apply_profile from ARM_DIRECTION
        # Right mitten on the side of the hood next to the face plate (rest space, rides Head).
        THINK_CHEEK_R=(-0.170, -0.150, 0.580),
    ),
    validate: dict(
        ARM_HIDDEN_AXIAL=0.11 * ARM_SCALE,
    ),
    previews: dict(
        TURNAROUND=tuple((f"turntable_{az:03d}", az, 6) for az in range(0, 360, 45)),
        BODY_TARGET=(0.0, 0.0, 0.45),
        BODY_DISTANCE=2.35,
        FACE_TARGET=(0.0, -0.18, 0.650),
        FACE_DISTANCE=0.62,
    ),
}

# Jolly's face is smaller-featured than Nubi's: same shapes, 80 % size, lips a little thinner.
MOUTH_SCALE = 0.80
MOUTH_THICKNESS_SCALE = 0.90

# ------------------------------------------------------------- palette ----
# sRGB 0..1, from public descriptions ("cream-colored", "plush ivory fur", "beady black eyes",
# "soft pink cheeks"). The face-plate tone is not described anywhere; a paler warm skin is a guess.
COLOR_FUR = (0.945, 0.910, 0.845)
COLOR_FUR_TOP = (0.975, 0.955, 0.915)
COLOR_FUR_SHADE = (0.835, 0.790, 0.715)
COLOR_FUR_GROOVE = (0.760, 0.705, 0.625)
COLOR_PLATE = (0.985, 0.925, 0.880)
COLOR_PLATE_RIM = (0.880, 0.800, 0.740)
COLOR_CHEEK = (0.955, 0.600, 0.650)
COLOR_SOLE = (0.800, 0.745, 0.670)
CHEEK_OPACITY = 0.85
FUR_GROOVE_STRENGTH = 0.55
NORMAL_STRENGTH = 2.5
NORMAL_TEXTURE = "T_Jolly_Normal"

PART_SCAN = C.PART_BODY
RENDER_EXPOSURE = -0.8


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="build_jolly")
    p.add_argument("--src", required=True, help="source mesh (.glb/.gltf/.stl); never committed")
    p.add_argument("--out-dir", default=str(DEFAULT_OUT_DIR), help="git-ignored Unity output folder")
    p.add_argument("--render-dir", default=str(DEFAULT_RENDER_DIR), help="git-ignored render folder")
    p.add_argument("--no-render", action="store_true", help="skip preview renders")
    p.add_argument("--clips", default="", help="comma-separated clip sheets to render (default: all)")
    return p.parse_args(argv)


def apply_profile():
    for module, values in PROFILE.items():
        for name, value in values.items():
            if not hasattr(module, name):
                raise AttributeError(f"{module.__name__} has no {name}; the Nubi package changed")
            setattr(module, name, value)
    d = Vector(C.ARM_DIRECTION).normalized()
    clips.REST_ARM_DIR_R = (-d.x, d.y, d.z)
    shapes.MOUTH_BASIS = _scaled_mouth(shapes.MOUTH_BASIS)
    shapes.MOUTH_SHAPES = {k: _scaled_mouth(v) for k, v in shapes.MOUTH_SHAPES.items()}
    validate._max_depth = _max_depth_parity
    texture.PAINTERS.update({C.PART_BODY: color_scan, C.PART_ARM: color_fur, C.PART_THUMB: color_fur,
                             C.PART_LEG: color_fur, C.PART_FOOT: color_foot})


def _inside(bvh, p):
    """Ray parity against the closed body shell."""
    direction = Vector((0.0123, 0.0071, 1.0)).normalized()  # off-axis, so the ray never runs along an edge
    origin, hits = p.copy(), 0
    while True:
        hit, _n, _i, _d = bvh.ray_cast(origin, direction)
        if hit is None:
            return hits % 2 == 1
        hits += 1
        origin = hit + direction * 1e-5


def _max_depth_parity(bvh, points):
    """validate._max_depth calls a point inside when it is behind its nearest face. Next to the scan's
    creases (arm groove edges, the flattened bottom rim) the nearest face can belong to the far side of a
    ridge, which flags points 10-30 cm outside the body; a parity test confirms each candidate."""
    worst = 0.0
    for p in points:
        loc, normal, _i, dist = bvh.find_nearest(p)
        if loc is not None and (p - loc).dot(normal) < 0.0 and dist > worst and _inside(bvh, p):
            worst = dist
    return worst


def _scaled_mouth(shape):
    half_w, lift, upper, lower, thick, roundness = shape
    s = MOUTH_SCALE
    return (half_w * s, lift * s, upper * s, lower * s, thick * MOUTH_THICKNESS_SCALE, roundness)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = C.FPS


# ------------------------------------------------------------ scan body ----

def import_scan(path):
    ext = Path(path).suffix.lower()
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".stl":
        bpy.ops.wm.stl_import(filepath=path)
    else:
        raise ValueError(f"unsupported source format {ext}")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"{path} contains no mesh")
    with bpy.context.temp_override(active_object=meshes[0], selected_objects=list(bpy.data.objects),
                                   selected_editable_objects=list(bpy.data.objects)):
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    for o in [o for o in bpy.data.objects if o.type != "MESH"]:
        bpy.data.objects.remove(o)
    if len(meshes) > 1:
        with bpy.context.temp_override(active_object=meshes[0], selected_editable_objects=meshes):
            bpy.ops.object.join()
    obj = meshes[0]
    with bpy.context.temp_override(active_object=obj, selected_editable_objects=[obj]):
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def normalise_scan(obj):
    """Weld the triangle soup, stand it on the origin (feet at z 0, centred in X/Y) at HEIGHT."""
    mesh = obj.data
    co = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    lo, hi = co.min(axis=0), co.max(axis=0)
    s = HEIGHT / (hi[2] - lo[2])
    co = (co - np.array([(lo[0] + hi[0]) * 0.5, (lo[1] + hi[1]) * 0.5, lo[2]])) * s
    mesh.vertices.foreach_set("co", co.ravel())
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=WELD_DIST * HEIGHT)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def raw_kdtree(obj):
    co = [v.co.copy() for v in obj.data.vertices]
    kd = KDTree(len(co))
    for i, c in enumerate(co):
        kd.insert(c, i)
    kd.balance()
    return kd


def _outline_at(bvh, theta, z):
    d = Vector((cos(theta), sin(theta), 0.0))
    hit, _n, _i, dist = bvh.ray_cast(Vector((0.0, 0.0, z)) + d * 2.0, -d)
    return 2.0 - dist if hit is not None else 0.0


def deflate_arms_and_legs(obj):
    """The print's arms are fused to the torso along their length and its legs are stubs; neither can
    swing. Push the arm bump back onto the torso outline (interpolated across the arm in section angle)
    and flatten the legs into the body bottom; generated limbs replace both."""
    bvh = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())
    th0, th1 = (radians(a) for a in ARM_ZONE_THETA)
    z_lo, z_full, z_hi = ARM_ZONE_Z

    footprint_theta = np.linspace(-pi, pi, 73)
    footprint = np.array([_outline_at(bvh, t, LEG_CUT_Z + 0.02) for t in footprint_theta])
    zs = np.arange(z_lo - 0.02, z_hi + 0.02, 0.005)
    table = {}
    for side in (1, -1):
        # theta is measured from the side's own +-X axis, so the right side mirrors it about Y.
        r0 = [_outline_at(bvh, th0 if side > 0 else pi - th0, z) for z in zs]
        r1 = [_outline_at(bvh, th1 if side > 0 else pi - th1, z) for z in zs]
        table[side] = (np.array(r0), np.array(r1))

    for v in obj.data.vertices:
        x, y, z = v.co
        if z < LEG_CUT_Z:
            # Flatten the stubs and pull them inside the body's footprint: the print's feet reach further
            # out than the belly, and a plain clamp left a thin flange around the bottom.
            r = (x * x + y * y) ** 0.5
            r_max = LEG_TUCK * np.interp(atan2(y, x), footprint_theta, footprint)
            k = min(1.0, r_max / r) if r > 1e-9 else 1.0
            v.co.x, v.co.y, v.co.z = x * k, y * k, LEG_CUT_Z
            continue
        if not (z_lo <= z <= z_hi):
            continue
        side = 1 if x >= 0 else -1
        theta = atan2(y, abs(x))
        if not (th0 < theta < th1):
            continue
        r0 = np.interp(z, zs, table[side][0])
        r1 = np.interp(z, zs, table[side][1])
        t = (theta - th0) / (th1 - th0)
        target = r0 + (r1 - r0) * t + (0.012 - ARM_GROOVE) * sin(pi * t)  # sections bulge between the samples
        r = (x * x + y * y) ** 0.5
        if r <= target:
            continue
        fade = 1.0 if z <= z_full else max(0.0, 1.0 - (z - z_full) / (z_hi - z_full))
        new_r = r - (r - target) * fade
        v.co.x, v.co.y = x * new_r / r, y * new_r / r
    obj.data.update()


def _apply_modifier(obj, mod):
    with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def remesh_and_soften(obj):
    obj.data.remesh_voxel_size = VOXEL_SIZE
    with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
        bpy.ops.object.voxel_remesh()
    mod = obj.modifiers.new("Soften", "LAPLACIANSMOOTH")
    mod.iterations = SMOOTH_ITERATIONS
    mod.lambda_factor = SMOOTH_LAMBDA
    mod.use_volume_preserve = True
    mod.use_normalized = True
    _apply_modifier(obj, mod)


def _near_feature(co):
    return min((Vector((co.x, co.z)) - Vector(c)).length for c in FACE_FEATURES) < FEATURE_RADIUS


def sand_face_features(obj):
    """The print's dot eyes and mouth bump stick out of the plate; smooth them flat under the generated face."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    verts = [v for v in bm.verts if v.co.y < 0 and _near_feature(v.co)]
    for _ in range(FEATURE_PASSES):
        bmesh.ops.smooth_vert(bm, verts=verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def decimate(obj, target_tris):
    obj.data.calc_loop_triangles()
    tris = len(obj.data.loop_triangles)
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = min(1.0, target_tris / tris)
    mod.use_symmetry = True
    mod.symmetry_axis = "X"
    mod.use_collapse_triangulate = True
    _apply_modifier(obj, mod)


def tag_scan(obj, kd):
    """Part ids and the face-plate mask (in nubi_aux.x) for the remeshed body."""
    mesh = obj.data
    mask = np.zeros(len(mesh.vertices))
    for v in mesh.vertices:
        if v.co.z < PLATE_MIN_Z or v.co.y > -0.05 or v.normal.y > -0.2:
            continue
        if _near_feature(v.co):
            mask[v.index] = 1.0
            continue
        n = len(kd.find_range(v.co, DENSITY_RADIUS))
        t = min(max((DENSITY_FUR - n) / (DENSITY_FUR - DENSITY_PLATE), 0.0), 1.0)
        mask[v.index] = t * t * (3.0 - 2.0 * t)
    edges = np.empty(len(mesh.edges) * 2, dtype=np.int64)
    mesh.edges.foreach_get("vertices", edges)
    edges = edges.reshape(-1, 2)
    for _ in range(PLATE_MASK_BLUR):
        acc = np.zeros_like(mask)
        cnt = np.zeros_like(mask)
        np.add.at(acc, edges[:, 0], mask[edges[:, 1]])
        np.add.at(acc, edges[:, 1], mask[edges[:, 0]])
        np.add.at(cnt, edges[:, 0], 1)
        np.add.at(cnt, edges[:, 1], 1)
        mask = 0.5 * mask + 0.5 * acc / np.maximum(cnt, 1)

    aux = mesh.attributes.new(G.AUX_ATTR, "FLOAT_VECTOR", "POINT")
    aux.data.foreach_set("vector", np.stack([mask, np.zeros_like(mask), np.zeros_like(mask)], 1).ravel())
    part = mesh.attributes.new(G.PART_ATTR, "INT", "FACE")
    part.data.foreach_set("value", np.full(len(mesh.polygons), PART_SCAN, dtype=np.int32))
    G.shade_smooth(obj)
    return float((mask > 0.5).mean())


def build_scan_body(src):
    obj = import_scan(src)
    normalise_scan(obj)
    kd = raw_kdtree(obj)
    deflate_arms_and_legs(obj)
    remesh_and_soften(obj)
    sand_face_features(obj)
    decimate(obj, BODY_TRIS)
    plate = tag_scan(obj, kd)
    obj.name = "Body"
    return obj, plate


# --------------------------------------------------------------- parts ----

def build_parts(body):
    bvh = G.body_bvh(body)
    rig.weight_body(body)
    parts = [body]
    landmarks = rig.Landmarks()
    eye_frames = {}
    for side in "LR":
        arm = G.build_arm(side)
        rig.weight_arm(arm, side)
        thumb = G.build_thumb(side)
        rig.weight_rigid(thumb, f"Hand_{side}")
        leg = G.build_leg(side)
        rig.weight_leg(leg, side)
        foot = G.build_foot(side)
        rig.weight_rigid(foot, f"Foot_{side}")
        eye, frame, pivot = G.build_eye(side, bvh)
        rig.weight_rigid(eye, f"Eye_{side}")
        eye_frames[side] = frame
        landmarks.eye_pivots[side] = pivot
        landmarks.eye_centers[side] = frame.translation.copy()
        parts += [arm, thumb, leg, foot, eye]

    mouth = G.build_mouth(shapes.MOUTH_BASIS, bvh)
    rig.weight_mouth(mouth)
    parts.append(mouth)
    # No sprout: the two Sprout bones stay in the rig (drop-in contract) on the crown, without skin.
    landmarks.sprout_base, landmarks.sprout_up = G.sprout_frame(bvh)
    landmarks.sprout_tip = landmarks.sprout_base + landmarks.sprout_up * 0.12

    cheeks = [tuple(G.project_to_body(bvh, x, C.CHEEK_Z)[0]) for x in (C.CHEEK_X, -C.CHEEK_X)]
    return parts, bvh, landmarks, eye_frames, cheeks


def join(parts):
    body = parts[0]
    with bpy.context.temp_override(active_object=body, selected_editable_objects=parts):
        bpy.ops.object.join()
    body.name = C.MESH_NAME
    body.data.name = C.MESH_NAME
    return body


# -------------------------------------------------------------- texture ----

def _rgb(c):
    return np.asarray(c, dtype=np.float32)


def _sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _mix(a, b, t):
    return a * (1.0 - t[:, None]) + b * t[:, None]


# Fur strands: a sum of plane waves whose wave vectors lie almost flat, so the crests run downwards like
# combed fur; evaluated on surface position, so it is seamless across UV islands. Fixed seed: deterministic.
_rng = np.random.default_rng(7)
_angles = _rng.uniform(0.0, pi, 14)
_wavelengths = _rng.uniform(0.007, 0.020, 14)
FUR_WAVES = np.stack([np.cos(_angles), np.sin(_angles), _rng.uniform(-0.25, 0.25, 14)], 1) \
    * (2.0 * pi / _wavelengths)[:, None]
FUR_PHASES = _rng.uniform(0.0, 2.0 * pi, 14)


def fur_height(pos):
    """0 (groove) .. 1 (crest)."""
    h = np.sin(pos @ FUR_WAVES.T + FUR_PHASES).mean(axis=1)
    clump = np.sin(pos @ (FUR_WAVES[:4].T * 0.18) + FUR_PHASES[:4]).mean(axis=1)
    return np.clip(0.5 + 1.6 * h * (0.7 + 0.3 * clump), 0.0, 1.0)


def _fur_base(pos):
    z = pos[:, 2]
    col = _mix(np.broadcast_to(_rgb(COLOR_FUR_SHADE), pos.shape), _rgb(COLOR_FUR), _sstep(0.05, 0.40, z))
    col = _mix(col, _rgb(COLOR_FUR_TOP), _sstep(0.55, HEIGHT, z) * 0.8)
    groove = (1.0 - fur_height(pos)) * FUR_GROOVE_STRENGTH
    return _mix(col, _rgb(COLOR_FUR_GROOVE), groove)


def color_fur(pos, _aux, _cheeks):
    return _fur_base(pos)


def color_foot(pos, aux, _cheeks):
    sole = _sstep(-0.30, -0.45, aux[:, 1])
    return _mix(_fur_base(pos), _rgb(COLOR_SOLE), sole)


def color_scan(pos, aux, cheeks):
    plate = _sstep(0.35, 0.75, aux[:, 0])
    rim = _sstep(0.10, 0.45, aux[:, 0]) * (1.0 - _sstep(0.55, 0.95, aux[:, 0]))
    face = _mix(np.broadcast_to(_rgb(COLOR_PLATE), pos.shape), _rgb(COLOR_PLATE_RIM), rim * 0.6)
    blush = np.zeros(len(pos), dtype=np.float32)
    for c in cheeks:
        d2 = np.sum((pos - np.asarray(c, dtype=np.float32)) ** 2, axis=1)
        blush = np.maximum(blush, np.exp(-d2 / (C.CHEEK_RADIUS ** 2)))
    face = _mix(face, _rgb(COLOR_CHEEK), CHEEK_OPACITY * _sstep(0.10, 0.80, blush))
    return _mix(_fur_base(pos), face, plate)


def fill_unpainted(image):
    """Texels no triangle covers stay black after the shared painter's dilation; decimation slivers can
    sample them (black specks on the fur). Nothing painted is pure black, so those become fur."""
    w, h = image.size
    px = np.empty(w * h * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    px = px.reshape(-1, 4)
    empty = px[:, :3].max(axis=1) <= 0.0
    px[empty, :3] = COLOR_FUR
    image.pixels.foreach_set(px.ravel())
    image.save()


def paint_normal_map(obj, out_path, size=C.TEXTURE_SIZE):
    """Tangent-space fur normal map from the same strand height field (UV-space gradient). Only the
    fur parts get relief; plate, eyes and mouth stay flat."""
    uv, co, aux, part = texture._gather_triangles(obj.data)
    pos_img, aux_img, part_img = texture._rasterise(uv, co, aux, part, size)
    fur = np.isin(part_img, (C.PART_BODY, C.PART_ARM, C.PART_THUMB, C.PART_LEG, C.PART_FOOT))
    h = np.zeros((size, size), dtype=np.float32)
    h[fur] = fur_height(pos_img[fur])
    if fur.any():
        body = fur & (part_img == C.PART_BODY)
        h[body] *= 1.0 - _sstep(0.2, 0.6, aux_img[body][:, 0])
    dx = np.zeros_like(h)
    dy = np.zeros_like(h)
    both_x = fur[:, 2:] & fur[:, :-2]
    both_y = fur[2:, :] & fur[:-2, :]
    dx[:, 1:-1] = np.where(both_x, (h[:, 2:] - h[:, :-2]) * 0.5, 0.0)
    dy[1:-1, :] = np.where(both_y, (h[2:, :] - h[:-2, :]) * 0.5, 0.0)
    n = np.stack([-dx * NORMAL_STRENGTH, -dy * NORMAL_STRENGTH, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    rgb = texture._dilate(n * 0.5 + 0.5, part_img >= 0, C.TEXTURE_DILATE_PX)
    rgb[part_img < 0] = (0.5, 0.5, 1.0)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    img = bpy.data.images.new(NORMAL_TEXTURE, size, size, alpha=False, is_data=True)
    rgba = np.ones((size, size, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(rgb, 0.0, 1.0)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = out_path
    img.file_format = "PNG"
    img.save()
    img.source = "FILE"
    img.filepath = out_path
    img.colorspace_settings.name = "Non-Color"
    img.reload()
    return img


def add_normal_map(material, image):
    nodes, links = material.node_tree.nodes, material.node_tree.links
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.location = (-400, -200)
    nmap = nodes.new("ShaderNodeNormalMap")
    nmap.location = (-150, -200)
    links.new(tex.outputs["Color"], nmap.inputs["Color"])
    links.new(nmap.outputs["Normal"], nodes.get("Principled BSDF").inputs["Normal"])


# ---------------------------------------------------------------- main ----

def rename_renders(render_dir):
    for name in os.listdir(render_dir):
        if name.startswith("nubi_"):
            os.replace(os.path.join(render_dir, name), os.path.join(render_dir, "jolly_" + name[len("nubi_"):]))


def main():
    args = parse_args()
    out_dir = Path(args.out_dir)
    render_dir = Path(args.render_dir)
    fbx_path = out_dir / "Jolly.fbx"
    presets_path = out_dir / "Jolly_FacePresets.json"
    albedo_path = out_dir / "Textures" / "T_Jolly_Albedo.png"
    normal_path = out_dir / "Textures" / f"{NORMAL_TEXTURE}.png"
    report_path = out_dir / "build_report.json"
    blend_path = render_dir / "Jolly.blend"

    apply_profile()
    reset_scene()

    body, plate_share = build_scan_body(args.src)
    parts, _bvh, landmarks, eye_frames, cheeks = build_parts(body)
    mesh = join(parts)
    shapes.add_shape_keys(mesh, _bvh, eye_frames)

    texture.unwrap(mesh)
    image, coverage = texture.paint_texture(mesh, cheeks, str(albedo_path))
    fill_unpainted(image)
    normal = paint_normal_map(mesh, str(normal_path))
    texture.assign_materials(mesh, image)
    add_normal_map(mesh.data.materials[0], normal)

    arm = rig.build_armature(landmarks)
    rig.bind(mesh, arm)
    animation.bake_all(arm)

    report = validate.static_report(mesh, arm)
    report["source"] = Path(args.src).name
    report["face_plate_vertex_share"] = round(plate_share, 3)
    report["uv_coverage"] = round(coverage, 3)
    report["blink_closed_ratio"] = validate.blink_report(mesh)
    report["penetration"] = validate.penetration_report(mesh, arm)
    report["locomotion"] = validate.locomotion_report(arm)
    report["outputs"] = {"fbx": fbx_path.name, "face_presets": presets_path.name,
                         "albedo": f"Textures/{albedo_path.name}", "normal": f"Textures/{normal_path.name}"}

    bpy.context.scene.frame_set(0)
    export.export_fbx(str(fbx_path), mesh, arm)
    faces.write_json(str(presets_path), report["shape_keys"], CLIPS)
    export.save_blend(str(blend_path))

    if not args.no_render:
        cam = R.setup_stage()
        bpy.context.scene.view_settings.exposure = RENDER_EXPOSURE  # the stage is lit for Nubi's coral
        previews.turnaround(str(render_dir), cam, arm, mesh)
        previews.face_sheet(str(render_dir), cam, arm, mesh)
        only = [c for c in args.clips.split(",") if c] or None
        previews.clip_sheets(str(render_dir), cam, arm, mesh, only)
        rename_renders(str(render_dir))

    report_path.write_text(json.dumps(report, indent=2) + "\n")
    print("JOLLY_REPORT " + json.dumps(report))


if __name__ == "__main__":
    main()
