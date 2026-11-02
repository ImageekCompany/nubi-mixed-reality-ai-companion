"""Builds the Nubi companion character from code and exports it for Unity.

Usage (from the repo root):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
        --python tools/blender/build_nubi.py -- [--no-render] [--clips Walk,Wave]

Outputs:
    JollyXR/Assets/Jolly/Art/Character/Nubi.fbx
    JollyXR/Assets/Jolly/Art/Character/Nubi_FacePresets.json
    JollyXR/Assets/Jolly/Art/Character/Textures/T_Nubi_Albedo.png
    tools/blender/out/Nubi.blend
    docs/art/nubi/*.png, docs/art/nubi/build_report.json
"""

import argparse
import json
import os
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))

from nubi import config as C  # noqa: E402
from nubi import geometry as G  # noqa: E402
from nubi import faces, rig, shapes, texture, animation, validate, export, previews  # noqa: E402
from nubi import render as R  # noqa: E402
from nubi.clips import CLIPS  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
UNITY_CHARACTER_DIR = REPO / "JollyXR" / "Assets" / "Jolly" / "Art" / "Character"
FBX_PATH = UNITY_CHARACTER_DIR / "Nubi.fbx"
FACE_PRESETS_PATH = UNITY_CHARACTER_DIR / "Nubi_FacePresets.json"
TEXTURE_PATH = UNITY_CHARACTER_DIR / "Textures" / f"{C.TEXTURE_NAME}.png"
BLEND_PATH = REPO / "tools" / "blender" / "out" / "Nubi.blend"
PREVIEW_DIR = REPO / "docs" / "art" / "nubi"
REPORT_PATH = PREVIEW_DIR / "build_report.json"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="build_nubi")
    p.add_argument("--no-render", action="store_true", help="skip preview renders")
    p.add_argument("--clips", default="", help="comma-separated clip sheets to render (default: all)")
    return p.parse_args(argv)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = C.FPS


def build_parts():
    body = G.build_body()
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
    stem, tip = G.build_stem(bvh)
    rig.weight_stem(stem)
    landmarks.sprout_base, landmarks.sprout_up = G.sprout_frame(bvh)
    landmarks.sprout_tip = tip
    leaves = [G.build_leaf(side, tip) for side in "LR"]
    for leaf in leaves:
        rig.weight_rigid(leaf, "Sprout_2")
    parts += [mouth, stem, *leaves]

    cheeks = [tuple(G.project_to_body(bvh, x, C.CHEEK_Z)[0]) for x in (C.CHEEK_X, -C.CHEEK_X)]
    return parts, bvh, landmarks, eye_frames, cheeks


def join(parts):
    body = parts[0]
    with bpy.context.temp_override(active_object=body, selected_editable_objects=parts):
        bpy.ops.object.join()
    body.name = C.MESH_NAME
    body.data.name = C.MESH_NAME
    return body


def main():
    args = parse_args()
    reset_scene()

    parts, bvh, landmarks, eye_frames, cheeks = build_parts()
    mesh = join(parts)
    shapes.add_shape_keys(mesh, bvh, eye_frames)

    texture.unwrap(mesh)
    image, coverage = texture.paint_texture(mesh, cheeks, str(TEXTURE_PATH))
    texture.assign_materials(mesh, image)

    arm = rig.build_armature(landmarks)
    rig.bind(mesh, arm)
    animation.bake_all(arm)

    report = validate.static_report(mesh, arm)
    report["uv_coverage"] = round(coverage, 3)
    report["blink_closed_ratio"] = validate.blink_report(mesh)
    report["penetration"] = validate.penetration_report(mesh, arm)
    report["locomotion"] = validate.locomotion_report(arm)
    report["contacts"] = validate.contact_report(arm)
    report["outputs"] = {"fbx": str(FBX_PATH.relative_to(REPO)),
                         "face_presets": str(FACE_PRESETS_PATH.relative_to(REPO)), "texture": str(TEXTURE_PATH.relative_to(REPO)),
                         "blend": str(BLEND_PATH.relative_to(REPO))}

    bpy.context.scene.frame_set(0)
    export.export_fbx(str(FBX_PATH), mesh, arm)
    faces.write_json(str(FACE_PRESETS_PATH), report["shape_keys"], CLIPS)
    export.save_blend(str(BLEND_PATH))

    if not args.no_render:
        cam = R.setup_stage()
        out = str(PREVIEW_DIR)
        previews.turnaround(out, cam, arm, mesh)
        previews.face_sheet(out, cam, arm, mesh)
        only = [c for c in args.clips.split(",") if c] or None
        previews.clip_sheets(out, cam, arm, mesh, only)

    os.makedirs(PREVIEW_DIR, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2) + "\n")
    print("NUBI_REPORT " + json.dumps(report))


if __name__ == "__main__":
    main()
