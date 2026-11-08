"""Le Cracheur (stage III, the throat; Bestiaire 3.1): "Spitter" in the code.

The Seve invaded the throat: a GOITRE, a translucent sac of amber sap under pressure, wider than the head,
crusted collar behind the neck. Read from above: a DROP (the round goitre in front, the hunched body
tapering behind). Hunched, torn work jacket. Ranged: the sac swells and lights up 0.5 s, then a hiccup
throws the head forward and the spit leaves the mouth.

Extra bones: Sac (child of Neck, the outer sac, scaled by the clips), SacGlow (child of Sac, the bright core:
when it grows past the outer skin the sac "lights up").
Anims: Walk, Special (spit, 24 f), Attack (quick claw), Idle (sac pulsing).
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
skel = K.humanoid(height=1.82, bulk=0.95, hunch=0.85, arm=1.08, limb=0.95, head=1.0, shoulders=0.92, hips=0.95)
JR = skel.joints
JR["neck"] = [JR["neck"][0], (0.085, 0.08)]
J = {n: p for n, (p, r) in JR.items()}

# the sac hangs under the jaw, in front of the neck, and bulges out on both sides past the head
SAC_C = J["neck"] + V((0, -0.05, -0.03))
SAC_R = V((0.21, 0.165, 0.14))
L.add_bone(skel, "Sac", SAC_C + V((0, 0.07, 0.02)), SAC_C + V((0, -0.08, -0.04)), "Neck")
L.add_bone(skel, "SacGlow", SAC_C, SAC_C + V((0, -0.1, -0.03)), "Sac")


def sac_parts(seed):
    rnd = K.random.Random(seed)
    o = V((rnd.random() * 30, rnd.random() * 30, rnd.random() * 30))

    def skin_veins(c, s):
        d = c - SAC_C
        # thin dark veins, darker amber mottling, stretched grey skin underneath (the top stays glowing: the camera sees it)
        n = K.perlin(d * 11 + o)
        if abs(n) < 0.022:
            return "veins"
        if abs(n) < 0.05:
            return "crust_dark"
        if d.z < -SAC_R.z * 0.55 or (d.z < -SAC_R.z * 0.2 and d.y > SAC_R.y * 0.3):
            return "skin_ambre"
        if n > 0.32:
            return "amber"
        return None
    outer = L.blob(1.0, SAC_C, scale=tuple(SAC_R), subdiv=3, jitter=0.05, seed=seed, swatch="amber_glow_soft", bone="Sac", paint=skin_veins)
    core = L.blob(1.0, SAC_C + V((0, -0.01, -0.01)), scale=tuple(SAC_R * 0.86), subdiv=2, jitter=0.16, seed=seed + 1, swatch="amber_glow", bone="SacGlow")
    return [outer, core]


def crust_bits(body, seed):
    parts = sac_parts(seed)
    # two crust horns on the nape, where the goitre wraps the neck
    for i, x in enumerate((-0.1, 0.1)):
        parts.append(K.shard(0.1, 0.03, J["neck"] + V((x, 0.05, 0.03)), (x * 4, 0.7, 1.0), swatch="crust", bone="Neck", seed=seed + 9 + i))
    # sealed eyes, open mouth, a string of hardening sap from the lip
    parts += L.sealed_face(body, J, seed + 3, mouth_w=1.1, jaw_drop=0.006)
    m, mn = L.face_point(body, J, 0.012, -0.05, ref="head")
    parts.append(K.shard(0.05, 0.01, m + V((0, 0.0, 0.0)), (0.1, -0.3, -1), swatch="amber", bone="Head", seed=seed + 4, sides=3))
    # crust claws, a plate on the right shoulder, the left forearm
    for side in ("L", "R"):
        parts += L.crust_claws(J, side, seed + 20 + (side == "R") * 5)
    parts += K.plate_cluster(J["shoulder.R"] + V((-0.03, 0.03, 0.05)), (-0.5, 0.3, 1), 4, 0.06, swatch=["crust", "amber", "crust_dark"], seed=seed + 5)
    mid = (J["elbow.L"] + J["wrist.L"]) / 2
    parts += K.plate_cluster(mid + V((0.03, 0, 0.01)), (1, 0, 0.3), 3, 0.04, swatch=["crust", "amber"], seed=seed + 6)
    return parts


SKIN = "skin_ambre"
OUTFIT = {"Head": SKIN, "Neck": SKIN, "Chest": "canvas_olive", "Spine": "canvas_olive",
          "Shoulder": "canvas_olive", "UpperArm": "canvas_olive", "LowerArm": "canvas_olive", "Hand": "skin_ambre_dark",
          "Hips": "cargo_grey", "UpperLeg": "cargo_grey", "LowerLeg": "cargo_grey", "Foot": "boot_brown"}
def open_jacket(c, s, bone, t):
    """The jacket hangs open on a dirty undershirt."""
    if bone in ("Chest", "Spine") and abs(c.x) < 0.075 and c.y < J["spine"].y - 0.06:
        return "shirt_white_dirty"
    return None


VEINS = L.veins_near([(SAC_C, 0.2), (J["shoulder.R"], 0.12), ((J["elbow.L"] + J["wrist.L"]) / 2, 0.08), (J["skull"] + V((0, -0.08, 0)), 0.1)])

body = K.skin_body(skel, subsurf=1)
K.outfit_faces(body, skel, OUTFIT, tear=0.07, tear_swatch=SKIN, seed=404, extra=L.chain(L.along(skel, {"LowerArm": [(0.55, "canvas_olive"), (1.0, SKIN)]}), open_jacket, VEINS))
bits = crust_bits(body, 404)
L.cull(body, L.ellipsoid_test(SAC_C, SAC_R, 0.9))
char = K.Character("Cracheur", skel)
char.parts = [body] + bits
char.build(ao=0.55, grime=0.25, mud_height=0.4, seed=404)
char.mesh_obj.name = "Cracheur_A"
objs = [char.mesh_obj]
c = char

# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------
WALK_FRAMES, WALK_SPEED, WALK_DUTY = 24, 2.0, 0.6


def sac(scale, glow):
    """Sac scale (absolute) and the core's scale relative to the sac (>1.16 or so = breaches the skin)."""
    return {"Sac": {"rot": K.Quaternion(), "scale": scale}, "SacGlow": {"rot": K.Quaternion(), "scale": glow}}


def walk(ph):
    t = ph * math.tau
    sL = math.cos(L.gait_angle(ph, WALK_DUTY))
    hips_R = L.eul(x=10, y=3 * sL, z=6 * sL)
    pose, _ = L.biped_legs(c, ph, WALK_SPEED, WALK_FRAMES, duty=WALK_DUTY, lift=0.13, center=0.02, toe_off=20, heel=6,
                           hips_R=hips_R, hips_off=(0, 0, -0.035 + 0.012 * math.cos(2 * t)))
    pose.update(K.merge(
        L.arm2(c, "L", forward=18 - 16 * sL, raise_=-14, elbow=30 + 8 * max(0, sL), wrist=10),
        L.arm2(c, "R", forward=18 + 16 * sL, raise_=-14, elbow=30 + 8 * max(0, -sL), wrist=10),
    ))
    pose["Spine"] = c.q("Spine", x=6, z=-4 * sL)
    pose["Chest"] = c.q("Chest", x=4, z=-5 * sL)
    pose["Neck"] = c.q("Neck", x=-6, y=4 * math.sin(t))
    pose["Head"] = c.q("Head", x=-14 + 4 * math.cos(2 * t))
    # the sac sloshes with the steps
    pose.update(sac(1.0 + 0.035 * math.cos(2 * t + 0.8), 0.86))
    return pose


def spit(ph):
    """0..0.62 (0.5 s): the sac swells and lights up, the body rears to load. 0.62..0.75: the hiccup throws the
    head forward (spit leaves the mouth at ~frame 17). 0.75..1: recover. 24 frames."""
    load = L.smooth(ph / 0.62)
    hic = L.env(ph, 0.62, 0.7, 0.74, 1.0)
    rel = L.smooth((ph - 0.64) / 0.12)   # pressure released
    plant = {"L": L.planted(c, "L", dy=-0.14), "R": L.planted(c, "R", dy=0.2)}
    hips_R = L.eul(x=10 - 6 * load * (1 - rel) + 6 * hic)
    pose, _ = L.biped_legs(c, 0.0, 0.0, 24, plant=plant, hips_R=hips_R,
                           hips_off=(0, 0.03 * load * (1 - rel) - 0.06 * hic, -0.04 - 0.02 * load * (1 - rel) - 0.03 * hic))
    tremble = math.sin(ph * 90) * load * (1 - rel)
    pose.update(K.merge(
        L.arm2(c, "L", forward=14 - 12 * load * (1 - rel) + 20 * hic, raise_=-10 + 14 * load * (1 - rel), elbow=35, wrist=10, shrug=10 * load * (1 - rel)),
        L.arm2(c, "R", forward=14 - 12 * load * (1 - rel) + 20 * hic, raise_=-10 + 14 * load * (1 - rel), elbow=35, wrist=10, shrug=10 * load * (1 - rel)),
    ))
    pose["Spine"] = c.q("Spine", x=6 - 10 * load * (1 - rel) + 10 * hic)
    pose["Chest"] = c.q("Chest", x=4 - 8 * load * (1 - rel) + 10 * hic)
    pose["Neck"] = c.q("Neck", x=-6 - 18 * load * (1 - rel) + 38 * hic, y=2 * tremble)
    pose["Head"] = c.q("Head", x=-14 - 16 * load * (1 - rel) + 22 * hic)
    s = 1.0 + 0.36 * load ** 1.5 * (1 - rel) - 0.2 * hic * rel + 0.015 * tremble
    g = 0.86 + 0.29 * load ** 2 * (1 - rel)
    pose.update(sac(s, g))
    return pose


def claw(ph):
    """Quick right-hand slash (0.4 s)."""
    a = L.smooth(ph / 0.3)
    b = L.smooth((ph - 0.3) / 0.2)
    r = L.smooth((ph - 0.55) / 0.45)
    wind, hit = a * (1 - b), b * (1 - r)
    plant = {"L": L.planted(c, "L", dy=-0.16), "R": L.planted(c, "R", dy=0.18, heel_lift=10 * hit)}
    hips_R = L.eul(x=10 + 8 * hit, z=-12 * wind + 14 * hit)
    pose, _ = L.biped_legs(c, 0.0, 0.0, 12, plant=plant, hips_R=hips_R, hips_off=(0, 0.03 * wind - 0.08 * hit, -0.04 - 0.03 * hit))
    pose.update(K.merge(
        L.arm2(c, "L", forward=20 + 10 * hit, raise_=-12, elbow=35, wrist=10),
        L.arm2(c, "R", forward=18 + 95 * wind + 10 * hit, raise_=-12 + 50 * wind - 10 * hit, elbow=30 + 50 * wind - 20 * hit, twist=-20 * hit, wrist=10 - 25 * hit),
    ))
    pose["Spine"] = c.q("Spine", x=6 + 8 * hit, z=-10 * wind + 16 * hit)
    pose["Chest"] = c.q("Chest", x=4 + 6 * hit, z=-14 * wind + 22 * hit)
    pose["Neck"] = c.q("Neck", x=-6)
    pose["Head"] = c.q("Head", x=-14 - 6 * hit)
    pose.update(sac(1.0 + 0.06 * hit, 0.86))
    return pose


def idle(ph):
    t = ph * math.tau
    br = math.sin(2 * t)
    plant = {"L": L.planted(c, "L", dy=-0.08, dx=0.02), "R": L.planted(c, "R", dy=0.12, dx=0.02)}
    hips_R = L.eul(x=10, y=2 * math.sin(t), z=3 * math.sin(t + 0.5))
    pose, _ = L.biped_legs(c, 0.0, 0.0, 40, plant=plant, hips_R=hips_R, hips_off=(0.02 * math.sin(t), 0, -0.045 + 0.008 * br))
    pose.update(K.merge(
        L.arm2(c, "L", forward=16 + 3 * br, raise_=-12, elbow=32, wrist=12),
        L.arm2(c, "R", forward=12 + 3 * br, raise_=-12, elbow=28, wrist=12),
    ))
    gulp = L.pulse(ph, 0.55, 0.12)
    pose["Spine"] = c.q("Spine", x=6 + 2 * br)
    pose["Chest"] = c.q("Chest", x=4 + 2 * br)
    pose["Neck"] = c.q("Neck", x=-6 - 6 * gulp, z=10 * math.sin(t))
    pose["Head"] = c.q("Head", x=-14 - 8 * gulp, y=6 * math.sin(t + 1))
    # the sac pulses gently: two slow beats, a gulp
    pose.update(sac(1.0 + 0.045 * math.sin(2 * t) + 0.06 * gulp, 0.86 + 0.05 * gulp))
    return pose


walk_act = c.action("Walk", WALK_FRAMES, walk)
c.action("Special", 24, spit, loop=False)
c.action("Attack", 12, claw, loop=False)
c.action("Idle", 40, idle)
c.rig.animation_data.action = walk_act

K.export_fbx([c.rig] + objs, "Characters/Cracheur")
for o in objs:
    K.report(asset=o.name, tris=K.tri_count(o))
for name in ("Walk", "Special", "Attack", "Idle"):
    L.check_anim(c, objs[0], bpy.data.actions[name])

L.preview("cracheur", objs, frame=1, action=walk_act)
L.preview("cracheur_walk", objs, action=walk_act, views=((90, 8),), extra_frames=[1, 4, 7, 10, 13], size=300)
L.preview("cracheur_special", objs, action=bpy.data.actions["Special"], views=((60, 25),), extra_frames=[1, 9, 15, 17, 19, 25], size=300)
if not QUICK:
    L.preview("cracheur_attack", objs, action=bpy.data.actions["Attack"], views=((90, 8),), extra_frames=[1, 4, 7, 9, 13], size=300)
    L.preview("cracheur_game", objs, frame=1, action=walk_act, views=((200, 60), (160, 60), (20, 60)), size=300)
    L.preview("cracheur_game_glow", objs, frame=15, action=bpy.data.actions["Special"], views=((200, 60), (20, 60)), size=300)
    L.preview("cracheur_idle", objs, action=bpy.data.actions["Idle"], views=((40, 20),), extra_frames=[1, 11, 22, 31], size=280)
