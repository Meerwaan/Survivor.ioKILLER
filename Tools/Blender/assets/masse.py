"""La Masse (stage III, the arms; Bestiaire 3.1): "Brute" in the code.

A big host (an ex-steelworker of the Combinat) whose arms were over-built: ~2.45 m, two massive crust
EPAULIERES (pauldrons of plates) on the shoulders, a plated back, club forearms with crusted fists that
hang to the ground, a small head sunk between the shoulders. Torn Combinat workwear (blue, reflective tape).
Read from above: a SQUARE.
Anims: Walk (heavy stomp, weight shift), Attack (overhead double-fist slam), Idle.
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
skel = K.humanoid(height=2.85, bulk=1.55, hunch=0.85, arm=1.72, leg=0.74, head=0.7, shoulders=1.02, limb=1.18, hips=1.25, arms_out=12)
JR = skel.joints


def set_r(name, rx, ry=None):
    JR[name] = [JR[name][0], (rx, ry if ry is not None else rx)]


# the head sinks between the shoulders, pushed forward
for n, (dz, dy) in {"neck": (-0.12, -0.08), "head": (-0.13, -0.15), "skull": (-0.13, -0.15), "head_top": (-0.13, -0.145)}.items():
    JR[n][0] = JR[n][0] + V((0, dy, dz))
# explicit radii (rx, ry): a barrel chest, a thick trapezius up to the head, club forearms, pillar legs
for n, r in {"pelvis": (0.27, 0.22), "waist": (0.27, 0.24), "spine": (0.31, 0.27), "chest": (0.34, 0.3), "uchest": (0.32, 0.28),
             "neck": (0.115, 0.11), "head": (0.1, 0.105), "skull": (0.125, 0.135), "head_top": (0.085, 0.095)}.items():
    set_r(n, *r)
for side in ("L", "R"):
    for n, r in {"shoulder": (0.14,), "bicep": (0.11, 0.115), "elbow": (0.095,), "forearm": (0.19, 0.17), "wrist": (0.14,),
                 "hand": (0.17, 0.13), "fingers": (0.15, 0.115), "hip": (0.19,), "thigh": (0.18, 0.19), "knee": (0.13,),
                 "calf": (0.14, 0.15), "ankle": (0.1,), "toe": (0.11, 0.08)}.items():
        set_r(n + "." + side, *r)
J = {n: p for n, (p, r) in JR.items()}
SX = {"L": 1, "R": -1}
PAUL = {}  # side -> (center, radii) of the pauldron dome, for culling the hidden shoulder


def pauldron(side, seed):
    """The epauliere: an amber dome over the shoulder shingled with big dark crust plates (amber shows in the
    seams), plates hanging over the upper arm and the upper back, two amber spikes. Rigid on Shoulder."""
    s = SX[side]
    bone = "Shoulder." + side
    sh = J["shoulder." + side]
    c0 = sh + V((0.03 * s, 0.05, 0.12))
    radii = V((0.25, 0.25, 0.17))
    PAUL[side] = (c0, radii)
    parts = [K.ico(1.0, c0, scale=tuple(radii), swatch="amber", bone=bone, subdiv=2, jitter=0.06, seed=seed)]
    rnd = K.random.Random(seed)
    k = 0
    tones = ["crust_dark", "crust_dark", "crust", "crust_black", "crust_dark", "crust_black", "crust_dark", "crust_black", "crust_dark"]
    for elev, n, size in ((88, 1, 0.18), (48, 4, 0.16), (18, 4, 0.12)):
        for i in range(n):
            az = math.radians(i * 360.0 / n + elev + rnd.uniform(-12, 12))
            e = math.radians(elev + rnd.uniform(-6, 6))
            d = V((math.cos(az) * math.cos(e), math.sin(az) * math.cos(e), math.sin(e)))
            pos = c0 + V((d.x * radii.x * 1.02 * s, d.y * radii.y * 1.02, d.z * radii.z * 1.02))
            nor = V((d.x / radii.x * s, d.y / radii.y, d.z / radii.z)).normalized()
            if elev < 20:
                pos += V((0, 0, 0.01))
                nor = (nor + V((0, 0, 0.9))).normalized()
            sz = size * rnd.uniform(0.9, 1.15)
            m = K.Matrix.Translation(pos) @ nor.to_track_quat("Z", "Y").to_matrix().to_4x4() @ K.Matrix.Rotation(rnd.uniform(0, 6.3), 4, "Z")                 @ K.Matrix.Diagonal((sz * 1.3, sz, sz * 0.3, 1))
            pl = K.ico(1.0, (0, 0, 0), swatch=tones[k % len(tones)], bone=bone, subdiv=1, jitter=0.12, seed=seed + 20 + k)
            parts.append(pl.transformed(m))
            k += 1
    for i, (dx, dy, ln) in enumerate(((0.05, 0.1, 0.22), (0.18, -0.04, 0.16))):
        parts.append(K.shard(ln, 0.055, c0 + V((dx * s, dy, 0.13)), (0.5 * s, 0.4 + dy, 1.0), swatch="amber", bone=bone, seed=seed + 10 + i))
    return parts


def crust_bits(body, seed):
    parts = pauldron("L", seed) + pauldron("R", seed + 50)
    # the plated back between the pauldrons
    zc, zs = J["uchest"].z, J["spine"].z
    rays = [((x, 1.5, z), (0, -1, -0.25)) for x, z in ((0.0, zc + 0.02), (-0.17, zc - 0.08), (0.16, zc - 0.12), (0.03, zc - 0.22), (0.12, zs))]
    parts += L.plates_on(body, rays, 0.15, ["crust_dark", "crust", "crust_black", "crust_dark", "crust"], bone="Chest", seed=seed + 3)
    # club forearms: plates on the outside, crust knuckles
    for side in ("L", "R"):
        s = SX[side]
        e, w = J["elbow." + side], J["wrist." + side]
        rays = [(e.lerp(w, k) + V((0.8 * s, 0.3, 0.25)), V((-0.8 * s, -0.3, -0.25))) for k in (0.35, 0.65)]
        parts += L.plates_on(body, rays, 0.11, ["crust_dark", "amber"], bone="LowerArm." + side, seed=seed + 5 + (s > 0))
        parts += L.crust_claws(J, side, seed + 30, n=4, length=0.11, width=0.03, swatch="amber", curl=-0.25, spread=0.06)
    # left thigh: a crust patch (asymmetric)
    p, n = L.surface(body, (J["thigh.L"].x + 0.6, J["thigh.L"].y - 0.2, J["thigh.L"].z), (-1, 0.3, 0))
    parts += K.plate_cluster(p, n, 1, 0.1, swatch="crust_dark", seed=seed + 6)
    parts += L.sealed_face(body, J, seed + 7, mouth_w=1.3)
    p, n = L.surface(body, J["skull"] + V((0, -0.4, 0.4)), (0, 1, -1))
    parts += K.plate_cluster(p, n, 2, 0.08, swatch=["crust", "crust_dark"], bone="Head", seed=seed + 8, spread=0.8)
    return parts


def bands(c, s, bone, t):
    """Combinat workwear: reflective tape round the jacket's hem and the trouser shins."""
    if L.kind(s) != "cloth":
        return None
    if bone == "Spine" and abs(c.z - (J["waist"].z + 0.06)) < 0.045:
        return "reflective"
    if bone.startswith("LowerLeg") and abs(c.z - J["knee.L"].z * 0.5) < 0.04:
        return "reflective"
    if bone.startswith("UpperArm") and abs(c.z - J["elbow.L"].z - 0.22) < 0.04:
        return "reflective"
    return None


SKIN = "skin_ambre_dark"
OUTFIT = {"Head": SKIN, "Neck": SKIN, "Chest": "workwear_blue", "Spine": "workwear_blue", "Shoulder": SKIN,
          "UpperArm": "workwear_blue", "LowerArm": SKIN, "Hand": "crust_dark",
          "Hips": "workwear_blue_dark", "UpperLeg": "workwear_blue_dark", "LowerLeg": "workwear_blue_dark", "Foot": "boot_brown"}

body = K.skin_body(skel, subsurf=1)
VEINS = L.chain(L.noise_patches("veins", scale=9.0, thresh=0.42, only=("skin",), seed=9,
                                region=lambda c: c.z > J["elbow.L"].z - 0.2),
                L.noise_patches("crust_dark", scale=6.0, thresh=0.45, only=("skin",), seed=19))
K.outfit_faces(body, skel, OUTFIT, tear=0.16, tear_swatch=SKIN, seed=303, extra=L.chain(L.along(skel, {"LowerArm": [(0.1, "workwear_blue"), (1.0, SKIN)]}), bands, VEINS))
bits = crust_bits(body, 303)
for side in ("L", "R"):
    cc, rr = PAUL[side]
    inside = L.ellipsoid_test(cc, rr, 0.92)
    L.cull(body, lambda v, f=inside: f(v))
parts = [body] + bits

char = K.Character("Masse", skel)
char.parts = parts
char.build(ao=0.6, grime=0.25, mud_height=0.5, seed=303)
char.mesh_obj.name = "Masse_A"
objs = [char.mesh_obj]
c = char

# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------
WALK_FRAMES, WALK_SPEED, WALK_DUTY = 32, 1.7, 0.64


def stance_w(u, duty):
    return math.sin(math.pi * u / duty) if u < duty else 0.0


def stomp(ph):
    uL, uR = ph, (ph + 0.5) % 1.0
    sL = math.cos(L.gait_angle(uL, WALK_DUTY))
    # weight over the stance leg; a jolt when a foot slams down
    shift = stance_w(uL, WALK_DUTY) - stance_w(uR, WALK_DUTY)
    imp = max(L.pulse(uL, 0.05, 0.2), L.pulse(uR, 0.05, 0.2))
    hips_R = L.eul(x=8, y=6 * shift, z=7 * sL)
    pose, _ = L.biped_legs(c, ph, WALK_SPEED, WALK_FRAMES, duty=WALK_DUTY, lift=0.2, center=0.03, width=0.03, toe_off=12, heel=0,
                           hips_R=hips_R, hips_off=(0.07 * shift, 0, -0.02 - 0.05 * imp), shape=1.7, roll=0.25)
    # heavy arms swing like pendulums, a little late
    lag = math.cos(L.gait_angle((ph - 0.07) % 1.0, WALK_DUTY))
    pose.update(K.merge(
        L.arm2(c, "L", forward=14 - 20 * lag, raise_=-4 + 4 * shift, elbow=14 + 6 * imp, wrist=8, shrug=-4 * shift),
        L.arm2(c, "R", forward=14 + 20 * lag, raise_=-4 - 4 * shift, elbow=14 + 6 * imp, wrist=8, shrug=4 * shift),
    ))
    pose["Spine"] = c.q("Spine", x=6 + 3 * imp, y=-3 * shift, z=-5 * sL)
    pose["Chest"] = c.q("Chest", x=4, y=-3 * shift, z=-6 * sL)
    pose["Neck"] = c.q("Neck", x=-4 + 6 * imp)
    pose["Head"] = c.q("Head", x=-10 - 4 * imp, z=4 * sL)
    return pose


def slam(ph):
    """Both fists up over the head (0..0.42), down onto the ground in front (..0.56), hold, recover. 18 frames.
    The impact pose is solved by IK: the fists land ON the ground ~1 m in front."""
    up = L.smooth(ph / 0.42)
    down = L.smooth((ph - 0.42) / 0.14)
    rec = L.smooth((ph - 0.72) / 0.28)
    k_up = up * (1 - down)
    k_dn = down * (1 - rec)
    plant = {"L": L.planted(c, "L", dy=-0.22, dx=0.06), "R": L.planted(c, "R", dy=0.28, dx=0.06, heel_lift=12 * k_dn)}
    hips_R = L.eul(x=8 - 8 * k_up + 20 * k_dn)
    pose, _ = L.biped_legs(c, 0.0, 0.0, 18, plant=plant, hips_R=hips_R,
                           hips_off=(0, 0.05 * k_up - 0.1 * k_dn, -0.05 - 0.03 * k_up - 0.16 * k_dn))
    pose["Spine"] = c.q("Spine", x=6 - 16 * k_up + 16 * k_dn)
    pose["Chest"] = c.q("Chest", x=4 - 8 * k_up + 10 * k_dn)
    pose["Neck"] = c.q("Neck", x=-4 + 6 * k_up - 10 * k_dn)
    pose["Head"] = c.q("Head", x=-10 - 12 * k_up + 6 * k_dn)
    fk_arms = {}
    for side in ("L", "R"):
        fk_arms.update(L.arm2(c, side, forward=14 + 150 * k_up, raise_=-4 - 10 * k_up, elbow=14 + 45 * k_up, wrist=8 - 20 * k_up,
                              shrug=22 * k_up))
    if k_dn > 0:
        pose.update({k: v for k, v in fk_arms.items() if k.startswith("Shoulder")})
        ik_arms = {}
        for side in ("L", "R"):
            s = SX[side]
            shp = L.point_on(c, pose, "Shoulder." + side, L.tail(c, "Shoulder." + side))
            reach = (L.tail(c, "UpperArm." + side) - L.head(c, "UpperArm." + side)).length + (L.tail(c, "LowerArm." + side) - L.head(c, "LowerArm." + side)).length
            zt = 0.47
            hor = math.sqrt(max(0.05, (0.97 * reach) ** 2 - (shp.z - zt) ** 2))
            tgt = V((0.38 * s, shp.y - hor, zt))
            ik_arms.update(L.limb_ik(c, pose, "UpperArm." + side, "LowerArm." + side, tgt, (s, 0.6, 0.2)))
            ik_arms["Hand." + side] = c.q("Hand." + side, x=0)
        start = {}
        for side in ("L", "R"):
            start.update(L.arm2(c, side, forward=164 if rec == 0 else 14, raise_=-14 if rec == 0 else -4, elbow=59 if rec == 0 else 14,
                                wrist=-12 if rec == 0 else 8))
        arms = L.blend(start, ik_arms, k_dn)
        pose.update({k: v for k, v in arms.items() if not k.startswith("Shoulder")})
    else:
        pose.update(fk_arms)
    return pose


def idle(ph):
    t = ph * math.tau
    br = math.sin(2 * t)            # two heavy breaths a loop
    sh = math.sin(t)                # slow weight shift
    snort = L.pulse(ph, 0.7, 0.08)  # a head toss
    plant = {"L": L.planted(c, "L", dy=-0.12, dx=0.05), "R": L.planted(c, "R", dy=0.14, dx=0.05)}
    hips_R = L.eul(x=8, y=3 * sh, z=3 * math.sin(t + 1.0))
    pose, _ = L.biped_legs(c, 0.0, 0.0, 48, plant=plant, hips_R=hips_R, hips_off=(0.03 * sh, 0, -0.06 + 0.012 * br))
    pose.update(K.merge(
        L.arm2(c, "L", forward=16 + 3 * br, raise_=-3, elbow=16 - 3 * br, wrist=10 + 8 * math.sin(t * 3), shrug=6 * br),
        L.arm2(c, "R", forward=14 + 3 * br, raise_=-3, elbow=18 - 3 * br, wrist=10 + 8 * math.sin(t * 3 + 2), shrug=6 * br),
    ))
    pose["Spine"] = c.q("Spine", x=6 - 2 * br, y=-2 * sh)
    pose["Chest"] = c.q("Chest", x=4 - 2.5 * br)
    pose["Neck"] = c.q("Neck", x=-4 - 8 * snort, z=8 * math.sin(t))
    pose["Head"] = c.q("Head", x=-10 + 3 * br - 10 * snort, y=8 * snort)
    return pose


walk = c.action("Walk", WALK_FRAMES, stomp)
c.action("Attack", 18, slam, loop=False)
c.action("Idle", 48, idle)
c.rig.animation_data.action = walk

K.export_fbx([c.rig] + objs, "Characters/Masse")
for o in objs:
    K.report(asset=o.name, tris=K.tri_count(o))
for name in ("Walk", "Attack", "Idle"):
    L.check_anim(c, objs[0], bpy.data.actions[name])

L.preview("masse", objs, frame=1, action=walk)
L.preview("masse_walk", objs, action=walk, views=((90, 8),), extra_frames=[1, 5, 9, 13, 17], size=300)
if not QUICK:
    L.preview("masse_attack", objs, action=bpy.data.actions["Attack"], views=((90, 8),), extra_frames=[1, 5, 8, 11, 14, 19], size=300)
    L.preview("masse_attack_front", objs, action=bpy.data.actions["Attack"], views=((25, 30),), extra_frames=[8, 12], size=300)
    L.preview("masse_idle", objs, action=bpy.data.actions["Idle"], views=((60, 15),), extra_frames=[1, 13, 25, 37], size=300)
    L.preview("masse_game", objs, frame=1, action=walk, views=((200, 60), (160, 60), (20, 60)), size=300)
