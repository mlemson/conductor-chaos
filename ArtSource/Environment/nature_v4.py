"""Boskit (#244): grote loofbomen voor een echt bos en bloemenperken die direct op het gras staan (#245).
Zelfde stijl en hulpjes als nature_v3: chunky ronde low-poly klonten, kleuren alleen uit palette.json, pivot midden-onder,
Y omhoog, 1 unit = 1 m. Alles is een gesloten volume (onderdelen steken in elkaar), dus geen z-fighting.

  Trees/   Tree_Beech_Tall_01/02 (10-12 m, hoge kale stam, brede koepel), Tree_Beech_Young_01 (ondergroei),
           Tree_Oak_Giant_01 (reuzeneik, wijde kruin, wortels), Tree_Linden_01 (hoge eivorm), Tree_Poplar_01 (zuil),
           Tree_Maple_01 / Tree_Maple_Autumn_01 / Tree_Maple_Red_01 (getrapte kruin), Tree_Chestnut_01 (met bloeikaarsen),
           Tree_Dead_01 (kale dode boom)
  Plants/  FlowerBed_Grass_Round_01/02, FlowerBed_Grass_Long_01: perkjes zonder bak of aarden bult
"""
import math

import numpy as np

import meshkit as mk
from nature_v3 import blob, flower, lathe, limb, scatter, tulip


def materials(R):
    leaf = {"cells": 0.6, "cellSize": 0.7, "rim": 0.30}
    return {
        "M_Env_LeafBeech": (R["grass"][2], R["grass"], 0.05, -0.06, dict(leaf)),
        "M_Env_LeafLinden": (R["dino_green"][2], R["dino_green"], 0.05, -0.10, dict(leaf)),
        "M_Env_LeafPoplar": (R["chalk_green"][3], R["chalk_green"], 0.05, 0.10, dict(leaf)),
        "M_Env_LeafMaple": (R["grass"][3], R["grass"], 0.05, 0.02, dict(leaf)),
        "M_Env_LeafChestnut": (R["grass"][1], R["grass"], 0.05, -0.02, dict(leaf)),
        "M_Env_BeechBark": (R["metal"][0], R["metal"], 0.05, -0.32, {"cells": 0.4, "cellSize": 0.5}),
        "M_Env_DeadWood": (R["cliff"][1], R["cliff"], 0.05, -0.18, {"cells": 0.4, "cellSize": 0.4}),
        "M_Env_BedLeaf": (R["chalk_green"][4], R["chalk_green"], 0.05, 0.06, {"cells": 0.5, "cellSize": 0.4, "rim": 0.25}),
        "M_Env_Blossom": (R["scarf_cream"][4], R["scarf_cream"], 0.05, 0.0, {"cells": 0.0, "rim": 0.15}),
    }


def crown(rng, centers, mat, amp=0.09, squash=0.86):
    """Kruin van grote ronde klonten (x, y, z, r)."""
    return [blob(r, r * squash, r, rng, amp, seg=8, rings=5, mat=mat).transform(t=(x, y, z)) for (x, y, z, r) in centers]


def root_flare(r, h, mat, n=5, rng=None):
    """Wortelvoet: brede voet en een paar wortels die schuin de grond in lopen."""
    parts = [lathe([(0.0, -0.12), (r * 1.55, -0.12), (r * 1.15, h * 0.35), (r * 0.98, h), (0.0, h + 0.05)], 8, mat)]
    for k in range(n):
        a = 2 * math.pi * k / n + (rng.uniform(-0.3, 0.3) if rng is not None else 0.0)
        out = (math.cos(a) * r * 2.3, -0.10, math.sin(a) * r * 2.3)
        parts.append(limb((math.cos(a) * r * 0.4, h * 0.8, math.sin(a) * r * 0.4), out, r * 0.42, r * 0.18, mat, 5))
    return parts


def dome(rng, y0, y1, R0, n_ring, r_clump, extra_top=True):
    """Koepelvormige kruin tussen y0 en y1, buitenstraal R0: twee ringen klonten plus een top."""
    cs = []
    mid = (y0 + y1) * 0.5
    for k in range(n_ring):
        a = 2 * math.pi * k / n_ring + rng.uniform(-0.25, 0.25)
        rr = R0 - r_clump * rng.uniform(0.75, 0.95)
        cs.append((math.cos(a) * rr, y0 + r_clump * 0.75 + rng.uniform(-0.2, 0.25), math.sin(a) * rr, r_clump * rng.uniform(0.9, 1.08)))
    for k in range(max(3, n_ring - 2)):
        a = 2 * math.pi * (k + 0.5) / max(3, n_ring - 2) + rng.uniform(-0.3, 0.3)
        rr = (R0 - r_clump) * 0.55
        cs.append((math.cos(a) * rr, mid + r_clump * 0.45 + rng.uniform(-0.2, 0.2), math.sin(a) * rr, r_clump * rng.uniform(0.95, 1.12)))
    if extra_top:
        cs.append((rng.uniform(-0.3, 0.3), y1 - r_clump * 0.85, rng.uniform(-0.3, 0.3), r_clump * 1.15))
    return cs


# --------------------------------------------------------------- bosbomen
def beech_tall(rng, height=10.5, leaf="M_Env_LeafBeech"):
    """Hoge beuk: rechte gladde grijze stam tot ruim halverwege, een paar zware takken, brede koepel bovenin.
    De kruin begint hoog, zodat je in het bos tussen de stammen door kijkt."""
    bark = "M_Env_BeechBark"
    h0 = height * 0.5
    lean = (rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25))
    parts = root_flare(0.36, 0.5, bark, 5, rng)
    parts.append(limb((0, 0.3, 0), (lean[0], h0, lean[1]), 0.36, 0.26, bark, 8))
    tips = []
    for k in range(4):
        a = 2 * math.pi * k / 4 + rng.uniform(-0.35, 0.35)
        tip = (lean[0] + math.cos(a) * 1.7, h0 + 1.6 + rng.uniform(-0.3, 0.4), lean[1] + math.sin(a) * 1.7)
        parts.append(limb((lean[0], h0 - 0.2, lean[1]), tip, 0.19, 0.09, bark, 6))
        tips.append(tip)
    parts.append(limb((lean[0], h0 - 0.1, lean[1]), (lean[0] * 1.2, h0 + 2.4, lean[1] * 1.2), 0.2, 0.1, bark, 6))
    cs = dome(rng, h0 + 0.2, height, 4.1, 8, 1.75)
    cs = [(x + lean[0], y, z + lean[1], r) for (x, y, z, r) in cs]
    return mk.merge(*parts, *crown(rng, cs, leaf, 0.09, 0.82))


def beech_young(rng, height=5.0, leaf="M_Env_LeafBeech"):
    """Jonge beuk voor de ondergroei: dunne stam, kleine smalle kruin."""
    bark = "M_Env_BeechBark"
    parts = [limb((0, -0.12, 0), (0.06, height * 0.62, 0.03), 0.12, 0.06, bark, 6),
             lathe([(0.0, -0.1), (0.2, -0.1), (0.13, 0.15), (0.0, 0.2)], 6, bark)]
    cs = [(0.05, height * 0.55, 0.0, 0.95), (0.35, height * 0.68, 0.2, 0.7), (-0.3, height * 0.7, -0.15, 0.72),
          (0.0, height * 0.86, 0.05, 0.62)]
    return mk.merge(*parts, *crown(rng, cs, leaf, 0.10))


def oak_giant(rng, height=8.5, leaf="M_Env_LeafOak"):
    """Reuzeneik: korte, heel dikke stam met grote wortels, vijf zware takken die wijd uitwaaieren, brede platte kruin."""
    tr = "M_Env_Trunk"
    h0 = 2.6
    parts = root_flare(0.62, 0.7, tr, 6, rng)
    parts.append(limb((0, 0.4, 0), (0.1, h0, 0.05), 0.62, 0.5, tr, 8))
    tips = []
    for k in range(5):
        a = 2 * math.pi * k / 5 + rng.uniform(-0.3, 0.3)
        mid = (math.cos(a) * 1.4, h0 + 1.2 + rng.uniform(-0.2, 0.3), math.sin(a) * 1.4)
        tip = (math.cos(a) * 3.1, h0 + 2.5 + rng.uniform(-0.3, 0.4), math.sin(a) * 3.1)
        parts.append(limb((0.1, h0 - 0.2, 0.05), mid, 0.34, 0.24, tr, 7))
        parts.append(limb(mid, tip, 0.24, 0.12, tr, 6))
        tips.append(tip)
    cs = [(x * 1.05, y + rng.uniform(0.3, 0.7), z * 1.05, rng.uniform(1.5, 1.75)) for (x, y, z) in tips]
    for k in range(5):
        a = 2 * math.pi * (k + 0.5) / 5 + rng.uniform(-0.3, 0.3)
        cs.append((math.cos(a) * 2.6, h0 + 2.6 + rng.uniform(-0.2, 0.2), math.sin(a) * 2.6, rng.uniform(1.35, 1.55)))
    for k in range(4):
        a = 2 * math.pi * k / 4 + rng.uniform(-0.4, 0.4)
        cs.append((math.cos(a) * 1.2, height - 1.7 + rng.uniform(-0.2, 0.2), math.sin(a) * 1.2, rng.uniform(1.55, 1.8)))
    cs.append((0.0, height - 1.3, 0.0, 1.7))
    return mk.merge(*parts, *crown(rng, cs, leaf, 0.09, 0.78))


def linden(rng, height=9.0, leaf="M_Env_LeafLinden"):
    """Linde: rechte stam, hoge eivormige kruin die laag begint."""
    tr = "M_Env_Trunk"
    h0 = height * 0.32
    parts = root_flare(0.3, 0.4, tr, 4, rng)
    parts.append(limb((0, 0.2, 0), (0.05, height * 0.7, 0.0), 0.3, 0.14, tr, 7))
    for k in range(3):
        a = 2 * math.pi * k / 3 + rng.uniform(-0.3, 0.3)
        y = h0 + 0.6 + k * 0.9
        parts.append(limb((0.03, y, 0), (math.cos(a) * 1.2, y + 1.1, math.sin(a) * 1.2), 0.12, 0.06, tr, 5))
    cs = []
    rows = 5
    for i in range(rows):
        t = i / (rows - 1)
        y = h0 + 0.9 + t * (height - h0 - 1.9)
        R0 = 2.5 * math.sin(math.pi * (0.25 + 0.7 * t)) + 0.4
        n = max(1, int(round(R0 * 2.2)))
        rc = 1.2 - 0.25 * t
        for k in range(n):
            a = 2 * math.pi * k / n + i * 0.7 + rng.uniform(-0.2, 0.2)
            rr = max(0.0, R0 - rc * 0.9)
            cs.append((math.cos(a) * rr, y + rng.uniform(-0.15, 0.15), math.sin(a) * rr, rc * rng.uniform(0.95, 1.1)))
    cs.append((0.0, height - 0.95, 0.0, 0.95))
    return mk.merge(*parts, *crown(rng, cs, leaf, 0.09, 0.9))


def poplar(rng, height=11.0, leaf="M_Env_LeafPoplar"):
    """Populier: smalle hoge zuil van gestapelde klonten op een dunne rechte stam."""
    tr = "M_Env_Trunk"
    parts = [limb((0, -0.12, 0), (0, height * 0.82, 0), 0.24, 0.08, tr, 7),
             lathe([(0.0, -0.1), (0.38, -0.1), (0.26, 0.25), (0.0, 0.3)], 7, tr)]
    cs = []
    y = 1.9
    k = 0
    while y < height - 1.0:
        t = (y - 1.9) / (height - 2.9)
        rc = 0.95 * math.sin(math.pi * (0.18 + 0.78 * t)) + 0.35
        a = k * 2.2
        cs.append((math.cos(a) * 0.25, y, math.sin(a) * 0.25, rc))
        if 0.15 < t < 0.8:
            cs.append((math.cos(a + 3.1) * 0.35, y + 0.35, math.sin(a + 3.1) * 0.35, rc * 0.85))
        y += 0.95
        k += 1
    cs.append((0.0, height - 0.6, 0.0, 0.5))
    return mk.merge(*parts, *crown(rng, cs, leaf, 0.08, 1.15))


def maple(rng, height=7.5, leaf="M_Env_LeafMaple"):
    """Esdoorn: stam die zich in twee splitst, kruin in twee trappen (onderste breed, bovenste smaller)."""
    tr = "M_Env_Trunk"
    h0 = height * 0.33
    parts = root_flare(0.3, 0.4, tr, 4, rng)
    parts.append(limb((0, 0.2, 0), (0, h0, 0), 0.3, 0.24, tr, 7))
    for s in (-1, 1):
        parts.append(limb((0, h0 - 0.1, 0), (s * 0.9, height * 0.72, rng.uniform(-0.3, 0.3)), 0.2, 0.09, tr, 6))
    cs = []
    for (y, R0, n, rc) in [(h0 + 1.6, 2.9, 7, 1.25), (h0 + 3.2, 2.0, 5, 1.15), (height - 0.9, 0.6, 2, 1.0)]:
        for k in range(n):
            a = 2 * math.pi * k / n + rng.uniform(-0.25, 0.25) + y
            rr = max(0.0, R0 - rc * 0.85)
            cs.append((math.cos(a) * rr, y + rng.uniform(-0.15, 0.2), math.sin(a) * rr, rc * rng.uniform(0.92, 1.08)))
    return mk.merge(*parts, *crown(rng, cs, leaf, 0.10, 0.74))


def candle(rng, h=0.42):
    """Bloeikaars van de kastanje: rechtop staande, bolle getande kegel."""
    m = lathe([(0.0, -0.2), (0.10, -0.16), (0.12, h * 0.25), (0.06, h * 0.7), (0.0, h)], 6, "M_Env_Blossom",
              rmod=lambda i, k: 1.0 if k % 2 == 0 else 0.82)
    return m.transform(R=mk.rot_z(rng.uniform(-0.15, 0.15)) @ mk.rot_x(rng.uniform(-0.15, 0.15)))


def chestnut(rng, height=8.0, leaf="M_Env_LeafChestnut"):
    """Kastanje: dikke stam, ronde volle kruin met witte bloeikaarsen die er net uit steken."""
    tr = "M_Env_Trunk"
    h0 = height * 0.3
    parts = root_flare(0.4, 0.5, tr, 5, rng)
    parts.append(limb((0, 0.3, 0), (0.05, h0, 0), 0.4, 0.32, tr, 7))
    for k in range(4):
        a = 2 * math.pi * k / 4 + rng.uniform(-0.3, 0.3)
        parts.append(limb((0.05, h0 - 0.1, 0), (math.cos(a) * 1.5, h0 + 1.7, math.sin(a) * 1.5), 0.2, 0.1, tr, 6))
    cs = dome(rng, h0 + 0.2, height, 3.3, 8, 1.45)
    clumps = crown(rng, cs, leaf, 0.09, 0.84)
    # kaarsen op de bovenkant van de buitenste klonten
    out = []
    for (x, y, z, r) in cs:
        for _ in range(3):
            a = rng.uniform(0, 2 * math.pi)
            el = rng.uniform(0.45, 1.1)
            d = np.array([math.cos(a) * math.cos(el), math.sin(el), math.sin(a) * math.cos(el)])
            p = np.array([x, y, z]) + d * np.array([r, r * 0.84, r]) * 0.9
            if math.hypot(p[0], p[2]) < 1.0 and p[1] < height - 1.0:
                continue
            out.append(candle(rng).transform(t=tuple(p)))
    return mk.merge(*parts, *clumps, *out)


def dead_tree(rng, height=6.5):
    """Dode boom: kale grijze stam met een afgebroken top en een paar knoestige takken (geen blad)."""
    w = "M_Env_DeadWood"
    parts = root_flare(0.3, 0.4, w, 4, rng)
    top = (0.2, height, 0.1)
    parts.append(limb((0, 0.2, 0), top, 0.3, 0.12, w, 6))
    for k in range(5):
        a = 2 * math.pi * k / 5 + rng.uniform(-0.4, 0.4)
        y = height * rng.uniform(0.4, 0.85)
        f = y / height
        base = (0.2 * f, y, 0.1 * f)
        mid = (base[0] + math.cos(a) * 1.0, y + 0.7, base[2] + math.sin(a) * 1.0)
        tip = (mid[0] + math.cos(a + 0.5) * 0.7, mid[1] + 0.8, mid[2] + math.sin(a + 0.5) * 0.7)
        parts.append(limb(base, mid, 0.1, 0.06, w, 5))
        parts.append(limb(mid, tip, 0.06, 0.03, w, 4))
    # afgebroken top: lichte breukrand die net buiten de stam steekt
    parts.append(lathe([(0.0, -0.22), (0.14, -0.22), (0.135, 0.0), (0.06, 0.1), (0.0, 0.06)], 6, "M_Env_LogCut").transform(t=top))
    return mk.merge(*parts)


# ------------------------------------------------- bloemenperken op het gras
LEAVES = "M_Env_BedLeaf"                  # donkerder dan het terreingras, zodat het perkje als perkje leest
BED_COLOURS = [["M_Env_Tulip", "M_Env_TulipYellow", "M_Env_FlowerPink"],
               ["M_Env_FlowerBlue", "M_Env_Petal", "M_Env_FlowerPink"]]


def foliage(rng, x, z, s, i):
    """Bol bladgroen met een vlakke onderkant net onder het maaiveld (staat zo altijd in het gras). Elke klont krijgt een
    eigen bodemhoogte (6 mm per stap), zodat de verzonken bodems van buren nooit in één vlak liggen."""
    bottom = -0.02 - 0.006 * i
    return blob(s, s * 0.7, s, rng, 0.12, seg=7, rings=4, mat=LEAVES, flat_bottom=bottom - s * 0.24).transform(t=(x, s * 0.24, z))


def bloom(rng, mat, scale=1.35):
    """Bloem op ware perkgrootte: wat groter dan de losse veldbloemen, stengel begint 4 cm onder nul."""
    return flower(rng, mat).transform(s=(scale, scale, scale), t=(0, -0.04, 0))


def grass_bed_round(rng, R0=0.85, palette=0):
    """Rond perkje zonder bak of aarden bult: een volle kring bladgroen met tulpen in het hart en bloemen eromheen."""
    cols = BED_COLOURS[palette]
    parts = []
    for i, (x, z) in enumerate(scatter(rng, 14, R0 * 0.9, 0.26)):
        parts.append(foliage(rng, x, z, rng.uniform(0.18, 0.26), i))
    for (x, z) in scatter(rng, 12, R0 * 0.38, 0.10):
        parts.append(tulip(rng, cols[int(rng.integers(2))], rng.uniform(0.40, 0.52)).transform(s=(1.25, 1.0, 1.25), t=(x, -0.04, z)))
    for (x, z) in scatter(rng, 36, R0 * 0.98, 0.085):
        if x * x + z * z < (R0 * 0.40) ** 2:
            continue
        parts.append(bloom(rng, cols[int(rng.integers(3))]).transform(t=(x, 0, z)))
    return mk.merge(*parts)


def grass_bed_long(rng, L=2.4, D=0.75):
    """Lang perk (langs X) zonder bak: een strook bladgroen met tulpen in het midden en bloemen langs de randen."""
    parts = []
    for i, x in enumerate(np.linspace(-L / 2 + 0.22, L / 2 - 0.22, 9)):
        for j, z in enumerate((-0.17, 0.17)):
            parts.append(foliage(rng, x + rng.uniform(-0.06, 0.06), z + rng.uniform(-0.05, 0.05), rng.uniform(0.18, 0.24), 2 * i + j))
    for x in np.linspace(-L / 2 + 0.12, L / 2 - 0.12, 14):
        for z in (-D * 0.36, -D * 0.12, D * 0.12, D * 0.36):
            xx = x + rng.uniform(-0.04, 0.04) + (0.08 if z > 0 else 0.0)
            if abs(xx) > L / 2 - 0.08:
                continue
            mat = BED_COLOURS[0 if x < 0 else 1][int(rng.integers(3))]
            if abs(z) < 0.2 and rng.random() < 0.7:
                f = tulip(rng, mat, rng.uniform(0.40, 0.50)).transform(s=(1.25, 1.0, 1.25), t=(0, -0.04, 0))
            else:
                f = bloom(rng, mat)
            parts.append(f.transform(t=(xx, 0, z + rng.uniform(-0.04, 0.04))))
    return mk.merge(*parts)


def build(rng_for):
    A = {}
    r = rng_for(15)
    A["Trees/Tree_Beech_Tall_01"] = (beech_tall(r), 300, 0.35)
    A["Trees/Tree_Beech_Tall_02"] = (beech_tall(r, 12.0), 300, 0.35)
    A["Trees/Tree_Beech_Young_01"] = (beech_young(r), 600, 0.25)
    A["Trees/Tree_Oak_Giant_01"] = (oak_giant(r), 250, 0.40)
    A["Trees/Tree_Linden_01"] = (linden(r), 300, 0.35)
    A["Trees/Tree_Poplar_01"] = (poplar(r), 400, 0.30)
    A["Trees/Tree_Maple_01"] = (maple(r), 350, 0.30)
    A["Trees/Tree_Maple_Autumn_01"] = (maple(r, 7.0, "M_Env_LeafAutumn"), 350, 0.30)
    A["Trees/Tree_Maple_Red_01"] = (maple(r, 7.2, "M_Env_LeafAutumnRed"), 350, 0.30)
    A["Trees/Tree_Chestnut_01"] = (chestnut(r), 300, 0.35)
    A["Trees/Tree_Dead_01"] = (dead_tree(r), 600, 0.25)
    r = rng_for(16)
    A["Plants/FlowerBed_Grass_Round_01"] = (grass_bed_round(r), 2000, 0.08)
    A["Plants/FlowerBed_Grass_Round_02"] = (grass_bed_round(r, 0.65, 1), 2000, 0.08)
    A["Plants/FlowerBed_Grass_Long_01"] = (grass_bed_long(r), 2000, 0.08)
    return A
