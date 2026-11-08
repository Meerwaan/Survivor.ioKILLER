"""Helpers for the Boucher / Pilleur / Rat batch, on top of cordon_kit (never edits it).

- FK: forward kinematics of a K.Character pose dict (same value forms as Character.action), so a pose
  function can measure itself: where the feet are, how fast the stance foot slides.
- leg_ik: two-bone leg IK (knee hinge on the armature X axis, lateral swing on Y, optional foot pitch),
  used to plant feet: gaits with no foot sliding and an exact natural ground speed, crouches that keep
  the feet on the floor.
- foot_track / gait helpers: stance (linear, on the ground) + swing (eased arc) per foot.
- aux bones: extra bones that only carry rigid parts (a sack, a back growth) and must not steal the
  body's smooth weights or outfit colors (context manager `aux_excluded`), plus `reparent`.
- crust_mass: a lumpy crust growth with glowing fissures painted per face.
- checks: lowest mesh vertex per frame (feet on the ground), natural speed of a locomotion clip.
- preview: K.preview rendered in a private temp folder (K.preview's shared _tmp.png collides when
  several Blender instances render at the same time), then copied to Tools/Blender/previews.
"""
import math
import os
import random
import shutil
import sys
import tempfile

import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.noise import noise as perlin

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import cordon_kit as K  # noqa: E402


# ---------------------------------------------------------------------------------------------
# Aux bones
# ---------------------------------------------------------------------------------------------

class aux_excluded:
    """While active, the kit's smooth weights (Character.build / add_variant) and outfit_faces ignore the
    segments of `bones`. Those bones exist in the rig and only carry rigid parts."""

    def __init__(self, bones):
        self.bones = set(bones)
        self._orig = None

    def __enter__(self):
        self._orig = K._seg_info
        orig, bones = self._orig, self.bones
        K._seg_info = lambda skel: [s for s in orig(skel) if s[2] not in bones]
        return self

    def __exit__(self, *exc):
        K._seg_info = self._orig
        return False


def reparent(c, bone, parent):
    """Edit-mode reparent (an aux bone built from a disconnected segment starts under Root)."""
    rig = c.rig
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    ebs = rig.data.edit_bones
    ebs[bone].parent = ebs[parent]
    ebs[bone].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")


# ---------------------------------------------------------------------------------------------
# Forward kinematics
# ---------------------------------------------------------------------------------------------

def _parse(val):
    q, loc, sc = Quaternion(), None, (1.0, 1.0, 1.0)
    if val is None:
        pass
    elif isinstance(val, dict):
        q = val.get("rot", q)
        loc = val.get("loc")
        s = val.get("scale")
        if s is not None:
            sc = (s, s, s) if isinstance(s, (int, float)) else tuple(s)
    elif isinstance(val, tuple):
        q, loc = val
    else:
        q = val
    return q, loc, sc


class FK:
    def __init__(self, c):
        self.c = c
        self.bones = c.rig.data.bones
        self.rest_inv = {b.name: b.matrix_local.inverted() for b in self.bones}

    def mats(self, pose):
        out = {}

        def get(b):
            m = out.get(b.name)
            if m is not None:
                return m
            q, loc, sc = _parse(pose.get(b.name))
            loc_l = b.matrix_local.to_3x3().inverted() @ Vector(loc) if loc is not None else Vector()
            basis = Matrix.Translation(loc_l) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((sc[0], sc[1], sc[2], 1.0))
            if b.parent:
                m = get(b.parent) @ (self.rest_inv[b.parent.name] @ b.matrix_local) @ basis
            else:
                m = b.matrix_local @ basis
            out[b.name] = m
            return m

        for b in self.bones:
            get(b)
        return out

    def point(self, mats, bone, p):
        """Posed position of a rest (armature space) point carried rigidly by `bone`."""
        return mats[bone] @ (self.rest_inv[bone] @ Vector(p))


# ---------------------------------------------------------------------------------------------
# Leg IK
# ---------------------------------------------------------------------------------------------

def _ang(y, z):
    return math.atan2(z, y)


def _wrap(a):
    while a > math.pi:
        a -= math.tau
    while a < -math.pi:
        a += math.tau
    return a


def leg_ik(c, fk, pose, upper, lower, foot, target, foot_pitch=0.0, bend=None, reach=0.995, extra_upper=None):
    """Writes pose[upper], pose[lower] (and pose[foot]) so the tail of `lower` reaches `target`
    (armature space). Knee hinge = armature X, the leg plane swings on Y to follow sideways targets.
    bend: +1 knee forward (-Y), -1 backward; None = from the rest pose. foot_pitch (deg, + = toe up):
    the foot's world pitch vs rest. Returns |target - hip| / leg length (> reach means clamped)."""
    B = fk.bones
    bu, bl = B[upper], B[lower]
    for k in (upper, lower, foot):
        if k:
            pose.pop(k, None)
    mats = fk.mats(pose)
    par = bu.parent
    P = mats[par.name] @ fk.rest_inv[par.name] @ bu.matrix_local
    T = bu.matrix_local @ P.inverted() @ Vector(target)  # target as if the parents were at rest
    H, Kn, A = bu.head_local, bu.tail_local, bl.tail_local
    if bend is None:
        # sign of the knee offset from the hip->ankle line, in the YZ plane
        hy, hz, ky, kz, ay, az = H.y, H.z, Kn.y, Kn.z, A.y, A.z
        cross = (ay - hy) * (kz - hz) - (az - hz) * (ky - hy)
        bend = -1 if cross > 0 else 1
    # lateral swing: put the target in the leg plane
    rest_dx = A.x - H.x
    d = T - H
    gamma = math.atan2(d.x, -d.z) - math.atan2(rest_dx, -(A.z - H.z))
    beta = -gamma  # rotation about +Y moves a downward bone toward -X
    Ry = Matrix.Rotation(-beta, 3, "Y")
    d2 = Ry @ d
    l1 = math.hypot(Kn.y - H.y, Kn.z - H.z)
    l2 = math.hypot(A.y - Kn.y, A.z - Kn.z)
    dist = math.hypot(d2.y, d2.z)
    ratio = dist / (l1 + l2)
    dist = max(abs(l1 - l2) + 1e-5, min(dist, (l1 + l2) * reach))
    cos_a = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
    alpha = math.acos(max(-1.0, min(1.0, cos_a)))
    base = _ang(d2.y, d2.z)
    th = base - bend * alpha
    ky, kz = l1 * math.cos(th), l1 * math.sin(th)
    tdy, tdz = dist * math.cos(base), dist * math.sin(base)
    sh = _ang(tdy - ky, tdz - kz)
    r1 = _wrap(th - _ang(Kn.y - H.y, Kn.z - H.z))
    r2 = _wrap(sh - _ang(A.y - Kn.y, A.z - Kn.z) - r1)
    qu = c.q(upper, x=math.degrees(r1), y=math.degrees(beta))
    if extra_upper is not None:
        qu = qu @ extra_upper
    pose[upper] = qu
    pose[lower] = c.q(lower, x=math.degrees(r2))
    if foot:
        R = (P @ fk.rest_inv[upper]).to_3x3()
        v = R @ Vector((0, 1, 0))
        parent_pitch = _ang(v.y, v.z)
        r3 = _wrap(-math.radians(foot_pitch) - parent_pitch - r1 - r2)
        pose[foot] = c.q(foot, x=math.degrees(r3), y=-math.degrees(beta))
    return ratio


# ---------------------------------------------------------------------------------------------
# Gait
# ---------------------------------------------------------------------------------------------

def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def foot_track(u, duty, stride, lift, roll_off=0.0, roll_land=0.0, lift_bias=0.8):
    """Foot path for one foot at its own phase u (0 = touchdown). Stance: on the ground, moving back
    linearly (forward = -Y) from -stride/2 to +stride/2. Swing: eased forward arc of height `lift`.
    Returns (dy, dz, pitch_deg, in_stance). pitch: + toe up (landing), - toe down (push off)."""
    u %= 1.0
    if u < duty:
        s = u / duty
        dy = -stride / 2 + stride * s
        pitch = 0.0
        if roll_land and s < 0.15:
            pitch = roll_land * (1 - s / 0.15)
        if roll_off and s > 0.65:
            pitch = -roll_off * smooth((s - 0.65) / 0.35)
        return dy, 0.0, pitch, True
    s = (u - duty) / (1 - duty)
    e = 0.5 - 0.5 * math.cos(math.pi * s)
    dy = stride / 2 - stride * e
    dz = lift * math.sin(math.pi * min(1.0, s ** lift_bias))
    pitch = -roll_off * (1 - smooth(s / 0.45)) + roll_land * smooth((s - 0.55) / 0.45)
    return dy, dz, pitch, False


class Foot:
    """Rest geometry of one leg for planting: the ankle (IK target), and the sole points that must stay
    above the ground (toe and heel), as offsets from the ankle at rest."""

    def __init__(self, c, side, upper="UpperLeg", lower="LowerLeg", foot="Foot", toe_z=0.0, heel_back=0.0):
        B = c.rig.data.bones
        self.upper, self.lower = upper + "." + side, lower + "." + side
        self.foot = (foot + "." + side) if foot else None
        self.side = side
        self.hip = B[self.upper].head_local.copy()
        self.ankle = B[self.lower].tail_local.copy()
        if self.foot:
            self.toe = B[self.foot].tail_local.copy()
            self.toe_rel = self.toe - self.ankle
        else:
            self.toe = self.ankle.copy()
            self.toe_rel = Vector()
        self.heel_rel = Vector((0, heel_back, -self.ankle.z))  # heel bottom, behind/below the ankle
        self.toe_z = toe_z  # rest height of the toe point (its bottom touches the ground)
        self.length = (B[self.upper].tail_local - self.hip).length + (self.ankle - B[self.upper].tail_local).length

    def ankle_for(self, dy, dz, pitch, dx=0.0):
        """Ankle target for a sole moved by (dx, dy) on the ground, lifted dz, pitched (deg, + toe up);
        the ankle rises when the foot pivots on its toe or heel so the sole never sinks."""
        a = self.ankle + Vector((dx, dy, dz))
        if self.foot and pitch:
            R = Matrix.Rotation(-math.radians(pitch), 3, "X")
            toe = R @ self.toe_rel
            heel = R @ self.heel_rel
            need = max(self.toe.z - toe.z, -heel.z)  # toe point stays >= its rest height, heel >= 0
            a.z = max(a.z, need + dz)
        return a


def max_hip_drop_for(feet_targets, hip_offset, reach=0.985):
    """Highest hip z offset (<= hip_offset) that lets every stance foot reach its ankle target."""
    best = hip_offset
    for f, tgt in feet_targets:
        dx = tgt.x - f.hip.x
        dy = tgt.y - f.hip.y
        r2 = (f.length * reach) ** 2 - dx * dx - dy * dy
        if r2 <= 0:
            continue
        zmax = tgt.z + math.sqrt(r2) - f.hip.z
        best = min(best, zmax)
    return best


# ---------------------------------------------------------------------------------------------
# Parts
# ---------------------------------------------------------------------------------------------

def crust_mass(center, radius, scale=(1, 1, 1), rot=(0, 0, 0), bone=None, seed=0, subdiv=2, jitter=0.16,
               base=("crust_black", "crust_dark"), fissure="amber_glow", rim="amber",
               width=0.1, freq=2.2, groove=0.05, rim_share=0.8, patch=0.3, patch_freq=1.6):
    """A lumpy crust growth (icosphere, subdiv 1 = 20 faces, 2 = 80, 3 = 320, 4 = 1280) whose faces are
    crust (base[0], with low-frequency patches of base[1:] over a `patch` share), crossed by glowing
    fissures (faces along the zero lines of a noise field), slightly recessed. width = share of fissure
    faces, rim_share x width = share of `rim` faces bordering them. Deterministic (seed)."""
    part = K.ico(radius, center, rot, scale, swatch=base[0], bone=bone, subdiv=subdiv, jitter=jitter, seed=seed)
    rnd = random.Random(seed * 7 + 1)
    offs = Vector((rnd.random() * 40, rnd.random() * 40, rnd.random() * 40))
    offs2 = Vector((rnd.random() * 40, rnd.random() * 40, rnd.random() * 40))
    cen = Vector(center)
    sc = Vector(scale)

    def field(p):
        q = p - cen
        q = Vector((q.x / sc.x, q.y / sc.y, q.z / sc.z)) / radius
        return perlin(q * freq + offs)

    vals = [field(v) for v in part.verts]
    # thresholds as quantiles of the field (noise amplitude varies): `width` = share of fissure faces
    fvals = sorted(abs(sum(vals[i] for i in f) / len(f)) for f in part.faces)
    t_fis = fvals[int(len(fvals) * width)]
    t_rim = fvals[int(len(fvals) * min(0.95, width * (1.0 + rim_share)))]
    cents = [sum((part.verts[i] for i in f), Vector()) / len(f) for f in part.faces]
    n2s = [perlin((cc - cen) / radius * patch_freq + offs2) for cc in cents]
    ns = sorted(n2s)
    t_patch = [ns[min(len(ns) - 1, int(len(ns) * (1 - patch * (k + 1) / max(1, len(base) - 1))))] for k in range(len(base) - 1)]
    vsorted = sorted(abs(x) for x in vals)
    t_groove = vsorted[int(len(vsorted) * width * 1.3)]
    # recess the fissures: vertices close to a zero line sink toward the center
    for i, v in enumerate(part.verts):
        if abs(vals[i]) <= t_groove:
            part.verts[i] = cen + (v - cen) * (1.0 - groove)
    sws = []
    for fi, f in enumerate(part.faces):
        fv = abs(sum(vals[i] for i in f) / len(f))
        if fv <= t_fis:
            sws.append(fissure)
        elif fv <= t_rim and rim:
            sws.append(rim)
        else:
            s = base[0]
            for k, tp in enumerate(t_patch):
                if n2s[fi] >= tp:
                    s = base[k + 1]
            sws.append(s)
    part.swatch = sws
    return part


def colored(part, fn):
    """Recolor a part per face: fn(face_center, current_swatch) -> swatch."""
    out = []
    for fi, f in enumerate(part.faces):
        c = sum((part.verts[i] for i in f), Vector()) / len(f)
        out.append(fn(c, part.swatch[fi]))
    part.swatch = out
    return part


def tube(points, radii, swatch, bone=None, sides=6, smooth=True):
    """A tube along a polyline (straps, ropes, a tail): rings of `sides` vertices, capped."""
    pts = [Vector(p) for p in points]
    verts, faces = [], []
    n = len(pts)
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        t1 = d.orthogonal().normalized()
        t2 = d.cross(t1)
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        for k in range(sides):
            a = math.tau * k / sides
            verts.append(p + (t1 * math.cos(a) + t2 * math.sin(a)) * r)
    for i in range(n - 1):
        for k in range(sides):
            a, b = i * sides + k, i * sides + (k + 1) % sides
            faces.append([a, b, b + sides, a + sides])
    faces.append(list(range(sides))[::-1])
    faces.append([(n - 1) * sides + k for k in range(sides)])
    return K.Part(verts, faces, swatch, bone, smooth)


def strap(a, b, width, thick, swatch, bone=None, bulge=Vector((0, 0, 0)), segs=4):
    """A flat strap from a to b, bowed by `bulge` at the middle (over a shoulder)."""
    a, b = Vector(a), Vector(b)
    d = (b - a)
    pts = [a + d * (i / segs) + bulge * math.sin(math.pi * i / segs) for i in range(segs + 1)]
    verts, faces = [], []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, segs)] - pts[max(i - 1, 0)]).normalized()
        side = t.cross(Vector((0, 0, 1))) if abs(t.z) < 0.9 else t.cross(Vector((0, 1, 0)))
        side.normalize()
        nrm = side.cross(t).normalized()
        for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            verts.append(p + side * sx * width / 2 + nrm * sz * thick / 2)
    for i in range(segs):
        for k in range(4):
            a0, b0 = i * 4 + k, i * 4 + (k + 1) % 4
            faces.append([a0, b0, b0 + 4, a0 + 4])
    faces.append([3, 2, 1, 0])
    faces.append([segs * 4 + k for k in range(4)])
    return K.Part(verts, faces, swatch, bone, False)


# ---------------------------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------------------------

def lowest_per_frame(obj, action, frames):
    """Lowest evaluated vertex z for frames 1..frames+1 of `action` (feet on the ground check)."""
    rig = obj.parent
    rig.animation_data.action = action
    sc = bpy.context.scene
    out = []
    for f in range(1, frames + 2):
        sc.frame_set(f)
        dg = bpy.context.evaluated_depsgraph_get()
        ev = obj.evaluated_get(dg)
        m = ev.to_mesh()
        out.append(min((obj.matrix_world @ v.co).z for v in m.vertices))
        ev.to_mesh_clear()
    return out


def contact_speed(fk, pose_fn, frames, effectors, tol=0.012):
    """Natural ground speed of a locomotion pose function: average backward speed (m/s) of the feet in
    contact (z within tol of their lowest). effectors = [(bone, rest point)]. Returns (speed, slip)
    where slip is the spread (std) of the contact speed."""
    pts = []
    for f in range(frames + 1):
        pose = pose_fn(f / frames)
        mats = fk.mats(pose)
        pts.append([fk.point(mats, b, p) for b, p in effectors])
    zmin = [min(pts[f][i].z for f in range(frames + 1)) for i in range(len(effectors))]
    vs = []
    for f in range(frames):
        for i in range(len(effectors)):
            if pts[f][i].z < zmin[i] + tol and pts[f + 1][i].z < zmin[i] + tol:
                vs.append((pts[f + 1][i].y - pts[f][i].y) * K.FPS)
    if not vs:
        return 0.0, 0.0
    m = sum(vs) / len(vs)
    sd = math.sqrt(sum((v - m) ** 2 for v in vs) / len(vs))
    return m, sd


def check_fk(c, fk, action, pose_fn, frame_phase, frames):
    """Max distance between our FK and Blender's evaluated pose bones (sanity check)."""
    rig = c.rig
    rig.animation_data.action = action
    f = int(round(frame_phase * frames))
    bpy.context.scene.frame_set(f + 1)
    mats = fk.mats(pose_fn(f / frames))
    err = 0.0
    for pb in rig.pose.bones:
        err = max(err, (pb.matrix.translation - mats[pb.name].translation).length,
                  ((pb.matrix @ Vector((0, pb.bone.length, 0, 1))).to_3d() - (mats[pb.name] @ Vector((0, pb.bone.length, 0, 1))).to_3d()).length)
    return err


# ---------------------------------------------------------------------------------------------
# Preview in a private folder
# ---------------------------------------------------------------------------------------------

def preview(name, objs, **kw):
    real = os.path.join(K.KIT_DIR, "previews")
    priv = tempfile.mkdtemp(prefix="souche_pv_")
    old = K.PREVIEWS
    K.PREVIEWS = priv
    try:
        path = K.preview(name, objs, **kw)
        os.makedirs(real, exist_ok=True)
        dst = os.path.join(real, name + ".png")
        shutil.copyfile(path, dst)
    finally:
        K.PREVIEWS = old
        shutil.rmtree(priv, ignore_errors=True)
    return dst


def lowest_vertex_info(obj, action, frame):
    """(z, main bone, rest position) of the lowest evaluated vertex at `frame` (debug)."""
    rig = obj.parent
    rig.animation_data.action = action
    bpy.context.scene.frame_set(frame)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    m = ev.to_mesh()
    zs = [(obj.matrix_world @ v.co).z for v in m.vertices]
    ev.to_mesh_clear()
    i = min(range(len(zs)), key=lambda k: zs[k])
    v = obj.data.vertices[i]
    g = max(v.groups, key=lambda gr: gr.weight) if v.groups else None
    return zs[i], (obj.vertex_groups[g.group].name if g else None), tuple(round(x, 3) for x in v.co)


def reweight(c, obj, start, count, exclude):
    """Recomputes the kit's smooth weights for vertices [start, start + count) of `obj` (a part's range:
    add_variant writes parts in order) ignoring the segments of `exclude` bones, e.g. keep a skinned
    trunk's belly off the lower leg bones."""
    segs = [s for s in K._seg_info(c.skel) if s[2] not in set(exclude)]
    groups = list(obj.vertex_groups)
    for i in range(start, start + count):
        co = obj.data.vertices[i].co
        w = K._weights_for(co, segs, c.skel)
        for g in groups:
            g.remove([i])
        for bn, wt in w.items():
            obj.vertex_groups[bn].add([i], wt, "REPLACE")


def set_weights(obj, start, weights):
    """Explicit weights for vertices start.. start + len(weights): weights[i] = {bone: w}
    (e.g. a tube's rings: each ring of `sides` vertices gets the same dict)."""
    groups = list(obj.vertex_groups)
    for k, w in enumerate(weights):
        i = start + k
        for g in groups:
            g.remove([i])
        for bn, wt in w.items():
            obj.vertex_groups[bn].add([i], wt, "REPLACE")
