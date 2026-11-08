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

Cabine v3 (referentiebeeld @mlemson, 8 nov 2026):
  * brede lessenaar over bijna de hele cabinebreedte: startknop | vuurdeur | hittemeter | schuifhendel | bordje;
  * de snelheidshendel is een brede trekhendel die van boven naar beneden door een gouden sleuf schuift
    (boven = vooruit, midden = neutraal, onder = achteruit; zie het bordje met pijlen);
  * hittemeter (oranje/rode zone, vlammetje) met losse 'HeatNeedle' die de brandstof van het vuur toont;
  * plafondlamp met gouden kooi ('CabLamp', hangt aan het dak);
  * geen kachelpijp en geen kolenbak in de cabine meer: de schepkolen liggen nu op de kolenwagen.

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
from train_couplers import add_couplers
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
MAIN_ROD_Z = WHEEL_Z + WHEEL_W / 2 + 0.135   # drijfstang: buiten de koppelstang (die ligt op +0.06)


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
    return mk.merge(boiler, *panels, cap, whistle, build_chimney())


def build_chimney():
    """Schoorsteen op de rookkast (issue #107): rode voet die in de ketel zakt, donkere pijp met
    uitlopende kraag en een gouden kroonrand bovenop. Twaalf facetten, net als de rest van de loc."""
    xc, y0 = BOILER[1] - 0.85, BOILER_TOP - 0.06
    seg = 12
    foot = mk.lathe([(0.0, -0.10), (0.27, -0.10), (0.27, 0.08), (0.21, 0.17), (0.16, 0.24), (0.0, 0.24)], seg,
                    mat="M_Loco_Red", phase=math.pi / seg)
    stack = mk.lathe([(0.0, 0.18), (0.155, 0.18), (0.155, 0.70), (0.19, 0.78), (0.0, 0.78)], seg,
                     mat="M_Loco_Trim", phase=math.pi / seg)
    crown = mk.lathe([(0.0, 0.76), (0.215, 0.76), (0.235, 0.80), (0.235, 0.90), (0.20, 0.93), (0.13, 0.93),
                      (0.11, 0.89), (0.0, 0.89)], seg, mat="M_Loco_Gold", phase=math.pi / seg)
    soot = mk.cylinder(0.115, 0.02, seg, 0.0, mat="M_Loco_Frame", axis="y").transform(t=(0, 0.895, 0))
    band = mk.lathe([(0.0, 0.30), (0.17, 0.30), (0.17, 0.36), (0.0, 0.36)], seg, mat="M_Loco_Gold", phase=math.pi / seg)
    return mk.merge(foot, stack, crown, soot, band).transform(t=(xc, y0, ZC))


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
    # Laat de gouden lip over de dagkant vallen zonder hetzelfde vlak te delen.
    side = mk.frame_ring(win[0] + 0.014, win[1] - 0.014, win[2] + 0.014, win[3] - 0.014,
                         0.06, -0.018, 0.04, 0.02, mat="M_Loco_Gold")
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
    # Bij het frame houden zodat het opstapje de grenzen van de cabinecamera niet vergroot.
    cab_rear = CAB[0]
    step_end = cab_rear - 0.66
    parts.append(rbox(step_end, cab_rear + 0.025, CAB_FLOOR - 0.14, CAB_FLOOR - 0.012,
                      ZC - 0.62, ZC + 0.62, 0.028, "M_Loco_Trim"))
    parts.append(rbox(step_end + 0.035, cab_rear - 0.006, CAB_FLOOR - 0.021, CAB_FLOOR,
                      ZC - REAR_DOOR, ZC + REAR_DOOR, 0.007, "M_Loco_Interact"))
    post = step_end + 0.07
    top_rail = CAB_FLOOR + 0.68
    for dz in (-0.55, 0.55):
        z = ZC + dz
        parts += [mk.capsule_between((cab_rear + 0.022, top_rail + 0.06, z), (post, top_rail, z),
                                     0.028, 0.028, 10, 2, mat="M_Loco_Interact"),
                  mk.capsule_between((post, top_rail, z), (post, CAB_FLOOR - 0.03, z),
                                     0.028, 0.028, 10, 2, mat="M_Loco_Interact")]
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
        # drijfstang ligt buiten wiel én koppelstang (issue #107): kruiskop aan het cilindereinde steekt naar buiten,
        # de stang loopt daarvandaan recht naar een verlengde krukpen op het voorwiel
        z_rod = ZC + sgn * MAIN_ROD_Z
        y_pin = RAIL_Y + WHEEL_R - 0.12
        za, zb = sorted((z, z_rod + sgn * 0.05))
        crosshead = rbox(x0c - 0.07, x0c + 0.05, yc - 0.07, yc + 0.07, za, zb, 0.03, "M_Loco_Gold")
        piston = mk.capsule_between((x0c - 0.02, yc, z_rod), (WHEEL_X[-1], y_pin, z_rod), 0.034, 0.034, 10, 2, mat="M_Loco_Gold")
        saddle = rbox(x0c + 0.05, x1c - 0.05, yc + 0.12, FRAME[3] - 0.10, z - 0.12, z + 0.12, 0.05, "M_Loco_Frame")
        parts += [body, *bands, cap, crosshead, piston, saddle]
    return mk.merge(*parts)


# ----------------------------------------------------------- lessenaar (cabine v3)
# Alles op het voorvlak wordt gebouwd in 'vlakruimte': u = naar rechts (gezien vanuit de cabine, = +Z),
# v = omhoog vanaf de cabinevloer, d = uit het vlak de cabine in (= -X). face() zet dat om naar loc-ruimte.
FIRE_X0, FIRE_X1 = CAB[1] - 0.98, CAB[1] - 0.24      # voorvlak (kijkt de cabine in) en achterkant
CONSOLE_HW = 0.98                                     # halve breedte (cabine binnen: 1,05)
CONSOLE_H = 0.72
BUTTON_UV = (-0.80, 0.43)                             # startknop
DOOR_HINGE_U = -0.62                                  # vuurdeur 0,76 breed: u -0,62 .. +0,14
DOOR_MID_U = DOOR_HINGE_U + 0.38
HEAT_UV, HEAT_R = (0.30, 0.42), 0.125                 # hittemeter
LEVER_U, LEVER_V = 0.61, 0.41                         # hendel in de middenstand
LEVER_TRAVEL = 0.19                                   # = TrainDrive.LeverTravel (boven = vooruit, onder = achteruit)
LEVER_OUT = 0.11                                      # hart van de trekstang voor het vlak
SLOT = (0.58, 0.64, 0.16, 0.655)                      # sleuf u0, u1, v0, v1
PLATE = (0.745, 0.935, 0.115, 0.70)                   # bordje met pijlen
LAMP_X = (CAB[0] + CAB[1]) / 2                        # plafondlamp midden in de cabine


def face(m, u=0.0, v=0.0, d=0.0):
    """Mesh uit vlakruimte (x = u, y = v, z = d) naar loc-ruimte op het voorvlak van de lessenaar."""
    return m.transform(R=mk.rot_y(-math.pi / 2), t=(FIRE_X0 - d, CAB_FLOOR + v, ZC + u))


def flat(points, z0, z1, mat, c=0.0015):
    """Plat icoontje/wijzer (polygoon in het vlak) met een klein afgeschuind randje."""
    return mk.prism(points, z0, z1, c, mat=mat)


def build_gauge(r, zones=(), flame=False, depth=0.05, needle_angle=-120.0):
    """Ronde meter in vlakruimte (midden = oorsprong, kijkt naar +z): gouden rand, perkamenten wijzerplaat,
    streepjes over 240 graden, optioneel gekleurde zones en een vlammetje. Geeft (meter, wijzer) terug; de wijzer
    staat op `needle_angle` (graden vanaf boven, met de klok mee) en draait om z."""
    seg = 20
    ri = r * 0.80
    # zelfde opbouw als de drukmeter op het blad: gouden schijf met afgeronde rand, perkamenten plaat ervoor
    bezel = mk.cylinder(r, depth, seg, 0.015, mat="M_Loco_Gold", axis="z").transform(t=(0, 0, depth / 2))
    zf = depth + 0.004                                       # voorkant van de wijzerplaat
    dial = mk.cylinder(ri, 0.010, seg, 0.003, mat="M_Loco_Dial", axis="z").transform(t=(0, 0, zf - 0.005))
    parts = [bezel, dial]

    def sector(a0, a1, r0, r1, n=10):
        pts = [(r1 * math.sin(math.radians(a0 + (a1 - a0) * k / n)), r1 * math.cos(math.radians(a0 + (a1 - a0) * k / n)))
               for k in range(n + 1)]
        pts += [(r0 * math.sin(math.radians(a1 - (a1 - a0) * k / n)), r0 * math.cos(math.radians(a1 - (a1 - a0) * k / n)))
                for k in range(n + 1)]
        return pts

    for (a0, a1, mat) in zones:
        parts.append(flat(sector(a0, a1, ri * 0.66, ri * 0.92), zf, zf + 0.004, mat))
    for k in range(9):                                       # streepjes: -120 .. +120 graden
        a = -120.0 + 30.0 * k
        length = ri * (0.24 if k % 2 == 0 else 0.15)
        tick = flat([(-0.0045, ri * 0.90 - length), (0.0045, ri * 0.90 - length), (0.0045, ri * 0.90), (-0.0045, ri * 0.90)],
                    zf, zf + 0.006, "M_Loco_Trim")
        parts.append(tick.transform(R=mk.rot_z(-math.radians(a))))
    if flame:                                                # vlammetje onderin (tussen de uiteinden van de schaal)
        outer = [(0.0, 1.0), (0.14, 0.78), (0.30, 0.56), (0.36, 0.34), (0.30, 0.13), (0.15, 0.02), (-0.15, 0.02),
                 (-0.30, 0.13), (-0.36, 0.34), (-0.30, 0.52), (-0.20, 0.66), (-0.14, 0.50), (-0.06, 0.70)]
        inner = [(0.0, 0.62), (0.13, 0.40), (0.17, 0.22), (0.09, 0.08), (-0.09, 0.08), (-0.17, 0.22), (-0.13, 0.40)]
        h, y0 = ri * 0.46, -ri * 0.78
        parts.append(flat([(x * h, y0 + y * h) for x, y in outer], zf, zf + 0.004, "M_Loco_Warm"))
        parts.append(flat([(x * h, y0 + y * h) for x, y in inner], zf + 0.003, zf + 0.007, "M_Loco_Gold"))
    needle = mk.merge(
        flat([(-0.0075, -ri * 0.16), (0.0075, -ri * 0.16), (0.0028, ri * 0.80), (-0.0028, ri * 0.80)], zf + 0.007, zf + 0.012,
             "M_Loco_Trim"),
        mk.cylinder(ri * 0.13, 0.010, 12, 0.003, mat="M_Loco_Gold", axis="z").transform(t=(0, 0, zf + 0.013)))
    needle = needle.transform(R=mk.rot_z(-math.radians(needle_angle)))
    return mk.merge(*parts), needle


def build_lever_unit():
    """Gouden sleuf met donkere groef voor de trekhendel + bordje (boven = vooruit, streep = stil, onder = achteruit)."""
    u0, u1, v0, v1 = SLOT
    w = 0.045
    parts = [mk.frame_ring(u0, u1, v0, v1, w, 0.0, 0.035, 0.012, mat="M_Loco_Gold"),
             rbox(u0 - 0.004, u1 + 0.004, v0 - 0.004, v1 + 0.004, -0.012, 0.004, 0.02, "M_Loco_Frame")]
    for uu in (u0 - w / 2, u1 + w / 2):
        for vv in (v0 - w / 2, v1 + w / 2):
            parts.append(mk.ellipsoid(0.011, 0.011, 0.007, 10, 5, mat="M_Loco_Gold").transform(t=(uu, vv, 0.035)))
    pu0, pu1, pv0, pv1 = PLATE
    parts += [rbox(pu0, pu1, pv0, pv1, -0.03, 0.012, 0.02, "M_Loco_RedDark"),
              mk.frame_ring(pu0 + 0.016, pu1 - 0.016, pv0 + 0.016, pv1 - 0.016, 0.010, 0.010, 0.018, 0.003, mat="M_Loco_Gold")]
    cu = (pu0 + pu1) / 2
    z0, z1 = 0.011, 0.019
    arrow = [(0.0, 0.045), (0.040, 0.008), (0.013, 0.008), (0.013, -0.042), (-0.013, -0.042), (-0.013, 0.008), (-0.040, 0.008)]
    train = [(-0.050, 0.010), (0.050, 0.010), (0.050, 0.032), (0.036, 0.038), (0.036, 0.054), (0.024, 0.054),
             (0.024, 0.038), (-0.012, 0.038), (-0.012, 0.058), (-0.056, 0.058), (-0.056, 0.051), (-0.050, 0.051)]

    def icon_train(v):
        body = flat([(x + cu, y + v - 0.034) for x, y in train], z0, z1, "M_Loco_Dial")
        wheels = [mk.cylinder(0.012, z1 - z0 + 0.002, 12, 0.002, mat="M_Loco_Dial", axis="z").transform(
            t=(cu + xw, v - 0.024, (z0 + z1) / 2 + 0.001)) for xw in (-0.030, 0.026)]
        return mk.merge(body, *wheels)

    parts += [flat([(x + cu, y + 0.62) for x, y in arrow], z0, z1, "M_Loco_Dial"),
              icon_train(0.53),
              rbox(cu - 0.032, cu + 0.032, LEVER_V - 0.008, LEVER_V + 0.008, z0, z1, 0.003, "M_Loco_Dial"),
              flat([(x + cu, -y + 0.30) for x, y in arrow], z0, z1, "M_Loco_Dial"),
              icon_train(0.21)]
    return mk.merge(*parts)


def build_firebox():
    """Brede lessenaar met kooloven (achterkant ketel): vuurdeur-opening, gouden rand met rood blad, drukmeter op
    het blad, startknopring, hittemeter, sleuf + bordje voor de trekhendel en vier gouden bouten.
    Losse onderdelen voor gameplay: FireboxDoor, Regulator, StartButton, SpeedNeedle en HeatNeedle."""
    fx0, fx1 = FIRE_X0, FIRE_X1
    lo, hi = DOOR_HINGE_U + 0.04, DOOR_HINGE_U + 0.72      # vuuropening achter de deur
    stove = mk.hollow_box(
        (fx0, fx1, CAB_FLOOR - 0.01, CAB_FLOOR + CONSOLE_H, ZC - CONSOLE_HW, ZC + CONSOLE_HW),
        (fx0 + 0.07, fx1 - 0.08, CAB_FLOOR + 0.10, CAB_FLOOR + 0.60, ZC + lo - 0.07, ZC + hi + 0.07),
        [(0, -1, CAB_FLOOR + 0.13, CAB_FLOOR + 0.52, ZC + lo, ZC + hi)],
        radius=0.09, mat="M_Loco_Trim", inner_mat="M_Loco_Frame")
    top = mk.merge(
        rbox(fx0 - 0.03, fx1 + 0.03, CAB_FLOOR + 0.70, CAB_FLOOR + 0.75, ZC - CONSOLE_HW - 0.03, ZC + CONSOLE_HW + 0.03,
             0.024, "M_Loco_Gold"),
        rbox(fx0 - 0.02, fx1 + 0.02, CAB_FLOOR + 0.738, CAB_FLOOR + 0.78, ZC - CONSOLE_HW - 0.02, ZC + CONSOLE_HW + 0.02,
             0.020, "M_Loco_RedDark"))
    bu, bv = BUTTON_UV
    button_ring = mk.cylinder(0.102, 0.038, 16, 0.009, mat="M_Loco_Gold", axis="x").transform(
        t=(fx0 - 0.018, CAB_FLOOR + bv, ZC + bu))
    gauge = mk.merge(
        mk.cylinder(0.11, 0.05, 20, 0.015, mat="M_Loco_Gold", axis="x").transform(t=(fx0 - 0.02, CAB_FLOOR + 0.96, ZC - 0.20)),
        mk.cylinder(0.085, 0.02, 20, 0.005, mat="M_Loco_Dial", axis="x").transform(t=(fx0 - 0.045, CAB_FLOOR + 0.96, ZC - 0.20)),
        mk.capsule_between((fx0 + 0.05, CAB_FLOOR + 0.78, ZC - 0.20), (fx0 - 0.01, CAB_FLOOR + 0.91, ZC - 0.20), 0.025, 0.025, 8, 1,
                           mat="M_Loco_Gold"),
        *(rbox(-0.007, -0.005, 0.062, 0.081, -0.004, 0.004, 0.0008, "M_Loco_Trim").transform(
            R=mk.rot_x(math.radians(angle)), t=(fx0 - 0.05, CAB_FLOOR + 0.96, ZC - 0.20))
          for angle in range(-120, 121, 40)))
    heat, _ = build_gauge(HEAT_R, zones=((30.0, 75.0, "M_Loco_Warm"), (75.0, 120.0, "M_Loco_Red")), flame=True)
    bolts = [face(mk.ellipsoid(0.022, 0.022, 0.012, 12, 6, mat="M_Loco_Gold"), uu, vv, 0.002)
             for uu in (-0.92, 0.46) for vv in (0.06, 0.64)]
    return mk.merge(stove, top, gauge, button_ring, face(heat, *HEAT_UV), face(build_lever_unit()), *bolts), (fx0, fx1)


def build_heat_needle():
    """Wijzer van de hittemeter (los: TrainDrive draait hem om lokale X, net als SpeedNeedle). Rust = koud (-120)."""
    _, needle = build_gauge(HEAT_R, needle_angle=-120.0)
    return needle.transform(R=mk.rot_y(-math.pi / 2))


def build_firebox_door():
    """Vuurdeurtje (los onderdeel; pivot = scharnier links), gouden rand met gloeiend venster."""
    d = mk.merge(rbox(-0.025, 0.03, -0.24, 0.24, 0.0, 0.76, 0.03, "M_Loco_Gold", div=3),
                 rbox(-0.045, -0.01, -0.196, 0.196, 0.048, 0.712, 0.025, "M_Loco_RedDark"),
                 rbox(-0.055, -0.035, -0.126, 0.126, 0.12, 0.59, 0.009, "M_Loco_Fire"),
                 *(mk.cylinder(0.037, 0.13, 12, 0.009, mat="M_Loco_Frame", axis="y").transform(
                     t=(0, y, 0.014)) for y in (-0.155, 0.155)))
    return d


def build_regulator():
    """Brede trekhendel (los onderdeel; pivot = hart van de greep in de middenstand). Schuift langs lokale Y door de
    sleuf; de donkere steel steekt in de groef. Geel = interactie."""
    half = 0.10
    return mk.merge(
        mk.cylinder(0.040, 2 * half, 14, 0.012, mat="M_Loco_Interact", axis="z"),
        *(mk.cylinder(0.047, 0.03, 14, 0.010, mat="M_Loco_Frame", axis="z").transform(t=(0, 0, s * (half + 0.012)))
          for s in (-1, 1)),
        mk.cylinder(0.034, 0.045, 12, 0.008, mat="M_Loco_Gold", axis="x").transform(t=(0.045, 0, 0)),
        rbox(0.05, LEVER_OUT + 0.02, -0.022, 0.022, -0.022, 0.022, 0.008, "M_Loco_Frame"))


def build_cab_lamp():
    """Plafondlamp: gouden voet, gloeiende glazen koepel met gouden kooi en een knopje onderaan (pivot = plafond)."""
    seg = 16
    y_rim = -0.050
    rg, ry = 0.112, 0.120
    base = mk.lathe([(0.0, 0.0), (0.150, 0.0), (0.150, -0.016), (0.132, -0.034), (0.118, -0.042), (0.0, -0.046)], seg,
                    mat="M_Loco_Gold", phase=math.pi / seg)
    collar = mk.torus(0.120, 0.014, seg, 6, mat="M_Loco_Gold").transform(t=(0, y_rim, 0))
    glass = mk.lathe([(rg * math.cos(math.radians(a)), y_rim - ry * math.sin(math.radians(a))) for a in range(0, 91, 15)],
                     seg, mat="M_Loco_Lamp", phase=math.pi / seg)
    cage = [collar]
    rc, ryc = rg + 0.010, ry + 0.010
    for phi in (0.0, math.pi / 2):                          # twee bogen die elkaar onderin kruisen
        pts = [(rc * math.cos(math.radians(a)) * math.cos(phi), y_rim - ryc * math.sin(math.radians(a)),
                rc * math.cos(math.radians(a)) * math.sin(phi)) for a in range(0, 181, 20)]
        cage += [mk.capsule_between(p, q, 0.0085, 0.0085, 8, 1, mat="M_Loco_Gold") for p, q in zip(pts[:-1], pts[1:])]
    lat = math.radians(48)
    cage.append(mk.torus(rc * math.cos(lat), 0.0085, seg, 5, mat="M_Loco_Gold").transform(t=(0, y_rim - ryc * math.sin(lat), 0)))
    knob = mk.ellipsoid(0.022, 0.018, 0.022, 10, 5, mat="M_Loco_Gold").transform(t=(0, y_rim - ryc - 0.010, 0))
    return mk.merge(base, glass, *cage, knob)


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
        # verlengde krukpen op het voorwiel: draagt de drijfstang buiten de koppelstang
        z_rod = ZC + sgn * MAIN_ROD_Z
        zp0, zp1 = sorted((rz, z_rod + sgn * 0.05))
        parts.append(mk.cylinder(0.05, zp1 - zp0, 10, 0.015, mat="M_Loco_Gold", axis="z").transform(
            t=(WHEEL_X[-1], yc - 0.12, (zp0 + zp1) / 2)))
    return mk.merge(*parts)


def build_grabrails():
    """Robuuste gebogen grepen: verticale stang met twee afgeronde steunen (geel = interactie)."""
    parts = []
    x0, x1 = CAB
    for zf, sgn in ((ZC + CAB_W / 2, 1), (ZC - CAB_W / 2, -1)):
        z = zf + sgn * 0.13
        for xg in (x0 + 0.12,):                     # midden op de achterstijl (raam begint op x0 + 0.26)
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
    sc.material("M_Loco_Warm", R_["vest"][2], R_["vest"], 0.30, opts={"flat": 0.4, "paint": 0.4})   # oranje zone hittemeter
    sc.add(mk.Part("Locomotive", None))
    sc.add(mk.Part("Frame", build_frame(), (0, 0, 0), "Locomotive"))
    cab, roof = build_cab()
    sc.add(mk.Part("Cab", cab, (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("CabRoof", roof, (0, 0, 0), "Cab"))   # los: te verbergen voor een camera van bovenaf
    sc.add(mk.Part("CabLamp", build_cab_lamp(), (LAMP_X, ROOF_Y - 0.04, ZC), "CabRoof"))   # hangt aan het plafond
    sc.add(mk.Part("Boiler", build_boiler(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("HeadLight", build_headlight(), (BOILER[1] - 0.06, LAMP_Y, ZC), "Locomotive"))
    sc.add(mk.Part("Wheels", build_wheels(), (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("Cylinders", build_cylinders(), (0, 0, 0), "Locomotive"))
    firebox, (fx0, fx1) = build_firebox()
    sc.add(mk.Part("Firebox", firebox, (0, 0, 0), "Locomotive"))
    sc.add(mk.Part("FireboxDoor", build_firebox_door(), (fx0 - 0.035, CAB_FLOOR + 0.32, ZC + DOOR_HINGE_U), "Firebox"))
    sc.add(mk.Part("Regulator", build_regulator(), (fx0 - LEVER_OUT, CAB_FLOOR + LEVER_V, ZC + LEVER_U), "Firebox"))
    sc.add(mk.Part("HeatNeedle", build_heat_needle(), (fx0, CAB_FLOOR + HEAT_UV[1], ZC + HEAT_UV[0]), "Firebox"))
    sc.add(mk.Part("GrabRails", build_grabrails(), (0, 0, 0), "Locomotive"))
    needle = rbox(-0.008, 0.008, 0, 0.075, -0.008, 0.008, 0.003, "M_Loco_Trim")
    needle = needle.transform(R=mk.rot_x(math.radians(-120)))
    sc.add(mk.Part("SpeedNeedle", needle, (fx0 - 0.08, CAB_FLOOR + 0.96, ZC - 0.20), "Firebox"))
    button = mk.cylinder(0.075, 0.04, 16, 0.012, mat="M_Loco_Red", axis="x")
    sc.add(mk.Part("StartButton", button, (fx0 - 0.05, CAB_FLOOR + BUTTON_UV[1], ZC + BUTTON_UV[0]), "Firebox"))
    latch = mk.cylinder(0.047, 0.12, 12, 0.018, mat="M_Loco_Interact", axis="x")
    sc.add(mk.Part("FireboxLatch", latch, (-0.074, 0, 0.686), "FireboxDoor"))
    shovel = mk.merge(
        mk.capsule_between((0, 0, 0), (0, 0, 0.51), 0.024, 0.019, 10, 2, mat="M_Loco_Gold"),
        rbox(-0.07, 0.07, -0.027, 0.027, -0.029, 0.029, 0.018, "M_Loco_Interact"),
        rbox(-0.044, 0.044, -0.035, 0.032, 0.43, 0.51, 0.014, "M_Loco_Gold"),
        rbox(-0.145, 0.145, -0.040, -0.008, 0.47, 0.77, 0.012, "M_Loco_Frame"),
        *(rbox(a, b, -0.024, 0.055, 0.475, 0.75, 0.009, "M_Loco_Trim")
          for a, b in ((-0.145, -0.122), (0.122, 0.145))),
        rbox(-0.145, 0.145, -0.024, 0.025, 0.748, 0.77, 0.009, "M_Loco_Trim"))
    sc.add(mk.Part("CoalShovel", shovel, (CAB[0] + 0.98, CAB_FLOOR + 0.74, ZC - 0.91), "Locomotive",
                   rotation=mk.rot_x(math.radians(83))))
    sc.add(mk.Part("ShovelLoad", None, (0, 0.006, 0.61), "CoalShovel"))
    # de schepkolen (CoalChunk*, CoalScoopPoint) liggen sinds cabine v3 op de kolenwagen ('CoalStock')
    sc.add(mk.Part("FireboxFeedPoint", None, (fx0 - 0.06, CAB_FLOOR + 0.33, ZC + DOOR_MID_U), "Firebox"))
    coals = mk.merge(*(mk.ellipsoid(0.07, 0.047, 0.085, 7, 4, mat="M_Loco_Coal").transform(
        t=(fx0 + x, CAB_FLOOR + 0.135 + y, ZC + DOOR_MID_U + z))
        for x, y, z in ((0.18, 0, -0.17), (0.19, .014, 0), (0.18, .008, .17), (.31, .016, -.11), (.32, .009, .07), (.32, 0, .21), (.43, .022, 0))))
    sc.add(mk.Part("FireboxCoals", coals, (0, 0, 0), "Firebox"))
    flame = mk.merge(
        mk.lathe([(0, 0), (0.082, 0.021), (0.072, 0.091), (0.045, 0.16), (0, 0.285)], 8, mat="M_Loco_Fire"),
        *(mk.lathe([(0, 0), (0.064, 0.026), (0.048, 0.085), (0, height)], 7, mat="M_Loco_Fire")
          .transform(R=mk.rot_x(tilt), t=(x, 0, z))
          for x, z, height, tilt in ((0.02, -0.11, 0.21, -0.22), (0.055, 0.115, 0.235, 0.27), (-0.095, 0.018, 0.16, -0.18))))
    sc.add(mk.Part("FireboxFlame", flame, (fx0 + 0.28, CAB_FLOOR + 0.16, ZC + DOOR_MID_U), "Firebox"))
    sc.add(mk.Part("ChimneySmoke", None, (BOILER[1] - 0.85, BOILER_TOP + 0.89, ZC), "Locomotive"))
    add_couplers(sc, "Locomotive", RAIL_Y, ZC, FRAME[0] - 0.10, FRAME[1] + 0.10, "M_Loco_Trim", "M_Loco_Interact")
    for p in sc.parts:
        p.gradient = 0.30
        if p.name in ("Cab", "CabRoof", "CabLamp", "Boiler", "Frame", "HeadLight", "Cylinders", "Firebox", "FireboxDoor",
                      "Regulator", "HeatNeedle"):
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
