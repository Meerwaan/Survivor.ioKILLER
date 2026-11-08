"""Helpers for the Passeur (Tools/Blender/assets/passeur.py), the hero. Built on cordon_kit, never edits it.

What it adds to the kit:
- geometry: rings (tori, partial), tubes along a path (straps, wires, horns, braids), slabs (thick curved
  sheets: capes, coat flaps, wings), shells grown from the body surface (plate carrier, belts, hood), raycasts
  on the body to stick details exactly on the skin, a measure of the body radius around a bone;
- weights: a post-pass after Character.add_variant that re-weights chosen parts (rigid on an EXTRA bone such as
  Vial, weights copied from the body vertex a shell grew from, gradients for coat flaps);
- extra bones on the kit rig;
- a pose system in armature space (absolute rotations per bone, parents first) with two-bone IK for planted feet
  and aimed hands, converted to the kit's action() format;
- renders: tiles and contact sheets (several skins side by side), close-ups.
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

import cordon_kit as K

V = Vector


# =============================================================================================
# Geometry
# =============================================================================================

def basis(axis, ref=None):
    """Orthonormal (x, y, z) with z = axis and x as close as possible to `ref`."""
    z = Vector(axis).normalized()
    r = Vector(ref) if ref is not None else (Vector((0, 0, 1)) if abs(z.z) < 0.9 else Vector((0, -1, 0)))
    x = r - z * r.dot(z)
    if x.length < 1e-6:
        x = z.orthogonal()
    x.normalize()
    return x, z.cross(x), z


def recalc(part):
    """Consistent outward winding for a closed part (bmesh recalc)."""
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in part.verts]
    keep = []
    for fi, f in enumerate(part.faces):
        try:
            bm.faces.new([vs[i] for i in f])
            keep.append(fi)
        except ValueError:
            pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.verts.index_update()
    sws = [part.swatch[i] for i in keep]
    part.faces = [[v.index for v in f.verts] for f in bm.faces]
    part.swatch = sws
    bm.free()
    return part


def ring(center, axis, R, r, segs=12, sides=6, arc=None, ref=None, swatch="g55", bone=None, smooth=True):
    """Torus around `axis` through `center`. r = tube radius or (radial, axial) for a flat band.
    arc = (a0, a1) degrees measured from `ref` around `axis` (capped partial ring)."""
    e1, e2, ax = basis(axis, ref)
    rr, ra = (r, r) if not isinstance(r, (tuple, list)) else r
    closed = arc is None
    a0, a1 = (0.0, 360.0) if closed else arc
    n = segs if closed else segs + 1
    verts, faces = [], []
    for i in range(n):
        th = math.radians(a0 + (a1 - a0) * i / segs)
        radial = e1 * math.cos(th) + e2 * math.sin(th)
        c = Vector(center) + radial * R
        for j in range(sides):
            ph = math.tau * (j + 0.5) / sides
            verts.append(c + radial * (rr * math.cos(ph)) + ax * (ra * math.sin(ph)))
    for i in range(segs):
        i2 = (i + 1) % n
        for j in range(sides):
            j2 = (j + 1) % sides
            faces.append([i * sides + j, i2 * sides + j, i2 * sides + j2, i * sides + j2])
    if not closed:
        faces.append(list(range(sides)))
        faces.append([(n - 1) * sides + j for j in range(sides)])
    return recalc(K.Part(verts, faces, swatch, bone, smooth))


def tube(points, radius, sides=6, swatch="g55", bone=None, smooth=True, caps=True, ref=None, squash=1.0):
    """A tube along a polyline. radius = float or list (0 at an end = pointed tip). squash flattens the
    section along the frame's y (straps, flat braids). Parallel-transport frames, so it never twists."""
    pts = [Vector(p) for p in points]
    n = len(pts)
    rad = list(radius) if isinstance(radius, (list, tuple)) else [radius] * n
    tans = []
    for i in range(n):
        a, b = pts[max(0, i - 1)], pts[min(n - 1, i + 1)]
        tans.append((b - a).normalized())
    x, _, _ = basis(tans[0], ref)
    frames = []
    for i in range(n):
        if i:
            q = tans[i - 1].rotation_difference(tans[i])
            x = q @ x
            x = (x - tans[i] * x.dot(tans[i])).normalized()
        frames.append((x.copy(), tans[i].cross(x)))
    verts, faces, rows = [], [], []
    for i in range(n):
        fx, fy = frames[i]
        if rad[i] <= 1e-6:
            rows.append([len(verts)])
            verts.append(pts[i])
            continue
        row = []
        for j in range(sides):
            ph = math.tau * (j + 0.5) / sides
            row.append(len(verts))
            verts.append(pts[i] + fx * (rad[i] * math.cos(ph)) + fy * (rad[i] * squash * math.sin(ph)))
        rows.append(row)
    for i in range(n - 1):
        a, b = rows[i], rows[i + 1]
        if len(a) == 1 and len(b) == 1:
            continue
        if len(a) == 1:
            for j in range(sides):
                faces.append([a[0], b[(j + 1) % sides], b[j]])
        elif len(b) == 1:
            for j in range(sides):
                faces.append([a[j], a[(j + 1) % sides], b[0]])
        else:
            for j in range(sides):
                j2 = (j + 1) % sides
                faces.append([a[j], a[j2], b[j2], b[j]])
    if caps:
        if len(rows[0]) > 1:
            faces.append(list(rows[0]))
        if len(rows[-1]) > 1:
            faces.append(list(rows[-1]))
    return recalc(K.Part(verts, faces, swatch, bone, smooth))


def slab(fn, nu, nv, thick, swatch="g55", bone=None, smooth=True, closed_u=False):
    """A thick sheet: fn(u, v) -> point for u, v in [0, 1]. The sheet is offset +/- thick/2 along its normal
    and closed by walls, so it reads from both sides (single-sided shading in Unity)."""
    nu1 = nu if closed_u else nu + 1
    P = [[Vector(fn((i % nu) / nu if closed_u else i / nu, j / nv)) for j in range(nv + 1)] for i in range(nu1)]
    e = 1e-3
    verts = []
    idx_f, idx_b = {}, {}
    for i in range(nu1):
        for j in range(nv + 1):
            u, v = (i % nu) / nu if closed_u else i / nu, j / nv
            du = Vector(fn(min(1.0, u + e) if not closed_u else u + e, v)) - Vector(fn(max(0.0, u - e) if not closed_u else u - e, v))
            dv = Vector(fn(u, min(1.0, v + e))) - Vector(fn(u, max(0.0, v - e)))
            nrm = du.cross(dv)
            nrm = nrm.normalized() if nrm.length > 1e-9 else Vector((0, 0, 1))
            th = thick(u, v) if callable(thick) else thick
            idx_f[i, j] = len(verts)
            verts.append(P[i][j] + nrm * th * 0.5)
            idx_b[i, j] = len(verts)
            verts.append(P[i][j] - nrm * th * 0.5)
    faces = []
    iu = range(nu) if closed_u else range(nu)
    for i in iu:
        i2 = (i + 1) % nu if closed_u else i + 1
        for j in range(nv):
            faces.append([idx_f[i, j], idx_f[i2, j], idx_f[i2, j + 1], idx_f[i, j + 1]])
            faces.append([idx_b[i, j + 1], idx_b[i2, j + 1], idx_b[i2, j], idx_b[i, j]])
    # walls
    for i in iu:
        i2 = (i + 1) % nu if closed_u else i + 1
        for j in (0, nv):
            faces.append([idx_f[i, j], idx_b[i, j], idx_b[i2, j], idx_f[i2, j]])
    if not closed_u:
        for i in (0, nu):
            for j in range(nv):
                faces.append([idx_f[i, j], idx_f[i, j + 1], idx_b[i, j + 1], idx_b[i, j]])
    return recalc(K.Part(verts, faces, swatch, bone, smooth))


def merge_parts(parts, swatch=None, bone=None):
    """Several parts into one (fewer objects to track)."""
    verts, faces, sws = [], [], []
    for p in parts:
        base = len(verts)
        verts += p.verts
        faces += [[i + base for i in f] for f in p.faces]
        sws += list(p.swatch)
    return K.Part(verts, faces, sws if swatch is None else swatch, bone if bone is not None else parts[0].bone, parts[0].smooth)


def mirror_x(part):
    """A mirrored copy (X flipped, winding reversed, bone .L <-> .R)."""
    verts = [Vector((-v.x, v.y, v.z)) for v in part.verts]
    faces = [list(reversed(f)) for f in part.faces]
    b = part.bone
    if b and b.endswith(".L"):
        b = b[:-2] + ".R"
    elif b and b.endswith(".R"):
        b = b[:-2] + ".L"
    p = K.Part(verts, faces, list(part.swatch), b, part.smooth)
    for attr in ("wspec", "wsrc"):
        if hasattr(part, attr):
            setattr(p, attr, getattr(part, attr))
    if hasattr(part, "wsrc"):
        p.wsrc = [Vector((-v.x, v.y, v.z)) for v in part.wsrc]
    return p


def xform(part, m):
    part.verts = [m @ v for v in part.verts]
    return part


def frame_matrix(origin, x, y, z):
    """4x4 from an origin and three axis vectors (local X, Y, Z in world)."""
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = origin
    return m


def drop_faces(part, keep):
    """A copy keeping the faces i where keep(i); unused vertices are removed."""
    faces = [(f, part.swatch[i]) for i, f in enumerate(part.faces) if keep(i)]
    used = sorted({i for f, _ in faces for i in f})
    remap = {o: n for n, o in enumerate(used)}
    return K.Part([part.verts[i] for i in used], [[remap[i] for i in f] for f, _ in faces],
                  [s for _, s in faces], part.bone, part.smooth)


def tris(parts):
    return sum(len(f) - 2 for p in parts for f in p.faces)


def islands(part):
    """Connected pieces of a part, as separate Parts."""
    parent = list(range(len(part.verts)))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for f in part.faces:
        for i in f[1:]:
            ra, rb = find(f[0]), find(i)
            if ra != rb:
                parent[ra] = rb
    groups = {}
    for fi, f in enumerate(part.faces):
        groups.setdefault(find(f[0]), []).append(fi)
    out = []
    for fis in groups.values():
        s = set(fis)
        out.append(drop_faces(part, lambda i: i in s))
    return out


def hull(points, swatch="g55", bone=None, smooth=False):
    bm = bmesh.new()
    for p in points:
        bm.verts.new(Vector(p))
    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    for g in res.get("geom_interior", []) + res.get("geom_unused", []):
        if isinstance(g, bmesh.types.BMVert) and g.is_valid:
            bm.verts.remove(g)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.verts.index_update()
    verts = [v.co.copy() for v in bm.verts]
    faces = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return K.Part(verts, faces, swatch, bone, smooth)


def cut(part, co, no, region=None):
    """Bisects the faces (whose center passes region(center)) along a plane: clean paint seams on the body."""
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in part.verts]
    for f in part.faces:
        try:
            bm.faces.new([vs[i] for i in f])
        except ValueError:
            pass
    faces = [f for f in bm.faces if region is None or region(f.calc_center_median())]
    if faces:
        edges = list({e for f in faces for e in f.edges})
        verts = list({v for f in faces for v in f.verts})
        bmesh.ops.bisect_plane(bm, geom=faces + edges + verts, dist=1e-5, plane_co=Vector(co), plane_no=Vector(no))
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.verts.index_update()
    part.verts = [v.co.copy() for v in bm.verts]
    part.faces = [[v.index for v in f.verts] for f in bm.faces]
    part.swatch = [part.swatch[0]] * len(part.faces)
    bm.free()
    return part


def recolor(part, fn):
    """Per-face swatch from fn(face_center, old_swatch)."""
    out = []
    for fi, f in enumerate(part.faces):
        c = sum((part.verts[i] for i in f), Vector()) / len(f)
        out.append(fn(c, part.swatch[fi]) or part.swatch[fi])
    part.swatch = out
    return part


# =============================================================================================
# The body: normals, raycasts, radius, shells
# =============================================================================================

class Body:
    """Wraps the skin_body Part: BVH for raycasts, vertex normals, the skeleton segments."""

    def __init__(self, part, skel):
        self.part = part
        self.skel = skel
        self.tree = BVHTree.FromPolygons([tuple(v) for v in part.verts], part.faces)
        nrm = [Vector() for _ in part.verts]
        for f in part.faces:
            a, b, c = (part.verts[i] for i in f[:3])
            fn = (b - a).cross(c - a)
            for i in f:
                nrm[i] += fn
        self.vnorm = [n.normalized() if n.length > 0 else Vector((0, 0, 1)) for n in nrm]
        self.segs = K._seg_info(skel)
        self.J = {n: p.copy() for n, (p, r) in skel.joints.items()}
        self.fbone = []
        self.ft = []
        for f in part.faces:
            c = sum((part.verts[i] for i in f), Vector()) / len(f)
            i, t, _ = K._closest_seg(c, self.segs)
            self.fbone.append(self.segs[i][2])
            self.ft.append(t)

    def hit(self, target, outward, far=0.6):
        """Outer surface point near `target`, approached from the `outward` side: (location, normal)."""
        d = Vector(outward).normalized()
        loc, nrm, _, _ = self.tree.ray_cast(Vector(target) + d * far, -d, far * 2)
        if loc is None:
            return Vector(target), d
        return loc, nrm

    def radius(self, a, b, t, band=0.012):
        """Max distance of body vertices from the a->b axis around parameter t (fits straps, cuffs)."""
        a, b = Vector(a), Vector(b)
        ax = b - a
        L = ax.length
        ax /= L
        c = a + ax * (L * t)
        best = 0.0
        for v in self.part.verts:
            h = (v - c).dot(ax)
            if abs(h) < band:
                r = ((v - c) - ax * h).length
                if r < 0.25:
                    best = max(best, r)
        return best

    def face_centers(self):
        P = self.part
        return [sum((P.verts[i] for i in f), Vector()) / len(f) for f in P.faces]

    def shell(self, select, offset, swatch, bone=None, walls=True, sink=0.006, smooth=True):
        """A garment layer grown from the body: the faces whose center passes select(center, normal) are
        copied, pushed out by `offset` (float or fn(p) -> float) along the vertex normals, and closed by side
        walls that start inside the body. Weights are copied from the source body vertices."""
        P = self.part
        cs = self.face_centers()
        sel = []
        for fi, f in enumerate(P.faces):
            a, b, c = (P.verts[i] for i in f[:3])
            bn = self.fbone[fi] if fi < len(self.fbone) else ""
            if select(cs[fi], (b - a).cross(c - a).normalized(), bn):
                sel.append(fi)
        used = sorted({i for fi in sel for i in P.faces[fi]})
        out_i, in_i = {}, {}
        verts, src = [], []
        for i in used:
            o = offset(P.verts[i]) if callable(offset) else offset
            out_i[i] = len(verts)
            verts.append(P.verts[i] + self.vnorm[i] * o)
            src.append(P.verts[i].copy())
        faces = [[out_i[i] for i in P.faces[fi]] for fi in sel]
        if walls:
            count = {}
            for fi in sel:
                f = P.faces[fi]
                for k in range(len(f)):
                    e = (f[k], f[(k + 1) % len(f)])
                    key = tuple(sorted(e))
                    count[key] = count.get(key, 0) + 1
            for fi in sel:
                f = P.faces[fi]
                fc = cs[fi]
                for k in range(len(f)):
                    a, b = f[k], f[(k + 1) % len(f)]
                    if count[tuple(sorted((a, b)))] != 1:
                        continue
                    for i in (a, b):
                        if i not in in_i:
                            in_i[i] = len(verts)
                            verts.append(P.verts[i] - self.vnorm[i] * sink)
                            src.append(P.verts[i].copy())
                    # outer edge runs a->b like the face loop; the wall is (b_out, a_out, a_in, b_in)
                    quad = [out_i[b], out_i[a], in_i[a], in_i[b]]
                    q0, q1, q2 = (verts[j] for j in quad[:3])
                    nrm = (q1 - q0).cross(q2 - q1)
                    mid = (P.verts[a] + P.verts[b]) / 2
                    if nrm.dot(mid - fc) < 0:
                        quad.reverse()
                    faces.append(quad)
        part = K.Part(verts, faces, swatch, bone, smooth)
        if bone is None:
            part.wsrc = src
            part.wspec = "src"
        return part


# =============================================================================================
# Weights post-pass and extra bones
# =============================================================================================

def rigid(part, bone):
    """Marks a part rigid on `bone`, which may be an extra bone the kit does not know (set after add_variant)."""
    part.bone = None
    part.wspec = ("rigid", bone)
    return part


def weighted(part, fn):
    """Marks a part with custom weights fn(rest_position) -> {bone: weight}."""
    part.bone = None
    part.wspec = ("fn", fn)
    return part


def trunk_weights(skel, bones=("Hips", "Spine", "Chest")):
    """Weights like the body, but only from the trunk (keeps vests and belts off the arms)."""
    segs = [s for s in K._seg_info(skel) if s[2] in bones]

    def fn(p):
        return K._weights_for(p, segs, skel)
    return fn


def apply_weights(obj, parts, skel):
    """Re-weights the marked parts of a variant built by Character.add_variant (same vertex order)."""
    segs = K._seg_info(skel)
    groups = {g.name: g for g in obj.vertex_groups}
    off = 0
    jobs = []
    for part in parts:
        n = len(part.verts)
        spec = getattr(part, "wspec", None)
        if spec is not None:
            for k in range(n):
                co = part.verts[k]
                if spec == "src":
                    w = K._weights_for(part.wsrc[k], segs, skel)
                elif spec[0] == "rigid":
                    w = {spec[1]: 1.0}
                else:
                    w = spec[1](co)
                tot = sum(w.values()) or 1.0
                jobs.append((off + k, {b: x / tot for b, x in w.items() if x > 1e-4}))
        off += n
    assert off == len(obj.data.vertices), (off, len(obj.data.vertices))
    if not jobs:
        return
    idx = [j[0] for j in jobs]
    for g in obj.vertex_groups:
        g.remove(idx)
    for vi, w in jobs:
        for bn, wt in w.items():
            if bn not in groups:
                groups[bn] = obj.vertex_groups.new(name=bn)
            groups[bn].add([vi], wt, "REPLACE")


def add_bones(rig, specs):
    """specs: [(name, head, tail, parent)]. Bones are not connected; roll keeps local X near world X."""
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.scene.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    for name, head, tail, parent in specs:
        b = eb.new(name)
        b.head = Vector(head)
        b.tail = Vector(tail)
        d = (b.tail - b.head).normalized()
        b.align_roll(Vector((0, -1, 0)) if abs(d.z) > 0.7 else Vector((0, 0, 1)))
        b.parent = eb[parent]
        b.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")


# =============================================================================================
# Pose system (armature space) + IK
# =============================================================================================

def rx(a):
    return Quaternion((1, 0, 0), math.radians(a))


def ry(a):
    return Quaternion((0, 1, 0), math.radians(a))


def rz(a):
    return Quaternion((0, 0, 1), math.radians(a))


def rot(x=0.0, y=0.0, z=0.0):
    """Armature-space rotation: X first, then Y, then Z (degrees). +x tips up-vectors forward (-Y)."""
    return rz(z) @ ry(y) @ rx(x)


def qframe(a, b):
    """Rotation matrix with columns (a, b', a x b') where b' is b made orthogonal to a."""
    a = Vector(a).normalized()
    b = Vector(b)
    b = (b - a * b.dot(a)).normalized()
    return Matrix((a, b, a.cross(b))).transposed()


def aim(rest_a, rest_b, a, b):
    """Rotation taking the rest frame (rest_a, rest_b) onto (a, b)."""
    return (qframe(a, b) @ qframe(rest_a, rest_b).transposed()).to_quaternion()


class RigInfo:
    def __init__(self, c):
        self.c = c
        bones = c.rig.data.bones
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in bones}
        self.head = {b.name: b.head_local.copy() for b in bones}
        self.tail = {b.name: b.tail_local.copy() for b in bones}
        self.mq = {b.name: b.matrix_local.to_3x3().to_quaternion() for b in bones}
        order, seen = [], set()

        def visit(n):
            if n in seen:
                return
            p = self.parent[n]
            if p:
                visit(p)
            seen.add(n)
            order.append(n)
        for b in bones:
            visit(b.name)
        self.order = order

    def dir(self, b):
        return (self.tail[b] - self.head[b]).normalized()

    def length(self, b):
        return (self.tail[b] - self.head[b]).length


class Pose:
    """Absolute armature-space rotation deltas D[bone] (from rest); unset bones follow their parent rigidly.
    offset[bone] moves a bone's head (world offset, used on Hips). Set parents before children."""

    def __init__(self, R):
        self.R = R
        self.D = {}
        self.offset = {}
        self.scale = {}

    def d(self, b):
        while b is not None:
            if b in self.D:
                return self.D[b]
            b = self.R.parent[b]
        return Quaternion()

    def head(self, b):
        p = self.R.parent[b]
        h = self.R.head[b] if p is None else self.point(p, self.R.head[b])
        if b in self.offset:
            h = h + self.d(p) @ Vector(self.offset[b]) if p else h + Vector(self.offset[b])
        return h

    def point(self, b, rest_pt):
        """Where a point rigidly attached to bone b (given in rest coordinates) is in this pose."""
        return self.head(b) + self.d(b) @ (Vector(rest_pt) - self.R.head[b])

    def rel(self, b, q):
        """Rotation relative to the parent, in armature axes (like the kit's c.q)."""
        self.D[b] = self.d(self.R.parent[b]) @ q
        return self

    def abs(self, b, q):
        self.D[b] = q.copy()
        return self

    def kit(self):
        """{bone: local quaternion | (q, offset) | dict} for Character.action()."""
        out = {}
        for b in self.R.order:
            if b not in self.D and b not in self.offset and b not in self.scale:
                continue
            p = self.R.parent[b]
            relq = self.d(p).inverted() @ self.d(b)
            M = self.R.mq[b]
            local = M.inverted() @ relq @ M
            if b in self.scale:
                out[b] = {"rot": local, "scale": self.scale[b]}
                if b in self.offset:
                    out[b]["loc"] = self.offset[b]
            elif b in self.offset:
                out[b] = (local, Vector(self.offset[b]))
            else:
                out[b] = local
        return out

    def copy(self):
        p = Pose(self.R)
        p.D = {k: v.copy() for k, v in self.D.items()}
        p.offset = {k: Vector(v) for k, v in self.offset.items()}
        p.scale = dict(self.scale)
        return p


def two_bone(P, upper, lower, target, pole, rest_pole, twist=0.0):
    """Two-bone IK: aims upper/lower so the end of `lower` reaches `target`, bending toward `pole`.
    rest_pole = direction the joint bends toward in the rest pose (forward for knees, back for elbows).
    twist rolls the lower bone around its own axis (degrees). Returns the joint position."""
    R = P.R
    S = P.head(upper)
    l1 = (R.head[lower] - R.head[upper]).length
    l2 = R.length(lower)
    d = Vector(target) - S
    dist = max(abs(l1 - l2) + 1e-4, min(d.length, (l1 + l2) * 0.9995))
    u = d.normalized()
    pv = Vector(pole) - u * Vector(pole).dot(u)
    if pv.length < 1e-6:
        pv = u.orthogonal()
    v = pv.normalized()
    ca = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
    A = math.acos(max(-1.0, min(1.0, ca)))
    E = S + (u * math.cos(A) + v * math.sin(A)) * l1
    W = S + u * dist
    n = u.cross(v)                       # hinge axis
    ra_u = (R.head[lower] - R.head[upper]).normalized()
    ra_l = R.dir(lower)
    rp = Vector(rest_pole)
    rn_u = ra_u.cross(rp - ra_u * rp.dot(ra_u)).normalized()
    rn_l = ra_l.cross(rp - ra_l * rp.dot(ra_l)).normalized()
    au = (E - S).normalized()
    al = (W - E).normalized()
    Du = aim(ra_u, rn_u, au, n)
    Dl = aim(ra_l, rn_l, al, n)
    if twist:
        Dl = Quaternion(al, math.radians(twist)) @ Dl
    P.abs(upper, Du)
    P.abs(lower, Dl)
    return E


def ease(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def blend_pose(a, b, t):
    """Slerp of every absolute delta and lerp of offsets between two Poses."""
    p = Pose(a.R)
    for k in set(a.D) | set(b.D):
        p.D[k] = a.d(k).slerp(b.d(k), t)
    for k in set(a.offset) | set(b.offset):
        p.offset[k] = Vector(a.offset.get(k, (0, 0, 0))).lerp(Vector(b.offset.get(k, (0, 0, 0))), t)
    for k in set(a.scale) | set(b.scale):
        sa, sb = a.scale.get(k, 1.0), b.scale.get(k, 1.0)
        if isinstance(sa, (int, float)) and isinstance(sb, (int, float)):
            p.scale[k] = lerp(sa, sb, t)
        else:
            p.scale[k] = tuple(lerp(Vector(sa if not isinstance(sa, (int, float)) else (sa,) * 3)[i],
                                    Vector(sb if not isinstance(sb, (int, float)) else (sb,) * 3)[i], t) for i in range(3))
    return p


# =============================================================================================
# Renders
# =============================================================================================

class Stage:
    """Camera, sun, ground and world for previews (same look as the kit's preview)."""

    def __init__(self, size=420):
        sc = bpy.context.scene
        K._engine()
        sc.render.resolution_x = size
        sc.render.resolution_y = size
        sc.render.film_transparent = False
        try:
            sc.view_settings.view_transform = "Standard"
        except TypeError:
            pass
        world = bpy.data.worlds.get("PreviewWorld") or bpy.data.worlds.new("PreviewWorld")
        try:
            world.use_nodes = True
        except AttributeError:
            pass
        bg = world.node_tree.nodes.get("Background")
        bg.inputs[0].default_value = (0.32, 0.35, 0.39, 1)
        bg.inputs[1].default_value = 0.9
        sc.world = world
        gme = bpy.data.meshes.new("pv_ground")
        g = 30
        gme.from_pydata([(-g, -g, 0), (g, -g, 0), (g, g, 0), (-g, g, 0)], [], [(0, 1, 2, 3)])
        self.ground = K.link(bpy.data.objects.new("pv_ground", gme))
        gmat = bpy.data.materials.new("pv_ground_mat")
        try:
            gmat.use_nodes = True
        except AttributeError:
            pass
        gmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.08, 0.085, 0.09, 1)
        gme.materials.append(gmat)
        sun_d = bpy.data.lights.new("pv_sun", "SUN")
        sun_d.energy = 3.2
        self.sun = K.link(bpy.data.objects.new("pv_sun", sun_d))
        self.sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
        cam_d = bpy.data.cameras.new("pv_cam")
        cam_d.type = "ORTHO"
        self.cam = K.link(bpy.data.objects.new("pv_cam", cam_d))
        sc.camera = self.cam
        self.size = size
        self.tmp = os.path.join(K.PREVIEWS, "_tmp_passeur.png")
        os.makedirs(K.PREVIEWS, exist_ok=True)

    def shot(self, objs, center, radius, az, el):
        sc = bpy.context.scene
        for o in sc.objects:
            if o.type == "MESH" and o is not self.ground:
                o.hide_render = o not in objs
        a, e = math.radians(az), math.radians(el)
        d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        self.cam.data.ortho_scale = radius * 2.3
        self.cam.location = Vector(center) + d * radius * 8
        self.cam.data.clip_end = radius * 20
        self.cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = self.tmp
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(self.tmp)
        arr = np.array(im.pixels[:], dtype=np.float32).reshape(self.size, self.size, 4)
        bpy.data.images.remove(im)
        return arr

    def close(self):
        for o in (self.ground, self.sun, self.cam):
            bpy.data.objects.remove(o)
        for o in bpy.context.scene.objects:
            o.hide_render = False
        if os.path.exists(self.tmp):
            os.remove(self.tmp)


def bounds(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in objs:
        ev = o.evaluated_get(dg)
        m = ev.to_mesh()
        pts += [o.matrix_world @ v.co for v in m.vertices]
        ev.to_mesh_clear()
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def save_sheet(name, rows):
    """rows: list of lists of tiles (H x W x 4), all the same size."""
    sheet = np.concatenate([np.concatenate(r, axis=1) for r in reversed(rows)], axis=0)
    out = bpy.data.images.new("pv_sheet_" + name, sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = os.path.join(K.PREVIEWS, name + ".png")
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    return os.path.join(K.PREVIEWS, name + ".png")


def sheet(name, groups, views=((20, 58),), size=300, frames=(None,), action=None, rig=None,
          center=None, radius=None, cols=None, fixed=True):
    """Renders each group (list of objects) from each view at each frame. Rows = groups x frames
    (or a grid of `cols` tiles). center/radius fix the camera (else the bounds of the first group)."""
    st = Stage(size)
    sc = bpy.context.scene
    if action is not None and rig is not None:
        rig.animation_data.action = action
    tiles = []
    cam = None
    for g in groups:
        for fr in frames:
            if fr is not None:
                sc.frame_set(fr)
            if cam is None or not fixed:
                if center is None:
                    lo, hi = bounds(g)
                    c = (lo + hi) / 2
                    r = max((hi - lo).length / 2, 0.2)
                else:
                    c, r = Vector(center), radius
                cam = (c, r)
            row = [st.shot(g, cam[0], cam[1], az, el) for az, el in views]
            tiles.append(row)
    st.close()
    if cols:
        flat = [t for r in tiles for t in r]
        blank = np.zeros_like(flat[0])
        rows = [flat[i:i + cols] for i in range(0, len(flat), cols)]
        if len(rows[-1]) < cols:
            rows[-1] += [blank] * (cols - len(rows[-1]))
        return save_sheet(name, rows)
    return save_sheet(name, tiles)


# =============================================================================================
# Checks
# =============================================================================================

def posed_points(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    m = ev.to_mesh()
    pts = [obj.matrix_world @ v.co for v in m.vertices]
    ev.to_mesh_clear()
    return pts


def min_z_over(obj, rig, action, frames):
    rig.animation_data.action = action
    sc = bpy.context.scene
    out = []
    for f in frames:
        sc.frame_set(f)
        out.append(min(p.z for p in posed_points(obj)))
    return out
