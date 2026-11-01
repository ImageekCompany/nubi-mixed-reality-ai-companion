"""Design constants for Nubi. All lengths in metres, Blender space: Z up, character faces -Y.

Character-space conventions used everywhere in this package:
  +X = Nubi's LEFT side, -Y = forward (towards the viewer in Front view), +Z = up.
"""

from math import radians

# ---------------------------------------------------------------- body ----
# Gumdrop/bean body: an elliptical surface of revolution with a tapered top.
BODY_Z_BOTTOM = 0.115
BODY_Z_TOP = 0.845
BODY_RADIUS_X = 0.330          # widest half-width
BODY_DEPTH_RATIO = 0.88        # Y radius / X radius (slightly flattened front-to-back)
BODY_TAPER = 0.20              # how much narrower the top is than the belly (teardrop)
BODY_BOTTOM_FULLNESS = 0.70    # <1 squares off the bottom so it sits on the legs
BODY_TOP_FULLNESS = 0.82       # <1 rounds the crown into a gumdrop instead of an egg point
BODY_TIP = 0.012               # soft point at the crown where the sprout grows
BODY_FRONT_BULGE = 0.035       # belly pushes forward a little
BODY_SEGMENTS = 24             # cage resolution around (before subdivision)
BODY_RINGS = 18                # cage resolution bottom->top (before subdivision)

SUBSURF_LEVELS_BODY = 2
SUBSURF_LEVELS_LIMB = 1
SUBSURF_LEVELS_SMALL = 1

# ---------------------------------------------------------------- face ----
FACE_EYE_Z = 0.615
FACE_EYE_X = 0.112
EYE_RADIUS_W = 0.047           # half-width of an eye dome
EYE_RADIUS_H = 0.064           # half-height
EYE_DEPTH = 0.030              # dome protrusion half-depth
EYE_EMBED = 0.45               # fraction of depth sunk into the head
EYE_PIVOT_DEPTH = 0.27         # eye bone pivot behind the surface: eyes slide over the face when rotated
EYE_SEGMENTS = 24
EYE_RINGS = 10
EYE_TILT_DEG = 6.0             # eyes lean slightly outward at the top: friendlier look

MOUTH_CENTER_Z = 0.530
MOUTH_SURFACE_OFFSET = 0.0015  # avoid z-fighting with the skin
MOUTH_COLUMNS = 24
MOUTH_ROWS = 6

CHEEK_X = 0.170
CHEEK_Z = 0.545
CHEEK_RADIUS = 0.050
BELLY_CENTER_Z = 0.300
BELLY_RADIUS_X = 0.175
BELLY_RADIUS_Z = 0.135

# --------------------------------------------------------------- limbs ----
SHOULDER = (0.272, -0.005, 0.425)   # left shoulder pivot (mirrored for right)
ARM_DIRECTION = (0.62, -0.10, -0.78)  # rest pose: relaxed A-pose, slightly forward
ARM_ELBOW_T = 0.45                    # elbow position as a fraction of arm length
ARM_WRIST_T = 0.66
ARM_LENGTH = 0.270
# (axial distance from shoulder, radius) control points: the first/last radius 0 are poles.
ARM_PROFILE = (
    (-0.045, 0.000), (-0.038, 0.034), (-0.022, 0.050), (0.000, 0.054), (0.050, 0.051),
    (0.100, 0.048), (0.150, 0.046), (0.178, 0.052), (0.205, 0.062), (0.235, 0.064),
    (0.262, 0.055), (0.280, 0.034), (0.287, 0.000),
)
HAND_FLATTEN = 0.72            # mitten thickness relative to width
HAND_START = 0.175             # axial distance where the mitten begins
THUMB_RADII = (0.020, 0.026, 0.018)
THUMB_OFFSET = (0.205, -0.040)  # (axial, towards-front) offset from the arm axis
ARM_SEGMENTS = 16

HIP = (0.118, 0.000, 0.185)     # left hip pivot
KNEE_Z = 0.110
ANKLE_Z = 0.060
LEG_PROFILE = (                 # along -Z from the hip pivot
    (-0.050, 0.000), (-0.043, 0.032), (-0.025, 0.050), (0.000, 0.054), (0.060, 0.052),
    (0.100, 0.050), (0.125, 0.045), (0.140, 0.028), (0.145, 0.000),
)
LEG_SEGMENTS = 16
FOOT_CENTER = (0.122, -0.042, 0.040)
FOOT_RADII = (0.080, 0.112, 0.056)  # x, y (length), z
FOOT_SOLE_FLATTEN = 0.45            # lower half squash: flat sole, round top
FOOT_SEGMENTS = 16
FOOT_RINGS = 12

# -------------------------------------------------------------- sprout ----
# The sprout is what makes the silhouette "Nubi" rather than "an egg", so it is sized to read at 1.5-2 m
# in passthrough (leaves 1.5x, stem 1.3x of the first pass, which shrank to a few pixels side-on).
SPROUT_STEM_LENGTH = 0.110
SPROUT_STEM_RADIUS = 0.019
SPROUT_LEAF_LENGTH = 0.172
SPROUT_LEAF_WIDTH = 0.090
SPROUT_LEAF_THICKNESS = 0.016
SPROUT_LEAF_LIFT_DEG = 32.0     # leaves angle up from horizontal
SPROUT_LEAF_CURL = 0.036        # leaf tips curl upwards
SPROUT_LEAF_YAW_DEG = 18.0      # leaves splay slightly forward/back for a less flat silhouette
SPROUT_LEAF_ROLL_DEG = 50.0     # blades turn their faces towards the front so they read in front view

# ------------------------------------------------------------ palette ----
# sRGB 0..1. Warm coral reads well against neutral room interiors in passthrough MR
# and is visually far from the white/grey hooded-yeti look of Meta's Jolly.
COLOR_BODY = (0.949, 0.471, 0.380)        # #F27861 coral
COLOR_BODY_TOP = (0.965, 0.537, 0.427)    # slightly lighter crown for a soft top-light gradient
COLOR_BODY_SHADE = (0.851, 0.388, 0.322)  # underside
COLOR_CREAM = (1.000, 0.886, 0.769)       # belly + mittens
COLOR_CHEEK = (0.960, 0.290, 0.420)        # #F54A6B rose: a coral-ish pink vanished on coral skin
COLOR_SOLE = (0.808, 0.400, 0.337)
COLOR_EYE = (0.122, 0.090, 0.149)         # deep plum-black
COLOR_EYE_LOW = (0.290, 0.200, 0.360)     # lower-iris glow
COLOR_EYE_GLINT = (1.000, 1.000, 1.000)
COLOR_MOUTH = (0.329, 0.098, 0.153)
COLOR_TONGUE = (0.957, 0.471, 0.541)
COLOR_STEM = (0.290, 0.580, 0.240)
COLOR_LEAF = (0.365, 0.733, 0.290)        # #5DBB4A: saturated enough to hold against bright passthrough
COLOR_LEAF_RIB = (0.260, 0.560, 0.200)

TEXTURE_SIZE = 1024
TEXTURE_DILATE_PX = 8
UV_ISLAND_MARGIN = 0.006
UV_MAIN_SCALE = 0.90           # smart-project islands are shrunk into [0, 0.9]^2 ...
UV_MOUTH_RECT = (0.905, 0.905, 0.090, 0.090)  # ... to free a fixed rect (x, y, w, h) for the mouth grid

ROUGHNESS_SKIN = 0.62
ROUGHNESS_GLOSS = 0.12

# -------------------------------------------------------------- naming ----
MESH_NAME = "Nubi_Mesh"
ARMATURE_NAME = "Nubi_Rig"
MATERIAL_SKIN = "M_Nubi_Skin"
MATERIAL_GLOSS = "M_Nubi_Gloss"
TEXTURE_NAME = "T_Nubi_Albedo"

# Part ids stored in the "nubi_part" face attribute (drive texturing + validation).
PART_BODY, PART_ARM, PART_LEG, PART_FOOT, PART_EYE, PART_MOUTH, PART_STEM, PART_LEAF, PART_THUMB = range(9)

FPS = 30
MAX_INFLUENCES = 4

FRAMING_HEIGHT = 1.0
