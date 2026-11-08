"""Le Rejeton (Bestiaire 3.1): "SplitterSmall" in the code. A bud fallen off a Colonie.

No bosse, NO head: a crusted chunk of torso (~0.7 m tall) scuttling on THREE limbs, its mouth gaping in the
middle of the top, ringed with teeth and amber crust. Read from above: a TRIANGLE (two front limbs, one
behind). Custom skeleton: Body (root child) + FrontL_Up/Low, FrontR_Up/Low, Back_Up/Low.
Anims: Run (fast scuttle, planted feet), Attack (rear and bite), Idle.
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
CORE = V((0, 0, 0.45))
LEGS = [("FrontL", 55.0), ("FrontR", -55.0), ("Back", 180.0)]  # azimuth from the front (-Y) toward the left (+X)


def leg_dir(az):
    a = math.radians(az)
    return V((math.sin(a), -math.cos(a), 0))


k = K.Skeleton()
k.joint("base", (0, 0.0, 0.3), (0.15, 0.15))
k.joint("core", CORE, (0.23, 0.22))
k.joint("top", (0, 0.01, 0.63), (0.18, 0.17))
k.seg("base", "core", "Body")
for name, az in LEGS:
    d = leg_dir(az)
    k.joint("hip_" + name, CORE + d * 0.2 + V((0, 0, 0.01)), 0.11)  # the chunk's "shoulder" toward each limb
    k.joint("knee_" + name, CORE + d * 0.37 + V((0, 0, 0.13)), 0.05)
    k.joint("foot_" + name, V((d.x * 0.5, d.y * 0.5, 0.028)), 0.027)
    k.seg("core", "hip_" + name, "Body")
for name, az in LEGS:
    k.seg("hip_" + name, "knee_" + name, name + "_Up").seg("knee_" + name, "foot_" + name, name + "_Low")
k.seg("core", "top", "Body")
J = {n: p for n, (p, r) in k.joints.items()}


def bits(body, seed):
    rnd = K.random.Random(seed)
    parts = []
    # the mouth in the middle of the top, tilted toward the front, ringed with teeth and amber lips
    top, top_n = L.surface(body, (0, -0.04, 1.5), (0, 0, -1))
    mn = (top_n + V((0, -0.35, 0))).normalized()
    mc = top - top_n * 0.012
    side = V((1, 0, 0))
    fwd = side.cross(mn).normalized()
    mo = K.ico(1.0, (0, 0, 0), scale=(0.1, 0.085, 0.035), swatch="mouth", subdiv=1)
    mo.bone = "Body"
    parts.append(L.oriented(mo, mc, fwd, mn))
    for i in range(6):
        a = i / 6 * math.tau + 0.3
        q = mc + side * math.cos(a) * 0.1 + fwd * math.sin(a) * 0.085
        rim, rn = L.surface(body, q + V((0, 0, 0.5)), (0, 0, -1))
        if rim is None:
            continue
        parts.append(K.shard(0.045, 0.014, rim - rn * 0.005, (rn * 0.7 - (rim - mc).normalized()), swatch="teeth", bone="Body", seed=seed + i, sides=3))
        if i % 3 == 0:
            q2 = mc + (side * math.cos(a) * 0.1 + fwd * math.sin(a) * 0.085) * 1.55
            lip, ln = L.surface(body, q2 + V((0, 0, 0.5)), (0, 0, -1))
            if lip is not None and L.bone_at(k, lip) == "Body":
                parts += K.plate_cluster(lip, ln, 1, 0.06, swatch="amber" if i % 4 else "crust", bone="Body", seed=seed + 10 + i)
    # crust plates all over the chunk, darker low down; a few amber spikes on the back
    placed = 0
    for i in range(30):
        if placed >= 4:
            break
        az = rnd.uniform(0, math.tau)
        el = rnd.uniform(-0.1, 0.7)
        dd = V((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el)))
        p, n = L.surface(body, CORE + dd * 0.7, -dd)
        if p is not None and L.bone_at(k, p) == "Body" and (p - mc).length > 0.14:
            placed += 1
            parts += K.plate_cluster(p, n, 1, rnd.uniform(0.07, 0.1), swatch=["crust_dark", "crust", "crust_black"][i % 3], bone="Body", seed=seed + 20 + i, flat=0.4)
    for i, (x, y) in enumerate(((0.08, 0.1), (-0.08, 0.08), (0.0, 0.14))):
        p, n = L.surface(body, V((x, y, 1.5)), (0, 0, -1))
        if p is not None and L.bone_at(k, p) == "Body":
            parts.append(K.shard(0.08, 0.025, p, (n + V((0, 0.3, 0.7))).normalized(), swatch="amber", bone="Body", seed=seed + 30 + i))
    # amber claws at the tips, a crust plate on each upper limb
    for name, az in LEGS:
        d = leg_dir(az)
        ft = J["foot_" + name]
        parts.append(K.shard(0.08, 0.022, ft + V((0, 0, 0.05)), (d * 0.5 + V((0, 0, -1))).normalized(), swatch="amber", bone=name + "_Low", seed=seed + 40, sides=3))
    return parts


OUTFIT = {"Body": "skin_ambre", "FrontL_Up": "skin_ambre_dark", "FrontR_Up": "skin_ambre_dark", "Back_Up": "skin_ambre_dark",
          "FrontL_Low": "crust_dark", "FrontR_Low": "crust_dark", "Back_Low": "crust_dark"}


def veins(c, s, bone, t):
    if bone == "Body" and s == "skin_ambre" and abs(K.perlin(c * 9 + V((5, 2, 9)))) < 0.035:
        return "veins"
    return None


body = K.skin_body(k, subsurf=1)
K.outfit_faces(body, k, OUTFIT, seed=707, extra=L.chain(L.along(k, {"FrontL_Low": [(0.3, "skin_ambre_dark"), (1.0, "crust_dark")],
                                                                     "FrontR_Low": [(0.3, "skin_ambre_dark"), (1.0, "crust_dark")],
                                                                     "Back_Low": [(0.3, "skin_ambre_dark"), (1.0, "crust_dark")]}), veins))
char = K.Character("Rejeton", k)
char.parts = [body] + bits(body, 707)
char.build(ao=0.6, grime=0.3, mud_height=0.2, seed=707)
char.mesh_obj.name = "Rejeton_A"
objs = [char.mesh_obj]
c = char

# ---------------------------------------------------------------------------------------------
# Animation: three IK legs under a bobbing body
# ---------------------------------------------------------------------------------------------
RUN_FRAMES, RUN_SPEED, RUN_DUTY = 10, 3.4, 0.56
OFFS = {"FrontL": 0.0, "Back": 1 / 3, "FrontR": 2 / 3}
CENTER = {"FrontL": 0.05, "FrontR": 0.05, "Back": -0.12}  # stance centers (+ = behind the rest foot)


def legs(R, off, targets, reach=0.985):
    """Solves the three legs for a body pose; lowers the body if a planted foot would be out of reach."""
    off = V(off)
    dz = 0.0
    for name, (p, planted) in targets.items():
        if not planted:
            continue
        hp = L.xform(L.head(c, name + "_Up"), L.head(c, "Body"), R, off)
        l1 = (L.tail(c, name + "_Up") - L.head(c, name + "_Up")).length
        l2 = (L.tail(c, name + "_Low") - L.head(c, name + "_Low")).length
        Rr = (l1 + l2) * reach
        hor2 = (p.x - hp.x) ** 2 + (p.y - hp.y) ** 2
        if hor2 < Rr * Rr:
            dz = min(dz, p.z + math.sqrt(Rr * Rr - hor2) - hp.z)
    off = off + V((0, 0, dz))
    pose = {"Body": (L.to_local(c, "Body", R), tuple(off))}
    for name, az in LEGS:
        d = leg_dir(az)
        q, _, _ = L.ik2(c, name + "_Up", name + "_Low", targets[name][0], d * 0.5 + V((0, 0, 1)), parent="Body", parent_R=R, parent_off=off)
        pose.update(q)
    return pose


def foot_rest(name):
    return L.tail(c, name + "_Low") + V((0, 0, 0.004))


def run(ph):
    T = RUN_FRAMES / K.FPS
    S = RUN_SPEED * RUN_DUTY * T
    targets = {}
    lifts = {}
    for name, az in LEGS:
        u = (ph + OFFS[name]) % 1.0
        F = foot_rest(name) + V((0, CENTER[name], 0))
        if u < RUN_DUTY:
            targets[name] = (F + V((0, -S / 2 + S * u / RUN_DUTY, 0)), True)
            lifts[name] = 0.0
        else:
            w = (u - RUN_DUTY) / (1 - RUN_DUTY)
            y = S / 2 - S * L.smooth(w)
            z = 0.11 * math.sin(math.pi * w)
            targets[name] = (F + V((0, y, z)), False)
            lifts[name] = math.sin(math.pi * w)
    t = ph * math.tau
    # the body rolls away from the lifted leg, bobs at each of the three touchdowns
    roll = 7 * (lifts["FrontR"] - lifts["FrontL"])
    pitch = 6 * (lifts["Back"] - 0.5 * (lifts["FrontL"] + lifts["FrontR"])) + 10
    R = L.eul(x=pitch, y=roll, z=4 * math.sin(t))
    return legs(R, (0, 0, -0.03 + 0.02 * math.cos(3 * t)), targets)


def bite(ph):
    """Rear up on the back leg (..0.4), lunge mouth-first and bite (..0.6), back off. 12 frames, bite at ~frame 7."""
    a = L.smooth(ph / 0.4)
    b = L.smooth((ph - 0.4) / 0.2)
    r = L.smooth((ph - 0.6) / 0.4)
    rear, lunge = a * (1 - b), b * (1 - r)
    targets = {}
    for name, az in LEGS:
        F = foot_rest(name)
        if name == "Back":
            targets[name] = (F, True)
        else:
            # the front limbs lift while it rears, then slam down further forward
            targets[name] = (F + V((0, -0.1 * rear - 0.14 * lunge, 0.2 * rear * (1 - lunge))), lunge > 0.5 or rear < 0.1)
    R = L.eul(x=10 - 32 * rear + 32 * lunge)
    return legs(R, (0, 0.04 * rear - 0.16 * lunge, 0.06 * rear - 0.08 * lunge), targets)


def idle(ph):
    t = ph * math.tau
    tap = L.pulse(ph, 0.3, 0.12)
    tap2 = L.pulse(ph, 0.72, 0.1)
    targets = {}
    for name, az in LEGS:
        F = foot_rest(name)
        lift = (tap if name == "FrontL" else 0) + (tap2 if name == "Back" else 0)
        targets[name] = (F + V((0, -0.03 * lift, 0.06 * lift)), lift < 0.05)
    R = L.eul(x=10 + 3 * math.sin(2 * t), y=4 * math.sin(t), z=10 * math.sin(t + 0.5))
    return legs(R, (0, 0, -0.04 + 0.015 * math.sin(2 * t)), targets)


run_act = c.action("Run", RUN_FRAMES, run)
c.action("Attack", 12, bite, loop=False)
c.action("Idle", 30, idle)
c.rig.animation_data.action = run_act

K.export_fbx([c.rig] + objs, "Characters/Rejeton")
for o in objs:
    K.report(asset=o.name, tris=K.tri_count(o))
feet = [n + "_Low" for n, _ in LEGS]
for name in ("Run", "Attack", "Idle"):
    L.check_anim(c, objs[0], bpy.data.actions[name], feet=())
# planted speed, measured on the limb tips
sc = bpy.context.scene
c.rig.animation_data.action = run_act
tips = {n: [] for n in feet}
for fr in range(1, RUN_FRAMES + 2):
    sc.frame_set(fr)
    for n in feet:
        tips[n].append(c.rig.pose.bones[n].tail.copy())
sp = []
for n, ps in tips.items():
    for i in range(len(ps) - 1):
        if ps[i].z < 0.045 and ps[i + 1].z < 0.045:
            sp.append((ps[i + 1].y - ps[i].y) * K.FPS)
K.report(run_planted_speed="%.2f..%.2f" % (min(sp), max(sp)))

L.preview("rejeton", objs, frame=1, action=run_act)
L.preview("rejeton_run", objs, action=run_act, views=((90, 10),), extra_frames=[1, 3, 5, 7, 9], size=260)
if not QUICK:
    L.preview("rejeton_attack", objs, action=bpy.data.actions["Attack"], views=((60, 15),), extra_frames=[1, 4, 6, 8, 12], size=260)
    L.preview("rejeton_game", objs, frame=1, action=run_act, views=((200, 60), (160, 60), (20, 60), (0, 89)), size=260)
    L.preview("rejeton_idle", objs, action=bpy.data.actions["Idle"], views=((40, 25),), extra_frames=[1, 10, 20, 23], size=260)
