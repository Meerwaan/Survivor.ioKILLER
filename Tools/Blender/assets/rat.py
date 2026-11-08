"""Le Rat (la Nuee, Bestiaire 3.2): swarms of 8 to 12 rats from the flooded metro, weak bites.

The first animals contaminated by the water. Natural rat silhouette, oversized for the top camera
(~0.55 m long with the tail, ~0.2 m high), wet grey-brown fur, a row of small amber dorsal plates down
the spine (the animals' "plaques dorsales"), amber incisors ("crocs d'ambre"), a pink-grey tail.
Read from above: a grey-brown teardrop with a line of amber dots along its back and a long tail.

Custom quadruped skeleton (K.Skeleton): Hips (root, pelvis), Spine, Neck, Head, Jaw, Tail1-3,
FrontUpper/FrontLower.L/R, HindUpper/HindLower.L/R. Clips: Run (loop: a trot scurry, diagonal pairs, spine
flex, tail wave, paws planted by IK), Attack (lunge bite), Idle (sniff). <= 900 tris.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _souche_lib as S  # noqa: E402

K = S.K
V = K.Vector

K.reset()

# ---------------------------------------------------------------------------------------------
# Skeleton (faces -Y, paws at Z = 0)
# ---------------------------------------------------------------------------------------------
sk = K.Skeleton()
sk.joint("pelvis", (0, 0.07, 0.108), (0.072, 0.066))
sk.joint("mid", (0, 0.0, 0.116), (0.07, 0.066))
sk.joint("chest", (0, -0.07, 0.108), (0.06, 0.057))
sk.joint("neck", (0, -0.112, 0.102), (0.046, 0.045))
sk.joint("head", (0, -0.148, 0.098), (0.042, 0.044))
sk.joint("snout", (0, -0.2, 0.082), (0.017, 0.017))
sk.joint("jaw", (0, -0.188, 0.066), (0.012, 0.012))
sk.joint("rump", (0, 0.122, 0.098), (0.06, 0.056))
sk.joint("tail1", (0, 0.152, 0.092), 0.017)
sk.joint("tail2", (0, 0.212, 0.078), 0.013)
sk.joint("tail3", (0, 0.28, 0.054), 0.009)
sk.joint("tail4", (0, 0.35, 0.03), 0.005)
# hind legs: haunch, knee forward, hock back, toes forward
sk.joint("haunch.L", (0.04, 0.098, 0.08), 0.04)
sk.joint("knee.L", (0.05, 0.05, 0.05), 0.019)
sk.joint("ankle.L", (0.05, 0.09, 0.03), 0.012)
sk.joint("toe.L", (0.05, 0.064, 0.006), 0.011)
# front legs: shoulder, elbow back, paw forward
sk.joint("shoulderF.L", (0.034, -0.066, 0.078), 0.026)
sk.joint("elbow.L", (0.042, -0.045, 0.05), 0.016)
sk.joint("wrist.L", (0.042, -0.08, 0.016), 0.01)
sk.joint("paw.L", (0.042, -0.097, 0.006), 0.011)
sk.seg("pelvis", "mid", "Hips").seg("pelvis", "rump", "Hips")
sk.seg("mid", "chest", "Spine").seg("chest", "neck", "Neck").seg("neck", "head", "Head").seg("head", "snout", "Head")
sk.seg("head", "jaw", "Jaw")
sk.seg("rump", "tail1", "Tail1").seg("tail1", "tail2", "Tail1").seg("tail2", "tail3", "Tail2").seg("tail3", "tail4", "Tail3")
sk.seg("rump", "haunch.L", "HindUpper.L").seg("haunch.L", "knee.L", "HindUpper.L")
sk.seg("knee.L", "ankle.L", "HindLower.L").seg("ankle.L", "toe.L", "HindLower.L")
sk.seg("chest", "shoulderF.L", "FrontUpper.L").seg("shoulderF.L", "elbow.L", "FrontUpper.L")
sk.seg("elbow.L", "wrist.L", "FrontLower.L").seg("wrist.L", "paw.L", "FrontLower.L")
sk.mirror()
J = {n: p for n, (p, r) in sk.joints.items()}


def sub(keep):
    """A sub-skeleton made of the segments kept by keep(a, b, bone), to skin the trunk on its own."""
    k = K.Skeleton()
    k.joints = sk.joints
    k.segments = [s for s in sk.segments if keep(*s)]
    return k


SIDES = 5


def limb(names, swatch_fn, ring_weights, radii=None):
    """A thin smooth tube through joints (legs, tail): far cheaper and rounder than a skinned limb.
    ring_weights: one {bone: weight} per joint, applied after the build (see RINGS)."""
    pts = [J[n] for n in names]
    radii = radii or [max(sk.joints[n][1]) for n in names]
    part = S.tube(pts, radii, "hair_brown", bone=None, sides=SIDES)
    RINGS.append((part, ring_weights))
    return S.colored(part, swatch_fn)


RINGS = []


# ---------------------------------------------------------------------------------------------
# Fur colors
# ---------------------------------------------------------------------------------------------
TRUNK = ["rump", "pelvis", "mid", "chest", "neck", "head", "snout"]
RND = random.Random(5)
OFF = V((RND.random() * 30, RND.random() * 30, RND.random() * 30))


def axis_z(y):
    pts = sorted((J[n].y, J[n].z, max(sk.joints[n][1])) for n in TRUNK)
    if y <= pts[0][0]:
        return pts[0][1], pts[0][2]
    for (y0, z0, r0), (y1, z1, r1) in zip(pts, pts[1:]):
        if y <= y1:
            k = (y - y0) / (y1 - y0)
            return z0 + (z1 - z0) * k, r0 + (r1 - r0) * k
    return pts[-1][1], pts[-1][2]


def fur(c, s, bone, t):
    key = bone[:-2] if bone.endswith((".L", ".R")) else bone
    if key.startswith("Tail"):
        return "lips"
    if key in ("FrontLower", "HindLower"):
        return "lips" if c.z < 0.022 else "mud_wet"
    if key == "Jaw":
        return "hair_grey"
    az, r = axis_z(c.y)
    h = (c.z - az) / max(r, 1e-3)
    n = K.perlin(c * 22.0 + OFF)
    if key == "Head" and c.y < -0.188:
        return "lips"  # the bare nose
    if h > 0.3:
        return "stubble" if n < 0.2 else "hair_brown"
    if h < -0.5:
        return "hair_grey"
    return "coat_grey" if n > 0.05 else "stubble"


# ---------------------------------------------------------------------------------------------
# Parts
# ---------------------------------------------------------------------------------------------

def details():
    parts = []
    hd = J["head"]
    # ears (pink inside), beady eyes, amber incisors (upper on the head, lower on the jaw)
    for sx in (-1, 1):
        parts.append(K.ball(0.021, hd + V((0.03 * sx, 0.012, 0.038)), rot=(0, 25 * sx, 0), scale=(1.0, 0.45, 1.05), swatch="lips",
                            bone="Head", segments=6, rings=3, smooth=False))
        parts.append(K.ico(0.0095, hd + V((0.027 * sx, -0.028, 0.018)), swatch="eye_dark", bone="Head", subdiv=1))
        parts.append(K.box((0.006, 0.006, 0.016), J["snout"] + V((0.004 * sx, -0.008, -0.016)), rot=(-12, 0, 0), swatch="amber", bone="Head"))
        parts.append(K.box((0.005, 0.005, 0.012), J["jaw"] + V((0.004 * sx, -0.012, 0.006)), rot=(14, 0, 0), swatch="amber", bone="Jaw"))
    # the dorsal plates: a row of small angular amber plates down the spine, biggest over the shoulders
    row = [("Neck", -0.104, 0.032), ("Spine", -0.066, 0.044), ("Spine", -0.03, 0.048), ("Hips", 0.008, 0.046),
           ("Hips", 0.045, 0.04), ("Hips", 0.08, 0.034), ("Hips", 0.112, 0.026)]
    for i, (bone, y, ln) in enumerate(row):
        az, r = axis_z(y)
        base = V((0, y, az + r * 0.92))
        parts.append(K.shard(ln, ln * 0.5, base, (0, 0.6, 1.0), swatch="amber" if i % 2 == 0 else "crust",
                             bone=bone, seed=40 + i, sides=4))
    return parts


# the trunk is skinned from its own one-chain skeleton (rump to snout, so the rear doesn't pinch at the
# pelvis branch) with the jaw, haunches and shoulders as side chains; legs and tail are tubes.
skin_sk = K.Skeleton()
skin_sk.joints = sk.joints
for a, b in (("rump", "pelvis"), ("pelvis", "mid"), ("mid", "chest"), ("chest", "neck"), ("neck", "head"), ("head", "snout"),
             ("head", "jaw"), ("rump", "haunch.L"), ("rump", "haunch.R"), ("chest", "shoulderF.L"), ("chest", "shoulderF.R")):
    skin_sk.seg(a, b, "skin")
body = K.skin_body(skin_sk, subsurf=1)
K.outfit_faces(body, sk, {"*": "hair_brown"}, extra=fur)
tail = limb(["rump", "tail1", "tail2", "tail3", "tail4"], lambda p, s0: "lips",
            [{"Hips": 0.4, "Tail1": 0.6}, {"Tail1": 1}, {"Tail1": 0.5, "Tail2": 0.5}, {"Tail2": 0.5, "Tail3": 0.5}, {"Tail3": 1}],
            radii=[0.021, 0.017, 0.013, 0.009, 0.005])
legs = []
for sd in ("L", "R"):
    hu, hl, fu, fl = "HindUpper." + sd, "HindLower." + sd, "FrontUpper." + sd, "FrontLower." + sd
    legs.append(limb(["haunch." + sd, "knee." + sd, "ankle." + sd, "toe." + sd], lambda p, s0: "lips" if p.z < 0.03 else "hair_brown",
                     [{hu: 1}, {hu: 0.5, hl: 0.5}, {hl: 1}, {hl: 1}]))
    legs.append(limb(["shoulderF." + sd, "elbow." + sd, "wrist." + sd, "paw." + sd], lambda p, s0: "lips" if p.z < 0.024 else "hair_brown",
                     [{fu: 1}, {fu: 0.5, fl: 0.5}, {fl: 1}, {fl: 1}]))

c = K.Character("Rat", sk)
c.parts = [body, tail] + legs + details()
c.build(ao=0.5, grime=0.12, mud_height=0.03, seed=3)
obj = c.mesh_obj
# the trunk's belly must not follow the lower legs (they sweep right under it)
S.reweight(c, obj, 0, len(body.verts), [b for b in sk.bone_names() if "Lower" in b])
_off = 0
for _p in c.parts:
    for _q, _rw in RINGS:
        if _q is _p:
            S.set_weights(obj, _off, [w for w in _rw for _ in range(SIDES)])
    _off += len(_p.verts)
fk = S.FK(c)
LEGS = {"FL": S.Foot(c, "L", "FrontUpper", "FrontLower", None), "FR": S.Foot(c, "R", "FrontUpper", "FrontLower", None),
        "HL": S.Foot(c, "L", "HindUpper", "HindLower", None), "HR": S.Foot(c, "R", "HindUpper", "HindLower", None)}

# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------
IK_LOG = {}


def plant(pose, clip, targets):
    for f, a in targets:
        r = S.leg_ik(c, fk, pose, f.upper, f.lower, None, a)
        IK_LOG[clip] = max(IK_LOG.get(clip, 0.0), r)
    return pose


def keys(ph, pts):
    if ph <= pts[0][0]:
        return pts[0][1]
    for (p0, v0), (p1, v1) in zip(pts, pts[1:]):
        if ph <= p1:
            return v0 + (v1 - v0) * S.smooth((ph - p0) / (p1 - p0))
    return pts[-1][1]


RUN_F, RUN_DUTY, RUN_STRIDE = 8, 0.42, 0.11
PHASE = {"FL": 0.0, "HR": 0.0, "FR": 0.5, "HL": 0.5}  # trot: diagonal pairs


def tail_wave(t, amp=1.0, lift=0.0):
    return {"Tail1": c.q("Tail1", z=10 * amp * math.sin(t - 0.7), x=10 + lift),
            "Tail2": c.q("Tail2", z=16 * amp * math.sin(t - 1.5), x=-2 - 0.5 * lift),
            "Tail3": c.q("Tail3", z=22 * amp * math.sin(t - 2.3), x=-3)}


def run(ph):
    t = ph * math.tau
    tg, stance = [], []
    for k, f in LEGS.items():
        # front paws reach a little further than the hind ones
        dy, dz, _p, st = S.foot_track(ph + PHASE[k], RUN_DUTY, RUN_STRIDE, 0.03, 0, 0)
        a = f.ankle_for(dy + (0.008 if k[0] == "F" else 0.004), dz, 0.0)
        tg.append((f, a))
        if st:
            stance.append((f, a))
    want = -0.006 + 0.006 * math.cos(4 * math.pi * (ph - 0.3))
    hz = S.max_hip_drop_for(stance, want, reach=0.97)
    # spine flex: the body snakes (yaw) with each diagonal pair, a slight pitch rock twice per cycle
    pose = {"Hips": (c.q("Hips", z=7 * math.sin(t), x=2.5 * math.cos(2 * t)), (0, 0, hz)),
            "Spine": c.q("Spine", z=-10 * math.sin(t), x=-2.5 * math.cos(2 * t)),
            "Neck": c.q("Neck", z=4 * math.sin(t), x=-4),
            "Head": c.q("Head", z=3 * math.sin(t), x=2 * math.cos(2 * t))}
    pose.update(tail_wave(t, 1.0, lift=4))
    plant(pose, "Run", tg)
    return pose


def stand_all(pose, clip, offs=None):
    offs = offs or {}
    tg = []
    for k, f in LEGS.items():
        dy, dz = offs.get(k, (0.0, 0.0))
        tg.append((f, f.ankle_for(dy, dz, 0.0)))
    return plant(pose, clip, tg)


def idle(ph):
    # sniffing: the nose twitches fast, the head lifts and turns to scent, the tail sweeps slowly
    t = ph * math.tau
    lift = keys(ph, [(0, 0), (0.2, 1), (0.55, 1), (0.7, 0.2), (0.85, 0.6), (1.0, 0)])
    turn = keys(ph, [(0, 0), (0.25, 0), (0.35, 1), (0.5, 1), (0.6, -0.7), (0.78, -0.7), (0.9, 0), (1.0, 0)])
    twitch = math.sin(ph * math.tau * 9) * (0.4 + 0.6 * lift)
    pose = {"Hips": (c.q("Hips", x=-3 * lift), (0, 0.004 * lift, -0.004 + 0.0015 * math.sin(2 * t))),
            "Spine": {"rot": c.q("Spine", x=-8 * lift, z=4 * turn), "scale": 1.0 + 0.02 * (0.5 + 0.5 * math.sin(3 * t))},
            "Neck": c.q("Neck", x=-12 * lift, z=14 * turn),
            "Head": c.q("Head", x=-6 * lift + 3.5 * twitch, z=10 * turn),
            "Jaw": c.q("Jaw", x=2 + 2 * max(0.0, twitch))}
    pose.update(tail_wave(t * 0.5, 0.6, lift=-6))
    stand_all(pose, "Idle", {"FL": (0.005 * lift, 0), "FR": (-0.004, 0)})
    return pose


ATTACK_F = 12


def attack(ph):
    # gather back (0-0.35), lunge forward with the jaw wide (0.35-0.55), snap shut (0.55-0.65), recover
    gather = keys(ph, [(0, 0), (0.3, 1), (0.42, 0), (1.0, 0)])
    lunge = keys(ph, [(0, 0), (0.32, 0), (0.5, 1), (0.7, 1), (1.0, 0)])
    jaw = keys(ph, [(0, 0), (0.3, 0.2), (0.45, 1), (0.58, 1), (0.64, -0.1), (0.8, 0), (1.0, 0)])
    pose = {"Hips": (c.q("Hips", x=-8 * gather + 6 * lunge), (0, 0.03 * gather - 0.07 * lunge, -0.022 * gather + 0.008 * lunge)),
            "Spine": c.q("Spine", x=10 * gather - 10 * lunge),
            "Neck": c.q("Neck", x=14 * gather - 22 * lunge),
            "Head": c.q("Head", x=8 * gather - 14 * lunge),
            "Jaw": c.q("Jaw", x=45 * jaw)}
    pose.update(tail_wave(ph * math.tau, 0.4, lift=10 * lunge))
    # the front paws hop forward with the lunge, the hind paws push and stay
    fwd = -0.075 * lunge
    hop = 0.03 * math.sin(math.pi * min(1.0, max(0.0, (ph - 0.32) / 0.3)))
    stand_all(pose, "Attack", {"FL": (fwd, hop), "FR": (fwd - 0.006, hop)})
    return pose


CLIPS = [("Run", RUN_F, run, True), ("Attack", ATTACK_F, attack, False), ("Idle", 48, idle, True)]
ACTS = {}
for name, frames, fn, loop in CLIPS:
    ACTS[name] = c.action(name, frames, fn, loop=loop)

# ---------------------------------------------------------------------------------------------
# Checks, export, previews
# ---------------------------------------------------------------------------------------------
eff = [(f.lower, f.ankle) for f in LEGS.values()]
for name, frames, fn, loop in CLIPS:
    low = S.lowest_per_frame(obj, ACTS[name], frames)
    extra = ""
    if name == "Run":
        v, sd = S.contact_speed(fk, fn, frames, eff, tol=0.004)
        extra = "speed=%.2f slip_sd=%.2f" % (v, sd)
    K.report(clip=name, frames=frames, loop=loop, ik_reach=round(IK_LOG.get(name, 0), 3),
             lowest_min=round(min(low), 3), lowest_max=round(max(low), 3), info=extra)
K.report(fk_err=round(S.check_fk(c, fk, ACTS["Run"], run, 0.25, RUN_F), 5))
c.rig.animation_data.action = ACTS["Run"]
K.export_fbx([c.rig, obj], "Characters/Rat")
K.bpy.context.scene.frame_set(1)
pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
K.report(asset=obj.name, tris=K.tri_count(obj), bones=len(c.rig.data.bones),
         size="%.2f x %.2f x %.2f" % (max(p.x for p in pts) - min(p.x for p in pts), max(p.y for p in pts) - min(p.y for p in pts),
                                      max(p.z for p in pts)))
S.preview("rat", [obj], frame=1, action=ACTS["Idle"])
S.preview("rat_run", [obj], action=ACTS["Run"], views=((90, 8),), extra_frames=[1, 3, 5, 7])
S.preview("rat_run_top", [obj], action=ACTS["Run"], views=((20, 58),), extra_frames=[1, 3, 5, 7])
S.preview("rat_attack", [obj], action=ACTS["Attack"], views=((70, 10),), extra_frames=[1, 4, 7, 9, 12])
S.preview("rat_idle", [obj], action=ACTS["Idle"], views=((60, 15),), extra_frames=[1, 10, 20, 30])
