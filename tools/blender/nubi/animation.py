"""Bakes clip functions into Blender actions (one key per frame) and stacks them as NLA strips.

Every bone is keyed in every clip, so a clip never inherits a pose from whatever played before it
(important both for the FBX NLA bake and for Unity Animator transitions). The FBX exporter names
each take after its NLA strip, which is why strip names equal the clip names exactly.
"""

import bpy
from bpy_extras import anim_utils

from . import config as C
from .clips import CLIPS
from .posing import RigModel

STRIP_GAP = 10


def _bone_path(bone, channel):
    return f'pose.bones["{bone}"].{channel}'


def bake_clip(arm_obj, rig, clip):
    action = bpy.data.actions.new(clip.name)
    action.use_fake_user = True
    slot = action.slots.new(id_type="OBJECT", name=arm_obj.name)
    channelbag = anim_utils.action_ensure_channelbag_for_slot(action, slot)

    frames = list(range(clip.length + 1))
    samples = []
    prev = {}
    for f in frames:
        # Loops: the last frame re-samples frame 0 so the seam is exact.
        pose = rig.evaluate(clip.pose(0.0 if (clip.loop and f == clip.length) else float(f)))
        for bone, (q, t, sc) in pose.items():
            if bone in prev and prev[bone].dot(q) < 0.0:
                q = -q  # keep quaternion hemisphere continuous for per-channel interpolation
            prev[bone] = q
            pose[bone] = (q, t, sc)
        samples.append(pose)

    for bone in rig.names:
        for channel, size in (("rotation_quaternion", 4), ("location", 3), ("scale", 3)):
            for i in range(size):
                fc = channelbag.fcurves.new(_bone_path(bone, channel), index=i, group_name=bone)
                fc.keyframe_points.add(len(frames))
                co = []
                for f, pose in zip(frames, samples):
                    q, t, sc = pose[bone]
                    value = {"rotation_quaternion": q, "location": t, "scale": sc}[channel][i]
                    co += [float(f), value]
                fc.keyframe_points.foreach_set("co", co)
                for kp in fc.keyframe_points:
                    kp.interpolation = "LINEAR"
                fc.update()
    return action, slot


def bake_all(arm_obj):
    for pb in arm_obj.pose.bones:
        pb.rotation_mode = "QUATERNION"
    rig = RigModel(arm_obj)
    ad = arm_obj.animation_data or arm_obj.animation_data_create()
    start = 0
    baked = []
    for clip in CLIPS:
        action, slot = bake_clip(arm_obj, rig, clip)
        track = ad.nla_tracks.new()
        track.name = clip.name
        strip = track.strips.new(clip.name, start, action)
        strip.action_slot = slot
        strip.name = clip.name
        baked.append((clip, action, strip))
        start += clip.length + STRIP_GAP
    ad.action = None
    scene = bpy.context.scene
    scene.render.fps = C.FPS
    scene.frame_start = 0
    scene.frame_end = start
    return baked


def solo(arm_obj, clip_name):
    """Make one strip audible (for previews/validation); returns its start frame.
    clip_name=None mutes every track so the pose can be set by hand."""
    start = None
    for track in arm_obj.animation_data.nla_tracks:
        track.mute = track.name != clip_name
        if track.name == clip_name:
            start = int(track.strips[0].frame_start)
    return start


def unsolo(arm_obj):
    for track in arm_obj.animation_data.nla_tracks:
        track.mute = False
