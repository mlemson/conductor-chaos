"""Veewagen (tweede wagontype) in de gamestijl — art proof (issue #25).

Klassieke veewagen, speelgoedachtig: een rode kooi uit één stuk (hoekstijlen, regels en deurstijlen als
één doorlopend kader) om een houten lattenwand. De latten zijn één geheel met de wand: de kieren zijn
echte openingen, zodat je het stro en de hooibalen binnen ziet. Verder: een gewelfd donkerbruin dak, een
schuifdeur aan beide kanten (los onderdeel, schuift langs X), kopse doorgangen met balkonnetjes en gele
relingen (doorloopbaar zoals de regionale wagon, issue #24), en een onderstel met draaistellen.
Low-poly: harde facetten, effen vlakken; de shader legt er grote kleurvlakken en vuil onderin overheen.

Assen: lengte langs X, buitenzijde (perronkant) op z = 0 en kijkt naar +Z, vloer op y = 0.10.
Ankers: SlideDoor_Near/Far (dicht; open = lokale X -1,55), kopse doorgangen + balkons.

Uitvoer:
  Assets/Art/Models/Train/CattleWagon/CattleWagon_Blockout.fbx   Unity
  ArtSource/Exports/CattleWagon_Blockout.glb                     Blender/preview
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
RAMPS = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

L, W = 6.40, 2.60
ZC = -W / 2
Y0, Y1 = 0.0, 2.20
FLOOR = 0.10
WALL = 0.08
CAGE = 0.07                     # hoe ver het rode kader voor de latten uitsteekt
SILL_Y = 0.16
BAY_Y = (0.30, 1.95)
DOOR = (-0.75, 0.75)
END_DOOR = 0.45
END_TOP = 1.92
SIDE_BAYS = [(-3.06, -2.05), (-1.93, -0.92), (0.92, 1.93), (2.05, 3.06)]
END_BAYS = [(ZC - 1.18, ZC - 0.62), (ZC + 0.62, ZC + 1.18)]
BOGIE_X = 2.05
RAIL_Y = -0.62
WHEEL_Z = 1.29                  # wielhart t.o.v. hartlijn = spoorwijdte 2,58 m (gelijk aan loc/tender en de spoorkit)


def box(x0, x1, y0, y1, z0, z1, r, mat, div=1):
    m = mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, r, div, mat=mat).transform(t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    m.sharp = True
    return m


def slat_gaps(n=6, slat=0.17):
    """Kieren tussen n latten over de hoogte van een vak."""
    gap = (BAY_Y[1] - BAY_Y[0] - n * slat) / (n - 1)
    return [(BAY_Y[0] + k * (slat + gap) + slat, BAY_Y[0] + k * (slat + gap) + slat + gap) for k in range(n - 1)]


def build_walls():
    """Houten lattenwand: holle bak met echte kieren, deuropeningen en kopse doorgangen (één stuk)."""
    outer = (-L / 2, L / 2, Y0, Y1, ZC - W / 2, ZC + W / 2)
    inner = (-L / 2 + WALL, L / 2 - WALL, Y0 + FLOOR, Y1 - WALL, ZC - W / 2 + WALL, ZC + W / 2 - WALL)
    holes = []
    for sg in (1, -1):
        for (a, b) in SIDE_BAYS:
            holes += [(2, sg, a, b, g0, g1) for (g0, g1) in slat_gaps()]
        holes.append((2, sg, DOOR[0], DOOR[1], SILL_Y, BAY_Y[1]))
        for (za, zb) in END_BAYS:
            holes += [(0, sg, g0, g1, za, zb) for (g0, g1) in slat_gaps()]
        holes.append((0, sg, SILL_Y, END_TOP, ZC - END_DOOR, ZC + END_DOOR))
    return mk.hollow_box(outer, inner, holes, radius=0.02, step=0.30, mat="M_Cattle_Slat", inner_mat="M_Cattle_Inner")


def build_cage():
    """Rood kader rond de hele bak, uit één stuk: hoekstijlen, regels, deur- en vakstijlen lopen door."""
    c = CAGE
    outer = (-L / 2 - c, L / 2 + c, Y0, Y1 + 0.04, ZC - W / 2 - c, ZC + W / 2 + c)
    inner = (-L / 2, L / 2, Y0, Y1, ZC - W / 2, ZC + W / 2)
    r = 0.05
    holes = [(1, 1, -L / 2 - c + r, L / 2 + c - r, ZC - W / 2 - c + r, ZC + W / 2 + c - r, -L / 2, L / 2, ZC - W / 2, ZC + W / 2),
             (1, -1, -L / 2 - c + r, L / 2 + c - r, ZC - W / 2 - c + r, ZC + W / 2 + c - r, -L / 2, L / 2, ZC - W / 2, ZC + W / 2)]
    for sg in (1, -1):
        holes += [(2, sg, a, b) + BAY_Y for (a, b) in SIDE_BAYS]
        holes.append((2, sg, DOOR[0], DOOR[1], SILL_Y, BAY_Y[1]))
        holes += [(0, sg) + BAY_Y + (za, zb) for (za, zb) in END_BAYS]
        holes.append((0, sg, SILL_Y, END_TOP, ZC - END_DOOR, ZC + END_DOOR))
    return mk.hollow_box(outer, inner, holes, radius=r, step=0.30, mat="M_Cattle_Red")


def build_roof():
    z0, z1 = ZC - W / 2 - 0.20, ZC + W / 2 + 0.20
    prof = mk.superellipse_arch(z0, z1, Y1 + 0.12, Y1 + 0.46, Y1 + 0.02, n=2.6, seg=12, fillet=0.05, fseg=1)
    roof = mk.extrude_profile(prof, -L / 2 - 0.20, L / 2 + 0.20, nx=8, round0=0.08, round1=0.08, rings=1, mat="M_Cattle_Roof")
    vents = [box(vx - 0.28, vx + 0.28, Y1 + 0.40, Y1 + 0.54, ZC - 0.22, ZC + 0.22, 0.04, "M_Cattle_Trim") for vx in (-1.8, 1.8)]
    return mk.merge(roof, *vents)


def build_door():
    """Schuifdeur: plankenblad met Z-schoor uit één stuk, gele greep, twee loopwieltjes."""
    w, h = (DOOR[1] - DOOR[0]) / 2 + 0.08, BAY_Y[1] - SILL_Y + 0.10
    parts = [box(-w, w, 0.0, h, -0.03, 0.03, 0.02, "M_Cattle_Slat")]
    # planknaden als ondiepe groeven zou extra geometrie vragen: hier geschilderde, lichtere planken
    for k, xa in enumerate(np.linspace(-w + 0.06, w - 0.06, 5)[:-1]):
        xb = xa + (2 * w - 0.12) / 4 - 0.03
        parts.append(box(xa + 0.015, xb, 0.08, h - 0.08, 0.02, 0.036, 0.008, "M_Cattle_Plank"))
    z_prof = [(-w + 0.06, h - 0.24), (w - 0.06, h - 0.24), (w - 0.06, h - 0.08), (-w + 0.06, h - 0.08)]
    zshape = [(-w + 0.06, 0.08), (w - 0.06, 0.08), (w - 0.06, 0.24), (-w + 0.30, 0.24),
              (w - 0.06, h - 0.30), (w - 0.06, h - 0.08), (-w + 0.06, h - 0.08), (-w + 0.06, h - 0.24),
              (w - 0.30, h - 0.24), (-w + 0.06, 0.30)]
    parts.append(mk.prism(mk.chamfer_poly(zshape, 0.02), 0.03, 0.07, 0.012, mat="M_Cattle_Red"))
    del z_prof
    parts.append(mk.capsule_between((w - 0.16, 0.80, 0.08), (w - 0.16, 1.20, 0.08), 0.025, 0.025, 8, 1, mat="M_Cattle_Interact"))
    for xr in (-w + 0.25, w - 0.25):
        wheel = mk.cylinder(0.07, 0.05, 8, 0.01, mat="M_Cattle_Trim", axis="z")
        wheel.sharp = True
        parts.append(wheel.transform(t=(xr, h + 0.05, 0.0)))
    return mk.merge(*parts)


def build_door_tracks():
    """Looprail boven de deur (lang genoeg om open te schuiven), op beide kanten."""
    near = box(DOOR[0] - 1.75, DOOR[1] + 0.20, BAY_Y[1] + 0.12, BAY_Y[1] + 0.20, 0.0, CAGE + 0.10, 0.02, "M_Cattle_Trim")
    far = near.transform(s=(1, 1, -1), t=(0, 0, -W))
    return mk.merge(near, far)


def build_interior():
    zin0, zin1 = ZC - W / 2 + WALL, ZC + W / 2 - WALL
    parts = [box(-L / 2 + WALL, L / 2 - WALL, FLOOR - 0.01, FLOOR + 0.03, zin0, zin1, 0.01, "M_Cattle_Straw")]
    rng = np.random.default_rng(11)
    # hooibalen langs de wanden (gangpad tussen de kopse deuren blijft vrij)
    for (x, z, stack) in ((-2.55, zin0 + 0.32, 2), (-1.75, zin0 + 0.32, 1), (2.55, zin1 - 0.32, 2), (1.80, zin1 - 0.32, 1),
                          (2.55, zin0 + 0.32, 1)):
        for k in range(stack):
            rot = rng.uniform(-0.15, 0.15)
            b = box(-0.42, 0.42, 0.0, 0.40, -0.26, 0.26, 0.06, "M_Cattle_Straw", div=1)
            band = mk.merge(box(-0.24, -0.18, -0.005, 0.41, -0.272, 0.272, 0.01, "M_Cattle_Trim"),    # 1 cm boven de baal
                            box(0.18, 0.24, -0.005, 0.41, -0.272, 0.272, 0.01, "M_Cattle_Trim"))
            parts.append(mk.merge(b, band).transform(R=mk.rot_y(rot), t=(x, FLOOR + 0.03 + k * 0.40, z)))
    # voerbak (één stuk: U-profiel) tegen de overkant
    trough = mk.prism(mk.chamfer_poly([(-0.32, 0.0), (0.32, 0.0), (0.32, 0.36), (0.24, 0.36), (0.24, 0.10), (-0.24, 0.10),
                                       (-0.24, 0.36), (-0.32, 0.36)], 0.02), -0.70, 0.70, 0.02, mat="M_Cattle_Plank")
    parts.append(trough.transform(R=mk.rot_y(math.pi / 2), t=(-0.2, FLOOR + 0.03, zin0 + 0.36)))
    return mk.merge(*parts)


def build_underframe():
    parts = [box(-L / 2 + 0.25, L / 2 - 0.25, -0.30, Y0 + 0.02, ZC - W / 2 + 0.18, ZC + W / 2 - 0.18, 0.08, "M_Cattle_Trim")]
    for bx in (-BOGIE_X, BOGIE_X):
        parts.append(box(bx - 0.95, bx + 0.95, -0.40, -0.16, ZC - W / 2 + 0.14, ZC + W / 2 - 0.14, 0.06, "M_Cattle_Trim"))
        for ax in (bx - 0.62, bx + 0.62):
            for zs in (ZC + WHEEL_Z, ZC - WHEEL_Z):
                w = mk.cylinder(0.33, 0.12, 12, 0.035, mat="M_Cattle_Wheel", axis="z")
                hub = mk.cylinder(0.13, 0.16, 8, 0.03, mat="M_Cattle_Red", axis="z")
                w.sharp = hub.sharp = True
                parts.append(mk.merge(w, hub).transform(t=(ax, RAIL_Y + 0.33, zs)))
        for zs in (ZC + WHEEL_Z + 0.13, ZC - WHEEL_Z - 0.13):      # aspotten buiten de wielen
            parts.append(box(bx - 0.22, bx + 0.22, -0.36, -0.10, zs - 0.06, zs + 0.06, 0.02, "M_Cattle_Red"))
    return mk.merge(*parts)


def build_ends():
    """Balkonnetjes (beloopbaar), gele relingen, buffers en koppeling op beide kopse kanten."""
    parts = []
    for sgn in (1, -1):
        ex = sgn * (L / 2 + CAGE)
        parts.append(box(min(ex - sgn * 0.05, ex + sgn * 0.32), max(ex - sgn * 0.05, ex + sgn * 0.32), Y0 + 0.02, SILL_Y + 0.02,
                         ZC - 0.62, ZC + 0.62, 0.03, "M_Cattle_Trim"))
        parts.append(box(min(ex, ex + sgn * 0.30), max(ex, ex + sgn * 0.30), SILL_Y + 0.01, SILL_Y + 0.03,
                         ZC - END_DOOR, ZC + END_DOOR, 0.006, "M_Cattle_Interact"))
        for dz in (-0.58, 0.58):
            xo = ex + sgn * 0.28
            parts += [mk.capsule_between((ex - sgn * 0.02, 1.08, ZC + dz), (xo, 1.08, ZC + dz), 0.026, 0.026, 8, 1, mat="M_Cattle_Interact"),
                      mk.capsule_between((xo, 1.08, ZC + dz), (xo, SILL_Y + 0.02, ZC + dz), 0.026, 0.026, 8, 1, mat="M_Cattle_Interact")]
        for dz in (-0.80, 0.80):
            buf = mk.cylinder(0.07, 0.30, 8, 0.015, mat="M_Cattle_Trim")
            plate = mk.cylinder(0.16, 0.06, 8, 0.02, mat="M_Cattle_Trim")
            buf.sharp = plate.sharp = True
            parts.append(buf.transform(t=(ex + sgn * 0.12, 0.17, ZC + dz)))
            parts.append(plate.transform(t=(ex + sgn * 0.27, 0.17, ZC + dz)))
        parts.append(box(min(ex, ex + sgn * 0.33), max(ex, ex + sgn * 0.33), -0.18, -0.02, ZC - 0.12, ZC + 0.12, 0.04, "M_Cattle_Trim"))
    # gouden naambordje op de onderregel (beide kanten)
    plaque = box(-2.75, -2.10, 0.08, 0.24, 0.0, CAGE + 0.012, 0.015, "M_Cattle_Gold")
    parts += [plaque, plaque.transform(s=(1, 1, -1), t=(0, 0, -W))]
    return mk.merge(*parts)


def build_cattle_wagon():
    sc = mk.Scene("CattleWagon_Blockout")
    R_ = RAMPS
    sc.material("M_Cattle_Red", R_["train_red"][2], R_["train_red"], 0.25, ramp_shift=-0.04)
    sc.material("M_Cattle_Slat", R_["interior_wood"][2], R_["interior_wood"], 0.15, ramp_shift=0.02)
    sc.material("M_Cattle_Plank", R_["interior_wood"][3], R_["interior_wood"], 0.15, ramp_shift=0.10)
    sc.material("M_Cattle_Inner", R_["cab_interior"][3], R_["cab_interior"], 0.10, ramp_shift=0.05)
    sc.material("M_Cattle_Roof", R_["wagon_brown"][1], R_["wagon_brown"], 0.20, ramp_shift=-0.18)
    sc.material("M_Cattle_Trim", R_["train_trim"][1], R_["train_trim"], 0.20)
    sc.material("M_Cattle_Wheel", R_["ui_bg"][2], R_["ui_bg"], 0.30)
    sc.material("M_Cattle_Gold", R_["gold_trim"][3], R_["gold_trim"], 0.40, ramp_shift=0.04)
    sc.material("M_Cattle_Straw", R_["track_wood"][2], R_["track_wood"], 0.05, ramp_shift=0.02)
    sc.material("M_Cattle_Interact", R_["accent_yellow"][2], R_["accent_yellow"], 0.30)
    sc.add(mk.Part("CattleWagon", None))
    sc.add(mk.Part("Walls", build_walls(), (0, 0, 0), "CattleWagon"))
    sc.add(mk.Part("Cage", build_cage(), (0, 0, 0), "CattleWagon"))
    sc.add(mk.Part("Roof", build_roof(), (0, 0, 0), "CattleWagon"))
    sc.add(mk.Part("Interior", build_interior(), (0, 0, 0), "CattleWagon"))
    sc.add(mk.Part("DoorTracks", build_door_tracks(), (0, 0, 0), "CattleWagon"))
    door = build_door()
    sc.add(mk.Part("SlideDoor_Near", door, (0, SILL_Y - 0.04, CAGE + 0.05), "CattleWagon"))
    sc.add(mk.Part("SlideDoor_Far", door.transform(s=(1, 1, -1)), (0, SILL_Y - 0.04, -W - CAGE - 0.05), "CattleWagon"))
    sc.add(mk.Part("Underframe", build_underframe(), (0, 0, 0), "CattleWagon"))
    sc.add(mk.Part("Ends", build_ends(), (0, 0, 0), "CattleWagon"))
    for p in sc.parts:
        p.gradient = 0.25
    sc.facet_all()
    sc.style_all(grime=1.0, **mk.TRAIN_CELLS)
    sc.bake_shading(density=900, radius=0.30, noise_freq=1.1, edge_range=(3.0, 10.0))
    return sc


def main():
    sc = build_cattle_wagon()
    rel = "Assets/Art/Models/Train/CattleWagon/CattleWagon_Blockout.fbx"
    exp_dir = os.path.join(ROOT, "ArtSource", "Exports")
    os.makedirs(os.path.join(ROOT, os.path.dirname(rel)), exist_ok=True)
    sc.write_fbx_ascii(os.path.join(ROOT, rel))
    sc.write_glb(os.path.join(exp_dir, "CattleWagon_Blockout.glb"))
    unity_assets.export_unity(ROOT, sc, rel, "Assets/Art/Materials/Train/CattleWagon")
    mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"],
                "opts": v["opts"]} for k, v in sc.materials.items()}
    json.dump({"model": "CattleWagon_Blockout", "stats": sc.stats(), "materials": mats},
              open(os.path.join(exp_dir, "CattleWagon_Blockout.materials.json"), "w", encoding="utf-8"), indent=2)
    print("cattle", sc.stats())


if __name__ == "__main__":
    main()
