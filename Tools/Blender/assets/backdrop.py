"""Far backdrop of the outdoor maps: Props/Backdrop.fbx.

- SixTowers: the six white bioreactors of REGEN on the Combinat (Univers 2.6), 60 m, cyan band, catwalk rings,
  pipes between them, a low plant building in front. Very low detail: it lives far away in the fog.
- Skyline: a 260 m strip of distant Ostrov silhouettes: panel blocks with window bands, point towers, a sawtooth
  steel hall, a cooling tower and two striped chimneys of the thermal plant, a water tower, a mast.

Both objects: origin = bottom center, the side meant to face the game camera is +Y (Unity -Z): place them far
along Unity +Z (screen up) with rotation 0. Total <= 3000 tris.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _decor_lib as D  # noqa: E402

K = D.K
V = D.V


def dome(R, h, loc, sw, seg=12):
    """Low dome on a ring of radius R at loc (2 rings + apex, 36 tris)."""
    x0, y0, z0 = loc
    verts, faces = [], []
    rings = [(R, 0.0), (R * 0.72, h * 0.7)]
    for r, z in rings:
        for i in range(seg):
            a = math.tau * i / seg
            verts.append((x0 + math.cos(a) * r, y0 + math.sin(a) * r, z0 + z))
    verts.append((x0, y0, z0 + h))
    for k in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            faces.append([k * seg + i, k * seg + j, (k + 1) * seg + j, (k + 1) * seg + i])
    top = len(verts) - 1
    base = (len(rings) - 1) * seg
    for i in range(seg):
        faces.append([base + i, base + (i + 1) % seg, top])
    return K.Part(verts, faces, sw)


def annulus(r_in, r_out, z, h, loc, sw, seg=12):
    verts, faces = [], []
    for i in range(seg):
        a = math.tau * i / seg
        c, s = math.cos(a), math.sin(a)
        verts += [(loc[0] + c * r_in, loc[1] + s * r_in, z + h), (loc[0] + c * r_out, loc[1] + s * r_out, z + h),
                  (loc[0] + c * r_out, loc[1] + s * r_out, z)]
    for i in range(seg):
        a, b = i * 3, ((i + 1) % seg) * 3
        faces += [[a, a + 1, b + 1, b], [a + 1, a + 2, b + 2, b + 1]]
    return K.Part(verts, faces, sw)


def six_towers():
    parts = []
    pos = [(-30, 9), (-6, 11), (18, 9), (-18, -11), (6, -9), (30, -11)]
    R, H = 6.5, 52.0
    for i, (x, y) in enumerate(pos):
        body = K.cyl(R, H - 4, (x, y, 4 + (H - 4) / 2), swatch="regen_white", segments=12, smooth=False, caps=False)
        parts += D.band([body], 37.5, 41.0, "cyan_paint")
        parts += [dome(R, 8.0, (x, y, H), "regen_white"),
                  K.cyl(R + 0.8, 4.0, (x, y, 2.0), swatch="regen_grey", segments=12, smooth=False),
                  K.cyl(R + 0.06, 0.6, (x, y, H - 1.6), swatch="cyan", segments=12, smooth=False, caps=False),
                  annulus(R, R + 1.1, 41.2, 0.4, (x, y), "regen_plate"),
                  D.limb((x + R + 0.45, y + 0.5, 4.0), (x + R + 0.45, y + 0.5, H - 2.0), 0.55, 0.55, "regen_plate", 6)]
    links = [(0, 1, 24), (1, 2, 30), (3, 4, 22), (4, 5, 28), (1, 4, 18), (0, 3, 26), (2, 5, 20)]
    for a, b, z in links:
        (xa, ya), (xb, yb) = pos[a], pos[b]
        parts.append(D.limb((xa, ya, z), (xb, yb, z), 0.9, 0.9, "regen_grey", 6))
    plant = D.prism([(-36, 18), (36, 18), (36, 28), (-36, 28)], 0, 8, "regen_grey", "regen_white")
    parts += D.band([plant], 6.0, 7.0, "cyan_paint")
    parts += [D.prism([(-20, 20), (-8, 20), (-8, 26), (-20, 26)], 8, 11, "regen_grey", "regen_white"),
              D.prism([(14, 19), (24, 19), (24, 27), (14, 27)], 8, 10, "regen_grey", "regen_white")]
    return D.make("SixTowers", parts, "backdrop", "backdrop", 1, "backdrop", blocks=False, seed=101, ao=0.0, grime=0.08,
                  grime_height=6.0, note="six REGEN bioreactors, 60 m, footprint ~73 x 46 m; place 150-300 m away in the fog")


def skyline():
    rnd = random.Random(103)
    parts = []
    x = -130.0
    k = 0
    while x < 130:
        kind = rnd.choices(["slab", "point", "low"], [5, 2, 2])[0]
        if kind == "slab":
            w, d, h = rnd.uniform(30, 60), rnd.uniform(11, 14), rnd.uniform(24, 42)
        elif kind == "point":
            w, d, h = rnd.uniform(16, 20), rnd.uniform(16, 20), rnd.uniform(36, 48)
        else:
            w, d, h = rnd.uniform(20, 40), rnd.uniform(14, 24), rnd.uniform(8, 14)
        y = rnd.uniform(-8, 8)
        sw = rnd.choice(["concrete", "concrete_light", "plaster", "concrete_dark", "plaster_blue"])
        x0, x1 = x, x + w
        parts.append(D.prism([(x0, y - d / 2), (x1, y - d / 2), (x1, y + d / 2), (x0, y + d / 2)], 0, h, "concrete_dark", sw))
        if kind != "low":
            # roof machine room
            cx = rnd.uniform(x0 + 4, x1 - 8)
            parts.append(D.prism([(cx, y - 2), (cx + 5, y - 2), (cx + 5, y + 2), (cx, y + 2)], h, h + 3, "concrete_dark", "concrete"))
            # window bands on the camera side (+Y), every 2.8 m: the panel block read
            yf = y + d / 2 + 0.05
            fl = 2.8
            z = 3.4
            while z < h - 1.5:
                parts.append(D.poly([(x1 - 1.5, yf, z), (x0 + 1.5, yf, z), (x0 + 1.5, yf, z + 1.2), (x1 - 1.5, yf, z + 1.2)], "concrete_dark" if sw != "concrete_dark" else "g15"))
                z += fl * (2 if kind == "point" else 1)
        x = x1 + rnd.uniform(2, 14)
        k += 1
    # the Combinat: sawtooth steel hall, cooling tower, two striped chimneys, water tower, a mast
    prof = [(-27, 0), (27, 0), (27, 12)]
    for i in range(6):  # sawtooth roof: vertical glazing, then the slope down
        xr = 27 - i * 9
        prof += [(xr, 17), (xr - 9, 12)]
    hall = D.extrude_xz([(px + 40, pz) for px, pz in prof], -10, 10, "metal_dark", "rust_dark", back="metal_dark")
    parts.append(hall)
    seg = 12
    rings = [(14.0, 0.0), (9.5, 22.0), (10.5, 30.0)]
    verts, faces = [], []
    for r, z in rings:
        for i in range(seg):
            a = math.tau * i / seg
            verts.append((-60 + math.cos(a) * r, -4 + math.sin(a) * r, z))
    for kk in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            faces.append([kk * seg + i, kk * seg + j, (kk + 1) * seg + j, (kk + 1) * seg + i])
    parts.append(K.Part(verts, faces, "concrete_light"))
    for cx, h in ((70, 70), (82, 60)):
        ch = D.limb((cx, 2, 0), (cx, 2, h), 3.0, 2.0, "g75", 8)
        parts += D.band(D.band([ch], h - 12, h - 6, "red_3"), h - 24, h - 18, "red_3")
    parts += [D.limb((-95, 4, 0), (-95, 4, 26), 1.0, 1.0, "concrete", 6),
              K.cyl(6.0, 7.0, (-95, 4, 29.5), swatch="concrete_light", segments=10, smooth=False),
              K.cyl(6.0, 3.0, (-95, 4, 34.5), swatch="concrete", segments=10, smooth=False, radius2=0.5),
              D.limb((105, -3, 0), (105, -3, 75), 1.2, 0.3, "metal_worn", 4),
              D.limb((101, -3, 40), (109, -3, 40), 0.25, 0.25, "metal_worn", 4)]
    return D.make("Skyline", parts, "backdrop", "backdrop", 1, "backdrop", blocks=False, seed=105, ao=0.0, grime=0.08,
                  grime_height=6.0, note="260 m strip of silhouettes; detailed side +Y")


def build_all():
    D.REG.clear()
    six_towers()
    skyline()
    return [o for o, _ in D.REG]


if __name__ == "__main__":
    K.reset()
    objs = build_all()
    total = sum(m["tris"] for _, m in D.REG)
    K.report(asset="Backdrop", tris=total, budget="3000" + ("" if total <= 3000 else " OVER"))
    path = K.export_fbx(objs, "Props/Backdrop", animated=False)
    K.report(fbx=path)
    # where the backdrop entries go in the manifest (merged by decor_ostrov's manifest + these)
    import json
    man = json.load(open(os.path.join(D.HERE, "decor_manifest.json"), encoding="utf-8")) if os.path.exists(os.path.join(D.HERE, "decor_manifest.json")) else None
    if man is not None:
        names = {e["name"] for e in D.manifest_entries()}
        man["objects"] = [e for e in man["objects"] if e["name"] not in names] + D.manifest_entries()
        json.dump(man, open(os.path.join(D.HERE, "decor_manifest.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    # preview: from the playfield side, low, with the Skyline in front of the towers
    sun = D.setup_render(1200, 600, sky=(0.55, 0.6, 0.66), sky_strength=1.0, sun_energy=2.5)
    sk = [o for o in objs if o.name == "Skyline"][0]
    sk.location = (0, 120, 0)
    # (no world volume fog here: in EEVEE headless it blacks out the frame)
    g = D.ground_plane(1200, name="bd_ground")
    p = D.render_camera("backdrop", (0, 420, 18), (0, 0, 28), 1200, 600, fov=24, clip_end=2000)
    K.report(preview=p)
