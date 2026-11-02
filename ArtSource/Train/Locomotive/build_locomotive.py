"""Locomotief v2 — P0 art proof (issue #22).

Versie 2 volgt de art review 'Locomotief · ontwerpblad v2' van @mlemson:
  * rondere, vriendelijke ketelneus (rechte zijkanten + ronde bovenkant) met goudoranje sierlijst
    op de schouder en een donkerder inzetpaneel onderaan;
  * geïntegreerde achthoekige koplamp: donkerbruine rookkastring met vier gouden bouten,
    achthoekige bolle lens en een gouden kapje bovenop de neus;
  * stevigere cabine: dikke stijlen met kapiteel en voet, gewelfd donkerbruin dak met royaal
    overstek, gouden sierlijst op de borstwering, deurpaneel achter;
  * netter onderstel: donker frame met rode bovenrand, rode bufferbalken met gouden hoeken,
    blokvormige buffers, wielen met rode naaf en een gouden drijfstang;
  * robuuste gebogen grepen (geel = interactie) en een klein gouden fluitje.
Alles met zachte, ronde afrondingen; geschilderde variatie in vertexkleur.

Assen: rijrichting +X (kop), perronkant op z = 0 (kijkt naar +Z), spoorstaaf op y = -0.62,
cabinevloer op y = 0.74. Starre hiërarchie.

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
W = 2.70                    # breedte frame
ZC = -W / 2                 # hartlijn
RAIL_Y = -0.62
FRAME = (-2.25, 4.20, 0.02, 0.34)      # frame: x0, x1, y0, y1
CAB = (-2.05, 0.15)                     # cabine x
CAB_W = 2.50
CAB_FLOOR = 0.74
WALL_TOP = 1.36                         # borstwering (conducteur leunt erop)
ROOF_Y = 2.40                           # onderkant dak
BOILER = (0.10, 3.80)                   # ketel x
BOILER_W = 1.92
BOILER_SIDE_TOP = 0.92                  # tot hier rechte zijkant, daarboven de ronde bovenkant
BOILER_TOP = 1.50
LAMP_Y = 0.90
WHEEL_R = 0.40
WHEEL_X = (-1.30, 0.62, 2.54)


def rbox(x0, x1, y0, y1, z0, z1, r, mat, grid=None, div=2):
    """Zacht afgeronde box (gladde normalen). grid = rasterstap voor gebakken variatie op grote vlakken."""
    if grid:
        m = mk.rounded_box_grid(x1 - x0, y1 - y0, z1 - z0, r, div=div, step=grid, mat=mat)
    else:
        m = mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, r, div, mat=mat)
    return m.transform(t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


def to_x(m):
    """Lathe-as (Y) naar +X draaien."""
    return m.transform(R=mk.rot_z(-math.pi / 2))


# ----------------------------------------------------------------- ketelneus
def build_boiler():
    z0, z1 = ZC - BOILER_W / 2, ZC + BOILER_W / 2
    prof = mk.superellipse_arch(z0, z1, BOILER_SIDE_TOP, BOILER_TOP, FRAME[3] - 0.02, n=2.3, seg=24, fillet=0.08)
    fseg = 3
    first_arc = fseg + 1                       # index van het eerste boogpunt

    def mat_fn(i):
        # goudoranje sierlijst op de 'schouder' van de ketel (beide kanten)
        k = i - first_arc
        return "M_Loco_Gold" if k in (4, 5, 18, 19) else "M_Loco_Red"

    boiler = mk.extrude_profile(prof, BOILER[0], BOILER[1], nx=16, round0=0.0, round1=0.16, rings=4, mat_fn=mat_fn)
    # donkerder inzetpaneel onderaan op beide zijkanten (concept/v2)
    panels = []
    for zf, sgn in ((z1, 1), (z0, -1)):
        zz = (zf - 0.02, zf + 0.035) if sgn > 0 else (zf - 0.035, zf + 0.02)
        panels.append(rbox(BOILER[0] + 0.95, BOILER[1] - 0.70, 0.48, 0.84, zz[0], zz[1], 0.025, "M_Loco_RedDark"))
    # gouden kapje bovenop de neus + fluitje bij de cabine
    cap = rbox(BOILER[1] - 0.42, BOILER[1] - 0.04, BOILER_TOP - 0.08, BOILER_TOP + 0.07, ZC - 0.30, ZC + 0.30, 0.05,
               "M_Loco_Gold")
    whistle = mk.merge(mk.capsule_between((BOILER[0] + 0.55, BOILER_TOP - 0.10, ZC), (BOILER[0] + 0.55, BOILER_TOP + 0.20, ZC),
                                          0.045, 0.045, 12, 2, mat="M_Loco_Gold"),
                       mk.ellipsoid(0.075, 0.05, 0.075, 12, 6, mat="M_Loco_Gold").transform(t=(BOILER[0] + 0.55, BOILER_TOP + 0.22, ZC)))
    return mk.merge(boiler, *panels, cap, whistle)


def build_headlight():
    """Rookkastring (achthoek, donkerbruin) met vier gouden bouten en een achthoekige bolle lens."""
    ro, ri, depth = 0.58, 0.38, 0.26
    c = 0.06
    ring = mk.lathe([(ri + c, depth), (ro - c, depth), (ro, depth - c), (ro, 0.0), (ri, 0.0), (ri, depth - c * 1.4)],
                    8, mat="M_Loco_Trim", closed=True, phase=math.pi / 8)
    ring = to_x(ring)
    ring.sharp = False
    back = to_x(mk.lathe([(0.0, 0.02), (ri + 0.02, 0.02), (ri + 0.02, 0.06), (0.0, 0.06)], 8, mat="M_Loco_Trim",
                         phase=math.pi / 8))
    lens = to_x(mk.lathe([(0.0, 0.17), (0.18, 0.155), (0.30, 0.11), (ri - 0.005, 0.05), (ri - 0.005, 0.02), (0.0, 0.02)],
                         8, mat="M_Loco_Lamp", phase=math.pi / 8))
    bolts = []
    rb = (ro + ri) / 2 + 0.01
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        bolts.append(mk.ellipsoid(0.034, 0.068, 0.068, 12, 6, mat="M_Loco_Gold").transform(
            t=(depth + 0.01, rb * math.sin(a), rb * math.cos(a))))
    return mk.merge(ring, back, lens, *bolts)


# ------------------------------------------------------------------- cabine
def build_cab():
    z_out, z_in = ZC + CAB_W / 2, ZC - CAB_W / 2
    x0, x1 = CAB
    parts = [
        rbox(x0, x1, FRAME[3] - 0.02, CAB_FLOOR, z_in, z_out, 0.07, "M_Loco_Red", 0.5),                     # vloerbak
        rbox(x0, x1, CAB_FLOOR - 0.02, WALL_TOP, z_out - 0.20, z_out, 0.07, "M_Loco_Red", 0.5),            # borstwering perronkant
        rbox(x0, x1, CAB_FLOOR - 0.02, WALL_TOP, z_in, z_in + 0.20, 0.07, "M_Loco_Red", 0.5),              # borstwering overkant
        rbox(x0, x0 + 0.20, CAB_FLOOR - 0.02, ROOF_Y, z_in, z_out, 0.07, "M_Loco_Red", 0.5),               # achterwand
    ]
    # gouden sierlijst op de borstweringen
    for (za, zb) in ((z_out - 0.23, z_out + 0.03), (z_in - 0.03, z_in + 0.23)):
        parts.append(rbox(x0 + 0.05, x1 - 0.05, WALL_TOP - 0.02, WALL_TOP + 0.06, za, zb, 0.03, "M_Loco_Gold"))
    parts.append(rbox(x0 - 0.03, x0 + 0.23, WALL_TOP - 0.02, WALL_TOP + 0.06, z_in + 0.05, z_out - 0.05, 0.03, "M_Loco_Gold"))
    # deurpaneel op de achterwand
    parts.append(rbox(x0 - 0.03, x0 + 0.02, CAB_FLOOR + 0.12, ROOF_Y - 0.22, ZC - 0.42, ZC + 0.42, 0.03, "M_Loco_RedDark"))
    # vier dikke stijlen met voet en kapiteel
    p = 0.26
    for xs in (x0, x1 - p):
        for zs in (z_in, z_out - p):
            parts.append(rbox(xs, xs + p, CAB_FLOOR, ROOF_Y + 0.02, zs, zs + p, 0.06, "M_Loco_Red"))
            for (y0, y1) in ((WALL_TOP - 0.02, WALL_TOP + 0.14), (ROOF_Y - 0.16, ROOF_Y + 0.02)):
                parts.append(rbox(xs - 0.04, xs + p + 0.04, y0, y1, zs - 0.04, zs + p + 0.04, 0.05, "M_Loco_Red"))
    # bovenregel voorkant (boven de ketel)
    parts.append(rbox(x1 - p, x1, ROOF_Y - 0.26, ROOF_Y + 0.02, z_in, z_out, 0.06, "M_Loco_Red", 0.6))
    # gewelfd dak met royaal overstek (donkerbruin)
    rz0, rz1 = ZC - CAB_W / 2 - 0.26, ZC + CAB_W / 2 + 0.26
    roof_prof = mk.superellipse_arch(rz0, rz1, ROOF_Y + 0.14, ROOF_Y + 0.46, ROOF_Y, n=3.2, seg=22, fillet=0.06)
    parts.append(mk.extrude_profile(roof_prof, x0 - 0.30, x1 + 0.34, nx=10, round0=0.10, round1=0.10, rings=3,
                                    mat="M_Loco_Roof"))
    # donker interieur (diepte in de opening)
    parts.append(rbox(x0 + 0.20, x0 + 0.24, CAB_FLOOR, ROOF_Y, z_in + 0.2, z_out - 0.2, 0.01, "M_Loco_Interior"))
    parts.append(rbox(x0 + 0.20, x1 - 0.05, CAB_FLOOR - 0.01, CAB_FLOOR + 0.01, z_in + 0.2, z_out - 0.2, 0.005, "M_Loco_Interior"))
    parts.append(rbox(x0 + 0.2, x1 - 0.2, ROOF_Y - 0.02, ROOF_Y + 0.02, z_in + 0.2, z_out - 0.2, 0.005, "M_Loco_Interior"))
    return mk.merge(*parts)


# ----------------------------------------------------------------- onderstel
def build_frame():
    x0, x1, y0, y1 = FRAME
    parts = [rbox(x0, x1, y0, y1 - 0.06, ZC - W / 2, ZC + W / 2, 0.07, "M_Loco_Frame", 0.7),
             rbox(x0 + 0.02, x1 - 0.02, y1 - 0.08, y1, ZC - W / 2 - 0.01, ZC + W / 2 + 0.01, 0.03, "M_Loco_Red")]   # rode bovenrand
    for sgn, xe in ((1, x1), (-1, x0)):
        bx0, bx1 = (xe, xe + 0.16) if sgn > 0 else (xe - 0.16, xe)
        parts.append(rbox(bx0, bx1, -0.06, 0.44, ZC - W / 2 + 0.02, ZC + W / 2 - 0.02, 0.05, "M_Loco_Red"))       # bufferbalk
        for zs in (ZC - W / 2, ZC + W / 2 - 0.20):
            parts.append(rbox(bx0 - 0.01, bx1 + 0.01, -0.08, 0.46, zs, zs + 0.20, 0.05, "M_Loco_Gold"))           # gouden hoeken
        for dz in (-0.78, 0.78):
            stem = mk.cylinder(0.07, 0.20, 8, 0.015, mat="M_Loco_Frame")
            xs = xe + sgn * 0.24
            parts.append(stem.transform(t=(xs, 0.20, ZC + dz)))
            parts.append(rbox(xe + sgn * 0.30 - 0.09, xe + sgn * 0.30 + 0.09, 0.08, 0.32, ZC + dz - 0.13, ZC + dz + 0.13, 0.05,
                              "M_Loco_Trim"))                                                                       # blokbuffer
        hook = rbox(min(xe, xe + sgn * 0.42), max(xe, xe + sgn * 0.42), 0.12, 0.26, ZC - 0.10, ZC + 0.10, 0.04, "M_Loco_Frame")
        parts.append(hook)
    # trapjes onder de cabine
    for zf, sgn in ((ZC + W / 2, 1), (ZC - W / 2, -1)):
        for (y0s, y1s, d) in ((-0.30, -0.22, 0.13), (-0.02, 0.06, 0.08)):
            za, zb = (zf, zf + d) if sgn > 0 else (zf - d, zf)
            parts.append(rbox(-1.60, -0.98, y0s, y1s, za, zb, 0.02, "M_Loco_Frame"))
        for xs in (-1.58, -1.06):
            za, zb = (zf, zf + 0.12) if sgn > 0 else (zf - 0.12, zf)
            parts.append(rbox(xs, xs + 0.06, -0.30, 0.10, za, zb, 0.015, "M_Loco_Frame"))
    return mk.merge(*parts)


def build_wheels():
    """Buitenliggende wielen met rode naaf en een gouden drijfstang per kant."""
    parts = []
    yc = RAIL_Y + WHEEL_R
    for zs, sgn in ((ZC + W / 2 + 0.04, 1), (ZC - W / 2 - 0.04, -1)):
        for x in WHEEL_X:
            tire = mk.cylinder(WHEEL_R, 0.13, 16, 0.04, mat="M_Loco_Wheel", axis="z")
            hub = mk.cylinder(WHEEL_R * 0.66, 0.15, 16, 0.03, mat="M_Loco_Red", axis="z")
            cap = mk.cylinder(0.10, 0.19, 10, 0.03, mat="M_Loco_RedDark", axis="z")
            parts += [m.transform(t=(x, yc, zs)) for m in (tire, hub, cap)]
        rz = zs + sgn * 0.13
        parts.append(rbox(WHEEL_X[0] - 0.09, WHEEL_X[-1] + 0.09, yc - 0.17, yc - 0.07, rz - 0.035, rz + 0.035, 0.03, "M_Loco_Gold"))
        for x in WHEEL_X:
            parts.append(mk.cylinder(0.055, 0.11, 8, 0.015, mat="M_Loco_Gold", axis="z").transform(t=(x, yc - 0.12, rz)))
    return mk.merge(*parts)


def build_grabrails():
    """Robuuste gebogen grepen: verticale stang met twee afgeronde steunen (geel = interactie)."""
    parts = []
    x0, x1 = CAB
    for zf, sgn in ((ZC + CAB_W / 2, 1), (ZC - CAB_W / 2, -1)):
        z = zf + sgn * 0.13
        for xg in (x0 + 0.42,):
            ya, yb = CAB_FLOOR + 0.08, WALL_TOP + 0.70
            parts.append(mk.capsule_between((xg, ya, z), (xg, yb, z), 0.035, 0.035, 12, 3, mat="M_Loco_Interact"))
            for y in (ya, yb):
                parts.append(mk.capsule_between((xg, y, zf - sgn * 0.02), (xg, y, z), 0.032, 0.032, 12, 3, mat="M_Loco_Interact"))
    return mk.merge(*parts)


def build_locomotive():
    sc = mk.Scene("Locomotive_Blockout")
    R_ = RAMPS
    sc.material("M_Loco_Red", R_["train_red"][2], R_["train_red"], 0.25, opts={"paint": 1.4, "edge": 1.2})
    sc.material("M_Loco_RedDark", R_["train_red"][1], R_["train_red"], 0.25, ramp_shift=-0.26, opts={"paint": 1.3, "edge": 1.2})
    sc.material("M_Loco_Roof", R_["wagon_brown"][1], R_["wagon_brown"], 0.20, ramp_shift=-0.14, opts={"paint": 1.4, "edge": 1.2})
    sc.material("M_Loco_Gold", R_["gold_trim"][3], R_["gold_trim"], 0.40, ramp_shift=0.04, opts={"paint": 0.8, "edge": 1.4})
    sc.material("M_Loco_Frame", R_["ui_bg"][2], R_["ui_bg"], 0.25, ramp_shift=0.06, opts={"paint": 1.0, "edge": 1.4})
    sc.material("M_Loco_Trim", R_["train_trim"][1], R_["train_trim"], 0.20, ramp_shift=-0.05, opts={"paint": 1.2, "edge": 1.4})
    sc.material("M_Loco_Interior", R_["cab_interior"][0], R_["cab_interior"], 0.10, ramp_shift=-0.25, opts={"paint": 0.6})
    sc.material("M_Loco_Wheel", R_["ui_bg"][2], R_["ui_bg"], 0.30, opts={"paint": 0.8, "edge": 1.4})
    sc.material("M_Loco_Interact", R_["accent_yellow"][2], R_["accent_yellow"], 0.30, opts={"paint": 0.6})
    lens_ramp = [R_["helmet"][1], R_["helmet"][2], R_["headlight"][1], R_["headlight"][3], R_["headlight"][4]]
    sc.material("M_Loco_Lamp", R_["headlight"][2], lens_ramp, 0.50, emissive=R_["headlight"][3],
                opts={"emissive": 1.0, "rim": 0.0, "paint": 0.2})
    sc.add(mk.Part("Locomotive", None))
    sc.add(mk.Part("Frame", build_frame(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("Cab", build_cab(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("Boiler", build_boiler(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("HeadLight", build_headlight(), (BOILER[1] - 0.06, LAMP_Y, ZC), "Locomotive"))
    sc.add(mk.Part("Wheels", build_wheels(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("GrabRails", build_grabrails(), (0, 0, 0), "Locomotive"))
    for p in sc.parts:
        p.gradient = 0.30
        if p.name in ("Cab", "Boiler", "Frame", "HeadLight"):
            p.ao_group = "body"
    sc.get("Wheels").gradient = 0.12
    sc.bake_shading(density=1100, radius=0.30, noise_freq=1.3, edge_range=(4.0, 16.0))
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
