"""Woestijnkit voor levels 6 en 7 (rode kloven, cactussen, mijn): de props uit de conceptbladen in
Docs/References/Desert/ in dezelfde chunky low-poly stijl als de rest van de omgeving (build_environment_kit.py).

Kleuren alleen uit palette.json (groep 'Woestijn' + bestaande ramps): rots en lagen = desert_rock/desert_band,
zand = desert_sand, cactus = dino_green, bloemen = desert_flower, hout = track_wood, goud = gold_trim.
Alles is een gesloten volume; onderdelen steken in elkaar (nooit een dunne plaat óp een vlak), dus geen z-fighting.
Pivot midden-onder, Y omhoog, +Z voor, 1 unit = 1 m.

  Desert/  Cactus_Saguaro_01/02, Cactus_Saguaro_Small_01, Cactus_Barrel_01, Cactus_Prickly_01,
           Desert_Agave_01, Desert_Scrub_01, Desert_GrassTuft_01,
           Rock_Desert_Small_01, Rock_Desert_Medium_01, Rock_Desert_Large_01, Rock_Desert_Boulder_01,
           Hoodoo_01, Hoodoo_02, Rock_Arch_01 (boog over het spoor: 11 m vrij onder de boog),
           Mesa_Back_01..03 (verre tafelbergen), Skull_Bull_01, Tumbleweed_01,
           Windmill_01 (knoop 'Rotor' draait om lokale Z), Mine_Entrance_01, MineCart_Gold_01, Mine_Crane_01,
           Sign_BisonCrossing_01, Sign_Chevron_01, Flag_Pennant_01

Gebruik:  python ArtSource/Environment/Desert/build_desert_kit.py [naamfilter ...]
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ENV = os.path.abspath(os.path.join(HERE, ".."))
sys.path.insert(0, ENV)
import build_environment_kit as env  # noqa: E402
from build_environment_kit import mk, unity_assets, ROOT, R, MAT_DIR, MODEL_DIR  # noqa: E402
from nature_v3 import blob, lathe, limb, blade, scatter  # noqa: E402

EXP_DIR = os.path.join(ROOT, "ArtSource", "Exports", "Environment", "Desert")
DESERT_DIR = MODEL_DIR + "/Desert"

ROCK = {"cells": 0.7, "cellSize": 0.9, "rim": 0.22}
MATERIALS = {
    # rots: losse blokken zonder lagen (vallende stenen draaien; lagen zouden over het blok glijden)
    "M_Env_DesertRock": (R["desert_rock"][2], R["desert_rock"], 0.05, 0.02, dict(ROCK)),
    # kliffen, hoodoos, boog en tafelbergen: horizontale lagen in wereldruimte (StylizedRamp _Strata)
    "M_Env_DesertCliff": (R["desert_rock"][2], R["desert_rock"], 0.05, 0.06, {"cells": 0.55, "cellSize": 1.1, "rim": 0.22,
                                                                               "strata": 1.15}),
    "M_Env_DesertCliffStone": (R["desert_band"][2], R["desert_band"], 0.05, -0.02, {"cells": 0.55, "cellSize": 1.1,
                                                                                     "rim": 0.22, "strata": 0.9}),
    "M_Env_DesertMesaFar": (R["desert_rock"][3], R["desert_rock"], 0.05, 0.16, {"cells": 0.6, "cellSize": 6.0, "rim": 0.30,
                                                                                 "edge": 0.6, "strata": 4.5}),
    "M_Env_DesertSand": (R["desert_sand"][2], R["desert_sand"], 0.05, 0.02, {"cells": 0.7, "cellSize": 0.9}),
    "M_Env_DesertScrub": (R["shorts_olive"][3], R["shorts_olive"], 0.05, 0.10, {"cells": 0.5, "cellSize": 0.5}),
    "M_Env_Cactus": (R["dino_green"][2], R["dino_green"], 0.05, -0.04, {"cells": 0.4, "cellSize": 0.5, "rim": 0.28}),
    "M_Env_CactusSpine": (R["mustache_cream"][3], R["mustache_cream"], 0.05, 0.10, {"cells": 0.0, "rim": 0.10}),
    "M_Env_CactusFlower": (R["desert_flower"][3], R["desert_flower"], 0.05, 0.04, {"cells": 0.0, "rim": 0.20}),
    "M_Env_Agave": (R["grass"][3], R["grass"], 0.05, 0.12, {"cells": 0.4, "cellSize": 0.4, "rim": 0.25}),
    "M_Env_DryWood": (R["track_wood"][1], R["track_wood"], 0.05, -0.22, {"cells": 0.4, "cellSize": 0.4}),
    "M_Env_Tumbleweed": (R["track_wood"][3], R["track_wood"], 0.05, -0.04, {"cells": 0.3, "cellSize": 0.3, "rim": 0.20}),
    "M_Env_Bone": (R["mustache_cream"][3], R["mustache_cream"], 0.05, 0.10, {"cells": 0.3, "cellSize": 0.3}),
    "M_Env_BoneHorn": (R["hair_brown"][3], R["hair_brown"], 0.05, 0.05, {"cells": 0.0}),
    "M_Env_Timber": (R["track_wood"][2], R["track_wood"], 0.05, -0.16, {"cells": 0.6, "cellSize": 0.5, "grime": 0.3}),
    "M_Env_TimberDark": (R["interior_wood"][2], R["interior_wood"], 0.05, -0.12, {"cells": 0.5, "cellSize": 0.5, "grime": 0.3}),
    "M_Env_IronBand": (R["metal"][1], R["metal"], 0.2, -0.20, {"cells": 0.0, "rim": 0.30}),
    "M_Env_RustBlade": (R["wagon_brown"][2], R["wagon_brown"], 0.05, -0.05, {"cells": 0.3, "cellSize": 0.6}),
    "M_Env_MineDark": (R["ink"][2], R["ink"], 0.0, -0.10, {"cells": 0.0, "flat": 0.8, "rim": 0.0, "edge": 0.0}),
    "M_Env_Lantern": (R["headlight"][2], R["headlight"], 0.05, 0.10, {"emissive": 1.0, "cells": 0.0}),
    "M_Env_Gold": (R["gold_trim"][3], R["gold_trim"], 0.4, 0.10, {"cells": 0.0, "spec": 1.4, "rim": 0.40}),
    "M_Env_Rope": (R["safari_hat"][2], R["safari_hat"], 0.05, -0.05, {"cells": 0.0}),
    "M_Env_SignYellow": (R["warning"][3], R["warning"], 0.05, 0.10, {"cells": 0.0, "rim": 0.15}),
    "M_Env_SignBlack": (R["ink"][3], R["ink"], 0.05, 0.10, {"cells": 0.0, "rim": 0.10}),
    "M_Env_FlagRed": (R["title_red"][2], R["title_red"], 0.05, 0.05, {"cells": 0.0, "cull": 0.0}),
}


# ------------------------------------------------------------------ helpers
def ribbed(p0, p1, r0, r1, mat, ribs=8, depth=0.13, cap=True):
    """Geribde cactusbuis van p0 naar p1 met ronde top: lathe met afwisselend vol/hol per segment."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0
    L = float(np.linalg.norm(d))
    prof = [(0.0, -0.02), (r0, 0.0), (r0 * 0.99 + r1 * 0.01, L * 0.35), (r1, L - r1 * 0.75)]
    if cap:
        prof += [(r1 * 0.80, L - r1 * 0.18), (r1 * 0.42, L + r1 * 0.12), (0.0, L + r1 * 0.22)]
    else:
        prof += [(r1, L), (0.0, L + 0.001)]
    m = lathe(prof, ribs * 2, mat, rmod=lambda i, k: 1.0 if k % 2 == 0 else 1.0 - depth)
    y = d / L
    x = np.cross(y, [0, 0, 1.0]) if abs(y[2]) < 0.9 else np.cross(y, [1.0, 0, 0])
    x /= np.linalg.norm(x)
    z = np.cross(x, y)
    return m.transform(R=np.stack([x, y, z], axis=1), t=tuple(p0))


def bloom(r, mat="M_Env_CactusFlower"):
    """Cactusbloem: vijf ronde blaadjes en een geel hart (lokaal: midden op y = 0, wijst naar +Y)."""
    parts = [mk.ellipsoid(r * 0.42, r * 0.36, r * 0.42, 8, 5, mat="M_Env_FlowerHeart").transform(t=(0, r * 0.18, 0))]
    for k in range(5):
        a = 2 * math.pi * k / 5
        petal = mk.ellipsoid(r * 0.52, r * 0.22, r * 0.34, 8, 4, mat=mat)
        petal = petal.transform(R=mk.rot_z(math.radians(-24))).transform(t=(r * 0.52, r * 0.10, 0)).transform(R=mk.rot_y(a))
        parts.append(petal)
    return mk.merge(*parts)


def surface_spines(rng, body, n, min_d=0.09, mat="M_Env_CactusSpine", max_up=0.9):
    """Stekelbosjes precies op het oppervlak: op echte hoekpunten van de cactus, langs de vertexnormaal en een paar mm
    ingebed. Zo zweeft er nooit een stekel naast een afgeronde top of de dunne rand van een schijf."""
    V = body.V
    N = mk.vertex_normals(body)
    out, used = [], []
    for i in rng.permutation(len(V)):
        if len(out) >= n:
            break
        p, nn = V[i], N[i]
        if nn[1] > max_up or p[1] < 0.05:
            continue
        if any(np.linalg.norm(p - q) < min_d for q in used):
            continue
        used.append(p)
        cone = lathe([(0.0, -0.015), (0.017, 0.0), (0.0, 0.05)], 3, mat)
        out.append(cone.transform(R=_frame(nn), t=tuple(p - nn * 0.006)))
    return out


def _frame(up):
    up = np.asarray(up, float) / np.linalg.norm(up)
    x = np.cross(up, [0, 0, 1.0]) if abs(up[2]) < 0.9 else np.cross(up, [1.0, 0, 0])
    x /= np.linalg.norm(x)
    z = np.cross(x, up)
    return np.stack([x, up, z], axis=1)


def box(x0, x1, y0, y1, z0, z1, mat, r=0.03):
    sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
    return mk.rounded_box(sx, sy, sz, min(r, sx * 0.3, sy * 0.3, sz * 0.3), 1, mat=mat).transform(
        t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


def beam(p0, p1, w, mat, r=0.025):
    """Vierkante balk tussen twee punten (afgeronde randen)."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0
    L = float(np.linalg.norm(d))
    b = mk.rounded_box(w, L, w, min(r, w * 0.3), 1, mat=mat).transform(t=(0, L / 2, 0))
    return b.transform(R=_frame(d), t=tuple(p0))


# ------------------------------------------------------------------ cactussen en planten
def saguaro(rng, H=3.2, r=0.28, arms=((0.42, 1.0, 0.9, 1), (0.55, 0.85, 0.7, -1)), flowers=0):
    """Saguaro: geribde stam met armen die eerst opzij en dan omhoog gaan (arm = (hoogte-fractie, uitsteek, lengte, kant))."""
    parts = [ribbed((0, -0.12, 0), (0, H, 0), r, r * 0.9, "M_Env_Cactus")]
    spine_count = int(14 * H)
    tops = [(0.0, H + r * 0.9 * 0.2, 0.0, r * 0.9)]
    for frac, reach, length, side in arms:
        ra = r * 0.62
        h = H * frac
        yaw = rng.uniform(-0.5, 0.5) + (0 if side > 0 else math.pi)
        dx, dz = math.cos(yaw), math.sin(yaw)
        elbow = np.array([dx * reach, h, dz * reach])
        parts.append(ribbed((0, h - ra * 0.2, 0), elbow - np.array([dx, 0, dz]) * ra * 0.4, ra, ra, "M_Env_Cactus", cap=False))
        parts.append(mk.ellipsoid(ra * 1.02, ra * 1.02, ra * 1.02, 16, 8, mat="M_Env_Cactus").transform(t=tuple(elbow)))
        top = elbow + np.array([0, length, 0])
        parts.append(ribbed(elbow, top, ra, ra * 0.92, "M_Env_Cactus"))
        spine_count += int(10 * length)
        tops.append((top[0], top[1] + ra * 0.92 * 0.2, top[2], ra * 0.92))
    parts += surface_spines(rng, mk.merge(*parts), spine_count)
    for k in range(flowers):
        x, y, z, rr = tops[k % len(tops)]
        parts.append(bloom(0.15).transform(t=(x, y - 0.02, z)))
    return mk.merge(*parts)


def barrel_cactus(rng, r=0.36, h=0.62):
    prof = [(0.0, -0.05), (r * 0.82, -0.05), (r, h * 0.28), (r * 0.96, h * 0.62), (r * 0.70, h * 0.92), (r * 0.30, h * 1.02), (0.0, h * 1.03)]
    body = lathe(prof, 28, "M_Env_Cactus", rmod=lambda i, k: 1.0 if k % 2 == 0 else 0.88)
    parts = [body] + surface_spines(rng, body, 40, min_d=0.07, max_up=0.95)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        parts.append(bloom(0.10).transform(t=(math.cos(a) * r * 0.28, h * 0.99, math.sin(a) * r * 0.28)))
    return mk.merge(*parts)


def prickly_pear(rng):
    """Schijfcactus: platte peddels die op elkaar groeien, bloemen op de randen."""
    parts = []

    def pad(c, size, tilt, yaw):
        p = blob(size * 0.62, size, size * 0.17, rng, 0.04, seg=10, rings=6, mat="M_Env_Cactus")
        return p.transform(R=mk.rot_y(yaw) @ mk.rot_z(tilt), t=c)

    base = [((0.0, 0.32, 0.0), 0.36, 0.05, 0.0), ((0.22, 0.30, 0.10), 0.30, -0.45, 0.9)]
    tier = [((-0.20, 0.82, 0.03), 0.30, 0.35, 0.2), ((0.18, 0.86, -0.04), 0.28, -0.30, -0.3), ((0.48, 0.66, 0.14), 0.24, -0.70, 1.0)]
    top = [((-0.30, 1.22, 0.06), 0.22, 0.55, 0.4), ((0.12, 1.28, -0.02), 0.22, -0.15, -0.1)]
    for c, s, t, y in base + tier + top:
        parts.append(pad(c, s, t, y))
    parts += surface_spines(rng, mk.merge(*parts), 34, min_d=0.08, max_up=1.01)
    for c, s, t, y in top + tier[:1]:
        tip = mk.rot_y(y) @ mk.rot_z(t) @ np.array([0, s * 0.92, 0]) + np.asarray(c)
        parts.append(bloom(0.09).transform(t=tuple(tip)))
    return mk.merge(*parts)


def agave(rng, n=14, h=0.62):
    parts = []
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.15, 0.15)
        hh = h * rng.uniform(0.7, 1.1)
        b = lathe([(0.0, -0.02), (0.07, 0.0), (0.06, hh * 0.4), (0.0, hh)], 4, "M_Env_Agave")
        b.V[:, 2] *= 0.45
        lean = rng.uniform(0.45, 0.85) if k % 3 else rng.uniform(0.15, 0.35)
        parts.append(b.transform(R=mk.rot_y(-a) @ mk.rot_z(-lean)).transform(t=(math.cos(a) * 0.05, 0, math.sin(a) * 0.05)))
    parts.append(lathe([(0.0, -0.04), (0.12, -0.04), (0.08, 0.10), (0.0, 0.14)], 6, "M_Env_Agave"))
    return mk.merge(*parts)


def scrub(rng):
    """Droge struik: grijsbruine takjes met een paar olijfkleurige bolletjes."""
    parts = []
    for k in range(9):
        a = 2 * math.pi * k / 9 + rng.uniform(-0.3, 0.3)
        mid = (math.cos(a) * 0.22, 0.30 + rng.uniform(-0.05, 0.08), math.sin(a) * 0.22)
        tip = (math.cos(a) * 0.48, 0.55 + rng.uniform(-0.1, 0.12), math.sin(a) * 0.48)
        parts.append(limb((0, -0.04, 0), mid, 0.04, 0.025, "M_Env_DryWood", 4))
        parts.append(limb(mid, tip, 0.025, 0.012, "M_Env_DryWood", 4))
        if k % 2 == 0:
            parts.append(blob(0.14, 0.11, 0.14, rng, 0.12, seg=6, rings=4, mat="M_Env_DesertScrub").transform(t=tip))
    return mk.merge(*parts)


def desert_tuft(rng, n=11, h=0.5):
    parts = []
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.3, 0.3)
        hh = h * rng.uniform(0.55, 1.1)
        r = rng.uniform(0.02, 0.07)
        bl = lathe([(0.03, 0.0), (0.0, hh)], 4, "M_Env_Agave")
        tip = np.array([math.cos(a) * (r + hh * 0.4), 0, math.sin(a) * (r + hh * 0.4)])
        V = bl.V.copy()
        V[:, 0] += V[:, 1] / hh * tip[0]
        V[:, 2] += V[:, 1] / hh * tip[2]
        bl.V = V
        parts.append(bl.transform(t=(math.cos(a) * r, -0.02, math.sin(a) * r)))
    return mk.merge(*parts)


# ------------------------------------------------------------------ rotsen
def desert_rock(size, rng, squash=0.62, flat=0.78, mat="M_Env_DesertRock"):
    """Blokkige rode steen met afgeplatte bovenkant (zoals de losse blokken in het concept)."""
    a = blob(size * 0.58, size * 0.5 * squash, size * 0.5, rng, 0.16, seg=6, rings=4, mat=mat)
    top = a.V[:, 1].max() * flat
    a.V[:, 1] = np.minimum(a.V[:, 1], top)
    a.V[:, 1] -= a.V[:, 1].min() + size * 0.07
    parts = [a]
    if size > 1.3:
        b = blob(size * 0.36, size * 0.3, size * 0.32, rng, 0.15, seg=6, rings=4, mat=mat)
        b.V[:, 1] = np.minimum(b.V[:, 1], b.V[:, 1].max() * 0.8)
        b.V[:, 1] -= b.V[:, 1].min() + size * 0.05
        parts.append(b.transform(t=(size * 0.45, 0, size * 0.2)))
    return mk.merge(*parts)


def boulder(rng, size=1.0):
    a = blob(size * 0.5, size * 0.44, size * 0.48, rng, 0.11, seg=7, rings=5, mat="M_Env_DesertRock")
    a.V[:, 1] -= a.V[:, 1].min()
    return a


def hoodoo(rng, H=7.0, tiers=5, w=1.4):
    """Rotszuil: gestapelde, afgeplatte klonten (smaller in het midden) met een bredere kap en zand bovenop."""
    parts = []
    y = -0.4
    for k in range(tiers):
        t = k / max(1, tiers - 1)
        rw = w * (1.0 - 0.35 * math.sin(t * math.pi * 0.9)) * rng.uniform(0.92, 1.06)
        h = (H + 0.4) / tiers
        b = blob(rw, h * 0.62, rw * 0.92, rng, 0.08, seg=7, rings=4, mat="M_Env_DesertCliff")
        b.V[:, 1] = np.clip(b.V[:, 1], -h * 0.5, h * 0.5)
        parts.append(b.transform(t=(rng.uniform(-0.12, 0.12), y + h * 0.5, rng.uniform(-0.12, 0.12))))
        y += h * 0.88
    cap = blob(w * 1.25, 0.55, w * 1.15, rng, 0.08, seg=8, rings=4, mat="M_Env_DesertCliffStone")
    cap.V[:, 1] = np.clip(cap.V[:, 1], -0.3, 0.3)
    parts.append(cap.transform(t=(0, y + 0.1, 0)))
    sand = blob(w * 1.0, 0.25, w * 0.9, rng, 0.06, seg=8, rings=3, mat="M_Env_DesertSand")
    sand.V[:, 1] = np.maximum(sand.V[:, 1], -0.05)
    parts.append(sand.transform(t=(0, y + 0.32, 0)))
    return mk.merge(*parts)


def rock_arch(rng, span=16.0, clear=11.5, depth=4.4):
    """Natuurlijke rotsboog uit één stuk: een blokkige doorsnede die langs een halve ellips loopt, dik aan de voeten en
    dunner bovenin, met zand op de bovenkant (per vlak gekleurd, dus geen losse plaat erop). Tussen de poten is ruim
    10 m vrij, onder de top 11 m: de trein rijdt er makkelijk onderdoor."""
    half = span * 0.5
    H = clear + 1.4
    centre = [(-half, -2.6), (-half, -1.2)]
    n = 22
    for i in range(n + 1):
        u = math.pi * i / n
        centre.append((-math.cos(u) * half, math.sin(u) * H))
    centre += [(half, -1.2), (half, -2.6)]
    C = np.array(centre, float)
    seg = 12
    V, F = [], []
    rings = []
    for i, (x, y) in enumerate(C):
        a = C[max(i - 1, 0)]
        b = C[min(i + 1, len(C) - 1)]
        t = b - a
        t /= np.linalg.norm(t)
        nrm = np.array([t[1], -t[0]])                    # in het boogvlak, loodrecht op de middellijn
        if np.dot(nrm, np.array([x, y - 0.0])) < 0:
            nrm = -nrm
        lift = max(0.0, min(1.0, (y + 1.2) / H))
        thick = 2.7 + 2.2 * (1.0 - math.sin(lift * math.pi / 2) ** 1.5)
        deep = depth * (1.0 + 0.22 * (1.0 - lift))
        ring = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            c, s_ = math.cos(th), math.sin(th)
            ex = 0.5  # superellips-exponent < 1 = blokkig
            px = math.copysign(abs(c) ** ex, c) * thick * 0.5
            pz = math.copysign(abs(s_) ** ex, s_) * deep * 0.5
            j = rng.normal(0, 0.10, 3) * np.array([thick, 0.6, deep]) * 0.5
            P = np.array([x + nrm[0] * px, y + nrm[1] * px, pz]) + j
            ring.append(len(V))
            V.append(P)
        rings.append(ring)
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for k in range(seg):
            k2 = (k + 1) % seg
            F.append((r0[k], r0[k2], r1[k2], r1[k]))
    for ring, flip in ((rings[0], True), (rings[-1], False)):
        c = len(V)
        V.append(np.mean([V[i] for i in ring], axis=0))
        for k in range(seg):
            f = (c, ring[(k + 1) % seg], ring[k])
            F.append(f[::-1] if flip else f)
    m = mk.Mesh(np.array(V, float), F, ["M_Env_DesertCliff"] * len(F))
    m = mk._orient_closed(m)
    m.sharp = True
    T = mk.Mesh(m.V, m.F, m.M)
    mats = []
    for f in m.F:
        P = m.V[list(f)]
        nn = np.cross(P[1] - P[0], P[2] - P[0])
        nn /= max(np.linalg.norm(nn), 1e-9)
        mats.append("M_Env_DesertSand" if nn[1] > 0.72 and P[:, 1].mean() > H * 0.55 else
                    "M_Env_DesertCliffStone" if P[:, 1].mean() < 1.5 else "M_Env_DesertCliff")
    m.M = mats
    parts = [m]
    for side in (-1, 1):                                   # puinblokken aan de voeten
        for k in range(3):
            s = rng.uniform(0.7, 1.3)
            b = blob(s, s * 0.7, s, rng, 0.12, seg=6, rings=4, mat="M_Env_DesertRock")
            b.V[:, 1] = np.minimum(b.V[:, 1], b.V[:, 1].max() * 0.75)
            parts.append(b.transform(t=(side * (half + rng.uniform(1.6, 2.8)), s * 0.2, rng.uniform(-2.6, 2.6))))
    return mk.merge(*parts)


def mesa_back(width, height, rng, tiers=2, seg=9):
    """Verre tafelberg: steile getrapte wanden, vlakke top; plat in de diepte zoals de verre bergen."""
    prof = [(1.0, -3.0), (0.96, height * 0.35)]
    if tiers > 1:
        prof += [(0.80, height * 0.42), (0.76, height * 0.78)]
    prof += [(0.66, height * 0.86), (0.62, height), (0.0, height + 0.01)]
    rk = rng.uniform(0.86, 1.14, seg)
    m = env.lathe_mod([(width / 2 * r, y) for r, y in prof], seg, rmod=lambda i, k: rk[k], phase=rng.uniform(0, 6.28),
                      mat="M_Env_DesertMesaFar")
    m.V[:, 2] *= 0.5
    return m


# ------------------------------------------------------------------ decor
def bull_skull(rng):
    parts = [blob(0.17, 0.12, 0.17, rng, 0.04, seg=9, rings=6, mat="M_Env_Bone").transform(t=(0, 0.12, -0.02))]
    snout = lathe([(0.0, 0.0), (0.12, 0.0), (0.10, 0.18), (0.07, 0.30), (0.0, 0.32)], 8, "M_Env_Bone")
    parts.append(snout.transform(R=mk.rot_x(math.radians(95)), t=(0, 0.11, 0.04)))
    for side in (1, -1):
        eye = mk.ellipsoid(0.045, 0.04, 0.03, 8, 5, mat="M_Env_BoneHorn").transform(t=(side * 0.085, 0.15, 0.13))
        parts.append(eye)
        pts = [(side * 0.12, 0.18, -0.03), (side * 0.30, 0.20, -0.04), (side * 0.44, 0.30, 0.02), (side * 0.47, 0.42, 0.10)]
        for (a, b, r0, r1, mat) in [(pts[0], pts[1], 0.05, 0.042, "M_Env_Bone"), (pts[1], pts[2], 0.042, 0.03, "M_Env_Bone"),
                                    (pts[2], pts[3], 0.03, 0.012, "M_Env_BoneHorn")]:
            parts.append(limb(a, b, r0, r1, mat, 7))
        parts.append(mk.ellipsoid(0.043, 0.043, 0.043, 8, 5, mat="M_Env_Bone").transform(t=pts[1]))
        parts.append(mk.ellipsoid(0.031, 0.031, 0.031, 8, 5, mat="M_Env_Bone").transform(t=pts[2]))
    for side in (1, -1):
        parts.append(mk.ellipsoid(0.022, 0.018, 0.02, 6, 4, mat="M_Env_BoneHorn").transform(t=(side * 0.035, 0.08, 0.36)))
    m = mk.merge(*parts)
    m.V[:, 1] -= m.V[:, 1].min() + 0.03
    return m


def tumbleweed(rng, r=0.46):
    parts = [blob(r * 0.35, r * 0.35, r * 0.35, rng, 0.1, seg=6, rings=4, mat="M_Env_Tumbleweed")]
    for k in range(70):
        u, v = rng.normal(size=3), rng.normal(size=3)
        u /= np.linalg.norm(u)
        v /= np.linalg.norm(v)
        a = u * r * rng.uniform(0.75, 1.0)
        b = v * r * rng.uniform(0.75, 1.0)
        mid = (a + b) * 0.5
        mid = mid / max(np.linalg.norm(mid), 1e-3) * r * rng.uniform(0.85, 1.05)
        parts.append(limb(a, mid, 0.016, 0.014, "M_Env_Tumbleweed", 4))
        parts.append(limb(mid, b, 0.014, 0.010, "M_Env_Tumbleweed", 4))
    m = mk.merge(*parts)
    m.V[:, 1] -= m.V[:, 1].min()
    return m


def mine_entrance(rng, W=4.4, H=4.6, D=2.2):
    """Houten mijningang in een rotsheuvel: staanders, latei met overstek, schoren, lantaarns en een donkere gang.
    De opening is breed genoeg voor een zijspoor dat in de mijn eindigt."""
    parts = []
    t = 0.34
    for side in (-1, 1):
        x = side * (W / 2 + t / 2)
        parts.append(box(x - t / 2, x + t / 2, -0.3, H, -0.2, 0.2, "M_Env_Timber"))
        parts.append(beam((x, H * 0.62, 0.0), (side * (W / 2 - 0.6), H + 0.02, 0.0), 0.2, "M_Env_TimberDark"))
        # lantaarn aan de staander
        lx = x + side * 0.32
        parts.append(box(lx - 0.13, lx + 0.13, H * 0.62 - 0.2, H * 0.62 + 0.18, 0.12, 0.38, "M_Env_IronBand"))
        parts.append(box(lx - 0.10, lx + 0.10, H * 0.62 - 0.16, H * 0.62 + 0.12, 0.16, 0.42, "M_Env_Lantern", 0.04))
        parts.append(beam((x + side * 0.15, H * 0.62 + 0.26, 0.25), (lx, H * 0.62 + 0.18, 0.25), 0.06, "M_Env_IronBand", 0.01))
    parts.append(box(-W / 2 - 0.9, W / 2 + 0.9, H, H + 0.42, -0.28, 0.28, "M_Env_Timber"))
    parts.append(box(-W / 2 - 0.5, W / 2 + 0.5, H + 0.42, H + 0.70, -0.22, 0.22, "M_Env_TimberDark"))
    # gang: donkere binnenbak, de gangwanden zijn houten schotten
    parts.append(box(-W / 2, W / 2, -0.3, H, -D - 0.35, -D, "M_Env_MineDark", 0.01))
    for side in (-1, 1):
        parts.append(box(side * W / 2 - (0.12 if side > 0 else -0.0), side * W / 2 + (0.0 if side > 0 else 0.12), -0.3, H,
                         -D - 0.02, -0.18, "M_Env_TimberDark", 0.02))
    parts.append(box(-W / 2 - 0.05, W / 2 + 0.05, H - 0.14, H + 0.02, -D - 0.02, -0.18, "M_Env_TimberDark", 0.02))
    # lampjes in de gang
    for k in range(2):
        z = -0.7 - k * 0.8
        parts.append(mk.ellipsoid(0.08, 0.10, 0.08, 6, 4, mat="M_Env_Lantern").transform(t=(0, H - 0.3, z)))
    # rotsheuvel om de ingang (binnenkant blijft vrij)
    for k in range(14):
        a = math.pi * k / 13
        x = -math.cos(a) * (W / 2 + 1.7)
        y = math.sin(a) * (H + 0.8) - 0.4
        s = rng.uniform(1.5, 2.1)
        b = blob(s, s * 0.85, 1.6, rng, 0.08, seg=7, rings=4, mat="M_Env_DesertCliff")
        parts.append(b.transform(t=(x + math.copysign(0.4, x) if abs(x) > 0.2 else x, y, -1.3)))
    for side in (-1, 1):
        b = blob(2.2, 2.6, 1.9, rng, 0.07, seg=7, rings=4, mat="M_Env_DesertCliff")
        parts.append(b.transform(t=(side * (W / 2 + 2.6), 0.9, -1.6)))
    m = mk.merge(*parts)
    return m


def mine_cart(rng, L=1.5, W=1.0, H=0.78):
    parts = []
    y0 = 0.30
    tub = mk.rounded_box(W, H - y0, L, 0.06, 2, mat="M_Env_TimberDark").transform(t=(0, y0 + (H - y0) / 2, 0))
    V = tub.V.copy()
    top = V[:, 1] > y0 + (H - y0) / 2
    V[top, 0] *= 1.12
    V[top, 2] *= 1.10
    tub.V = V
    parts.append(tub)
    for z in (-L * 0.32, L * 0.32):
        parts.append(box(-W * 0.6, W * 0.6, H - 0.08, H + 0.02, z - 0.05, z + 0.05, "M_Env_IronBand", 0.015))
    parts.append(box(-W * 0.6, W * 0.6, y0 + 0.02, y0 + 0.1, -L * 0.56, L * 0.56, "M_Env_IronBand", 0.015))
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(mk.cylinder(0.17, 0.10, 14, 0.02, mat="M_Env_IronBand", axis="x").transform(t=(sx * (W * 0.5 - 0.02), 0.17, sz * L * 0.32)))
            parts.append(mk.cylinder(0.07, 0.12, 8, 0.0, mat="M_Env_TimberDark", axis="x").transform(t=(sx * (W * 0.5 + 0.02), 0.17, sz * L * 0.32)))
    parts.append(beam((-W * 0.5, 0.17, -L * 0.32), (W * 0.5, 0.17, -L * 0.32), 0.07, "M_Env_IronBand", 0.01))
    parts.append(beam((-W * 0.5, 0.17, L * 0.32), (W * 0.5, 0.17, L * 0.32), 0.07, "M_Env_IronBand", 0.01))
    for k in range(16):
        x, z = rng.uniform(-W * 0.38, W * 0.38), rng.uniform(-L * 0.38, L * 0.38)
        s = rng.uniform(0.12, 0.2)
        n = blob(s, s * 0.8, s, rng, 0.18, seg=5, rings=3, mat="M_Env_Gold")
        parts.append(n.transform(t=(x, H - 0.06 + s * 0.35 * (1 - (x * x + z * z) / (L * L * 0.2)), z)))
    return mk.merge(*parts)


def mine_crane(rng):
    parts = [box(-0.17, 0.17, -0.3, 4.4, -0.17, 0.17, "M_Env_Timber")]
    parts.append(beam((0, 4.1, 0), (2.6, 4.25, 0), 0.24, "M_Env_Timber"))
    parts.append(beam((0, 2.6, 0), (1.6, 4.15, 0), 0.16, "M_Env_TimberDark"))
    parts.append(beam((2.45, 4.1, 0), (2.45, 2.45, 0), 0.04, "M_Env_Rope", 0.01))
    parts.append(mk.torus(0.09, 0.02, 10, 5, mat="M_Env_IronBand").transform(R=mk.rot_x(math.pi / 2), t=(2.45, 2.40, 0)))
    parts.append(box(2.13, 2.77, 1.62, 2.28, -0.32, 0.32, "M_Env_TimberDark", 0.04))
    parts.append(box(2.10, 2.80, 1.92, 1.98, -0.35, 0.35, "M_Env_IronBand", 0.01))
    for side in (-1, 1):
        parts.append(beam((side * 0.9, -0.2, 0.6 * side), (0, 1.4, 0), 0.14, "M_Env_TimberDark"))
    parts.append(box(-0.5, 0.5, -0.3, 0.1, -0.5, 0.5, "M_Env_TimberDark", 0.04))
    return mk.merge(*parts)


BISON_SILHOUETTE = [  # zijaanzicht naar rechts: hoge bult, lage kop met hoorn en baard, dunne achterhand, staartje
    (-0.30, -0.20), (-0.25, -0.20), (-0.24, -0.07), (-0.10, -0.08), (-0.09, -0.20), (-0.04, -0.20), (-0.04, -0.06),
    (0.09, -0.07), (0.10, -0.20), (0.15, -0.20), (0.16, -0.08), (0.20, -0.12), (0.23, -0.06), (0.28, -0.06),
    (0.33, -0.01), (0.33, 0.05), (0.28, 0.09), (0.27, 0.15), (0.24, 0.16), (0.24, 0.11), (0.18, 0.19), (0.08, 0.24),
    (-0.04, 0.21), (-0.16, 0.13), (-0.27, 0.11), (-0.31, 0.07), (-0.35, 0.00), (-0.33, -0.01), (-0.30, 0.04),
    (-0.31, -0.05)]


def sign_bison(rng):
    """Geel ruitvormig waarschuwingsbord met een zwart bizonsilhouet op een houten paal (zoals in het concept)."""
    parts = [box(-0.08, 0.08, -0.4, 2.0, -0.18, -0.02, "M_Env_Timber")]
    S = 0.62
    diamond = [(0, S), (S, 0), (0, -S), (-S, 0)]
    rim = [(0, S + 0.07), (S + 0.07, 0), (0, -S - 0.07), (-S - 0.07, 0)]
    board_y = 2.15
    parts.append(mk.prism(rim, -0.02, 0.03, c=0.015, mat="M_Env_SignBlack").transform(t=(0, board_y, 0)))
    parts.append(mk.prism(diamond, 0.02, 0.06, c=0.012, mat="M_Env_SignYellow").transform(t=(0, board_y, 0)))
    sil = [(x * 1.45, y * 1.45) for x, y in BISON_SILHOUETTE]
    parts.append(mk.prism(sil, 0.055, 0.075, c=0.004, mat="M_Env_SignBlack").transform(t=(0, board_y - 0.02, 0)))
    parts.append(box(-0.1, 0.1, board_y + 0.2, board_y + 0.28, -0.05, 0.0, "M_Env_IronBand", 0.01))
    return mk.merge(*parts)


def sign_chevron(rng, W=1.9, H=0.62):
    """Bochtbord: geel bord met drie zwarte pijlpunten (naar -X) op twee palen."""
    parts = []
    for x in (-W * 0.36, W * 0.36):
        parts.append(box(x - 0.07, x + 0.07, -0.4, 1.25, -0.14, -0.01, "M_Env_Timber"))
    y = 0.95
    parts.append(box(-W / 2 - 0.05, W / 2 + 0.05, y - 0.05, y + H + 0.05, -0.01, 0.03, "M_Env_SignBlack", 0.012))
    parts.append(box(-W / 2, W / 2, y, y + H, 0.02, 0.06, "M_Env_SignYellow", 0.01))
    for k in range(3):
        cx = (k - 1) * W * 0.3
        h, w = H * 0.36, W * 0.07
        chev = [(cx - w, 0), (cx + w * 0.2, h), (cx + w * 1.4, h), (cx + w * 0.2, 0), (cx + w * 1.4, -h), (cx + w * 0.2, -h)]
        parts.append(mk.prism(chev, 0.055, 0.075, c=0.004, mat="M_Env_SignBlack").transform(t=(0, y + H / 2, 0)))
    return mk.merge(*parts)


def flag_pennant(rng, H=3.4):
    parts = [mk.cylinder(0.045, H + 0.3, 8, 0.0, mat="M_Env_TimberDark", axis="y").transform(t=(0, (H + 0.3) / 2 - 0.3, 0))]
    parts.append(mk.ellipsoid(0.07, 0.07, 0.07, 8, 5, mat="M_Env_Gold").transform(t=(0, H + 0.03, 0)))
    tri = [(0.03, 0.0), (1.05, -0.22), (0.92, -0.33), (1.10, -0.48), (0.03, -0.62)]
    parts.append(mk.prism(tri, -0.018, 0.018, c=0.006, mat="M_Env_FlagRed").transform(t=(0, H - 0.05, 0)))
    return mk.merge(*parts)


# ------------------------------------------------------------------ windmolen (meer onderdelen)
def windmill_parts(rng, H=7.6):
    """Toren en rotor apart: de rotor draait in Unity (WindmillSpin) om zijn lokale Z-as."""
    tower = []
    base, top = 1.35, 0.35
    corners = [(1, 1), (1, -1), (-1, -1), (-1, 1)]
    for sx, sz in corners:
        tower.append(beam((sx * base, -0.3, sz * base), (sx * top, H, sz * top), 0.14, "M_Env_TimberDark"))
    for level in (0.18, 0.42, 0.66):
        y = H * level
        w = base + (top - base) * level
        for k in range(4):
            a, b = corners[k], corners[(k + 1) % 4]
            tower.append(beam((a[0] * w, y, a[1] * w), (b[0] * w, y, b[1] * w), 0.09, "M_Env_Timber", 0.015))
    for k in range(4):
        a, b = corners[k], corners[(k + 1) % 4]
        y0, y1 = H * 0.18, H * 0.42
        w0, w1 = base + (top - base) * 0.18, base + (top - base) * 0.42
        tower.append(beam((a[0] * w0, y0, a[1] * w0), (b[0] * w1, y1, b[1] * w1), 0.06, "M_Env_IronBand", 0.01))
    tower.append(box(-0.55, 0.55, H - 0.05, H + 0.08, -0.55, 0.55, "M_Env_Timber", 0.02))
    tower.append(box(-0.22, 0.22, H + 0.08, H + 0.62, -0.42, 0.28, "M_Env_IronBand", 0.04))
    # staartvaan naar achter
    tower.append(beam((0, H + 0.4, -0.3), (0, H + 0.5, -2.0), 0.07, "M_Env_IronBand", 0.01))
    vane = [(0.0, 0.0), (0.0, 0.8), (-0.9, 0.55), (-0.9, 0.1)]
    tower.append(mk.prism(vane, -0.02, 0.02, c=0.006, mat="M_Env_RustBlade").transform(R=mk.rot_y(math.pi / 2), t=(0, H + 0.15, -1.6)))
    tower.append(beam((0.0, -0.3, 0.0), (0.0, H, 0.0), 0.05, "M_Env_IronBand", 0.01))
    rotor = [mk.cylinder(0.16, 0.30, 12, 0.03, mat="M_Env_IronBand", axis="z")]
    nb = 14
    for k in range(nb):
        a = 2 * math.pi * k / nb
        blade_shape = [(0.30, -0.06), (1.55, -0.16), (1.55, 0.16), (0.30, 0.06)]
        b = mk.prism(blade_shape, -0.02, 0.02, c=0.006, mat="M_Env_RustBlade")
        b = b.transform(R=mk.rot_x(math.radians(18))).transform(R=mk.rot_z(a))
        rotor.append(b)
    rotor.append(mk.torus(1.0, 0.03, 28, 4, mat="M_Env_IronBand").transform(R=mk.rot_x(math.pi / 2)))
    rotor.append(mk.torus(1.5, 0.03, 32, 4, mat="M_Env_IronBand").transform(R=mk.rot_x(math.pi / 2)))
    return mk.merge(*tower), mk.merge(*rotor), (0.0, H + 0.36, 0.45)


# ------------------------------------------------------------------ export
def build(rng_for):
    A = {}
    r = rng_for(61)
    A["Cactus_Saguaro_01"] = (saguaro(r, 3.2, 0.28, ((0.42, 0.9, 0.95, 1), (0.58, 0.8, 0.75, -1)), flowers=2), 900, 0.18)
    A["Cactus_Saguaro_02"] = (saguaro(r, 4.3, 0.33, ((0.36, 1.0, 1.25, 1), (0.52, 0.95, 1.0, -1), (0.68, 0.7, 0.6, 1)), flowers=3), 700, 0.22)
    A["Cactus_Saguaro_Small_01"] = (saguaro(r, 1.7, 0.21, ((0.45, 0.55, 0.45, 1),), flowers=1), 1200, 0.14)
    A["Cactus_Barrel_01"] = (barrel_cactus(r), 2500, 0.08)
    A["Cactus_Prickly_01"] = (prickly_pear(r), 2000, 0.10)
    r = rng_for(62)
    A["Desert_Agave_01"] = (agave(r), 3000, 0.06)
    A["Desert_Scrub_01"] = (scrub(r), 2500, 0.08)
    A["Desert_GrassTuft_01"] = (desert_tuft(r), 3000, 0.06)
    r = rng_for(63)
    A["Rock_Desert_Small_01"] = (desert_rock(0.7, r), 1500, 0.15)
    A["Rock_Desert_Medium_01"] = (desert_rock(1.4, r, 0.7), 900, 0.20)
    A["Rock_Desert_Large_01"] = (desert_rock(2.8, r, 0.66), 300, 0.35)
    A["Rock_Desert_Boulder_01"] = (boulder(r, 1.0), 1200, 0.15)
    A["Hoodoo_01"] = (hoodoo(r, 7.0, 5, 1.4), 150, 0.40)
    A["Hoodoo_02"] = (hoodoo(r, 10.5, 6, 1.9), 100, 0.45)
    A["Rock_Arch_01"] = (rock_arch(r), 40, 0.60)
    r = rng_for(64)
    A["Mesa_Back_01"] = (mesa_back(56, 26, r, 2), 0.6, 4.0)
    A["Mesa_Back_02"] = (mesa_back(76, 20, r, 1), 0.4, 4.0)
    A["Mesa_Back_03"] = (mesa_back(40, 32, r, 2, 8), 0.5, 4.0)
    r = rng_for(65)
    A["Skull_Bull_01"] = (bull_skull(r), 3000, 0.06)
    A["Tumbleweed_01"] = (tumbleweed(r), 1500, 0.08)
    A["Mine_Entrance_01"] = (mine_entrance(r), 120, 0.40)
    A["MineCart_Gold_01"] = (mine_cart(r), 1500, 0.12)
    A["Mine_Crane_01"] = (mine_crane(r), 600, 0.20)
    A["Sign_BisonCrossing_01"] = (sign_bison(r), 1500, 0.10)
    A["Sign_Chevron_01"] = (sign_chevron(r), 1500, 0.10)
    A["Flag_Pennant_01"] = (flag_pennant(r), 1500, 0.10)
    return A


def main(only=None):
    env.MATERIALS.update(MATERIALS)
    os.makedirs(EXP_DIR, exist_ok=True)
    unity_assets.folder_meta(ROOT, MAT_DIR)
    os.makedirs(os.path.join(ROOT, DESERT_DIR), exist_ok=True)
    unity_assets.folder_meta(ROOT, DESERT_DIR)
    guids = {n: unity_assets.material(ROOT, f"{MAT_DIR}/{n}.mat", n, b, sm, None, op.get("opacity"), ramp, sh, op)
             for n, (b, ramp, sm, sh, op) in env.MATERIALS.items() if n in MATERIALS or n == "M_Env_FlowerHeart"}
    rng_for = lambda k: np.random.default_rng([2026, k])  # noqa: E731
    A = build(rng_for)
    if only:
        A = {k: v for k, v in A.items() if any(o in k for o in only)}
    manifest_path = os.path.join(EXP_DIR, "DesertKit.json")
    manifest = json.load(open(manifest_path, encoding="utf-8"))["assets"] if only and os.path.exists(manifest_path) else {}

    def write(name, sc, V):
        fbx_rel = f"{DESERT_DIR}/{name}.fbx"
        sc.write_fbx_ascii(os.path.join(ROOT, fbx_rel))
        sc.write_glb(os.path.join(EXP_DIR, name + ".glb"))
        unity_assets.model_meta(ROOT, fbx_rel, {m: guids[m] for m in sc.materials})
        manifest[name] = {"fbx": fbx_rel, "tris": sc.stats()["tris"], "min": np.round(V.min(0), 3).tolist(),
                          "max": np.round(V.max(0), 3).tolist(), "materials": sorted(sc.materials)}
        print(f"{name:26s} {sc.stats()['tris']:6d} tris  {np.round(V.min(0), 2)} .. {np.round(V.max(0), 2)}")

    for name, (mesh, dens, rad) in A.items():
        sc = env.make_scene(name, mesh, dens, rad, gradient=0.25)
        write(name, sc, mesh.V)

    if not only or any(o in "Windmill_01" for o in only):
        tower, rotor, hub = windmill_parts(rng_for(66))
        sc = mk.Scene("Windmill_01")
        for mname in sorted(set(tower.M) | set(rotor.M)):
            b, ramp, sm, sh, op = env.MATERIALS[mname]
            sc.material(mname, b, ramp, sm, ramp_shift=sh, opts=dict(op))
        sc.add(mk.Part("Windmill_01", None))
        sc.add(mk.Part("Tower", tower, (0, 0, 0), "Windmill_01", gradient=0.25))
        sc.add(mk.Part("Rotor", rotor, hub, "Windmill_01", gradient=0.0))
        sc.facet_all()
        sc.bake_shading(density=500, radius=0.25, noise_freq=0.8, edge_range=(3.0, 12.0), max_samples=40000)
        write("Windmill_01", sc, np.vstack([tower.V, rotor.V + np.asarray(hub)]))

    json.dump({"assets": manifest, "materials": {n: {"base": b, "ramp": ramp, "rampShift": sh, "smoothness": sm, "opts": op}
                                                 for n, (b, ramp, sm, sh, op) in MATERIALS.items()}},
              open(manifest_path, "w", encoding="utf-8"), indent=2)


if __name__ == "__main__":
    main(sys.argv[1:] or None)
