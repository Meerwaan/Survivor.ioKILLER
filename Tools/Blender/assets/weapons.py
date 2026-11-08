"""Weapons and projectiles (Univers 6.6): Props/Weapons.fbx, static meshes, one shared palette.

Machete      road-maintenance brush machete, worn steel + taped handle. Prefab footprint: 1.43 m long, blade 1.1 m.
MolotovBottle green bottle, paper label, rag wick burning at the tip (the only fire colors here).
Grenade      Cordon stock F1 'limonka', olive, fuze + spoon + ring pin up (+Z = Unity +Y).
SawBlade     Combinat circular saw blade, 1.0 m, FLAT in XY, carbide teeth, rust, hub.
Bullet / Pellet   bright tracer slugs (fire_yellow), elongated along -Y.
SpitGlob     the Cracheur's glob of glowing sap.
CrustShard   the boss ring projectile: an angular crust shard.
Revolver     police service revolver (UI / Carnet), +15 %.
Forward = Blender -Y (Unity +Z) for everything that has one. Origins: bounds center (all of these fly or spin).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _props_lib as L  # noqa: E402
from _props_lib import K, V, xf  # noqa: E402

K.reset()


# ---------------------------------------------------------------------------------------------
def machete():
    # stations along the blade, heel (y = +0.45) to tip (y = -0.65): (y, x of the spine, x of the edge)
    st = [(0.45, 0.045, -0.045), (0.38, 0.046, -0.053), (0.26, 0.048, -0.063), (0.12, 0.05, -0.074), (-0.02, 0.052, -0.085),
          (-0.16, 0.054, -0.096), (-0.3, 0.056, -0.106), (-0.42, 0.057, -0.113), (-0.51, 0.055, -0.116), (-0.58, 0.04, -0.112),
          (-0.63, 0.01, -0.1)]
    nicks = {6: 0.014}  # worn edge: one chip
    tip = V((-0.085, -0.668, 0.0))
    verts, faces = [], []
    n = len(st)
    for i, (y, xs, xe) in enumerate(st):
        xe += nicks.get(i, 0.0)
        k = i / (n - 1)
        t = 0.034 * (1 - k) + 0.016 * k       # spine thickness (oversized for the camera)
        xb = min(xe + 0.03, xs - 0.012)         # the bright sharpened bevel
        tb = t * 0.5
        verts += [V((xs, y, t / 2)), V((xb, y, tb / 2)), V((xe, y, 0)), V((xb, y, -tb / 2)), V((xs, y, -t / 2))]
    sws = []
    look = ["metal_worn", "steel", "steel_bright", "steel_bright", "steel"]
    for i in range(n - 1):
        a, b = i * 5, (i + 1) * 5
        faces += [[a + 0, b + 0, b + 4, a + 4], [a + 0, a + 1, b + 1, b + 0], [a + 1, a + 2, b + 2, b + 1],
                  [a + 2, a + 3, b + 3, b + 2], [a + 3, a + 4, b + 4, b + 3]]
        sws += [look[0], look[1], look[2], look[3], look[4]]
    T = len(verts)
    verts.append(tip)
    a = (n - 1) * 5
    for q in range(5):
        faces.append([a + q, a + (q + 1) % 5, T])
        sws.append(look[q] if q else "metal_worn")
    blade = [K.Part(verts, faces, sws, None, False)]

    def mid(c):
        y = max(min(c.y, 0.45), -0.63)
        for (y0, s0, e0), (y1, s1, e1) in zip(st, st[1:]):
            if y1 <= y <= y0:
                f = (y0 - y) / (y0 - y1)
                return V(((s0 + e0) / 2 * (1 - f) + (s1 + e1) / 2 * f, c.y, 0))
        return V((-0.04, c.y, 0))
    L.orient(blade, mid)
    # rust creeping from the spine and the heel, worn grey patches on the flats
    L.rust(blade, base="steel", amount=0.42, scale=9.0, seed=4, where=lambda c, nn: c.x > -0.03 or c.y > 0.25)
    L.rust(blade, base="steel", patches=("metal_worn",), amount=0.25, scale=6.0, seed=9)
    L.rust(blade, base="metal_worn", patches=("rust_dark",), amount=0.3, scale=8.0, seed=2)

    # handle: bolster, black tape with two road-works yellow bands, pommel (axis along +Y)
    prof = [(0.05, 0.0), (0.057, 0.0), (0.057, 0.035), (0.046, 0.045), (0.044, 0.09), (0.048, 0.094), (0.048, 0.14), (0.044, 0.144),
            (0.043, 0.22), (0.048, 0.224), (0.048, 0.27), (0.044, 0.274), (0.046, 0.318), (0.055, 0.332), (0.05, 0.345)]
    band = ["metal_dark", "metal_dark", "metal_dark", "strap_black", "hazard_yellow", "hazard_yellow", "strap_black", "strap_black",
            "hazard_yellow", "hazard_yellow", "strap_black", "strap_black", "metal_dark", "metal_dark"]
    handle = L.lathe(prof, segments=8, loc=(0, 0.43, 0), rot=(-90, 0, 0), scale=(1.15, 0.8, 1), swatch=band, phase=math.pi / 8,
                     cap_swatch="metal_dark", hard=(2, 3, 12))
    # a loose end of tape and a lanyard loop of paracord at the pommel
    tape = [K.box((0.03, 0.05, 0.012), (0.045, 0.52, 0.03), rot=(0, 20, 8), swatch="hazard_yellow")]
    cord = L.torus(0.035, 0.007, seg=8, sides=4, loc=(0, 0.81, 0), rot=(0, 0, 0), swatch="bandana_red", sx=0.8, sy=1.0)
    return L.finish("Machete", [blade, handle, tape, cord], origin="center", ao=0.35, grime=0.0, seed=3)


# ---------------------------------------------------------------------------------------------
def molotov():
    P = [(0.0, 0.0), (0.07, 0.0), (0.082, 0.012), (0.086, 0.035), (0.086, 0.075), (0.086, 0.17), (0.084, 0.195), (0.065, 0.235),
         (0.038, 0.262), (0.03, 0.29), (0.03, 0.318), (0.036, 0.322), (0.036, 0.338), (0.024, 0.34)]

    def glass(b, j):
        if b == 4:  # the label, on most of the way round
            return "paper" if j < 6 else "glass_green"
        if b in (3, 5) and j == 2:
            return "green_4"  # a highlight line on the glass
        return "glass_green"
    bottle = L.lathe(P, segments=8, swatch=glass, cap_top=False, hard=(4, 5))
    rag_ring = L.lathe([(0.042, 0.268), (0.046, 0.285), (0.044, 0.305), (0.034, 0.318)], segments=7, swatch="rag", cap_top=False, cap_bottom=False)
    L.jitter(rag_ring, 0.004, seed=3)
    wick = L.sweep([(0, 0, 0.3), (0.004, 0, 0.345), (0.02, 0.008, 0.38), (0.036, 0.014, 0.4)],
                   [(0.012, 0.012), (-0.012, 0.012), (-0.012, -0.012), (0.012, -0.012)], swatch="rag", scales=[1, 1, 0.9, 0.8])
    tail = L.sweep([(0.034, 0.0, 0.29), (0.05, -0.02, 0.27), (0.058, -0.03, 0.24)], [(0.014, 0.004), (-0.014, 0.004), (-0.014, -0.004), (0.014, -0.004)],
                   swatch="rag")
    f0 = V((0.042, 0.016, 0.415))
    fire = [K.ico(0.042, f0, scale=(1, 1, 1.15), swatch="fire_orange", subdiv=1, jitter=0.15, seed=5),
            L.crystal(0.085, 0.022, f0 + V((0, 0, 0.01)), (0.35, 0.1, 1), back=0.2, sides=4, seed=2, swatch=("fire_orange",)),
            L.crystal(0.06, 0.018, f0 + V((0.01, 0.0, 0.0)), (1, 0.3, 0.5), back=0.2, sides=4, seed=3, swatch=("fire_orange",)),
            L.crystal(0.07, 0.02, f0 + V((-0.01, 0.0, 0.01)), (-0.3, 0.2, 1), back=0.2, sides=4, seed=4, swatch=("fire_yellow",)),
            K.ico(0.026, f0 + V((0.012, -0.03, 0.012)), swatch="fire_yellow", subdiv=1, seed=6)]
    parts = [bottle, rag_ring, wick, tail, fire]
    L.move(parts, xf(rot=(90, 0, 0)))  # bottle axis +Z -> neck toward -Y (Unity +Z)
    return L.finish("MolotovBottle", parts, origin="center", ao=0.35, grime=0.0, seed=4)


# ---------------------------------------------------------------------------------------------
def grenade():
    G = [(0.0, 0.0), (0.06, 0.008), (0.1, 0.045), (0.113, 0.082), (0.1, 0.094), (0.118, 0.11), (0.12, 0.15),
         (0.102, 0.166), (0.112, 0.18), (0.092, 0.225), (0.055, 0.255), (0.036, 0.262)]
    grooves = {3, 4, 6, 7}
    body = L.lathe(G, segments=10, swatch=lambda b, j: "green_1" if b in grooves else ("military_green" if j % 2 == 0 else "vest_dark"),
                   star=(2, 0.86), cap_swatch="military_green")
    fuze = L.lathe([(0.04, 0.25), (0.04, 0.285), (0.032, 0.292), (0.032, 0.312), (0.024, 0.318), (0.0, 0.32)], segments=8,
                   swatch="metal_dark", cap_bottom=False)
    lever = L.sweep([(0, 0.0, 0.322), (0, -0.035, 0.326), (0, -0.065, 0.312), (0, -0.092, 0.286), (0, -0.113, 0.24), (0, -0.124, 0.19), (0, -0.124, 0.15)],
                    [(0.018, 0.006), (-0.018, 0.006), (-0.018, -0.006), (0.018, -0.006)], swatch="metal_worn", side=(1, 0, 0), smooth=False)
    pin = [K.cyl(0.006, 0.07, (0.045, 0, 0.3), rot=(0, 90, 0), swatch="steel", segments=6)]
    ring = L.torus(0.032, 0.007, seg=10, sides=3, loc=(0.1, 0.0, 0.3), rot=(90, 0, 0), swatch="steel_bright")
    stencil = [K.box((0.05, 0.004, 0.012), (0.0, -0.118, 0.125), swatch="hazard_yellow")]  # yellow lot band, Cordon stock
    return L.finish("Grenade", [body, fuze, lever, pin, ring, stencil], origin="center", ao=0.4, grime=0.0, seed=5)


# ---------------------------------------------------------------------------------------------
def saw_blade():
    T, Rg, th = 16, 0.43, 0.015
    D = math.tau / T

    def pol(r, a, z):
        return V((r * math.cos(a), r * math.sin(a), z))
    verts, faces, sws = [], [], []

    def add(v):
        verts.append(v)
        return len(verts) - 1
    # outline: gullet, tip, back of tooth (per tooth), top and bottom
    out_t, out_b = [], []
    for k in range(T):
        a = k * D
        for r, da in ((Rg, 0.0), (0.505, 0.16 * D), (0.462, 0.8 * D)):
            out_t.append(add(pol(r, a + da, th)))
            out_b.append(add(pol(r, a + da, -th)))
    N = len(out_t)
    for i in range(N):
        i2 = (i + 1) % N
        faces.append([out_b[i], out_b[i2], out_t[i2], out_t[i]])
        sws.append("steel_bright" if i % 3 else "metal_worn")
    for k in range(T):
        g0, t, s, g1 = 3 * k, 3 * k + 1, 3 * k + 2, (3 * k + 3) % N
        faces.append([out_t[g0], out_t[t], out_t[s], out_t[g1]])
        sws.append("steel_bright")
        faces.append([out_b[g1], out_b[s], out_b[t], out_b[g0]])
        sws.append("steel_bright")
    # plate: concentric rings (top), one band below
    rings_t = [[out_t[3 * k] for k in range(T)]]
    for r in (0.34, 0.24, 0.12):
        rings_t.append([add(pol(r, k * D, th)) for k in range(T)])
    for i in range(len(rings_t) - 1):
        o, n_ = rings_t[i], rings_t[i + 1]
        for k in range(T):
            k2 = (k + 1) % T
            faces.append([o[k], o[k2], n_[k2], n_[k]])
            sws.append("steel" if i < 2 else "metal_worn")
    hub_b = [add(pol(0.12, k * D, -th)) for k in range(T)]
    gb = [out_b[3 * k] for k in range(T)]
    for k in range(T):
        k2 = (k + 1) % T
        faces.append([gb[k2], gb[k], hub_b[k], hub_b[k2]])
        sws.append("steel")
    plate = [K.Part(verts, faces, sws, None, False)]
    L.rust(plate, base="steel", amount=0.4, scale=4.5, seed=11)
    L.rust(plate, base="steel", patches=("metal_worn",), amount=0.25, scale=7.0, seed=12)
    # expansion slots (laser cut), dark lines from four gullets inward, both faces
    slots = []
    for k in (1, 5, 9, 13):
        a = k * D
        for z, flip in ((th + 0.0015, False), (-th - 0.0015, True)):
            p0, p1 = pol(0.43, a - 0.01, z), pol(0.35, a - 0.01, z)
            q0, q1 = pol(0.43, a + 0.012, z), pol(0.35, a + 0.012, z)
            c = [p0, q0, q1, p1] if not flip else [p1, q1, q0, p0]
            if (c[1] - c[0]).cross(c[2] - c[0]).z * (1 if not flip else -1) < 0:
                c = list(reversed(c))
            slots += L.quad(c, "metal_gun")
    hub = L.lathe([(0.125, -0.03), (0.13, -0.02), (0.13, 0.02), (0.125, 0.03), (0.06, 0.03), (0.05, 0.022), (0.0, 0.022)], segments=12,
                  swatch=["metal_gun", "metal_gun", "metal_gun", "metal_dark", "metal_dark", "hazard_black"], hard=(3, 4), cap_swatch="metal_gun")
    bolts = [K.box((0.022, 0.022, 0.012), (0.085 * math.cos(a), 0.085 * math.sin(a), 0.034), rot=(0, 0, math.degrees(a)), swatch="steel")
             for a in (0.4, 0.4 + math.tau / 3, 0.4 + 2 * math.tau / 3)]
    return L.finish("SawBlade", [plate, slots, hub, bolts], origin="center", ao=0.3, grime=0.0, seed=6)


# ---------------------------------------------------------------------------------------------
def slug(name, length, radius, seg=6):
    P = [(0.0, 0.0), (radius * 0.62, 0.0), (radius * 0.88, length * 0.2), (radius, length * 0.5), (radius * 0.86, length * 0.75),
         (radius * 0.5, length * 0.92), (0.0, length)]
    sw = ["fire_orange", "fire_orange", "fire_yellow", "fire_yellow", "fire_yellow", "fire_yellow"]
    parts = L.lathe(P, segments=seg, swatch=sw, rot=(90, 0, 0), cap_swatch="fire_orange")  # tip toward -Y
    return L.finish(name, parts, origin="center", ao=0.0, grime=0.0)


# ---------------------------------------------------------------------------------------------
def spit_glob():
    # a teardrop of sap: round glowing head forward (-Y), a stretched tail behind, two droplets shed
    P = [(0.0, 0.0), (0.025, 0.035), (0.06, 0.1), (0.105, 0.18), (0.145, 0.26), (0.165, 0.33), (0.155, 0.39), (0.115, 0.44), (0.055, 0.47), (0.0, 0.478)]

    def sap(b, j):
        if b <= 2:
            return "amber_glow_soft" if j % 3 else "amber"
        return "amber_glow" if (b + j) % 4 else "amber_glow_soft"
    P = [(r * 0.82, z * 0.82) for r, z in P]
    body = L.lathe(P, segments=9, swatch=sap, rot=(90, 0, 0), smooth=True)
    L.jitter(body, 0.012, seed=8)
    drops = [K.ico(0.04, (0.012, 0.075, 0.01), scale=(1, 1.4, 1), swatch="amber_glow_soft", subdiv=1, jitter=0.1, seed=10, smooth=True),
             K.ico(0.026, (-0.03, 0.15, -0.012), scale=(1, 1.3, 1), swatch="amber_glow", subdiv=1, jitter=0.1, seed=11, smooth=True)]
    return L.finish("SpitGlob", [body, drops], origin="center", ao=0.15, grime=0.0)


# ---------------------------------------------------------------------------------------------
def crust_shard():
    c = V((0, 0, 0.12))
    main = L.crystal(0.3, 0.085, c, (0, -1, 0), back=0.45, sides=5, seed=21, flat=0.75,
                     swatch=("amber", "crust", "amber_glow_soft", "crust_dark", "amber"), back_swatch="crust_dark")
    fins = [L.crystal(0.13, 0.035, c + V((0.035, 0.06, 0.01)), (0.6, 1, 0.25), back=0.3, sides=4, seed=22, swatch=("crust", "amber")),
            L.crystal(0.11, 0.032, c + V((-0.04, 0.05, -0.01)), (-0.7, 1, -0.2), back=0.3, sides=4, seed=23, swatch=("crust_dark", "amber")),
            L.crystal(0.1, 0.03, c + V((0.0, 0.04, 0.045)), (0.05, 0.8, 1), back=0.3, sides=4, seed=24, swatch=("amber_glow_soft", "crust"))]
    return L.finish("CrustShard", [main, fins], origin="center", ao=0.25, grime=0.0)


# ---------------------------------------------------------------------------------------------
def revolver():
    S = 1.15  # oversized 15 %
    cz = -0.014  # cylinder axis below the bore (bore at Z = 0)
    cyl = L.lathe([(0.0155, -0.021), (0.019, -0.017), (0.019, 0.017), (0.0155, 0.021)], segments=12, loc=(0, 0, cz), rot=(90, 0, 0),
                  swatch="metal_worn", star=(2, 0.84), cap_swatch="metal_dark")
    barrel = [K.cyl(0.0085, 0.1, (0, -0.073, 0), rot=(90, 0, 0), swatch="metal_gun", segments=8),
              K.cyl(0.0045, 0.002, (0, -0.1235, 0), rot=(90, 0, 0), swatch="hazard_black", segments=6)]
    frame = [K.box((0.013, 0.1, 0.015), (0, -0.073, -0.0125), swatch="metal_gun"),          # ejector shroud
             K.box((0.009, 0.1, 0.006), (0, -0.073, 0.009), swatch="metal_dark"),           # top rib
             K.box((0.005, 0.012, 0.012), (0, -0.117, 0.016), swatch="metal_dark"),         # front sight
             K.box((0.017, 0.05, 0.01), (0, 0.0, 0.0095), swatch="metal_gun"),              # top strap
             K.box((0.019, 0.054, 0.013), (0, 0.0, -0.0395), swatch="metal_gun"),           # under the cylinder
             K.box((0.017, 0.007, 0.052), (0, -0.0245, -0.014), swatch="metal_gun"),        # front of the window
             K.box((0.023, 0.009, 0.058), (0, 0.0245, -0.013), swatch="metal_gun"),         # recoil shield
             K.box((0.019, 0.034, 0.03), (0, 0.042, -0.012), swatch="metal_gun"),           # back of the frame
             K.box((0.006, 0.012, 0.008), (0.012, 0.018, -0.006), swatch="steel"),          # cylinder latch
             K.box((0.007, 0.012, 0.024), (0, 0.037, 0.012), rot=(-28, 0, 0), swatch="metal_dark"),   # hammer
             K.box((0.008, 0.014, 0.006), (0, 0.046, 0.024), rot=(-15, 0, 0), swatch="metal_worn")]   # hammer spur
    grip = [K.box((0.031, 0.036, 0.088), (0, 0.062, -0.068), rot=(22, 0, 0), swatch="wood", bevel=0.004),
            K.box((0.025, 0.04, 0.008), (0, 0.079, -0.112), rot=(22, 0, 0), swatch="metal_gun")]
    rect = [(0.0042, 0.0028), (-0.0042, 0.0028), (-0.0042, -0.0028), (0.0042, -0.0028)]
    guard = L.sweep([(0, -0.006, -0.044), (0, -0.004, -0.061), (0, 0.008, -0.073), (0, 0.026, -0.071), (0, 0.037, -0.058), (0, 0.042, -0.044)],
                    rect, swatch="metal_gun", side=(1, 0, 0), smooth=False)
    trigger = L.sweep([(0, 0.013, -0.045), (0, 0.009, -0.055), (0, 0.012, -0.065)], [(0.003, 0.002), (-0.003, 0.002), (-0.003, -0.002), (0.003, -0.002)],
                      swatch="metal_dark", side=(1, 0, 0), smooth=False)
    lanyard = L.torus(0.008, 0.002, seg=6, sides=3, loc=(0, 0.092, -0.12), rot=(0, 90, 0), swatch="steel")
    parts = [cyl, barrel, frame, grip, guard, trigger, lanyard]
    L.move(parts, xf(scale=(S, S, S)))
    return L.finish("Revolver", parts, origin="center", ao=0.3, grime=0.0)


objs = [machete(), molotov(), grenade(), saw_blade(), slug("Bullet", 0.55, 0.08), slug("Pellet", 0.3, 0.06, seg=6),
        spit_glob(), crust_shard(), revolver()]
budget = 500
for o in objs:
    L.report(o, budget)
K.export_fbx(objs, "Props/Weapons", animated=False)

by = {o.name: o for o in objs}
L.sheet("weapons_machete", [by["Machete"]], views=((20, 58), (200, 58), (90, 10)))
L.sheet("weapons_saw", [by["SawBlade"]], views=((20, 58), (90, 10)))
L.sheet("weapons_small", [by[n] for n in ("MolotovBottle", "Grenade", "Revolver")], cols=3, views=((20, 58), (90, 10)))
L.sheet("weapons_revolver", [by["Revolver"]], views=((90, 4), (30, 35), (270, 4)))
L.sheet("weapons_grenade", [by["Grenade"], by["MolotovBottle"]], cols=2, views=((0, 25), (90, 25)))
L.sheet("weapons_proj", [by[n] for n in ("Bullet", "Pellet", "SpitGlob", "CrustShard")], cols=4, views=((20, 58), (90, 10)))
L.game_view("weapons_game", [by[n] for n in ("Machete", "SawBlade", "MolotovBottle", "Grenade", "Bullet", "Pellet", "SpitGlob", "CrustShard")],
            spacing=1.15, cols=4)
