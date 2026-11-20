"""Gebouwmodules van Grand Whistleton: hoofdgevel, hal met tongewelf, vleugel (mansarde), paviljoen, klokkentoren.

Maatvoering in stationsruimte (zie gs_kit): spoor 1 op z = 0, perron 1 tot z = 5,15, voorplein (apron) tot z = 9.
  hal          x -12..12, z 9..31 (gevels 0,6 m dik), gewelf: aanzet FL+9, pijl 7 m (kruin FL+16)
  vleugels     x 12..24 (en gespiegeld), z 11..29: twee bouwlagen, mansardedak (lager dan de hal)
  paviljoens   x 24..33, z 10..30: springen naar voren, topgevels met oculus, pannendak (hoger dan de vleugels)
  toren        x 12,4..17,6, z 26,6..31,8: tussen hal en rechtervleugel aan de pleinkant, tot FL+30
"""
import math

import numpy as np

from gs_kit import (FL, Boxes, arch_band, arch_loop, ball, capsule, circle_loop, clock, cyl, disc, face_frame, frustum,
                    mk, opening_loop, quoins, rbox, seam_lines, slab, train_icon, tube, wall, window)
import gs_kit as kit

HX = 12.0
Z_RAIL, Z_SQ = 9.0, 31.0
HT = 0.6                                   # gevel- en halwanddikte
HS = FL + 9.0                              # aanzet gewelf
RISE = 7.0
VR = (HX ** 2 + RISE ** 2) / (2 * RISE)
VC = HS + RISE - VR
DOORS = (-5.6, 0.0, 5.6)                   # middens van de drie entreebogen
DW, DSPR = 3.6, FL + 3.3                   # breedte, aanzet boog
NAME_Y = (FL + 6.35, FL + 7.45)            # hoofdgestel met naambord
LUN_X, LUN_Y = 8.4, FL + 7.85              # boogvenster: halve breedte, onderkant
LUN_R = VR - 1.75

LINK = (12.0, 24.0, 11.0, 29.0)
L_EAVE, L_MID, L_TOP = FL + 7.6, FL + 10.0, FL + 10.7
PAV = (24.0, 33.0, 10.0, 30.0)
P_EAVE, P_PITCH = FL + 8.8, 40 * math.pi / 180
TOWER = (12.4, 17.6, 26.6, 31.8)
T_SHAFT, T_STAGE = FL + 18.0, FL + 22.6


def arc_y(x, r=None):
    r = VR if r is None else r
    return VC + math.sqrt(max(0.0, r * r - x * x))


def arc_pts(r, x_lim, n=40):
    a = math.asin(min(1.0, x_lim / r))
    return [(r * math.sin(t), VC + r * math.cos(t)) for t in np.linspace(-a, a, n)]


# ================================================================== hoofdgevel
def hall_facade():
    """Lokaal: x over de gevel, buitenkant z = 0 (naar +z), binnen tot z = -0,6. Pivot = midden onder."""
    parts, boxes = [], Boxes()
    # muur met de drie entrees als uitsparing en het boogvenster als gat
    outline = sc_outline()
    lun = lunette_loop()
    doors = [(x - DW / 2, x + DW / 2, DSPR) for x in DOORS]
    pts = kit.sc.wall_outline(-HX, HX, FL - 0.3, HS, doors, None, 16)[:-1]
    pts += [(x, y) for (x, y) in reversed(outline)]
    parts.append(slab(pts, [lun], -HT, 0.0, 0.04, "M_GS_Plaster"))
    # flankerende pijlers met kroonlijst en messing bol (het concept: ballen op de aanzet van de boog)
    for s in (-1, 1):
        xa, xb = sorted((s * (HX - 0.15), s * (HX + 1.45)))
        parts.append(rbox(xa, xb, FL - 0.3, HS + 1.3, -1.0, 1.15, 0.06, "M_GS_Sandstone"))
        for y in np.arange(FL + 0.9, HS + 0.6, 0.62):                    # voegen: liggende banden
            parts.append(rbox(xa - 0.03, xb + 0.03, y, y + 0.06, -0.95, 1.18, 0.02, "M_GS_SandstoneDark"))
        parts.append(rbox(xa - 0.22, xb + 0.22, HS + 1.3, HS + 1.62, -1.2, 1.37, 0.06, "M_GS_SandstoneDark"))
        parts.append(rbox(xa - 0.08, xb + 0.08, HS + 1.62, HS + 1.85, -1.06, 1.23, 0.05, "M_GS_Sandstone"))
        xm = (xa + xb) / 2
        parts.append(mk.lathe([(0.0, 0.0), (0.34, 0.0), (0.40, 0.10), (0.22, 0.26), (0.0, 0.30)], 14, mat="M_GS_Sandstone")
                     .transform(t=(xm, HS + 1.85, 0.08)))
        parts.append(ball(0.42, "M_GS_Brass", 18, 10).transform(t=(xm, HS + 2.55, 0.08)))
        boxes.add(xa, xb, 0.0, HS + 1.85, -1.0, 1.15)
    # grote boog: dikke zandstenen archivolt rond het gewelf, sluitsteen en bol op de kruin
    band = [(x, y) for x, y in arc_pts(VR + 0.95, HX + 0.15, 44)] + \
           [(x, y) for x, y in reversed(arc_pts(VR - 0.25, HX - 0.05, 44))]
    parts.append(slab(band, [], -0.2, 0.75, 0.06, "M_GS_Sandstone"))
    for k, t in enumerate(np.linspace(-0.62, 0.62, 13)):                  # blokverdeling op de archivolt
        if k in (0, 12):
            continue
        p0 = ((VR - 0.25) * math.sin(t), VC + (VR - 0.25) * math.cos(t))
        p1 = ((VR + 0.95) * math.sin(t), VC + (VR + 0.95) * math.cos(t))
        parts.append(capsule((p0[0], p0[1], 0.77), (p1[0], p1[1], 0.77), 0.035, mat="M_GS_SandstoneDark", seg=5, rings=1))
    top = VC + VR
    parts.append(slab([(-0.75, top + 1.25), (0.75, top + 1.25), (0.48, top - 0.45), (-0.48, top - 0.45)], [], -0.25, 0.95, 0.05,
                      "M_GS_SandstoneDark"))
    parts.append(mk.lathe([(0.0, 0.0), (0.42, 0.0), (0.48, 0.12), (0.25, 0.32), (0.0, 0.36)], 14, mat="M_GS_Sandstone")
                 .transform(t=(0.0, top + 1.25, 0.35)))
    parts.append(ball(0.62, "M_GS_Brass", 20, 12).transform(t=(0.0, top + 2.15, 0.35)))
    # binnenkader van het boogvenster (zandsteen, 0,35 m naar voren) + lekdorpel
    inner = lunette_loop()
    frame_out = [(x, y) for x, y in kit.sc._offset_loop(kit.sc._ccw(kit.sc.clean_loop(inner)), -0.45)]
    frame_in = kit.sc._offset_loop(kit.sc._ccw(kit.sc.clean_loop(inner)), 0.03)
    parts.append(slab(frame_out, [frame_in], -0.1, 0.38, 0.04, "M_GS_Sandstone"))
    parts.append(rbox(-LUN_X - 0.7, LUN_X + 0.7, LUN_Y - 0.36, LUN_Y - 0.02, -0.2, 0.6, 0.05, "M_GS_SandstoneDark"))
    parts.append(lunette_glazing())
    # hoofdgestel met het naambord (bord zelf is een aanpasbare Sign_-knoop in de module)
    parts.append(rbox(-HX + 0.1, HX - 0.1, NAME_Y[0], NAME_Y[1], -0.1, 0.42, 0.06, "M_GS_Sandstone"))
    parts.append(rbox(-HX + 0.02, HX - 0.02, NAME_Y[1], NAME_Y[1] + 0.22, -0.1, 0.55, 0.05, "M_GS_SandstoneDark"))
    parts.append(rbox(-HX + 0.05, HX - 0.05, NAME_Y[0] - 0.18, NAME_Y[0], -0.1, 0.50, 0.04, "M_GS_SandstoneDark"))
    # entreebogen: diepe zandstenen omlijsting, sluitsteen, waaierlicht met roedes in de boogkop
    for x in DOORS:
        x0, x1 = x - DW / 2, x + DW / 2
        parts.append(slab(arch_band(x0 + 0.03, x1 - 0.03, FL - 0.02, DSPR, 0.45, 16), [], -HT - 0.02, 0.32, 0.05, "M_GS_Sandstone"))
        parts.append(slab(arch_band(x0 - 0.42, x1 + 0.42, DSPR - 0.25, DSPR, 0.12, 16), [], 0.3, 0.42, 0.03, "M_GS_SandstoneDark"))
        r = DW / 2
        parts.append(slab([(x - 0.24, DSPR + r + 0.62), (x + 0.24, DSPR + r + 0.62), (x + 0.16, DSPR + r - 0.08),
                           (x - 0.16, DSPR + r - 0.08)], [], -0.1, 0.48, 0.04, "M_GS_Sandstone"))
        zg = -0.32
        parts.append(slab(arch_loop(x0 + 0.02, x1 - 0.02, DSPR - 0.001, DSPR, 16), [], zg - 0.012, zg + 0.012, 0.004, "M_GS_Glass"))
        parts.append(rbox(x0, x1, DSPR - 0.16, DSPR + 0.02, zg - 0.08, zg + 0.08, 0.02, "M_GS_Steel"))
        for a in np.linspace(20, 160, 5):
            ar = math.radians(a)
            parts.append(capsule((x + 0.25 * math.cos(ar), DSPR + 0.25 * math.sin(ar), zg),
                                 (x + (r - 0.06) * math.cos(ar), DSPR + (r - 0.06) * math.sin(ar), zg), 0.045, mat="M_GS_Steel",
                                 seg=5, rings=1))
        ring = [(x + 0.28 * math.cos(t), DSPR + 0.28 * math.sin(t), zg) for t in np.linspace(0, math.pi, 9)]
        parts.append(tube(ring, 0.05, "M_GS_Steel", seg=6))
        # lantaarn in de boog
        parts.append(capsule((x, DSPR + r - 0.05, -0.15), (x, DSPR + 0.95, -0.15), 0.02, mat="M_GS_Steel", seg=5, rings=1))
        parts.append(kit.sc.lantern(1.15, "M_GS_Steel", "M_GS_Lamp").transform(t=(x, DSPR + 0.30, -0.15)))
    # rustica: liggende voegen in de onderbouw, en pilasters tussen de entrees
    for y in np.arange(FL + 0.95, NAME_Y[0] - 0.62, 0.55):
        xs_ = [-HX] + [v for x in DOORS for v in (x - DW / 2 - 0.42, x + DW / 2 + 0.42)] + [HX]
        for a, b in zip(xs_[0::2], xs_[1::2]):
            parts.append(rbox(a + 0.02, b - 0.02, y, y + 0.05, -0.02, 0.035, 0.012, "M_GS_SandstoneDark"))
    for x in (-8.9, -2.8, 2.8, 8.9):
        parts.append(rbox(x - 0.36, x + 0.36, FL + 0.5, NAME_Y[0] - 0.18, -0.1, 0.30, 0.05, "M_GS_Sandstone"))
        parts.append(rbox(x - 0.48, x + 0.48, NAME_Y[0] - 0.45, NAME_Y[0] - 0.18, -0.1, 0.40, 0.04, "M_GS_SandstoneDark"))
    # plint over de hele gevel (niet in de doorgangen)
    edges = [-HX] + [v for x in DOORS for v in (x - DW / 2 - 0.42, x + DW / 2 + 0.42)] + [HX]
    for a, b in zip(edges[0::2], edges[1::2]):
        parts.append(rbox(a, b, FL - 0.3, FL + 0.55, -0.1, 0.12, 0.04, "M_GS_SandstoneDark"))
    for x in DOORS:                                           # drempel: vloer loopt op FL door de dagkant
        parts.append(rbox(x - DW / 2, x + DW / 2, FL - 0.12, FL - 0.006, -HT, 0.0, 0.01, "M_GS_SandstoneDark"))
    # botsing: muurdelen naast/tussen de entrees en alles boven de entrees
    for a, b in zip(edges[0::2], edges[1::2]):
        boxes.add(a, b, 0.0, DSPR + DW / 2 + 0.1, -HT, 0.12)
    boxes.add(-HX, HX, DSPR + DW / 2 + 0.1, VC + VR, -HT, 0.4)
    lights = [("Light_Arch_%d" % (k + 1), (x, DSPR + 0.2, 0.6)) for k, x in enumerate(DOORS)]
    return mk.merge(*parts), boxes, lights


def sc_outline():
    """Bovenrand van de gevel: rechte zijden tot de aanzet, dan de boog van het gewelf."""
    return [(x, y) for x, y in arc_pts(VR, HX, 44)]


def lunette_loop():
    a = math.asin(LUN_X / LUN_R)
    pts = [(-LUN_X, LUN_Y), (LUN_X, LUN_Y)]
    pts += [(LUN_R * math.sin(t), VC + LUN_R * math.cos(t)) for t in np.linspace(a, -a, 36)]
    return pts


def lunette_glazing():
    """Glas 0,4 m terug in de dagkant met dikke marine roedes: staanders, kalf, twee boogroedes, stralen."""
    zg = -0.40
    parts = [slab(lunette_loop(), [], zg - 0.015, zg + 0.015, 0.004, "M_GS_Glass")]
    top_at = lambda x: arc_y(x, LUN_R)                        # noqa: E731
    for x in (-5.6, -2.8, 0.0, 2.8, 5.6):
        parts.append(rbox(x - 0.09, x + 0.09, LUN_Y, top_at(x) - 0.02, zg - 0.09, zg + 0.09, 0.025, "M_GS_Steel"))
    for y in (LUN_Y + 2.2,):
        parts.append(rbox(-LUN_X, LUN_X, y - 0.09, y + 0.09, zg - 0.058, zg + 0.058, 0.025, "M_GS_Steel"))
    for rr in (LUN_R - 1.9, LUN_R - 3.9):
        lim = min(LUN_X, rr - 0.01)
        ys = [p for p in arc_pts(rr, lim, 30) if p[1] > LUN_Y + 2.2]
        if len(ys) > 2:
            parts.append(tube([(x, y, zg) for x, y in ys], 0.08, "M_GS_Steel", seg=6))
    parts.append(rbox(-LUN_X, LUN_X, LUN_Y - 0.06, LUN_Y + 0.12, zg - 0.12, zg + 0.12, 0.03, "M_GS_Steel"))
    hub_y = LUN_Y + 2.2
    for a in (28, 52, 76, 104, 128, 152):                               # waaier van stralen vanaf het kalf
        t = math.radians(a)
        d = np.array([math.cos(t), math.sin(t)])
        lo, hi = 0.0, 20.0
        for _ in range(40):                                              # snijpunt met de boog
            mid = (lo + hi) / 2
            px, py = d * mid + (0.0, hub_y)
            lo, hi = (mid, hi) if (px * px + (py - VC) ** 2 < LUN_R ** 2 and abs(px) < LUN_X) else (lo, mid)
        p1 = d * (lo - 0.05) + (0.0, hub_y)
        parts.append(capsule((d[0] * 1.3, hub_y + d[1] * 1.3, zg), (p1[0], p1[1], zg), 0.07, mat="M_GS_Steel", seg=6, rings=1))
    hub = [(1.2 * math.cos(t), hub_y + 1.2 * math.sin(t), zg) for t in np.linspace(0, math.pi, 13)]
    parts.append(tube(hub, 0.12, "M_GS_Steel", seg=6))
    return mk.merge(*parts)


# ================================================================== hal: wanden, vloer, gewelf
SIDE_OPEN = {  # openingen in de halzijwanden (z in stationsruimte): (z0, z1, aanzet) — breed, voor spelers met lading
    -1: [(14.6, 21.4, FL + 3.4)],          # links: grote boog naar de crew-briefing
    1: [(12.0, 14.4, FL + 2.4)],           # rechts: deur naar het kantoor van de stationschef
}


def hall_shell():
    """In stationsruimte; export met pivot (0, 0, 20)."""
    parts, boxes, lights = [], Boxes(), []
    z0, z1 = Z_RAIL, Z_SQ
    zi0, zi1 = Z_RAIL + HT, Z_SQ - HT
    # zijwanden (baksteen buiten, pleister binnen via een binnenschil) met openingen naar de vleugels
    for s in (-1, 1):
        ops = SIDE_OPEN[s]
        pts = kit.sc.wall_outline(zi0, zi1, FL - 0.3, HS, [(a, b, y) for a, b, y in ops], None, 14)
        parts.append(_on_side(s, slab(pts, [], 0.0, HT, 0.03, "M_GS_Brick"), HX))
        for a, b, y in ops:                                   # zandstenen omlijsting van de opening (binnenkant)
            parts.append(_on_side(s, slab(arch_band(a + 0.03, b - 0.03, FL - 0.02, y, 0.35, 14), [], 0.0, 0.18, 0.03, "M_GS_Sandstone"),
                                  HX - HT))
        for a, b, _ in ops:
            parts.append(rbox(*sorted((s * HX, s * (HX - HT))), FL - 0.12, FL - 0.006, a, b, 0.01,
                              "M_GS_SandstoneDark"))
        cuts = [zi0] + [v for a, b, _ in ops for v in (a, b)] + [zi1]
        for a, b in zip(cuts[0::2], cuts[1::2]):
            boxes.add(s * HX, s * (HX - HT), FL, HS, a, b)
        for a, b, y in ops:
            boxes.add(s * HX, s * (HX - HT), y + (b - a) / 2, HS, a, b)
    # binnen: zandstenen pilasters op de spantposities, lambrisering, kroonlijst op de aanzet
    for z in RIBS:
        for s in (-1, 1):
            if any(a - 0.4 < z < b + 0.4 for a, b, _ in SIDE_OPEN[s]):
                continue
            x = s * (HX - HT)
            xa, xb = sorted((x, x - s * 0.35))
            parts.append(rbox(xa, xb, FL, HS - 0.3, z - 0.32, z + 0.32, 0.05, "M_GS_Sandstone"))
            parts.append(rbox(*sorted((x, x - s * 0.55)), HS - 0.55, HS - 0.25, z - 0.45, z + 0.45, 0.05, "M_GS_SandstoneDark"))
    for s in (-1, 1):
        x = s * (HX - HT)
        cuts = [zi0] + [v for a, b, _ in SIDE_OPEN[s] for v in (a - 0.35, b + 0.35)] + [zi1]
        for a, b in zip(cuts[0::2], cuts[1::2]):
            parts.append(rbox(*sorted((x, x - s * 0.06)), FL + 0.006, FL + 1.1, a, b, 0.02, "M_GS_WoodDark"))
        parts.append(rbox(*sorted((x, x - s * 0.40)), HS - 0.25, HS + 0.05, zi0, zi1, 0.05, "M_GS_SandstoneDark"))
    # vloer: grote tegels zandsteen / warm grijs in een ruitpatroon, met een rand
    S = 1.6
    xs = np.arange(-HX + HT, HX - HT - 1e-6, S)
    zs = np.arange(zi0, zi1 - 1e-6, S)
    for i, x in enumerate(xs):
        for j, z in enumerate(zs):
            mat = ("M_GS_TilesLight", "M_GS_Sandstone")[(i + j) % 2]
            parts.append(rbox(x + 0.006, min(x + S, HX - HT) - 0.006, FL - 0.08, FL, z + 0.006, min(z + S, zi1) - 0.006,
                              0.01, mat))
    parts.append(rbox(-HX + 0.1, HX - 0.1, 0.0, FL - 0.08, z0 + 0.1, z1 - 0.1, 0.0, "M_GS_SandstoneDark"))
    boxes.add(-HX, HX, 0.0, FL, z0, z1)
    # gewelf
    v, vb = vault(z0 - 0.7, z1 + 0.7)
    parts.append(v)
    boxes.extend(vb)
    # hanglampen aan de spanten
    for z in RIBS[1:-1]:
        for x in (-5.2, 5.2):
            parts.append(globe_lamp(HS + 2.2 - (FL + 6.4)).transform(t=(x, HS + 2.2, z)))
            lights.append((x, FL + 6.0, z))
    return mk.merge(*parts), boxes, lights


def _on_side(s, m, x_face):
    """Plaat in (u = z, y), dikte e langs de wandnormaal naar binnen, op x = s * x_face."""
    R = np.array([[0, 0, -1], [0, 1, 0], [1, 0, 0]], float)      # (u, y, e) -> (x = -e, y, z = u), det = +1
    m = m.transform(R=R, t=(x_face, 0, 0))
    return m if s > 0 else m.transform(s=(-1, 1, 1))


RIBS = tuple(np.linspace(Z_RAIL + 1.2, Z_SQ - 1.2, 8))


def vault(z0, z1):
    """Leisteenblauw metalen tongewelf met een brede glazen lichtstraat in de kruin, felsnaden, spanten binnen."""
    parts, boxes = [], Boxes()
    a_edge = math.asin(min(1.0, (HX + 0.45) / VR))
    a_glass = math.asin(4.6 / VR)

    def band(a0, a1, r0, r1, n):
        out = [(r1 * math.sin(t), VC + r1 * math.cos(t)) for t in np.linspace(a0, a1, n)]
        inn = [(r0 * math.sin(t), VC + r0 * math.cos(t)) for t in np.linspace(a1, a0, n)]
        return out + inn
    for sg in (-1, 1):
        a0, a1 = sorted((sg * a_glass, sg * a_edge))
        parts.append(slab(band(a0, a1, VR - 0.08, VR + 0.22, 12), [], z0, z1, 0.05, "M_GS_Roof"))
        for t in np.linspace(a0 + 0.04, a1 - 0.02, 9):                    # felsnaden langs het gewelf
            p = ((VR + 0.25) * math.sin(t), VC + (VR + 0.25) * math.cos(t))
            parts.append(capsule((p[0], p[1], z0 + 0.05), (p[0], p[1], z1 - 0.05), 0.045, mat="M_GS_RoofDark", seg=5, rings=1))
        # dakgoot
        ge = ((VR + 0.25) * math.sin(sg * a_edge), VC + (VR + 0.25) * math.cos(sg * a_edge))
        parts.append(rbox(ge[0] - 0.22, ge[0] + 0.22, ge[1] - 0.30, ge[1] + 0.02, z0, z1, 0.06, "M_GS_SteelLight"))
    ag = a_glass + 0.015
    parts.append(slab(band(-ag, ag, VR + 0.04, VR + 0.08, 14), [], z0 + 0.3, z1 - 0.3, 0.01, "M_GS_Glass"))
    for z in np.arange(z0 + 1.2, z1 - 0.5, 1.6):                          # roedes over de lichtstraat
        pts = [((VR + 0.12) * math.sin(t), VC + (VR + 0.12) * math.cos(t), z) for t in np.linspace(-a_glass, a_glass, 9)]
        parts.append(tube(pts, 0.05, "M_GS_Steel", seg=5))
    for x in (-4.6, 4.6):
        y = arc_y(x, VR + 0.10)
        parts.append(rbox(x - 0.14, x + 0.14, y - 0.06, y + 0.22, z0 + 0.1, z1 - 0.1, 0.04, "M_GS_RoofDark"))
    # spanten: vakwerkboog (twee koorden + stijlen) op elke pilaster
    a_rib = math.asin((HX - HT - 0.2) / VR)
    for z in RIBS:
        for rr, rad in ((VR - 0.22, 0.10), (VR - 1.05, 0.08)):
            pts = [(rr * math.sin(t), VC + rr * math.cos(t), z) for t in np.linspace(-a_rib, a_rib, 28)]
            parts.append(tube(pts, rad, "M_GS_Steel", seg=7))
        for t in np.linspace(-a_rib, a_rib, 15)[1:-1]:
            p0 = ((VR - 0.22) * math.sin(t), VC + (VR - 0.22) * math.cos(t))
            p1 = ((VR - 1.05) * math.sin(t), VC + (VR - 1.05) * math.cos(t))
            parts.append(capsule((p0[0], p0[1], z), (p1[0], p1[1], z), 0.05, mat="M_GS_Steel", seg=5, rings=1))
        for s in (-1, 1):                                                # voeten met messing ring
            x = s * (HX - HT - 0.25)
            parts.append(mk.torus(0.17, 0.05, 12, 5, mat="M_GS_Brass").transform(t=(x, HS - 0.12, z)))
    boxes.add(-HX - 0.4, HX + 0.4, HS, HS + RISE * 0.75, z0, z1)
    return mk.merge(*parts), boxes


def globe_lamp(drop):
    """Hanglamp: stang, marine kap met messing ring en een grote gloeiende bol (concept). Pivot = ophangpunt."""
    parts = [capsule((0, 0, 0), (0, -drop + 0.55, 0), 0.03, mat="M_GS_Steel", seg=5, rings=1),
             mk.lathe([(0.0, 0.0), (0.30, 0.0), (0.34, -0.06), (0.16, -0.22), (0.0, -0.24)], 14, mat="M_GS_Steel")
             .transform(t=(0, -drop + 0.55, 0)),
             mk.torus(0.31, 0.03, 16, 5, mat="M_GS_Brass").transform(t=(0, -drop + 0.52, 0)),
             ball(0.34, "M_GS_Lamp", 18, 10).transform(t=(0, -drop + 0.15, 0))]
    return mk.merge(*parts)


# ================================================================== vleugel (mansarde, symmetrisch)
def link_wing():
    """Lokaal: x -6..6, z -9..9 (vleugel 12 x 18), pivot midden onder. Twee bouwlagen, mansardedak met dakkapellen.
    Kopse kanten sluiten aan op hal en paviljoen (geen eigen kopgevels). Begane grond is begaanbaar."""
    parts, boxes, lights = [], Boxes(), []
    L, D = 12.0, 18.0
    t = 0.5
    bays = (-4.5, -1.5, 1.5, 4.5)
    for side, axis in ((1, "+z"), (-1, "-z")):
        R = kit.face_frame(axis)
        zf = side * D / 2
        gw = [dict(kind="arch", x0=x - 0.75 + L / 2, x1=x + 0.75 + L / 2, y0=FL + 1.0, ys=FL + 2.75) for x in bays]
        uw = [dict(kind="seg", x0=x - 0.6 + L / 2, x1=x + 0.6 + L / 2, y0=FL + 5.0, ys=FL + 6.55, rise=0.25) for x in bays]
        w = wall(L, L_EAVE, gw + uw, t=t, mat="M_GS_Brick")
        det = [window(o, t=t, glass_depth=0.30, glow=0.9 if o["y0"] > FL + 4 else None) for o in gw + uw]
        # verdiepingsband, kroonlijst met tandlijst, plint
        det.append(rbox(-0.1, L + 0.1, FL + 4.15, FL + 4.45, -0.05, 0.18, 0.04, "M_GS_Sandstone"))
        det.append(rbox(-0.1, L + 0.1, L_EAVE - 0.55, L_EAVE - 0.3, -0.05, 0.14, 0.03, "M_GS_SandstoneDark"))
        det.append(rbox(-0.1, L + 0.1, L_EAVE - 0.3, L_EAVE + 0.05, -0.05, 0.45, 0.05, "M_GS_Sandstone"))
        for x in np.arange(0.25, L - 0.1, 0.5):
            det.append(rbox(x - 0.09, x + 0.09, L_EAVE - 0.5, L_EAVE - 0.32, 0.12, 0.30, 0.02, "M_GS_Sandstone"))
        det.append(rbox(-0.1, L + 0.1, FL - 0.3, FL + 0.6, -0.05, 0.10, 0.04, "M_GS_SandstoneDark"))
        panel = kit.place(mk.merge(w, *det), R, (0, 0, 0))
        parts.append(panel.transform(t=(-L / 2 * side, 0, zf)) if side > 0 else panel.transform(t=(L / 2, 0, zf)))
        boxes.add(-L / 2, L / 2, 0.0, L_EAVE, zf, zf - side * t)
    # vloer, plafond, verlichte bovenverdieping achter de ramen
    parts.append(rbox(-L / 2, L / 2, 0.0, FL - 0.07, -D / 2 + t, D / 2 - t, 0.0, "M_GS_SandstoneDark"))
    for x in np.arange(-L / 2, L / 2 - 1e-6, 0.4):
        parts.append(rbox(x + 0.006, min(x + 0.4, L / 2) - 0.006, FL - 0.07, FL, -D / 2 + t, D / 2 - t, 0.012,
                          ("M_GS_Wood", "M_GS_WoodDark")[int(round((x + L / 2) / 0.4)) % 2]))
    parts.append(rbox(-L / 2, L / 2, FL + 3.9, FL + 4.1, -D / 2 + t, D / 2 - t, 0.0, "M_GS_Interior"))
    for x in np.arange(-L / 2 + 1.0, L / 2 - 0.5, 2.0):
        parts.append(rbox(x - 0.14, x + 0.14, FL + 3.62, FL + 3.92, -D / 2 + t, D / 2 - t, 0.03, "M_GS_Wood"))
    boxes.add(-L / 2, L / 2, 0.0, FL, -D / 2, D / 2)
    boxes.add(-L / 2, L / 2, FL + 3.9, L_EAVE, -D / 2, D / 2)
    # mansardedak: steil onderdeel met felsnaden, vlakke kap, dakkapellen en schoorstenen
    ov = 0.35
    x0, x1, z0, z1 = -L / 2 - 0.05, L / 2 + 0.05, -D / 2 - ov, D / 2 + ov
    ins = 1.5
    parts.append(frustum(x0, x1, z0, z1, L_EAVE, ins, L_MID, "M_GS_Roof"))
    parts.append(seam_lines(x0, x1, z0, z1, L_EAVE, ins, L_MID, 0.75, "M_GS_RoofDark"))
    parts.append(frustum(x0 + ins, x1 - ins, z0 + ins, z1 - ins, L_MID, 0.6, L_TOP, "M_GS_RoofDark"))
    parts.append(rbox(x0 + ins - 0.05, x1 - ins + 0.05, L_MID - 0.08, L_MID + 0.10, z0 + ins - 0.05, z1 - ins + 0.05, 0.04,
                      "M_GS_SteelLight"))
    for side in (1, -1):
        for x in (-3.0, 0.0, 3.0):
            parts.append(dormer(x, side * (D / 2 + ov - ins * 0.55), side))
    for x, z in ((-3.6, -2.0), (3.6, 2.0)):
        parts.append(chimney(x, z, L_TOP - 0.2, L_TOP + 1.6))
    boxes.add(x0, x1, L_EAVE, L_MID, z0, z1)
    lights += [("Light_Room_%d" % (k + 1), (x, FL + 3.0, z)) for k, (x, z) in enumerate(((-3.0, -4.5), (3.0, 4.5)))]
    return mk.merge(*parts), boxes, lights


def dormer(x, z, side):
    """Dakkapel op het steile mansardevlak: zandstenen omlijsting, boograampje, rond leisteenblauw kapje."""
    w, h = 1.25, 1.55
    y0 = L_EAVE + 0.55
    R = kit.face_frame("+z" if side > 0 else "-z")
    o = dict(kind="arch", x0=-0.42, x1=0.42, y0=y0 + 0.25, ys=y0 + 0.95)
    face = slab([(-w / 2, y0 - 0.5), (w / 2, y0 - 0.5), (w / 2, y0 + h), (-w / 2, y0 + h)], [opening_loop(o)], -1.3, 0.0, 0.04,
                "M_GS_Sandstone")
    win = window(o, t=1.3, glass_depth=0.18, bars=True, sill=False, keystone=False, glow=0.25)
    yr = y0 + h
    roof = slab(arch_loop(-w / 2 - 0.16, w / 2 + 0.16, yr - 0.02, yr - 0.02, 12), [], -1.45, 0.22, 0.05, "M_GS_Roof")
    cap = rbox(-w / 2 - 0.12, w / 2 + 0.12, yr - 0.14, yr + 0.02, -1.35, 0.15, 0.04, "M_GS_SandstoneDark")
    m = mk.merge(face, win, cap, roof)
    return kit.place(m, R, (x, 0, z))


def chimney(x, z, y0, y1):
    parts = [rbox(x - 0.45, x + 0.45, y0, y1, z - 0.32, z + 0.32, 0.05, "M_GS_Brick"),
             rbox(x - 0.55, x + 0.55, y1 - 0.25, y1 - 0.05, z - 0.42, z + 0.42, 0.04, "M_GS_Sandstone"),
             rbox(x - 0.5, x + 0.5, y1 - 0.05, y1 + 0.12, z - 0.37, z + 0.37, 0.04, "M_GS_SandstoneDark")]
    for dx in (-0.2, 0.2):
        parts.append(cyl(0.12, 0.4, 10, 0.03, "M_GS_Terracotta", axis="y").transform(t=(x + dx, y1 + 0.3, z)))
    return mk.merge(*parts)


# ================================================================== paviljoen
def pavilion():
    """Lokaal: x -4,5 (binnen, tegen de vleugel) .. 4,5 (buiten), z -10..10. Topgevels met oculus aan beide kopse
    kanten (spoor en plein), pannendak met de nok langs z, buitengevel met ramen, hoekblokken."""
    parts, boxes, lights = [], Boxes(), []
    W, D, t = 9.0, 20.0, 0.55
    ridge = P_EAVE + (W / 2 + 0.4) * math.tan(P_PITCH) - 0.4 * math.tan(P_PITCH)
    for side, axis in ((1, "+z"), (-1, "-z")):
        R = kit.face_frame(axis)
        outline = [(0.0, FL - 0.3), (W, FL - 0.3), (W, P_EAVE), (W / 2, ridge), (0.0, P_EAVE)]
        xs = (1.5, 4.5, 7.5)
        gw = [dict(kind="arch", x0=x - 0.75, x1=x + 0.75, y0=FL + 1.0, ys=FL + 2.75) for x in xs]
        uw = [dict(kind="arch", x0=x - 0.6, x1=x + 0.6, y0=FL + 4.9, ys=FL + 6.25) for x in xs]
        ocu = dict(kind="round", x0=W / 2 - 0.75, x1=W / 2 + 0.75, y0=P_EAVE + 0.75, ys=P_EAVE + 2.25)
        w = wall(W, P_EAVE, gw + uw + [ocu], t=t, mat="M_GS_Brick", outline=outline)
        det = [window(o, t=t, glass_depth=0.30, glow=1.2) for o in gw + uw]
        det.append(window(ocu, t=t, glass_depth=0.2, glow=0.5))
        # zandstenen kroonlijst van het fronton (schuin en liggend), banden, plint, hoekblokken
        det.append(rbox(-0.25, W + 0.25, P_EAVE - 0.2, P_EAVE + 0.2, -0.05, 0.5, 0.05, "M_GS_Sandstone"))
        for sx in (1, -1):
            xa, xb = (0.0, W / 2) if sx > 0 else (W, W / 2)
            dx, dy = xb - xa, ridge - P_EAVE
            Lr = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)
            rake = rbox(-0.3, Lr + 0.25, -0.18, 0.22, -0.12, 0.48 if sx > 0 else 0.465, 0.05, "M_GS_Sandstone").transform(R=mk.rot_z(ang),
                                                                                                    t=(xa, P_EAVE + 0.18, 0))
            det.append(rake)
        det.append(rbox(W / 2 - 0.4, W / 2 + 0.4, ridge + 0.1, ridge + 0.55, -0.05, 0.45, 0.06, "M_GS_SandstoneDark"))
        det.append(ball(0.32, "M_GS_Brass", 14, 8).transform(t=(W / 2, ridge + 0.9, 0.2)))
        det.append(rbox(-0.1, W + 0.1, FL + 4.15, FL + 4.45, -0.05, 0.18, 0.04, "M_GS_Sandstone"))
        det.append(rbox(-0.1, W + 0.1, FL - 0.3, FL + 0.6, -0.05, 0.10, 0.04, "M_GS_SandstoneDark"))
        det.append(quoins(W, 0.0, FL + 0.6, P_EAVE - 0.2, 1, 1).transform(t=(0, 0, 0)))
        det.append(quoins(0.0, 0.0, FL + 0.6, P_EAVE - 0.2, -1, 1))
        # wimpel met treinpictogram tussen de bovenramen
        det.append(banner().transform(t=(3.0, FL + 5.6, 0.0)))
        det.append(banner().transform(t=(6.0, FL + 5.6, 0.0)))
        panel = kit.place(mk.merge(w, *det), R, (0, 0, 0))
        zf = side * D / 2
        parts.append(panel.transform(t=(-W / 2, 0, zf)) if side > 0 else panel.transform(t=(W / 2, 0, zf)))
        boxes.add(-W / 2, W / 2, 0.0, P_EAVE, zf, zf - side * t)
    # buitengevel (x = +4,5) met zes traveeën ramen, binnenwand (x = -4,5) baksteen zonder ramen boven de vleugel
    for axis, xo, has_windows in (("+x", W / 2, True), ("-x", -W / 2, False)):
        R = kit.face_frame(axis)
        zs = np.linspace(-D / 2 + 2.2, D / 2 - 2.2, 6)
        gw = [dict(kind="arch", x0=z - 0.7 + D / 2, x1=z + 0.7 + D / 2, y0=FL + 1.0, ys=FL + 2.75) for z in zs] if has_windows else []
        uw = [dict(kind="arch", x0=z - 0.55 + D / 2, x1=z + 0.55 + D / 2, y0=FL + 4.9, ys=FL + 6.25) for z in zs] if has_windows else []
        w = wall(D - 2 * t, P_EAVE, [dict(o, x0=o["x0"] - t, x1=o["x1"] - t) for o in gw + uw], t=t, mat="M_GS_Brick")
        det = [window(dict(o, x0=o["x0"] - t, x1=o["x1"] - t), t=t, glass_depth=0.3, glow=1.2) for o in gw + uw]
        # 1 cm lager/hoger dan de banden van de kopgevels: op de hoeken overlappen ze zonder dubbele vlakken
        det.append(rbox(-0.05, D - 2 * t + 0.05, P_EAVE - 0.19, P_EAVE + 0.19, -0.05, 0.49, 0.05, "M_GS_Sandstone"))
        det.append(rbox(-0.05, D - 2 * t + 0.05, FL + 4.16, FL + 4.44, -0.05, 0.17, 0.04, "M_GS_Sandstone"))
        det.append(rbox(-0.05, D - 2 * t + 0.05, FL - 0.3, FL + 0.59, -0.05, 0.09, 0.04, "M_GS_SandstoneDark"))
        m = kit.place(mk.merge(w, *det), R, (0, 0, 0))
        # lokaal x 0..D-2t  ->  z; '+x': lokale x loopt naar -z
        parts.append(m.transform(t=(xo, 0, D / 2 - t)) if axis == "+x" else m.transform(t=(xo, 0, -D / 2 + t)))
        boxes.add(xo, xo - (t if axis == "+x" else -t), 0.0, P_EAVE, -D / 2, D / 2)
    # verlichte binnenkant achter de ramen (paviljoens zijn dicht: opslag, technische ruimtes)
    boxes.add(-W / 2, W / 2, 0.0, P_EAVE, -D / 2, D / 2)
    # pannendak, nok langs z
    import station_props as props
    ov = 0.45
    rise = (W / 2 + ov) * math.tan(P_PITCH)
    y0 = P_EAVE - ov * math.tan(P_PITCH) + 0.16 / math.cos(P_PITCH)
    roof, _ = props.gable_roof(D + 2 * 0.25, W + 2 * ov, y0, rise, ridge_axis="z", rows=7, tile_mat="M_GS_Terracotta",
                               ridge_mat="M_GS_TerracottaDark")
    parts.append(roof)
    parts.append(chimney(1.6, -5.0, ridge - 1.6, ridge + 1.3))
    parts.append(chimney(1.6, 5.0, ridge - 1.6, ridge + 1.3))
    boxes.add(-W / 2, W / 2, P_EAVE, ridge, -D / 2, D / 2)
    return mk.merge(*parts), boxes, lights


def banner():
    """Marine wimpel met messing rand en treinpictogram aan een smeedijzeren arm (gevel z = 0, steekt naar +z)."""
    parts = [capsule((0, 0.55, 0.0), (0, 0.55, 0.75), 0.03, mat="M_GS_Steel", seg=5, rings=1),
             ball(0.05, "M_GS_Brass", 8, 5).transform(t=(0, 0.55, 0.78))]
    pts = [(-0.30, 0.5), (0.30, 0.5), (0.30, -0.45), (0.0, -0.62), (-0.30, -0.45)]
    flag = slab(pts, [], -0.02, 0.02, 0.01, "M_GS_Steel").transform(R=mk.rot_y(math.pi / 2), t=(0, 0, 0.5))
    trim = slab([(x * 1.06, y * 1.06) for x, y in pts], [[(x * 0.85, y * 0.85 - 0.02) for x, y in pts]], -0.028, 0.028, 0.005, "M_GS_Brass") \
        .transform(R=mk.rot_y(math.pi / 2), t=(0, 0, 0.5))
    icon = train_icon(0.34, 0.03, "M_GS_Cream").transform(R=mk.rot_y(math.pi / 2), t=(0.02, -0.02, 0.5))
    icon2 = train_icon(0.34, 0.03, "M_GS_Cream").transform(R=mk.rot_y(-math.pi / 2), t=(-0.02, -0.02, 0.5))
    return mk.merge(*parts, flag, trim, icon, icon2)


# ================================================================== klokkentoren
def clock_tower():
    """Lokaal: pivot midden onder, schacht 5,2 x 5,2 m; voorkant +z (plein)."""
    parts, boxes, lights = [], Boxes(), []
    h = 2.6
    tw = 0.53                                              # niet gelijk aan de vleugelmuren (0,5) waar de toren erin staat
    wins = [(FL + 6.2, FL + 8.1, 1.1), (FL + 11.0, FL + 13.2, 1.1), (FL + 15.2, FL + 16.9, 0.9)]
    for axis in ("+z", "+x", "-x", "-z"):
        R = kit.face_frame(axis)
        short = axis in ("+x", "-x")
        L = 2 * h - (2 * tw if short else 0.0)
        ops = [dict(kind="arch", x0=L / 2 - w / 2, x1=L / 2 + w / 2, y0=y0, ys=ys) for (y0, ys, w) in wins]
        if axis == "+z":                                   # deur aan de pleinkant (dienstingang)
            ops.append(dict(kind="arch", x0=L / 2 - 0.8, x1=L / 2 + 0.8, y0=0.0, ys=FL + 1.6, door=True))
        wl = wall(L, T_SHAFT, ops, t=tw, mat="M_GS_Brick", y0=0.0)
        det = [window(o, t=tw, glass_depth=0.25, glow=0.6) for o in ops if not o.get("door")]
        if axis == "+z":
            det.append(slab(arch_band(L / 2 - 0.77, L / 2 + 0.77, FL - 0.02, FL + 1.6, 0.33, 14), [], -0.1, 0.25, 0.04, "M_GS_Sandstone"))
            det.append(rbox(L / 2 - 0.8, L / 2 + 0.8, FL - 0.02, FL + 2.38, -tw + 0.06, -tw + 0.14, 0.03, "M_GS_WoodDark"))
        panel = mk.merge(wl, *det).transform(t=(-L / 2, 0, h))
        parts.append(kit.place(panel, R))
    parts.append(rbox(-h + tw, h - tw, 0.0, FL, -h + tw, h - tw, 0.0, "M_GS_SandstoneDark"))
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(quoins(sx * h, sz * h, FL + 0.6, T_SHAFT - 0.4, sx, sz).transform(t=(0, 0, 0)))
    for y in (FL - 0.3, FL + 4.3, FL + 9.6, FL + 14.4):
        y1 = y + (0.92 if y < FL else 0.32)
        parts.append(rbox(-h - 0.14, h + 0.14, y, y1, -h - 0.14, h + 0.14, 0.05, "M_GS_Sandstone"))
    # klokkamer: zandsteen, kroonlijsten, vier klokken met messing rand
    c = h + 0.25
    parts.append(rbox(-c, c, T_SHAFT, T_STAGE, -c, c, 0.1, "M_GS_Sandstone"))
    for y, e in ((T_SHAFT - 0.05, 0.42), (T_STAGE - 0.35, 0.45)):
        parts.append(rbox(-c - e, c + e, y, y + 0.36, -c - e, c + e, 0.06, "M_GS_SandstoneDark"))
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(rbox(sx * c - 0.3, sx * c + 0.3, T_SHAFT + 0.3, T_STAGE - 0.35, sz * c - 0.3, sz * c + 0.3, 0.06,
                              "M_GS_SandstoneDark"))
    yc = (T_SHAFT + T_STAGE) / 2 + 0.05
    for axis in ("+z", "+x", "-x", "-z"):
        R = kit.face_frame(axis)
        face = mk.merge(disc(1.62, -0.05, 0.10, "M_GS_SandstoneDark", 36, 0.04),
                        clock(1.45, 0.16, rim_mat="M_GS_Brass", face_mat="M_GS_Cream", hand_mat="M_GS_Steel",
                              hour=-1.1, minute=0.0).transform(t=(0, 0, 0.10)))
        parts.append(kit.place(face.transform(t=(0, yc, c)), R))
    # spits: vierkante pannenspits met een geknikte voet, messing bol en vlaggetje
    sy0 = T_STAGE + 0.10
    s = c + 0.35
    parts.append(frustum(-s, s, -s, s, sy0, 0.9, sy0 + 1.0, "M_GS_Terracotta", cap=False))
    parts.append(mk.lathe([(0.0, 0.0), (1.0, 0.0), (0.0, 1.0)], 4, (s - 0.9) * math.sqrt(2), (s - 0.9) * math.sqrt(2), 2.0,
                          mat="M_GS_Terracotta", phase=math.pi / 4).transform(s=(1, 6.2, 1), t=(0, sy0 + 1.0, 0)))
    tip = sy0 + 7.2
    parts.append(ball(0.42, "M_GS_Brass", 16, 9).transform(t=(0, tip + 0.15, 0)))
    parts.append(capsule((0, tip + 0.4, 0), (0, tip + 2.4, 0), 0.04, mat="M_GS_Steel", seg=5, rings=1))
    flag = slab([(0, 0), (1.25, 0.05), (1.05, -0.32), (1.25, -0.70), (0, -0.62)], [], -0.02, 0.02, 0.01, "M_GS_Steel")
    parts.append(flag.transform(t=(0.04, tip + 2.3, 0)))
    parts.append(train_icon(0.42, 0.03, "M_GS_Brass").transform(t=(0.62, tip + 1.97, 0.0)))
    parts.append(train_icon(0.42, 0.03, "M_GS_Brass").transform(R=mk.rot_y(math.pi), t=(0.62, tip + 1.97, 0.0)))
    boxes.add(-c, c, 0.0, T_STAGE, -c, c)
    lights.append(("Light_TowerClock", (0.0, yc, c + 1.2)))
    return mk.merge(*parts), boxes, lights
