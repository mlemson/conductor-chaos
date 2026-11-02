"""Locomotief in conceptstijl — P0 art proof (issue #22).

Naar de locomotief in de conceptafbeelding: een open cabine met dikke, afgeschuinde stijlen en een
dik overstekend dak (personages leunen eruit), een lage brede kap met grote schuine facetten, één
achthoekige koplamp met dikke donkere rand, buitenliggende wielen met drijfstang en buffers.
Bewust weinig onderdelen; elk onderdeel afgewerkt met afschuiningen en geschilderde variatie
(vertexkleur: occlusie, randlicht, verfvlekken).

Assen: rijrichting +X (kop/koplamp), perronkant op z = 0 (kijkt naar +Z), spoorstaaf op y = -0.62,
cabinevloer op y = 0.55. Starre hiërarchie; geen bewegende delen behalve 'Wheels' (draaien om z).

Uitvoer:
  Assets/Art/Models/Train/Locomotive/Locomotive_Blockout.fbx   Unity
  ArtSource/Exports/Locomotive_Blockout.glb                    Blender/preview
  ArtSource/Exports/Locomotive_Blockout.materials.json         ramps + shaderhints
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

# ---------------------------------------------------------------- maten (m)
W = 2.80                    # breedte loopbord
ZC = -W / 2                 # hartlijn
RAIL_Y = -0.62
FRAME = (-2.25, 4.30, 0.04, 0.34)      # loopbord: x0, x1, y0, y1
CAB = (-2.05, 0.15)                     # cabine x
CAB_W = 2.56                            # cabinebreedte
CAB_FLOOR = 0.74
WALL_TOP = 1.36                         # borstwering (personage leunt erop)
ROOF_Y = (2.42, 2.74)
HOOD = (0.10, 4.02, 1.42)               # kap: x0, x1, bovenkant
HOOD_W = 1.96
LAMP = (0.90, 0.44, 0.31)               # koplamp: hoogte, buitenstraal, lensstraal
WHEEL_R = 0.40
WHEEL_X = (-1.30, 0.62, 2.54)


def box(x0, x1, y0, y1, z0, z1, ch, mat, grid=None):
    """Afgeschuinde box (één schuine facet per rand). grid = rasterstap voor gebakken variatie."""
    if grid:
        m = mk.rounded_box_grid(x1 - x0, y1 - y0, z1 - z0, ch, div=1, step=grid, mat=mat)
    else:
        m = mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, ch, 1, mat=mat)
    m = m.transform(t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    m.sharp = True
    return m


def octagon_ring(r_out, r_in, depth, mat, chamfer=0.04):
    """Achthoekige ring (koplamprand) met afgeschuinde voor- en binnenkant, as langs +X."""
    c = chamfer
    prof = [(r_in + c, depth), (r_out - c, depth), (r_out, depth - c), (r_out, 0.0),
            (r_in, 0.0), (r_in, depth - c * 1.5)]
    m = mk.lathe(prof, 8, mat=mat, closed=True, phase=math.pi / 8)
    m.sharp = True
    return m.transform(R=mk.rot_z(-math.pi / 2))      # lathe-as (Y) -> X


def build_cab():
    z_out, z_in = ZC + CAB_W / 2, ZC - CAB_W / 2
    x0, x1 = CAB
    p = 0.22                                                  # stijldikte
    parts = [
        # vloer + borstweringen (perronkant, achterkant, overkant) — de voorkant is de kap
        box(x0, x1, FRAME[3] - 0.02, CAB_FLOOR, z_in, z_out, 0.06, "M_Loco_Red", 0.25),
        box(x0, x1, CAB_FLOOR - 0.02, WALL_TOP, z_out - 0.20, z_out, 0.07, "M_Loco_Red", 0.25),
        box(x0, x1, CAB_FLOOR - 0.02, WALL_TOP, z_in, z_in + 0.20, 0.07, "M_Loco_Red", 0.25),
        box(x0, x0 + 0.20, CAB_FLOOR - 0.02, ROOF_Y[0], z_in, z_out, 0.07, "M_Loco_Red", 0.25),
    ]
    # vier dikke hoekstijlen
    for xs in (x0, x1 - p):
        for zs in (z_in, z_out - p):
            parts.append(box(xs, xs + p, CAB_FLOOR, ROOF_Y[0] + 0.02, zs, zs + p, 0.06, "M_Loco_Red", 0.25))
    # bovenregel aan de voorkant (raamkader boven de kap)
    parts.append(box(x1 - p, x1, ROOF_Y[0] - 0.24, ROOF_Y[0] + 0.02, z_in, z_out, 0.06, "M_Loco_Red", 0.25))
    # dikke dakplaat met overstek, iets donkerder rood
    parts.append(box(x0 - 0.22, x1 + 0.26, ROOF_Y[0], ROOF_Y[1], ZC - CAB_W / 2 - 0.20, ZC + CAB_W / 2 + 0.20, 0.13,
                     "M_Loco_RedDark", 0.25))
    # donker cabine-interieur (achterwand binnenkant + vloer), zodat de opening diepte krijgt
    parts.append(box(x0 + 0.20, x0 + 0.24, CAB_FLOOR, ROOF_Y[0], z_in + 0.2, z_out - 0.2, 0.01, "M_Loco_Interior"))
    parts.append(box(x0 + 0.20, x1 - 0.05, CAB_FLOOR - 0.01, CAB_FLOOR + 0.01, z_in + 0.2, z_out - 0.2, 0.005, "M_Loco_Interior"))
    return mk.merge(*parts)


def build_hood():
    x0, x1, top = HOOD
    z_out, z_in = ZC + HOOD_W / 2, ZC - HOOD_W / 2
    hood = box(x0, x1, FRAME[3] - 0.02, top, z_in, z_out, 0.24, "M_Loco_Red", 0.22)
    # één donkerder inzetpaneel per zijkant (concept)
    panels = []
    for zf, sgn in ((z_out, 1), (z_in, -1)):
        panels.append(box(x0 + 1.55, x0 + 2.65, 0.56, 1.10, zf - 0.01 if sgn > 0 else zf - 0.03,
                          zf + 0.03 if sgn > 0 else zf + 0.01, 0.025, "M_Loco_RedDark"))
    return mk.merge(hood, *panels)


def build_headlight():
    h, ro, rl = LAMP
    ring = octagon_ring(ro, rl - 0.01, 0.16, "M_Loco_Trim", 0.045)
    back = mk.cylinder(ro - 0.02, 0.06, 8, 0.01, mat="M_Loco_Trim")
    back.sharp = True
    back = back.transform(R=mk.rot_x(math.pi / 8), t=(0.0, 0, 0))
    lens = mk.ellipsoid(0.07, rl, rl, 16, 8, mat="M_Loco_Lamp").transform(t=(0.07, 0, 0))
    return mk.merge(ring, back, lens)


def build_frame():
    x0, x1, y0, y1 = FRAME
    parts = [box(x0, x1, y0, y1, ZC - W / 2, ZC + W / 2, 0.07, "M_Loco_Trim", 0.3)]
    # bufferbalk + buffers + koppeling (voor en achter)
    for sgn, xe in ((1, x1), (-1, x0)):
        parts.append(box(min(xe, xe + sgn * 0.12), max(xe, xe + sgn * 0.12), -0.04, 0.42, ZC - W / 2 + 0.05, ZC + W / 2 - 0.05,
                         0.05, "M_Loco_RedDark"))
        for dz in (-0.85, 0.85):
            stem = mk.cylinder(0.075, 0.26, 8, 0.015, mat="M_Loco_Trim")
            plate = mk.cylinder(0.16, 0.06, 8, 0.022, mat="M_Loco_Trim")
            stem.sharp = plate.sharp = True
            parts.append(stem.transform(t=(xe + sgn * 0.23, 0.20, ZC + dz)))
            parts.append(plate.transform(t=(xe + sgn * 0.36, 0.20, ZC + dz)))
        parts.append(box(min(xe, xe + sgn * 0.40), max(xe, xe + sgn * 0.40), 0.10, 0.28, ZC - 0.12, ZC + 0.12, 0.04, "M_Loco_Trim"))
    # trapjes onder de cabine (beide kanten)
    for zf in (ZC + W / 2, ZC - W / 2):
        sgn = 1 if zf > ZC else -1
        for k, (y0s, y1s) in enumerate(((-0.30, -0.22), (-0.04, 0.04))):
            zz = zf + sgn * (0.10 - 0.05 * k)
            parts.append(box(-1.45, -0.85, y0s, y1s, min(zf, zz), max(zf, zz) + 0.0001, 0.02, "M_Loco_Trim"))
        parts.append(box(-1.42, -1.36, -0.30, 0.10, min(zf, zf + sgn * 0.11), max(zf, zf + sgn * 0.11), 0.01, "M_Loco_Trim"))
        parts.append(box(-0.94, -0.88, -0.30, 0.10, min(zf, zf + sgn * 0.11), max(zf, zf + sgn * 0.11), 0.01, "M_Loco_Trim"))
    return mk.merge(*parts)


def build_wheels():
    """Buitenliggende wielen met naaf en één drijfstang per kant."""
    parts = []
    yc = RAIL_Y + WHEEL_R
    for zs, sgn in ((ZC + W / 2 + 0.02, 1), (ZC - W / 2 - 0.02, -1)):
        for x in WHEEL_X:
            tire = mk.cylinder(WHEEL_R, 0.13, 12, 0.035, mat="M_Loco_Wheel", axis="z")
            hub = mk.cylinder(WHEEL_R * 0.62, 0.15, 12, 0.02, mat="M_Loco_RedDark", axis="z")
            cap = mk.cylinder(0.10, 0.19, 8, 0.02, mat="M_Loco_Trim", axis="z")
            for m in (tire, hub, cap):
                m.sharp = True
            parts += [tire.transform(t=(x, yc, zs)), hub.transform(t=(x, yc, zs)), cap.transform(t=(x, yc, zs))]
        rod_z = zs + sgn * 0.13
        parts.append(box(WHEEL_X[0] - 0.08, WHEEL_X[-1] + 0.08, yc - 0.17, yc - 0.07, rod_z - 0.03, rod_z + 0.03,
                         0.02, "M_Loco_Trim"))
        for x in WHEEL_X:
            pin = mk.cylinder(0.05, 0.10, 8, 0.01, mat="M_Loco_Trim", axis="z")
            pin.sharp = True
            parts.append(pin.transform(t=(x, yc - 0.12, rod_z)))
    return mk.merge(*parts)


def build_grabrails():
    parts = []
    x0, _ = CAB
    for zf, sgn in ((ZC + CAB_W / 2, 1), (ZC - CAB_W / 2, -1)):
        z = zf + sgn * 0.09
        parts.append(mk.capsule_between((x0 + 0.48, CAB_FLOOR + 0.10, z), (x0 + 0.48, WALL_TOP + 0.62, z), 0.028, 0.028, 8, 1,
                                        mat="M_Loco_Interact"))
        for y in (CAB_FLOOR + 0.16, WALL_TOP + 0.56):
            parts.append(box(x0 + 0.45, x0 + 0.51, y - 0.03, y + 0.03, min(zf, z), max(zf, z), 0.01, "M_Loco_Trim"))
    return mk.merge(*parts)


def build_locomotive():
    sc = mk.Scene("Locomotive_Blockout")
    R_ = RAMPS
    edge = {"edge": 1.5}
    sc.material("M_Loco_Red", R_["train_red"][2], R_["train_red"], 0.25, opts={"paint": 1.6, **edge})
    sc.material("M_Loco_RedDark", R_["train_red"][1], R_["train_red"], 0.25, ramp_shift=-0.30, opts={"paint": 1.5, **edge})
    sc.material("M_Loco_Trim", R_["train_trim"][1], R_["train_trim"], 0.20, ramp_shift=-0.12, opts={"paint": 1.2, **edge})
    sc.material("M_Loco_Interior", R_["cab_interior"][0], R_["cab_interior"], 0.10, ramp_shift=-0.25, opts={"paint": 0.6})
    sc.material("M_Loco_Wheel", R_["ui_bg"][2], R_["ui_bg"], 0.30, opts={"paint": 0.8, **edge})
    sc.material("M_Loco_Interact", R_["accent_yellow"][2], R_["accent_yellow"], 0.30, opts={"paint": 0.6})
    lens_ramp = [R_["helmet"][1], R_["helmet"][2], R_["headlight"][1], R_["headlight"][3], R_["headlight"][4]]  # rand oranje -> midden lichtgeel (concept)
    sc.material("M_Loco_Lamp", R_["headlight"][2], lens_ramp, 0.50, emissive=R_["headlight"][3],
                opts={"emissive": 1.0, "rim": 0.0, "paint": 0.2})
    sc.add(mk.Part("Locomotive", None))
    sc.add(mk.Part("Frame", build_frame(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("Cab", build_cab(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("Hood", build_hood(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("HeadLight", build_headlight(), (HOOD[1] - 0.02, LAMP[0], ZC), "Locomotive"))
    sc.add(mk.Part("Wheels", build_wheels(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("GrabRails", build_grabrails(), (0, 0, 0), "Locomotive"))
    for p in sc.parts:
        p.gradient = 0.32
        if p.name in ("Cab", "Hood", "Frame"):
            p.ao_group = "body"
    sc.get("HeadLight").ao_group = "body"
    sc.get("Wheels").gradient = 0.15
    sc.bake_shading(density=1100, radius=0.32, noise_freq=1.4, edge_range=(2.5, 9.0))
    return sc


def main():
    sc = build_locomotive()
    rel = "Assets/Art/Models/Train/Locomotive/Locomotive_Blockout.fbx"
    exp_dir = os.path.join(ROOT, "ArtSource", "Exports")
    os.makedirs(os.path.join(ROOT, os.path.dirname(rel)), exist_ok=True)
    os.makedirs(exp_dir, exist_ok=True)
    sc.write_fbx_ascii(os.path.join(ROOT, rel))
    sc.write_glb(os.path.join(exp_dir, "Locomotive_Blockout.glb"))
    unity_assets.export_unity(ROOT, sc, rel, "Assets/Art/Materials/Train/Locomotive")
    mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"],
                "opts": v["opts"]} for k, v in sc.materials.items()}
    json.dump({"model": "Locomotive_Blockout", "stats": sc.stats(), "materials": mats},
              open(os.path.join(exp_dir, "Locomotive_Blockout.materials.json"), "w", encoding="utf-8"), indent=2)
    print("loco", sc.stats())


if __name__ == "__main__":
    main()
