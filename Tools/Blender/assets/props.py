"""Breakable / explosive props (Bestiaire 3.8): Props/Props.fbx, static meshes, one shared palette.

Baril_Fuel   red rusted fuel drum, 0.85 x 1.1 m, hoops, bungs, flammable placards (explodes).
Baril_Regen  REGEN reagent drum, same size: white, cyan band, hazard label, a cyan cap.
CaisseRegen  REGEN supply crate, 0.9 m cube: white polymer, grey corner guards, cyan band and chevrons, wooden skids.
They stand on the ground: origin at the bottom center. Dried flood mud at the foot (the Crue).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _props_lib as L  # noqa: E402
from _props_lib import K, V, xf  # noqa: E402

K.reset()

# drum profile (radius, z): bottom chime, two rolling hoops, top chime, recessed lid
DRUM = [(0.395, 0.0), (0.42, 0.015), (0.42, 0.045), (0.408, 0.055), (0.408, 0.19), (0.408, 0.33),
        (0.425, 0.345), (0.425, 0.375), (0.408, 0.39), (0.408, 0.47), (0.408, 0.63), (0.408, 0.71),
        (0.425, 0.725), (0.425, 0.755), (0.408, 0.77), (0.408, 0.91), (0.408, 1.045),
        (0.42, 1.06), (0.42, 1.095), (0.405, 1.1), (0.395, 1.078), (0.27, 1.078), (0.0, 1.078)]
HOOPS = {5, 6, 7, 11, 12, 13}
CHIMES = {0, 1, 2, 16, 17, 18, 19}
LID = (20, 21)
BAND = 9  # z 0.47 -> 0.63, between the hoops
SEG = 16


def drum(name, body, hoop, chime, lid, extra=None):
    def sw(b, j):
        if extra:
            s = extra(b, j)
            if s:
                return s
        if b in LID:
            return lid
        if b in HOOPS:
            return hoop
        if b in CHIMES:
            return chime
        return body
    return L.lathe(DRUM, segments=SEG, swatch=sw, hard=(3, 16, 19, 20), cap_swatch=chime)


def placard(angle, z, size, outer, inner, r=0.409):
    """A diamond placard flat on the drum side, facing outward at `angle` (radians, 0 = +X)."""
    c = V((math.cos(angle) * r, math.sin(angle) * r, z))
    rot = (0, 45, math.degrees(angle) + 90)
    return [K.box((size, 0.012, size), c, rot=rot, swatch=outer),
            K.box((size * 0.72, 0.016, size * 0.72), c, rot=rot, swatch=inner)]


def bung(x, y, z, r, cap):
    return L.lathe([(r * 1.25, z), (r * 1.25, z + 0.012), (r, z + 0.018), (r, z + 0.032), (r * 0.6, z + 0.036), (0, z + 0.036)], segments=8,
                   loc=(x, y, 0), swatch=["metal_dark", "metal_dark", cap, cap, cap], cap_bottom=False)


def baril_fuel():
    parts = [drum("Baril_Fuel", "fuel_red", "fuel_red", "fuel_red", "fuel_red")]
    # rust: heavy at the foot, on the chimes and hoops, patches everywhere; black streaks under the bung
    L.rust(parts, base="fuel_red", amount=0.5, scale=6.0, seed=91, where=lambda c, n: c.z < 0.3 or (c.z > 1.04 and n.z < 0.9) or 0.33 < c.z < 0.39 or 0.71 < c.z < 0.78)
    L.rust(parts, base="fuel_red", amount=0.2, scale=4.0, seed=92)
    bang = math.atan2(0.1, 0.2)
    L.recolor(parts, lambda c, n, s: "rust_dark" if (s == "fuel_red" and c.z > 0.6 and abs(math.atan2(c.y, c.x) - bang) < 0.25 and n.z < 0.5) else None)
    L.dent(parts, (-0.36, -0.2, 0.62), 0.22, 0.05)
    parts += bung(0.2, 0.1, 1.078, 0.045, "metal_worn") + bung(-0.22, -0.05, 1.078, 0.03, "metal_worn")
    # flammable placards (white diamond, red core) on four sides
    for a in (math.pi / 2, -math.pi / 2, 0.0, math.pi):
        parts += placard(a, 0.55, 0.17, "sign_white", "red_danger")
    return L.finish("Baril_Fuel", parts, origin="bottom", ao=0.45, grime=0.2, grime_height=0.5, mud_height=0.1, seed=9)


def baril_regen():
    def band(b, j):
        return "cyan" if b == BAND else None  # the cyan band between the hoops
    parts = [drum("Baril_Regen", "regen_white", "regen_grey", "regen_grey", "regen_grey", extra=band)]
    L.rust(parts, base="regen_white", patches=("plastic_white", "regen_grey"), amount=0.25, scale=5.0, seed=93)
    parts += bung(0.2, 0.1, 1.078, 0.05, "cyan_paint") + bung(-0.22, -0.05, 1.078, 0.032, "metal_worn")
    # REGEN reagent hazard labels: yellow square, black core, on two sides; a grey info plate
    for a in (math.pi / 2, -math.pi / 2):
        parts += placard(a, 0.83, 0.17, "hazard_yellow", "hazard_black")
        c = V((math.cos(a + 0.9) * 0.41, math.sin(a + 0.9) * 0.41, 0.27))
        parts.append(K.box((0.16, 0.012, 0.1), c, rot=(0, 0, math.degrees(a + 0.9) + 90), swatch="regen_plate"))
    return L.finish("Baril_Regen", parts, origin="bottom", ao=0.45, grime=0.22, grime_height=0.5, mud_height=0.12, seed=10)


def caisse_regen():
    B, H, z0 = 0.86, 0.79, 0.06
    parts = [K.box((B, B, H), (0, 0, z0 + H / 2), swatch="regen_white", bevel=0.015),
             # wooden skids (pallet)
             K.box((0.9, 0.11, z0), (0, 0.32, z0 / 2), swatch="wood_plank"),
             K.box((0.9, 0.11, z0), (0, 0.0, z0 / 2), swatch="wood_plank"),
             K.box((0.9, 0.11, z0), (0, -0.32, z0 / 2), swatch="wood_plank"),
             # lid rim
             K.box((0.9, 0.9, 0.05), (0, 0, z0 + H - 0.005), swatch="regen_grey", bevel=0.012),
             # cyan band
             K.box((B + 0.016, B + 0.016, 0.075), (0, 0, z0 + H * 0.48), swatch="cyan")]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(K.box((0.075, 0.075, H - 0.02), (sx * (B / 2 - 0.025), sy * (B / 2 - 0.025), z0 + H / 2), swatch="regen_grey"))
    top = z0 + H + 0.022
    # two cyan chevrons on the lid (REGEN), pointing the same way: read from the camera
    for dy in (-0.11, 0.11):
        for sx in (-1, 1):
            parts.append(K.box((0.26, 0.06, 0.012), (sx * 0.09, dy + 0.045, top), rot=(0, 0, sx * 32), swatch="cyan"))
    # latches on the rim, an info plate with stencil lines on two sides
    for sy in (-1, 1):
        parts.append(K.box((0.12, 0.03, 0.08), (0, sy * 0.455, z0 + H - 0.04), swatch="metal_dark"))
        y = sy * (B / 2 + 0.006)
        parts += [K.box((0.3, 0.008, 0.14), (0.12, y, z0 + 0.2), swatch="regen_plate"),
                  K.box((0.22, 0.012, 0.022), (0.12, y, z0 + 0.235), swatch="g25"),
                  K.box((0.16, 0.012, 0.018), (0.09, y, z0 + 0.175), swatch="g25")]
    parts = L.rust(parts, base="regen_white", patches=("plastic_white",), amount=0.25, scale=4.0, seed=94)
    return L.finish("CaisseRegen", parts, origin="bottom", ao=0.45, grime=0.22, grime_height=0.45, mud_height=0.1, seed=11)


objs = [baril_fuel(), baril_regen(), caisse_regen()]
for o in objs:
    L.report(o, 1200)
K.export_fbx(objs, "Props/Props", animated=False)
L.sheet("props_close", objs, cols=3, views=((20, 58), (200, 58), (90, 12)))
L.game_view("props_game", objs, spacing=1.5, cols=3)
