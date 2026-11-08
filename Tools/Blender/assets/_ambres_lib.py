"""Shared helpers for the Ambre specials (Fievreux, Masse, Cracheur, Kyste, Colonie, Rejeton).

Kept out of cordon_kit.py on purpose (the kit is shared). What it adds:
- extra bones on a kit skeleton (sacs, buds, bellies: rigid parts that animate on their own),
- ray casts on the skinned body to put crust exactly on the surface,
- face culling (body faces hidden inside a balloon / a pauldron cost tris for nothing),
- exact two-bone IK in the parent's frame, and a biped gait with PLANTED feet: the stance foot slides
  backward at exactly `speed` m/s, so a clip's natural ground speed is known by construction,
- an animation checker (mesh lowest point per frame, measured planted-foot speed).
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import cordon_kit as K  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from mathutils.noise import noise as perlin  # noqa: E402

V = Vector


# ---------------------------------------------------------------------------------------------
# Extra bones
# ---------------------------------------------------------------------------------------------

class XSkeleton(K.Skeleton):
    """A kit Skeleton plus bones that are not grown by the Skin modifier (no segment, no body weights):
    only rigid parts ride them."""

    def bone_names(self):
        names = K.Skeleton.bone_names(self)
        return names + [b for b in self.extra if b not in names]

    def bone_span(self, bone):
        if bone in self.extra:
            h, t, _ = self.extra[bone]
            return h.copy(), t.copy()
        return K.Skeleton.bone_span(self, bone)

    def bone_parent(self, bone):
        if bone in self.extra:
            return self.extra[bone][2]
        return K.Skeleton.bone_parent(self, bone)


def add_bone(skel, name, head, tail, parent):
    if not isinstance(skel, XSkeleton):
        skel.__class__ = XSkeleton
        skel.extra = {}
    skel.extra[name] = (V(head), V(tail), parent)
    return skel


# ---------------------------------------------------------------------------------------------
# Geometry helpers
# ---------------------------------------------------------------------------------------------

def surface(part, origin, direction):
    """First hit of a ray on a Part: (point, normal) or (None, None)."""
    tree = getattr(part, "_tree", None)
    if tree is None:
        tree = BVHTree.FromPolygons([tuple(v) for v in part.verts], part.faces)
        part._tree = tree
    loc, nor, _, _ = tree.ray_cast(V(origin), V(direction).normalized())
    return loc, nor


def back_point(part, x, z, y_far=2.0):
    """The body's back surface (+Y side) at (x, z)."""
    return surface(part, (x, y_far, z), (0, -1, 0))


def cull(part, inside):
    """Drops the faces whose every vertex satisfies inside(v) (hidden geometry), then compacts."""
    keep, sws = [], []
    for f, s in zip(part.faces, part.swatch):
        if all(inside(part.verts[i]) for i in f):
            continue
        keep.append(f)
        sws.append(s)
    used = sorted({i for f in keep for i in f})
    remap = {o: n for n, o in enumerate(used)}
    part.verts = [part.verts[i] for i in used]
    part.faces = [[remap[i] for i in f] for f in keep]
    part.swatch = sws
    part._tree = None
    return part


def ellipsoid_test(center, radii, shrink=1.0):
    c, r = V(center), V(radii)

    def inside(v):
        d = v - c
        return (d.x / (r.x * shrink)) ** 2 + (d.y / (r.y * shrink)) ** 2 + (d.z / (r.z * shrink)) ** 2 < 1.0
    return inside


def paint_part(part, fn):
    """Re-colors a Part face by face: fn(face_center, current_swatch) -> swatch or None."""
    out = []
    for f, s in zip(part.faces, part.swatch):
        c = sum((part.verts[i] for i in f), V()) / len(f)
        out.append(fn(c, s) or s)
    part.swatch = out
    return part


def blob(radius, loc, scale=(1, 1, 1), subdiv=2, jitter=0.08, seed=0, swatch="crust", bone=None, smooth=True, paint=None):
    """A lumpy smooth icosphere (balloons, sacs, buds) with optional per-face painting."""
    p = K.ico(radius, loc, scale=scale, swatch=swatch, bone=bone, subdiv=subdiv, jitter=jitter, seed=seed, smooth=smooth)
    if paint:
        paint_part(p, paint)
    return p


def chain(*fns):
    """Combines outfit_faces `extra` painters: each gets the swatch the previous one produced."""
    def f(c, s, bone, t):
        for fn in fns:
            s = fn(c, s, bone, t) or s
        return s
    return f


def kind(s):
    return K.PALETTE[K.sw(s)][2]


# ---------------------------------------------------------------------------------------------
# Rotations
# ---------------------------------------------------------------------------------------------

def rx(d):
    return Quaternion((1, 0, 0), math.radians(d))


def ry(d):
    return Quaternion((0, 1, 0), math.radians(d))


def rz(d):
    return Quaternion((0, 0, 1), math.radians(d))


def eul(x=0.0, y=0.0, z=0.0):
    """Armature-space rotation, same order as Character.q (z, then y, then x, all about armature axes)."""
    return rz(z) @ ry(y) @ rx(x)


def rest_q(c, bone):
    return c.rig.data.bones[bone].matrix_local.to_quaternion()


def to_local(c, bone, R):
    """Armature-space rotation R, relative to the parent's posed frame -> pose-bone quaternion."""
    M = rest_q(c, bone)
    return M.inverted() @ R @ M


def head(c, bone):
    return c.rig.data.bones[bone].head_local.copy()


def tail(c, bone):
    return c.rig.data.bones[bone].tail_local.copy()


def xform(p, pivot, R, off):
    """Rigid transform of a rest point by a bone posed with armature rotation R about `pivot`, then moved by off."""
    return V(pivot) + V(off) + R @ (V(p) - V(pivot))


def ik2(c, upper, lower, target, pole, parent=None, parent_R=None, parent_off=(0, 0, 0)):
    """Two-bone IK. target: world position for the end of `lower`; pole: world direction the middle joint
    bends toward. parent: the posed parent bone of `upper` (rotation parent_R about its rest head, moved by
    parent_off). Returns ({upper: q, lower: q}, R_lower_total, reached_point)."""
    R_p = parent_R or Quaternion()
    P = head(c, parent) if parent else V()
    Rinv = R_p.inverted()
    T = P + Rinv @ (V(target) - P - V(parent_off))
    pole_l = Rinv @ V(pole)
    H, K0, F0 = head(c, upper), head(c, lower), tail(c, lower)
    l1, l2 = (K0 - H).length, (F0 - K0).length
    axis = T - H
    d = max(abs(l1 - l2) + 1e-4, min(axis.length, (l1 + l2) * 0.9999))
    au = axis.normalized()
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    h = math.sqrt(max(0.0, l1 * l1 - a * a))
    perp = pole_l - au * pole_l.dot(au)
    if perp.length < 1e-6:
        perp = (K0 - H) - au * (K0 - H).dot(au)
    perp.normalize()
    Kp = H + au * a + perp * h
    Fr = H + au * d
    R1 = (K0 - H).rotation_difference(Kp - H)
    R2 = (R1 @ (F0 - K0)).rotation_difference(Fr - Kp)
    out = {upper: to_local(c, upper, R1), lower: to_local(c, lower, R1.inverted() @ R2 @ R1)}
    reached = P + V(parent_off) + R_p @ (Fr - P)
    return out, R_p @ R2 @ R1, reached


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def pulse(ph, at, width):
    """A periodic bump (0..1..0) centered on phase `at`, `width` wide: twitches inside a loop."""
    d = (ph - at + 0.5) % 1.0 - 0.5
    if abs(d) > width * 0.5:
        return 0.0
    return 0.5 + 0.5 * math.cos(d / (width * 0.5) * math.pi)


def env(ph, a, b, c_=None, d=None):
    """Envelope: 0 before a, ramps to 1 at b, holds, back to 0 between c_ and d (smoothstep edges)."""
    up = smooth((ph - a) / max(1e-6, b - a))
    if c_ is None:
        return up
    return up * (1.0 - smooth((ph - c_) / max(1e-6, d - c_)))


# ---------------------------------------------------------------------------------------------
# Biped gait with planted feet
# ---------------------------------------------------------------------------------------------

def foot_track(c, side, u, S, duty, lift, center=0.0, width=0.0, toe_off=18.0, heel=8.0, roll=0.3, swing_fwd=0.0, shape=1.0):
    """Ankle target (world) and foot pitch (deg, + toe down) for local gait phase u. The stance foot slides
    from -S/2 (front) to +S/2 (back) linearly: ground speed = S / (duty * cycle time)."""
    A0, T0 = head(c, "Foot." + side), tail(c, "Foot." + side)
    sx = 1 if side == "L" else -1
    base = A0 + V((width * sx, center, 0))

    def stance(s):
        y = -S / 2 + S * s
        p = base + V((0, y, 0))
        pitch = 0.0
        if s > 1 - roll and toe_off:
            th = toe_off * smooth((s - (1 - roll)) / roll)
            toe = T0 + V((width * sx, center + y, 0))
            p = toe + rx(th) @ (p - toe)
            pitch = th
        elif s < 0.2 and heel:
            pitch = -heel * (1 - s / 0.2)
        return p, pitch

    if u < duty:
        return stance(u / duty)
    w = (u - duty) / (1 - duty)
    p0, a0 = stance(1.0)
    p1, a1 = stance(0.0)
    k = smooth(w ** shape)
    p = p0.lerp(p1, k)
    p.y += -swing_fwd * math.sin(math.pi * w)
    p.z += lift * math.sin(math.pi * w ** shape) ** 0.8
    pitch = a0 + (a1 - a0) * smooth(w * 1.4)
    return p, pitch


def biped_legs(c, ph, speed, frames, duty=0.6, lift=0.12, center=0.0, width=0.0, toe_off=18.0, heel=8.0,
               hips_R=None, hips_off=(0, 0, 0), reach=0.985, pole_out=0.15, offsets=(0.0, 0.5), swing_fwd=0.0,
               plant=None, shape=1.0, roll=0.3):
    """Legs + Hips for a gait frame. hips_off z is the WANTED drop/bob; it is clamped so every stance foot
    stays reachable (no foot ever leaves the ground during its stance). plant: {side: (ankle, pitch)} forces
    a foot to a fixed world spot (one-shots).
    Returns (pose, info) with info['hip_z'] the applied hips offset."""
    T = frames / K.FPS
    S = speed * duty * T
    R_h = hips_R or Quaternion()
    Hh = head(c, "Hips")
    off = V(hips_off)
    tracks = {}
    for side, o in zip(("L", "R"), offsets):
        if plant and side in plant:
            tracks[side] = (V(plant[side][0]), plant[side][1], True)
            continue
        u = (ph + o) % 1.0
        p, pitch = foot_track(c, side, u, S, duty, lift, center, width, toe_off, heel, roll=roll, swing_fwd=swing_fwd, shape=shape)
        tracks[side] = (p, pitch, u < duty)
    # clamp the hips height so that planted feet are reachable
    dz = off.z
    for side, (p, pitch, planted) in tracks.items():
        if not planted:
            continue
        Hp = xform(head(c, "UpperLeg." + side), Hh, R_h, V((off.x, off.y, 0)))
        l1 = (head(c, "LowerLeg." + side) - head(c, "UpperLeg." + side)).length
        l2 = (tail(c, "LowerLeg." + side) - head(c, "LowerLeg." + side)).length
        R = (l1 + l2) * reach
        hor2 = (p.x - Hp.x) ** 2 + (p.y - Hp.y) ** 2
        if hor2 < R * R:
            dz = min(dz, p.z + math.sqrt(R * R - hor2) - Hp.z)
    off = V((off.x, off.y, dz))
    pose = {"Hips": (to_local(c, "Hips", R_h), tuple(off))}
    for side, (p, pitch, planted) in tracks.items():
        sx = 1 if side == "L" else -1
        legs, R_low, _ = ik2(c, "UpperLeg." + side, "LowerLeg." + side, p, V((pole_out * sx, -1, 0)),
                             parent="Hips", parent_R=R_h, parent_off=off)
        pose.update(legs)
        pose["Foot." + side] = to_local(c, "Foot." + side, R_low.inverted() @ rx(pitch))
    return pose, {"hip_z": dz, "S": S}


# ---------------------------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------------------------

def check_anim(c, obj, act, label=None, feet=("Foot.L", "Foot.R")):
    """Prints the mesh's lowest point per frame (feet on the ground?) and the planted-foot speed."""
    sc = bpy.context.scene
    c.rig.animation_data.action = act
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    lows, foot_pos = [], {f: [] for f in feet}
    for fr in range(f0, f1 + 1):
        sc.frame_set(fr)
        dg = bpy.context.evaluated_depsgraph_get()
        ev = obj.evaluated_get(dg)
        m = ev.to_mesh()
        lows.append(min((obj.matrix_world @ v.co).z for v in m.vertices))
        ev.to_mesh_clear()
        for f in feet:
            if f in c.rig.pose.bones:
                foot_pos[f].append(c.rig.pose.bones[f].head.copy())
    msg = "CORDON_ANIM %s frames=%d low_min=%.3f low_max=%.3f" % (label or act.name, f1 - f0, min(lows), max(lows))
    speeds = []
    for f, ps in foot_pos.items():
        if not ps:
            continue
        zmin = min(p.z for p in ps)
        for i in range(len(ps) - 1):
            if ps[i].z < zmin + 0.004 and ps[i + 1].z < zmin + 0.004:
                speeds.append((ps[i + 1].y - ps[i].y) * K.FPS)
    if speeds:
        msg += " planted_speed=%.2f..%.2f" % (min(speeds), max(speeds))
    print(msg)
    sys.stdout.flush()
    return lows


# ---------------------------------------------------------------------------------------------
# Building blocks shared by the Ambres
# ---------------------------------------------------------------------------------------------

def oriented(part, loc, along, normal):
    """Moves a Part built at the origin so its local Y runs `along` and its local Z is `normal`."""
    z = V(normal).normalized()
    y = (V(along) - z * V(along).dot(z)).normalized()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = V(loc)
    return part.transformed(m)


def bone_at(skel, p, allowed=None):
    """Bone of the closest skeleton segment (optionally among `allowed` bones)."""
    segs = [s for s in K._seg_info(skel) if allowed is None or s[2] in allowed]
    i, _, _ = K._closest_seg(V(p), segs)
    return segs[i][2]


def face_point(part, J, dx, dz, ref="skull"):
    """A point on the front of the face (ray from -Y)."""
    return surface(part, J[ref] + V((dx, -1.5, dz)), (0, 1, 0))


def sealed_face(body, J, seed, mouth_w=1.0, band="amber", jaw_drop=0.0):
    """Stage II+ face: a crust band sealing the eyes, an open dark mouth with a few teeth."""
    parts = []
    eye, en = face_point(body, J, 0.0, -0.01)
    parts.append(K.ico(0.06, eye - V((0, -0.012, 0)), scale=(1.65, 0.6, 0.62), swatch=band, bone="Head", subdiv=1, jitter=0.15, seed=seed))
    m, mn = face_point(body, J, 0.0, -0.035 - jaw_drop, ref="head")
    parts.append(K.ball(0.034, m + V((0, 0.012, 0)), scale=(1.2 * mouth_w, 0.5, 1.25), swatch="mouth", bone="Head", segments=8, rings=4))
    parts.append(K.box((0.045 * mouth_w, 0.012, 0.012), m + V((0, -0.002, 0.03)), swatch="teeth", bone="Head"))
    return parts


def crust_claws(J, side, seed, n=3, length=0.09, width=0.016, swatch="amber", curl=-0.4, spread=0.025):
    """Fingers fused into crust hooks (as on the Ambre)."""
    parts = []
    w, h = J["wrist." + side], J["fingers." + side]
    d = (h - w).normalized()
    for i in range(n):
        off = (i - (n - 1) / 2) * spread
        side_v = V((0, 1, 0)).cross(d).normalized() * off
        parts.append(K.shard(length, width, h + side_v - d * 0.01, d + V((0, curl, 0)), swatch=swatch, bone="Hand." + side, seed=seed + i))
    return parts


def veins_near(centers, vein="skin_ambre_dark", core="veins"):
    """outfit extra: darker skin + black veins around crust centers (only on skin faces)."""
    def paint(c, s, bone, t):
        if kind(s) != "skin":
            return None
        for cc, r in centers:
            d = (c - V(cc)).length
            if d < r * 0.55:
                return core
            if d < r:
                return vein
        return None
    return paint


def noise_patches(new, scale=6.0, thresh=0.35, only=("skin",), seed=0, region=None):
    """outfit extra: blotches of `new` where Perlin noise is high (fever blotches, stains, crust)."""
    rnd = random.Random(seed)
    o = V((rnd.random() * 40, rnd.random() * 40, rnd.random() * 40))

    def paint(c, s, bone, t):
        if only and kind(s) not in only:
            return None
        if region and not region(c):
            return None
        if perlin(c * scale + o) > thresh:
            return new
        return None
    return paint


def gait_angle(u, duty):
    """0 at touchdown (foot forward), pi at liftoff (foot back), 2 pi at the next touchdown: cos() of it
    is +1 when the foot is forward. Drives the arms in sync with planted-foot gaits."""
    if u < duty:
        return math.pi * u / duty
    return math.pi + math.pi * (u - duty) / (1 - duty)


def planted(c, side, dy=0.0, dx=0.0, heel_lift=0.0, toe_up=0.0):
    """A planted foot for one-shots: ankle target and pitch. heel_lift rotates about the toe (ankle rises)."""
    A0, T0 = head(c, "Foot." + side), tail(c, "Foot." + side)
    sx = 1 if side == "L" else -1
    A, T = A0 + V((dx * sx, dy, 0)), T0 + V((dx * sx, dy, 0))
    if heel_lift:
        A = T + rx(heel_lift) @ (A - T)
        return A, heel_lift
    return A, -toe_up


def _bend_axis(c, bone):
    """Axis perpendicular to a limb bone (rest) such that a positive rotation swings it forward (-Y)."""
    a = (tail(c, bone) - head(c, bone)).normalized()
    b = a.cross(V((0, -1, 0)))
    if b.length < 1e-4:
        b = V((1, 0, 0))
    b.normalize()
    if (Quaternion(b, 0.1) @ a).y > a.y:
        b = -b
    return b


def arm2(c, side, forward=0.0, raise_=0.0, elbow=0.0, twist=0.0, wrist=0.0, shoulder=0.0, shrug=0.0):
    """Like K.arm but the arm is first lowered/raised (raise_, + up), THEN swung (forward, + forward): an
    A-pose arm can hang along the body and pump. elbow / wrist bend toward the front, perpendicular to the
    forearm. shoulder swings the clavicle forward (+), shrug lifts it (+)."""
    s = 1 if side == "L" else -1
    up, lo, ha, sh = "UpperArm." + side, "LowerArm." + side, "Hand." + side, "Shoulder." + side
    R = rx(-forward) @ ry(-raise_ * s) @ rz(twist * s)
    out = {up: to_local(c, up, R),
           lo: to_local(c, lo, Quaternion(_bend_axis(c, lo), math.radians(elbow))),
           ha: to_local(c, ha, Quaternion(_bend_axis(c, ha), math.radians(wrist)))}
    if shoulder or shrug:
        out[sh] = to_local(c, sh, rz(-shoulder * s) @ ry(-shrug * s))
    return out


# ---------------------------------------------------------------------------------------------
# Forward kinematics on a pose dict (as returned by pose functions), and limb IK on top of it
# ---------------------------------------------------------------------------------------------

def fk(c, pose, bone):
    """Posed armature-space 4x4 matrix of `bone` for a pose dict (rotations + Hips-style world offsets)."""
    b = c.rig.data.bones[bone]
    M = b.matrix_local
    val = pose.get(bone)
    q, loc = Quaternion(), V()
    if isinstance(val, dict):
        q = val.get("rot", Quaternion())
        if "loc" in val:
            loc = M.to_3x3().inverted() @ V(val["loc"])
    elif isinstance(val, tuple):
        q = val[0]
        loc = M.to_3x3().inverted() @ V(val[1])
    elif val is not None:
        q = val
    local = Matrix.Translation(loc) @ q.to_matrix().to_4x4()
    if b.parent:
        return fk(c, pose, b.parent.name) @ b.parent.matrix_local.inverted() @ M @ local
    return M @ local


def bone_xf(c, pose, bone):
    """(rotation from rest, head displacement) of a posed bone, in armature space."""
    P = fk(c, pose, bone)
    M = c.rig.data.bones[bone].matrix_local
    return P.to_quaternion() @ M.to_quaternion().inverted(), P.translation - M.translation


def point_on(c, pose, bone, p_rest):
    """Where a rest-space point rigidly attached to `bone` ends up in the pose."""
    R, off = bone_xf(c, pose, bone)
    h = head(c, bone)
    return h + off + R @ (V(p_rest) - h)


def limb_ik(c, pose, upper, lower, target, pole):
    """Two-bone IK for a limb whose parent is already posed in `pose` (arms on a bent torso)."""
    parent = c.rig.data.bones[upper].parent.name
    R, off = bone_xf(c, pose, parent)
    q, R_low, reached = ik2(c, upper, lower, target, pole, parent=parent, parent_R=R, parent_off=off)
    return q


def blend(pa, pb, k):
    """Per-bone slerp between two pose dicts of plain quaternions (missing bones = rest)."""
    out = {}
    for bn in set(pa) | set(pb):
        a, b = pa.get(bn, Quaternion()), pb.get(bn, Quaternion())
        if isinstance(a, tuple) or isinstance(b, tuple):
            out[bn] = b if k > 0.5 else a
            continue
        out[bn] = a.slerp(b, k)
    return out


def along(skel, spec):
    """outfit extra: {bone_key: [(t_max, swatch), ...]} applied along the WHOLE bone (head -> tail).
    (The kit's list specs use t per skeleton segment, so a 2-segment bone gets two bands.)"""
    spans = {}

    def paint(c, s, bone, t):
        key = bone[:-2] if bone.endswith((".L", ".R")) else bone
        if key not in spec:
            return None
        if bone not in spans:
            spans[bone] = skel.bone_span(bone)
        h, tl = spans[bone]
        d = tl - h
        tt = max(0.0, min(1.0, (c - h).dot(d) / max(1e-9, d.length_squared)))
        for tmax, swn in spec[key]:
            if tt <= tmax:
                return swn
        return spec[key][-1][1]
    return paint


def preview(name, objs, **kw):
    """K.preview through a per-process temp folder: the kit renders every tile via previews/_tmp.png, so two
    builds running at the same time can swap tiles. The final PNG lands in previews/<name>.png as usual."""
    real = K.PREVIEWS
    tmpdir = os.path.join(real, "_tmp_%d" % os.getpid())
    K.PREVIEWS = tmpdir
    try:
        path = K.preview(name, objs, **kw)
    finally:
        K.PREVIEWS = real
    dst = os.path.join(real, name + ".png")
    os.replace(path, dst)
    try:
        os.rmdir(tmpdir)
    except OSError:
        pass
    return dst


def plates_on(body, rays, size, swatch, bone=None, seed=0, flat=0.35, sink=0.01):
    """One crust plate per ray (origin, direction) that hits the body, lying on the hit surface.
    (K.plate_cluster spreads plates on a flat tangent plane: on a curved back they float.)"""
    parts = []
    for i, (o, d) in enumerate(rays):
        p, n = surface(body, o, d)
        if p is None:
            continue
        sws = swatch if isinstance(swatch, str) else swatch[i % len(swatch)]
        parts += K.plate_cluster(p - n * sink, n, 1, size, swatch=sws, bone=bone, seed=seed + i, spread=0.0, flat=flat)
    return parts
