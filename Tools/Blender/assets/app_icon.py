"""App icon (iOS / Android): the big amber éclat (Eclat_3 of pickups.py) glowing on the Cordon's cold dark grey.
Renders Assets/_Game/Textures/AppIcon.png, 1024x1024, no alpha (iOS refuses transparent icons).
Run: blender --background --factory-startup --python Tools/Blender/assets/app_icon.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _props_lib as L  # noqa: E402
from _props_lib import K  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

K.reset()

GLOW = ("amber_glow", "crystal", "amber_glow_soft", "amber_glow", "crystal", "amber_glow_soft")
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Game", "Textures", "AppIcon.png")


def resin_base(r, loc, seed, scale=(1, 1, 0.55), subdiv=1):
    base = [K.ico(r, loc, scale=scale, swatch="amber", subdiv=subdiv, jitter=0.18, seed=seed)]
    L.recolor(base, lambda c, n, s: "crust_dark" if n.z < -0.2 else ("amber_glow_soft" if n.z > 0.75 else None))
    return base


def eclat():
    parts = [resin_base(0.22, (0, 0, 0.07), 51, scale=(1.1, 1, 0.45), subdiv=2),
             L.crystal(0.5, 0.14, (0.0, 0.0, 0.09), (0.04, 0.05, 1), back=0.15, sides=6, seed=52, swatch=GLOW, back_swatch="amber"),
             L.crystal(0.34, 0.1, (0.12, -0.05, 0.08), (0.55, -0.25, 1), back=0.15, sides=5, seed=53, swatch=GLOW[1:], back_swatch="amber"),
             L.crystal(0.31, 0.095, (-0.13, -0.03, 0.08), (-0.6, -0.15, 1), back=0.15, sides=5, seed=54, swatch=GLOW[2:], back_swatch="amber"),
             L.crystal(0.27, 0.09, (0.02, 0.13, 0.08), (0.1, 0.6, 1), back=0.15, sides=5, seed=55, swatch=GLOW[3:], back_swatch="amber"),
             L.crystal(0.18, 0.065, (0.1, 0.11, 0.07), (0.6, 0.45, 0.8), back=0.15, sides=4, seed=56, swatch=("crystal", "amber_glow"), back_swatch="amber"),
             L.crystal(0.16, 0.06, (-0.1, -0.13, 0.06), (-0.35, -0.7, 0.8), back=0.15, sides=4, seed=57, swatch=("amber_glow", "crystal"), back_swatch="amber")]
    parts += K.plate_cluster((0, 0, 0.03), (0, 0, 1), 4, 0.09, swatch=["crust_dark", "crust"], seed=58, spread=2.2)
    return L.finish("Eclat_Icon", parts, origin="bottom", ao=0.3, grime=0.0)


obj = eclat()
sc = bpy.context.scene
K._engine()
sc.render.resolution_x = 1024
sc.render.resolution_y = 1024
sc.render.film_transparent = False
try:
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Medium High Contrast"
except TypeError:
    try:
        sc.view_settings.view_transform = "Filmic"
    except TypeError:
        pass

# Cold dark world, like the fog of Ostrov at night.
world = bpy.data.worlds.new("IconWorld")
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (0.025, 0.03, 0.038, 1)
bg.inputs[1].default_value = 1.0
sc.world = world

# Wet asphalt the crystal stands on (reflects the glow).
gme = bpy.data.meshes.new("icon_ground")
g = 6.0
gme.from_pydata([(-g, -g, 0), (g, -g, 0), (g, g, 0), (-g, g, 0)], [], [(0, 1, 2, 3)])
gobj = None  # no floor: the cluster floats in front of its own glow
gmat = bpy.data.materials.new("icon_ground_mat")
gmat.use_nodes = True
p = gmat.node_tree.nodes["Principled BSDF"]
p.inputs["Base Color"].default_value = (0.02, 0.024, 0.03, 1)
p.inputs["Roughness"].default_value = 0.4
# Backdrop: a vertical emissive wall with a soft amber halo in the middle (stands in for bloom).
bme = bpy.data.meshes.new("icon_back")
b = 8.0
bme.from_pydata([(-b, 0, -b), (b, 0, -b), (b, 0, b), (-b, 0, b)], [], [(0, 1, 2, 3)])
bobj = K.link(bpy.data.objects.new("icon_back", bme))
bobj.location = (0.0, 2.0, 0.0)
bmat = bpy.data.materials.new("icon_back_mat")
bmat.use_nodes = True
nt = bmat.node_tree
for n in list(nt.nodes):
    nt.nodes.remove(n)
out = nt.nodes.new("ShaderNodeOutputMaterial")
emit = nt.nodes.new("ShaderNodeEmission")
coord = nt.nodes.new("ShaderNodeTexCoord")
mapping = nt.nodes.new("ShaderNodeMapping")
mapping.inputs["Location"].default_value = (0.0, 0.0, 0.06)   # halo centered on the cluster
mapping.inputs["Scale"].default_value = (0.75, 0.75, 0.75)
grad = nt.nodes.new("ShaderNodeTexGradient")
grad.gradient_type = "SPHERICAL"
ramp = nt.nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].color = (0.012, 0.016, 0.022, 1)
ramp.color_ramp.elements[1].position = 0.85
ramp.color_ramp.elements[1].color = (0.4, 0.13, 0.025, 1)
mid = ramp.color_ramp.elements.new(0.4)
mid.color = (0.05, 0.03, 0.025, 1)
nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
nt.links.new(mapping.outputs["Vector"], grad.inputs["Vector"])
nt.links.new(grad.outputs["Fac"], ramp.inputs["Fac"])
nt.links.new(ramp.outputs["Color"], emit.inputs["Color"])
emit.inputs["Strength"].default_value = 1.0
nt.links.new(emit.outputs[0], out.inputs[0])
bme.materials.append(bmat)

# Light: a cold rim from behind, an amber point inside the cluster (the glow), a faint cold fill.
def light(kind, energy, loc, color, size=0.5):
    d = bpy.data.lights.new("l_" + kind + str(len(bpy.data.lights)), kind)
    d.energy = energy
    d.color = color
    if hasattr(d, "shadow_soft_size"):
        d.shadow_soft_size = size
    o = K.link(bpy.data.objects.new(d.name, d))
    o.location = loc
    return o

rim = light("SPOT", 60.0, (0.35, 0.9, 1.2), (0.55, 0.7, 0.95), 0.3)
rim.rotation_euler = (Vector((0, 0, 0.3)) - rim.location).to_track_quat("-Z", "Y").to_euler()
light("POINT", 30.0, (0.0, 0.0, 0.3), (1.0, 0.5, 0.15), 0.1)
fill = light("SPOT", 18.0, (-1.2, -1.4, 1.0), (0.6, 0.68, 0.8), 0.6)
fill.rotation_euler = (Vector((0, 0, 0.3)) - fill.location).to_track_quat("-Z", "Y").to_euler()

# Camera: slightly above, the cluster filling ~70% of the square.
cam_d = bpy.data.cameras.new("icon_cam")
cam_d.lens = 85
cam = K.link(bpy.data.objects.new("icon_cam", cam_d))
target = Vector((0.0, 0.0, 0.3))
cam.location = target + Vector((0.0, -1.0, 0.36)).normalized() * 2.5
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.camera = cam

# Glow through the compositor (Eevee has no bloom anymore).
try:
    sc.use_nodes = True
    nt = sc.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    rl = nt.nodes.new("CompositorNodeRLayers")
    glare = nt.nodes.new("CompositorNodeGlare")
    glare.glare_type = "FOG_GLOW"
    try:
        glare.quality = "HIGH"
        glare.threshold = 0.6
        glare.size = 8
    except (AttributeError, TypeError):
        pass
    comp = nt.nodes.new("CompositorNodeComposite")
    nt.links.new(rl.outputs["Image"], glare.inputs["Image"])
    nt.links.new(glare.outputs["Image"], comp.inputs["Image"])
except Exception as e:  # compositor API differs between versions: the icon still renders without glow
    print("glow skipped:", e)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("CORDON_ICON", OUT)
