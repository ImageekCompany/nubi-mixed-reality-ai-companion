"""Preview renders (EEVEE, Workbench fallback) and contact sheets composed with numpy."""

import os
from math import radians

import bpy
import numpy as np
from mathutils import Vector

from . import config as C

PREVIEW_SIZE = 800
SHEET_CELL = 400
BACKGROUND = (0.80, 0.83, 0.87)
FLOOR_COLOR = (0.86, 0.87, 0.89)
LABEL_COLOR = (0.15, 0.15, 0.18)
LABEL_DEPTH = 0.5  # beyond the camera's 0.1 m clip start


def setup_stage():
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    world = bpy.data.worlds.new("NubiPreviewWorld")
    scene.world = world
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (*BACKGROUND, 1.0)
    bg.inputs["Strength"].default_value = 0.55

    floor_mesh = bpy.data.meshes.new("PreviewFloor")
    s = 6.0
    floor_mesh.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("PreviewFloor", floor_mesh)
    mat = bpy.data.materials.new("M_PreviewFloor")
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*FLOOR_COLOR, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    floor_mesh.materials.append(mat)
    scene.collection.objects.link(floor)

    def area_light(name, energy, size, location, color=(1, 1, 1)):
        light = bpy.data.lights.new(name, "AREA")
        light.energy = energy
        light.size = size
        light.color = color
        obj = bpy.data.objects.new(name, light)
        obj.location = location
        direction = Vector((0, 0, 0.55)) - Vector(location)
        obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(obj)

    area_light("Key", 260, 2.0, (-1.6, -2.2, 2.4), (1.0, 0.96, 0.92))
    area_light("Fill", 90, 3.0, (2.2, -1.8, 1.2), (0.90, 0.94, 1.0))
    area_light("Rim", 160, 1.5, (0.8, 2.4, 2.0))

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def aim_camera(cam, target, azimuth_deg, elevation_deg, distance, lens=60.0):
    """Azimuth 0 = in front of Nubi (camera on -Y), positive = towards Nubi's left (+X)."""
    az, el = radians(azimuth_deg), radians(elevation_deg)
    from math import cos, sin
    offset = Vector((sin(az) * cos(el), -cos(az) * cos(el), sin(el))) * distance
    cam.location = Vector(target) + offset
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens


def render_still(path, size=PREVIEW_SIZE, height=None):
    scene = bpy.context.scene
    scene.render.resolution_x = size
    scene.render.resolution_y = height or size
    scene.render.resolution_percentage = 100
    scene.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)


def label_object(cam, text):
    """A text object glued to the bottom of the camera frame (cheap way to caption sheet cells)."""
    curve = bpy.data.curves.new("Label", "FONT")
    curve.body = text
    curve.align_x = "CENTER"
    obj = bpy.data.objects.new("Label", curve)
    mat = bpy.data.materials.new("M_Label")
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*LABEL_COLOR, 1.0)
    bsdf.inputs["Roughness"].default_value = 1.0
    curve.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = cam
    depth = LABEL_DEPTH
    half_height = 36.0 / 2.0 / cam.data.lens * depth  # frame half-height at `depth` in front of the lens
    curve.size = half_height * 0.10
    obj.location = (0, -half_height * 0.88, -depth)
    return obj


def remove_object(obj):
    data = obj.data
    bpy.data.objects.remove(obj)
    if data is not None and data.users == 0:
        if isinstance(data, bpy.types.Curve):
            bpy.data.curves.remove(data)


def load_rgb(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)


def compose_sheet(cell_paths, columns, out_path):
    """Tile equally sized PNGs (row-major, top-left first) into one PNG."""
    cells = [load_rgb(p) for p in cell_paths]
    h, w = cells[0].shape[:2]
    rows = (len(cells) + columns - 1) // columns
    sheet = np.ones((rows * h, columns * w, 4), dtype=np.float32)
    sheet[..., :3] = BACKGROUND
    for i, cell in enumerate(cells):
        r, c = divmod(i, columns)
        # Blender pixel buffers are bottom-up: row 0 of the sheet is its bottom.
        y0 = (rows - 1 - r) * h
        sheet[y0:y0 + h, c * w:(c + 1) * w] = cell
    img = bpy.data.images.new("Sheet", columns * w, rows * h, alpha=False)
    img.pixels.foreach_set(sheet.ravel())
    img.filepath_raw = out_path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    for p in cell_paths:
        os.remove(p)
