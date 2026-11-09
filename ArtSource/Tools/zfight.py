"""Zoekt dubbele vlakken (z-fighting): twee zichtbare vlakken die (bijna) in hetzelfde vlak liggen, dezelfde kant
op kijken en elkaar overlappen. In Unity flikkeren die of tonen ze strepen (ook in de schaduw), afhankelijk van de
kijkhoek. Werkt op GLB-exports (alleen numpy nodig), voor één asset of een hele layout van geplaatste assets.

    python ArtSource/Tools/zfight.py ArtSource/Exports/Environment/Station/Station_Building.glb
    python ArtSource/Tools/zfight.py --layout Assets/Art/Layouts/StationExample.json

Regel voor nieuwe modellen (Docs/ART_STYLE_GUIDE.md): leg vlakken naast elkaar, niet als dunne plaat op een ander
vlak. Moet iets er toch op liggen (dorpel, mat, strip), dan minstens GAP boven het vlak eronder, of snij het vlak
eronder weg.
"""
import json
import math
import os
import struct
import sys
from collections import defaultdict

import numpy as np

GAP = 0.005         # vlakken die dichter dan 5 mm bij elkaar liggen en overlappen tellen als dubbel
MIN_AREA = 4e-4     # overlap kleiner dan 4 cm² negeren (randjes van afschuiningen, snijlijnen)
COS_PARALLEL = math.cos(math.radians(0.5))   # alleen (vrijwel) evenwijdig; snijdende bollingen tellen niet


def _quat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def glb_triangles(path, skip_hidden=True, hide=()):
    """[(knoopnaam, driehoeken (N,3,3) in assetcoördinaten)] voor alle zichtbare meshknopen."""
    b = open(path, "rb").read()
    n = struct.unpack("<I", b[12:16])[0]
    js = json.loads(b[20:20 + n])
    off = 20 + n
    bl = struct.unpack("<I", b[off:off + 4])[0]
    data = b[off + 8:off + 8 + bl]
    size = {5121: 1, 5123: 2, 5125: 4, 5126: 4}
    dt = {5121: "<u1", 5123: "<u2", 5125: "<u4", 5126: "<f4"}
    width = {"SCALAR": 1, "VEC3": 3}

    def acc(i):
        a = js["accessors"][i]
        bv = js["bufferViews"][a["bufferView"]]
        st = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        k = width[a["type"]]
        cnt = a["count"] * k
        return np.frombuffer(data[st:st + cnt * size[a["componentType"]]], dtype=dt[a["componentType"]]).reshape(-1, k)
    out = []

    def walk(i, M, name):
        nd = js["nodes"][i]
        nm = nd.get("name", "")
        if skip_hidden and ((nd.get("extras") or {}).get("hidden") or nm.startswith("Col_")):
            return
        if nm in hide:
            return
        if "matrix" in nd:
            L = np.array(nd["matrix"]).reshape(4, 4).T
        else:
            R = np.eye(4)
            R[:3, :3] = _quat(nd.get("rotation", [0, 0, 0, 1]))
            T = np.eye(4)
            T[:3, 3] = nd.get("translation", [0, 0, 0])
            L = T @ R @ np.diag(list(nd.get("scale", [1, 1, 1])) + [1])
        W = M @ L
        if "mesh" in nd:
            for pr in js["meshes"][nd["mesh"]]["primitives"]:
                V = acc(pr["attributes"]["POSITION"]).astype(float) @ W[:3, :3].T + W[:3, 3]
                I = acc(pr["indices"]).reshape(-1, 3) if "indices" in pr else np.arange(len(V)).reshape(-1, 3)
                mat = js["materials"][pr["material"]]["name"] if "material" in pr else ""
                out.append((f"{nm}/{mat}" if mat else nm, V[I]))
        for c in nd.get("children", []):
            walk(c, W, nm)
    for r in js["scenes"][0]["nodes"]:
        walk(r, np.eye(4), "")
    return out


def _clip(poly, a, b):
    """Sutherland-Hodgman: houd het deel van poly links van de lijn a->b (2D)."""
    res = []
    n = len(poly)
    if not n:
        return res
    ex, ey = b[0] - a[0], b[1] - a[1]

    def side(p):
        return ex * (p[1] - a[1]) - ey * (p[0] - a[0])
    for i in range(n):
        p, q = poly[i], poly[(i + 1) % n]
        sp, sq = side(p), side(q)
        if sp >= 0:
            res.append(p)
        if (sp >= 0) != (sq >= 0):
            t = sp / (sp - sq)
            res.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
    return res


def _area(poly):
    s = 0.0
    for i in range(len(poly)):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % len(poly)]
        s += x0 * y1 - x1 * y0
    return 0.5 * s


def _overlap(t1, t2):
    """Overlappend polygoon (lijst 2D-punten) van twee driehoeken."""
    p = [tuple(v) for v in t1]
    q = [tuple(v) for v in t2]
    if _area(p) < 0:
        p = p[::-1]
    if _area(q) < 0:
        q = q[::-1]
    for i in range(3):
        p = _clip(p, q[i], q[(i + 1) % 3])
        if len(p) < 3:
            return []
    return p


def _centroid(poly):
    a = _area(poly)
    cx = cy = 0.0
    for i in range(len(poly)):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % len(poly)]
        f = x0 * y1 - x1 * y0
        cx += (x0 + x1) * f
        cy += (y0 + y1) * f
    return cx / (6 * a), cy / (6 * a)


class _Inside:
    """Is een punt bedekt? Dan ligt het binnen een andere gesloten vorm (een los blok, cilinder ...), en is het vlak
    daar onzichtbaar. Vormen = samenhangende stukken mesh; binnen/buiten met een schuine straal (pariteit)."""

    DIR = np.array([0.5771, 0.5779, 0.5767]) / np.linalg.norm([0.5771, 0.5779, 0.5767])

    def __init__(self, T, lab):
        n = len(T)
        parent = np.arange(n)

        def root(i):
            while parent[i] != i:
                parent[i] = parent[parent[i]]
                i = parent[i]
            return i
        first = {}
        q = np.round(T * 1e4).astype(np.int64)
        for i in range(n):
            for k in range(3):
                key = (lab[i], q[i, k, 0], q[i, k, 1], q[i, k, 2])
                j = first.setdefault(key, i)
                if j != i:
                    ri, rj = root(i), root(j)
                    if ri != rj:
                        parent[ri] = rj
        comp = np.array([root(i) for i in range(n)])
        self.parts = []
        for c in np.unique(comp):
            m = comp == c
            if m.sum() < 4:
                continue
            P = T[m]
            self.parts.append((P.min((0, 1)), P.max((0, 1)), P[:, 0], P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]))

    def covered(self, p):
        d = self.DIR
        for lo, hi, v0, e1, e2 in self.parts:
            if np.any(p < lo + 1e-4) or np.any(p > hi - 1e-4):
                continue
            pv = np.cross(d, e2)
            det = (e1 * pv).sum(1)
            good = np.abs(det) > 1e-12
            inv = np.where(good, 1.0 / np.where(good, det, 1), 0)
            tv = p - v0
            u = (tv * pv).sum(1) * inv
            qv = np.cross(tv, e1)
            v = (qv * d).sum(1) * inv
            t = (e2 * qv).sum(1) * inv
            hit = good & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 0)
            if hit.sum() % 2 == 1:
                return True
        return False


def find(groups, gap=GAP, min_area=MIN_AREA, same_group=True, ground=None):
    """groups: [(label, tris (N,3,3))]. Geeft [(label_a, label_b, gebied m², hoogteverschil, middelpunt)].
    Driehoeken van hetzelfde label tellen ook mee (stukken die binnen één mesh op elkaar liggen) als same_group.
    Onderkanten op de grond (normaal omlaag, binnen 1 cm van ground, standaard het laagste punt) zie je nooit."""
    labels, tris = [], []
    for lab, T in groups:
        if len(T):
            labels += [lab] * len(T)
            tris.append(T)
    if not tris:
        return []
    T = np.concatenate(tris)
    lab = np.array(labels, dtype=object)
    e1, e2 = T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]
    N = np.cross(e1, e2)
    A = np.linalg.norm(N, axis=1)
    ok = A > 1e-9
    T, lab, N, A = T[ok], lab[ok], N[ok] / A[ok, None], A[ok] / 2
    g = T[:, :, 1].min() if ground is None else ground
    under = (N[:, 1] < -0.5) & (T[:, :, 1].max(1) < g + 0.01)
    # emmers op (gekwantiseerde normaal); alleen zelfde kant op kijkend telt (achterkanten worden niet getekend)
    key = np.round(N * 40).astype(int)
    buckets = defaultdict(list)
    for i, k in enumerate(map(tuple, key)):
        buckets[k].append(i)
    inside = _Inside(T, lab)

    def pressed(p, n):
        """Ligt het vlak tegen een ander vlak dat de andere kant op kijkt (twee blokken die elkaar raken)?
        Dan zit het ertussen en is het onzichtbaar."""
        m = (N @ n < -COS_PARALLEL) & np.all(T.min(1) <= p + 0.003, axis=1) & np.all(T.max(1) >= p - 0.003, axis=1)
        for i in np.nonzero(m)[0]:
            if abs(N[i] @ (p - T[i, 0])) > 0.003:
                continue
            a, b, c = T[i]
            v0, v1, v2 = b - a, c - a, p - a
            d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
            den = d00 * d11 - d01 * d01
            if den <= 0:
                continue
            bv = (d11 * d20 - d01 * d21) / den
            bw = (d00 * d21 - d01 * d20) / den
            if bv >= -1e-6 and bw >= -1e-6 and bv + bw <= 1 + 1e-6:
                return True
        return False
    hits = defaultdict(lambda: [0.0, 0.0, np.zeros(3), None])
    done = set()
    for k, idx in buckets.items():
        # buuremmers erbij, zodat normaalafronding geen paren mist
        cand = list(idx)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    if (dx, dy, dz) != (0, 0, 0):
                        cand += buckets.get((k[0] + dx, k[1] + dy, k[2] + dz), [])
        if len(cand) < 2:
            continue
        cand = np.array(sorted(set(cand)))
        n0 = N[idx[0]]
        # 2D-assenstelsel in het vlak
        u = np.cross(n0, [0, 1, 0] if abs(n0[1]) < 0.9 else [1, 0, 0])
        u /= np.linalg.norm(u)
        v = np.cross(n0, u)
        P = T[cand]
        d = P[:, 0] @ n0
        uv = np.stack([P @ u, P @ v], axis=-1)
        lo, hi = uv.min(1), uv.max(1)
        cell = 0.5
        grid = defaultdict(list)
        for j in range(len(cand)):
            for gx in range(int(math.floor(lo[j, 0] / cell)), int(math.floor(hi[j, 0] / cell)) + 1):
                for gy in range(int(math.floor(lo[j, 1] / cell)), int(math.floor(hi[j, 1] / cell)) + 1):
                    grid[(gx, gy)].append(j)
        own = set(idx)
        for members in grid.values():
            if len(members) < 2:
                continue
            for a_ in range(len(members)):
                ja = members[a_]
                ia = cand[ja]
                for jb in members[a_ + 1:]:
                    ib = cand[jb]
                    if ia not in own and ib not in own:
                        continue
                    if under[ia] or under[ib]:
                        continue
                    pair = (ia, ib) if ia < ib else (ib, ia)
                    if pair in done:
                        continue
                    done.add(pair)
                    if abs(d[ja] - d[jb]) > gap or N[ia] @ N[ib] < COS_PARALLEL:
                        continue
                    if not same_group and lab[ia] == lab[ib]:
                        continue
                    if lo[ja, 0] >= hi[jb, 0] or lo[jb, 0] >= hi[ja, 0] or lo[ja, 1] >= hi[jb, 1] or lo[jb, 1] >= hi[ja, 1]:
                        continue
                    # nauwkeurig in het eigen vlak van het paar (gemiddelde normaal), niet dat van de emmer
                    npair = N[ia] + N[ib]
                    npair /= np.linalg.norm(npair)
                    pu = np.cross(npair, [0, 1, 0] if abs(npair[1]) < 0.9 else [1, 0, 0])
                    pu /= np.linalg.norm(pu)
                    pv = np.cross(npair, pu)
                    poly = _overlap(np.stack([T[ia] @ pu, T[ia] @ pv], -1), np.stack([T[ib] @ pu, T[ib] @ pv], -1))
                    ar = abs(_area(poly)) if poly else 0.0
                    if ar <= 1e-7:
                        continue

                    # werkelijke afstand tussen de twee vlakken boven het overlapgebied (niet precies evenwijdig)
                    def height(i, q):
                        base = pu * q[0] + pv * q[1]
                        return (N[i] @ T[i, 0] - N[i] @ base) / (N[i] @ npair)
                    sep = max(abs(height(ia, q) - height(ib, q)) for q in poly)
                    if sep > gap:
                        continue
                    cx, cy = _centroid(poly)
                    top = max(height(ia, (cx, cy)), height(ib, (cx, cy)))
                    pt = pu * cx + pv * cy + npair * (top + 0.002)
                    if inside.covered(pt) or pressed(pu * cx + pv * cy + npair * top, npair):
                        continue
                    la, lb = sorted((lab[ia], lab[ib]))
                    h = hits[(la, lb, tuple(np.round(n0, 1)), tuple(np.round(pt / 0.5).astype(int)))]
                    h[0] += ar
                    h[1] = max(h[1], sep)
                    h[2] = h[2] + ar * pt
                    h[3] = n0
    res = []
    for (la, lb, _, _p), (ar, dz, c, nn) in hits.items():
        if ar >= min_area:
            res.append((la, lb, ar, dz, c / ar, nn))
    return sorted(res, key=lambda r: -r[2])


def place(tris, pos, rot_y_deg, unity=False):
    """Plaats driehoeken zoals de renderer (three.js: rotatie om y, dan verschuiving)."""
    a = math.radians(rot_y_deg)
    c, s = math.cos(a), math.sin(a)
    R = np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return tris @ R.T + np.asarray(pos, dtype=float)


def report(res, title):
    if not res:
        print(f"{title}: geen dubbele vlakken")
        return
    print(f"{title}: {len(res)} dubbele vlakken")
    for la, lb, ar, dz, c, n in res[:40]:
        print(f"  {la} <> {lb}: {ar * 1e4:.0f} cm², {dz * 100:.1f} cm uit elkaar, rond ({c[0]:.2f}, {c[1]:.2f}, "
              f"{c[2]:.2f}), normaal ({n[0]:.1f}, {n[1]:.1f}, {n[2]:.1f})")


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        sys.exit(1)
    for p in args:
        groups = glb_triangles(p)
        report(find(groups), os.path.basename(p))
