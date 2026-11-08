"""Le Fievreux (stage I, Bestiaire 3.1): "Runner" in the code. Hours to 3 days after contact.

Still human: thin, flushed and blotched with fever, everyday clothes torn, eyes still open (one starting
to crust). Read from above: a LEANING ARROW, a fine amber crest along the spine from the nape to the
lower back, bursting through the torn back of the clothes. Two variants share one skeleton.
Anims: Run (wild sprint, planted feet), Attack (lunging bite), Idle (twitchy, panting).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _ambres_lib as L  # noqa: E402
from _ambres_lib import K, V  # noqa: E402

QUICK = "--quick" in sys.argv

K.reset()
skel = K.humanoid(height=1.76, bulk=0.86, hunch=0.12, arm=1.03, limb=0.88, head=1.06, shoulders=0.95, hips=0.95)
J = {n: p for n, (p, r) in skel.joints.items()}


def trunk_y(z):
    """y of the trunk center line at height z (rest)."""
    pts = [J[n] for n in ("pelvis", "waist", "spine", "chest", "uchest", "neck")]
    for a, b in zip(pts, pts[1:]):
        if a.z <= z <= b.z:
            k = (z - a.z) / max(1e-6, b.z - a.z)
            return a.y + (b.y - a.y) * k
    return pts[0].y if z < pts[0].z else pts[-1].y


Z_TOP, Z_BOT = J["neck"].z + 0.02, J["waist"].z - 0.02


def on_spine(c, w=0.05):
    return abs(c.x) < w and c.y > trunk_y(c.z) and Z_BOT - 0.03 < c.z < Z_TOP + 0.03


def spine_tear(skin):
    """The crest has split the clothes along the spine: skin, black veins at the crest's root."""
    def paint(c, s, bone, t):
        if not on_spine(c, 0.055):
            return None
        if abs(c.x) < 0.026:
            return "veins"
        if L.kind(s) == "cloth":
            return skin
        return None
    return paint


def hair(sw):
    sk = J["skull"]

    def paint(c, s, bone, t):
        if bone != "Head":
            return None
        rel = c - sk
        if rel.z > 0.035 or (rel.z > -0.05 and rel.y > 0.02) or (rel.z > -0.01 and rel.y > -0.04 and abs(rel.x) > 0.075):
            return sw
        return None
    return paint


def fever(skin, blotch, seed):
    """Flushed skin: darker fever blotches on the face, neck and arms."""
    return L.noise_patches(blotch, scale=12.0, thresh=0.3, only=("skin",), seed=seed)


def crest(body, seed):
    """The bosse of stage I: a row of amber spikes standing on a ridge of crust along the spine."""
    parts = []
    n = 11
    pts = []
    for i in range(n):
        z = Z_TOP + (Z_BOT - Z_TOP) * i / (n - 1)
        p, nor = L.back_point(body, 0.0, z)
        if p is not None:
            pts.append((p, nor))
    segs = K._seg_info(skel)
    trunk = ("Hips", "Spine", "Chest", "Neck")
    for i, (p, nor) in enumerate(pts):
        k = i / (len(pts) - 1)
        bone = L.bone_at(skel, p, trunk)
        # spikes: tallest on the upper back, leaning back toward the tail (a fin, "fleche penchee")
        ln = 0.075 + 0.07 * math.sin(math.pi * min(1.0, k * 1.25)) ** 0.7
        d = (nor + V((0, 0.0, 0.9 - 0.5 * k))).normalized() + V((0, 0.25, 0))
        parts.append(K.shard(ln, 0.03, p - nor * 0.012, d, swatch="amber" if i % 3 else "crust", bone=bone, seed=seed + i, sides=4))
        # a smaller side spike every other point, alternating sides (asymmetric, organic)
        if i % 2 == 1:
            sx = 1 if (i // 2) % 2 else -1
            q, qn = L.surface(body, p + V((0.04 * sx, 0.3, 0)), (0, -1, 0))
            if q is not None:
                parts.append(K.shard(ln * 0.55, 0.02, q - qn * 0.01, (qn + V((0.4 * sx, 0, 0.6))).normalized(), swatch="crust",
                                     bone=bone, seed=seed + 40 + i))
        # the ridge under the spikes: elongated plates of dark crust following the spine
        if i < len(pts) - 1:
            q = pts[i + 1][0]
            mid = (p + q) / 2
            pl = K.ico(1.0, (0, 0, 0), scale=(0.034, (q - p).length * 0.7 + 0.012, 0.018), swatch="crust_dark", subdiv=1, jitter=0.12, seed=seed + 20 + i)
            pl.bone = L.bone_at(skel, mid, trunk)
            parts.append(L.oriented(pl, mid, q - p, nor))
    return parts


def face(body, seed, crusted_eye="R"):
    """Eyes still open (one starting to crust over), mouth open, panting."""
    parts = []
    for side, sx in (("L", 1), ("R", -1)):
        p, n = L.face_point(body, J, 0.037 * sx, -0.012)
        if side == crusted_eye:
            parts.append(K.ico(0.03, p + V((0.004 * sx, 0.004, 0.004)), scale=(1.25, 0.55, 0.9), swatch="amber", bone="Head", subdiv=1, jitter=0.2, seed=seed))
        else:
            parts.append(K.ico(0.019, p + V((0, 0.006, 0)), scale=(1.15, 0.6, 0.85), swatch="eye_white", bone="Head", subdiv=1))
            parts.append(K.ico(0.009, p + V((0, -0.004, 0)), scale=(1, 0.5, 1), swatch="eye_dark", bone="Head", subdiv=1))
    m, mn = L.face_point(body, J, 0.0, -0.035, ref="head")
    parts.append(K.ball(0.032, m + V((0, 0.012, 0)), scale=(1.1, 0.5, 1.25), swatch="mouth", bone="Head", segments=8, rings=4))
    parts.append(K.box((0.04, 0.012, 0.01), m + V((0, -0.002, 0.026)), swatch="teeth", bone="Head"))
    return parts


SK_A, SK_B = "skin_light", "skin_mid"
VARIANTS = {
    # a young man in a teal tracksuit, black track pants, white sneakers
    "Fievreux_A": dict(skin=SK_A, seed=101, tear=0.11, hair="hair_brown",
                       outfit={"Head": SK_A, "Neck": SK_A, "Chest": "tracksuit_teal", "Spine": "tracksuit_teal", "Shoulder": "tracksuit_teal",
                               "UpperArm": "tracksuit_teal", "LowerArm": "tracksuit_teal", "Hand": SK_A,
                               "Hips": "track_black", "UpperLeg": "track_black", "LowerLeg": "track_black", "Foot": "sneaker_white"}),
    # a man in a faded red t-shirt and jeans, bare forearms
    "Fievreux_B": dict(skin=SK_B, seed=211, tear=0.07, hair="hair_dark",
                       outfit={"Head": SK_B, "Neck": SK_B, "Chest": "shirt_red_faded", "Spine": "shirt_red_faded", "Shoulder": "shirt_red_faded",
                               "UpperArm": "shirt_red_faded", "LowerArm": SK_B, "Hand": SK_B,
                               "Hips": "jeans", "UpperLeg": "jeans", "LowerLeg": "jeans", "Foot": "shoe_dark"}),
}


def stripes(sw_stripe):
    """Tracksuit: a light stripe down the outside of the sleeves and the trouser legs."""
    def paint(c, s, bone, t):
        if L.kind(s) != "cloth":
            return None
        b = bone[:-2] if bone.endswith((".L", ".R")) else bone
        if b in ("UpperArm", "LowerArm"):
            sh = J["shoulder.L"] if bone.endswith(".L") else J["shoulder.R"]
            if abs(c.x) > abs(sh.x) and c.z > sh.z - 0.5 and abs(c.y - J["elbow.L"].y) < 0.022 and abs(c.x) > abs(J["bicep.L"].x) - 0.0:
                return sw_stripe
        if b in ("UpperLeg", "LowerLeg"):
            hx = abs(J["knee.L"].x)
            if abs(c.x) > hx + 0.055 and abs(c.y) < 0.03:
                return sw_stripe
        return None
    return paint


char = K.Character("Fievreux", skel)
objs = []
for vi, (name, v) in enumerate(VARIANTS.items()):
    body = K.skin_body(skel, subsurf=1)
    extra = [spine_tear(v["skin"]), hair(v["hair"]), fever(v["skin"], "lips", v["seed"])]
    if name == "Fievreux_A":
        extra.insert(0, stripes("shirt_white_dirty"))
        extra.insert(0, L.along(skel, {"LowerArm": [(0.82, "tracksuit_teal"), (1.0, SK_A)]}))
    else:
        extra.insert(0, L.along(skel, {"UpperArm": [(0.42, "shirt_red_faded"), (1.0, SK_B)]}))
    K.outfit_faces(body, skel, v["outfit"], tear=v["tear"], tear_swatch=v["skin"], seed=v["seed"], extra=L.chain(*extra),
                  tear_bones=("Chest", "Spine", "UpperLeg", "LowerLeg"))
    parts = [body] + crest(body, v["seed"]) + face(body, v["seed"], crusted_eye="R" if vi == 0 else "L")
    if vi == 0:
        char.parts = parts
        char.build(ao=0.55, grime=0.22, mud_height=0.0, seed=v["seed"])
        char.mesh_obj.name = name
        objs.append(char.mesh_obj)
    else:
        objs.append(char.add_variant(name, parts, ao=0.55, grime=0.22, mud_height=0.0, seed=v["seed"]))

c = char

# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------
RUN_FRAMES, RUN_SPEED, RUN_DUTY = 16, 4.4, 0.36


def run(ph):
    t = ph * math.tau
    uL, uR = ph % 1.0, (ph + 0.5) % 1.0
    aL, aR = L.gait_angle(uL, RUN_DUTY), L.gait_angle(uR, RUN_DUTY)
    # compression: lowest at mid-stance, up in the flight phases
    comp = max(math.sin(math.pi * min(1.0, u / RUN_DUTY)) if u < RUN_DUTY else 0.0 for u in (uL, uR))
    hips_R = L.eul(x=17, y=4 * math.cos(aL), z=7 * math.cos(aL))
    pose, info = L.biped_legs(c, ph, RUN_SPEED, RUN_FRAMES, duty=RUN_DUTY, lift=0.26, center=0.06, toe_off=28, heel=0,
                              hips_R=hips_R, hips_off=(0, 0, -0.03 - 0.05 * comp + 0.02 * (1 - comp)), swing_fwd=0.05)
    sL, sR = math.cos(aL), math.cos(aR)  # +1 = that leg forward
    pose.update(K.merge(
        L.arm2(c, "L", forward=-18 + 50 * sR, raise_=-22, elbow=95 - 8 * sR, wrist=15, shoulder=8 * sR),
        L.arm2(c, "R", forward=-18 + 50 * sL, raise_=-22, elbow=95 - 8 * sL, wrist=15, shoulder=8 * sL),
    ))
    pose["Spine"] = c.q("Spine", x=11 + 2 * comp, z=-6 * sL)
    pose["Chest"] = c.q("Chest", x=9, z=-9 * sL)
    pose["Neck"] = c.q("Neck", x=6)
    pose["Head"] = c.q("Head", x=-36 + 5 * math.cos(2 * t), z=3 * sL)
    return pose


def bite(ph):
    """Crouch, lunge forward with the jaws (both claws reaching), recover. 14 frames, the bite lands at ~0.5."""
    a = L.env(ph, 0.0, 0.3)              # crouch / wind-up
    b = L.env(ph, 0.3, 0.5, 0.62, 1.0)   # lunge, then recover
    plant = {"L": L.planted(c, "L", dy=-0.2), "R": L.planted(c, "R", dy=0.28, heel_lift=10 + 25 * b)}
    hips_R = L.eul(x=8 + 6 * a + 16 * b)
    pose, _ = L.biped_legs(c, 0.0, 0.0, 14, plant=plant, hips_R=hips_R,
                           hips_off=(0, 0.06 * a * (1 - b) - 0.34 * b, -0.06 - 0.06 * a + 0.02 * b))
    pose.update(K.merge(
        L.arm2(c, "L", forward=15 + 35 * a + 50 * b, raise_=-12 + 22 * a, elbow=75 - 50 * b, wrist=-25 * b),
        L.arm2(c, "R", forward=15 + 30 * a + 55 * b, raise_=-12 + 26 * a, elbow=80 - 50 * b, wrist=-25 * b),
    ))
    pose["Spine"] = c.q("Spine", x=10 - 6 * a + 12 * b)
    pose["Chest"] = c.q("Chest", x=8 - 6 * a + 10 * b)
    pose["Neck"] = c.q("Neck", x=6 - 14 * a + 22 * b)
    pose["Head"] = c.q("Head", x=-24 - 12 * a - 6 * b)
    return pose


def idle(ph):
    """Can't stand still: panting (4 breaths a loop), head snaps, fingers and shoulders twitching."""
    t = ph * math.tau
    breath = math.sin(4 * t)
    shift = math.sin(t)
    plant = {"L": L.planted(c, "L", dy=-0.08, dx=0.02), "R": L.planted(c, "R", dy=0.1, dx=0.02)}
    hips_R = L.eul(x=8, y=3 * shift, z=4 * math.sin(t + 0.6))
    pose, _ = L.biped_legs(c, 0.0, 0.0, 40, plant=plant, hips_R=hips_R, hips_off=(0.025 * shift, 0, -0.07 + 0.006 * breath))
    snap1 = L.pulse(ph, 0.22, 0.08)   # head snaps left
    snap2 = L.pulse(ph, 0.58, 0.06)   # head jerks up and right
    tw = L.pulse(ph, 0.4, 0.05)       # right shoulder twitch
    tw2 = L.pulse(ph, 0.83, 0.05)     # left hand spasm
    pose.update(K.merge(
        L.arm2(c, "L", forward=12 + 4 * breath + 25 * tw2, raise_=-20, elbow=45 + 30 * tw2, wrist=10 + 35 * tw2),
        L.arm2(c, "R", forward=8 + 4 * breath + 15 * tw, raise_=-20 + 10 * tw, elbow=40 + 12 * tw, wrist=10, shrug=14 * tw),
    ))
    pose["Spine"] = c.q("Spine", x=8 + 2 * breath, z=-3 * shift)
    pose["Chest"] = c.q("Chest", x=6 + 2.5 * breath)
    pose["Neck"] = c.q("Neck", x=8 - 10 * snap2, z=22 * snap1, y=-8 * snap2)
    pose["Head"] = c.q("Head", x=-22 - 3 * breath - 8 * snap2, z=14 * snap1, y=10 * snap2)
    return pose


run_act = c.action("Run", RUN_FRAMES, run)
c.action("Attack", 14, bite, loop=False)
c.action("Idle", 40, idle)
c.rig.animation_data.action = run_act

K.export_fbx([c.rig] + objs, "Characters/Fievreux")
for o in objs:
    K.report(asset=o.name, tris=K.tri_count(o))
import bpy  # noqa: E402
for name in ("Run", "Attack", "Idle"):
    L.check_anim(c, objs[0], bpy.data.actions[name])
K.report(run_speed=RUN_SPEED, run_frames=RUN_FRAMES, run_step=round(RUN_SPEED * RUN_FRAMES / K.FPS / 2, 3))

L.preview("fievreux", [objs[0]], frame=1, action=run_act)
if not QUICK:
    L.preview("fievreux_b", [objs[1]], frame=5, action=run_act, views=((20, 58), (200, 58)))
L.preview("fievreux_run", [objs[0]], action=run_act, views=((90, 8),), extra_frames=[1, 3, 5, 7, 9], size=300)
if not QUICK:
    L.preview("fievreux_attack", [objs[0]], action=bpy.data.actions["Attack"], views=((90, 8),), extra_frames=[1, 5, 8, 11, 15], size=300)
    L.preview("fievreux_idle", [objs[0]], action=bpy.data.actions["Idle"], views=((60, 15),), extra_frames=[1, 10, 24, 32], size=300)
    L.preview("fievreux_game", [objs[0]], frame=3, action=run_act, views=((200, 60), (160, 60), (20, 60)), size=240)
