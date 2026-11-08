"""
Helpers for the CORDON world dressing: the Ostrov decor kit, the backdrop and the ground texture.
Used by decor_ostrov.py, backdrop.py, ground_ostrov.py and street_scene.py (same folder).

Builds on cordon_kit (Part, build_prop, palette, export) without changing it:
- geometry: prisms / profile extrusions, pillows (sandbags), blobs, tori, limbs between two points, panes;
- z cuts: bisect parts on horizontal planes and recolor by height (the flood line, the Ligne; color bands);
- make(): build_prop + triangulation + recentering (origin = bottom center) + manifest metadata;
- renders: per-prop contact sheets on the real ground texture, and free cameras for scenes;
- manifest: sizes and colliders in Unity axes (x = width, y = height, z = depth).
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.noise import noise as perlin

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
import cordon_kit as K  # noqa: E402

V = Vector
GROUND_TEX = os.path.join(K.ART, "Textures", "T_Ground_Ostrov.png")
BUDGET = {"small": 300, "medium": 1000, "large": 2500, "backdrop": 3000}

# swatches that keep their color under the flood line (lights, the Seve itself)
NO_MUD = {"lamp_cold", "amber_glow", "amber_glow_soft", "amber", "crust", "crust_dark", "cyan"}


def args():
    """Blender args after '--' as a dict: -- only=Car_A,Car_B fast=1"""
    out = {}
    if "--" in sys.argv:
        for a in sys.argv[sys.argv.index("--") + 1:]:
            if "=" in a:
                k, v = a.split("=", 1)
                out[k] = v
    return out


# ---------------------------------------------------------------------------------------------
# Geometry
# ---------------------------------------------------------------------------------------------

def flat(parts):
    out = []
    for p in parts:
        if isinstance(p, (list, tuple)):
            out += flat(p)
        elif p is not None:
            out.append(p)
    return out


def M(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    return K._mat(loc, rot, scale)


def xform(parts, m):
    for p in flat(parts):
        p.transformed(m)
    return parts


def about(parts, pivot, rot):
    """Rotates parts by euler `rot` (deg) around `pivot`."""
    pv = V(pivot)
    return xform(parts, Matrix.Translation(pv) @ M(rot=rot) @ Matrix.Translation(-pv))


def recolor(parts, swatch):
    for p in flat(parts):
        p.swatch = [swatch] * len(p.faces)
    return parts


def speckle(parts, choices, seed=0):
    """Random swatch per face from a list of (swatch, weight)."""
    rnd = random.Random(seed)
    names = [c[0] for c in choices]
    w = [c[1] for c in choices]
    for p in flat(parts):
        p.swatch = [rnd.choices(names, w)[0] for _ in p.faces]
    return parts


def _signed_area(pts):
    return sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1])) * 0.5


def prism(pts, z0, z1, top, side=None, bottom=None, inset=0.0, top_z=None, sides=True):
    """Extrudes a 2D outline (XY) from z0 to z1. inset (0..1) shrinks the top toward the centroid (slanted
    sides), top_z gives per-vertex heights of the top ring (curled plates). bottom: swatch or None (open)."""
    pts = [tuple(p[:2]) for p in pts]
    tz = list(top_z) if top_z else None
    if _signed_area(pts) < 0:
        pts = pts[::-1]
        if tz:
            tz = tz[::-1]
    n = len(pts)
    cx = sum(p[0] for p in pts) / n
    cy = sum(p[1] for p in pts) / n
    verts = [(x, y, z0) for x, y in pts]
    for i, (x, y) in enumerate(pts):
        verts.append((x + (cx - x) * inset, y + (cy - y) * inset, tz[i] if tz else z1))
    faces = [list(range(n, 2 * n))]
    sws = [top]
    for i in range(n if sides else 0):
        j = (i + 1) % n
        faces.append([i, j, n + j, n + i])
        sws.append(side or top)
    if bottom:
        faces.append(list(range(n - 1, -1, -1)))
        sws.append(bottom)
    return K.Part(verts, faces, sws)


_YZX = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # local (a, b, c) -> world (c, a, b)
_XZY = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # local (a, b, c) -> world (a, -c, b)


def extrude_yz(profile, x0, x1, cap, side=None, cap2=None, inset=0.0):
    """A (y, z) profile extruded along X from x0 to x1 (car bodies, jersey barriers). cap = +X end face."""
    return prism(profile, x0, x1, cap, side, cap2 or cap, inset).transformed(_YZX)


def extrude_xz(profile, y0, y1, front, side=None, back=None, inset=0.0):
    """An (x, z) profile extruded along Y between y0 < y1 (walls, signs). front = the -Y face."""
    p = prism(profile, -y1, -y0, front, side, back or front, inset)
    return p.transformed(_XZY)


def poly(pts3d, swatch):
    """One planar face from 3D points (counter-clockwise seen from the side it faces)."""
    return K.Part([V(p) for p in pts3d], [list(range(len(pts3d)))], swatch)


def quad_on(corners, swatch, shrink=0.0, offset=0.006):
    """A pane on a face: corners (4 world points, CCW seen from outside) shrunk toward the center and pushed out."""
    c = [V(p) for p in corners]
    ctr = sum(c, V()) / 4
    n = (c[1] - c[0]).cross(c[2] - c[0]).normalized()
    pts = [ctr + (p - ctr) * (1 - shrink) + n * offset for p in c]
    return poly(pts, swatch)


def lerp(a, b, t):
    return V(a).lerp(V(b), t)


def pillow(size, loc=(0, 0, 0), rot=(0, 0, 0), swatch="sandbag", puff=0.14, sag=0.0):
    """A filled bag: a box whose top and bottom rings are pulled in (20 tris). sag lowers the top center."""
    sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    k = 1 - puff
    verts = []
    for z, s in ((-sz, k), (0, 1.0), (sz, k)):
        for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            verts.append((x * sx * s, y * sy * s, z))
    faces = [[3, 2, 1, 0], [8, 9, 10, 11]]
    for ring in (0, 4):
        for i in range(4):
            j = (i + 1) % 4
            faces.append([ring + i, ring + j, ring + 4 + j, ring + 4 + i])
    p = K.Part(verts, faces, swatch)
    if sag:
        for v in p.verts[8:]:
            v.z -= sag
    return p.transformed(M(loc, rot))


def blob(radius, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), swatch="g25", seg=6, rings=4, jitter=0.15, seed=0, smooth=False):
    """A low-poly lumpy sphere (bags, bushes, tarps, mud lumps)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=radius)
    for v in bm.verts:
        v.co *= 1.0 + (rnd.random() * 2 - 1) * jitter
    return K._from_bm(bm, swatch, None, smooth, M(loc, rot, scale))


def chunk(radius, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), swatch="rubble", jitter=0.25, seed=0):
    """An angular rubble chunk: a jittered icosahedron (20 tris)."""
    return K.ico(radius, loc, rot, scale, swatch=swatch, subdiv=1, jitter=jitter, seed=seed)


def torus(R, r, loc=(0, 0, 0), rot=(0, 0, 0), swatch="tire", seg=8, sides=4, smooth=False):
    """Torus around local Z. With sides=4 the section is a square (flat top: tires)."""
    verts, faces = [], []
    for i in range(seg):
        a = math.tau * i / seg
        for j in range(sides):
            b = math.tau * j / sides + math.pi / sides
            rr = R + r * math.cos(b)
            verts.append((rr * math.cos(a), rr * math.sin(a), r * math.sin(b)))
    for i in range(seg):
        for j in range(sides):
            i2, j2 = (i + 1) % seg, (j + 1) % sides
            faces.append([i * sides + j, i2 * sides + j, i2 * sides + j2, i * sides + j2])
    return K.Part(verts, faces, swatch, smooth=smooth).transformed(M(loc, rot))


def limb(p0, p1, r0, r1, swatch="bark", seg=5, cap=False, spin=0.0):
    """A tapered cylinder from p0 to p1 (branches, pipes, poles, rebar)."""
    p0, p1 = V(p0), V(p1)
    d = p1 - p0
    L = max(d.length, 1e-4)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=L)
    rot = V((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    m = Matrix.Translation(p0) @ rot @ Matrix.Rotation(spin, 4, "Z") @ Matrix.Translation((0, 0, L / 2))
    return K._from_bm(bm, swatch, None, False, m)


def disc(radius, depth, loc=(0, 0, 0), rot=(0, 0, 0), swatch="g25", seg=8, radius2=None):
    return K.cyl(radius, depth, loc, rot, swatch=swatch, segments=seg, radius2=radius2, smooth=False)


def blob_outline(n, rx, ry, seed=0, wobble=0.25, center=(0, 0)):
    """An irregular closed outline (puddles, stains, mounds): n points around an ellipse."""
    rnd = random.Random(seed)
    off = V((rnd.random() * 50, rnd.random() * 50, 0))
    pts = []
    for i in range(n):
        a = math.tau * i / n
        c, s = math.cos(a), math.sin(a)
        k = 1.0 + wobble * perlin(off + V((c * 1.3, s * 1.3, 0))) + (rnd.random() - 0.5) * wobble * 0.3
        pts.append((center[0] + c * rx * k, center[1] + s * ry * k))
    return pts


def voronoi_cells(points, box):
    """Clipped Voronoi cells of 2D points inside box (x0, y0, x1, y1): half-plane clipping, small n only."""
    x0, y0, x1, y1 = box
    cells = []
    for i, p in enumerate(points):
        poly2 = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
        for j, q in enumerate(points):
            if i == j:
                continue
            mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
            nx, ny = q[0] - p[0], q[1] - p[1]
            poly2 = _clip(poly2, mx, my, nx, ny)
            if not poly2:
                break
        cells.append(poly2)
    return cells


def _clip(pts, mx, my, nx, ny):
    """Keeps the part of the polygon on the side (p - m) . n <= 0."""
    out = []
    n = len(pts)
    for k in range(n):
        a, b = pts[k], pts[(k + 1) % n]
        da = (a[0] - mx) * nx + (a[1] - my) * ny
        db = (b[0] - mx) * nx + (b[1] - my) * ny
        if da <= 0:
            out.append(a)
        if (da < 0 < db) or (db < 0 < da):
            t = da / (da - db)
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out


def plates(outline_fn, box, cell=0.22, z=(0.008, 0.02), gap=0.14, swatches=(("brown_2", 1),), seed=0, curl=0.006, sides=False):
    """Cracked dried-mud plates (the Croute): Voronoi cells of a jittered grid (spacing `cell`) inside an outline,
    shrunk toward their centers (the cracks show the ground), flat tops with curled-up rims. Border cells shrink more,
    so the patch frays out. outline_fn(x, y) -> True if the point belongs to the patch."""
    rnd = random.Random(seed)
    x0, y0, x1, y1 = box
    pts = []
    y = y0 + cell * 0.5
    while y < y1:
        x = x0 + cell * 0.5
        while x < x1:
            p = (x + (rnd.random() - 0.5) * cell * 0.8, y + (rnd.random() - 0.5) * cell * 0.8)
            if outline_fn(*p):
                pts.append(p)
            x += cell
        y += cell
    cells = []
    for i, p in enumerate(pts):
        near = [q for q in pts if q is not p and abs(q[0] - p[0]) < cell * 3 and abs(q[1] - p[1]) < cell * 3]
        poly2 = [(p[0] - cell * 1.5, p[1] - cell * 1.5), (p[0] + cell * 1.5, p[1] - cell * 1.5),
                 (p[0] + cell * 1.5, p[1] + cell * 1.5), (p[0] - cell * 1.5, p[1] + cell * 1.5)]
        for q in near:
            poly2 = _clip(poly2, (p[0] + q[0]) / 2, (p[1] + q[1]) / 2, q[0] - p[0], q[1] - p[1])
            if not poly2:
                break
        cells.append(poly2)
    names = [s for s, _ in swatches]
    w = [wt for _, wt in swatches]
    out = []
    for p, c in zip(pts, cells):
        if len(c) < 3:
            continue
        cx = sum(v[0] for v in c) / len(c)
        cy = sum(v[1] for v in c) / len(c)
        g = gap * (0.7 + rnd.random() * 0.6)
        if not all(outline_fn(*v) for v in c):
            g = 0.35 + rnd.random() * 0.3  # fraying border
        shr = [(cx + (v[0] - cx) * (1 - g), cy + (v[1] - cy) * (1 - g)) for v in c]
        zt = z[0] + rnd.random() * (z[1] - z[0])
        tz = [zt + curl * (0.5 + rnd.random()) for _ in shr]
        out.append(prism(shr, 0.0, zt, rnd.choices(names, w)[0], top_z=tz, sides=sides))
    return out


# ---------------------------------------------------------------------------------------------
# Horizontal cuts: the flood line (la Ligne) and color bands
# ---------------------------------------------------------------------------------------------

def _part_bm(part):
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in part.verts]
    lay = bm.faces.layers.int.new("sw")
    names = []
    for f, s in zip(part.faces, part.swatch):
        try:
            face = bm.faces.new([vs[i] for i in f])
        except ValueError:
            continue
        if s not in names:
            names.append(s)
        face[lay] = names.index(s)
    return bm, lay, names


def _bm_part(bm, lay, names, part, fn=None):
    bm.verts.index_update()
    verts = [v.co.copy() for v in bm.verts]
    faces, sws = [], []
    for f in bm.faces:
        s = names[f[lay]]
        if fn:
            s = fn(f.calc_center_median(), s, f.normal) or s
        faces.append([v.index for v in f.verts])
        sws.append(s)
    out = K.Part(verts, faces, sws, part.bone, part.smooth, part.uv_scale)
    bm.free()
    return out


def zcut(parts, cuts, fn, only=None):
    """Bisects every part crossing the horizontal planes z in `cuts`, then fn(face_center, swatch, normal)
    may return a new swatch. only(part) -> bool limits the parts touched."""
    out = []
    for p in flat(parts):
        if only and not only(p):
            out.append(p)
            continue
        zs = [v.z for v in p.verts]
        lo, hi = min(zs), max(zs)
        if lo > max(cuts) + 0.5:
            out.append(p)
            continue
        bm, lay, names = _part_bm(p)
        for z in cuts:
            if lo + 1e-4 < z < hi - 1e-4:
                geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
                bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=(0, 0, z), plane_no=(0, 0, 1))
        out.append(_bm_part(bm, lay, names, p, fn))
    return out


def flood(parts, h=0.40, line=0.035, seed=0, keep=()):
    """La Ligne: everything below h was under the Crue. Faces below get a dark flood-mud stain (mud_wet / brown_2,
    a few mud_dry flecks: the bright 'croute sechee' swatch read as a wooden plinth from the camera), a darker
    tide line (brown_1) sits just under h."""
    rnd = random.Random(seed)
    keep = set(keep) | NO_MUD

    def fn(c, s, n):
        if s in keep:
            return None
        kind = K.PALETTE[K.sw(s)][2]
        if c.z < h - line:
            if kind in ("rubber", "glass") or s.startswith("g0") or s.startswith("g1"):
                return "brown_1"
            r = rnd.random()
            return "mud_wet" if r < 0.55 else ("brown_2" if r < 0.9 else "mud_dry")
        if c.z < h:
            return "brown_1"
        return None
    return zcut(parts, [h - line, h], fn)


def band(parts, z0, z1, swatch, only=None, sides_only=True):
    """Paints a horizontal band (REGEN cyan stripe, chimney rings) between z0 and z1."""
    def fn(c, s, n):
        if z0 < c.z < z1 and (not sides_only or abs(n.z) < 0.7):
            return swatch
        return None
    return zcut(parts, [z0, z1], fn, only)


# ---------------------------------------------------------------------------------------------
# Building objects + metadata
# ---------------------------------------------------------------------------------------------

def bounds(parts):
    vs = [v for p in flat(parts) for v in p.verts]
    lo = V((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = V((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def triangulate(obj):
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bm.to_mesh(me)
    bm.free()
    me.update()


REG = []


def make(name, parts, kind, cluster, weight, budget, blocks=False, core=None, collider=None,
         shrink=0.92, ao=0.5, grime=0.25, grime_height=0.5, seed=7, center=True, center_on=None, note=""):
    """Builds one prop (origin = bottom center of its bounds), triangulated, registered for the manifest.
    center_on: parts whose footprint center becomes the origin (default: everything).
    collider: None = bounds of `core` parts (or of everything), footprint scaled by `shrink`;
    or ((sx, sy, sz), (cx, cy, cz)) in Blender axes, relative to the final origin."""
    parts = flat(parts)
    lo, hi = bounds(parts)
    clo, chi = bounds(flat(center_on)) if center_on else (lo, hi)
    off = V(((clo.x + chi.x) / 2, (clo.y + chi.y) / 2, 0.0)) if center else V((0, 0, 0))
    obj = K.build_prop(name, parts, ao=ao, grime=grime, grime_height=grime_height, seed=seed, origin=off)
    triangulate(obj)
    lo, hi = lo - off, hi - off
    col = None
    if blocks:
        if collider is None:
            clo, chi = bounds(flat(core)) if core else (lo + off, hi + off)
            clo, chi = clo - off, chi - off
            size = V(((chi.x - clo.x) * shrink, (chi.y - clo.y) * shrink, chi.z - max(clo.z, 0.0)))
            ctr = V(((clo.x + chi.x) / 2, (clo.y + chi.y) / 2, max(clo.z, 0.0) + size.z / 2))
        else:
            size, ctr = V(collider[0]), V(collider[1])
        col = (size, ctr)
    tris = K.tri_count(obj)
    lo = V((lo.x, lo.y, max(lo.z, 0.0)))  # what is buried (rubble, mud lumps, the container base) does not count
    meta = dict(name=name, kind=kind, cluster=cluster, weight=weight, budget=budget, blocks=blocks,
                lo=lo, hi=hi, collider=col, tris=tris, note=note)
    REG.append((obj, meta))
    lim = BUDGET.get(budget, 99999)
    flag = "" if tris <= lim else "  OVER BUDGET (%d)" % lim
    if bounds(parts)[0].z < -0.35:
        flag += "  DEEP BELOW GROUND %.2f" % bounds(parts)[0].z
    K.report(asset=name, tris=tris, size="%.2fx%.2fx%.2f" % (hi.x - lo.x, hi.y - lo.y, hi.z - lo.z), budget=budget + flag)
    return obj


def _r(v, n=3):
    return [round(float(x), n) for x in v]


def manifest_entries(reg=None):
    out = []
    for obj, m in (reg or REG):
        lo, hi = m["lo"], m["hi"]
        # Unity: x = width (Blender X, mirrored), y = height (Blender Z), z = depth (Blender -Y)
        e = {
            "name": m["name"],
            "size": _r((hi.x - lo.x, hi.z - lo.z, hi.y - lo.y), 2),
            "blocks": bool(m["blocks"]),
            "kind": m["kind"],
            "weight": m["weight"],
            "cluster": m["cluster"],
            "tris": m["tris"],
            "bounds_center": _r((-(lo.x + hi.x) / 2, (lo.z + hi.z) / 2, -(lo.y + hi.y) / 2), 2),
        }
        if m["collider"]:
            s, c = m["collider"]
            e["collider"] = _r((s.x, s.z, s.y), 2)
            e["collider_center"] = _r((-c.x, c.z, -c.y), 2)
        if m["note"]:
            e["note"] = m["note"]
        out.append(e)
    return out


def write_manifest(path, entries, extra=None):
    doc = {
        "about": "CORDON decor kit (Tools/Blender/assets). Units meters, Unity axes: size/collider = [x width, y height, z depth]. "
                 "collider_center / bounds_center are relative to the object origin. Origin of every object = bottom center at (0,0,0) "
                 "(tall props: the base of the pole/trunk; cars: the center of the body). "
                 "Blender -Y = Unity forward (+Z).",
        "objects": entries,
    }
    if extra:
        doc.update(extra)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1, ensure_ascii=False)
    return path


# ---------------------------------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------------------------------

def setup_render(w, h, sky=(0.30, 0.33, 0.37), sky_strength=0.9, sun_energy=3.0, sun_rot=(50, 0, -35), sun_color=(0.93, 0.96, 1.0)):
    sc = bpy.context.scene
    K._engine()
    sc.render.resolution_x = w
    sc.render.resolution_y = h
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    for attr, val in (("use_raytracing", True), ("taa_render_samples", 32)):
        try:
            setattr(sc.eevee, attr, val)
        except (AttributeError, TypeError):
            pass
    world = bpy.data.worlds.get("DecorWorld") or bpy.data.worlds.new("DecorWorld")
    try:
        world.use_nodes = True
    except AttributeError:
        pass
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (sky[0], sky[1], sky[2], 1)
    bg.inputs[1].default_value = sky_strength
    sc.world = world
    sun = bpy.data.objects.get("dc_sun")
    if not sun:
        sd = bpy.data.lights.new("dc_sun", "SUN")
        sun = K.link(bpy.data.objects.new("dc_sun", sd))
    sun.data.energy = sun_energy
    sun.data.color = sun_color
    try:
        sun.data.angle = math.radians(3.0)
    except AttributeError:
        pass
    sun.rotation_euler = tuple(math.radians(a) for a in sun_rot)
    return sun


def ground_material(path=GROUND_TEX, tile=8.0, name="M_GroundPreview"):
    """Preview of the ground shader: albedo from RGB, roughness = 1 - alpha (alpha = smoothness), tiled in world meters."""
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    try:
        mat.use_nodes = True
    except AttributeError:
        pass
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    if not os.path.exists(path):
        bsdf.inputs["Base Color"].default_value = (0.03, 0.035, 0.04, 1)
        return mat
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = "sRGB"
    img.alpha_mode = "CHANNEL_PACKED"
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1.0 / tile, 1.0 / tile, 1.0)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.extension = "REPEAT"
    tex.interpolation = "Linear"
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    nt.links.new(tex.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    try:
        bsdf.inputs["Specular IOR Level"].default_value = 0.5
    except KeyError:
        pass
    return mat


def ground_plane(size=60.0, mat=None, name="dc_ground", center=(0, 0)):
    g = size / 2
    me = bpy.data.meshes.new(name)
    cx, cy = center
    me.from_pydata([(cx - g, cy - g, 0), (cx + g, cy - g, 0), (cx + g, cy + g, 0), (cx - g, cy + g, 0)], [], [(0, 1, 2, 3)])
    obj = K.link(bpy.data.objects.new(name, me))
    me.materials.append(mat or ground_material())
    return obj


def _emit_mat(name="M_Label", color=(0.9, 0.9, 0.85, 1), strength=1.0):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    try:
        mat.use_nodes = True
    except AttributeError:
        pass
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = color
    em.inputs["Strength"].default_value = strength
    nt.links.new(em.outputs[0], out.inputs[0])
    return mat


def label(text, loc, size, rot_z=180.0):
    cu = bpy.data.curves.new("lbl_" + text, "FONT")
    cu.body = text
    cu.size = size
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    ob = K.link(bpy.data.objects.new("lbl_" + text, cu))
    ob.location = loc
    ob.rotation_euler = (0, 0, math.radians(rot_z))
    cu.materials.append(_emit_mat())
    return ob


def _render_to_array(path_tmp, w, h):
    sc = bpy.context.scene
    sc.render.filepath = path_tmp
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path_tmp)
    arr = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(im)
    return arr


def save_array(arr, path):
    h, w = arr.shape[:2]
    out = bpy.data.images.new("dc_sheet", w, h, alpha=True)
    out.pixels.foreach_set(arr.astype(np.float32).ravel())
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    return path


def sheet(name, objs, views=((200, 60),), tile=360, cols=6, labels=True, ground=True):
    """Contact sheet: every object alone, auto-framed (ortho), from each view, on the ground texture."""
    sc = bpy.context.scene
    setup_render(tile, tile)
    all_mesh = [o for o in sc.objects if o.type in ("MESH", "FONT")]
    saved = {o.name: o.hide_render for o in all_mesh}
    gobj = ground_plane(200.0) if ground else None
    cam_d = bpy.data.cameras.new("dc_cam")
    cam_d.type = "ORTHO"
    cam = K.link(bpy.data.objects.new("dc_cam", cam_d))
    sc.camera = cam
    tmp = os.path.join(K.PREVIEWS, "_dc_tmp.png")
    os.makedirs(K.PREVIEWS, exist_ok=True)
    tiles = []
    for obj in objs:
        for o in all_mesh:
            o.hide_render = True
        obj.hide_render = False
        if gobj:
            gobj.hide_render = False
        dg = bpy.context.evaluated_depsgraph_get()
        pts = [obj.matrix_world @ v.co for v in obj.evaluated_get(dg).data.vertices]
        lo = V((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = V((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        center = (lo + hi) / 2
        radius = max((hi - lo).length / 2, 0.35)
        lbl = label(obj.name, (center.x, hi.y + radius * 0.55 + 0.05, 0.02), radius * 0.2) if labels else None
        for az, el in views:
            a, e = math.radians(az), math.radians(el)
            d = V((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
            cam.location = center + d * radius * 8
            cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
            cam_d.ortho_scale = radius * 2.5
            cam_d.clip_end = radius * 40
            tiles.append(_render_to_array(tmp, tile, tile))
        if lbl:
            bpy.data.objects.remove(lbl)
    per_row = cols * len(views)
    while len(tiles) % per_row:
        tiles.append(np.zeros_like(tiles[0]))
    rows = [np.concatenate(tiles[i:i + per_row], axis=1) for i in range(0, len(tiles), per_row)]
    sheet_arr = np.concatenate(rows[::-1], axis=0)
    for o in all_mesh:
        if o.name in saved:
            o.hide_render = saved[o.name]
    bpy.data.objects.remove(cam)
    if gobj:
        bpy.data.objects.remove(gobj)
    if os.path.exists(tmp):
        os.remove(tmp)
    return save_array(sheet_arr, os.path.join(K.PREVIEWS, name + ".png"))


def render_camera(name, cam_loc, target, w, h, lens=None, fov=None, ortho=None, clip_end=500.0):
    """Renders the current scene from a camera looking at target into previews/<name>.png."""
    sc = bpy.context.scene
    cam_d = bpy.data.cameras.new("dc_cam2")
    if ortho:
        cam_d.type = "ORTHO"
        cam_d.ortho_scale = ortho
    else:
        cam_d.sensor_fit = "VERTICAL"
        if fov:
            cam_d.angle_y = math.radians(fov)
        elif lens:
            cam_d.lens = lens
    cam_d.clip_end = clip_end
    cam = K.link(bpy.data.objects.new("dc_cam2", cam_d))
    cam.location = cam_loc
    d = (V(target) - V(cam_loc)).normalized()
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.resolution_x = w
    sc.render.resolution_y = h
    sc.render.filepath = os.path.join(K.PREVIEWS, name + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    return sc.render.filepath


def place(obj, loc, rot_z=0.0, name=None):
    """A linked duplicate of obj at loc (x, y), rotated around Z (deg). For scenes."""
    dup = obj.copy()
    dup.name = name or (obj.name + "_inst")
    bpy.context.scene.collection.objects.link(dup)
    dup.location = (loc[0], loc[1], loc[2] if len(loc) > 2 else 0.0)
    dup.rotation_euler = (0, 0, math.radians(rot_z))
    return dup
