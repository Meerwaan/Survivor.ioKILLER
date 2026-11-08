"""L'Ambre (stage II, Bestiaire 3.1): the base of every Crue. Zombie in the code.

Read from above: a hunched oval with a round amber DOME on the upper back (Bestiaire 2).
Pale grey skin, eyes sealed by a crust band, mouth open, crust claws, torn civilian clothes,
dried flood mud up the shins (la Crue). Three clothing variants share one skeleton.
Anims: Walk (lurching shamble), Attack (claw swipe), Idle.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import cordon_kit as K

K.reset()
skel = K.humanoid(height=1.72, bulk=1.0, hunch=0.55, arm=1.03, limb=0.95)
J = {n: p for n, (p, r) in skel.joints.items()}


def crust_bits(seed):
    """The dome, the plates, the sealed eyes, the claws: shared by every variant (slightly reseeded)."""
    parts = []
    chest = J["chest"]
    # the bosse: a dome high on the back (+Y is the back), crusted, with a few amber spikes
    dome_c = chest + K.Vector((0.0, 0.11, 0.05))
    parts.append(K.ico(0.17, dome_c, scale=(1.15, 0.85, 0.95), swatch="crust_dark", bone="Chest", subdiv=2, jitter=0.12, seed=seed))
    parts += K.plate_cluster(dome_c + K.Vector((0, 0.1, 0.06)), (0, 0.7, 0.7), 7, 0.08, swatch=["amber", "crust"], bone="Chest", seed=seed + 1, spread=1.4)
    for i, (dx, dz, ln) in enumerate(((-0.07, 0.08, 0.09), (0.06, 0.1, 0.11), (0.0, 0.0, 0.08), (0.1, -0.02, 0.07))):
        parts.append(K.shard(ln, 0.035, dome_c + K.Vector((dx, 0.1, dz)), (dx * 3, 1.0, 0.6 + dz * 2), swatch="amber", bone="Chest", seed=seed + 10 + i))
    # sealed eyes: a band of amber crust across the face; open dark mouth below it
    skull, jaw = J["skull"], J["head"]
    parts.append(K.ico(0.062, skull + K.Vector((0, -0.088, -0.005)), scale=(1.6, 0.55, 0.6), swatch="amber", bone="Head", subdiv=1, jitter=0.15, seed=seed + 2))
    parts.append(K.ball(0.032, jaw + K.Vector((0, -0.07, 0.005)), scale=(1.25, 0.5, 1.15), swatch="mouth", bone="Head", segments=8, rings=4))
    # crust claws: fingers fused into three amber hooks per hand
    for side in ("L", "R"):
        w, h = J["wrist." + side], J["fingers." + side]
        d = (h - w).normalized()
        for i, off in enumerate((-0.025, 0.0, 0.025)):
            side_v = K.Vector((0, 1, 0)).cross(d).normalized() * off
            parts.append(K.shard(0.09, 0.016, h + side_v - d * 0.01, d + K.Vector((0, -0.4, 0)), swatch="amber", bone="Hand." + side, seed=seed + 20 + i))
    # crust plates growing through the skin (asymmetric): left shoulder, right forearm, right thigh
    parts += K.plate_cluster(J["shoulder.L"] + K.Vector((0.03, 0.02, 0.05)), (0.5, 0.2, 1), 5, 0.06, swatch=["crust", "amber", "crust_dark"], seed=seed + 3)
    mid = (J["elbow.R"] + J["wrist.R"]) / 2
    parts += K.plate_cluster(mid + K.Vector((-0.03, 0, 0.01)), (-1, 0, 0.3), 4, 0.04, swatch=["crust", "amber"], seed=seed + 4)
    parts += K.plate_cluster((J["hip.R"] + J["knee.R"]) / 2 + K.Vector((-0.05, -0.03, 0)), (-0.6, -0.8, 0), 3, 0.045, swatch="crust_dark", seed=seed + 5)
    return parts


def veins_near(centers, vein="skin_ambre_dark"):
    """Darker skin + black veins around the crust: the Seve spreading under the skin."""
    def paint(c, s, bone, t):
        if K.PALETTE[K.sw(s)][2] != "skin":
            return None
        for cc, r in centers:
            d = (c - cc).length
            if d < r * 0.6:
                return "veins"
            if d < r:
                return vein
        return None
    return paint


VEINS = veins_near([(J["chest"] + K.Vector((0, 0.1, 0.05)), 0.28), (J["shoulder.L"], 0.13), ((J["elbow.R"] + J["wrist.R"]) / 2, 0.09), (J["skull"] + K.Vector((0, -0.08, 0)), 0.1)])

VARIANTS = {
    # a man in a grey work jacket over a faded blue shirt, jeans
    "Ambre_A": dict(outfit={"Head": "skin_ambre", "Neck": "skin_ambre", "Chest": "coat_grey", "Spine": "shirt_blue_faded",
                            "Shoulder": "coat_grey", "UpperArm": "coat_grey", "LowerArm": [(0.55, "coat_grey"), (1.0, "skin_ambre")],
                            "Hand": "skin_ambre_dark", "Hips": "jeans", "UpperLeg": "jeans", "LowerLeg": "jeans", "Foot": "shoe_dark"},
                    tear=0.12, seed=11),
    # a woman in a wine sweater and a long brown coat
    "Ambre_B": dict(outfit={"Head": "skin_ambre_pale", "Neck": "skin_ambre_pale", "Chest": "sweater_wine", "Spine": "coat_brown",
                            "Shoulder": "coat_brown", "UpperArm": "coat_brown", "LowerArm": [(0.7, "coat_brown"), (1.0, "skin_ambre_pale")],
                            "Hand": "skin_ambre", "Hips": "coat_brown", "UpperLeg": [(0.6, "coat_brown"), (1.0, "track_black")],
                            "LowerLeg": "track_black", "Foot": "sneaker_white"},
                    tear=0.1, seed=23),
    # a retiree in a mustard cardigan, beige shirt, grey trousers, slippers
    "Ambre_C": dict(outfit={"Head": "skin_ambre_dark", "Neck": "skin_ambre_dark", "Chest": "sweater_mustard", "Spine": "shirt_beige",
                            "Shoulder": "sweater_mustard", "UpperArm": "sweater_mustard", "LowerArm": [(0.4, "shirt_beige"), (1.0, "skin_ambre_dark")],
                            "Hand": "skin_ambre_dark", "Hips": "cargo_grey", "UpperLeg": "cargo_grey", "LowerLeg": "cargo_grey", "Foot": "leather_brown"},
                    tear=0.14, seed=37),
}

char = K.Character("Ambre", skel)
objs = []
first = True
for name, v in VARIANTS.items():
    body = K.skin_body(skel, subsurf=1)
    K.outfit_faces(body, skel, v["outfit"], tear=v["tear"], tear_swatch=v["outfit"]["Head"], seed=v["seed"], extra=VEINS)
    parts = [body] + crust_bits(v["seed"])
    if first:
        char.parts = parts
        char.build(ao=0.6, grime=0.25, mud_height=0.42, seed=v["seed"])
        char.mesh_obj.name = name
        objs.append(char.mesh_obj)
        first = False
    else:
        objs.append(char.add_variant(name, parts, ao=0.6, grime=0.25, mud_height=0.42, seed=v["seed"]))

c = char


def shamble(ph):
    # a lurching, forward-leaning walk: arms half raised toward the prey, right leg dragging, head bobbing
    p = K.walk_pose(c, ph, stride=30, knee=40, arm_swing=10, lean=14, bob=0.035, sway=7, twist=8,
                    elbow=28, arm_raise=-16, arm_forward=36, limp=0.35, head_nod=6)
    t = ph * math.tau
    p["Neck"] = c.q("Neck", x=-6, y=6 * math.sin(t))  # head cocked, sniffing (they don't see, they smell)
    return p


def claw(ph):
    # wind up the right arm, slash down and across, recover (0.45 s)
    base = K.walk_pose(c, 0.0, stride=0, knee=0, arm_swing=0, lean=14, bob=0, sway=0, twist=0, elbow=28, arm_raise=-16, arm_forward=36)
    if ph < 0.35:
        k = ph / 0.35
        up = K.arm(c, "R", forward=36 + 90 * k, raise_=-16 + 40 * k, elbow=28 + 40 * k)
        tw = c.q("Chest", x=7, z=-25 * k)
    elif ph < 0.55:
        k = (ph - 0.35) / 0.2
        up = K.arm(c, "R", forward=128 - 120 * k, raise_=38 - 50 * k, elbow=68 - 50 * k)
        tw = c.q("Chest", x=7 + 18 * k, z=-25 + 50 * k)
    else:
        k = (ph - 0.55) / 0.45
        k = k * k * (3 - 2 * k)
        up = K.arm(c, "R", forward=8 + 30 * k, raise_=-12 + 20 * k, elbow=18 + 10 * k)
        tw = c.q("Chest", x=25 - 18 * k, z=25 - 25 * k)
    base.update(up)
    base["Chest"] = tw
    return base


def idle(ph):
    t = ph * math.tau
    p = K.walk_pose(c, 0.0, stride=0, knee=6, arm_swing=0, lean=16, bob=0, sway=0, twist=0, elbow=30, arm_raise=6, arm_forward=25)
    p["Hips"] = (c.q("Hips", y=3 * math.sin(t)), (0, 0, -0.015 + 0.01 * math.sin(2 * t)))
    p["Neck"] = c.q("Neck", x=-8, y=10 * math.sin(t), z=15 * math.sin(t * 0.5 + 1))
    return p


walk = c.action("Walk", 24, shamble)
c.action("Attack", 14, claw, loop=False)
c.action("Idle", 48, idle)
c.rig.animation_data.action = walk

K.export_fbx([c.rig] + objs, "Characters/Ambre")
for o in objs:
    K.report(asset=o.name, tris=K.tri_count(o))
K.preview("ambre", [objs[0]], frame=1, action=walk)
K.preview("ambre_b", [objs[1]], frame=1, action=walk, views=((20, 58), (200, 58)))
K.preview("ambre_c", [objs[2]], frame=1, action=walk, views=((20, 58), (200, 58)))
K.preview("ambre_walk", [objs[0]], action=walk, views=((90, 10),), extra_frames=[1, 4, 7, 10])
