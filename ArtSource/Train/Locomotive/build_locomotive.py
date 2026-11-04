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
W = 2.30                    # breedte frame (wielen liggen erbuiten)
ZC = -1.35                  # hartlijn (perronkant op z = 0)
RAIL_Y = -0.62
FRAME = (-2.55, 3.42, -0.06, 0.34)     # frame: x0, x1, y0, y1 (bufferbalk direct onder de lamp)
CAB = (-2.35, 0.25)                     # cabine x (2,6 m: ruimte voor het kooloventje)
CAB_W = 2.50
CAB_FLOOR = 0.74
WALL_TOP = 1.36                         # borstwering (conducteur leunt erop)
ROOF_Y = 2.40                           # onderkant dak
BOILER = (0.20, 3.25)                   # ketel x (korter dan v2)
BOILER_W = 1.92
BOILER_SIDE_TOP = 0.92                  # tot hier rechte zijkant, daarboven de ronde bovenkant
BOILER_TOP = 1.50
LAMP_Y = 0.90
WHEEL_R = 0.40
WHEEL_X = (-1.45, 0.42, 2.28)
WHEEL_W = 0.22              # brede, stevige wielen
WHEEL_Z = W / 2 + 0.14      # wielhart t.o.v. hartlijn (buitenliggend)


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
        zz = (zf - 0.01, zf + 0.012) if sgn > 0 else (zf - 0.012, zf + 0.01)
        panels.append(rbox(BOILER[0] + 0.75, BOILER[1] - 0.70, 0.46, 0.84, zz[0], zz[1], 0.008, "M_Loco_Panel", div=2))
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
REAR_DOOR = 0.42            # halve breedte doorgang in de achterwand (naar de kolenwagen)
REAR_DOOR_TOP = ROOF_Y - 0.40


def build_cab():
    """Cabine uit één stuk: holle schaal (vloerbak, borstweringen, stijlen, bovenregels) met open bovenkant,
    zijopeningen, open voorkant (ketel) en een doorgang in de achterwand naar de kolenwagen."""
    z_out, z_in = ZC + CAB_W / 2, ZC - CAB_W / 2
    x0, x1 = CAB
    p, t, r = 0.26, 0.20, 0.07
    top = ROOF_Y + 0.02
    outer = (x0, x1, FRAME[3] - 0.02, top, z_in, z_out)
    inner = (x0 + t, x1 - t, CAB_FLOOR, top, z_in + t, z_out - t)
    win = (x0 + p, x1 - p, WALL_TOP, ROOF_Y - 0.26)
    holes = [(1, 1, x0 + r, x1 - r, z_in + r, z_out - r, x0 + t, x1 - t, z_in + t, z_out - t),   # open bovenkant (dak los)
             (2, 1) + win, (2, -1) + win,
             (0, 1, CAB_FLOOR, ROOF_Y - 0.26, z_in + p, z_out - p),                               # voorkant: ketel
             (0, -1, CAB_FLOOR, REAR_DOOR_TOP, ZC - REAR_DOOR, ZC + REAR_DOOR)]                   # achterdeur
    parts = [mk.hollow_box(outer, inner, holes, radius=r, step=0.25, div=2, mat="M_Loco_Red", inner_mat="M_Loco_Interior")]
    # gouden kaders uit één stuk rond de zijopeningen en de achterdoorgang
    side = mk.frame_ring(*win, 0.06, -0.012, 0.04, 0.02, mat="M_Loco_Gold")
    parts += [side.transform(t=(0, 0, z_out)), side.transform(s=(1, 1, -1), t=(0, 0, z_in))]
    rear = mk.frame_ring(ZC - REAR_DOOR, ZC + REAR_DOOR, CAB_FLOOR - 0.05, REAR_DOOR_TOP, 0.06, -0.012, 0.04, 0.02,
                         mat="M_Loco_Gold", open_bottom=True)
    parts.append(rear.transform(R=mk.rot_y(-math.pi / 2), t=(x0, 0, 0)))
    # geschilderde panelen ('vierkantjes', concept): dun, bijna vlak op de cabinewanden, doffer rood
    for zf, sgn in ((z_out, 1), (z_in, -1)):
        za, zb = (zf - 0.01, zf + 0.012) if sgn > 0 else (zf - 0.012, zf + 0.01)
        for (pa, pb) in ((x0 + 0.36, x0 + 1.22), (x0 + 1.40, x1 - 0.36)):
            parts.append(rbox(pa, pb, CAB_FLOOR + 0.06, WALL_TOP - 0.16, za, zb, 0.008, "M_Loco_Panel", div=2))
        parts.append(rbox(x0 + 0.36, x1 - 0.36, FRAME[3] + 0.06, CAB_FLOOR - 0.08, za, zb, 0.008, "M_Loco_Panel", div=2))
    for (za, zb) in ((z_in + 0.14, ZC - REAR_DOOR - 0.16), (ZC + REAR_DOOR + 0.16, z_out - 0.14)):
        parts.append(rbox(x0 - 0.012, x0 + 0.01, CAB_FLOOR + 0.06, WALL_TOP - 0.16, za, zb, 0.008, "M_Loco_Panel", div=2))
    # gewelfd dak met royaal overstek (donkerbruin) + donker plafond
    rz0, rz1 = ZC - CAB_W / 2 - 0.26, ZC + CAB_W / 2 + 0.26
    roof_prof = mk.superellipse_arch(rz0, rz1, ROOF_Y + 0.14, ROOF_Y + 0.46, ROOF_Y, n=3.2, seg=22, fillet=0.06)
    roof = mk.extrude_profile(roof_prof, x0 - 0.30, x1 + 0.34, nx=10, round0=0.10, round1=0.10, rings=3, mat="M_Loco_Roof")
    roof = mk.merge(roof, rbox(x0 + t, x1 - t, ROOF_Y - 0.04, ROOF_Y + 0.015, z_in + t, z_out - t, 0.005, "M_Loco_Interior"))
    return mk.merge(*parts), roof


# ----------------------------------------------------------------- onderstel
def build_frame():
    x0, x1, y0, y1 = FRAME
    parts = [
        # hoofdframe met ruime ronde langsranden + rode bovenrand
        rbox(x0, x1, y0, y1 - 0.06, ZC - W / 2, ZC + W / 2, 0.12, "M_Loco_Frame", 1.2, div=2),
        rbox(x0 + 0.03, x1 - 0.03, y1 - 0.09, y1, ZC - W / 2 - 0.015, ZC + W / 2 + 0.015, 0.035, "M_Loco_Red", div=2),
        # frameliggers tussen de wielen: geven massa aan de onderkant, onderrand rond
        rbox(x0 + 0.45, x1 - 0.55, -0.42, y0 + 0.04, ZC - W / 2 + 0.10, ZC - W / 2 + 0.30, 0.08, "M_Loco_Frame", div=2),
        rbox(x0 + 0.45, x1 - 0.55, -0.42, y0 + 0.04, ZC + W / 2 - 0.30, ZC + W / 2 - 0.10, 0.08, "M_Loco_Frame", div=2),
        # ronde buik onder de ketel (asbak/watertank)
        rbox(BOILER[0] + 0.25, BOILER[1] - 0.45, -0.34, y0 + 0.04, ZC - 0.62, ZC + 0.62, 0.16, "M_Loco_Trim", div=2),
    ]
    beam_half = 1.00                                   # bufferbalk smaller dan het frame
    for sgn, xe in ((1, x1), (-1, x0)):
        bx0, bx1 = (xe - 0.02, xe + 0.15) if sgn > 0 else (xe - 0.15, xe + 0.02)
        parts.append(rbox(bx0, bx1, -0.04, 0.38, ZC - beam_half, ZC + beam_half, 0.07, "M_Loco_Red", div=2))
        for zs in (ZC - beam_half - 0.02, ZC + beam_half - 0.15):
            parts.append(rbox(bx0 - 0.01, bx1 + 0.02, -0.03, 0.37, zs, zs + 0.17, 0.07, "M_Loco_Gold", div=2))  # ronde hoekkussens
        for dz in (-0.62, 0.62):
            xs = xe + sgn * 0.22
            parts.append(mk.cylinder(0.065, 0.16, 12, 0.02, mat="M_Loco_Frame").transform(t=(xs, 0.17, ZC + dz)))
            parts.append(rbox(xe + sgn * 0.33 - 0.08, xe + sgn * 0.33 + 0.08, 0.06, 0.29, ZC + dz - 0.12, ZC + dz + 0.12, 0.07,
                              "M_Loco_Trim", div=2))                                                               # ronde blokbuffer
        parts.append(rbox(min(xe, xe + sgn * 0.40), max(xe, xe + sgn * 0.40), 0.10, 0.24, ZC - 0.09, ZC + 0.09, 0.05,
                          "M_Loco_Frame", div=2))                                                                  # koppeling
    # trapjes onder de cabine
    for zf, sgn in ((ZC + CAB_W / 2, 1), (ZC - CAB_W / 2, -1)):
        for (y0s, y1s, d) in ((-0.30, -0.22, 0.14), (-0.02, 0.06, 0.06)):
            za, zb = (zf - 0.06, zf + d) if sgn > 0 else (zf - d, zf + 0.06)
            parts.append(rbox(-1.95, -1.33, y0s, y1s, za, zb, 0.025, "M_Loco_Frame"))
        for xs in (-1.93, -1.41):
            za, zb = (zf - 0.06, zf + 0.13) if sgn > 0 else (zf - 0.13, zf + 0.06)
            parts.append(rbox(xs, xs + 0.06, -0.30, 0.10, za, zb, 0.02, "M_Loco_Frame"))
    return mk.merge(*parts)


def build_cylinders():
    """Stoomcilinders voor de wielen (beide kanten) met gouden banden en zuigerstang naar het voorwiel."""
    parts = []
    yc = FRAME[2] - 0.04                      # op wielashoogte, tegen het frame
    for sgn in (1, -1):
        z = ZC + sgn * (WHEEL_Z - 0.04)
        x0c, x1c = WHEEL_X[-1] + WHEEL_R + 0.10, FRAME[1] - 0.04
        L = x1c - x0c
        body = mk.cylinder(0.21, L, 16, 0.07, mat="M_Loco_Red", axis="x").transform(t=((x0c + x1c) / 2, yc, z))
        bands = [mk.cylinder(0.225, 0.05, 16, 0.015, mat="M_Loco_Gold", axis="x").transform(t=(xb, yc, z))
                 for xb in (x0c + 0.07, x1c - 0.07)]
        cap = mk.cylinder(0.12, 0.06, 16, 0.02, mat="M_Loco_Frame", axis="x").transform(t=(x1c + 0.02, yc, z))
        piston = mk.capsule_between((x0c - 0.02, yc, z + sgn * 0.06), (WHEEL_X[-1] + 0.10, RAIL_Y + WHEEL_R - 0.12, z + sgn * 0.17),
                                    0.032, 0.032, 10, 2, mat="M_Loco_Gold")
        saddle = rbox(x0c + 0.05, x1c - 0.05, yc + 0.12, FRAME[3] - 0.10, z - 0.12, z + 0.12, 0.05, "M_Loco_Frame")
        parts += [body, *bands, cap, piston, saddle]
    return mk.merge(*parts)


def build_firebox():
    """Kooloventje in de cabine (achterkant ketel): donker blok met kachelpijp, drukmeter en kolenbak.
    Losse onderdelen voor gameplay: FireboxDoor (scharniert om lokale Y) en Regulator (draait om lokale Z)."""
    x0, x1 = CAB
    fx0, fx1 = x1 - 0.82, x1 - 0.24
    stove = rbox(fx0, fx1, CAB_FLOOR - 0.01, CAB_FLOOR + 0.86, ZC - 0.40, ZC + 0.40, 0.09, "M_Loco_Trim", div=3)
    top = rbox(fx0 - 0.03, fx1 + 0.03, CAB_FLOOR + 0.84, CAB_FLOOR + 0.92, ZC - 0.43, ZC + 0.43, 0.04, "M_Loco_Gold", div=3)
    pipe = mk.capsule_between((fx0 + 0.28, CAB_FLOOR + 0.9, ZC + 0.18), (fx0 + 0.28, ROOF_Y + 0.05, ZC + 0.18),
                              0.075, 0.075, 12, 2, mat="M_Loco_Frame")
    gauge = mk.merge(
        mk.cylinder(0.11, 0.05, 20, 0.015, mat="M_Loco_Gold", axis="x").transform(t=(fx0 - 0.02, CAB_FLOOR + 1.10, ZC - 0.20)),
        mk.cylinder(0.085, 0.02, 20, 0.005, mat="M_Loco_Dial", axis="x").transform(t=(fx0 - 0.045, CAB_FLOOR + 1.10, ZC - 0.20)),
        rbox(fx0 - 0.06, fx0 - 0.045, CAB_FLOOR + 1.09, CAB_FLOOR + 1.17, ZC - 0.21, ZC - 0.19, 0.004, "M_Loco_Trim"),
        mk.capsule_between((fx0 + 0.05, CAB_FLOOR + 0.92, ZC - 0.20), (fx0 - 0.01, CAB_FLOOR + 1.05, ZC - 0.20), 0.025, 0.025, 8, 1,
                           mat="M_Loco_Gold"))
    rng = np.random.default_rng(3)
    lumps = []
    for k in range(16):
        sx, sy, sz = rng.uniform(0.07, 0.12), rng.uniform(0.05, 0.08), rng.uniform(0.07, 0.11)
        lump = mk.ellipsoid(sx, sy, sz, 7, 4, mat="M_Loco_Coal")
        lump.sharp = True                                   # facetjes = steenkool
        lumps.append(lump.transform(R=mk.rot_y(rng.uniform(0, math.pi)) @ mk.rot_x(rng.uniform(-0.4, 0.4)),
                                    t=(x0 + 0.34 + rng.uniform(0, 0.36), CAB_FLOOR + 0.40 + rng.uniform(-0.01, 0.06),
                                       ZC - 0.98 + rng.uniform(0, 0.42))))
    # schepbak in de hoek (overkant), zodat de doorgang naar de kolenwagen vrij blijft
    bin_ = mk.merge(rbox(x0 + 0.22, x0 + 0.80, CAB_FLOOR - 0.01, CAB_FLOOR + 0.42, ZC - 1.04, ZC - 0.50, 0.05, "M_Loco_RedDark", div=3),
                    *lumps)
    return mk.merge(stove, top, pipe, gauge), bin_, (fx0, fx1)


def build_firebox_door():
    """Vuurdeurtje (los onderdeel; pivot = scharnier links), gouden rand met gloeiend venster."""
    d = mk.merge(rbox(-0.015, 0.03, -0.17, 0.17, 0.0, 0.40, 0.03, "M_Loco_Gold", div=3),
                 rbox(-0.035, -0.005, -0.10, 0.10, 0.08, 0.32, 0.025, "M_Loco_Fire", div=3),
                 mk.ellipsoid(0.02, 0.03, 0.03, 8, 4, mat="M_Loco_Trim").transform(t=(-0.03, 0.0, 0.36)))
    return d


def build_regulator():
    """Snelheidshendel (los onderdeel; pivot = as, draait om lokale Z). Geel = interactie."""
    return mk.merge(mk.cylinder(0.05, 0.08, 12, 0.01, mat="M_Loco_Frame", axis="z"),
                    mk.capsule_between((0, 0, 0.0), (-0.05, 0.42, 0.0), 0.025, 0.022, 8, 2, mat="M_Loco_Trim"),
                    mk.ellipsoid(0.055, 0.055, 0.055, 12, 6, mat="M_Loco_Interact").transform(t=(-0.05, 0.45, 0)))


def build_wheels():
    """Brede buitenliggende wielen met dikke rode naaf en een gouden drijfstang per kant."""
    parts = []
    yc = RAIL_Y + WHEEL_R
    for sgn in (1, -1):
        zs = ZC + sgn * WHEEL_Z
        for x in WHEEL_X:
            tire = mk.cylinder(WHEEL_R, WHEEL_W, 16, 0.06, mat="M_Loco_Wheel", axis="z")
            hub = mk.cylinder(WHEEL_R * 0.70, WHEEL_W + 0.03, 14, 0.04, mat="M_Loco_Red", axis="z")
            cap = mk.cylinder(0.11, WHEEL_W + 0.07, 12, 0.03, mat="M_Loco_RedDark", axis="z")
            parts += [m.transform(t=(x, yc, zs)) for m in (tire, hub, cap)]
        rz = zs + sgn * (WHEEL_W / 2 + 0.06)
        parts.append(rbox(WHEEL_X[0] - 0.09, WHEEL_X[-1] + 0.09, yc - 0.17, yc - 0.07, rz - 0.035, rz + 0.035, 0.03, "M_Loco_Gold"))
        for x in WHEEL_X:
            parts.append(mk.cylinder(0.06, 0.12, 10, 0.02, mat="M_Loco_Gold", axis="z").transform(t=(x, yc - 0.12, rz)))
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
    sc.material("M_Loco_Panel", R_["train_red"][1], R_["train_red"], 0.20, ramp_shift=-0.22, opts={"paint": 1.6, "edge": 0.0})
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
    fire_ramp = [R_["vest"][0], R_["vest"][2], R_["helmet"][2], R_["headlight"][1], R_["headlight"][3]]
    sc.material("M_Loco_Fire", R_["vest"][2], fire_ramp, 0.40, emissive=R_["helmet"][2], opts={"emissive": 1.0, "rim": 0.0, "paint": 0.6})
    sc.material("M_Loco_Coal", R_["ui_bg"][1], R_["ui_bg"], 0.30, ramp_shift=-0.1, opts={"paint": 1.0, "edge": 1.6})
    sc.material("M_Loco_Dial", R_["parchment"][3], R_["parchment"], 0.30, opts={"flat": 0.5, "paint": 0.3})
    sc.add(mk.Part("Locomotive", None))
    sc.add(mk.Part("Frame", build_frame(), (0, 0, 0), "Locomotive"))
    cab, roof = build_cab()
    sc.add(mk.Part("Cab", cab, (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("CabRoof", roof, (0, 0, 0), "Cab"))   # los: te verbergen voor een camera van bovenaf
    sc.add(mk.Part("Boiler", build_boiler(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("HeadLight", build_headlight(), (BOILER[1] - 0.06, LAMP_Y, ZC), "Locomotive"))
    sc.add(mk.Part("Wheels", build_wheels(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("Cylinders", build_cylinders(), (0, 0, 0), "Locomotive"))
    firebox, coal, (fx0, fx1) = build_firebox()
    sc.add(mk.Part("Firebox", firebox, (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("FireboxDoor", build_firebox_door(), (fx0, CAB_FLOOR + 0.44, ZC - 0.20), "Firebox"))
    sc.add(mk.Part("Regulator", build_regulator(), (fx0 - 0.02, CAB_FLOOR + 0.62, ZC + 0.48), "Firebox"))
    sc.add(mk.Part("CoalBin", coal, (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("GrabRails", build_grabrails(), (0, 0, 0), "Locomotive"))
    for p in sc.parts:
        p.gradient = 0.30
        if p.name in ("Cab", "CabRoof", "Boiler", "Frame", "HeadLight", "Cylinders", "Firebox", "CoalBin", "FireboxDoor", "Regulator"):
            p.ao_group = "body"
    sc.get("Wheels").gradient = 0.12
    sc.facet_all()
    sc.style_all(grime=1.0, **mk.TRAIN_CELLS)
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
