"""
CORDON asset kit for Blender 5.2, run headless:

    blender --background --factory-startup --python Tools/Blender/assets/<asset>.py

Every model of the game is built by a script in Tools/Blender/assets/ that uses this kit, so the
whole art set can be rebuilt, tweaked and re-exported at any time.

What the kit gives:
- PALETTE: the bible's colors (Univers.md 9.4) as swatches of one shared 1024 texture
  (Assets/_Game/Art/Textures/T_Palette.png). RGB = color with a little painted grain, A = 1 - emission (opaque = no glow).
  Every model maps its faces into swatches, so the whole game uses ONE texture and ONE material.
- Skeleton / humanoid(): a body grown from joints with the Skin modifier, colored by an "outfit"
  (bone -> swatch), skinned to a matching armature with smooth joint weights.
- Part helpers (box, cyl, ball, ico, shard...): extra pieces (crust plates, the bosse, bags, weapons),
  either rigid on one bone or following the body.
- AO and grime baked into vertex colors (the shader multiplies them), plus an optional flood-mud line.
- Animation helpers: poses from readable angles ("leg forward 30 deg"), cycles, one-shots.
- export_character / export_props: FBX for Unity (Y up, -Z forward) + a preview PNG in Tools/Blender/previews.

Conventions (Blender side): Z up, the character faces -Y, its left side (.L) is +X, feet at Z = 0, meters.
"""

import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree
from mathutils.noise import noise as perlin

KIT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(KIT_DIR, "..", ".."))
ART = os.path.join(REPO, "Assets", "_Game", "Art")
PREVIEWS = os.path.join(KIT_DIR, "previews")
FPS = 30

# ---------------------------------------------------------------------------------------------
# Palette. Order is the texture layout: NEVER reorder or delete, only append (or fill a "spare").
# (name, hex, kind, emission 0..1)
# ---------------------------------------------------------------------------------------------
PALETTE = [
    # --- La Seve, the Ambres (0..)
    ("skin_ambre", "9EA09A", "skin", 0), ("skin_ambre_dark", "7C7F79", "skin", 0), ("skin_ambre_pale", "B9BAB3", "skin", 0),
    ("crust_dark", "8C4A12", "crust", 0.18), ("crust", "A85A18", "crust", 0.22), ("amber", "C26A1C", "crust", 0.38),
    ("amber_glow", "E58E2B", "emissive", 1.0), ("amber_glow_soft", "D07A22", "emissive", 0.55), ("veins", "221006", "flat", 0),
    ("ichor", "3A1A08", "flat", 0), ("mouth", "2A0E08", "flat", 0), ("mud_dry", "8A6A3A", "mud", 0),
    ("mud_wet", "5C5540", "mud", 0), ("crystal", "F0A040", "emissive", 0.85), ("crust_black", "3A2410", "crust", 0.05),
    ("teeth", "B8AE92", "flat", 0),
    # --- Human skin and hair (16..)
    ("skin_light", "C9A58A", "skin", 0), ("skin_mid", "A87B5E", "skin", 0), ("skin_dark", "6E4A36", "skin", 0),
    ("hair_dark", "2B2420", "fur", 0), ("hair_brown", "5A3E2B", "fur", 0), ("hair_grey", "8C8A86", "fur", 0),
    ("lips", "8A5A4E", "skin", 0), ("eye_white", "C8C4BA", "flat", 0), ("eye_dark", "1A1614", "flat", 0),
    ("stubble", "4A3E36", "skin", 0), ("spare_26", "808080", "flat", 0), ("spare_27", "808080", "flat", 0),
    ("spare_28", "808080", "flat", 0), ("spare_29", "808080", "flat", 0), ("spare_30", "808080", "flat", 0),
    ("spare_31", "808080", "flat", 0),
    # --- Cloth (32..)
    ("hoodie_charcoal", "3A3D40", "cloth", 0), ("hoodie_olive", "4D5240", "cloth", 0), ("vest_khaki", "6B6E4F", "cloth", 0),
    ("vest_dark", "3F4233", "cloth", 0), ("cargo_grey", "55585A", "cloth", 0), ("cargo_khaki", "6A6450", "cloth", 0),
    ("jeans", "3E4B5E", "cloth", 0), ("jeans_faded", "5D6B7C", "cloth", 0), ("shirt_white_dirty", "B8B2A2", "cloth", 0),
    ("shirt_beige", "9C8F74", "cloth", 0), ("shirt_red_faded", "7E3B33", "cloth", 0), ("shirt_blue_faded", "4F6475", "cloth", 0),
    ("shirt_green_faded", "556049", "cloth", 0), ("coat_brown", "5E4632", "cloth", 0), ("coat_grey", "5F6266", "cloth", 0),
    ("sweater_mustard", "8A7232", "cloth", 0), ("workwear_blue", "2F4F7A", "cloth", 0), ("workwear_blue_dark", "243D5E", "cloth", 0),
    ("reflective", "D9DEE3", "metal", 0.12), ("apron_white", "CFC9BC", "cloth", 0), ("apron_stain", "6B2A1E", "cloth", 0),
    ("bandana_red", "8E2F28", "cloth", 0), ("rag", "7A7266", "cloth", 0), ("canvas_olive", "4F5338", "cloth", 0),
    ("canvas_tan", "8A7A5A", "cloth", 0), ("bedroll_rust", "7A4A30", "cloth", 0), ("plate_carrier", "4A4C40", "cloth", 0),
    ("track_black", "26282B", "cloth", 0), ("sweater_wine", "5A2A30", "cloth", 0), ("coat_navy", "2C3442", "cloth", 0),
    ("tracksuit_teal", "2F5A5E", "cloth", 0), ("cloth_violet", "7E4FC7", "cloth", 0),
    # --- Leather, rubber (64..)
    ("leather_brown", "4A3423", "rubber", 0), ("leather_black", "222224", "rubber", 0), ("boot_brown", "3D2C1E", "rubber", 0),
    ("shoe_dark", "2A2A2C", "rubber", 0), ("glove_black", "1E1F21", "rubber", 0), ("rubber_black", "1A1B1C", "rubber", 0),
    ("strap_black", "232425", "rubber", 0), ("tire", "1C1C1E", "rubber", 0), ("sneaker_white", "A8A69E", "rubber", 0),
    ("leather_tan", "7A5A3A", "rubber", 0), ("spare_74", "808080", "flat", 0), ("spare_75", "808080", "flat", 0),
    ("spare_76", "808080", "flat", 0), ("spare_77", "808080", "flat", 0), ("spare_78", "808080", "flat", 0),
    ("spare_79", "808080", "flat", 0),
    # --- Metal (80..)
    ("metal_gun", "2A2C2F", "metal", 0), ("metal_dark", "3B3E42", "metal", 0), ("metal_worn", "6E7176", "metal", 0),
    ("steel", "9DA2A8", "metal", 0), ("steel_bright", "C9CDD2", "metal", 0), ("rust", "7D3E1F", "metal", 0),
    ("rust_dark", "4E2614", "metal", 0), ("brass", "A88A44", "metal", 0), ("gold", "F4C84A", "metal", 0.3),
    ("gold_dark", "B8902E", "metal", 0.12), ("military_green", "4A5236", "metal", 0), ("fuel_red", "8A2A1E", "metal", 0),
    ("car_white", "B5B4AC", "metal", 0), ("car_red", "7A2E26", "metal", 0), ("car_blue", "3A4E66", "metal", 0),
    ("car_green", "4A5A44", "metal", 0),
    # --- REGEN, factions, signals (96..)
    ("regen_white", "E9EEF0", "flat", 0), ("regen_grey", "A9B0B5", "flat", 0), ("regen_plate", "8E959B", "metal", 0),
    ("cyan", "2CB1C4", "emissive", 1.0), ("cyan_paint", "1F8A99", "flat", 0.15), ("violet", "7E4FC7", "flat", 0),
    ("red_danger", "B8322A", "flat", 0), ("red_blood", "7A1410", "flat", 0), ("green_safe", "5FD08A", "emissive", 0.6),
    ("toxic", "A6D23C", "emissive", 0.6), ("electric", "9ADCFF", "emissive", 1.0), ("fire_yellow", "FFC43D", "emissive", 1.0),
    ("fire_orange", "FF6A2A", "emissive", 1.0), ("smoke", "3A3A3A", "flat", 0), ("hazard_yellow", "B89A2E", "flat", 0),
    ("hazard_black", "1E1E1E", "flat", 0),
    # --- Decor (112..)
    ("concrete", "4B535C", "concrete", 0), ("concrete_light", "6E757C", "concrete", 0), ("concrete_dark", "353B42", "concrete", 0),
    ("asphalt", "26292E", "concrete", 0), ("asphalt_light", "3A3E44", "concrete", 0), ("paint_white_road", "B9B8B0", "concrete", 0),
    ("paint_yellow_road", "A8913A", "concrete", 0), ("brick", "6E3B2C", "concrete", 0), ("tile_white", "C7C9C6", "flat", 0),
    ("plaster", "8F8A7E", "concrete", 0), ("plaster_green", "6F7A6A", "concrete", 0), ("plaster_blue", "5C6F80", "concrete", 0),
    ("limestone", "BDB8A7", "concrete", 0), ("wood", "6A4B30", "wood", 0), ("wood_dark", "3E2C1C", "wood", 0),
    ("wood_plank", "8A6A44", "wood", 0),
    # (128..)
    ("cardboard", "8C7350", "paper", 0), ("paper", "D8D2C2", "paper", 0), ("grass_dead", "6B6E4F", "fur", 0),
    ("grass_yellow", "8A8452", "fur", 0), ("moss", "4E5C3B", "fur", 0), ("leaf_dark", "3B4630", "fur", 0),
    ("bark", "3F352B", "wood", 0), ("sky", "8F99A3", "flat", 0), ("glass_dark", "2E3A40", "glass", 0),
    ("glass_green", "3E5A3A", "glass", 0), ("glass_amber", "6B3A10", "glass", 0), ("headlight", "D8DCC8", "flat", 0),
    ("lamp_cold", "DDE8F0", "emissive", 1.0), ("car_beige", "A39878", "metal", 0), ("car_yellow", "A88B3A", "metal", 0),
    ("plastic_white", "C9C7BE", "flat", 0),
    # (144..)
    ("plastic_blue", "3A5C80", "flat", 0), ("tarp_blue", "3D5A78", "cloth", 0), ("tarp_green", "4D5A3E", "cloth", 0),
    ("sandbag", "8C8064", "cloth", 0), ("meat", "8A3B36", "skin", 0), ("can_label", "A63A2C", "paper", 0),
    ("food_tin", "A6A9AC", "metal", 0), ("plastic_red", "8E2A24", "flat", 0), ("plastic_yellow", "A89436", "flat", 0),
    ("sign_blue", "2E4C7A", "flat", 0), ("sign_white", "D0D2CC", "flat", 0), ("curtain_green", "5E7A6A", "cloth", 0),
    ("mattress", "8E8878", "cloth", 0), ("tile_green", "6E8A7E", "flat", 0), ("chalk", "E2E0D8", "flat", 0),
    ("rubble", "7A766C", "concrete", 0),
    # --- Grey ramp (160..)
    ("g05", "0D0D0D", "flat", 0), ("g15", "262626", "flat", 0), ("g25", "404040", "flat", 0), ("g35", "595959", "flat", 0),
    ("g45", "737373", "flat", 0), ("g55", "8C8C8C", "flat", 0), ("g65", "A6A6A6", "flat", 0), ("g75", "BFBFBF", "flat", 0),
    ("g85", "D9D9D9", "flat", 0), ("g95", "F2F2F2", "flat", 0),
    ("brown_1", "3A2A1C", "flat", 0), ("brown_2", "5C432C", "flat", 0), ("brown_3", "7E5E40", "flat", 0), ("brown_4", "A08060", "flat", 0),
    ("green_1", "2E3626", "flat", 0), ("green_2", "46523A", "flat", 0),
    # (176..)
    ("green_3", "5E6E4E", "flat", 0), ("green_4", "7A8A66", "flat", 0), ("blue_1", "1E2836", "flat", 0), ("blue_2", "2E3E54", "flat", 0),
    ("blue_3", "485C78", "flat", 0), ("blue_4", "6A7E98", "flat", 0), ("red_1", "3A1412", "flat", 0), ("red_2", "5E201C", "flat", 0),
    ("red_3", "862E28", "flat", 0), ("red_4", "A84A40", "flat", 0),
]
GRID = 16
CELL = 64
TEX = GRID * CELL
MARGIN = 7
_INDEX = {name: i for i, (name, *_rest) in enumerate(PALETTE)}
assert len(PALETTE) <= GRID * GRID, "palette is full"
assert len(_INDEX) == len(PALETTE), "duplicate swatch name"


def sw(name):
    """Index of a swatch, with a helpful error."""
    if name not in _INDEX:
        raise KeyError("unknown swatch '%s'. Known: %s" % (name, ", ".join(sorted(_INDEX))))
    return _INDEX[name]


def _hex(h):
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def build_palette_image(save=True):
    """Paints the palette texture (deterministic) and returns the Blender image."""
    rng = np.random.default_rng(1717)
    img = np.zeros((TEX, TEX, 4), dtype=np.float32)
    yy, xx = np.mgrid[0:CELL, 0:CELL].astype(np.float32)
    for i, (name, hx, kind, emit) in enumerate(PALETTE):
        col, row = i % GRID, i // GRID
        base = np.array(_hex(hx), dtype=np.float32)
        n = rng.standard_normal((CELL, CELL)).astype(np.float32)
        # low-frequency blotches: blurred noise by box filtering
        low = rng.standard_normal((8, 8)).astype(np.float32)
        low = np.kron(low, np.ones((CELL // 8, CELL // 8), dtype=np.float32))
        k = {
            "flat": (0.025, 0.02), "cloth": (0.06, 0.05), "skin": (0.03, 0.07), "crust": (0.07, 0.1),
            "metal": (0.04, 0.05), "concrete": (0.07, 0.07), "wood": (0.05, 0.06), "rubber": (0.03, 0.03),
            "fur": (0.07, 0.06), "paper": (0.03, 0.04), "mud": (0.06, 0.1), "glass": (0.015, 0.02), "emissive": (0.01, 0.04),
        }[kind]
        shade = 1.0 + n * k[0] + low * k[1]
        if kind == "cloth":
            shade += 0.012 * np.sin(yy * 1.6)  # weave
        elif kind == "wood":
            shade += 0.06 * np.sin(xx * 0.5 + low * 2.0)
        elif kind == "metal":
            shade += 0.05 * (rng.random((CELL, CELL)) > 0.985) + 0.03 * np.sin(yy * 0.9 + low * 3.0)
        elif kind == "crust":
            # dark veins between plates
            v = np.abs(np.sin(xx * 0.32 + low * 2.2) * np.sin(yy * 0.27 - low * 1.7))
            shade *= 1.0 - 0.28 * (v < 0.12)
        cell = np.clip(base[None, None, :] * shade[:, :, None], 0, 1)
        # flat border so mipmaps never bleed a neighbor's color
        cell[:MARGIN, :, :] = base
        cell[-MARGIN:, :, :] = base
        cell[:, :MARGIN, :] = base
        cell[:, -MARGIN:, :] = base
        y0 = (GRID - 1 - row) * CELL  # numpy row 0 = bottom of the Blender image
        x0 = col * CELL
        img[y0:y0 + CELL, x0:x0 + CELL, :3] = cell
        img[y0:y0 + CELL, x0:x0 + CELL, 3] = 1.0 - emit  # alpha = 1 - emission
    image = bpy.data.images.get("T_Palette") or bpy.data.images.new("T_Palette", TEX, TEX, alpha=True)
    image.colorspace_settings.name = "sRGB"  # set BEFORE the pixels: changing it regenerates the image
    image.alpha_mode = "CHANNEL_PACKED"  # alpha is data (emission mask), never premultiplied
    image.pixels.foreach_set(img.ravel())
    if save:
        path = os.path.join(ART, "Textures", "T_Palette.png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        image.filepath_raw = path
        image.file_format = "PNG"
        image.save()
    return image


def cell_uv(index, rnd):
    """A random small window inside a swatch: (u0, v0, size) in UV space."""
    col, row = index % GRID, index // GRID
    inner = (CELL - 2 * MARGIN - 18) / TEX
    u0 = (col * CELL + MARGIN + 9) / TEX + rnd.random() * inner * 0.5
    v0 = ((GRID - 1 - row) * CELL + MARGIN + 9) / TEX + rnd.random() * inner * 0.5
    return u0, v0, inner * 0.5


# ---------------------------------------------------------------------------------------------
# Scene helpers
# ---------------------------------------------------------------------------------------------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    sc.render.fps = FPS
    build_palette_image(save=False)


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def palette_material():
    """Preview material: palette texture x vertex color AO, emission from the alpha mask."""
    mat = bpy.data.materials.get("M_Palette")
    if mat:
        return mat
    mat = bpy.data.materials.new("M_Palette")
    try:
        mat.use_nodes = True
    except AttributeError:
        pass
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images["T_Palette"]
    tex.interpolation = "Closest"
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Col"
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    col_in = [s for s in mul.inputs if s.type == "RGBA"]
    col_out = [s for s in mul.outputs if s.type == "RGBA"][0]
    nt.links.new(tex.outputs["Color"], col_in[0])
    nt.links.new(vc.outputs["Color"], col_in[1])
    nt.links.new(col_out, bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    nt.links.new(tex.outputs["Alpha"], inv.inputs[1])
    emit = nt.nodes.new("ShaderNodeMath")
    emit.operation = "MULTIPLY"
    emit.inputs[1].default_value = 1.5
    nt.links.new(inv.outputs[0], emit.inputs[0])
    nt.links.new(emit.outputs[0], bsdf.inputs["Emission Strength"])
    bsdf.inputs["Roughness"].default_value = 0.75
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    return mat


# ---------------------------------------------------------------------------------------------
# Parts: small meshes kept as bmesh-ready vertex/face lists, each with a swatch and a bone.
# ---------------------------------------------------------------------------------------------

class Part:
    """A piece of geometry. verts are world-space Vectors, faces index lists.
    bone=None means "follow the body" (smooth weights from the skeleton), otherwise rigid on that bone."""

    def __init__(self, verts, faces, swatch, bone=None, smooth=False, uv_scale=1.0):
        self.verts = [Vector(v) for v in verts]
        self.faces = [list(f) for f in faces]
        self.swatch = swatch if isinstance(swatch, (list, tuple)) else [swatch] * len(self.faces)
        self.bone = bone
        self.smooth = smooth
        self.uv_scale = uv_scale

    def transformed(self, m):
        self.verts = [m @ v for v in self.verts]
        return self


def _from_bm(bm, swatch, bone, smooth, matrix=None):
    if matrix is not None:
        bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts)
    bm.verts.ensure_lookup_table()
    verts = [v.co.copy() for v in bm.verts]
    faces = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return Part(verts, faces, swatch, bone, smooth)


def _mat(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    """loc, rot in degrees (XYZ euler), scale -> 4x4 matrix."""
    from mathutils import Euler
    r = Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4()
    s = Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))
    return Matrix.Translation(Vector(loc)) @ r @ s


def box(size, loc=(0, 0, 0), rot=(0, 0, 0), swatch="g55", bone=None, bevel=0.0, smooth=False, taper=None):
    """Box of size (x, y, z). taper=(sx, sy) scales the top face (a wedge, a truncated pyramid)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    if taper:
        for v in bm.verts:
            if v.co.z > 0:
                v.co.x *= taper[0]
                v.co.y *= taper[1]
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel / max(size), segments=1, affect="EDGES", profile=0.5)
    return _from_bm(bm, swatch, bone, smooth, _mat(loc, rot, size))


def cyl(radius, depth, loc=(0, 0, 0), rot=(0, 0, 0), swatch="g55", bone=None, segments=8, radius2=None, smooth=True, caps=True):
    """Cylinder (or cone with radius2) along local Z, centered."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=segments, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=depth)
    return _from_bm(bm, swatch, bone, smooth, _mat(loc, rot))


def ball(radius, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), swatch="g55", bone=None, segments=10, rings=6, smooth=True):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)
    return _from_bm(bm, swatch, bone, smooth, _mat(loc, rot, scale))


def ico(radius, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), swatch="crust", bone=None, subdiv=1, jitter=0.0, seed=0, smooth=False):
    """Icosphere, optionally lumpy (jitter = fraction of radius): crust, rocks, growths."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    if jitter > 0:
        rnd = random.Random(seed)
        for v in bm.verts:
            v.co *= 1.0 + (rnd.random() * 2 - 1) * jitter
    return _from_bm(bm, swatch, bone, smooth, _mat(loc, rot, scale))


def shard(length, width, loc, direction, swatch="crust", bone=None, seed=0, sides=4):
    """An angular crust plate / crystal spike pointing along `direction` (amber growths, claws)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=sides, radius1=width, radius2=0.0, depth=length)
    for v in bm.verts:
        v.co.x *= 1.0 + (rnd.random() - 0.5) * 0.5
        v.co.y *= 1.0 + (rnd.random() - 0.5) * 0.5
        v.co.z += length * 0.5
    d = Vector(direction).normalized()
    rot = Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4()
    spin = Matrix.Rotation(rnd.random() * math.pi, 4, "Z")
    return _from_bm(bm, swatch, bone, False, Matrix.Translation(Vector(loc)) @ rot @ spin)


def plate_cluster(center, normal, count, size, swatch="crust", bone=None, seed=0, spread=1.0, flat=0.35):
    """A patch of flattened angular plates on a surface (the croute), lying on `normal`."""
    rnd = random.Random(seed)
    parts = []
    n = Vector(normal).normalized()
    t1 = n.orthogonal().normalized()
    t2 = n.cross(t1)
    for i in range(count):
        a = rnd.random() * math.tau
        r = math.sqrt(rnd.random()) * size * spread
        p = Vector(center) + (t1 * math.cos(a) + t2 * math.sin(a)) * r
        s = size * (0.35 + rnd.random() * 0.45)
        rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
        m = Matrix.Translation(p) @ rot @ Matrix.Rotation(rnd.random() * math.tau, 4, "Z") @ Matrix.Diagonal((s, s * (0.6 + rnd.random() * 0.6), s * flat, 1))
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
        for v in bm.verts:
            v.co *= 1.0 + (rnd.random() - 0.5) * 0.35
        sws = swatch if isinstance(swatch, str) else rnd.choice(swatch)
        parts.append(_from_bm(bm, sws, bone, False, m))
    return parts


# ---------------------------------------------------------------------------------------------
# Skeleton -> skin-modifier body + armature
# ---------------------------------------------------------------------------------------------

class Skeleton:
    """Joints (name -> position, radius) and segments (a, b, bone). The first segment's bone is the root child.
    A bone's parent is the bone of the segment that ends at its start joint."""

    def __init__(self):
        self.joints = {}
        self.segments = []  # (a, b, bone)

    def joint(self, name, pos, radius):
        r = radius if isinstance(radius, (tuple, list)) else (radius, radius)
        self.joints[name] = [Vector(pos), tuple(r)]
        return self

    def seg(self, a, b, bone):
        self.segments.append((a, b, bone))
        return self

    def mirror(self):
        """Copies every joint/segment whose name ends with .L to a .R one (X flipped)."""
        for name, (p, r) in list(self.joints.items()):
            if name.endswith(".L"):
                self.joints[name[:-2] + ".R"] = [Vector((-p.x, p.y, p.z)), r]
        for a, b, bone in list(self.segments):
            if bone.endswith(".L") or a.endswith(".L") or b.endswith(".L"):
                fa = a[:-2] + ".R" if a.endswith(".L") else a
                fb = b[:-2] + ".R" if b.endswith(".L") else b
                fbone = bone[:-2] + ".R" if bone.endswith(".L") else bone
                self.segments.append((fa, fb, fbone))
        return self

    def bone_names(self):
        seen = []
        for _, _, b in self.segments:
            if b not in seen:
                seen.append(b)
        return seen

    def bone_span(self, bone):
        """(head, tail) of a bone: start of its first segment, end of its last one."""
        segs = [(a, b) for a, b, bn in self.segments if bn == bone]
        return self.joints[segs[0][0]][0].copy(), self.joints[segs[-1][1]][0].copy()

    def bone_parent(self, bone):
        start = [a for a, b, bn in self.segments if bn == bone][0]
        for a, b, bn in self.segments:
            if b == start and bn != bone:
                return bn
        return None


def humanoid(height=1.75, bulk=1.0, hunch=0.0, arm=1.0, leg=1.0, head=1.0, shoulders=1.0, belly=0.0, hips=1.0, arms_out=30.0, limb=1.0):
    """Standard human skeleton (A-pose, arms 30 deg out). bulk thickens everything, limb thickens arms/legs,
    hunch (0..1) pushes the chest/neck/head forward and down (the Ambre stoop).
    Proportions are slightly heroic (limbs, hands and head +15 %) so bodies read from the top-down camera."""
    s = height / 1.75
    k = Skeleton()
    hz = 0.96 * s * leg ** 0.5
    fwd = -1.0  # forward is -Y
    hy = hunch * 0.12 * s
    L = 1.18 * limb * bulk  # limb thickness factor
    T = 1.1 * bulk          # torso thickness factor
    k.joint("pelvis", (0, 0.005, hz), (0.155 * T * hips, 0.115 * T * (1 + belly * 0.3)))
    k.joint("waist", (0, fwd * hy * 0.25, hz + 0.13 * s), (0.135 * T * (1 + belly * 0.7), 0.1 * T * (1 + belly * 1.0)))
    k.joint("spine", (0, fwd * hy * 0.55, hz + 0.25 * s - hunch * 0.015 * s), (0.155 * T * (1 + belly * 0.4), 0.11 * T * (1 + belly * 0.6)))
    k.joint("chest", (0, fwd * hy * 0.85, hz + 0.37 * s - hunch * 0.04 * s), (0.175 * T * shoulders ** 0.5, 0.12 * T))
    k.joint("uchest", (0, fwd * hy * 1.1, hz + 0.45 * s - hunch * 0.07 * s), (0.165 * T * shoulders ** 0.6, 0.11 * T))
    neck_z = hz + 0.53 * s - hunch * 0.1 * s
    k.joint("neck", (0, fwd * hy * 1.5, neck_z), (0.06 * bulk ** 0.5, 0.065 * bulk ** 0.5))
    hs = 1.12 * head * s
    k.joint("head", (0, fwd * hy * 1.9 - 0.01, neck_z + 0.06 * s), (0.07 * hs, 0.075 * hs))
    k.joint("skull", (0, fwd * hy * 2.0 + 0.008, neck_z + 0.15 * s * head), (0.088 * hs, 0.1 * hs))
    k.joint("head_top", (0, fwd * hy * 2.05 + 0.012, neck_z + 0.235 * s * head), (0.06 * hs, 0.07 * hs))
    sx = 0.2 * s * shoulders * (0.85 + 0.15 * bulk)
    sz = hz + 0.455 * s - hunch * 0.06 * s
    k.joint("shoulder.L", (sx, fwd * hy * 1.0 + 0.01, sz), 0.07 * L)
    a = math.radians(arms_out)
    ua, la, hl = 0.29 * s * arm, 0.26 * s * arm, 0.1 * s
    dvec = Vector((math.sin(a), 0, -math.cos(a)))
    sh = Vector((sx, fwd * hy + 0.01, sz))
    elbow = sh + dvec * ua + Vector((0, -0.02, 0))
    wrist = elbow + dvec * la + Vector((0, -0.03, 0))
    tip = wrist + dvec * hl
    k.joint("bicep.L", sh.lerp(elbow, 0.45), (0.058 * L, 0.062 * L))
    k.joint("elbow.L", elbow, 0.046 * L)
    k.joint("forearm.L", elbow.lerp(wrist, 0.3), (0.05 * L, 0.047 * L))
    k.joint("wrist.L", wrist, 0.034 * L)
    k.joint("hand.L", wrist.lerp(tip, 0.55), (0.048 * L, 0.026 * L))
    k.joint("fingers.L", tip, (0.04 * L, 0.02 * L))
    hip = Vector((0.1 * s * hips, 0, hz - 0.05 * s))
    knee = Vector((0.105 * s * hips, -0.012, hz * 0.52))
    ankle = Vector((0.105 * s * hips, 0.025, 0.085 * s))
    k.joint("hip.L", hip, 0.085 * L)
    k.joint("thigh.L", hip.lerp(knee, 0.4), (0.08 * L, 0.084 * L))
    k.joint("knee.L", knee, 0.06 * L)
    k.joint("calf.L", knee.lerp(ankle, 0.32) + Vector((0, 0.015, 0)), (0.062 * L, 0.066 * L))
    k.joint("ankle.L", ankle, 0.044 * L)
    k.joint("toe.L", (ankle.x, -0.135 * s, 0.04 * s), (0.05 * L, 0.036 * L))
    k.seg("pelvis", "waist", "Hips").seg("waist", "spine", "Spine").seg("spine", "chest", "Spine")
    k.seg("chest", "uchest", "Chest").seg("uchest", "neck", "Chest")
    k.seg("neck", "head", "Neck").seg("head", "skull", "Head").seg("skull", "head_top", "Head")
    k.seg("uchest", "shoulder.L", "Shoulder.L").seg("shoulder.L", "bicep.L", "UpperArm.L").seg("bicep.L", "elbow.L", "UpperArm.L")
    k.seg("elbow.L", "forearm.L", "LowerArm.L").seg("forearm.L", "wrist.L", "LowerArm.L")
    k.seg("wrist.L", "hand.L", "Hand.L").seg("hand.L", "fingers.L", "Hand.L")
    k.seg("pelvis", "hip.L", "Hips").seg("hip.L", "thigh.L", "UpperLeg.L").seg("thigh.L", "knee.L", "UpperLeg.L")
    k.seg("knee.L", "calf.L", "LowerLeg.L").seg("calf.L", "ankle.L", "LowerLeg.L").seg("ankle.L", "toe.L", "Foot.L")
    k.mirror()
    k.leg_length = (hip - knee).length + (knee - ankle).length + 0.08 * s
    return k


def _chains(skel):
    """Splits the skeleton into branch-free chains: the first path from the root is the trunk, every other
    branch becomes its own chain starting at the branch joint."""
    outs = {}
    for a, b, _ in skel.segments:
        outs.setdefault(a, []).append(b)
    root = skel.segments[0][0]
    pending = [(root, b) for b in outs.get(root, [])]
    chains = []
    while pending:
        start, nxt = pending.pop(0)
        chain = [start, nxt]
        cur = nxt
        while outs.get(cur):
            nx = outs[cur]
            for extra in nx[1:]:
                pending.append((cur, extra))
            chain.append(nx[0])
            cur = nx[0]
        chains.append(chain)
    return chains


def skin_body(skel, subsurf=1, decimate=1.0, smooth=True, fatten=None):
    """Grows a mesh from the skeleton with the Skin modifier. Returns a Part (vertices world space).
    Each branch-free chain (trunk, arms, legs) is skinned on its own and they overlap at the joints:
    no hull spikes at the pelvis or the shoulders. Sections are ROUND (the Skin modifier's section frame can
    spin on vertical chains), then each vertex is squashed toward its bone axis to the wanted (x, y) radii.
    Subdivision rounds sections inward, so radii are scaled up to keep the given sizes."""
    if fatten is None:
        fatten = {0: 1.0, 1: 1.3, 2: 1.38}.get(subsurf, 1.4)
    verts, edges, radii, roots = [], [], [], []
    for chain in _chains(skel):
        base = len(verts)
        for i, n in enumerate(chain):
            verts.append(tuple(skel.joints[n][0]))
            # a side chain starts inside its parent with its own (child) radius
            rn = chain[1] if i == 0 and chain is not None and len(chain) > 1 and base > 0 else n
            radii.append(max(skel.joints[rn][1]) * fatten)
            if i:
                edges.append((base + i - 1, base + i))
        roots.append(base)
    me = bpy.data.meshes.new("skin_src")
    me.from_pydata(verts, edges, [])
    obj = link(bpy.data.objects.new("skin_src", me))
    mod = obj.modifiers.new("Skin", "SKIN")
    mod.use_smooth_shade = smooth
    mod.branch_smoothing = 0.0
    for i, r in enumerate(radii):
        d = me.skin_vertices[0].data[i]
        d.radius = (r, r)
        d.use_root = i in roots
    if subsurf:
        ss = obj.modifiers.new("Sub", "SUBSURF")
        ss.levels = subsurf
        ss.render_levels = subsurf
    if decimate < 1.0:
        dec = obj.modifiers.new("Dec", "DECIMATE")
        dec.ratio = decimate
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    baked = bpy.data.meshes.new_from_object(ev)
    bm = bmesh.new()
    bm.from_mesh(baked)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.verts.ensure_lookup_table()
    segs = _seg_info(skel)
    out = []
    for v in bm.verts:
        p = v.co.copy()
        i, t, _ = _closest_seg(p, segs)
        a, b, _, pa, pb = segs[i]
        d = pb - pa
        if d.length > 1e-6 and abs(d.normalized().z) > 0.75:  # only vertical bones: X / Y are their section axes
            ra, rb = skel.joints[a][1], skel.joints[b][1]
            rx, ry = ra[0] + (rb[0] - ra[0]) * t, ra[1] + (rb[1] - ra[1]) * t
            r = max(rx, ry)
            if r > 1e-6 and abs(rx - ry) > 1e-4:
                axis = pa + d * t
                off = p - axis
                off.x *= rx / r
                off.y *= ry / r
                p = axis + off
        out.append(p)
    part = Part(out, [[v.index for v in f.verts] for f in bm.faces], "g55", None, smooth)
    bm.free()
    bpy.data.objects.remove(obj)
    return part


def _seg_info(skel):
    out = []
    for a, b, bone in skel.segments:
        pa, pb = skel.joints[a][0], skel.joints[b][0]
        out.append((a, b, bone, pa, pb))
    return out


def _closest_seg(p, segs):
    best, bt, bi = 1e9, 0.0, 0
    for i, (_, _, _, pa, pb) in enumerate(segs):
        ab = pb - pa
        L2 = ab.length_squared or 1e-9
        t = max(0.0, min(1.0, (p - pa).dot(ab) / L2))
        d = (pa + ab * t - p).length
        if d < best:
            best, bt, bi = d, t, i
    return bi, bt, best


def outfit_faces(part, skel, outfit, tear=0.0, tear_swatch=None, seed=0, extra=None, tear_bones=("Chest", "Spine", "Hips", "UpperLeg", "LowerLeg")):
    """Colors the body faces by bone: outfit[bone_without_side] = swatch, or a list of (t_max, swatch)
    along the bone (sleeves, socks). tear (0..1) opens holes in the clothes showing tear_swatch (skin).
    extra(face_center, swatch, bone, t) -> swatch lets an asset paint details (stains, a flood line...)."""
    segs = _seg_info(skel)
    # position along the WHOLE bone (a bone can span several segments): t_bone = 0 at its head, 1 at its tail
    bone_len, seg_start = {}, []
    for a, b, bone, pa, pb in segs:
        seg_start.append(bone_len.get(bone, 0.0))
        bone_len[bone] = bone_len.get(bone, 0.0) + (pb - pa).length
    rnd = random.Random(seed)
    offs = Vector((rnd.random() * 50, rnd.random() * 50, rnd.random() * 50))
    out = []
    for f in part.faces:
        c = sum((part.verts[i] for i in f), Vector()) / len(f)
        i, t, _ = _closest_seg(c, segs)
        bone = segs[i][2]
        L = bone_len[bone] or 1.0
        t = (seg_start[i] + t * (segs[i][4] - segs[i][3]).length) / L
        key = bone[:-2] if bone.endswith((".L", ".R")) else bone
        spec = outfit.get(bone, outfit.get(key, outfit.get("*", "g55")))
        if isinstance(spec, (list, tuple)) and not isinstance(spec, str):
            s = spec[-1][1]
            for tmax, swn in spec:
                if t <= tmax:
                    s = swn
                    break
        else:
            s = spec
        if tear > 0 and tear_swatch and key in tear_bones and PALETTE[sw(s)][2] == "cloth":
            nz = perlin(c * 4.5 + offs)
            if nz > 0.55 - tear:
                s = tear_swatch
        if extra:
            s = extra(c, s, bone, t) or s
        out.append(s)
    part.swatch = out
    return part


# ---------------------------------------------------------------------------------------------
# Character assembly
# ---------------------------------------------------------------------------------------------

def _weights_for(p, segs, skel, blend=0.3):
    """Smooth weights: the closest segment's bone, blended with the parent / child bone near joints."""
    i, t, _ = _closest_seg(p, segs)
    a, b, bone, pa, pb = segs[i]
    w = {bone: 1.0}
    if t < blend:
        parents = [s for s in segs if s[1] == a and s[2] != bone]
        if parents:
            j = min(parents, key=lambda s: (s[4] - p).length)
            k = 0.5 * (1.0 - t / blend)
            w = {bone: 1.0 - k, j[2]: w.get(j[2], 0) + k}
    elif t > 1.0 - blend:
        kids = [s for s in segs if s[0] == b and s[2] != bone]
        if kids:
            j = min(kids, key=lambda s: _closest_seg(p, [s])[2])
            k = 0.5 * (1.0 - (1.0 - t) / blend)
            w = {bone: 1.0 - k, j[2]: w.get(j[2], 0) + k}
    return w


def bake_vertex_colors(bm, ao=0.55, rays=14, dist=0.32, grime_height=0.6, grime=0.22, mud_height=0.0, mud=(0.82, 0.72, 0.56), seed=3):
    """AO (hemisphere ray casts) * grime gradient toward the ground * optional flood-mud line, into 'Col'."""
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    tree = BVHTree.FromBMesh(bm)
    rnd = random.Random(seed)
    dirs = []
    for _ in range(rays):
        u, v = rnd.random(), rnd.random()
        r, th = math.sqrt(u), math.tau * v
        dirs.append(Vector((r * math.cos(th), r * math.sin(th), math.sqrt(max(0.0, 1 - u)))))
    cols = []
    for vert in bm.verts:
        n = vert.normal if vert.normal.length > 0.1 else Vector((0, 0, 1))
        rot = n.to_track_quat("Z", "Y")
        hit = 0
        o = vert.co + n * 0.004
        for d in dirs:
            loc, _, _, _ = tree.ray_cast(o, rot @ d, dist)
            if loc is not None:
                hit += 1
        occl = 1.0 - ao * hit / rays
        z = vert.co.z
        g = 1.0 - grime * max(0.0, 1.0 - z / grime_height) if grime_height > 0 else 1.0
        c = [occl * g] * 3
        if mud_height > 0:
            m = max(0.0, min(1.0, (mud_height - z) / 0.06 + 0.5))
            c = [c[i] * (1.0 + (mud[i] - 1.0) * m) for i in range(3)]
        cols.append(c)
    return cols


class Character:
    """Collects the body and the parts, then builds one skinned mesh + armature, ready to animate."""

    def __init__(self, name, skel):
        self.name = name
        self.skel = skel
        self.parts = []
        self.rig = None
        self.mesh_obj = None

    def add(self, *parts):
        for p in parts:
            if isinstance(p, (list, tuple)):
                self.add(*p)
            else:
                self.parts.append(p)
        return self

    def build(self, ao=0.55, grime=0.22, mud_height=0.0, seed=5):
        """Builds the armature and the main mesh (named like the character)."""
        self._make_rig()
        self.mesh_obj = self.add_variant(self.name, self.parts, ao=ao, grime=grime, mud_height=mud_height, seed=seed)
        return self

    def _make_rig(self):
        skel = self.skel
        # Armature
        arm = bpy.data.armatures.new(self.name + "_RigData")
        rig = link(bpy.data.objects.new("Rig", arm))
        bpy.context.view_layer.objects.active = rig
        rig.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        root = arm.edit_bones.new("Root")
        root.head = (0, 0, 0)
        root.tail = (0, 0, 0.25)
        made = {"Root": root}
        for bone in skel.bone_names():
            h, t = skel.bone_span(bone)
            eb = arm.edit_bones.new(bone)
            eb.head = h
            eb.tail = t if (t - h).length > 1e-4 else h + Vector((0, 0, 0.05))
            # roll so the bone's local X stays close to world X (keeps the pose helpers predictable)
            eb.align_roll(Vector((0, -1, 0)) if abs((eb.tail - eb.head).normalized().z) > 0.7 else Vector((0, 0, 1)))
            made[bone] = eb
        for bone in skel.bone_names():
            par = skel.bone_parent(bone)
            made[bone].parent = made[par] if par else made["Root"]
        bpy.ops.object.mode_set(mode="OBJECT")
        self.rig = rig

    def add_variant(self, name, parts, ao=0.55, grime=0.22, mud_height=0.0, seed=5):
        """Another skinned mesh on the same skeleton (clothing / look variants of a horde enemy)."""
        skel = self.skel
        segs = _seg_info(skel)
        rig = self.rig
        bm = bmesh.new()
        uv_l = bm.loops.layers.uv.new("UVMap")
        deform = bm.verts.layers.deform.verify()
        bone_index = {b: i for i, b in enumerate(["Root"] + skel.bone_names())}
        rnd = random.Random(seed)
        for part in parts:
            base = len(bm.verts)
            vs = [bm.verts.new(v) for v in part.verts]
            for v in vs:
                w = {part.bone: 1.0} if part.bone else _weights_for(v.co, segs, skel)
                for bn, wt in w.items():
                    v[deform][bone_index[bn]] = wt
            for fi, f in enumerate(part.faces):
                try:
                    face = bm.faces.new([vs[i] for i in f])
                except ValueError:
                    continue
                face.smooth = part.smooth
                u0, v0, size = cell_uv(sw(part.swatch[fi]), rnd)
                # planar projection on the face's dominant axis, small window inside the swatch
                n = face.normal
                ax = max(range(3), key=lambda i: abs(n[i]))
                pa, pb = [(1, 2), (0, 2), (0, 1)][ax]
                for loop in face.loops:
                    co = loop.vert.co
                    loop[uv_l].uv = (u0 + (co[pa] * 2.0 * part.uv_scale) % 1.0 * size,
                                     v0 + (co[pb] * 2.0 * part.uv_scale) % 1.0 * size)
        bm.normal_update()
        cols = bake_vertex_colors(bm, ao=ao, grime=grime, mud_height=mud_height)
        me = bpy.data.meshes.new(name + "_Mesh")
        bm.to_mesh(me)
        bm.free()
        col = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
        for i, c in enumerate(cols):
            col.data[i].color = (c[0], c[1], c[2], 1.0)
        me.materials.append(palette_material())
        obj = link(bpy.data.objects.new(name, me))
        for b in ["Root"] + skel.bone_names():
            obj.vertex_groups.new(name=b)
        # vertex groups were written through the deform layer in index order
        obj.parent = rig
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        return obj

    # ----- animation -----

    def action(self, name, frames, pose_fn, loop=True, step=1):
        """Keys pose_fn(phase 0..1) every `step` frames. pose_fn returns {bone: value} where value is a
        Quaternion, a (Quaternion, world offset) tuple, or {"rot": q, "loc": world offset, "scale": s | (x, y, z)}.
        Bones not returned go back to rest. Loops end on the first pose; one-shots end at phase 1."""
        rig = self.rig
        rig.animation_data_create()
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        rig.animation_data.action = act
        for pb in rig.pose.bones:
            pb.rotation_mode = "QUATERNION"
        last = frames
        for f in list(range(0, frames, step)) + [last]:
            ph = (0.0 if f == last else f / frames) if loop else f / frames
            pose = pose_fn(ph)
            for pb in rig.pose.bones:
                pb.rotation_quaternion = Quaternion()
                pb.location = Vector()
            for pb in rig.pose.bones:
                pb.scale = Vector((1, 1, 1))
            for bn, val in pose.items():
                if bn not in rig.pose.bones:
                    continue
                pb = rig.pose.bones[bn]
                if isinstance(val, dict):
                    if "rot" in val:
                        pb.rotation_quaternion = val["rot"]
                    if "loc" in val:
                        pb.location = self.world_to_local(bn, val["loc"])
                    if "scale" in val:
                        sc = val["scale"]
                        pb.scale = Vector((sc, sc, sc)) if isinstance(sc, (int, float)) else Vector(sc)
                elif isinstance(val, tuple):
                    pb.rotation_quaternion, loc = val
                    pb.location = self.world_to_local(bn, loc)
                else:
                    pb.rotation_quaternion = val
            for pb in rig.pose.bones:
                pb.keyframe_insert("rotation_quaternion", frame=f + 1)
                pb.keyframe_insert("location", frame=f + 1)
                pb.keyframe_insert("scale", frame=f + 1)
        act.use_frame_range = True
        act.frame_start, act.frame_end = 1, last + 1
        return act

    def world_to_local(self, bone, vec):
        b = self.rig.data.bones[bone]
        return b.matrix_local.to_3x3().inverted() @ Vector(vec)

    def q(self, bone, x=0.0, y=0.0, z=0.0):
        """Rotation of `bone` by angles (degrees) around the ARMATURE's X, Y, Z axes, at rest.
        With the character facing -Y: +x tips an upward bone forward (lean / nod) and swings a downward
        bone backward; +z turns left; +y rolls toward the character's right."""
        b = self.rig.data.bones[bone]
        inv = b.matrix_local.to_3x3().inverted()
        out = Quaternion()
        for axis, ang in ((Vector((0, 0, 1)), z), (Vector((0, 1, 0)), y), (Vector((1, 0, 0)), x)):
            if ang:
                out = out @ Quaternion((inv @ axis).normalized(), math.radians(ang))
        return out


# ---- readable pose vocabulary (all degrees). Character faces -Y. ----

def leg(c, side, forward=0.0, knee=0.0, foot=0.0, out=0.0):
    """forward: thigh swing (+ forward), knee: bend (+), foot: toe up (+), out: spread sideways (+)."""
    s = 1 if side == "L" else -1
    return {
        "UpperLeg." + side: c.q("UpperLeg." + side, x=-forward, y=-out * s),
        "LowerLeg." + side: c.q("LowerLeg." + side, x=knee),
        "Foot." + side: c.q("Foot." + side, x=-foot),
    }


def arm(c, side, forward=0.0, raise_=0.0, elbow=0.0, twist=0.0, wrist=0.0):
    """forward: swing (+ forward), raise_: lift sideways (+ up), elbow: bend (+), twist: roll the arm."""
    s = 1 if side == "L" else -1
    return {
        "UpperArm." + side: c.q("UpperArm." + side, x=-forward, y=-raise_ * s, z=twist * s),
        "LowerArm." + side: c.q("LowerArm." + side, x=-elbow),
        "Hand." + side: c.q("Hand." + side, x=-wrist),
    }


def merge(*dicts):
    out = {}
    for d in dicts:
        for k, v in d.items():
            if k in out and not isinstance(v, tuple) and not isinstance(out[k], tuple):
                out[k] = out[k] @ v
            else:
                out[k] = v
    return out


def walk_pose(c, ph, stride=28.0, knee=42.0, arm_swing=22.0, lean=6.0, bob=0.03, sway=4.0, twist=6.0,
              elbow=20.0, arm_raise=0.0, arm_forward=0.0, limp=0.0, head_nod=0.0, heavy=0.0):
    """One frame of a walk/run cycle at phase ph (0..1). limp > 0 drags the right leg."""
    t = ph * math.tau
    sL, sR = math.sin(t), math.sin(t + math.pi)
    kL = max(0.0, math.cos(t)) * knee + 4
    kR = max(0.0, math.cos(t + math.pi)) * knee * (1 - limp * 0.7) + 4
    # the hips drop while the legs are spread, so the stance foot stays on the ground
    Lg = getattr(c.skel, "leg_length", 0.9)
    spread = math.radians(stride) * abs(math.sin(t))
    # bob only LOWERS the hips (at contact), never lifts them above standing height (the stance foot would float)
    hip_z = -Lg * (1 - math.cos(spread)) * 0.9 - bob * 0.5 * (1 - math.cos(2 * t)) - heavy * 0.03
    pose = merge(
        leg(c, "L", forward=stride * sL, knee=kL, foot=-sL * 8),
        leg(c, "R", forward=stride * sR * (1 - limp * 0.5), knee=kR, foot=-sR * 8),
        arm(c, "L", forward=arm_forward - arm_swing * sL, raise_=arm_raise, elbow=elbow + 8 * max(0, -sL)),
        arm(c, "R", forward=arm_forward - arm_swing * sR, raise_=arm_raise, elbow=elbow + 8 * max(0, -sR)),
        {"Spine": c.q("Spine", x=lean * 0.5, z=twist * sL * 0.5, y=sway * 0.3 * sL),
         "Chest": c.q("Chest", x=lean * 0.5, z=-twist * sL),
         "Head": c.q("Head", x=head_nod * math.cos(2 * t) - lean * 0.4),
         "Neck": c.q("Neck", x=-lean * 0.2)},
    )
    pose["Hips"] = (c.q("Hips", y=sway * sL, z=twist * 0.4 * sL), (0, 0, hip_z))
    return pose


# ---------------------------------------------------------------------------------------------
# Static props
# ---------------------------------------------------------------------------------------------

def build_prop(name, parts, ao=0.5, grime=0.25, grime_height=0.5, mud_height=0.0, seed=7, origin=(0, 0, 0)):
    """Merges parts into one static mesh object named `name` (origin = `origin`)."""
    bm = bmesh.new()
    uv_l = bm.loops.layers.uv.new("UVMap")
    rnd = random.Random(seed)
    o = Vector(origin)
    for part in parts:
        vs = [bm.verts.new(v - o) for v in part.verts]
        for fi, f in enumerate(part.faces):
            try:
                face = bm.faces.new([vs[i] for i in f])
            except ValueError:
                continue
            face.smooth = part.smooth
            u0, v0, size = cell_uv(sw(part.swatch[fi]), rnd)
            n = face.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            pa, pb = [(1, 2), (0, 2), (0, 1)][ax]
            for loop in face.loops:
                co = loop.vert.co
                loop[uv_l].uv = (u0 + (co[pa] * 1.5 * part.uv_scale) % 1.0 * size,
                                 v0 + (co[pb] * 1.5 * part.uv_scale) % 1.0 * size)
    bm.normal_update()
    cols = bake_vertex_colors(bm, ao=ao, grime=grime, grime_height=grime_height, mud_height=mud_height)
    me = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(me)
    bm.free()
    col = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    for i, c in enumerate(cols):
        col.data[i].color = (c[0], c[1], c[2], 1.0)
    me.materials.append(palette_material())
    return link(bpy.data.objects.new(name, me))


# ---------------------------------------------------------------------------------------------
# Export + preview
# ---------------------------------------------------------------------------------------------

def _select_only(objs):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def export_fbx(objs, rel_path, animated=True):
    """Writes Assets/_Game/Art/<rel_path>.fbx."""
    path = os.path.join(ART, rel_path + ".fbx")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    _select_only(objs)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type="OFF", colors_type="LINEAR",
        use_tspace=False, add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL", bake_anim=animated, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=animated, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode="STRIP", embed_textures=False,
    )
    return path


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def _engine():
    for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            bpy.context.scene.render.engine = e
            return e
        except TypeError:
            continue
    bpy.context.scene.render.engine = "BLENDER_WORKBENCH"
    return "BLENDER_WORKBENCH"


def preview(name, objs, views=((20, 58), (200, 58), (90, 12)), size=420, frame=None, action=None, extra_frames=None):
    """Renders the objects from the game's camera angle (front and back) and from the side, side by side,
    into Tools/Blender/previews/<name>.png. views = (azimuth deg, elevation deg); azimuth 0 = in front (-Y)."""
    sc = bpy.context.scene
    _engine()
    hidden = [o for o in sc.objects if o.type == "MESH" and o not in objs and not o.hide_render]
    for o in hidden:
        o.hide_render = True
    sc.render.resolution_x = size
    sc.render.resolution_y = size
    sc.render.film_transparent = False
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    world = bpy.data.worlds.get("PreviewWorld") or bpy.data.worlds.new("PreviewWorld")
    try:
        world.use_nodes = True
    except AttributeError:
        pass
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.32, 0.35, 0.39, 1)
    bg.inputs[1].default_value = 0.9
    sc.world = world
    if action is not None and objs and objs[0].parent and objs[0].parent.type == "ARMATURE":
        objs[0].parent.animation_data.action = action
    if frame is not None:
        sc.frame_set(frame)
    # bounds
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in objs:
        ev = o.evaluated_get(dg)
        m = ev.to_mesh()
        pts += [o.matrix_world @ v.co for v in m.vertices]
        ev.to_mesh_clear()
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.2)
    # ground
    gme = bpy.data.meshes.new("pv_ground")
    g = radius * 4
    gme.from_pydata([(-g, -g, 0), (g, -g, 0), (g, g, 0), (-g, g, 0)], [], [(0, 1, 2, 3)])
    gobj = link(bpy.data.objects.new("pv_ground", gme))
    gmat = bpy.data.materials.new("pv_ground_mat")
    try:
        gmat.use_nodes = True
    except AttributeError:
        pass
    gmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.08, 0.085, 0.09, 1)
    gme.materials.append(gmat)
    sun_d = bpy.data.lights.new("pv_sun", "SUN")
    sun_d.energy = 3.2
    sun = link(bpy.data.objects.new("pv_sun", sun_d))
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    cam_d = bpy.data.cameras.new("pv_cam")
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = radius * 2.3
    cam = link(bpy.data.objects.new("pv_cam", cam_d))
    sc.camera = cam
    tiles = []
    tmp = os.path.join(PREVIEWS, "_tmp_%d.png" % os.getpid())  # one per Blender process (parallel runs)
    os.makedirs(PREVIEWS, exist_ok=True)
    frames = [frame] if not extra_frames else extra_frames
    for fr in frames:
        if fr is not None:
            sc.frame_set(fr)
        for az, el in views:
            a, e = math.radians(az), math.radians(el)
            d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
            cam.location = center + d * radius * 6
            cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
            sc.render.filepath = tmp
            bpy.ops.render.render(write_still=True)
            im = bpy.data.images.load(tmp)
            arr = np.array(im.pixels[:], dtype=np.float32).reshape(size, size, 4)
            bpy.data.images.remove(im)
            tiles.append(arr)
    sheet = np.concatenate(tiles, axis=1)
    out = bpy.data.images.new("pv_sheet", sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = os.path.join(PREVIEWS, name + ".png")
    out.file_format = "PNG"
    out.save()
    for o in (gobj, sun, cam):
        bpy.data.objects.remove(o)
    for o in hidden:
        o.hide_render = False
    if os.path.exists(tmp):
        os.remove(tmp)
    return out.filepath_raw


def report(**kw):
    print("CORDON_REPORT", " ".join("%s=%s" % (k, v) for k, v in kw.items()))
    sys.stdout.flush()
