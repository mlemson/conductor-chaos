"""meshkit — kleine, afhankelijkheidsvrije (numpy) toolkit om gestileerde
low-poly game-assets parametrisch op te bouwen en te exporteren.

Waarom zelf bouwen: de art-source moet reproduceerbaar zijn vanuit Git, zonder
Blender of andere DCC-software. Alle vormen zijn simpele, ronde primitieven
(lathe, superellips, afgeronde box, ellipsoïde) met schone quad-topologie.

Exports:
  * FBX 7.4 ASCII  -> Unity (Y-up, voorkant +Z, 1 unit = 1 meter)
  * glTF 2.0 .glb   -> Blender / viewers / previews

Conventies
  * Y omhoog, het model kijkt naar +Z. Linkerkant van het personage = +X.
  * Elke Part heeft een pivot (lokale oorsprong) en een parent; vertices staan
    in lokale coördinaten relatief aan die pivot.
"""
from __future__ import annotations

import json
import math
import struct
from dataclasses import dataclass, field

import numpy as np


# --------------------------------------------------------------------- utils
def srgb_hex(h: str):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def srgb_to_linear(c):
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def spow(x, p):
    return np.sign(x) * np.abs(x) ** p


def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def rot_y(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def euler_xyz_deg(R):
    """Rotatiematrix -> Euler XYZ in graden (FBX 'eEulerXYZ': R = Rz*Ry*Rx)."""
    sy = -R[2, 0]
    sy = max(-1.0, min(1.0, sy))
    y = math.asin(sy)
    if abs(sy) < 0.99999:
        x = math.atan2(R[2, 1], R[2, 2])
        z = math.atan2(R[1, 0], R[0, 0])
    else:
        x = math.atan2(-R[1, 2], R[1, 1])
        z = 0.0
    return tuple(math.degrees(v) for v in (x, y, z))


def quat_from_matrix(R):
    t = np.trace(R)
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        w = 0.25 * s
        x = (R[2, 1] - R[1, 2]) / s
        y = (R[0, 2] - R[2, 0]) / s
        z = (R[1, 0] - R[0, 1]) / s
    elif R[0, 0] > R[1, 1] and R[0, 0] > R[2, 2]:
        s = math.sqrt(1.0 + R[0, 0] - R[1, 1] - R[2, 2]) * 2
        w = (R[2, 1] - R[1, 2]) / s
        x = 0.25 * s
        y = (R[0, 1] + R[1, 0]) / s
        z = (R[0, 2] + R[2, 0]) / s
    elif R[1, 1] > R[2, 2]:
        s = math.sqrt(1.0 + R[1, 1] - R[0, 0] - R[2, 2]) * 2
        w = (R[0, 2] - R[2, 0]) / s
        x = (R[0, 1] + R[1, 0]) / s
        y = 0.25 * s
        z = (R[1, 2] + R[2, 1]) / s
    else:
        s = math.sqrt(1.0 + R[2, 2] - R[0, 0] - R[1, 1]) * 2
        w = (R[1, 0] - R[0, 1]) / s
        x = (R[0, 2] + R[2, 0]) / s
        y = (R[1, 2] + R[2, 1]) / s
        z = 0.25 * s
    return [x, y, z, w]


def _ascii_fbx_detectable(text):
    """Bootst de ASCII-detectie van three.js FBXLoader na (posities 0,1,3,6,10,...)."""
    magic = "Kaydara\\FBX\\Binary\\\\"
    pos = 0
    for i, ch in enumerate(magic):
        if pos >= len(text) or text[pos] == ch:
            return False
        pos += i + 1
    return True


# ---------------------------------------------------------------------- mesh
@dataclass
class Mesh:
    V: np.ndarray = field(default_factory=lambda: np.zeros((0, 3)))
    F: list = field(default_factory=list)      # lijst van tuples (tri/quad)
    M: list = field(default_factory=list)      # materiaalnaam per face
    sharp: bool = False                        # True = platte normalen per face
    C: np.ndarray | None = None                # optioneel: per-vertex 'shade' RGBA (zie Scene.bake_shading)
    N: np.ndarray | None = None                # optionele gladde per-vertex normalen
    FS: list | None = None                     # optioneel: per face 'plat' (facet) — blijft behouden bij merge

    def copy(self):
        return Mesh(self.V.copy(), list(self.F), list(self.M), self.sharp,
                    None if self.C is None else self.C.copy(),
                    N=None if self.N is None else self.N.copy(),
                    FS=None if self.FS is None else list(self.FS))

    def face_sharp(self):
        return list(self.FS) if self.FS is not None else [self.sharp] * len(self.F)

    def transform(self, R=None, t=(0, 0, 0), s=None):
        V = self.V
        if s is not None:
            V = V * np.asarray(s, float)
        if R is not None:
            V = V @ np.asarray(R).T
        V = V + np.asarray(t, float)
        out = self.copy()
        out.V = V
        if out.N is not None:
            if s is not None:
                out.N = out.N / np.asarray(s, float)
            if R is not None:
                out.N = out.N @ np.asarray(R).T
            out.N /= np.maximum(np.linalg.norm(out.N, axis=1, keepdims=True), 1e-12)
        if s is not None and np.prod(np.sign(s)) < 0:      # spiegelen -> winding omdraaien
            out.F = [tuple(reversed(f)) for f in out.F]
        return out

    def set_material(self, name):
        self.M = [name] * len(self.F)
        return self

    def tri_count(self):
        return sum(len(f) - 2 for f in self.F)


def merge(*meshes):
    out = Mesh()
    Vs, F, M = [], [], []
    off = 0
    for m in meshes:
        Vs.append(m.V)
        F += [tuple(i + off for i in f) for f in m.F]
        M += m.M
        off += len(m.V)
    out.V = np.vstack(Vs) if Vs else np.zeros((0, 3))
    out.F, out.M = F, M
    if any(m.N is not None for m in meshes):
        out.N = np.vstack([vertex_normals(m) for m in meshes])
    fs = []
    for m in meshes:
        fs += m.face_sharp()
    if any(fs):
        out.FS = fs
        out.sharp = all(fs)
    return out


def vertex_normals(mesh: Mesh):
    if mesh.N is not None:
        return mesh.N.copy()
    V = mesh.V
    N = np.zeros_like(V)
    for f in mesh.F:
        p = V[list(f)]
        # Newell-normaal (werkt voor tri én quad), area-gewogen
        n = np.zeros(3)
        for i in range(len(f)):
            a, b = p[i], p[(i + 1) % len(f)]
            n += np.array([(a[1] - b[1]) * (a[2] + b[2]),
                           (a[2] - b[2]) * (a[0] + b[0]),
                           (a[0] - b[0]) * (a[1] + b[1])])
        for i in f:
            N[i] += n
    ln = np.linalg.norm(N, axis=1, keepdims=True)
    ln[ln == 0] = 1
    return N / ln


def corner_normals(mesh: Mesh):
    """Normaal per polygon-hoek (FBX 'ByPolygonVertex'); platte (facet) faces krijgen hun vlaknormaal."""
    fs = mesh.face_sharp()
    VN = vertex_normals(mesh) if not all(fs) else None
    out = []
    for f, sh in zip(mesh.F, fs):
        if sh:
            p = mesh.V[list(f)]
            n = np.cross(p[1] - p[0], p[2] - p[0])
            if len(f) == 4:
                n += np.cross(p[2] - p[0], p[3] - p[0])
            n /= (np.linalg.norm(n) or 1)
            out += [n] * len(f)
        else:
            out += [VN[i] for i in f]
    return np.array(out)


# ----------------------------------------------------------------- primitives
def lathe(profile, segments=24, a=1.0, b=1.0, n=2.0, closed=False, mat="Default", phase=0.0):
    """Draai een profiel [(r, y), ...] rond de Y-as.

    Doorsnede = superellips met halve assen a (X) en b (Z) en exponent n
    (n=2 cirkel, n>2 'squircle'). r=0 aan het begin/einde wordt een pool.
    closed=True: het profiel is een gesloten lus (bv. torus-buis).
    """
    prof = list(profile)
    V, F = [], []
    ring_idx = []
    ang = [phase + 2 * math.pi * k / segments for k in range(segments)]
    cs = [(spow(math.cos(t), 2.0 / n), spow(math.sin(t), 2.0 / n)) for t in ang]
    for (r, y) in prof:
        if r <= 1e-9 and not closed:
            ring_idx.append([len(V)])
            V.append((0.0, y, 0.0))
        else:
            idx = []
            for (c, s) in cs:
                idx.append(len(V))
                V.append((a * r * c, y, b * r * s))
            ring_idx.append(idx)
    rings = ring_idx + ([ring_idx[0]] if closed else [])
    for r0, r1 in zip(rings[:-1], rings[1:]):
        if len(r0) == 1 and len(r1) == 1:
            continue
        for k in range(segments):
            k2 = (k + 1) % segments
            if len(r0) == 1:
                F.append((r0[0], r1[k2], r1[k]))
            elif len(r1) == 1:
                F.append((r0[k], r0[k2], r1[0]))
            else:
                F.append((r0[k], r0[k2], r1[k2], r1[k]))
    m = Mesh(np.array(V, float), F, [mat] * len(F))
    return orient_outward(m)


def orient_outward(m: Mesh):
    """Zorg dat faces naar buiten wijzen (t.o.v. het zwaartepunt)."""
    c = m.V.mean(axis=0)
    flip = 0
    for f in m.F[: min(len(m.F), 200)]:
        p = m.V[list(f)]
        n = np.cross(p[1] - p[0], p[2] - p[0])
        if np.dot(n, p.mean(0) - c) < 0:
            flip += 1
    if flip > min(len(m.F), 200) / 2:
        m.F = [tuple(reversed(f)) for f in m.F]
    return m


def ellipsoid(rx, ry, rz, seg=20, rings=12, mat="Default", n=2.0):
    prof = []
    for i in range(rings + 1):
        t = -math.pi / 2 + math.pi * i / rings
        prof.append((spow(math.cos(t), 2.0 / n) if i not in (0, rings) else 0.0, ry * spow(math.sin(t), 2.0 / n)))
    return lathe(prof, seg, rx, rz, 2.0, mat=mat)


def torus(R, r, seg=24, tube=8, mat="Default", a=1.0, b=1.0, n=2.0, ry=None):
    ry = r if ry is None else ry
    prof = [(R + r * math.cos(2 * math.pi * i / tube), ry * math.sin(2 * math.pi * i / tube)) for i in range(tube)]
    # lathe met r als absolute straal -> a,b als extra schaal
    return lathe([(p[0], p[1]) for p in prof], seg, a, b, n, closed=True, mat=mat)


def rounded_box(sx, sy, sz, radius, div=3, mat="Default"):
    """Afgeronde box met echte ronde randen: grid per zijvlak, randen op de boog gesampled."""
    H = np.array([sx, sy, sz]) / 2.0
    h = H - radius

    def axis_samples(Hk, hk):
        arc = [-hk - radius * math.cos(math.pi / 2 * j / div) for j in range(div + 1)]  # -H .. -h
        return arc + [-v for v in reversed(arc)]  # -H..-h, h..H

    S = [axis_samples(H[k], h[k]) for k in range(3)]
    V, F, index = [], [], {}

    def vid(p):
        key = tuple(np.round(p, 6))
        if key not in index:
            index[key] = len(V)
            V.append(np.array(p, float))
        return index[key]

    for ax in range(3):
        for sg in (-1, 1):
            u_ax, v_ax = [a for a in (0, 1, 2) if a != ax]
            us, vs = S[u_ax], S[v_ax]
            grid = np.zeros((len(us), len(vs)), int)
            for i, u in enumerate(us):
                for j, v in enumerate(vs):
                    p = np.zeros(3)
                    p[ax], p[u_ax], p[v_ax] = sg * H[ax], u, v
                    inner = np.clip(p, -h, h)
                    d = p - inner
                    nd = np.linalg.norm(d)
                    if nd < 1e-12:
                        d = np.zeros(3); d[ax] = sg; nd = 1.0
                    grid[i, j] = vid(inner + d / nd * radius)
            for i in range(len(us) - 1):
                for j in range(len(vs) - 1):
                    q = (grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1])
                    if len(set(q)) == 4:
                        F.append(q)
                    elif len(set(q)) == 3:
                        F.append(tuple(dict.fromkeys(q)))
    m = Mesh(np.array(V, float), F, [mat] * len(F))
    # winding per face naar buiten (convexe vorm -> t.o.v. middelpunt)
    fixed = []
    for f in m.F:
        p = m.V[list(f)]
        n = np.cross(p[1] - p[0], p[2] - p[0])
        fixed.append(f if np.dot(n, p.mean(0)) >= 0 else tuple(reversed(f)))
    m.F = fixed
    return m


def rounded_box_grid(sx, sy, sz, radius, div=4, step=0.2, cuts=((), (), ()), holes=(), mat="Default"):
    """Afgeronde box met regelmatig rooster op de vlakke delen (voor gebakken variatie) en echte gaten.

    cuts:  extra rasterlijnen per as (x, y, z) zodat gatranden precies op het raster vallen.
    holes: [(as, teken, u0, u1, v0, v1), ...] — rechthoek op het vlak loodrecht op 'as' (0/1/2) aan kant
           'teken' (+1/-1), in de twee overige assen (oplopende volgorde) — die faces worden weggelaten.
    """
    H = np.array([sx, sy, sz]) / 2.0
    h = H - radius

    def samples(k):
        arc = [-h[k] - radius * math.cos(math.pi / 2 * j / div) for j in range(div + 1)]
        n = max(1, int(math.ceil(2 * h[k] / step)))
        mid = set(np.round(np.linspace(-h[k], h[k], n + 1), 6))
        mid |= {round(c, 6) for c in cuts[k] if -h[k] < c < h[k]}
        return arc[:-1] + sorted(mid) + [-v for v in reversed(arc[:-1])]

    S = [samples(k) for k in range(3)]
    V, F, index = [], [], {}

    def vid(p):
        key = tuple(np.round(p, 6))
        if key not in index:
            index[key] = len(V)
            V.append(np.array(p, float))
        return index[key]

    for ax in range(3):
        for sg in (-1, 1):
            u_ax, v_ax = [a for a in (0, 1, 2) if a != ax]
            us, vs = S[u_ax], S[v_ax]
            grid = np.zeros((len(us), len(vs)), int)
            for i, u in enumerate(us):
                for j, v in enumerate(vs):
                    p = np.zeros(3)
                    p[ax], p[u_ax], p[v_ax] = sg * H[ax], u, v
                    inner = np.clip(p, -h, h)
                    d = p - inner
                    nd = np.linalg.norm(d)
                    if nd < 1e-12:
                        d = np.zeros(3); d[ax] = sg; nd = 1.0
                    grid[i, j] = vid(inner + d / nd * radius)
            for i in range(len(us) - 1):
                for j in range(len(vs) - 1):
                    cu, cv = (us[i] + us[i + 1]) / 2, (vs[j] + vs[j + 1]) / 2
                    if any(hx == ax and hs == sg and u0 < cu < u1 and v0 < cv < v1 for (hx, hs, u0, u1, v0, v1) in holes):
                        continue
                    q = (grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1])
                    if len(set(q)) == 4:
                        F.append(q)
                    elif len(set(q)) == 3:
                        F.append(tuple(dict.fromkeys(q)))
    m = Mesh(np.array(V, float), F, [mat] * len(F))
    fixed = []
    for f in m.F:
        p = m.V[list(f)]
        n = np.cross(p[1] - p[0], p[2] - p[0])
        fixed.append(f if np.dot(n, p.mean(0)) >= 0 else tuple(reversed(f)))
    m.F = fixed
    return m


# ------------------------------------------------- uit één stuk: kaders, holle bakken, prisma's
def _signed_volume(V, F):
    vol = 0.0
    for f in F:
        p0 = V[f[0]]
        for k in range(1, len(f) - 1):
            vol += np.dot(p0, np.cross(V[f[k]], V[f[k + 1]]))
    return vol / 6.0


def _orient_closed(m):
    if _signed_volume(m.V, m.F) < 0:
        m.F = [tuple(reversed(f)) for f in m.F]
    return m


def _offset_poly(P, d):
    """Verschuif een gesloten polygoon (CCW) d naar binnen (miter per hoek)."""
    P = np.asarray(P, float)
    n = len(P)
    out = []
    for i in range(n):
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]
        e1 = (b - a) / np.linalg.norm(b - a)
        e2 = (c - b) / np.linalg.norm(c - b)
        n1 = np.array([-e1[1], e1[0]])
        n2 = np.array([-e2[1], e2[0]])
        m = n1 + n2
        m /= np.linalg.norm(m)
        out.append(b + m * d / max(np.dot(m, n1), 0.3))
    return np.array(out)


def _poly_area(P):
    P = np.asarray(P, float)
    return 0.5 * np.sum(P[:, 0] * np.roll(P[:, 1], -1) - np.roll(P[:, 0], -1) * P[:, 1])


def _earclip(P):
    """Driehoeken voor een eenvoudige CCW-polygoon (indices)."""
    idx = list(range(len(P)))
    tris = []

    def inside(p, a, b, c):
        def s(p1, p2, p3):
            return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])
        d1, d2, d3 = s(p, a, b), s(p, b, c), s(p, c, a)
        return not ((d1 < -1e-12 or d2 < -1e-12 or d3 < -1e-12) and (d1 > 1e-12 or d2 > 1e-12 or d3 > 1e-12))
    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = P[i0], P[i1], P[i2]
            if (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]) <= 1e-12:
                continue
            if any(inside(P[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            break
    tris.append(tuple(idx))
    return tris


def chamfer_poly(points, c):
    """Vervang elke hoek door een kleine afschuining (low-poly 'bevel'), ook bij binnenhoeken."""
    P = np.asarray(points, float)
    out = []
    n = len(P)
    for i in range(n):
        a, b, cc = P[i - 1], P[i], P[(i + 1) % n]
        da, db = np.linalg.norm(b - a), np.linalg.norm(cc - b)
        out.append(b + (a - b) / da * min(c, da * 0.45))
        out.append(b + (cc - b) / db * min(c, db * 0.45))
    return out


def prism(profile_xy, z0, z1, c=0.04, mat="Default"):
    """Polygoon in XY (mag hol/stapvormig zijn) geëxtrudeerd langs Z met afgeschuinde zijranden: één gesloten stuk."""
    P = np.asarray(profile_xy, float)
    if _poly_area(P) < 0:
        P = P[::-1]
    Pi = _offset_poly(P, c)
    rings = [(Pi, z0), (P, z0 + c), (P, z1 - c), (Pi, z1)]
    V, F = [], []
    n = len(P)
    for (Q, z) in rings:
        V += [(q[0], q[1], z) for q in Q]
    for r in range(3):
        for i in range(n):
            j = (i + 1) % n
            F.append((r * n + i, r * n + j, (r + 1) * n + j, (r + 1) * n + i))
    for (a, b, cc) in _earclip(Pi):
        F.append((a, cc, b))
        F.append((3 * n + a, 3 * n + b, 3 * n + cc))
    m = Mesh(np.array(V, float), F, [mat] * len(F))
    m.sharp = True
    return _orient_closed(m)


def frame_ring(x0, x1, y0, y1, w, z0, z1, c=0.02, mat="Default", open_bottom=False):
    """Kader rond een opening [x0,x1]x[y0,y1] in het XY-vlak, diepte z0..z1, uit één stuk (verstekhoeken).

    open_bottom=True: U-vorm voor een deur (geen onderregel, staanders eindigen op y0)."""
    prof = [(0.0, z0), (0.0, z1 - c), (c, z1), (w - c, z1), (w, z1 - c), (w, z0)]
    if open_bottom:
        path = [((x0 - w, y0), (1, 0)), ((x0 - w, y1 + w), (1, -1)), ((x1 + w, y1 + w), (-1, -1)), ((x1 + w, y0), (-1, 0))]
    else:
        path = [((x0 - w, y0 - w), (1, 1)), ((x1 + w, y0 - w), (-1, 1)), ((x1 + w, y1 + w), (-1, -1)), ((x0 - w, y1 + w), (1, -1))]
    V, F = [], []
    npf = len(prof)
    for (px, py), (ix, iy) in path:
        V += [(px + d * ix, py + d * iy, z) for (d, z) in prof]
    segs = len(path) if not open_bottom else len(path) - 1
    for k in range(segs):
        k2 = (k + 1) % len(path)
        for j in range(npf):
            j2 = (j + 1) % npf
            F.append((k * npf + j, k * npf + j2, k2 * npf + j2, k2 * npf + j))
    if open_bottom:
        last = (len(path) - 1) * npf
        for j in range(1, npf - 1):
            F.append((0, j + 1, j))
            F.append((last, last + j, last + j + 1))
    m = Mesh(np.array(V, float), F, [mat] * len(F))
    m.sharp = True
    return _orient_closed(m)


def hollow_box(outer, inner, holes=(), radius=0.1, inner_radius=0.005, step=0.25, div=1, mat="Default",
               inner_mat=None, jamb_mat=None):
    """Holle afgeschuinde bak uit één stuk: buitenschil + binnenschil + dagkanten (doorgangen) ertussen.

    outer/inner: (x0, x1, y0, y1, z0, z1) in absolute coördinaten (binnen moet in buiten liggen).
    holes: (as, teken, u0, u1, v0, v1[, iu0, iu1, iv0, iv1]) — opening in het vlak loodrecht op 'as' aan kant
           'teken'; u/v = de twee overige assen in oplopende volgorde (absolute coördinaten). De optionele
           binnenrechthoek maakt een rand (bv. open bovenkant). Openingen moeten in het vlakke deel liggen.
    """
    ob, ib = np.array(outer, float).reshape(3, 2), np.array(inner, float).reshape(3, 2)
    co, Ho = ob.mean(1), (ob[:, 1] - ob[:, 0]) / 2
    ci, Hi = ib.mean(1), (ib[:, 1] - ib[:, 0]) / 2
    H6 = [tuple(h) if len(h) == 10 else tuple(h) + tuple(h[2:6]) for h in holes]

    def local(hs, c, inner_side):
        out, cuts = [], [set(), set(), set()]
        for h in hs:
            ax, sg = h[0], h[1]
            u_ax, v_ax = [a for a in (0, 1, 2) if a != ax]
            u0, u1, v0, v1 = h[6:10] if inner_side else h[2:6]
            r = (u0 - c[u_ax], u1 - c[u_ax], v0 - c[v_ax], v1 - c[v_ax])
            out.append((ax, sg) + r)
            cuts[u_ax] |= {r[0], r[1]}
            cuts[v_ax] |= {r[2], r[3]}
        return out, tuple(sorted(x) for x in cuts)

    ho, cuto = local(H6, co, False)
    hi, cuti = local(H6, ci, True)
    om = rounded_box_grid(*(2 * Ho), radius, div=div, step=step, cuts=cuto, holes=ho, mat=mat).transform(t=co)
    im = rounded_box_grid(*(2 * Hi), inner_radius, div=1, step=step, cuts=cuti, holes=hi,
                          mat=inner_mat or mat).transform(t=ci)
    im.F = [tuple(reversed(f)) for f in im.F]
    prisms = []
    for h in H6:
        ax, sg = h[0], h[1]
        u_ax, v_ax = [a for a in (0, 1, 2) if a != ax]
        a_o, a_i = co[ax] + sg * Ho[ax], ci[ax] + sg * Hi[ax]
        prisms.append((ax, u_ax, v_ax, max(h[2], h[6]), min(h[3], h[7]), max(h[4], h[8]), min(h[5], h[9]),
                       min(a_o, a_i), max(a_o, a_i)))

    def solid(p):
        if np.any(p < ob[:, 0]) or np.any(p > ob[:, 1]):
            return False
        if np.all(p > ib[:, 0]) and np.all(p < ib[:, 1]):
            return False
        for (ax, ua, va, u0, u1, v0, v1, a0, a1) in prisms:
            if u0 < p[ua] < u1 and v0 < p[va] < v1 and a0 - 1e-6 <= p[ax] <= a1 + 1e-6:
                return False
        return True

    V, F = [], []
    for h in H6:
        ax, sg = h[0], h[1]
        u_ax, v_ax = [a for a in (0, 1, 2) if a != ax]
        a_o, a_i = co[ax] + sg * Ho[ax], ci[ax] + sg * Hi[ax]

        def P(a, u, v):
            q = np.zeros(3)
            q[ax], q[u_ax], q[v_ax] = a, u, v
            return q
        ro = [(h[2], h[4]), (h[3], h[4]), (h[3], h[5]), (h[2], h[5])]
        ri = [(h[6], h[8]), (h[7], h[8]), (h[7], h[9]), (h[6], h[9])]
        for k in range(4):
            k2 = (k + 1) % 4
            q = [P(a_o, *ro[k]), P(a_o, *ro[k2]), P(a_i, *ri[k2]), P(a_i, *ri[k])]
            if np.linalg.norm(q[0] - q[3]) < 1e-9 and np.linalg.norm(q[1] - q[2]) < 1e-9:
                continue
            n = np.cross(q[1] - q[0], q[3] - q[0])
            if np.linalg.norm(n) < 1e-12:
                n = np.cross(q[2] - q[1], q[0] - q[1])
            n /= np.linalg.norm(n)
            mid = np.mean(q, 0)
            if solid(mid + n * 2e-3):
                q = q[::-1]
            F.append(tuple(range(len(V), len(V) + 4)))
            V += q
    jm = Mesh(np.array(V, float).reshape(-1, 3), F, [jamb_mat or mat] * len(F))
    out = merge(om, im, jm)
    out.sharp = True
    out.FS = None
    return out


def cylinder(r, length, seg=24, bevel=0.0, mat="Default", axis="x"):
    """Cilinder (optioneel afgeschuinde randen) langs een as, gecentreerd."""
    b = min(bevel, r * 0.5, length * 0.5)
    prof = [(0.0, -length / 2), (r - b, -length / 2)]
    if b > 0:
        for k in range(1, 4):
            th = math.pi / 2 * k / 3
            prof.append((r - b + b * math.sin(th), -length / 2 + b - b * math.cos(th)))
        for k in range(0, 4):
            th = math.pi / 2 * k / 3
            prof.append((r - b + b * math.cos(th), length / 2 - b + b * math.sin(th)))
    else:
        prof += [(r, -length / 2), (r, length / 2)]
    prof += [(r - b, length / 2), (0.0, length / 2)]
    m = lathe(prof, seg, mat=mat)
    if axis == "x":
        m = m.transform(R=rot_z(math.pi / 2))
    elif axis == "z":
        m = m.transform(R=rot_x(math.pi / 2))
    return m


def capsule_between(p0, p1, r0, r1, seg=16, rings=6, mat="Default"):
    """Taps toelopende capsule van p0 naar p1 (lokale Y-as langs het segment)."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    L = np.linalg.norm(p1 - p0)
    prof = []
    for i in range(rings + 1):  # onderste halve bol
        t = -math.pi / 2 + (math.pi / 2) * i / rings
        prof.append((r0 * math.cos(t) if i else 0.0, r0 * math.sin(t)))
    for i in range(1, 4):
        f = i / 4
        prof.append((r0 + (r1 - r0) * f, L * f))
    for i in range(rings + 1):  # bovenste halve bol
        t = (math.pi / 2) * i / rings
        prof.append((r1 * math.cos(t) if i < rings else 0.0, L + r1 * math.sin(t)))
    m = lathe(prof, seg, mat=mat)
    d = (p1 - p0) / L
    up = np.array([0, 1.0, 0])
    v = np.cross(up, d)
    c = float(np.dot(up, d))
    if np.linalg.norm(v) < 1e-9:
        R = np.eye(3) if c > 0 else rot_x(math.pi)
    else:
        vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
        R = np.eye(3) + vx + vx @ vx * (1 / (1 + c))
    return m.transform(R=R, t=p0)


# --------------------------------------------------------------------- scene
@dataclass
class Part:
    name: str
    mesh: Mesh | None
    pivot: tuple = (0, 0, 0)          # positie t.o.v. parent-pivot
    parent: str | None = None
    rotation: np.ndarray | None = None  # lokale rotatie (3x3) t.o.v. parent
    visible: bool = True
    ao_group: str = "main"            # onderdelen in dezelfde groep beschaduwen elkaar
    ao_receive: tuple = ()            # extra groepen waarvan dit onderdeel occlusie ontvangt
    gradient: float = 0.18            # sterkte van 'onderkant donkerder' (0 = uit)
    edges: bool = True                # randlicht op afrondingen/facetranden bakken


def _value_noise(P, seed=0):
    """Gladde 3D value noise (0..1) op punten P (N,3)."""
    rng = np.random.default_rng(seed)
    perm = rng.permutation(256)
    vals = rng.random(256)
    Pi = np.floor(P).astype(np.int64)
    f = P - Pi
    u = f * f * (3 - 2 * f)

    def h(ix, iy, iz):
        return vals[perm[(perm[(perm[ix & 255] + iy) & 255] + iz) & 255]]

    x0, y0, z0 = Pi[:, 0], Pi[:, 1], Pi[:, 2]
    acc = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1]) * (u[:, 2] if dz else 1 - u[:, 2])
                acc = acc + w * h(x0 + dx, y0 + dy, z0 + dz)
    return acc


def fbm(P, freq=1.0, octaves=3, seed=0):
    tot, amp, norm = 0.0, 1.0, 0.0
    for o in range(octaves):
        tot = tot + amp * _value_noise(P * freq * (2.03 ** o) + 17.1 * o, seed + o)
        norm += amp
        amp *= 0.5
    return tot / norm


class Scene:
    def __init__(self, name):
        self.name = name
        self.parts: list[Part] = []
        self.materials: dict[str, dict] = {}

    def add(self, part: Part):
        self.parts.append(part)
        return part

    def material(self, name, base_hex, ramp=None, smoothness=0.25, emissive=None, ramp_shift=0.0, opts=None):
        """opts = shaderhints voor de stylized-lit shader (bv. flat, noShadow, rim, spec)."""
        self.materials[name] = dict(base=base_hex, ramp=ramp or [base_hex] * 5, smoothness=smoothness,
                                    emissive=emissive, ramp_shift=ramp_shift, opts=opts or {})

    def get(self, name):
        return next(p for p in self.parts if p.name == name)

    def world_matrix(self, part: Part):
        M = np.eye(4)
        chain = []
        p = part
        while p is not None:
            chain.append(p)
            p = self.get(p.parent) if p.parent else None
        for q in reversed(chain):
            L = np.eye(4)
            L[:3, :3] = q.rotation if q.rotation is not None else np.eye(3)
            L[:3, 3] = q.pivot
            M = M @ L
        return M

    def style_all(self, **opts):
        """Zet shaderhints (cells, cellSize, grime, grimeY) voor alle materialen, tenzij een materiaal ze zelf heeft."""
        for m in self.materials.values():
            for k, v in opts.items():
                m["opts"].setdefault(k, v)

    def facet_all(self, skip=()):
        """Low-poly look: alle vlakken plat (harde normalen), behalve onderdelen in `skip`."""
        for p in self.parts:
            if p.mesh is not None and p.name not in skip:
                p.mesh.sharp = True
                p.mesh.FS = None

    def bake_shading(self, density=3000.0, radius=0.12, noise_freq=4.0, edge_range=(9.0, 30.0), seed=1,
                     max_samples=120000):
        """Bak 'geschilderde' variatie per vertex in mesh.C = (R occlusie, G randlicht, B verfvariatie, A 1).

        R: 1 = open, 0 = diep in een hoek/onder een rand; inclusief zacht donkerder naar de onderkant.
        G: kromming (afgeronde randen) -> lichter randje, zoals de geschilderde bevels in het concept.
        B: lage-frequentie verfvlekken (fBm), 0.5 = neutraal.
        De stylized-lit shader (render.html, later Shader Graph) leest deze kanalen.
        """
        try:
            from scipy.spatial import cKDTree
        except ImportError:
            from spatial_fallback import cKDTree
        rng = np.random.default_rng(seed)
        parts = [p for p in self.parts if p.mesh is not None]
        world = {}
        for p in parts:
            M = self.world_matrix(p)
            Vw = p.mesh.V @ M[:3, :3].T + M[:3, 3]
            Nw = vertex_normals(p.mesh) @ M[:3, :3].T
            world[p.name] = (Vw, Nw)
        # oppervlakte-samples per groep (alleen zichtbare onderdelen werpen occlusie)
        groups = {}
        for p in parts:
            if not p.visible:
                continue
            Vw, _ = world[p.name]
            tris = [(f[0], f[i], f[i + 1]) for f in p.mesh.F for i in range(1, len(f) - 1)]
            T = np.array(tris)
            a, b, c = Vw[T[:, 0]], Vw[T[:, 1]], Vw[T[:, 2]]
            area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
            n = int(min(max_samples, max(50, area.sum() * density)))
            idx = rng.choice(len(T), n, p=area / area.sum())
            r1, r2 = rng.random(n), rng.random(n)
            sq = np.sqrt(r1)
            pts = (1 - sq)[:, None] * a[idx] + (sq * (1 - r2))[:, None] * b[idx] + (sq * r2)[:, None] * c[idx]
            groups.setdefault(p.ao_group, []).append(pts)
        trees = {g: cKDTree(np.vstack(v)) for g, v in groups.items()}
        for p in parts:
            Vw, Nw = world[p.name]
            Nn = Nw / np.maximum(np.linalg.norm(Nw, axis=1, keepdims=True), 1e-9)
            occ = np.zeros(len(Vw))
            cnt = np.zeros(len(Vw))
            for g in (p.ao_group,) + tuple(p.ao_receive):
                if g not in trees:
                    continue
                tree = trees[g]
                origin = Vw + Nn * radius * 0.04
                k = min(96, tree.n)
                d, ii = tree.query(origin, k=k, distance_upper_bound=radius)
                ok = np.isfinite(d)
                ii = np.where(ok, ii, 0)
                Q = tree.data[ii]
                D = Q - origin[:, None, :]
                L = np.maximum(np.linalg.norm(D, axis=2), 1e-9)
                cosv = np.einsum("nkd,nd->nk", D / L[:, :, None], Nn)
                w = np.clip(cosv - 0.25, 0, None) * (1 - np.clip(L / radius, 0, 1)) * ok
                occ += w.sum(1)
                cnt += ok.sum(1)
            frac = occ / np.maximum(cnt, 1)
            ao = 1 - np.clip(frac * 3.2, 0, 1) ** 0.9
            if p.gradient > 0:
                ys = Vw[:, 1]
                h = (ys - ys.min()) / max(1e-6, ys.max() - ys.min())
                ao = ao * (1 - p.gradient * (1 - np.clip(h / 0.7, 0, 1)) ** 1.5)
            # kromming via randen
            E = {}
            for f in p.mesh.F:
                for i in range(len(f)):
                    a_, b_ = f[i], f[(i + 1) % len(f)]
                    E[(min(a_, b_), max(a_, b_))] = 1
            Ei = np.array(list(E.keys()))
            VN = vertex_normals(p.mesh)
            k_e = np.linalg.norm(VN[Ei[:, 0]] - VN[Ei[:, 1]], axis=1) / np.maximum(
                np.linalg.norm(p.mesh.V[Ei[:, 0]] - p.mesh.V[Ei[:, 1]], axis=1), 1e-6)
            kv = np.zeros(len(Vw))
            np.maximum.at(kv, Ei[:, 0], k_e)
            np.maximum.at(kv, Ei[:, 1], k_e)
            e0, e1 = edge_range
            edge = np.clip((kv - e0) / (e1 - e0), 0, 1)
            edge = edge * edge * (3 - 2 * edge)
            if not p.edges:
                edge[:] = 0
            nz = fbm(Vw, noise_freq, 3, seed)
            nz = np.clip((nz - 0.5) * 1.8 + 0.5, 0, 1)
            p.mesh.C = np.stack([ao, edge, nz, np.ones(len(Vw))], axis=1).astype(np.float32)

    def stats(self):
        tris = sum(p.mesh.tri_count() for p in self.parts if p.mesh)
        verts = sum(len(p.mesh.V) for p in self.parts if p.mesh)
        return dict(parts=len(self.parts), tris=tris, verts=verts, materials=len(self.materials))

    # ------------------------------------------------------------ FBX ASCII
    def write_fbx_ascii(self, path, creator="Conductor Chaos meshkit"):
        uid = [100000]

        def nid():
            uid[0] += 1
            return uid[0]

        def arr(vals, fmt="{:.6f}"):
            return ",".join(fmt.format(v) if isinstance(v, float) else str(v) for v in vals)

        mat_ids = {name: nid() for name in self.materials}
        model_ids = {p.name: nid() for p in self.parts}
        geo_ids = {p.name: nid() for p in self.parts if p.mesh is not None}
        out = []
        w = out.append
        w("; FBX 7.4.0 project file")
        w("; Generated by %s" % creator)
        w("FBXHeaderExtension:  {")
        w("\tFBXHeaderVersion: 1003\n\tFBXVersion: 7400")
        w('\tCreator: "%s"' % creator)
        w("}")
        w("GlobalSettings:  {\n\tVersion: 1000\n\tProperties70:  {")
        for line in [
            'P: "UpAxis", "int", "Integer", "",1', 'P: "UpAxisSign", "int", "Integer", "",1',
            'P: "FrontAxis", "int", "Integer", "",2', 'P: "FrontAxisSign", "int", "Integer", "",1',
            'P: "CoordAxis", "int", "Integer", "",0', 'P: "CoordAxisSign", "int", "Integer", "",1',
            'P: "OriginalUpAxis", "int", "Integer", "",1', 'P: "OriginalUpAxisSign", "int", "Integer", "",1',
            'P: "UnitScaleFactor", "double", "Number", "",100', 'P: "OriginalUnitScaleFactor", "double", "Number", "",100']:
            w("\t\t" + line)
        w("\t}\n}")
        n_geo = len(geo_ids)
        w("Definitions:  {\n\tVersion: 100\n\tCount: %d" % (1 + len(self.parts) + n_geo + len(self.materials)))
        w('\tObjectType: "GlobalSettings" {\n\t\tCount: 1\n\t}')
        w('\tObjectType: "Model" {\n\t\tCount: %d\n\t}' % len(self.parts))
        w('\tObjectType: "Geometry" {\n\t\tCount: %d\n\t}' % n_geo)
        w('\tObjectType: "Material" {\n\t\tCount: %d\n\t}' % len(self.materials))
        w("}")
        w("Objects:  {")
        conns = []
        for p in self.parts:
            mid = model_ids[p.name]
            kind = "Mesh" if p.mesh is not None else "Null"
            w('\tModel: %d, "Model::%s", "%s" {' % (mid, p.name, kind))
            w("\t\tVersion: 232\n\t\tProperties70:  {")
            w('\t\t\tP: "RotationActive", "bool", "", "",1')
            w('\t\t\tP: "InheritType", "enum", "", "",1')
            w('\t\t\tP: "DefaultAttributeIndex", "int", "Integer", "",0')
            tx, ty, tz = p.pivot
            w('\t\t\tP: "Lcl Translation", "Lcl Translation", "", "A",%.6f,%.6f,%.6f' % (tx, ty, tz))
            if p.rotation is not None:
                rx, ry, rz = euler_xyz_deg(p.rotation)
                w('\t\t\tP: "Lcl Rotation", "Lcl Rotation", "", "A",%.6f,%.6f,%.6f' % (rx, ry, rz))
            if not p.visible:
                w('\t\t\tP: "Visibility", "Visibility", "", "A",0')
            w("\t\t}\n\t\tShading: T\n\t\tCulling: \"CullingOff\"\n\t}")
            conns.append((mid, model_ids[p.parent] if p.parent else 0))
            if p.mesh is None:
                continue
            m = p.mesh
            gid = geo_ids[p.name]
            w('\tGeometry: %d, "Geometry::%s", "Mesh" {' % (gid, p.name))
            verts = m.V.reshape(-1).tolist()
            w("\t\tVertices: *%d {\n\t\t\ta: %s\n\t\t} " % (len(verts), arr(verts)))
            pvi = []
            for f in m.F:
                pvi += list(f[:-1]) + [-(f[-1]) - 1]
            w("\t\tPolygonVertexIndex: *%d {\n\t\t\ta: %s\n\t\t} " % (len(pvi), arr(pvi)))
            w("\t\tGeometryVersion: 124")
            cn = corner_normals(m).reshape(-1).tolist()
            w('\t\tLayerElementNormal: 0 {\n\t\t\tVersion: 102\n\t\t\tName: ""\n\t\t\tMappingInformationType: "ByPolygonVertex"\n\t\t\tReferenceInformationType: "Direct"')
            w("\t\t\tNormals: *%d {\n\t\t\t\ta: %s\n\t\t\t} \n\t\t}" % (len(cn), arr(cn)))
            if m.C is not None:
                cc = np.array([m.C[i] for f in m.F for i in f]).reshape(-1).tolist()
                w('\t\tLayerElementColor: 0 {\n\t\t\tVersion: 101\n\t\t\tName: "Shade"\n\t\t\tMappingInformationType: "ByPolygonVertex"\n\t\t\tReferenceInformationType: "Direct"')
                w("\t\t\tColors: *%d {\n\t\t\t\ta: %s\n\t\t\t} \n\t\t}" % (len(cc), arr(cc, "{:.4f}")))
            used = []
            for name in m.M:
                if name not in used:
                    used.append(name)
            midx = [used.index(name) for name in m.M]
            w('\t\tLayerElementMaterial: 0 {\n\t\t\tVersion: 101\n\t\t\tName: ""\n\t\t\tMappingInformationType: "%s"\n\t\t\tReferenceInformationType: "IndexToDirect"'
              % ("ByPolygon" if len(used) > 1 else "AllSame"))
            mi = midx if len(used) > 1 else [0]
            w("\t\t\tMaterials: *%d {\n\t\t\t\ta: %s\n\t\t\t} \n\t\t}" % (len(mi), arr(mi)))
            w('\t\tLayer: 0 {\n\t\t\tVersion: 100\n\t\t\tLayerElement:  {\n\t\t\t\tType: "LayerElementNormal"\n\t\t\t\tTypedIndex: 0\n\t\t\t}'
              + ('\n\t\t\tLayerElement:  {\n\t\t\t\tType: "LayerElementColor"\n\t\t\t\tTypedIndex: 0\n\t\t\t}' if m.C is not None else '')
              + '\n\t\t\tLayerElement:  {\n\t\t\t\tType: "LayerElementMaterial"\n\t\t\t\tTypedIndex: 0\n\t\t\t}\n\t\t}')
            w("\t}")
            conns.append((gid, mid))
            for name in used:  # volgorde = materiaalindex
                conns.append((mat_ids[name], mid))
        for name, md in self.materials.items():
            r, g, b = srgb_hex(md["base"])
            w('\tMaterial: %d, "Material::%s", "" {' % (mat_ids[name], name))
            w('\t\tVersion: 102\n\t\tShadingModel: "lambert"\n\t\tMultiLayer: 0\n\t\tProperties70:  {')
            w('\t\t\tP: "DiffuseColor", "Color", "", "A",%.6f,%.6f,%.6f' % (r, g, b))
            w('\t\t\tP: "DiffuseFactor", "Number", "", "A",1')
            if md.get("emissive"):
                er, eg, eb = srgb_hex(md["emissive"])
                w('\t\t\tP: "EmissiveColor", "Color", "", "A",%.6f,%.6f,%.6f' % (er, eg, eb))
                w('\t\t\tP: "EmissiveFactor", "Number", "", "A",1')
            w("\t\t}\n\t}")
        w("}")
        w("Connections:  {")
        for a, b in conns:
            w('\tC: "OO",%d,%d' % (a, b))
        w("}")
        text = "\n".join(out) + "\n"
        # Sommige parsers (o.a. three.js FBXLoader) herkennen ASCII-FBX door tekens op vaste
        # posities te vergelijken met de binaire header; vul de commentaarregel op tot dat niet botst.
        pad = 0
        while not _ascii_fbx_detectable(text) and pad < 64:
            pad += 1
            out[1] = "; Generated by %s%s" % (creator, " " * pad)
            text = "\n".join(out) + "\n"
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)

    # ----------------------------------------------------------------- GLB
    def write_glb(self, path, include_hidden=True):
        bin_chunks = bytearray()
        accessors, views, meshes, nodes = [], [], [], []
        mat_names = list(self.materials)
        materials = []
        for name in mat_names:
            md = self.materials[name]
            lin = srgb_to_linear(srgb_hex(md["base"]))
            mat = {"name": name, "pbrMetallicRoughness": {"baseColorFactor": [*lin, 1.0], "metallicFactor": 0.0,
                                                           "roughnessFactor": 1.0 - md["smoothness"]},
                   "extras": {"ramp": md["ramp"], "rampShift": md["ramp_shift"], "opts": md["opts"]}}
            if md.get("emissive"):
                mat["emissiveFactor"] = list(srgb_to_linear(srgb_hex(md["emissive"])))
            materials.append(mat)

        def add_view(data: bytes, target=None):
            while len(bin_chunks) % 4:
                bin_chunks.append(0)
            off = len(bin_chunks)
            bin_chunks.extend(data)
            v = {"buffer": 0, "byteOffset": off, "byteLength": len(data)}
            if target:
                v["target"] = target
            views.append(v)
            return len(views) - 1

        def add_acc(arr_np, ctype, typ, target, minmax=False):
            view = add_view(arr_np.tobytes(), target)
            acc = {"bufferView": view, "componentType": ctype, "count": int(arr_np.shape[0]), "type": typ}
            if minmax:
                acc["min"] = arr_np.min(0).tolist()
                acc["max"] = arr_np.max(0).tolist()
            accessors.append(acc)
            return len(accessors) - 1

        name_to_node = {}
        for p in self.parts:
            node = {"name": p.name, "translation": [float(x) for x in p.pivot]}
            if p.rotation is not None:
                node["rotation"] = quat_from_matrix(p.rotation)
            if not p.visible:
                node["extras"] = {"hidden": True}
            if p.mesh is not None and (p.visible or include_hidden):
                m = p.mesh
                # splits polygonen in driehoeken; vertices per hoek (normalen per hoek)
                cn = corner_normals(m)
                prims = []
                k = 0
                per_mat = {}
                for f, mn in zip(m.F, m.M):
                    corners = list(range(k, k + len(f)))
                    k += len(f)
                    lst = per_mat.setdefault(mn, ([], []))
                    for i in range(1, len(f) - 1):
                        lst[0].append((f[0], corners[0]))
                        lst[0].append((f[i], corners[i]))
                        lst[0].append((f[i + 1], corners[i + 1]))
                for mn, (tri, _) in per_mat.items():
                    # dedupliceer (vertex, normaal) combinaties
                    keymap, pos, nor, idx, col = {}, [], [], [], []
                    for vi, ci in tri:
                        nkey = (vi, tuple(np.round(cn[ci], 4)))
                        if nkey not in keymap:
                            keymap[nkey] = len(pos)
                            pos.append(m.V[vi])
                            nor.append(cn[ci])
                            if m.C is not None:
                                col.append(m.C[vi])
                        idx.append(keymap[nkey])
                    P = np.array(pos, np.float32)
                    N = np.array(nor, np.float32)
                    I = np.array(idx, np.uint32)
                    attrs = {"POSITION": add_acc(P, 5126, "VEC3", 34962, True),
                             "NORMAL": add_acc(N, 5126, "VEC3", 34962)}
                    if col:
                        attrs["COLOR_0"] = add_acc(np.array(col, np.float32), 5126, "VEC4", 34962)
                    prims.append({"attributes": attrs,
                                  "indices": add_acc(I, 5125, "SCALAR", 34963),
                                  "material": mat_names.index(mn)})
                meshes.append({"name": p.name, "primitives": prims})
                node["mesh"] = len(meshes) - 1
            nodes.append(node)
            name_to_node[p.name] = len(nodes) - 1
        roots = []
        for p in self.parts:
            if p.parent:
                nodes[name_to_node[p.parent]].setdefault("children", []).append(name_to_node[p.name])
            else:
                roots.append(name_to_node[p.name])
        while len(bin_chunks) % 4:
            bin_chunks.append(0)
        gltf = {"asset": {"version": "2.0", "generator": "Conductor Chaos meshkit"},
                "scene": 0, "scenes": [{"name": self.name, "nodes": roots}], "nodes": nodes,
                "meshes": meshes, "materials": materials, "accessors": accessors, "bufferViews": views,
                "buffers": [{"byteLength": len(bin_chunks)}]}
        js = json.dumps(gltf, separators=(",", ":")).encode()
        while len(js) % 4:
            js += b" "
        total = 12 + 8 + len(js) + 8 + len(bin_chunks)
        with open(path, "wb") as fh:
            fh.write(struct.pack("<III", 0x46546C67, 2, total))
            fh.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
            fh.write(struct.pack("<II", len(bin_chunks), 0x004E4942) + bytes(bin_chunks))


# ------------------------------------------------------------------ extrusie
def extrude_profile(profile, x0, x1, nx=12, round0=0.0, round1=0.0, rings=4, mat_fn=None, mat="Default"):
    """Trek een gesloten 2D-doorsnede [(z, y), ...] (tegen de klok in) langs X van x0 naar x1.

    round0/round1: straal van de afgeronde begin/eindrand (doorsnede krimpt langs een kwartcirkel),
    daarna een vlakke kap. mat_fn(i) -> materiaal voor strook i (tussen profielpunt i en i+1),
    bijvoorbeeld voor een sierlijst langs de lengte. Gladde normalen (sharp=False).
    """
    P = np.array(profile, float)
    n = len(P)
    c = P.mean(axis=0)
    # 2D-vertexnormalen (naar buiten) voor het insnoeren van de rand
    N2 = np.zeros_like(P)
    for i in range(n):
        a, b = P[i - 1], P[(i + 1) % n]
        t = b - a
        nn = np.array([t[1], -t[0]])
        if np.dot(nn, P[i] - c) < 0:
            nn = -nn
        N2[i] = nn / (np.linalg.norm(nn) or 1)
    stations = []          # (x, inset)
    for k in range(rings + 1):
        th = math.pi / 2 * k / rings
        if round0 > 0:
            stations.append((x0 + round0 - round0 * math.sin(th), round0 * (1 - math.cos(th))))
    stations = list(reversed(stations))
    xa, xb = x0 + round0, x1 - round1
    for k in range(nx + 1):
        stations.append((xa + (xb - xa) * k / nx, 0.0))
    for k in range(1, rings + 1):
        th = math.pi / 2 * k / rings
        if round1 > 0:
            stations.append((xb + round1 * math.sin(th), round1 * (1 - math.cos(th))))
    # dubbele stations (zonder afronding) weghalen
    clean = []
    for s in stations:
        if not clean or abs(s[0] - clean[-1][0]) > 1e-9 or abs(s[1] - clean[-1][1]) > 1e-9:
            clean.append(s)
    V, F, M = [], [], []
    for (x, inset) in clean:
        Q = P - N2 * inset
        for (z, y) in Q:
            V.append((x, y, z))
    for r in range(len(clean) - 1):
        for i in range(n):
            j = (i + 1) % n
            a, b = r * n + i, r * n + j
            F.append((a, (r + 1) * n + i, (r + 1) * n + j, b))
            M.append(mat_fn(i) if mat_fn else mat)
    # kappen
    for r, sgn in ((0, -1), (len(clean) - 1, 1)):
        x, inset = clean[r]
        ci = len(V)
        cc = (P - N2 * inset).mean(axis=0)
        V.append((x, cc[1], cc[0]))
        for i in range(n):
            j = (i + 1) % n
            F.append((r * n + i, r * n + j, ci) if sgn < 0 else (r * n + j, r * n + i, ci))
            M.append(mat_fn(i) if (mat_fn and False) else (mat if not mat_fn else mat_fn(-1)))
    m = Mesh(np.array(V, float), F, M)
    # winding naar buiten (per face t.o.v. de lokale doorsnede-as)
    fixed = []
    for f in m.F:
        p = m.V[list(f)]
        nrm = np.cross(p[1] - p[0], p[2] - p[0])
        cen = p.mean(0)
        axis_pt = np.array([cen[0], c[1], c[0]])
        out = cen - axis_pt
        if len(f) == 3 and abs(cen[0] - x0) < 1e-6 or len(f) == 3 and abs(cen[0] - x1) < 1e-6:
            out = np.array([np.sign(cen[0] - (x0 + x1) / 2), 0, 0])
        fixed.append(f if np.dot(nrm, out) >= 0 else tuple(reversed(f)))
    m.F = fixed
    return m


def superellipse_arch(z0, z1, y_base, y_top, y_bottom, n=2.6, seg=20, fillet=0.06, fseg=3):
    """Doorsnede 'ketel': rechte zijkanten van y_bottom tot y_base, daarboven een superellips-boog tot y_top.
    Onderhoeken afgerond met fillet. Tegen de klok in (gezien vanaf +X), punten als (z, y)."""
    zc, a = (z0 + z1) / 2, (z1 - z0) / 2
    b = y_top - y_base
    pts = []
    # rechter onderhoek (z1, y_bottom) -> fillet
    for k in range(fseg + 1):
        th = -math.pi / 2 + math.pi / 2 * k / fseg
        pts.append((z1 - fillet + fillet * math.cos(th), y_bottom + fillet + fillet * math.sin(th)))
    # boog van rechts (z1, y_base) over de top naar links
    for k in range(seg + 1):
        th = math.pi * k / seg
        cz, sy = math.cos(th), math.sin(th)
        pts.append((zc + a * np.sign(cz) * abs(cz) ** (2 / n), y_base + b * abs(sy) ** (2 / n)))
    for k in range(fseg + 1):
        th = math.pi + math.pi / 2 * k / fseg
        pts.append((z0 + fillet + fillet * math.cos(th), y_bottom + fillet + fillet * math.sin(th)))
    # tegen de klok in gezien vanaf +X: z rechts, y omhoog -> volgorde rechts-onder, top, links-onder is CCW
    return pts
