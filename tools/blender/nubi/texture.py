"""UVs, a procedurally painted albedo texture, and the two URP-friendly materials.

The albedo is painted in Python: every triangle is rasterised in UV space, the texel's surface
position / part-local coordinates are interpolated, and a per-part colour function is evaluated.
No Cycles bake, so the result is deterministic and fast. URP Lit ignores vertex colours, which is
why colour lives in a texture rather than in a colour attribute.
"""

import os
from math import radians

import bpy
import numpy as np

from . import config as C
from .geometry import AUX_ATTR, PART_ATTR

GLOSS_PARTS = (C.PART_EYE,)  # a glossy mouth catches floor reflections and reads as teeth


# --------------------------------------------------------------- UVs ----

def unwrap(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=radians(60.0), island_margin=C.UV_ISLAND_MARGIN,
                             area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    _place_mouth_uvs(obj.data)


def _place_mouth_uvs(mesh):
    """The mouth's basis shape is a thin closed line, so a geometric unwrap would give it a sliver
    of texture that smears when visemes open it. Map its (u, v) grid onto a fixed square instead."""
    uv_layer = mesh.uv_layers.active.data
    uv = np.empty(len(uv_layer) * 2, dtype=np.float32)
    uv_layer.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2) * C.UV_MAIN_SCALE
    aux = mesh.attributes[AUX_ATTR].data
    part = mesh.attributes[PART_ATTR].data
    x0, y0, w, h = C.UV_MOUTH_RECT
    for poly in mesh.polygons:
        if part[poly.index].value != C.PART_MOUTH:
            continue
        for li in poly.loop_indices:
            u, v, _ = aux[mesh.loops[li].vertex_index].vector
            uv[li] = (x0 + (u + 1.0) * 0.5 * w, y0 + v * h)
    uv_layer.foreach_set("uv", uv.ravel())


# ------------------------------------------------------------ helpers ----

def _srgb(c):
    return np.asarray(c, dtype=np.float32)


def _smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _mix(a, b, t):
    t = t[:, None]
    return a * (1.0 - t) + b * t


# ------------------------------------------------------ colour per part ----

CHEEK_OPACITY = 0.90   # 0.65 with a paler pink was invisible at room distance


def color_body(pos, _aux, cheeks):
    x, y, z = pos[:, 0], pos[:, 1], pos[:, 2]
    col = _mix(np.broadcast_to(_srgb(C.COLOR_BODY_SHADE), pos.shape), _srgb(C.COLOR_BODY),
               _smoothstep(C.BODY_Z_BOTTOM, 0.36, z))
    col = _mix(col, _srgb(C.COLOR_BODY_TOP), _smoothstep(0.55, C.BODY_Z_TOP, z) * 0.85)
    d = (x / C.BELLY_RADIUS_X) ** 2 + ((z - C.BELLY_CENTER_Z) / C.BELLY_RADIUS_Z) ** 2
    belly = _smoothstep(1.04, 0.96, d) * _smoothstep(-0.05, -0.15, y)
    col = _mix(col, _srgb(C.COLOR_CREAM), belly)
    blush = np.zeros(len(pos), dtype=np.float32)
    for c in cheeks:
        dist2 = np.sum((pos - np.asarray(c, dtype=np.float32)) ** 2, axis=1)
        blush = np.maximum(blush, np.exp(-dist2 / (C.CHEEK_RADIUS ** 2)))
    return _mix(col, _srgb(C.COLOR_CHEEK), CHEEK_OPACITY * _smoothstep(0.15, 0.75, blush))


def color_arm(pos, aux, _cheeks):
    a = aux[:, 0]
    col = np.broadcast_to(_srgb(C.COLOR_BODY), pos.shape)
    return _mix(col, _srgb(C.COLOR_CREAM), _smoothstep(C.HAND_START - 0.004, C.HAND_START + 0.004, a))


def color_thumb(pos, _aux, _cheeks):
    return np.broadcast_to(_srgb(C.COLOR_CREAM), pos.shape).copy()


def color_leg(pos, _aux, _cheeks):
    return _mix(np.broadcast_to(_srgb(C.COLOR_BODY_SHADE), pos.shape), _srgb(C.COLOR_BODY),
                _smoothstep(0.03, 0.12, pos[:, 2]))


def color_foot(pos, aux, _cheeks):
    sole = _smoothstep(-0.30, -0.45, aux[:, 1])
    return _mix(np.broadcast_to(_srgb(C.COLOR_BODY), pos.shape), _srgb(C.COLOR_SOLE), sole)


def color_eye(pos, aux, _cheeks):
    nx, ny, nz = aux[:, 0], aux[:, 1], aux[:, 2]
    col = _mix(np.broadcast_to(_srgb(C.COLOR_EYE), pos.shape), _srgb(C.COLOR_EYE_LOW),
               _smoothstep(-0.15, -0.75, ny) * 0.8)
    front = _smoothstep(0.1, 0.4, nz)
    big = np.hypot((nx + 0.32) / 0.24, (ny - 0.38) / 0.28)
    small = np.hypot((nx - 0.30) / 0.11, (ny + 0.38) / 0.11)
    glint = np.maximum(_smoothstep(1.05, 0.9, big), _smoothstep(1.1, 0.85, small)) * front
    return _mix(col, _srgb(C.COLOR_EYE_GLINT), glint)


def color_mouth(pos, aux, _cheeks):
    u, v = aux[:, 0], aux[:, 1]
    tongue = _smoothstep(1.05, 0.9, (u / 0.62) ** 2 + (v / 0.42) ** 2)
    return _mix(np.broadcast_to(_srgb(C.COLOR_MOUTH), pos.shape), _srgb(C.COLOR_TONGUE), tongue)


def color_stem(pos, _aux, _cheeks):
    return np.broadcast_to(_srgb(C.COLOR_STEM), pos.shape).copy()


def color_leaf(pos, aux, _cheeks):
    a, across = aux[:, 0], aux[:, 1]
    col = _mix(np.broadcast_to(_srgb(C.COLOR_LEAF), pos.shape), _srgb(C.COLOR_STEM),
               _smoothstep(0.04, 0.0, a))
    rib = np.exp(-(across / 0.12) ** 2) * _smoothstep(0.0, 0.02, a) * _smoothstep(C.SPROUT_LEAF_LENGTH, 0.06, a)
    return _mix(col, _srgb(C.COLOR_LEAF_RIB), rib * 0.8)


PAINTERS = {
    C.PART_BODY: color_body, C.PART_ARM: color_arm, C.PART_THUMB: color_thumb,
    C.PART_LEG: color_leg, C.PART_FOOT: color_foot, C.PART_EYE: color_eye,
    C.PART_MOUTH: color_mouth, C.PART_STEM: color_stem, C.PART_LEAF: color_leaf,
}


# --------------------------------------------------------- rasteriser ----

def _gather_triangles(mesh):
    mesh.calc_loop_triangles()
    tris = mesh.loop_triangles
    n = len(tris)
    loops = np.empty(n * 3, dtype=np.int64)
    verts = np.empty(n * 3, dtype=np.int64)
    polys = np.empty(n, dtype=np.int64)
    tris.foreach_get("loops", loops)
    tris.foreach_get("vertices", verts)
    tris.foreach_get("polygon_index", polys)

    uv_all = np.empty(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get("uv", uv_all)
    co_all = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get("co", co_all)
    aux_all = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.attributes[AUX_ATTR].data.foreach_get("vector", aux_all)
    part_all = np.empty(len(mesh.polygons), dtype=np.int32)
    mesh.attributes[PART_ATTR].data.foreach_get("value", part_all)

    uv = uv_all.reshape(-1, 2)[loops].reshape(n, 3, 2)
    co = co_all.reshape(-1, 3)[verts].reshape(n, 3, 3)
    aux = aux_all.reshape(-1, 3)[verts].reshape(n, 3, 3)
    return uv, co, aux, part_all[polys]


def _rasterise(uv, co, aux, part, size):
    """Returns per-texel interpolated position, aux, part and a coverage mask."""
    pos_img = np.zeros((size, size, 3), dtype=np.float32)
    aux_img = np.zeros((size, size, 3), dtype=np.float32)
    part_img = np.full((size, size), -1, dtype=np.int32)
    px = uv * size - 0.5  # texel centres at integer coordinates
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
        eps = -0.02
        inside = (w0 >= eps) & (w1 >= eps) & (w2 >= eps)
        if not inside.any():
            continue
        w = np.stack((w0[inside], w1[inside], w2[inside]), axis=1)
        ys, xs = gy[inside], gx[inside]
        pos_img[ys, xs] = w @ co[t]
        aux_img[ys, xs] = w @ aux[t]
        part_img[ys, xs] = part[t]
    return pos_img, aux_img, part_img


def _dilate(rgb, covered, iterations):
    """Bleed colours into empty texels so mip-mapping/bilinear filtering never samples background."""
    rgb, covered = rgb.copy(), covered.copy()
    for _ in range(iterations):
        acc = np.zeros_like(rgb)
        cnt = np.zeros(covered.shape, dtype=np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            shifted_c = np.roll(np.roll(covered, dy, 0), dx, 1)
            shifted_rgb = np.roll(np.roll(rgb, dy, 0), dx, 1)
            acc += shifted_rgb * shifted_c[..., None]
            cnt += shifted_c
        fill = (~covered) & (cnt > 0)
        rgb[fill] = acc[fill] / cnt[fill][:, None]
        covered = covered | fill
    return rgb


def paint_texture(obj, cheeks, out_path, size=C.TEXTURE_SIZE):
    uv, co, aux, part = _gather_triangles(obj.data)
    pos_img, aux_img, part_img = _rasterise(uv, co, aux, part, size)
    rgb = np.zeros((size, size, 3), dtype=np.float32)
    for part_id, painter in PAINTERS.items():
        mask = part_img == part_id
        if mask.any():
            rgb[mask] = painter(pos_img[mask], aux_img[mask], cheeks)
    rgb = _dilate(rgb, part_img >= 0, C.TEXTURE_DILATE_PX)

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    img = bpy.data.images.new(C.TEXTURE_NAME, size, size, alpha=False)
    rgba = np.ones((size, size, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(rgb, 0.0, 1.0)
    img.pixels.foreach_set(rgba.ravel())  # row 0 = v 0, same convention as UVs
    img.filepath_raw = out_path
    img.file_format = "PNG"
    img.save()
    img.source = "FILE"
    img.filepath = out_path
    img.reload()
    return img, float((part_img >= 0).mean())


# ----------------------------------------------------------- materials ----

def make_material(name, image, roughness):
    mat = bpy.data.materials.new(name)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = 0.0
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.location = (-400, 200)
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def assign_materials(obj, image):
    skin = make_material(C.MATERIAL_SKIN, image, C.ROUGHNESS_SKIN)
    gloss = make_material(C.MATERIAL_GLOSS, image, C.ROUGHNESS_GLOSS)
    mesh = obj.data
    mesh.materials.clear()
    mesh.materials.append(skin)
    mesh.materials.append(gloss)
    part = np.empty(len(mesh.polygons), dtype=np.int32)
    mesh.attributes[PART_ATTR].data.foreach_get("value", part)
    idx = np.isin(part, GLOSS_PARTS).astype(np.int32)
    mesh.polygons.foreach_set("material_index", idx)
    mesh.update()
