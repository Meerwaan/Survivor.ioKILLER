"""Helpers for CORDON static props, weapons and pickups, on top of cordon_kit (never edit the kit).

- lathe / sweep / torus / prism: low-poly generators returning kit Parts (lists of Parts).
- recolor / dent: per-face paint (rust, grime patches) and vertex pushes (dents) on Parts.
- finish(): kit build_prop + origin placement (bottom center or bounds center) + ngon triangulation.
- sheet() / game_view(): close-up turnaround previews and a phone-scale render from the game camera.
Conventions as the kit: Blender Z up, forward = -Y (Unity +Z), meters.
"""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.noise import noise as perlin

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import cordon_kit as K  # noqa: E402

V = Vector


def xf(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    return K._mat(loc, rot, scale)


def flatten(parts):
    for p in parts:
        if isinstance(p, (list, tuple)):
            yield from flatten(p)
        else:
            yield p


def _sw_pick(swatch, *key):
    if callable(swatch):
        return swatch(*key)
    if isinstance(swatch, (list, tuple)):
        return swatch[key[0] % len(swatch)]
    return swatch


# ---------------------------------------------------------------------------------------------
# Generators
# ---------------------------------------------------------------------------------------------

def lathe(profile, segments=12, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), swatch="g55", smooth=True,
          cap_bottom=True, cap_top=True, cap_swatch=None, hard=(), star=None, phase=0.0, ovals=None):
    """Surface of revolution around local Z. profile = [(radius, z), ...] from bottom to top
    (trace the outside, over the top and inward for lids: normals stay outward).
    swatch: a name, a list per band, or fn(band, seg) -> name. hard: profile indices where shading creases.
    star=(k, f): every k-th ring vertex gets radius * f (flutes, ribs). Caps are flat ngons (own verts)."""
    n = segments
    verts, faces, sws = [], [], []

    def ring(r, z):
        if r < 1e-6:
            verts.append(V((0, 0, z)))
            return [len(verts) - 1] * n
        ids = []
        for j in range(n):
            a = phase + math.tau * j / n
            rr = r * (star[1] if star and j % star[0] == 0 else 1.0)
            verts.append(V((rr * math.cos(a), rr * math.sin(a), z)))
            ids.append(len(verts) - 1)
        return ids

    tops, bots = [], []
    for i, (r, z) in enumerate(profile):
        ids = ring(r, z)
        tops.append(ids)
        bots.append(ring(r, z) if (i in hard and 0 < i < len(profile) - 1) else ids)
    for b in range(len(profile) - 1):
        lo, hi = bots[b], tops[b + 1]
        for j in range(n):
            j2 = (j + 1) % n
            f = []
            for v in (lo[j], lo[j2], hi[j2], hi[j]):
                if v not in f:
                    f.append(v)
            if len(f) >= 3:
                faces.append(f)
                sws.append(_sw_pick(swatch, b, j))
    side = K.Part(verts, faces, sws, None, smooth)
    m = xf(loc, rot, scale)
    side.transformed(m)
    out = [side]
    cverts, cfaces, csws = [], [], []
    cs = cap_swatch if isinstance(cap_swatch, (list, tuple)) else (cap_swatch, cap_swatch)
    for which, idx, flip in (("b", 0, True), ("t", -1, False)):
        if (which == "b" and not cap_bottom) or (which == "t" and not cap_top):
            continue
        r, z = profile[idx]
        if r < 1e-6:
            continue
        base = len(cverts)
        for j in range(n):
            a = phase + math.tau * j / n
            rr = r * (star[1] if star and j % star[0] == 0 else 1.0)
            cverts.append(V((rr * math.cos(a), rr * math.sin(a), z)))
        ids = list(range(base, base + n))
        cfaces.append(list(reversed(ids)) if flip else ids)
        default = _sw_pick(swatch, 0 if flip else len(profile) - 2, 0)
        csws.append((cs[0] if flip else cs[1]) or default)
    if cfaces:
        cap = K.Part(cverts, cfaces, csws, None, False)
        cap.transformed(m)
        out.append(cap)
    return out


def sweep(path, section, closed=False, loc=(0, 0, 0), rot=(0, 0, 0), swatch="g55", smooth=True, caps=True,
          up=(0, 0, 1), scales=None, cap_swatch=None, side=None):
    """Extrudes a closed 2D section [(x, y), ...] (CCW) along a polyline. x = side, y = 'up' of the frame.
    swatch: name, list per path segment, or fn(seg, k). scales: per path point, float or (sx, sy).
    side: a fixed side vector (for paths curving in a plane that contains `up`: levers, guards, straps)."""
    P = [V(p) for p in path]
    N = len(P)
    S = len(section)
    verts, faces, sws = [], [], []
    rings = []
    for i in range(N):
        if closed:
            t = P[(i + 1) % N] - P[i - 1]
        else:
            t = P[min(i + 1, N - 1)] - P[max(i - 1, 0)]
        t.normalize()
        if side is not None:
            sv = V(side)
            nx = (sv - t * sv.dot(t)).normalized()
        else:
            u = V(up)
            if abs(t.dot(u.normalized())) > 0.97:
                u = V((1, 0, 0)) if abs(t.x) < 0.9 else V((0, 1, 0))
            nx = u.cross(t).normalized()
        ny = t.cross(nx).normalized()
        sc = scales[i] if scales else 1.0
        sx, sy = (sc, sc) if isinstance(sc, (int, float)) else sc
        ids = []
        for x, y in section:
            verts.append(P[i] + nx * (x * sx) + ny * (y * sy))
            ids.append(len(verts) - 1)
        rings.append(ids)
    segs = N if closed else N - 1
    for i in range(segs):
        a, b = rings[i], rings[(i + 1) % N]
        for k in range(S):
            k2 = (k + 1) % S
            faces.append([a[k], a[k2], b[k2], b[k]])
            sws.append(_sw_pick(swatch, i, k))
    body = K.Part(verts, faces, sws, None, smooth)
    m = xf(loc, rot)
    body.transformed(m)
    out = [body]
    if caps and not closed:
        cv = [verts[i].copy() for i in rings[0]] + [verts[i].copy() for i in rings[-1]]
        cf = [list(reversed(range(S))), list(range(S, 2 * S))]
        csw = cap_swatch or _sw_pick(swatch, 0, 0)
        cap = K.Part(cv, cf, [csw, csw], None, False)
        cap.transformed(m)
        out.append(cap)
    return out


def crystal(length, width, loc=(0, 0, 0), direction=(0, 0, 1), back=0.3, sides=5, seed=0, swatch=("amber_glow", "amber_glow_soft"),
            back_swatch=None, flat=1.0, wobble=0.25):
    """Faceted bipyramid shard (amber éclats, crust shards): base ring at loc, tip at loc + dir * length,
    a short back point at loc - dir * length * back. swatch: list cycled per facet or fn(k). flat squashes one side."""
    rnd = random.Random(seed)
    d = V(direction).normalized()
    nx = d.orthogonal().normalized()
    ny = d.cross(nx).normalized()
    c = V(loc)
    a0 = rnd.random() * math.tau
    ring = []
    for k in range(sides):
        a = a0 + math.tau * k / sides + rnd.uniform(-0.2, 0.2)
        r = width * (1 + rnd.uniform(-wobble, wobble))
        ring.append(c + nx * (math.cos(a) * r) + ny * (math.sin(a) * r * flat) + d * (rnd.uniform(-0.08, 0.08) * length))
    tip = c + d * length + nx * rnd.uniform(-0.1, 0.1) * width
    bk = c - d * (length * back)
    verts = ring + [tip, bk]
    T, B = sides, sides + 1
    faces, sws = [], []
    for k in range(sides):
        k2 = (k + 1) % sides
        faces.append([k, k2, T])
        sws.append(swatch(k) if callable(swatch) else swatch[k % len(swatch)])
    for k in range(sides):
        k2 = (k + 1) % sides
        faces.append([k2, k, B])
        sws.append(back_swatch or (swatch(k) if callable(swatch) else swatch[k % len(swatch)]))
    return [K.Part(verts, faces, sws, None, False)]


def orient(parts, inside):
    """Flips faces whose normal points toward inside(face_center) (a point inside the volume)."""
    for p in flatten(parts):
        for f in range(len(p.faces)):
            c = face_center(p, f)
            if face_normal(p, f).dot(c - V(inside(c))) < 0:
                p.faces[f] = list(reversed(p.faces[f]))
    return parts


def circle(r, n, start=0.0, sx=1.0, sy=1.0):
    return [(r * sx * math.cos(start + math.tau * k / n), r * sy * math.sin(start + math.tau * k / n)) for k in range(n)]


def torus(R, r, seg=10, sides=4, loc=(0, 0, 0), rot=(0, 0, 0), swatch="g55", smooth=True, sx=1.0, sy=1.0, arc=None):
    """Ring in the local XY plane (axis Z). arc=(a0, a1) degrees for an open arc (with caps)."""
    if arc:
        a0, a1 = math.radians(arc[0]), math.radians(arc[1])
        path = [(R * sx * math.cos(a0 + (a1 - a0) * i / seg), R * sy * math.sin(a0 + (a1 - a0) * i / seg), 0) for i in range(seg + 1)]
        return sweep(path, circle(r, sides, math.pi / sides), closed=False, loc=loc, rot=rot, swatch=swatch, smooth=smooth)
    path = [(R * sx * math.cos(math.tau * i / seg), R * sy * math.sin(math.tau * i / seg), 0) for i in range(seg)]
    return sweep(path, circle(r, sides, math.pi / sides), closed=True, loc=loc, rot=rot, swatch=swatch, smooth=smooth)


def prism(outline, z0, z1, loc=(0, 0, 0), rot=(0, 0, 0), top="g55", side="g55", bottom=None, smooth=False, top_scale=1.0, top_offset=(0, 0)):
    """Extrudes a CCW XY outline from z0 to z1. side: name or fn(edge_index) -> name.
    top_scale / top_offset shrink and shift the top outline (bevelled lids, wedges)."""
    n = len(outline)
    cx = sum(p[0] for p in outline) / n
    cy = sum(p[1] for p in outline) / n
    verts = [V((x, y, z0)) for x, y in outline]
    verts += [V((cx + (x - cx) * top_scale + top_offset[0], cy + (y - cy) * top_scale + top_offset[1], z1)) for x, y in outline]
    faces, sws = [], []
    for k in range(n):
        k2 = (k + 1) % n
        faces.append([k, k2, n + k2, n + k])
        sws.append(side(k) if callable(side) else side)
    faces.append(list(range(n, 2 * n)))
    sws.append(top)
    faces.append(list(reversed(range(n))))
    sws.append(bottom or top)
    p = K.Part(verts, faces, sws, None, smooth)
    p.transformed(xf(loc, rot))
    return [p]


def quad(corners, swatch, offset=(0, 0, 0)):
    """A single flat decal face (CCW seen from the side it faces)."""
    o = V(offset)
    return [K.Part([V(c) + o for c in corners], [list(range(len(corners)))], swatch, None, False)]


def decal_box(size, loc, rot=(0, 0, 0), swatch="g55"):
    """A very thin box used as paint / tape / label (12 tris, but never z-fights)."""
    return [K.box(size, loc, rot, swatch=swatch)]


# ---------------------------------------------------------------------------------------------
# Paint and dents
# ---------------------------------------------------------------------------------------------

def face_center(part, f):
    return sum((part.verts[i] for i in part.faces[f]), V()) / len(part.faces[f])


def face_normal(part, f):
    vs = [part.verts[i] for i in part.faces[f]]
    n = V()
    for i in range(len(vs)):
        a, b = vs[i], vs[(i + 1) % len(vs)]
        n += V(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
    return n.normalized() if n.length > 1e-9 else V((0, 0, 1))


def recolor(parts, fn):
    """fn(center, normal, swatch) -> new swatch or None, for every face of every part."""
    for p in flatten(parts):
        p.swatch = [fn(face_center(p, f), face_normal(p, f), p.swatch[f]) or p.swatch[f] for f in range(len(p.faces))]
    return parts


def rust(parts, base=None, patches=("rust", "rust_dark"), amount=0.35, scale=3.0, seed=1, where=None):
    """Noise patches of rust on faces of `base` swatch (or any if None). where(center, normal) filters."""
    rnd = random.Random(seed)
    off = V((rnd.random() * 40, rnd.random() * 40, rnd.random() * 40))

    def fn(c, n, s):
        if base and s not in (base if isinstance(base, (list, tuple)) else (base,)):
            return None
        if where and not where(c, n):
            return None
        v = perlin(c * scale + off)
        if v > 0.55 - amount:
            return patches[0] if (len(patches) == 1 or v < 0.75 - amount * 0.5) else patches[1]
        return None
    return recolor(parts, fn)


def dent(parts, center, radius, depth, direction=None):
    """Pushes vertices within `radius` of center by `depth` along direction (default: toward the center line Z)."""
    c = V(center)
    for p in flatten(parts):
        for i, v in enumerate(p.verts):
            d = (v - c).length
            if d < radius:
                k = (1 - d / radius) ** 2
                if direction is None:
                    dr = V((-v.x, -v.y, 0))
                    dr = dr.normalized() if dr.length > 1e-6 else V((0, 0, -1))
                else:
                    dr = V(direction).normalized()
                p.verts[i] = v + dr * depth * k
    return parts


def jitter(parts, amount, seed=0):
    rnd = random.Random(seed)
    for p in flatten(parts):
        p.verts = [v + V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * amount for v in p.verts]
    return parts


def move(parts, m):
    for p in flatten(parts):
        p.transformed(m)
    return parts


# ---------------------------------------------------------------------------------------------
# Build, origin, checks
# ---------------------------------------------------------------------------------------------

def bounds(obj):
    vs = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = V((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = V((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def smooth_colors(obj, iterations=2, keep=0.5):
    """The kit bakes AO with 14 rays per vertex: on big flat ngons/fans (drum lids, crate tops) that noise
    shows as a star pattern. Averages each vertex with its edge neighbors and its co-located twins."""
    me = obj.data
    col = me.color_attributes.get("Col")
    n = len(me.vertices)
    if col is None or n == 0:
        return
    c = np.zeros(n * 4, dtype=np.float64)
    col.data.foreach_get("color", c)
    c = c.reshape(n, 4)
    e = np.zeros(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", e)
    e = e.reshape(-1, 2)
    co = np.zeros(n * 3, dtype=np.float64)
    me.vertices.foreach_get("co", co)
    key = np.round(co.reshape(n, 3) / 0.002).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = np.asarray(inv).ravel()
    cnt = np.bincount(inv).astype(np.float64)
    for _ in range(iterations):
        sums = np.zeros((cnt.shape[0], 4))
        np.add.at(sums, inv, c)
        c = sums[inv] / cnt[inv, None]
        acc = c.copy()
        deg = np.ones(n)
        np.add.at(acc, e[:, 0], c[e[:, 1]])
        np.add.at(acc, e[:, 1], c[e[:, 0]])
        np.add.at(deg, e[:, 0], 1)
        np.add.at(deg, e[:, 1], 1)
        c = c * keep + (acc / deg[:, None]) * (1 - keep)
    c[:, 3] = 1.0
    col.data.foreach_set("color", c.astype(np.float32).ravel())


def finish(name, parts, origin="bottom", ao=0.45, grime=0.2, grime_height=0.4, seed=7, mud_height=0.0, float_=None, ao_smooth=2):
    """Merges parts into one static mesh (kit build_prop, AO baked), then places the origin:
    origin='bottom' -> bottom center of the bounds, 'center' -> bounds center, None -> as built.
    Ngons are triangulated here so Unity never guesses a concave polygon.
    Build everything resting on Z = 0 (the kit's grime gradient starts at Z = 0) and pick the origin here."""
    obj = K.build_prop(name, list(flatten(parts)), ao=ao, grime=grime, grime_height=grime_height, seed=seed, mud_height=mud_height)
    obj["float"] = bool(float_ if float_ is not None else origin == "center")
    if ao_smooth:
        smooth_colors(obj, ao_smooth)
    me = obj.data
    lo, hi = bounds(obj)
    if origin == "bottom":
        me.transform(Matrix.Translation(-V(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    elif origin == "center":
        me.transform(Matrix.Translation(-(lo + hi) / 2))
    bm = bmesh.new()
    bm.from_mesh(me)
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(me)
    bm.free()
    me.update()
    return obj


def dims(obj):
    """(x, y, z) in UNITY terms: Unity X = Blender X, Unity Y (up) = Blender Z, Unity Z (forward) = Blender -Y."""
    lo, hi = bounds(obj)
    d = hi - lo
    return "%.2fx%.2fx%.2f" % (d.x, d.z, d.y)


def report(obj, budget):
    t = K.tri_count(obj)
    lo, hi = bounds(obj)
    K.report(asset=obj.name, tris=t, budget=budget, ok=t <= budget, unity_xyz=dims(obj),
             zmin="%.3f" % lo.z, zmax="%.3f" % hi.z)
    return t


# ---------------------------------------------------------------------------------------------
# Previews
# ---------------------------------------------------------------------------------------------

def _layout(objs, cols, gap, lift):
    """Places objects on a grid (X then -Y rows), each resting on the ground for the render. Returns restore fn."""
    saved = [o.location.copy() for o in objs]
    boxes = [bounds(o) for o in objs]
    rows = [objs[i:i + cols] for i in range(0, len(objs), cols)]
    y = 0.0
    k = 0
    for row in rows:
        x = 0.0
        depth = 0.0
        placed = []
        for o in row:
            lo, hi = boxes[k]
            k += 1
            w = hi.x - lo.x
            o.location = V((x - lo.x, y - (lo.y + hi.y) / 2, (-lo.z if lift else 0.0)))
            placed.append(o)
            x += w + gap
            depth = max(depth, hi.y - lo.y)
        shift = (x - gap) / 2
        for o in placed:
            o.location.x -= shift
        y -= depth + gap
    bpy.context.view_layer.update()

    def restore():
        for o, s in zip(objs, saved):
            o.location = s
        bpy.context.view_layer.update()
    return restore


def _cull():
    """Previews cull back faces like Unity does, so a flipped face shows up as a hole."""
    m = bpy.data.materials.get("M_Palette")
    if m is not None:
        m.use_backface_culling = True


def sheet(name, objs, cols=4, gap=0.12, views=((20, 58), (200, 58), (90, 12)), size=420, lift=True):
    """Kit preview of several objects laid out on the ground. The kit renders through a shared
    previews/_tmp.png, which collides when several Blender processes run at once: render into a private
    folder and move the sheet into previews/."""
    _cull()
    restore = _layout(objs, cols, gap, lift)
    real = K.PREVIEWS
    private = os.path.join(real, "_tmp_props_%d" % os.getpid())
    K.PREVIEWS = private
    try:
        path = K.preview(name, objs, views=views, size=size)
    finally:
        K.PREVIEWS = real
        restore()
    final = os.path.join(real, name + ".png")
    os.replace(path, final)
    try:
        os.rmdir(private)
    except OSError:
        pass
    return final


def game_view(name, objs, spacing=1.6, width=560, height=720, distance=21.0, fov=50.0, hero=True, cols=None):
    """Phone-scale check: perspective camera of the game (offset 0, 15, -8.5 -> ~60 deg, FOV 50),
    objects at real size on asphalt, a 1.8 m hero stand-in for scale. Floating pickups sit 0.5 m up like in game."""
    sc = bpy.context.scene
    K._engine()
    _cull()
    saved = [o.location.copy() for o in objs]
    extra = []
    cols = cols or len(objs)
    for i, o in enumerate(objs):
        lo, hi = bounds(o)
        r, c = divmod(i, cols)
        x = (c - (min(cols, len(objs)) - 1) / 2) * spacing
        y = r * spacing * 0.9
        z = (0.5 if o.get("float") else -lo.z)
        o.location = V((x, y, z))
    ground = K.build_prop("pv_asphalt", [K.box((40, 40, 0.1), (0, 0, -0.05), swatch="asphalt")], ao=0, grime=0)
    extra.append(ground)
    if hero:
        hp = [K.cyl(0.32, 1.2, (0, -2.2, 0.75), swatch="hoodie_charcoal", segments=10),
              K.ball(0.2, (0, -2.2, 1.55), swatch="skin_mid"), K.box((0.62, 0.32, 0.5), (0, -2.2, 1.05), swatch="plate_carrier")]
        extra.append(K.build_prop("pv_hero", hp, ao=0.2, grime=0))
    show = list(objs) + extra
    hidden = [o for o in sc.objects if o.type == "MESH" and o not in show and not o.hide_render]
    for o in hidden:
        o.hide_render = True
    sc.render.resolution_x, sc.render.resolution_y = width, height
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
    sun_d = bpy.data.lights.new("gv_sun", "SUN")
    sun_d.energy = 3.0
    sun = K.link(bpy.data.objects.new("gv_sun", sun_d))
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    cam_d = bpy.data.cameras.new("gv_cam")
    cam_d.type = "PERSP"
    cam_d.sensor_fit = "VERTICAL"
    # the render is a crop of a 2x-points iPhone portrait screen (780 x 1688) whose full height spans `fov`
    cam_d.angle_y = 2.0 * math.atan(math.tan(math.radians(fov) / 2.0) * height / 1688.0)
    cam = K.link(bpy.data.objects.new("gv_cam", cam_d))
    # game camera: Unity offset (0, 15, -8.5) -> Blender (0, +8.5, 15) direction, looking at the center
    d = V((0, 8.5, 15)).normalized()
    target = V((0, 0, 0.3))
    cam.location = target + d * distance
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    os.makedirs(K.PREVIEWS, exist_ok=True)
    sc.render.filepath = os.path.join(K.PREVIEWS, name + ".png")
    bpy.ops.render.render(write_still=True)
    for o in (sun, cam):
        bpy.data.objects.remove(o)
    for o in extra:
        bpy.data.objects.remove(o)
    for o in hidden:
        o.hide_render = False
    for o, s in zip(objs, saved):
        o.location = s
    bpy.context.view_layer.update()
    return sc.render.filepath
