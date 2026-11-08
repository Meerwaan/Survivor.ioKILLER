"""Le Passeur, the hero (Univers 4.5, 6.1, 6.7, 9.8): a smuggler from camp Relais 17, and his 12 skins.

One rig, 12 mesh variants (names are the Unity contract): Passeur (Survivant, default), Passeur_Camo,
Passeur_Sherif, Passeur_Punk, Passeur_Medic, Passeur_King, Passeur_Viking, Passeur_Hazmat, Passeur_Angel,
Passeur_Demon, Passeur_Neon (lore: "Greffe"), Passeur_Founder.
Every skin keeps THE INJECTOR on the left forearm (REGEN field device: white/grey casing, straps, one cyan diode,
an amber vial = the diegetic level gauge) and the police service revolver in the right hand (+15 %).

Extra bones: Vial (child of LowerArm.L, the amber liquid, scale its local Y 0..1 = empty..full), Muzzle (child of
Hand.R, at the barrel tip, +Y along the barrel), Cape (Chest, the King's cape), Lantern (Chest, the Demon's
lantern), Halo (Head, the Angel's halo wire), Tail (Hips, the Demon's costume tail).
Clips: Idle, Run (natural speed 5.2 m/s), Hit, Inject, Death.

Env switches (optional): PASSEUR_STAGE = comma list of rest | anim | lineup | gauge | export | skins | check |
closeups | none (default "all" = anim + lineup + gauge + export); PASSEUR_ONLY = variant names to build (quick
iteration; Passeur is always built first, do not export a partial build); PASSEUR_DEBUG=1 prints per-part tris.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
sys.path.insert(0, HERE)
import cordon_kit as K  # noqa: E402
import _passeur_lib as L  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402
from mathutils.noise import noise as perlin  # noqa: E402

V = Vector
STAGE = os.environ.get("PASSEUR_STAGE", "all")
STAGES = set(STAGE.split(","))
ONLY = [s for s in os.environ.get("PASSEUR_ONLY", "").split(",") if s]

K.reset()
skel = K.humanoid(height=1.78, bulk=1.0, shoulders=1.08, hips=0.95)
J = {n: p.copy() for n, (p, r) in skel.joints.items()}
SEGS = K._seg_info(skel)
SX = {"L": 1.0, "R": -1.0}


def side_of(name, s):
    return name + "." + s


# =============================================================================================
# Body
# =============================================================================================

SPAN = {b: skel.bone_span(b) for b in skel.bone_names()}


def bone_t(c, bone):
    h, t = SPAN[bone]
    d = t - h
    return max(0.0, min(1.0, (c - h).dot(d) / d.length_squared))


def bone_of(c):
    i, t, _ = K._closest_seg(c, SEGS)
    return SEGS[i][2]


_SRC = {}


def _sources():
    """Three resolutions of the same body; we keep the trunk from the finest, arms mid, legs coarse."""
    if not _SRC:
        _SRC["hi"] = L.islands(K.skin_body(skel, subsurf=2))
        _SRC["mid"] = L.islands(K.skin_body(skel, subsurf=2, decimate=0.55))
        _SRC["lo"] = L.islands(K.skin_body(skel, subsurf=1))
    return _SRC


def _kind(part):
    c = sum(part.verts, V()) / len(part.verts)
    if abs(c.x) > 0.18:
        return "arm"
    return "trunk" if c.z > 1.05 else "leg"


def _pick(res, kinds):
    out = []
    for isl in _sources()[res]:
        if _kind(isl) in kinds:
            out.append(K.Part([v.copy() for v in isl.verts], [list(f) for f in isl.faces], "g55", None, True))
    return out


def make_body(inflate=None, hand_flat=(0.62, 0.8), cuts=(), legs="lo"):
    """The skin-modifier body (trunk fine, arms medium, legs coarse), minus the kit's egg head and neck
    (we build our own), with flatter hands. inflate = {bone_key: meters} pushes the surface out (baggy suits).
    cuts = [(point, normal, bones)] clean paint seams. Returns (body, proxy): proxy is a coarse copy for shells."""
    part = L.merge_parts(_pick("hi", ("trunk",)) + _pick("mid", ("arm",)) + _pick(legs, ("leg",)), swatch="g55", bone=None)
    part.swatch = ["g55"] * len(part.faces)
    proxy = L.merge_parts(_pick("lo", ("trunk", "arm", "leg")), swatch="g55", bone=None)
    proxy.swatch = ["g55"] * len(proxy.faces)
    for p in (part, proxy):
        _shape(p, inflate, hand_flat)
    for co, no, bones in cuts:
        L.cut(part, co, no, region=lambda c, bones=bones: bone_of(c) in bones)
    out = []
    for p in (part, proxy):
        b = L.Body(p, skel)
        keep = [bn not in ("Neck", "Head") for bn in b.fbone]
        out.append(L.Body(L.drop_faces(p, lambda i: keep[i]), skel))
    return out[0], out[1]


def _shape(part, inflate, hand_flat):
    # flatter hands: squash along the palm normal (palms face the thighs at rest)
    for i, v in enumerate(part.verts):
        si, t, _ = K._closest_seg(v, SEGS)
        a, b, bone, pa, pb = SEGS[si]
        key = bone[:-2] if bone.endswith((".L", ".R")) else bone
        if key == "Hand":
            side = bone[-1]
            ax = (J[side_of("fingers", side)] - J[side_of("wrist", side)]).normalized()
            inward = V((-1 if side == "L" else 1, 0, 0))
            m = (inward - ax * inward.dot(ax)).normalized()
            seg_t = (v - J[side_of("wrist", side)]).dot(ax) / (J[side_of("fingers", side)] - J[side_of("wrist", side)]).length
            k = (hand_flat[0] if side == "L" else hand_flat[1])
            f = 1.0 + (k - 1.0) * L.ease(seg_t / 0.35)
            wr = J[side_of("wrist", side)]
            along = (v - wr).dot(ax)
            axis_pt = wr + ax * along
            off = v - axis_pt
            h = off.dot(m)
            v = v - m * h * (1 - f)
            if side == "R" and along > 0:   # the gun hand is a fist: shorter
                v = v - ax * along * 0.24
            part.verts[i] = v
        if key == "Foot" and part.verts[i].z < 0.006:   # the kit's soles sink ~1 cm: flat at the ground
            part.verts[i] = V((part.verts[i].x, part.verts[i].y, 0.006))
        if inflate and key in inflate:
            v = part.verts[i]
            axis_pt = pa + (pb - pa) * t
            d = v - axis_pt
            if d.length > 1e-6:
                part.verts[i] = v + d.normalized() * inflate[key]


def seams(hem=None, shoe=0.14, glove=None, sleeve=None, extra=()):
    """Cut planes for a skin's paint boundaries."""
    out = []
    trunk = ("Hips", "Spine", "Chest", "Shoulder.L", "Shoulder.R")
    if hem:
        out.append((V((0, 0, hem)), V((0, 0, 1)), trunk))
    if shoe:
        out.append((V((0, 0, shoe)), V((0, 0, 1)), ("LowerLeg.L", "LowerLeg.R", "Foot.L", "Foot.R")))
    out.append((V((0, 0, 0.022)), V((0, 0, 1)), ("Foot.L", "Foot.R")))
    for bone, t in ([("Hand", glove)] if glove else []) + ([("LowerArm", sleeve)] if sleeve else []):
        for s in ("L", "R"):
            h, tl = SPAN[bone + "." + s]
            out.append((h + (tl - h) * t, (tl - h).normalized(), (bone + "." + s,)))
    return out + list(extra)


def neck_tube(swatch, radius=0.057):
    """Our neck: from inside the trunk to inside the head, weights Chest -> Neck -> Head by height."""
    pts = [V((0, 0.012, 1.44)), V((0, 0.008, 1.5)), V((0, 0.0, 1.56)), V((0, -0.002, 1.615))]
    p = L.tube(pts, [radius * 1.25, radius, radius * 0.98, radius * 0.95], sides=10, swatch=swatch)

    def w(co):
        z = co.z
        if z < 1.49:
            return {"Chest": 1.0}
        if z < 1.545:
            k = (z - 1.49) / 0.055
            return {"Chest": 1 - k, "Neck": k}
        k = min(1.0, (z - 1.545) / 0.04)
        return {"Neck": 1 - k, "Head": k}
    return L.weighted(p, w)


def painter(spec):
    """Body colors from a skin spec: torso top above hem_z, pants below, sleeves, hands, shoes.
    spec keys: skin, top, pants, hem_z, sleeve [(t, sw)], hand [(t, sw)], shoe, shoe_z, sole, extra(c, s, key)."""
    skin = spec["skin"]
    top = spec.get("top", "hoodie_charcoal")
    pants = spec.get("pants", "cargo_khaki")
    hem = spec.get("hem_z", 0.98)
    sleeve = spec.get("sleeve", [(1.0, top)])
    lower = spec.get("lower", sleeve)
    hand = spec.get("hand", [(1.0, skin)])
    shoe = spec.get("shoe", "boot_brown")
    shoe_z = spec.get("shoe_z", 0.13)
    sole = spec.get("sole", "rubber_black")
    extra = spec.get("extra")

    def pick(lst, t):
        for tm, s in lst:
            if t <= tm:
                return s
        return lst[-1][1]

    def fn(c, s, bone, _seg_t):
        key = bone[:-2] if bone.endswith((".L", ".R")) else bone
        t = bone_t(c, bone)
        if key in ("Hips", "Spine", "Chest", "Shoulder"):
            out = top if c.z > hem else pants
        elif key == "UpperArm":
            out = pick(sleeve, t * 0.5)
        elif key == "LowerArm":
            out = pick(lower, 0.5 + t * 0.5)
        elif key == "Hand":
            out = pick(hand, t)
        elif key == "UpperLeg":
            out = pants
        elif key == "LowerLeg":
            out = shoe if c.z < shoe_z else pants
        elif key == "Foot":
            out = sole if c.z < 0.022 else shoe
        else:
            out = skin
        if extra:
            out = extra(c, out, key, t) or out
        return out
    return fn


def paint_body(body, spec, seed=0):
    K.outfit_faces(body.part, skel, {"*": "g55"}, extra=painter(spec), seed=seed)
    return body.part


# =============================================================================================
# Head
# =============================================================================================

HC = V((0, 0.012, 1.672))  # cranium center


def head_shape(i_ring, n_up, n_down, th):
    """Loft: a cranium dome above the equator, a jaw narrowing to the chin below. th = 0 front."""
    st, ct = math.sin(th), math.cos(th)
    if i_ring <= n_up:
        phi = (math.pi / 2) * i_ring / n_up
        r = math.sin(phi)
        z = HC.z + 0.104 * math.cos(phi)
        ry = 0.103 if ct > 0 else 0.099
        return V((0.085 * r * st, HC.y - ry * r * ct, z))
    h = (i_ring - n_up) / n_down
    yc = HC.y - 0.03 * h
    z = HC.z - 0.118 * h ** 1.1
    rx = 0.085 * (1 - 0.55 * h ** 2.2)
    ryf = 0.103 - 0.04 * h ** 1.5
    ryb = 0.099 * (1 - 0.7 * h ** 1.3)
    return V((rx * st, yc - (ryf if ct > 0 else ryb) * ct, z))


def head_mesh(skin, segs=16, n_up=5, n_down=6):
    verts = [V((HC.x, HC.y, HC.z + 0.104))]
    rows = [[0]]
    for i in range(1, n_up + n_down + 1):
        row = []
        for j in range(segs):
            row.append(len(verts))
            verts.append(head_shape(i, n_up, n_down, math.tau * j / segs))
        rows.append(row)
    # chin cap
    chin = sum((verts[k] for k in rows[-1]), V()) / segs
    verts.append(chin + V((0, 0, -0.004)))
    rows.append([len(verts) - 1])
    faces = []
    for i in range(len(rows) - 1):
        a, b = rows[i], rows[i + 1]
        if len(a) == 1:
            for j in range(segs):
                faces.append([a[0], b[j], b[(j + 1) % segs]])
        elif len(b) == 1:
            for j in range(segs):
                faces.append([a[j], b[0], a[(j + 1) % segs]])
        else:
            for j in range(segs):
                j2 = (j + 1) % segs
                faces.append([a[j], b[j], b[j2], a[j2]])
    p = K.Part(verts, faces, skin, "Head", smooth=True)
    p = L.recalc(p)
    # open the underside behind the jaw: the neck tube comes in there
    cs = [sum((p.verts[i] for i in f), V()) / len(f) for f in p.faces]
    p = L.drop_faces(p, lambda i: not (cs[i].z < 1.612 and cs[i].y > -0.012))
    return p


def hairline_z(c):
    th = math.atan2(c.x, -(c.y - HC.y))  # 0 front, +-pi back
    f = (1 - math.cos(th)) / 2
    xs = [0, 0.22, 0.45, 0.7, 1.0]
    zs = [1.716, 1.70, 1.676, 1.632, 1.598]
    for k in range(len(xs) - 1):
        if f <= xs[k + 1]:
            u = (f - xs[k]) / (xs[k + 1] - xs[k])
            return zs[k] + (zs[k + 1] - zs[k]) * u
    return zs[-1]


def head_parts(skin, hair="hair_brown", style="short", brow=None, beard="shadow", seed=3, ears=True, face=True):
    """Head + face + hair. style: short | none | mohawk | cap (only the sides, under a hat).
    beard: None | shadow (darker skin on the jaw) | stubble | full (hair color)."""
    parts = []
    head = head_mesh(skin)
    brow = brow or ("hair_dark" if hair in ("hair_brown", "hair_dark") else hair)
    shadow = {"skin_light": "skin_mid", "skin_mid": "skin_dark", "skin_dark": "stubble"}.get(skin, skin)

    def skin_paint(c, s):
        if style not in ("none",) and c.z > hairline_z(c) - 0.004:
            if style == "mohawk":
                return "stubble" if abs(c.x) > 0.024 else hair
            return hair
        jaw = c.z < 1.603 and c.y < HC.y - 0.02 and (abs(c.x) > 0.022 or c.z < 1.585)
        if beard == "full" and c.z < 1.628 and c.y < HC.y + 0.01:
            return hair
        if beard == "stubble" and jaw:
            return "stubble"
        if beard == "shadow" and jaw:
            return shadow
        return None
    L.recolor(head, skin_paint)
    parts.append(head)
    hb = L.Body(head, skel)
    rnd = random.Random(seed)
    if style in ("short", "cap", "long"):
        def sel(c, n, bone):
            z0 = hairline_z(c)
            if style == "cap":
                return z0 + 0.004 < c.z < 1.735
            return c.z > z0 + 0.004

        def off(pv):
            nz = perlin(pv * 38.0 + V((seed, 0, 0)))
            top = max(0.0, (pv.z - 1.70) / 0.08)
            return 0.006 + 0.004 * nz + 0.007 * top
        hp = hb.shell(sel, off, hair, bone="Head", sink=0.003)
        parts.append(hp)
    if face:
        # nose (hull), brows, eyes, mouth line, ears
        parts.append(L.hull([(0, -0.086, 1.682), (0, -0.117, 1.641), (0.017, -0.099, 1.634), (-0.017, -0.099, 1.634),
                             (0, -0.105, 1.629), (0.011, -0.083, 1.662), (-0.011, -0.083, 1.662), (0, -0.082, 1.634),
                             (0.007, -0.094, 1.674), (-0.007, -0.094, 1.674)], swatch=skin, bone="Head"))
        for s in (1, -1):
            eye_c = V((0.031 * s, HC.y - 0.091, 1.676))
            parts.append(K.ball(0.0115, eye_c, scale=(1.15, 0.55, 0.75), swatch="eye_dark", bone="Head", segments=8, rings=4))
            parts.append(K.box((0.032, 0.011, 0.009), loc=(0.033 * s, HC.y - 0.098, 1.693), rot=(0, -8 * s, 0), swatch=brow, bone="Head", bevel=0.002))
            if ears:
                parts.append(K.ball(0.02, (0.084 * s, HC.y + 0.006, 1.652), rot=(0, 0, 0), scale=(0.55, 1.05, 1.5), swatch=skin, bone="Head", segments=8, rings=5))
        parts.append(K.box((0.03, 0.008, 0.006), loc=(0, HC.y - 0.092, 1.611), swatch="lips", bone="Head", bevel=0.002))
    return parts


# =============================================================================================
# The injector (left forearm) and the revolver (right hand)
# =============================================================================================

def forearm_frame():
    E, W = J["elbow.L"], J["wrist.L"]
    a = (W - E).normalized()
    lat = V((1, 0, 0))
    lat = (lat - a * lat.dot(a)).normalized()
    fwd = V((0, -1, 0))
    fwd = (fwd - a * fwd.dot(a)).normalized()
    n = (lat + fwd * 0.75).normalized()
    s = a.cross(n)
    return E, W, a, n, s


INJ_T = 0.5     # casing center along the forearm (0 elbow, 1 wrist)
INJ_LEN = 0.15


def injector(body):
    """REGEN field injector strapped on the left forearm. Everything rigid on LowerArm.L, the liquid on Vial."""
    E, W, a, n, s = forearm_frame()
    Lf = (W - E).length
    tc = INJ_T
    R0 = body.radius(E, W, tc - 0.06 / Lf * 1.0)
    R1 = body.radius(E, W, tc + 0.06 / Lf * 1.0)
    Rc = body.radius(E, W, tc)
    O = E + a * (Lf * tc)
    M = L.frame_matrix(O, a, s, n)          # local x along the forearm (to the wrist), y side, z up (casing normal)
    parts = []

    def R_at(u):  # u in meters from the center along a
        k = (u + 0.06) / 0.12
        return R0 + (R1 - R0) * max(0.0, min(1.0, k))

    # cradle: a curved grey plate hugging the forearm under the casing
    def cradle(uu, vv):
        u = (uu - 0.5) * 0.16
        ang = math.radians((vv - 0.5) * 210)
        r = R_at(u) + 0.0045
        return O + a * u + (n * math.cos(ang) + s * math.sin(ang)) * r
    parts.append(L.slab(cradle, 4, 8, 0.006, swatch="regen_grey", bone="LowerArm.L"))
    top = Rc + 0.026                       # casing top height above the axis
    cas = K.box((INJ_LEN, 0.064, 0.03), loc=(0, 0, top - 0.015), swatch="regen_white", bevel=0.007)
    parts.append(L.xform(cas, M))
    # end blocks (grey), guard rails, caps, glass, diode, button
    for u in (-INJ_LEN / 2 + 0.008, INJ_LEN / 2 - 0.008):
        parts.append(L.xform(K.box((0.016, 0.068, 0.034), loc=(u, 0, top - 0.015), swatch="regen_grey", bevel=0.005), M))
    for v in (-0.021, 0.021):
        parts.append(L.xform(K.box((0.112, 0.008, 0.012), loc=(0, v, top + 0.004), swatch="regen_plate", bevel=0.002), M))
    vz = top + 0.008
    parts.append(L.xform(K.cyl(0.0105, 0.118, loc=(0, 0, vz), rot=(0, 90, 0), swatch="glass_dark", segments=8), M))
    for u in (-0.0625, 0.0625):
        parts.append(L.xform(K.cyl(0.0148, 0.012, loc=(u, 0, vz), rot=(0, 90, 0), swatch="steel", segments=8), M))
    parts.append(L.xform(K.box((0.012, 0.012, 0.007), loc=(-0.063, 0.031, top + 0.002), swatch="cyan", bevel=0.002), M))
    parts.append(L.xform(K.cyl(0.0105, 0.012, loc=(0.06, -0.03, top + 0.004), swatch="regen_plate", segments=8), M))
    for p in parts:
        p.bone = "LowerArm.L"
    # straps around the forearm (over the sleeve), with a buckle
    for u in (-0.045, 0.045):
        t = tc + u / Lf
        r = body.radius(E, W, t) + 0.0075
        c = E + a * (Lf * t)
        parts.append(L.ring(c, a, r, (0.0045, 0.011), segs=12, sides=4, ref=n, swatch="strap_black", bone="LowerArm.L"))
        parts.append(L.xform(K.box((0.02, 0.016, 0.008), loc=(u, 0, 0), swatch="metal_worn", bevel=0.002),
                             L.frame_matrix(c - n * (r + 0.002), a, -s, -n)))
        parts[-1].bone = "LowerArm.L"
    # hose from the casing to the inner wrist (the injection port)
    pts = []
    for k, (u, ang) in enumerate(((0.07, 28), (0.082, 75), (0.094, 125), (0.104, 165))):
        rr = body.radius(E, W, tc + u / Lf) + 0.008
        an = math.radians(ang)
        pts.append(O + a * u + (n * math.cos(an) - s * math.sin(an)) * rr)
    pts[0] = O + a * 0.07 + n * (top - 0.012) - s * 0.03
    parts.append(L.tube(pts, 0.0052, sides=5, swatch="rubber_black", bone="LowerArm.L"))
    pend = pts[-1]
    parts.append(L.xform(K.box((0.018, 0.016, 0.01), swatch="regen_grey", bevel=0.003),
                         L.frame_matrix(pend, a, s, (pend - (O + a * 0.104)).normalized())))
    parts[-1].bone = "LowerArm.L"
    # the amber liquid: its own bone (Vial). Head at the wrist end (inside the cap), fills toward the elbow.
    liq = K.cyl(0.012, 0.1175, loc=(0.00125, 0, vz), rot=(0, 90, 0), swatch="amber", segments=8)
    parts.append(L.rigid(L.xform(liq, M), "Vial"))
    vial_head = M @ V((0.06, 0, vz))
    vial_tail = M @ V((-0.0575, 0, vz))
    return parts, (vial_head, vial_tail)


def gun_frame():
    """Revolver frame at rest: X = barrel (along the hand), Z = gun up (thumb side, forward at rest)."""
    a = (J["fingers.R"] - J["wrist.R"]).normalized()
    g = V((0, 1, 0))
    g = (g - a * g.dot(a)).normalized()   # grip direction (pinky side, backward at rest)
    z = -g
    y = z.cross(a)
    o = J["wrist.R"] + a * 0.05      # the grip passes through the middle of the fist
    return L.frame_matrix(o, a, y, z), o, a, z


def revolver():
    """Police service revolver, +15 %. Rigid on Hand.R. Returns (parts, muzzle_point, barrel_dir)."""
    M, o, ax, up = gun_frame()
    P = []
    bz = 0.07
    M = M @ Matrix.Translation(V((0.03, 0, 0)))
    P.append(K.cyl(0.0098, 0.122, loc=(0.106, 0, bz), rot=(0, 90, 0), swatch="metal_gun", segments=8))
    P.append(K.box((0.112, 0.015, 0.014), loc=(0.106, 0, bz - 0.014), swatch="metal_gun", bevel=0.003))
    P.append(K.box((0.012, 0.005, 0.012), loc=(0.16, 0, bz + 0.013), swatch="metal_gun"))
    P.append(K.cyl(0.0245, 0.047, loc=(0.02, 0, bz - 0.014), rot=(0, 90, 0), swatch="metal_worn", segments=6, smooth=False))
    P.append(K.box((0.034, 0.024, 0.066), loc=(-0.017, 0, bz - 0.012), swatch="metal_gun", bevel=0.004))
    P.append(K.box((0.052, 0.017, 0.009), loc=(0.02, 0, bz + 0.012), swatch="metal_gun"))
    P.append(K.box((0.05, 0.02, 0.012), loc=(0.022, 0, bz - 0.044), swatch="metal_gun"))
    P.append(K.box((0.014, 0.009, 0.022), loc=(-0.036, 0, bz + 0.016), rot=(0, -28, 0), swatch="metal_gun"))
    P.append(L.ring(V((0.016, 0, bz - 0.064)), V((0, 1, 0)), 0.016, 0.003, segs=8, sides=4, arc=(-20, 200), ref=V((1, 0, 0)), swatch="metal_gun"))
    P.append(K.box((0.005, 0.006, 0.02), loc=(0.012, 0, bz - 0.058), rot=(0, 15, 0), swatch="steel"))
    P.append(K.box((0.046, 0.032, 0.15), loc=(-0.04, 0, bz - 0.112), rot=(0, 16, 0), swatch="wood", bevel=0.008, taper=(1.0, 1.08)))
    P.append(K.box((0.044, 0.03, 0.01), loc=(-0.062, 0, bz - 0.186), rot=(0, 16, 0), swatch="metal_gun"))
    for p in P:
        L.xform(p, M)
        p.bone = "Hand.R"
    muzzle = M @ V((0.168, 0, bz))
    return P, muzzle, ax


# =============================================================================================
# Shared gear
# =============================================================================================

def pack(color="canvas_tan", pocket="canvas_olive", mat="bedroll_rust", depth=0.13, w=0.3, h=0.34):
    """Small backpack with a rolled mat on top. Rigid on Chest. Straps follow the trunk."""
    P = []
    yb = 0.158 + depth / 2
    zc = 1.29
    P.append(K.box((w, depth, h), loc=(0, yb, zc), swatch=color, bevel=0.03, taper=(0.92, 0.9)))
    P.append(K.box((w * 0.72, 0.045, h * 0.45), loc=(0, yb + depth / 2 + 0.012, zc - h * 0.18), swatch=pocket, bevel=0.012))
    P.append(K.box((w * 0.75, 0.05, 0.03), loc=(0, yb + depth / 2 + 0.005, zc + h * 0.38), swatch=pocket, bevel=0.01, rot=(-12, 0, 0)))
    mz = zc + h / 2 + 0.05
    if mat:
        P.append(K.cyl(0.058, 0.44, loc=(0, yb - 0.005, mz), rot=(0, 90, 0), swatch=mat, segments=10))
        for x in (-0.13, 0.13):
            P.append(L.ring(V((x, yb - 0.005, mz)), V((1, 0, 0)), 0.061, (0.004, 0.011), segs=10, sides=4, swatch="strap_black"))
    for p in P:
        p.bone = "Chest"
    return P


def pack_straps(color="strap_black", x=0.105):
    """Backpack shoulder straps over the shoulders to the chest."""
    P = []
    for s in (1, -1):
        pts = [V((x * s, 0.19, 1.42)), V((x * s, 0.12, 1.485)), V((x * s * 1.03, 0.02, 1.505)),
               V((x * s * 1.08, -0.08, 1.47)), V((x * s * 1.15, -0.155, 1.39)), V((x * s * 1.2, -0.175, 1.28))]
        P.append(L.weighted(L.tube(pts, 0.016, sides=4, squash=0.3, swatch=color, ref=V((x * s, 0, 0))), L.trunk_weights(skel)))
    return P


def gas_mask(at=V((0, -0.178, 1.455)), facing=V((0, -0.75, -0.66)), strap=True):
    """Gas mask hanging at the neck, resting on the upper chest. Rigid on Chest."""
    f = facing.normalized()
    up = V((0, 0, 1))
    upv = (up - f * up.dot(f)).normalized()
    side = upv.cross(f)
    M = L.frame_matrix(at, side, upv, f)
    P = [K.ball(0.064, (0, 0, 0), scale=(1.0, 1.08, 0.55), swatch="g35", segments=10, rings=6),
         K.cyl(0.034, 0.05, loc=(0, -0.028, 0.04), swatch="military_green", segments=8),
         K.cyl(0.028, 0.01, loc=(0, -0.028, 0.068), swatch="metal_dark", segments=8)]
    for s in (1, -1):
        P.append(K.cyl(0.023, 0.012, loc=(0.031 * s, 0.024, 0.026), swatch="metal_worn", segments=8))
        P.append(K.cyl(0.018, 0.012, loc=(0.031 * s, 0.024, 0.03), swatch="glass_dark", segments=8))
    for p in P:
        L.xform(p, M)
        p.bone = "Chest"
    if strap:
        P.append(L.ring(V((0, 0.01, 1.49)), V((0, 0.32, 1)), 0.088, (0.004, 0.009), segs=12, sides=4, swatch="strap_black", bone="Chest"))
    return P


def hood_down(color, at=V((0, 0.045, 1.49))):
    """The hoodie's hood bunched behind the neck."""
    p = L.ring(at, V((0, 0.55, 1)), 0.088, (0.045, 0.034), segs=12, sides=6, arc=(-120, 120), ref=V((0, 1, 0)), swatch=color, bone="Chest")
    rnd = random.Random(4)
    p.verts = [v + (v - at).normalized() * (rnd.random() - 0.5) * 0.012 for v in p.verts]
    return p


def plate_carrier(body, color="plate_carrier", pouch="vest_dark", band=True, pouches=True):
    """Cheap plate carrier over the hoodie: front + back plates, cummerbund, shoulder straps, pouches."""
    P = []
    trunk = ("Spine", "Chest", "Hips")
    P.append(body.shell(lambda c, n, b: b in trunk and 1.15 < c.z < 1.47 and c.y < -0.03 and abs(c.x) < 0.155, 0.022, color))
    P.append(body.shell(lambda c, n, b: b in trunk and 1.15 < c.z < 1.47 and c.y > 0.03 and abs(c.x) < 0.155, 0.02, color))
    if band:
        P.append(body.shell(lambda c, n, b: b in trunk and 1.13 < c.z < 1.25, 0.014, color))
    P.append(body.shell(lambda c, n, b: b in ("Chest", "Shoulder.L", "Shoulder.R") and c.z > 1.41 and 0.06 < abs(c.x) < 0.15 and abs(c.y) < 0.16, 0.016, color))
    if pouches:
        for k, x in enumerate((-0.075, 0.0, 0.075)):
            loc, nrm = body.hit(V((x, 0, 1.25)), V((0, -1, 0)))
            c = loc + V((0, -0.022 - 0.018, 0))
            P.append(L.weighted(K.box((0.062, 0.036, 0.1), loc=c, swatch=pouch, bevel=0.008), L.trunk_weights(skel)))
            P.append(L.weighted(K.box((0.066, 0.04, 0.022), loc=c + V((0, -0.002, 0.052)), swatch=color, bevel=0.005), L.trunk_weights(skel)))
        loc, nrm = body.hit(V((0, 0, 1.39)), V((0, -1, 0)))
        P.append(L.weighted(K.box((0.1, 0.012, 0.06), loc=loc + V((0, -0.026, 0)), swatch="canvas_tan", bevel=0.004), L.trunk_weights(skel)))
    return P


def cargo_pockets(body, color, flap=None):
    P = []
    for s in ("L", "R"):
        sx = 1 if s == "L" else -1
        c0 = J["hip." + s].lerp(J["knee." + s], 0.55)
        loc, nrm = body.hit(c0, V((sx, 0.05, 0)))
        out = V((sx, 0, 0))
        M = L.frame_matrix(loc + out * 0.012, V((0, -1, 0)), V((0, 0, 1)), out) if sx > 0 else L.frame_matrix(loc + out * 0.012, V((0, 1, 0)), V((0, 0, 1)), out)
        P.append(L.xform(K.box((0.12, 0.15, 0.03), swatch=color, bevel=0.01), M))
        P[-1].bone = "UpperLeg." + s
        P.append(L.xform(K.box((0.128, 0.04, 0.036), loc=(0, 0.06, 0.003), swatch=flap or color, bevel=0.006), M))
        P[-1].bone = "UpperLeg." + s
    return P


def band(body, zmin, zmax, color, off=0.008, bones=("Hips", "Spine")):
    return body.shell(lambda c, n, b: b in bones and zmin < c.z < zmax, off, color)


def cuffs(body, color, t0=0.86, off=0.006):
    return body.shell(lambda c, n, b: b in ("LowerArm.L", "LowerArm.R") and _t_on(c, b) > t0, off, color)


def _t_on(c, bone):
    best = None
    for a, b, bn, pa, pb in SEGS:
        if bn == bone:
            ab = pb - pa
            t = max(0.0, min(1.0, (c - pa).dot(ab) / ab.length_squared))
            d = (pa + ab * t - c).length
            if best is None or d < best[0]:
                best = (d, a, t)
    if best is None:
        return 0.0
    # LowerArm = elbow->forearm->wrist: remap to 0..1 over the bone
    if bone.startswith("LowerArm"):
        s = bone[-1]
        E, W = J["elbow." + s], J["wrist." + s]
        return max(0.0, min(1.0, (c - E).dot(W - E) / (W - E).length_squared))
    return best[2]


def soles(body, color="rubber_black", off=0.009):
    p = body.shell(lambda c, n, b: b.startswith("Foot") and c.z < 0.035, off, color)
    p.verts = [V((v.x, v.y, max(0.0, v.z))) for v in p.verts]
    return p


# =============================================================================================
# Costume builders
# =============================================================================================

CAMO = ("green_2", "brown_2", "vest_khaki", "green_1")


def camo(c, seed=0, scale=6.5, cols=CAMO):
    n1 = perlin(c * scale + V((seed * 1.7, 3.1, 7.7)))
    n2 = perlin(c * scale * 1.6 + V((11.0, seed * 0.9, 2.3)))
    if n1 > 0.22:
        return cols[1]
    if n1 < -0.25:
        return cols[3]
    if n2 > 0.12:
        return cols[2]
    return cols[0]


def camo_part(p, seed=0):
    return L.recolor(p, lambda c, s: camo(c, seed))


def face_normals(part):
    out = []
    for f in part.faces:
        a, b, c = (part.verts[i] for i in f[:3])
        n = (b - a).cross(c - a)
        out.append(n.normalized() if n.length > 0 else V((0, 0, 1)))
    return out


def recolor_n(part, fn):
    ns = face_normals(part)
    out = []
    for fi, f in enumerate(part.faces):
        c = sum((part.verts[i] for i in f), V()) / len(f)
        out.append(fn(c, ns[fi], part.swatch[fi]) or part.swatch[fi])
    part.swatch = out
    return part


def skirt(side, z_top, z_bot, color, rx=(0.185, 0.25), ry=(0.15, 0.205), a0=14, a1=174, thick=0.012, k=0.92,
          nu=7, nv=4, cy=0.005, lining=None, front=0.165):
    """Half of a coat skirt (one per leg): flares from the waist, follows its thigh more and more toward the
    hem (weights Hips -> UpperLeg), so it never cuts through the legs. a0..a1: degrees from the front."""
    sx = SX[side]

    def fn(u, v):
        a = math.radians(a0 + (a1 - a0) * u)
        e = v ** 1.25
        rxx = rx[0] + (rx[1] - rx[0]) * e
        back = (1 - math.cos(a)) / 2                       # 0 at the front, 1 at the back
        ry1 = front + (ry[1] - front) * back                # the front hangs straighter than the back
        ryy = ry[0] + (ry1 - ry[0]) * e
        return V((sx * rxx * math.sin(a), cy - ryy * math.cos(a), z_top + (z_bot - z_top) * v))
    p = L.slab(fn, nu, nv, thick, swatch=color)
    if lining:
        def inner(c, n, s):
            radial = V((c.x, c.y - cy, 0))
            return lining if radial.length > 1e-6 and n.dot(radial.normalized()) < -0.3 else None
        recolor_n(p, inner)

    def w(co):
        v = max(0.0, min(1.0, (z_top - co.z) / (z_top - z_bot)))
        wl = k * L.ease(min(1.0, v * 1.3))
        return {"Hips": 1 - wl, "UpperLeg." + side: wl}
    return L.weighted(p, w)


CAPE_ROWS = [(1.545, 0.095, 0.085, 0.0, 120), (1.535, 0.25, 0.155, 0.02, 98), (1.42, 0.29, 0.19, 0.05, 84),
             (1.02, 0.31, 0.225, 0.1, 72), (0.56, 0.33, 0.245, 0.14, 66)]


def cape(color, rows=CAPE_ROWS, thick=0.014, nu=12, nv=8, lining=None):
    """A cape draped over the shoulders: upper rows on Chest, lower rows on the Cape flap bone."""
    def row(v):
        x = v * (len(rows) - 1)
        i = min(len(rows) - 2, int(x))
        t = x - i
        return [rows[i][k] + (rows[i + 1][k] - rows[i][k]) * t for k in range(5)]

    def fn(u, v):
        z, rxx, ryy, cy, half = row(v)
        a = math.radians((u - 0.5) * 2 * half)
        return V((rxx * math.sin(a), cy + ryy * math.cos(a), z))
    p = L.slab(fn, nu, nv, thick, swatch=color)
    if lining:
        def inner(c, n, s):
            radial = V((c.x, c.y - 0.05, 0))
            return lining if radial.length > 1e-6 and n.dot(radial.normalized()) < -0.3 else None
        recolor_n(p, inner)

    def w(co):
        k = L.ease((1.43 - co.z) / 0.3)
        return {"Chest": 1 - k, "Cape": k}
    return L.weighted(p, w)


def cowboy_hat(felt="coat_brown", band_c="leather_black", z0=1.722, tilt=7):
    P = []

    def brim(u, v):
        th = u * math.tau
        r = 0.096 + (0.245 - 0.096) * v
        z = z0 + 0.055 * (math.sin(th) ** 2) * v ** 2 - 0.012 * (math.cos(th) ** 2) * v
        return V((r * math.sin(th), HC.y - r * math.cos(th), z))
    P.append(L.slab(brim, 24, 3, 0.008, swatch=felt, closed_u=True))
    P.append(K.cyl(0.094, 0.13, loc=(0, HC.y + 0.004, z0 + 0.065), swatch=felt, segments=12, radius2=0.08))
    P.append(K.box((0.13, 0.05, 0.03), loc=(0, HC.y + 0.004, z0 + 0.128), swatch=felt, bevel=0.01))   # crease ridge
    P.append(L.ring(V((0, HC.y + 0.004, z0 + 0.018)), V((0, 0, 1)), 0.096, (0.005, 0.014), segs=14, sides=4, swatch=band_c))
    m = Matrix.Translation(V((0, HC.y, z0))) @ Matrix.Rotation(math.radians(-tilt), 4, "X") @ Matrix.Translation(-V((0, HC.y, z0)))
    for p in P:
        L.xform(p, m)
        p.bone = "Head"
    return P


def patrol_cap(seed=2, z0=1.722):
    P = [K.cyl(0.098, 0.085, loc=(0, HC.y + 0.004, z0 + 0.04), swatch="green_2", segments=12, radius2=0.093)]

    def visor(u, v):
        a = math.radians(-72 + 144 * u)
        r = 0.093 + 0.07 * v
        return V((r * math.sin(a), HC.y - r * math.cos(a), z0 + 0.004 - 0.022 * v))
    P.append(L.slab(visor, 6, 2, 0.008, swatch="green_1"))
    for p in P:
        camo_part(p, seed)
        p.bone = "Head"
    P[-1].swatch = ["green_1"] * len(P[-1].faces)
    return P


def crown(z0=1.735):
    P = [L.ring(V((0, HC.y + 0.006, z0)), V((0, 0.08, 1)), 0.101, (0.006, 0.022), segs=16, sides=4, swatch="brass")]
    for i in range(5):
        a = math.radians(i * 72)
        d = V((math.sin(a), -math.cos(a), 0))
        base = V((0, HC.y + 0.006, z0 + 0.018)) + d * 0.101
        tipdir = d * 0.25 + V((0, 0, 1))
        P.append(K.shard(0.07, 0.024, base, tipdir, swatch="brass", seed=40 + i, sides=4))
        P.append(K.ball(0.011, base + tipdir.normalized() * 0.072, swatch="gold", segments=6, rings=3))
    for x in (-0.045, 0.0, 0.045):
        P.append(K.box((0.016, 0.01, 0.016), loc=(x, HC.y - 0.1 + abs(x) * 0.25, z0), rot=(0, 0, x * 400), swatch="car_red", bevel=0.003))
    for p in P:
        p.bone = "Head"
    return P


def viking_helmet():
    dome = K.ball(0.106, (0, HC.y + 0.004, 1.698), scale=(0.93, 1.03, 0.9), swatch="metal_worn", segments=14, rings=8)
    cs = [sum((dome.verts[i] for i in f), V()) / len(f) for f in dome.faces]
    dome = L.drop_faces(dome, lambda i: cs[i].z > 1.693)
    P = [dome,
         L.ring(V((0, HC.y + 0.004, 1.695)), V((0, 0, 1)), 0.098, (0.009, 0.012), segs=14, sides=4, swatch="metal_dark"),
         K.box((0.024, 0.014, 0.075), loc=(0, HC.y - 0.106, 1.675), swatch="metal_dark", bevel=0.004),
         K.box((0.026, 0.22, 0.012), loc=(0, HC.y + 0.004, 1.79), rot=(-4, 0, 0), swatch="metal_dark")]
    for s in (1, -1):
        pts = [V((0.088 * s, HC.y + 0.0, 1.735)), V((0.13 * s, HC.y - 0.01, 1.75)), V((0.18 * s, HC.y - 0.02, 1.79)),
               V((0.205 * s, HC.y - 0.035, 1.85)), V((0.2 * s, HC.y - 0.05, 1.91))]
        P.append(L.tube(pts, [0.028, 0.024, 0.017, 0.009, 0.0], sides=7, swatch="plastic_white"))
    for p in P:
        p.bone = "Head"
    return P


def bicorne(z0=1.728):
    P = []
    for side in (-1, 1):
        def flap(u, v, side=side):
            x = (u - 0.5) * 0.5
            h = 0.135 * math.sqrt(max(0.0, 1 - (2 * x / 0.5) ** 2)) + 0.03
            return V((x, HC.y + side * 0.055 * (1 - v) ** 1.15, z0 + v * h))
        P.append(L.slab(flap, 10, 3, 0.012, swatch="track_black"))
    edge = []
    for i in range(11):
        x = (i / 10 - 0.5) * 0.5
        h = 0.135 * math.sqrt(max(0.0, 1 - (2 * x / 0.5) ** 2)) + 0.03
        edge.append(V((x, HC.y, z0 + h + 0.004)))
    P.append(L.tube(edge, 0.006, sides=4, swatch="gold_dark"))
    P.append(K.cyl(0.026, 0.008, loc=(0.11, HC.y - 0.04, z0 + 0.09), rot=(80, 0, 0), swatch="red_3", segments=8))
    for p in P:
        p.bone = "Head"
    return P


def demon_horns():
    P = []
    for s in (1, -1):
        pts = [V((0.05 * s, HC.y - 0.045, 1.752)), V((0.066 * s, HC.y - 0.04, 1.79)), V((0.085 * s, HC.y - 0.015, 1.84)),
               V((0.09 * s, HC.y + 0.03, 1.875)), V((0.08 * s, HC.y + 0.07, 1.885))]
        P.append(L.tube(pts, [0.024, 0.019, 0.013, 0.007, 0.0], sides=7, swatch="red_3"))
    for p in P:
        p.bone = "Head"
    return P


def halo():
    P = [L.ring(V((0, HC.y + 0.03, 1.905)), V((0, 0.2, 1)), 0.088, 0.0095, segs=16, sides=5, swatch="lamp_cold"),
         L.tube([V((0, HC.y + 0.095, 1.66)), V((0, HC.y + 0.125, 1.74)), V((0, HC.y + 0.13, 1.83)), V((0, HC.y + 0.115, 1.89))],
                0.0035, sides=4, swatch="metal_worn")]
    for p in P:
        L.rigid(p, "Halo")
    return P


def wings(color="chalk", tip="apron_white"):
    """Small feathered costume wings on elastic straps: two layered slabs per side, facing back."""
    P = []
    for s in (1, -1):
        root = V((0.055 * s, 0.205, 1.385))
        span = V((0.86 * s, 0.42, 0.3)).normalized()
        down = V((0.05 * s, 0.72, -0.7))
        down = (down - span * down.dot(span)).normalized()
        nrm = span.cross(down).normalized() * s
        for layer, (L0, w0, off, col) in enumerate(((0.44, 0.33, 0.0, color), (0.32, 0.2, 0.035, tip))):
            def fn(u, v, L0=L0, w0=w0, off=off, layer=layer, root=root, span=span, down=down, nrm=nrm):
                a = u * L0
                top = -0.13 * math.sin(min(1.0, u * 1.08) * math.pi * 0.72)
                width = w0 * (1 - u) ** 0.5 + 0.045
                scal = 0.032 * abs(math.sin(u * math.pi * 4.5)) * v * v
                return root + span * a + down * (top + (width + scal) * v + off) - nrm * (0.014 * layer)
            P.append(L.slab(fn, 7, 3, 0.018, swatch=col))
        P.append(L.tube([V((0.05 * s, 0.18, 1.38)), V((0.1 * s, 0.13, 1.49)), V((0.12 * s, 0.0, 1.51)),
                         V((0.13 * s, -0.12, 1.45)), V((0.14 * s, -0.16, 1.36))], 0.007, sides=4, squash=0.4, swatch="chalk"))
    for p in P:
        p.bone = "Chest"
    return P


def lantern():
    """The 'ember aura': a small lantern hanging at the neck on a cord, glowing core. On the Lantern bone."""
    c = V((0, -0.2, 1.3))
    P = [K.cyl(0.03, 0.012, loc=c + V((0, 0, 0.05)), swatch="metal_dark", segments=8),
         K.cyl(0.036, 0.016, loc=c + V((0, 0, -0.05)), swatch="metal_dark", segments=8),
         K.cyl(0.024, 0.075, loc=c, swatch="fire_orange", segments=8),
         L.ring(c + V((0, 0, 0.066)), V((1, 0, 0)), 0.014, 0.003, segs=8, sides=4, swatch="metal_dark")]
    for k in range(4):
        a = math.radians(45 + 90 * k)
        P.append(K.box((0.008, 0.008, 0.1), loc=c + V((math.sin(a) * 0.03, math.cos(a) * 0.03, 0)), swatch="metal_dark"))
    for p in P:
        L.rigid(p, "Lantern")
    for s in (1, -1):
        cord = L.tube([V((0.07 * s, -0.04, 1.52)), V((0.06 * s, -0.12, 1.47)), V((0.025 * s, -0.18, 1.41)), c + V((0.004 * s, 0, 0.07))],
                      0.004, sides=4, swatch="strap_black")

        def w(co):
            k = L.ease((1.5 - co.z) / 0.12)
            return {"Chest": 1 - k, "Lantern": k}
        P.append(L.weighted(cord, w))
    return P


def demon_tail():
    pts = [V((0, 0.14, 1.0)), V((0, 0.2, 0.93)), V((0, 0.27, 0.84)), V((0.02, 0.33, 0.76)), V((0.04, 0.36, 0.68))]
    P = [L.tube(pts, [0.016, 0.014, 0.012, 0.01, 0.008], sides=6, swatch="red_2"),
         L.hull([(0.04, 0.37, 0.69), (0.08, 0.38, 0.64), (0.0, 0.38, 0.64), (0.045, 0.395, 0.575), (0.04, 0.36, 0.63), (0.04, 0.4, 0.63)], swatch="red_3")]
    for p in P:
        L.rigid(p, "Tail")
    return P


def star_badge(at, normal, r=0.036, swatch="brass"):
    """A five-pointed costume star (never six: no religious symbol)."""
    n = V(normal).normalized()
    up = V((0, 0, 1))
    x = up.cross(n).normalized()
    y = n.cross(x)
    pts = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5
        rr = r if k % 2 == 0 else r * 0.45
        pts.append((math.cos(a) * rr, math.sin(a) * rr))
    verts, faces = [], []
    for z in (0.0, 0.007):
        for px, py in pts:
            verts.append(at + x * px + y * py + n * z)
        verts.append(at + n * (z + 0.002))
    cf, cb = 10, 21
    for k in range(10):
        k2 = (k + 1) % 10
        faces.append([11 + k, 11 + k2, cb])
        faces.append([k2, k, cf])
        faces.append([k, k2, 11 + k2, 11 + k])
    return L.recalc(K.Part(verts, faces, swatch, "Chest", smooth=False))


def stethoscope(body):
    """Around the neck (behind the collar) and down the front of the coat, snapped to the chest."""
    def front(x, z):
        loc, n = body.hit(V((x, 0, z)), V((0, -1, 0)))
        return loc + n * 0.013
    pts = [front(0.07, 1.31), front(0.085, 1.4), front(0.085, 1.46), V((0.078, -0.04, 1.535)), V((0.04, 0.055, 1.56)),
           V((0.0, 0.08, 1.565)), V((-0.04, 0.055, 1.56)), V((-0.078, -0.04, 1.535)), front(-0.085, 1.46), front(-0.085, 1.4), front(-0.07, 1.29)]
    tube = L.tube(pts, 0.0065, sides=5, swatch="rubber_black")
    end_l, end_r = pts[0], pts[-1]
    P = [L.weighted(tube, L.trunk_weights(skel)),
         L.weighted(K.cyl(0.022, 0.012, loc=end_r + V((0, -0.008, -0.022)), rot=(80, 0, 0), swatch="steel", segments=8), L.trunk_weights(skel)),
         L.weighted(K.box((0.014, 0.014, 0.04), loc=end_l + V((0, -0.006, -0.02)), swatch="steel"), L.trunk_weights(skel))]
    return P


def glasses():
    P = []
    for s in (1, -1):
        P.append(L.ring(V((0.032 * s, HC.y - 0.1, 1.676)), V((0, 1, 0)), 0.019, 0.0028, segs=10, sides=4, swatch="metal_dark"))
        P.append(K.box((0.004, 0.07, 0.004), loc=(0.083 * s, HC.y - 0.063, 1.68), swatch="metal_dark"))
    P.append(K.box((0.026, 0.004, 0.004), loc=(0, HC.y - 0.104, 1.68), swatch="metal_dark"))
    for p in P:
        p.bone = "Head"
    return P


def epaulette(side, color="brass", fringe="brass"):
    s = SX[side]
    c = V((0.215 * s, 0.0, 1.522))
    P = [K.box((0.12, 0.11, 0.016), loc=c, rot=(0, -12 * s, 0), swatch=color, bevel=0.006)]
    for k in range(6):
        a = math.radians(-60 + k * 24)
        p = c + V((0.06 * s * math.cos(a) + 0.01 * s, 0.055 * math.sin(a), -0.03))
        P.append(K.cyl(0.0055, 0.045, loc=p, swatch=fringe, segments=4))
    for p in P:
        p.bone = "Shoulder." + side
    return P


def shield_back():
    c = V((0, 0.27, 1.26))
    n = V((0, 1, 0.18)).normalized()
    rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
    P = [K.cyl(0.25, 0.022, swatch="wood_plank", segments=16),
         L.ring(V((0, 0, 0.0)), V((0, 0, 1)), 0.25, (0.012, 0.016), segs=16, sides=4, swatch="metal_worn"),
         K.ball(0.055, (0, 0, 0.012), scale=(1, 1, 0.6), swatch="metal_worn", segments=8, rings=4)]
    for x in (-0.08, 0.08):
        P.append(K.box((0.012, 0.47, 0.006), loc=(x, 0, 0.013), swatch="wood_dark"))
    for p in P:
        L.xform(p, Matrix.Translation(c) @ rot)
        p.bone = "Chest"
    return P


def regulator_harness(body):
    """A stolen REGEN R-3 Regulator harness: white spine plate with three cyan diodes, straps, cables."""
    P = []

    def plate(u, v):
        x = (u - 0.5) * 0.12 * (1.0 - 0.25 * v)
        z = 1.49 - 0.37 * v
        loc, n = body.hit(V((x, 0, z)), V((0, 1, 0)))
        return loc + V((0, 0.028, 0))
    P.append(L.weighted(L.slab(plate, 3, 6, 0.018, swatch="regen_white"), L.trunk_weights(skel)))
    for z in (1.43, 1.33, 1.23):
        loc, n = body.hit(V((0, 0, z)), V((0, 1, 0)))
        base = loc + V((0, 0.039, 0))
        P.append(L.weighted(K.cyl(0.02, 0.008, loc=base, rot=(-90, 0, 0), swatch="regen_grey", segments=8), L.trunk_weights(skel)))
        P.append(L.weighted(K.cyl(0.012, 0.01, loc=base + V((0, 0.005, 0)), rot=(-90, 0, 0), swatch="cyan", segments=8), L.trunk_weights(skel)))
    for s in (1, -1):
        pts = [V((0.045 * s, 0.17, 1.47)), V((0.09 * s, 0.12, 1.515)), V((0.12 * s, 0.0, 1.52)), V((0.13 * s, -0.11, 1.46)),
               V((0.14 * s, -0.15, 1.36)), V((0.17 * s, -0.12, 1.27)), V((0.2 * s, -0.02, 1.24)), V((0.16 * s, 0.11, 1.25)), V((0.05 * s, 0.165, 1.26))]
        P.append(L.weighted(L.tube(pts, 0.013, sides=4, squash=0.35, swatch="strap_black"), L.trunk_weights(skel)))
        cab = [V((0.04 * s, 0.165, 1.14)), V((0.1 * s, 0.15, 1.08)), V((0.15 * s, 0.1, 1.02)), V((0.17 * s, 0.02, 1.0))]
        P.append(L.weighted(L.tube(cab, 0.008, sides=5, swatch="rubber_black"), L.trunk_weights(skel)))
    return P


def papr():
    """Hazmat: blower/filter unit on the back with a corrugated hose up to the hood."""
    P = [K.box((0.24, 0.1, 0.17), loc=(0, 0.215, 1.15), swatch="military_green", bevel=0.02)]
    for s in (1, -1):
        P.append(K.cyl(0.045, 0.05, loc=(0.135 * s, 0.215, 1.15), rot=(0, 90, 0), swatch="metal_dark", segments=10))
    for p in P:
        p.bone = "Chest"
    hose = L.tube([V((0.05, 0.25, 1.24)), V((0.06, 0.27, 1.36)), V((0.05, 0.23, 1.5)), V((0.03, 0.16, 1.59)), V((0.0, 0.11, 1.64))],
                  0.02, sides=6, swatch="rubber_black")

    def w(co):
        if co.z < 1.45:
            return {"Chest": 1.0}
        k = L.ease((co.z - 1.45) / 0.18)
        return {"Chest": 1 - k, "Head": k}
    P.append(L.weighted(hose, w))
    return P


def hazmat_head():
    """The hood (the head grown 2 cm, yellow) with a full face mask, visor and side filter."""
    hd = head_mesh("plastic_yellow")
    hb = L.Body(hd, skel)
    hood = hb.shell(lambda c, n, b: True, 0.02, "plastic_yellow", bone="Head", walls=False)

    def face(c, s):
        if c.y < HC.y - 0.055 and 1.565 < c.z < 1.725:
            return "g25"
        return None
    L.recolor(hood, face)
    P = [hood]
    hb2 = L.Body(hood, skel)
    P.append(hb2.shell(lambda c, n, b: c.y < HC.y - 0.07 and 1.645 < c.z < 1.71 and abs(c.x) < 0.075, 0.01, "glass_dark", bone="Head"))
    P.append(K.cyl(0.04, 0.06, loc=(0.055, HC.y - 0.105, 1.6), rot=(70, 0, 30), swatch="military_green", segments=10))
    P.append(K.cyl(0.032, 0.012, loc=(0.068, HC.y - 0.13, 1.592), rot=(70, 0, 30), swatch="metal_dark", segments=10))
    P.append(K.cyl(0.022, 0.02, loc=(-0.01, HC.y - 0.118, 1.595), rot=(80, 0, 0), swatch="metal_dark", segments=8))
    for p in P:
        p.bone = "Head"
    return P


def mohawk(color="red_4", seed=5):
    P = []
    rnd = random.Random(seed)
    n = 9
    for i in range(n):
        t = i / (n - 1)
        a = math.radians(24 + 142 * t)
        base = V((0, HC.y - math.cos(a) * 0.098, HC.z + math.sin(a) * 0.1 + 0.002))
        d = (base - HC).normalized() + V((0, 0.35, 0.15))
        ln = 0.075 + 0.04 * math.sin(t * math.pi) + rnd.random() * 0.012
        P.append(K.shard(ln, 0.022, base, d, swatch=color, bone="Head", seed=seed + i, sides=4))
    return P


def spikes(body, spots, swatch="steel_bright", h=0.018, r=0.007):
    P = []
    for target, outward in spots:
        loc, nrm = body.hit(target, outward)
        c = loc + nrm * (h * 0.4 + 0.004)
        p = K.cyl(r, h, loc=(0, 0, 0), swatch=swatch, segments=5, radius2=0.0015)
        q = V((0, 0, 1)).rotation_difference(nrm)
        p.verts = [c + q @ v for v in p.verts]
        P.append(p)
    return P


def lapels(body, color, z0=1.1, z1=1.47, w=0.05, off=0.01):
    """Two strips along a coat's front opening."""
    return [body.shell(lambda c, n, b, s=s: b in ("Spine", "Chest") and z0 < c.z < z1 and c.y < -0.02 and
                       0.03 < s * c.x < 0.03 + w + (c.z - z0) * 0.12, off, color) for s in (1, -1)]


def collar_ring(color, R=0.085, r=(0.03, 0.035), z=1.5, y=0.012, tilt=0.25):
    return L.ring(V((0, y, z)), V((0, tilt, 1)), R, r, segs=14, sides=5, swatch=color, bone="Chest")


def belt(body, color="leather_black", z0=0.955, z1=1.005, buckle="steel", off=0.012):
    P = [body.shell(lambda c, n, b: b in ("Hips", "Spine") and z0 < c.z < z1, off, color)]
    loc, n = body.hit(V((0, 0, (z0 + z1) / 2)), V((0, -1, 0)))
    P.append(K.box((0.05, 0.012, 0.04), loc=loc + V((0, -off - 0.006, 0)), swatch=buckle, bevel=0.004, bone="Hips"))
    return P


def medic_pack():
    P = [K.box((0.27, 0.13, 0.3), loc=(0, 0.225, 1.3), swatch="green_2", bevel=0.03, taper=(0.95, 0.92))]
    for (sz, loc) in (((0.11, 0.012, 0.034), (0, 0.296, 1.3)), ((0.034, 0.012, 0.11), (0, 0.296, 1.3)),
                      ((0.1, 0.03, 0.012), (0, 0.225, 1.455)), ((0.03, 0.1, 0.012), (0, 0.225, 1.455))):
        P.append(K.box(sz, loc=loc, swatch="chalk"))
    for p in P:
        p.bone = "Chest"
    return P


def knee_pads(body, color="rubber_black"):
    return [body.shell(lambda c, n, b: b in ("UpperLeg.L", "UpperLeg.R", "LowerLeg.L", "LowerLeg.R") and 0.44 < c.z < 0.6 and c.y < -0.035, 0.014, color)]


def forearm_ring(side, t, r_add, size, color):
    """A band around a forearm (bracer, taped cuff, studded wristband)."""
    E, W = J["elbow." + side], J["wrist." + side]
    return L.ring(E.lerp(W, t), (W - E).normalized(), r_add, size, segs=10, sides=4, swatch=color, bone="LowerArm." + side)


def chest_v(body, color, z0=1.08, z1=1.47, w=0.13, edge=None):
    """What shows in an open jacket: a V-shaped piece laid on the chest, with lapel edges."""
    def half(v):
        return (w * (1 - v) + 0.012) / 2

    def fn(u, v):
        z = z1 - (z1 - z0) * v
        loc, n = body.hit(V(((u - 0.5) * 2 * half(v), 0, z)), V((0, -1, 0)))
        return loc + n * 0.004
    P = [L.weighted(L.slab(fn, 2, 5, 0.006, swatch=color), L.trunk_weights(skel))]
    if edge:
        for sd in (1, -1):
            pts = []
            for v in (0.0, 0.25, 0.5, 0.75, 1.0):
                loc, n = body.hit(V((sd * (half(v) + 0.008), 0, z1 - (z1 - z0) * v)), V((0, -1, 0)))
                pts.append(loc + n * 0.007)
            P.append(L.weighted(L.tube(pts, 0.014, sides=4, squash=0.45, swatch=edge), L.trunk_weights(skel)))
    return P


def sash(body, color="red_3", width=0.045):
    """A flat parade sash over the right shoulder to the left hip (front and back), knotted at the hip."""
    P = []
    for side, d in (("front", (0, -1, 0)), ("back", (0, 1, 0))):
        sy = -1 if side == "front" else 1
        path = [(-0.15, 1.535), (-0.11, 1.4), (-0.03, 1.28), (0.06, 1.16), (0.13, 1.06), (0.165, 1.01)]
        pts, nrm = [], []
        for x, z in path:
            loc, n = body.hit(V((x, 0, z)), V(d) if z < 1.5 else V((0, 0.4 * sy, 1)))
            pts.append(loc + n * 0.016)
            nrm.append(n)
        ref = (pts[1] - pts[0]).normalized().cross(nrm[1])
        P.append(L.weighted(L.tube(pts, width, sides=4, squash=0.25, swatch=color, ref=ref), L.trunk_weights(skel)))
    loc, n = body.hit(V((0.17, 0, 1.0)), V((1, -0.4, 0)))
    P.append(L.weighted(K.ball(0.035, loc + n * 0.02, scale=(0.8, 1.0, 1.1), swatch=color, segments=8, rings=5), L.trunk_weights(skel)))
    for k, (dx, dz) in enumerate(((0.01, -0.09), (0.035, -0.075))):
        P.append(L.weighted(K.box((0.04, 0.012, 0.11), loc=loc + n * 0.02 + V((dx, -0.01, dz)), rot=(0, -8 + 14 * k, 0), swatch=color, bevel=0.004), L.trunk_weights(skel)))
    return P


def patch_disc(body, at, outward, r=0.065, color="bandana_red", ring=None):
    loc, n = body.hit(at, outward)
    p = K.cyl(r, 0.006, loc=(0, 0, 0), swatch=color, segments=12)
    q = V((0, 0, 1)).rotation_difference(n)
    c = loc + n * 0.005
    p.verts = [c + q @ v for v in p.verts]
    return L.weighted(p, L.trunk_weights(skel))


# =============================================================================================
# Skins
# =============================================================================================

def base_parts(body, proxy, spec, head_kw, neck=None, seed=1):
    parts = [paint_body(body, spec, seed=seed), neck_tube(neck or spec["skin"])]
    parts += head_parts(spec["skin"], **head_kw)
    parts += [soles(proxy, spec.get("sole", "rubber_black"))]
    return parts


def skin_passeur():
    """Survivant: hoodie under a cheap plate carrier, cargo pants, hiking shoes, fingerless gloves, gas mask at
    the neck, small backpack with a rolled mat. Visible face, short hair."""
    body, proxy = make_body(cuts=seams(hem=0.975, shoe=0.14, glove=0.6))
    spec = dict(skin="skin_light", top="hoodie_charcoal", pants="cargo_khaki", hem_z=0.975,
                hand=[(0.6, "glove_black"), (1.0, "skin_light")], shoe="boot_brown", shoe_z=0.14)
    parts = base_parts(body, proxy, spec, dict(hair="hair_brown", style="short"))
    parts += [band(proxy, 0.95, 1.0, "hoodie_charcoal", off=0.012), hood_down("hoodie_charcoal")]
    parts += plate_carrier(proxy, color="vest_khaki", pouch="plate_carrier")
    parts += gas_mask()
    parts += pack() + pack_straps()
    parts += cargo_pockets(body, "cargo_khaki", flap="cargo_grey")
    return body, parts


def skin_camo():
    """Cordon army surplus (bought at the Guichets): camo jacket and trousers, patrol cap, olive carrier with
    pouches, knee pads, black boots, tactical gloves, big olive pack."""
    body, proxy = make_body(cuts=seams(hem=0.975, shoe=0.2))
    spec = dict(skin="skin_mid", top="green_2", pants="green_2", hem_z=0.975, hand=[(1.0, "glove_black")],
                shoe="shoe_dark", shoe_z=0.2, extra=lambda c, s, key, t: camo(c, 1) if s == "green_2" else None)
    parts = base_parts(body, proxy, spec, dict(hair="hair_dark", style="cap", beard="stubble"))
    parts += patrol_cap(seed=3)
    parts += [camo_part(band(proxy, 0.95, 1.0, "green_2", off=0.012), 4)]
    parts += plate_carrier(proxy, color="canvas_olive", pouch="green_1")
    parts += gas_mask()
    parts += pack(color="canvas_olive", pocket="green_1", mat="tarp_green", depth=0.15, w=0.32, h=0.38) + pack_straps("green_1")
    parts += [camo_part(p, 5) for p in cargo_pockets(body, "green_2")]
    parts += knee_pads(proxy)
    return body, parts


def skin_sherif():
    """An Ostrov police uniform (pale blue shirt, navy trousers, duty belt with the empty holster of the very
    revolver he holds) + the Maskarada sheriff star and cowboy hat. A moustache comes with the hat."""
    body, proxy = make_body(cuts=seams(hem=0.985, shoe=0.12))
    spec = dict(skin="skin_light", top="shirt_blue_faded", pants="coat_navy", hem_z=0.985, shoe="shoe_dark", shoe_z=0.12)
    parts = base_parts(body, proxy, spec, dict(hair="hair_brown", style="cap", beard="shadow"))
    parts += cowboy_hat()
    parts.append(K.box((0.06, 0.016, 0.014), loc=(0, HC.y - 0.096, 1.626), swatch="hair_brown", bevel=0.005, bone="Head"))
    parts += belt(body, "leather_black", buckle="steel")
    parts.append(K.box((0.05, 0.075, 0.13), loc=(-0.188, 0.0, 0.93), rot=(0, 6, 0), swatch="leather_black", bevel=0.01, bone="Hips"))
    parts.append(K.box((0.04, 0.03, 0.05), loc=(0.17, -0.06, 0.975), swatch="leather_black", bevel=0.006, bone="Hips"))
    for s in (1, -1):   # breast pockets with flaps, navy shoulder straps
        loc, n = body.hit(V((0.085 * s, 0, 1.34)), V((0, -1, 0)))
        parts.append(L.weighted(K.box((0.075, 0.018, 0.085), loc=loc + V((0, -0.01, 0)), swatch="shirt_blue_faded", bevel=0.006), L.trunk_weights(skel)))
        parts.append(L.weighted(K.box((0.08, 0.024, 0.024), loc=loc + V((0, -0.013, 0.04)), swatch="shirt_blue_faded", bevel=0.004), L.trunk_weights(skel)))
        parts.append(K.box((0.13, 0.045, 0.012), loc=(0.15 * s, 0.0, 1.515), rot=(0, -14 * s, 0), swatch="coat_navy", bevel=0.004, bone="Shoulder." + ("L" if s > 0 else "R")))
    parts.append(collar_ring("shirt_blue_faded", R=0.075, r=(0.022, 0.02), z=1.51))
    loc, n = body.hit(V((0.1, 0, 1.36)), V((0.15, -1, 0)))
    parts.append(star_badge(loc + n * 0.012, n))
    return body, parts


def skin_punk():
    """The squat scene of Ostrov-Centre: red crest on a shaved head, studded black leather jacket over a white
    tee, ripped jeans, tall boots, studded belt and wallet chain."""
    body, proxy = make_body(cuts=seams(hem=0.975, shoe=0.27, glove=0.6))

    def rips(c, s, key, t):
        if key in ("UpperLeg", "LowerLeg") and s == "jeans_faded":
            if perlin(c * 9.0 + V((2.0, 5.0, 1.0))) > 0.42 and c.y < 0.02:
                return "skin_light"
        return None
    spec = dict(skin="skin_light", top="leather_black", pants="jeans_faded", hem_z=0.975, shoe="leather_black", shoe_z=0.27,
                hand=[(0.6, "glove_black"), (1.0, "skin_light")], extra=rips)
    parts = base_parts(body, proxy, spec, dict(hair="red_4", style="mohawk", beard="stubble", brow="hair_dark"))
    parts += mohawk("red_4")
    parts.append(collar_ring("leather_black", R=0.09, r=(0.032, 0.03), z=1.49, y=0.02))
    parts += chest_v(body, "shirt_white_dirty", z0=1.1, z1=1.46, w=0.12, edge="leather_black")
    parts += [band(body, 0.955, 1.0, "leather_black", off=0.01)]
    spots = []
    for s in (1, -1):
        for k in range(4):
            spots.append((V((0.24 * s + 0.02 * s * k, -0.04 + 0.03 * k, 1.5 - 0.012 * k)), V((0.3 * s, 0, 1))))
    for k in range(10):
        a = math.tau * k / 10
        spots.append((V((0.09 * math.cos(a), 0, 1.29 + 0.09 * math.sin(a))), V((0, 1, 0))))
    parts += spikes(body, spots)
    parts.append(patch_disc(body, V((0, 0, 1.29)), V((0, 1, 0)), r=0.07))
    parts += belt(body, "leather_black", z0=0.93, z1=0.975, buckle="steel_bright")
    for k in range(8):
        a = -0.9 + k * 0.26
        loc, n = body.hit(V((0.17 * math.sin(a), 0, 0.952)), V((math.sin(a), -math.cos(a), 0)))
        parts.append(K.box((0.012, 0.012, 0.012), loc=loc + n * 0.016, swatch="steel_bright", bone="Hips"))
    chain = [V((-0.14, -0.09, 0.95)), V((-0.17, -0.04, 0.87)), V((-0.18, 0.04, 0.85)), V((-0.15, 0.1, 0.9))]
    parts.append(L.tube(chain, 0.004, sides=4, swatch="steel", bone="Hips"))
    parts.append(forearm_ring("R", 0.92, 0.05, (0.006, 0.014), "leather_black"))
    return body, parts


def skin_medic():
    """A North-wing hospital lab coat (knee length, open) over green scrubs, stethoscope, blue nitrile gloves,
    glasses, sneakers, a medic pack."""
    body, proxy = make_body(cuts=seams(hem=0.975, shoe=0.1, glove=0.02))

    spec = dict(skin="skin_light", top="tile_white", pants="curtain_green", hem_z=0.975, shoe="sneaker_white", shoe_z=0.1,
                hand=[(1.0, "plastic_blue")])
    parts = base_parts(body, proxy, spec, dict(hair="hair_dark", style="short", beard=None), seed=2)
    parts += glasses()
    parts += chest_v(body, "curtain_green", z0=1.02, z1=1.47, w=0.12, edge="tile_white")
    parts += stethoscope(body)
    parts.append(collar_ring("tile_white", R=0.085, r=(0.026, 0.03), z=1.5, y=0.02))
    for s in "LR":
        parts.append(skirt(s, 1.03, 0.5, "tile_white", lining="shirt_white_dirty"))
    loc, n = body.hit(V((0.1, 0, 1.33)), V((0, -1, 0)))
    parts.append(L.weighted(K.box((0.075, 0.014, 0.08), loc=loc + V((0, -0.006, 0)), swatch="tile_white", bevel=0.004), L.trunk_weights(skel)))
    for x, col in ((0.085, "plastic_blue"), (0.1, "red_3"), (0.114, "metal_dark")):
        parts.append(L.weighted(K.box((0.008, 0.008, 0.05), loc=loc + V((x - 0.1, -0.02, 0.04)), swatch=col), L.trunk_weights(skel)))
    parts += medic_pack()
    parts += pack_straps("strap_black", x=0.1)
    return body, parts


def skin_king():
    """'Roi du camp': a costume from the reserve of the Theatre d'Ostrov. Brass crown with a few gold tips, red
    cape with an ermine collar, wine doublet, belt with a gold buckle, tall boots."""
    body, proxy = make_body(cuts=seams(hem=0.86, shoe=0.34, glove=0.02))
    spec = dict(skin="skin_light", top="sweater_wine", pants="track_black", hem_z=0.86, shoe="leather_brown", shoe_z=0.34,
                hand=[(1.0, "leather_tan")])
    parts = base_parts(body, proxy, spec, dict(hair="hair_brown", style="short", beard="stubble"))
    parts += crown()
    parts.append(cape("bandana_red", lining="red_2"))
    col = collar_ring("apron_white", R=0.135, r=(0.05, 0.04), z=1.505, y=0.03, tilt=0.15)
    L.recolor(col, lambda c, s: "hair_dark" if perlin(c * 40.0) > 0.45 else None)
    parts.append(col)
    for s in "LR":
        parts.append(skirt(s, 1.0, 0.74, "sweater_wine", rx=(0.18, 0.215), ry=(0.145, 0.175), thick=0.01, nv=2))
    parts += belt(body, "leather_brown", z0=0.97, z1=1.02, buckle="gold")
    for s in ("L", "R"):
        parts.append(proxy.shell(lambda c, n, b, s=s: b == "LowerLeg." + s and 0.32 < c.z < 0.37, 0.012, "leather_brown"))
    return body, parts


def skin_viking():
    """Maskarada's Halloween aisle: horned helmet, fur mantle, a braided fake beard, brown tunic, leg wraps,
    round shield on the back."""
    body, proxy = make_body(cuts=seams(hem=0.8, shoe=0.16, glove=0.6))

    def wraps(c, s, key, t):
        if key == "LowerLeg" and 0.16 < c.z < 0.42 and int(c.z / 0.045) % 2 == 0:
            return "canvas_tan"
        return None
    spec = dict(skin="skin_light", top="leather_brown", pants="rag", hem_z=0.8, shoe="boot_brown", shoe_z=0.16,
                hand=[(0.6, "leather_brown"), (1.0, "skin_light")], extra=wraps)
    parts = base_parts(body, proxy, spec, dict(hair="brown_3", style="cap", beard="full", brow="brown_3"))
    parts += viking_helmet()
    hd = L.Body(head_mesh("brown_3"), skel)
    beard = hd.shell(lambda c, n, b: c.z < 1.632 and c.y < HC.y + 0.02, lambda p: 0.012 + 0.008 * perlin(p * 50.0), "brown_3", bone="Head")
    beard.verts = [v + V((0, -0.004, -0.012)) * max(0.0, (1.63 - v.z) / 0.07) for v in beard.verts]
    parts.append(beard)
    for s in (1, -1):
        br = [V((0.03 * s, HC.y - 0.09, 1.56)), V((0.034 * s, HC.y - 0.1, 1.52)), V((0.03 * s, HC.y - 0.105, 1.48)), V((0.028 * s, HC.y - 0.105, 1.45))]
        parts.append(L.tube(br, [0.016, 0.014, 0.012, 0.009], sides=5, swatch="brown_3", bone="Head"))
        parts.append(L.ring(br[2], V((0, 0, 1)), 0.013, 0.004, segs=6, sides=4, swatch="metal_worn", bone="Head"))
    fur = proxy.shell(lambda c, n, b: b in ("Chest", "Shoulder.L", "Shoulder.R") and c.z > 1.38 and (c.y > -0.06 or abs(c.x) > 0.12),
                      lambda p: 0.03 + 0.02 * perlin(p * 22.0), "hair_brown")
    L.recolor(fur, lambda c, s: "hair_dark" if perlin(c * 13.0 + V((4, 4, 4))) > 0.3 else None)
    parts.append(fur)
    for s in "LR":
        parts.append(skirt(s, 0.99, 0.66, "leather_brown", rx=(0.18, 0.225), ry=(0.145, 0.18), thick=0.01, nv=2))
    parts += belt(body, "leather_black", z0=0.97, z1=1.03, buckle="brass")
    parts += shield_back()
    parts.append(forearm_ring("R", 0.7, 0.056, (0.008, 0.04), "leather_brown"))
    return body, parts


def skin_hazmat():
    """A REAL Cordon NRBC suit (traded for a lot of gold): baggy yellow suit, hood, full mask with a side filter
    and visor, powered filter unit on the back with a hose, rubber gloves and boots, taped cuffs.
    (The left forearm is not inflated: the injector straps it tight, same Vial position on every skin.)"""
    inflate = {"Spine": 0.022, "Chest": 0.02, "Hips": 0.024, "Shoulder": 0.018, "UpperArm": 0.014, "UpperLeg": 0.018, "LowerLeg": 0.016}
    body, proxy = make_body(inflate=inflate, cuts=seams(shoe=0.3, glove=0.02))
    spec = dict(skin="skin_light", top="plastic_yellow", pants="plastic_yellow", hem_z=0.0, shoe="rubber_black", shoe_z=0.3,
                hand=[(1.0, "rubber_black")])
    parts = [paint_body(body, spec, seed=3), neck_tube("plastic_yellow", radius=0.075), soles(proxy)]
    parts += hazmat_head()
    parts += papr()
    parts += belt(body, "strap_black", z0=1.0, z1=1.05, buckle="metal_dark", off=0.012)
    for s in ("L", "R"):
        parts.append(proxy.shell(lambda c, n, b, s=s: b == "LowerLeg." + s and 0.29 < c.z < 0.335, 0.012, "g25"))
    parts.append(forearm_ring("R", 0.9, 0.05, (0.006, 0.016), "g25"))
    loc, n = proxy.hit(V((0.11, 0, 1.36)), V((0.2, -1, 0)))
    parts.append(L.weighted(K.box((0.07, 0.01, 0.05), loc=loc + n * 0.012, swatch="hazard_black", bevel=0.003), L.trunk_weights(skel)))
    return body, parts


def skin_angel():
    """Maskarada again: a white costume tunic, a battery LED halo on a spring wire, small feathered wings."""
    body, proxy = make_body(cuts=seams(hem=0.6, shoe=0.1))
    spec = dict(skin="skin_light", top="shirt_white_dirty", pants="apron_white", hem_z=0.6, shoe="sneaker_white", shoe_z=0.1)
    parts = base_parts(body, proxy, spec, dict(hair="hair_brown", style="short", beard=None), seed=4)
    parts += halo()
    parts += wings()
    for s in "LR":
        parts.append(skirt(s, 1.0, 0.6, "shirt_white_dirty", rx=(0.18, 0.235), ry=(0.145, 0.19), a0=4, a1=178, thick=0.01, nv=3))
    parts += belt(body, "canvas_tan", z0=0.98, z1=1.02, buckle="canvas_tan", off=0.01)
    parts.append(collar_ring("shirt_white_dirty", R=0.08, r=(0.022, 0.02), z=1.505))
    return body, parts


def skin_demon():
    """Maskarada's adult aisle: plastic horns, black and dark red costume, a high pointed collar, a costume
    tail, and the 'ember aura' = a small lantern hanging at the neck (fire_orange)."""
    body, proxy = make_body(cuts=seams(hem=0.975, shoe=0.25, glove=0.02))

    spec = dict(skin="skin_light", top="red_1", pants="track_black", hem_z=0.975, shoe="leather_black", shoe_z=0.25,
                hand=[(1.0, "glove_black")])
    parts = base_parts(body, proxy, spec, dict(hair="hair_dark", style="short", beard="stubble"))
    parts += demon_horns()
    parts += lantern()
    parts += demon_tail()

    def collar(u, v):
        a = math.radians(-125 + 250 * u)
        r = 0.1 + 0.07 * v
        tip = 0.04 * v * (math.cos(3 * a) * 0.5 + 0.5)
        return V((r * math.sin(a), 0.035 + r * math.cos(a) * 0.9, 1.48 + 0.16 * v + tip))
    parts.append(L.slab(collar, 10, 2, 0.01, swatch="red_2", bone="Chest"))
    parts += belt(body, "leather_black", z0=0.955, z1=1.0, buckle="metal_dark")
    return body, parts


def skin_neon():
    """'Greffe': a stolen REGEN Regulator harness on the back (white spine plate, three cyan diodes, straps,
    cables) over a dark sleeveless compression top; shaved head; dark trousers and boots."""
    body, proxy = make_body(cuts=seams(hem=0.975, shoe=0.18, glove=0.6))
    spec = dict(skin="skin_light", top="blue_1", pants="track_black", hem_z=0.975, shoe="shoe_dark", shoe_z=0.18,
                sleeve=[(0.18, "blue_1"), (1.0, "skin_light")], lower=[(1.0, "skin_light")],
                hand=[(0.6, "glove_black"), (1.0, "skin_light")])
    parts = base_parts(body, proxy, spec, dict(hair="stubble", style="none", beard="shadow"))
    parts += regulator_harness(proxy)
    parts += [band(proxy, 0.955, 1.0, "track_black", off=0.012)]
    parts.append(collar_ring("blue_1", R=0.07, r=(0.016, 0.016), z=1.5))
    return body, parts


def skin_founder():
    """The parade outfit of the Ostrov Opera, worn by the man who founded the Relais: long navy coat with a red
    lining, gold epaulettes and a few gold buttons, red sash, white gloves and breeches, tall boots, bicorne."""
    body, proxy = make_body(inflate={"Spine": 0.008, "Chest": 0.008}, cuts=seams(hem=0.975, shoe=0.44, glove=0.02))
    spec = dict(skin="skin_light", top="coat_navy", pants="apron_white", hem_z=0.975, shoe="leather_black", shoe_z=0.44,
                hand=[(1.0, "apron_white")], lower=[(0.9, "coat_navy"), (1.0, "red_3")])
    parts = base_parts(body, proxy, spec, dict(hair="hair_grey", style="cap", beard="shadow", brow="hair_grey"), seed=6)
    parts += bicorne()
    parts += epaulette("L") + epaulette("R")
    for s in "LR":
        parts.append(skirt(s, 1.03, 0.52, "coat_navy", lining="red_3", a0=12))
    parts += sash(body)
    for s in (1, -1):
        for z in (1.38, 1.28, 1.18):
            loc, n = body.hit(V((0.06 * s, 0, z)), V((0, -1, 0)))
            parts.append(L.weighted(K.box((0.016, 0.012, 0.016), loc=loc + n * 0.005, swatch="gold", bevel=0.004), L.trunk_weights(skel)))
    col = collar_ring("coat_navy", R=0.08, r=(0.022, 0.04), z=1.53, y=0.02)
    L.recolor(col, lambda c, s: "brass" if c.z > 1.555 else None)
    parts.append(col)
    parts += belt(body, "apron_white", z0=0.96, z1=1.0, buckle="brass", off=0.014)
    return body, parts


SKINS = [("Passeur", skin_passeur), ("Passeur_Camo", skin_camo), ("Passeur_Sherif", skin_sherif), ("Passeur_Punk", skin_punk),
         ("Passeur_Medic", skin_medic), ("Passeur_King", skin_king), ("Passeur_Viking", skin_viking), ("Passeur_Hazmat", skin_hazmat),
         ("Passeur_Angel", skin_angel), ("Passeur_Demon", skin_demon), ("Passeur_Neon", skin_neon), ("Passeur_Founder", skin_founder)]


# =============================================================================================
# Build
# =============================================================================================

char = K.Character("Passeur", skel)
objs = []
variant_parts = {}
vial_axis = None
muzzle = None
for name, fn in SKINS:
    if ONLY and name not in ONLY and name != "Passeur":
        continue
    body, parts = fn()
    if vial_axis is None:   # one injector for every skin: the Vial bone sits at the same place on all of them
        INJ, vial_axis = injector(body)
        GUN, muzzle, barrel = revolver()
    inj, gun = INJ, GUN
    parts = parts + inj + gun
    variant_parts[name] = parts
    if not objs:
        char.parts = parts
        char.build(ao=0.5, grime=0.18, seed=7)
        char.mesh_obj.name = name
        o = char.mesh_obj
        L.add_bones(char.rig, [
            ("Vial", vial_axis[0], vial_axis[1], "LowerArm.L"),
            ("Muzzle", muzzle, muzzle + barrel * 0.08, "Hand.R"),
            ("Cape", V((0, 0.16, 1.47)), V((0, 0.2, 0.98)), "Chest"),
            ("Lantern", V((0, -0.165, 1.47)), V((0, -0.19, 1.33)), "Chest"),
            ("Halo", V((0, 0.1, 1.64)), V((0, 0.07, 1.86)), "Head"),
            ("Tail", V((0, 0.15, 1.0)), V((0, 0.33, 0.8)), "Hips"),
        ])
    else:
        o = char.add_variant(name, parts, ao=0.5, grime=0.18, seed=7)
    L.apply_weights(o, parts, skel)
    objs.append(o)
    K.report(asset=name, tris=K.tri_count(o))
    if os.environ.get("PASSEUR_DEBUG"):
        for i, p in enumerate(parts):
            if not p.faces:
                print("CORDON EMPTY part", i)
        rows = sorted(((L.tris([p]), i, p.swatch[0] if p.faces else "-", p.bone or getattr(p, "wspec", "auto")) for i, p in enumerate(parts)), reverse=True)
        print("CORDON parts", name, " | ".join("%d:%s/%s=%d" % (i, s, str(b)[:12], t) for t, i, s, b in rows[:18]))

# =============================================================================================
# Animation (armature-space poses, IK legs and hands)
# =============================================================================================

R = L.RigInfo(char)
RUN_SPEED = 5.2           # m/s, the game's speed: the stance foot moves back at exactly this speed in root space
RUN_FRAMES = 18           # 0.6 s cycle
HEEL0 = {s: V((J["ankle." + s].x, 0.075, 0.0)) for s in "LR"}
BALL0 = {s: V((J["ankle." + s].x, -0.1, 0.0)) for s in "LR"}
ANK0 = {s: J["ankle." + s].copy() for s in "LR"}
SX = {"L": 1.0, "R": -1.0}


def foot_q(pitch, yaw=0.0):
    """Absolute foot rotation: pitch > 0 = toe down (heel up), yaw > 0 = toe out (degrees)."""
    return L.rz(yaw) @ L.rx(pitch)


TOE0 = {s: V((J["ankle." + s].x, -0.2, 0.0)) for s in "LR"}


def foot_on_ground(s, shift, pitch, yaw=0.0, pivot=None):
    """Ankle position for a foot translated by `shift` (rest frame) and pitched on its heel (toe up), its
    ball (heel up) or its toe tip (pivot='toe', kneeling), so the contact point never slides."""
    q = foot_q(pitch, yaw)
    piv = (TOE0 if pivot == "toe" else HEEL0 if pitch < 0 else BALL0)[s]
    return piv + shift + q @ (ANK0[s] - piv), q


def leg_ik(P, s, ankle, q, knee_out=0.12, pole=None):
    pole = V(pole) if pole is not None else V((knee_out * SX[s], -1, 0))
    L.two_bone(P, "UpperLeg." + s, "LowerLeg." + s, ankle, pole, V((0, -1, 0)))
    P.abs("Foot." + s, q)
    if not hasattr(P, "legs"):
        P.legs = {}
    P.legs[s] = (V(ankle), q.copy(), pole.normalized())


def hand_frame_q(side, axis, grip):
    """Absolute Hand rotation taking the rest (hand axis, grip side) onto (axis, grip)."""
    a0 = R.dir("Hand." + side)
    g0 = V((0, 1, 0))
    return L.aim(a0, g0, axis, grip)


def share_twist(P, side, k=0.5):
    """Moves part of the wrist twist into the forearm (no candy-wrapper wrist)."""
    lo, ha = "LowerArm." + side, "Hand." + side
    Dl, Dh = P.d(lo), P.d(ha)
    rel = Dl.inverted() @ Dh
    ax = R.dir(lo)
    p = V((rel.x, rel.y, rel.z))
    proj = ax * p.dot(ax)
    tw = Quaternion((rel.w, proj.x, proj.y, proj.z))
    if tw.magnitude < 1e-6:
        return
    tw.normalize()
    if tw.w < 0:
        tw.negate()
    part = Quaternion(ax, tw.angle * k) if V((tw.x, tw.y, tw.z)).dot(ax) >= 0 else Quaternion(ax, -tw.angle * k)
    P.abs(lo, Dl @ part)
    P.abs(ha, Dh)


def arm_ik(P, side, wrist, pole, hand_q=None, twist_share=0.5):
    L.two_bone(P, "UpperArm." + side, "LowerArm." + side, wrist, pole, V((0, 1, 0)))
    if hand_q is not None:
        P.abs("Hand." + side, hand_q)
        if twist_share:
            share_twist(P, side, twist_share)


def chest_pt(P, p):
    """A point given in rest coordinates that moves with the chest."""
    return P.point("Chest", p)


def gun_hand_q(P, barrel, cant=0.0):
    """Right hand orientation for a barrel direction; the grip hangs below the barrel (cant rolls it)."""
    b = V(barrel).normalized()
    down = V((0, 0, -1))
    g = (down - b * down.dot(b)).normalized()
    if cant:
        g = Quaternion(b, math.radians(cant)) @ g
    return hand_frame_q("R", b, g)


def torso(P, hips_off, hips_q, spine=(0, 0, 0), chest=(0, 0, 0), neck=(0, 0, 0), head=None, shoulders=(0, 0)):
    P.offset["Hips"] = V(hips_off)
    P.abs("Hips", hips_q)
    P.rel("Spine", L.rot(*spine))
    P.rel("Chest", L.rot(*chest))
    P.rel("Neck", L.rot(*neck))
    if head is not None:
        P.abs("Head", head)
    for s, a in zip("LR", shoulders):
        if a:
            P.rel("Shoulder." + s, L.rot(y=-a * SX[s]))


def relaxed_left(P, wrist, pole=V((0.5, 1.0, -0.3)), bend=12.0, roll=0.0):
    """Left (injector) arm by IK with a relaxed hand continuing the forearm."""
    L.two_bone(P, "UpperArm.L", "LowerArm.L", wrist, pole, V((0, 1, 0)), twist=roll)
    P.rel("Hand.L", L.rot(x=-bend * 0.4, y=bend * 0.3))


# ---------------- Run ----------------

def run_leg(phase, s):
    """Ankle target + foot rotation for one leg at its own phase (0 = heel strike)."""
    T = RUN_FRAMES / K.FPS
    land, leave = 0.30, 0.42
    travel = land + leave + (HEEL0[s].y - BALL0[s].y)
    cf = travel / (RUN_SPEED * T)
    y_land = -land - HEEL0[s].y
    if phase < cf:
        u = phase / cf
        shift = V((0, y_land + travel * u, 0))
        if u < 0.22:
            pitch = -10 * (1 - L.ease(u / 0.22))
        elif u < 0.5:
            pitch = 0.0
        else:
            pitch = 45 * ((u - 0.5) / 0.5) ** 1.6
        return foot_on_ground(s, shift, pitch, yaw=4 * SX[s])
    sw = (phase - cf) / (1 - cf)
    a_off, _ = foot_on_ground(s, V((0, y_land + travel, 0)), 45, yaw=4 * SX[s])
    a_on, _ = foot_on_ground(s, V((0, y_land, 0)), -10, yaw=4 * SX[s])
    x = ANK0[s].x
    keys = [(0.0, a_off.y, a_off.z, 45), (0.12, 0.5, 0.25, 62), (0.32, 0.36, 0.45, 70), (0.55, 0.0, 0.47, 35),
            (0.75, -0.3, 0.33, 2), (0.9, -0.39, 0.17, -12), (1.0, a_on.y, a_on.z, -10)]
    for k in range(len(keys) - 1):
        if sw <= keys[k + 1][0]:
            break
    k0, k1 = keys[max(0, k - 1)], keys[min(len(keys) - 1, k + 2)]
    p1, p2 = keys[k], keys[k + 1]
    t = (sw - p1[0]) / (p2[0] - p1[0])
    # Catmull-Rom through the keys (y, z, pitch)
    out = []
    for i in (1, 2, 3):
        a, b, c, d = k0[i], p1[i], p2[i], k1[i]
        out.append(0.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t ** 3))
    return V((x, out[0], out[1])), foot_q(out[2], yaw=4 * SX[s])


def run_pose(ph, weapon=True):
    t = ph * math.tau
    P = L.Pose(R)
    # hips: lowest at each mid-stance, highest in flight; twist with the legs
    T = RUN_FRAMES / K.FPS
    cf = (0.30 + 0.42 + 0.175) / (RUN_SPEED * T)
    low = cf * 0.45
    hz = -0.055 + 0.022 * math.cos(2 * math.tau * (ph - low) + math.pi)
    hips_q = L.rot(x=3, y=2.5 * math.sin(t), z=-7 * math.cos(t))
    torso(P, (0, 0.0, hz), hips_q, spine=(5, 0, 3 * math.cos(t)), chest=(6, -1.5 * math.sin(t), 10 * math.cos(t)), neck=(-4, 0, 0),
          head=L.rot(x=4, z=-1.5 * math.cos(t)))
    for s, off in (("L", 0.0), ("R", 0.5)):
        a, q = run_leg((ph + off) % 1.0, s)
        leg_ik(P, s, a, q)
    # left arm swings (opposite to the left leg), injector up and out
    sw = math.cos(t)          # +1 when the left leg lands (left arm back)
    E_l = P.head("UpperArm.L")
    wrist = chest_pt(P, V((0.27, 0.06 + 0.2 * sw, 1.06 + 0.07 * max(0.0, -sw) - 0.02 * sw)))
    wrist.y += 0.0
    relaxed_left(P, wrist, pole=V((0.45, 1.0, -0.2)), bend=8 + 8 * max(0, -sw), roll=-10)
    # right arm: revolver held forward-ish, small counter swing
    if weapon:
        g = chest_pt(P, V((-0.2, -0.33 - 0.04 * sw, 1.13 + 0.015 * math.sin(2 * t))))
        arm_ik(P, "R", g, V((-0.55, 0.9, -0.5)), gun_hand_q(P, V((0.12, -1, -0.28)), cant=-12))
    P.rel("Cape", L.rot(x=32 + 5 * math.sin(2 * t)))
    P.rel("Lantern", L.rot(x=-14 - 8 * math.sin(2 * t + 0.6), y=4 * math.sin(t)))
    P.rel("Halo", L.rot(x=-5 + 4 * math.sin(2 * t + 1.0), y=3 * math.sin(t)))
    P.rel("Tail", L.rot(x=18 + 6 * math.sin(2 * t), z=14 * math.sin(t)))
    return P


# ---------------- Idle ----------------

IDLE_FRAMES = 72
FEET_IDLE = {"L": (V((0.02, -0.03, 0)), 8.0), "R": (V((-0.03, 0.05, 0)), 10.0)}


def idle_pose(ph):
    t = ph * math.tau
    P = L.Pose(R)
    shift = 0.022 * math.sin(t)
    breath = math.sin(2 * t)
    torso(P, (shift, 0.006, -0.028 - 0.006 * abs(math.sin(t))), L.rot(x=2, y=-3.0 * math.sin(t), z=-3),
          spine=(1.5 + 0.6 * breath, 1.5 * math.sin(t), 2), chest=(1.5 + 1.0 * breath, 0.8 * math.sin(t), 3), neck=(-2, 0, 0),
          head=L.rot(x=3 + 1.0 * breath, z=6 * math.sin(t * 0.5 + 0.8) - 2), shoulders=(1.2 * breath, 1.2 * breath))
    for s in "LR":
        off, yaw = FEET_IDLE[s]
        a, q = foot_on_ground(s, off, 0.0, yaw * SX[s])
        leg_ik(P, s, a, q, knee_out=0.25)
    relaxed_left(P, chest_pt(P, V((0.37, -0.03, 0.925))), pole=V((0.6, 1.0, -0.2)), bend=14, roll=-6)
    g = chest_pt(P, V((-0.36, -0.1, 0.945)))
    arm_ik(P, "R", g, V((-0.7, 1.0, -0.1)), gun_hand_q(P, V((-0.1, -0.55, -1.0)), cant=-25))
    P.rel("Cape", L.rot(x=3 + 1.5 * math.sin(t)))
    P.rel("Lantern", L.rot(x=-3 + 1.5 * breath))
    P.rel("Halo", L.rot(x=2 * math.sin(2 * t)))
    P.rel("Tail", L.rot(x=8, z=8 * math.sin(t)))
    return P


# ---------------- Hit, Inject, Death ----------------

HIT_FRAMES = 8
INJECT_FRAMES = 15
DEATH_FRAMES = 33


def hit_pose(ph):
    P = idle_pose(0.0)
    k = math.sin(min(1.0, ph / 0.3) * math.pi / 2) if ph < 0.3 else 1 - L.ease((ph - 0.3) / 0.7)
    Q = L.Pose(R)
    torso(Q, (0.0, 0.025, -0.045), L.rot(x=-4, y=3, z=-6), spine=(-6, 0, -4), chest=(-10, 3, -8), neck=(4, 0, 0),
          head=L.rot(x=-16, z=-8), shoulders=(6, 6))
    for s in "LR":
        off, yaw = FEET_IDLE[s]
        a, q = foot_on_ground(s, off, 0.0, yaw * SX[s])
        leg_ik(Q, s, a, q, knee_out=0.25)
    relaxed_left(Q, chest_pt(Q, V((0.42, 0.02, 1.02))), pole=V((0.6, 1.0, -0.2)), bend=25)
    g = chest_pt(Q, V((-0.38, -0.08, 1.05)))
    arm_ik(Q, "R", g, V((-0.7, 1.0, -0.1)), gun_hand_q(Q, V((-0.2, -0.6, -0.8)), cant=-25))
    Q.rel("Cape", L.rot(x=12))
    Q.rel("Lantern", L.rot(x=-20))
    Q.rel("Halo", L.rot(x=-10))
    Q.rel("Tail", L.rot(x=20))
    return _fk_blend(P, Q, k, ("L", "R"))


def _fk_blend(A, B, k, legs=("L", "R")):
    """Blend two poses; legs are re-solved by IK from blended ankle targets, foot rotations and knee poles,
    so feet stay planted and shins never sweep through the ground."""
    P = L.blend_pose(A, B, k)
    for s in legs:
        la, lb = getattr(A, "legs", {}).get(s), getattr(B, "legs", {}).get(s)
        if not la or not lb:
            continue
        pole = la[2].lerp(lb[2], k)
        leg_ik(P, s, la[0].lerp(lb[0], k), la[1].slerp(lb[1], k), pole=pole)
    return P


def inject_pose(ph):
    """He brings the left forearm up, slams the right fist (gun in hand) on the injector, shudders, back."""
    base = idle_pose(0.0)
    Q = L.Pose(R)
    if ph < 0.32:
        k = L.ease(ph / 0.32)
        tap = 0.0
    elif ph < 0.42:
        k, tap = 1.0, L.ease((ph - 0.32) / 0.1)
    elif ph < 0.62:
        k, tap = 1.0, 1.0 - 0.3 * L.ease((ph - 0.42) / 0.2)
    else:
        k, tap = 1.0 - L.ease((ph - 0.62) / 0.38), 0.7 * (1 - L.ease((ph - 0.62) / 0.3))
    shudder = math.sin(ph * 60) * 1.5 if 0.4 < ph < 0.7 else 0.0
    torso(Q, (0.0, -0.01, -0.04 - 0.012 * tap), L.rot(x=3, z=-6), spine=(3 + 3 * tap, 0, -4), chest=(4 + 5 * tap + shudder, 0, -6),
          neck=(6 * tap, 0, 0), head=L.rot(x=14 + 8 * tap, z=10), shoulders=(4 * tap, 3 * tap))
    for s in "LR":
        off, yaw = FEET_IDLE[s]
        a, q = foot_on_ground(s, off, 0.0, yaw * SX[s])
        leg_ik(Q, s, a, q, knee_out=0.25)
    # left forearm across the belly, injector facing up
    lw = chest_pt(Q, V((0.02, -0.33, 1.15)))
    L.two_bone(Q, "UpperArm.L", "LowerArm.L", lw, V((0.6, 0.6, -1.0)), V((0, 1, 0)), twist=-35)
    Q.rel("Hand.L", L.rot(x=-10))
    inj = Q.point("LowerArm.L", J["elbow.L"].lerp(J["wrist.L"], INJ_T + 0.4 * 0.15 / 0.27)) + V((0, 0, 0.05))
    g = inj + V((-0.03, 0.0, 0.12 * (1 - tap) + 0.03))
    arm_ik(Q, "R", g, V((-1.0, 0.6, -0.2)), gun_hand_q(Q, V((0.7, -0.7, 0.15)), cant=-60), twist_share=0.6)
    Q.rel("Cape", L.rot(x=6))
    Q.rel("Lantern", L.rot(x=-10 * tap))
    Q.rel("Tail", L.rot(x=10, z=10 * tap))
    return _fk_blend(base, Q, k, ("L", "R"))


def death_keys():
    """Key poses of the collapse: hit, knees buckle, kneel, fall forward, lie prone (injector up)."""
    keys = []
    # 0: idle start
    keys.append((0.0, idle_pose(0.0)))
    # 1: stagger back
    A = L.Pose(R)
    torso(A, (0, 0.04, -0.06), L.rot(x=-6, z=-8), spine=(-8, 0, -4), chest=(-12, 4, -6), neck=(6, 0, 0), head=L.rot(x=-20, z=-10), shoulders=(8, 8))
    for s in "LR":
        off, yaw = FEET_IDLE[s]
        a, q = foot_on_ground(s, off, 0.0, yaw * SX[s])
        leg_ik(A, s, a, q, knee_out=0.25)
    relaxed_left(A, chest_pt(A, V((0.45, 0.06, 1.02))), bend=25)
    arm_ik(A, "R", chest_pt(A, V((-0.45, 0.0, 1.04))), V((-0.7, 1.0, -0.1)), gun_hand_q(A, V((-0.3, -0.4, -0.85)), cant=-30))
    keys.append((0.18, A))
    # 2: knees buckle, on the knees (shins on the ground)
    B = L.Pose(R)
    torso(B, (0, -0.05, -0.475), L.rot(x=12, z=-4), spine=(10, 0, 0), chest=(14, 0, 0), neck=(10, 0, 0), head=L.rot(x=34, z=-12))
    for s in "LR":
        a, q = foot_on_ground(s, V((0.025 * SX[s], 0.5, 0)), 70, pivot="toe")
        leg_ik(B, s, a, q, pole=V((0.12 * SX[s], -1, -0.5)))
    relaxed_left(B, chest_pt(B, V((0.34, -0.12, 0.9))), bend=20)
    arm_ik(B, "R", chest_pt(B, V((-0.34, -0.1, 0.9))), V((-0.7, 1.0, -0.1)), gun_hand_q(B, V((-0.1, -0.5, -0.9)), cant=-30))
    keys.append((0.5, B))
    # 3: prone (falls forward), head turned to the left, left arm out with the injector facing up
    C = L.Pose(R)
    C.offset["Hips"] = V((0.04, -0.52, -0.735))
    C.abs("Hips", L.rot(x=86, y=-6, z=4))
    C.rel("Spine", L.rot(x=3))
    C.rel("Chest", L.rot(x=2, y=4))
    C.rel("Neck", L.rot(x=-8))
    C.abs("Head", L.rot(x=88) @ L.rot(z=62, x=-4))
    for s, spread in (("L", 8), ("R", 12)):
        C.rel("UpperLeg." + s, L.rot(x=-2, y=-spread * SX[s]))
        C.rel("LowerLeg." + s, L.rot(x=12))
        C.rel("Foot." + s, L.rot(x=40))
        leg_ik(C, s, C.point("LowerLeg." + s, ANK0[s]), C.d("Foot." + s), pole=V((0.1 * SX[s], 0.15, -1)))
    C.rel("UpperArm.L", L.rot(x=-11, y=-78))
    C.rel("LowerArm.L", L.rot(x=-28))
    C.rel("Hand.L", L.rot(x=-10))
    C.rel("UpperArm.R", L.rot(x=2, y=-14))
    C.rel("LowerArm.R", L.rot(x=-10))
    C.rel("Hand.R", L.rot(x=34))
    C.rel("Cape", L.rot(x=-8))
    C.rel("Lantern", L.rot(x=60))
    C.rel("Tail", L.rot(x=25))
    keys.append((0.86, C))
    C2 = C.copy()
    C2.legs = dict(C.legs)
    C2.offset["Hips"] = V((0.04, -0.53, -0.745))
    keys.append((1.0, C2))
    return keys


DEATH_KEYS = None


def death_pose(ph):
    keys = DEATH_KEYS
    for i in range(len(keys) - 1):
        if ph <= keys[i + 1][0]:
            break
    t0, A = keys[i]
    t1, B = keys[i + 1]
    k = (ph - t0) / max(1e-6, t1 - t0)
    k = L.ease(k) if i != 2 else k * k   # the fall accelerates
    if i != 2:
        return _fk_blend(A, B, k)
    # the fall pivots on the knees: the pelvis travels on an arc around them, not on a straight line
    P = L.blend_pose(A, B, k)
    pel0 = R.head["Hips"]
    km = (A.point("UpperLeg.L", J["knee.L"]) + A.point("UpperLeg.R", J["knee.R"])) / 2
    va = (pel0 + A.offset["Hips"]) - km
    vb = (pel0 + B.offset["Hips"]) - km
    a0, a1 = math.atan2(va.z, -va.y), math.atan2(vb.z, -vb.y)
    ang = a0 + (a1 - a0) * k
    rad = va.yz.length + (vb.yz.length - va.yz.length) * k
    x = va.x + (vb.x - va.x) * k
    P.offset["Hips"] = km + V((x, -math.cos(ang) * rad, math.sin(ang) * rad)) - pel0
    for sd in "LR":
        la, lb = A.legs[sd], B.legs[sd]
        leg_ik(P, sd, la[0].lerp(lb[0], k), la[1].slerp(lb[1], k), pole=la[2].lerp(lb[2], k))
    return P


def make_actions():
    global DEATH_KEYS
    DEATH_KEYS = death_keys()
    acts = {}
    acts["Idle"] = char.action("Idle", IDLE_FRAMES, lambda ph: idle_pose(ph).kit(), loop=True)
    acts["Run"] = char.action("Run", RUN_FRAMES, lambda ph: run_pose(ph).kit(), loop=True)
    acts["Hit"] = char.action("Hit", HIT_FRAMES, lambda ph: hit_pose(ph).kit(), loop=False)
    acts["Inject"] = char.action("Inject", INJECT_FRAMES, lambda ph: inject_pose(ph).kit(), loop=False)
    acts["Death"] = char.action("Death", DEATH_FRAMES, lambda ph: death_pose(ph).kit(), loop=False)
    char.rig.animation_data.action = acts["Idle"]
    return acts


def measure_run_speed(obj, act):
    """Measured stance-foot speed: lowest left-sole vertices, frame to frame while on the ground."""
    char.rig.animation_data.action = act
    sc = K.bpy.context.scene
    left = [i for i, v in enumerate(obj.data.vertices) if v.co.z < 0.03 and v.co.x > 0.05]
    track = []
    for f in range(1, RUN_FRAMES + 2):
        sc.frame_set(f)
        pts = L.posed_points(obj)
        lp = [pts[i] for i in left]
        mz = min(p.z for p in lp)
        cy = sum(p.y for p in lp) / len(lp)
        track.append((f, mz, cy))
    speeds = []
    for (f0, z0, y0), (f1, z1, y1) in zip(track, track[1:]):
        if z0 < 0.012 and z1 < 0.012:
            speeds.append((y1 - y0) * K.FPS)
    return track, speeds


ACTS = make_actions() if not STAGES <= {"none", "rest"} else {}

if STAGES & {"rest"}:
    L.sheet("passeur_rest", [[objs[0]]], views=((20, 58), (200, 58), (0, 8), (90, 8)), size=460)
    L.sheet("passeur_closeup", [[objs[0]]], views=((0, 6), (35, 25), (160, 30), (90, 6)), size=420, center=V((0, -0.02, 1.6)), radius=0.2)
    E, W, a, n, s = forearm_frame()
    L.sheet("passeur_injector", [[objs[0]]], views=((60, 40), (100, 15), (20, 70)), size=420, center=(E + W) / 2, radius=0.16)
    L.sheet("passeur_gun", [[objs[0]]], views=((-60, 20), (-100, 10), (-20, 60)), size=420, center=J["hand.R"] + V((0, -0.05, -0.03)), radius=0.17)

if STAGES & {"anim", "all"} and ACTS:
    o = objs[0]
    track, speeds = measure_run_speed(o, ACTS["Run"])
    if speeds:
        K.report(run_speed_measured="%.2f" % (sum(speeds) / len(speeds)), contact_frames=len(speeds),
                 run_speed_min="%.2f" % min(speeds), run_speed_max="%.2f" % max(speeds))
    for nm, act in ACTS.items():
        fr = act.frame_range
        zs = L.min_z_over(o, char.rig, act, range(int(fr[0]), int(fr[1]) + 1))
        K.report(clip=nm, frames=int(fr[1] - fr[0]), min_z="%.3f" % min(zs), max_min_z="%.3f" % max(zs))
    rf = [1, 4, 7, 10, 13, 16]
    L.sheet("passeur_run_side", [[o]], views=((90, 6),), frames=rf, action=ACTS["Run"], rig=char.rig, size=300,
            center=V((0, -0.1, 0.9)), radius=1.0, cols=6)
    L.sheet("passeur_run_top", [[o]], views=((20, 58),), frames=rf, action=ACTS["Run"], rig=char.rig, size=300,
            center=V((0, -0.1, 0.9)), radius=1.0, cols=6)
    L.sheet("passeur_run_front", [[o]], views=((0, 6),), frames=rf, action=ACTS["Run"], rig=char.rig, size=300,
            center=V((0, -0.1, 0.9)), radius=1.0, cols=6)
    L.sheet("passeur_idle", [[o]], views=((20, 30), (90, 6)), frames=[1, 19, 37, 55], action=ACTS["Idle"], rig=char.rig, size=300,
            center=V((0, 0, 0.9)), radius=1.0, cols=8)
    L.sheet("passeur_inject", [[o]], views=((30, 35),), frames=[1, 4, 6, 8, 10, 13], action=ACTS["Inject"], rig=char.rig, size=300,
            center=V((0, -0.1, 1.0)), radius=0.85, cols=6)
    L.sheet("passeur_hit", [[o]], views=((60, 20),), frames=[1, 3, 5, 9], action=ACTS["Hit"], rig=char.rig, size=300,
            center=V((0, 0, 0.9)), radius=1.0, cols=4)
    L.sheet("passeur_death", [[o]], views=((90, 10),), frames=[1, 6, 12, 17, 22, 26, 30, 34], action=ACTS["Death"], rig=char.rig, size=260,
            center=V((0, -0.6, 0.6)), radius=1.25, cols=8)
    L.sheet("passeur_death_end", [[o]], views=((20, 58), (90, 8), (200, 40)), frames=[34], action=ACTS["Death"], rig=char.rig, size=360,
            center=V((0, -0.8, 0.3)), radius=1.1)

if STAGES & {"lineup", "all"} and ACTS:
    groups = [[o] for o in objs]
    for nm, views in (("passeur_lineup_game", ((20, 58),)), ("passeur_lineup_front", ((0, 8),)), ("passeur_lineup_back", ((200, 40),))):
        L.sheet(nm, groups, views=views, frames=[1], action=ACTS["Idle"], rig=char.rig, size=300,
                center=V((0, 0, 0.95)), radius=1.05, cols=6)

if STAGES & {"skins"} and ACTS:
    for o in objs:
        L.sheet("skin_" + o.name, [[o]], views=((20, 58), (90, 6), (200, 35)), frames=[1, 5, 10], action=ACTS["Run"], rig=char.rig, size=260,
                center=V((0, -0.1, 0.95)), radius=1.05, cols=9)

if STAGES & {"check"} and ACTS:
    for o in objs:
        row = []
        for nm, act in ACTS.items():
            fr = act.frame_range
            zs = L.min_z_over(o, char.rig, act, range(int(fr[0]), int(fr[1]) + 1))
            row.append("%s=%.3f" % (nm, min(zs)))
        K.report(skin=o.name, min_z=" ".join(row))
    groups = [[o] for o in objs]
    L.sheet("passeur_lineup_inject", groups, views=((25, 40),), frames=[7], action=ACTS["Inject"], rig=char.rig, size=300,
            center=V((0, -0.05, 1.0)), radius=0.95, cols=6)
    L.sheet("passeur_lineup_death", groups, views=((20, 58),), frames=[34], action=ACTS["Death"], rig=char.rig, size=300,
            center=V((0, -0.7, 0.25)), radius=1.15, cols=6)
    L.sheet("passeur_lineup_run", groups, views=((20, 58),), frames=[4], action=ACTS["Run"], rig=char.rig, size=300,
            center=V((0, -0.1, 0.95)), radius=1.05, cols=6)
    L.sheet("passeur_lineup_heads", groups, views=((15, 15),), frames=[1], action=ACTS["Idle"], rig=char.rig, size=300,
            center=V((0, -0.02, 1.66)), radius=0.26, cols=6)

if STAGES & {"closeups"} and ACTS:
    for o in objs:
        L.sheet("close_" + o.name, [[o]], views=((10, 12), (200, 20)), frames=[1], action=ACTS["Idle"], rig=char.rig, size=380,
                center=V((0, -0.02, 1.25)), radius=0.45)

if STAGES & {"gauge", "all"}:
    # the level gauge: Vial bone local Y scale 0 (empty), 0.5, 1 (full), in the rest pose
    rig = char.rig
    keep = rig.animation_data.action if rig.animation_data else None
    if rig.animation_data:
        rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = V()
        pb.scale = V((1, 1, 1))
    E, W, a, n, s = forearm_frame()
    tiles = []
    st = L.Stage(320)
    for k in (0.0, 0.5, 1.0):
        rig.pose.bones["Vial"].scale = V((1, max(k, 1e-4), 1))
        K.bpy.context.view_layer.update()
        tiles.append(st.shot([objs[0]], E.lerp(W, INJ_T) + n * 0.03, 0.11, 70, 45))
    st.close()
    L.save_sheet("passeur_gauge", [tiles])
    rig.pose.bones["Vial"].scale = V((1, 1, 1))
    if rig.animation_data:
        rig.animation_data.action = keep

if STAGES & {"export", "all"} and ACTS and not ONLY:
    char.rig.animation_data.action = ACTS["Idle"]
    K.bpy.context.scene.frame_set(1)
    path = K.export_fbx([char.rig] + objs, "Characters/Passeur")
    K.report(fbx=path)
    for o in objs:
        K.report(variant=o.name, tris=K.tri_count(o))
    for nm, act in ACTS.items():
        fr = act.frame_range
        K.report(clip=nm, frames=int(fr[1] - fr[0]), seconds="%.3f" % ((fr[1] - fr[0]) / K.FPS))
    for b in char.rig.data.bones:
        if b.name in ("Vial", "Muzzle", "Cape", "Lantern", "Halo", "Tail"):
            K.report(bone=b.name, parent=b.parent.name, head=tuple(round(x, 3) for x in b.head_local),
                     tail=tuple(round(x, 3) for x in b.tail_local))
    track, speeds = measure_run_speed(objs[0], ACTS["Run"])
    K.report(run_design_speed=RUN_SPEED, run_cycle_frames=RUN_FRAMES, run_cycle_m=round(RUN_SPEED * RUN_FRAMES / K.FPS, 3),
             run_measured_avg="%.2f" % (sum(speeds) / max(1, len(speeds))))
