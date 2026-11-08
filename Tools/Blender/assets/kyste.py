"""Le Kyste (stage III, the belly; Bestiaire 3.1): "Bloater" in the code.

A host who drank a lot of contaminated water: the whole torso became a reservoir of amber sap under
pressure, a translucent glowing BALLOON (dark veins, patches of stretched grey skin, crust where it tears).
Small head perched on top, short limbs, a waddle. Read from above: a CIRCLE.
Kamikaze: the game scales/wobbles the model during the 0.75 s fuse, then it bursts.

Extra bone: Belly (child of Spine) carries the balloon; Walk jiggles it, Special swells it.
Anims: Walk (waddle), Special (one-shot tremble and swell, ends swollen), Idle.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _ambres_lib as L  # noqa: E402
from _ambres_lib import K, V  # noqa: E402
import bpy  # noqa: E402

QUICK = "--quick" in sys.argv

K.reset()
skel = K.humanoid(height=1.62, bulk=1.05, hunch=0.1, arm=0.8, leg=0.8, head=0.92, limb=1.1, hips=1.3, arms_out=50)
JR = skel.joints


def move(names, delta):
    for n in names:
        JR[n][0] = JR[n][0] + V(delta)


# the balloon is the torso; the head sits on its top front, the stubby arms come out of its sides
BAL_C = V((0, 0.01, 1.0))
BAL_R = V((0.43, 0.39, 0.38))
for n, p in {"waist": (0, 0.0, 0.9), "spine": (0, 0.0, 1.0), "chest": (0, -0.02, 1.1), "uchest": (0, -0.06, 1.2)}.items():
    JR[n][0] = V(p)
d = V((0, -0.17, 1.3)) - JR["neck"][0]
move(("neck", "head", "skull", "head_top"), d)
for side, sx in (("L", 1), ("R", -1)):
    d = V((0.3 * sx, -0.03, 1.13)) - JR["shoulder." + side][0]
    move([n + "." + side for n in ("shoulder", "bicep", "elbow", "forearm", "wrist", "hand", "fingers")], d)
J = {n: p for n, (p, r) in JR.items()}
L.add_bone(skel, "Belly", BAL_C, BAL_C + V((0, 0, 0.3)), "Spine")


def balloon(seed):
    rnd = K.random.Random(seed)
    o1 = V((rnd.random() * 30, rnd.random() * 30, rnd.random() * 30))
    o2 = V((rnd.random() * 30, rnd.random() * 30, rnd.random() * 30))

    def paint(c, s):
        d = c - BAL_C
        nz = d.z / BAL_R.z
        sk = K.perlin(d * 1.9 + o2)
        v = K.perlin(d * 5.0 + o1)
        # grey skin still stretched over the top (where the head sits) and the bottom; big torn patches elsewhere
        w = 0.16 * K.perlin(d * 6.0 + o2 * 0.5)
        if nz > 0.82 + w or nz < -0.78 - w or sk > 0.24:
            return "skin_ambre"
        if (sk > 0.18 or nz > 0.75 + w * 1.5 or nz < -0.7 - w * 1.5) and K.perlin(d * 9.0 + o1 * 0.3) > -0.05:
            return "crust_dark"
        if abs(v) < 0.024:
            return "veins"
        return None
    return L.blob(1.0, BAL_C, scale=tuple(BAL_R), subdiv=3, jitter=0.035, seed=seed, swatch="amber_glow_soft", bone="Belly", paint=paint)


def bits(body, seed):
    parts = [balloon(seed)]
    # crust where the skin tore: around the neck, a ring at the hips, a few plates on the sides
    for i in range(5):
        a = math.radians(-90 + (i - 2) * 38)
        p = BAL_C + V((math.cos(a) * BAL_R.x * 0.62, math.sin(a) * BAL_R.y * 0.62, BAL_R.z * 0.8))
        n = V(((p - BAL_C).x / BAL_R.x, (p - BAL_C).y / BAL_R.y, (p - BAL_C).z / BAL_R.z)).normalized()
        parts += K.plate_cluster(p, n, 1, 0.075, swatch="crust" if i % 2 else "crust_dark", bone="Belly", seed=seed + 10 + i)
    for i in range(6):
        a = i / 6 * math.tau + 0.3
        p = BAL_C + V((math.cos(a) * BAL_R.x * 0.75, math.sin(a) * BAL_R.y * 0.75, -BAL_R.z * 0.63))
        n = V(((p - BAL_C).x / BAL_R.x, (p - BAL_C).y / BAL_R.y, (p - BAL_C).z / BAL_R.z)).normalized()
        parts += K.plate_cluster(p, n, 1, 0.08, swatch="crust_dark" if i % 2 else "crust_black", bone="Belly", seed=seed + 20 + i)
    parts += L.sealed_face(body, J, seed + 3, mouth_w=1.0)
    for side in ("L", "R"):
        parts += L.crust_claws(J, side, seed + 30 + (side == "R"), length=0.06, width=0.013)
    return parts


SKIN = "skin_ambre"
OUTFIT = {"Head": SKIN, "Neck": SKIN, "Chest": SKIN, "Spine": SKIN, "Shoulder": SKIN, "UpperArm": SKIN, "LowerArm": SKIN,
          "Hand": "skin_ambre_dark", "Hips": "cargo_khaki", "UpperLeg": "cargo_khaki", "LowerLeg": "cargo_khaki", "Foot": "leather_brown"}
body = K.skin_body(skel, subsurf=1)
K.outfit_faces(body, skel, OUTFIT, tear=0.12, tear_swatch=SKIN, seed=505,
               extra=L.veins_near([(J["shoulder.L"], 0.12), (J["shoulder.R"], 0.12), (J["neck"], 0.1)]))
parts = bits(body, 505)
L.cull(body, L.ellipsoid_test(BAL_C, BAL_R, 0.96))
char = K.Character("Kyste", skel)
char.parts = [body] + parts
char.build(ao=0.5, grime=0.2, mud_height=0.35, seed=505)
char.mesh_obj.name = "Kyste_A"
objs = [char.mesh_obj]
c = char

# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------
WALK_FRAMES, WALK_SPEED, WALK_DUTY = 16, 1.9, 0.62


def belly(scale):
    return {"Belly": {"rot": K.Quaternion(), "scale": scale}}


def waddle(ph):
    t = ph * math.tau
    uL, uR = ph, (ph + 0.5) % 1.0
    sL = math.cos(L.gait_angle(uL, WALK_DUTY))
    wL = math.sin(math.pi * uL / WALK_DUTY) if uL < WALK_DUTY else 0.0
    wR = math.sin(math.pi * uR / WALK_DUTY) if uR < WALK_DUTY else 0.0
    shift = wL - wR  # + = weight on the left foot
    hips_R = L.eul(x=2, y=9 * shift, z=5 * sL)
    pose, _ = L.biped_legs(c, ph, WALK_SPEED, WALK_FRAMES, duty=WALK_DUTY, lift=0.1, center=0.02, width=0.05, toe_off=10, heel=4,
                           hips_R=hips_R, hips_off=(0.055 * shift, 0, -0.03 + 0.015 * math.cos(2 * t)), pole_out=0.45)
    # the balloon rocks over the stance foot and lags behind the bounce; little arms flap for balance
    pose["Spine"] = c.q("Spine", x=-2, y=5 * shift)
    pose["Chest"] = c.q("Chest", y=2 * shift)
    pose["Neck"] = c.q("Neck", y=-8 * shift)
    pose["Head"] = c.q("Head", x=-6, y=-6 * shift, z=5 * sL)
    pose.update(K.merge(
        L.arm2(c, "L", forward=10 - 14 * sL, raise_=-14 + 16 * max(0.0, -shift), elbow=35, wrist=15),
        L.arm2(c, "R", forward=10 + 14 * sL, raise_=-14 + 16 * max(0.0, shift), elbow=35, wrist=15),
    ))
    j = 0.035 * math.cos(2 * t - 1.2)
    pose["Belly"] = {"rot": c.q("Belly", y=3 * shift, x=2 * math.cos(2 * t - 1.2)), "scale": (1 + j * 0.6, 1 - j, 1 + j * 0.6)}
    return pose


def swell(ph):
    """The fuse (22 f, ~0.73 s): it stops dead, braces, throws its head back and swells, trembling harder and
    harder. Ends swollen (the game bursts it)."""
    k = L.smooth(ph / 0.35)
    g = ph ** 1.6
    tr = math.sin(ph * 95) * (0.3 + 0.7 * g)
    plant = {"L": L.planted(c, "L", dy=-0.05, dx=0.08), "R": L.planted(c, "R", dy=0.07, dx=0.08)}
    hips_R = L.eul(x=-3 * k, y=2.5 * tr)
    pose, _ = L.biped_legs(c, 0.0, 0.0, 22, plant=plant, hips_R=hips_R, hips_off=(0.012 * tr, 0, -0.05 * k + 0.01 * tr), pole_out=0.45)
    pose["Spine"] = c.q("Spine", x=-6 * k, y=2 * tr)
    pose["Neck"] = c.q("Neck", x=-14 * k, z=4 * tr)
    pose["Head"] = c.q("Head", x=-22 * k + 3 * tr)
    pose.update(K.merge(
        L.arm2(c, "L", forward=10, raise_=-10 + 40 * k + 4 * tr, elbow=30 + 20 * k, wrist=-20 * k),
        L.arm2(c, "R", forward=10, raise_=-10 + 40 * k - 4 * tr, elbow=30 + 20 * k, wrist=-20 * k),
    ))
    sc = 1.0 + 0.2 * g + 0.02 * tr
    pose["Belly"] = {"rot": c.q("Belly", y=2 * tr), "scale": (sc, sc * (1 - 0.03 * tr), sc)}
    return pose


def idle(ph):
    t = ph * math.tau
    br = math.sin(t)
    plant = {"L": L.planted(c, "L", dy=-0.05, dx=0.06), "R": L.planted(c, "R", dy=0.06, dx=0.06)}
    hips_R = L.eul(x=2, y=4 * math.sin(t), z=3 * math.sin(t + 0.8))
    pose, _ = L.biped_legs(c, 0.0, 0.0, 48, plant=plant, hips_R=hips_R, hips_off=(0.03 * math.sin(t), 0, -0.04 + 0.01 * br), pole_out=0.45)
    gurgle = L.pulse(ph, 0.62, 0.1)
    pose["Spine"] = c.q("Spine", x=-2, y=3 * math.sin(t))
    pose["Neck"] = c.q("Neck", z=12 * math.sin(t + 1.5), y=-3 * math.sin(t))
    pose["Head"] = c.q("Head", x=-6 - 6 * gurgle)
    pose.update(K.merge(
        L.arm2(c, "L", forward=8 + 3 * br, raise_=-12, elbow=35, wrist=15 + 15 * gurgle),
        L.arm2(c, "R", forward=8 + 3 * br, raise_=-12, elbow=35, wrist=15 + 15 * gurgle),
    ))
    sc = 1.0 + 0.025 * br + 0.03 * gurgle
    pose["Belly"] = {"rot": c.q("Belly", x=2 * gurgle), "scale": (sc, sc, sc)}
    return pose


walk = c.action("Walk", WALK_FRAMES, waddle)
c.action("Special", 22, swell, loop=False)
c.action("Idle", 48, idle)
c.rig.animation_data.action = walk

K.export_fbx([c.rig] + objs, "Characters/Kyste")
for o in objs:
    K.report(asset=o.name, tris=K.tri_count(o))
for name in ("Walk", "Special", "Idle"):
    L.check_anim(c, objs[0], bpy.data.actions[name])

L.preview("kyste", objs, frame=1, action=walk)
L.preview("kyste_walk", objs, action=walk, views=((0, 10),), extra_frames=[1, 3, 5, 7, 9, 11], size=280)
L.preview("kyste_walk_side", objs, action=walk, views=((90, 8),), extra_frames=[1, 4, 7, 10], size=280)
if not QUICK:
    L.preview("kyste_special", objs, action=bpy.data.actions["Special"], views=((40, 25),), extra_frames=[1, 8, 15, 23], size=280)
    L.preview("kyste_game", objs, frame=1, action=walk, views=((200, 60), (160, 60), (20, 60)), size=300)
    L.preview("kyste_idle", objs, action=bpy.data.actions["Idle"], views=((20, 20),), extra_frames=[1, 13, 20, 31], size=280)
