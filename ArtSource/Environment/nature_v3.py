"""Natuurkit v3 (#240), waterprops (#241) en winterkit (#242) — zelfde stijl als build_environment_kit.py:
chunky, ronde low-poly volumes, kleuren alleen uit palette.json, pivot midden-onder, Y omhoog, 1 unit = 1 m.

Elke functie geeft één mk.Mesh terug (materialen per vlak). Alles is een gesloten volume: geen losse vlakken, dus
geen dubbelzijdigheid nodig en geen z-fighting (onderdelen steken in elkaar of liggen >= 6 mm uit elkaar).

  Trees/   Tree_Oak_01, Tree_Oak_Autumn_01, Tree_Birch_01, Tree_Birch_Autumn_01, Tree_Willow_01,
           Bush_Round_02, Bush_Berry_01, Bush_Blossom_01, Bush_Hedge_01
  Plants/  Fern_01, Fern_02, Mushroom_Red_01, Mushroom_Cluster_01, FlowerBed_Round_01, FlowerBed_Long_01,
           Flower_Tulips_01, Flower_Wild_01
  Wood/    Log_Fallen_01 (4 m, met mos en paddenstoelen), Log_Fallen_02 (2,2 m), Stump_01
  Water/   Lily_Pads_01, Lily_Pad_Single, Reeds_01, Cattails_01, Duck_01, Duck_02, Jetty_01, Rowboat_01
  Winter/  Tree_Pine_Snow_Small/Medium/Large, Rock_Snow_Medium/Large, Snow_Drift_01/02, Snow_Pile_Track,
           Snowman_01, Cabin_01, Icicles_2m, Frozen_Lake_01
"""
import math

import numpy as np

import meshkit as mk

DUCK_EYE = "M_Env_Dark"


def materials(R):
    """Extra materialen (naam -> (basis, ramp, smoothness, shift, opts)) in de vorm van build_environment_kit.MATERIALS."""
    leaf = {"cells": 0.6, "cellSize": 0.6, "rim": 0.30}
    flat = {"cells": 0.0, "rim": 0.2}
    return {
        "M_Env_LeafOak": (R["grass"][2], R["grass"], 0.05, 0.04, dict(leaf)),
        "M_Env_LeafAutumn": (R["crate"][2], R["crate"], 0.05, 0.0, dict(leaf)),
        "M_Env_LeafAutumnRed": (R["roof_terracotta"][3], R["roof_terracotta"], 0.05, 0.05, dict(leaf)),
        "M_Env_LeafBirch": (R["dino_green"][2], R["dino_green"], 0.05, 0.0, dict(leaf)),
        "M_Env_LeafBirchAutumn": (R["accent_yellow"][2], R["accent_yellow"], 0.05, -0.05, dict(leaf)),
        "M_Env_Willow": (R["dino_green"][1], R["dino_green"], 0.05, -0.12, {"cells": 0.5, "cellSize": 0.5, "rim": 0.3}),
        "M_Env_BirchBark": (R["wool"][3], R["wool"], 0.05, 0.0, {"cells": 0.3}),
        "M_Env_Dark": (R["ui_bg"][2], R["ui_bg"], 0.05, 0.0, {"cells": 0.0}),
        "M_Env_Moss": (R["dino_green"][1], R["dino_green"], 0.05, -0.25, {"cells": 0.6, "cellSize": 0.4}),
        "M_Env_LogCut": (R["track_wood"][3], R["track_wood"], 0.05, 0.0, {"cells": 0.0}),
        "M_Env_MushroomRed": (R["title_red"][2], R["title_red"], 0.05, 0.0, dict(flat)),
        "M_Env_MushroomBrown": (R["leather"][3], R["leather"], 0.05, 0.0, dict(flat)),
        "M_Env_MushroomStem": (R["scarf_cream"][3], R["scarf_cream"], 0.05, 0.0, dict(flat)),
        "M_Env_Fern": (R["grass"][1], R["grass"], 0.05, -0.08, {"cells": 0.4, "cellSize": 0.4}),
        "M_Env_Tulip": (R["shirt_coral"][2], R["shirt_coral"], 0.05, 0.0, dict(flat)),
        "M_Env_TulipYellow": (R["helmet"][2], R["helmet"], 0.05, 0.0, dict(flat)),
        "M_Env_FlowerPink": (R["haze_purple"][3], R["haze_purple"], 0.05, 0.05, dict(flat)),
        "M_Env_FlowerBlue": (R["backpack_teal"][4], R["backpack_teal"], 0.05, 0.15, dict(flat)),
        "M_Env_Berry": (R["hoodie_red"][2], R["hoodie_red"], 0.10, 0.0, dict(flat)),
        "M_Env_LilyPad": (R["dino_green"][1], R["dino_green"], 0.10, -0.05, {"cells": 0.3}),
        "M_Env_LilyFlower": (R["haze_purple"][4], R["haze_purple"], 0.05, 0.10, dict(flat)),
        "M_Env_Reed": (R["shorts_olive"][3], R["shorts_olive"], 0.05, 0.05, {"cells": 0.0}),
        "M_Env_Cattail": (R["leather"][1], R["leather"], 0.05, 0.0, {"cells": 0.0}),
        "M_Env_DuckWhite": (R["wool"][4], R["wool"], 0.05, 0.0, {"cells": 0.0, "rim": 0.25}),
        "M_Env_DuckBrown": (R["leather"][4], R["leather"], 0.05, 0.05, {"cells": 0.0, "rim": 0.25}),
        "M_Env_DuckGreen": (R["pine"][4], R["pine"], 0.10, 0.10, {"cells": 0.0, "rim": 0.25}),
        "M_Env_Beak": (R["helmet"][1], R["helmet"], 0.05, 0.0, {"cells": 0.0}),
        "M_Env_Plank": (R["track_wood"][1], R["track_wood"], 0.05, -0.05, {"cells": 0.4, "cellSize": 0.5}),
        "M_Env_PlankDark": (R["interior_wood"][1], R["interior_wood"], 0.05, -0.10, {"cells": 0.4, "cellSize": 0.5}),
        "M_Env_BoatRed": (R["title_red"][2], R["title_red"], 0.10, 0.0, {"cells": 0.2}),
        "M_Env_BoatCream": (R["scarf_cream"][3], R["scarf_cream"], 0.10, 0.0, {"cells": 0.2}),
        "M_Env_SnowSoft": (R["cloud_snow"][4], R["cloud_snow"], 0.05, 0.10, {"cells": 0.3, "cellSize": 0.8, "rim": 0.20}),
        "M_Env_Ice": (R["water"][4], R["water"], 0.40, 0.30, {"cells": 0.3, "cellSize": 1.5, "rim": 0.35}),
        "M_Env_IceDark": (R["water"][1], R["water"], 0.40, 0.0, {"cells": 0.3, "cellSize": 2.0, "rim": 0.25}),
        "M_Env_Carrot": (R["crate"][3], R["crate"], 0.05, 0.0, dict(flat)),
        "M_Env_Scarf": (R["hoodie_red"][2], R["hoodie_red"], 0.05, 0.0, {"cells": 0.0}),
        "M_Env_Log": (R["interior_wood"][2], R["interior_wood"], 0.05, -0.02, {"cells": 0.5, "cellSize": 0.5}),
        "M_Env_Chimney": (R["cliff"][2], R["cliff"], 0.05, -0.10, {"cells": 0.6, "cellSize": 0.3}),
        "M_Env_Window": (R["accent_yellow"][4], R["accent_yellow"], 0.05, 0.15, {"cells": 0.0, "emissive": 0.55}),
    }


# ------------------------------------------------------------------ helpers
def blob(rx, ry, rz, rng, amp=0.12, seg=7, rings=4, mat="Default", flat_bottom=None):
    m = mk.ellipsoid(rx, ry, rz, seg, rings, mat=mat)
    keys = {}
    V = m.V.copy()
    for i, p in enumerate(V):
        key = tuple(np.round(p, 5))
        if key not in keys:
            keys[key] = rng.normal(0, amp, 3) * np.array([rx, ry, rz])
        V[i] = p + keys[key]
    if flat_bottom is not None:
        V[:, 1] = np.maximum(V[:, 1], flat_bottom)
    m.V = V
    m.sharp = True
    return m


def lathe(profile, seg, mat, phase=0.0, rmod=None):
    V, F, rings = [], [], []
    for i, (r, y) in enumerate(profile):
        if r <= 1e-9:
            rings.append([len(V)])
            V.append((0.0, y, 0.0))
            continue
        idx = []
        for k in range(seg):
            a = phase + 2 * math.pi * k / seg
            rr = r * (rmod(i, k) if rmod else 1.0)
            idx.append(len(V))
            V.append((rr * math.cos(a), y, rr * math.sin(a)))
        rings.append(idx)
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for k in range(seg):
            k2 = (k + 1) % seg
            if len(r0) == 1:
                F.append((r0[0], r1[k2], r1[k]))
            elif len(r1) == 1:
                F.append((r0[k], r0[k2], r1[0]))
            else:
                F.append((r0[k], r0[k2], r1[k2], r1[k]))
    m = mk.Mesh(np.array(V, float), F, [mat] * len(F))
    m.sharp = True
    return mk._orient_closed(m)


def limb(p0, p1, r0, r1, mat, seg=6):
    """Tak/stam tussen twee punten: zeskantige afgeknotte kegel met dichte uiteinden."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0
    L = float(np.linalg.norm(d))
    m = lathe([(0.0, -0.001), (r0, 0.0), (r1, L), (0.0, L + 0.001)], seg, mat)
    y = d / L
    x = np.cross(y, [0, 0, 1.0]) if abs(y[2]) < 0.9 else np.cross(y, [1.0, 0, 0])
    x /= np.linalg.norm(x)
    z = np.cross(x, y)
    R = np.stack([x, y, z], axis=1)
    return m.transform(R=R, t=tuple(p0))


def blade(h, w, bend, mat, rng=None):
    """Smal blad/halm: driekantige spits, iets gebogen in +X."""
    b = lathe([(w, 0.0), (w * 0.7, h * 0.5), (0.0, h)], 3, mat)
    b.V[:, 0] += (b.V[:, 1] / h) ** 2 * bend
    return b


def scatter(rng, n, radius, min_d=0.0):
    pts = []
    tries = 0
    while len(pts) < n and tries < 2000:
        tries += 1
        a, r = rng.uniform(0, 2 * math.pi), radius * math.sqrt(rng.uniform(0, 1))
        p = (r * math.cos(a), r * math.sin(a))
        if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 >= min_d ** 2 for q in pts):
            pts.append(p)
    return pts


# --------------------------------------------------------------- loofbomen
def crown(rng, centers, mat, amp=0.10):
    """Kruin van ronde klonten: (x, y, z, r) per klont, een tikje platgedrukt."""
    return [blob(r, r * 0.86, r, rng, amp, seg=8, rings=5, mat=mat).transform(t=(x, y, z)) for (x, y, z, r) in centers]


def oak(rng, leaf="M_Env_LeafOak", height=5.2):
    """Ronde eik: korte dikke stam die zich in drie takken splitst, brede bolle kruin uit 7 klonten."""
    tr = "M_Env_Trunk"
    h0 = height * 0.36
    parts = [limb((0, -0.15, 0), (0, h0, 0), 0.34, 0.26, tr, 7),
             lathe([(0.0, -0.1), (0.52, -0.1), (0.36, 0.25), (0.0, 0.3)], 7, tr)]          # wortelvoet
    tips = []
    for k in range(3):
        a = 2 * math.pi * k / 3 + rng.uniform(-0.3, 0.3)
        tip = (math.cos(a) * 0.9, h0 + 1.1 + rng.uniform(-0.2, 0.2), math.sin(a) * 0.9)
        parts.append(limb((0, h0 - 0.1, 0), tip, 0.20, 0.11, tr, 6))
        tips.append(tip)
    cy = h0 + 1.75
    centers = [(0, cy + 0.55, 0, 1.45)]
    for (x, y, z) in tips:
        centers.append((x * 1.15, cy + rng.uniform(-0.15, 0.2), z * 1.15, rng.uniform(1.05, 1.25)))
    for k in range(3):
        a = 2 * math.pi * (k + 0.5) / 3 + rng.uniform(-0.3, 0.3)
        centers.append((math.cos(a) * 1.2, cy - 0.35, math.sin(a) * 1.2, rng.uniform(0.85, 1.0)))
    return mk.merge(*parts, *crown(rng, centers, leaf))


def birch(rng, leaf="M_Env_LeafBirch", height=6.0):
    """Berk: slanke witte stam met donkere vlekbanden, smalle hoge kruin uit gestapelde klonten."""
    parts = [limb((0, -0.15, 0), (0.05, height * 0.82, 0.02), 0.17, 0.08, "M_Env_BirchBark", 7)]
    for k in range(6):                                   # donkere bandjes: iets dikker dan de stam (steken eruit)
        y = 0.45 + k * height * 0.11 + rng.uniform(-0.1, 0.1)
        f = y / (height * 0.82)
        r = 0.17 + (0.08 - 0.17) * f + 0.012
        band = lathe([(0.0, y - 0.03), (r, y - 0.03), (r, y + 0.03 + rng.uniform(0, 0.04)), (0.0, y + 0.06)], 7,
                     "M_Env_Dark", phase=rng.uniform(0, 1),
                     rmod=lambda i, kk: 1.0 if kk % 3 else 0.6)
        parts.append(band.transform(t=(0.05 * f, 0, 0.02 * f)))
    for s in (-1, 1):                                    # twee zijtakjes
        y = height * rng.uniform(0.42, 0.55)
        parts.append(limb((0.03, y, 0), (s * 0.75, y + 0.7, rng.uniform(-0.3, 0.3)), 0.06, 0.03, "M_Env_BirchBark", 5))
    base = height * 0.45
    centers = [(0.0, base + 0.5, 0.0, 0.95), (0.25, base + 1.35, -0.1, 0.85), (-0.15, base + 2.15, 0.1, 0.72),
               (0.05, base + 2.85, 0.0, 0.52), (-0.55, base + 0.95, 0.25, 0.62), (0.6, base + 0.7, 0.3, 0.6)]
    return mk.merge(*parts, *crown(rng, centers, leaf, 0.12))


def willow(rng, height=5.0):
    """Treurwilg: knoestige scheve stam, bolle kruin en een gordijn van hangende twijgen (gekartelde rokken)."""
    tr = "M_Env_Trunk"
    parts = [limb((0, -0.15, 0), (0.25, height * 0.45, 0.1), 0.36, 0.24, tr, 7),
             limb((0.25, height * 0.45, 0.1), (-0.2, height * 0.62, 0.0), 0.24, 0.16, tr, 6),
             lathe([(0.0, -0.1), (0.55, -0.1), (0.38, 0.3), (0.0, 0.35)], 7, tr)]
    top = height * 0.78
    parts += crown(rng, [(0, top, 0, 1.55), (0.6, top - 0.3, 0.4, 1.1), (-0.7, top - 0.2, -0.3, 1.1)], "M_Env_Willow", 0.08)
    # hangende twijgen: smalle 'lamellen' (dunne gebogen platen) rondom, onderrand gekarteld
    n = 22
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.08, 0.08)
        r = rng.uniform(1.55, 1.85)
        y0 = top - 0.1
        y1 = rng.uniform(0.7, 1.3)
        w = 0.32
        strand = lathe([(0.0, y1), (w * 0.4, y1 + 0.2), (w, (y0 + y1) / 2), (w * 0.8, y0), (0.0, y0 + 0.1)], 4, "M_Env_Willow",
                       phase=math.pi / 4)
        strand.V[:, 2] *= 0.35                                   # plat: een lamel, geen kegel
        strand.V[:, 0] += (y0 - strand.V[:, 1]) * -0.08          # onderkant buigt iets naar binnen
        parts.append(strand.transform(R=mk.rot_y(-a + math.pi / 2), t=(math.cos(a) * r, 0, math.sin(a) * r)))
    return mk.merge(*parts)


# --------------------------------------------------------------- struiken
def bush_round(rng, mat="M_Env_Bush", r=0.62):
    return mk.merge(*[blob(r * f, r * f * 0.85, r * f, rng, 0.10, seg=8, rings=5, mat=mat, flat_bottom=-0.25).transform(t=(x, y, z))
                      for (x, y, z, f) in [(0, r * 0.75, 0, 1.0), (r * 0.7, r * 0.5, r * 0.3, 0.7), (-r * 0.65, r * 0.45, -r * 0.2, 0.72),
                                           (r * 0.1, r * 1.35, -r * 0.1, 0.6)]])


def dots_on(rng, mesh, n, r, mat, min_y=0.25, seg=6):
    """Kleine bolletjes (bessen/bloesem) op het buitenoppervlak van een mesh (op hoekpunten, iets naar buiten)."""
    V = mesh.V
    c = V.mean(0)
    cand = [v for v in V if v[1] > min_y]
    out = []
    for i in rng.choice(len(cand), size=min(n, len(cand)), replace=False):
        v = cand[i]
        d = v - c
        d[1] *= 0.3
        d /= max(1e-6, np.linalg.norm(d))
        out.append(mk.ellipsoid(r, r, r, seg, 4, mat=mat).transform(t=tuple(v + d * r * 0.3)))
    return out


def bush_berry(rng):
    b = bush_round(rng, "M_Env_Bush", 0.55)
    return mk.merge(b, *dots_on(rng, b, 22, 0.05, "M_Env_Berry"))


def bush_blossom(rng):
    b = bush_round(rng, "M_Env_LeafBirch", 0.6)
    return mk.merge(b, *dots_on(rng, b, 26, 0.075, "M_Env_FlowerPink", seg=5))


def hedge(rng, L=2.4):
    lumps = []
    n = int(L / 0.5)
    for k in range(n):
        x = -L / 2 + 0.3 + k * (L - 0.6) / max(1, n - 1)
        lumps.append(blob(0.42, 0.45, 0.38, rng, 0.08, seg=8, rings=5, mat="M_Env_Bush", flat_bottom=-0.4).transform(t=(x, 0.48, 0)))
    return mk.merge(*lumps)


# ------------------------------------------------------ varens, paddenstoelen
def frond(rng, length, mat="M_Env_Fern", leaflets=8):
    """Varenblad: boogvormige nerf (omhoog en weer omlaag) met brede paarsgewijze blaadjes die naar de punt kleiner worden."""
    parts = []
    pts = []
    for i in range(leaflets + 2):
        t = i / (leaflets + 1)
        pts.append(np.array([0.0, math.sin(t * math.pi * 0.85) * length * 0.45, t * length]))
    for p0, p1 in zip(pts[:-1], pts[1:]):
        parts.append(limb(p0, p1, 0.022, 0.016, mat, 4))
    for i in range(1, leaflets + 1):
        t = i / (leaflets + 1)
        p = pts[i]
        s = (1.0 - t * 0.7) * length * 0.20
        for side in (-1, 1):
            leaf = mk.ellipsoid(s, 0.022, s * 0.36, 6, 3, mat=mat)
            parts.append(leaf.transform(R=mk.rot_y(side * 0.55) @ mk.rot_z(side * 0.18), t=(side * s * 0.85, p[1] - 0.012, p[2] + s * 0.25)))
    return mk.merge(*parts)


def fern(rng, n=9, size=0.75):
    parts = [blob(0.14, 0.10, 0.14, rng, 0.1, mat="M_Env_Moss", flat_bottom=0.0)]
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.25, 0.25)
        L = size * rng.uniform(0.8, 1.1)
        f = frond(rng, L).transform(R=mk.rot_x(rng.uniform(-0.05, 0.25)))
        parts.append(f.transform(R=mk.rot_y(-a + math.pi / 2), t=(0, 0.05, 0)))
    return mk.merge(*parts)


def mushroom(rng, h, r, cap="M_Env_MushroomRed", dots=True, tilt=0.0):
    stem = lathe([(0.0, 0.0), (r * 0.42, 0.0), (r * 0.30, h * 0.5), (r * 0.34, h), (0.0, h + 0.01)], 7, "M_Env_MushroomStem")
    capm = lathe([(0.0, h - 0.02), (r * 0.5, h - 0.03), (r, h + r * 0.12), (r * 0.82, h + r * 0.55), (r * 0.4, h + r * 0.78),
                  (0.0, h + r * 0.82)], 9, cap)
    parts = [stem, capm]
    if dots:
        for k in range(6):
            a = 2 * math.pi * k / 6 + rng.uniform(-0.3, 0.3)
            rr = r * rng.uniform(0.45, 0.7)
            y = h + r * (0.78 - 0.55 * (rr / r) ** 2) + 0.004
            parts.append(mk.ellipsoid(r * 0.12, r * 0.05, r * 0.12, 6, 3, mat="M_Env_Petal").transform(t=(math.cos(a) * rr, y, math.sin(a) * rr)))
    m = mk.merge(*parts)
    return m.transform(R=mk.rot_z(tilt)) if tilt else m


def mushroom_cluster(rng):
    parts = []
    for (x, z, h, r) in [(0.0, 0.0, 0.16, 0.10), (0.13, 0.05, 0.11, 0.07), (-0.10, 0.09, 0.09, 0.06), (0.05, -0.12, 0.07, 0.05),
                         (-0.14, -0.08, 0.13, 0.08)]:
        parts.append(mushroom(rng, h, r, "M_Env_MushroomBrown", dots=False, tilt=rng.uniform(-0.2, 0.2)).transform(
            R=mk.rot_y(rng.uniform(0, 6.28)), t=(x, 0, z)))
    return mk.merge(*parts)


# ----------------------------------------------------------- bloemenperken
def tulip(rng, mat, h=None):
    h = h or rng.uniform(0.28, 0.40)
    stem = limb((0, 0, 0), (rng.uniform(-0.02, 0.02), h, rng.uniform(-0.02, 0.02)), 0.010, 0.008, "M_Env_Stem", 4)
    cup = lathe([(0.0, h - 0.01), (0.035, h), (0.045, h + 0.04), (0.038, h + 0.08), (0.0, h + 0.065)], 6, mat,
                rmod=lambda i, k: 1.0 if k % 2 == 0 else 0.85)
    leaf = blade(h * 0.6, 0.03, 0.05, "M_Env_Stem").transform(R=mk.rot_y(rng.uniform(0, 6.28)))
    return mk.merge(stem, cup, leaf)


def flower(rng, mat):
    h = rng.uniform(0.16, 0.30)
    stem = limb((0, 0, 0), (0, h, 0), 0.009, 0.007, "M_Env_Stem", 4)
    head = mk.ellipsoid(0.045, 0.028, 0.045, 7, 3, mat=mat).transform(t=(0, h + 0.01, 0))
    heart = mk.ellipsoid(0.018, 0.012, 0.018, 6, 3, mat="M_Env_FlowerHeart").transform(t=(0, h + 0.03, 0))
    return mk.merge(stem, head, heart).transform(R=mk.rot_z(rng.uniform(-0.2, 0.2)))


def flower_bed_round(rng, R0=0.75):
    """Rond verhoogd perkje: aarden bult met een rand van keitjes, tulpen in het midden en bloemen eromheen."""
    parts = [lathe([(0.0, 0.0), (R0, 0.0), (R0 * 0.96, 0.10), (R0 * 0.7, 0.16), (0.0, 0.19)], 12, "M_Env_Dirt")]
    n = 14
    for k in range(n):
        a = 2 * math.pi * k / n
        parts.append(blob(0.11, 0.08, 0.10, rng, 0.12, seg=6, rings=3, mat="M_Env_PathStone" if k % 3 else "M_Env_PathStoneMid",
                          flat_bottom=-0.03).transform(R=mk.rot_y(-a), t=(math.cos(a) * R0, 0.04, math.sin(a) * R0)))
    for (x, z) in scatter(rng, 7, R0 * 0.62, 0.22):                      # bladgroen tussen de bloemen
        parts.append(blob(0.17, 0.12, 0.17, rng, 0.12, seg=6, rings=3, mat="M_Env_Bush", flat_bottom=-0.04).transform(
            t=(x, 0.23 - 0.10 * (x * x + z * z) / R0 ** 2, z)))
    for (x, z) in scatter(rng, 11, R0 * 0.38, 0.09):
        parts.append(tulip(rng, "M_Env_Tulip" if rng.random() < 0.6 else "M_Env_TulipYellow").transform(t=(x, 0.16, z)))
    for (x, z) in scatter(rng, 22, R0 * 0.80, 0.08):
        if x * x + z * z < (R0 * 0.42) ** 2:
            continue
        mat = ["M_Env_FlowerPink", "M_Env_FlowerBlue", "M_Env_Petal"][int(rng.integers(3))]
        y = 0.16 - 0.08 * (x * x + z * z) / R0 ** 2
        parts.append(flower(rng, mat).transform(t=(x, y, z)))
    return mk.merge(*parts)


def flower_bed_long(rng, L=2.4, D=0.6):
    """Lange houten bak (planten langs X) vol bloemen."""
    parts = [mk.rounded_box(L, 0.32, D, 0.03, div=1, mat="M_Env_Plank").transform(t=(0, 0.16, 0)),
             mk.rounded_box(L - 0.12, 0.06, D - 0.12, 0.02, div=1, mat="M_Env_Dirt").transform(t=(0, 0.31, 0))]
    for x in np.linspace(-L / 2 + 0.2, L / 2 - 0.2, 9):
        for z in (-D * 0.22, D * 0.22):
            xx = x + rng.uniform(-0.05, 0.05) + (0.12 if z > 0 else 0.0)
            if abs(xx) > L / 2 - 0.12:
                continue
            k = int(rng.integers(4))
            f = tulip(rng, "M_Env_Tulip") if k == 0 else flower(rng, ["M_Env_FlowerPink", "M_Env_TulipYellow", "M_Env_FlowerBlue"][k - 1])
            parts.append(f.transform(t=(xx, 0.33, z + rng.uniform(-0.04, 0.04))))
    return mk.merge(*parts)


def tulip_patch(rng):
    parts = []
    for (x, z) in scatter(rng, 11, 0.32, 0.09):
        parts.append(tulip(rng, ["M_Env_Tulip", "M_Env_TulipYellow", "M_Env_FlowerPink"][int(rng.integers(3))]).transform(t=(x, 0, z)))
    return mk.merge(*parts)


def wild_patch(rng, grass_tuft):
    parts = [grass_tuft(rng, 6, 0.30).transform(t=(x, 0, z)) for (x, z) in scatter(rng, 3, 0.4, 0.25)]
    for (x, z) in scatter(rng, 9, 0.45, 0.10):
        parts.append(flower(rng, ["M_Env_FlowerPink", "M_Env_FlowerBlue", "M_Env_TulipYellow", "M_Env_Petal"][int(rng.integers(4))]).transform(t=(x, 0, z)))
    return mk.merge(*parts)


# ----------------------------------------------------------- boomstammen
def log(rng, L=4.0, r=0.32, moss=True, shrooms=True, stub=True):
    """Omgevallen stam langs X: achtkantige bast, lichte zaagvlakken met jaarringen, mosplekken, takstomp."""
    seg = 10
    prof = [(0.0, -L / 2), (r * 0.98, -L / 2)]
    for j in range(7):                                    # ruwe bast: lichte knobbels over de lengte
        y = -L / 2 + 0.06 + j * (L - 0.12) / 6
        prof.append((r * (1.0 - 0.10 * (j / 6)) * (1.04 if j % 2 else 0.98), y))
    prof += [(r * 0.88, L / 2), (0.0, L / 2)]
    body = lathe(prof, seg, "M_Env_Trunk", rmod=lambda i, k: 1.0 + 0.07 * ((k * 7 + i * 3) % 3 - 1))
    # zaagvlakken: dunne lichte schijven die net uitsteken, met een donkerdere ring
    ends = []
    for s, rr in ((-1, r * 1.0), (1, r * 0.95)):
        y = s * (L / 2 + 0.012)
        ends.append(lathe([(0.0, y - s * 0.04), (rr, y - s * 0.04), (rr, y), (0.0, y)], seg, "M_Env_LogCut", phase=0.1))
        ends.append(lathe([(0.0, y - s * 0.006), (rr * 0.45, y - s * 0.006), (rr * 0.45, y + s * 0.008), (0.0, y + s * 0.008)], seg, "M_Env_Trunk"))
    parts = [body, *ends]
    if stub:
        parts.append(limb((0.3, 0, 0), (0.75, 0.55, 0.15), 0.11, 0.07, "M_Env_Trunk", 6))
    m = mk.merge(*parts).transform(R=mk.rot_z(math.pi / 2), t=(0, r * 0.88, 0))
    extra = []
    if moss:
        for x in (-L * 0.3, L * 0.12, L * 0.38):
            extra.append(blob(0.20, 0.08, 0.15, rng, 0.18, seg=7, rings=4, mat="M_Env_Moss").transform(
                t=(x + rng.uniform(-0.1, 0.1), r * 1.70, rng.uniform(-0.08, 0.08))))
    if shrooms:
        for k in range(3):
            x = -L * 0.15 + k * 0.16
            extra.append(mushroom(rng, 0.10, 0.10, "M_Env_MushroomBrown", dots=False).transform(
                R=mk.rot_x(1.25), t=(x, r * 0.9, r * 0.82)))
    return mk.merge(m, *extra)


def stump(rng, r=0.36, h=0.42):
    body = lathe([(0.0, -0.05), (r * 1.45, -0.05), (r * 1.1, 0.12), (r, h * 0.6), (r * 0.98, h), (0.0, h)], 8, "M_Env_Trunk",
                 rmod=lambda i, k: 1.0 + (0.18 if (i <= 1 and k % 2 == 0) else 0.0))
    top = lathe([(0.0, h - 0.02), (r * 0.9, h - 0.02), (r * 0.9, h + 0.008), (0.0, h + 0.008)], 8, "M_Env_LogCut")
    ring = lathe([(0.0, h + 0.008), (r * 0.42, h + 0.008), (r * 0.42, h + 0.016), (0.0, h + 0.016)], 8, "M_Env_Trunk")
    moss = blob(0.24, 0.06, 0.18, rng, 0.15, seg=6, rings=3, mat="M_Env_Moss").transform(t=(r * 0.9, h * 0.45, 0.1))
    return mk.merge(body, top, ring, moss, mushroom(rng, 0.09, 0.07, dots=True).transform(t=(-r * 1.15, 0, 0.25)))


# ----------------------------------------------------------------- water
def lily_pad(rng, r):
    """Blad met een inkeping: platte schijf waar een wigje uit is (geen wig = volle cirkel minus 40 graden)."""
    seg = 12
    notch = 0.7
    V = [(0.0, 0.0, 0.0), (0.0, 0.03, 0.0)]
    ring_lo, ring_hi = [], []
    for k in range(seg + 1):
        a = notch / 2 + (2 * math.pi - notch) * k / seg
        rr = r * (1.0 + 0.04 * math.sin(k * 2.3))
        ring_lo.append(len(V)); V.append((rr * math.cos(a), 0.0, rr * math.sin(a)))
        ring_hi.append(len(V)); V.append((rr * math.cos(a) * 0.98, 0.03, rr * math.sin(a) * 0.98))
    F = []
    for k in range(seg):
        F.append((1, ring_hi[k], ring_hi[k + 1]))
        F.append((0, ring_lo[k + 1], ring_lo[k]))
        F.append((ring_lo[k], ring_lo[k + 1], ring_hi[k + 1], ring_hi[k]))
    F.append((0, ring_lo[0], ring_hi[0], 1))
    F.append((0, 1, ring_hi[-1], ring_lo[-1]))
    m = mk.Mesh(np.array(V, float), F, ["M_Env_LilyPad"] * len(F))
    m.sharp = True
    return mk._orient_closed(m).transform(R=mk.rot_y(rng.uniform(0, 6.28)))


def lily_flower(rng, s=1.0):
    parts = [mk.ellipsoid(0.03 * s, 0.025 * s, 0.03 * s, 6, 3, mat="M_Env_FlowerHeart").transform(t=(0, 0.07 * s, 0))]
    for ring, (n, tilt, rr) in enumerate([(6, 0.7, 0.06), (6, 1.0, 0.045)]):
        for k in range(n):
            a = 2 * math.pi * (k + ring * 0.5) / n
            pet = mk.ellipsoid(0.055 * s, 0.018 * s, 0.026 * s, 6, 3, mat="M_Env_LilyFlower")
            parts.append(pet.transform(R=mk.rot_y(-a) @ mk.rot_z(tilt), t=(math.cos(a) * rr * s, 0.05 * s, math.sin(a) * rr * s)))
    return mk.merge(*parts)


def lily_pads(rng):
    parts = []
    for k, (x, z, r) in enumerate([(0.0, 0.0, 0.32), (0.55, 0.18, 0.24), (-0.35, 0.45, 0.22), (0.2, -0.5, 0.28), (-0.5, -0.25, 0.18)]):
        parts.append(lily_pad(rng, r).transform(t=(x, k * 0.008, z)))        # elk blad een fractie hoger: nooit in één vlak
    parts.append(lily_flower(rng).transform(t=(0.05, 0.02, 0.02)))
    parts.append(lily_flower(rng, 0.8).transform(t=(-0.33, 0.02, 0.43)))
    return mk.merge(*parts)


def reeds(rng, n=16, h=1.1, cattails=False):
    parts = []
    for (x, z) in scatter(rng, n, 0.35, 0.05):
        hh = h * rng.uniform(0.7, 1.15)
        b = blade(hh, 0.03, rng.uniform(-0.12, 0.12), "M_Env_Reed").transform(R=mk.rot_y(rng.uniform(0, 6.28)), t=(x, -0.05, z))
        parts.append(b)
    if cattails:
        for (x, z) in scatter(rng, 6, 0.25, 0.08):
            hh = h * rng.uniform(1.0, 1.25)
            parts.append(limb((x, -0.05, z), (x, hh, z), 0.012, 0.010, "M_Env_Reed", 4))
            parts.append(lathe([(0.0, hh - 0.30), (0.035, hh - 0.27), (0.04, hh - 0.08), (0.0, hh - 0.04)], 6,
                               "M_Env_Cattail").transform(t=(x, 0, z)))
    return mk.merge(*parts)


def duck(rng, body="M_Env_DuckWhite", head=None):
    """Eendje (drijvend): bolle romp met opgewipt staartje, ronde kop, platte oranje snavel, kraaloogjes. Pivot op de
    waterlijn, voorkant +Z."""
    head = head or body
    parts = [mk.ellipsoid(0.16, 0.11, 0.22, 10, 6, mat=body).transform(t=(0, 0.06, 0)),
             mk.ellipsoid(0.08, 0.06, 0.10, 8, 4, mat=body).transform(R=mk.rot_x(0.6), t=(0, 0.13, -0.19)),
             mk.ellipsoid(0.10, 0.10, 0.10, 10, 6, mat=head).transform(t=(0, 0.24, 0.15)),
             mk.ellipsoid(0.055, 0.022, 0.07, 8, 4, mat="M_Env_Beak").transform(t=(0, 0.21, 0.26))]
    for s in (-1, 1):
        parts.append(mk.ellipsoid(0.10, 0.06, 0.15, 8, 4, mat=body).transform(R=mk.rot_y(s * 0.1), t=(s * 0.12, 0.09, -0.02)))
        parts.append(mk.ellipsoid(0.02, 0.026, 0.02, 6, 4, mat=DUCK_EYE).transform(t=(s * 0.062, 0.27, 0.225)))
    return mk.merge(*parts)


def jetty(rng, L=4.0, W=1.3):
    """Houten steiger langs +Z (begint op de pivot aan de wal), planken op twee liggers, palen het water in."""
    parts = []
    n = int(L / 0.22)
    for k in range(n):
        z = 0.11 + k * L / n
        parts.append(mk.rounded_box(W + rng.uniform(-0.04, 0.04), 0.06, L / n - 0.025, 0.012, div=1, mat="M_Env_Plank" if k % 4 else "M_Env_PlankDark")
                     .transform(R=mk.rot_y(rng.uniform(-0.02, 0.02)), t=(rng.uniform(-0.02, 0.02), 0.46, z)))
    for s in (-1, 1):
        parts.append(mk.rounded_box(0.12, 0.14, L, 0.02, div=1, mat="M_Env_PlankDark").transform(t=(s * (W / 2 - 0.15), 0.36, L / 2)))
        for z in np.linspace(0.25, L - 0.15, 4):
            parts.append(limb((s * (W / 2 - 0.05), -1.2, z), (s * (W / 2 - 0.05), 0.62, z), 0.085, 0.08, "M_Env_PlankDark", 6))
    return mk.merge(*parts)


def rowboat(rng, L=2.6, W=1.05):
    """Roeibootje: rode romp met crème rand, twee bankjes, twee riemen. Pivot op de waterlijn, lengte langs Z."""
    rings = 9
    V, F, M = [], [], []
    stations = np.linspace(-1, 1, rings)
    secs = []
    for t in stations:
        half = W / 2 * math.cos(t * math.pi / 2) ** 0.55
        z = t * L / 2
        lift = 0.12 * t ** 4
        sec = [(-half, 0.36 + lift), (-half * 0.92, 0.10 + lift * 0.5), (-half * 0.5, -0.12 + lift * 0.3), (0.0, -0.16 + lift * 0.3),
               (half * 0.5, -0.12 + lift * 0.3), (half * 0.92, 0.10 + lift * 0.5), (half, 0.36 + lift)]
        secs.append((z, sec))
    # buitenkant en (iets kleinere) binnenkant als één gesloten schaal
    def ring_pts(z, sec, inset):
        out = []
        for (x, y) in sec:
            out.append((x * (1 - inset), y + (inset * 0.6 if y < 0.3 else 0.0), z * (1 - inset * 0.6)))
        return out
    outer = [ring_pts(z, s, 0.0) for z, s in secs]
    inner = [ring_pts(z, s, 0.10) for z, s in secs]
    n = len(secs[0][1])
    def add_grid(grid, flip):
        base = len(V)
        for row in grid:
            V.extend(row)
        for i in range(len(grid) - 1):
            for j in range(n - 1):
                a, b, c, d = base + i * n + j, base + i * n + j + 1, base + (i + 1) * n + j + 1, base + (i + 1) * n + j
                F.append((a, d, c, b) if flip else (a, b, c, d))
                M.append("M_Env_BoatRed" if not flip else "M_Env_BoatCream")
        return base
    bo = add_grid(outer, False)
    bi = add_grid(inner, True)
    for i in range(len(secs) - 1):                       # bovenrand
        for j in (0, n - 1):
            a, b = bo + i * n + j, bo + (i + 1) * n + j
            c, d = bi + (i + 1) * n + j, bi + i * n + j
            F.append((a, b, c, d) if j == 0 else (a, d, c, b))
            M.append("M_Env_BoatCream")
    for i in (0, len(secs) - 1):                         # kop en spiegel dicht
        ring = [bo + i * n + j for j in range(n)] + [bi + i * n + j for j in reversed(range(n))]
        F.append(tuple(ring) if i == 0 else tuple(reversed(ring)))
        M.append("M_Env_BoatRed")
    m = mk.Mesh(np.array(V, float), F, M)
    m.sharp = True
    m = mk._orient_closed(m)
    parts = [m]
    for z in (-0.35, 0.45):
        parts.append(mk.rounded_box(W * 0.78, 0.05, 0.24, 0.015, div=1, mat="M_Env_Plank").transform(t=(0, 0.22, z)))
    for s in (-1, 1):
        parts.append(limb((s * 0.42, 0.40, 0.05), (s * 1.05, -0.02, 0.55), 0.025, 0.025, "M_Env_Plank", 5))
        parts.append(mk.ellipsoid(0.07, 0.015, 0.17, 6, 3, mat="M_Env_Plank").transform(
            R=mk.rot_y(s * 0.9), t=(s * 1.08, -0.04, 0.6)))
    return mk.merge(*parts)


# ----------------------------------------------------------------- winter
def pine_snow(height, tiers, width, rng):
    """Besneeuwde den: dezelfde opbouw als de natuurkit-den, elke laag met een gekartelde sneeuwkap erbovenop."""
    leaf, snow = "M_Env_PineLeaf", "M_Env_SnowSoft"
    trunk_h = height * 0.22
    tr = max(0.10, width * 0.11)
    parts = [lathe([(tr * 1.35, -0.15), (tr * 1.15, 0.04), (tr, trunk_h * 0.6), (tr * 0.85, trunk_h + 0.3), (0, trunk_h + 0.32)],
                   6, "M_Env_Trunk")]
    crown0 = trunk_h * 0.85
    span = height - crown0
    for t in range(tiers):
        f = t / max(1, tiers - 1)
        base = crown0 + span * 0.62 * (t / tiers)
        top = base + span * (0.56 - 0.12 * f) if t < tiers - 1 else height
        r = width / 2 * (1.0 - 0.22 * t) * rng.uniform(0.95, 1.05)
        h = top - base
        ph = rng.uniform(0, 2 * math.pi)
        prof = [(0.0, top), (r * 0.42, top - h * 0.36), (r * 0.86, base + h * 0.18), (r, base + h * 0.04),
                (r * 0.88, base - h * 0.04), (r * 0.36, base), (0.0, base + h * 0.06)]
        parts.append(lathe(prof, 8, leaf, ph, rmod=lambda i, k: 1.0 + (0.05 if (k % 2 == 0 and i in (2, 3, 4)) else -0.03)))
        # sneeuwkap: volgt de bovenste 55% van de kegel, 4 cm erboven; onderrand gekarteld (druipers)
        lift = 0.045
        cap = [(0.0, top + lift), (r * 0.44, top - h * 0.36 + lift * 0.9),
               (r * 0.72, top - h * 0.62 + lift * 0.8), (r * 0.62, top - h * 0.66), (r * 0.30, top - h * 0.30), (0.0, top - h * 0.1)]
        parts.append(lathe(cap, 8, snow, ph, rmod=lambda i, k: 1.0 + (0.10 if (k % 2 == 0 and i in (2, 3)) else 0.0)))
    return mk.merge(*parts)


def rock_snow(size, rng, squash=0.7):
    a = blob(size * 0.55, size * 0.50 * squash, size * 0.48, rng, 0.13, seg=7, rings=4, mat="M_Env_Rock")
    a.V[:, 1] -= a.V[:, 1].min() + size * 0.08
    top = a.V[:, 1].max()
    cap = blob(size * 0.46, size * 0.16, size * 0.40, rng, 0.08, seg=8, rings=4, mat="M_Env_SnowSoft")
    cap.V[:, 1] = np.maximum(cap.V[:, 1], -size * 0.02)
    return mk.merge(a, cap.transform(t=(size * 0.02, top - size * 0.10, 0.0)))


def snow_drift(rng, L=3.0, W=1.6, H=0.55):
    m = blob(L / 2, H, W / 2, rng, 0.06, seg=10, rings=5, mat="M_Env_SnowSoft", flat_bottom=0.0)
    m.V[:, 1] -= 0.02
    m.V[:, 0] += m.V[:, 1] * 0.5                        # door de wind scheef aangewaaid
    return m


def snow_pile_track(rng):
    """Sneeuwhoop over het spoor (1,9 m breed, 1,6 m diep, 0,7 m hoog): ligt over beide rails heen."""
    lumps = [blob(1.05, 0.62, 0.8, rng, 0.08, seg=10, rings=5, mat="M_Env_SnowSoft", flat_bottom=0.0),
             blob(0.6, 0.45, 0.55, rng, 0.08, seg=8, rings=4, mat="M_Env_SnowSoft", flat_bottom=-0.1).transform(t=(0.65, 0.05, 0.3)),
             blob(0.5, 0.4, 0.5, rng, 0.08, seg=8, rings=4, mat="M_Env_SnowSoft", flat_bottom=-0.1).transform(t=(-0.7, 0.0, -0.25))]
    return mk.merge(*lumps)


def snowman(rng):
    s = "M_Env_SnowSoft"
    parts = [blob(0.48, 0.42, 0.48, rng, 0.03, seg=12, rings=7, mat=s, flat_bottom=0.0).transform(t=(0, 0.40, 0)),
             blob(0.34, 0.31, 0.34, rng, 0.03, seg=12, rings=7, mat=s).transform(t=(0, 1.0, 0)),
             blob(0.24, 0.23, 0.24, rng, 0.03, seg=12, rings=7, mat=s).transform(t=(0, 1.47, 0))]
    parts.append(lathe([(0.0, 0.0), (0.045, 0.0), (0.0, 0.26)], 6, "M_Env_Carrot").transform(R=mk.rot_x(math.pi / 2), t=(0, 1.47, 0.22)))
    for sx in (-1, 1):
        parts.append(mk.ellipsoid(0.035, 0.04, 0.03, 6, 4, mat="M_Env_Dark").transform(t=(sx * 0.085, 1.56, 0.205)))
        parts.append(limb((sx * 0.30, 1.05, 0.0), (sx * 0.85, 1.42, 0.05), 0.03, 0.018, "M_Env_Trunk", 5))
        parts.append(limb((sx * 0.72, 1.33, 0.04), (sx * 0.86, 1.56, 0.06), 0.015, 0.01, "M_Env_Trunk", 4))
    for k, y in enumerate((1.16, 0.98, 0.78)):
        parts.append(mk.ellipsoid(0.035, 0.035, 0.025, 6, 4, mat="M_Env_Dark").transform(t=(0, y, 0.33 - k * 0.0 + (0.01 if k == 2 else 0))))
    for k in range(5):                                   # glimlach van kooltjes
        a = -0.6 + 1.2 * k / 4
        parts.append(mk.ellipsoid(0.018, 0.018, 0.015, 5, 3, mat="M_Env_Dark").transform(
            t=(math.sin(a) * 0.10, 1.40 - math.cos(a) * 0.035, 0.215)))
    parts.append(lathe([(0.0, 1.26), (0.27, 1.26), (0.29, 1.30), (0.26, 1.36), (0.0, 1.36)], 10, "M_Env_Scarf"))
    parts.append(mk.rounded_box(0.12, 0.30, 0.05, 0.02, div=1, mat="M_Env_Scarf").transform(R=mk.rot_z(0.2), t=(0.15, 1.15, 0.28)))
    parts.append(lathe([(0.0, 1.63), (0.27, 1.63), (0.27, 1.67), (0.17, 1.67), (0.16, 1.92), (0.0, 1.93)], 10, "M_Env_Dark").transform(
        R=mk.rot_z(0.12), t=(0.04, -0.01, 0)))
    return mk.merge(*parts)


def cabin(rng, W=3.6, D=3.0, H=2.2):
    """Houthakkershut: wanden van liggende stammen, zadeldak met dikke sneeuwlaag, stenen schoorsteen (ChimneySmoke-
    anker apart in de builder), deur en twee warm verlichte ramen. Pivot midden-onder, voorkant +Z."""
    parts = [mk.rounded_box(W + 0.2, 0.25, D + 0.2, 0.04, div=1, mat="M_Env_Chimney").transform(t=(0, 0.1, 0))]
    n = int(H / 0.26)
    for k in range(n):
        y = 0.32 + k * 0.26
        for (sx, sz, L, along_x) in ((0, D / 2, W + 0.35, True), (0, -D / 2, W + 0.35, True), (W / 2, 0, D + 0.35, False), (-W / 2, 0, D + 0.35, False)):
            off = 0.0 if (k % 2 == 0) == along_x else 0.13
            rr = 0.15
            if along_x:
                parts.append(limb((-L / 2, y + off * 0.0, sz), (L / 2, y, sz), rr, rr, "M_Env_Log", 6))
            else:
                parts.append(limb((sx, y + 0.13, -L / 2), (sx, y + 0.13, L / 2), rr, rr, "M_Env_Log", 6))
    top = 0.32 + n * 0.26
    # gevelvullingen (driehoeken) voor en achter
    ridge = top + 1.25
    for s in (-1, 1):
        g = mk.prism([(-W / 2 - 0.05, top - 0.15), (W / 2 + 0.05, top - 0.15), (0.0, ridge - 0.05)], s * D / 2 - 0.12, s * D / 2 + 0.12, 0.02,
                     mat="M_Env_Log") if s > 0 else mk.prism([(-W / 2 - 0.05, top - 0.15), (W / 2 + 0.05, top - 0.15), (0.0, ridge - 0.05)],
                                                              -D / 2 - 0.12, -D / 2 + 0.12, 0.02, mat="M_Env_Log")
        parts.append(g)
    # dak: twee schuine platen + dikke sneeuw erop
    pitch = math.atan2(1.25, W / 2)
    slope = math.hypot(W / 2 + 0.45, 1.25 + 0.35)
    for s in (-1, 1):
        roof = mk.rounded_box(slope, 0.14, D + (0.9 if s > 0 else 0.87), 0.03, div=1, mat="M_Env_PlankDark")
        parts.append(roof.transform(R=mk.rot_z(s * -pitch), t=(s * (W / 4 + 0.08), top + 0.62 + 0.06, 0)))
        snow = mk.rounded_box(slope + 0.05, 0.22, D + (0.98 if s > 0 else 0.95), 0.10, div=1, mat="M_Env_SnowSoft")
        parts.append(snow.transform(R=mk.rot_z(s * -pitch), t=(s * (W / 4 + 0.03), top + 0.62 + 0.23, 0)))
    # schoorsteen
    parts.append(mk.rounded_box(0.55, 2.2, 0.55, 0.05, div=1, mat="M_Env_Chimney").transform(t=(W / 2 - 0.75, top + 0.9, -0.5)))
    parts.append(mk.rounded_box(0.62, 0.18, 0.62, 0.06, div=1, mat="M_Env_SnowSoft").transform(t=(W / 2 - 0.75, top + 2.05, -0.5)))
    # deur en ramen op de voorgevel (+Z)
    fz = D / 2 + 0.17
    parts.append(mk.rounded_box(0.85, 1.55, 0.08, 0.03, div=1, mat="M_Env_PlankDark").transform(t=(-0.6, 0.32 + 0.78, fz)))
    parts.append(mk.ellipsoid(0.04, 0.04, 0.03, 6, 4, mat="M_Env_Beak").transform(t=(-0.3, 1.05, fz + 0.06)))
    for x in (0.85,):
        parts.append(mk.rounded_box(0.8, 0.7, 0.06, 0.03, div=1, mat="M_Env_Window").transform(t=(x, 1.25, fz)))
        parts.append(mk.rounded_box(0.92, 0.1, 0.14, 0.03, div=1, mat="M_Env_PlankDark").transform(t=(x, 0.86, fz + 0.03)))
        parts.append(mk.rounded_box(0.06, 0.72, 0.1, 0.02, div=1, mat="M_Env_PlankDark").transform(t=(x, 1.25, fz + 0.04)))
    # brandhoutstapel tegen de zijgevel
    for k in range(3):
        for j in range(3 - k):
            parts.append(mk.cylinder(0.08, 0.7, 7, 0.01, mat="M_Env_Log", axis="x").transform(
                t=(-W / 2 - 0.55, 0.10 + k * 0.155, -0.8 + j * 0.17 + k * 0.085)))
    return mk.merge(*parts)


def icicles(rng, L=2.0):
    """Rij ijspegels onder een dakrand (pivot in het midden van de bovenrand, rij langs X)."""
    parts = [mk.rounded_box(L, 0.06, 0.08, 0.02, div=1, mat="M_Env_SnowSoft").transform(t=(0, -0.01, 0))]
    x = -L / 2 + 0.06
    while x < L / 2 - 0.05:
        h = rng.uniform(0.12, 0.45)
        r = rng.uniform(0.025, 0.045)
        parts.append(lathe([(0.0, -0.02), (r, -0.03), (r * 0.6, -0.03 - h * 0.45), (0.0, -0.03 - h)], 5, "M_Env_Ice").transform(t=(x, 0, 0)))
        x += rng.uniform(0.09, 0.16)
    return mk.merge(*parts)


def frozen_lake(rng, R0=6.0):
    """Bevroren meertje: ijsschijf (3 cm boven de pivot, dus op het terrein te leggen) met donkere scheuren en een
    rand van sneeuwbulten. Je kunt erop lopen."""
    seg = 18
    rmod = lambda i, k: 1.0 + 0.10 * math.sin(k * 1.7) + 0.06 * math.cos(k * 3.1)
    ice = lathe([(0.0, 0.0), (R0, 0.0), (R0, 0.05), (0.0, 0.05)], seg, "M_Env_Ice", rmod=rmod)
    parts = [ice]
    for k in range(5):                                   # scheuren: dunne donkere latjes net boven het ijs
        a = rng.uniform(0, 2 * math.pi)
        p = np.array([rng.uniform(-R0 * 0.4, R0 * 0.4), 0.056, rng.uniform(-R0 * 0.4, R0 * 0.4)])
        for j in range(3):
            q = p + np.array([math.cos(a), 0, math.sin(a)]) * rng.uniform(0.5, 1.1)
            d = q - p
            L = float(np.linalg.norm(d))
            seg_m = mk.rounded_box(L, 0.008, 0.04, 0.002, div=1, mat="M_Env_IceDark")
            parts.append(seg_m.transform(R=mk.rot_y(-math.atan2(d[2], d[0])), t=tuple((p + q) / 2)))
            p = q
            a += rng.uniform(-0.8, 0.8)
    n = 26
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.05, 0.05)
        rr = R0 * rmod(1, k * seg / n) + rng.uniform(-0.1, 0.15)
        parts.append(blob(rng.uniform(0.5, 0.9), rng.uniform(0.18, 0.35), rng.uniform(0.4, 0.6), rng, 0.08, seg=7, rings=4,
                          mat="M_Env_SnowSoft", flat_bottom=-0.05).transform(R=mk.rot_y(-a), t=(math.cos(a) * rr, 0.0, math.sin(a) * rr)))
    return mk.merge(*parts)


def build(rng_for, grass_tuft):
    """Alle assets van deze module: rel-pad -> (mesh, dichtheid, straal) zoals build_environment_kit verwacht."""
    A = {}
    r = rng_for(10)
    A["Trees/Tree_Oak_01"] = (oak(r), 500, 0.25)
    A["Trees/Tree_Oak_Autumn_01"] = (oak(r, "M_Env_LeafAutumn", 4.8), 500, 0.25)
    A["Trees/Tree_Oak_Autumn_02"] = (oak(r, "M_Env_LeafAutumnRed", 5.0), 500, 0.25)
    A["Trees/Tree_Birch_01"] = (birch(r), 600, 0.22)
    A["Trees/Tree_Birch_Autumn_01"] = (birch(r, "M_Env_LeafBirchAutumn", 5.4), 600, 0.22)
    A["Trees/Tree_Willow_01"] = (willow(r), 400, 0.25)
    A["Trees/Bush_Round_02"] = (bush_round(r), 900, 0.20)
    A["Trees/Bush_Berry_01"] = (bush_berry(r), 900, 0.18)
    A["Trees/Bush_Blossom_01"] = (bush_blossom(r), 900, 0.18)
    A["Trees/Bush_Hedge_01"] = (hedge(r), 700, 0.20)
    r = rng_for(11)
    A["Plants/Fern_01"] = (fern(r), 3000, 0.06)
    A["Plants/Fern_02"] = (fern(r, 9, 1.0), 2500, 0.07)
    A["Plants/Mushroom_Red_01"] = (mushroom(r, 0.16, 0.12), 4000, 0.04)
    A["Plants/Mushroom_Cluster_01"] = (mushroom_cluster(r), 4000, 0.04)
    A["Plants/FlowerBed_Round_01"] = (flower_bed_round(r), 2000, 0.08)
    A["Plants/FlowerBed_Long_01"] = (flower_bed_long(r), 2000, 0.08)
    A["Plants/Flower_Tulips_01"] = (tulip_patch(r), 3000, 0.05)
    A["Plants/Flower_Wild_01"] = (wild_patch(r, grass_tuft), 3000, 0.05)
    r = rng_for(12)
    A["Wood/Log_Fallen_01"] = (log(r), 900, 0.15)
    A["Wood/Log_Fallen_02"] = (log(r, 2.2, 0.26, shrooms=False, stub=False), 1000, 0.12)
    A["Wood/Stump_01"] = (stump(r), 1500, 0.10)
    r = rng_for(13)
    A["Water/Lily_Pads_01"] = (lily_pads(r), 3000, 0.05)
    A["Water/Lily_Pad_Single"] = (mk.merge(lily_pad(r, 0.3), lily_flower(r).transform(t=(0.02, 0.02, 0.0))), 3000, 0.05)
    A["Water/Reeds_01"] = (reeds(r), 3000, 0.05)
    A["Water/Cattails_01"] = (reeds(r, 12, 1.2, cattails=True), 3000, 0.05)
    A["Water/Duck_01"] = (duck(r).transform(s=(1.25, 1.25, 1.25)), 6000, 0.03)
    A["Water/Duck_02"] = (duck(r, "M_Env_DuckBrown", "M_Env_DuckGreen").transform(s=(1.25, 1.25, 1.25)), 6000, 0.03)
    A["Water/Jetty_01"] = (jetty(r), 900, 0.12)
    A["Water/Rowboat_01"] = (rowboat(r), 1500, 0.10)
    r = rng_for(14)
    A["Winter/Tree_Pine_Snow_Small"] = (pine_snow(2.4, 2, 1.8, r), 900, 0.20)
    A["Winter/Tree_Pine_Snow_Medium"] = (pine_snow(3.8, 3, 2.6, r), 600, 0.25)
    A["Winter/Tree_Pine_Snow_Large"] = (pine_snow(5.8, 4, 3.5, r), 300, 0.30)
    A["Winter/Rock_Snow_Medium"] = (rock_snow(1.2, r), 900, 0.20)
    A["Winter/Rock_Snow_Large"] = (rock_snow(2.4, r), 300, 0.35)
    A["Winter/Snow_Drift_01"] = (snow_drift(r), 500, 0.25)
    A["Winter/Snow_Drift_02"] = (snow_drift(r, 5.0, 2.2, 0.8), 300, 0.30)
    A["Winter/Snow_Pile_Track"] = (snow_pile_track(r), 600, 0.20)
    A["Winter/Snowman_01"] = (snowman(r), 1500, 0.10)
    A["Winter/Cabin_01"] = (cabin(r), 300, 0.25)
    A["Winter/Icicles_2m"] = (icicles(r), 3000, 0.05)
    A["Winter/Frozen_Lake_01"] = (frozen_lake(r), 120, 0.40)
    return A
