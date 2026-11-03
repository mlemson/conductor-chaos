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
    "M_Env_Cliff": (R["cliff"][2], R["cliff"], 0.05, 0.0, {"cells": 1.0, "cellSize": 0.8, "rim": 0.22}),
    "M_Env_Grass": (R["grass"][2], R["grass"], 0.05, 0.02, {"cells": 0.8, "cellSize": 0.9}),
    "M_Env_Mountain": (R["mountain"][2], R["mountain"], 0.05, 0.02, {"cells": 0.8, "cellSize": 6.0, "rim": 0.12, "edge": 0.6}),
    "M_Env_Snow": (R["cloud_snow"][3], R["cloud_snow"], 0.05, 0.05, {"cells": 0.4, "cellSize": 5.0, "rim": 0.10}),
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
    rings = [(offset(base, -col + 0.02), -0.30),
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
    for mname in set(mesh.M):
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
    A["Mountains/Mountain_Back_01"] = (mountain(46, 30, rng), 0.6, 4.0)
    A["Mountains/Mountain_Back_02"] = (mountain(60, 34, rng, peaks=2), 0.4, 4.0)
    A["Mountains/Mountain_Back_03"] = (mountain(70, 22, rng, peaks=3, seg=8), 0.4, 4.0)
    A["Sky/Cloud_01"] = (cloud(rng, 5, 6.0), 40, 0.6)
    A["Sky/Cloud_02"] = (cloud(rng, 4, 4.5), 50, 0.6)
    A["Sky/Cloud_03"] = (cloud(rng, 3, 3.0), 60, 0.6)

    os.makedirs(EXP_DIR, exist_ok=True)
    unity_assets.folder_meta(ROOT, MAT_DIR)
    guids = {n: unity_assets.material(ROOT, f"{MAT_DIR}/{n}.mat", n, b, sm, None, op.get("opacity"))
             for n, (b, ramp, sm, sh, op) in MATERIALS.items()}
    manifest = {}
    for rel, (mesh, dens, rad) in A.items():
        name = os.path.basename(rel)
        sc = make_scene(name, mesh, dens, rad, gradient=0.0 if rel.startswith("Sky") else 0.25,
                        facet=not rel.startswith("Sky"))
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
