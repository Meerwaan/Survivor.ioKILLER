"""Le Pilleur (Charognard, Opportunite, Bestiaire 3.3). Thief in the code.

A looter who never fights: he grabs the player's dropped eclats and gold and runs, zig-zagging, leaking
coins when hit. Human, not infected. Layered civilian clothes under a blue Combinat work jacket with
silver reflective tape (shoulders, arms, waist: readable from above), hoodie underneath, bandana over the
face, beanie, gloves, and a BIG bulging canvas sack on his back with gold glinting at its mouth (the loot).
Read from above: a pale lumpy sack with gold at the top, blue shoulders with silver stripes.

Clips: Run (loop, two strides: a crouched sneaky run, he glances back over his left shoulder once, the
sack bounces on its own bone), Idle (nervous look-around), Special (0.3 s stumble/flinch when hit).
Extra bone: Sack (child of Chest). Two variants (jacket shade, bandana, beanie, trousers).
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
skel = K.humanoid(height=1.74, bulk=1.0, hunch=0.12, arm=1.0, limb=0.98, arms_out=28)
J = {n: p for n, (p, r) in skel.joints.items()}
BACK_Y = J["chest"].y + skel.joints["chest"][1][1]

# the Sack: aux bone, pivot high on the back (where the straps hold it), hanging down (disconnected
# segment, reparented to Chest after the build)
SACK_TOP = V((0, BACK_Y + 0.07, J["uchest"].z - 0.02))
skel.joint("sack_a", SACK_TOP, 0.03).joint("sack_b", SACK_TOP + V((0, 0.06, -0.3)), 0.03)
skel.seg("sack_a", "sack_b", "Sack")
AUX = ("Sack",)
SPANS = {b: skel.bone_span(b) for b in skel.bone_names()}


def bone_t(bone, p):
    h, t = SPANS[bone]
    d = t - h
    return max(0.0, min(1.0, (p - h).dot(d) / max(d.length_squared, 1e-9)))


VARIANTS = {
    "Pilleur_A": dict(jacket="workwear_blue", hoodie="hoodie_olive", bandana="bandana_red", beanie="hoodie_charcoal",
                      pants="cargo_khaki", boots="boot_brown", skin="skin_mid", seed=11),
    "Pilleur_B": dict(jacket="workwear_blue_dark", hoodie="hoodie_charcoal", bandana="canvas_olive", beanie="shirt_red_faded",
                      pants="jeans", boots="sneaker_white", skin="skin_light", seed=23),
}
BAND_Z = J["spine"].z - 0.07  # reflective band around the jacket, under the sack's bottom


def painter(v):
    jk = v["jacket"]

    def paint(c, s, bone, t):
        key = bone[:-2] if bone.endswith((".L", ".R")) else bone
        tt = bone_t(bone, c)
        if key == "UpperArm":
            return "reflective" if 0.56 < tt < 0.7 else jk
        if key == "LowerArm":
            if 0.4 < tt < 0.54:
                return "reflective"
            return jk if tt < 0.9 else v["hoodie"]  # hoodie cuff out of the jacket sleeve
        if key == "Shoulder":
            return "reflective" if 0.5 < tt < 0.78 else jk
        if key in ("Chest", "Spine", "Hips"):
            if abs(c.z - BAND_Z) < 0.034:
                return "reflective"
            if c.z < J["waist"].z - 0.0:
                return v["hoodie"] if c.z > J["waist"].z - 0.05 else v["pants"]
            return jk
        return None
    return paint


def outfit(v):
    return {"Head": v["skin"], "Neck": v["hoodie"], "Chest": v["jacket"], "Spine": v["jacket"], "Shoulder": v["jacket"],
            "UpperArm": v["jacket"], "LowerArm": v["jacket"], "Hand": "glove_black", "Hips": v["pants"],
            "UpperLeg": v["pants"], "LowerLeg": [(0.7, v["pants"]), (1.0, v["boots"])], "Foot": v["boots"]}


# ---------------------------------------------------------------------------------------------
# Parts
# ---------------------------------------------------------------------------------------------

def head_gear(v):
    parts = []
    sk, hd, top = J["skull"], J["head"], J["head_top"]
    # beanie: a cap over the skull with a folded rim
    parts.append(K.ball(0.104, sk + V((0, 0.006, 0.03)), scale=(1.0, 1.06, 0.92), swatch=v["beanie"], bone="Head", segments=10, rings=6))
    parts.append(K.cyl(0.103, 0.035, sk + V((0, 0.004, -0.005)), rot=(-8, 0, 0), swatch=v["beanie"], bone="Head", segments=10,
                       radius2=0.106))
    parts.append(K.ball(0.03, top + V((0, 0.0, 0.0)), swatch=v["beanie"], bone="Head", segments=6, rings=4))
    # bandana over nose and mouth, a triangle flap under the chin, the knot at the back
    parts.append(K.ball(0.092, hd + V((0, -0.008, 0.012)), scale=(1.06, 1.08, 0.72), swatch=v["bandana"], bone="Head", segments=10, rings=5))
    parts.append(K.shard(0.08, 0.06, hd + V((0, -0.07, 0.02)), (0, -0.35, -1.0), swatch=v["bandana"], bone="Head", seed=v["seed"] + 3, sides=3))
    parts.append(K.ball(0.025, hd + V((0, 0.095, 0.03)), swatch=v["bandana"], bone="Head", segments=6, rings=4))
    # eyes in the slit between beanie and bandana
    for sx in (-1, 1):
        parts.append(K.ball(0.014, sk + V((0.035 * sx, -0.093, -0.035)), swatch="eye_dark", bone="Head", segments=6, rings=4))
    return parts


def jacket_bits(v):
    parts = []
    # collar up around the neck, the hood bunched on the upper back under the sack straps
    parts.append(K.cyl(0.085, 0.07, J["neck"] + V((0, 0.0, -0.025)), rot=(-12, 0, 0), swatch=v["jacket"], bone="Chest", segments=10, radius2=0.1))
    parts.append(K.ball(0.1, J["uchest"] + V((0, 0.1, 0.08)), scale=(1.3, 0.6, 0.6), swatch=v["hoodie"], bone="Chest", segments=8, rings=5))
    # straps: from the sack's top over the shoulders, down the front to the armpits
    for sx in (-1, 1):
        a = SACK_TOP + V((0.1 * sx, -0.02, 0.02))
        top = J["shoulder.L"] * 0.0 + V((0.12 * sx, J["uchest"].y - 0.0, J["uchest"].z + 0.075))
        b = V((0.15 * sx, J["chest"].y - 0.15, J["chest"].z - 0.02))
        parts.append(S.strap(a, top, 0.05, 0.016, "strap_black", bone="Chest", bulge=V((0, 0, 0.02)), segs=3))
        parts.append(S.strap(top, b, 0.05, 0.016, "strap_black", bone="Chest", bulge=V((0, -0.035, 0.01)), segs=3))
    return parts


def sack(seed):
    """The loot: a big lumpy canvas sack, gathered and tied at the top, gold showing at the mouth."""
    rnd = random.Random(seed)
    parts = []
    c = SACK_TOP + V((0, 0.115, -0.3))
    body = K.ico(0.25, c, scale=(1.12, 0.86, 1.18), swatch="canvas_tan", bone="Sack", subdiv=3, jitter=0.06, seed=seed)

    def tint(p, s0):
        n = K.perlin((p - c) * 5.0 + V((3.1, 7.7, 1.3)))
        return "sandbag" if n > 0.18 else s0
    S.colored(body, tint)
    parts.append(body)
    # bulges: things inside pushing the canvas out
    for i, (off, r) in enumerate((((0.17, 0.1, -0.08), 0.13), ((-0.15, 0.12, 0.05), 0.12), ((0.04, 0.17, -0.2), 0.12), ((-0.08, 0.05, -0.27), 0.1))):
        parts.append(K.ico(r, c + V(off), scale=(1.0, 0.8, 1.0), swatch="canvas_tan" if i % 2 else "sandbag", bone="Sack",
                           subdiv=2, jitter=0.08, seed=seed + 10 + i, smooth=True))
    # a sewn patch
    parts.append(K.box((0.12, 0.012, 0.1), c + V((-0.11, 0.21, 0.06)), rot=(0, 12, 8), swatch="rag", bone="Sack"))
    # the mouth: gathered neck, rope, gold
    nk = c + V((0, 0.06, 0.29))
    up = V((0, 0.45, 1.0)).normalized()
    m = K.Matrix.Translation(nk) @ up.to_track_quat("Z", "Y").to_matrix().to_4x4()
    parts.append(K.cyl(0.1, 0.09, swatch="canvas_tan", bone="Sack", segments=8, radius2=0.06).transformed(m))
    parts.append(S.tube([nk + up * 0.01 + V((math.cos(a) * 0.075, math.sin(a) * 0.06, 0)) for a in [i * math.tau / 7 for i in range(8)]],
                        0.012, "rag", bone="Sack", sides=4))
    lip = nk + up * 0.07
    parts.append(K.cyl(0.085, 0.04, swatch="canvas_tan", bone="Sack", segments=8, radius2=0.11).transformed(
        K.Matrix.Translation(lip) @ up.to_track_quat("Z", "Y").to_matrix().to_4x4()))
    for i in range(5):
        a = rnd.random() * math.tau
        p = lip + V((math.cos(a) * 0.05, math.sin(a) * 0.04, 0.02 + rnd.random() * 0.02))
        d = V((rnd.random() - 0.5, rnd.random() - 0.5, 1.0)).normalized()
        parts.append(K.cyl(0.032, 0.01, swatch="gold" if i % 2 == 0 else "gold_dark", bone="Sack", segments=8).transformed(
            K.Matrix.Translation(p) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()))
    # a gold candlestick poking out (loot from the flats)
    parts.append(K.cyl(0.016, 0.15, lip + V((0.03, 0.03, 0.05)), rot=(-28, 18, 0), swatch="gold", bone="Sack", segments=6))
    parts.append(K.cyl(0.036, 0.03, lip + V((0.055, 0.072, 0.12)), rot=(-28, 18, 0), swatch="gold", bone="Sack", segments=8, radius2=0.022))
    # a coin escaping through a tear on the side
    parts.append(K.cyl(0.03, 0.01, c + V((0.27, 0.06, -0.1)), rot=(0, 80, 10), swatch="gold", bone="Sack", segments=8))
    return parts


# ---------------------------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------------------------
char = K.Character("Pilleur", skel)
objs = []
with S.aux_excluded(AUX):
    for i, (name, v) in enumerate(VARIANTS.items()):
        body = K.skin_body(skel, subsurf=1)
        K.outfit_faces(body, skel, outfit(v), seed=v["seed"], extra=painter(v))
        parts = [body] + head_gear(v) + jacket_bits(v) + sack(77)
        if i == 0:
            char.parts = parts
            char.build(ao=0.55, grime=0.22, mud_height=0.22, seed=v["seed"])
            char.mesh_obj.name = name
            objs.append(char.mesh_obj)
        else:
            objs.append(char.add_variant(name, parts, ao=0.55, grime=0.22, mud_height=0.22, seed=v["seed"]))
c = char
S.reparent(c, "Sack", "Chest")
fk = S.FK(c)
FEET = [S.Foot(c, "L", heel_back=0.05), S.Foot(c, "R", heel_back=0.05)]

# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------
IK_LOG = {}


def plant(pose, clip, targets):
    for f, a, pitch in targets:
        r = S.leg_ik(c, fk, pose, f.upper, f.lower, f.foot, a, foot_pitch=pitch)
        IK_LOG[clip] = max(IK_LOG.get(clip, 0.0), r)
    return pose


def gait(g, duty, stride, lift, roll_off, roll_land, hip_base, bob, bob_phase=0.0):
    tg, stance = [], []
    for f, off in ((FEET[0], 0.0), (FEET[1], 0.5)):
        dy, dz, pitch, st = S.foot_track(g + off, duty, stride, lift, roll_off, roll_land)
        a = f.ankle_for(dy, dz, pitch)
        tg.append((f, a, pitch))
        if st:
            stance.append((f, a))
    want = hip_base - bob * 0.5 * (1 + math.cos(4 * math.pi * (g - bob_phase)))
    return tg, S.max_hip_drop_for(stance, want)


def keys(ph, pts):
    if ph <= pts[0][0]:
        return pts[0][1]
    for (p0, v0), (p1, v1) in zip(pts, pts[1:]):
        if ph <= p1:
            return v0 + (v1 - v0) * S.smooth((ph - p0) / (p1 - p0))
    return pts[-1][1]


RUN_F, RUN_DUTY, RUN_STRIDE = 32, 0.36, 0.84  # two strides per loop


def sack_bounce(g, amp=1.0):
    """The sack lags the body's bob (twice per stride) and rocks with the steps."""
    return {"rot": c.q("Sack", x=7 * amp * math.sin(4 * math.pi * g - 1.3), y=4 * amp * math.sin(2 * math.pi * g + 0.4),
                       z=3 * amp * math.sin(2 * math.pi * g + 1.0)),
            "loc": (0, 0, 0.022 * amp * math.cos(4 * math.pi * g - 1.7))}


def run(ph):
    g = (2 * ph) % 1.0
    t = g * math.tau
    tg, hz = gait(g, RUN_DUTY, RUN_STRIDE, 0.2, 22, 8, -0.1, 0.045, bob_phase=0.12)
    look = keys(ph, [(0, 0), (0.46, 0), (0.58, 1), (0.8, 1), (0.94, 0)])  # glance back over the left shoulder
    sw_ = math.cos(t)
    pose = {"Hips": (c.q("Hips", x=10, y=-3 * math.sin(t), z=-8 * sw_), (0, -0.02, hz)),
            "Spine": c.q("Spine", x=8, z=3 * sw_ + 6 * look),
            "Chest": c.q("Chest", x=10 - 4 * look, z=9 * sw_ + 14 * look),
            "Neck": c.q("Neck", x=-8 + 4 * look, z=30 * look),
            "Head": c.q("Head", x=-10 + 6 * look, z=38 * look, y=-6 * look),
            "Sack": sack_bounce(g)}
    plant(pose, "Run", tg)
    pose.update(K.arm(c, "L", forward=-42 * sw_ + 8, raise_=-14, elbow=78 + 14 * max(0, -sw_)))
    pose.update(K.arm(c, "R", forward=42 * sw_ + 8, raise_=-14, elbow=78 + 14 * max(0, sw_)))
    return pose


def stand(pose, clip, hz, dyL=-0.06, dyR=0.08, dxL=0.03, dxR=-0.03, dzL=0.0, dzR=0.0, pL=0.0, pR=0.0):
    fL, fR = FEET
    plant(pose, clip, [(fL, fL.ankle_for(dyL, dzL, pL, dxL), pL), (fR, fR.ankle_for(dyR, dzR, pR, dxR), pR)])
    return pose


def idle(ph):
    # crouched, on edge: quick glances left and right, a hitch of the sack, weight shifting
    t = ph * math.tau
    look = keys(ph, [(0, 0), (0.1, 0), (0.18, 1), (0.36, 1), (0.46, -1), (0.66, -1), (0.76, 0), (1.0, 0)])
    hitch = keys(ph, [(0, 0), (0.8, 0), (0.86, 1), (0.94, 0)])
    hz = -0.07 + 0.01 * math.sin(2 * t) + 0.02 * hitch
    pose = {"Hips": (c.q("Hips", x=8, y=2 * math.sin(t)), (0.015 * math.sin(t), 0, hz)),
            "Spine": c.q("Spine", x=7, z=4 * look),
            "Chest": c.q("Chest", x=8 - 3 * hitch, z=8 * look),
            "Neck": c.q("Neck", x=-8, z=26 * look),
            "Head": c.q("Head", x=-8 + 3 * math.sin(3 * t), z=30 * look),
            "Sack": {"rot": c.q("Sack", x=-8 * hitch + 1.5 * math.sin(2 * t), y=2 * math.sin(t)), "loc": (0, 0, 0.04 * hitch)}}
    stand(pose, "Idle", hz, dyL=-0.1, dyR=0.1, dxL=0.04, dxR=-0.04)
    pose.update(K.arm(c, "L", forward=16 + 22 * hitch + 3 * math.sin(t), raise_=-14, elbow=42 + 40 * hitch))
    pose.update(K.arm(c, "R", forward=16 + 22 * hitch + 3 * math.sin(t + 1), raise_=-14, elbow=42 + 40 * hitch))
    return pose


SPECIAL_F = 9


def special(ph):
    # hit: the body snaps back and twists, arms fly up, a stumbling half-step; then back into the run lean
    hit = keys(ph, [(0, 0), (0.25, 1), (0.55, 0.6), (1.0, 0)])
    lean = keys(ph, [(0, 1), (0.25, 0), (1.0, 1)])
    hz = -0.1 - 0.06 * hit
    pose = {"Hips": (c.q("Hips", x=10 * lean - 6 * hit, z=8 * hit), (0, 0.04 * hit, hz)),
            "Spine": c.q("Spine", x=8 * lean - 10 * hit, z=6 * hit),
            "Chest": c.q("Chest", x=10 * lean - 14 * hit, z=10 * hit, y=-6 * hit),
            "Neck": c.q("Neck", x=-8 - 14 * hit),
            "Head": c.q("Head", x=-10 - 18 * hit, y=-10 * hit),
            "Sack": {"rot": c.q("Sack", x=-14 * hit, y=8 * hit), "loc": (0, 0, 0.05 * hit)}}
    step = keys(ph, [(0, 0), (0.2, 0), (0.6, 1), (1.0, 1)])
    stand(pose, "Special", hz, dyL=-0.2 + 0.32 * step, dzL=0.08 * math.sin(math.pi * step), dyR=0.18, dxL=0.05 + 0.05 * step, dxR=-0.04,
          pL=10 * math.sin(math.pi * step))
    pose.update(K.arm(c, "L", forward=20 + 40 * hit, raise_=-12 + 46 * hit, elbow=70 - 30 * hit))
    pose.update(K.arm(c, "R", forward=20 + 30 * hit, raise_=-12 + 36 * hit, elbow=70 - 20 * hit))
    return pose


CLIPS = [("Run", RUN_F, run, True), ("Idle", 48, idle, True), ("Special", SPECIAL_F, special, False)]
ACTS = {}
for name, frames, fn, loop in CLIPS:
    ACTS[name] = c.action(name, frames, fn, loop=loop)

# ---------------------------------------------------------------------------------------------
# Checks, export, previews
# ---------------------------------------------------------------------------------------------
eff = [(f.lower, f.ankle) for f in FEET]
for name, frames, fn, loop in CLIPS:
    low = S.lowest_per_frame(objs[0], ACTS[name], frames)
    extra = ""
    if name == "Run":
        v, sd = S.contact_speed(fk, fn, frames, eff)
        extra = "speed=%.2f slip_sd=%.2f" % (v, sd)
    K.report(clip=name, frames=frames, loop=loop, ik_reach=round(IK_LOG.get(name, 0), 3),
             lowest_min=round(min(low), 3), lowest_max=round(max(low), 3), info=extra)
K.report(fk_err=round(S.check_fk(c, fk, ACTS["Run"], run, 0.3, RUN_F), 5))
c.rig.animation_data.action = ACTS["Run"]
K.export_fbx([c.rig] + objs, "Characters/Pilleur")
for o in objs:
    K.report(asset=o.name, tris=K.tri_count(o))
S.preview("pilleur", [objs[0]], frame=1, action=ACTS["Idle"])
S.preview("pilleur_b", [objs[1]], frame=1, action=ACTS["Idle"], views=((20, 58), (200, 58)))
S.preview("pilleur_run", [objs[0]], action=ACTS["Run"], views=((90, 10),), extra_frames=[1, 5, 9, 13])
S.preview("pilleur_glance", [objs[0]], action=ACTS["Run"], views=((200, 58),), extra_frames=[5, 21])
S.preview("pilleur_special", [objs[0]], action=ACTS["Special"], views=((60, 15),), extra_frames=[1, 3, 5, 10])
S.preview("pilleur_idle", [objs[0]], action=ACTS["Idle"], views=((20, 40),), extra_frames=[1, 12, 26])
