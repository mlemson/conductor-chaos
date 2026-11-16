"""Emplacement: watertoren, tappunt met slang, spuitmond en draaikraan (issues #232, #233, #235).

Alles staat op maaiveld (y = 0), pivot midden-onder, +X wijst naar het spoor.

  Yard_WaterTower   houten ton met ijzeren banden op een vakwerkonderstel, kegeldak, ladder en bordes.
                    Zet het midden 4,0 m uit het hart van het spoor. Bediening op een gietijzeren standaard aan de
                    spoorkant: hendel voor de vulpijp en een rood handwiel voor het water.
  Yard_HoseTap      standpijp met kwartslagkraantje, koppeling en een haspel met opgerolde rode slang.
  Prop_HoseNozzle   (map Props) losse spuitmond met trekker: een gewone netwerkprop die je met je handen pakt.
  Yard_HoseSegment  stuk slang van 0,5 m langs +X (pivot aan het begin), voor een slang die in het spel wordt
                    gelegd tussen Point_HoseOut van de kraan en Point_HoseIn van de spuitmond.
  Yard_Crane        handbediende draaikraan: stenen voet met gietijzeren zuil, draaiend bovendeel met machinehuisje,
                    contragewicht, ladder en een gele vakwerkgiek; haak aan een kabel. Bereik 6,6 m vanaf het midden:
                    zet hem 5,5 m uit het hart van het spoor.

Knoopconventie voor latere gameplay (de prefabbouwer zet alles onder Move_* niet statisch):
  Move_<Deel>         beweegt; draait om zijn eigen pivot of schuift langs zijn eigen as (zie hieronder)
  Pose_<Deel>_<Stand> lege knoop met de positie/rotatie van Move_<Deel> in die stand (zelfde parent). Het model
                      staat in de eerste stand die hier genoemd wordt.
  Point_<Naam>        leeg anker: waar water uitkomt, waar de slang vast zit, waar de haak hangt, ...
  Col_<Naam>          verborgen botsvorm (onder Move_* beweegt hij mee)

  Watertoren  Move_Spout (vulpijp, om Z)          Pose_Spout_Up / _Down          Point_WaterOut (onder de sok)
              Move_Lever_Spout (hendel, om Z)     Pose_Lever_Spout_Up / _Down
              Move_Valve (handwiel, om Z)         Pose_Valve_Closed / _Open
              Move_Gauge (peilwijzer, langs Y)    Pose_Gauge_Full / _Empty
  Tappunt     Move_Valve (kraantje, om Y)         Pose_Valve_Closed / _Open      Point_HoseOut (koppeling)
              Move_Reel (haspel, om Z)            (draait vrij)
  Spuitmond   Move_Trigger (trekker, om Z)        Pose_Trigger_Released / _Pressed
              Point_WaterOut, Point_HoseIn
  Kraan       Move_Slew (bovendeel, om Y)         (draait vrij)                  Point_Operator (staanplek)
              Move_Lever_Slew / Move_Lever_Hoist  Pose_Lever_*_Neutral / _Forward / _Back (om Z, veert terug)
              Move_Hook (haak, langs Y)           Pose_Hook_Top / _Bottom        Point_HookTip
              Cable (kabel van katrol naar haak, 3,0 m lang: schaal Y = afstand / 3,0)
"""
import math

import numpy as np

from station_common import DEG, ball, capsule, col_box, cyl, disc, mk, rbox, tube

COL = "M_Stn_Collider"


def _poses(name, parent, pivot, states):
    """Lege Pose_-knopen: states = [(stand, rotatie 3x3 of None, positie of None)]."""
    return [(f"Pose_{name}_{st}", None, parent, tuple(pos if pos is not None else pivot), rot, True)
            for (st, rot, pos) in states]


def _band(r_in, r_out, y0, y1, mat, seg=20, phase=0.0):
    return mk.lathe([(r_in, y0), (r_out, y0), (r_out, y1), (r_in, y1)], seg, mat=mat, closed=True, phase=phase)


def _ladder(x, z, y0, y1, width=0.44, normal="z", step=0.30, mat="M_Stn_Yellow"):
    """Rechte ladder: twee bomen en sporten; de ladder staat in het vlak loodrecht op `normal`."""
    out = []
    for dx in (-width / 2, width / 2):
        a = (x + dx, y0, z) if normal == "z" else (x, y0, z + dx)
        b = (x + dx, y1, z) if normal == "z" else (x, y1, z + dx)
        out.append(capsule(a, b, 0.032, seg=8, rings=1, mat=mat))
    y = y0 + 0.30
    while y < y1 - 0.12:
        a = (x - width / 2, y, z) if normal == "z" else (x, y, z - width / 2)
        b = (x + width / 2, y, z) if normal == "z" else (x, y, z + width / 2)
        out.append(tube([a, b], 0.022, mat, seg=6, caps=False))
        y += step
    return out


def _lever(length, knob_mat, rod_mat="M_Stn_Yellow"):
    """Hendel langs +Y vanaf de pivot (as van de hendel = Z): voetstuk, stang en bolle knop."""
    return mk.merge(cyl(0.05, 0.10, 12, 0.01, "M_Stn_Dark", axis="z"),
                    capsule((0, 0.0, 0), (0, length, 0), 0.024, seg=8, rings=1, mat=rod_mat),
                    ball(0.055, knob_mat, 12, 7).transform(t=(0, length + 0.03, 0)))


# =================================================================== watertoren (#232)
LEG_FOOT, LEG_HEAD = 1.15, 0.98          # halve maat van het onderstel bij de grond en onder het bordes
DECK_Y0, DECK_Y1, DECK_HALF = 4.25, 4.45, 1.65
TANK_Y0, TANK_Y1 = 4.40, 6.50            # de ton begint in het bordes (geen gedeeld vlak)
TANK_SEG, TANK_PHASE = 20, math.pi / 20
SPOUT_PIVOT = (1.66, 4.85, 0.0)
STAND = (1.55, -0.62)                    # bedieningsstandaard (x, z)


def _tank_r(y):
    t = (y - TANK_Y0) / (TANK_Y1 - TANK_Y0)
    return 1.45 + 0.05 * math.sin(math.pi * t) - 0.07 * t


def _leg_at(y):
    t = (y - 0.30) / (DECK_Y0 + 0.02 - 0.30)
    return LEG_FOOT + (LEG_HEAD - LEG_FOOT) * t


def _tower_frame():
    parts = []
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(rbox(sx * LEG_FOOT - 0.27, sx * LEG_FOOT + 0.27, 0.0, 0.34, sz * LEG_FOOT - 0.27,
                              sz * LEG_FOOT + 0.27, 0.05, "M_Stn_Stone"))
            parts.append(capsule((sx * LEG_FOOT, 0.30, sz * LEG_FOOT), (sx * LEG_HEAD, DECK_Y0 + 0.02, sz * LEG_HEAD),
                                 0.12, seg=6, rings=1, mat="M_Stn_Timber"))
    # gordingen rondom (langs X iets lager dan langs Z: ze raken elkaar niet in hetzelfde vlak)
    for (y0, y1) in ((2.06, 2.22), (3.86, 4.02)):
        for s in (-1, 1):
            a = _leg_at((y0 + y1) / 2)
            parts.append(rbox(-a + 0.13, a - 0.13, y0, y1, s * a - 0.07, s * a + 0.07, 0.025, "M_Stn_TimberDark"))
            parts.append(rbox(s * a - 0.07, s * a + 0.07, y0 + 0.06, y1 + 0.06, -a + 0.13, a - 0.13, 0.025,
                              "M_Stn_TimberDark"))
    # andreaskruisen in elk vak, iets naar buiten zodat ze over de gordingen lopen
    for (ya, yb) in ((0.45, 2.10), (2.22, 3.88)):
        aa, ab = _leg_at(ya) + 0.05, _leg_at(yb) + 0.05
        for s in (-1, 1):
            for d in (-1, 1):
                parts.append(capsule((-d * aa, ya, s * aa), (d * ab, yb, s * ab), 0.055, seg=6, rings=1,
                                     mat="M_Stn_Timber"))
                parts.append(capsule((s * aa, ya, -d * aa), (s * ab, yb, d * ab), 0.055, seg=6, rings=1,
                                     mat="M_Stn_Timber"))
    # bordes met leuning (opening bij de ladder aan -Z)
    parts.append(rbox(-DECK_HALF, DECK_HALF, DECK_Y0, DECK_Y1, -DECK_HALF, DECK_HALF, 0.04, "M_Stn_Floor"))
    parts.append(rbox(-DECK_HALF - 0.04, DECK_HALF + 0.04, DECK_Y0 - 0.12, DECK_Y0 + 0.04, -DECK_HALF - 0.04,
                      DECK_HALF + 0.04, 0.03, "M_Stn_TimberDark"))
    h = DECK_HALF - 0.07
    corners = [(-h, -h), (h, -h), (h, h), (-h, h)]
    for (x, z) in corners + [(0.0, h), (h, 0.0), (-h, 0.0), (0.42, -h), (-0.42, -h)]:
        parts.append(capsule((x, DECK_Y1 - 0.02, z), (x, DECK_Y1 + 1.0, z), 0.035, seg=6, rings=1, mat="M_Stn_Navy"))
    rail = [(-0.42, -h), (-h, -h), (-h, h), (h, h), (h, -h), (0.42, -h)]
    for y in (DECK_Y1 + 0.55, DECK_Y1 + 1.0):
        parts.append(tube([(x, y - 0.002 * (k % 2), z) for k, (x, z) in enumerate(rail)], 0.03, "M_Stn_Navy", seg=6))
    # ladder van de grond naar het bordes (-Z), met beugels naar de gording en het bordes
    zl = -DECK_HALF - 0.15
    parts += _ladder(0.0, zl, 0.0, DECK_Y1 + 1.0)
    for x in (-0.22, 0.22):
        parts.append(rbox(x - 0.03, x + 0.03, DECK_Y0 + 0.02, DECK_Y0 + 0.10, zl - 0.02, -DECK_HALF + 0.10, 0.01,
                          "M_Stn_Dark"))
        a = _leg_at(2.14)
        parts.append(rbox(x - 0.03, x + 0.03, 2.10, 2.17, zl - 0.02, -a + 0.05, 0.01, "M_Stn_Dark"))
    return parts


def _tower_tank():
    ys = np.linspace(TANK_Y0, TANK_Y1, 9)
    prof = [(0.0, TANK_Y0)] + [(_tank_r(y), y) for y in ys] + [(0.0, TANK_Y1)]
    parts = [mk.lathe(prof, TANK_SEG, mat="M_Stn_Timber", closed=True, phase=TANK_PHASE)]
    for t in (0.10, 0.38, 0.66, 0.90):
        y = TANK_Y0 + t * (TANK_Y1 - TANK_Y0)
        r = _tank_r(y)
        parts.append(_band(r - 0.03, r + 0.035, y - 0.05, y + 0.05, "M_Stn_Dark", TANK_SEG, TANK_PHASE))
    # kegeldak met overstek en een gele bol
    parts.append(mk.lathe([(0.0, 6.42), (1.58, 6.42), (1.84, 6.34), (1.86, 6.44), (0.12, 7.56), (0.0, 7.58)],
                          16, mat="M_Stn_Roof", closed=True, phase=math.pi / 16))
    parts.append(cyl(0.05, 0.22, 8, 0.0, "M_Stn_Dark").transform(t=(0, 7.62, 0)))
    parts.append(ball(0.13, "M_Stn_Yellow", 14, 8).transform(t=(0, 7.80, 0)))
    # aansluiting van de vulpijp: huis op de ton aan de spoorkant
    px, py, _ = SPOUT_PIVOT
    parts.append(rbox(1.30, px - 0.02, py - 0.20, py + 0.20, -0.22, 0.22, 0.05, "M_Stn_Dark"))
    return parts


def _spout():
    """Vulpijp langs +X vanaf het scharnier (as Z), met een omlaag gebogen eind en een canvas sok."""
    m = [cyl(0.15, 0.56, 14, 0.02, "M_Stn_Dark", axis="z"),
         tube([(0.02, 0.0, 0.0), (2.30, 0.0, 0.0)], 0.14, "M_Stn_Navy", seg=12),
         tube([(2.25, 0.0, 0.0), (2.50, -0.04, 0.0), (2.64, -0.18, 0.0), (2.68, -0.36, 0.0)], 0.13, "M_Stn_Navy",
              seg=12, smooth=True)]
    for x in (0.35, 1.30, 2.15):
        m.append(cyl(0.165, 0.08, 14, 0.015, "M_Stn_Yellow", axis="x").transform(t=(x, 0, 0)))
    sock = mk.lathe([(0.15, 0.0), (0.16, -0.12), (0.14, -0.32), (0.11, -0.46), (0.115, -0.50)], 12, mat="M_Stn_Sack")
    m.append(sock.transform(t=(2.68, -0.30, 0.0)))
    col = mk.merge(col_box(-0.15, 2.45, -0.16, 0.16, -0.28, 0.28), col_box(2.45, 2.85, -0.82, 0.10, -0.17, 0.17))
    return mk.merge(*m), col


def _stand():
    """Gietijzeren bedieningsstandaard aan de spoorkant met een stang naar het bordes."""
    x, z = STAND
    parts = [rbox(x - 0.24, x + 0.24, 0.0, 0.10, z - 0.30, z + 0.30, 0.03, "M_Stn_Stone"),
             mk.lathe([(0.12, 0.08), (0.12, 0.16), (0.075, 0.24), (0.065, 0.86), (0.0, 0.86)], 12,
                      mat="M_Stn_Navy").transform(t=(x, 0, z)),
             rbox(x - 0.12, x + 0.12, 0.82, 1.06, z - 0.24, z + 0.24, 0.04, "M_Stn_Navy"),
             rbox(x - 0.10, x + 0.10, 1.05, 1.09, z - 0.22, z + 0.22, 0.015, "M_Stn_Yellow"),
             capsule((x - 0.02, 1.08, z), (1.05, DECK_Y0 - 0.06, z * 0.55), 0.022, seg=6, rings=1, mat="M_Stn_Dark")]
    col = col_box(x - 0.14, x + 0.14, 0.0, 1.10, z - 0.26, z + 0.26)
    return parts, col


def _gauge():
    """Peilschaal op de ton (schuin naar het spoor gericht) met een gele wijzer die langs Y schuift."""
    R = mk.rot_y(50 * DEG)
    zb = 1.53
    board = [rbox(-0.15, 0.15, 4.62, 6.22, zb, zb + 0.05, 0.02, "M_Stn_Cream")]
    for y in (4.75, 5.40, 6.05):
        board.append(rbox(-0.04, 0.04, y, y + 0.06, zb - 0.06, zb + 0.005, 0.01, "M_Stn_Dark"))
    for k in range(9):
        y = 4.78 + k * 0.165
        w = 0.10 if k % 4 == 0 else 0.06
        board.append(rbox(-0.13, -0.13 + w, y - 0.012, y + 0.012, zb + 0.05, zb + 0.062, 0.003, "M_Stn_Navy"))
    pointer = mk.prism([(0.0, 0.0), (0.12, -0.05), (0.12, 0.05)], 0.0, 0.025, c=0.006, mat="M_Stn_Yellow")
    full, empty = R @ np.array([-0.02, 6.10, zb + 0.065]), R @ np.array([-0.02, 4.78, zb + 0.065])
    return mk.merge(*board).transform(R=R), pointer, R, tuple(full), tuple(empty)


def water_tower_nodes():
    parts = _tower_frame() + _tower_tank()
    stand, stand_col = _stand()
    parts += stand
    board, pointer, R_g, full, empty = _gauge()
    parts.append(board)
    spout, spout_col = _spout()
    x, z = STAND
    lever_piv, valve_piv = (x, 0.94, z + 0.27), (x, 0.94, z - 0.27)
    wheel = mk.merge(mk.torus(0.17, 0.025, 16, 6, mat="M_Stn_Red").transform(R=mk.rot_x(math.pi / 2)),
                     *[capsule((0, 0, 0), (0.16 * math.cos(a), 0.16 * math.sin(a), 0), 0.016, seg=6, rings=1,
                               mat="M_Stn_Red") for a in (0.0, 2 * math.pi / 3, 4 * math.pi / 3)],
                     cyl(0.04, 0.12, 10, 0.01, "M_Stn_Dark", axis="z"),
                     ball(0.03, "M_Stn_Yellow", 8, 5).transform(t=(0.17, 0.0, -0.035)))
    wheel = wheel.transform(t=(0, 0, -0.07))
    lever = _lever(0.80, "M_Stn_Red").transform(t=(0, 0, 0.07))
    up, down = mk.rot_z(25 * DEG), mk.rot_z(-40 * DEG)
    tank_col = cyl(1.45, TANK_Y1 + 0.95 - TANK_Y0, 10, 0.0, COL).transform(t=(0, (TANK_Y0 + TANK_Y1 + 0.95) / 2, 0))
    main_col = mk.merge(tank_col, col_box(-DECK_HALF, DECK_HALF, DECK_Y0 - 0.12, DECK_Y1, -DECK_HALF, DECK_HALF),
                        *[col_box(sx * LEG_HEAD - 0.20, sx * LEG_HEAD + 0.20, 0.0, DECK_Y0, sz * LEG_HEAD - 0.20,
                                  sz * LEG_HEAD + 0.20) for sx in (-1, 1) for sz in (-1, 1)],
                        col_box(-0.30, 0.30, 0.0, DECK_Y1 + 1.0, -DECK_HALF - 0.22, -DECK_HALF - 0.08), stand_col)
    rail_col = mk.merge(col_box(-DECK_HALF, DECK_HALF, DECK_Y1, DECK_Y1 + 1.05, DECK_HALF - 0.12, DECK_HALF),
                        col_box(-DECK_HALF, -DECK_HALF + 0.12, DECK_Y1, DECK_Y1 + 1.05, -DECK_HALF, DECK_HALF),
                        col_box(DECK_HALF - 0.12, DECK_HALF, DECK_Y1, DECK_Y1 + 1.05, -DECK_HALF, DECK_HALF))
    nodes = [("Tower", mk.merge(*parts), None, (0, 0, 0), None, True),
             ("Col_Main", main_col, None, (0, 0, 0), None, False),
             ("Col_Railing", rail_col, None, (0, 0, 0), None, False),
             ("Move_Spout", spout, None, SPOUT_PIVOT, mk.rot_z(75 * DEG), True),
             ("Col_Spout", spout_col, "Move_Spout", (0, 0, 0), None, False),
             ("Point_WaterOut", None, "Move_Spout", (2.68, -0.82, 0.0), None, True),
             ("Move_Lever_Spout", lever, None, lever_piv, up, True),
             ("Col_Lever_Spout", col_box(-0.06, 0.06, 0.0, 0.90, 0.02, 0.13), "Move_Lever_Spout", (0, 0, 0), None, False),
             ("Move_Valve", wheel, None, valve_piv, None, True),
             ("Col_Valve", col_box(-0.20, 0.20, -0.20, 0.20, -0.15, 0.0), "Move_Valve", (0, 0, 0), None, False),
             ("Move_Gauge", pointer, None, full, R_g, True)]
    nodes += _poses("Spout", None, SPOUT_PIVOT, [("Up", mk.rot_z(75 * DEG), None), ("Down", mk.rot_z(-12 * DEG), None)])
    nodes += _poses("Lever_Spout", None, lever_piv, [("Up", up, None), ("Down", down, None)])
    nodes += _poses("Valve", None, valve_piv, [("Closed", None, None), ("Open", mk.rot_z(-120 * DEG), None)])
    nodes += _poses("Gauge", None, full, [("Full", R_g, full), ("Empty", R_g, empty)])
    return nodes


# =================================================================== tappunt + slang (#233)
TAP_Y = 0.78                 # hoogte van de uitloop
REEL = (0.95, 0.62, 0.0)     # as van de haspel (langs Z)


def hose_tap_nodes():
    parts = [rbox(-0.28, 0.28, 0.0, 0.12, -0.28, 0.28, 0.03, "M_Stn_Stone"),
             mk.lathe([(0.13, 0.10), (0.13, 0.20), (0.085, 0.27), (0.075, 0.95), (0.092, 1.00), (0.092, 1.06),
                       (0.06, 1.11), (0.0, 1.16)], 14, mat="M_Stn_Navy"),
             _band(0.07, 0.10, 0.30, 0.35, "M_Stn_Yellow", 14),
             _band(0.06, 0.088, 0.90, 0.94, "M_Stn_Yellow", 14),
             ball(0.05, "M_Stn_Yellow", 10, 6).transform(t=(0, 1.17, 0)),
             tube([(0.05, TAP_Y, 0.0), (0.30, TAP_Y, 0.0)], 0.04, "M_Stn_Navy", seg=10),
             ball(0.068, "M_Stn_Dark", 12, 7).transform(t=(0.20, TAP_Y, 0)),
             cyl(0.052, 0.07, 12, 0.012, "M_Stn_Yellow", axis="x").transform(t=(0.335, TAP_Y, 0))]
    # haspel: twee zijborden op pootjes, as met draaikoppeling aan +Z; de slang loopt van de koppeling naar de as
    rx, ry, _ = REEL
    for s in (-1, 1):
        plate = mk.prism([(-0.30, 0.0), (0.30, 0.0), (0.06, ry + 0.06), (-0.06, ry + 0.06)], -0.025, 0.025, c=0.012,
                         mat="M_Stn_Navy")
        parts.append(plate.transform(t=(rx, 0, s * 0.30)))
        parts.append(rbox(rx - 0.34, rx + 0.34, 0.0, 0.05, s * 0.30 - 0.06, s * 0.30 + 0.06, 0.015, "M_Stn_Dark"))
    parts.append(cyl(0.03, 0.76, 10, 0.0, "M_Stn_Dark", axis="z").transform(t=(rx, ry, 0.0)))
    parts.append(cyl(0.05, 0.08, 10, 0.01, "M_Stn_Yellow", axis="z").transform(t=(rx, ry, 0.40)))
    parts.append(tube([(0.37, TAP_Y, 0.0), (0.48, 0.70, 0.12), (0.62, 0.58, 0.40), (0.80, 0.60, 0.44), (rx, ry, 0.44)],
                      0.035, "M_Stn_Red", seg=8, smooth=True))
    # draaiende haspel: naaf, flenzen, opgerolde slang en een slinger aan -Z
    reel = [cyl(0.11, 0.44, 14, 0.01, "M_Stn_Dark", axis="z")]
    for s in (-1, 1):
        reel.append(disc(0.34, -0.015, 0.015, "M_Stn_Yellow", 18, 0.006).transform(t=(0, 0, s * 0.215)))
    for k in range(5):
        z = -0.16 + k * 0.08
        for (R_, sh) in ((0.16, 0.0), (0.235, 0.004)):
            reel.append(mk.torus(R_, 0.036 - sh, 14, 6, mat="M_Stn_Red").transform(R=mk.rot_x(math.pi / 2), t=(0, 0, z)))
    # slinger buiten het zijbord (de as steekt 0,39 uit het midden)
    reel.append(cyl(0.045, 0.04, 10, 0.01, "M_Stn_Yellow", axis="z").transform(t=(0, 0, -0.37)))
    reel.append(rbox(-0.035, 0.035, -0.30, 0.03, -0.415, -0.375, 0.01, "M_Stn_Yellow"))
    reel.append(capsule((0, -0.27, -0.41), (0, -0.27, -0.54), 0.03, seg=8, rings=1, mat="M_Stn_Red"))
    valve = mk.merge(cyl(0.018, 0.07, 8, 0.0, "M_Stn_Dark").transform(t=(0, 0.035, 0)),
                     rbox(-0.022, 0.022, 0.05, 0.085, -0.035, 0.30, 0.012, "M_Stn_Yellow"),
                     capsule((0, 0.068, 0.20), (0, 0.068, 0.32), 0.028, seg=8, rings=1, mat="M_Stn_Red"))
    vpiv = (0.20, TAP_Y + 0.065, 0.0)
    col = mk.merge(col_box(-0.28, 0.28, 0.0, 0.12, -0.28, 0.28), col_box(-0.10, 0.10, 0.12, 1.20, -0.10, 0.10),
                   col_box(0.05, 0.37, TAP_Y - 0.07, TAP_Y + 0.07, -0.07, 0.07),
                   col_box(rx - 0.34, rx + 0.34, 0.0, ry + 0.36, -0.33, 0.33))
    nodes = [("Tap", mk.merge(*parts), None, (0, 0, 0), None, True),
             ("Col_Main", col, None, (0, 0, 0), None, False),
             ("Point_HoseOut", None, None, (0.37, TAP_Y, 0.0), None, True),
             ("Move_Valve", valve, None, vpiv, None, True),
             ("Col_Valve", col_box(-0.04, 0.04, 0.0, 0.10, -0.04, 0.34), "Move_Valve", (0, 0, 0), None, False),
             ("Move_Reel", mk.merge(*reel), None, REEL, None, True),
             ("Point_ReelHose", None, "Move_Reel", (0.0, -0.27, 0.0), None, True)]
    nodes += _poses("Valve", None, vpiv, [("Closed", None, None), ("Open", mk.rot_y(90 * DEG), None)])
    return nodes


def hose_nozzle_nodes():
    """Spuitmond: messing loop langs +X, rode handgreep, trekker; ligt plat genoeg om op te pakken."""
    y = 0.16
    barrel = mk.lathe([(0.0, 0.0), (0.036, 0.0), (0.042, 0.03), (0.042, 0.15), (0.032, 0.20), (0.022, 0.25),
                       (0.027, 0.26), (0.027, 0.275), (0.0, 0.275)], 12, mat="M_Stn_Yellow")
    parts = [barrel.transform(R=mk.rot_z(-math.pi / 2), t=(-0.04, y, 0.0)),
             cyl(0.03, 0.07, 10, 0.01, "M_Stn_Dark", axis="x").transform(t=(-0.07, y, 0.0)),
             tube([(-0.105, y, 0.0), (-0.20, y - 0.02, 0.0), (-0.26, y - 0.09, 0.0)], 0.032, "M_Stn_Red", seg=8, smooth=True),
             capsule((-0.03, y - 0.02, 0.0), (-0.075, 0.035, 0.0), 0.03, seg=10, rings=2, mat="M_Stn_Red"),
             tube([(0.05, y - 0.04, 0.0), (0.06, 0.06, 0.0), (0.0, 0.03, 0.0), (-0.06, 0.035, 0.0)], 0.009,
                  "M_Stn_Dark", seg=6, smooth=True)]
    trigger = rbox(-0.012, 0.012, -0.085, 0.0, -0.014, 0.014, 0.006, "M_Stn_Yellow").transform(R=mk.rot_z(-12 * DEG))
    tpiv = (0.02, y - 0.04, 0.0)
    col = mk.merge(col_box(-0.30, 0.24, 0.0, y + 0.045, -0.045, 0.045))
    nodes = [("Nozzle", mk.merge(*parts), None, (0, 0, 0), None, True),
             ("Col_Main", col, None, (0, 0, 0), None, False),
             ("Move_Trigger", trigger, None, tpiv, None, True),
             ("Point_WaterOut", None, None, (0.24, y, 0.0), None, True),
             ("Point_HoseIn", None, None, (-0.26, y - 0.09, 0.0), None, True)]
    nodes += _poses("Trigger", None, tpiv, [("Released", None, None), ("Pressed", mk.rot_z(-22 * DEG), None)])
    return nodes


def hose_segment_nodes():
    return [("Hose", tube([(0.0, 0.0, 0.0), (0.5, 0.0, 0.0)], 0.035, "M_Stn_Red", seg=8), None, (0, 0, 0), None, True)]


# =================================================================== draaikraan (#235)
SLEW_Y = 2.25
JIB_FOOT = (0.55, 0.45)
JIB_TIP = (6.40, 6.25)
SHEAVE_R = 0.22
CABLE_L = 3.0


def _jib():
    (fx, fy), (tx, ty) = JIB_FOOT, JIB_TIP
    d = np.array([tx - fx, ty - fy])
    L = float(np.linalg.norm(d))
    u = d / L
    n = np.array([-u[1], u[0]])

    def p(t, up, side):
        base = np.array([fx, fy]) + u * L * t
        depth = 0.10 + 0.42 * math.sin(math.pi * t)
        w = 0.46 + (0.13 - 0.46) * t
        q = base + n * depth * up
        return (q[0], q[1], side * w)
    ts = np.linspace(0.0, 0.97, 13)
    parts = []
    for side in (-1, 1):
        parts.append(tube([p(t, 0, side) for t in ts], 0.06, "M_Stn_Yellow", seg=8))
        parts.append(tube([p(t, 1, side) for t in ts], 0.05, "M_Stn_Yellow", seg=8))
        for i in range(1, len(ts) - 1):                                  # zigzag tussen onder- en bovenrand
            a, b = (ts[i - 1], 0) if i % 2 else (ts[i - 1], 1), (ts[i], 1) if i % 2 else (ts[i], 0)
            parts.append(tube([p(a[0], a[1], side), p(b[0], b[1], side)], 0.026, "M_Stn_YellowDark", seg=6, caps=False))
    for i, t in enumerate(ts[1:-1]):                                     # dwarsverbanden boven en onder
        up = i % 2
        parts.append(tube([p(t, up, -1), p(t, up, 1)], 0.024, "M_Stn_YellowDark", seg=6, caps=False))
    # voetscharnier en kopkatrol
    parts.append(cyl(0.09, 1.10, 12, 0.01, "M_Stn_Dark", axis="z").transform(t=(fx, fy, 0)))
    parts.append(cyl(SHEAVE_R, 0.10, 18, 0.02, "M_Stn_Dark", axis="z").transform(t=(tx, ty, 0)))
    parts.append(cyl(0.05, 0.40, 8, 0.0, "M_Stn_Yellow", axis="z").transform(t=(tx, ty, 0)))
    for s in (-1, 1):
        plate = mk.prism([(-0.30, -0.20), (0.10, -0.20), (0.30, 0.10), (0.0, 0.30), (-0.30, 0.10)], -0.02, 0.02, c=0.008,
                         mat="M_Stn_Yellow")
        parts.append(plate.transform(t=(tx, ty, s * 0.155)))
    top = p(0.97, 1, 0)
    col = col_box(-0.14, L * 0.97 + 0.10, -0.12, 0.52, -0.46, 0.46).transform(R=mk.rot_z(math.atan2(u[1], u[0])),
                                                                               t=(fx, fy, 0))
    return parts, col, (top[0], top[1])


def _crane_top():
    """Bovendeel in lokale maat (y = 0 = onderkant draaischijf)."""
    parts = [mk.lathe([(0.0, 0.0), (1.45, 0.0), (1.50, 0.05), (1.50, 0.14), (0.0, 0.14)], 18, mat="M_Stn_Floor",
                      closed=True),
             _band(1.40, 1.53, 0.02, 0.10, "M_Stn_Navy", 18)]
    # machinehuisje achterop met raampje naar de giek, schuin dak en contragewicht
    parts.append(rbox(-1.45, -0.55, 0.12, 1.70, -0.72, 0.72, 0.05, "M_Stn_Plaster"))
    for (x, z) in ((-1.45, -0.72), (-1.45, 0.72), (-0.55, -0.72), (-0.55, 0.72)):
        parts.append(rbox(x - 0.07, x + 0.07, 0.12, 1.72, z - 0.07, z + 0.07, 0.02, "M_Stn_Timber"))
    parts.append(rbox(-0.57, -0.50, 0.85, 1.42, -0.42, 0.42, 0.02, "M_Stn_Timber"))
    parts.append(rbox(-0.52, -0.49, 0.92, 1.35, -0.35, 0.35, 0.01, "M_Stn_Window"))
    roof = rbox(-0.58, 0.58, -0.05, 0.05, -0.95, 0.95, 0.03, "M_Stn_Roof").transform(R=mk.rot_z(-14 * DEG))
    parts.append(roof.transform(t=(-1.0, 1.86, 0)))
    parts.append(rbox(-1.80, -1.47, 0.12, 1.25, -0.62, 0.62, 0.05, "M_Stn_StoneDark"))
    parts.append(rbox(-1.83, -1.44, 1.22, 1.32, -0.65, 0.65, 0.03, "M_Stn_Yellow"))
    # lier voor het huisje: twee wangen, trommel met kabel
    for s in (-1, 1):
        parts.append(rbox(-0.50, -0.20, 0.12, 0.95, s * 0.34 - 0.03, s * 0.34 + 0.03, 0.012, "M_Stn_Navy"))
    parts.append(cyl(0.16, 0.62, 16, 0.02, "M_Stn_Dark", axis="z").transform(t=(-0.35, 0.72, 0)))
    for k in range(5):
        parts.append(mk.torus(0.17, 0.022, 14, 5, mat="M_Stn_Metal").transform(R=mk.rot_x(math.pi / 2),
                                                                                  t=(-0.35, 0.72, -0.18 + k * 0.09)))
    # bok achter de giek met tuien naar de kop
    apex = (-0.30, 2.75, 0.0)
    for s in (-1, 1):
        parts.append(capsule((-0.62, 0.14, s * 0.62), apex, 0.06, seg=6, rings=1, mat="M_Stn_Yellow"))
        parts.append(rbox(-0.70, -0.54, 0.12, 0.20, s * 0.62 - 0.08, s * 0.62 + 0.08, 0.02, "M_Stn_Dark"))
    parts.append(capsule((-0.36, 2.75, -0.08), (-0.36, 2.75, 0.08), 0.09, seg=10, rings=1, mat="M_Stn_Dark"))
    # voetblokken van de giek
    for s in (-1, 1):
        parts.append(rbox(0.40, 0.70, 0.12, 0.55, s * 0.50 - 0.04, s * 0.50 + 0.04, 0.015, "M_Stn_Navy"))
    # bedieningspaneel met twee hendels, leuning aan de voorkant (open bij de ladder en het huisje)
    parts.append(rbox(-0.12, 0.12, 0.12, 0.92, -0.80, -0.36, 0.04, "M_Stn_Navy"))
    parts.append(rbox(-0.14, 0.14, 0.90, 0.95, -0.82, -0.34, 0.015, "M_Stn_Yellow"))
    angles = [a * DEG for a in range(-118, 72, 19)]
    for a in angles:
        parts.append(capsule((1.42 * math.cos(a), 0.12, 1.42 * math.sin(a)), (1.42 * math.cos(a), 1.05, 1.42 * math.sin(a)),
                             0.03, seg=6, rings=1, mat="M_Stn_Navy"))
    for y in (0.60, 1.05):
        parts.append(tube([(1.42 * math.cos(a), y, 1.42 * math.sin(a)) for a in angles], 0.028, "M_Stn_Navy", seg=6))
    # ladder van de grond naar de draaischijf (draait mee)
    parts += _ladder(-0.20, 1.60, -SLEW_Y + 0.05, 1.00, width=0.44, normal="z")
    for x in (-0.42, 0.02):
        parts.append(rbox(x - 0.03, x + 0.03, 0.035, 0.125, 1.40, 1.62, 0.01, "M_Stn_Dark"))
    return parts


def crane_nodes():
    base = [mk.lathe([(0.0, 0.0), (1.25, 0.0), (1.25, 0.45), (1.10, 0.60), (0.0, 0.60)], 8, mat="M_Stn_Stone",
                     closed=True, phase=math.pi / 8),
            mk.lathe([(0.62, 0.55), (0.62, 0.75), (0.45, 0.95), (0.38, 1.90), (0.48, 2.05), (0.48, 2.15), (0.0, 2.15)], 16,
                     mat="M_Stn_Navy"),
            _band(0.40, 0.56, 2.13, 2.23, "M_Stn_Yellow", 16)]
    top = _crane_top()
    jib, jib_col, guy_end = _jib()
    top += jib
    apex = (-0.36, 2.75)
    top.append(capsule((apex[0], apex[1], 0.0), (guy_end[0], guy_end[1], 0.0), 0.035, seg=6, rings=1, mat="M_Stn_Dark"))
    top.append(capsule((apex[0], apex[1], 0.0), (-1.62, 1.30, 0.0), 0.035, seg=6, rings=1, mat="M_Stn_Dark"))
    tx, ty = JIB_TIP
    cx = tx + SHEAVE_R
    cable = tube([(0.0, 0.0, 0.0), (0.0, -CABLE_L, 0.0)], 0.02, "M_Stn_Dark", seg=6)
    # haak: geel blok met katrolschijf, rode haak
    hook = [rbox(-0.17, 0.17, -0.44, 0.0, -0.13, 0.13, 0.05, "M_Stn_Yellow"),
            rbox(-0.185, 0.185, -0.30, -0.22, -0.145, 0.145, 0.02, "M_Stn_Dark"),
            disc(0.11, -0.15, 0.15, "M_Stn_Dark", 14, 0.02).transform(t=(0, -0.16, 0)),
            cyl(0.045, 0.14, 10, 0.0, "M_Stn_Dark").transform(t=(0, -0.50, 0))]
    arc = [(0.0, -0.56, 0.0), (0.0, -0.72, 0.0)]
    arc += [(0.15 + 0.15 * math.cos(a * DEG), -0.72 + 0.15 * math.sin(a * DEG), 0.0) for a in range(180, 391, 30)]
    hook.append(tube(arc, 0.045, "M_Stn_Red", seg=8, smooth=True))
    hook = mk.merge(*hook)
    slew_col = mk.merge(cyl(1.5, 0.14, 12, 0.0, COL).transform(t=(0, 0.07, 0)),
                        col_box(-1.83, -0.50, 0.12, 1.95, -0.75, 0.75), col_box(-0.12, 0.12, 0.12, 0.95, -0.82, -0.34),
                        col_box(-0.45, 0.05, -SLEW_Y, 1.0, 1.52, 1.68))
    rail_col = mk.merge(*[col_box(1.42 * math.cos(a) - 0.08, 1.42 * math.cos(a) + 0.08, 0.14, 1.08,
                                  1.42 * math.sin(a) - 0.08, 1.42 * math.sin(a) + 0.08) for a in
                          [a * DEG for a in range(-118, 72, 19)]])
    hook_top = (cx, ty - CABLE_L, 0.0)
    levers = []
    for name, z, knob in (("Slew", -0.70, "M_Stn_Navy"), ("Hoist", -0.46, "M_Stn_Red")):
        piv = (0.0, 0.95, z)
        levers += [(f"Move_Lever_{name}", _lever(0.55, knob), "Move_Slew", piv, None, True),
                   (f"Col_Lever_{name}", col_box(-0.06, 0.06, 0.0, 0.64, -0.06, 0.06), f"Move_Lever_{name}", (0, 0, 0), None,
                    False)]
        levers += _poses(f"Lever_{name}", "Move_Slew", piv, [("Neutral", None, None), ("Forward", mk.rot_z(-30 * DEG), None),
                                                              ("Back", mk.rot_z(30 * DEG), None)])
    nodes = [("Base", mk.merge(*base), None, (0, 0, 0), None, True),
             ("Col_Main", mk.merge(cyl(1.25, 0.60, 8, 0.0, COL).transform(t=(0, 0.30, 0)),
                                   cyl(0.50, 1.65, 10, 0.0, COL).transform(t=(0, 1.40, 0))), None, (0, 0, 0), None, False),
             ("Move_Slew", mk.merge(*top), None, (0, SLEW_Y, 0), None, True),
             ("Col_Slew", slew_col, "Move_Slew", (0, 0, 0), None, False),
             ("Col_Railing", rail_col, "Move_Slew", (0, 0, 0), None, False),
             ("Col_Jib", jib_col, "Move_Slew", (0, 0, 0), None, False),
             ("Point_Operator", None, "Move_Slew", (0.35, 0.14, -0.55), None, True),
             ("Cable", cable, "Move_Slew", (cx, ty, 0.0), None, True),
             ("Move_Hook", hook, "Move_Slew", hook_top, None, True),
             ("Col_Hook", col_box(-0.18, 0.32, -0.92, 0.0, -0.14, 0.14), "Move_Hook", (0, 0, 0), None, False),
             ("Point_HookTip", None, "Move_Hook", (0.15, -0.86, 0.0), None, True)] + levers
    nodes += _poses("Hook", "Move_Slew", hook_top, [("Top", None, (cx, ty - 1.0, 0.0)),
                                                     ("Bottom", None, (cx, -SLEW_Y + 1.06, 0.0))])
    return nodes


ASSETS = {
    "Yard_WaterTower": (water_tower_nodes, 160, 0.30),
    "Yard_HoseTap": (hose_tap_nodes, 900, 0.10),
    "Yard_HoseSegment": (hose_segment_nodes, 900, 0.05),
    "Yard_Crane": (crane_nodes, 160, 0.30),
}
PROPS = {"Prop_HoseNozzle": (hose_nozzle_nodes, 2400, 0.05)}
