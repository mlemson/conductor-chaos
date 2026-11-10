"""Gedeelde bouwstenen voor het stationnetje en de perronkit (issue #182).

* Materialen: één set M_Stn_* voor alle stationsassets (kleuren uit palette.json, groep 'Station & perron'
  plus parchment/accent_yellow/train_red/headlight/grass/pine/crate).
* Maatvoering die het spoor en de treinen volgen (zie ook ArtSource/Environment/build_track_kit.py):
    PLATFORM_TOP = 1,05 m  (= bodem van de wagonbak; de wagonvloer ligt 0,10 hoger)
    PLATFORM_EDGE = 1,65 m uit het hart van het spoor (loc steekt onder perronhoogte 1,58 m uit)
* Vormen: afgeronde blokken (rbox), platen met (boog)gaten (slab), buizen (tube), letters uit
  afgeronde streken (geen fontbestand nodig) en het treinpictogram uit het concept.
* Export: FBX + GLB + Unity-materialen/meta, met Col_*-botsvormen (verborgen; de prefabbouwer in Unity
  maakt er MeshColliders van) en een manifest StationKit.json.
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import meshkit as mk  # noqa: E402
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
R = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

MAT_DIR = "Assets/Art/Materials/Environment/Station"
MODEL_DIR = "Assets/Art/Models/Environment/Station"
EXP_DIR = os.path.join(ROOT, "ArtSource", "Exports", "Environment", "Station")

PLATFORM_TOP = 1.05
PLATFORM_EDGE = 1.65
DEG = math.pi / 180.0

WOOD = {"cells": 0.6, "cellSize": 0.5, "grime": 0.35}
STONE = {"cells": 0.8, "cellSize": 0.7, "grime": 0.5}
PAINT = {"cells": 0.5, "cellSize": 0.8, "grime": 0.25}

# naam: (ramp, smoothness, ramp_shift, opts)
MATERIALS = {
    "M_Stn_Roof": (R["station_navy"], 0.10, 0.00, {"cells": 0.7, "cellSize": 0.9, "rim": 0.25}),
    "M_Stn_RoofDark": (R["station_navy"], 0.10, -0.25, {"cells": 0.6, "cellSize": 0.9}),
    "M_Stn_Navy": (R["station_navy"], 0.20, -0.05, PAINT),
    "M_Stn_NavyLight": (R["station_navy"], 0.20, 0.30, PAINT),
    "M_Stn_Plaster": (R["parchment"], 0.05, 0.10, {"cells": 0.5, "cellSize": 1.0, "grime": 0.45, "paint": 1.2}),
    "M_Stn_PlasterIn": (R["parchment"], 0.05, -0.05, {"cells": 0.4, "cellSize": 1.0, "grime": 0.2}),
    "M_Stn_Timber": (R["station_timber"], 0.10, 0.00, WOOD),
    "M_Stn_TimberDark": (R["station_timber"], 0.10, -0.35, WOOD),
    "M_Stn_WoodLight": (R["crate"], 0.10, 0.05, WOOD),
    "M_Stn_Floor": (R["interior_wood"], 0.10, 0.10, {"cells": 0.6, "cellSize": 0.6, "grime": 0.3}),
    "M_Stn_FloorAlt": (R["interior_wood"], 0.10, -0.05, {"cells": 0.6, "cellSize": 0.6, "grime": 0.3}),
    "M_Stn_Stone": (R["plinth_stone"], 0.05, 0.00, STONE),
    "M_Stn_StoneDark": (R["plinth_stone"], 0.05, -0.30, STONE),
    "M_Stn_Paving": (R["platform_stone"], 0.05, 0.05, {"cells": 0.9, "cellSize": 0.8, "grime": 0.4}),
    "M_Stn_PavingDark": (R["platform_stone"], 0.05, -0.18, {"cells": 0.8, "cellSize": 0.8, "grime": 0.6}),
    "M_Stn_PavingLight": (R["platform_stone"], 0.05, 0.25, {"cells": 0.8, "cellSize": 0.8, "grime": 0.3}),
    "M_Stn_Yellow": (R["accent_yellow"], 0.25, 0.00, {"cells": 0.4, "cellSize": 0.6}),
    "M_Stn_YellowDark": (R["accent_yellow"], 0.25, -0.35, {"cells": 0.4, "cellSize": 0.6}),
    "M_Stn_Cream": (R["parchment"], 0.20, 0.35, {"cells": 0.2}),
    "M_Stn_Red": (R["train_red"], 0.25, 0.05, {"cells": 0.4, "cellSize": 0.6}),
    "M_Stn_White": (R["cloud_snow"], 0.20, 0.10, {"cells": 0.3, "cellSize": 0.6}),
    "M_Stn_Metal": (R["metal"], 0.35, -0.10, {"cells": 0.3}),
    "M_Stn_Dark": (R["ui_bg"], 0.25, 0.10, {"cells": 0.0}),
    "M_Stn_Glow": (R["headlight"], 0.50, 0.15, {"emissive": 0.85, "rim": 0.0, "paint": 0.0, "cells": 0.0}),
    "M_Stn_Window": (R["headlight"], 0.60, -0.20, {"emissive": 0.35, "opacity": 0.72, "flat": 0.5, "cells": 0.0,
                                                    "grime": 0.0}),
    "M_Stn_SignalRed": (R["train_red"], 0.40, 0.45, {"emissive": 0.55, "cells": 0.0}),
    "M_Stn_Leaf": (R["grass"], 0.05, -0.05, {"cells": 0.6, "cellSize": 0.4, "rim": 0.25}),
    "M_Stn_Soil": (R["cab_interior"], 0.05, -0.10, {"cells": 0.5}),
    "M_Stn_Collider": (R["ui_bg"], 0.0, 0.0, {"cells": 0.0}),
    # hoofdstation met briefingruimte (issue #207): pannendak, routebord en kluisjes, gemeten in het concept
    "M_Stn_RoofRed": (R["roof_terracotta"], 0.10, 0.00, {"cells": 0.7, "cellSize": 0.9, "rim": 0.25}),
    "M_Stn_RoofRedDark": (R["roof_terracotta"], 0.10, -0.30, {"cells": 0.6, "cellSize": 0.9}),
    "M_Stn_Chalk": (R["chalk_green"], 0.05, 0.00, {"cells": 0.5, "cellSize": 0.8, "grime": 0.2}),
    "M_Stn_ChalkDark": (R["chalk_green"], 0.05, -0.35, {"cells": 0.3}),
    "M_Stn_Locker": (R["locker_blue"], 0.25, 0.05, PAINT),
    "M_Stn_LockerDark": (R["locker_blue"], 0.25, -0.35, PAINT),
    # boerderijdecor (farm_props.py): stro, schuurrood en water, uit bestaande ramps
    "M_Stn_Straw": (R["crate"], 0.05, 0.30, {"cells": 0.6, "cellSize": 0.35, "paint": 1.2}),
    "M_Stn_StrawDark": (R["crate"], 0.05, 0.00, {"cells": 0.6, "cellSize": 0.35, "grime": 0.3}),
    "M_Stn_BarnRed": (R["train_red"], 0.10, -0.15, WOOD),
    "M_Stn_BarnRedDark": (R["train_red"], 0.10, -0.50, WOOD),
    "M_Stn_Water": (R["water"], 0.50, 0.10, {"cells": 0.3, "cellSize": 0.6, "grime": 0.0}),
    "M_Stn_Wheat": (R["accent_yellow"], 0.05, 0.05, {"cells": 0.5, "cellSize": 0.3}),
    "M_Stn_WheatDark": (R["accent_yellow"], 0.05, -0.40, {"cells": 0.3, "cellSize": 0.3}),
    "M_Stn_FieldSoil": (R["interior_wood"], 0.05, -0.05, {"cells": 0.6, "cellSize": 0.5, "grime": 0.2}),
    "M_Stn_Dirt": (R["track_wood"], 0.05, -0.15, {"cells": 0.6, "cellSize": 0.6, "grime": 0.2}),
    "M_Stn_Sack": (R["safari_hat"], 0.05, 0.15, {"cells": 0.5, "cellSize": 0.3}),
    "M_Stn_SackDark": (R["safari_hat"], 0.05, -0.50, {"cells": 0.2}),
}


# ------------------------------------------------------------------ basisvormen
def rbox(x0, x1, y0, y1, z0, z1, r=0.04, mat="M_Stn_Timber", div=1):
    """Blok met afgeschuinde randen (één facet per rand, 28 driehoeken); div > 1 = afgerond (duurder)."""
    sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
    r = max(0.0, min(r, sx / 2.05, sy / 2.05, sz / 2.05))
    if r <= 1e-4:
        return _plain_box(sx, sy, sz, mat).transform(t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    if div > 1:
        return mk.rounded_box(sx, sy, sz, r, div, mat=mat).transform(t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    return slab([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], (), z0, z1, r, mat)


def _plain_box(sx, sy, sz, mat):
    V = np.array([(x, y, z) for x in (-sx / 2, sx / 2) for y in (-sy / 2, sy / 2) for z in (-sz / 2, sz / 2)])
    F = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    m = mk.Mesh(V, F, [mat] * 6)
    m.sharp = True
    return mk._orient_closed(m)


def col_box(x0, x1, y0, y1, z0, z1):
    return rbox(x0, x1, y0, y1, z0, z1, 0.0, "M_Stn_Collider")


def cyl(r, length, seg=16, bevel=0.0, mat="M_Stn_Timber", axis="y"):
    return mk.cylinder(r, length, seg, bevel, mat=mat, axis=axis)


def capsule(p0, p1, r0, r1=None, seg=12, rings=3, mat="M_Stn_Timber"):
    return mk.capsule_between(p0, p1, r0, r0 if r1 is None else r1, seg, rings, mat=mat)


def ball(r, mat, seg=18, rings=10, sy=1.0):
    return mk.ellipsoid(r, r * sy, r, seg, rings, mat=mat)


# ------------------------------------------------------- polygonen met gaten
def _area(P):
    P = np.asarray(P, float)
    return 0.5 * np.sum(P[:, 0] * np.roll(P[:, 1], -1) - np.roll(P[:, 0], -1) * P[:, 1])


def _ccw(P):
    P = [tuple(map(float, p)) for p in P]
    return P if _area(P) > 0 else P[::-1]


def _cross2(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def _proper_cross(p, q, a, b, eps=1e-9):
    d1, d2 = _cross2(p, q, a), _cross2(p, q, b)
    d3, d4 = _cross2(a, b, p), _cross2(a, b, q)
    return ((d1 > eps and d2 < -eps) or (d1 < -eps and d2 > eps)) and \
           ((d3 > eps and d4 < -eps) or (d3 < -eps and d4 > eps))


def _bridge(outer, holes):
    """Gaten via 'bruggen' in de buitenrand opnemen -> één (zwak) eenvoudige polygoon voor earclip."""
    poly = list(outer)
    holes = [list(h) for h in holes]
    order = sorted(range(len(holes)), key=lambda i: -max(p[0] for p in holes[i]))
    for hi in order:
        h = holes[hi]
        mi = int(np.argmax([p[0] for p in h]))
        M = h[mi]
        edges = [(poly[k], poly[(k + 1) % len(poly)]) for k in range(len(poly))]
        for other in holes:
            edges += [(other[k], other[(k + 1) % len(other)]) for k in range(len(other))]
        best = None
        for j in sorted(range(len(poly)), key=lambda j: (poly[j][0] - M[0]) ** 2 + (poly[j][1] - M[1]) ** 2):
            P = poly[j]
            if all(not _proper_cross(M, P, a, b) for a, b in edges):
                best = j
                break
        j = best if best is not None else 0
        poly = poly[:j + 1] + h[mi:] + h[:mi + 1] + poly[j:]
    return poly


def _earclip(P):
    n = len(P)
    idx = list(range(n))
    tris = []

    def same(a, b):
        return abs(a[0] - b[0]) < 1e-9 and abs(a[1] - b[1]) < 1e-9

    def inside(p, a, b, c):
        if same(p, a) or same(p, b) or same(p, c):
            return False
        d1, d2, d3 = _cross2(a, b, p), _cross2(b, c, p), _cross2(c, a, p)
        return d1 > -1e-12 and d2 > -1e-12 and d3 > -1e-12
    guard = 0
    while len(idx) > 3 and guard < 20 * n * n:
        guard += 1
        found = False
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[(k)], idx[(k + 1) % len(idx)]
            a, b, c = P[i0], P[i1], P[i2]
            cr = _cross2(a, b, c)
            if cr <= 1e-12:
                continue
            if any(inside(P[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            found = True
            break
        if not found:                       # gedegenereerd (collineair): knip het kleinste hoekje weg
            k = min(range(len(idx)), key=lambda k: abs(_cross2(P[idx[k - 1]], P[idx[k]], P[idx[(k + 1) % len(idx)]])))
            idx.pop(k)
    if len(idx) == 3:
        tris.append(tuple(idx))
    return tris


def _offset_loop(P, d):
    """Verschuif een lus d naar links (CCW buitenrand: naar binnen; CW gat: het materiaal in)."""
    P = np.asarray(P, float)
    n = len(P)
    out = []
    for i in range(n):
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]
        e1 = (b - a) / max(np.linalg.norm(b - a), 1e-12)
        e2 = (c - b) / max(np.linalg.norm(c - b), 1e-12)
        n1 = np.array([-e1[1], e1[0]])
        n2 = np.array([-e2[1], e2[0]])
        m = n1 + n2
        nm = np.linalg.norm(m)
        m = n1 if nm < 1e-9 else m / nm
        out.append(b + m * d / max(np.dot(m, n1), 0.35))
    return out


def clean_loop(P, eps=1e-4):
    out = []
    for p in P:
        if not out or math.hypot(p[0] - out[-1][0], p[1] - out[-1][1]) > eps:
            out.append((float(p[0]), float(p[1])))
    if len(out) > 1 and math.hypot(out[0][0] - out[-1][0], out[0][1] - out[-1][1]) <= eps:
        out.pop()
    return out


def slab(outer, holes=(), z0=0.0, z1=0.1, c=0.02, mat="M_Stn_Plaster"):
    """Polygoon (XY) met gaten, geëxtrudeerd langs Z met afgeschuinde randen (ook rond de gaten). Eén gesloten stuk."""
    outer = _ccw(clean_loop(outer))
    holes = [_ccw(clean_loop(h))[::-1] for h in holes]          # gaten CW
    loops = [outer] + holes
    V, F = [], []
    cap0, cap1 = [], []
    for L in loops:
        n = len(L)
        Li = _offset_loop(L, c) if c > 0 else L
        rings = [(Li, z0), (L, z0 + c), (L, z1 - c), (Li, z1)] if c > 0 else [(L, z0), (L, z1)]
        base = len(V)
        for (Q, z) in rings:
            V += [(q[0], q[1], z) for q in Q]
        for r in range(len(rings) - 1):
            for i in range(n):
                j = (i + 1) % n
                F.append((base + r * n + i, base + r * n + j, base + (r + 1) * n + j, base + (r + 1) * n + i))
        cap0.append([(tuple(Li[i]), base + i) for i in range(n)])
        cap1.append([(tuple(Li[i]), base + (len(rings) - 1) * n + i) for i in range(n)])
    for caps, top in ((cap0, False), (cap1, True)):
        poly = _bridge([p for p, _ in caps[0]], [[p for p, _ in h] for h in caps[1:]])
        lookup = {}
        for loop in caps:
            for p, vi in loop:
                lookup[(round(p[0], 9), round(p[1], 9))] = vi
        ids = [lookup[(round(p[0], 9), round(p[1], 9))] for p in poly]
        for (a, b, cc) in _earclip(poly):
            if top:
                F.append((ids[a], ids[b], ids[cc]))
            else:
                F.append((ids[a], ids[cc], ids[b]))
    m = mk.Mesh(np.array(V, float), F, [mat] * len(F))
    m.sharp = True
    return mk._orient_closed(m)


def arch_loop(x0, x1, y0, y_spring, seg=12, flat_bottom=True):
    """Rechthoek met halfronde bovenkant (boog vanaf y_spring), CCW."""
    r = (x1 - x0) / 2
    cx = (x0 + x1) / 2
    pts = [(x0, y0), (x1, y0)]
    for k in range(seg + 1):
        a = math.pi * k / seg
        pts.append((cx + r * math.cos(a), y_spring + r * math.sin(a)))
    return pts


def arch_band(xa, xb, y0, y_spring, w, seg=12):
    """Kozijn rond een boogdeur: U-vormige band (open aan de onderkant), CCW."""
    r_in = (xb - xa) / 2
    cx = (xa + xb) / 2
    r_out = r_in + w
    pts = [(xa - w, y0), (xa, y0)]
    for k in range(seg + 1):                                   # binnenboog links -> rechts (met de klok mee)
        a = math.pi - math.pi * k / seg
        pts.append((cx + r_in * math.cos(a), y_spring + r_in * math.sin(a)))
    pts += [(xb, y0), (xb + w, y0)]
    for k in range(seg + 1):                                   # buitenboog rechts -> links
        a = math.pi * k / seg
        pts.append((cx + r_out * math.cos(a), y_spring + r_out * math.sin(a)))
    return pts


def wall_outline(x0, x1, y0, y1, doors=(), ridge=None, seg=12):
    """Muur-omtrek (CCW) met boogdeuren als uitsparing in de onderrand en optioneel een topgevel.
    doors: [(xa, xb, y_spring)], ridge: hoogte van de nok (midden) of None."""
    pts = [(x0, y0)]
    for (xa, xb, ys) in sorted(doors):
        r = (xb - xa) / 2
        cx = (xa + xb) / 2
        pts += [(xa, y0)]
        for k in range(seg + 1):
            a = math.pi - math.pi * k / seg
            pts.append((cx + r * math.cos(a), ys + r * math.sin(a)))
        pts += [(xb, y0)]
    pts += [(x1, y0), (x1, y1)]
    if ridge is not None:
        pts.append(((x0 + x1) / 2, ridge))
    pts.append((x0, y1))
    return pts


def circle_loop(cx, cy, r, seg=20, start=0.0):
    return [(cx + r * math.cos(start + 2 * math.pi * k / seg), cy + r * math.sin(start + 2 * math.pi * k / seg))
            for k in range(seg)]


def rounded_rect_loop(x0, x1, y0, y1, r, seg=4):
    pts = []
    for (cx, cy, a0) in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def disc(r, z0, z1, mat, seg=24, c=0.01):
    return slab(circle_loop(0, 0, r, seg), (), z0, z1, c, mat)


# ------------------------------------------------------------------ buizen
def smooth_path(points, n=40):
    P = np.asarray(points, float)
    for _ in range(3):
        Q = [P[0]]
        for a, b in zip(P[:-1], P[1:]):
            Q += [0.75 * a + 0.25 * b, 0.25 * a + 0.75 * b]
        Q.append(P[-1])
        P = np.array(Q)
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]
    t = np.linspace(0, d[-1], n)
    return np.stack([np.interp(t, d, P[:, k]) for k in range(3)], 1)


def tube(points, radius, mat, n=None, seg=10, smooth=False, closed=False, caps=True, phase=0.0):
    """Ronde buis langs een pad (parallel transport), met bolle eindkappen. phase draait de doorsnede."""
    P = np.asarray(points, float)
    if smooth:
        P = smooth_path(P, n or max(12, 6 * len(P)))
    if closed:
        T = np.roll(P, -1, axis=0) - np.roll(P, 1, axis=0)
    else:
        T = np.gradient(P, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True)
    ref = np.array([0, 0, 1.0]) if abs(T[0][2]) < 0.9 else np.array([1.0, 0, 0])
    B = [np.cross(ref, T[0])]
    B[0] /= np.linalg.norm(B[0])
    for i in range(1, len(P)):
        b = B[-1] - np.dot(B[-1], T[i]) * T[i]
        if np.linalg.norm(b) < 1e-6:                 # scherpe keerbocht: kies een nieuwe loodlijn
            b = np.cross(ref, T[i])
        B.append(b / np.linalg.norm(b))
    B = np.array(B)
    U = np.cross(T, B)
    sec = [(math.cos(2 * math.pi * k / seg + phase), math.sin(2 * math.pi * k / seg + phase)) for k in range(seg)]
    n_, k_ = len(P), seg
    V = [P[i] + (B[i] * u + U[i] * v) * radius for i in range(n_) for (u, v) in sec]
    F = []
    for i in range(n_ if closed else n_ - 1):
        i2 = (i + 1) % n_
        for j in range(k_):
            j2 = (j + 1) % k_
            F.append((i * k_ + j, i * k_ + j2, i2 * k_ + j2, i2 * k_ + j))
    cap_ends = (caps, caps) if isinstance(caps, bool) else tuple(caps)     # (begin, eind)
    if not closed:
        for end, sgn, on in ((0, -1, cap_ends[0]), (n_ - 1, 1, cap_ends[1])):
            if not on:
                continue
            prev = list(range(end * k_, end * k_ + k_))
            for q in range(1, 3):
                a = math.pi / 2 * q / 3
                ring = []
                for (u, v) in sec:
                    ring.append(len(V))
                    V.append(P[end] + (B[end] * u + U[end] * v) * radius * math.cos(a) + T[end] * sgn * radius * math.sin(a))
                for j in range(k_):
                    j2 = (j + 1) % k_
                    F.append((prev[j], prev[j2], ring[j2], ring[j]) if sgn > 0 else (prev[j], ring[j], ring[j2], prev[j2]))
                prev = ring
            tip = len(V)
            V.append(P[end] + T[end] * sgn * radius)
            for j in range(k_):
                j2 = (j + 1) % k_
                F.append((prev[j], prev[j2], tip) if sgn > 0 else (prev[j], tip, prev[j2]))
    m = mk.Mesh(np.array(V, float), F, [mat] * len(F))
    p = m.V[list(F[0])]
    if np.dot(np.cross(p[1] - p[0], p[2] - p[0]), p.mean(0) - P[0]) < 0:
        m.F = [tuple(reversed(f)) for f in m.F]
    return m


# --------------------------------------------------------- letters & iconen
def _arc(cx, cy, rx, ry, a0, a1, n=10):
    return [(cx + rx * math.cos(math.radians(a)), cy + ry * math.sin(math.radians(a)))
            for a in np.linspace(a0, a1, n)]


# Afgeronde streekletters (kaphoogte 1): lijst van (punten, gesloten). Zelf ontworpen, geen fontbestand.
GLYPHS = {
    "S": (0.64, [(_arc(0.32, 0.745, 0.27, 0.235, 25, 270, 11) + _arc(0.32, 0.265, 0.29, 0.245, 90, -150, 11), False)]),
    "T": (0.66, [([(0.0, 1.0), (0.66, 1.0)], False), ([(0.33, 1.0), (0.33, 0.0)], False)]),
    "A": (0.74, [([(0.0, 0.0), (0.37, 1.0), (0.74, 0.0)], False), ([(0.16, 0.33), (0.58, 0.33)], False)]),
    "I": (0.0, [([(0.0, 0.0), (0.0, 1.0)], False)]),
    "O": (0.78, [(_arc(0.39, 0.5, 0.39, 0.5, 0, 360, 25)[:-1], True)]),
    "N": (0.66, [([(0.0, 0.0), (0.0, 1.0), (0.66, 0.0), (0.66, 1.0)], False)]),
    # hoofdstation (issue #207): TICKETS e.a.
    "M": (0.84, [([(0.0, 0.0), (0.0, 1.0), (0.42, 0.40), (0.84, 1.0), (0.84, 0.0)], False)]),
    "E": (0.54, [([(0.54, 1.0), (0.0, 1.0), (0.0, 0.0), (0.54, 0.0)], False), ([(0.0, 0.5), (0.44, 0.5)], False)]),
    "H": (0.64, [([(0.0, 0.0), (0.0, 1.0)], False), ([(0.64, 0.0), (0.64, 1.0)], False),
                 ([(0.0, 0.5), (0.64, 0.5)], False)]),
    "U": (0.64, [([(0.0, 1.0)] + _arc(0.32, 0.32, 0.32, 0.32, 180, 360, 11) + [(0.64, 1.0)], False)]),
    "B": (0.60, [([(0.0, 0.0), (0.0, 1.0)], False),
                 ([(0.0, 1.0), (0.30, 1.0)] + _arc(0.30, 0.765, 0.235, 0.235, 90, -90, 9), False),   # eindigt op de middenstreep
                 ([(0.0, 0.53), (0.32, 0.53)] + _arc(0.32, 0.265, 0.265, 0.265, 90, -90, 9) + [(0.0, 0.0)], False)]),
    "R": (0.62, [([(0.0, 0.0), (0.0, 1.0)], False),
                 ([(0.0, 1.0), (0.32, 1.0)] + _arc(0.32, 0.75, 0.25, 0.25, 90, -90, 9) + [(0.0, 0.50)], False),
                 ([(0.26, 0.50), (0.62, 0.0)], False)]),
    "G": (0.74, [(_arc(0.39, 0.5, 0.39, 0.5, 50, 335, 15) + [(0.74, 0.44), (0.44, 0.44)], False)]),
    "C": (0.70, [(_arc(0.39, 0.5, 0.39, 0.5, 50, 310, 15), False)]),
    "K": (0.60, [([(0.0, 0.0), (0.0, 1.0)], False), ([(0.60, 1.0), (0.04, 0.40)], False),
                 ([(0.22, 0.60), (0.62, 0.0)], False)]),
    # aanpasbare stationsnamen (issue #209): rest van het alfabet, cijfers en leestekens
    "D": (0.66, [([(0.0, 0.0), (0.0, 1.0), (0.24, 1.0)] + _arc(0.24, 0.5, 0.42, 0.5, 90, -90, 13)[1:-1] + [(0.24, 0.0)], True)]),
    "F": (0.52, [([(0.52, 1.0), (0.0, 1.0), (0.0, 0.0)], False), ([(0.0, 0.52), (0.42, 0.52)], False)]),
    "J": (0.52, [([(0.52, 1.0), (0.52, 0.30)] + _arc(0.26, 0.30, 0.26, 0.30, 0, -180, 11)[1:], False)]),
    "L": (0.50, [([(0.0, 1.0), (0.0, 0.0), (0.50, 0.0)], False)]),
    "P": (0.58, [([(0.0, 0.0), (0.0, 1.0), (0.30, 1.0)] + _arc(0.30, 0.74, 0.27, 0.26, 90, -90, 9) + [(0.04, 0.48)], False)]),
    "Q": (0.78, [(_arc(0.39, 0.5, 0.39, 0.5, 0, 360, 25)[:-1], True), ([(0.50, 0.22), (0.80, -0.04)], False)]),
    "V": (0.70, [([(0.0, 1.0), (0.35, 0.0), (0.70, 1.0)], False)]),
    "W": (0.98, [([(0.0, 1.0), (0.22, 0.0), (0.49, 0.64), (0.76, 0.0), (0.98, 1.0)], False)]),
    "X": (0.64, [([(0.0, 1.0), (0.64, 0.0)], False), ([(0.0, 0.0), (0.64, 1.0)], False)]),
    "Y": (0.68, [([(0.0, 1.0), (0.34, 0.47)], False), ([(0.68, 1.0), (0.34, 0.50), (0.34, 0.0)], False)]),
    "Z": (0.62, [([(0.0, 1.0), (0.62, 1.0), (0.0, 0.0), (0.62, 0.0)], False)]),
    "0": (0.62, [(_arc(0.31, 0.5, 0.31, 0.5, 0, 360, 25)[:-1], True)]),
    "1": (0.34, [([(0.0, 0.78), (0.30, 1.0), (0.30, 0.0)], False)]),
    "2": (0.60, [(_arc(0.30, 0.70, 0.28, 0.28, 160, -40, 11) + [(0.0, 0.0), (0.60, 0.0)], False)]),
    "3": (0.58, [(_arc(0.28, 0.76, 0.25, 0.24, 155, -90, 11), False),
                 (_arc(0.28, 0.27, 0.29, 0.27, 90, -155, 11), False)]),
    "4": (0.62, [([(0.46, 0.0), (0.46, 1.0), (0.0, 0.30), (0.62, 0.30)], False)]),
    "5": (0.58, [([(0.56, 1.0), (0.08, 1.0), (0.06, 0.58)] + _arc(0.28, 0.32, 0.30, 0.32, 125, -150, 11), False)]),
    "6": (0.60, [(_arc(0.40, 0.42, 0.38, 0.58, 80, 180, 8) + _arc(0.30, 0.30, 0.28, 0.30, 180, 520, 15)[1:], False)]),
    "7": (0.58, [([(0.0, 1.0), (0.58, 1.0), (0.16, 0.0)], False)]),
    "8": (0.60, [(_arc(0.30, 0.765, 0.235, 0.235, -90, 270, 21)[:-1], True),
                 (_arc(0.30, 0.27, 0.29, 0.27, 90, 450, 23)[:-1], True)]),
    "9": (0.60, [([(0.60 - x, 1.0 - y) for x, y in _arc(0.40, 0.42, 0.38, 0.58, 80, 180, 8) +
                   _arc(0.30, 0.30, 0.28, 0.30, 180, 520, 15)[1:]], False)]),
    "-": (0.40, [([(0.0, 0.45), (0.40, 0.45)], False)]),
    "'": (0.0, [([(0.0, 1.0), (0.0, 0.74)], False)]),
    ".": (0.0, [([(0.0, 0.0), (0.0, 0.02)], False)]),
}


def _split_sharp(P, closed, max_turn=40.0):
    """Knipt een streek op scherpe hoeken (> max_turn graden) in stukken, zodat de buis daar een ronde kop krijgt
    in plaats van een spitse verstekhoek (M, W, D, P ...). Gesloten lussen zonder scherpe hoek blijven heel."""
    n = len(P)

    def sharp(i):
        a, b, c = np.array(P[(i - 1) % n]), np.array(P[i]), np.array(P[(i + 1) % n])
        u, v = b - a, c - b
        lu, lv = np.linalg.norm(u), np.linalg.norm(v)
        if lu < 1e-6 or lv < 1e-6:
            return False
        return math.degrees(math.acos(np.clip(u @ v / (lu * lv), -1, 1))) > max_turn
    if closed:
        corners = [i for i in range(n) if sharp(i)]
        if not corners:
            return [(P, True)]
        k = corners[0]
        P = P[k:] + P[:k] + [P[k]]                         # open lus die op een hoek begint en eindigt
        n = len(P)
    pieces, cur = [], [P[0]]
    for i in range(1, n):
        cur.append(P[i])
        if i < n - 1 and sharp(i):
            pieces.append(cur)
            cur = [P[i]]
    pieces.append(cur)
    return [(q, False) for q in pieces]


def text_mesh(text, height, depth, mat, stroke=0.25, spacing=0.30):
    """Tekst uit afgeronde streken in het XY-vlak, gecentreerd, voorkant +Z, dikte `depth`."""
    parts, x = [], 0.0
    r = stroke / 2
    for ch in text:
        if ch == " ":
            x += 0.5
            continue
        w, strokes = GLYPHS[ch]
        for pts, closed in strokes:
            P = [(x + px, py, 0.0) for (px, py) in pts]
            if len(P) <= 4 and not closed:          # rechte streken: losse capsules met ronde koppen
                for a, b in zip(P[:-1], P[1:]):
                    parts.append(tube([a, b], r, mat, seg=10, phase=math.pi / 10))
                continue
            pieces = _split_sharp(P, closed)
            for k, (Q, cl) in enumerate(pieces):
                # op een knik heeft alleen het vorige stuk een kop; het volgende begint open in die kop,
                # zodat er geen twee bolkoppen op elkaar liggen (z-fighting)
                first = k == 0 and not closed
                if cl:
                    parts.append(tube(Q, r, mat, seg=10, closed=True, phase=math.pi / 10))
                elif len(Q) <= 3:
                    for i, (a, b) in enumerate(zip(Q[:-1], Q[1:])):
                        parts.append(tube([a, b], r, mat, seg=10, phase=math.pi / 10, caps=(first and i == 0, True)))
                else:
                    parts.append(tube(Q, r, mat, seg=10, phase=math.pi / 10, caps=(first, True)))
        x += w + spacing
    width = x - spacing
    m = mk.merge(*parts)
    m = m.transform(t=(-width / 2, -0.5, 0.0))
    m.V[:, 2] *= (depth / 2) / r
    m.V[:, 2] += depth / 2
    m = m.transform(s=(height, height, 1.0))
    m.sharp = False
    return m, width * height


def train_icon(size, depth, mat, wheel_mat=None, c=None):
    """Locomotiefje uit het concept (cabine links met raam, ketel, schoorsteen, wielen), breedte `size`."""
    s = size
    c = 0.008 * s if c is None else c
    body = [(0.00, 0.20), (0.96, 0.20), (0.96, 0.30), (1.00, 0.30), (1.00, 0.40), (0.96, 0.40), (0.96, 0.56),
            (0.80, 0.56), (0.80, 0.74), (0.66, 0.74), (0.66, 0.56), (0.40, 0.56), (0.40, 0.84), (0.44, 0.84),
            (0.44, 0.92), (-0.04, 0.92), (-0.04, 0.84), (0.00, 0.84)]
    win = rounded_rect_loop(0.08, 0.32, 0.56, 0.76, 0.035, 2)
    body = [(x * s, y * s) for x, y in body]
    win = [(x * s, y * s) for x, y in win]
    parts = [slab(rounded_corners(body, 0.025 * s), [win], 0.0, depth, c, mat)]
    pw = max(0.006, 0.3 * depth)                    # wielen steken iets uit, anders vallen ze samen met de romp
    for (wx, wr) in ((0.17, 0.13), (0.50, 0.12), (0.80, 0.12)):
        parts.append(slab(circle_loop(wx * s, 0.13 * s, wr * s, 16), [circle_loop(wx * s, 0.13 * s, wr * s * 0.35, 10)],
                          -pw, depth + pw, c, wheel_mat or mat))
    m = mk.merge(*parts)
    return m.transform(t=(-0.48 * s, -0.46 * s, 0.0))


def rounded_corners(P, r, seg=2):
    """Rond elke hoek van een polygoon licht af (kleine boogjes), voor zachte silhouetten."""
    P = np.asarray(P, float)
    out = []
    n = len(P)
    for i in range(n):
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]
        da, dc = a - b, c - b
        la, lc = np.linalg.norm(da), np.linalg.norm(dc)
        rr = min(r, la * 0.45, lc * 0.45)
        p0, p1 = b + da / la * rr, b + dc / lc * rr
        for k in range(seg + 1):
            t = k / seg
            q = (1 - t) ** 2 * p0 + 2 * (1 - t) * t * b + t ** 2 * p1
            out.append((q[0], q[1]))
    return out


def clock(r, depth, rim_mat="M_Stn_Yellow", face_mat="M_Stn_Cream", hand_mat="M_Stn_Navy", hour=-0.6, minute=0.0):
    """Klok (voorkant +Z): gele rand, crème wijzerplaat, streepjes en wijzers; ronde behuizing tot z=0."""
    parts = [slab(circle_loop(0, 0, r, 32), [circle_loop(0, 0, r * 0.80, 32)], 0.0, depth, r * 0.08, rim_mat),
             disc(r * 0.82, 0.0, depth * 0.6, face_mat, 32, r * 0.03),
             disc(r * 0.075, depth * 0.6, depth * 0.6 + r * 0.10, hand_mat, 12, r * 0.02)]
    for k in range(12):
        a = 2 * math.pi * k / 12
        big = k % 3 == 0
        L, w = (r * 0.13, r * 0.055) if big else (r * 0.07, r * 0.04)
        rr = r * 0.66
        parts.append(rbox(-w / 2, w / 2, -L / 2, L / 2, depth * 0.6, depth * 0.6 + r * 0.04, w * 0.4, hand_mat)
                     .transform(R=mk.rot_z(-a), t=(rr * math.sin(a), rr * math.cos(a), 0)))
    for ang, L, w in ((hour, r * 0.42, r * 0.10), (minute, r * 0.62, r * 0.075)):
        hand = rbox(-w / 2, w / 2, -w * 0.6, L, depth * 0.6 + 0.004, depth * 0.6 + r * 0.06, w * 0.45, hand_mat)
        parts.append(hand.transform(R=mk.rot_z(-ang)))
    return mk.merge(*parts)


def lantern(scale=1.0, mat_frame="M_Stn_Navy", mat_glow="M_Stn_Glow"):
    """Lantaarnkop uit het concept (marine kap, gloeiend glas, voet), oorsprong = onderkant, Y omhoog."""
    s = scale
    parts = [mk.lathe([(0.0, 0.0), (0.10, 0.0), (0.12, 0.03), (0.09, 0.06), (0.0, 0.06)], 16, mat=mat_frame),
             mk.lathe([(0.0, 0.05), (0.12, 0.05), (0.15, 0.30), (0.0, 0.30)], 8, mat=mat_glow, phase=0.5),
             mk.lathe([(0.0, 0.28), (0.20, 0.29), (0.21, 0.32), (0.13, 0.42), (0.04, 0.47), (0.05, 0.53),
                       (0.0, 0.55)], 8, mat=mat_frame, phase=0.5)]
    for k in range(8):                                   # spijltjes op de hoeken van het glas
        a = 2 * math.pi * (k + 0.5) / 8
        parts.append(capsule((0.122 * math.cos(a), 0.06, 0.122 * math.sin(a)),
                             (0.152 * math.cos(a), 0.29, 0.152 * math.sin(a)), 0.012, mat=mat_frame, seg=6, rings=1))
    m = mk.merge(*parts)
    return m.transform(s=(s, s, s))


# ------------------------------------------------------------------ export
_GUIDS = {}


def ensure_materials():
    if _GUIDS:
        return _GUIDS
    os.makedirs(EXP_DIR, exist_ok=True)
    unity_assets.folder_meta(ROOT, MAT_DIR)
    for n, (ramp, sm, sh, op) in MATERIALS.items():
        _GUIDS[n] = unity_assets.material(ROOT, f"{MAT_DIR}/{n}.mat", n, ramp[2], sm, None, op.get("opacity"), ramp, sh, op)
    return _GUIDS


def export(name, sub, parts, density=600, radius=0.20, smooth=(), manifest=None, extra=None, model_dir=None,
           readable=False):
    """parts: [(knoopnaam, mesh, parent of None, pivot, rotatie, zichtbaar)], Col_* = verborgen botsvorm.
    model_dir: andere Unity-map dan de stationsmodellen (bijv. de bordletters, die geen prefab worden)."""
    guids = ensure_materials()
    sc = mk.Scene(name)
    used = sorted({m for (_, mesh, *_r) in parts if mesh is not None for m in mesh.M})
    for mname in used:
        ramp, sm, sh, op = MATERIALS[mname]
        sc.material(mname, ramp[2], ramp, sm, ramp_shift=sh, opts=dict(op))
    sc.add(mk.Part(name, None))
    for (pname, mesh, parent, pivot, rot, visible) in parts:
        sc.add(mk.Part(pname, mesh, tuple(pivot), parent or name, rot, visible=visible))
        sc.parts[-1].gradient = 0.0 if pname.startswith("Col_") else 0.2
    sc.facet_all(skip=smooth)
    sc.bake_shading(density=density, radius=radius, noise_freq=0.9, edge_range=(3.0, 12.0), max_samples=60000)
    base = model_dir or MODEL_DIR
    rel_dir = f"{base}/{sub}" if sub else base
    os.makedirs(os.path.join(ROOT, rel_dir), exist_ok=True)
    unity_assets.folder_meta(ROOT, base)
    unity_assets.folder_meta(ROOT, rel_dir)
    fbx_rel = f"{rel_dir}/{name}.fbx"
    sc.write_fbx_ascii(os.path.join(ROOT, fbx_rel))
    sc.write_glb(os.path.join(EXP_DIR, name + ".glb"), include_hidden=False)
    unity_assets.model_meta(ROOT, fbx_rel, {m: guids[m] for m in sc.materials}, readable=readable)
    pts = []
    for p in sc.parts:
        if p.mesh is None or not p.visible:
            continue
        M = sc.world_matrix(p)
        pts.append(p.mesh.V @ M[:3, :3].T + M[:3, 3])
    P = np.vstack(pts)
    tris = sum(p.mesh.tri_count() for p in sc.parts if p.mesh is not None and p.visible)
    info = {"fbx": fbx_rel, "category": sub or "Building", "tris": int(tris),
            "min": np.round(P.min(0), 3).tolist(), "max": np.round(P.max(0), 3).tolist(),
            "nodes": [p.name for p in sc.parts]}
    if extra:
        info.update(extra)
    if manifest is not None:
        manifest[name] = info
    print(f"{name:28s} {tris:6d} tris  size {np.round(P.max(0) - P.min(0), 2)}")
    return sc


def write_manifest(manifest, path_name="StationKit.json"):
    path = os.path.join(EXP_DIR, path_name)
    old = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {"assets": {}}
    old["assets"].update(manifest)
    old["station"] = {"platformTop": PLATFORM_TOP, "platformEdgeFromTrackCentre": PLATFORM_EDGE}
    old["materials"] = {n: {"ramp": r, "smoothness": sm, "rampShift": sh, "opts": op} for n, (r, sm, sh, op) in MATERIALS.items()}
    json.dump(old, open(path, "w", encoding="utf-8"), indent=2)
