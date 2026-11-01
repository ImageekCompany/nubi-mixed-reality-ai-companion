"""Preview set written to docs/art/nubi: turnaround, face/viseme sheet, per-clip contact sheets."""

import os

import bpy

from . import render as R
from .animation import solo, unsolo
from .clips import CLIPS
from .faces import FACE_PRESETS
from .posing import euler_q

TURNAROUND = (("front", 0, 6), ("three_quarter", 35, 10), ("side", 90, 4), ("back", 180, 6))
BODY_TARGET = (0.0, 0.0, 0.50)
BODY_DISTANCE = 2.45
FACE_TARGET = (0.0, -0.20, 0.585)
FACE_DISTANCE = 0.85
FACE_KEYS = ("Basis", "sil", "A", "E", "I", "O", "U", "Smile", "Frown", "Blink_L", "Blink_R", "Happy_Eyes",
             "Wide_Eyes", "Sad_Eyes")
# Eye-bone look-at checks at the edge of the safe range (character-space euler degrees). Beyond about
# yaw 16 / pitch 8 combined, the outer eye starts to sink into the side of the head.
EYE_LOOKS = (("look left", (0.0, 0.0, 16.0)), ("look up-right", (-8.0, 0.0, -10.0)))
SHEET_FRAMES = 6
SHEET_COLUMNS = 3
FACE_COLUMNS = 8
ANIM_AZIMUTH = 30
# Right-arm clips are shown from Nubi's right; the gaits from the side so the stride reads.
ANIM_AZIMUTH_OVERRIDE = {"Wave": -30, "Point": -80, "Think": -15, "Walk": 65, "Hop": 65}
ANIM_ELEVATION = 8


def _reset_keys(mesh_obj):
    for kb in mesh_obj.data.shape_keys.key_blocks[1:]:
        kb.value = 0.0


def turnaround(out_dir, cam, arm_obj, mesh_obj):
    start = solo(arm_obj, "Idle")
    bpy.context.scene.frame_set(start)
    _reset_keys(mesh_obj)
    paths = []
    for name, az, el in TURNAROUND:
        R.aim_camera(cam, BODY_TARGET, az, el, BODY_DISTANCE, 50)
        path = os.path.join(out_dir, f"nubi_{name}.png")
        R.render_still(path)
        paths.append(path)
    unsolo(arm_obj)
    return paths


def _pose_eyes(arm_obj, deg):
    for side in "LR":
        pb = arm_obj.pose.bones[f"Eye_{side}"]
        rest = pb.bone.matrix_local.to_quaternion()
        pb.rotation_quaternion = rest.inverted() @ euler_q(deg) @ rest


def face_sheet(out_dir, cam, arm_obj, mesh_obj):
    solo(arm_obj, None)  # all tracks muted: the face is shown in rest pose, eyes posed by hand
    for pb in arm_obj.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.context.scene.frame_set(0)
    R.aim_camera(cam, FACE_TARGET, 8, 4, FACE_DISTANCE, 60)
    cells = []
    for key in FACE_KEYS:
        _reset_keys(mesh_obj)
        if key != "Basis":
            mesh_obj.data.shape_keys.key_blocks[key].value = 1.0
        label = R.label_object(cam, "neutral" if key == "Basis" else key)
        path = os.path.join(out_dir, f"_face_{key}.png")
        R.render_still(path, R.SHEET_CELL)
        R.remove_object(label)
        cells.append(path)
    _reset_keys(mesh_obj)
    for name, deg in EYE_LOOKS:
        _pose_eyes(arm_obj, deg)
        label = R.label_object(cam, name)
        path = os.path.join(out_dir, f"_face_{name.replace(' ', '_')}.png")
        R.render_still(path, R.SHEET_CELL)
        R.remove_object(label)
        cells.append(path)
    _pose_eyes(arm_obj, (0.0, 0.0, 0.0))
    unsolo(arm_obj)
    out = os.path.join(out_dir, "nubi_face_shapes.png")
    R.compose_sheet(cells, FACE_COLUMNS, out)
    return out


def clip_sheets(out_dir, cam, arm_obj, mesh_obj, only=None):
    scene = bpy.context.scene
    _reset_keys(mesh_obj)
    outputs = []
    for clip in CLIPS:
        if only and clip.name not in only:
            continue
        start = solo(arm_obj, clip.name)
        _reset_keys(mesh_obj)
        for key, weight in FACE_PRESETS.get(clip.name, {}).items():
            mesh_obj.data.shape_keys.key_blocks[key].value = weight
        R.aim_camera(cam, BODY_TARGET, ANIM_AZIMUTH_OVERRIDE.get(clip.name, ANIM_AZIMUTH),
                     ANIM_ELEVATION, BODY_DISTANCE, 50)
        cells = []
        last = clip.length - (1 if clip.loop else 0)
        for i in range(SHEET_FRAMES):
            f = round(i * last / (SHEET_FRAMES - 1))
            scene.frame_set(start + f)
            label = R.label_object(cam, f"{clip.name}  f{f}")
            path = os.path.join(out_dir, f"_{clip.name}_{f}.png")
            R.render_still(path, R.SHEET_CELL)
            R.remove_object(label)
            cells.append(path)
        out = os.path.join(out_dir, f"nubi_clip_{clip.name.lower()}.png")
        R.compose_sheet(cells, SHEET_COLUMNS, out)
        outputs.append(out)
    _reset_keys(mesh_obj)
    unsolo(arm_obj)
    return outputs
