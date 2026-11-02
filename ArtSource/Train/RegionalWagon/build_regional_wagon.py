"""Regionale reizigerswagon in facetstijl — blockout voor de P0 art proof (issue #9).

Vervangt de eerdere 'wagonhoek'. Zoals de chunky locomotief in het concept bestaat de wagon uit
afgeschuinde blokken met vlakke vlakken (elk vlak een eigen toon), een dik overstekend dak,
dikke donkere kaders, inzetpanelen, een gouden bies, draaistellen met grote wielen en
achthoekige koplampen. Geel = interactie (sleutelslot, grepen, drempel).

Assen: lengte langs X, buitenzijde (perronkant) op z = 0 en kijkt naar +Z, perron op y = 0,
wagonvloer op y = 0.10. Interactie-ankers: DoorLeaf_L/R (gesloten; schuiven open langs lokale X),
KeyLock (0,72 m hoog), DoorSill.

Uitvoer:
  Assets/Art/Models/Train/RegionalWagon/RegionalWagon_Blockout.fbx   Unity
  ArtSource/Exports/RegionalWagon_Blockout.glb                       Blender/preview
  ArtSource/Exports/ArtCheck_Environment.glb                         alleen voor de art-check render
"""
import json
import math
import os
import random
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
# Facetstijl zoals de locomotief in het concept: afgeschuinde blokken met vlakke vlakken
# (div=1 -> één schuine facet per rand), zodat elk vlak een eigen toon krijgt in de ramp.
L = 7.2                    # lengte bak (gedrongen, speelgoedachtig)
W = 2.60                   # breedte bak
XC, ZC = 0.0, -W / 2
Y0, Y1 = 0.0, 2.24         # onder/bovenkant bak (dak ligt erbovenop)
CH = 0.16                  # afschuining van de bak
FLOOR = 0.10
DOOR = (-0.20, 1.00)       # deuropening x
DOOR_TOP = 1.86
WIN_Y = (1.00, 1.68)
WINDOWS = [(-3.24, -2.42), (-2.24, -1.42), (-1.24, -0.42), (1.30, 2.12), (2.32, 3.14)]
LOCK = (DOOR[0] - 0.36, 0.72)
WALL = 0.10                # wanddikte (holle bak, beloopbaar interieur)
SILL_Y = Y0 + CH           # onderkant van deuropeningen (vlak deel van de bak)
END_DOOR = 0.45            # halve breedte kopse doorgang
END_TOP = 1.92
WINDOWS_FAR = WINDOWS + [(-0.02, 0.80)]
BOGIE_X = 2.30
RAIL_Y = -0.62


def box(x0, x1, y0, y1, z0, z1, r, mat, div=1, sharp=True):
    m = mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, r, div, mat=mat).transform(
        t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    m.sharp = sharp
    return m


def frame(x0, x1, y0, y1, w, z0, z1, r, mat, open_bottom=False):
    """Dik, afgeschuind kader rond een opening, uit één stuk (verstekhoeken; geen losse latten)."""
    return mk.frame_ring(x0, x1, y0, y1, w, z0, z1, min(r, w * 0.4), mat=mat, open_bottom=open_bottom)


def mirror_far(m):
    """Spiegel een onderdeel van de perronkant (z = 0) naar de overkant (z = -W)."""
    return m.transform(s=(1, 1, -1), t=(0, 0, -W))


def end_frame(sgn, u0, u1, y0, y1, w, d0, d1, r, mat, open_bottom=True):
    """Kader op een kopse kant (x = ±L/2): gebouwd in het XY-vlak en naar het YZ-vlak gedraaid."""
    if sgn > 0:
        f = frame(-u1, -u0, y0, y1, w, d0, d1, r, mat, open_bottom).transform(R=mk.rot_y(math.pi / 2))
    else:
        f = frame(u0, u1, y0, y1, w, d0, d1, r, mat, open_bottom).transform(R=mk.rot_y(-math.pi / 2))
    return f.transform(t=(XC + sgn * L / 2, 0, 0))


def glass(x0, x1, y0, y1, z):
    """Donker glas met één grote, zachte luchtreflectie (concept: donkere ramen, lichte weerspiegeling)."""
    g = box(x0, x1, y0, y1, z - 0.03, z, 0.004, "M_Train_Glass")
    w, h = x1 - x0, y1 - y0
    # schuine reflectieband over de bovenhoek
    pts = np.array([[x0 + 0.08 * w, y1 - 0.02 * h], [x0 + 0.48 * w, y1 - 0.02 * h],
                    [x0 + 0.20 * w, y0 + 0.30 * h], [x0 + 0.02 * w, y0 + 0.48 * h]])
    V = np.array([[x, y, z + 0.003] for x, y in pts])
    shine = mk.Mesh(V, [(0, 3, 2, 1)], ["M_Train_GlassShine"])
    shine.sharp = True
    return mk.merge(g, shine)


def octagon_lamp(r, depth, axis="x"):
    """Achthoekige lamp met donkere rand, zoals de koplamp van de locomotief in het concept."""
    house = mk.cylinder(r, depth, 8, r * 0.18, mat="M_Train_Trim", axis=axis)
    lens = mk.cylinder(r * 0.72, depth * 0.6, 8, r * 0.08, mat="M_Train_Lamp", axis=axis)
    off = {"x": (depth * 0.35, 0, 0), "z": (0, 0, depth * 0.35)}[axis]
    house.sharp = lens.sharp = True
    return mk.merge(house, lens.transform(t=off))


def build_body():
    """Holle bak uit één stuk: buiten- en binnenwand met dagkanten voor ramen, zijdeur en kopse doorgangen."""
    outer = (XC - L / 2, XC + L / 2, Y0, Y1, ZC - W / 2, ZC + W / 2)
    inner = (XC - L / 2 + WALL, XC + L / 2 - WALL, Y0 + FLOOR, Y1 - WALL, ZC - W / 2 + WALL, ZC + W / 2 - WALL)
    holes = [(2, 1, a, b, WIN_Y[0], WIN_Y[1]) for (a, b) in WINDOWS]
    holes += [(2, -1, a, b, WIN_Y[0], WIN_Y[1]) for (a, b) in WINDOWS_FAR]
    holes.append((2, 1, DOOR[0], DOOR[1], SILL_Y, DOOR_TOP))
    for sgn in (1, -1):
        holes.append((0, sgn, SILL_Y, END_TOP, ZC - END_DOOR, ZC + END_DOOR))
    return mk.hollow_box(outer, inner, holes, radius=CH, step=0.24, mat="M_Train_Red", inner_mat="M_Train_Inner")


def bench(x, facing, z0, z1):
    """Bank (zitting + rugleuning) uit één stuk; 'facing' = +1/-1 kijkrichting langs X."""
    prof = [(-0.12, Y0 + FLOOR), (0.46, Y0 + FLOOR), (0.46, 0.50), (0.04, 0.50), (0.0, 1.04), (-0.12, 1.04)]
    m = mk.prism(mk.chamfer_poly(prof, 0.035), z0, z1, 0.03, mat="M_Train_Seat")
    if facing < 0:
        m = m.transform(s=(-1, 1, 1))
    return m.transform(t=(x, 0, 0))


def build_interior():
    zin0, zin1 = ZC - W / 2 + WALL, ZC + W / 2 - WALL
    parts = [box(XC - L / 2 + WALL, XC + L / 2 - WALL, Y0 + FLOOR - 0.01, Y0 + FLOOR + 0.02, zin0, zin1, 0.008, "M_Train_Floor")]
    seats = [(-3.37, 1), (-2.45, -1), (-2.21, 1), (-1.45, -1), (-1.21, 1), (-0.32, -1), (1.20, 1), (2.10, -1), (2.34, 1), (3.37, -1)]
    for (x, f) in seats:
        for (za, zb) in ((zin0 + 0.02, ZC - END_DOOR - 0.05), (ZC + END_DOOR + 0.05, zin1 - 0.02)):
            if za > zb:
                continue
            parts.append(bench(x, f, za, zb))
    return mk.merge(*parts)


def build_wheel(x, z):
    w = mk.cylinder(0.33, 0.12, 12, 0.035, mat="M_Train_Wheel", axis="z")
    hub = mk.cylinder(0.13, 0.16, 8, 0.03, mat="M_Train_RedLow", axis="z")
    w.sharp = hub.sharp = True
    return mk.merge(w, hub).transform(t=(x, RAIL_Y + 0.33, z))


def build_wagon():
    sc = mk.Scene("RegionalWagon_Blockout")
    R_ = RAMPS
    sc.material("M_Train_Red", R_["train_red"][2], R_["train_red"], 0.25, opts={"paint": 1.5})
    sc.material("M_Train_RedPanel", R_["train_red"][3], R_["train_red"], 0.25, ramp_shift=0.06, opts={"paint": 1.5})
    sc.material("M_Train_RedLow", R_["train_red"][1], R_["train_red"], 0.25, ramp_shift=-0.18, opts={"paint": 1.3})
    sc.material("M_Train_Roof", R_["wagon_brown"][1], R_["wagon_brown"], 0.20, ramp_shift=-0.18, opts={"paint": 1.6})
    sc.material("M_Train_Trim", R_["train_trim"][1], R_["train_trim"], 0.20, opts={"paint": 1.2})
    sc.material("M_Train_Wheel", R_["ui_bg"][2], R_["ui_bg"], 0.30, opts={"paint": 0.8})
    sc.material("M_Train_Gold", R_["gold_trim"][2], R_["gold_trim"], 0.40)
    sc.material("M_Train_Glass", R_["uniform_navy"][1], R_["uniform_navy"], 0.70, ramp_shift=-0.10,
                opts={"flat": 0.5, "rim": 0.25, "paint": 0.3})
    sc.material("M_Train_GlassShine", R_["sky"][2], R_["sky"], 0.70, ramp_shift=-0.05, opts={"flat": 0.85, "rim": 0, "paint": 0})
    sc.material("M_Train_Interact", R_["accent_yellow"][2], R_["accent_yellow"], 0.30)
    sc.material("M_Train_Inner", R_["cab_interior"][3], R_["cab_interior"], 0.15, ramp_shift=0.05)
    sc.material("M_Train_Floor", R_["interior_wood"][1], R_["interior_wood"], 0.15, ramp_shift=-0.12)
    sc.material("M_Train_Seat", R_["interior_wood"][3], R_["interior_wood"], 0.15, ramp_shift=0.04)
    sc.material("M_Train_Lamp", R_["headlight"][2], R_["headlight"], 0.50, emissive=R_["headlight"][3],
                opts={"emissive": 0.85, "rim": 0.0, "paint": 0})
    sc.add(mk.Part("RegionalWagon", None))
    sc.add(mk.Part("Body", build_body(), (0, 0, 0), "RegionalWagon"))

    sc.add(mk.Part("Interior", build_interior(), (0, 0, 0), "RegionalWagon"))

    # ramen (beide kanten): kader uit één stuk + donker glas met luchtreflectie in de dagkant
    win = []
    for (a, b) in WINDOWS:
        win.append(frame(a, b, WIN_Y[0], WIN_Y[1], 0.075, -0.01, 0.045, 0.022, "M_Train_Trim"))
        win.append(glass(a, b, WIN_Y[0], WIN_Y[1], -0.035))
    for (a, b) in WINDOWS_FAR:
        win.append(mirror_far(frame(a, b, WIN_Y[0], WIN_Y[1], 0.075, -0.01, 0.045, 0.022, "M_Train_Trim")))
        win.append(mirror_far(glass(a, b, WIN_Y[0], WIN_Y[1], -0.035)))
    sc.add(mk.Part("Windows", mk.merge(*win), (0, 0, 0), "RegionalWagon"))

    # livrei (beide kanten): lichtere inzetpanelen, gouden bies, donkere band boven deur/ramen
    liv, far = [], []
    for (a, b) in ((-3.40, -1.86), (-1.74, -0.36), (1.16, 2.22), (2.34, 3.40)):
        liv.append(box(a, b, 0.24, 0.76, -0.02, 0.028, 0.02, "M_Train_RedPanel"))
    for (a, b) in ((-3.40, -1.86), (-1.74, -0.36), (-0.24, 1.04), (1.16, 2.22), (2.34, 3.40)):
        far.append(box(a, b, 0.24, 0.76, -0.02, 0.028, 0.02, "M_Train_RedPanel"))
    for (a, b) in ((XC - L / 2 + 0.12, DOOR[0] - 0.17), (DOOR[1] + 0.17, XC + L / 2 - 0.12)):
        liv.append(box(a, b, 0.845, 0.895, -0.02, 0.035, 0.012, "M_Train_Gold"))
    far.append(box(XC - L / 2 + 0.12, XC + L / 2 - 0.12, 0.845, 0.895, -0.02, 0.035, 0.012, "M_Train_Gold"))
    for lst in (liv, far):
        lst.append(box(XC - L / 2 + 0.12, XC + L / 2 - 0.12, 1.97, 2.05, -0.02, 0.03, 0.015, "M_Train_RedLow"))
    sc.add(mk.Part("Livery", mk.merge(*liv, *[mirror_far(m) for m in far]), (0, 0, 0), "RegionalWagon"))

    # dak: dikke, overstekende afgeschuinde plaat + verhoging en ventilatiekastjes
    roof = [box(XC - L / 2 - 0.16, XC + L / 2 + 0.16, Y1 - 0.06, Y1 + 0.30, ZC - W / 2 - 0.16, ZC + W / 2 + 0.16, 0.13, "M_Train_Roof"),
            box(XC - L / 2 + 0.40, XC + L / 2 - 0.40, Y1 + 0.26, Y1 + 0.46, ZC - W / 2 + 0.40, ZC + W / 2 - 0.40, 0.10, "M_Train_Roof")]
    for vx in (-2.2, 0.0, 2.2):
        roof.append(box(vx - 0.32, vx + 0.32, Y1 + 0.44, Y1 + 0.58, ZC - 0.32, ZC + 0.32, 0.05, "M_Train_Trim"))
    sc.add(mk.Part("Roof", mk.merge(*roof), (0, 0, 0), "RegionalWagon"))

    # zijdeur: kader uit één stuk (U-vorm op de drempel), twee rode bladen in de dagkant, gele grepen
    sc.add(mk.Part("DoorFrame", frame(DOOR[0], DOOR[1], Y0 + 0.08, DOOR_TOP, 0.07, -0.01, 0.045, 0.022, "M_Train_Trim",
                                      open_bottom=True), (0, 0, 0), "RegionalWagon"))
    half = (DOOR[1] - DOOR[0]) / 2
    leaf_h = DOOR_TOP - SILL_Y + 0.08
    for nm, cx, sgn in (("DoorLeaf_L", DOOR[0] + half / 2, -1), ("DoorLeaf_R", DOOR[1] - half / 2, 1)):
        hw = half / 2 - 0.008
        leaf = [box(-hw, hw, 0.0, leaf_h, -0.03, 0.03, 0.022, "M_Train_Red")]
        leaf.append(frame(-hw + 0.13, hw - 0.13, 0.86, 1.42, 0.05, 0.0, 0.05, 0.015, "M_Train_Trim"))
        leaf.append(glass(-hw + 0.13, hw - 0.13, 0.86, 1.42, 0.035))
        leaf.append(box(-hw + 0.08, hw - 0.08, 0.14, 0.56, 0.02, 0.048, 0.015, "M_Train_RedPanel"))
        gx = -sgn * (hw - 0.07)
        leaf.append(mk.capsule_between((gx, 0.52, 0.065), (gx, 0.95, 0.065), 0.022, 0.022, 8, 1, mat="M_Train_Interact"))
        sc.add(mk.Part(nm, mk.merge(*leaf), (cx, SILL_Y - 0.04, -0.05), "RegionalWagon"))
    sill = mk.merge(box(DOOR[0] - 0.10, DOOR[1] + 0.10, Y0 + 0.06, SILL_Y + 0.02, -0.14, 0.11, 0.025, "M_Train_Trim"),
                    box(DOOR[0], DOOR[1], SILL_Y + 0.01, SILL_Y + 0.03, 0.0, 0.10, 0.008, "M_Train_Interact"))
    sc.add(mk.Part("DoorSill", sill, (0, 0, 0), "RegionalWagon"))
    # buitengrepen naast de deur: één gebogen stang (staander + steunen) per kant
    rails = []
    for gx in (DOOR[0] - 0.17, DOOR[1] + 0.17):
        rails += [mk.capsule_between((gx, 0.40, -0.01), (gx, 0.40, 0.11), 0.026, 0.026, 8, 1, mat="M_Train_Interact"),
                  mk.capsule_between((gx, 0.40, 0.11), (gx, 1.60, 0.11), 0.026, 0.026, 8, 1, mat="M_Train_Interact"),
                  mk.capsule_between((gx, 1.60, 0.11), (gx, 1.60, -0.01), 0.026, 0.026, 8, 1, mat="M_Train_Interact")]
    sc.add(mk.Part("GrabRails", mk.merge(*rails), (0, 0, 0), "RegionalWagon"))
    lock = mk.merge(box(-0.09, 0.09, -0.12, 0.12, -0.01, 0.045, 0.03, "M_Train_Interact"),
                    mk.cylinder(0.032, 0.02, 8, 0.004, mat="M_Train_Trim", axis="z").transform(t=(0, 0.025, 0.052)),
                    box(-0.01, 0.01, -0.05, 0.02, 0.04, 0.058, 0.004, "M_Train_Trim"))
    sc.add(mk.Part("KeyLock", lock, (LOCK[0], LOCK[1], 0.0), "RegionalWagon"))
    sc.add(mk.Part("DoorLamp", octagon_lamp(0.09, 0.08, "z"), ((DOOR[0] + DOOR[1]) / 2, DOOR_TOP + 0.17, 0.02), "RegionalWagon"))

    # onderstel + draaistellen met grote wielen
    under = [box(XC - L / 2 + 0.25, XC + L / 2 - 0.25, -0.30, Y0 + 0.02, ZC - W / 2 + 0.18, ZC + W / 2 - 0.12, 0.08, "M_Train_Trim")]
    for bx in (-BOGIE_X, BOGIE_X):
        under.append(box(bx - 0.95, bx + 0.95, -0.40, -0.16, ZC - W / 2 + 0.14, ZC + W / 2 - 0.14, 0.06, "M_Train_Trim"))
        for ax in (bx - 0.62, bx + 0.62):
            for zs in (ZC + W / 2 - 0.20, ZC - W / 2 + 0.20):
                under.append(build_wheel(ax, zs))
        for zs in (ZC + W / 2 - 0.08, ZC - W / 2 + 0.08):
            under.append(box(bx - 0.22, bx + 0.22, -0.36, -0.10, zs - 0.06, zs + 0.06, 0.02, "M_Train_RedLow"))
    sc.add(mk.Part("Underframe", mk.merge(*under), (0, 0, 0), "RegionalWagon"))

    # kopse kanten: open doorgang met kader, balkonnetje (beloopbaar) met gele relingen, lampen, buffers, koppeling
    ends = []
    for sgn in (1, -1):
        ex = XC + sgn * L / 2
        ends.append(end_frame(sgn, ZC - END_DOOR, ZC + END_DOOR, Y0 + 0.08, END_TOP, 0.07, -0.01, 0.045, 0.022, "M_Train_Trim"))
        ends.append(box(min(ex - sgn * 0.05, ex + sgn * 0.32), max(ex - sgn * 0.05, ex + sgn * 0.32), Y0 + 0.02, SILL_Y + 0.02,
                        ZC - 0.62, ZC + 0.62, 0.03, "M_Train_Trim", div=1))
        ends.append(box(min(ex, ex + sgn * 0.30), max(ex, ex + sgn * 0.30), SILL_Y + 0.01, SILL_Y + 0.03,
                        ZC - END_DOOR, ZC + END_DOOR, 0.006, "M_Train_Interact"))
        for dz in (-0.58, 0.58):
            xo = ex + sgn * 0.28
            ends += [mk.capsule_between((ex - sgn * 0.02, 1.08, ZC + dz), (xo, 1.08, ZC + dz), 0.026, 0.026, 8, 1, mat="M_Train_Interact"),
                     mk.capsule_between((xo, 1.08, ZC + dz), (xo, SILL_Y + 0.02, ZC + dz), 0.026, 0.026, 8, 1, mat="M_Train_Interact")]
        for dz in (-0.80, 0.80):
            ends.append(octagon_lamp(0.15, 0.12).transform(R=None if sgn > 0 else mk.rot_y(math.pi), t=(ex + sgn * 0.05, 0.80, ZC + dz)))
            buf = mk.cylinder(0.07, 0.30, 8, 0.015, mat="M_Train_Trim")
            plate = mk.cylinder(0.16, 0.06, 8, 0.02, mat="M_Train_Trim")
            buf.sharp = plate.sharp = True
            ends.append(buf.transform(t=(ex + sgn * 0.15, 0.17, ZC + dz)))
            ends.append(plate.transform(t=(ex + sgn * 0.30, 0.17, ZC + dz)))
        ends.append(box(min(ex, ex + sgn * 0.36), max(ex, ex + sgn * 0.36), -0.18, -0.02, ZC - 0.12, ZC + 0.12, 0.04, "M_Train_Trim"))
    sc.add(mk.Part("Ends", mk.merge(*ends), (0, 0, 0), "RegionalWagon"))
    for p in sc.parts:
        p.gradient = 0.25
    sc.facet_all()
    sc.style_all(grime=1.0)
    sc.bake_shading(density=900, radius=0.30, noise_freq=1.1, edge_range=(3.0, 10.0))
    return sc


def build_env():
    """Art-check decor: perron + landschap. Alleen voor de render, geen game-asset."""
    random.seed(4)
    sc = mk.Scene("ArtCheck_Environment")
    R_ = RAMPS
    sc.material("M_Env_Platform", R_["parchment"][1], R_["parchment"], 0.1, ramp_shift=-0.38, opts={"paint": 1.6})
    sc.material("M_Env_PlatformEdge", R_["accent_yellow"][2], R_["accent_yellow"], 0.2)
    sc.material("M_Env_Ballast", R_["cliff"][1], R_["cliff"], 0.1, ramp_shift=-0.1, opts={"paint": 1.6})
    sc.material("M_Env_Sleeper", R_["track_wood"][2], R_["track_wood"], 0.1, opts={"paint": 1.4})
    sc.material("M_Env_Rail", R_["metal"][2], R_["metal"], 0.4)
    sc.material("M_Env_Grass", R_["grass"][2], R_["grass"], 0.05, opts={"paint": 1.8})
    sc.material("M_Env_Pine", R_["pine"][2], R_["pine"], 0.05, opts={"paint": 1.4})
    sc.material("M_Env_Trunk", R_["interior_wood"][1], R_["interior_wood"], 0.05)
    sc.material("M_Env_Rock", R_["mountain"][2], R_["mountain"], 0.05, opts={"rim": 0.1, "paint": 1.4})
    sc.material("M_Env_Snow", R_["cloud_snow"][2], R_["cloud_snow"], 0.05, opts={"rim": 0.1})
    sc.material("M_Env_Bench", R_["interior_wood"][3], R_["interior_wood"], 0.1)
    sc.material("M_Env_Lamp", R_["headlight"][2], R_["headlight"], 0.5, opts={"emissive": 0.8, "rim": 0, "paint": 0})
    sc.material("M_Env_Post", R_["train_trim"][1], R_["train_trim"], 0.2)
    sc.add(mk.Part("ArtCheck_Environment", None))
    plat = mk.merge(box(-7.5, 7.5, -0.75, 0.0, 0.10, 4.2, 0.14, "M_Env_Platform", 3),
                    box(-7.4, 7.4, -0.02, 0.008, 0.36, 0.50, 0.01, "M_Env_PlatformEdge"))
    sc.add(mk.Part("Platform", plat, (0, 0, 0), "ArtCheck_Environment"))
    trk = [box(-14, 14, -1.05, -0.78, -W - 0.6, 0.15, 0.10, "M_Env_Ballast")]
    for x in np.arange(-13.5, 14, 0.62):
        trk.append(box(x - 0.13, x + 0.13, -0.80, -0.68, -W - 0.25, -0.05, 0.03, "M_Env_Sleeper"))
    for zr in (ZC + 1.05 - 0.02, ZC - 1.05 + 0.02):
        trk.append(box(-14, 14, -0.68, -0.60, zr - 0.04, zr + 0.04, 0.015, "M_Env_Rail"))
    sc.add(mk.Part("Track", mk.merge(*trk), (0, 0, 0), "ArtCheck_Environment"))
    bench = mk.merge(box(-0.7, 0.7, 0.38, 0.46, -0.25, 0.25, 0.04, "M_Env_Bench"),
                     box(-0.7, 0.7, 0.5, 0.86, 0.16, 0.26, 0.04, "M_Env_Bench"),
                     box(-0.6, -0.5, 0.0, 0.4, -0.2, 0.2, 0.03, "M_Env_Post"),
                     box(0.5, 0.6, 0.0, 0.4, -0.2, 0.2, 0.03, "M_Env_Post"))
    sc.add(mk.Part("Bench", bench, (5.0, 0, 2.9), "ArtCheck_Environment"))
    lantern = mk.merge(mk.capsule_between((0, 0, 0), (0, 2.6, 0), 0.05, 0.04, 10, 2, mat="M_Env_Post"),
                       mk.ellipsoid(0.16, 0.2, 0.16, 14, 8, mat="M_Env_Lamp").transform(t=(0, 2.7, 0)),
                       mk.lathe([(0, 2.92), (0.22, 2.86), (0.24, 2.83), (0.0, 2.83)], 12, mat="M_Env_Post"))
    sc.add(mk.Part("Lantern", lantern, (-1.6, 0, 3.5), "ArtCheck_Environment"))
    ground = box(-60, 60, -1.2, -0.9, -60, -W - 0.5, 0.2, "M_Env_Grass")
    ground2 = box(-60, 60, -1.2, -0.80, 4.2, 40, 0.2, "M_Env_Grass")
    ground3 = box(-60, -7.7, -1.2, -0.85, -W - 0.55, 4.25, 0.2, "M_Env_Grass")
    sc.add(mk.Part("Ground", mk.merge(ground, ground2, ground3), (0, 0, 0), "ArtCheck_Environment"))
    pines = []
    for _ in range(30):
        x = random.uniform(-28, 22)
        z = random.uniform(-26, -5.0)
        s = random.uniform(1.6, 3.6)
        layers = [mk.lathe([(0, 0), (1.0, 0.0), (0.0, 1.1)], 8, mat="M_Env_Pine").transform(
            s=(0.55 * s * (1 - 0.22 * k), 0.55 * s, 0.55 * s * (1 - 0.22 * k)), t=(0, s * (0.35 + 0.42 * k), 0)) for k in range(3)]
        trunk = mk.capsule_between((0, -0.6, 0), (0, 0.5 * s, 0), 0.08 * s, 0.07 * s, 6, 1, mat="M_Env_Trunk")
        pines.append(mk.merge(trunk, *layers).transform(R=mk.rot_z(random.uniform(-0.08, 0.08)), t=(x, -0.9, z)))
    sc.add(mk.Part("Pines", mk.merge(*pines), (0, 0, 0), "ArtCheck_Environment"))
    mts = []
    for (x, z, h, w) in ((-34, -60, 26, 22), (-8, -70, 34, 26), (18, -62, 24, 20), (40, -75, 30, 24), (-56, -72, 22, 22)):
        prof = [(0, 0), (1.0, 0), (0.55, 0.45), (0.22, 0.82), (0.0, 1.0)]
        m = mk.lathe(prof, 7, mat="M_Env_Rock", phase=random.uniform(0, 1)).transform(s=(w, h, w * 0.7), t=(x, -1, z))
        cap = mk.lathe([(0.24, 0.80), (0.11, 0.9), (0.0, 1.0)], 7, mat="M_Env_Snow").transform(s=(w * 1.04, h * 1.005, w * 0.73), t=(x, -1, z))
        mts += [m, cap]
    mtn = mk.merge(*mts)
    mtn.sharp = True
    sc.add(mk.Part("Mountains", mtn, (0, 0, 0), "ArtCheck_Environment"))
    for p in sc.parts:
        p.gradient = 0.0 if p.name in ("Ground", "Platform", "Mountains") else 0.15
        p.edges = p.name != "Mountains"
    sc.facet_all()
    sc.style_all(cellSize=1.1)
    sc.bake_shading(density=60, radius=1.2, noise_freq=0.45, edge_range=(4.0, 14.0), max_samples=60000)
    return sc


def main():
    wag = build_wagon()
    env = build_env()
    rel = "Assets/Art/Models/Train/RegionalWagon/RegionalWagon_Blockout.fbx"
    exp_dir = os.path.join(ROOT, "ArtSource", "Exports")
    os.makedirs(os.path.join(ROOT, os.path.dirname(rel)), exist_ok=True)
    os.makedirs(exp_dir, exist_ok=True)
    wag.write_fbx_ascii(os.path.join(ROOT, rel))
    wag.write_glb(os.path.join(exp_dir, "RegionalWagon_Blockout.glb"))
    unity_assets.export_unity(ROOT, wag, rel, "Assets/Art/Materials/Train/RegionalWagon")
    env.write_glb(os.path.join(exp_dir, "ArtCheck_Environment.glb"))
    mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"],
                "opts": v["opts"]} for k, v in wag.materials.items()}
    json.dump({"model": "RegionalWagon_Blockout", "stats": wag.stats(), "materials": mats},
              open(os.path.join(exp_dir, "RegionalWagon_Blockout.materials.json"), "w", encoding="utf-8"), indent=2)
    print("wagon", wag.stats())
    print("env", env.stats())


if __name__ == "__main__":
    main()
