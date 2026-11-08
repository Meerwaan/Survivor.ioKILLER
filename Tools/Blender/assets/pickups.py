"""Pickups (Univers 6.1-6.2): Props/Pickups.fbx, static meshes, one shared palette.

Eclat_1/2/3   XP éclats of amber resin (orange glow, never blue gems): shard, cluster, big crystal cluster.
Ration        healing: a dented food tin + an energy bar. Green (vert sûr) = soin.
Or            gold: a small stack of coins + a ring. Gold = loot only.
Collecteur    REGEN magnetic collector: white polymer horseshoe, cyan coils.
CaisseLargage REGEN supply drop crate, straps, cyan seam, parachute bundle on top.
Cache         Charognard loot cache: dented ammo can, blue tape, reflective strip, spray tag.
All float and spin around Unity Y in game -> origin at the bounds center. Built resting on Z = 0, then centered.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _props_lib as L  # noqa: E402
from _props_lib import K, V, xf  # noqa: E402

K.reset()

GLOW = ("amber_glow", "crystal", "amber_glow_soft", "amber_glow", "crystal", "amber_glow_soft")


def resin_base(r, loc, seed, scale=(1, 1, 0.55), subdiv=1):
    """The lump of hardened sap the shards grow from: darker amber, crust underneath."""
    base = [K.ico(r, loc, scale=scale, swatch="amber", subdiv=subdiv, jitter=0.18, seed=seed)]
    L.recolor(base, lambda c, n, s: "crust_dark" if n.z < -0.2 else ("amber_glow_soft" if n.z > 0.75 else None))
    return base


def eclat_1():
    # one chunky shard of amber resin with a splinter: glowing tip, darker resin at the foot
    parts = [L.crystal(0.25, 0.09, (0, 0, 0.08), (0.12, 0.05, 1), back=0.32, sides=5, seed=31, swatch=GLOW, back_swatch="amber"),
             L.crystal(0.12, 0.045, (0.04, 0.0, 0.05), (1, -0.3, 0.9), back=0.3, sides=4, seed=32, swatch=("crystal", "amber_glow"), back_swatch="amber")]
    return L.finish("Eclat_1", parts, origin="center", ao=0.25, grime=0.0)


def eclat_2():
    # a little cluster: three shards out of a lump of hardened sap
    parts = [resin_base(0.13, (0, 0, 0.05), 41),
             L.crystal(0.33, 0.1, (0.0, 0.0, 0.07), (0.05, 0.08, 1), back=0.2, sides=5, seed=42, swatch=GLOW, back_swatch="amber"),
             L.crystal(0.22, 0.075, (0.07, -0.03, 0.06), (0.55, -0.25, 1), back=0.2, sides=5, seed=43, swatch=GLOW[1:], back_swatch="amber"),
             L.crystal(0.2, 0.07, (-0.07, 0.03, 0.06), (-0.55, 0.3, 1), back=0.2, sides=5, seed=44, swatch=GLOW[2:], back_swatch="amber")]
    return L.finish("Eclat_2", parts, origin="center", ao=0.3, grime=0.0)


def eclat_3():
    # the big one: a glowing crystal cluster, thick shards, crust at the foot
    parts = [resin_base(0.22, (0, 0, 0.07), 51, scale=(1.1, 1, 0.45), subdiv=2),
             L.crystal(0.5, 0.14, (0.0, 0.0, 0.09), (0.04, 0.05, 1), back=0.15, sides=6, seed=52, swatch=GLOW, back_swatch="amber"),
             L.crystal(0.34, 0.1, (0.12, -0.05, 0.08), (0.55, -0.25, 1), back=0.15, sides=5, seed=53, swatch=GLOW[1:], back_swatch="amber"),
             L.crystal(0.31, 0.095, (-0.13, -0.03, 0.08), (-0.6, -0.15, 1), back=0.15, sides=5, seed=54, swatch=GLOW[2:], back_swatch="amber"),
             L.crystal(0.27, 0.09, (0.02, 0.13, 0.08), (0.1, 0.6, 1), back=0.15, sides=5, seed=55, swatch=GLOW[3:], back_swatch="amber"),
             L.crystal(0.18, 0.065, (0.1, 0.11, 0.07), (0.6, 0.45, 0.8), back=0.15, sides=4, seed=56, swatch=("crystal", "amber_glow"), back_swatch="amber"),
             L.crystal(0.16, 0.06, (-0.1, -0.13, 0.06), (-0.35, -0.7, 0.8), back=0.15, sides=4, seed=57, swatch=("amber_glow", "crystal"), back_swatch="amber")]
    parts += K.plate_cluster((0, 0, 0.03), (0, 0, 1), 4, 0.09, swatch=["crust_dark", "crust"], seed=58, spread=2.2)
    return L.finish("Eclat_3", parts, origin="center", ao=0.3, grime=0.0)


# ---------------------------------------------------------------------------------------------
def ration():
    # the tin: rolled seams, ribs, a paper label with the green band of the dispensary (soin), pull-ring lid
    P = [(0.122, 0.0), (0.13, 0.012), (0.13, 0.026), (0.125, 0.034), (0.125, 0.07), (0.126, 0.115), (0.126, 0.165), (0.125, 0.21),
         (0.125, 0.236), (0.131, 0.25), (0.122, 0.264), (0.116, 0.256), (0.0, 0.256)]

    def tin(b, j):
        if b in (4, 6):
            return "paper"
        if b == 5:
            return "green_safe"
        return "food_tin"
    can = L.lathe(P, segments=9, loc=(-0.13, 0.0, 0.0), swatch=tin, cap_top=False, hard=(4, 7), cap_swatch="food_tin")
    L.dent(can, (-0.13 + 0.12, -0.06, 0.15), 0.08, 0.025)  # a dent on the side
    pull = L.torus(0.035, 0.008, seg=6, sides=3, loc=(-0.13 + 0.035, 0.03, 0.262), rot=(0, 0, 30), swatch="steel_bright")
    # the energy bar, lying against the tin: brown wrapper, green band, crimped foil ends
    bar_m = xf((0.13, -0.02, 0.05), (0, -18, 62))
    bar = [K.box((0.27, 0.1, 0.045), (0, 0, 0), swatch="cardboard"),
           K.box((0.07, 0.104, 0.049), (0.02, 0, 0), swatch="green_safe"),
           K.box((0.03, 0.09, 0.012), (0.15, 0, 0), swatch="steel_bright"),
           K.box((0.03, 0.09, 0.012), (-0.15, 0, 0), swatch="steel_bright")]
    L.move(bar, bar_m)
    # a lift so the bar's lowest corner rests on the ground
    lo = min(v.z for p in L.flatten(bar) for v in p.verts)
    L.move(bar, xf((0, 0, -lo)))
    parts = [can, pull, bar]
    L.move(parts, xf(scale=(1.1, 1.1, 1.1)))
    return L.finish("Ration", parts, origin="center", ao=0.35, grime=0.08, grime_height=0.15)


# ---------------------------------------------------------------------------------------------
def gold():
    rnd = random.Random(61)
    parts = []
    th = 0.034
    for i in range(4):
        dx, dy = rnd.uniform(-0.012, 0.012), rnd.uniform(-0.012, 0.012)
        parts.append(L.lathe([(0.1, 0.0), (0.1, th)], segments=10, loc=(-0.04 + dx, 0.01 + dy, i * th), phase=rnd.random(),
                             swatch="gold_dark", cap_swatch=("gold_dark", "gold")))
    # one coin standing against the stack, one fallen in front
    parts.append(L.lathe([(0.1, -th / 2), (0.1, th / 2)], segments=10, loc=(0.1, -0.005, 0.105), rot=(0, 72, 15),
                         swatch="gold_dark", cap_swatch=("gold", "gold")))
    parts.append(L.lathe([(0.095, 0.0), (0.095, th)], segments=10, loc=(0.06, -0.15, 0.0), rot=(6, 0, 0), swatch="gold_dark", cap_swatch=("gold_dark", "gold")))
    # a ring: thick band (the only gold that is not a coin), lying on its side
    parts.append(L.torus(0.055, 0.017, seg=10, sides=4, loc=(-0.15, -0.12, 0.022), rot=(10, 0, 0), swatch="gold"))
    return L.finish("Or", parts, origin="center", ao=0.35, grime=0.0)


# ---------------------------------------------------------------------------------------------
def collector():
    # white REGEN polymer horseshoe (U open toward -Y), grey pole pieces, cyan coils, status light
    R, arm = 0.2, 0.15
    path = [(-R, -arm, 0)] + [(R * math.cos(math.pi - math.pi * i / 8), R * math.sin(math.pi - math.pi * i / 8), 0) for i in range(9)] + [(R, -arm, 0)]
    sec = [(0.062, 0.0), (0.036, 0.058), (-0.036, 0.058), (-0.062, 0.0), (-0.036, -0.058), (0.036, -0.058)]
    body = L.sweep(path, sec, swatch="regen_white", smooth=False, side=None, up=(0, 0, 1), cap_swatch="regen_plate")
    parts = [L.move(body, xf((0, 0, 0.065)))]
    for sx in (-1, 1):
        x = sx * R
        parts += [K.box((0.15, 0.04, 0.13), (x, -0.035, 0.065), swatch="cyan"),
                  K.box((0.15, 0.04, 0.13), (x, -0.095, 0.065), swatch="cyan"),
                  K.box((0.135, 0.02, 0.122), (x, -0.065, 0.065), swatch="regen_grey"),
                  K.box((0.13, 0.03, 0.115), (x, -0.165, 0.065), swatch="regen_plate")]   # pole piece
    parts += [K.box((0.12, 0.07, 0.03), (0, R, 0.135), swatch="regen_grey", bevel=0.008),
              K.box((0.05, 0.03, 0.012), (0, R - 0.005, 0.153), swatch="cyan")]
    return L.finish("Collecteur", parts, origin="center", ao=0.35, grime=0.0)


# ---------------------------------------------------------------------------------------------
def drop_crate():
    W, D, H = 1.04, 0.7, 0.54   # body (on skids)
    z0 = 0.06
    parts = [K.box((W, D, H), (0, 0, z0 + H / 2), swatch="regen_white"),
             # skids
             K.box((1.1, 0.09, z0), (0, 0.24, z0 / 2), swatch="regen_plate"),
             K.box((1.1, 0.09, z0), (0, -0.24, z0 / 2), swatch="regen_plate"),
             # the lid seam: a cyan line all around
             K.box((W + 0.016, D + 0.016, 0.028), (0, 0, z0 + H * 0.78), swatch="cyan")]
    # grey corner guards
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(K.box((0.08, 0.08, H + 0.01), (sx * (W / 2 - 0.03), sy * (D / 2 - 0.03), z0 + H / 2), swatch="regen_grey"))
    # two black straps over the crate (Y-Z), with buckles
    for x in (-0.3, 0.3):
        parts.append(K.box((0.07, D + 0.025, H + 0.02), (x, 0, z0 + H / 2 + 0.005), swatch="strap_black"))
    # cyan chevrons (REGEN) on the long sides
    for sy in (-1, 1):
        y = sy * (D / 2 + 0.006)
        for dx in (-0.06, 0.06):
            parts.append(K.box((0.15, 0.008, 0.04), (dx, y, z0 + H * 0.42), rot=(0, 40 if dx < 0 else -40, 0), swatch="cyan"))
    # parachute bundle on top, tied down
    top = z0 + H
    bundle = [K.box((0.52, 0.38, 0.13), (0, 0, top + 0.065), swatch="canvas_olive", bevel=0.045),
              K.box((0.4, 0.3, 0.03), (0.0, 0.0, top + 0.125), rot=(0, 0, 4), swatch="canvas_tan")]   # the pack's flap
    L.jitter(bundle, 0.008, seed=71)
    ties = [K.box((0.04, 0.42, 0.11), (-0.13, 0, top + 0.09), swatch="strap_black"),
            K.box((0.04, 0.42, 0.11), (0.13, 0, top + 0.09), swatch="strap_black")]
    parts += [bundle, ties]
    return L.finish("CaisseLargage", parts, origin="center", ao=0.4, grime=0.12, grime_height=0.3)


# ---------------------------------------------------------------------------------------------
def cache():
    # a dented military ammo can, repainted by nobody: olive, blue work tape, a reflective strip, a chalk spray tag
    W, D, H = 0.76, 0.5, 0.42
    body = [K.box((W, D, H), (0, 0, H / 2), swatch="military_green", bevel=0.02)]
    lid = [K.box((W + 0.04, D + 0.04, 0.1), (0, 0, H + 0.04), swatch="military_green", bevel=0.02)]
    L.jitter(body, 0.006, seed=81)
    L.dent(body, (0.2, -D / 2, 0.25), 0.2, 0.03, direction=(0, 1, 0.2))
    L.move(lid, xf((0, 0, 0), (2.5, -1.5, 0)))  # the lid sits a bit crooked
    L.rust(body + lid, base="military_green", patches=("rust", "metal_worn"), amount=0.22, scale=5.0, seed=82)
    handle = [K.box((0.3, 0.045, 0.025), (0, 0, H + 0.115), swatch="metal_dark"),
              K.box((0.03, 0.04, 0.03), (-0.14, 0, H + 0.098), swatch="metal_dark"),
              K.box((0.03, 0.04, 0.03), (0.14, 0, H + 0.098), swatch="metal_dark")]
    latch = [K.box((0.05, 0.16, 0.16), (W / 2 + 0.02, 0, H - 0.03), swatch="metal_dark", bevel=0.01)]
    tape = [K.box((0.09, D + 0.06, 0.012), (-0.24, 0, H + 0.095), rot=(0, 0, 12), swatch="workwear_blue"),
            K.box((0.09, D + 0.06, 0.012), (0.27, 0, H + 0.095), rot=(0, 0, -8), swatch="workwear_blue"),
            K.box((0.09, 0.012, 0.3), (-0.235, -(D / 2 + 0.024), H - 0.08), rot=(0, 12, 0), swatch="workwear_blue")]
    strip = [K.box((W - 0.06, 0.012, 0.045), (0, -(D / 2 + 0.005), 0.12), swatch="reflective"),
             K.box((W - 0.06, 0.012, 0.045), (0, D / 2 + 0.005, 0.12), swatch="reflective")]
    # spray tag on the lid, beside the handle: a chalk-white circle struck through (the Charognard 'taken' mark)
    zt = H + 0.094
    tag = L.torus(0.075, 0.016, seg=8, sides=3, loc=(0.03, -0.145, zt), swatch="chalk", smooth=False)
    L.move(tag, xf((0, 0, zt * 0.7), (0, 0, 0), (1, 1, 0.3)))
    tag += [K.box((0.2, 0.03, 0.006), (0.03, -0.145, zt), rot=(0, 0, 35), swatch="chalk")]
    return L.finish("Cache", [body, lid, handle, latch, tape, strip, tag], origin="center", ao=0.4, grime=0.12, grime_height=0.3)


objs = [eclat_1(), eclat_2(), eclat_3(), ration(), gold(), collector(), drop_crate(), cache()]
for o in objs:
    L.report(o, 300)
K.export_fbx(objs, "Props/Pickups", animated=False)

by = {o.name: o for o in objs}
L.sheet("pickups_eclats", [by["Eclat_1"], by["Eclat_2"], by["Eclat_3"]], cols=3, views=((20, 58), (200, 58), (90, 10)))
L.sheet("pickups_small", [by["Ration"], by["Or"], by["Collecteur"]], cols=3, views=((20, 58), (200, 58), (90, 10)))
L.sheet("pickups_crates", [by["CaisseLargage"], by["Cache"]], cols=2, views=((20, 58), (200, 58), (90, 10)))
L.game_view("pickups_game", objs, spacing=1.25, cols=4)
