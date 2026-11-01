"""Armature and skin weights.

Weights are computed analytically per part instead of with bone-heat: every part is its own
closed shell, so each shell only ever blends between the bones of its own chain (an arm can never
pick up Chest/Head influence, a foot can never pick up the other leg). That gives deterministic,
smooth weights with at most 2-3 influences per vertex and zero unweighted vertices by construction.
"""

from dataclasses import dataclass, field

import bpy
from mathutils import Vector

from . import config as C
from .geometry import AUX_ATTR, arm_frame, mirror_x

# Body chain split heights (z, blend half-width). The body is one blob with no real neck, so the
# head ramp is deliberately wide (z 0.33..0.50): a narrow ramp shears into a visible fold line when
# the head tilts. The mouth layer copies these weights at its own (basis) height, so it rides the
# skin; the widest viseme dips to z ~0.478, where Head weight is still ~0.95.
BODY_SPLITS = (
    ("Hips", None),
    ("Spine", (0.220, 0.060)),
    ("Chest", (0.290, 0.055)),
    ("Neck", (0.345, 0.040)),
    ("Head", (0.415, 0.085)),
)
ELBOW_BLEND = 0.030
WRIST_BLEND = 0.022
KNEE_BLEND = 0.020
ANKLE_BLEND = 0.018
SPROUT_SPLIT = 0.055
SPROUT_BLEND = 0.020

SPINE_HEADS = {"Hips": 0.170, "Spine": 0.230, "Chest": 0.300, "Neck": 0.360, "Head": 0.420}
HEAD_TAIL_Z = 0.840


@dataclass
class Landmarks:
    eye_pivots: dict = field(default_factory=dict)   # side -> Vector
    eye_centers: dict = field(default_factory=dict)  # side -> Vector
    sprout_base: Vector = None
    sprout_up: Vector = None
    sprout_tip: Vector = None


def smoothstep(edge0, edge1, x):
    if edge0 == edge1:
        return 0.0 if x < edge0 else 1.0
    t = min(max((x - edge0) / (edge1 - edge0), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def chain_weights(value, splits):
    """Partition of unity along a 1D coordinate. splits = ((bone, None|(at, half_width)), ...)."""
    ramps = [1.0]
    for _bone, split in splits[1:]:
        at, half = split
        ramps.append(smoothstep(at - half, at + half, value))
    ramps.append(0.0)
    out = {}
    for i, (bone, _s) in enumerate(splits):
        w = ramps[i] - ramps[i + 1]
        if w > 1e-4:
            out[bone] = w
    return out


def arm_points(side):
    frame = arm_frame(side)
    origin = frame.translation
    axis = frame.col[1].xyz
    return {
        "shoulder": origin,
        "elbow": origin + axis * (C.ARM_LENGTH * C.ARM_ELBOW_T),
        "wrist": origin + axis * (C.ARM_LENGTH * C.ARM_WRIST_T),
        "tip": origin + axis * C.ARM_LENGTH,
    }


def leg_points(side):
    hip = Vector(C.HIP)
    toe = Vector((C.FOOT_CENTER[0], C.FOOT_CENTER[1] - C.FOOT_RADII[1] * 0.8, C.FOOT_CENTER[2]))
    pts = {
        "hip": hip,
        "knee": Vector((hip.x, hip.y, C.KNEE_Z)),
        "ankle": Vector((hip.x, hip.y, C.ANKLE_Z)),
        "toe": toe,
    }
    if side == "R":
        pts = {k: mirror_x(v) for k, v in pts.items()}
    return pts


def build_armature(landmarks):
    arm_data = bpy.data.armatures.new(C.ARMATURE_NAME)
    arm_data.display_type = "STICK"
    obj = bpy.data.objects.new(C.ARMATURE_NAME, arm_data)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    front = Vector((0, -1, 0))
    up = Vector((0, 0, 1))

    def bone(name, head, tail, parent=None, roll_axis=front, connect=False):
        b = eb.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        b.align_roll(roll_axis)
        if parent:
            b.parent = eb[parent]
            b.use_connect = connect
        return b

    bone("Root", (0, 0, 0), (0, 0, 0.08))
    z = SPINE_HEADS
    bone("Hips", (0, 0, z["Hips"]), (0, 0, z["Spine"]), "Root")
    bone("Spine", (0, 0, z["Spine"]), (0, 0, z["Chest"]), "Hips", connect=True)
    bone("Chest", (0, 0, z["Chest"]), (0, 0, z["Neck"]), "Spine", connect=True)
    bone("Neck", (0, 0, z["Neck"]), (0, 0, z["Head"]), "Chest", connect=True)
    bone("Head", (0, 0, z["Head"]), (0, 0, HEAD_TAIL_Z), "Neck", connect=True)

    sprout_mid = landmarks.sprout_base + landmarks.sprout_up * SPROUT_SPLIT
    bone("Sprout_1", landmarks.sprout_base, sprout_mid, "Head")
    bone("Sprout_2", sprout_mid, landmarks.sprout_tip + landmarks.sprout_up * 0.05, "Sprout_1", connect=True)

    for side in "LR":
        pivot, center = landmarks.eye_pivots[side], landmarks.eye_centers[side]
        bone(f"Eye_{side}", pivot, center, "Head", roll_axis=up)

        a = arm_points(side)
        bone(f"UpperArm_{side}", a["shoulder"], a["elbow"], "Chest")
        bone(f"LowerArm_{side}", a["elbow"], a["wrist"], f"UpperArm_{side}", connect=True)
        bone(f"Hand_{side}", a["wrist"], a["tip"], f"LowerArm_{side}", connect=True)

        g = leg_points(side)
        bone(f"UpperLeg_{side}", g["hip"], g["knee"], "Hips")
        bone(f"LowerLeg_{side}", g["knee"], g["ankle"], f"UpperLeg_{side}", connect=True)
        bone(f"Foot_{side}", g["ankle"], g["toe"], f"LowerLeg_{side}", connect=True, roll_axis=up)

    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


# ------------------------------------------------------------- weights ----

def _aux(obj):
    attr = obj.data.attributes[AUX_ATTR]
    return [d.vector for d in attr.data]


def _write(obj, per_vertex):
    groups = {}
    for vi, weights in enumerate(per_vertex):
        for bone, w in weights.items():
            if bone not in groups:
                groups[bone] = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            groups[bone].add([vi], w, "REPLACE")


def weight_body(obj):
    _write(obj, [chain_weights(v.co.z, BODY_SPLITS) for v in obj.data.vertices])


def weight_mouth(obj):
    _write(obj, [chain_weights(v.co.z, BODY_SPLITS) for v in obj.data.vertices])


def weight_rigid(obj, bone):
    _write(obj, [{bone: 1.0} for _v in obj.data.vertices])


def weight_arm(obj, side):
    elbow = C.ARM_LENGTH * C.ARM_ELBOW_T
    wrist = C.ARM_LENGTH * C.ARM_WRIST_T
    splits = ((f"UpperArm_{side}", None),
              (f"LowerArm_{side}", (elbow, ELBOW_BLEND)),
              (f"Hand_{side}", (wrist, WRIST_BLEND)))
    _write(obj, [chain_weights(a[0], splits) for a in _aux(obj)])


def weight_leg(obj, side):
    hip_z = C.HIP[2]
    splits = ((f"UpperLeg_{side}", None),
              (f"LowerLeg_{side}", (hip_z - C.KNEE_Z, KNEE_BLEND)),
              (f"Foot_{side}", (hip_z - C.ANKLE_Z, ANKLE_BLEND)))
    _write(obj, [chain_weights(a[0], splits) for a in _aux(obj)])


def weight_stem(obj):
    splits = (("Sprout_1", None), ("Sprout_2", (SPROUT_SPLIT, SPROUT_BLEND)))
    _write(obj, [chain_weights(a[0], splits) for a in _aux(obj)])


def bind(mesh_obj, arm_obj):
    mesh_obj.parent = arm_obj
    mod = mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm_obj
    mod.use_vertex_groups = True
    mod.use_deform_preserve_volume = False
