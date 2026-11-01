"""FBX (Unity) and .blend outputs."""

import os
from contextlib import contextmanager

import bpy
from io_scene_fbx import fbx_utils

SHAPE_KEY_FBX_PROPS = fbx_utils.AnimationCurveNodeWrapper.kinds["SHAPE_KEY"][2]


@contextmanager
def _without_shape_key_curves():
    """The exporter keys every shape key in every NLA take (force_keep=True, even when static).
    In Unity those constant-0 blendshape curves would make the Animator overwrite lip-sync and blink
    weights every frame, so for this export only, shape-key curve nodes are dropped from the takes.
    Shape keys themselves (the blendshape geometry) are unaffected."""
    original = fbx_utils.AnimationCurveNodeWrapper.get_final_data

    def filtered(self, scene, ref_id, force_keep=False):
        if self.fbx_props and self.fbx_props[0] == SHAPE_KEY_FBX_PROPS:
            return
        yield from original(self, scene, ref_id, force_keep)

    fbx_utils.AnimationCurveNodeWrapper.get_final_data = filtered
    try:
        yield
    finally:
        fbx_utils.AnimationCurveNodeWrapper.get_final_data = original


def export_fbx(path, mesh_obj, arm_obj):
    """Unity-oriented FBX: metres at scale 1 (UnitScaleFactor 100 = 1 m), Y-up, Nubi faces Unity +Z,
    no leaf bones, one take per NLA strip (take name == strip name), shape keys included.
    bake_space_transform bakes the mesh, but the exporter still writes the armature node with a -90 X
    rotation (compensated on Root); Unity's "Bake Axis Conversion" import option removes it.

    The armature modifier is the only modifier, and mesh modifiers are NOT applied: applying modifiers
    makes the FBX exporter drop shape keys."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    with _without_shape_key_curves():
        _export(path)


def _export(path):
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=False,
        mesh_smooth_type="OFF",
        use_tspace=False,
        use_custom_props=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="RELATIVE",
        embed_textures=False,
    )


def save_blend(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0  # no Nubi.blend1 backups next to the output
    bpy.ops.wm.save_as_mainfile(filepath=path, check_existing=False, copy=True)
