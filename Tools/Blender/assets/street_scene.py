"""Look-dev: a street of Ostrov-Sud dressed with the decor kit on the ground texture, seen from the game camera
(21 m, 60 deg pitch, portrait). Ambres from Characters/Ambre.fbx for scale and contrast, a player stand-in,
a few amber eclats and a white event circle to check that what matters pops over the ground.

Writes Tools/Blender/previews/street_scene.png (+ street_scene_b.png on the variant ground). Run AFTER the other scripts.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _decor_lib as D  # noqa: E402
import decor_ostrov  # noqa: E402

K = D.K
V = D.V
bpy = D.bpy

LAYOUT = [
    # (name, x, y, rot_z)  Blender axes: the camera sits at +Y looking -Y (screen up = -Y)
    ("Decal_RoadLine", 0.0, 6.5, 0), ("Decal_RoadLine", 0.0, 0.5, 0), ("Decal_RoadLine", 0.0, -11.5, 0),
    ("Decal_Zebra", 0.2, -5.2, 0),
    ("Car_A", 4.3, 3.0, 188), ("Car_B", 4.6, -3.2, 3), ("Car_C", -5.4, -1.6, 72), ("Wall_Ruin_A", -7.3, -2.5, 90),
    ("Car_Burnt", -3.6, -11.5, 28), ("Decal_Crater", -0.8, -13.6, 0),
    ("Barrier_Jersey", 1.6, -15.0, 0), ("Barrier_Jersey", 3.8, -15.2, 6), ("Barrier_Checkpoint", -1.9, -14.4, 0),
    ("Sandbags", 6.2, -17.5, 180), ("Container_Regen", -8.5, -17.0, 75), ("Fence_Chain", 7.6, -12.8, 90),
    ("Lamp", 2.9, 7.2, -90), ("Lamp", -2.9, -7.8, 90),
    ("BusShelter", 7.0, 0.6, -90), ("Kiosk", -6.2, 5.0, 90), ("Tree_Dead", -4.0, 1.6, 0), ("Tree_Dead", 5.3, -9.2, 140),
    ("Sign_Road", 2.9, -7.6, 180), ("Planter", -3.3, 8.4, 0), ("Bench", -4.6, -6.4, 90), ("Wall_Ruin_B", 8.5, 6.5, -90),
    ("TrashBags", -4.8, 7.0, 0), ("Tires", -7.0, -6.4, 0), ("Pallets", -7.8, -13.0, 20), ("Barrels", 6.6, -6.8, 0),
    ("Debris_A", 2.0, -9.8, 0), ("Debris_B", -1.6, -8.7, 40), ("Suitcase", 1.7, 2.1, 30), ("Stroller", 2.5, 3.8, -25),
    ("Decal_Papers", 1.2, 1.2, 0), ("Decal_Papers", -1.0, -15.8, 90),
    ("Decal_Puddle_A", -1.4, 4.4, 10), ("Decal_Puddle_B", 1.3, -6.9, 30), ("Decal_Crust_A", -2.0, -3.2, 0),
    ("Decal_Crust_B", -4.6, -4.4, 70), ("Decal_Manhole", 0.9, 5.4, 0), ("Resurgence_Small", -2.4, 0.4, 0),
]

PLAYER = (0.0, 0.0)
AMBRES = [(1.6, -1.8, 200), (-1.0, -6.0, 170), (3.0, -11.6, 190), (-0.4, -3.4, 150), (2.2, 5.0, 10)]
ECLATS = [(0.8, -2.6), (-0.6, 2.6), (1.9, -4.4), (-1.6, -1.0), (0.3, -8.0), (2.6, 1.4)]
CIRCLE = (-1.6, -10.0, 1.7)


def import_ambres():
    path = os.path.join(K.ART, "Characters", "Ambre.fbx")
    if not os.path.exists(path):
        return []
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    bpy.context.scene.frame_set(6)
    dg = bpy.context.evaluated_depsgraph_get()
    statics = []
    for o in new:
        if o.type != "MESH":
            continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        if me.color_attributes and me.color_attributes[0].name != "Col":
            me.color_attributes[0].name = "Col"
        me.materials.clear()
        me.materials.append(K.palette_material())
        st = K.link(bpy.data.objects.new(o.name + "_static", me))
        st.matrix_world = o.matrix_world.copy()
        statics.append(st)
    for o in new:
        bpy.data.objects.remove(o)
    for st in statics:
        st.location = (0, 0, 0)
        st.rotation_euler = (0, 0, 0)
    return statics


def player():
    parts = [K.cyl(0.26, 0.95, (0, 0, 0.62), swatch="coat_navy", segments=10, smooth=False),
             K.ball(0.13, (0, 0, 1.25), swatch="skin_mid", segments=8, rings=5, smooth=False),
             K.box((0.36, 0.22, 0.42), (0, 0.18, 1.0), swatch="canvas_olive"),
             K.cyl(0.13, 0.4, (0.12, 0, 0.18), swatch="cargo_grey", segments=6, smooth=False),
             K.cyl(0.13, 0.4, (-0.12, 0, 0.18), swatch="cargo_grey", segments=6, smooth=False),
             K.box((0.06, 0.6, 0.08), (0.28, -0.25, 1.0), swatch="metal_gun")]
    return K.build_prop("PlayerStandIn", parts, ao=0.4, grime=0.1)


def eclat(loc):
    parts = [K.ico(0.12, (loc[0], loc[1], 0.35), scale=(0.7, 0.7, 1.2), swatch="amber_glow", subdiv=1, jitter=0.1)]
    return K.build_prop("Eclat", parts, ao=0.0, grime=0.0)


def circle(x, y, r):
    me = bpy.data.meshes.new("evt")
    verts, faces = [], []
    seg = 48
    for i in range(seg):
        a = math.tau * i / seg
        for rr in (r - 0.05, r):
            verts.append((x + math.cos(a) * rr, y + math.sin(a) * rr, 0.02))
    for i in range(seg):
        j = (i + 1) % seg
        faces.append((2 * i, 2 * i + 1, 2 * j + 1, 2 * j))
    me.from_pydata(verts, [], faces)
    ob = K.link(bpy.data.objects.new("EventCircle", me))
    me.materials.append(D._emit_mat("M_Evt", (0.95, 0.95, 0.93, 1), 2.0))
    return ob


def scene(tex, name):
    for m in list(bpy.data.materials):
        if m.name.startswith("M_GroundPreview"):
            bpy.data.materials.remove(m)
    D.setup_render(720, 1440, sky=(0.30, 0.33, 0.37), sky_strength=0.85, sun_energy=2.4, sun_rot=(52, 0, -30))
    g = D.ground_plane(80.0, D.ground_material(tex), name="scene_ground")
    cam = (PLAYER[0], PLAYER[1] + 21 * math.cos(math.radians(60)), 21 * math.sin(math.radians(60)))
    p = D.render_camera(name, cam, (PLAYER[0], PLAYER[1], 0.0), 720, 1440, fov=50, clip_end=300)
    bpy.data.objects.remove(g)
    return p


if __name__ == "__main__":
    K.reset()
    objs = decor_ostrov.build_all()
    lib = {o.name: o for o in objs}
    for o in objs:
        o.hide_render = True
    for n, x, y, r in LAYOUT:
        inst = D.place(lib[n], (x, y), r)
        inst.hide_render = False
    am = import_ambres()
    for i, (x, y, r) in enumerate(AMBRES):
        src = am[i % len(am)] if am else None
        if src:
            D.place(src, (x, y), r).hide_render = False
    for o in am:
        o.hide_render = True
    pl = player()
    pl.location = (PLAYER[0], PLAYER[1], 0)
    for e in ECLATS:
        eclat(e)
    circle(*CIRCLE)
    p = scene(D.GROUND_TEX, "street_scene")
    K.report(preview=p)
    pb = scene(D.GROUND_TEX.replace(".png", "_B.png"), "street_scene_b")
    K.report(preview=pb)
