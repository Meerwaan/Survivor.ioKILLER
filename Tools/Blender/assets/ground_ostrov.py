"""Ground of Ostrov (chapter 1-2 streets and car parks): Assets/_Game/Art/Textures/T_Ground_Ostrov.png (+ _B).

2048 x 2048, TILEABLE, one tile = 8 m (256 px per meter). Painted with numpy, deterministic.
RGB (sRGB) = albedo, DARK and low contrast (Univers 9.3): mean around 0.19.
A = SMOOTHNESS (data, never transparency): puddles ~0.94, wet asphalt ~0.5-0.75, concrete ~0.4, dry Croute ~0.12.

What is on it (Univers 9.4-9.6): old concrete road slabs (4 x 2 m, running bond) patched with cracked wet asphalt,
dark tar joints, spalled corners, alligator cracks, oil stains, mud smears, faded parking paint, and the Croute:
the dried amber flood mud, MATTE, in the low cracks and in mud-cracked patches around the puddles (never glowing).
Variant B (saison de Crue / low streets): more asphalt, more Croute, more puddles, other seed.

Everything is periodic by construction (FFT noise, toroidal Voronoi, slab grid dividing the tile).
"""
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _decor_lib as D  # noqa: E402

K = D.K
bpy = D.bpy

N = 2048
TILE = 8.0
PXM = N / TILE


def col(name, mul=1.0):
    return np.array(K._hex(K.PALETTE[K.sw(name)][1]), np.float32) * mul


def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a + (b - a) * t


class Painter:
    def __init__(self, seed):
        self.rng = np.random.default_rng(seed)
        kx = np.fft.rfftfreq(N, d=1.0 / N).astype(np.float32)
        ky = np.fft.fftfreq(N, d=1.0 / N).astype(np.float32)
        KX, KY = np.meshgrid(kx, ky)
        self.kr = np.sqrt(KX * KX + KY * KY)
        yy, xx = np.mgrid[0:N, 0:N]
        self.x = xx.astype(np.float32)
        self.y = yy.astype(np.float32)

    def noise(self, fmin, fmax, beta=1.0):
        """Periodic fractal noise, features between fmin and fmax cycles per tile (1 cycle = 8 m). ~N(0, 1)."""
        F = np.fft.rfft2(self.rng.standard_normal((N, N)).astype(np.float32))
        kr = self.kr
        amp = np.maximum(kr, 0.5) ** (-beta) * np.exp(-(kr / fmax) ** 2) * (1 - np.exp(-(kr / fmin) ** 2))
        out = np.fft.irfft2(F * amp, s=(N, N)).astype(np.float32)
        out -= out.mean()
        out /= out.std() + 1e-9
        return out

    def white(self):
        return self.rng.random((N, N)).astype(np.float32)

    def worley(self, G, warp=None):
        """Toroidal Voronoi with G x G jittered cells: (edge distance in px, cell id)."""
        pts = self.rng.random((G, G, 2)).astype(np.float32)
        s = G / N
        X = (self.x + 0.5) * s
        Y = (self.y + 0.5) * s
        if warp is not None:
            X = X + warp[0]
            Y = Y + warp[1]
        ix = np.floor(X).astype(np.int32)
        iy = np.floor(Y).astype(np.int32)
        fx = X - ix
        fy = Y - iy
        f1 = np.full((N, N), 9.0, np.float32)
        f2 = f1.copy()
        cid = np.zeros((N, N), np.int32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                cx = (ix + dx) % G
                cy = (iy + dy) % G
                px = pts[cy, cx, 0] + dx - fx
                py = pts[cy, cx, 1] + dy - fy
                d = px * px + py * py
                closer = d < f1
                f2 = np.where(closer, f1, np.minimum(f2, d))
                cid = np.where(closer, cy * G + cx, cid)
                f1 = np.where(closer, d, f1)
        edge = (np.sqrt(f2) - np.sqrt(f1)) * 0.5 / s
        return edge, cid

    def warp(self, fmin, fmax, amount_cells):
        return (self.noise(fmin, fmax) * amount_cells, self.noise(fmin, fmax) * amount_cells)


def paint(seed=11, asphalt_cov=0.55, crust_cov=0.08, puddle_cov=0.055, paint_marks=True, target_mean=0.19):
    t0 = time.time()
    P = Painter(seed)
    x, y = P.x, P.y
    rng = P.rng

    # ---------------- height field: where water and mud collect ----------------
    H = P.noise(1.0, 7, 1.3) + 0.3 * P.noise(7, 30, 1.0)
    H = (H - H.mean()) / H.std()

    # ---------------- concrete slabs, 4 x 2 m running bond (divides the 8 m tile) ----------------
    row = (y // 512).astype(np.int32)
    ly = y - row * 512
    off = (row % 2) * 512
    xo = x + off
    xs = xo % 1024
    sid = row * 2 + ((xo // 1024).astype(np.int32) % 2)
    wob = P.noise(16, 90) * 1.3
    d_edge = np.minimum(np.minimum(ly, 512 - ly), np.minimum(xs, 1024 - xs)) + wob
    joint = sstep(6.0, 2.5, d_edge)
    n_slab = 8
    slab_tone = rng.normal(0.0, 0.035, n_slab).astype(np.float32)[sid]
    slab_cracked = (rng.random(n_slab) < 0.6).astype(np.float32)[sid]
    gx = rng.normal(0, 0.03, n_slab).astype(np.float32)[sid]
    slab_grad = gx * (xs / 1024.0 - 0.5) + rng.normal(0, 0.03, n_slab).astype(np.float32)[sid] * (ly / 512.0 - 0.5)
    dcx = np.minimum(xs, 1024 - xs)
    dcy = np.minimum(ly, 512 - ly)
    dcorner = np.sqrt(dcx * dcx + dcy * dcy)
    sp_r = 26 + 16 * P.noise(6, 30)
    spall = sstep(sp_r + 3, sp_r - 3, dcorner) * (P.noise(3, 12) > -0.3)
    # spalls along the joints too (broken slab edges)
    edge_spall = sstep(10, 5, d_edge - 6 * P.noise(10, 60)) * sstep(0.9, 1.4, P.noise(4, 20))
    spall = np.maximum(spall, edge_spall)

    blotch = P.noise(3, 25)
    fine = P.noise(150, 900, 0.3)
    conc = mix(col("concrete_dark"), col("concrete"), np.clip(0.22 + 0.12 * blotch, 0, 1)) * 0.8
    conc = conc * (1 + slab_tone + slab_grad)[..., None] * (1 + 0.035 * fine)[..., None]
    pits = (P.white() > 0.996).astype(np.float32)
    conc = conc * (1 - 0.3 * pits)[..., None]

    W, _ = P.worley(5, P.warp(4, 16, 0.22))
    crack_c = sstep(3.6, 1.2, W) * sstep(-0.1, 0.4, P.noise(2, 10)) * slab_cracked

    # ---------------- asphalt patches over the slabs ----------------
    slab_bias = rng.choice(np.array([-0.7, 0.0, 0.0, 0.7], np.float32), n_slab)[sid]
    a = P.noise(1.0, 5, 1.0) + 0.3 * P.noise(5, 30, 0.8) + slab_bias
    sub = a[::8, ::8]
    thr = float(np.quantile(sub, 1 - asphalt_cov))
    asph = sstep(thr - 0.03, thr + 0.03, a)
    # raveled patch borders: holes where the binder is gone
    rim = 1 - np.abs(asph * 2 - 1)
    asph = asph * (1 - rim * (P.white() > 0.55))
    worn = sstep(0.4, 1.8, P.noise(3, 20)) * 0.35
    asp = mix(col("asphalt"), col("asphalt_light"), worn)
    agg = P.noise(250, 1024, 0.0)
    stones = (P.white() > 0.994).astype(np.float32)
    asp = asp * (1 + 0.05 * agg + 0.12 * stones)[..., None]

    W1, _ = P.worley(3, P.warp(3, 12, 0.35))
    crack_long = sstep(2.8, 0.9, W1) * sstep(-0.3, 0.2, P.noise(2, 8))
    W2, _ = P.worley(24, P.warp(8, 40, 0.18))
    fatigue = sstep(0.9, 1.5, P.noise(1.5, 8))
    crack_al = sstep(2.0, 0.6, W2) * fatigue
    crack_a = np.maximum(crack_long, crack_al)

    # ---------------- masks of the surface ----------------
    concrete_w = 1 - asph
    crack = np.maximum(crack_c * concrete_w, crack_a * asph)
    crack = np.maximum(crack, joint * concrete_w * 0.85)
    spall = spall * concrete_w

    Hp = H + 0.12 * P.noise(12, 70)
    tp = float(np.quantile(Hp[::8, ::8], puddle_cov))
    puddle = sstep(tp + 0.02, tp - 0.02, Hp)
    wet = sstep(tp + 0.9, tp, Hp)

    Hc = H + 0.28 * P.noise(8, 50)
    tc = float(np.quantile(Hc[::8, ::8], crust_cov + puddle_cov))
    crust_patch = sstep(tc + 0.08, tc - 0.08, Hc) * (1 - sstep(tp + 0.1, tp - 0.05, Hp))
    low = sstep(0.4, -0.5, H)
    crack_wide = np.maximum(sstep(3.5, 1.2, W) * concrete_w * slab_cracked, sstep(2.6, 0.8, W1) * asph)
    crack_wide = np.maximum(crack_wide, sstep(5.0, 2.0, d_edge) * concrete_w)
    crust_crack = crack_wide * low * sstep(-0.6, 0.2, P.noise(3, 20))
    crust_crack = np.maximum(crust_crack, spall * low)

    # mud cracks inside the Croute patches (13 cm plates), curled lighter rims
    Wm, cm = P.worley(60, P.warp(10, 60, 0.15))
    plate_tone = (rng.normal(0, 0.06, 60 * 60).astype(np.float32))[cm]
    gap = sstep(2.2, 0.6, Wm)
    rimc = sstep(6.0, 2.0, Wm) - gap

    crust_col = mix(col("mud_dry"), col("crust_dark"), 0.3) * 0.5
    crust_col = crust_col * 0.75 + crust_col.mean() * 0.25  # matte, a little desaturated
    crust_tex = crust_col * (1 + plate_tone + 0.07 * rimc)[..., None]
    crust_tex = mix(crust_tex, crust_col * 0.6, gap)
    crust_amt = np.clip(crust_patch + crust_crack * 0.9, 0, 1)

    mud = sstep(0.7, 1.8, P.noise(2, 16)) * 0.55
    mud_col = col("mud_wet", 0.5)

    oil = np.zeros((N, N), np.float32)
    on = P.noise(3, 24)
    for _ in range(5):
        cx, cy = rng.random() * N, rng.random() * N
        r = (0.28 + rng.random() * 0.3) * PXM
        dx = ((x - cx + N / 2) % N) - N / 2
        dy = ((y - cy + N / 2) % N) - N / 2
        dd = np.sqrt(dx * dx * (0.6 + rng.random() * 0.8) + dy * dy) / r + 0.3 * on
        oil = np.maximum(oil, sstep(1.0, 0.65, dd) * (0.6 + 0.4 * rng.random()))

    paint_amt = np.zeros((N, N), np.float32)
    paint_col = np.zeros((N, N, 3), np.float32)
    if paint_marks:
        keep = sstep(-0.2, 0.6, P.noise(6, 70)) * (P.white() > 0.3)
        yel = sstep(17, 14, np.abs(x - 3.1 * PXM)) * sstep(0.5 * PXM, 0.7 * PXM, y) * sstep(4.3 * PXM, 4.1 * PXM, y)
        wh = np.zeros((N, N), np.float32)
        for d0 in (0.5, 2.5, 4.5):
            wh = np.maximum(wh, sstep(15, 12, np.abs(y - 6.3 * PXM)) * sstep(d0 * PXM, d0 * PXM + 8, x) * sstep((d0 + 1.0) * PXM, (d0 + 1.0) * PXM - 8, x))
        paint_amt = np.maximum(yel, wh) * keep * 0.5
        paint_col = mix(np.broadcast_to(col("paint_white_road"), (N, N, 3)), np.broadcast_to(col("paint_yellow_road"), (N, N, 3)), (yel > wh).astype(np.float32))

    # ---------------- color ----------------
    c = mix(conc, asp, asph)
    c = c * (1 - 0.55 * crack)[..., None]
    c = mix(c, col("mud_wet", 0.42), spall * 0.8)
    c = mix(c, mud_col, mud)
    c = mix(c, paint_col * 0.85, paint_amt)
    c = c * (1 - 0.48 * oil)[..., None]
    c = mix(c, crust_tex, crust_amt)
    c = c * (1 - 0.22 * wet * (1 - crust_amt))[..., None]
    puddle_col = np.array((0.06, 0.07, 0.082), np.float32)
    c = mix(c, puddle_col + c * 0.3, puddle)
    c = c * (1 + 0.025 * fine)[..., None]

    # ---------------- smoothness ----------------
    s = 0.40 * concrete_w + 0.50 * asph
    s = s + (0.80 - s) * wet * 0.55
    s = s + (0.26 - s) * crack
    s = s + (0.34 - s) * mud
    s = s + (0.36 - s) * paint_amt
    s = s + (0.84 - s) * oil
    s = s + ((0.11 + 0.08 * gap) - s) * crust_amt
    s = s + (0.94 - s) * puddle
    s = np.clip(s + 0.04 * P.noise(20, 200), 0.03, 0.97)

    mean = float(c.mean())
    gain = float(np.clip(target_mean / mean, 0.85, 1.2))
    c = np.clip(c * gain, 0, 1)
    stats = dict(mean_before=round(mean, 4), gain=round(gain, 3), mean=round(float(c.mean()), 4),
                 mean_rgb=[round(float(v), 4) for v in c.reshape(-1, 3).mean(0)],
                 smooth_mean=round(float(s.mean()), 3), puddle=round(float((puddle > 0.5).mean()), 3),
                 crust=round(float((crust_amt > 0.5).mean()), 3), asphalt=round(float((asph > 0.5).mean()), 3),
                 secs=round(time.time() - t0, 1))
    rgba = np.concatenate([c, s[..., None]], axis=2).astype(np.float32)
    return rgba, stats


def save_tex(rgba, name):
    path = os.path.join(K.ART, "Textures", name + ".png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = bpy.data.images.get(name) or bpy.data.images.new(name, N, N, alpha=True)
    img.colorspace_settings.name = "sRGB"  # BEFORE the pixels (changing it regenerates the buffer)
    img.alpha_mode = "CHANNEL_PACKED"  # alpha = smoothness data, never premultiplied
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return path


def preview_tiles(rgba_list, name):
    """2 x 2 tiles of each variant (seams would show as lines) + the smoothness (alpha) as grey."""
    cols = []
    for rgba in rgba_list:
        small = rgba[::4, ::4]
        t = np.concatenate([np.concatenate([small, small], 1)] * 2, 0)
        rgb = t.copy()
        rgb[..., 3] = 1
        al = np.repeat(t[..., 3:4], 4, axis=2)
        al[..., 3] = 1
        cols.append(np.concatenate([rgb, al], axis=1))
    D.save_array(np.concatenate(cols[::-1], axis=0), os.path.join(K.PREVIEWS, name + ".png"))


if __name__ == "__main__":
    K.reset()
    A, sa = paint(seed=11)
    pa = save_tex(A, "T_Ground_Ostrov")
    K.report(asset="T_Ground_Ostrov", path=pa, **{k: str(v).replace(" ", "") for k, v in sa.items()})
    B, sb = paint(seed=29, asphalt_cov=0.72, crust_cov=0.15, puddle_cov=0.085, paint_marks=False, target_mean=0.18)
    pb = save_tex(B, "T_Ground_Ostrov_B")
    K.report(asset="T_Ground_Ostrov_B", path=pb, **{k: str(v).replace(" ", "") for k, v in sb.items()})
    preview_tiles([A, B], "ground_ostrov_tiles")
