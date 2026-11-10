"""Losse wereldprops bij de dieren: hooibaal en melkbus (concept: Docs/References/Animals/*-concept.png).

* `Prop_HayBale`  dezelfde maat als de balen in de veewagen (0,84 x 0,40 x 0,52 m; 8 mm vrij van de grond, de
                  banden dragen), twee leren banden, wat sprieten.
* `Prop_MilkCan`  melkbus van 0,62 m met deksel, twee gouden handvatten en een koeienkop in reliëf.

Beide staan met de onderkant op y = 0, midden op de oorsprong (pivot = grond), 1 unit = 1 m.
Kleuren: ramps crate + accent_yellow (stro), leather, metal, gold_trim (palette.json).
Uitvoer: Assets/Art/Models/Animals/<Prop>/<Prop>.fbx (+ ArtSource/Exports/Animals/<Prop>.glb en .materials.json)
Gebruik:  python ArtSource/Animals/build_animal_props.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import animal_kit as ak  # noqa: E402
from animal_kit import ck, mk, DEG  # noqa: E402

BAKE = dict(density=2500, radius=0.12, noise_freq=4.0, edge_range=(14.0, 45.0))


# ------------------------------------------------------------------- hooibaal
def build_hay_bale(P):
    L, H, D = 0.84, 0.40, 0.52
    parts = [mk.rounded_box(L, H, D, 0.055, 3, mat=P + "Straw").transform(t=(0, H / 2 + 0.008, 0))]
    for x in (-0.21, 0.21):                                  # banden: 1 cm buiten de baal (geen samenvallende vlakken)
        parts.append(mk.rounded_box(0.062, H + 0.018, D + 0.020, 0.018, 2, mat=P + "Strap").transform(t=(x, H / 2 + 0.009, 0)))
    rng = np.random.default_rng(3)
    for _ in range(14):                                      # sprieten die uit de kopse kanten en bovenkant steken
        face = rng.integers(0, 3)
        ln = rng.uniform(0.05, 0.09)
        blade = mk.lathe([(0.0, 0.0), (0.008, 0.004), (0.007, ln * 0.7), (0.0, ln)], 6, 1.0, 0.45, mat=P + "StrawLight")
        tilt = mk.rot_z(rng.uniform(-0.5, 0.5)) @ mk.rot_x(rng.uniform(-0.5, 0.5))
        if face == 0:
            x = rng.choice([-0.36, -0.06, 0.06, 0.36]) + rng.uniform(-0.04, 0.04)
            pos, R = (x, H - 0.004, rng.uniform(-0.18, 0.18)), tilt
        else:
            s = 1 if face == 1 else -1
            pos, R = (s * (L / 2 - 0.012), rng.uniform(0.10, H - 0.08), rng.uniform(-0.18, 0.18)), mk.rot_z(-s * 80 * DEG) @ tilt
        parts.append(blade.transform(R=R, t=pos))
    return mk.merge(*parts)


def hay_scene():
    asset, P = "Prop_HayBale", "M_HayBale_"
    sc = mk.Scene(asset)
    ck.material(sc, P + "Straw", "crate", 0.30, 0.20, paint=1.2, cells=0.6, cellSize=0.25)
    ck.material(sc, P + "StrawLight", "accent_yellow", 0.50, 0.20)
    ck.material(sc, P + "Strap", "leather", 0.0, 0.25)
    sc.add(mk.Part(asset, None))
    sc.add(mk.Part("HayBale", build_hay_bale(P), (0, 0, 0), asset, ao_group="bale", gradient=0.14))
    return sc


# -------------------------------------------------------------------- melkbus
def cow_icon(mat, w=0.105, depth=0.010):
    """Koeienkop in reliëf (lokaal XY, dikte langs +Z vanaf 0): kop, twee oren en twee hoorntjes als één silhouet."""
    def rb(sx, sy, r, t):
        return mk.rounded_box(sx, sy, depth, min(r, depth * 0.45), 1, mat=mat).transform(t=(t[0], t[1], depth / 2))
    s = w
    head = [(-0.30, 0.22), (0.30, 0.22), (0.34, 0.02), (0.24, -0.34), (-0.24, -0.34), (-0.34, 0.02)]
    parts = [ck.plate([(x * s, y * s) for x, y in head], depth, mat, chamfer=0.003)]
    for sx in (1, -1):
        ear = [(0.28, 0.16), (0.50, 0.24), (0.52, 0.10), (0.30, 0.02)]
        horn = [(0.14, 0.20), (0.30, 0.20), (0.30, 0.44), (0.22, 0.40)]
        for poly in (ear, horn):
            pts = [(sx * x * s, y * s) for x, y in poly]
            parts.append(ck.plate(pts if sx > 0 else pts[::-1], depth * 0.9, mat, chamfer=0.003))
    return mk.merge(*parts)


def build_milk_can(P):
    R, H = 0.170, 0.62
    prof = [(0.0, 0.0), (R - 0.020, 0.0), (R - 0.004, 0.010), (R, 0.030), (R, 0.375), (R - 0.006, 0.405),
            (R - 0.030, 0.445), (0.118, 0.480), (0.112, 0.500), (0.112, 0.540), (0.120, 0.552), (0.142, 0.560),
            (0.146, 0.572), (0.140, 0.584), (0.0, 0.584)]
    can = mk.lathe(prof, 36, mat=P + "Metal")
    # twee ribbels (elk één ring die 6 mm buiten de wand steekt)
    ribs = [mk.torus(R + 0.001, 0.011, 36, 8, mat=P + "MetalDark").transform(t=(0, y, 0)) for y in (0.075, 0.345)]
    lid = mk.lathe([(0.0, 0.578), (0.132, 0.578), (0.136, 0.590), (0.128, 0.604), (0.060, 0.612), (0.040, 0.624),
                    (0.034, 0.640), (0.0, 0.644)], 36, mat=P + "MetalDark")
    parts = [can, lid] + ribs
    for s in (1, -1):                                         # handvatten aan weerszijden van de schouder
        pts = [(s * 0.150, 0.415, 0), (s * 0.215, 0.440, 0), (s * 0.232, 0.490, 0), (s * 0.200, 0.535, 0),
               (s * 0.118, 0.530, 0)]
        parts.append(ck.tube(pts, 0.017, P + "Handle", n=22, seg=10))
        for (x, y) in ((0.156, 0.418), (0.122, 0.528)):       # bevestigingsplaatjes
            parts.append(mk.ellipsoid(0.022, 0.026, 0.026, 12, 8, mat=P + "Handle").transform(t=(s * x, y, 0)))
    icon = cow_icon(P + "MetalLight", w=0.165, depth=0.012).transform(t=(0, 0.215, R - 0.003))
    V = icon.V.copy()                                         # buig het reliëf om de bus (x = booglengte)
    th = V[:, 0] / R
    r = V[:, 2]
    V[:, 0], V[:, 2] = r * np.sin(th), r * np.cos(th)
    icon.V = V
    parts.append(icon)
    return mk.merge(*parts)


def milk_scene():
    asset, P = "Prop_MilkCan", "M_MilkCan_"
    sc = mk.Scene(asset)
    ck.material(sc, P + "Metal", "metal", 0.20, 0.35, paint=1.1, cells=0.5, cellSize=0.3)
    ck.material(sc, P + "MetalDark", "metal", -0.15, 0.35)
    ck.material(sc, P + "MetalLight", "metal", 0.75, 0.35)
    ck.material(sc, P + "Handle", "gold_trim", 0.05, 0.35)
    sc.add(mk.Part(asset, None))
    sc.add(mk.Part("MilkCan", build_milk_can(P), (0, 0, 0), asset, ao_group="can", gradient=0.14))
    return sc


def main():
    ak.finish(hay_scene(), "Prop_HayBale", bake=BAKE)
    ak.finish(milk_scene(), "Prop_MilkCan", bake=BAKE)


if __name__ == "__main__":
    main()
