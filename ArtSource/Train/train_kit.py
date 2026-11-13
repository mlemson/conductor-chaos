"""Gedeelde bouwdoos voor de treinsets uit de upgrade-conceptbladen van @mlemson (Diesel jaren '60,
Modern Express, Futuristic Express).

Zelfde maatvoering en bouwwijze als de regionale wagon, zodat alles aan elkaar past:
lengte langs X, perronkant op z = 0 (kijkt naar +Z), perron op y = 0, vloer op y = 0.10, spoorstaaf op y = -0.62,
kopse doorgangen met balkonnetjes op dezelfde plek, dezelfde magneetkoppeling.

Nieuw in deze sets:
  * Ronde vormen: bak met ronde randen, gewelfd dak (`vault_roof`) met vlak loopdek in het midden (y = 2.50),
    ronde buik onder de bak en een neus uit vloeiende doorsneden (`loft`): romp + glazen kap, van dak tot bumper.
  * Dakopgang: een gele ladder binnen tegen de wand naar een dakluik met opstaande rand, en een gele ladder buiten
    op de perronkant die met de welving mee het dak op loopt. Geen relingen op het dak.
  * Onderhoudspunten: knoop `RepairPoint_<Naam>` met daaronder `Fixed_Rail__<Naam>` (heel), `Broken_Bits__<Naam>`
    (kapot, verborgen), `Repair_Bolt__<Naam>` (gele bout, steekt 15 cm uit) en `Repair_Bolt_Home__<Naam>`.
    Dat zijn de knoopnamen van `RepairPoint` (moersleutel, issue #215) met een achtervoegsel, omdat knoopnamen in
    één FBX uniek moeten zijn.
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import meshkit as mk  # noqa: E402
from train_couplers import add_couplers  # noqa: E402
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

# ---------------------------------------------------------------- maten (m), gelijk aan de regionale wagon
W = 2.60
ZC = -W / 2
Y0, Y1 = 0.0, 2.24
CH = 0.16
FLOOR = 0.10
WALL = 0.10
SILL_Y = Y0 + CH
END_DOOR = 0.45
END_TOP = 1.92
RAIL_Y = -0.62
LIP = 0.014
ROOF_OVER = 0.16                 # overstek van het dak
ROOF_TOP = Y1 + 0.26             # loopdek
BOLT_OUT = 0.15                  # zo ver steekt een losse bout uit


def box(x0, x1, y0, y1, z0, z1, r, mat, div=1, sharp=True):
    m = mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, r, div, mat=mat).transform(
        t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    m.sharp = sharp
    return m


def frame(x0, x1, y0, y1, w, z0, z1, r, mat, open_bottom=False):
    """Kader uit één stuk rond een opening; de binnenrand valt LIP over de dagkant (geen gedeeld vlak)."""
    return mk.frame_ring(x0 + LIP, x1 - LIP, y0 if open_bottom else y0 + LIP, y1 - LIP, w, z0, z1, min(r, w * 0.4),
                         mat=mat, open_bottom=open_bottom)


def mirror_far(m):
    """Perronkant (z = 0) -> overkant (z = -W)."""
    return m.transform(s=(1, 1, -1), t=(0, 0, -W))


def to_end(m, sgn, x):
    """Onderdeel gebouwd in het XY-vlak (lokale x = -z wereld bij sgn > 0, lokale z = naar buiten) naar een kopse kant."""
    return m.transform(R=mk.rot_y(math.pi / 2 if sgn > 0 else -math.pi / 2), t=(x, 0, 0))


def flat(m):
    """Onderdeel gebouwd in het XY-vlak (dikte langs lokale z) plat leggen: lokale z -> wereld y, lokale y -> -z."""
    return m.transform(R=mk.rot_x(-math.pi / 2))


def rot_to(normal):
    """Rotatie die de lokale +Y-as naar een hoofdrichting draait."""
    nx, ny, nz = normal
    if ny > 0.5:
        return None
    if nz > 0.5:
        return mk.rot_x(math.pi / 2)
    if nz < -0.5:
        return mk.rot_x(-math.pi / 2)
    if nx > 0.5:
        return mk.rot_z(-math.pi / 2)
    return mk.rot_z(math.pi / 2)


def tube(p0, p1, r, mat, seg=8):
    return mk.capsule_between(p0, p1, r, r, seg, 1, mat=mat)


def pipe_run(points, r, mat):
    """Doorlopende stang langs punten; om en om een fractie dunner, zodat bolkoppen in de hoeken niet samenvallen."""
    return [tube(a, b, r - 0.002 * (k % 2), mat) for k, (a, b) in enumerate(zip(points[:-1], points[1:]))]


def segs(x0, x1, excl):
    """Stukken van [x0, x1] buiten de uitgesloten bereiken (voor banden die om deuren en luiken heen lopen)."""
    out, xa = [], x0
    for (a, b) in sorted(excl):
        if a - xa > 0.05:
            out.append((xa, a))
        xa = max(xa, b)
    if x1 - xa > 0.05:
        out.append((xa, x1))
    return out


def c_ok(a, xa):
    return a - xa > 1e-6


ROOF_BASE = Y1 - 0.14            # hier begint de welving van het dak
ROOF_A = W / 2 + 0.03            # halve dakbreedte (3 cm buiten de wand)
ROOF_FLAT = 0.72                 # halve breedte van het vlakke loopdek


def shoulder(u):
    """Dakwelving dwars: 1 op het vlakke loopdek, kwart-ellips naar de dakrand (u = z - ZC)."""
    au = abs(u)
    if au <= ROOF_FLAT:
        return 1.0
    t = min(1.0, (au - ROOF_FLAT) / (ROOF_A - ROOF_FLAT))
    return math.sqrt(max(0.0, 1.0 - t * t))


def roof_y(u):
    return ROOF_BASE + (ROOF_TOP - ROOF_BASE) * shoulder(u)


def _ring(st, N, half):
    zc, a, n = st.get("zc", ZC), st["a"], st.get("n", 3.0)
    if half:
        yc, b = st["yb"], st["yt"] - st["yb"]
        ts = [math.pi * j / N for j in range(N + 1)]
    else:
        yc, b = (st["yb"] + st["yt"]) / 2, (st["yt"] - st["yb"]) / 2
        ts = [2 * math.pi * j / N for j in range(N)]
    out = []
    for t in ts:
        c, sn = math.cos(t), math.sin(t)
        out.append((st["x"], yc + b * math.copysign(abs(sn) ** (2 / n), sn), zc + a * math.copysign(abs(c) ** (2 / n), c)))
    return out


def loft(stations, N=16, half=False, mat="Default", mat_fn=None, cap0=False, cap1=False):
    """Vloeiende romp door superellips-doorsneden: stations = [{x, a (halve breedte), yb, yt, n (hoekigheid)}].
    half=True geeft een open schaal (alleen de bovenste helft, voor een glazen kap). mat_fn(i, j, middelpunt) kiest
    het materiaal per vlak, zodat kleurbanden op hoogte doorlopen. Vlakken wijzen altijd naar buiten."""
    rings = [_ring(st, N, half) for st in stations]
    nr = len(rings[0])
    V = [p for r in rings for p in r]
    Vn = np.array(V, float)
    cen = [np.array((st["x"], st["yb"] + (st["yt"] - st["yb"]) * (0.3 if half else 0.5), st.get("zc", ZC))) for st in stations]
    F, M = [], []
    segs = nr - 1 if half else nr
    for i in range(len(rings) - 1):
        for j in range(segs):
            q = [i * nr + j, (i + 1) * nr + j, (i + 1) * nr + (j + 1) % nr, i * nr + (j + 1) % nr]
            P = Vn[q]
            nrm = np.cross(P[2] - P[0], P[3] - P[1])
            c = P.mean(0)
            if np.linalg.norm(nrm) < 1e-9:
                continue
            if np.dot(nrm, c - (cen[i] + cen[i + 1]) / 2) < 0:
                q = q[::-1]
            fm = mat_fn(i, j, c) if mat_fn else mat
            if fm is None:                      # mat_fn geeft None: hier zit een gat
                continue
            F.append(tuple(q))
            M.append(fm)
    for (flag, i, sx) in ((cap0, 0, -1.0), (cap1, len(rings) - 1, 1.0)):
        if not flag:
            continue
        V.append(tuple(Vn[i * nr:(i + 1) * nr].mean(0)))
        k = len(V) - 1
        for j in range(segs):
            t = [k, i * nr + j, i * nr + (j + 1) % nr]
            P = np.array([V[a] for a in t], float)
            if np.cross(P[1] - P[0], P[2] - P[0])[0] * sx < 0:
                t = t[::-1]
            F.append(tuple(t))
            M.append(mat_fn(max(i - 1, 0), j, P.mean(0)) if mat_fn else mat)
    m = mk.Mesh(np.array(V, float), F, M)
    m.sharp = True
    return m


def smooth(stations, count=18, tip=1.7):
    """Vloeiend verloop door een paar sleuteldoorsneden (monotone spline per maat), dichter bemonsterd naar de punt."""
    from scipy.interpolate import PchipInterpolator
    xs = [st["x"] for st in stations]
    keys = [k for k in stations[0] if k != "x"]
    f = {k: PchipInterpolator(xs, [st[k] for st in stations]) for k in keys}
    t = np.linspace(0.0, 1.0, count)
    x = xs[0] + (xs[-1] - xs[0]) * (1.0 - (1.0 - t) ** tip)
    return [dict(x=float(xi), **{k: float(f[k](xi)) for k in keys}) for xi in x]


def vshell(x0, levels, N=24, mat="Default", mat_fn=None, cap_top=True, cap_bottom=True):
    """Ronde kop als open schaal voor een kopwand, opgebouwd uit horizontale ringen (halve superellips in het
    grondvlak): levels = [{y, a (halve breedte), d (diepte naar voren), n (hoekigheid)}] van onder naar boven.
    Omdat de ringen horizontaal liggen, vallen raamband, sierbanden en dakrand precies op de lijnen van het model.
    mat_fn(i, j, jj, middelpunt): i = laag, j = segment (0 = overkant ... N-1 = perronkant), jj = afstand tot de zijkant."""
    rings = []
    for lv in levels:
        e = 2.0 / lv.get("n", 3.5)
        ring = []
        for j in range(N + 1):
            t = -math.pi / 2 + math.pi * j / N
            sn, cs = math.sin(t), math.cos(t)
            ring.append((x0 + lv["d"] * abs(cs) ** e, lv["y"], ZC + lv["a"] * math.copysign(abs(sn) ** e, sn)))
        rings.append(ring)
    nr = N + 1
    V = [p for r in rings for p in r]
    Vn = np.array(V, float)
    F, M = [], []
    for i in range(len(rings) - 1):
        for j in range(N):
            q = [i * nr + j, (i + 1) * nr + j, (i + 1) * nr + j + 1, i * nr + j + 1]
            P = Vn[q]
            nrm = np.cross(P[2] - P[0], P[3] - P[1])
            c = P.mean(0)
            if np.linalg.norm(nrm) < 1e-10:
                continue
            if np.dot(nrm, c - np.array((x0 - 0.3, c[1], ZC))) < 0:
                q = q[::-1]
            F.append(tuple(q))
            M.append(mat_fn(i, j, min(j, N - 1 - j), c) if mat_fn else mat)
    for (flag, i, sy) in ((cap_bottom, 0, -1.0), (cap_top, len(rings) - 1, 1.0)):
        if not flag:
            continue
        V.append((x0, levels[i]["y"], ZC))
        k = len(V) - 1
        for j in range(N):
            t = [k, i * nr + j, i * nr + j + 1]
            P = np.array([V[a] for a in t], float)
            if np.cross(P[1] - P[0], P[2] - P[0])[1] * sy < 0:
                t = t[::-1]
            F.append(tuple(t))
            M.append(mat_fn(max(i - 1, 0), j, min(j, N - 1 - j), P.mean(0)) if mat_fn else mat)
    m = mk.Mesh(np.array(V, float), F, M)
    m.sharp = True
    return m


def shell_point(x0, lv, u):
    """Punt op een ring van `vshell` op afstand u uit het midden (om er een lamp op te zetten)."""
    e = lv.get("n", 3.5)
    c = max(0.0, 1.0 - (abs(u) / lv["a"]) ** e) ** (1.0 / e)
    return (x0 + lv["d"] * c, lv["y"], ZC + u)


def _samples(x0, x1, r0, r1, step, extra=()):
    out = {x0, x1}
    ns = 6
    for (r, base, sg) in ((r0, x0, 1.0), (r1, x1, -1.0)):
        if r:
            out |= {base + sg * r * (1 - math.cos(k * math.pi / 2 / ns)) for k in range(1, ns + 1)}
    a, b = x0 + (r0 or 0.0), x1 - (r1 or 0.0)
    n = max(1, int(round((b - a) / step)))
    out |= {a + (b - a) * k / n for k in range(n + 1)}
    keep = sorted(e for e in extra if x0 < e < x1)
    vals = [v for v in sorted(out) if all(abs(v - e) > 0.03 for e in keep)] + keep
    return sorted(vals)


def vault_roof(x0, x1, mat, holes=(), round0=0.5, round1=0.5):
    """Gewelfd dak uit één stuk: vlak loopdek in het midden, ronde schouders naar de dakrand en ronde kopse einden,
    met echte luikgaten (x0, x1, z0, z1). Geen onderkant: het dak ligt als kap over de bak."""
    g = 0.015                       # luikgat iets ruimer dan dat in de bak: de dagkanten delen geen vlak
    hs = [(a - g, b + g, c - ZC - g, d - ZC + g) for (a, b, c, d) in holes]
    xs = _samples(x0, x1, round0, round1, 0.55, [v for h in hs for v in h[:2]])
    us = _samples(-ROOF_A, ROOF_A, ROOF_A - ROOF_FLAT, ROOF_A - ROOF_FLAT, 0.36, [v for h in hs for v in h[2:]])

    def top(x, u):
        e = 1.0
        for (r, d) in ((round0, x - x0), (round1, x1 - x)):
            if r and d < r:
                e *= math.sqrt(max(0.0, 1.0 - ((r - d) / r) ** 2))
        return ROOF_BASE + (ROOF_TOP - ROOF_BASE) * shoulder(u) * e
    nx, nu = len(xs), len(us)
    V = [(xs[i], top(xs[i], us[j]), ZC + us[j]) for i in range(nx) for j in range(nu)]

    def hole_at(i, j):
        if i < 0 or j < 0 or i >= nx - 1 or j >= nu - 1:
            return None
        cx, cu = (xs[i] + xs[i + 1]) / 2, (us[j] + us[j + 1]) / 2
        return any(a < cx < b and c < cu < d for (a, b, c, d) in hs)
    F, M = [], []

    def face(ids, want):
        P = np.array([V[k] for k in ids], float)
        n = np.cross(P[2] - P[0], P[3] - P[1]) if len(ids) == 4 else np.cross(P[1] - P[0], P[2] - P[0])
        if np.linalg.norm(n) < 1e-10:
            return
        F.append(tuple(ids if np.dot(n, want) > 0 else ids[::-1]))
        M.append(mat)
    for i in range(nx - 1):
        for j in range(nu - 1):
            if hole_at(i, j):
                continue
            a, b, c, d = i * nu + j, (i + 1) * nu + j, (i + 1) * nu + j + 1, i * nu + j + 1
            face([a, b, c, d], (0, 1, 0))
            cx, cu = (xs[i] + xs[i + 1]) / 2, (us[j] + us[j + 1]) / 2
            for (p, q, ni, nj) in ((a, b, i, j - 1), (d, c, i, j + 1), (a, d, i - 1, j), (b, c, i + 1, j)):
                other = hole_at(ni, nj)
                if other is False:
                    continue
                yb = (Y1 - 0.06) if other else (ROOF_BASE - 0.10)
                V.append((V[p][0], yb, V[p][2]))
                V.append((V[q][0], yb, V[q][2]))
                mid = ((V[p][0] + V[q][0]) / 2 - cx, 0.0, (V[p][2] + V[q][2]) / 2 - ZC - cu)
                face([p, q, len(V) - 1, len(V) - 2], mid)
    m = mk.Mesh(np.array(V, float), F, M)
    m.sharp = True
    return m


class Car:
    """Eén voertuig: meshkit-scène + helpers. `prefix` = materiaalgroep (M_<prefix>_<Deel>)."""

    def __init__(self, name, root, prefix, L):
        self.sc = mk.Scene(name)
        self.name, self.root, self.p, self.L = name, root, prefix, L
        self.sc.add(mk.Part(root, None))
        self.repairs = []
        self.door_style = "manual"       # kopdeuren: "manual" (houten deur met greep) of "auto" (glazen schuifdeur)

    def m(self, key):
        return f"M_{self.p}_{key}"

    def add(self, name, mesh, pivot=(0, 0, 0), parent=None, **kw):
        return self.sc.add(mk.Part(name, mesh, pivot, parent or self.root, **kw))

    # ------------------------------------------------------------ materialen
    def materials(self, colors):
        """colors: {deel: (ramp, index, smoothness, ramp_shift[, opts])}. Vaste delen (trim, glas, ...) komen erbij."""
        R_ = RAMPS
        sc = self.sc
        for key, spec in colors.items():
            ramp, idx, smooth, shift = spec[:4]
            opts = dict(spec[4]) if len(spec) > 4 else {"paint": 1.4}
            emissive = R_[ramp][min(idx + 1, 4)] if opts.get("emissive") else None
            sc.material(self.m(key), R_[ramp][idx], R_[ramp], smooth, emissive=emissive, ramp_shift=shift, opts=opts)
        std = {
            "Trim": ("train_trim", 1, 0.20, 0.0, {"paint": 1.2}),
            "Wheel": ("ui_bg", 2, 0.30, 0.0, {"paint": 0.8}),
            "Metal": ("metal", 2, 0.35, 0.0, {"paint": 1.0}),
            "Interact": ("accent_yellow", 2, 0.30, 0.0, {}),
            "Inner": ("cab_interior", 3, 0.15, 0.05, {}),
            "Floor": ("interior_wood", 1, 0.15, -0.12, {}),
            "Smoke": ("haze_purple", 1, 0.05, -0.10, {"cells": 0.0, "grime": 0.0}),
            "Steam": ("cloud_snow", 3, 0.05, 0.0, {"cells": 0.0, "grime": 0.0}),
            "Go": ("dino_green", 3, 0.45, 0.0, {"emissive": 0.7, "rim": 0.0, "paint": 0, "cells": 0.0, "grime": 0.0}),
        }
        for key, (ramp, idx, smooth, shift, opts) in std.items():
            if self.m(key) not in sc.materials:
                sc.material(self.m(key), R_[ramp][idx], R_[ramp], smooth, ramp_shift=shift, opts=dict(opts),
                            emissive=R_[ramp][min(idx + 1, 4)] if opts.get("emissive") else None)
        if self.m("Glass") not in sc.materials:
            sc.material(self.m("Glass"), R_["sky"][2], R_["sky"], 0.85, ramp_shift=0.05,
                        opts={"flat": 0.6, "rim": 0.35, "opacity": 0.22, "cells": 0.0, "grime": 0.0})
        sc.material(self.m("GlassShine"), R_["sky"][2], R_["sky"], 0.70, ramp_shift=-0.05,
                    opts={"flat": 0.85, "rim": 0, "opacity": 0.55, "cells": 0.0, "grime": 0.0})
        sc.material(self.m("GlassDark"), R_["ui_bg"][1], R_["ui_bg"], 0.80, ramp_shift=0.0,
                    opts={"flat": 0.6, "rim": 0.3, "opacity": 0.78, "cells": 0.0, "grime": 0.0})
        if self.m("Lamp") not in sc.materials:
            sc.material(self.m("Lamp"), R_["headlight"][2], R_["headlight"], 0.50, emissive=R_["headlight"][3],
                        opts={"emissive": 0.85, "rim": 0.0, "paint": 0})
        sc.material(self.m("Hot"), R_["title_red"][3], R_["title_red"], 0.40, emissive=R_["title_red"][3],
                    opts={"emissive": 0.9, "rim": 0.0, "paint": 0, "cells": 0.0, "grime": 0.0})

    # ------------------------------------------------------------------- bak
    def body(self, win_near, win_far, win_y, doors_near=(), doors_far=(), ends=(1, -1), end_holes=(), roof_holes=(),
             door_top=1.86, mat="Body"):
        """Holle bak uit één stuk met ramen, zijdeuren, kopse doorgangen, extra gaten in de kopwanden en dakluiken.
        end_holes: (sgn, y0, y1, z0, z1); roof_holes: (x0, x1, z0, z1)."""
        L = self.L
        outer = (-L / 2, L / 2, Y0, Y1, ZC - W / 2, ZC + W / 2)
        inner = (-L / 2 + WALL, L / 2 - WALL, Y0 + FLOOR, Y1 - WALL, ZC - W / 2 + WALL, ZC + W / 2 - WALL)
        holes = []
        for sgn, wins, doors in ((1, win_near, doors_near), (-1, win_far, doors_far)):
            holes += [(2, sgn, a, b, win_y[0], win_y[1]) for (a, b) in wins]
            holes += [(2, sgn, a, b, SILL_Y, door_top) for (a, b) in doors]
        for sgn in ends:
            holes.append((0, sgn, SILL_Y, END_TOP, ZC - END_DOOR, ZC + END_DOOR))
        holes += [(0, sgn, y0, y1, z0, z1) for (sgn, y0, y1, z0, z1) in end_holes]
        holes += [(1, 1, x0, x1, z0, z1) for (x0, x1, z0, z1) in roof_holes]
        mesh = mk.hollow_box(outer, inner, holes, radius=CH, step=0.24, div=3, mat=self.m(mat), inner_mat=self.m("Inner"))
        self.win = (list(win_near), list(win_far), win_y)
        self.ends_open = tuple(ends)
        self.door_top = door_top
        return self.add("Body", mesh)

    def glass(self, x0, x1, y0, y1, z):
        """Doorzichtig glas met één schuine luchtreflectie (6 mm voor het glas)."""
        g = box(x0, x1, y0, y1, z - 0.03, z, 0.004, self.m("Glass"))
        w, h = x1 - x0, y1 - y0
        pts = np.array([[x0 + 0.08 * w, y1 - 0.02 * h], [x0 + 0.48 * w, y1 - 0.02 * h],
                        [x0 + 0.20 * w, y0 + 0.30 * h], [x0 + 0.02 * w, y0 + 0.48 * h]])
        V = np.array([[x, y, z + 0.006] for x, y in pts])
        shine = mk.Mesh(V, [(0, 3, 2, 1)], [self.m("GlassShine")])
        shine.sharp = True
        return mk.merge(g, shine)

    def windows(self, frame_mat="Trim", fw=0.075):
        near, far, wy = self.win
        win = []
        for (a, b) in near:
            win += [frame(a, b, wy[0], wy[1], fw, -0.01, 0.045, 0.022, self.m(frame_mat)), self.glass(a, b, wy[0], wy[1], -0.035)]
        for (a, b) in far:
            win += [mirror_far(frame(a, b, wy[0], wy[1], fw, -0.01, 0.045, 0.022, self.m(frame_mat))),
                    mirror_far(self.glass(a, b, wy[0], wy[1], -0.035))]
        return self.add("Windows", mk.merge(*win))

    def end_window(self, sgn, z0, z1, y0, y1, frame_mat="Trim", fw=0.075):
        """Raam in een kopwand (voorruit): kader + glas."""
        ex = sgn * self.L / 2
        u0, u1 = (-z1, -z0) if sgn > 0 else (z0, z1)
        return [to_end(frame(u0, u1, y0, y1, fw, -0.01, 0.045, 0.022, self.m(frame_mat)), sgn, ex),
                to_end(self.glass(u0, u1, y0, y1, -0.035), sgn, ex)]

    def bands(self, name, specs):
        """Livrei: (x0, x1, y0, y1, materiaal, diepte) op beide zijwanden; steekt 'diepte' voor de wand uit."""
        out = []
        for (x0, x1, y0, y1, mat, depth) in specs:
            b = box(x0, x1, y0, y1, -0.02, depth, min(0.02, (y1 - y0) * 0.3), self.m(mat))
            out += [b, mirror_far(b)]
        return self.add(name, mk.merge(*out))


    def window_band(self, name, y0, y1, mat, depth=0.024, doors_near=(), doors_far=(), x0=None, x1=None):
        """Raamband uit één stuk per zijwand: plaat met gaten voor ramen en deuren (de gaten zijn 2 cm ruimer dan de
        dagkant en vallen onder de raamkaders, zodat band en dagkant geen vlak delen)."""
        L = self.L
        xa = -L / 2 + 0.12 if x0 is None else x0
        xb = L / 2 - 0.12 if x1 is None else x1
        near, far, wy = self.win
        out = []
        for wins, doors, is_far in ((near, doors_near, False), (far, doors_far, True)):
            holes = [(a - 0.02, b + 0.02, wy[0] - 0.02, wy[1] + 0.02) for (a, b) in wins if a > xa and b < xb]
            holes += [(a - 0.02, b + 0.02, y0 - 1.0, self.door_top + 0.02) for (a, b) in doors]
            holes = [(a, b, max(c, y0), min(d, y1)) for (a, b, c, d) in holes]
            plate = self._plate((xa, xb, y0, y1, -0.02, depth), holes, self.m(mat))
            out.append(mirror_far(plate) if is_far else plate)
        return self.add(name, mk.merge(*out))

    def _plate(self, bounds, holes, mat):
        """Plaat met gaten; een gat dat de onder- of bovenrand raakt (deur) splitst de plaat in losse stukken."""
        x0, x1, y0, y1, z0, z1 = bounds
        cuts = sorted([(a, b) for (a, b, c, d) in holes if c <= y0 + 1e-6 or d >= y1 - 1e-6])
        segs, xa = [], x0
        for (a, b) in cuts:
            if c_ok(a, xa):
                segs.append((xa, a))
            xa = b
        segs.append((xa, x1))
        out = []
        for (sa, sb) in segs:
            if sb - sa < 0.06:
                continue
            hs = [h for h in holes if h[0] > sa and h[1] < sb and h[2] > y0 + 1e-6 and h[3] < y1 - 1e-6]
            out.append(mk.through_box((sa, sb, y0, y1, z0, z1), hs, radius=0.012, step=0.30, mat=mat) if hs else
                       box(sa, sb, y0, y1, z0, z1, 0.012, mat))
        return mk.merge(*out)


    # ------------------------------------------------------------- zijdeuren
    def side_door(self, x0, x1, far=False, leaf="Door", frame_mat="Interact", panel=None, mark=None, single=False):
        """Dubbele schuifdeur (dicht) in een zijwand: kader uit één stuk, twee bladen met ruit en gele grepen, opstap.
        Knopen: SideDoorFrame, SideDoorLeaf_L/R, SideDoorSill (+ _Far aan de overkant). De bladen schuiven open langs
        lokale X (elk een halve deurbreedte van elkaar af); pivot = midden-onder van het blad, zoals DoorLeaf_L/R van
        de regionale wagon. Bewust een andere knoopnaam: TrainDrive bedient nu alleen DoorLeaf_* van één wagon."""
        sfx = "_Far" if far else ""
        side = mirror_far if far else (lambda m: m)
        flip = (lambda m: m.transform(s=(1, 1, -1))) if far else (lambda m: m)
        zs = -1.0 if far else 1.0
        zw = -W if far else 0.0
        top = self.door_top
        it, tr = self.m("Interact"), self.m("Trim")
        self.add("SideDoorFrame" + sfx, side(frame(x0, x1, Y0 + 0.08, top, 0.08, -0.01, 0.05, 0.022, self.m(frame_mat),
                                                    open_bottom=True)))
        half = (x1 - x0) if single else (x1 - x0) / 2
        leaf_h = top - SILL_Y + 0.08
        leaves = ((("SideDoorLeaf" + sfx, (x0 + x1) / 2, -1),) if single else
                  (("SideDoorLeaf_L" + sfx, x0 + half / 2, -1), ("SideDoorLeaf_R" + sfx, x1 - half / 2, 1)))
        for nm, cx, sgn in leaves:
            hw = half / 2 - 0.008
            parts = [mk.through_box((-hw, hw, 0.0, leaf_h, -0.03, 0.03), [(-hw + 0.11, hw - 0.11, 0.80, 1.46)], 0.022,
                                    step=0.3, mat=self.m(leaf)),
                     frame(-hw + 0.11, hw - 0.11, 0.80, 1.46, 0.045, 0.0, 0.05, 0.015, tr),
                     self.glass(-hw + 0.11, hw - 0.11, 0.80, 1.46, 0.015)]
            if panel:
                parts.append(box(-hw + 0.08, hw - 0.08, 0.14, 0.56, 0.02, 0.048, 0.015, self.m(panel)))
            if mark:      # pijltje naar de kant waarheen het blad openschuift
                a = np.array([[sgn * 0.10, 0.35], [-sgn * 0.06, 0.47], [-sgn * 0.06, 0.23]])
                V = np.array([[x, y, 0.056] for x, y in a])
                tri = mk.Mesh(V, [(0, 1, 2) if sgn > 0 else (0, 2, 1)], [self.m(mark)])
                tri.sharp = True
                parts.append(tri)
            gx = -sgn * (hw - 0.05)
            for face in (1, -1):
                z = face * 0.075
                parts.append(tube((gx, 0.52, z), (gx, 0.95, z), 0.022, it))
                parts += [box(gx - 0.019, gx + 0.019, y - 0.019, y + 0.019, min(z, 0), max(z, 0), 0.008, it) for y in (0.54, 0.93)]
            self.add(nm, flip(mk.merge(*parts)), (cx, SILL_Y - 0.04, zw - zs * 0.05))
        sill = mk.merge(box(x0 - 0.10, x1 + 0.10, Y0 + 0.06, SILL_Y + 0.02, -0.14, 0.32, 0.025, tr),
                        box(x0, x1, SILL_Y + 0.01, SILL_Y + 0.03, 0.0, 0.31, 0.008, it))
        rails = []
        for gx in (x0 - 0.19, x1 + 0.19):
            rails += pipe_run([(gx, 0.40, -0.01), (gx, 0.40, 0.11), (gx, 1.60, 0.11), (gx, 1.60, -0.01)], 0.026, it)
        self.add("SideDoorSill" + sfx, side(mk.merge(sill, *rails)))

    # ------------------------------------------------------------------- dak
    def roof(self, holes=(), mat="Roof", front_open=False):
        """Gewelfd dak met vlak loopdek en echte luikgaten (x0, x1, z0, z1). front_open: geen ronding aan +X, daar
        sluit de neuskap op het dak aan."""
        L = self.L
        return vault_roof(-L / 2 - 0.03, L / 2 + (0.02 if front_open else 0.03), self.m(mat), holes,
                          round1=0.0 if front_open else 0.5)

    def hatch(self, key, x0, x1, z0, z1, hinge="far"):
        """Dakluik: opstaande rand uit één stuk + grijpbeugels (vast) en een open klep (losse knoop RoofHatch_<key>).
        De klep scharniert om lokale X; dicht = rotatie 0."""
        rim = flat(frame(x0, x1, -z1, -z0, 0.08, ROOF_TOP - 0.34, ROOF_TOP + 0.12, 0.02, self.m("Trim")))
        zh = z0 - 0.08 if hinge == "far" else z1 + 0.08
        sg = 1 if hinge == "far" else -1
        lid = box(0.0, x1 - x0 + 0.12, 0.0, 0.06, 0.0, z1 - z0 + 0.12, 0.02, self.m("Roof"))
        grip = tube((0.30, 0.09, z1 - z0 - 0.10), (x1 - x0 - 0.18, 0.09, z1 - z0 - 0.10), 0.024, self.m("Interact"))
        feet = [box(xx - 0.02, xx + 0.02, 0.05, 0.09, z1 - z0 - 0.12, z1 - z0 - 0.08, 0.006, self.m("Interact"))
                for xx in (0.30, x1 - x0 - 0.18)]
        lidm = mk.merge(lid, grip, *feet)
        if sg < 0:
            lidm = lidm.transform(s=(1, 1, -1))
        self.add(f"RoofHatch_{key}", lidm, (x0 - 0.06, ROOF_TOP + 0.13, zh), rotation=mk.rot_x(math.radians(-104 * sg)))
        return rim

    # ------------------------------------------------------------- onderstel
    def underframe(self, bogie_x, hub="BodyLow", belly="BodyLow", x0=None, x1=None):
        """Ronde buik onder de bak (één vloeiende romp) met draaistellen; de wielen steken er half onderuit."""
        L = self.L
        xa = -L / 2 + 0.10 if x0 is None else x0
        xb = L / 2 - 0.10 if x1 is None else x1
        st = [dict(x=xa, a=1.12, yb=-0.10, yt=0.06, n=4.0), dict(x=xa + 0.10, a=1.26, yb=-0.20, yt=0.07, n=4.5),
              dict(x=xa + 0.30, a=W / 2 + 0.03, yb=-0.26, yt=0.07, n=5.0),
              dict(x=xb - 0.30, a=W / 2 + 0.03, yb=-0.26, yt=0.07, n=5.0),
              dict(x=xb - 0.10, a=1.26, yb=-0.20, yt=0.07, n=4.5), dict(x=xb, a=1.12, yb=-0.10, yt=0.06, n=4.0)]
        under = [loft(st, N=20, mat=self.m(belly), cap0=True, cap1=True)]
        for bx in bogie_x:
            under.append(box(bx - 0.95, bx + 0.95, -0.44, -0.20, ZC - W / 2 + 0.16, ZC + W / 2 - 0.16, 0.08, self.m("Trim"), div=2))
            for ax in (bx - 0.62, bx + 0.62):
                for zs in (ZC + 1.29, ZC - 1.29):
                    w = mk.cylinder(0.33, 0.12, 14, 0.04, mat=self.m("Wheel"), axis="z")
                    h = mk.cylinder(0.13, 0.16, 8, 0.03, mat=self.m(hub), axis="z")
                    w.sharp = h.sharp = True
                    under.append(mk.merge(w, h).transform(t=(ax, RAIL_Y + 0.33, zs)))
        return self.add("Underframe", mk.merge(*under))

    # ------------------------------------------------------------ kopse kant
    def end(self, sgn, lamps="Lamp"):
        """Kopse doorgang: deurkader, een harmonica (balg) uit één stuk waar je doorheen loopt, en kleine lampen."""
        ex = sgn * self.L / 2
        tr, it = self.m("Trim"), self.m("Interact")
        u0, u1 = (-(ZC + END_DOOR), -(ZC - END_DOOR)) if sgn > 0 else (ZC - END_DOOR, ZC + END_DOOR)
        out = [to_end(frame(u0, u1, Y0 + 0.08, END_TOP, 0.07, -0.01, 0.045, 0.022, tr, open_bottom=True), sgn, ex),
               to_end(mk.frame_ring(u0 - 0.10, u1 + 0.10, SILL_Y + 0.02, END_TOP + 0.10, 0.13, -0.02, 0.40, 0.05, mat=tr), sgn, ex)]
        xa, xb = sorted((ex + sgn * 0.01, ex + sgn * 0.39))
        out.append(box(xa, xb, SILL_Y + 0.01, SILL_Y + 0.034, ZC - END_DOOR, ZC + END_DOOR, 0.006, it))
        if lamps:
            for dz in (-0.92, 0.92):
                house = mk.cylinder(0.13, 0.10, 8, 0.027, mat=tr, axis="x")
                lens = mk.cylinder(0.095, 0.07, 8, 0.012, mat=self.m(lamps), axis="x").transform(t=(0.03, 0, 0))
                house.sharp = lens.sharp = True
                out.append(mk.merge(house, lens).transform(R=None if sgn > 0 else mk.rot_y(math.pi), t=(ex + sgn * 0.04, 0.62, ZC + dz)))
        self.end_door(sgn)
        return out

    def end_door(self, sgn):
        """Kopdeur: twee schuifbladen (dicht) aan de binnenkant van de kopwand, zodat de doorgang kan sluiten.
        Knopen EndDoorLeaf_L_<Front|Rear> en EndDoorLeaf_R_...; open = elk blad 0,46 m van het midden af schuiven langs
        lokale Z (L naar +Z, R naar -Z). Pivot = midden-onder van het blad. De bladen passen open naast de doorgang."""
        tag = "Front" if sgn > 0 else "Rear"
        xw = sgn * (self.L / 2 - WALL - 0.032)
        w = END_DOOR + 0.012
        y0 = Y0 + FLOOR + 0.03
        h = END_TOP + 0.05 - y0
        hw = w / 2 - 0.003
        tr, it = self.m("Trim"), self.m("Interact")
        for nm, side in (("L", -1), ("R", 1)):
            if self.door_style == "auto":
                hole = (-hw + 0.055, hw - 0.055, 0.22, h - 0.10)
                bits = [mk.through_box((-hw, hw, 0.0, h, -0.02, 0.02), [hole], 0.012, step=0.3, mat=tr),
                        self.glass(hole[0], hole[1], hole[2], hole[3], 0.012),
                        box(side * hw - 0.012, side * hw + 0.012, 0.10, h - 0.10, -0.032, 0.032, 0.006, it)]
            else:
                hole = (-hw + 0.07, hw - 0.07, 0.92, h - 0.14)
                bits = [mk.through_box((-hw, hw, 0.0, h, -0.02, 0.02), [hole], 0.012, step=0.3, mat=self.m("Body")),
                        frame(hole[0], hole[1], hole[2], hole[3], 0.04, -0.034, 0.034, 0.012, tr),
                        self.glass(hole[0], hole[1], hole[2], hole[3], 0.012)]
                gx = side * (hw - 0.06)
                for face in (1, -1):
                    bits.append(tube((gx, 0.62, face * 0.065), (gx, 0.86, face * 0.065), 0.02, it))
                    bits += [box(gx - 0.017, gx + 0.017, y - 0.017, y + 0.017, min(face * 0.065, 0), max(face * 0.065, 0), 0.006, it)
                             for y in (0.64, 0.84)]
            leaf = mk.merge(*bits).transform(R=mk.rot_y(math.pi / 2))
            self.add(f"EndDoorLeaf_{nm}_{tag}", leaf, (xw, y0, ZC - side * w / 2))

    def side_ladder(self, x, width=0.40):
        """Gele dakladder op de perronkant: recht omhoog langs de wand en met de welving mee over de dakschouder tot op
        het loopdek. Begint boven perronhoogte, zodat hij langs het perron past."""
        it = self.m("Interact")
        zo = 0.10
        curve = [(roof_y(u) + 0.07, ZC + u) for u in (1.27, 1.14, 1.00, 0.86, ROOF_FLAT)]
        out = []
        for k, dx in enumerate((-width / 2, width / 2)):
            pts = [(x + dx, 0.30, zo), (x + dx, ROOF_BASE - 0.06, zo)] + [(x + dx, y, z) for (y, z) in curve]
            pts.append((x + dx, ROOF_TOP - 0.02, ZC + ROOF_FLAT - 0.10))
            out += pipe_run(pts, 0.028 - 0.0004 * k, it)
        ys = [0.44 + 0.27 * k for k in range(6)]
        for y in ys:
            out.append(tube((x - width / 2, y, zo), (x + width / 2, y, zo), 0.022, it))
        for (y, z) in curve[:-1]:
            out.append(tube((x - width / 2, y, z), (x + width / 2, y, z), 0.022, it))
        for y in (0.36, 1.22, 1.94):
            for dx in (-width / 2, width / 2):
                out.append(tube((x + dx, y, zo), (x + dx, y, -0.02), 0.018, it))
        return mk.merge(*out)

    # -------------------------------------------------------------- interieur
    def floor(self, mat="Floor"):
        L = self.L
        return box(-L / 2 + WALL, L / 2 - WALL, Y0 + FLOOR - 0.01, Y0 + FLOOR + 0.02, ZC - W / 2 + WALL, ZC + W / 2 - WALL,
                   0.008, self.m(mat))

    def bench(self, x, facing, za, zb, mat="Seat", head=None):
        """Bank uit één stuk (zitting + rug), met een losse hoofdsteun erbovenop als 'head' een materiaal is."""
        dy = 0.002 if facing < 0 else 0.0
        prof = [(-0.12, Y0 + FLOOR + dy), (0.46, Y0 + FLOOR + dy), (0.46, 0.50 + dy), (0.04, 0.50 + dy), (0.0, 1.04 + dy),
                (-0.12, 1.04 + dy)]
        m = mk.prism(mk.chamfer_poly(prof, 0.035), za + 2 * dy, zb - 2 * dy, 0.03, mat=self.m(mat))
        if head:
            m = mk.merge(m, box(-0.135, 0.015, 1.04 + dy, 1.24 + dy, za + 0.05, zb - 0.05, 0.035, self.m(head)))
        if facing < 0:
            m = m.transform(s=(-1, 1, 1))
        return m.transform(t=(x, 0, 0))

    def benches(self, seats, mat="Seat", head=None, sides=(1, -1)):
        zin0, zin1 = ZC - W / 2 + WALL, ZC + W / 2 - WALL
        out = []
        for (x, f) in seats:
            if -1 in sides:
                out.append(self.bench(x, f, zin0 + 0.02, ZC - END_DOOR - 0.05, mat, head))
            if 1 in sides:
                out.append(self.bench(x, f, ZC + END_DOOR + 0.05, zin1 - 0.02, mat, head))
        return out

    def roof_ladder(self, x, width=0.44):
        """Gele binnenladder tegen de wand aan de overkant, recht omhoog door het dakluik. Neemt binnen 20 cm in."""
        it = self.m("Interact")
        zl = -W + WALL + 0.16
        y0, y1 = Y0 + FLOOR + 0.02, ROOF_TOP + 0.10
        out = [tube((x + dx, y0, zl), (x + dx, y1, zl), 0.03 - 0.0004 * k, it) for k, dx in enumerate((-width / 2, width / 2))]
        out += [tube((x - width / 2, 0.38 + 0.26 * k, zl), (x + width / 2, 0.38 + 0.26 * k, zl), 0.024, it) for k in range(9)]
        for y in (0.50, 1.30, 2.02):
            for dx in (-width / 2, width / 2):
                out.append(tube((x + dx, y + 0.012, zl), (x + dx, y + 0.012, -W + WALL - 0.02), 0.018, it))
        return mk.merge(*out)

    def ladder_hole(self, x):
        """Dakluik (x0, x1, z0, z1) boven `roof_ladder`."""
        return (x - 0.38, x + 0.38, -W + 0.20, -W + 0.96)

    # ------------------------------------------------------- onderhoudspunten
    def bolt(self):
        out = BOLT_OUT
        b = mk.merge(mk.cylinder(0.07, 0.06, 6, 0.012, mat=self.m("Interact"), axis="y").transform(t=(0, 0.03, 0)),
                     mk.cylinder(0.03, out + 0.02, 8, 0.0, mat=self.m("Metal"), axis="y").transform(
                         t=(0, -(out + 0.02) / 2 + 0.001, 0)))
        b.sharp = True
        return b

    def repair(self, key, pos, fixed, broken, bolt_at, normal):
        """Onderhoudspunt. fixed/broken: meshes t.o.v. pos; bolt_at: plek van de boutkop (helemaal erin) t.o.v. pos;
        normal: richting waarin de bout uitsteekt."""
        node = f"RepairPoint_{key}"
        self.add(node, None, pos)
        self.add(f"Fixed_Rail__{key}", fixed, (0, 0, 0), node)
        self.add(f"Broken_Bits__{key}", broken, (0, 0, 0), node, visible=False)
        n = np.asarray(normal, float)
        home = np.asarray(bolt_at, float)
        self.add(f"Repair_Bolt__{key}", self.bolt(), tuple(home + n * BOLT_OUT), node, rotation=rot_to(normal))
        self.add(f"Repair_Bolt_Home__{key}", None, tuple(home), node)
        self.repairs.append(key)

    def puffs(self, pts, mat="Smoke"):
        """Wolkjes (rook/stoom) bij een kapot onderdeel: zachte bollen, geen facetten."""
        out = []
        for (x, y, z, r) in pts:
            e = mk.ellipsoid(r, r * 0.85, r, 8, 5, mat=self.m(mat)).transform(t=(x, y, z))
            out.append(e)
        return out

    def fan(self, key, x, z, r=0.42):
        """Dakventilator: vaste achthoekige behuizing; heel = vier bladen, kapot = scheve bladen + roet."""
        tr, me = self.m("Trim"), self.m("Metal")
        house = mk.lathe([(r - 0.10, 0.0), (r, 0.0), (r, 0.13), (r - 0.05, 0.17), (r - 0.10, 0.17), (r - 0.10, 0.05)],
                         8, mat=tr, closed=True, phase=math.pi / 8)
        pit = mk.cylinder(r - 0.09, 0.04, 8, 0.0, mat=self.m("Wheel"), axis="y").transform(t=(0, 0.03, 0))
        house = mk.merge(house, pit).transform(t=(x, ROOF_TOP - 0.01, z))

        def blades(tilt, missing=()):
            out = [mk.cylinder(0.09, 0.07, 8, 0.015, mat=tr, axis="y").transform(t=(0, 0.115, 0))]
            for k in range(4):
                if k in missing:
                    continue
                b = box(0.07, r - 0.13, 0.085, 0.115, -0.065, 0.065, 0.012, me)
                out.append(b.transform(R=mk.rot_y(k * math.pi / 2 + 0.3) @ mk.rot_x(0.25)))
            m = mk.merge(*out)
            return m.transform(R=mk.rot_z(tilt)) if tilt else m
        broken = mk.merge(blades(0.20, missing=(2,)),
                          box(0.10, r - 0.16, 0.20, 0.23, -0.06, 0.06, 0.01, me).transform(R=mk.rot_y(0.9) @ mk.rot_z(0.5)),
                          *self.puffs([(0.05, 0.36, 0.02, 0.17), (-0.10, 0.60, -0.06, 0.22), (0.08, 0.88, 0.05, 0.27)]))
        self.repair(key, (x, ROOF_TOP - 0.01, z), blades(0.0), broken, (r + 0.11, 0.022, 0.0), (0, 1, 0))
        foot = box(x + r - 0.02, x + r + 0.20, ROOF_TOP - 0.02, ROOF_TOP + 0.012, z - 0.10, z + 0.10, 0.008, tr)
        return mk.merge(house, foot)

    def side_panel(self, key, x, y, w=0.80, h=0.62, far=False, mat="Panel"):
        """Motorluik in de zijwand: vast donker vak met leidingen; heel = luik ervoor, kapot = luik hangt scheef."""
        tr, me = self.m("Trim"), self.m("Metal")
        zs = -1.0 if far else 1.0
        zw = -W if far else 0.0

        def side(m):
            return m.transform(s=(1, 1, -1)) if far else m
        bay = mk.merge(box(-w / 2, w / 2, -h / 2, h / 2, -0.02, 0.016, 0.015, self.m("Wheel")),
                       *pipe_run([(-w / 2 + 0.10, -h * 0.26, 0.03), (-0.05, -h * 0.26, 0.03), (-0.05, h * 0.28, 0.03)],
                                 0.028, me),
                       mk.cylinder(min(0.09, h * 0.3), 0.05, 8, 0.01, mat=me, axis="z").transform(t=(w / 2 - 0.20, 0.0, 0.035)))
        lid = mk.merge(box(-w / 2 - 0.04, w / 2 + 0.04, -h / 2 - 0.04, h / 2 + 0.04, 0.055, 0.085, 0.02, self.m(mat)),
                       *[box(-w / 2 + 0.08, w / 2 - 0.08, yy - h * 0.07, yy + h * 0.07, 0.08, 0.098, 0.008, tr)
                         for yy in (-h * 0.28, 0.0, h * 0.28)])
        hang = lid.transform(R=mk.rot_z(-0.42) @ mk.rot_x(0.16), t=(0.10, -0.20, 0.03))
        broken = mk.merge(hang, *self.puffs([(-0.08, h / 2 + 0.16, 0.20, 0.15), (0.06, h / 2 + 0.42, 0.24, 0.20)]))
        self.repair(key, (x, y, zw), side(lid), side(broken), (w / 2 + 0.16, h / 2 - 0.08, zs * 0.03), (0, 0, zs))
        plate = box(w / 2 + 0.07, w / 2 + 0.25, h / 2 - 0.17, h / 2 + 0.01, -0.02, 0.03, 0.012, tr)
        return mk.merge(side(bay), side(plate)).transform(t=(x, y, zw))

    def axle_box(self, key, x, far=False):
        """Aspot op het draaistel: heel = deksel, kapot = deksel scheef + gloeiende (warmgelopen) aspot."""
        zs = -1.0 if far else 1.0
        z = (ZC - W / 2 - 0.18) if far else (ZC + W / 2 + 0.18)
        cover = box(-0.15, 0.15, -0.13, 0.13, -0.035, 0.035, 0.03, self.m("Trim"))
        hot = mk.cylinder(0.11, 0.05, 8, 0.01, mat=self.m("Hot"), axis="z")
        hot.sharp = True
        broken = mk.merge(hot, cover.transform(R=mk.rot_z(0.6), t=(0.16, -0.20, zs * 0.03)),
                          *self.puffs([(0.0, 0.24, zs * 0.08, 0.12), (0.06, 0.46, zs * 0.10, 0.16)]))
        self.repair(key, (x, -0.23, z), cover, broken, (0.0, 0.13, 0.0), (0, 1, 0))

    def pantograph(self, key, x, z=None):
        """Stroomafnemer: vaste voet met isolatoren; heel = uitgeklapte schaar met sleepstuk, kapot = ingezakt en scheef."""
        z = ZC if z is None else z
        tr, me, it = self.m("Trim"), self.m("Metal"), self.m("Interact")
        base = [box(-0.62, 0.62, 0.0, 0.07, -0.40, 0.40, 0.02, tr)]
        for (bx, bz) in ((-0.45, -0.28), (-0.45, 0.28), (0.45, -0.28), (0.45, 0.28)):
            base.append(mk.cylinder(0.07, 0.13, 8, 0.02, mat=self.m("Steam"), axis="y").transform(t=(bx, 0.13, bz)))
        base.append(box(-0.52, 0.52, 0.19, 0.25, -0.34, 0.34, 0.02, me))

        def arms(lift, lean):
            out = []
            yk, yt = 0.25 + lift * 0.42, 0.25 + lift * 0.86
            for dz in (-0.26, 0.26):
                out += pipe_run([(-0.40, 0.24, dz), (0.34, yk, dz), (-0.05, yt, dz)], 0.028, me)
            out.append(tube((0.34, yk, -0.26), (0.34, yk, 0.26), 0.022, me))
            head = mk.merge(box(-0.11, 0.01, yt, yt + 0.05, -0.62, 0.62, 0.015, tr),
                            box(-0.09, -0.01, yt + 0.045, yt + 0.075, -0.52, 0.52, 0.01, it))
            out.append(head)
            m = mk.merge(*out)
            return m.transform(R=mk.rot_x(lean)) if lean else m
        broken = mk.merge(arms(0.22, 0.20), *self.puffs([(0.10, 0.62, -0.20, 0.14), (0.0, 0.86, 0.10, 0.19)], "Smoke"))
        self.repair(key, (x, ROOF_TOP - 0.01, z), arms(1.0, 0.0), broken, (0.74, 0.032, 0.0), (0, 1, 0))
        foot = box(0.60, 0.84, -0.01, 0.022, -0.10, 0.10, 0.008, tr)
        return mk.merge(*base, foot).transform(t=(x, ROOF_TOP - 0.01, z))

    def hose(self, key, sgn, dz=0.52):
        """Remslang op de kopse kant: heel = lus naar de koppelkraan, kapot = slang hangt los en blaast stoom af."""
        ex = sgn * self.L / 2
        tr, me = self.m("Trim"), self.m("Metal")
        s = sgn
        tap = mk.merge(box(-0.06, 0.06, -0.06, 0.06, -0.06, 0.06, 0.02, me).transform(t=(s * 0.07, 0.0, 0.0)),
                       box(-0.05, 0.05, -0.05, 0.05, -0.05, 0.05, 0.015, me).transform(t=(s * 0.07, 0.0, 0.34)))
        good = mk.merge(*pipe_run([(s * 0.10, -0.03, 0.0), (s * 0.22, -0.20, 0.08), (s * 0.22, -0.20, 0.26), (s * 0.10, -0.03, 0.34)],
                                  0.034, tr))
        loose = mk.merge(*pipe_run([(s * 0.10, -0.03, 0.0), (s * 0.20, -0.24, 0.05), (s * 0.24, -0.50, 0.10)], 0.034, tr),
                         *self.puffs([(s * 0.26, 0.10, 0.34, 0.11), (s * 0.40, 0.26, 0.36, 0.15), (s * 0.58, 0.46, 0.34, 0.20)],
                                     "Steam"))
        self.repair(key, (ex, 0.40, ZC + dz), good, loose, (s * 0.07, 0.06, 0.17), (0, 1, 0))
        mount = box(-0.05, 0.05, 0.0, 0.06, -0.05, 0.05, 0.012, me).transform(t=(s * 0.07, 0.0, 0.17))
        return mk.merge(tap, mount).transform(t=(ex, 0.40, ZC + dz))

    # ----------------------------------------------------------------- neus
    def hood(self, stations, mat_fn, N=32, count=18):
        """Neusromp: gesloten vloeiende romp voor de kopwand. Sleuteldoorsneden met x t.o.v. de kopwand (L/2)."""
        st = smooth([dict(s_, x=self.L / 2 + s_["x"]) for s_ in stations], count)
        return loft(st, N=N, mat_fn=mat_fn, cap1=True)

    def dome(self, stations, mat_fn, N=24, count=14):
        """Neuskap: open schaal (bovenste helft) van dak tot neusromp; mat_fn maakt er ruit, stijlen en dakrand van."""
        st = smooth([dict(s_, x=self.L / 2 + s_["x"]) for s_ in stations], count, tip=1.2)
        return loft(st, N=N, half=True, mat_fn=mat_fn, cap1=True)

    def ring_bands(self, stations, limits, top, N=32):
        """Kleurbanden op een neusromp die de vorm volgen: de band wordt gekozen op de hoogte van een ringsegment bij
        de kopwand (waar hij aansluit op de banden van de bak) en loopt dan langs de romp mee naar de punt. Zo vallen
        de randen op de lijnen van het model en niet dwars door de vlakken. limits: [(tot hoogte, materiaal)]."""
        base = _ring(dict(stations[0]), N, False)
        mid = [(base[j][1] + base[(j + 1) % N][1]) / 2 for j in range(N)]
        mats = []
        for y in mid:
            mats.append(next((self.m(m) for (lim, m) in limits if y < lim), self.m(top)))
        return mats

    def surface_x(self, stations, y, u):
        """x (t.o.v. de kopwand) waar een punt op hoogte y en afstand u uit het midden door de neusromp naar buiten
        komt: daar zet je een koplamp."""
        for st in smooth(stations, 60, tip=1.0):
            yc, b = (st["yb"] + st["yt"]) / 2, (st["yt"] - st["yb"]) / 2
            if b <= 1e-6 or st["a"] <= 1e-6 or (abs(u) / st["a"]) ** st["n"] + (abs(y - yc) / b) ** st["n"] > 1.0:
                return st["x"]
        return stations[-1]["x"]

    def dome_mat(self, brow=2, rim=3, pillar=False, glass="GlassDark", N=24):
        """Indeling van de neuskap langs de lijnen van het model: de eerste `brow` ringen zijn dakrand, de onderste
        `rim` segmenten aan weerszijden bakkleur (stijlen), de rest donkere ruit; pillar = middenstijl."""
        def fn(i, j, p):
            if i < brow:
                return self.m("Roof") if rim + 2 <= j < N - rim - 2 else self.m("Body")
            if j < rim or j >= N - rim or (pillar and j in (N // 2 - 1, N // 2)):
                return self.m("Body")
            return self.m(glass)
        return fn

    def console(self, style, xw=None):
        """Bestuurderslessenaar naar de cabinebladen ("60s", "2020" of "2040"); zie train_consoles.py."""
        import train_consoles
        train_consoles.add_console(self, style, xw)

    def desk(self, x, y, zw=1.5, mat="Desk"):
        """Lessenaar in de cabine: schuin blad uit één stuk met gashendel (ThrottleLever) en lampjes."""
        prof = [(-0.42, y - 0.50), (0.0, y - 0.50), (0.0, y + 0.02), (-0.14, y + 0.10), (-0.42, y - 0.04)]
        d = mk.prism(mk.chamfer_poly(prof, 0.03), ZC - zw / 2, ZC + zw / 2, 0.03, mat=self.m(mat)).transform(t=(x, 0, 0))
        gauges = []
        for dz in (-0.36, 0.0, 0.36):
            g = mk.cylinder(0.085, 0.03, 8, 0.008, mat=self.m("Lamp"), axis="y")
            g.sharp = True
            gauges.append(g.transform(R=mk.rot_z(-0.46), t=(x - 0.26, y + 0.052, ZC + dz)))
        self.add("CabDesk", mk.merge(d, *gauges))
        lever = mk.merge(tube((0, 0, 0), (0, 0.24, 0), 0.024, self.m("Metal")),
                         mk.ellipsoid(0.055, 0.055, 0.055, 8, 5, mat=self.m("Interact")).transform(t=(0, 0.27, 0)))
        self.add("ThrottleLever", lever, (x - 0.22, y + 0.03, ZC + 0.58), rotation=mk.rot_z(0.35))

    def cab_seat(self, x, mat="Interact"):
        prof = [(-0.30, Y0 + FLOOR), (0.22, Y0 + FLOOR), (0.22, 0.56), (-0.14, 0.56), (-0.18, 1.12), (-0.30, 1.12)]
        return mk.prism(mk.chamfer_poly(prof, 0.035), ZC - 0.32, ZC + 0.32, 0.03, mat=self.m(mat)).transform(t=(x, 0, 0))

    # -------------------------------------------------------------- afwerking
    def couplers(self, rear, front):
        add_couplers(self.sc, self.root, RAIL_Y, ZC, rear, front, self.m("Trim"), self.m("Interact"))

    def finish(self, density=900):
        sc = self.sc
        for p in sc.parts:
            p.gradient = 0.25
        sc.facet_all()
        sc.style_all(grime=1.0, **mk.TRAIN_CELLS)
        sc.bake_shading(density=density, radius=0.30, noise_freq=1.1, edge_range=(3.0, 10.0))
        return sc

    def export(self, folder, mat_folder):
        """FBX + .mat/.meta in Assets/Art, GLB + materials.json in ArtSource/Exports. De voertuigen van één set delen
        een materiaalmap (zelfde prefix, zelfde materialen)."""
        sc = self.sc
        rel = f"Assets/Art/Models/Train/{folder}/{self.name}.fbx"
        exp_dir = os.path.join(ROOT, "ArtSource", "Exports")
        os.makedirs(os.path.join(ROOT, os.path.dirname(rel)), exist_ok=True)
        sc.write_fbx_ascii(os.path.join(ROOT, rel))
        sc.write_glb(os.path.join(exp_dir, self.name + ".glb"))
        unity_assets.export_unity(ROOT, sc, rel, f"Assets/Art/Materials/Train/{mat_folder}")
        mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"],
                    "opts": v["opts"]} for k, v in sc.materials.items()}
        json.dump({"model": self.name, "stats": sc.stats(), "repairPoints": self.repairs, "materials": mats},
                  open(os.path.join(exp_dir, self.name + ".materials.json"), "w", encoding="utf-8"), indent=2)
        print(self.name, sc.stats(), "repair:", ",".join(self.repairs))
