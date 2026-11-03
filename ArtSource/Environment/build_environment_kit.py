"""Modulaire natuur- en achtergrondkit in de stijl van het concept (issue #30).

Stijlregels (concept + review @mlemson):
- Chunky, vriendelijk en gepolijst low-poly: grote leesbare vormen, weinig kleine details. Facetten met
  afgeschuinde overgangen in plaats van harde blokken.
- Kleuren alleen uit palette.json, matte materialen. Bovenkant lichter, onderkant donkerder (shader + gebakken
  vertexkleur). Grote kleurvlakken komen uit de shader (cellen).
- Diepte in lagen: voorgrond warm (gras, rots, kliffen), achtergrond koel en blauw (verre bergen), wolken
  simpel en zacht.

Assets (elk een eigen FBX voor Unity en een GLB voor Blender/preview, pivot midden-onder, Y omhoog, 1 unit = 1 m):
  Trees:     Tree_Pine_Small_01, Tree_Pine_Medium_01, Tree_Pine_Large_01, Tree_Pine_Variant_02,
             Tree_Pine_Background_01, Bush_01
  Rocks:     Rock_Small_01, Rock_Medium_01, Rock_Large_01
  Cliffs:    Cliff_Module_A (2×2 m, 2 m hoog), Cliff_Module_B (4×2 m, 3 m), Cliff_Module_C (2×2 m terras 1,2/2,6 m),
             Hill_Grass_01 (lage grasheuvel 6×4 m)
  Mountains: Mountain_Back_01..03 (verre silhouetten 40–70 m)
  Sky:       Cloud_01..03
Klifmodules staan op een raster van 2 m: footprint gecentreerd op de pivot, randen bollen tot ~0,1 m uit,
zodat buren elkaar iets overlappen (geen naden). Gras steekt een fractie over de rand.

Gebruik: python ArtSource/Environment/build_environment_kit.py
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
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
R = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

MAT_DIR = "Assets/Art/Materials/Environment"
MODEL_DIR = "Assets/Art/Models/Environment"
EXP_DIR = os.path.join(ROOT, "ArtSource", "Exports", "Environment")

# gedeelde materialen (naam -> (basis, ramp, smoothness, shift, opts))
MATERIALS = {
    "M_Env_PineLeaf": (R["pine"][2], R["pine"], 0.05, 0.16, {"cells": 0.6, "cellSize": 0.6, "rim": 0.30}),
    "M_Env_PineFar": (R["pine"][3], R["pine"], 0.05, 0.16, {"cells": 0.4, "cellSize": 1.2, "rim": 0.25}),
    "M_Env_Trunk": (R["interior_wood"][1], R["interior_wood"], 0.05, -0.05, {"cells": 0.5}),
    "M_Env_Bush": (R["grass"][1], R["grass"], 0.05, -0.18, {"cells": 0.6, "cellSize": 0.5}),
    "M_Env_Rock": (R["metal"][2], R["metal"], 0.05, 0.06, {"cells": 1.0, "cellSize": 0.6, "rim": 0.25}),
    "M_Env_Cliff": (R["cliff"][2], R["cliff"], 0.05, 0.04, {"cells": 0.55, "cellSize": 1.1, "rim": 0.22}),
    "M_Env_Grass": (R["grass"][2], R["grass"], 0.05, 0.02, {"cells": 0.8, "cellSize": 0.9}),
    "M_Env_Mountain": (R["mountain"][2], R["mountain"], 0.05, 0.02, {"cells": 0.8, "cellSize": 6.0, "rim": 0.12, "edge": 0.6}),
    "M_Env_Snow": (R["cloud_snow"][3], R["cloud_snow"], 0.05, 0.05, {"cells": 0.4, "cellSize": 5.0, "rim": 0.10}),
    "M_Env_Water": (R["water"][2], R["water"], 0.3, 0.02, {"cells": 0.0, "flat": 0.15, "rim": 0.20, "edge": 0.0}),
    "M_Env_WaterLight": (R["water"][4], R["water"], 0.3, 0.38, {"cells": 0.0, "flat": 0.55, "rim": 0.10, "edge": 0.0}),
    "M_Env_WaterDeep": (R["water"][0], R["water"], 0.3, -0.10, {"cells": 0.0, "flat": 0.15, "rim": 0.15, "edge": 0.0}),
    "M_Env_Mist": (R["cloud_snow"][4], R["cloud_snow"], 0.0, 0.30, {"cells": 0.0, "flat": 0.7, "rim": 0.0, "opacity": 0.45, "softEdge": 1.0,
                                                                       "noShadow": True, "edge": 0.0}),
    "M_Env_Foam": (R["cloud_snow"][4], R["cloud_snow"], 0.0, 0.25, {"cells": 0.0, "flat": 0.45, "rim": 0.20,
                                                                       "noShadow": True, "edge": 0.0}),
    # keien in drie tinten (licht / middel / donker grijs) + een warme zandsteen: contrast tussen de stenen
    "M_Env_PathStone": (R["metal"][4], R["metal"], 0.05, 0.08, {"cells": 0.5, "cellSize": 0.6, "rim": 0.2, "edge": 2.0}),
    "M_Env_PathStoneMid": (R["metal"][2], R["metal"], 0.05, -0.32, {"cells": 0.5, "cellSize": 0.6, "rim": 0.2, "edge": 2.0}),
    "M_Env_PathStoneDark": (R["metal"][0], R["metal"], 0.05, -0.72, {"cells": 0.5, "cellSize": 0.6, "rim": 0.15, "edge": 2.0}),
    "M_Env_PathStoneWarm": (R["cliff"][3], R["cliff"], 0.05, -0.12, {"cells": 0.5, "cellSize": 0.6, "rim": 0.2, "edge": 2.0}),
    "M_Env_Dirt": (R["cliff"][1], R["cliff"], 0.05, -0.30, {"cells": 0.6, "cellSize": 0.6, "rim": 0.1}),
    "M_Env_Petal": (R["cloud_snow"][4], R["cloud_snow"], 0.05, 0.10, {"cells": 0.0, "rim": 0.2}),
    "M_Env_FlowerHeart": (R["helmet"][2], R["helmet"], 0.05, 0.0, {"cells": 0.0}),
    "M_Env_Stem": (R["grass"][1], R["grass"], 0.05, -0.20, {"cells": 0.0}),
    "M_Env_Cloud": (R["cloud_snow"][3], R["cloud_snow"], 0.0, 0.12, {"cells": 0.0, "flat": 0.35, "rim": 0.25,
                                                                       "noShadow": True, "edge": 0.0}),
}


# ------------------------------------------------------------------ helpers
def lathe_mod(profile, seg, rmod=None, ymod=None, phase=0.0, mat="Default"):
    """Lathe rond Y met per hoekpunt een straal- en hoogtevariatie: rmod(ring, k) -> factor, ymod(ring, k) -> dy."""
    V, F, rings = [], [], []
    for i, (r, y) in enumerate(profile):
        if r <= 1e-9:
            rings.append([len(V)])
            V.append((0.0, y + (ymod(i, 0) if ymod else 0.0), 0.0))
            continue
        idx = []
        for k in range(seg):
            a = phase + 2 * math.pi * k / seg
            rr = r * (rmod(i, k) if rmod else 1.0)
            idx.append(len(V))
            V.append((rr * math.cos(a), y + (ymod(i, k) if ymod else 0.0), rr * math.sin(a)))
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


def loft(rings, mat, top=True, bottom=True):
    """Ringen [(pts (N,2) in x/z, y of y-per-punt), ...] van onder naar boven verbinden; caps optioneel."""
    V, F = [], []
    n = len(rings[0][0])
    for pts, y in rings:
        ys = np.broadcast_to(np.asarray(y, float), (n,))
        V += [(p[0], yy, p[1]) for p, yy in zip(pts, ys)]
    for r in range(len(rings) - 1):
        for i in range(n):
            j = (i + 1) % n
            F.append((r * n + i, (r + 1) * n + i, (r + 1) * n + j, r * n + j))
    P2 = np.asarray(rings[0][0])
    if mk._poly_area(P2) < 0:
        order = list(range(n))[::-1]
    else:
        order = list(range(n))
    tris = mk._earclip(P2[order])
    if bottom:
        F += [(order[a], order[b], order[c]) for (a, b, c) in tris]
    if top:
        Pt = np.asarray(rings[-1][0])
        tt = mk._earclip(Pt[order])
        base = (len(rings) - 1) * n
        F += [(base + order[a], base + order[c], base + order[b]) for (a, b, c) in tt]
    m = mk.Mesh(np.array(V, float), F, [mat] * len(F))
    m.sharp = True
    return mk._orient_closed(m)


def rect_outline(x0, x1, z0, z1, step=0.45, chamfer=0.30):
    """Rechthoekige footprint (CCW in x/z) met afgeschuinde hoeken, onderverdeeld in punten om de ~step m."""
    P = np.array(mk.chamfer_poly([(x0, z0), (x1, z0), (x1, z1), (x0, z1)], chamfer))
    if mk._poly_area(P) < 0:
        P = P[::-1]
    out = []
    for i in range(len(P)):
        a, b = P[i], P[(i + 1) % len(P)]
        n = max(1, int(round(np.linalg.norm(b - a) / step)))
        out += [a + (b - a) * t / n for t in range(n)]
    return np.array(out)


def offset(P, d):
    """d > 0 = naar binnen, d < 0 = naar buiten (per punt mag d een array zijn)."""
    P = np.asarray(P, float)
    n = len(P)
    out = []
    dd = np.broadcast_to(np.asarray(d, float), (n,))
    for i in range(n):
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]
        e1 = (b - a) / np.linalg.norm(b - a)
        e2 = (c - b) / np.linalg.norm(c - b)
        nrm = np.array([-e1[1], e1[0]]) + np.array([-e2[1], e2[0]])
        nrm /= np.linalg.norm(nrm)
        out.append(b + nrm * dd[i])
    return np.array(out)


def blob(rx, ry, rz, rng, amp=0.12, seg=7, rings=4, mat="Default", flat_bottom=None):
    """Onregelmatige low-poly klont (rots/struik/wolk): ellipsoïde met verschoven hoekpunten."""
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


# -------------------------------------------------------------------- bomen
def pine(height, tiers, width, rng, lean=0.0, seg=8, far=False):
    """Naaldboom: korte zeskantige stam + 2–4 gestapelde kegellagen met een afgeronde, licht gekartelde rand."""
    leaf = "M_Env_PineFar" if far else "M_Env_PineLeaf"
    trunk_h = height * 0.22
    tr = max(0.10, width * 0.11)
    trunk = lathe_mod([(tr * 1.35, -0.15), (tr * 1.15, 0.04), (tr, trunk_h * 0.6), (tr * 0.85, trunk_h + 0.3), (0, trunk_h + 0.32)],
                      6, mat="M_Env_Trunk", phase=rng.uniform(0, 1))
    parts = [trunk]
    crown0 = trunk_h * 0.85
    span = height - crown0
    for t in range(tiers):
        f = t / max(1, tiers - 1)
        base = crown0 + span * 0.62 * (t / tiers)
        top = base + span * (0.56 - 0.12 * f) if t < tiers - 1 else height
        r = width / 2 * (1.0 - 0.22 * t) * rng.uniform(0.95, 1.05)
        h = top - base
        prof = [(0.0, top), (r * 0.42, top - h * 0.36), (r * 0.86, base + h * 0.18), (r, base + h * 0.04),
                (r * 0.88, base - h * 0.04), (r * 0.36, base), (0.0, base + h * 0.06)]
        jag = 0.05 if not far else 0.0
        ph = rng.uniform(0, 2 * math.pi)
        tier = lathe_mod(prof, seg,
                         rmod=lambda i, k, j=jag: 1.0 + (j if (k % 2 == 0 and i in (2, 3, 4)) else (-j * 0.6 if i in (2, 3, 4) else 0.0)),
                         ymod=lambda i, k, j=jag, hh=h: (-j * hh * 0.5 if (k % 2 == 0 and i in (3, 4)) else 0.0),
                         phase=ph, mat=leaf)
        if lean:
            tier.V[:, 0] += lean * (tier.V[:, 1] - crown0) / span * height * 0.08
        parts.append(tier)
    return mk.merge(*parts)


def bush(rng):
    lumps = [blob(0.55, 0.42, 0.50, rng, 0.10, mat="M_Env_Bush", flat_bottom=0.0).transform(t=(0, 0.30, 0)),
             blob(0.42, 0.34, 0.40, rng, 0.10, mat="M_Env_Bush", flat_bottom=-0.2).transform(t=(0.45, 0.22, 0.12)),
             blob(0.38, 0.30, 0.36, rng, 0.10, mat="M_Env_Bush", flat_bottom=-0.2).transform(t=(-0.42, 0.20, -0.08)),
             blob(0.30, 0.26, 0.30, rng, 0.10, mat="M_Env_Bush", flat_bottom=-0.2).transform(t=(0.05, 0.55, -0.10))]
    return mk.merge(*lumps)


# ------------------------------------------------------------------- rotsen
def rock(size, rng, squash=0.65):
    a = blob(size * 0.55, size * 0.50 * squash, size * 0.48, rng, 0.13, seg=7, rings=4, mat="M_Env_Rock")
    a.V[:, 1] -= a.V[:, 1].min() + size * 0.08          # iets in de grond
    parts = [a]
    if size > 1.2:                                          # grote rots: tweede klont ernaast, één massa
        b = blob(size * 0.34, size * 0.30, size * 0.32, rng, 0.13, seg=6, rings=4, mat="M_Env_Rock")
        b.V[:, 1] -= b.V[:, 1].min() + size * 0.05
        parts.append(b.transform(t=(size * 0.42, 0, size * 0.18)))
    return mk.merge(*parts)


# ------------------------------------------------------------------- kliffen
def cliff_block(x0, x1, z0, z1, H, rng, ledge=True):
    """Klifblok met verticale facetkolommen, een terrasrand halverwege en een grasdek met overhangende rand."""
    base = rect_outline(x0, x1, z0, z1)
    n = len(base)
    col = np.array([0.07 if k % 2 else -0.02 for k in range(n)]) + rng.uniform(-0.02, 0.04, n)   # verticale kolommen
    rings = [(offset(base, -col - 0.14), -0.30),                       # voet: breder, sluit aan op buren
             (offset(base, -col - 0.05), min(0.35, H * 0.18)),
             (offset(base, -col - rng.uniform(0.0, 0.05, n)), H * 0.30)]
    if ledge and H >= 1.8:                                  # terrasrand: klein stapje naar binnen
        yl = H * 0.52
        rings += [(offset(base, -col - 0.06), yl), (offset(base, -col + 0.07), yl + 0.10)]
    rings += [(offset(base, -col + 0.02 - rng.uniform(0.0, 0.05, n)), H * 0.82), (offset(base, -col + 0.06), H - 0.02)]
    body = loft(rings, "M_Env_Cliff")
    drip = rng.uniform(0.06, 0.24, n) * (rng.uniform(0, 1, n) > 0.35)
    grass = loft([(offset(base, -col - 0.05), H - 0.08 - drip),
                  (offset(base, -col - 0.10), H + 0.03),
                  (offset(base, -col + 0.06), H + 0.14),
                  (offset(base, -col + 0.30), H + 0.17)], "M_Env_Grass")
    return mk.merge(body, grass)


def hill(rng):
    base = rect_outline(-3.0, 3.0, -2.0, 2.0, step=0.6, chamfer=0.9)
    n = len(base)
    j = rng.uniform(-0.08, 0.12, n)
    rings = [(offset(base, -j), -0.25), (offset(base, -j + 0.45), 0.45), (offset(base, -j * 0.5 + 0.95), 0.92),
             (offset(base, 1.30), 1.12)]
    return loft(rings, "M_Env_Grass")


# ------------------------------------------------- kliffen v2: clusters van grote, gefacetteerde pijlers
def pillar(x0, x1, z0, z1, H, rng, tufts=True):
    """Pijler zoals in het referentieblad: grote vlakke facetten (2–3 per zijde), afgeschuinde hoeken,
    een vlak grasdek met korte overhang en een paar graspluk-bolletjes."""
    base = rect_outline(x0, x1, z0, z1, step=0.85, chamfer=min(0.35, (x1 - x0) * 0.18, (z1 - z0) * 0.18))
    n = len(base)
    fold = np.array([0.13 if k % 2 else -0.04 for k in range(n)]) + rng.uniform(-0.02, 0.04, n)
    tilt = rng.uniform(-0.06, 0.06, n)
    rings = [(offset(base, -fold - 0.16), -0.30),                     # voet: breder, loopt in de grond over
             (offset(base, -fold - 0.06), min(0.35, H * 0.2)),
             (offset(base, -fold * 1.1 + tilt), H * 0.45),
             (offset(base, -fold * 0.9 + 0.03), H * 0.88),
             (offset(base, -fold * 0.5 + 0.10), H - 0.02)]
    body = loft(rings, "M_Env_Cliff")
    drip = rng.uniform(0.04, 0.16, n) * (rng.uniform(0, 1, n) > 0.45)
    grass = loft([(offset(base, -fold * 0.5 + 0.02), H - 0.07 - drip),
                  (offset(base, -fold * 0.5 - 0.04), H + 0.04),
                  (offset(base, -fold * 0.5 + 0.12), H + 0.12),
                  (offset(base, min(0.40, 0.22 * min(x1 - x0, z1 - z0))), H + 0.14)], "M_Env_Grass")
    parts = [body, grass]
    if tufts:
        for _ in range(rng.integers(1, 4)):
            tx = rng.uniform(x0 + 0.3, x1 - 0.3)
            tz = rng.uniform(z0 + 0.3, z1 - 0.3)
            s = rng.uniform(0.20, 0.34)
            t = blob(s * 1.1, s * 0.95, s, rng, 0.10, seg=7, rings=4, mat="M_Env_Bush", flat_bottom=-s * 0.5)
            parts.append(t.transform(t=(tx, H + 0.12 + s * 0.45, tz)))
    return mk.merge(*parts)


def cluster(blocks, rng):
    """Klifcluster als één massa: een gedeelde voet (laag terras met gras) over de hele footprint, waaruit
    pijlers van verschillende hoogte oprijzen die elkaar overlappen. Geen losse blokken met kieren meer."""
    xs0, xs1 = min(b[0] for b in blocks), max(b[1] for b in blocks)
    zs0, zs1 = min(b[2] for b in blocks), max(b[3] for b in blocks)
    hb = max(0.7, min(b[4] for b in blocks) * 0.55)
    parts = [pillar(xs0 + 0.05, xs1 - 0.05, zs0 + 0.05, zs1 - 0.05, hb, rng, tufts=True)]
    g = 0.14                                                          # pijlers groeien in elkaar
    for (x0, x1, z0, z1, h) in sorted(blocks, key=lambda b: -b[4]):
        parts.append(pillar(x0 - g, x1 + g, z0 - g, z1 + g, h, rng))
    return mk.merge(*parts)


def _fall_profile(H, lip_r, back, drift, n_arc=6, n_fall=14):
    """Middellijn van het water (u = naar voren, v = hoogte): stroom bovenop -> ronde lip -> val met lichte boog."""
    pts = [(-back, H), (-back * 0.5, H), (-0.05, H)]
    for k in range(1, n_arc + 1):                                  # lip: kwartcirkel over de rand
        a = math.pi / 2 * (1 - k / n_arc)
        pts.append((lip_r * math.cos(a), H - lip_r + lip_r * math.sin(a)))
    v0 = H - lip_r
    for k in range(1, n_fall + 1):
        f = k / n_fall
        pts.append((lip_r + drift * math.sqrt(f), v0 * (1 - f) - 0.05 * f))
    return pts


def waterfall(H, W, rng, strips=None):
    """Stilstaande waterval in 'plaatje'-stijl, v2: één doorlopend, gegroefd watervel dat als stroom over de
    klifrand komt, over een ronde lip buigt en in een lichte boog valt. Verticale banen in drie watertinten
    (facetten vangen elk anders het licht), dunne lichtstrepen, gerafelde onderrand, een poel met lichte
    ring en een wolk van schuim en mist. Pivot midden-onder; de lip ligt op y = H tegen een klifrand (-Z)."""
    parts = []
    lip_r = min(0.45, 0.10 * H + 0.10)
    drift = 0.10 + 0.04 * H
    prof = _fall_profile(H, lip_r, back=1.2, drift=drift)
    nprof = len(prof)
    cols = max(6, int(round(W / 0.16)))
    xs = np.linspace(-W / 2, W / 2, cols + 1)
    # groeven: zigzag over de breedte (in/uit) + willekeur, sterker naar beneden; randen iets naar achteren
    ridge = np.array([0.075 * (1 if j % 2 else -1) + rng.uniform(-0.02, 0.02) for j in range(cols + 1)])
    edge = np.clip(1.0 - np.abs(np.linspace(-1, 1, cols + 1)) ** 6, 0.0, 1.0)
    bottom = rng.uniform(0.0, 0.35, cols + 1) * (0.6 + 0.4 * (np.arange(cols + 1) % 2))
    t = 0.10
    V, F, M = [], [], []

    def vid(p):
        V.append(p)
        return len(V) - 1
    front = np.zeros((nprof, cols + 1), int)
    back = np.zeros((nprof, cols + 1), int)
    for i, (u, v) in enumerate(prof):
        fall = max(0.0, min(1.0, (H - v) / H)) if u > 0 else 0.0
        for j, x in enumerate(xs):
            vv = max(v, bottom[j]) if i == nprof - 1 else v
            uu = u + ridge[j] * (0.25 + 0.75 * fall) - (1 - edge[j]) * 0.06
            front[i, j] = vid((x, vv, uu))
            # achterkant: naar binnen (richting klif / naar beneden bij de stroom)
            if u <= 0:
                back[i, j] = vid((x, vv - t, uu))
            else:
                back[i, j] = vid((x, vv, uu - t))
    band = []
    for j in range(cols):
        r = rng.uniform()
        band.append("M_Env_WaterLight" if r < 0.28 else ("M_Env_WaterDeep" if r > 0.86 else "M_Env_Water"))
    band[0] = band[-1] = "M_Env_WaterDeep"
    for i in range(nprof - 1):
        for j in range(cols):
            a, b, c, d = front[i, j], front[i, j + 1], front[i + 1, j + 1], front[i + 1, j]
            F.append((a, d, c, b)); M.append(band[j] if prof[i][0] > -0.3 else "M_Env_Water")
            a, b, c, d = back[i, j], back[i, j + 1], back[i + 1, j + 1], back[i + 1, j]
            F.append((a, b, c, d)); M.append("M_Env_WaterDeep")
    for i in range(nprof - 1):                                          # zijkanten
        for j, flip in ((0, False), (cols, True)):
            q = (front[i, j], front[i + 1, j], back[i + 1, j], back[i, j])
            F.append(q[::-1] if flip else q); M.append("M_Env_WaterDeep")
    for j in range(cols):                                               # onder- en achterrand
        for i, flip in ((nprof - 1, False), (0, True)):
            q = (front[i, j], front[i, j + 1], back[i, j + 1], back[i, j])
            F.append(q[::-1] if flip else q); M.append("M_Env_WaterDeep")
    sheet = mk.Mesh(np.array(V, float), F, M)
    sheet.sharp = True
    parts.append(mk._orient_closed(sheet))
    # dunne lichtstrepen vóór het vel (het water 'glanst' in banen)
    for _ in range(max(2, int(W / 0.5))):
        x = rng.uniform(-W / 2 + 0.15, W / 2 - 0.15)
        y0, y1 = rng.uniform(0.3, H * 0.35), rng.uniform(H * 0.55, H - lip_r - 0.1)
        w = rng.uniform(0.025, 0.045)
        u = lip_r + drift * math.sqrt(max(0.0, (H - lip_r - (y0 + y1) / 2) / (H - lip_r))) + 0.06
        streak = mk.rounded_box(w * 2, y1 - y0, 0.02, w * 0.9, 1, mat="M_Env_WaterLight").transform(t=(x, (y0 + y1) / 2, u))
        parts.append(streak)
    # poel aan de voet met een lichte ring
    pr = W * 0.75 + 0.5
    pool = lathe_mod([(0.0, 0.03), (pr * 0.55, 0.03), (pr, 0.02), (pr * 1.02, -0.06), (0.0, -0.06)], 14,
                     rmod=lambda i, k: 1.0 + 0.06 * math.sin(k * 2.1), mat="M_Env_WaterDeep")
    pool.V[:, 2] *= 0.55
    ring = lathe_mod([(pr * 0.50, 0.045), (pr * 0.62, 0.045), (pr * 0.62, 0.02), (pr * 0.50, 0.02)], 14,
                     rmod=lambda i, k: 1.0 + 0.06 * math.sin(k * 2.1), mat="M_Env_WaterLight")
    ring.V[:, 2] *= 0.55
    parts += [pool.transform(t=(0, 0, lip_r + drift + 0.2)), ring.transform(t=(0, 0, lip_r + drift + 0.2))]
    # schuim: twee rijen puffen + spetters, en een doorzichtige mistwolk
    zf = lip_r + drift + 0.05
    n = max(4, int(W / 0.35))
    for row, (zo, sc, yo) in enumerate(((0.0, 1.0, 0.0), (0.35, 0.75, -0.05))):
        for k in range(n + row):
            x = -W / 2 - 0.1 + (W + 0.2) * (k + 0.5) / (n + row) + rng.uniform(-0.08, 0.08)
            r = rng.uniform(0.26, 0.40) * sc * (0.8 + 0.3 * min(1.0, W / 2.0))
            puff = mk.ellipsoid(r * 1.15, r * 0.85, r * 0.95, 12, 7, mat="M_Env_Foam")
            puff.V[:, 1] = np.maximum(puff.V[:, 1], -r * 0.15)
            parts.append(puff.transform(t=(x, r * 0.25 + yo, zf + zo + rng.uniform(-0.05, 0.08))))
    for k in range(max(3, n)):
        r = rng.uniform(0.06, 0.12)
        parts.append(mk.ellipsoid(r, r * 0.9, r, 8, 5, mat="M_Env_Foam").transform(
            t=(rng.uniform(-W / 2 - 0.4, W / 2 + 0.4), rng.uniform(0.05, 0.55), zf + rng.uniform(0.45, 1.0))))
    nm = max(2, int(W / 0.7) + 1)
    for k in range(nm):
        r = rng.uniform(0.40, 0.60) * (0.7 + 0.3 * min(1.0, W / 2.0))
        mist = mk.ellipsoid(r * 1.3, r, r, 12, 7, mat="M_Env_Mist")
        x = -W / 2 + W * (k + 0.5) / nm + rng.uniform(-0.1, 0.1)
        parts.append(mist.transform(t=(x, rng.uniform(0.35, 0.8), zf + rng.uniform(0.05, 0.35))))
    return mk.merge(*parts)


# ------------------------------------------------------- paden en planten
STONE_MATS = ["M_Env_PathStone", "M_Env_PathStoneMid", "M_Env_PathStoneDark", "M_Env_PathStoneWarm"]
STONE_WEIGHTS = [0.32, 0.30, 0.24, 0.14]


def stone(r, rng, h=0.07, mat=None):
    """Afgeronde kei (laag-poly), licht in de grond: gefacetteerde bolle bovenkant (vlakken vangen elk
    ander licht), schuine rand. Zonder mat krijgt elke kei willekeurig een licht/middel/donker/warm tint."""
    if mat is None:
        mat = STONE_MATS[rng.choice(len(STONE_MATS), p=STONE_WEIGHTS)]
    seg = 7
    rk = rng.uniform(0.85, 1.15, seg)
    dy = rng.uniform(-0.55, 0.55, (6, seg)) * h          # scheve facetten op de bovenkant
    prof = [(0.0, h * 2.6), (r * 0.40, h * 2.25), (r * 0.76, h * 1.45), (r, h * 0.45), (r * 0.96, -0.04), (0.0, -0.04)]
    m = lathe_mod(prof, seg, rmod=lambda i, k, rk=rk: rk[k],
                  ymod=lambda i, k, dy=dy: dy[i, k] if i in (1, 2) else (dy[0, 0] * 0.5 if i == 0 else 0.0),
                  phase=rng.uniform(0, 6.28), mat=mat)
    m.V[:, 0] *= rng.uniform(0.85, 1.15)
    return m


def place_stones(points, rng, rmin=0.20, rmax=0.29):
    return [stone(rng.uniform(rmin, rmax), rng).transform(R=mk.rot_y(rng.uniform(0, 6.28)), t=(x, 0.0, z)) for (x, z) in points]


def poisson(area_fn, n, min_d, rng, tries=4000):
    pts = []
    for _ in range(tries):
        p = area_fn()
        if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 >= min_d ** 2 for q in pts):
            pts.append(p)
            if len(pts) >= n:
                break
    return pts


def path_bed(outline, mat="M_Env_Dirt"):
    """Lage, platte grondstrook onder de keien (leest als pad, ook op gras)."""
    return loft([(offset(outline, -0.02), -0.03), (outline, 0.015), (offset(outline, 0.06), 0.02)], mat)


def path_straight(rng):
    pts = poisson(lambda: (rng.uniform(-0.85, 0.85), rng.uniform(-0.30, 0.30)), 8, 0.62, rng)
    bed = path_bed(rect_outline(-1.0, 1.0, -0.55, 0.55, step=0.25, chamfer=0.25))
    return mk.merge(bed, *place_stones(pts, rng))


def path_curve(rng):
    """Kwartbocht (straal 2 m tot het midden, 1,1 m breed), eindigt op het raster zoals het rechte stuk."""
    rc = 2.0
    def pt():
        a = rng.uniform(0.08, math.pi / 2 - 0.08)
        r = rc + rng.uniform(-0.38, 0.38)
        return (rc - r * math.cos(a), -rc + r * math.sin(a))
    pts = poisson(pt, 10, 0.62, rng)
    arc = []
    for k in range(9):
        a = math.pi / 2 * k / 8
        arc.append((rc - (rc + 0.55) * math.cos(a), -rc + (rc + 0.55) * math.sin(a)))
    for k in range(8, -1, -1):
        a = math.pi / 2 * k / 8
        arc.append((rc - (rc - 0.55) * math.cos(a), -rc + (rc - 0.55) * math.sin(a)))
    outline = np.array(arc)
    if mk._poly_area(outline) < 0:
        outline = outline[::-1]
    return mk.merge(path_bed(outline), *place_stones(pts, rng))


def path_patch(rng):
    pts = poisson(lambda: tuple(rng.uniform(-0.65, 0.65, 2)), 8, 0.60, rng)
    pts = [p for p in pts if p[0] ** 2 + p[1] ** 2 < 0.72 ** 2]
    a = np.linspace(0, 2 * math.pi, 14, endpoint=False)
    outline = np.stack([np.cos(a) * 0.95 * rng.uniform(0.9, 1.1, 14), np.sin(a) * 0.95 * rng.uniform(0.9, 1.1, 14)], -1)
    return mk.merge(path_bed(outline), *place_stones(pts, rng))


def daisy(rng, h):
    """Madeliefje: dun steeltje, gele kern, 6 witte blaadjes (zacht, in de stijl van het concept)."""
    stem = mk.capsule_between((0, 0, 0), (0, h, 0), 0.012, 0.010, 5, 1, mat="M_Env_Stem")
    heart = mk.ellipsoid(0.035, 0.022, 0.035, 8, 4, mat="M_Env_FlowerHeart").transform(t=(0, h + 0.01, 0))
    petals = []
    for k in range(6):
        a = 2 * math.pi * k / 6
        pet = mk.ellipsoid(0.045, 0.012, 0.022, 6, 3, mat="M_Env_Petal")
        petals.append(pet.transform(R=mk.rot_y(-a), t=(math.cos(a) * 0.055, h + 0.004, math.sin(a) * 0.055)))
    return mk.merge(stem, heart, *petals).transform(R=mk.rot_z(rng.uniform(-0.15, 0.15)) @ mk.rot_y(rng.uniform(0, 6.28)))


def grass_tuft(rng, n=7, h=0.32):
    blades = []
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.3, 0.3)
        r = rng.uniform(0.02, 0.09)
        hh = h * rng.uniform(0.6, 1.1)
        tip = (math.cos(a) * (r + hh * 0.35), hh, math.sin(a) * (r + hh * 0.35))
        b = lathe_mod([(0.035, 0.0), (0.0, 1.0)], 4, mat="M_Env_Bush")
        b.V[:, 1] *= hh
        # scheeftrekken naar de tip
        b.V[:, 0] += b.V[:, 1] / hh * (tip[0] - math.cos(a) * r)
        b.V[:, 2] += b.V[:, 1] / hh * (tip[2] - math.sin(a) * r)
        blades.append(b.transform(t=(math.cos(a) * r, -0.02, math.sin(a) * r)))
    return mk.merge(*blades)


def flower_patch(rng, n_daisies=5, n_tufts=3, radius=0.45):
    parts = []
    for _ in range(n_tufts):
        parts.append(grass_tuft(rng, 6, 0.28).transform(t=(rng.uniform(-radius, radius), 0, rng.uniform(-radius, radius))))
    for _ in range(n_daisies):
        parts.append(daisy(rng, rng.uniform(0.12, 0.22)).transform(t=(rng.uniform(-radius, radius), 0, rng.uniform(-radius, radius))))
    return mk.merge(*parts)


# ------------------------------------------------------------------- bergen
def mountain(width, height, rng, peaks=1, seg=9):
    """Verre berg: groot facetsilhouet (plat in de diepte) met een simpele, gekartelde sneeuwkap."""
    parts = []
    for p in range(peaks):
        w = width * (1.0 if p == 0 else rng.uniform(0.55, 0.75))
        h = height * (1.0 if p == 0 else rng.uniform(0.6, 0.8))
        cx = 0.0 if p == 0 else (width * 0.38 * (1 if p == 1 else -1))
        rk = rng.uniform(0.85, 1.15, seg)
        fr = [(1.0, -2.0), (0.80, h * 0.25), (0.48, h * 0.58), (0.20, h * 0.86), (0.0, h)]

        def rb(y):
            for (r0, y0), (r1, y1) in zip(fr[:-1], fr[1:]):
                if y0 <= y <= y1:
                    return w / 2 * (r0 + (r1 - r0) * (y - y0) / (y1 - y0))
            return 0.0
        ph = rng.uniform(0, 2 * math.pi)
        body = lathe_mod([(w / 2 * r, y) for (r, y) in fr], seg, rmod=lambda i, k, rk=rk: rk[k], phase=ph, mat="M_Env_Mountain")
        y0 = h * 0.64
        dy = [h * 0.07 if k % 2 else 0.0 for k in range(seg)]
        cap = lathe_mod([(rb(y0), y0), (rb(h * 0.86), h * 0.86), (0.0, h + 0.25)], seg,
                        rmod=lambda i, k, rk=rk: rk[k] * 1.035 * (rb(y0 + dy[k]) / rb(y0) if i == 0 else 1.0),
                        ymod=lambda i, k: dy[k] if i == 0 else 0.0, phase=ph, mat="M_Env_Snow")
        for m in (body, cap):
            m.V[:, 2] *= 0.55                                # plat in de diepte: silhouet
        parts += [body.transform(t=(cx, 0, 0)), cap.transform(t=(cx, 0, 0))]
    return mk.merge(*parts)


def cloud(rng, n=5, w=6.0):
    puffs = []
    for k in range(n):
        x = (k - (n - 1) / 2) * w / n + rng.uniform(-0.3, 0.3)
        r = w / n * rng.uniform(0.75, 1.1) * (1.25 if abs(k - (n - 1) / 2) < 1 else 0.95)
        b = mk.ellipsoid(r, r * 0.80, r * 0.75, 12, 7, mat="M_Env_Cloud")
        b.V[:, 1] = np.maximum(b.V[:, 1], -r * 0.25)        # vlakke onderkant
        puffs.append(b.transform(t=(x, r * 0.25 + (0.35 if abs(k - (n - 1) / 2) < 1 else 0.0), rng.uniform(-0.2, 0.2))))
    m = mk.merge(*puffs)
    m.sharp = False
    m.FS = None
    return m


# ------------------------------------------------------------------ export
def make_scene(name, mesh, density, radius, gradient=0.25, facet=True):
    sc = mk.Scene(name)
    for mname in sorted(set(mesh.M)):          # vaste volgorde: geen ruis in FBX/meta-diffs
        b, ramp, sm, sh, op = MATERIALS[mname]
        sc.material(mname, b, ramp, sm, ramp_shift=sh, opts=dict(op))
    sc.add(mk.Part(name, mesh, (0, 0, 0), None))
    sc.parts[0].gradient = gradient
    if facet:
        sc.facet_all()
    sc.bake_shading(density=density, radius=radius, noise_freq=0.8, edge_range=(3.0, 12.0), max_samples=40000)
    return sc


def build_all():
    rng = np.random.default_rng(2026)
    A = {}
    A["Trees/Tree_Pine_Small_01"] = (pine(2.2, 2, 1.7, rng), 900, 0.20)
    A["Trees/Tree_Pine_Medium_01"] = (pine(3.6, 3, 2.5, rng), 600, 0.25)
    A["Trees/Tree_Pine_Large_01"] = (pine(5.6, 4, 3.4, rng), 300, 0.30)
    A["Trees/Tree_Pine_Variant_02"] = (pine(4.2, 3, 2.7, rng, lean=1.0, seg=7), 400, 0.25)
    A["Trees/Tree_Pine_Background_01"] = (pine(6.5, 2, 3.4, rng, seg=6, far=True), 150, 0.40)
    A["Trees/Bush_01"] = (bush(rng), 900, 0.20)
    A["Rocks/Rock_Small_01"] = (rock(0.6, rng), 1500, 0.15)
    A["Rocks/Rock_Medium_01"] = (rock(1.2, rng, 0.75), 900, 0.20)
    A["Rocks/Rock_Large_01"] = (rock(2.4, rng, 0.70), 300, 0.35)
    A["Cliffs/Cliff_Module_A"] = (cliff_block(-1, 1, -1, 1, 2.0, rng), 250, 0.35)
    A["Cliffs/Cliff_Module_B"] = (cliff_block(-2, 2, -1, 1, 3.0, rng), 160, 0.40)
    terr = mk.merge(cliff_block(-1, 1, 0.0, 1, 1.2, rng, ledge=False), cliff_block(-1, 1, -1, 0.15, 2.6, rng))
    A["Cliffs/Cliff_Module_C"] = (terr, 250, 0.35)
    A["Cliffs/Hill_Grass_01"] = (hill(rng), 120, 0.50)
    # v2: clusters in referentiestijl (grote facetten, terrassen) — meer variatie en grotere formaten
    A["Cliffs/Cliff_Pillar_Small"] = (pillar(-0.75, 0.75, -0.75, 0.75, 2.4, rng), 300, 0.30)
    A["Cliffs/Cliff_Pillar_Tall"] = (pillar(-1, 1, -1, 1, 4.6, rng), 200, 0.35)
    A["Cliffs/Cliff_Ledge_Low"] = (pillar(-2, 2, -1, 1, 1.0, rng), 200, 0.35)
    A["Cliffs/Cliff_Block_Wide"] = (pillar(-2, 2, -1, 1, 2.2, rng), 200, 0.35)
    A["Cliffs/Cliff_Cluster_Step"] = (cluster([(-3.0, -0.8, -1.4, 0.4, 4.2), (-1.0, 1.2, -1.2, 0.8, 2.8),
                                                (0.9, 3.0, -1.0, 1.2, 1.5)], rng), 150, 0.40)
    A["Cliffs/Cliff_Cluster_Corner"] = (cluster([(-2.0, 0.2, -2.0, 0.2, 3.6), (0.0, 2.0, -2.0, -0.2, 2.4),
                                                  (-2.0, -0.2, 0.0, 2.0, 1.8), (-0.1, 1.2, 0.0, 1.2, 1.0)], rng), 150, 0.40)
    A["Cliffs/Cliff_Cluster_Tall"] = (cluster([(-3.0, -0.4, -2.0, 0.6, 6.5), (-0.6, 2.4, -2.2, 0.2, 5.0),
                                                (-2.4, 0.4, 0.4, 2.0, 2.6), (0.2, 3.0, 0.0, 1.8, 1.6)], rng), 100, 0.45)
    A["Cliffs/Cliff_Big_01"] = (cluster([(-4.0, 0.2, -3.0, 1.0, 9.5), (-0.2, 4.0, -3.2, 0.4, 7.4),
                                          (-4.2, -0.8, 0.6, 3.0, 4.6), (-1.0, 2.2, 0.2, 2.6, 3.0),
                                          (2.0, 4.4, 0.2, 2.4, 1.8)], rng), 60, 0.55)
    rng = np.random.default_rng([2026, 1])          # eigen reeks per sectie: wijzigingen lopen niet door
    A["Paths/Stone_Single_01"] = (stone(0.32, rng, mat="M_Env_PathStone"), 2500, 0.10)
    A["Paths/Stone_Single_02"] = (stone(0.45, rng, mat="M_Env_PathStoneMid"), 2500, 0.10)
    A["Paths/Stone_Single_03"] = (stone(0.60, rng, 0.09, mat="M_Env_PathStoneDark"), 2000, 0.12)
    A["Paths/Stone_Single_04"] = (stone(0.40, rng, mat="M_Env_PathStoneWarm"), 2500, 0.10)
    A["Paths/Stone_Single_05"] = (stone(0.26, rng, mat="M_Env_PathStoneDark"), 2500, 0.10)
    A["Paths/Stone_Single_06"] = (stone(0.52, rng, 0.08, mat="M_Env_PathStone"), 2000, 0.12)
    A["Paths/Path_Stones_Straight"] = (path_straight(rng), 900, 0.15)
    A["Paths/Path_Stones_Curve"] = (path_curve(rng), 700, 0.15)
    A["Paths/Path_Stones_Patch"] = (path_patch(rng), 900, 0.15)
    rng = np.random.default_rng([2026, 2])          # eigen reeks per sectie: wijzigingen lopen niet door
    A["Plants/Flower_Daisy_01"] = (mk.merge(daisy(rng, 0.18), daisy(rng, 0.14).transform(t=(0.10, 0, 0.06)),
                                            daisy(rng, 0.20).transform(t=(-0.08, 0, 0.09))), 4000, 0.05)
    A["Plants/Grass_Tuft_01"] = (grass_tuft(rng, 7, 0.32), 3000, 0.06)
    A["Plants/Grass_Tuft_02"] = (grass_tuft(rng, 10, 0.45), 3000, 0.06)
    A["Plants/Flower_Patch_01"] = (flower_patch(rng), 2500, 0.08)
    rng = np.random.default_rng([2026, 3])          # eigen reeks per sectie: wijzigingen lopen niet door
    A["Water/Waterfall_Large"] = (waterfall(6.0, 2.4, rng), 200, 0.30)
    A["Water/Waterfall_Medium"] = (waterfall(4.0, 1.6, rng), 250, 0.30)
    A["Water/Waterfall_Small"] = (waterfall(2.4, 0.9, rng), 300, 0.25)
    rng = np.random.default_rng([2026, 4])          # eigen reeks per sectie: wijzigingen lopen niet door
    A["Mountains/Mountain_Back_01"] = (mountain(46, 30, rng), 0.6, 4.0)
    A["Mountains/Mountain_Back_02"] = (mountain(60, 34, rng, peaks=2), 0.4, 4.0)
    A["Mountains/Mountain_Back_03"] = (mountain(70, 22, rng, peaks=3, seg=8), 0.4, 4.0)
    rng = np.random.default_rng([2026, 5])          # eigen reeks per sectie: wijzigingen lopen niet door
    A["Sky/Cloud_01"] = (cloud(rng, 5, 6.0), 40, 0.6)
    A["Sky/Cloud_02"] = (cloud(rng, 4, 4.5), 50, 0.6)
    A["Sky/Cloud_03"] = (cloud(rng, 3, 3.0), 60, 0.6)

    os.makedirs(EXP_DIR, exist_ok=True)
    unity_assets.folder_meta(ROOT, MAT_DIR)
    guids = {n: unity_assets.material(ROOT, f"{MAT_DIR}/{n}.mat", n, b, sm, None, op.get("opacity"), ramp, sh, op)
             for n, (b, ramp, sm, sh, op) in MATERIALS.items()}
    manifest = {}
    for rel, (mesh, dens, rad) in A.items():
        name = os.path.basename(rel)
        smooth = rel.startswith("Sky")
        sc = make_scene(name, mesh, dens, rad, gradient=0.0 if smooth or rel.startswith("Water") else 0.25, facet=not smooth)
        if rel.startswith("Water"):                         # schuim zacht, water facet
            m = sc.parts[0].mesh
            m.FS = [mm not in ("M_Env_Foam", "M_Env_Mist") for mm in m.M]
            m.sharp = False
        fbx_rel = f"{MODEL_DIR}/{rel}.fbx"
        os.makedirs(os.path.join(ROOT, os.path.dirname(fbx_rel)), exist_ok=True)
        unity_assets.folder_meta(ROOT, os.path.dirname(fbx_rel))
        sc.write_fbx_ascii(os.path.join(ROOT, fbx_rel))
        sc.write_glb(os.path.join(EXP_DIR, name + ".glb"))
        unity_assets.model_meta(ROOT, fbx_rel, {m: guids[m] for m in sc.materials})
        V = mesh.V
        manifest[name] = {"category": rel.split("/")[0], "fbx": fbx_rel, "tris": sc.stats()["tris"],
                          "min": np.round(V.min(0), 3).tolist(), "max": np.round(V.max(0), 3).tolist(),
                          "materials": sorted(sc.materials)}
        print(f"{name:26s} {sc.stats()['tris']:6d} tris")
    mats = {n: {"base": b, "ramp": ramp, "rampShift": sh, "smoothness": sm, "opts": op} for n, (b, ramp, sm, sh, op) in MATERIALS.items()}
    json.dump({"assets": manifest, "materials": mats}, open(os.path.join(EXP_DIR, "EnvironmentKit.json"), "w", encoding="utf-8"), indent=2)


if __name__ == "__main__":
    build_all()
