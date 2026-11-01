"""Shape keys: mouth visemes/expressions and eye blinks, authored on the joined mesh.

Mouth shapes are re-projected onto the face for every key, so open/wide shapes hug the curved head
instead of cutting into it. Visemes follow the uLipSync A/I/U/E/O(+N/sil) set the voice pipeline
feeds (critic.md C6: shape keys, no jaw bone).
"""

from mathutils import Vector

from . import config as C
from .geometry import AUX_ATTR, PART_ATTR, mouth_outline, project_to_body

# (half_width, corner_lift, upper_bulge, lower_drop, thickness, roundness); see geometry.mouth_outline.
# The resting/sil lines are ~1.5x the open-viseme lip thickness: at 0.0065 the neutral mouth was ~5 px
# tall on Quest 3 at 2 m and read as a scratch.
MOUTH_BASIS = (0.040, 0.009, 0.000, 0.003, 0.0098, 0.5)
MOUTH_SHAPES = {
    "sil":   (0.034, 0.003, 0.000, 0.000, 0.0083, 0.5),
    "A":     (0.044, 0.004, 0.006, 0.052, 0.0060, 0.55),
    "I":     (0.061, 0.012, 0.002, 0.010, 0.0060, 0.40),   # wide, flat slot: must not read as E
    "U":     (0.017, 0.000, 0.011, 0.013, 0.0050, 0.5),
    "E":     (0.046, 0.006, 0.006, 0.039, 0.0060, 0.5),    # narrower and more open than I
    "O":     (0.028, 0.000, 0.020, 0.032, 0.0060, 0.5),
    "Smile": (0.060, 0.026, 0.000, 0.018, 0.0060, 0.6),
    "Frown": (0.040, -0.016, 0.000, 0.000, 0.0065, 0.5),
}

BLINK_LINE_Y = -0.28        # closed-lid line height, fraction of eye half-height
BLINK_SAG = 0.12            # gentle downward curve of the closed lid
HAPPY_LINE_Y = -0.10
HAPPY_ARCH = 0.55           # "^" arch height for the happy-eyes key
CLOSED_THICKNESS = 0.11     # remaining lid thickness as a fraction of eye half-height
CLOSED_DEPTH_SCALE = 0.70   # sink the closed lid towards the face so it doesn't ridge out
WIDE_SCALE = 1.15           # Wide_Eyes: dome grows in the face plane ...
WIDE_DEPTH_SCALE = 1.06     # ... and a little out of it, so it doesn't look flattened
SAD_LID_INNER = 0.62        # Sad_Eyes lid line (fraction of half-height): high at the inner corner ...
SAD_LID_OUTER = -0.05       # ... dropping to the outer corner, the "puppy" droop
SAD_DROP = 0.08             # whole eye sinks a little (fraction of half-height)


def _part_vertices(mesh, part):
    """Vertex indices used by faces of the given part."""
    face_part = mesh.attributes[PART_ATTR].data
    out = set()
    for poly in mesh.polygons:
        if face_part[poly.index].value == part:
            out.update(poly.vertices)
    return sorted(out)


def _eye_side(mesh, vi):
    return "L" if mesh.vertices[vi].co.x > 0 else "R"


def _closed_eye_local(local, line_y, curve, half_h):
    """Squash an eye-local point onto a curved lid line (curve > 0 arches up, < 0 sags)."""
    nx = local.x / C.EYE_RADIUS_W
    centre = (line_y + curve * (1.0 - nx * nx)) * half_h
    y = centre + local.y * CLOSED_THICKNESS
    return Vector((local.x, y, local.z * CLOSED_DEPTH_SCALE))


def _wide_eye_local(local):
    return Vector((local.x * WIDE_SCALE, local.y * WIDE_SCALE, local.z * WIDE_DEPTH_SCALE))


def _sad_eye_local(local, inner_sign, half_h):
    """Squash the dome under a lid line that slopes down to the outer corner (inner_sign is +1 where
    local +x is the inner corner). Compressing instead of clamping keeps the dome and its glint smooth."""
    t_inner = max(-1.0, min(1.0, inner_sign * local.x / C.EYE_RADIUS_W))
    lid = (SAD_LID_OUTER + (SAD_LID_INNER - SAD_LID_OUTER) * 0.5 * (1.0 + t_inner)) * half_h
    k = (lid + half_h) / (2.0 * half_h)
    y = -half_h + (local.y + half_h) * k
    return Vector((local.x, y - SAD_DROP * half_h, local.z * (0.75 + 0.25 * k)))


def _inner_sign(frame, side):
    """Eye-local +x points towards the nose (inner corner) -> +1, towards the ear -> -1."""
    world_x = (frame.to_3x3() @ Vector((1.0, 0.0, 0.0))).x
    towards_nose = -1.0 if side == "L" else 1.0
    return 1.0 if world_x * towards_nose > 0 else -1.0


def add_shape_keys(obj, body_bvh, eye_frames):
    mesh = obj.data
    if mesh.shape_keys is None:
        obj.shape_key_add(name="Basis", from_mix=False)
    aux = mesh.attributes[AUX_ATTR].data

    mouth = _part_vertices(mesh, C.PART_MOUTH)
    for name, shape in MOUTH_SHAPES.items():
        key = obj.shape_key_add(name=name, from_mix=False)
        for vi in mouth:
            u, v, _ = aux[vi].vector
            x, dz = mouth_outline(shape, u, v)
            loc, _n = project_to_body(body_bvh, x, C.MOUTH_CENTER_Z + dz, C.MOUTH_SURFACE_OFFSET)
            key.data[vi].co = loc

    eyes = _part_vertices(mesh, C.PART_EYE)
    eye_keys = (
        ("Blink_L", ("L",), BLINK_LINE_Y, -BLINK_SAG),
        ("Blink_R", ("R",), BLINK_LINE_Y, -BLINK_SAG),
        ("Happy_Eyes", ("L", "R"), HAPPY_LINE_Y, HAPPY_ARCH),
    )
    for name, sides, line_y, curve in eye_keys:
        key = obj.shape_key_add(name=name, from_mix=False)
        for vi in eyes:
            side = _eye_side(mesh, vi)
            if side not in sides:
                continue
            frame = eye_frames[side]
            local = frame.inverted() @ mesh.vertices[vi].co
            key.data[vi].co = frame @ _closed_eye_local(local, line_y, curve, C.EYE_RADIUS_H)

    for name in ("Wide_Eyes", "Sad_Eyes"):
        key = obj.shape_key_add(name=name, from_mix=False)
        for vi in eyes:
            side = _eye_side(mesh, vi)
            frame = eye_frames[side]
            local = frame.inverted() @ mesh.vertices[vi].co
            if name == "Wide_Eyes":
                new = _wide_eye_local(local)
            else:
                new = _sad_eye_local(local, _inner_sign(frame, side), C.EYE_RADIUS_H)
            key.data[vi].co = frame @ new
    return [k.name for k in mesh.shape_keys.key_blocks]
