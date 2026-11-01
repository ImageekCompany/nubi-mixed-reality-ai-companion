"""Procedural mesh parts for Nubi.

Every part is a closed surface of revolution (quad rings + triangle-fan poles) built as a coarse
cage, then Catmull-Clark subdivided and applied. Each part writes two attributes that survive
joining and subdivision:
  nubi_part (FACE, INT)          which body part a face belongs to (texturing, validation)
  nubi_aux  (POINT, FLOAT_VECTOR) part-local coordinates used by the texture painter and shape keys
"""

from math import cos, sin, pi, radians, exp

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

from . import config as C

PART_ATTR = "nubi_part"
AUX_ATTR = "nubi_aux"


# ------------------------------------------------------------ helpers ----

def frame_matrix(origin, axis_x, axis_y, axis_z):
    """4x4 matrix whose columns are the given (orthonormalised by caller) axes."""
    m = Matrix.Identity(4)
    for row in range(3):
        m[row][0] = axis_x[row]
        m[row][1] = axis_y[row]
        m[row][2] = axis_z[row]
        m[row][3] = origin[row]
    return m


def orthonormal_frame(z_axis, up_hint=Vector((0, 0, 1))):
    """Right-handed frame with Z along z_axis and Y as close to up_hint as possible."""
    z = Vector(z_axis).normalized()
    x = up_hint.cross(z)
    if x.length < 1e-6:
        x = Vector((1, 0, 0)).cross(z)
    x.normalize()
    y = z.cross(x).normalized()
    return x, y, z


def smooth_profile(points, a):
    """Piecewise Catmull-Rom interpolation of (axial, radius) control points."""
    if a <= points[0][0]:
        return points[0][1]
    if a >= points[-1][0]:
        return points[-1][1]
    for i in range(len(points) - 1):
        a0, r0 = points[i]
        a1, r1 = points[i + 1]
        if a0 <= a <= a1:
            t = (a - a0) / (a1 - a0)
            rm = points[i - 1][1] if i > 0 else r0
            rp = points[i + 2][1] if i + 2 < len(points) else r1
            t2, t3 = t * t, t * t * t
            return 0.5 * ((2 * r0) + (-rm + r1) * t + (2 * rm - 5 * r0 + 4 * r1 - rp) * t2
                          + (-rm + 3 * r0 - 3 * r1 + rp) * t3)
    return 0.0


def build_revolution(name, rings, pole_start, pole_end, part, ring_aux=None, pole_aux=(None, None)):
    """Create a mesh object from rings of points (each ring has the same count) and two poles.

    ring_aux / pole_aux supply nubi_aux per vertex (same nesting as rings / poles)."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    aux_layer = bm.verts.layers.float_vector.new(AUX_ATTR)
    part_layer = bm.faces.layers.int.new(PART_ATTR)

    def add_vert(co, aux):
        v = bm.verts.new(co)
        v[aux_layer] = aux if aux is not None else (0.0, 0.0, 0.0)
        return v

    ring_verts = []
    for ri, ring in enumerate(rings):
        ring_verts.append([add_vert(co, ring_aux[ri][si] if ring_aux else None) for si, co in enumerate(ring)])
    vs = add_vert(pole_start, pole_aux[0])
    ve = add_vert(pole_end, pole_aux[1])
    n = len(rings[0])

    def face(verts):
        f = bm.faces.new(verts)
        f[part_layer] = part

    for i in range(n):
        face((vs, ring_verts[0][(i + 1) % n], ring_verts[0][i]))
    for r in range(len(rings) - 1):
        a, b = ring_verts[r], ring_verts[r + 1]
        for i in range(n):
            face((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    last = ring_verts[-1]
    for i in range(n):
        face((ve, last[i], last[(i + 1) % n]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def apply_subsurf(obj, levels):
    if levels <= 0:
        return
    mod = obj.modifiers.new("Subsurf", "SUBSURF")
    mod.levels = levels
    mod.render_levels = levels
    mod.uv_smooth = "PRESERVE_BOUNDARIES"
    with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def shade_smooth(obj):
    obj.data.shade_smooth()


def finish_part(obj, levels):
    apply_subsurf(obj, levels)
    shade_smooth(obj)
    return obj


# --------------------------------------------------------------- body ----

def body_ring(phi, segments):
    s = (sin(phi) + 1.0) * 0.5
    zc = (C.BODY_Z_BOTTOM + C.BODY_Z_TOP) * 0.5
    hz = (C.BODY_Z_TOP - C.BODY_Z_BOTTOM) * 0.5
    c = max(cos(phi), 0.0)
    c = c ** (C.BODY_BOTTOM_FULLNESS if phi < 0 else C.BODY_TOP_FULLNESS)
    r = C.BODY_RADIUS_X * c * (1.0 - C.BODY_TAPER * s * s)
    z = zc + hz * sin(phi) + C.BODY_TIP * s ** 6
    belly = exp(-((s - 0.38) / 0.22) ** 2)
    ring = []
    for i in range(segments):
        th = 2 * pi * i / segments
        x = r * cos(th)
        y = r * C.BODY_DEPTH_RATIO * sin(th)
        if y < 0:
            y -= C.BODY_FRONT_BULGE * belly * (sin(th) ** 2) * c
        ring.append(Vector((x, y, z)))
    return ring


def build_body():
    rings = []
    for j in range(1, C.BODY_RINGS):
        phi = -pi / 2 + pi * j / C.BODY_RINGS
        rings.append(body_ring(phi, C.BODY_SEGMENTS))
    obj = build_revolution("Body", rings,
                           Vector((0, 0, C.BODY_Z_BOTTOM)),
                           Vector((0, 0, C.BODY_Z_TOP + C.BODY_TIP)), C.PART_BODY)
    return finish_part(obj, C.SUBSURF_LEVELS_BODY)


def body_bvh(body_obj):
    return BVHTree.FromObject(body_obj, bpy.context.evaluated_depsgraph_get())


def project_to_body(bvh, x, z, offset=0.0):
    """Ray from the front (-Y) onto the body; returns (location, normal)."""
    hit, normal, _idx, _dist = bvh.ray_cast(Vector((x, -2.0, z)), Vector((0, 1, 0)))
    if hit is None:
        raise RuntimeError(f"face projection missed the body at x={x:.3f} z={z:.3f}")
    return hit + normal * offset, normal


# --------------------------------------------------------------- limbs ----

def tube_rings(profile, frame, segments, flatten_fn=None):
    """Rings along local +Y of `frame` following an (axial, radius) profile.

    flatten_fn(axial) scales the local Z (thickness) of the cross-section."""
    axial_samples = [a for a, _r in profile[1:-1]]
    rings, aux = [], []
    for a in axial_samples:
        r = smooth_profile(profile, a)
        flat = flatten_fn(a) if flatten_fn else 1.0
        ring, ring_aux = [], []
        for i in range(segments):
            th = 2 * pi * i / segments
            local = Vector((r * cos(th), a, r * sin(th) * flat))
            ring.append(frame @ local)
            ring_aux.append((a, cos(th), sin(th)))
        rings.append(ring)
        aux.append(ring_aux)
    p0 = frame @ Vector((0, profile[0][0], 0))
    p1 = frame @ Vector((0, profile[-1][0], 0))
    return rings, aux, p0, p1, ((profile[0][0], 0, 0), (profile[-1][0], 0, 0))


def mirror_x(v):
    return Vector((-v[0], v[1], v[2]))


def arm_frame(side):
    """Local Y along the arm, local Z = palm normal (towards the body), at the shoulder."""
    shoulder = Vector(C.SHOULDER)
    direction = Vector(C.ARM_DIRECTION).normalized()
    if side == "R":
        shoulder, direction = mirror_x(shoulder), mirror_x(direction)
    inward = Vector((-1 if side == "L" else 1, 0, 0))
    palm = (inward - direction * inward.dot(direction)).normalized()
    x_axis = direction.cross(palm).normalized()
    return frame_matrix(shoulder, x_axis, direction, palm)


def build_arm(side):
    frame = arm_frame(side)

    def flatten(a):
        t = min(max((a - C.HAND_START) / 0.03, 0.0), 1.0)
        return 1.0 + (C.HAND_FLATTEN - 1.0) * t

    rings, aux, p0, p1, pole_aux = tube_rings(C.ARM_PROFILE, frame, C.ARM_SEGMENTS, flatten)
    obj = build_revolution(f"Arm_{side}", rings, p0, p1, C.PART_ARM, aux, pole_aux)
    return finish_part(obj, C.SUBSURF_LEVELS_LIMB)


def ellipsoid_rings(radii, segments, rings_count, lower_squash=1.0):
    """Rings of an ellipsoid along local Z (poles at -Z/+Z). lower_squash flattens local -Y."""
    rings, aux = [], []
    for j in range(1, rings_count):
        phi = -pi / 2 + pi * j / rings_count
        ring, ring_aux = [], []
        for i in range(segments):
            th = 2 * pi * i / segments
            nx, ny, nz = cos(phi) * cos(th), cos(phi) * sin(th), sin(phi)
            y = ny * radii[1]
            if y < 0:
                y *= lower_squash
            ring.append(Vector((nx * radii[0], y, nz * radii[2])))
            ring_aux.append((nx, ny, nz))
        rings.append(ring)
        aux.append(ring_aux)
    poles = (Vector((0, 0, -radii[2])), Vector((0, 0, radii[2])))
    return rings, aux, poles, ((0, 0, -1), (0, 0, 1))


def build_ellipsoid(name, frame, radii, segments, rings_count, part, levels, lower_squash=1.0):
    rings, aux, poles, pole_aux = ellipsoid_rings(radii, segments, rings_count, lower_squash)
    world_rings = [[frame @ p for p in ring] for ring in rings]
    obj = build_revolution(name, world_rings, frame @ poles[0], frame @ poles[1], part, aux, pole_aux)
    return finish_part(obj, levels)


def build_thumb(side):
    frame = arm_frame(side)
    axial, front = C.THUMB_OFFSET
    arm_x, arm_y, arm_z = (frame.col[i].xyz for i in range(3))
    forward = Vector((0, -1, 0))
    fwd_local = (forward - arm_y * forward.dot(arm_y)).normalized()
    center = frame.translation + arm_y * axial + fwd_local * (-front if front < 0 else front)
    # Thumb leans away from the arm axis, towards the front and slightly up.
    z_axis = (fwd_local * 0.8 - arm_y * 0.6).normalized()
    x, y, z = orthonormal_frame(z_axis, arm_z)
    return build_ellipsoid(f"Thumb_{side}", frame_matrix(center, x, y, z), C.THUMB_RADII,
                           10, 6, C.PART_THUMB, C.SUBSURF_LEVELS_SMALL)


def leg_frame(side):
    hip = Vector(C.HIP)
    if side == "R":
        hip = mirror_x(hip)
    # Local Y points down the leg; X stays world X so Z = X x Y = world +Y.
    return frame_matrix(hip, Vector((1, 0, 0)), Vector((0, 0, -1)), Vector((0, 1, 0)))


def build_leg(side):
    rings, aux, p0, p1, pole_aux = tube_rings(C.LEG_PROFILE, leg_frame(side), C.LEG_SEGMENTS)
    obj = build_revolution(f"Leg_{side}", rings, p0, p1, C.PART_LEG, aux, pole_aux)
    return finish_part(obj, C.SUBSURF_LEVELS_LIMB)


def build_foot(side):
    center = Vector(C.FOOT_CENTER)
    if side == "R":
        center = mirror_x(center)
    # Local Z = toe direction (-Y world), local Y = up, so lower_squash flattens the sole.
    frame = frame_matrix(center, Vector((-1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0)))
    radii = (C.FOOT_RADII[0], C.FOOT_RADII[2], C.FOOT_RADII[1])
    return build_ellipsoid(f"Foot_{side}", frame, radii, C.FOOT_SEGMENTS, C.FOOT_RINGS,
                           C.PART_FOOT, C.SUBSURF_LEVELS_SMALL, lower_squash=C.FOOT_SOLE_FLATTEN)


# ---------------------------------------------------------------- eyes ----

def eye_frame(side, bvh):
    x = C.FACE_EYE_X if side == "L" else -C.FACE_EYE_X
    surface, normal = project_to_body(bvh, x, C.FACE_EYE_Z)
    ex, ey, ez = orthonormal_frame(normal)
    tilt = radians(C.EYE_TILT_DEG) * (-1 if side == "L" else 1)
    rot = Matrix.Rotation(tilt, 3, ez)
    ex, ey = rot @ ex, rot @ ey
    origin = surface - normal * (C.EYE_DEPTH * C.EYE_EMBED)
    return frame_matrix(origin, ex, ey, ez), surface, normal


def build_eye(side, bvh):
    frame, surface, normal = eye_frame(side, bvh)
    radii = (C.EYE_RADIUS_W, C.EYE_RADIUS_H, C.EYE_DEPTH)
    obj = build_ellipsoid(f"Eye_{side}", frame, radii, C.EYE_SEGMENTS, C.EYE_RINGS, C.PART_EYE, 0)
    pivot = surface - normal * C.EYE_PIVOT_DEPTH
    return obj, frame, pivot


# --------------------------------------------------------------- mouth ----
# A mouth shape is (half_width, corner_lift, upper_bulge, lower_drop, thickness, roundness).
# corner_lift > 0 raises the corners relative to the middle (smile), < 0 is a frown.

def mouth_outline(shape, u, v):
    half_w, lift, upper, lower, thick, roundness = shape
    p = max(1.0 - u * u, 0.0) ** roundness
    mid = -lift * (1.0 - u * u)
    t = thick * (0.35 + 0.65 * (1.0 - u * u) ** 0.5)
    y_up = mid + upper * p + t * 0.5
    y_lo = mid - lower * p - t * 0.5
    return u * half_w, y_lo + (y_up - y_lo) * v


def mouth_grid_params():
    params = []
    for r in range(C.MOUTH_ROWS + 1):
        v = r / C.MOUTH_ROWS
        for c in range(C.MOUTH_COLUMNS + 1):
            u = -1.0 + 2.0 * c / C.MOUTH_COLUMNS
            u = sin(u * pi / 2)  # denser near the corners where the outline turns
            params.append((u, v))
    return params


def mouth_positions(shape, bvh):
    out = []
    for u, v in mouth_grid_params():
        x, dz = mouth_outline(shape, u, v)
        loc, _n = project_to_body(bvh, x, C.MOUTH_CENTER_Z + dz, C.MOUTH_SURFACE_OFFSET)
        out.append(loc)
    return out


def build_mouth(shape, bvh):
    me = bpy.data.meshes.new("Mouth")
    bm = bmesh.new()
    aux_layer = bm.verts.layers.float_vector.new(AUX_ATTR)
    part_layer = bm.faces.layers.int.new(PART_ATTR)
    verts = []
    for (u, v), co in zip(mouth_grid_params(), mouth_positions(shape, bvh)):
        vert = bm.verts.new(co)
        vert[aux_layer] = (u, v, 0.0)
        verts.append(vert)
    cols = C.MOUTH_COLUMNS + 1
    for r in range(C.MOUTH_ROWS):
        for c in range(C.MOUTH_COLUMNS):
            a = r * cols + c
            f = bm.faces.new((verts[a], verts[a + 1], verts[a + 1 + cols], verts[a + cols]))
            f[part_layer] = C.PART_MOUTH
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    # Outward normals must face -Y (front).
    if sum(f.normal.y for f in bm.faces) > 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Mouth", me)
    bpy.context.scene.collection.objects.link(obj)
    shade_smooth(obj)
    return obj


# -------------------------------------------------------------- sprout ----

def crown_point(bvh):
    hit, normal, _i, _d = bvh.ray_cast(Vector((0, 0, 2.0)), Vector((0, 0, -1)))
    return hit


def sprout_frame(bvh):
    base = crown_point(bvh) - Vector((0, 0, 0.03))
    up = Vector((0, 0.12, 1.0)).normalized()  # leans back a touch so it doesn't hide the face
    return base, up


def build_stem(bvh):
    base, up = sprout_frame(bvh)
    length = C.SPROUT_STEM_LENGTH + 0.03
    r = C.SPROUT_STEM_RADIUS
    profile = ((-0.008, 0.0), (0.0, r * 1.3), (0.03, r * 1.1), (length * 0.7, r),
               (length, r * 0.95), (length + 0.008, 0.0))
    x_axis = Vector((1, 0, 0))
    z_axis = x_axis.cross(up).normalized()
    frame = frame_matrix(base, x_axis, up, z_axis)
    rings, aux, p0, p1, pole_aux = tube_rings(profile, frame, 8)
    obj = build_revolution("Stem", rings, p0, p1, C.PART_STEM, aux, pole_aux)
    return finish_part(obj, C.SUBSURF_LEVELS_SMALL), base + up * length


def build_leaf(side, tip):
    sign = 1 if side == "L" else -1
    lift = radians(C.SPROUT_LEAF_LIFT_DEG)
    yaw = radians(C.SPROUT_LEAF_YAW_DEG) * sign
    direction = Matrix.Rotation(yaw, 3, "Z") @ Vector((sign * cos(lift), 0, sin(lift)))
    length = C.SPROUT_LEAF_LENGTH
    profile = [(-0.004, 0.0)]
    steps = 9
    for k in range(steps + 1):
        a = length * k / steps
        t = a / length
        profile.append((a, C.SPROUT_LEAF_WIDTH * 0.5 * (sin(pi * min(t * 0.92 + 0.04, 1.0)) ** 0.8) + 0.002))
    profile.append((length + 0.006, 0.0))
    normal = direction.cross(Vector((0, 1, 0))).normalized() * sign
    if normal.z < 0:
        normal = -normal
    # Roll the blade about its own axis so its face turns towards the front (-Y).
    normal = Matrix.Rotation(radians(C.SPROUT_LEAF_ROLL_DEG) * sign, 3, direction) @ normal
    if normal.y > 0:
        normal = Matrix.Rotation(-2 * radians(C.SPROUT_LEAF_ROLL_DEG) * sign, 3, direction) @ normal
    x_axis = direction.cross(normal).normalized()
    frame = frame_matrix(tip, x_axis, direction, normal)
    flat = C.SPROUT_LEAF_THICKNESS / C.SPROUT_LEAF_WIDTH
    rings, aux, p0, p1, pole_aux = tube_rings(profile, frame, 10, lambda a: flat)
    # Curl the blade upwards towards its tip.
    curled = []
    for ring, ring_aux in zip(rings, aux):
        a = ring_aux[0][0]
        lift_off = normal * C.SPROUT_LEAF_CURL * (max(a, 0.0) / length) ** 2
        curled.append([p + lift_off for p in ring])
    p1 = p1 + normal * C.SPROUT_LEAF_CURL
    obj = build_revolution(f"Leaf_{side}", curled, p0, p1, C.PART_LEAF, aux, pole_aux)
    return finish_part(obj, C.SUBSURF_LEVELS_SMALL)
