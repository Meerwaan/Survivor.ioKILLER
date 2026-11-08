"""Le Boucher (Souche, stage IV, Bestiaire 3.7): the first boss, Galeria Ostrov.

The butcher of the meat aisle, the first to eat the contaminated meat. Years of Seve rebuilt him into a
3.5 m slab of a man: a huge body in a stained white butcher's apron, a small sealed head sunk between
the shoulders, and a mountain of crust on the back and shoulders split by glowing amber fissures.
His cleaver is half fused into his right hand by the crust.
Read from above: the glowing back growth with its spikes (the boss) + the big steel cleaver on the right.

Clips: Walk (heavy, loop), Attack (cleaver chop), Windup (0.75 s: crouch, scrape, roar), Charge (loop:
head-down rush), Special (0.6 s: ring burst), Idle. Feet are planted by IK (no sliding).
Extra bone: Hump (child of Chest) carries the back growth, so it can flare (scale) in Windup / Special.
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
skel = K.humanoid(height=3.7, bulk=2.6, hunch=0.75, arm=1.08, leg=0.84, head=0.6, shoulders=1.45, belly=1.4,
                  hips=1.2, arms_out=24, limb=1.1)
J = {n: p for n, (p, r) in skel.joints.items()}


def rad(name, rx, ry=None):
    skel.joints[name][1] = (rx, rx if ry is None else ry)


# a deep barrel chest, a bull neck, a small head sunk into it, big fists (readability), heavy calves
rad("spine", 0.72, 0.62)
rad("chest", 0.64, 0.5)
rad("uchest", 0.6, 0.44)
rad("neck", 0.25, 0.24)
rad("head", 0.15, 0.16)
rad("skull", 0.15, 0.165)
rad("head_top", 0.1, 0.11)
for sd in ("L", "R"):
    rad("hand." + sd, 0.16, 0.1)
    rad("fingers." + sd, 0.13, 0.09)
    rad("wrist." + sd, 0.12)
    rad("toe." + sd, 0.14, 0.1)
    rad("ankle." + sd, 0.14)
    rad("calf." + sd, 0.25, 0.26)
    rad("thigh." + sd, 0.34, 0.35)
    rad("knee." + sd, 0.23)
for n, k in (("bicep.R", 1.18), ("elbow.R", 1.15), ("forearm.R", 1.2), ("wrist.R", 1.1), ("shoulder.R", 1.1)):
    r = skel.joints[n][1]
    rad(n, r[0] * k, r[1] * k)

# the Hump: an aux bone at the base of the back growth (disconnected segment, reparented to Chest)
BACK_Y = J["uchest"].y + skel.joints["uchest"][1][1]  # back surface at the shoulder blades
hump_a = V((0, BACK_Y - 0.1, J["uchest"].z + 0.05))
skel.joint("hump_a", hump_a, 0.05).joint("hump_b", hump_a + V((0, 0.1, 0.35)), 0.05)
skel.seg("hump_a", "hump_b", "Hump")
AUX = ("Hump",)

# ---------------------------------------------------------------------------------------------
# Body colors: shirt, apron, trousers, white rubber boots, grey infected skin with dark veins
# ---------------------------------------------------------------------------------------------
TRUNK = ["pelvis", "waist", "spine", "chest", "uchest"]


def trunk_axis_y(z):
    pts = sorted((J[n].z, J[n].y) for n in TRUNK)
    if z <= pts[0][0]:
        return pts[0][1]
    for (z0, y0), (z1, y1) in zip(pts, pts[1:]):
        if z <= z1:
            return y0 + (y1 - y0) * (z - z0) / (z1 - z0)
    return pts[-1][1]


RND = random.Random(91)
O1 = V((RND.random() * 30, RND.random() * 30, RND.random() * 30))
O2 = V((RND.random() * 30, RND.random() * 30, RND.random() * 30))
APRON_TOP = J["uchest"].z - 0.02
APRON_BOT = J["knee.L"].z + 0.12
VEIN_C = [(hump_a + V((0, 0.05, 0.1)), 0.75), (J["shoulder.L"], 0.35), (J["shoulder.R"], 0.35),
          ((J["elbow.L"] + J["wrist.L"]) / 2, 0.22), (J["wrist.R"], 0.3), (J["skull"], 0.2)]


def paint(c, s, bone, t):
    key = bone[:-2] if bone.endswith((".L", ".R")) else bone
    # the Seve spreading under the skin around the crust
    if K.PALETTE[K.sw(s)][2] == "skin":
        for cc, r in VEIN_C:
            d = (c - cc).length
            if d < r * 0.55:
                return "veins"
            if d < r:
                return "skin_ambre_dark"
    if key in ("Chest", "Spine", "Hips", "UpperLeg", "Shoulder") and APRON_BOT < c.z < APRON_TOP:
        ay = trunk_axis_y(c.z) if key != "UpperLeg" else 0.0
        bib = c.z > J["waist"].z + 0.1
        front = c.y < ay - (0.08 if not bib else 0.12)
        if front and (not bib or abs(c.x) < 0.42 - (c.z - J["waist"].z) * 0.12):
            n1 = K.perlin(c * 2.7 + O1)
            n2 = K.perlin(c * 3.6 + O2)
            if n2 > 0.4:
                return "ichor"
            if n1 > 0.34 or (c.z < APRON_BOT + 0.22 and n1 > 0.12):
                return "apron_stain"
            return "apron_white"
        # the apron's waist tie across the back
        if key in ("Spine", "Hips") and not front and abs(c.z - (J["waist"].z - 0.05)) < 0.07:
            return "apron_white"
    return None


OUTFIT = {"Head": "skin_ambre_dark", "Neck": "skin_ambre_dark", "Chest": "shirt_blue_faded", "Spine": "shirt_blue_faded",
          "Shoulder": "shirt_blue_faded", "UpperArm": [(0.3, "shirt_blue_faded"), (1.0, "skin_ambre")],
          "LowerArm": "skin_ambre", "Hand": "skin_ambre_dark", "Hips": "cargo_grey", "UpperLeg": "cargo_grey",
          "LowerLeg": [(0.62, "cargo_grey"), (1.0, "sneaker_white")], "Foot": "sneaker_white"}


# ---------------------------------------------------------------------------------------------
# Parts
# ---------------------------------------------------------------------------------------------

def slab(length, height, thick, nx, nz, origin, ax_len, ax_h, ax_t, color, bone, taper=0.0):
    """A flat blade: a grid of nx x nz cells on both faces (per-cell colors via color(i, k)), with rims.
    origin = middle of the spine at the handle end; the blade runs along ax_len, its edge along -ax_h."""
    verts, faces, sws = [], [], []
    for side in (-1, 1):
        for k in range(nz + 1):
            for i in range(nx + 1):
                u, w = i / nx, k / nz
                hh = height * (1 + taper * u)
                p = origin + ax_len * (u * length) - ax_h * (w * hh) + ax_t * (side * thick / 2)
                verts.append(p)
    row = nx + 1
    half = (nz + 1) * row
    for side in (0, 1):
        for k in range(nz):
            for i in range(nx):
                a = side * half + k * row + i
                q = [a, a + 1, a + row + 1, a + row]
                faces.append(q if side else q[::-1])
                sws.append(color(i, k))
    # rims: spine (k = 0), edge (k = nz), ends (i = 0, i = nx)
    for i in range(nx):
        for k, sw_ in ((0, "metal_dark"), (nz, "steel_bright")):
            a, b = k * row + i, k * row + i + 1
            f = [a, b, half + b, half + a]
            faces.append(f if k == 0 else f[::-1])
            sws.append(sw_)
    for k in range(nz):
        for i, sw_ in ((0, "metal_dark"), (nx, "steel")):
            a, b = k * row + i, (k + 1) * row + i
            f = [a, b, half + b, half + a]
            faces.append(f[::-1] if i == 0 else f)
            sws.append(sw_)
    return K.Part(verts, faces, sws, bone, False)


BLADE_PTS = []  # rest positions of the blade, for the floor-contact solver


def cleaver(seed):
    parts = []
    w, h = J["wrist.R"], J["fingers.R"]
    d = (h - w).normalized()                     # along the hand
    fwd = V((0, -1, 0))
    hax = (fwd - d * fwd.dot(d)).normalized()     # the handle axis, forward from the fist
    side = d.cross(hax).normalized()
    grip = w.lerp(h, 0.55)
    # handle (wood, mostly swallowed by the crust) through the fist
    hc = grip + hax * 0.05
    parts.append(K.cyl(0.055, 0.42, (0, 0, 0), swatch="wood_dark", bone="Hand.R", segments=6).transformed(
        K.Matrix.Translation(hc) @ hax.to_track_quat("Z", "Y").to_matrix().to_4x4()))
    # the blade: from the front of the fist, flat turned up-and-out so it reads from the top camera
    tilt = math.radians(45)  # edge down-and-out: the flat shows to the top camera AND from the side
    e = V((-math.sin(tilt), 0, -math.cos(tilt)))  # blade "height" direction (toward the edge), right hand = -X
    e = (e - hax * e.dot(hax)).normalized()
    tnorm = hax.cross(e).normalized()
    origin = grip + hax * 0.26 - e * 0.08
    L, Hh = 1.12, 0.6
    rnd = random.Random(seed)
    rust = {(rnd.randrange(1, 7), rnd.randrange(0, 3)) for _ in range(4)}

    def col(i, k):
        if i == 0 and k < 2:
            return "crust_dark"
        if i == 1 and k == 0:
            return "amber"
        if k == 3:
            return "steel_bright"
        if (i, k) in rust:
            return "rust"
        return "steel" if (i + k) % 3 else "metal_worn"

    blade = slab(L, Hh, 0.035, 7, 4, origin, hax, -e, tnorm, col, "Hand.R", taper=0.08)
    BLADE_PTS.extend(blade.verts)
    parts.append(blade)
    # the crust fusing hand, handle and blade heel together
    parts.append(S.crust_mass(grip + hax * 0.02, 0.17, scale=(1.15, 1.0, 1.1), bone="Hand.R", seed=seed + 1, subdiv=1,
                              jitter=0.2, width=0.12, freq=1.6))
    parts += K.plate_cluster(origin + hax * 0.06 + e * 0.02, tnorm, 4, 0.08, swatch=["crust", "amber", "crust_dark"],
                             bone="Hand.R", seed=seed + 2, spread=1.2)
    parts += K.plate_cluster(origin + hax * 0.06 + e * 0.02, -tnorm, 3, 0.07, swatch=["crust", "crust_dark"],
                             bone="Hand.R", seed=seed + 3, spread=1.0)
    for i in range(3):
        parts.append(K.shard(0.12 + 0.04 * i, 0.03, origin + hax * (0.08 + 0.1 * i) + e * 0.03, (-e * 1.0 + hax * 0.4),
                             swatch="amber", bone="Hand.R", seed=seed + 10 + i))
    return parts


def growths(seed):
    """The mountain on the back (Hump bone) and the shoulder crust (Chest): dark crust split by glowing fissures."""
    parts = []
    base = ("crust_black", "crust_dark")
    kw = dict(base=base, rim="amber", width=0.07, rim_share=0.7, patch=0.3)
    hc = hump_a + V((0, 0.32, 0.12))
    parts.append(S.crust_mass(hc, 0.56, scale=(1.42, 0.82, 0.92), bone="Hump", seed=seed, subdiv=4, jitter=0.05, freq=1.25, **kw))
    parts.append(S.crust_mass(hc + V((0.34, 0.1, 0.34)), 0.36, scale=(1.0, 0.95, 1.1), bone="Hump", seed=seed + 1, subdiv=3, jitter=0.07, freq=1.3, **kw))
    parts.append(S.crust_mass(hc + V((-0.3, 0.16, 0.28)), 0.33, scale=(1.1, 0.9, 1.0), bone="Hump", seed=seed + 2, subdiv=3, jitter=0.07, freq=1.3, **kw))
    parts.append(S.crust_mass(hc + V((0.02, 0.34, -0.3)), 0.34, scale=(1.35, 0.8, 0.9), bone="Hump", seed=seed + 3, subdiv=3, jitter=0.08, freq=1.3, **kw))
    # spikes (amber crystal) growing up and back out of the mass: the crown seen from above
    rnd = random.Random(seed + 9)
    spikes = [((0.05, 0.08, 0.5), (0.0, 0.45, 1.0), 0.55), ((0.36, 0.16, 0.6), (0.5, 0.45, 1.0), 0.45),
              ((-0.32, 0.22, 0.52), (-0.55, 0.55, 0.9), 0.42), ((0.16, 0.48, 0.18), (0.2, 1.0, 0.55), 0.42),
              ((-0.18, 0.46, 0.0), (-0.4, 1.0, 0.35), 0.38), ((0.62, 0.0, 0.2), (1.0, 0.25, 0.6), 0.34),
              ((-0.62, 0.05, 0.16), (-1.0, 0.3, 0.6), 0.34), ((-0.05, -0.08, 0.56), (0.1, -0.15, 1.0), 0.3)]
    for i, (off, dr, ln) in enumerate(spikes):
        parts.append(K.shard(ln * (0.9 + 0.2 * rnd.random()), 0.085, hc + V(off), dr, swatch="amber" if i % 3 else "amber_glow_soft",
                             bone="Hump", seed=seed + 20 + i, sides=4))
    # shoulder crust (rides the chest)
    for sd, sx in (("L", 1), ("R", -1)):
        sh = J["shoulder." + sd]
        parts.append(S.crust_mass(sh + V((0.08 * sx, 0.16, 0.2)), 0.36, scale=(1.2, 1.05, 0.8), bone="Chest", seed=seed + 30 + (sx > 0),
                                  subdiv=3, jitter=0.12, freq=1.3, base=base, rim="amber", width=0.08, rim_share=0.7))
        parts.append(K.shard(0.3, 0.07, sh + V((0.12 * sx, 0.28, 0.3)), (0.7 * sx, 0.55, 1.0), swatch="amber", bone="Chest", seed=seed + 40 + (sx > 0)))
    # plates along the arms, a growth on the left forearm, the right thigh
    parts += K.plate_cluster((J["elbow.L"] + J["wrist.L"]) / 2 + V((0.1, 0.04, 0)), (1, 0.3, 0.2), 5, 0.12, swatch=["crust", "amber", "crust_dark"], seed=seed + 50)
    parts += K.plate_cluster(J["bicep.R"] + V((-0.14, 0.05, 0.04)), (-1, 0.4, 0.4), 4, 0.11, swatch=["crust_dark", "crust"], seed=seed + 51)
    parts += K.plate_cluster((J["hip.R"] + J["knee.R"]) / 2 + V((-0.22, 0.1, 0)), (-0.8, 0.5, 0), 4, 0.11, swatch=["crust_dark", "crust"], seed=seed + 52)
    return parts


def head_bits(seed):
    parts = []
    sk, hd = J["skull"], J["head"]
    # eyes sealed under a band of crust, crust plates on the skull, the open mouth
    parts.append(K.ico(0.12, sk + V((0, -0.13, -0.02)), scale=(1.45, 0.55, 0.55), swatch="amber", bone="Head", subdiv=1, jitter=0.15, seed=seed))
    parts += K.plate_cluster(sk + V((0, 0.04, 0.12)), (0, 0.3, 1), 5, 0.09, swatch=["crust_dark", "crust", "crust_black"], bone="Head", seed=seed + 1, spread=1.2)
    parts.append(K.ball(0.06, hd + V((0, -0.14, -0.02)), scale=(1.4, 0.5, 1.0), swatch="mouth", bone="Head", segments=8, rings=4))
    return parts


# ---------------------------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------------------------
with S.aux_excluded(AUX):
    body = K.skin_body(skel, subsurf=2)
    K.outfit_faces(body, skel, OUTFIT, tear=0.1, tear_swatch="skin_ambre", seed=7, extra=paint,
                   tear_bones=("Chest", "Spine"))
    parts = [body] + growths(100) + cleaver(200) + head_bits(300)
    c = K.Character("Boucher", skel)
    c.parts = parts
    c.build(ao=0.6, grime=0.25, mud_height=0.55, seed=7)
S.reparent(c, "Hump", "Chest")
obj = c.mesh_obj
fk = S.FK(c)
FEET = [S.Foot(c, "L", heel_back=0.12), S.Foot(c, "R", heel_back=0.12)]
LEG_L = FEET[0].length

# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------
IK_LOG = {}


def plant(pose, clip, targets):
    """targets: [(Foot, ankle target, pitch)] -> IK into pose, logs the worst reach."""
    for f, a, pitch in targets:
        r = S.leg_ik(c, fk, pose, f.upper, f.lower, f.foot, a, foot_pitch=pitch)
        IK_LOG[clip] = max(IK_LOG.get(clip, 0.0), r)
    return pose


def gait(ph, duty, stride, lift, roll_off, roll_land, hip_base, bob, bob_phase=0.0):
    """Leg targets of a biped gait at phase ph + the hip height that keeps the stance feet reachable."""
    tg, stance = [], []
    for f, off in ((FEET[0], 0.0), (FEET[1], 0.5)):
        dy, dz, pitch, st = S.foot_track(ph + off, duty, stride, lift, roll_off, roll_land)
        a = f.ankle_for(dy, dz, pitch)
        tg.append((f, a, pitch))
        if st:
            stance.append((f, a))
    want = hip_base - bob * 0.5 * (1 + math.cos(4 * math.pi * (ph - bob_phase)))
    hz = S.max_hip_drop_for(stance, want)
    return tg, hz


WALK_F, WALK_DUTY, WALK_STRIDE = 40, 0.64, 1.72
CHARGE_F, CHARGE_DUTY, CHARGE_STRIDE = 16, 0.4, 1.95


def walk(ph):
    t = ph * math.tau
    # heavy: the body drops on each footfall, rolls onto the stance leg, the growth lags behind
    tg, hz = gait(ph, WALK_DUTY, WALK_STRIDE, 0.2, 18, 10, -0.1, 0.07, bob_phase=0.09)
    sway = math.sin(t)  # + = weight on the left leg
    pose = {"Hips": (c.q("Hips", x=4, y=-4 * sway, z=-6 * math.cos(t)), (0.05 * sway, 0, hz)),
            "Spine": c.q("Spine", x=4, y=2 * sway, z=3 * math.cos(t)),
            "Chest": c.q("Chest", x=6 + 1.5 * math.cos(2 * t), z=5 * math.cos(t)),
            "Neck": c.q("Neck", x=-4),
            "Head": c.q("Head", x=-6 + 3 * math.cos(2 * t + 0.6), y=3 * sway),
            "Hump": c.q("Hump", x=-2.5 * math.cos(2 * t - 1.2))}
    plant(pose, "Walk", tg)
    sw_ = math.cos(t)  # left leg forward at ph 0 -> right arm forward
    pose.update(K.arm(c, "L", forward=-16 * sw_ + 4, raise_=-6, elbow=18 + 6 * max(0, -sw_)))
    pose.update(K.arm(c, "R", forward=10 * sw_ + 10, raise_=-2, elbow=26 + 5 * max(0, sw_), wrist=-8))
    return pose


def charge(ph):
    # head-down rush: the whole body pitched forward behind the growth, long bounding strides
    t = ph * math.tau
    tg, hz = gait(ph, CHARGE_DUTY, CHARGE_STRIDE, 0.38, 25, 8, -0.2, 0.08, bob_phase=0.18)
    sw_ = math.cos(t)
    pose = {"Hips": (c.q("Hips", x=20, y=-3 * math.sin(t), z=-7 * sw_), (0, -0.1, hz)),
            "Spine": c.q("Spine", x=14, z=4 * sw_),
            "Chest": c.q("Chest", x=16 + 3 * math.cos(2 * t), z=6 * sw_),
            "Neck": c.q("Neck", x=16),
            "Head": c.q("Head", x=10),
            "Hump": c.q("Hump", x=-5 * math.cos(2 * t - 1.0))}
    plant(pose, "Charge", tg)
    pose.update(K.arm(c, "L", forward=-34 * sw_ - 6, raise_=-2, elbow=60 + 15 * max(0, -sw_)))
    pose.update(K.arm(c, "R", forward=14 * sw_ - 32, raise_=14, elbow=42 + 10 * max(0, sw_), wrist=10))
    return pose


def stand(clip, pose, hz, dyL=0.0, dyR=0.0, dzL=0.0, dzR=0.0, pL=0.0, pR=0.0, dxL=0.0, dxR=0.0):
    pose["Hips"] = (pose.get("Hips", K.Quaternion()), (0, 0, hz)) if not isinstance(pose.get("Hips"), tuple) else pose["Hips"]
    fL, fR = FEET
    plant(pose, clip, [(fL, fL.ankle_for(dyL, dzL, pL, dxL), pL), (fR, fR.ankle_for(dyR, dzR, pR, dxR), pR)])
    return pose


def ease(a, b, x):
    return a + (b - a) * S.smooth(x)


def keys(ph, pts):
    """Piecewise smooth interpolation through [(phase, value)]."""
    if ph <= pts[0][0]:
        return pts[0][1]
    for (p0, v0), (p1, v1) in zip(pts, pts[1:]):
        if ph <= p1:
            return ease(v0, v1, (ph - p0) / (p1 - p0))
    return pts[-1][1]


def blade_min(pose):
    mats = fk.mats(pose)
    return min(fk.point(mats, "Hand.R", q).z for q in BLADE_PTS)


def arm_on_floor(pose, torso_x, elbow, raise_, pitch=0.0, target=0.0, lo=0.0, hi=150.0):
    """Right arm angles putting the blade's lowest point at `target` height with the blade at a world
    pitch `pitch` (deg, + tip up): bisection on the arm swing, the wrist keeps the blade pitch."""
    def setp(f):
        w = torso_x - f - elbow + pitch
        pose.update(K.arm(c, "R", forward=f, raise_=raise_, elbow=elbow, wrist=w))
        return w
    lo = max(lo, torso_x - 0.5 * elbow)  # from "arm straight down" forward, the blade only rises
    for _ in range(16):
        mid = (lo + hi) / 2
        setp(mid)
        if blade_min(pose) > target:
            hi = mid
        else:
            lo = mid
    f = (lo + hi) / 2
    return f, torso_x - f - elbow + pitch


def arm_blend(pose, side, free, solved, w):
    """Blend two (forward, raise, elbow, wrist) arm settings."""
    v = [a + (b - a) * w for a, b in zip(free, solved)]
    pose.update(K.arm(c, side, forward=v[0], raise_=v[1], elbow=v[2], wrist=v[3]))


def idle(ph):
    t = ph * math.tau
    br = math.sin(t)  # one slow heavy breath per loop... and a half
    pose = {"Hips": (c.q("Hips", x=3, y=1.5 * math.sin(t)), (0.02 * math.sin(t), 0, -0.07 + 0.015 * br)),
            "Spine": c.q("Spine", x=4 - 1.5 * br),
            "Chest": {"rot": c.q("Chest", x=6 - 2.5 * br, z=2 * math.sin(t)), "scale": 1.0 + 0.025 * (br * 0.5 + 0.5)},
            "Neck": c.q("Neck", x=-4, z=8 * math.sin(t + 1.0)),
            "Head": c.q("Head", x=-6 + 3 * br, y=4 * math.sin(2 * t)),
            "Hump": {"rot": c.q("Hump", x=-1.5 * br), "scale": 1.0 + 0.02 * (br * 0.5 + 0.5)}}
    stand("Idle", pose, pose["Hips"][1][2], dyL=-0.18, dyR=0.16, dxL=0.04, dxR=-0.06)
    pose.update(K.arm(c, "L", forward=4 + 2 * br, raise_=-4, elbow=20 + 4 * br))
    pose.update(K.arm(c, "R", forward=12 + 2 * br, raise_=0, elbow=28 + 3 * br, wrist=-10 + 4 * math.sin(3 * t)))
    return pose


def attack(ph):
    # 0-0.4 raise the cleaver overhead, right shoulder back; 0.4-0.55 chop down into the floor in front
    # (wrist snaps, the whole weight follows); 0.55-0.72 the blade bites the floor; 0.72-1 recover
    up = keys(ph, [(0, 0), (0.4, 1), (0.5, 0), (1.0, 0)])
    chop = keys(ph, [(0, 0), (0.4, 0), (0.55, 1), (0.72, 1), (1.0, 0)])
    hz = -0.07 - 0.38 * chop + 0.04 * up
    hx, sx, cx = 3 + 14 * chop - 3 * up, 4 + 12 * chop - 6 * up, 6 + 18 * chop - 10 * up
    pose = {"Hips": (c.q("Hips", x=hx, z=-8 * up + 8 * chop), (0, -0.08 * chop, hz)),
            "Spine": c.q("Spine", x=sx, z=-6 * up + 5 * chop),
            "Chest": c.q("Chest", x=cx, z=-16 * up + 10 * chop),
            "Neck": c.q("Neck", x=-4 - 8 * up + 6 * chop),
            "Head": c.q("Head", x=-6 + 4 * chop),
            "Hump": c.q("Hump", x=-2 * chop + 3 * up)}
    stand("Attack", pose, hz, dyL=-0.34, dyR=0.22, dxL=0.04, dxR=-0.04)
    pose.update(K.arm(c, "L", forward=keys(ph, [(0, 4), (0.4, 36), (0.55, -22), (0.72, -18), (1.0, 4)]),
                      raise_=keys(ph, [(0, -4), (0.4, 22), (0.55, 8), (1.0, -4)]),
                      elbow=keys(ph, [(0, 20), (0.4, 55), (0.55, 30), (1.0, 20)])))
    free = (keys(ph, [(0, 12), (0.4, 150), (0.47, 120), (0.55, 70), (1.0, 12)]),
            keys(ph, [(0, 0), (0.4, 28), (0.55, 2), (1.0, 0)]),
            keys(ph, [(0, 28), (0.4, 85), (0.47, 40), (0.55, 6), (1.0, 28)]),
            keys(ph, [(0, -10), (0.4, 40), (0.47, 10), (0.55, -20), (1.0, -10)]))
    w = keys(ph, [(0, 0), (0.48, 0), (0.55, 1), (0.72, 1), (0.9, 0)])
    if w > 0:
        f, wr = arm_on_floor(pose, hx + sx + cx, 6, -6, pitch=-12, target=-0.03, lo=0, hi=140)
        arm_blend(pose, "R", free, (f, -6, 6, wr), w)
    else:
        pose.update(K.arm(c, "R", forward=free[0], raise_=free[1], elbow=free[2], wrist=free[3]))
    return pose


WINDUP_F = 22


def windup(ph):
    # crouch and drag the cleaver's edge along the floor (0-0.42), roar with the growth flaring
    # (0.42-0.78), drop into the rush stance (0.78-1)
    crouch = keys(ph, [(0, 0), (0.18, 1), (0.42, 1), (0.56, 0.3), (0.78, 0.25), (1.0, 0.75)])
    roar = keys(ph, [(0, 0), (0.4, 0), (0.55, 1), (0.78, 1), (0.95, 0)])
    rush = keys(ph, [(0, 0), (0.78, 0), (1.0, 1)])
    drag = keys(ph, [(0, 0), (0.14, 0), (0.42, 1)])
    hz = -0.07 - 0.44 * crouch
    lean = 8 + 22 * crouch - 26 * roar + 16 * rush
    shake = math.sin(ph * 70) * roar
    pose = {"Hips": (c.q("Hips", x=0.5 * lean, z=-6 * crouch * (1 - roar)), (0, 0.04 * roar - 0.06 * rush, hz)),
            "Spine": {"rot": c.q("Spine", x=0.3 * lean), "scale": 1.0 + 0.015 * roar},
            "Chest": {"rot": c.q("Chest", x=0.45 * lean, y=1.5 * shake, z=-6 * crouch * (1 - roar)), "scale": 1.0 + 0.03 * roar},
            "Neck": c.q("Neck", x=-4 - 16 * roar + 14 * rush + 8 * crouch),
            "Head": c.q("Head", x=-6 - 24 * roar + 10 * rush, y=5 * shake),
            "Hump": {"rot": c.q("Hump", x=-6 * roar), "scale": 1.0 + 0.16 * roar + 0.03 * crouch}}
    stand("Windup", pose, hz, dyL=-0.42, dyR=0.3, dxL=0.08, dxR=-0.1, pR=-6 * crouch)
    pose.update(K.arm(c, "L", forward=keys(ph, [(0, 4), (0.18, 26), (0.42, 22), (0.55, 4), (0.78, 4), (1.0, 18)]),
                      raise_=keys(ph, [(0, -4), (0.18, 2), (0.42, 4), (0.55, 50), (0.78, 50), (1.0, -2)]),
                      elbow=keys(ph, [(0, 20), (0.18, 34), (0.55, 36), (1.0, 55)])))
    # the cleaver: edge on the floor in front-right, dragged back (the elbow folds), flung out on the roar
    free = (keys(ph, [(0, 12), (0.18, 40), (0.42, 20), (0.56, 14), (0.78, 14), (1.0, -10)]),
            keys(ph, [(0, 0), (0.18, 10), (0.42, 14), (0.56, 48), (0.78, 46), (1.0, 8)]),
            keys(ph, [(0, 28), (0.18, 12), (0.42, 40), (0.56, 30), (1.0, 48)]),
            keys(ph, [(0, -10), (0.18, -20), (0.42, -10), (0.56, 6), (1.0, -16)]))
    w = keys(ph, [(0, 0), (0.12, 0), (0.2, 1), (0.42, 1), (0.52, 0)])
    if w > 0:
        el = ease(8, 20, drag)
        f, wr = arm_on_floor(pose, 1.25 * lean, el, ease(-6, 2, drag), pitch=ease(-26, -16, drag), target=-0.02, lo=-20, hi=120)
        arm_blend(pose, "R", free, (f, ease(-6, 2, drag), el, wr), w)
    else:
        pose.update(K.arm(c, "R", forward=free[0], raise_=free[1], elbow=free[2], wrist=free[3]))
    return pose


SPECIAL_F = 18


def special(ph):
    # gather (0-0.33), burst (0.33-0.5): chest heaves, arms fling wide, the growth flares; settle (0.5-1)
    gather = keys(ph, [(0, 0), (0.3, 1), (0.42, 0), (1.0, 0)])
    burst = keys(ph, [(0, 0), (0.3, 0), (0.44, 1), (0.62, 1), (1.0, 0)])
    hz = -0.07 - 0.18 * gather - 0.04 * burst
    pose = {"Hips": (c.q("Hips", x=3 + 6 * gather - 4 * burst), (0, 0.03 * burst, hz)),
            "Spine": {"rot": c.q("Spine", x=4 + 8 * gather - 8 * burst), "scale": 1.0 - 0.02 * gather + 0.015 * burst},
            "Chest": {"rot": c.q("Chest", x=6 + 14 * gather - 18 * burst), "scale": 1.0 - 0.03 * gather + 0.035 * burst},
            "Neck": c.q("Neck", x=-4 + 12 * gather - 16 * burst),
            "Head": c.q("Head", x=-6 + 10 * gather - 18 * burst),
            "Hump": {"rot": c.q("Hump", x=5 * gather - 7 * burst), "scale": 1.0 - 0.05 * gather + 0.26 * burst}}
    stand("Special", pose, hz, dyL=-0.24, dyR=0.2, dxL=0.06, dxR=-0.08)
    for sd in ("L", "R"):
        pose.update(K.arm(c, sd, forward=keys(ph, [(0, 8), (0.3, 34), (0.44, 12), (0.62, 12), (1.0, 8)]),
                          raise_=keys(ph, [(0, -2), (0.3, -14), (0.44, 32), (0.62, 29), (1.0, -2)]),
                          elbow=keys(ph, [(0, 24), (0.3, 75), (0.44, 58), (0.62, 60), (1.0, 24)]),
                          wrist=keys(ph, [(0, -6), (0.3, 20), (0.44, -20), (1.0, -6)]) if sd == "R" else 0))
    return pose


CLIPS = [("Walk", WALK_F, walk, True), ("Charge", CHARGE_F, charge, True), ("Idle", 60, idle, True),
         ("Attack", 21, attack, False), ("Windup", WINDUP_F, windup, False), ("Special", SPECIAL_F, special, False)]
ACTS = {}
for name, frames, fn, loop in CLIPS:
    ACTS[name] = c.action(name, frames, fn, loop=loop)
c.rig.animation_data.action = ACTS["Walk"]

# ---------------------------------------------------------------------------------------------
# Checks, export, previews
# ---------------------------------------------------------------------------------------------
eff = [(f.lower, f.ankle) for f in FEET]
for name, frames, fn, loop in CLIPS:
    low = S.lowest_per_frame(obj, ACTS[name], frames)
    extra = ""
    if name in ("Walk", "Charge"):
        v, sd = S.contact_speed(fk, fn, frames, eff)
        extra = "speed=%.2f slip_sd=%.2f" % (v, sd)
    K.report(clip=name, frames=frames, loop=loop, ik_reach=round(IK_LOG.get(name, 0), 3),
             lowest_min=round(min(low), 3), lowest_max=round(max(low), 3), info=extra)
    if name in ("Attack", "Windup", "Special"):
        K.report(clip=name, lowest=" ".join("%.2f" % z for z in low))
        K.report(clip=name, blade=" ".join("%.2f" % blade_min(fn(f / frames)) for f in range(frames + 1)))
K.report(fk_err=round(S.check_fk(c, fk, ACTS["Walk"], walk, 0.3, WALK_F), 5))

c.rig.animation_data.action = ACTS["Walk"]
K.export_fbx([c.rig, obj], "Characters/Boucher")
bpy = K.bpy
bpy.context.scene.frame_set(1)
pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
K.report(asset=obj.name, tris=K.tri_count(obj), bones=len(c.rig.data.bones),
         size="%.2f x %.2f x %.2f" % (max(p.x for p in pts) - min(p.x for p in pts), max(p.y for p in pts) - min(p.y for p in pts),
                                      max(p.z for p in pts)))
S.preview("boucher", [obj], frame=1, action=ACTS["Idle"])
S.preview("boucher_front", [obj], frame=1, action=ACTS["Idle"], views=((0, 15), (180, 15), (0, 89)))
S.preview("boucher_walk", [obj], action=ACTS["Walk"], views=((90, 10),), extra_frames=[1, 11, 21, 31])
S.preview("boucher_charge", [obj], action=ACTS["Charge"], views=((90, 10),), extra_frames=[1, 5, 9, 13])
S.preview("boucher_attack", [obj], action=ACTS["Attack"], views=((70, 12),), extra_frames=[1, 9, 11, 13, 17])
S.preview("boucher_windup", [obj], action=ACTS["Windup"], views=((70, 12),), extra_frames=[1, 6, 10, 14, 18, 23])
S.preview("boucher_special", [obj], action=ACTS["Special"], views=((20, 30),), extra_frames=[1, 6, 9, 12, 19])
