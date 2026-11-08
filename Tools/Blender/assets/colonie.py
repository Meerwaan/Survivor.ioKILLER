"""La Colonie (stage III, budding; Bestiaire 3.1): "Splitter" in the code.

The host "pushed" partial torsos like cuttings: three BUDS on the back, each a curled half-formed body
(a pale crusted dome, a mouth, three stubby clawed limbs: a Rejeton not yet dropped). Read from above:
a CLOVER (trefle). The buds squirm all the time ("ses torses s'agitent"), harder when it attacks.
On death the game swaps it for three Rejetons.

Extra bones: Bud1 (upper left back), Bud2 (upper right back), Bud3 (lower middle back), children of Chest,
head at the bud's root on the back, tail along its axis.
Anims: Walk, Attack (three quick strikes in 0.6 s), Idle.
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
skel = K.humanoid(height=1.9, bulk=1.05, hunch=0.6, arm=1.08, limb=1.0, head=1.0, shoulders=1.05)
for n in ("ankle.L", "ankle.R", "toe.L", "toe.R"):
    skel.joints[n][0] = skel.joints[n][0] + V((0, 0, 0.009))  # the kit's feet dip under z=0 when height > 1.75
J = {n: p for n, (p, r) in skel.joints.items()}

# buds: (bone, x on the back, z, size, lean of the axis)
BUDS = [("Bud1", 0.14, J["uchest"].z - 0.02, 0.17, (0.7, 1, 0.6)),
        ("Bud2", -0.14, J["uchest"].z - 0.04, 0.165, (-0.7, 1, 0.55)),
        ("Bud3", 0.0, J["spine"].z - 0.05, 0.155, (0.0, 1, 0.1))]
probe = K.skin_body(skel, subsurf=1)
BUD_FRAMES = {}
for bone, x, z, size, lean in BUDS:
    p, n = L.back_point(probe, x, z)
    axis = (n + V(lean) * 0.6).normalized()
    BUD_FRAMES[bone] = (p, axis, size)
    L.add_bone(skel, bone, p, p + axis * size * 1.6, "Chest")


def bud(bone, seed):
    """A curled half-formed body (a Rejeton not yet dropped): a dome of grey skin, its open mouth at the tip
    ringed with crust, three stubby clawed limbs radiating around it and gripping the host's back."""
    base, axis, size = BUD_FRAMES[bone]
    rnd = K.random.Random(seed)
    side = axis.cross(V((0, 0, 1)))
    side = side.normalized() if side.length > 0.1 else V((1, 0, 0))
    fwd = side.cross(axis).normalized()
    ctr = base + axis * size * 0.45
    parts = []

    def paint(cc, sw_):
        h = (cc - ctr).dot(axis) / size
        if h > 0.62:
            return "crust" if K.perlin(cc * 40) > 0 else "amber"
        if h < -0.25 and K.perlin(cc * 25 + V((3, 1, 2))) > 0.2:
            return "veins"
        return None
    dome = K.ico(1.0, (0, 0, 0), scale=(size, size * 0.95, size * 1.05), swatch="skin_ambre", subdiv=2, jitter=0.1, seed=seed, smooth=True)
    dome.bone = bone
    dome = L.oriented(dome, ctr, fwd, axis)
    L.paint_part(dome, paint)
    parts.append(dome)
    # the open mouth at the tip, a few teeth
    tip = ctr + axis * size * 0.97
    mo = K.ico(1.0, (0, 0, 0), scale=(size * 0.45, size * 0.4, size * 0.14), swatch="mouth", subdiv=1)
    mo.bone = bone
    parts.append(L.oriented(mo, tip, fwd, axis))
    for i in range(3):
        a = i / 3 * math.tau + rnd.random()
        q = tip + (side * math.cos(a) + fwd * math.sin(a)) * size * 0.32
        parts.append(K.shard(size * 0.22, size * 0.06, q, (axis - (q - tip).normalized() * 1.2), swatch="teeth", bone=bone, seed=seed + 5 + i, sides=3))
    # crust collar where it grows out of the host
    for i in range(2):
        a = i * math.pi + rnd.random()
        q = base + (side * math.cos(a) + fwd * math.sin(a)) * size * 0.8
        parts += K.plate_cluster(q, (axis * 0.7 + (q - base).normalized()).normalized(), 1, size * 0.5, swatch="crust_dark", bone=bone, seed=seed + 2 + i)
    # three stubby limbs radiating from the equator, bent down onto the host, amber claws
    for i in range(3):
        a = i / 3 * math.tau + math.radians(30) + rnd.uniform(-0.25, 0.25)
        out = side * math.cos(a) + fwd * math.sin(a)
        root = ctr + out * size * 0.7 + axis * size * 0.1
        dirr = (out - axis * 0.45).normalized()
        parts.append(K.shard(size * 0.85, size * 0.26, root, dirr, swatch="skin_ambre_dark", bone=bone, seed=seed + 10 + i, sides=4))
        parts.append(K.shard(size * 0.38, size * 0.11, root + dirr * size * 0.62, (dirr - axis * 0.8).normalized(), swatch="amber", bone=bone, seed=seed + 20 + i, sides=3))
    return parts


SKIN = "skin_ambre"
OUTFIT = {"Head": SKIN, "Neck": SKIN, "Chest": "hoodie_charcoal", "Spine": "hoodie_charcoal", "Shoulder": "hoodie_charcoal",
          "UpperArm": "hoodie_charcoal", "LowerArm": "hoodie_charcoal", "Hand": "skin_ambre_dark",
          "Hips": "cargo_khaki", "UpperLeg": "cargo_khaki", "LowerLeg": "cargo_khaki", "Foot": "boot_brown"}


def back_split(c, s, bone, t):
    """The buds burst through the hoodie: skin and black veins around each root."""
    for bn, (p, axis, size) in BUD_FRAMES.items():
        d = (c - p).length
        if d < size * 1.25:
            return "veins" if d < size * 0.95 else SKIN
    return None


body = K.skin_body(skel, subsurf=1)
K.outfit_faces(body, skel, OUTFIT, tear=0.12, tear_swatch=SKIN, seed=606,
               extra=L.chain(L.along(skel, {"LowerArm": [(0.6, "hoodie_charcoal"), (1.0, SKIN)]}), back_split))
parts = []
for i, (bone, *_r) in enumerate(BUDS):
    parts += bud(bone, 606 + 40 * i)
parts += L.sealed_face(body, J, 609)
for side in ("L", "R"):
    parts += L.crust_claws(J, side, 620 + (side == "R") * 5, length=0.1)
parts += K.plate_cluster(J["shoulder.R"] + V((-0.04, 0.02, 0.05)), (-0.5, 0.2, 1), 3, 0.06, swatch=["crust", "crust_dark"], seed=611)
for bn, (p, axis, size) in BUD_FRAMES.items():
    L.cull(body, L.ellipsoid_test(p + axis * size * 0.5, (size * 0.8, size * 0.8, size * 0.8)))

char = K.Character("Colonie", skel)
char.parts = [body] + parts
char.build(ao=0.6, grime=0.25, mud_height=0.42, seed=606)
char.mesh_obj.name = "Colonie_A"
objs = [char.mesh_obj]
c = char

# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------
WALK_FRAMES, WALK_SPEED, WALK_DUTY = 24, 2.0, 0.6


def squirm(ph, amp=1.0, fast=1):
    """The buds wriggle, each on its own rhythm (integer frequencies: loops stay seamless)."""
    t = ph * math.tau
    out = {}
    for i, (bone, *_r) in enumerate(BUDS):
        f1, f2, f3 = (2 + i) * fast, (3 + (i * 2) % 3) * fast, (1 + i) * fast
        rx_ = 16 * math.sin(f1 * t + i * 1.7)
        ry_ = 13 * math.sin(f2 * t + i * 0.9)
        rz_ = 10 * math.sin(f3 * t + i * 2.3)
        sc = 1.0 + 0.06 * math.sin(f1 * t + i * 2.1 + 1.0) * amp
        out[bone] = {"rot": c.q(bone, x=rx_ * amp, y=ry_ * amp, z=rz_ * amp), "scale": sc}
    return out


def walk(ph):
    t = ph * math.tau
    sL = math.cos(L.gait_angle(ph, WALK_DUTY))
    hips_R = L.eul(x=10, y=4 * sL, z=7 * sL)
    pose, _ = L.biped_legs(c, ph, WALK_SPEED, WALK_FRAMES, duty=WALK_DUTY, lift=0.13, center=0.02, toe_off=18, heel=6,
                           hips_R=hips_R, hips_off=(0, 0, -0.03 + 0.012 * math.cos(2 * t)))
    pose.update(K.merge(
        L.arm2(c, "L", forward=30 - 14 * sL, raise_=-10, elbow=30 + 10 * max(0, sL), wrist=10),
        L.arm2(c, "R", forward=30 + 14 * sL, raise_=-10, elbow=30 + 10 * max(0, -sL), wrist=10),
    ))
    pose["Spine"] = c.q("Spine", x=6, z=-5 * sL)
    pose["Chest"] = c.q("Chest", x=5, z=-7 * sL, y=3 * math.sin(t))
    pose["Neck"] = c.q("Neck", x=-4, y=5 * math.sin(t))
    pose["Head"] = c.q("Head", x=-12 + 4 * math.cos(2 * t))
    pose.update(squirm(ph))
    return pose


def strike(k, side, overhead=False):
    """One strike of the combo, k = 0..1: wind up (..0.4), hit (..0.65), back (..1)."""
    w = L.smooth(k / 0.4) * (1 - L.smooth((k - 0.4) / 0.25))
    h = L.smooth((k - 0.4) / 0.25) * (1 - L.smooth((k - 0.7) / 0.3))
    if overhead:
        return L.arm2(c, side, forward=30 + 120 * w + 10 * h, raise_=-10 + 20 * w, elbow=30 + 60 * w - 20 * h, wrist=10 - 30 * h), w, h
    return L.arm2(c, side, forward=30 + 55 * w + 60 * h, raise_=-10 + 60 * w - 25 * h, elbow=30 + 40 * w - 20 * h, twist=-30 * h, wrist=10 - 30 * h), w, h


def combo(ph):
    """Three quick strikes in 18 frames (0.6 s): right, left, right overhead. Hits land at frames ~4, 10, 16."""
    seq = [("R", False), ("L", False), ("R", True)]
    i = min(2, int(ph * 3))
    k = ph * 3 - i
    arms = K.merge(L.arm2(c, "L", forward=30, raise_=-10, elbow=30, wrist=10), L.arm2(c, "R", forward=30, raise_=-10, elbow=30, wrist=10))
    a, w, h = strike(k, *seq[i])
    arms.update(a)
    s = 1 if seq[i][0] == "L" else -1
    plant = {"L": L.planted(c, "L", dy=-0.2, dx=0.02), "R": L.planted(c, "R", dy=0.2, dx=0.02, heel_lift=8 * h)}
    hips_R = L.eul(x=10 + 6 * h, z=s * (10 * w - 14 * h))
    pose, _ = L.biped_legs(c, 0.0, 0.0, 18, plant=plant, hips_R=hips_R, hips_off=(0, 0.02 * w - 0.08 * h, -0.05 - 0.03 * h))
    pose.update(arms)
    pose["Spine"] = c.q("Spine", x=6 + 6 * h, z=s * (8 * w - 12 * h))
    pose["Chest"] = c.q("Chest", x=5 + 6 * h, z=s * (12 * w - 18 * h))
    pose["Neck"] = c.q("Neck", x=-4)
    pose["Head"] = c.q("Head", x=-12 - 4 * h)
    pose.update(squirm(ph, amp=1.5, fast=2))
    return pose


def idle(ph):
    t = ph * math.tau
    plant = {"L": L.planted(c, "L", dy=-0.08, dx=0.02), "R": L.planted(c, "R", dy=0.1, dx=0.02)}
    hips_R = L.eul(x=10, y=3 * math.sin(t), z=3 * math.sin(t + 0.4))
    pose, _ = L.biped_legs(c, 0.0, 0.0, 48, plant=plant, hips_R=hips_R, hips_off=(0.02 * math.sin(t), 0, -0.04 + 0.008 * math.sin(2 * t)))
    pose.update(K.merge(
        L.arm2(c, "L", forward=24 + 3 * math.sin(2 * t), raise_=-10, elbow=30, wrist=10),
        L.arm2(c, "R", forward=20 + 3 * math.sin(2 * t), raise_=-10, elbow=26, wrist=10),
    ))
    pose["Spine"] = c.q("Spine", x=6 + 2 * math.sin(2 * t))
    pose["Chest"] = c.q("Chest", x=5, y=3 * math.sin(t))
    pose["Neck"] = c.q("Neck", x=-4, z=12 * math.sin(t))
    pose["Head"] = c.q("Head", x=-12, y=6 * math.sin(t + 1))
    pose.update(squirm(ph, amp=0.8))
    return pose


walk_act = c.action("Walk", WALK_FRAMES, walk)
c.action("Attack", 18, combo, loop=False)
c.action("Idle", 48, idle)
c.rig.animation_data.action = walk_act

K.export_fbx([c.rig] + objs, "Characters/Colonie")
for o in objs:
    K.report(asset=o.name, tris=K.tri_count(o))
for name in ("Walk", "Attack", "Idle"):
    L.check_anim(c, objs[0], bpy.data.actions[name])

L.preview("colonie", objs, frame=1, action=walk_act)
L.preview("colonie_walk", objs, action=walk_act, views=((90, 8),), extra_frames=[1, 4, 7, 10, 13], size=300)
if not QUICK:
    L.preview("colonie_attack", objs, action=bpy.data.actions["Attack"], views=((30, 20),), extra_frames=[1, 3, 5, 9, 11, 16], size=300)
    L.preview("colonie_game", objs, frame=1, action=walk_act, views=((200, 60), (160, 60), (20, 60)), size=300)
    L.preview("colonie_buds", objs, action=walk_act, views=((180, 35),), extra_frames=[1, 5, 9, 13], size=300)
    L.preview("colonie_idle", objs, action=bpy.data.actions["Idle"], views=((150, 30),), extra_frames=[1, 13, 25, 37], size=280)
