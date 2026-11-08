"""Decor kit of Ostrov (chapters 1-2 streets, car parks, the Cordon's checkpoints): Props/Decor_Ostrov.fbx.

Every object sits at the origin, origin = bottom center (tall props: the base of the pole / trunk), front = -Y.
Univers 9.6: everything is wet, Eastern post-industrial street furniture, traces of the Crue (la Ligne at 40 cm on
what was there before the flood: cars, walls, kiosks, lamps, trees...) and of the Cordon (barriers, sandbags,
tracts, a burnt wreck). What the Cordon set up AFTER the Crue (jersey barriers, sandbags, the pole barrier) is
clean of the Ligne. Amber only for the Seve (the Resurgence), cyan only on the REGEN container, lamps cold white.

Run: blender --background --factory-startup --python Tools/Blender/assets/decor_ostrov.py [-- only=Car_A,Kiosk nosheet=1]
Writes the FBX, Tools/Blender/assets/decor_manifest.json and contact sheets in Tools/Blender/previews/decor_*.png.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _decor_lib as D  # noqa: E402

K = D.K
V = D.V
box, cyl = K.box, K.cyl
MANIFEST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "decor_manifest.json")


def rect_y(x0, z0, x1, z1, y, sw, facing=-1):
    pts = [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
    return D.poly(pts if facing < 0 else pts[::-1], sw)


def rect_x(y0, z0, y1, z1, x, sw, facing=1):
    pts = [(x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1)]
    return D.poly(pts if facing > 0 else pts[::-1], sw)


def rect_z(x0, y0, x1, y1, z, sw):
    return D.poly([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], sw)


def ring(r_in, r_out, z0, z1, sw, seg=12, side=None, loc=(0, 0)):
    """Annulus with a top and an outer side (manhole frames, tire rims)."""
    verts, faces = [], []
    for i in range(seg):
        a = math.tau * i / seg
        c, s = math.cos(a), math.sin(a)
        verts += [(loc[0] + c * r_in, loc[1] + s * r_in, z1), (loc[0] + c * r_out, loc[1] + s * r_out, z1),
                  (loc[0] + c * r_out, loc[1] + s * r_out, z0)]
    sws = []
    for i in range(seg):
        j = (i + 1) % seg
        a, b = i * 3, j * 3
        faces.append([a, a + 1, b + 1, b])
        sws.append(sw)
        faces.append([a + 1, a + 2, b + 2, b + 1])
        sws.append(side or sw)
    return K.Part(verts, faces, sws)


# =============================================================================================
# Cars: boxy compact Eastern 80s saloons (think the cars of the Comecon), faded paint
# =============================================================================================

BODY = [(-2.04, 0.28), (2.03, 0.28), (2.06, 0.62), (2.02, 0.86), (1.30, 0.90), (-0.80, 0.885), (-1.98, 0.80), (-2.07, 0.62)]
HOOD_SLOPE = math.degrees(math.atan2(0.085, 1.18))


def hood_z(y):
    return 0.80 + (y + 1.98) / 1.18 * 0.085


def car_parts(paint, seed, broken=(), door_open=False, rack=False, rust=(), tracts=0, flats=(), burnt=False):
    """Returns (body parts, wheel parts). Wheels: fl, fr, rl, rr (left = +X, the car faces -Y)."""
    rnd = random.Random(seed)
    hw = 0.80
    B = []
    body = D.extrude_yz(BODY, -hw, hw, paint, paint)
    B.append(body)
    # greenhouse: 8 verts, tumblehome
    bw, tw = 0.76, 0.64
    yb0, yb1, yt0, yt1 = -0.80, 1.30, -0.22, 0.88
    zb, zt = 0.885, (1.24 if burnt else 1.36)
    cv = [(-bw, yb0, zb), (bw, yb0, zb), (bw, yb1, zb), (-bw, yb1, zb), (-tw, yt0, zt), (tw, yt0, zt), (tw, yt1, zt), (-tw, yt1, zt)]
    if burnt:  # sagging roof
        cv = [(x, y, z - (0.05 * rnd.random() if z > 1 else 0)) for x, y, z in cv]
    cf = [[3, 2, 1, 0], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]
    B.append(K.Part(cv, cf, paint))
    P = [V(p) for p in cv]
    faces = {
        "wind": [P[0], P[1], P[5], P[4]],
        "rear": [P[2], P[3], P[7], P[6]],
    }
    for side, (a, b, c, d) in (("l", (1, 2, 6, 5)), ("r", (3, 0, 4, 7))):
        A, Bq, C, Dq = P[a], P[b], P[c], P[d]
        if side == "l":  # front -> rear along +Y
            p, q = A.lerp(Bq, 0.47), Dq.lerp(C, 0.5)
            faces["front_l"] = [A, p, q, Dq]
            faces["rear_l"] = [p, Bq, C, q]
        else:  # this face runs rear -> front
            p, q = A.lerp(Bq, 0.53), Dq.lerp(C, 0.5)
            faces["rear_r"] = [A, p, q, Dq]
            faces["front_r"] = [p, Bq, C, q]
    for key, quad in faces.items():
        if burnt or key in broken or (door_open and key == "front_l"):
            B.append(D.quad_on(quad, "g05", shrink=0.14))
            if not burnt and key in broken:
                # a shard left in the frame
                q0, q1, q3 = quad[0], quad[1], quad[3]
                ctr = sum(quad, V()) / 4
                n = (q1 - q0).cross(quad[2] - q0).normalized()
                s0 = ctr + (q0 - ctr) * 0.86 + n * 0.009
                B.append(D.poly([s0, s0 + (q1 - q0) * 0.45, s0 + (q3 - q0) * 0.55], "glass_dark"))
        else:
            B.append(D.quad_on(quad, "glass_dark", shrink=0.14))
    # bumpers, lights, grille, plates
    trim = "metal_gun" if burnt else "metal_worn"
    B += [box((1.66, 0.1, 0.12), (0, -2.08, 0.44), swatch=trim), box((1.66, 0.1, 0.12), (0, 2.08, 0.44), swatch=trim)]
    if not burnt:
        B += [box((0.34, 0.03, 0.14), (0.52, -2.07, 0.66), swatch="headlight" if "light_l" not in broken else "g05"),
              box((0.34, 0.03, 0.14), (-0.52, -2.07, 0.66), swatch="headlight"),
              box((0.62, 0.03, 0.14), (0, -2.07, 0.66), swatch="metal_dark"),
              box((0.32, 0.03, 0.12), (0.58, 2.06, 0.7), swatch="plastic_red"),
              box((0.32, 0.03, 0.12), (-0.58, 2.06, 0.7), swatch="plastic_red"),
              box((0.5, 0.02, 0.11), (0, -2.14, 0.44), swatch="sign_white"),
              box((0.5, 0.02, 0.11), (0, 2.09, 0.62), swatch="sign_white"),
              box((0.05, 0.13, 0.08), (0.84, -0.72, 0.97), swatch="metal_dark"),
              box((0.05, 0.13, 0.08), (-0.84, -0.72, 0.97), swatch="metal_dark")]
    for k, (x, y, z, sx, sy, sz) in enumerate(rust):  # rust patches: flat irregular blobs on the surfaces
        rot = (HOOD_SLOPE, 0, 0) if (z < 0 and y < -0.8) else (0, 0, 0)
        if z < 0:
            z = hood_z(y) + 0.004
        sc = (sx * 0.6, sy * 0.6, max(sz, 0.006) * 0.6)
        B.append(D.blob(1.0, (x, y, z), rot, sc, "rust_dark" if k % 3 else "rust", seg=6, rings=3, jitter=0.3, seed=seed + k))
    for i in range(tracts):  # Cordon tracts stuck on the wet hood
        y = -1.75 + rnd.random() * 0.8
        B.append(box((0.15, 0.21, 0.004), (rnd.uniform(-0.5, 0.5), y, hood_z(y) + 0.006), (HOOD_SLOPE, 0, rnd.uniform(-60, 60)), swatch="paper"))
    if door_open:
        # driver's door (+X side) swung open, the opening shows the dark cabin
        B.append(rect_x(-0.78, 0.36, 0.24, 0.86, 0.806, "g15"))
        dp = [box((0.05, 1.02, 0.5), (0.025, 0.51, 0.61), swatch=paint),
              box((0.025, 0.86, 0.34), (0.02, 0.48, 1.04), swatch="glass_dark"),
              box((0.04, 0.04, 0.36), (0.02, 0.92, 1.04), swatch=paint)]
        D.xform(dp, K._mat((0.80, -0.78, 0), (0, 0, -58)))
        B += dp
    if rack:
        B += [box((0.04, 1.05, 0.04), (s * 0.55, 0.33, 1.4), swatch="metal_dark") for s in (-1, 1)]
        B += [box((1.18, 0.04, 0.04), (0, y, 1.42), swatch="metal_dark") for y in (-0.12, 0.33, 0.78)]
        B += [D.pillow((1.0, 1.25, 0.2), (0.0, 0.36, 1.53), (0, 0, 6), swatch="mattress", puff=0.15, sag=0.04),
              box((0.62, 0.42, 0.2), (0.15, 0.25, 1.7), (0, 0, 14), swatch="leather_brown"),
              D.pillow((0.55, 0.4, 0.26), (-0.3, 0.75, 1.7), (0, 0, -20), swatch="canvas_tan", puff=0.3),
              box((0.03, 1.3, 0.02), (0.28, 0.36, 1.63), (0, 0, 6), swatch="strap_black"),
              box((0.03, 1.3, 0.02), (-0.3, 0.36, 1.63), (0, 0, 6), swatch="strap_black")]
    # wheels
    W = []
    for key, x, y in (("fl", 0.715, -1.21), ("fr", -0.715, -1.21), ("rl", 0.715, 1.21), ("rr", -0.715, 1.21)):
        if burnt:
            W.append(D.disc(0.2, 0.12, (x * 0.97, y, 0.2), (0, 90, 0), "metal_dark", seg=8))
            continue
        fl = 0.07 if key in flats else 0.0
        r = 0.29
        t = D.disc(r, 0.17, (0, 0, 0), (0, 0, 22.5), "tire", seg=8).transformed(K._mat((x, y, r - fl * 0.6), (0, 90, 0)))
        if fl:
            for v in t.verts:
                if v.z < 0.03:
                    v.z = 0.0
                    if abs(v.x) > abs(x):
                        v.x += 0.025 if x > 0 else -0.025
        W.append(t)
        s = 1 if x > 0 else -1
        W.append(D.disc(0.13, 0.02, (x + s * 0.09, y, r - fl * 0.6), (0, 90, 0), "metal_worn" if not fl else "rust", seg=6))
    return B, W


def build_car(name, paint, seed, weight, cluster, body_rot=(0, 0, 0), tilt=None, extra=None, note="", **kw):
    B, W = car_parts(paint, seed, **kw)
    core = [B[0], B[1]]
    if any(body_rot):
        D.about(B, (0, 0, 0.29), body_rot)
    parts = B + W
    if tilt:
        D.about(parts, tilt[0], tilt[1])
    parts = D.flood(parts, 0.40, seed=seed)
    core = parts[:2]
    if extra:
        parts += extra
    return D.make(name, parts, "obstacle", cluster, weight, "medium", blocks=True, core=core, center_on=core, shrink=0.96, seed=seed,
                  ao=0.5, grime=0.25, note=note)


def cars():
    out = []
    # A: faded white, rusting, driver's door left open in the evacuation, tracts on the hood
    out.append(build_car("Car_A", "car_white", 3, 3, "street", broken=("rear_r",), door_open=True, tracts=3,
                         rust=[(0.25, 0.2, 1.365, 0.3, 0.35, 0.008), (-0.3, 0.6, 1.365, 0.18, 0.2, 0.008),
                               (0.4, -1.4, -1, 0.35, 0.28, 0.006), (0.805, 1.6, 0.62, 0.01, 0.4, 0.14),
                               (-0.805, -1.55, 0.6, 0.01, 0.3, 0.16), (0.0, 1.7, 0.885, 0.6, 0.4, 0.008)],
                         note="driver door open on the car's left (Unity -X), outside the collider; origin = body center"))
    # B: the evacuation car, roof rack loaded, two flat tires on the right, rear window smashed
    out.append(build_car("Car_B", "car_blue", 5, 2, "evacuation", broken=("rear", "front_r"), rack=True,
                         flats=("fr", "rr"), body_rot=(0, -2.6, 0),
                         rust=[(-0.805, 1.5, 0.62, 0.01, 0.5, 0.14), (0.3, -1.2, -1, 0.3, 0.3, 0.006)]))
    # C: pushed by the Crue nose-up onto a heap of flood debris, mud piled on its side, windshield smashed
    heap = []
    rnd = random.Random(41)
    for i in range(9):
        heap.append(D.chunk(0.22 + rnd.random() * 0.16, (rnd.uniform(-0.8, 0.8), rnd.uniform(-2.5, -0.9), 0.2),
                            (rnd.uniform(0, 90), rnd.uniform(0, 90), 0), (1.2, 1.0, 0.7), rnd.choice(["rubble", "concrete_dark", "brown_2"]), seed=i))
    heap += [box((2.0, 0.16, 0.04), (0.2, -1.6, 0.36), (0, -12, 25), swatch="wood_plank"),
             box((1.6, 0.14, 0.04), (-0.4, -2.0, 0.3), (8, 10, -40), swatch="wood_dark"),
             D.limb((-1.2, -2.6, 0.05), (0.9, -1.1, 0.45), 0.07, 0.04, "bark", seg=5)]
    mud = [D.blob(0.5, (-1.0, y, 0.0), (0, 0, rnd.uniform(-20, 20)), (0.7, 1.3, 0.55), rnd.choice(["mud_wet", "brown_2"]), 7, 4, 0.2, seed=60 + i)
           for i, y in enumerate((-1.0, 0.2, 1.2))]
    mud2 = D.prism(D.blob_outline(14, 1.4, 1.0, seed=4, center=(0, -1.7)), 0.0, 0.22, "brown_2", "mud_wet", inset=0.55)
    out.append(build_car("Car_C", "car_green", 9, 1.5, "flood", broken=("wind", "front_l", "light_l"), flats=("rl",),
                         tilt=((0, 1.21, 0), (-9, 4, 6)), extra=heap + mud + [mud2],
                         rust=[(0.805, 0.4, 0.6, 0.01, 0.6, 0.18), (-0.2, 0.9, 1.365, 0.4, 0.3, 0.008)],
                         note="pushed nose-up onto a debris heap by the Crue"))
    # Burnt: a wreck from a Cordon strike, on its rims, hood popped, scorched ground
    B, W = car_parts("rust_dark", 13, burnt=True)
    D.speckle(B[:2], [("g15", 5), ("smoke", 3), ("rust_dark", 3), ("metal_gun", 2), ("g25", 1), ("rust", 0.7), ("car_red", 0.3)], seed=13)
    D.xform(B, D.M((0, 0, -0.14)))
    hood = box((1.5, 1.1, 0.03), (0, -1.45, 0.72), swatch="rust_dark")
    D.about([hood], (0, -0.9, 0.74), (-14, 0, 3))
    scorch = [D.prism(D.blob_outline(18, 2.4, 3.1, seed=7, wobble=0.35), 0, 0.006, "g05"),
              D.prism(D.blob_outline(18, 3.0, 3.7, seed=8, wobble=0.4), 0, 0.003, "smoke")]
    rnd = random.Random(13)
    ash = [D.blob(0.25, (rnd.uniform(-1.6, 1.6), rnd.uniform(-2.6, 2.6), 0.0), scale=(1.4, 1.0, 0.18), swatch=rnd.choice(["g45", "g25"]), seed=i) for i in range(5)]
    core = B[:2]
    parts = B + W + [hood] + scorch + ash
    out.append(D.make("Car_Burnt", parts, "obstacle", "strike", 1.0, "medium", blocks=True, core=core, center_on=core, shrink=0.96, seed=13,
                      ao=0.55, grime=0.3, note="scorch mark on the ground is part of the mesh (6 x 7.4 m)"))
    return out


# =============================================================================================
# The Cordon: barriers, sandbags, the pole barrier (set up after the Crue: no flood line)
# =============================================================================================

def jersey():
    prof = [(-0.305, 0), (0.305, 0), (0.305, 0.075), (0.18, 0.255), (0.075, 0.81), (-0.075, 0.81), (-0.18, 0.255), (-0.305, 0.075)]
    b = D.extrude_yz(prof, -1.0, 1.0, "concrete", "concrete")
    for v in b.verts:  # a broken corner at one end
        if v.x > 0.99 and v.z > 0.8 and v.y > 0:
            v.z -= 0.13
            v.x -= 0.12
        if v.x > 0.99 and abs(v.z - 0.255) < 0.01 and v.y > 0:
            v.x -= 0.05
    parts = [b, box((0.12, 0.25, 0.07), (-0.62, 0, 0.6), swatch="g15"), box((0.12, 0.25, 0.07), (0.6, 0, 0.6), swatch="g15")]
    for x in (-0.75, 0.62):  # lifting loops
        parts += [D.limb((x - 0.06, 0, 0.8), (x - 0.06, 0, 0.88), 0.012, 0.012, "rust", 4),
                  D.limb((x + 0.06, 0, 0.8), (x + 0.06, 0, 0.88), 0.012, 0.012, "rust", 4),
                  D.limb((x - 0.07, 0, 0.88), (x + 0.07, 0, 0.88), 0.012, 0.012, "rust", 4)]
    tilt = math.degrees(math.atan2(0.105, 0.555))
    for ang in (35, -35):  # the Cordon's search mark, sprayed on the -Y side
        m = K._mat((0.3, -0.134, 0.52)) @ K._mat(rot=(-tilt, 0, 0)) @ K._mat(rot=(0, ang, 0))
        parts.append(box((0.44, 0.02, 0.05), swatch="red_3").transformed(m))
    return D.make("Barrier_Jersey", parts, "obstacle", "checkpoint", 4, "medium", blocks=True, core=[b], shrink=1.0, seed=17,
                  ao=0.45, grime=0.3, grime_height=0.4)


def sandbags():
    rnd = random.Random(21)
    R = 2.0
    parts = []
    rows = 5
    for r in range(rows):
        n = 7 if r % 2 == 0 else 6
        span = math.radians(108)
        for i in range(n):
            if r == rows - 1 and i in (0, 4):
                continue  # gaps in the top row
            t = (i + 0.5 + (0.0 if r % 2 == 0 else 0.5)) / 7.0
            a = -span / 2 + span * t
            x, y = R * math.sin(a), R - R * math.cos(a) - 0.3
            z = 0.075 + r * 0.15
            sw = rnd.choices(["sandbag", "canvas_tan", "canvas_olive", "military_green"], [6, 2, 2, 1])[0]
            parts.append(D.pillow((0.58 + rnd.uniform(-0.03, 0.03), 0.34, 0.16), (x + rnd.uniform(-0.02, 0.02), y, z),
                                  (rnd.uniform(-3, 3), rnd.uniform(-3, 3), math.degrees(a) + rnd.uniform(-6, 6)), sw, puff=0.2, sag=0.02))
    # fallen bags and a torn one spilling sand
    parts += [D.pillow((0.58, 0.34, 0.16), (-0.7, -0.75, 0.08), (0, 0, 70), "sandbag", puff=0.22),
              D.pillow((0.58, 0.34, 0.16), (1.6, 0.2, 0.08), (0, 8, -20), "canvas_tan", puff=0.22),
              D.pillow((0.5, 0.3, 0.1), (0.5, -0.65, 0.05), (0, 0, 15), "sandbag", puff=0.3),
              D.prism(D.blob_outline(9, 0.45, 0.3, seed=3, center=(0.7, -0.85)), 0, 0.06, "mud_dry", inset=0.6)]
    return D.make("Sandbags", parts, "obstacle", "checkpoint", 2, "medium", blocks=True, shrink=0.85, seed=21, ao=0.55, grime=0.2,
                  note="curved Cordon firing position, convex side = forward (+Z), concave side behind")


def checkpoint():
    parts = [box((0.5, 0.5, 0.15), (-2.0, 0, 0.075), swatch="concrete_dark"),
             box((0.24, 0.24, 1.0), (-2.0, 0, 0.6), swatch="metal_dark"),
             box((0.5, 0.3, 0.3), (-2.42, 0, 0.98), swatch="concrete"),
             box((0.14, 0.32, 0.14), (-2.0, 0, 1.0), swatch="metal_gun")]
    n = 10
    x0, x1 = -1.95, 2.05
    for i in range(n):
        a = x0 + (x1 - x0) * i / n
        b = x0 + (x1 - x0) * (i + 1) / n
        parts.append(D.limb((a, 0, 1.0 - 0.012 * i), (b, 0, 1.0 - 0.012 * (i + 1)), 0.05, 0.045,
                            "red_danger" if i % 2 == 0 else "sign_white", seg=6))
    parts += [box((0.09, 0.09, 0.9), (2.0, 0, 0.45), swatch="metal_worn"),
              box((0.04, 0.12, 0.12), (1.95, 0, 0.94), (0, 20, 0), swatch="metal_worn"),
              box((0.04, 0.12, 0.12), (2.05, 0, 0.94), (0, -20, 0), swatch="metal_worn"),
              box((0.4, 0.4, 0.08), (2.0, 0, 0.04), swatch="concrete_dark")]
    # a round "no entry" plate hung from the pole: red disc, white bar, both faces
    parts += [D.disc(0.24, 0.02, (0.1, 0, 0.66), (90, 0, 0), "red_danger", seg=10),
              box((0.3, 0.03, 0.07), (0.1, 0, 0.66), swatch="sign_white"),
              box((0.015, 0.015, 0.12), (0.1, 0, 0.9), swatch="metal_dark")]
    parts += [D.pillow((0.56, 0.34, 0.16), (-2.0, 0.42, 0.08), (0, 0, 5), "sandbag", puff=0.2),
              D.pillow((0.56, 0.34, 0.16), (-2.05, -0.42, 0.08), (0, 0, -8), "sandbag", puff=0.2),
              D.pillow((0.56, 0.34, 0.16), (-2.0, 0.4, 0.23), (0, 0, 15), "canvas_olive", puff=0.2)]
    return D.make("Barrier_Checkpoint", parts, "obstacle", "checkpoint", 1, "medium", blocks=True,
                  collider=((4.9, 0.5, 1.15), (0, 0, 0.575)), seed=23, ao=0.45, grime=0.2,
                  note="lowered red/white pole, 4 m: thin line obstacle")


# =============================================================================================
# Street furniture (there before the Crue: flood line at 40 cm)
# =============================================================================================

def lamp():
    parts = [D.disc(0.17, 0.5, (0, 0, 0.25), swatch="concrete_dark", seg=6),
             D.limb((0, 0, 0.45), (0, 0, 4.6), 0.11, 0.07, "concrete_light", seg=6),
             box((0.12, 0.03, 0.28), (0, -0.165, 0.95), swatch="metal_dark")]
    pts = [V((0, 0, 4.5)), V((0, -0.25, 4.85)), V((0, -0.7, 5.02)), V((0, -1.25, 5.05))]
    for a, b in zip(pts, pts[1:]):
        parts.append(D.limb(a, b, 0.05, 0.04, "metal_worn", seg=5))
    head = [box((0.34, 0.6, 0.12), (0, -1.45, 5.06), swatch="metal_worn", taper=(0.8, 0.9)),
            D.disc(0.13, 0.22, (0, -1.55, 4.89), swatch="lamp_cold", seg=8, radius2=0.2),
            D.disc(0.21, 0.03, (0, -1.55, 5.0), swatch="metal_dark", seg=8)]
    D.about(head, (0, -1.2, 5.05), (-6, 0, 0))
    parts += head
    parts = D.flood(parts, 0.40, seed=5)
    return D.make("Lamp", parts, "tall", "street", 3, "small", blocks=True, collider=((0.36, 0.36, 4.6), (0, 0, 2.3)),
                  center=False, seed=25, ao=0.4, grime=0.2, note="origin = pole base; arm + cold-white emissive head 1.5 m forward (+Z)")


def bus_shelter():
    rnd = random.Random(27)
    parts = [box((3.1, 0.14, 2.25), (0, 0.66, 1.125), swatch="concrete")]
    for (x0, z0, x1, z1, sw) in ((-1.45, 0.7, 1.45, 2.15, "plaster_blue"), (-1.45, 0.7, 1.45, 1.15, "tile_green"),
                                  (-1.2, 1.15, -0.2, 1.4, "green_3"), (0.3, 1.3, 1.1, 2.0, "plastic_yellow"),
                                  (-0.9, 1.6, 0.1, 1.75, "tile_white"), (-0.6, 1.85, 0.4, 1.95, "tile_white")):
        parts.append(rect_y(x0, z0, x1, z1, 0.585 - 0.002 * len(parts), sw))  # abstract mosaic, inner face
    for x in (-1.55, 1.55):
        for y in (-0.62, 0.6):
            parts.append(box((0.07, 0.07, 2.25), (x, y, 1.125), swatch="metal_dark"))
    parts += [box((3.2, 0.08, 0.1), (0, -0.62, 2.2), swatch="metal_dark"),
              box((0.03, 1.15, 1.55), (-1.55, 0.0, 1.25), swatch="glass_dark"),
              box((0.03, 1.15, 0.06), (1.55, 0.0, 0.45), swatch="metal_dark"),
              D.poly([(1.56, -0.55, 0.48), (1.56, 0.2, 0.48), (1.56, -0.4, 0.95)], "glass_dark"),
              D.poly([(1.54, -0.4, 0.95), (1.54, 0.2, 0.48), (1.54, -0.55, 0.48)], "glass_dark")]
    roof = [box((3.45, 1.75, 0.1), (0, 0.0, 2.33), swatch="concrete_light")]
    roof += [D.blob(0.22, (rnd.uniform(-1.3, 1.3), rnd.uniform(-0.5, 0.5), 2.39), scale=(1.5, 1.0, 0.1), swatch=s, seg=9, rings=3, jitter=0.35, seed=i)
             for i, s in enumerate(["moss", "leaf_dark", "moss", "mud_wet"])]
    roof += [D.limb((-1.5, -0.6, 2.4), (0.6, 0.5, 2.44), 0.05, 0.02, "bark", seg=4),
             box((0.15, 0.21, 0.004), (0.9, -0.3, 2.385), (0, 0, 30), swatch="paper")]
    D.about(roof, (0, 0, 2.3), (-3, 0, 0))
    parts += roof
    parts += [box((2.4, 0.14, 0.04), (0, 0.38, 0.46), swatch="wood_plank"), box((2.4, 0.14, 0.04), (0, 0.2, 0.46), (0, 0, 1.5), swatch="wood"),
              box((0.06, 0.32, 0.44), (-1.0, 0.3, 0.22), swatch="metal_dark"), box((0.06, 0.32, 0.44), (1.0, 0.3, 0.22), swatch="metal_dark"),
              D.limb((1.85, -0.75, 0), (1.85, -0.75, 2.1), 0.03, 0.03, "metal_worn", 6),
              box((0.36, 0.02, 0.46), (1.85, -0.77, 1.82), swatch="sign_blue"),
              box((0.2, 0.025, 0.2), (1.85, -0.775, 1.86), swatch="sign_white"),
              D.disc(0.2, 0.7, (-1.85, -0.5, 0.35), swatch="metal_dark", seg=8)]
    parts = D.flood(parts, 0.40, seed=27)
    return D.make("BusShelter", parts, "obstacle", "street", 0.6, "medium", blocks=True,
                  collider=((3.3, 1.45, 2.45), (0, 0.05, 1.225)), seed=27, ao=0.55, grime=0.25,
                  note="open side = forward (+Z), abstract mosaic on the back wall")


def kiosk():
    rnd = random.Random(29)
    parts = [box((2.2, 1.6, 0.9), (0, 0, 0.45), swatch="plaster_green"),
             box((2.08, 1.48, 1.15), (0, 0, 1.475), swatch="g15")]
    for x in (-1.06, 1.06):
        for y in (-0.76, 0.76):
            parts.append(box((0.09, 0.09, 1.15), (x, y, 1.475), swatch="metal_dark"))
    # front (-Y): three panes, the middle one is the service window with its shelf, the right one shuttered
    xs = [-1.0, -0.34, 0.34, 1.0]
    for i in range(3):
        if i == 2:
            parts.append(rect_y(xs[i] + 0.03, 0.95, xs[i + 1] - 0.03, 2.0, -0.745, "metal_worn"))
            for k in range(5):
                parts.append(box((0.6, 0.02, 0.025), ((xs[i] + xs[i + 1]) / 2, -0.75, 1.05 + k * 0.2), swatch="metal_gun"))
        else:
            parts.append(rect_y(xs[i] + 0.03, 0.95, xs[i + 1] - 0.03, 2.0, -0.745, "glass_dark"))
    parts.append(box((0.8, 0.26, 0.04), (0, -0.92, 0.98), swatch="metal_worn"))
    # sides: +X broken, -X intact; back: door
    parts += [rect_x(-0.7, 0.95, 0.7, 2.0, 1.045, "g05"),
              D.poly([(1.05, -0.7, 0.96), (1.05, 0.1, 0.96), (1.05, -0.7, 1.55)], "glass_dark"),
              rect_x(-0.7, 0.95, 0.7, 2.0, -1.045, "glass_dark", facing=-1),
              rect_y(-0.4, 0.02, 0.4, 2.0, 0.805, "metal_dark", facing=1)]
    parts += [box((2.32, 1.72, 0.26), (0, 0, 2.12), swatch="sign_white"),
              rect_y(-1.16, 2.06, 1.16, 2.16, -0.865, "sign_blue"), rect_y(-1.16, 2.06, 1.16, 2.16, 0.865, "sign_blue", facing=1),
              rect_x(-0.86, 2.06, 0.86, 2.16, 1.165, "sign_blue"), rect_x(-0.86, 2.06, 0.86, 2.16, -1.165, "sign_blue", facing=-1),
              box((2.46, 1.86, 0.08), (0, 0, 2.29), swatch="metal_dark")]
    parts += [box((0.5, 0.4, 0.12), (0.5, 0.3, 2.39), (0, 0, 20), swatch="cardboard"),
              D.blob(0.35, (-0.6, -0.2, 2.335), scale=(1.4, 1.0, 0.12), swatch="moss", seg=9, rings=3, jitter=0.35, seed=3),
              D.blob(0.25, (0.2, -0.6, 2.335), scale=(1.4, 1.0, 0.1), swatch="leaf_dark", seg=9, rings=3, jitter=0.35, seed=4)]
    # bundles of unsold newspapers dumped at the back, soaked
    for i in range(3):
        parts.append(box((0.42, 0.3, 0.14), (rnd.uniform(-0.9, 0.9), 1.05 + rnd.uniform(0, 0.2), 0.07 + 0.14 * (i == 2)), (0, 0, rnd.uniform(-30, 30)),
                         swatch=["paper", "g65", "plaster"][i]))
    parts = D.flood(parts, 0.40, seed=29)
    return D.make("Kiosk", parts, "obstacle", "street", 0.6, "medium", blocks=True, collider=((2.3, 1.7, 2.35), (0, 0, 1.175)),
                  seed=29, ao=0.55, grime=0.25, note="newspaper kiosk, service window = forward (+Z)")


def wall_ruin_a():
    """Low broken brick wall, plaster coming off the -Y face, rubble at its foot."""
    rnd = random.Random(31)
    h = 1.35
    top = [(2.1, h)]
    x = 2.1
    while x > -2.1 + 1e-6:
        x2 = max(-2.1, x - rnd.choice([0.125, 0.25, 0.25, 0.375]))
        top.append((x2, h))
        if x2 <= -2.1 + 1e-6:
            break
        target = 2.1 if x2 < -1.1 else (0.95 if x2 < 0.5 else 1.45)
        step = round((target - h) / 0.075 * rnd.uniform(0.35, 0.8)) + rnd.choice([-1, 0, 0, 1])
        h2 = max(0.9, min(2.15, h + step * 0.075))
        if abs(h2 - h) > 1e-4:
            top.append((x2, h2))
        h = h2
        x = x2
    prof = [(-2.1, 0.0), (2.1, 0.0)] + top
    wall = D.extrude_xz(prof, -0.14, 0.14, "brick", "brick")
    parts = [wall]
    hz = lambda xx: min(hh for (px, hh) in top if abs(px - xx) < 0.4) if any(abs(px - xx) < 0.4 for px, _ in top) else 1.0
    for i, (cx, cz, rx, rz) in enumerate(((-1.3, 1.1, 0.75, 0.75), (0.4, 0.55, 0.9, 0.4), (1.5, 0.7, 0.55, 0.55))):
        pts = []
        for k in range(10):
            a = math.tau * k / 10
            px = cx + math.cos(a) * rx * (0.8 + rnd.random() * 0.35)
            pz = cz + math.sin(a) * rz * (0.8 + rnd.random() * 0.35)
            pts.append((max(-2.08, min(2.08, px)), max(0.0, min(hz(px) - 0.08, pz))))
        parts.append(D.poly([(px, -0.141 - 0.002 * i, pz) for px, pz in pts], "plaster" if i != 1 else "plaster_green"))
    for i in range(3):
        px, pz = top[rnd.randrange(len(top))]
        parts.append(box((0.25, 0.12, 0.075), (max(-1.9, min(1.9, px)), rnd.uniform(-0.05, 0.05), pz + 0.037), (0, 0, rnd.uniform(-20, 20)), swatch="brick"))
    rub = [D.prism(D.blob_outline(14, 0.9, 0.5, seed=2, center=(-0.4, 0.55)), 0, 0.12, "concrete_dark", inset=0.5)]
    for i in range(9):
        rub.append(box((0.25, 0.12, 0.075), (rnd.uniform(-1.2, 0.6), rnd.uniform(0.25, 0.9), 0.06 + rnd.random() * 0.08),
                       (rnd.uniform(-30, 30), rnd.uniform(-30, 30), rnd.uniform(0, 180)), swatch=rnd.choice(["brick", "brick", "red_2"])))
    rub += [D.chunk(0.16, (rnd.uniform(-1.0, 0.5), rnd.uniform(0.3, 0.8), 0.1), scale=(1, 1, 0.7), swatch=("plaster", "concrete", "brick")[i], seed=i) for i in range(3)]
    parts = D.flood(parts, 0.40, seed=31) + rub
    return D.make("Wall_Ruin_A", parts, "obstacle", "flood", 1.5, "medium", blocks=True, collider=((4.2, 0.4, 2.15), (0, 0, 1.075)),
                  seed=31, ao=0.55, grime=0.25, note="4.2 m brick wall 0.9-2.1 m, plaster on the front (+Z) face, rubble behind (not in the collider)")


def wall_ruin_b():
    """Prefab concrete fence (the diamond-relief panels of every Eastern factory), one panel broken."""
    rnd = random.Random(33)
    parts = []
    t = 0.12
    for k, (x0, x1, top_fn) in enumerate(((-2.1, -0.05, None), (0.05, 2.1, True))):
        if top_fn:
            pts = [(x0, 0.0), (x1, 0.0), (x1, 1.15)]
            xx = x1
            while xx > x0 + 0.1:
                xx -= rnd.uniform(0.15, 0.35)
                pts.append((max(x0, xx), 1.05 + rnd.uniform(-0.15, 0.45) + (0.4 if xx < x0 + 0.5 else 0)))
            pts.append((x0, 1.9))
            panel = D.extrude_xz(pts, -t / 2, t / 2, "concrete", "concrete")
            edge = sorted(pts[2:], key=lambda p: p[0])

            def top_at(xq, edge=edge):
                for (ax, az), (bx, bz) in zip(edge, edge[1:]):
                    if ax <= xq <= bx:
                        return az + (bz - az) * (xq - ax) / max(bx - ax, 1e-6)
                return edge[0][1]
        else:
            panel = D.extrude_xz([(x0, 0), (x1, 0), (x1, 1.9), (x0, 1.9)], -t / 2, t / 2, "concrete", "concrete")
        parts.append(panel)
        # diamond relief on the -Y face
        cols, rows = 6, 6
        w = (x1 - x0 - 0.16) / cols
        hgt = 1.5 / rows
        for i in range(cols):
            for j in range(rows):
                cx = x0 + 0.08 + w * (i + 0.5)
                cz = 0.25 + hgt * (j + 0.5)
                if top_fn and cz + hgt * 0.5 > min(top_at(cx - w * 0.45), top_at(cx), top_at(cx + w * 0.45)) - 0.04:
                    continue
                y = -t / 2
                pts3 = [V((cx, y, cz - hgt * 0.45)), V((cx + w * 0.45, y, cz)), V((cx, y, cz + hgt * 0.45)), V((cx - w * 0.45, y, cz))]
                apex = V((cx, y - 0.03, cz))
                for a in range(4):
                    p, q = pts3[a], pts3[(a + 1) % 4]
                    parts.append(D.poly([p, q, apex], "concrete_light" if (i + j) % 3 == 0 else "concrete"))
    for x in (-2.18, 0.0, 2.18):
        parts.append(box((0.16, 0.2, 2.05 if x < 1 else 1.4), (x, 0.0, (2.05 if x < 1 else 1.4) / 2), swatch="concrete_dark"))
    for i in range(4):
        x = 0.4 + i * 0.32
        parts.append(D.limb((x, 0.0, 1.0), (x + rnd.uniform(-0.1, 0.2), rnd.uniform(-0.15, 0.15), 1.35 + rnd.random() * 0.2), 0.012, 0.012, "rust", 4))
    chunk = D.extrude_xz([(0, 0), (0.9, 0), (0.8, 0.55), (0.1, 0.62)], -t / 2, t / 2, "concrete", "concrete")
    D.xform([chunk], K._mat((0.6, -0.55, 0.06), (82, 0, 12)))
    parts += [chunk, D.limb((0.7, -0.4, 0.15), (0.6, -0.95, 0.3), 0.012, 0.012, "rust", 4)]
    parts += [D.chunk(0.14, (rnd.uniform(0.2, 1.8), rnd.uniform(-0.6, -0.2), 0.06), scale=(1, 1, 0.6), swatch="concrete", seed=40 + i) for i in range(5)]
    walls = [p for p in parts]
    parts = D.flood(walls[:-7], 0.40, seed=33) + walls[-7:]
    return D.make("Wall_Ruin_B", parts, "obstacle", "flood", 1.5, "medium", blocks=True, collider=((4.5, 0.4, 2.05), (0, 0.0, 1.025)),
                  seed=33, ao=0.5, grime=0.25, note="prefab concrete fence, diamond relief on the front (+Z), one panel broken")


def container_regen():
    """A REGEN 20 ft container sunk 45 cm into the dried flood mud, slightly tilted. White, cyan band."""
    L, Wd, Hh = 6.06, 2.44, 2.12
    parts = []
    pitch = 0.34
    nrib = int((L - 0.3) / pitch)
    x0 = -nrib * pitch / 2
    for sgn in (-1, 1):
        pts = []
        for i in range(nrib):
            xb = x0 + i * pitch
            pts += [(xb, 0.0), (xb + 0.07, 0.0), (xb + 0.105, 0.04), (xb + 0.175, 0.04), (xb + 0.21, 0.0)]
        pts.append((x0 + nrib * pitch, 0.0))
        verts, faces = [], []
        for (px, d) in pts:
            y = sgn * (Wd / 2 - 0.04 + d)
            verts += [(px, y, 0.0), (px, y, Hh - 0.1)]
        for i in range(len(pts) - 1):
            a, b = 2 * i, 2 * i + 2
            f = [a, b, b + 1, a + 1]
            faces.append(f if sgn < 0 else f[::-1])
        side = K.Part(verts, faces, "regen_white")
        parts += D.band([side], 1.45, 1.72, "cyan_paint")
    parts += [rect_x(-Wd / 2, 0.0, Wd / 2, Hh - 0.1, L / 2 - 0.02, "regen_white"),
              rect_x(-Wd / 2, 0.0, Wd / 2, Hh - 0.1, -L / 2 + 0.02, "regen_grey", facing=-1)]
    for y in (-0.75, -0.45, 0.45, 0.75):
        parts.append(D.limb((L / 2 + 0.03, y, 0.0), (L / 2 + 0.03, y, Hh - 0.15), 0.025, 0.025, "regen_plate", 6))
    parts.append(box((0.03, 0.03, Hh - 0.12), (L / 2 + 0.01, 0, (Hh - 0.12) / 2), swatch="g25"))
    parts.append(box((L, Wd, 0.06), (0, 0, Hh - 0.07), swatch="regen_white"))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(box((0.16, 0.16, Hh), (sx * (L / 2 - 0.08), sy * (Wd / 2 - 0.08), Hh / 2), swatch="regen_grey"))
        parts.append(box((L, 0.12, 0.12), (0, sx * (Wd / 2 - 0.06), Hh - 0.08), swatch="regen_grey"))
    parts += [box((0.5, Wd - 0.1, 0.01), (-1.2, 0, Hh - 0.035), swatch="cyan_paint"),  # roof stripe, reads from the camera
              D.prism(D.blob_outline(10, 0.6, 0.4, seed=5, center=(1.3, 0.3)), Hh - 0.04, Hh - 0.035, "blue_1"),
              D.prism([(math.cos(math.tau * k / 6) * 0.28, math.sin(math.tau * k / 6) * 0.28) for k in range(6)], 0, 0.004, "cyan_paint")]
    hexa = parts.pop()  # REGEN hexagon, stood up on the -Y side
    D.xform([hexa], K._mat((-2.0, -Wd / 2 - 0.045, 1.0), (90, 0, 0)))
    parts.append(hexa)
    parts = D.flood(parts, 0.40, seed=35)
    D.about(parts, (0, 0, 0), (3.0, -1.5, 0))
    D.xform(parts, D.M((0, 0, -D.bounds(parts)[0].z)))
    mound = [D.prism(D.blob_outline(20, L / 2 + 1.1, Wd / 2 + 0.9, seed=6, wobble=0.2), 0, 0.3, "brown_2", "mud_wet", inset=0.35)]
    rnd = random.Random(35)
    for i in range(7):
        a = rnd.random() * math.tau
        mound.append(D.blob(0.45, (math.cos(a) * (L / 2 + 0.3), math.sin(a) * (Wd / 2 + 0.3), 0.05), (0, 0, rnd.uniform(0, 90)), (1.4, 1.0, 0.45),
                            rnd.choice(["mud_wet", "brown_2", "brown_3"]), 7, 3, 0.25, seed=70 + i))
    return D.make("Container_Regen", parts + mound, "obstacle", "checkpoint", 0.4, "large", blocks=True,
                  collider=((6.1, 2.5, 2.2), (0, 0, 1.1)), seed=35, ao=0.5, grime=0.3,
                  note="REGEN 20 ft container sunk in flood mud, 2.2 m visible; cyan band + roof stripe")


def fence_chain():
    rnd = random.Random(37)
    parts = [D.limb((s * 1.5, 0, 0), (s * 1.5, 0, 2.02), 0.032, 0.03, "metal_worn", 6) for s in (-1, 1)]
    parts.append(D.limb((-1.5, 0, 1.97), (1.5, 0, 1.97), 0.02, 0.02, "metal_worn", 5))
    x0, x1, z0, z1 = -1.46, 1.46, 0.06, 1.95

    def bulge(x, z):
        k = max(0.0, 1.0 - z / 0.85) ** 1.5
        return 0.22 * k * math.exp(-((x - 0.35) / 0.8) ** 2)

    w = 0.012
    step = 0.33 * math.sqrt(2)
    for d in (1, -1):
        cmin, cmax = (x0 - z1, x1 - z0) if d == 1 else (x0 + z0, x1 + z1)
        c = cmin + step * 0.5
        while c < cmax:
            if d == 1:
                za, zb = max(z0, x0 - c), min(z1, x1 - c)
                pa, pb = (za + c, za), (zb + c, zb)
            else:
                za, zb = max(z0, c - x1), min(z1, c - x0)
                pa, pb = (c - za, za), (c - zb, zb)
            c += step
            if zb - za < 0.05:
                continue
            for s in range(3):
                t0, t1 = s / 3, (s + 1) / 3
                a = (pa[0] + (pb[0] - pa[0]) * t0, pa[1] + (pb[1] - pa[1]) * t0)
                b = (pa[0] + (pb[0] - pa[0]) * t1, pa[1] + (pb[1] - pa[1]) * t1)
                mx = (a[0] + b[0]) / 2
                if 0.7 < mx < 1.3 and (a[1] + b[1]) / 2 < 0.7:
                    continue  # torn open
                dx, dz = b[0] - a[0], b[1] - a[1]
                L = math.hypot(dx, dz)
                nx, nz = -dz / L * w, dx / L * w
                qa = [(a[0] - nx, a[1] - nz), (b[0] - nx, b[1] - nz), (b[0] + nx, b[1] + nz), (a[0] + nx, a[1] + nz)]
                for side in (-1, 1):
                    pts = [(px, bulge(px, pz) + side * 0.004, pz) for px, pz in qa]
                    if (dx * 1 > 0) == (side < 0):
                        pts = pts[::-1]
                    # orient: front copy faces -Y, back copy faces +Y
                    p0, p1, p2 = V(pts[0]), V(pts[1]), V(pts[2])
                    nrm = (p1 - p0).cross(p2 - p0)
                    if (nrm.y > 0) != (side > 0):
                        pts = pts[::-1]
                    parts.append(D.poly(pts, "metal_worn"))
    parts = D.flood(parts, 0.40, seed=37)
    # flood debris caught in the mesh (+Y side, the way the water went)
    parts += [D.blob(0.22, (-0.7, 0.12, 0.12), scale=(1.6, 0.5, 0.6), swatch="plastic_white", seed=1),
              D.blob(0.2, (0.2, 0.28, 0.1), scale=(1.5, 0.6, 0.5), swatch="plastic_blue", seed=2),
              D.blob(0.25, (-1.2, 0.14, 0.1), scale=(1.0, 0.5, 0.5), swatch="grass_dead", seed=3),
              box((0.3, 0.004, 0.22), (-0.2, 0.03, 0.3), (0, 0, 4), swatch="paper"),
              box((0.25, 0.004, 0.2), (1.1, 0.1, 0.25), (10, 0, -8), swatch="g65")]
    return D.make("Fence_Chain", parts, "obstacle", "checkpoint", 2, "medium", blocks=True, collider=((3.1, 0.2, 2.0), (0, 0.0, 1.0)),
                  seed=37, ao=0.25, grime=0.2, note="3 m chain-link segment, bottom pushed backward (-Z) by the Crue, debris caught behind")


def sign_road():
    pole = [D.limb((0, 0, 0), (0, 0, 2.55), 0.035, 0.03, "metal_worn", 6)]
    tri = [(-0.35, 0.0), (0.35, 0.0), (0.0, 0.606)]
    border = D.extrude_xz(tri, -0.01, 0.01, "red_danger", "metal_worn", back="metal_worn")
    inner = D.extrude_xz([(x * 0.72, z * 0.72 + 0.055) for x, z in tri], -0.016, -0.01, "sign_white", "sign_white")
    bang = [box((0.045, 0.006, 0.17), (0, -0.017, 0.29), swatch="hazard_black"), box((0.045, 0.006, 0.045), (0, -0.017, 0.14), swatch="hazard_black")]
    plate = [box((0.5, 0.02, 0.28), (0, 0, -0.28), swatch="metal_worn"), box((0.46, 0.006, 0.24), (0, -0.012, -0.28), swatch="sign_blue"),
             box((0.3, 0.008, 0.06), (0, -0.016, -0.28), swatch="sign_white")]
    head = [border, inner] + bang + plate
    D.xform(head, K._mat((0, -0.045, 1.95)))
    head.append(box((0.08, 0.06, 0.06), (0, -0.02, 2.1), swatch="metal_dark"))
    parts = pole + head
    D.about(parts, (0, 0, 0), (7, 0, 0))
    parts = D.flood(parts, 0.40, seed=39)
    return D.make("Sign_Road", parts, "tall", "street", 1.5, "small", blocks=True, collider=((0.16, 0.16, 2.6), (0, 0, 1.3)),
                  center=False, seed=39, ao=0.3, grime=0.15, note="warning triangle '!' + blank blue plate facing forward (+Z), pole bent by the flood")


def tree_dead():
    rnd = random.Random(43)
    parts = [D.limb((0, 0, 0), (0, 0, 0.35), 0.3, 0.17, "bark", 6)]

    def grow(p, d, length, r0, depth):
        q = p + d.normalized() * length
        r1 = r0 * 0.6
        parts.append(D.limb(p, q, r0, r1, "bark", 5 if depth == 0 else (4 if depth < 2 else 3)))
        if depth >= 3:
            return
        n = 3 if depth == 0 else 2
        for i in range(n):
            ang = math.tau * i / n + rnd.uniform(-0.5, 0.5) + depth
            tiltd = rnd.uniform(0.45, 0.8) if depth == 0 else rnd.uniform(0.35, 0.7)
            nd = d.normalized() * math.cos(tiltd) + V((math.cos(ang), math.sin(ang), 0)) * math.sin(tiltd)
            grow(q, nd, length * rnd.uniform(0.55, 0.72), r1 * 0.95, depth + 1)

    grow(V((0, 0, 0.3)), V((0.05, 0.03, 1)), 2.2, 0.17, 0)
    parts.append(D.limb((0.4, -0.3, 0.04), (1.8, -0.9, 0.08), 0.06, 0.02, "bark", 4))
    parts = D.flood(parts, 0.40, seed=43)
    parts.append(D.prism(D.blob_outline(14, 0.7, 0.7, seed=1), 0, 0.03, "brown_1", inset=0.3))
    return D.make("Tree_Dead", parts, "tall", "street", 2, "medium", blocks=True, collider=((0.45, 0.45, 2.5), (0, 0, 1.25)),
                  center=False, seed=43, ao=0.3, grime=0.2, note="origin = trunk base")


def planter():
    rnd = random.Random(45)
    r_out, r_in, h = 0.62, 0.5, 0.55
    verts, faces, sws = [], [], []
    for i in range(6):
        a = math.tau * i / 6
        c, s = math.cos(a), math.sin(a)
        verts += [(c * r_out, s * r_out, 0), (c * r_out, s * r_out, h), (c * r_in, s * r_in, h), (c * r_in, s * r_in, h - 0.1)]
    for i in range(6):
        j = (i + 1) % 6
        a, b = i * 4, j * 4
        faces += [[a, b, b + 1, a + 1], [a + 1, b + 1, b + 2, a + 2], [a + 2, b + 2, b + 3, a + 3]]
        sws += ["concrete", "concrete_light", "concrete_dark"]
    parts = [K.Part(verts, faces, sws),
             D.prism([(math.cos(math.tau * k / 6) * r_in, math.sin(math.tau * k / 6) * r_in) for k in range(6)], h - 0.1, h - 0.1, "brown_1")]
    for i in range(26):
        a, rr = rnd.random() * math.tau, math.sqrt(rnd.random()) * 0.42
        base = V((math.cos(a) * rr, math.sin(a) * rr, h - 0.11))
        tip = base + V((rnd.uniform(-0.18, 0.18), rnd.uniform(-0.18, 0.18), rnd.uniform(0.2, 0.42)))
        parts.append(D.limb(base, tip, 0.03, 0.0, rnd.choice(["grass_dead", "grass_yellow", "grass_dead"]), 3))
    parts.append(D.disc(0.035, 0.11, (0.2, -0.15, h - 0.07), (90, 0, 30), "can_label", seg=6))
    parts = D.flood(parts, 0.40, seed=45)
    return D.make("Planter", parts, "obstacle", "street", 1.5, "small", blocks=True, shrink=0.9, seed=45, ao=0.5, grime=0.25)


def bench():
    parts = []
    for x in (-0.8, 0.8):
        parts.append(D.extrude_yz([(-0.25, 0), (0.15, 0), (0.15, 0.4), (0.3, 0.85), (0.22, 0.88), (0.05, 0.45), (-0.25, 0.45)],
                                  x - 0.06, x + 0.06, "concrete", "concrete"))
    for i, y in enumerate((-0.2, -0.06, 0.08)):
        if i == 1:
            parts.append(box((1.0, 0.12, 0.04), (-0.55, y, 0.47), (0, 0, 3), swatch="wood_dark"))
            continue
        parts.append(box((1.95, 0.12, 0.04), (0, y, 0.47), swatch="wood" if i else "wood_dark"))
    for z, yy in ((0.62, 0.2), (0.78, 0.27)):
        parts.append(box((1.95, 0.04, 0.11), (0, yy, z), (-20, 0, 0), swatch="wood" if z < 0.7 else "rag"))
    parts = D.flood(parts, 0.40, seed=47)
    return D.make("Bench", parts, "obstacle", "street", 1, "small", blocks=True, shrink=0.95, seed=47, ao=0.5, grime=0.25,
                  note="concrete park bench, a seat slat missing")


def barrels():
    rnd = random.Random(49)

    def drum(loc, rot, sw):
        p = [D.disc(0.29, 0.88, (0, 0, 0.44), swatch=sw, seg=8),
             cyl(0.3, 0.03, (0, 0, 0.3), swatch="rust", segments=8, smooth=False, caps=False),
             cyl(0.3, 0.03, (0, 0, 0.6), swatch="rust", segments=8, smooth=False, caps=False),
             D.disc(0.27, 0.01, (0, 0, 0.885), swatch="metal_dark", seg=8)]
        return D.xform(p, K._mat(loc, rot))
    parts = drum((0, 0, 0), (0, 0, 10), "military_green") + drum((0.72, 0.2, 0.29), (90, 0, 70), "rust_dark")
    parts = D.flood(parts, 0.40, seed=49)
    parts.append(D.prism(D.blob_outline(16, 0.5, 0.35, seed=2, center=(1.0, -0.4)), 0, 0.004, "g05", sides=False))
    return D.make("Barrels", parts, "obstacle", "street", 1.5, "small", blocks=True, shrink=0.85, seed=49, ao=0.45, grime=0.25,
                  note="200 L drums, one tipped over leaking")


# =============================================================================================
# Small clutter
# =============================================================================================

def pallets():
    rnd = random.Random(51)
    parts = []
    for k in range(3):
        m = K._mat((rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), k * 0.145), (0, 0, rnd.uniform(-6, 6) + (8 if k == 2 else 0)))
        pal = []
        for i in range(5):
            if k == 2 and i == 3:
                pal.append(box((0.62, 0.1, 0.022), (0.28, -0.4 + i * 0.2, 0.133), (0, -6, 0), swatch="wood"))
                continue
            pal.append(box((1.2, 0.1 + 0.04 * (i % 2 == 0), 0.022), (0, -0.4 + i * 0.2, 0.133), swatch=rnd.choice(["wood", "wood", "rag", "wood_plank"])))
        for j in (-0.36, 0.0, 0.36):
            pal.append(box((0.12, 0.8, 0.1), (j * 1.45, 0, 0.061), swatch="wood_dark"))
        parts += D.xform(pal, m)
    return D.make("Pallets", parts, "obstacle", "street", 1.5, "small", blocks=True, shrink=0.95, seed=51, ao=0.5, grime=0.25)


def trash_bags():
    rnd = random.Random(53)
    parts = []
    spots = [(0, 0, 0.26, 0.34), (0.48, 0.12, 0.22, 0.3), (-0.42, 0.18, 0.21, 0.28), (0.15, 0.48, 0.2, 0.27), (0.05, 0.12, 0.5, 0.25), (-0.2, -0.42, 0.15, 0.22)]
    for i, (x, y, z, r) in enumerate(spots):
        sw = ["rubber_black", "g15", "rubber_black", "plastic_blue", "g25", "rubber_black"][i]
        parts.append(D.blob(r, (x, y, z), (rnd.uniform(-20, 20), rnd.uniform(-20, 20), rnd.uniform(0, 90)), (1.0, 0.9, 0.82), sw, 6, 4, 0.18, seed=i))
        parts.append(D.limb((x, y, z + r * 0.75), (x + rnd.uniform(-0.05, 0.05), y, z + r * 0.75 + 0.12), 0.05, 0.015, sw, 3))
    parts += [box((0.2, 0.15, 0.004), (0.6, -0.4, 0.004), (0, 0, 30), swatch="paper"),
              box((0.15, 0.2, 0.004), (-0.5, -0.6, 0.004), (0, 0, -20), swatch="cardboard"),
              D.disc(0.035, 0.11, (0.4, -0.65, 0.035), (90, 0, 60), "food_tin", seg=6)]
    return D.make("TrashBags", parts, "small", "street", 4, "small", blocks=False, seed=53, ao=0.55, grime=0.15)


def tires():
    parts = [D.torus(0.225, 0.12, (0, 0, 0.085 + 0.17 * k), (0, 0, 15 * k), "tire" if k != 1 else "rubber_black") for k in range(3)]
    D.xform(parts[1:2], K._mat((0.03, -0.02, 0)))
    parts.append(D.torus(0.225, 0.12, (0.62, 0.3, 0.31), (75, 0, 30), "tire"))
    parts.append(D.disc(0.15, 0.01, (0, 0, 0.27), swatch="blue_1", seg=8))
    return D.make("Tires", parts, "obstacle", "flood", 2, "small", blocks=True, shrink=0.8, seed=55, ao=0.6, grime=0.2,
                  note="stack of 3 tires (water inside) + one leaning")


def debris_a():
    rnd = random.Random(57)
    parts = [D.prism(D.blob_outline(16, 1.05, 0.85, seed=3), 0, 0.14, "concrete_dark", inset=0.45)]
    for i in range(10):
        a = rnd.random() * math.tau
        rr = math.sqrt(rnd.random()) * 0.8
        parts.append(D.chunk(0.15 + rnd.random() * 0.17, (math.cos(a) * rr, math.sin(a) * rr * 0.8, 0.2 + (0.8 - rr) * 0.25),
                             (rnd.uniform(0, 90), rnd.uniform(0, 90), 0), (1.2, 1.0, 0.75), rnd.choice(["concrete", "concrete_light", "rubble", "concrete_dark"]), seed=i))
    slabs = [((0.3, -0.2, 0.3), (24, -12, 30)), ((-0.45, 0.25, 0.26), (-30, 8, -20)), ((0.55, 0.45, 0.2), (12, 28, 80))]
    for (loc, rot) in slabs:
        parts.append(box((0.85, 0.55, 0.11), loc, rot, swatch="concrete"))
    for i in range(4):
        p = V((rnd.uniform(-0.5, 0.5), rnd.uniform(-0.4, 0.4), 0.25))
        parts.append(D.limb(p, p + V((rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), rnd.uniform(0.25, 0.55))), 0.012, 0.012, "rust", 4))
    return D.make("Debris_A", parts, "obstacle", "strike", 2, "medium", blocks=True, shrink=0.6, seed=57, ao=0.6, grime=0.2,
                  note="concrete rubble with rebar, collider = the core of the heap")


def debris_b():
    rnd = random.Random(59)
    parts = [D.prism(D.blob_outline(16, 1.0, 0.7, seed=5), 0, 0.1, "brown_2", "mud_wet", inset=0.4)]
    for i in range(12):
        parts.append(box((0.25, 0.12, 0.075), (rnd.uniform(-0.8, 0.8), rnd.uniform(-0.5, 0.5), 0.13 + rnd.random() * 0.12),
                         (rnd.uniform(-35, 35), rnd.uniform(-35, 35), rnd.uniform(0, 180)), swatch=rnd.choice(["brick", "brick", "red_2", "plaster"])))
    for i in range(4):
        parts.append(box((rnd.uniform(0.9, 1.5), 0.12, 0.025), (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.3, 0.3), 0.18 + 0.03 * i),
                         (rnd.uniform(-8, 8), rnd.uniform(-10, 10), rnd.uniform(0, 180)), swatch=rnd.choice(["wood_plank", "wood_dark", "wood"])))
    chair = [box((0.42, 0.4, 0.04), (0, 0, 0.45), swatch="wood_dark"), box((0.42, 0.04, 0.4), (0, 0.18, 0.67), swatch="wood_dark")]
    chair += [box((0.04, 0.04, 0.45), (x, y, 0.225), swatch="wood_dark") for x in (-0.18, 0.18) for y in (-0.18, 0.18)]
    D.xform(chair, K._mat((0.55, 0.25, 0.3), (0, 95, 30)))
    parts += chair
    parts.append(D.prism(D.blob_outline(9, 0.55, 0.4, seed=8, center=(-0.5, 0.2)), 0.1, 0.16, "tarp_blue", inset=0.2,
                         top_z=[0.16 + 0.1 * math.sin(k) for k in range(9)]))
    return D.make("Debris_B", parts, "small", "flood", 2, "medium", blocks=False, seed=59, ao=0.6, grime=0.2,
                  note="bricks, planks, a broken chair and a tarp: walkable clutter")


def suitcase():
    parts = [box((0.68, 0.46, 0.2), (0, 0, 0.1), swatch="leather_brown", taper=(0.97, 0.96)),
             box((0.69, 0.47, 0.02), (0, 0, 0.12), swatch="leather_black"),
             box((0.05, 0.48, 0.21), (-0.18, 0, 0.105), swatch="strap_black"), box((0.05, 0.48, 0.21), (0.18, 0, 0.105), swatch="strap_black"),
             box((0.16, 0.03, 0.03), (0, -0.26, 0.12), swatch="leather_black"),
             box((0.03, 0.03, 0.03), (-0.31, -0.22, 0.2), swatch="brass"), box((0.03, 0.03, 0.03), (0.31, -0.22, 0.2), swatch="brass")]
    parts += [D.disc(0.13, 0.48, (0.25, 0.48, 0.12), (0, 90, 10), "canvas_olive", seg=6),
              box((0.4, 0.03, 0.02), (0.25, 0.48, 0.25), (0, 0, 10), swatch="strap_black"),
              D.blob(0.18, (-0.42, 0.32, 0.03), scale=(1.5, 1.1, 0.18), swatch="sweater_mustard", seed=2)]
    return D.make("Suitcase", parts, "small", "evacuation", 2, "small", blocks=False, seed=61, ao=0.5, grime=0.15,
                  note="abandoned in the evacuation, with a canvas duffel and a sweater")


def stroller():
    """An old pram, EMPTY (Univers 9.6: never a child)."""
    parts = [D.prism([(-0.25, -0.36), (0.25, -0.36), (0.27, 0.36), (-0.27, 0.36)], 0.42, 0.74, "track_black", "sweater_wine", inset=-0.06)]
    hood = D.extrude_yz([(0.0, 0.0), (0.37, 0.0)] + [(0.37 * math.cos(a), 0.37 * math.sin(a)) for a in [math.radians(18 * k) for k in range(1, 6)]],
                        -0.29, 0.29, "coat_navy", "coat_navy")
    D.xform([hood], K._mat((0, 0.0, 0.74)))
    parts.append(hood)
    parts += [box((0.03, 0.7, 0.03), (s * 0.2, 0, 0.33), swatch="metal_worn") for s in (-1, 1)]
    for y, r in ((-0.3, 0.16), (0.3, 0.19)):
        parts.append(D.limb((-0.3, y, r), (0.3, y, r), 0.012, 0.012, "metal_worn", 4))
        for s in (-1, 1):
            parts.append(D.disc(r, 0.035, (s * 0.3, y, r), (0, 90, 0), "rubber_black", seg=6))
    parts += [D.limb((-0.2, 0.33, 0.5), (-0.22, 0.62, 1.0), 0.012, 0.012, "metal_worn", 4),
              D.limb((0.2, 0.33, 0.5), (0.22, 0.62, 1.0), 0.012, 0.012, "metal_worn", 4),
              D.limb((-0.26, 0.62, 1.0), (0.26, 0.62, 1.0), 0.02, 0.02, "leather_black", 5)]
    D.about(parts, (0, 0, 0), (0, 4, 12))
    return D.make("Stroller", parts, "small", "evacuation", 0.5, "small", blocks=False, seed=63, ao=0.5, grime=0.2,
                  note="EMPTY pram left in the evacuation")


# =============================================================================================
# Ground decals (no collider, a few cm, just above Z = 0)
# =============================================================================================

def decal(name, parts, cluster, weight, note="", seed=71):
    return D.make(name, parts, "decal", cluster, weight, "small", blocks=False, seed=seed, ao=0.25, grime=0.05, grime_height=0.1, note=note)


CRUST = (("brown_1", 7), ("rust_dark", 2.5), ("brown_2", 1.2))


def crust_a():
    out = D.blob_outline(14, 1.25, 0.95, seed=11, wobble=0.3)
    poly2 = [(x, y) for x, y in out]
    inside = lambda x, y: _inside(poly2, x, y)
    parts = D.plates(inside, (-1.6, -1.3, 1.6, 1.3), 0.24, z=(0.01, 0.02), gap=0.14, swatches=CRUST, seed=11)
    return decal("Decal_Crust_A", parts, "flood", 5, "dried flood mud (la Croute), cracked plates, matte, never glowing", 73)


def crust_b():
    out = D.blob_outline(16, 1.9, 0.55, seed=12, wobble=0.35)
    inside = lambda x, y: _inside(out, x, y)
    parts = D.plates(inside, (-2.3, -0.8, 2.3, 0.8), 0.22, z=(0.008, 0.018), gap=0.15, swatches=CRUST, seed=12)
    return decal("Decal_Crust_B", parts, "flood", 4, "elongated streak of Croute (a gutter, a flow line)", 75)


def _inside(poly2, x, y):
    c = False
    n = len(poly2)
    for i in range(n):
        x0, y0 = poly2[i]
        x1, y1 = poly2[(i + 1) % n]
        if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
            c = not c
    return c


def puddle(name, rx, ry, seed, weight, lobes=None):
    o = D.blob_outline(28, rx, ry, seed=seed, wobble=0.4)
    o_rim = D.blob_outline(28, rx * 1.12, ry * 1.14, seed=seed, wobble=0.45)
    parts = [D.prism(o_rim, 0, 0.004, "brown_1", sides=False), D.prism(o, 0.004, 0.008, "blue_1", sides=False)]
    for (cx, cy, r) in (lobes or []):
        o2 = D.blob_outline(20, r, r * 0.8, seed=seed + 7, wobble=0.35, center=(cx, cy))
        o2r = D.blob_outline(20, r * 1.15, r * 0.95, seed=seed + 7, wobble=0.4, center=(cx, cy))
        parts += [D.prism(o2r, 0, 0.0035, "brown_1", sides=False), D.prism(o2, 0.0035, 0.0075, "blue_1", sides=False)]
    return decal(name, parts, "street", weight, "dark water + wet mud rim: give it a glossy material", seed)


def manhole():
    parts = [ring(0.36, 0.46, 0.0, 0.03, "concrete_dark", seg=12, side="concrete_dark"),
             D.disc(0.36, 0.024, (0, 0, 0.012), swatch="metal_gun", seg=12),
             box((0.06, 0.02, 0.01), (0.3, 0, 0.029), swatch="g05"),
             D.prism(D.blob_outline(18, 0.62, 0.52, seed=3, center=(0.06, 0.04)), 0, 0.002, "brown_1", sides=False),
             D.prism([(0.42 * math.cos(a), 0.42 * math.sin(a)) for a in [math.radians(200 + 18 * k) for k in range(8)]] +
                     [(0.62 * math.cos(a), 0.62 * math.sin(a)) for a in [math.radians(326 - 18 * k) for k in range(8)]], 0.002, 0.012, "brown_2", inset=0.0)]
    for k in range(-2, 3):  # cast bars across the cover (no ring, no cross: nothing that looks like a target)
        yb = k * 0.12
        parts.append(box((2 * math.sqrt(0.33 ** 2 - yb ** 2) * 0.9, 0.04, 0.008), (0, yb, 0.028), swatch="metal_dark"))
    return decal("Decal_Manhole", parts, "street", 2, "cast-iron cover, rust stain, mud seeping out on one side", 77)


def road_line():
    rnd = random.Random(79)
    parts = []
    y = -1.5
    while y < 1.5:
        L = rnd.uniform(0.2, 0.7)
        y1 = min(1.5, y + L)
        w0, w1 = 0.065 + rnd.uniform(-0.01, 0.01), 0.065 + rnd.uniform(-0.01, 0.01)
        pts = [(-w0, y), (w0, y), (w1, y1), (rnd.uniform(-0.02, 0.02), y1 + 0.02), (-w1, y1)]
        parts.append(D.prism(pts, 0, 0.004, rnd.choices(["g45", "g55", "g35"], [4, 1.5, 2])[0], sides=False))
        y = y1 + rnd.uniform(0.03, 0.18)
    return decal("Decal_RoadLine", parts, "street", 3, "one worn 3 m dash along Z (forward), place every 6 m", 79)


def zebra():
    rnd = random.Random(81)
    parts = []
    for k in range(5):
        x = -2.0 + k * 1.0
        y = -1.5
        while y < 1.5:
            y1 = min(1.5, y + rnd.uniform(0.3, 1.0))
            if rnd.random() > 0.18:
                pts = [(x - 0.25 + rnd.uniform(0, 0.03), y), (x + 0.25 - rnd.uniform(0, 0.03), y), (x + 0.25, y1), (x - 0.25, y1)]
                parts.append(D.prism(pts, 0, 0.004, rnd.choices(["g45", "g55", "g35"], [4, 1, 3])[0], sides=False))
            y = y1 + rnd.uniform(0.05, 0.25)
    return decal("Decal_Zebra", parts, "street", 0.8, "worn pedestrian crossing, 5 stripes", 81)


def papers():
    rnd = random.Random(83)
    parts = []
    for i in range(18):
        a = rnd.random() * math.tau
        r = (rnd.random() ** 0.7) * 1.8
        x, y = math.cos(a) * r, math.sin(a) * r * 0.8
        z = 0.003 + 0.0015 * (i % 6)
        m = K._mat((x, y, z), (0, 0, rnd.uniform(0, 360)))
        body = rnd.choices(["paper", "g65", "plaster", "g55"], [1.5, 3, 3, 2])[0]
        tract = [rect_z(-0.075, -0.105, 0.075, 0.055, 0, body), rect_z(-0.075, 0.055, 0.075, 0.105, 0, "red_3")]
        if i % 5 == 0:
            D.about(tract[1:], (0, 0.055, 0), (25, 0, 0))
        parts += D.xform(tract, m)
    for i in range(3):
        m = K._mat((rnd.uniform(-1.2, 1.2), rnd.uniform(-1, 1), 0.012 + 0.002 * i), (0, 0, rnd.uniform(0, 360)))
        parts += D.xform([rect_z(-0.2, -0.15, 0.2, 0.15, 0, "g65")], m)
    return decal("Decal_Papers", parts, "evacuation", 3, "tracts of the Cordon (white, red header) + soaked newspaper", 83)


def resurgence():
    rnd = random.Random(85)
    core = D.blob_outline(14, 0.42, 0.34, seed=5, wobble=0.25)
    parts = [D.prism(D.blob_outline(24, 1.15, 1.0, seed=6, wobble=0.35), 0, 0.004, "brown_1", sides=False),
             D.prism(core, 0.0, 0.014, "amber_glow_soft"),
             D.prism([(x * 0.38 + 0.05, y * 0.38) for x, y in core], 0.014, 0.017, "amber_glow")]

    def ring_in(x, y):
        rr = math.hypot(x / 1.0, y / 0.85)
        return 0.5 < rr < 0.98 and not _inside(core, x / 1.08, y / 1.08)
    parts += D.plates(ring_in, (-1.0, -0.9, 1.0, 0.9), 0.23, z=(0.012, 0.028), gap=0.13,
                      swatches=(("crust_black", 4), ("crust_dark", 3), ("rust_dark", 2), ("crust", 0.8)), seed=85)
    for i in range(4):
        a = rnd.random() * math.tau
        parts.append(D.blob(0.04 + rnd.random() * 0.03, (math.cos(a) * 0.2, math.sin(a) * 0.15, 0.014), scale=(1, 1, 0.6), swatch="amber", seg=5, rings=3, seed=i))
    return decal("Resurgence_Small", parts, "flood", 0.3, "RARE: the Seve welling up, emissive amber center (palette alpha)", 85)


def crater():
    rnd = random.Random(87)
    star = []
    for k in range(20):
        a = math.tau * k / 20
        r = (1.55 if k % 2 == 0 else 0.95) * (0.8 + rnd.random() * 0.4)
        star.append((math.cos(a) * r, math.sin(a) * r))
    parts = [D.prism(star, 0, 0.003, "g15", sides=False), D.prism(D.blob_outline(18, 0.8, 0.72, seed=3, wobble=0.35), 0.003, 0.007, "g05", sides=False)]
    for i in range(10):
        a = math.tau * i / 10 + rnd.uniform(-0.2, 0.2)
        r = 0.85 + rnd.random() * 0.4
        parts.append(box((0.32, 0.26, 0.06), (math.cos(a) * r, math.sin(a) * r, 0.08), (0, -rnd.uniform(15, 30), math.degrees(a)),
                         swatch=rnd.choice(["asphalt", "asphalt_light", "g25"])))
    parts += [D.chunk(0.08 + rnd.random() * 0.06, (rnd.uniform(-1.3, 1.3), rnd.uniform(-1.3, 1.3), 0.07), scale=(1, 1, 0.6), swatch="rubble", seed=i) for i in range(4)]
    return decal("Decal_Crater", parts, "strike", 0.8, "Cordon strike: scorched star + lifted asphalt chunks (<= 12 cm)", 87)


BUILDERS = [cars, jersey, sandbags, checkpoint, lamp, bus_shelter, kiosk, wall_ruin_a, wall_ruin_b, container_regen, fence_chain,
            sign_road, tree_dead, planter, bench, barrels, pallets, trash_bags, tires, debris_a, debris_b, suitcase, stroller,
            crust_a, crust_b, lambda: puddle("Decal_Puddle_A", 0.9, 0.6, 91, 6),
            lambda: puddle("Decal_Puddle_B", 1.3, 0.85, 93, 4, lobes=[(1.5, 0.6, 0.5)]),
            manhole, road_line, zebra, papers, resurgence, crater]


def build_all():
    D.REG.clear()
    for b in BUILDERS:
        b()
    return [o for o, _ in D.REG]


GROUPS = {
    "decor_vehicles": ["Car_A", "Car_B", "Car_C", "Car_Burnt"],
    "decor_cordon": ["Barrier_Jersey", "Sandbags", "Barrier_Checkpoint", "Container_Regen", "Fence_Chain", "Debris_A"],
    "decor_street": ["Lamp", "BusShelter", "Kiosk", "Wall_Ruin_A", "Wall_Ruin_B", "Sign_Road", "Tree_Dead", "Planter", "Bench"],
    "decor_small": ["Barrels", "Pallets", "TrashBags", "Tires", "Debris_B", "Suitcase", "Stroller"],
    "decor_decals": ["Decal_Crust_A", "Decal_Crust_B", "Decal_Puddle_A", "Decal_Puddle_B", "Decal_Manhole", "Decal_RoadLine",
                     "Decal_Zebra", "Decal_Papers", "Resurgence_Small", "Decal_Crater"],
}

if __name__ == "__main__":
    a = D.args()
    K.reset()
    objs = build_all()
    total = sum(m["tris"] for _, m in D.REG)
    K.report(asset="Decor_Ostrov", objects=len(objs), tris=total)
    if "only" not in a:
        path = K.export_fbx(objs, "Props/Decor_Ostrov", animated=False)
        K.report(fbx=path)
        D.write_manifest(MANIFEST, D.manifest_entries())
        K.report(manifest=MANIFEST)
    if a.get("nosheet") != "1":
        only = a.get("only")
        names = {o.name: o for o in objs}
        for g, lst in GROUPS.items():
            sel = [names[n] for n in lst if n in names and (not only or n in only.split(","))]
            if sel:
                p = D.sheet(g, sel, views=((200, 60), (30, 35)), tile=340, cols=min(3, len(sel)))
                K.report(preview=p)
