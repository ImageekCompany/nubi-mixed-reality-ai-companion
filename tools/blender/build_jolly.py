"""Builds a LOCAL-ONLY "Jolly-like" demo skin from a scan mesh the user supplies, as a drop-in for Nubi.

IP constraint: the source mesh and everything derived from it (FBX, textures, .blend, renders, report)
are third-party character IP. They are written only to git-ignored folders and are never committed or
published. This script holds no mesh, texture or image data; the body comes from --src at build time, and
the few landmark constants below (cut heights, eye, shoulder and paw positions) were measured on that print.

Usage (from the repo root):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
        --python tools/blender/build_jolly.py -- --src <mesh.glb|mesh.stl> [--no-render] [--clips Walk,Wave]

Outputs (all git-ignored):
    JollyXR/Assets/Jolly/Local/Characters/Jolly/Jolly.fbx, Jolly_FacePresets.json, build_report.json
    JollyXR/Assets/Jolly/Local/Characters/Jolly/Textures/T_Jolly_{Albedo,Normal,Fur,FurFlow,FurNoise}.png
    tools/blender/out/jolly/Jolly.blend (with the shell-fur preview rig) and v3/*.png renders

Drop-in contract: the same 22 bones, 13 shape keys and 15 clips as Nubi (docs/art/nubi/build_report.json).
The Nubi package is reused as is. Its modules read their design constants from module globals at call
time, so `apply_profile` swaps in Jolly's values (and Jolly's clip functions) before anything is built;
the Nubi build never runs in the same process, so nothing leaks back.

What comes from the scan and what is generated:
  scan     body, hood and face plate (the print's fused arms deflated, stub legs cut off, voxel remeshed,
           fur relief softened, decimated)
  generated arms (unioned into the body by the remesh, so they grow out of the shoulder with one surface),
           legs and feet (separate shells), eyes, mouth, UVs, the albedo and the shell-fur maps, rig,
           weights, shape keys and clips
The fur itself is not geometry: Unity renders it with a shell shader from the fur maps (docs/art/jolly-demo.md).
The Blender preview stacks the same kind of shells so the renders show what that shader will draw.
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
from nubi.clips import (CLIPS, HOP_CONTACT, HOP_CROUCH, HOP_HEIGHT, HOP_LENGTH, HOP_STRIDE, TAU,  # noqa: E402
                        WALK_BOB, WALK_CROUCH, WALK_LENGTH, WALK_LIFT, WALK_STANCE, WALK_STRIDE, ZERO,
                        _ease, _foot_phase, add, arms, scale, squash, track, wave)
from nubi.posing import PoseSpec  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
DEFAULT_OUT_DIR = REPO / "JollyXR" / "Assets" / "Jolly" / "Local" / "Characters" / "Jolly"
DEFAULT_RENDER_DIR = REPO / "tools" / "blender" / "out" / "jolly"

# ------------------------------------------------------------- scan fit ----
# Fractions and lengths below were measured on the "v2 Whole" print scaled to HEIGHT (cross-sections and
# front ray casts); they describe where to cut, not what the character looks like.
HEIGHT = 0.86                  # metres; Nubi's body height without the sprout, so the gait clips fit
WELD_DIST = 1e-6               # fraction of height
# The print is squatter than the reference (a wide pot belly, arms splayed out to 0.89 of the height against
# 0.66): it is narrowed and made shallower about the centre axis, so it reads as the reference's tall bean.
# Every x/y landmark below is a print measurement times this scale.
SCAN_SCALE_X, SCAN_SCALE_Y = 0.90, 0.94
SX, SY = SCAN_SCALE_X, SCAN_SCALE_Y
# The print's stub legs are cut off at LEG_CUT_Z, just above where they merge into the coat hem, and the
# cut is capped. (Flattening them into that plane instead left thin fins around the bottom after the
# remesh, whose folded UVs smeared dark occlusion onto the paws.)
LEG_CUT_Z = 0.125
# Its fused arms are deflated between these heights, fading out towards the top so part of the print's
# shoulder stays as a pad the generated arm grows out of.
ARM_ZONE_Z = (0.110, 0.400, 0.520)
ARM_ZONE_THETA = (-40.0, 50.0)      # section angles (deg, 0 = +X, -90 = front) bracketing the arm bump
ARM_GROOVE = 0.022              # the deflated side is pressed in a little more, so the hanging arm clears it
VOXEL_SIZE = 0.007
SMOOTH_ITERATIONS = 10          # Laplacian passes: sculpted fur grooves -> soft plush surface (with fewer,
                                # the grooves' pits pinch the fur shells into pale specks)
SMOOTH_LAMBDA = 0.8
# The print's dot eyes and the tiny mouth bump between them are sanded off the plate within this radius
# (x, z centres); the generated eyes and mouth replace them.
FACE_FEATURES = ((0.072 * SX, 0.668), (-0.072 * SX, 0.668), (0.0, 0.640))
FEATURE_RADIUS = 0.035
FEATURE_PASSES = 30
# Edge collapse folds a few triangles over their neighbours (on the face plate, the hood's sides, the
# armpits); the shells then push those vertices sideways into dark notches and pale specks. Folded faces
# are found by neighbour normals and their vertices relaxed until none are left.
FOLD_DOT = -0.3                 # adjacent face normals below this dot are a fold
FOLD_PASSES = 12
# The print's sculpted fur grooves survive the smoothing as ~2 mm pits (the body is otherwise convex to
# 0.2 mm). Shells converge over a pit into a pale star and the root shows through as a dark dot, so pits
# deeper than PIT_DEPTH are relaxed. Not on the face plate's inset rim or in the armpits, which are creases
# by design.
PIT_DEPTH = 0.0004
# The rim band kept as a crease, in the plate superellipse's radius (1 = plate edge). A whole-hood-front
# exclusion left the grooves above the plate and on the upper chest as dark dots.
PLATE_RIM_RP = (0.90, 1.10)
PIT_PASSES = 10
BODY_TRIS = 20500               # body + arms share of the 30k budget (legs, feet, eyes, mouth take ~5.5k)
PROXY_VOXEL = 0.010             # the arm-less torso used only by the penetration check
PROXY_TRIS = 8000

# Face plate: classified on the raw print by vertex density (the smooth plate is a few large triangles,
# the sculpted fur is dense), then transferred to the remesh as a soft mask. Only the front of the hood
# can be face plate.
DENSITY_RADIUS = 0.012
DENSITY_PLATE, DENSITY_FUR = 4, 12   # raw vertices within DENSITY_RADIUS
PLATE_MIN_Z = 0.48
PLATE_MASK_BLUR = 3
# The density mask is ragged where decimated fur meets the plate, so the plate is redrawn as a superellipse
# fitted to the mask's core (percentiles, robust to the ragged edge) on the front of the hood.
PLATE_POWER = 2.6
PLATE_SOFT = 0.06
PLATE_GROW = 1.03
PLATE_MAX_HALF_WIDTH = 0.20 * SX

# ----------------------------------------------------------------- arms ----
# Traced on the print: its arm is a paddle (about 19 cm front to back and 12 cm across at the paw) that
# hangs from the hood's base to hip height, paw centre at PAW_CENTER. The pivot sits inside the shoulder.
# The paw is brought in further than the width scale alone (0.279): the reference's arms hang close along
# the body. At the print's 19 degrees out they read as detached sticks; at 16 they hang close, and further in
# Listen cuts into the body.
SHOULDER = (0.215 * SX, -0.025 * SY, 0.478)
PAW_CENTER = (0.268, -0.035 * SY, 0.228)
ARM_LENGTH = 0.337
# (axial distance from the shoulder pivot, front-to-back radius); the first/last radius 0 are poles. The
# cap above the pivot is buried in the body, so the remesh fuses only the shoulder.
ARM_PROFILE = (
    (-0.065, 0.000), (-0.058, 0.040), (-0.040, 0.066), (-0.010, 0.078), (0.030, 0.076), (0.080, 0.068),
    (0.130, 0.064), (0.180, 0.068), (0.225, 0.080), (0.262, 0.086), (0.295, 0.076), (0.320, 0.052),
    (0.333, 0.022), (0.337, 0.000),
)
ARM_FLATTEN = 0.72             # side-to-side thickness / front-to-back width
ARM_SEGMENTS = 20
HAND_START = 0.205             # where the paw begins (shorter fur from here)
# Below the armpit the torso is carved back to this gap from the arm, so the arm can swing or rise
# without a web of skin; the shell fur fills the gap at rest. No carve in the shoulder, where the union is.
ARM_CLEARANCE = 0.020
ARM_CARVE_AXIAL = (0.040, 0.100)
# The remesh leaves a crease where the arm meets the body; this many smoothing passes round it into a fillet.
# Only above JUNCTION_MAX_AXIAL: further down, arm and torso are both within reach across the clearance gap.
JUNCTION_DISTANCE = 0.016
JUNCTION_MAX_AXIAL = 0.070
JUNCTION_PASSES = 18
# Arm membership blends over this band of (distance to body - distance to arm), and fades into the chest
# above the pivot so the shoulder pad stretches instead of creasing when the arm rises.
ARM_MEMBERSHIP_BAND = 0.006
ARM_SHOULDER_RAMP = (-0.035, 0.045)
ARM_ELBOW_BLEND = 0.050        # wider than Nubi's: the arm is twice as thick
ARM_WRIST_BLEND = 0.040
ARMPIT_BLEND_AXIAL = (-0.040, 0.130)   # where membership is diffused under the shoulder (see ArmTags)
ARMPIT_BLEND_PASSES = 8
ARM_INNER_RAMP_SHIFT = 0.050

# ------------------------------------------------------------ the profile ----
# Values Jolly overrides in the shared Nubi modules (see apply_profile). Lengths in metres, character space:
# +X = character's left, -Y = forward, +Z = up.
# The reference's legs are stubby columns that end in a rounded paw barely wider than the leg, so the leg
# keeps its thickness down to the foot (Nubi's tapers to an ankle over a flared foot: a mushroom here).
LEG_PROFILE = (
    (-0.050, 0.000), (-0.043, 0.046), (-0.025, 0.070), (0.000, 0.076), (0.060, 0.075),
    (0.100, 0.073), (0.125, 0.068), (0.140, 0.046), (0.145, 0.000),
)

PROFILE = {
    C: dict(
        SHOULDER=SHOULDER,
        ARM_DIRECTION=tuple((Vector(PAW_CENTER) - Vector(SHOULDER)).normalized()),
        ARM_LENGTH=ARM_LENGTH,
        HAND_START=HAND_START,
        LEG_PROFILE=LEG_PROFILE,
        FOOT_RADII=(0.080, 0.090, 0.060),
        # The reference's legs stand well inside the body's width (centres at ~0.42 of its half width); at
        # Nubi's hip spacing they splay out under the narrowed body like separate boots.
        HIP=(0.100, 0.000, 0.185),
        FOOT_CENTER=(0.104, -0.042, 0.040),
        FACE_EYE_X=0.072 * SX,
        FACE_EYE_Z=0.668,
        EYE_RADIUS_W=0.020,
        EYE_RADIUS_H=0.024,
        EYE_DEPTH=0.012,
        EYE_EMBED=0.40,
        EYE_PIVOT_DEPTH=0.30,
        EYE_SEGMENTS=16,
        EYE_RINGS=8,
        EYE_TILT_DEG=0.0,
        MOUTH_CENTER_Z=0.630,
        MOUTH_COLUMNS=20,
        MOUTH_ROWS=5,
        CHEEK_X=0.118 * SX,
        CHEEK_Z=0.628,
        CHEEK_RADIUS=0.036,
        MESH_NAME="Jolly_Mesh",
        # Kept as Nubi's: Animator bindings are "<armature>/Root/...", so with the same name Nubi's controller
        # (through an override controller) and its upper-body mask bind to this rig unchanged.
        ARMATURE_NAME=C.ARMATURE_NAME,
        MATERIAL_SKIN="M_Jolly_Fur",
        MATERIAL_GLOSS="M_Jolly_Gloss",
        TEXTURE_NAME="T_Jolly_Albedo",
        COLOR_EYE=(0.040, 0.034, 0.040),
        COLOR_EYE_LOW=(0.110, 0.090, 0.110),
        COLOR_MOUTH=(0.150, 0.055, 0.065),
        COLOR_TONGUE=(0.900, 0.480, 0.520),
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
        # Right paw on the side of the hood next to the face plate (rest space, rides Head).
        THINK_CHEEK_R=(-0.185 * SX, -0.165 * SY, 0.575),
        NOD_SQUASH=0.07,
    ),
    validate: dict(
        # The fused shoulder (axial < 0.10) is skin, not a limb that can cut into the body.
        ARM_HIDDEN_AXIAL=0.100,
    ),
    previews: dict(
        BODY_TARGET=(0.0, 0.0, 0.45),
        BODY_DISTANCE=2.35,
        FACE_TARGET=(0.0, -0.18, 0.640),
        FACE_DISTANCE=0.70,
    ),
}

# Jolly's face is smaller-featured than Nubi's: same shapes, 72 % size, a thin line of a smile.
MOUTH_SCALE = 0.72
MOUTH_THICKNESS_SCALE = 0.78

# ------------------------------------------------------------- palette ----
# sRGB 0..1, picked from the reference photo (sunlit belly ~ #DDC2B9, plate ~ #E4D6D1) and pushed a little
# towards blush pink: the shell shader darkens roots and lightens tips around this base.
COLOR_FUR = (0.950, 0.755, 0.720)
COLOR_FUR_TOP = (0.960, 0.790, 0.755)
COLOR_FUR_SHADE = (0.890, 0.680, 0.650)
COLOR_PLATE = (0.915, 0.810, 0.775)
COLOR_PLATE_RIM = (0.935, 0.790, 0.760)
COLOR_CHEEK = (0.965, 0.640, 0.650)
COLOR_SOLE = (0.860, 0.700, 0.680)
CHEEK_OPACITY = 0.80
COLOR_VARIATION = 0.035         # low-frequency clump tint in the albedo (fraction of value)
NORMAL_STRENGTH = 1.2
NORMAL_TEXTURE = "T_Jolly_Normal"

PART_SCAN = C.PART_BODY

# ------------------------------------------------------------------ fur ----
# Maps for Unity's shell-fur shader; the channel layout is the contract in docs/art/jolly-demo.md.
FUR_TEXTURE = "T_Jolly_Fur"            # R length, G density, B tip lightening, A occlusion (linear)
FLOW_TEXTURE = "T_Jolly_FurFlow"       # RG comb direction in UV space (0.5 = none), B comb strength
NOISE_TEXTURE = "T_Jolly_FurNoise"     # tiling strand field: R strand height, G clump, B strand random
NOISE_SIZE = 256
NOISE_CELLS = 32                        # strands per tile side
STRAND_SPACING = 0.003                  # metres between strands on the body; sets the recommended tiling
FUR_LENGTH = 0.024                      # metres at length 1.0
FUR_SHELLS = 16
FUR_DROOP = 0.35                        # tip sag under gravity, fraction of the shell height
FUR_ROOT_SHADE = 0.70                   # albedo multiplier at the root shell ...
FUR_TIP_TINT = 1.10                     # ... and at the tip (lighter tips)
AO_RAYS = 24
AO_DISTANCE = 0.20
AO_STRENGTH = 0.85
AO_BLUR = 4                             # edge-neighbour passes: 24 rays per vertex are noisy
AO_MIN = 0.35
AO_MIN_DISTANCE = 0.008
FUR_LEN_ATTR = "jolly_fur_len"         # preview only: per-vertex length for the shell offset
ARMPIT_SHORTEN = 0.85
# Skin the clips stretch (the armpit and shoulder pad when an arm rises) fans long shells out into streaks;
# fur is shortened by the worst per-vertex area stretch over every clip: from full length at STRETCH_FROM
# down to STRETCH_MIN_LENGTH at STRETCH_TO (deformed / rest area around the vertex).
STRETCH_FROM, STRETCH_TO = 1.35, 2.50
STRETCH_MIN_LENGTH = 0.30            # short: long stretched shells fan into streaks; bald reads as a bare tube
STRETCH_SAMPLES = 8                     # frames sampled per clip
STRETCH_BLUR = 2
VERTEX_STRETCH = None                   # per-vertex max area ratio; set by measure_stretch before the fur bake

# --------------------------------------------------------------- renders ----
RENDER_SUBDIR = "v4"
V2_SIZE = 1024
V2_VIEWS = (("front", 0, 4), ("three_quarter", 35, 8), ("side", 90, 4), ("back", 180, 6))
V2_TARGET = (0.0, 0.0, 0.44)
V2_DISTANCE = 2.25
SHEET_CELL = 256
SHEET_FRAMES = 4
SHEET_AZIMUTH = 25
SHEET_AZIMUTH_OVERRIDE = {"Wave": 30, "Point": -60, "Think": -25, "Walk": 60, "Hop": 60, "Happy": 15}
WAVE_POSE_FRAME = 22


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="build_jolly")
    p.add_argument("--src", required=True, help="source mesh (.glb/.gltf/.stl); never committed")
    p.add_argument("--out-dir", default=str(DEFAULT_OUT_DIR), help="git-ignored Unity output folder")
    p.add_argument("--render-dir", default=str(DEFAULT_RENDER_DIR), help="git-ignored render folder")
    p.add_argument("--no-render", action="store_true", help="skip preview renders")
    p.add_argument("--clips", default="", help="comma-separated clips for the contact sheet (default: all)")
    return p.parse_args(argv)


def apply_profile():
    for module, values in PROFILE.items():
        for name, value in values.items():
            if not hasattr(module, name):
                raise AttributeError(f"{module.__name__} has no {name}; the Nubi package changed")
            setattr(module, name, value)
    # Point winds the arm back past this direction before reaching; from the true rest direction the
    # wind-up grazes the torso (Jolly's arm hangs close), so it starts about 5 degrees further out.
    d = Vector(C.ARM_DIRECTION).normalized()
    clips.REST_ARM_DIR_R = tuple(Vector((-d.x - 0.09, d.y, d.z)).normalized())
    shapes.MOUTH_BASIS = _scaled_mouth(shapes.MOUTH_BASIS)
    shapes.MOUTH_SHAPES = {k: _scaled_mouth(v) for k, v in shapes.MOUTH_SHAPES.items()}
    validate._max_depth = _max_depth_parity
    texture.PAINTERS.update({C.PART_BODY: color_scan, C.PART_ARM: color_arm, C.PART_LEG: color_fur,
                             C.PART_FOOT: color_foot, C.PART_EYE: color_eye})
    apply_clips()


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
    creases (arm groove edges, the capped bottom rim) the nearest face can belong to the far side of a
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


def _sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


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
    """Weld the triangle soup, stand it on the origin (feet at z 0, centred in X/Y) at HEIGHT, narrowed by SCAN_SCALE_X/Y."""
    mesh = obj.data
    co = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    lo, hi = co.min(axis=0), co.max(axis=0)
    s = HEIGHT / (hi[2] - lo[2])
    co = (co - np.array([(lo[0] + hi[0]) * 0.5, (lo[1] + hi[1]) * 0.5, lo[2]])) * s * np.array([SX, SY, 1.0])
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


def deflate_arms(obj):
    """The print's arms are fused to the torso along their length, so they cannot swing. Push the arm
    bump back onto the torso outline (interpolated across the arm in section angle); generated arms
    replace them."""
    bvh = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())
    th0, th1 = (radians(a) for a in ARM_ZONE_THETA)
    z_lo, z_full, z_hi = ARM_ZONE_Z

    zs = np.arange(z_lo - 0.02, z_hi + 0.02, 0.005)
    table = {}
    for side in (1, -1):
        # theta is measured from the side's own +-X axis, so the right side mirrors it about Y.
        r0 = [_outline_at(bvh, th0 if side > 0 else pi - th0, z) for z in zs]
        r1 = [_outline_at(bvh, th1 if side > 0 else pi - th1, z) for z in zs]
        table[side] = (np.array(r0), np.array(r1))

    for v in obj.data.vertices:
        x, y, z = v.co
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


def cut_legs(obj):
    """Remove the print's stub legs below LEG_CUT_Z and cap the cut; generated legs replace them."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=(0.0, 0.0, LEG_CUT_Z), plane_no=(0.0, 0.0, 1.0),
                           clear_inner=True)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def _arm_local(co, side):
    """Arm-frame coordinates of points (n, 3): X across the paddle (front/back), A along the arm, Z towards
    the body (G.arm_frame's axes)."""
    m = np.array(G.arm_frame(side))
    local = (co - m[:3, 3]) @ m[:3, :3]
    return local[:, 0], local[:, 1], local[:, 2], m


def carve_under_arms(obj):
    """Press the torso back to ARM_CLEARANCE from where the new arm will hang (along the arm's own
    'towards the body' axis), below the armpit only."""
    mesh = obj.data
    co = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    pa = np.array([a for a, _r in ARM_PROFILE])
    pr = np.array([r for _a, r in ARM_PROFILE])
    for side in "LR":
        x, a, z, m = _arm_local(co, side)
        rx = np.interp(a, pa, pr) + ARM_CLEARANCE
        rz = np.interp(a, pa, pr) * ARM_FLATTEN + ARM_CLEARANCE
        bound = rz * np.sqrt(np.clip(1.0 - (x / rx) ** 2, 0.0, None))
        fade = _sstep(ARM_CARVE_AXIAL[0], ARM_CARVE_AXIAL[1], a) * (a < pa[-1] + ARM_CLEARANCE)
        on_side = co[:, 0] > 0 if side == "L" else co[:, 0] < 0
        sel = on_side & (fade > 0) & (z < bound) & (z > -0.5 * rz)
        co[sel] += np.outer((bound - z)[sel] * fade[sel], m[:3, 2])
    mesh.vertices.foreach_set("co", co.ravel())
    mesh.update()


def build_arm_shell(side):
    frame = G.arm_frame(side)
    rings, aux, p0, p1, pole_aux = G.tube_rings(ARM_PROFILE, frame, ARM_SEGMENTS, lambda _a: ARM_FLATTEN)
    obj = G.build_revolution(f"ArmShell_{side}", rings, p0, p1, C.PART_ARM, aux, pole_aux)
    return G.finish_part(obj, 1)


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


def _distances(co, bvhs):
    """Unsigned distance from each point to the nearest of several BVH surfaces."""
    out = np.full(len(co), 1e9)
    for bvh in bvhs:
        for i, p in enumerate(co):
            hit = bvh.find_nearest(Vector(p))
            if hit[0] is not None and hit[3] < out[i]:
                out[i] = hit[3]
    return out


def smooth_junction(obj, arm_bvhs, body_bvh):
    """Round the remesh's crease where arm and body meet: vertices close to both source surfaces."""
    co = np.array([v.co[:] for v in obj.data.vertices])
    d_arm = _distances(co, arm_bvhs.values())
    d_body = _distances(co, [body_bvh])
    axial = np.where(co[:, 0] >= 0, _arm_local(co, "L")[1], _arm_local(co, "R")[1])
    near = (np.maximum(d_arm, d_body) < JUNCTION_DISTANCE) & (axial < JUNCTION_MAX_AXIAL)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    sel = {bm.verts[i] for i in np.nonzero(near)[0]}
    sel |= {e.other_vert(v) for v in list(sel) for e in v.link_edges}   # one ring wider, so the fillet blends
    sel = list(sel)
    for _ in range(JUNCTION_PASSES):
        bmesh.ops.smooth_vert(bm, verts=sel, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return int(len(sel))


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


def repair_folds(obj):
    """Relax the vertices of folded faces (and one ring around them) until no fold is left; returns how many
    folded edges the decimation left."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    found = None
    for _ in range(FOLD_PASSES):
        bm.normal_update()
        folds = [e for e in bm.edges if len(e.link_faces) == 2
                 and e.link_faces[0].normal.dot(e.link_faces[1].normal) < FOLD_DOT]
        found = len(folds) if found is None else found
        if not folds:
            break
        sel = {v for e in folds for f in e.link_faces for v in f.verts}
        sel |= {e.other_vert(v) for v in list(sel) for e in v.link_edges}
        for _ in range(3):
            bmesh.ops.smooth_vert(bm, verts=list(sel), factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return found


def relax_pits(obj):
    """Relax pits deeper than PIT_DEPTH, except in the plate's inset rim (PLATE_RIM_RP, so after tag_scan
    has fitted PLATE_FRAME) and the armpit crease; returns how many pit vertices there were."""
    (_cx, _cy, cz), (hw, hh) = PLATE_FRAME
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    pits = None
    for _ in range(PIT_PASSES):
        bm.normal_update()
        sel = set()
        for v in bm.verts:
            co = v.co
            rp = ((abs(co.x) / hw) ** PLATE_POWER + (abs(co.z - cz) / hh) ** PLATE_POWER) ** (1.0 / PLATE_POWER)
            if not v.link_edges or (co.y < -0.05 and v.normal.y < -0.2 and PLATE_RIM_RP[0] < rp < PLATE_RIM_RP[1]) \
                    or (abs(co.x) > 0.17 * SX and 0.33 < co.z < 0.48):
                continue
            nb = [e.other_vert(v).co for e in v.link_edges]
            if (sum(nb, Vector()) / len(nb) - co).dot(v.normal) > PIT_DEPTH:
                sel.add(v)
        pits = len(sel) if pits is None else pits
        if not sel:
            break
        sel |= {e.other_vert(v) for v in list(sel) for e in v.link_edges}
        for _ in range(2):
            bmesh.ops.smooth_vert(bm, verts=list(sel), factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return pits


class ArmTags:
    """Per-vertex arm membership of the merged body: m (0 body .. 1 arm), axial distance along the arm and
    the ring angle, for weights, part ids and the painters."""

    def __init__(self, obj, arm_bvhs, body_bvh):
        co = np.array([v.co[:] for v in obj.data.vertices])
        d_arm = _distances(co, arm_bvhs.values())
        d_body = _distances(co, [body_bvh])
        self.side = np.where(co[:, 0] >= 0, 1, -1)
        self.axial = np.zeros(len(co))
        self.cos = np.zeros(len(co))
        self.sin = np.zeros(len(co))
        for side, sign in (("L", 1), ("R", -1)):
            x, a, z, _m = _arm_local(co, side)
            pick = self.side == sign
            self.axial[pick] = a[pick]
            ang = np.arctan2(z / ARM_FLATTEN, x)
            self.cos[pick], self.sin[pick] = np.cos(ang[pick]), np.sin(ang[pick])
        band = ARM_MEMBERSHIP_BAND
        # The inner side of the upper arm follows the arm from higher up: faded into the chest it stretched
        # into a long bare band when the arm rises; the outer side keeps the ramp so the shoulder doesn't crease.
        inner = _sstep(0.2, 0.8, self.sin)
        self.m = _sstep(-band, band, d_body - d_arm) * _sstep(*ARM_SHOULDER_RAMP, self.axial + ARM_INNER_RAMP_SHIFT * inner)
        # At the top of the gap under the arm the remesh bridges arm and torso with a few triangles whose
        # membership jumps 0 -> 1; a raised arm pulls them into a long web. Diffusing membership around the
        # armpit spreads that stretch over a soft armpit instead.
        edges = np.empty(len(obj.data.edges) * 2, dtype=np.int64)
        obj.data.edges.foreach_get("vertices", edges)
        edges = edges.reshape(-1, 2)
        zone = (self.axial > ARMPIT_BLEND_AXIAL[0]) & (self.axial < ARMPIT_BLEND_AXIAL[1]) & (co[:, 2] < SHOULDER[2])
        for _ in range(ARMPIT_BLEND_PASSES):
            acc, cnt = self.m.copy(), np.ones(len(co))
            np.add.at(acc, edges[:, 0], self.m[edges[:, 1]])
            np.add.at(acc, edges[:, 1], self.m[edges[:, 0]])
            np.add.at(cnt, edges[:, 0], 1)
            np.add.at(cnt, edges[:, 1], 1)
            self.m = np.where(zone, acc / cnt, self.m)


def tag_scan(obj, kd, arm):
    """Part ids, the face-plate mask (in nubi_aux.x for body vertices) and the arm frame coordinates
    (nubi_aux = axial, cos, sin for arm vertices) for the remeshed body."""
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

    co = np.array([v.co[:] for v in mesh.vertices])
    nrm = np.array([v.normal[:] for v in mesh.vertices])
    # The density test also fires on a few smooth patches at the hood's sides and base; the plate proper
    # sits between them.
    core = (mask > 0.5) & (np.abs(co[:, 0]) < PLATE_MAX_HALF_WIDTH) & (co[:, 2] > PLATE_MIN_Z + 0.05)
    half_w = float(np.percentile(np.abs(co[core, 0]), 99)) * PLATE_GROW
    z_lo, z_hi = (float(z) for z in np.percentile(co[core, 2], [1, 99]))
    cz, half_h = (z_lo + z_hi) * 0.5, (z_hi - z_lo) * 0.5 * PLATE_GROW
    rp = ((np.abs(co[:, 0]) / half_w) ** PLATE_POWER + (np.abs(co[:, 2] - cz) / half_h) ** PLATE_POWER) ** (1.0 / PLATE_POWER)
    front = (nrm[:, 1] < -0.2) & (co[:, 1] < -0.05) & (co[:, 2] > PLATE_MIN_Z - 0.05)
    mask = _sstep(1.0 + PLATE_SOFT, 1.0 - PLATE_SOFT, rp) * front
    global PLATE_FRAME
    PLATE_FRAME = ((0.0, float(co[mask > 0.5, 1].max()), cz), (half_w, half_h))

    is_arm = arm.m > 0.5
    aux_v = np.stack([np.where(is_arm, arm.axial, mask), np.where(is_arm, arm.cos, 0.0),
                      np.where(is_arm, arm.sin, 0.0)], 1)
    aux = mesh.attributes.new(G.AUX_ATTR, "FLOAT_VECTOR", "POINT")
    aux.data.foreach_set("vector", aux_v.ravel())
    face_m = np.array([arm.m[list(p.vertices)].mean() for p in mesh.polygons])
    part = mesh.attributes.new(G.PART_ATTR, "INT", "FACE")
    part.data.foreach_set("value", np.where(face_m > 0.5, C.PART_ARM, PART_SCAN).astype(np.int32))
    G.shade_smooth(obj)
    return float((mask > 0.5).mean())


PLATE_FRAME = ((0.0, -0.2, 0.64), (0.12, 0.10))   # ((0, rearmost plate y, centre z), (half width, half height)); set by tag_scan


def build_scan_body(src):
    obj = import_scan(src)
    normalise_scan(obj)
    kd = raw_kdtree(obj)
    deflate_arms(obj)
    cut_legs(obj)
    carve_under_arms(obj)
    proxy = build_torso_proxy(obj)
    body_bvh = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())
    shells = [build_arm_shell(side) for side in "LR"]
    arm_bvhs = {s: BVHTree.FromObject(o, bpy.context.evaluated_depsgraph_get()) for s, o in zip("LR", shells)}
    with bpy.context.temp_override(active_object=obj, selected_editable_objects=[obj] + shells):
        bpy.ops.object.join()
    for name in (G.AUX_ATTR, G.PART_ATTR):
        if name in obj.data.attributes:
            obj.data.attributes.remove(obj.data.attributes[name])
    remesh_and_soften(obj)
    junction = smooth_junction(obj, arm_bvhs, body_bvh)
    sand_face_features(obj)
    decimate(obj, BODY_TRIS)
    folds = repair_folds(obj)
    arm = ArmTags(obj, arm_bvhs, body_bvh)
    plate = tag_scan(obj, kd, arm)
    pits = relax_pits(obj)
    obj.name = "Body"
    return obj, plate, arm, junction, proxy, (folds, pits)


def build_torso_proxy(scan):
    """A closed, arm-less copy of the carved torso, skinned to the body chain only. Not exported: the
    penetration check tests the arms against it, because the merged body has holes where the arms join."""
    proxy = bpy.data.objects.new("TorsoProxy", scan.data.copy())
    bpy.context.scene.collection.objects.link(proxy)
    proxy.data.remesh_voxel_size = PROXY_VOXEL
    with bpy.context.temp_override(object=proxy, active_object=proxy, selected_objects=[proxy]):
        bpy.ops.object.voxel_remesh()
    decimate(proxy, PROXY_TRIS)
    rig.weight_body(proxy)
    proxy.hide_render = True
    proxy.hide_set(True)
    return proxy


def penetration_report(obj, arm_obj, proxy):
    """validate.penetration_report with the body shell taken from the posed torso proxy."""
    _faces, limbs, foot_faces = validate._vertex_sets(obj)
    proxy_faces = [list(p.vertices) for p in proxy.data.polygons]
    scene = bpy.context.scene
    out = {}
    for clip in CLIPS:
        start = animation.solo(arm_obj, clip.name)
        worst = {"arms_in_body": 0.0, "feet_overlap": 0.0, "worst_frame": 0, "worst_arm": None}
        for f in range(0, clip.length + 1, validate.PENETRATION_SAMPLE_STEP):
            scene.frame_set(start + f)
            deps = bpy.context.evaluated_depsgraph_get()
            co = [v.co.copy() for v in obj.evaluated_get(deps).data.vertices]
            body = BVHTree.FromPolygons([v.co.copy() for v in proxy.evaluated_get(deps).data.vertices], proxy_faces)
            depths = {k: _max_depth_parity(body, [co[i] for i in limbs.get(k, [])]) for k in ("arm_L", "arm_R")}
            arm_key = max(depths, key=depths.get)
            foot_l = BVHTree.FromPolygons(co, foot_faces["L"])
            d_feet = validate._max_depth(foot_l, [co[i] for i in limbs.get("foot_R", [])[::3]])
            if depths[arm_key] > worst["arms_in_body"]:
                worst["arms_in_body"], worst["worst_frame"], worst["worst_arm"] = depths[arm_key], f, arm_key
            worst["feet_overlap"] = max(worst["feet_overlap"], d_feet)
        tol = validate.PENETRATION_TOLERANCE_BY_CLIP.get(clip.name, validate.PENETRATION_TOLERANCE)
        worst["ok"] = worst["arms_in_body"] <= tol and worst["feet_overlap"] <= tol
        out[clip.name] = {k: (round(v, 4) if isinstance(v, float) else v) for k, v in worst.items()}
    animation.unsolo(arm_obj)
    return out


# --------------------------------------------------------------- parts ----

def weight_body_and_arms(obj, arm):
    """Body chain by height, arm chain by axial distance, blended by arm membership; at most 4 bones."""
    elbow = C.ARM_LENGTH * C.ARM_ELBOW_T
    wrist = C.ARM_LENGTH * C.ARM_WRIST_T
    per_vertex = []
    for v in obj.data.vertices:
        i = v.index
        m = float(arm.m[i])
        w = {b: x * (1.0 - m) for b, x in rig.chain_weights(v.co.z, rig.BODY_SPLITS).items()}
        if m > 1e-4:
            s = "L" if arm.side[i] > 0 else "R"
            splits = ((f"UpperArm_{s}", None), (f"LowerArm_{s}", (elbow, ARM_ELBOW_BLEND)),
                      (f"Hand_{s}", (wrist, ARM_WRIST_BLEND)))
            for b, x in rig.chain_weights(float(arm.axial[i]), splits).items():
                w[b] = w.get(b, 0.0) + x * m
        top = sorted(w.items(), key=lambda kv: -kv[1])[:C.MAX_INFLUENCES]
        total = sum(x for _b, x in top)
        per_vertex.append({b: x / total for b, x in top if x > 1e-5})
    rig._write(obj, per_vertex)


def build_parts(body, arm):
    bvh = G.body_bvh(body)
    weight_body_and_arms(body, arm)
    parts = [body]
    landmarks = rig.Landmarks()
    eye_frames = {}
    for side in "LR":
        leg = G.build_leg(side)
        rig.weight_leg(leg, side)
        foot = G.build_foot(side)
        rig.weight_rigid(foot, f"Foot_{side}")
        eye, frame, pivot = G.build_eye(side, bvh)
        rig.weight_rigid(eye, f"Eye_{side}")
        eye_frames[side] = frame
        landmarks.eye_pivots[side] = pivot
        landmarks.eye_centers[side] = frame.translation.copy()
        parts += [leg, foot, eye]

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


# ------------------------------------------------------------------ UVs ----

def unwrap(obj):
    """texture.unwrap, but the arms and the rest are projected separately: in one smart projection the
    arm's outer side and the torso side under it (same normals, joined at the shoulder) land in one island
    and overlap, which smears one's fur maps onto the other. Islands are then rescaled to one texel density
    and packed together."""
    mesh = obj.data
    parts = np.empty(len(mesh.polygons), dtype=np.int32)
    mesh.attributes[G.PART_ATTR].data.foreach_get("value", parts)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    tool = bpy.context.scene.tool_settings
    tool.use_uv_select_sync = True
    tool.mesh_select_mode = (False, False, True)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(mesh)
    bm.faces.ensure_lookup_table()
    for group in (parts == C.PART_ARM, parts != C.PART_ARM):
        # Face flags set in object mode are re-flushed from the vertex flags on entering edit mode, so
        # the selection is made here: deselect everything first, since a shared vertex keeps the
        # neighbouring group's faces selected otherwise.
        for f in bm.faces:
            f.select_set(False)
        for f in bm.faces:
            if group[f.index]:
                f.select_set(True)
        bmesh.update_edit_mesh(mesh)
        bpy.ops.uv.smart_project(angle_limit=radians(60.0), island_margin=C.UV_ISLAND_MARGIN,
                                 area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, margin=C.UV_ISLAND_MARGIN)
    bpy.ops.object.mode_set(mode="OBJECT")
    texture._place_mouth_uvs(mesh)


# -------------------------------------------------------------- albedo ----

def _rgb(c):
    return np.asarray(c, dtype=np.float32)


def _mix(a, b, t):
    return a * (1.0 - t[:, None]) + b * t[:, None]


def _waves(seed, count, wl_lo, wl_hi):
    rng = np.random.default_rng(seed)
    angles = rng.uniform(0.0, pi, count)
    lengths = rng.uniform(wl_lo, wl_hi, count)
    k = np.stack([np.cos(angles), np.sin(angles), rng.uniform(-1.0, 1.0, count)], 1)
    k /= np.linalg.norm(k, axis=1, keepdims=True)
    return k * (2.0 * pi / lengths)[:, None], rng.uniform(0.0, 2.0 * pi, count)


# Clumps: a sum of plane waves evaluated on surface position, so it is seamless across UV islands.
# Fixed seeds: deterministic.
CLUMP_WAVES, CLUMP_PHASES = _waves(7, 12, 0.025, 0.060)
TINT_WAVES, TINT_PHASES = _waves(11, 6, 0.08, 0.20)


def clump_height(pos):
    """0 (between clumps) .. 1 (clump crest), soft."""
    h = np.sin(pos @ CLUMP_WAVES.T + CLUMP_PHASES).mean(axis=1)
    return np.clip(0.5 + 1.4 * h, 0.0, 1.0)


def _fur_base(pos):
    z = pos[:, 2]
    col = _mix(np.broadcast_to(_rgb(COLOR_FUR_SHADE), pos.shape), _rgb(COLOR_FUR), _sstep(0.05, 0.40, z))
    col = _mix(col, _rgb(COLOR_FUR_TOP), _sstep(0.55, HEIGHT, z) * 0.8)
    tint = np.sin(pos @ TINT_WAVES.T + TINT_PHASES).mean(axis=1)
    return col * (1.0 + COLOR_VARIATION * (tint + 0.6 * (clump_height(pos) - 0.5)))[:, None]


def color_fur(pos, _aux, _cheeks):
    return _fur_base(pos)


def color_arm(pos, _aux, _cheeks):
    return _fur_base(pos)


def color_foot(pos, aux, _cheeks):
    sole = _sstep(-0.30, -0.45, aux[:, 1])
    return _mix(_fur_base(pos), _rgb(COLOR_SOLE), sole)


def _plate_weight(aux):
    return _sstep(0.35, 0.75, aux[:, 0])


def color_scan(pos, aux, cheeks):
    plate = _plate_weight(aux)
    rim = _sstep(0.10, 0.45, aux[:, 0]) * (1.0 - _sstep(0.55, 0.95, aux[:, 0]))
    face = _mix(np.broadcast_to(_rgb(COLOR_PLATE), pos.shape), _rgb(COLOR_PLATE_RIM), rim * 0.7)
    blush = np.zeros(len(pos), dtype=np.float32)
    for c in cheeks:
        d2 = np.sum((pos - np.asarray(c, dtype=np.float32)) ** 2, axis=1)
        blush = np.maximum(blush, np.exp(-d2 / (C.CHEEK_RADIUS ** 2)))
    face = _mix(face, _rgb(COLOR_CHEEK), CHEEK_OPACITY * _sstep(0.05, 0.85, blush))
    return _mix(_fur_base(pos), face, plate)


def color_eye(pos, aux, _cheeks):
    """Glossy black bead with one small highlight up and to the viewer's left (eye-local -x, +y)."""
    nx, ny, nz = aux[:, 0], aux[:, 1], aux[:, 2]
    col = _mix(np.broadcast_to(_rgb(C.COLOR_EYE), pos.shape), _rgb(C.COLOR_EYE_LOW),
               _sstep(-0.2, -0.8, ny) * 0.6)
    glint = _sstep(1.0, 0.7, np.hypot((nx + 0.30) / 0.17, (ny - 0.36) / 0.19)) * _sstep(0.1, 0.4, nz)
    return _mix(col, _rgb(C.COLOR_EYE_GLINT), glint)


def fill_unpainted(image, color):
    """Texels no triangle covers stay black after the shared painter's dilation; decimation slivers can
    sample them (black specks on the fur). Nothing painted is pure black, so those become fur."""
    w, h = image.size
    px = np.empty(w * h * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    px = px.reshape(-1, 4)
    empty = px[:, :3].max(axis=1) <= 0.0
    px[empty, :3] = color
    image.pixels.foreach_set(px.ravel())
    image.save()


# ------------------------------------------------------------ fur maps ----

def _rasterise_features(mesh, corner_features, size):
    """Like texture._rasterise, for an arbitrary (n_tris, 3, k) corner feature array; also returns the
    triangle index per texel so per-triangle data (tangent frames) can be looked up."""
    uv, _co, _aux, part = texture._gather_triangles(mesh)
    k = corner_features.shape[2]
    feat = np.zeros((size, size, k), dtype=np.float32)
    part_img = np.full((size, size), -1, dtype=np.int32)
    tri_img = np.full((size, size), -1, dtype=np.int64)
    px = uv * size - 0.5
    for t in range(len(px)):
        p = px[t]
        x0, y0 = np.floor(p.min(axis=0)).astype(int)
        x1, y1 = np.ceil(p.max(axis=0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, size - 1), min(y1, size - 1)
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        (ax, ay), (bx, by), (cx, cy) = p
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        w0 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
        w1 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
        w2 = 1.0 - w0 - w1
        inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not inside.any():
            continue
        w = np.stack((w0[inside], w1[inside], w2[inside]), axis=1)
        ys, xs = gy[inside], gx[inside]
        feat[ys, xs] = w @ corner_features[t]
        part_img[ys, xs] = part[t]
        tri_img[ys, xs] = t
    return feat, part_img, tri_img, uv


def _triangle_tangents(mesh, uv):
    """Unnormalised dP/du and dP/dv per loop triangle (the frame Unity's MikkTSpace tangents follow)."""
    _uv, co, _aux, _part = texture._gather_triangles(mesh)
    e1, e2 = co[:, 1] - co[:, 0], co[:, 2] - co[:, 0]
    d1, d2 = uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0]
    det = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]
    r = np.where(np.abs(det) > 1e-12, 1.0 / np.where(det == 0, 1.0, det), 0.0)[:, None]
    t = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) * r
    b = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) * r
    return t, b


def vertex_ao(obj):
    """Hemisphere occlusion per vertex (rest pose), from ray casts against the whole character."""
    mesh = obj.data
    # From the mesh data, not the evaluated object: it is bound to the rig by now, and the NLA would pose it.
    bvh = BVHTree.FromPolygons([v.co for v in mesh.vertices], [p.vertices[:] for p in mesh.polygons])
    rng = np.random.default_rng(3)
    u1, u2 = rng.random(AO_RAYS), rng.random(AO_RAYS)
    r, phi = np.sqrt(u1), 2.0 * pi * u2
    local = np.stack([r * np.cos(phi), r * np.sin(phi), np.sqrt(1.0 - u1)], 1)  # cosine-weighted
    ao = np.ones(len(mesh.vertices))
    for v in mesh.vertices:
        n = v.normal
        tx = Vector((1, 0, 0)) if abs(n.x) < 0.9 else Vector((0, 1, 0))
        tx = (tx - n * n.dot(tx)).normalized()
        ty = n.cross(tx)
        origin = v.co + n * 0.004
        occ = 0.0
        for lx, ly, lz in local:
            d = tx * lx + ty * ly + n * lz
            hit, _hn, _i, dist = bvh.ray_cast(origin, d, AO_DISTANCE)
            if hit is not None and dist > AO_MIN_DISTANCE:   # pits in the decimated scan are not occluders
                occ += 1.0 - dist / AO_DISTANCE
        ao[v.index] = 1.0 - AO_STRENGTH * occ / AO_RAYS
    edges = np.empty(len(mesh.edges) * 2, dtype=np.int64)
    mesh.edges.foreach_get("vertices", edges)
    edges = edges.reshape(-1, 2)
    for _ in range(AO_BLUR):
        acc = np.zeros_like(ao)
        cnt = np.zeros_like(ao)
        np.add.at(acc, edges[:, 0], ao[edges[:, 1]])
        np.add.at(acc, edges[:, 1], ao[edges[:, 0]])
        np.add.at(cnt, edges[:, 0], 1)
        np.add.at(cnt, edges[:, 1], 1)
        ao = 0.5 * ao + 0.5 * acc / np.maximum(cnt, 1)
    return np.maximum(ao, AO_MIN)


def fur_fields(pos, aux, part, nrm, stretch=None):
    """Fur length, density, tip lightening, comb direction (unit, in the surface plane) and comb strength."""
    n = len(pos)
    z = pos[:, 2]
    body = part == C.PART_BODY
    arm = part == C.PART_ARM
    leg = part == C.PART_LEG
    foot = part == C.PART_FOOT
    plate = np.where(body, _sstep(0.12, 0.55, aux[:, 0]), 0.0)
    paw = np.where(arm, _sstep(HAND_START - 0.03, HAND_START + 0.05, aux[:, 0]), 0.0)
    sole = np.where(foot, _sstep(-0.30, -0.45, aux[:, 1]), 0.0)

    length = np.zeros(n)
    underside = 0.30 + 0.70 * _sstep(-0.75, -0.30, nrm[:, 2])     # short where the body faces the floor
    (_cx, cy, cz), (hw, hh) = PLATE_FRAME
    rp = ((np.abs(pos[:, 0]) / hw) ** PLATE_POWER + (np.abs(z - cz) / hh) ** PLATE_POWER) ** (1.0 / PLATE_POWER)
    rim = 0.45 + 0.55 * _sstep(1.0, 1.35, rp)                      # short fur around the face keeps it open
    length[body] = ((1.0 - plate) * (0.35 + 0.65 * _sstep(0.13, 0.24, z)) * underside * rim)[body]
    length[arm] = (1.0 - 0.50 * paw)[arm]
    # The armpit skin stretches when an arm rises; long fur there fans out into streaks.
    armpit = np.zeros(n)
    for side, sign in (("L", 1.0), ("R", -1.0)):
        _x, a, zl, _m = _arm_local(pos.astype(np.float64), side)
        here = (pos[:, 0] * sign > 0) & (zl > 0.0)
        armpit = np.maximum(armpit, here * _sstep(-0.04, 0.0, a) * _sstep(0.14, 0.06, a))
    length *= 1.0 - ARMPIT_SHORTEN * armpit
    if stretch is not None:
        length *= 1.0 - (1.0 - STRETCH_MIN_LENGTH) * _sstep(STRETCH_FROM, STRETCH_TO, stretch)
    length[leg] = 0.65
    length[foot] = (0.40 - 0.28 * sole)[foot]

    density = np.zeros(n)
    density[body] = (1.0 - plate)[body]
    density[arm] = (1.0 - 0.15 * paw)[arm]
    density[leg] = 0.9
    density[foot] = 0.8
    tip = np.clip(0.4 + 0.6 * _sstep(0.2, 0.8, z), 0.0, 1.0) * (length > 0)

    # Combed down and a little outwards; radiating from a crown whorl on top; away from the face plate
    # around its rim (the hood frames the face); down the arm on the arm; towards the toes on the feet.
    radial = np.stack([pos[:, 0], pos[:, 1], np.zeros(n)], 1)
    radial /= np.maximum(np.linalg.norm(radial, axis=1, keepdims=True), 1e-6)
    comb = np.tile([0.0, 0.0, -1.0], (n, 1)) + radial * (0.35 + 1.4 * _sstep(0.72, 0.84, z))[:, None]
    around = _sstep(1.6, 1.0, rp) * (pos[:, 1] < cy + 0.06) * body
    away = np.stack([pos[:, 0], np.zeros(n), z - cz], 1)
    away /= np.maximum(np.linalg.norm(away, axis=1, keepdims=True), 1e-6)
    comb = comb * (1.0 - 0.8 * around)[:, None] + away * (0.8 * around)[:, None]
    d = np.array(C.ARM_DIRECTION)
    arm_dir = np.where((pos[:, 0] >= 0)[:, None], d, d * np.array([-1.0, 1.0, 1.0]))
    comb[arm] = arm_dir[arm]
    comb[foot] = (0.0, -1.0, -0.4)
    comb -= nrm * np.sum(comb * nrm, axis=1, keepdims=True)
    comb /= np.maximum(np.linalg.norm(comb, axis=1, keepdims=True), 1e-6)
    strength = np.where(body, 0.8 * (1.0 - plate), 0.0) + 0.85 * arm + 0.8 * leg + 0.6 * foot
    return length, density, tip, comb, strength


def _save_data_png(name, rgba, path):
    size_y, size_x = rgba.shape[:2]
    img = bpy.data.images.new(name, size_x, size_y, alpha=rgba.shape[2] == 4, is_data=True)
    full = np.ones((size_y, size_x, 4), dtype=np.float32)
    full[..., :rgba.shape[2]] = np.clip(rgba, 0.0, 1.0)
    img.pixels.foreach_set(full.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    if rgba.shape[2] == 4:
        img.alpha_mode = "STRAIGHT"
    img.save()
    img.source = "FILE"
    img.filepath = path
    img.colorspace_settings.name = "Non-Color"
    img.reload()
    return img


def _vertex_areas(mesh):
    mesh.calc_loop_triangles()
    tv = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int64)
    mesh.loop_triangles.foreach_get("vertices", tv)
    tv = tv.reshape(-1, 3)
    co = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    area = 0.5 * np.linalg.norm(np.cross(co[tv[:, 1]] - co[tv[:, 0]], co[tv[:, 2]] - co[tv[:, 0]]), axis=1)
    out = np.zeros(len(co))
    for k in range(3):
        np.add.at(out, tv[:, k], area)
    return out


def measure_stretch(mesh_obj, arm_obj):
    """Worst per-vertex skin stretch (deformed / rest area) over every clip, into VERTEX_STRETCH."""
    global VERTEX_STRETCH
    rest = np.maximum(_vertex_areas(mesh_obj.data), 1e-12)
    worst = np.ones(len(rest))
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for clip in CLIPS:
        start = previews.solo(arm_obj, clip.name)
        for i in range(STRETCH_SAMPLES):
            bpy.context.scene.frame_set(start + round((clip.length - 1) * i / (STRETCH_SAMPLES - 1)))
            ev = mesh_obj.evaluated_get(depsgraph)
            m = ev.to_mesh()
            ratio = _vertex_areas(m) / rest
            worst = np.maximum(worst, ratio / np.median(ratio))   # the whole-body squash and stretch is fine
            ev.to_mesh_clear()
    previews.unsolo(arm_obj)
    bpy.context.scene.frame_set(0)
    edges = np.empty(len(mesh_obj.data.edges) * 2, dtype=np.int64)
    mesh_obj.data.edges.foreach_get("vertices", edges)
    edges = edges.reshape(-1, 2)
    for _ in range(STRETCH_BLUR):
        acc, cnt = worst.copy(), np.ones(len(worst))
        np.add.at(acc, edges[:, 0], worst[edges[:, 1]])
        np.add.at(acc, edges[:, 1], worst[edges[:, 0]])
        np.add.at(cnt, edges[:, 0], 1)
        np.add.at(cnt, edges[:, 1], 1)
        worst = np.maximum(worst, acc / cnt)    # widen, never soften, the stretched zone
    VERTEX_STRETCH = worst
    return {"max": round(float(worst.max()), 2), "vertices_shortened": int((worst > STRETCH_FROM).sum())}


def bake_fur_maps(obj, out_dir, size=C.TEXTURE_SIZE):
    """T_Jolly_Fur, T_Jolly_FurFlow and the smooth normal map, from one rasterisation of the mesh."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    tris = mesh.loop_triangles
    verts = np.empty(len(tris) * 3, dtype=np.int64)
    tris.foreach_get("vertices", verts)
    verts = verts.reshape(-1, 3)
    co = np.array([v.co[:] for v in mesh.vertices], dtype=np.float32)
    nrm = np.array([v.normal[:] for v in mesh.vertices], dtype=np.float32)
    aux = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.attributes[G.AUX_ATTR].data.foreach_get("vector", aux)
    aux = aux.reshape(-1, 3)
    ao = vertex_ao(obj).astype(np.float32)
    st = (VERTEX_STRETCH if VERTEX_STRETCH is not None else np.ones(len(co))).astype(np.float32)
    corner = np.concatenate([co[verts], aux[verts], nrm[verts], ao[verts][..., None], st[verts][..., None]], axis=2)
    feat, part_img, tri_img, uv = _rasterise_features(mesh, corner, size)
    t_tri, b_tri = _triangle_tangents(mesh, uv)

    covered = part_img >= 0
    pos, aux_t, n_t, ao_t = feat[covered, 0:3], feat[covered, 3:6], feat[covered, 6:9], feat[covered, 9]
    n_t /= np.maximum(np.linalg.norm(n_t, axis=1, keepdims=True), 1e-6)
    part = part_img[covered]
    length, density, tip, comb, strength = fur_fields(pos, aux_t, part, n_t, feat[covered, 10])

    # UV-space comb direction: components along the tangent (dP/du) and along cross(N, T) * sign, which is
    # the bitangent Unity's URP builds from MikkTSpace tangents.
    t = t_tri[tri_img[covered]]
    t -= n_t * np.sum(t * n_t, axis=1, keepdims=True)
    t /= np.maximum(np.linalg.norm(t, axis=1, keepdims=True), 1e-9)
    sign = np.sign(np.sum(np.cross(n_t, t) * b_tri[tri_img[covered]], axis=1))
    sign[sign == 0] = 1.0
    b = np.cross(n_t, t) * sign[:, None]
    flow = np.stack([np.sum(comb * t, axis=1), np.sum(comb * b, axis=1)], 1)
    flow /= np.maximum(np.linalg.norm(flow, axis=1, keepdims=True), 1e-6)

    fur = np.zeros((size, size, 4), dtype=np.float32)
    fur[covered] = np.stack([length, density, tip, ao_t], 1)
    flow_img = np.full((size, size, 3), (0.5, 0.5, 0.0), dtype=np.float32)
    flow_img[covered] = np.concatenate([flow * 0.5 + 0.5, strength[:, None]], 1)

    # Smooth normal map: soft clumps on the fur, flat on the plate, eyes and mouth.
    h = np.zeros((size, size), dtype=np.float32)
    h[covered] = clump_height(pos) * np.clip(length, 0.0, 1.0)
    fur_mask = np.zeros((size, size), dtype=bool)
    fur_mask[covered] = length > 0
    dx = np.zeros_like(h)
    dy = np.zeros_like(h)
    dx[:, 1:-1] = np.where(fur_mask[:, 2:] & fur_mask[:, :-2], (h[:, 2:] - h[:, :-2]) * 0.5, 0.0)
    dy[1:-1, :] = np.where(fur_mask[2:, :] & fur_mask[:-2, :], (h[2:, :] - h[:-2, :]) * 0.5, 0.0)
    n_map = np.stack([-dx * NORMAL_STRENGTH, -dy * NORMAL_STRENGTH, np.ones_like(h)], -1)
    n_map /= np.linalg.norm(n_map, axis=-1, keepdims=True)

    fur = texture._dilate(fur, covered, C.TEXTURE_DILATE_PX)
    flow_img = texture._dilate(flow_img, covered, C.TEXTURE_DILATE_PX)
    n_map = texture._dilate(n_map * 0.5 + 0.5, covered, C.TEXTURE_DILATE_PX)
    n_map[~covered & (n_map.sum(-1) == 0)] = (0.5, 0.5, 1.0)

    os.makedirs(out_dir, exist_ok=True)
    images = {
        FUR_TEXTURE: _save_data_png(FUR_TEXTURE, fur, os.path.join(out_dir, f"{FUR_TEXTURE}.png")),
        FLOW_TEXTURE: _save_data_png(FLOW_TEXTURE, flow_img, os.path.join(out_dir, f"{FLOW_TEXTURE}.png")),
        NORMAL_TEXTURE: _save_data_png(NORMAL_TEXTURE, n_map, os.path.join(out_dir, f"{NORMAL_TEXTURE}.png")),
    }
    return images


def bake_noise_tile(out_dir):
    """Tiling strand field: a jittered grid of NOISE_CELLS^2 strands with wrap-around. R is a cone per
    strand (its random height times the falloff from its centre), so a shell at height h keeps the texels
    where R > h and strands taper towards their tips. G is a soft clump field, B a per-strand random."""
    rng = np.random.default_rng(5)
    n, size = NOISE_CELLS, NOISE_SIZE
    cell = size / n
    jitter = rng.uniform(0.15, 0.85, (n, n, 2))
    height = rng.uniform(0.55, 1.0, (n, n))
    rand = rng.random((n, n))
    ys, xs = np.mgrid[0:size, 0:size] + 0.5
    best = np.full((size, size), 1e9)
    r_out = np.zeros((size, size))
    b_out = np.zeros((size, size))
    ci, cj = (xs / cell).astype(int), (ys / cell).astype(int)
    for di in (-1, 0, 1):
        for dj in (-1, 0, 1):
            i, j = (ci + di) % n, (cj + dj) % n
            px = (ci + di + jitter[j, i, 0]) * cell
            py = (cj + dj + jitter[j, i, 1]) * cell
            d = np.hypot(xs - px, ys - py) / cell
            closer = d < best
            best = np.where(closer, d, best)
            r_out = np.where(closer, height[j, i] * np.clip(1.0 - d / 0.75, 0.0, 1.0) ** 0.7, r_out)
            b_out = np.where(closer, rand[j, i], b_out)
    k = 2.0 * pi / size
    g_out = 0.5 + 0.25 * (np.sin(xs * k * 3 + 1.3) * np.cos(ys * k * 2 + 0.4) + np.sin((xs + ys) * k * 5 + 2.1) * 0.6)
    tile = np.stack([r_out, np.clip(g_out, 0.0, 1.0), b_out], -1).astype(np.float32)
    return _save_data_png(NOISE_TEXTURE, tile, os.path.join(out_dir, f"{NOISE_TEXTURE}.png"))


def noise_tiling(obj):
    """UV repeats of the noise tile that give STRAND_SPACING on the surface (smart-project UVs keep one
    texel density across islands)."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv, co, _aux, part = texture._gather_triangles(mesh)
    keep = part != C.PART_MOUTH
    area3 = 0.5 * np.linalg.norm(np.cross(co[:, 1] - co[:, 0], co[:, 2] - co[:, 0]), axis=1)[keep].sum()
    d1, d2 = uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0]
    area_uv = 0.5 * np.abs(d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1])[keep].sum()
    metres_per_uv = (area3 / area_uv) ** 0.5
    return float(metres_per_uv / (NOISE_CELLS * STRAND_SPACING)), float(metres_per_uv)


def add_normal_map(material, image):
    nodes, links = material.node_tree.nodes, material.node_tree.links
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.location = (-400, -200)
    nmap = nodes.new("ShaderNodeNormalMap")
    nmap.location = (-150, -200)
    links.new(tex.outputs["Color"], nmap.inputs["Color"])
    links.new(nmap.outputs["Normal"], nodes.get("Principled BSDF").inputs["Normal"])


# --------------------------------------------------------------- clips ----
# Jolly's own takes for the clips that carry the character; the rest are Nubi's. Same names, lengths,
# loop flags and gait timing (Walk 0.49 m/s, Hop 0.90 m/s), so the Unity definition's tuning still holds.
IDLE_LENGTH = 100
WAVE_UP_L = (0.55, -0.22, 0.80)        # raised left arm, up and a little out and forward (the reference pose)
ARM_MAX_INWARD = 0.0                    # degrees: the arms hang 1.2 cm off the torso, so no clip swings them in


def _rest_arm(side):
    d = Vector(C.ARM_DIRECTION).normalized()
    return d if side == "L" else Vector((-d.x, d.y, d.z))


def jolly_idle(f):
    t = f / IDLE_LENGTH
    s = PoseSpec()
    breath = wave(t, 1.0, 0.0, 2)              # two slow breaths per loop
    sway = wave(t, 1.0)
    # Weight drifts side to side over planted feet; the blob breathes (stretch on the in-breath), the hood
    # follows the sway late and tilts the other way, which reads as a curious head tilt.
    s.loc["Hips"] = (0.007 * sway, 0.0, -0.002 + 0.002 * breath)
    s.rot["Hips"] = (0.0, -1.5 * sway, wave(t, 1.2, 0.1))
    s.scl["Spine"] = squash(-0.018 * breath)
    s.rot["Spine"] = (-0.8 * breath, 0.8 * wave(t, 1.0, 0.06), 0.0)
    s.rot["Chest"] = (-0.8 * wave(t, 1.0, 0.04, 2), 0.7 * wave(t, 1.0, 0.12), wave(t, 1.0, 0.2))
    s.rot["Head"] = (1.0 + wave(t, 1.8, 0.3) + 0.8 * wave(t, 1.0, 0.08, 2),
                     3.5 * wave(t, 1.0, 0.22), wave(t, 4.0, 0.15))
    for side, ph in (("L", 0.0), ("R", 0.10)):
        arms(s, side,
             upper=(wave(t, 2.5, 0.18 + ph), -2.5 - 1.2 * wave(t, 1.0, 0.05 + ph, 2), 0.0),
             lower=(-5.0 + wave(t, -3.0, 0.26 + ph), 0.0, 0.0),
             hand=(wave(t, -5.0, 0.34 + ph), 0.0, 0.0))
    return s


def jolly_walk(f):
    t = f / WALK_LENGTH
    s = PoseSpec()
    for side, offset in (("L", 0.0), ("R", 0.5)):
        y, lift, pitch = _foot_phase((t + offset) % 1.0, WALK_STRIDE, WALK_STANCE, WALK_LIFT)
        s.foot[side] = ((0.0, y, lift), (pitch, 0.0, 0.0))
    # Bouncy waddle: the blob rolls over the stance foot, squashes just after each contact and stretches
    # through the passing pose; the hood bobs a few frames behind; the arms swing and flap out at contact.
    contact = cos(TAU * (2 * t - 0.10))        # +1 just after each contact
    bob = -WALK_CROUCH - WALK_BOB * cos(TAU * (2 * t - 0.06))
    yaw = -10.0 * cos(TAU * (t - 0.04))
    roll = 9.0 * sin(TAU * (t - 0.04))
    s.loc["Hips"] = (0.018 * sin(TAU * (t - 0.06)), 0.0, bob)
    s.rot["Hips"] = (4.0, roll, yaw)
    s.scl["Spine"] = squash(0.05 * contact)
    s.rot["Spine"] = (1.0 + 1.5 * cos(TAU * (2 * t - 0.15)), -0.45 * roll, -0.40 * yaw)
    s.rot["Chest"] = (1.0 + 1.5 * cos(TAU * (2 * t - 0.22)), -0.30 * roll, -0.35 * yaw)
    s.rot["Head"] = (-3.0 + 3.0 * cos(TAU * (2 * t - 0.32)), -0.35 * 9.0 * sin(TAU * (t - 0.12)), -0.15 * yaw)
    for side, sign in (("L", 1.0), ("R", -1.0)):
        arms(s, side, upper=(sign * 22.0 * cos(TAU * (t - 0.08)), -7.0 - 4.0 * cos(TAU * (2 * t - 0.16)), 0.0),
             lower=(-10.0 - sign * 8.0 * cos(TAU * (t - 0.16)), 0.0, 0.0),
             hand=(-sign * 10.0 * cos(TAU * (t - 0.24)), 0.0, 0.0))
    return s


def jolly_hop(f):
    s = PoseSpec()
    c = float(HOP_CONTACT)
    air = HOP_LENGTH - c
    half = HOP_STRIDE * 0.5
    if f <= c:
        q = f / c
        y, lift, pitch = -half + HOP_STRIDE * q, 0.0, -14.0 * max(0.0, (q - 0.7) / 0.3)
        hz = -HOP_CROUCH - 0.014 * sin(pi * q)
        k = 0.09 * sin(pi * q)                 # squash on the ground
    else:
        q = (f - c) / air
        y = half - HOP_STRIDE * _ease(q)
        hz = -HOP_CROUCH + (HOP_HEIGHT + HOP_CROUCH) * sin(pi * q) ** 0.8
        lift = (hz + HOP_CROUCH) * 0.75
        pitch = -14.0 * (1 - q) ** 2 + 10.0 * sin(pi * q) * q
        k = -0.07 * sin(pi * min(1.0, q * 1.6))  # stretch on the way up, round again at the top
    for side in "LR":
        s.foot[side] = ((0.0, y, lift), (pitch, 0.0, 0.0))
    t = f / HOP_LENGTH
    s.loc["Hips"] = (0.0, 0.0, hz)
    s.scl["Spine"] = squash(k)
    stretch = max(0.0, hz + HOP_CROUCH) / (HOP_HEIGHT + HOP_CROUCH)
    s.rot["Hips"] = (8.0 - 4.0 * stretch, 0.0, 0.0)
    s.rot["Spine"] = (3.0 * cos(TAU * (t - 0.10)), 0.0, 0.0)
    s.rot["Chest"] = (3.0 * cos(TAU * (t - 0.16)), 0.0, 0.0)
    s.rot["Head"] = (-5.0 + 5.0 * cos(TAU * (t - 0.26)), 0.0, 0.0)
    for side in "LR":
        arms(s, side, upper=(-8.0 - 18.0 * cos(TAU * (t - 0.12)), -12.0 - 30.0 * stretch, 0.0),
             lower=(-14.0 - 12.0 * cos(TAU * (t - 0.20)), 0.0, 0.0),
             hand=(-12.0 * cos(TAU * (t - 0.28)), 0.0, 0.0))
    return s


def jolly_talk(f):
    """Nubi's gestures plus a little bounce of the whole blob on the beats."""
    s = clips.talk(f)
    t = f / 60.0
    beat = max(0.0, sin(TAU * 4 * t)) ** 2
    s.scl["Spine"] = squash(0.025 * beat - 0.008)
    s.loc["Hips"] = (0.0, 0.0, -0.002 - 0.004 * beat)
    hx, hy, hz = s.rot["Head"]
    s.rot["Head"] = (hx + 2.0 * max(0.0, sin(TAU * 4 * (t - 0.03))) ** 2, hy + wave(t, 3.0, 0.1), hz)
    return s


def jolly_wave(f):
    """Big friendly wave with the LEFT arm, like the reference: dip, swing the arm up past the target,
    three wags of the forearm with the paw trailing, drop with a little overshoot."""
    s = PoseSpec()
    reach = track(f, [(0, 0.0), (5, -0.08), (13, 1.08), (17, 0.96), (21, 1.0), (44, 1.0), (52, 0.12),
                      (56, -0.05), (60, 0.0)])
    d = _rest_arm("L").lerp(Vector(WAVE_UP_L).normalized(), reach)
    s.aim["UpperArm_L"] = tuple(d.normalized())
    wag_keys = [(0, 0.0), (13, -6.0), (19, 22.0), (25, -16.0), (31, 22.0), (37, -16.0), (43, 12.0), (50, 0.0)]
    hold = track(f, [(0, 0.0), (12, 1.0), (46, 1.0), (54, 0.0)])
    arms(s, "L", lower=(-8.0 * hold, -14.0 * hold + track(f, wag_keys, lag=1), 0.0),
         hand=(0.0, 0.7 * track(f, wag_keys, lag=4), 0.0))
    arms(s, "R", upper=track(f, [(0, ZERO), (10, (-6.0, -5.0, 0.0)), (48, (-6.0, -5.0, 0.0)), (60, ZERO)], lag=2),
         lower=track(f, [(0, ZERO), (10, (-10.0, 0, 0)), (48, (-10.0, 0, 0)), (60, ZERO)], lag=3))
    k = track(f, [(0, 0.0), (5, 0.06), (12, -0.045), (17, 0.012), (22, 0.0), (46, 0.0), (52, 0.03),
                  (57, -0.008), (60, 0.0)])
    s.scl["Spine"] = squash(k)
    s.loc["Hips"] = (0.0, 0.0, track(f, [(0, 0.0), (5, -0.012), (12, 0.006), (18, 0.0), (60, 0.0)]))
    body = [(0, ZERO), (5, (3.0, 1.0, 0.0)), (13, (-2.0, -4.0, 2.0)), (46, (-2.0, -4.0, 2.0)),
            (55, (1.0, 1.0, 0.0)), (60, ZERO)]
    s.rot["Chest"] = track(f, body)
    s.rot["Spine"] = scale(track(f, body, lag=1), 0.6)
    head = [(0, ZERO), (7, (4.0, 0.0, 0.0)), (16, (-4.0, 7.0, 4.0)), (46, (-4.0, 7.0, 4.0)), (58, ZERO)]
    s.rot["Head"] = add(track(f, head, lag=2), (0.0, 0.06 * track(f, wag_keys, lag=5), 0.0))
    return s


def jolly_happy(f):
    """Happy hop: crouch, spring up with both arms thrown high, land in a squash and wobble back."""
    s = PoseSpec()
    hz = track(f, [(0, 0.0), (6, -0.034), (10, 0.012), (15, 0.085), (19, 0.032), (22, -0.028),
                   (27, 0.006), (32, 0.0), (36, 0.0)])
    s.loc["Hips"] = (0.0, 0.0, hz)
    toe = track(f, [(0, 0.0), (9, 0.0), (14, -14.0), (20, 0.0), (36, 0.0)])
    for side in "LR":
        s.foot[side] = ((0.0, 0.0, max(0.0, hz)), (toe, 0.0, 0.0))
    s.scl["Spine"] = squash(track(f, [(0, 0.0), (6, 0.08), (10, -0.05), (15, -0.07), (19, -0.02),
                                      (22, 0.09), (26, -0.03), (31, 0.01), (36, 0.0)]))
    crouch = [(0, ZERO), (6, (7.0, 0.0, 0.0)), (11, (-4.0, 0.0, 0.0)), (15, (-6.0, 0.0, 0.0)),
              (22, (6.0, 0.0, 0.0)), (28, (-1.0, 0.0, 0.0)), (34, ZERO)]
    s.rot["Spine"] = track(f, crouch)
    s.rot["Chest"] = track(f, crouch, lag=1.5)
    s.rot["Head"] = add(scale(track(f, crouch, lag=3), -0.8),
                        track(f, [(0, ZERO), (12, (-7.0, 0, 0)), (26, (-7.0, 0, 0)), (34, ZERO)]))
    arm_up = [(0, ZERO), (6, (12.0, -2.0, 0.0)), (13, (-22.0, -118.0, 0.0)), (17, (-24.0, -126.0, 0.0)),
              (23, (-10.0, -50.0, 0.0)), (30, (2.0, 2.0, 0.0)), (36, ZERO)]
    for side in "LR":
        arms(s, side, upper=track(f, arm_up, lag=1.0),
             lower=scale(track(f, arm_up, lag=3.0), 0.30), hand=scale(track(f, arm_up, lag=4.5), 0.15))
    return s


def _no_inward_arms(fn):
    """Nubi's clips were authored for an A-pose with room between arm and body; Jolly's arms hang close,
    so their inward swings (+Y on the left arm, mirrored on the right) are clamped to ARM_MAX_INWARD.
    Only for hanging arms: on a raised arm +Y swings outwards, so Jolly's own clips are not wrapped."""
    def pose(f):
        s = fn(f)
        for bone in ("UpperArm", "LowerArm"):
            for side, sign in (("L", 1.0), ("R", -1.0)):
                r = s.rot.get(f"{bone}_{side}")
                if r is not None:
                    y = min(r[1] * sign, ARM_MAX_INWARD) * sign
                    s.rot[f"{bone}_{side}"] = (r[0], y, r[2])
        return s
    return pose


JOLLY_CLIPS = {"Idle": (IDLE_LENGTH, jolly_idle), "Walk": (WALK_LENGTH, jolly_walk), "Hop": (HOP_LENGTH, jolly_hop),
               "Talk": (60, jolly_talk), "Wave": (60, jolly_wave), "Happy": (36, jolly_happy)}


def apply_clips():
    for clip in CLIPS:
        if clip.name in JOLLY_CLIPS:
            clip.length, clip.pose = JOLLY_CLIPS[clip.name]
        else:
            clip.pose = _no_inward_arms(clip.pose)


# ------------------------------------------------------------- preview ----
# The shells Unity's shader draws, stacked in Blender: FUR_SHELLS copies of the skinned mesh, each pushed
# out along its normal by its height x FUR_LENGTH x the length map, sagging under gravity, alpha-clipped
# by the strand field. Preview only: none of this is exported.

def _shell_node_group():
    ng = bpy.data.node_groups.new("JollyFurShell", "GeometryNodeTree")
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    for name in ("Height", "Length", "Droop"):
        ng.interface.new_socket(name, in_out="INPUT", socket_type="NodeSocketFloat")
    nodes, links = ng.nodes, ng.links
    gi, go = nodes.new("NodeGroupInput"), nodes.new("NodeGroupOutput")
    attr = nodes.new("GeometryNodeInputNamedAttribute")
    attr.data_type = "FLOAT"
    attr.inputs["Name"].default_value = FUR_LEN_ATTR
    normal = nodes.new("GeometryNodeInputNormal")

    def math(op, a, b):
        node = nodes.new("ShaderNodeMath")
        node.operation = op
        links.new(a, node.inputs[0])
        links.new(b, node.inputs[1])
        return node.outputs[0]

    reach = math("MULTIPLY", math("MULTIPLY", gi.outputs["Height"], gi.outputs["Length"]), attr.outputs["Attribute"])
    sag = math("MULTIPLY", math("MULTIPLY", reach, gi.outputs["Height"]), gi.outputs["Droop"])
    out_vec = nodes.new("ShaderNodeVectorMath")
    out_vec.operation = "SCALE"
    links.new(normal.outputs["Normal"], out_vec.inputs[0])
    links.new(reach, out_vec.inputs["Scale"])
    down = nodes.new("ShaderNodeVectorMath")
    down.operation = "SCALE"
    down.inputs[0].default_value = (0.0, 0.0, -1.0)
    links.new(sag, down.inputs["Scale"])
    offset = nodes.new("ShaderNodeVectorMath")
    offset.operation = "ADD"
    links.new(out_vec.outputs[0], offset.inputs[0])
    links.new(down.outputs[0], offset.inputs[1])
    # Bald points (plate, eyes, mouth) are deleted: a zero-offset transparent shell over the opaque skin
    # renders black in EEVEE.
    bald = nodes.new("FunctionNodeCompare")
    bald.data_type = "FLOAT"
    bald.operation = "LESS_THAN"
    links.new(attr.outputs["Attribute"], bald.inputs[0])
    bald.inputs[1].default_value = 0.03
    delete = nodes.new("GeometryNodeDeleteGeometry")
    links.new(gi.outputs["Geometry"], delete.inputs["Geometry"])
    links.new(bald.outputs[0], delete.inputs["Selection"])
    setp = nodes.new("GeometryNodeSetPosition")
    links.new(delete.outputs["Geometry"], setp.inputs["Geometry"])
    links.new(offset.outputs[0], setp.inputs["Offset"])
    links.new(setp.outputs["Geometry"], go.inputs[0])
    return ng


def _shell_material(h, albedo, fur, noise, tiling):
    """One shell's material: albedo x root-to-tip tint x occlusion, alpha = strand(uv) > h where fur grows."""
    mat = bpy.data.materials.new(f"M_Preview_FurShell_{h:.3f}")
    mat.surface_render_method = "DITHERED"
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.85
    bsdf.inputs["Specular IOR Level"].default_value = 0.25
    bsdf.inputs["Sheen Weight"].default_value = 0.6
    bsdf.inputs["Sheen Tint"].default_value = (1.0, 0.78, 0.74, 1.0)   # a white sheen greys the pink
    out_node = nodes.get("Material Output")

    def tex(image, vector=None):
        node = nodes.new("ShaderNodeTexImage")
        node.image = image
        if vector is not None:
            links.new(vector, node.inputs["Vector"])
        return node

    def math(op, a, b):
        node = nodes.new("ShaderNodeMath")
        node.operation = op
        for i, v in enumerate((a, b)):
            if isinstance(v, (int, float)):
                node.inputs[i].default_value = v
            else:
                links.new(v, node.inputs[i])
        return node.outputs[0]

    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (tiling, tiling, 1.0)
    uv = nodes.new("ShaderNodeTexCoord")
    links.new(uv.outputs["UV"], mapping.inputs["Vector"])
    t_alb, t_fur, t_noise = tex(albedo), tex(fur), tex(noise, mapping.outputs[0])
    t_fur.image.alpha_mode = "CHANNEL_PACKED"
    split_fur = nodes.new("ShaderNodeSeparateColor")
    links.new(t_fur.outputs["Color"], split_fur.inputs[0])
    split_noise = nodes.new("ShaderNodeSeparateColor")
    links.new(t_noise.outputs["Color"], split_noise.inputs[0])
    # Strand survives if its cone height clears this shell (thinner where density is low).
    thresh = math("ADD", h, math("MULTIPLY", math("SUBTRACT", 1.0, split_fur.outputs[1]), 0.6))
    alpha = math("MULTIPLY", math("GREATER_THAN", split_noise.outputs[0], thresh),
                 math("GREATER_THAN", split_fur.outputs[0], 0.04))
    links.new(alpha, bsdf.inputs["Alpha"])
    tint = FUR_ROOT_SHADE + (FUR_TIP_TINT - FUR_ROOT_SHADE) * h
    tip_gain = math("ADD", tint, math("MULTIPLY", split_fur.outputs[2], 0.10 * h))
    occl = math("ADD", 1.0 - (1.0 - h), math("MULTIPLY", t_fur.outputs["Alpha"], 1.0 - h))
    shade = math("MULTIPLY", tip_gain, occl)
    strand = math("ADD", 0.93, math("MULTIPLY", split_noise.outputs[2], 0.14))
    col = nodes.new("ShaderNodeVectorMath")
    col.operation = "SCALE"
    links.new(t_alb.outputs["Color"], col.inputs[0])
    links.new(math("MULTIPLY", shade, strand), col.inputs["Scale"])
    links.new(col.outputs[0], bsdf.inputs["Base Color"])
    links.new(bsdf.outputs[0], out_node.inputs["Surface"])
    return mat


def _base_material(albedo, fur):
    """The skin under the shells: root-shaded albedo, darkened by occlusion (the h = 0 shell)."""
    mat = bpy.data.materials.new("M_Preview_FurBase")
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.9
    bsdf.inputs["Specular IOR Level"].default_value = 0.2
    t_alb = nodes.new("ShaderNodeTexImage")
    t_alb.image = albedo
    t_fur = nodes.new("ShaderNodeTexImage")
    t_fur.image = fur
    split = nodes.new("ShaderNodeSeparateColor")
    links.new(t_fur.outputs["Color"], split.inputs[0])
    # Bald parts (plate) keep the plain albedo; furred skin is the darker root colour.
    k = nodes.new("ShaderNodeMath")
    k.operation = "MULTIPLY_ADD"
    links.new(split.outputs[0], k.inputs[0])
    k.inputs[1].default_value = FUR_ROOT_SHADE - 1.0
    k.inputs[2].default_value = 1.0
    k2 = nodes.new("ShaderNodeMath")
    k2.operation = "MINIMUM"
    links.new(k.outputs[0], k2.inputs[0])
    k2.inputs[1].default_value = 1.0
    ao = nodes.new("ShaderNodeMath")
    ao.operation = "MULTIPLY"
    links.new(k2.outputs[0], ao.inputs[0])
    links.new(t_fur.outputs["Alpha"], ao.inputs[1])
    col = nodes.new("ShaderNodeVectorMath")
    col.operation = "SCALE"
    links.new(t_alb.outputs["Color"], col.inputs[0])
    links.new(ao.outputs[0], col.inputs["Scale"])
    links.new(col.outputs[0], bsdf.inputs["Base Color"])
    return mat


def vertex_fur_length(obj):
    """Per-vertex fur length (the length map's rule at each vertex, with its dominant part), for the shells."""
    mesh = obj.data
    parts = np.empty(len(mesh.polygons), dtype=np.int32)
    mesh.attributes[G.PART_ATTR].data.foreach_get("value", parts)
    vpart = np.full(len(mesh.vertices), C.PART_BODY)
    for p in mesh.polygons:          # eyes and mouth win over neighbours, so the face stays bald
        for vi in p.vertices:
            if parts[p.index] in (C.PART_EYE, C.PART_MOUTH) or vpart[vi] == C.PART_BODY:
                vpart[vi] = parts[p.index]
    co = np.array([v.co[:] for v in mesh.vertices])
    nrm = np.array([v.normal[:] for v in mesh.vertices])
    aux = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.attributes[G.AUX_ATTR].data.foreach_get("vector", aux)
    length = fur_fields(co, aux.reshape(-1, 3), vpart, nrm, VERTEX_STRETCH)[0]
    attr = mesh.attributes.get(FUR_LEN_ATTR) or mesh.attributes.new(FUR_LEN_ATTR, "FLOAT", "POINT")
    attr.data.foreach_set("value", length.astype(np.float32))


def add_fur_preview(mesh_obj, arm_obj, albedo, fur, noise, tiling):
    vertex_fur_length(mesh_obj)
    group = _shell_node_group()
    ids = {item.name: item.identifier for item in group.interface.items_tree
           if item.item_type == "SOCKET" and item.in_out == "INPUT"}
    base = _base_material(albedo, fur)
    mesh_obj.material_slots[0].link = "OBJECT"
    mesh_obj.material_slots[0].material = base
    shells = []
    for i in range(1, FUR_SHELLS + 1):
        h = i / FUR_SHELLS
        obj = bpy.data.objects.new(f"FurShell_{i:02d}", mesh_obj.data)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = arm_obj
        obj.matrix_parent_inverse = mesh_obj.matrix_parent_inverse.copy()
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = arm_obj
        gn = obj.modifiers.new("Shell", "NODES")
        gn.node_group = group
        gn[ids["Height"]] = h
        gn[ids["Length"]] = FUR_LENGTH
        gn[ids["Droop"]] = FUR_DROOP
        mat = _shell_material(h, albedo, fur, noise, tiling)
        for slot in obj.material_slots:
            slot.link = "OBJECT"
            slot.material = mat
        obj.visible_shadow = False   # EEVEE shadows ignore the clip, so bald (zero-offset) shells would shade the face
        shells.append(obj)
    return shells


def setup_v2_stage():
    cam = R.setup_stage()
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = 96
    # Standard, not AgX: AgX greys the blush pink to beige, and Unity's URP (Neutral or no tonemapping)
    # keeps the albedo's saturation, so Standard is the honest preview.
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium Contrast"
    scene.view_settings.exposure = -0.9
    bg = scene.world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.30, 0.31, 0.34, 1.0)
    bg.inputs["Strength"].default_value = 0.6
    floor = bpy.data.materials.get("M_PreviewFloor")
    floor.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (0.42, 0.42, 0.44, 1.0)
    for name, energy in (("Key", 300.0), ("Fill", 110.0), ("Rim", 320.0)):
        bpy.data.objects[name].data.energy = energy
    return cam


def _clip_frame(arm_obj, clip_name, frame):
    start = previews.solo(arm_obj, clip_name)
    bpy.context.scene.frame_set(start + frame)


def _set_face(mesh_obj, clip_name):
    for kb in mesh_obj.data.shape_keys.key_blocks[1:]:
        kb.value = 0.0
    for key, weight in faces.FACE_PRESETS.get(clip_name, {}).items():
        mesh_obj.data.shape_keys.key_blocks[key].value = weight


def render_v2(out_dir, cam, arm_obj, mesh_obj, only=None):
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    _set_face(mesh_obj, "Idle")
    _clip_frame(arm_obj, "Idle", 0)
    for name, az, el in V2_VIEWS:
        R.aim_camera(cam, V2_TARGET, az, el, V2_DISTANCE, 50)
        path = os.path.join(out_dir, f"jolly_{name}.png")
        R.render_still(path, V2_SIZE)
        paths.append(path)

    _set_face(mesh_obj, "Wave")
    _clip_frame(arm_obj, "Wave", WAVE_POSE_FRAME)
    R.aim_camera(cam, (0.0, 0.0, 0.50), 20, 6, V2_DISTANCE + 0.15, 50)
    path = os.path.join(out_dir, "jolly_wave_pose.png")
    R.render_still(path, V2_SIZE)
    paths.append(path)

    cells = []
    for clip in CLIPS:
        if only and clip.name not in only:
            continue
        _set_face(mesh_obj, clip.name)
        last = clip.length - (1 if clip.loop else 0)
        R.aim_camera(cam, V2_TARGET, SHEET_AZIMUTH_OVERRIDE.get(clip.name, SHEET_AZIMUTH), 6, V2_DISTANCE, 50)
        for i in range(SHEET_FRAMES):
            f = round(last * (i / SHEET_FRAMES if clip.loop else 0.12 + 0.76 * i / (SHEET_FRAMES - 1)))
            _clip_frame(arm_obj, clip.name, f)
            label = R.label_object(cam, f"{clip.name}  f{f}")
            cell = os.path.join(out_dir, f"_{clip.name}_{f}.png")
            R.render_still(cell, SHEET_CELL)
            R.remove_object(label)
            cells.append(cell)
    sheet = os.path.join(out_dir, "jolly_clips_sheet.png")
    R.compose_sheet(cells, SHEET_FRAMES, sheet)
    paths.append(sheet)
    previews.unsolo(arm_obj)
    for kb in mesh_obj.data.shape_keys.key_blocks[1:]:
        kb.value = 0.0
    face = previews.face_sheet(out_dir, cam, arm_obj, mesh_obj)
    os.replace(face, os.path.join(out_dir, "jolly_face_shapes.png"))
    paths.append(os.path.join(out_dir, "jolly_face_shapes.png"))
    return paths


# ---------------------------------------------------------------- main ----

def main():
    args = parse_args()
    out_dir = Path(args.out_dir)
    render_dir = Path(args.render_dir)
    tex_dir = out_dir / "Textures"
    fbx_path = out_dir / "Jolly.fbx"
    presets_path = out_dir / "Jolly_FacePresets.json"
    albedo_path = tex_dir / "T_Jolly_Albedo.png"
    report_path = out_dir / "build_report.json"
    blend_path = render_dir / "Jolly.blend"

    apply_profile()
    reset_scene()

    body, plate_share, arm_tags, junction, proxy, folds = build_scan_body(args.src)
    parts, _bvh, landmarks, eye_frames, cheeks = build_parts(body, arm_tags)
    mesh = join(parts)
    shapes.add_shape_keys(mesh, _bvh, eye_frames)

    unwrap(mesh)
    albedo, coverage = texture.paint_texture(mesh, cheeks, str(albedo_path))
    fill_unpainted(albedo, COLOR_FUR)
    texture.assign_materials(mesh, albedo)

    arm = rig.build_armature(landmarks)
    rig.bind(mesh, arm)
    rig.bind(proxy, arm)
    animation.bake_all(arm)

    stretch = measure_stretch(mesh, arm)
    maps = bake_fur_maps(mesh, str(tex_dir))
    noise = bake_noise_tile(str(tex_dir))
    tiling, metres_per_uv = noise_tiling(mesh)
    add_normal_map(mesh.data.materials[0], maps[NORMAL_TEXTURE])

    report = validate.static_report(mesh, arm)
    report["source"] = Path(args.src).name
    report["face_plate_vertex_share"] = round(plate_share, 3)
    report["arm_junction_vertices"] = junction
    report["decimate_folds_repaired"], report["pits_relaxed"] = folds
    report["uv_coverage"] = round(coverage, 3)
    report["blink_closed_ratio"] = validate.blink_report(mesh)
    report["penetration"] = penetration_report(mesh, arm, proxy)
    report["locomotion"] = validate.locomotion_report(arm)
    report["fur"] = {
        "shells_recommended": FUR_SHELLS,
        "length_m": FUR_LENGTH,
        "droop": FUR_DROOP,
        "root_shade": FUR_ROOT_SHADE,
        "tip_tint": FUR_TIP_TINT,
        "noise_tiling": round(tiling, 2),
        "strand_spacing_m": STRAND_SPACING,
        "metres_per_uv": round(metres_per_uv, 3),
        "clip_stretch": stretch,
    }
    report["outputs"] = {"fbx": fbx_path.name, "face_presets": presets_path.name,
                         "albedo": f"Textures/{albedo_path.name}", "normal": f"Textures/{NORMAL_TEXTURE}.png",
                         "fur": f"Textures/{FUR_TEXTURE}.png", "fur_flow": f"Textures/{FLOW_TEXTURE}.png",
                         "fur_noise": f"Textures/{NOISE_TEXTURE}.png"}

    bpy.context.scene.frame_set(0)
    export.export_fbx(str(fbx_path), mesh, arm)
    faces.write_json(str(presets_path), report["shape_keys"], CLIPS)
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    print("JOLLY_REPORT " + json.dumps(report))

    add_fur_preview(mesh, arm, albedo, maps[FUR_TEXTURE], noise, tiling)
    if not args.no_render:
        cam = setup_v2_stage()
        only = [c for c in args.clips.split(",") if c] or None
        render_v2(str(render_dir / RENDER_SUBDIR), cam, arm, mesh, only)
    export.save_blend(str(blend_path))


if __name__ == "__main__":
    main()
