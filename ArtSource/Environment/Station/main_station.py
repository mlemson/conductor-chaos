"""Hoofdstation met briefingruimte (issue #207), naar Docs/References/Environment/main-station-concept.png.

Zelfde stijl en conventies als het stationnetje (station_building.py), maar groter en met een pannendak:
  * middenhal 9,6 x 9,0 m, wanden 5,6 m, steile topgevel (42 graden) naar het spoor met een halfrond
    lichtvenster, klok en naambord (WHISTLETON, aanpasbaar); dubbele boogdeur (open, knopen Door_L/Door_R met scharnier)
  * twee zijvleugels (5,4 x 6,5 m, 0,5 m terugliggend) met lager zadeldak, dakkapel en schoorsteen;
    de vleugels zijn dicht (niet begaanbaar), hun ramen tonen een verlichte binnenwand
  * links een perronoverkapping (7,6 x 3,4 m) op palen met stenen voeten, banken, bagagekar en hangbord
  * binnen: de briefingruimte ('Training Hub') — koffiebar met espressomachine, prikbord, groen routebord met
    ROUTE-bord, vertrekbord met klok, twee rijen banken naar het bord, blauwe kluisjes, kapstok met pet
    en jasje, kratten, planten, rond kleed met treinicoon, hanglampen aan de bindbalken, open kap
Pivot: midden van de voorgevel op maaiveld (y = 0); voorkant kijkt naar +Z. Vloer op perronhoogte (1,05 m),
dus de voorgevel komt tegen de achterrand van het perron (5,15 m uit het hart van het spoor).
Ankers voor gameplay: Spawn_Briefing, Briefing_Board, Briefing_Seat_1..8, Spot_Coffee, Spot_Lockers, Light_*.
"""
import math

import numpy as np

from station_common import (DEG, PLATFORM_TOP, arch_band, arch_loop, ball, capsule, circle_loop, clock, col_box, cyl,
                            disc, mk, rbox, rounded_rect_loop, slab, tube, wall_outline)
import main_station_props as mp
import station_props as props
from station_platform import plan_slab

FL = PLATFORM_TOP
T = 0.26                           # wanddikte
YB = FL - 0.30                     # onderkant wanden (in de plint)
PLINTH = FL + 0.40
THICK = 0.16                       # dakbeschot

# middenhal
HW, HD = 4.8, 9.0
H_EAVE = FL + 5.6
H_PITCH = 42 * DEG
H_RIDGE_WALL = H_EAVE + HW * math.tan(H_PITCH)
H_OVER_SIDE, H_OVER_END = 0.45, 0.60
H_HALF = HW + H_OVER_SIDE
H_RISE = H_HALF * math.tan(H_PITCH)
H_Y0 = H_EAVE - H_OVER_SIDE * math.tan(H_PITCH) + THICK / math.cos(H_PITCH)
H_L = HD + 2 * H_OVER_END
H_TOP = H_Y0 + H_RISE

DOOR = (-1.25, 1.25, FL + 2.10)                  # dubbele boogdeur: x0, x1, aanzet boog
FRONT_WIN = (2.75, 3.85, FL + 1.05, FL + 2.35)    # |x0|, |x1|, onderkant, aanzet boog (links en rechts)
SIDE_WIN = (7.45, 8.45, FL + 1.05, FL + 2.25)     # in u = -z, beide zijwanden (achter de vleugels)
FAN_Y, FAN_R = H_EAVE + 0.10, 2.40               # halfrond lichtvenster in de topgevel
CLOCK_Y = FAN_Y + 1.20

# vleugels (rechts; links is gespiegeld)
WX0, WX1 = HW, 10.2
WZ0, WZ1 = -0.5, -7.0
WD = WZ0 - WZ1
W_EAVE = FL + 3.0
W_PITCH = 28 * DEG
W_OVER = 0.50
W_HALF = WD / 2 + W_OVER
W_RISE = W_HALF * math.tan(W_PITCH)
W_Y0 = W_EAVE - W_OVER * math.tan(W_PITCH) + THICK / math.cos(W_PITCH)
W_ZC = (WZ0 + WZ1) / 2
W_WINS = ((5.80, 6.90), (8.10, 9.20))            # ramen voor en achter (x), onderkant/aanzet:
W_WIN_Y = (FL + 0.95, FL + 1.95)
W_ROOF_X0 = WX0 - 0.08                           # dak begint 8 cm in de halwand
W_ROOF_X1 = WX1 + W_OVER

# overkapping (links van de linkervleugel)
CX1 = -WX1 - 0.16                                # tegen de plint van de vleugel
CX0 = -17.8
CD = 3.4
C_PITCH = 25 * DEG
C_OVER = 0.45
C_Y0 = FL + 2.35

# binnen
XI0, XI1 = -HW + T, HW - T
ZI0, ZI1 = -HD + T, -T
TIE_Y = FL + 3.9                                  # bindbalken (laag genoeg om de ruimte knus te houden)
TIE_Z = (-2.4, -5.0, -7.4)


def h_roof_under(x):
    """Onderkant dakbeschot van de hal boven x."""
    return H_TOP - abs(x) * math.tan(H_PITCH) - THICK / math.cos(H_PITCH)


def w_roof_under(z):
    """Onderkant dakbeschot van een vleugel boven z."""
    return W_Y0 + W_RISE - abs(z - W_ZC) * math.tan(W_PITCH) - THICK / math.cos(W_PITCH)


def side_slab(outline_uz, holes, x0, x1, c=0.03, mat="M_Stn_Plaster"):
    """Plaat in een zijwand: omtrek in (u = -z, y), dikte langs x van x0 tot x1."""
    return slab(outline_uz, holes, x0, x1, c, mat).transform(R=mk.rot_y(math.pi / 2))


def mirror_x(m):
    return m.transform(s=(-1, 1, 1))


# ------------------------------------------------------------------ vensters & deuren
def arched_window(x0, x1, y0, ys, depth0, depth1, bars=True):
    """Boograam in het XY-vlak (muur van z = depth0 tot depth1, buiten = depth1): kozijn door de dagkant,
    gloeiend glas, marine roedes (één staand, één liggend op de aanzet), vensterbank buiten."""
    w = 0.10
    r = (x1 - x0) / 2
    frame = slab(arch_loop(x0 - w, x1 + w, y0 - w, ys, 14), [arch_loop(x0 + 0.05, x1 - 0.05, y0 + 0.05, ys, 14)],
                 depth0 - 0.03, depth1 + 0.06, 0.02, "M_Stn_Timber")
    zc = (depth0 + depth1) / 2
    glass = slab(arch_loop(x0 + 0.04, x1 - 0.04, y0 + 0.04, ys, 14), [], zc - 0.012, zc + 0.012, 0.004, "M_Stn_Window")
    parts = [frame, glass]
    if bars:
        xm = (x0 + x1) / 2
        parts.append(rbox(xm - 0.035, xm + 0.035, y0 + 0.04, ys + r - 0.05, zc - 0.03, zc + 0.03, 0.012, "M_Stn_Navy"))
        parts.append(rbox(x0 + 0.04, x1 - 0.04, ys - 0.035, ys + 0.035, zc - 0.022, zc + 0.022, 0.010, "M_Stn_Navy"))
    parts.append(rbox(x0 - 0.18, x1 + 0.18, y0 - 0.20, y0 - 0.08, depth1 - 0.02, depth1 + 0.16, 0.03, "M_Stn_TimberDark"))
    return mk.merge(*parts)


def side_window(win, x0, x1, outward):
    """Boograam in een zijwand (u = -z). outward = +1 (rechts, buiten +x) of -1 (links)."""
    u0, u1, y0, ys = win
    w = arched_window(u0, u1, y0, ys, x0, x1).transform(R=mk.rot_y(math.pi / 2))
    return w if outward > 0 else mirror_x(w)


def door_leaf(side):
    """Eén blad van de dubbele boogdeur, lokaal: scharnier op x = 0, blad naar +X (side = -1, linkerblad) of
    naar -X (side = +1, rechterblad); onderkant y = 0, dikte langs Z. Glas boven, panelen onder, gele knop."""
    x0, x1, ys = DOOR
    w = (x1 - x0) / 2 - 0.03                          # 1,5 cm speling in het midden en langs het kozijn
    r = (x1 - x0) / 2
    spring = ys - FL
    xh = x0 + 0.03                                    # scharnier (wereld-x) van het linkerblad
    pts = [(0.0, 0.0), (w, 0.0)]                      # gebouwd als linkerblad (scharnier links, blad naar +x)
    for k in range(13):                               # bovenrand volgt de boog van het kozijn, 1 cm eronder
        lx = w - w * k / 12
        xw = xh + lx
        pts.append((lx, spring + math.sqrt(max(0.0, r * r - xw * xw)) - 0.01))
    hole = rounded_rect_loop(0.20, w - 0.14, 1.05, spring + 0.35, 0.05, 2)
    parts = [slab(pts, [hole], -0.035, 0.035, 0.012, "M_Stn_Navy"),
             slab(rounded_rect_loop(0.18, w - 0.12, 1.03, spring + 0.37, 0.05, 2), [], -0.012, 0.012, 0.004, "M_Stn_Window")]
    xm = (0.20 + w - 0.14) / 2
    parts.append(rbox(xm - 0.03, xm + 0.03, 1.05, spring + 0.35, -0.028, 0.028, 0.01, "M_Stn_Navy"))
    parts.append(rbox(0.20, w - 0.14, (1.05 + spring + 0.35) / 2 - 0.03, (1.05 + spring + 0.35) / 2 + 0.03, -0.021, 0.021,
                      0.008, "M_Stn_Navy"))
    for sgn in (1, -1):
        parts.append(slab(rounded_rect_loop(0.16, w - 0.12, 0.20, 0.88, 0.05, 2), [], 0.0, 0.016, 0.006, "M_Stn_NavyLight")
                     .transform(s=(1, 1, sgn), t=(0, 0, sgn * 0.034)))
        parts.append(ball(0.055, "M_Stn_Yellow", 12, 8).transform(t=(w - 0.12, 1.00, sgn * 0.075)))
        parts.append(capsule((w - 0.12, 1.0, sgn * 0.03), (w - 0.12, 1.0, sgn * 0.07), 0.018, mat="M_Stn_Yellow", seg=6,
                             rings=1))
    for y in (0.30, spring - 0.10):
        parts.append(rbox(-0.02, 0.15, y - 0.04, y + 0.04, -0.045, 0.045, 0.012, "M_Stn_Dark"))
    leaf = mk.merge(*parts)
    col = slab(pts, [], -0.05, 0.05, 0.0, "M_Stn_Collider")
    if side > 0:
        leaf, col = mirror_x(leaf), mirror_x(col)
    return leaf, col


def door_frame():
    x0, x1, ys = DOOR
    return mk.merge(slab(arch_band(x0, x1, FL - 0.01, ys, 0.16, 16), [], -0.02, 0.10, 0.025, "M_Stn_Timber"),
                    slab(arch_band(x0, x1, FL - 0.01, ys, 0.06, 16), [], -T - 0.02, -T + 0.04, 0.015, "M_Stn_Timber"))


def fanlight():
    """Halfrond lichtvenster in de topgevel met stralenroedes en de klok in het midden."""
    r, y = FAN_R, FAN_Y
    zc = -T / 2
    # kozijn steekt 4 cm in de opening, zodat de dagkant van de muur erin verdwijnt (geen dubbele vlakken)
    parts = [slab(arch_band(-r + 0.04, r - 0.04, y - 0.16, y, 0.22, 24), [], -T - 0.03, 0.08, 0.025, "M_Stn_Timber"),
             rbox(-r - 0.22, r + 0.22, y - 0.18, y + 0.02, -T - 0.05, 0.10, 0.025, "M_Stn_Timber"),          # onderdorpel
             slab(arch_loop(-r + 0.02, r - 0.02, y - 0.001, y, 24), [], zc - 0.012, zc + 0.012, 0.004, "M_Stn_Window")]
    for a in (25, 52, 90, 128, 155):                 # stralenroedes (houten), van de klok naar de boog
        ar = math.radians(a)
        p0 = (0.95 * math.cos(ar), y + 0.95 * math.sin(ar), zc)
        p1 = ((r - 0.05) * math.cos(ar), y + (r - 0.05) * math.sin(ar), zc)
        parts.append(capsule(p0, p1, 0.045, mat="M_Stn_Timber", seg=6, rings=1))
    ring = []
    for k in range(25):                              # boogroede halverwege
        a = math.pi * k / 24
        ring.append((1.65 * math.cos(a), y + 1.65 * math.sin(a), zc))
    parts.append(tube(ring, 0.035, "M_Stn_Timber", seg=6))
    parts.append(disc(0.98, -0.10, 0.06, "M_Stn_Timber", 32, 0.025).transform(t=(0.0, CLOCK_Y, 0.0)))
    parts.append(clock(0.82, 0.12, hour=-0.75, minute=0.0).transform(t=(0.0, CLOCK_Y, 0.06)))
    return mk.merge(*parts)


# ------------------------------------------------------------------ wanden
def hall_walls():
    holes_front = [arch_loop(s * FRONT_WIN[0] if s > 0 else -FRONT_WIN[1], FRONT_WIN[1] if s > 0 else -FRONT_WIN[0],
                             FRONT_WIN[2], FRONT_WIN[3], 14) for s in (-1, 1)]
    holes_front.append(arch_loop(-FAN_R, FAN_R, FAN_Y - 0.001, FAN_Y, 24))
    front = slab(wall_outline(-HW, HW, YB, H_EAVE, [DOOR], H_RIDGE_WALL, seg=16), holes_front, -T, 0.0, 0.03)
    back = slab(wall_outline(-HW, HW, YB, H_EAVE, [], H_RIDGE_WALL), [circle_loop(0.0, H_EAVE + 1.70, 0.55, 24)],
                -HD, -HD + T, 0.03)
    u0, u1, y0, ys = SIDE_WIN
    side_out = wall_outline(T, HD - T, YB, H_EAVE)
    right = side_slab(side_out, [arch_loop(u0, u1, y0, ys, 14)], HW - T, HW)
    left = mirror_x(right)
    return mk.merge(front, back, right, left)


def wing_walls():
    """Rechtervleugel: voor-, achter- en buitengevel (met topgevel). De binnenkant is de halwand."""
    zf0, zf1 = WZ0 - T, WZ0
    zb0, zb1 = WZ1, WZ1 + T
    holes = [arch_loop(a, b, W_WIN_Y[0], W_WIN_Y[1], 14) for a, b in W_WINS]
    front = slab([(WX0, YB), (WX1, YB), (WX1, W_EAVE), (WX0, W_EAVE)], holes, zf0, zf1, 0.03)
    back = slab([(WX0, YB), (WX1, YB), (WX1, W_EAVE), (WX0, W_EAVE)], holes, zb0, zb1, 0.03)
    ua, ub = -zf0, -zb1                                   # u-bereik van de zijwand (tussen voor- en achterwand)
    um = -W_ZC

    def top(u):
        return W_EAVE + (WD / 2 - abs(u - um)) * math.tan(W_PITCH)
    gable = [(ua, YB), (ub, YB), (ub, top(ub)), (um, top(um)), (ua, top(ua))]
    side = side_slab(gable, [arch_loop(um - 0.50, um + 0.50, W_WIN_Y[0], W_WIN_Y[1], 14),
                             circle_loop(um, W_EAVE + 0.85, 0.30, 20)], WX1 - T, WX1)
    return mk.merge(front, back, side)


def wing_windows():
    parts = []
    for a, b in W_WINS:
        parts.append(arched_window(a, b, W_WIN_Y[0], W_WIN_Y[1], WZ0 - T, WZ0))
        back = arched_window(-b, -a, W_WIN_Y[0], W_WIN_Y[1], -WZ1 - T, -WZ1)          # gedraaid naar achteren
        parts.append(back.transform(R=mk.rot_y(math.pi)))
    um = -W_ZC
    parts.append(side_window((um - 0.50, um + 0.50, W_WIN_Y[0], W_WIN_Y[1]), WX1 - T, WX1, 1))
    ring = slab(circle_loop(0, 0, 0.42, 24), [circle_loop(0, 0, 0.29, 24)], -0.03, T + 0.06, 0.02, "M_Stn_Timber")
    glass = disc(0.31, T / 2 - 0.012, T / 2 + 0.012, "M_Stn_Window", 24, 0.004)
    rnd = mk.merge(ring, glass).transform(R=mk.rot_y(math.pi / 2), t=(WX1 - T, W_EAVE + 0.85, W_ZC))
    parts.append(rnd)
    # verlichte binnenwand achter de ramen (de vleugels zijn niet begaanbaar)
    parts.append(rbox(WX0 + 0.30, WX1 - T - 0.40, FL - 0.06, W_EAVE - 0.10, WZ1 + T + 0.40, WZ0 - T - 0.40, 0.0,
                      "M_Stn_PlasterIn"))
    return mk.merge(*parts)


def hall_windows():
    parts = []
    for s in (-1, 1):
        a, b = (FRONT_WIN[0], FRONT_WIN[1]) if s > 0 else (-FRONT_WIN[1], -FRONT_WIN[0])
        parts.append(arched_window(a, b, FRONT_WIN[2], FRONT_WIN[3], -T, 0.0))
        parts.append(side_window(SIDE_WIN, HW - T, HW, s) if s > 0 else side_window(SIDE_WIN, HW - T, HW, -1))
    ring = slab(circle_loop(0, 0, 0.67, 24), [circle_loop(0, 0, 0.53, 24)], -T - 0.03, 0.06, 0.02, "M_Stn_Timber")
    glass = disc(0.55, -T / 2 - 0.012, -T / 2 + 0.012, "M_Stn_Window", 24, 0.004)
    bars = [rbox(-0.53, 0.53, -0.03, 0.03, -T / 2 - 0.025, -T / 2 + 0.025, 0.01, "M_Stn_Navy"),
            rbox(-0.03, 0.03, -0.53, 0.53, -T / 2 - 0.018, -T / 2 + 0.018, 0.008, "M_Stn_Navy")]
    parts.append(mk.merge(ring, glass, *bars).transform(R=mk.rot_y(math.pi), t=(0, H_EAVE + 1.70, -HD)))
    return mk.merge(*parts)


# ------------------------------------------------------------------ vakwerk
def hall_timber():
    parts = []
    for xs in (-1, 1):                                   # hoekstijlen
        for zc in (0.0, -HD):
            zo = 0.05 if zc == 0.0 else -0.05
            x0, x1 = xs * HW - 0.18 + 0.05 * xs, xs * HW + 0.18 + 0.05 * xs
            parts.append(rbox(min(x0, x1), max(x0, x1), PLINTH - 0.02, H_EAVE, zc - 0.18 + zo, zc + 0.18 + zo, 0.05,
                              "M_Stn_Timber"))
    # bindbalk voor (draagt het naambord) en achter, muurplaten opzij
    parts.append(rbox(-HW - 0.30, HW + 0.30, H_EAVE - 0.32, H_EAVE + 0.02, -0.06, 0.16, 0.05, "M_Stn_Timber"))
    parts.append(rbox(-HW - 0.30, HW + 0.30, H_EAVE - 0.32, H_EAVE + 0.02, -HD - 0.16, -HD + 0.06, 0.05, "M_Stn_Timber"))
    for xs in (-1, 1):
        x0, x1 = (HW - 0.08, HW + 0.10) if xs > 0 else (-HW - 0.10, -HW + 0.08)
        parts.append(rbox(x0, x1, H_EAVE - 0.32, H_EAVE + 0.02, -HD, 0.0, 0.05, "M_Stn_Timber"))
    # voorgevel: stijlen tussen deur en ramen, korbelen naar de bindbalk
    for xs in (-1, 1):
        x = xs * 2.38
        parts.append(rbox(x - 0.11, x + 0.11, PLINTH - 0.02, H_EAVE - 0.32, -0.04, 0.10, 0.03, "M_Stn_Timber"))
        parts.append(capsule((xs * (HW - 0.12), H_EAVE - 1.30, 0.07), (xs * (HW - 1.10), H_EAVE - 0.28, 0.07), 0.08,
                             mat="M_Stn_Timber", seg=6, rings=1))
    # windveren voor en achter
    for z0, z1 in ((H_OVER_END - 0.12, H_OVER_END + 0.02), (-HD - H_OVER_END - 0.02, -HD - H_OVER_END + 0.12)):
        yg, yr = h_roof_under(H_HALF), h_roof_under(0.0)
        prof = [(-H_HALF, yg - 0.30), (0.0, yr - 0.30), (H_HALF, yg - 0.30), (H_HALF, yg + 0.02), (0.0, yr + 0.02),
                (-H_HALF, yg + 0.02)]
        parts.append(slab(prof, (), z0, z1, 0.03, "M_Stn_Timber"))
    # achtergevel: vakwerk (stijlen, regel, schoren) zoals het achteraanzicht van het concept
    zb = -HD - 0.08
    for x in (-2.4, 2.4):
        parts.append(rbox(x - 0.11, x + 0.11, PLINTH - 0.02, H_EAVE - 0.32, zb - 0.04, zb + 0.08, 0.03, "M_Stn_Timber"))
    parts.append(rbox(-HW + 0.18, HW - 0.18, FL + 2.70, FL + 2.92, zb - 0.06, zb + 0.08, 0.03, "M_Stn_Timber"))
    for xs in (-1, 1):
        for (xa, ya, xb, yb) in ((xs * 0.45, FL + 2.95, xs * 2.25, H_EAVE - 0.35), (xs * 2.55, FL + 2.95, xs * 4.45, H_EAVE - 0.35)):
            parts.append(capsule((xa, ya, zb - 0.02), (xb, yb, zb - 0.02), 0.065, mat="M_Stn_TimberDark", seg=6, rings=1))
    # topgevel achter: stijlen naast het ronde raam
    for xs in (-1, 1):
        x = xs * 1.05
        parts.append(rbox(x - 0.10, x + 0.10, H_EAVE + 0.02, h_roof_under(x) + 0.05, zb - 0.04, zb + 0.08, 0.03,
                          "M_Stn_Timber"))
    return mk.merge(*parts)


def wing_timber():
    """Rechtervleugel: hoekstijlen, muurplaat, tussenstijlen en windveer op de buitengevel."""
    parts = []
    for zc, zo in ((WZ0, 0.05), (WZ1, -0.05)):
        parts.append(rbox(WX1 - 0.13, WX1 + 0.23, PLINTH - 0.02, W_EAVE - 0.02, zc - 0.18 + zo, zc + 0.18 + zo, 0.05,
                          "M_Stn_Timber"))
    parts.append(rbox(WX0 + 0.02, WX1 + 0.24, W_EAVE - 0.26, W_EAVE + 0.02, WZ0 - 0.06, WZ0 + 0.14, 0.04, "M_Stn_Timber"))
    parts.append(rbox(WX0 + 0.02, WX1 + 0.24, W_EAVE - 0.26, W_EAVE + 0.02, WZ1 - 0.14, WZ1 + 0.06, 0.04, "M_Stn_Timber"))
    parts.append(rbox(WX1 - 0.08, WX1 + 0.10, W_EAVE - 0.26, W_EAVE + 0.02, WZ1, WZ0, 0.04, "M_Stn_Timber"))
    for zf, zo in ((WZ0, 0.08), (WZ1, -0.08)):
        x = (W_WINS[0][1] + W_WINS[1][0]) / 2
        parts.append(rbox(x - 0.10, x + 0.10, PLINTH - 0.02, W_EAVE - 0.26, zf - 0.06 + zo * 0.5, zf + 0.06 + zo * 0.5,
                          0.03, "M_Stn_Timber"))
    # windveer langs de buitengevel (overstek van het dak)
    x0, x1 = W_ROOF_X1 - 0.12, W_ROOF_X1 + 0.02
    yg, yr = w_roof_under(W_ZC + W_HALF), w_roof_under(W_ZC)
    prof = [(-W_HALF, yg - 0.26), (0.0, yr - 0.26), (W_HALF, yg - 0.26), (W_HALF, yg + 0.02), (0.0, yr + 0.02),
            (-W_HALF, yg + 0.02)]
    parts.append(slab(prof, (), x0, x1, 0.03, "M_Stn_Timber").transform(R=mk.rot_y(math.pi / 2), t=(0, 0, W_ZC)))
    return mk.merge(*parts)


# ------------------------------------------------------------------ daken
def hall_roof():
    roof, col = props.gable_roof(H_L, 2 * H_HALF, H_Y0, H_RISE, ridge_axis="z", rows=15, tile_mat="M_Stn_RoofRed",
                                 ridge_mat="M_Stn_RoofRedDark")
    roof, col = roof.transform(t=(0, 0, -HD / 2)), col.transform(t=(0, 0, -HD / 2))
    z = H_OVER_END
    parts = [roof,
             mk.lathe([(0.0, 0.0), (0.18, 0.0), (0.24, 0.12), (0.26, 0.28), (0.0, 0.28)], 10, mat="M_Stn_Timber")
             .transform(t=(0.0, H_TOP + 0.12, z - 0.12)),
             ball(0.32, "M_Stn_Yellow", 20, 12).transform(t=(0.0, H_TOP + 0.68, z - 0.12))]
    return mk.merge(*parts), col


def chimney(cx, cz, y_roof, mat_top="M_Stn_PavingLight"):
    parts = [rbox(cx - 0.36, cx + 0.36, y_roof - 1.2, y_roof + 1.25, cz - 0.36, cz + 0.36, 0.06, "M_Stn_Stone")]
    for k, y in enumerate(np.arange(y_roof - 0.1, y_roof + 1.2 - 0.32, 0.34)):
        for side in range(4):
            off = 0.12 if (k + side) % 2 else -0.12
            b = rbox(-0.18 + off, 0.18 + off, y + 0.015, y + 0.32, 0.36, 0.39, 0.02, "M_Stn_StoneDark")
            parts.append(b.transform(R=mk.rot_y(side * math.pi / 2), t=(cx, 0, cz)))
    parts.append(rbox(cx - 0.46, cx + 0.46, y_roof + 1.20, y_roof + 1.38, cz - 0.46, cz + 0.46, 0.05, mat_top))
    parts.append(cyl(0.13, 0.30, 12, 0.03, "M_Stn_Timber", axis="y").transform(t=(cx, y_roof + 1.52, cz)))
    col = col_box(cx - 0.36, cx + 0.36, y_roof - 0.2, y_roof + 1.38, cz - 0.36, cz + 0.36)
    return mk.merge(*parts), col


def dormer(xc, z_front=-1.05, w=1.20):
    """Dakkapel op het voorste dakvlak van een vleugel: wangen tot in het dak, topgeveltje met boograampje.
    Het dakje loopt naar achteren tot het onder het hoofddak verdwijnt (vlak voor de nok)."""
    eave = FL + 4.10
    pitch = 32 * DEG
    hw = w / 2
    ridge = eave + hw * math.tan(pitch)
    z_back = W_ZC + 0.20
    y_low = w_roof_under(z_front) - 0.35
    parts = []
    front = slab([(xc - hw, y_low), (xc + hw, y_low), (xc + hw, eave), (xc, ridge), (xc - hw, eave)],
                 [arch_loop(xc - 0.25, xc + 0.25, FL + 3.66, FL + 3.82, 12)], z_front - 0.14, z_front, 0.02)
    parts.append(front)
    for xs in (-1, 1):                                    # wangen (vierkant, tot in het dak)
        x0, x1 = sorted((xc + xs * hw, xc + xs * (hw - 0.14)))
        parts.append(rbox(x0, x1, y_low, eave, z_back, z_front - 0.14, 0.02, "M_Stn_Plaster"))
    parts.append(arched_window(xc - 0.25, xc + 0.25, FL + 3.66, FL + 3.82, z_front - 0.14, z_front, bars=False))
    roof, col = props.gable_roof(z_front - z_back + 0.30, w + 0.40, eave - 0.20 * math.tan(pitch) + THICK / math.cos(pitch),
                                 (hw + 0.20) * math.tan(pitch), ridge_axis="z", rows=3, tile_mat="M_Stn_RoofRed",
                                 ridge_mat="M_Stn_RoofRedDark")
    zc = (z_front + 0.30 + z_back) / 2
    parts.append(roof.transform(t=(xc, 0, zc)))
    yg = eave - 0.20 * math.tan(pitch)
    prof = [(xc - hw - 0.20, yg - 0.16), (xc, ridge - 0.16), (xc + hw + 0.20, yg - 0.16), (xc + hw + 0.20, yg + 0.02),
            (xc, ridge + 0.02), (xc - hw - 0.20, yg + 0.02)]
    parts.append(slab(prof, (), z_front + 0.18, z_front + 0.30, 0.02, "M_Stn_Timber"))
    return mk.merge(*parts), col.transform(t=(xc, 0, zc))


def wing_roof():
    L = W_ROOF_X1 - W_ROOF_X0
    roof, col = props.gable_roof(L, 2 * W_HALF, W_Y0, W_RISE, ridge_axis="x", rows=7, tile_mat="M_Stn_RoofRed",
                                 ridge_mat="M_Stn_RoofRedDark")
    xc = (W_ROOF_X0 + W_ROOF_X1) / 2
    roof, col = roof.transform(t=(xc, 0, W_ZC)), col.transform(t=(xc, 0, W_ZC))
    dm, dcol = dormer(7.50)
    cx, cz = 5.95, -2.30
    ch, chcol = chimney(cx, cz, w_roof_under(cz) + THICK / math.cos(W_PITCH))
    return mk.merge(roof, dm, ch), mk.merge(col, dcol, chcol)


# ------------------------------------------------------------------ plint & vloer
FOOTPRINT = [(-HW, 0.0), (HW, 0.0), (HW, WZ0), (WX1, WZ0), (WX1, WZ1), (HW, WZ1), (HW, -HD), (-HW, -HD), (-HW, WZ1),
             (-WX1, WZ1), (-WX1, WZ0), (-HW, WZ0)]


def plinth():
    """Stenen plint langs de hele omtrek (twee verspringende lagen, onderbroken bij de voordeur) en kernen
    onder de vloeren. Bij buitenhoeken loopt de voor-/achterkant door, bij binnenhoeken de zijkant terug,
    zodat hoekblokken elkaar niet overlappen (geen dubbele vlakken)."""
    parts = [rbox(-HW + 0.05, HW - 0.05, 0.0, FL - 0.06, -HD + 0.05, -0.05, 0.0, "M_Stn_StoneDark")]
    for s in (-1, 1):
        x0, x1 = sorted((s * (WX0 + 0.05), s * (WX1 - 0.05)))
        parts.append(rbox(x0, x1, 0.0, FL - 0.06, WZ1 + 0.05, WZ0 - 0.05, 0.0, "M_Stn_StoneDark"))
    n = len(FOOTPRINT)
    pts = [np.array(p, float) for p in FOOTPRINT]
    OUT, IN = 0.16, 0.06

    def turn(i):                                          # > 0: buitenhoek (CW-omtrek in x,z met z naar voren)
        a, b, c = pts[i - 1], pts[i], pts[(i + 1) % n]
        return (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
    for row, (y0, y1) in enumerate(((0.0, FL - 0.02), (FL - 0.02, PLINTH))):
        for i in range(n):
            p0, p1 = pts[i], pts[(i + 1) % n]
            L = float(np.linalg.norm(p1 - p0))
            e = (p1 - p0) / L
            nrm = np.array([-e[1], e[0]])                  # naar buiten
            z_edge = abs(e[0]) > 0.5                       # loopt langs x (voor/achter): normaal langs z
            conv0, conv1 = turn(i) < 0, turn((i + 1) % n) < 0
            if z_edge:
                ext0, ext1 = (OUT if conv0 else IN), (OUT if conv1 else IN)
            else:
                ext0, ext1 = (-IN if conv0 else -OUT), (-IN if conv1 else -OUT)
            a_, b_ = -ext0, L + ext1
            cuts = sorted(set([a_, b_] + [c for c in np.arange(0.55 + 0.6 * (row % 2), L - 0.4, 1.2) if a_ + 0.2 < c < b_ - 0.2]))
            holes = []
            if row == 1 and i == 0:                        # voordeur
                ta, tb = DOOR[0] - p0[0] - 0.16, DOOR[1] - p0[0] + 0.16
                holes.append((ta, tb))
                cuts = sorted({c for c in cuts if not (ta - 0.2 < c < tb + 0.2)} | {ta, tb})
            for a, b in zip(cuts[:-1], cuts[1:]):
                if any(ta <= (a + b) / 2 <= tb for ta, tb in holes):
                    continue
                q0, q1 = p0 + e * a, p0 + e * b
                poly = [tuple(q0 - nrm * IN), tuple(q1 - nrm * IN), tuple(q1 + nrm * OUT), tuple(q0 + nrm * OUT)]
                parts.append(plan_slab(poly, y0, y1, 0.025, "M_Stn_Stone"))
    # drempel voor de voordeur en stenen sokkels met bloempotten naast de deur
    parts.append(rbox(DOOR[0] - 0.20, DOOR[1] + 0.20, FL - 0.02, FL + 0.015, -T - 0.04, 0.24, 0.015, "M_Stn_PavingLight"))
    return mk.merge(*parts)


def door_pots():
    parts, cols = [], []
    rng = np.random.default_rng(12)
    for s in (-1, 1):
        x = s * 1.98
        parts.append(rbox(x - 0.30, x + 0.30, 0.0, FL + 0.28, 0.20, 0.80, 0.04, "M_Stn_Stone"))
        pot = mk.merge(mk.lathe([(0.0, 0.0), (0.17, 0.0), (0.22, 0.30), (0.24, 0.34), (0.0, 0.34)], 14, mat="M_Stn_Timber"),
                       mk.lathe([(0.0, 0.30), (0.20, 0.30), (0.20, 0.32), (0.0, 0.32)], 14, mat="M_Stn_Soil"),
                       props.bush_mass(rng, 0.38, 0.38, 0.36, 4).transform(t=(0, 0.26, 0)))
        for k in range(4):
            a = k * math.pi / 2 + 0.4
            pot = mk.merge(pot, props.daisy(rng, rng.uniform(0.30, 0.42)).transform(t=(0.10 * math.cos(a), 0.30, 0.10 * math.sin(a))))
        parts.append(pot.transform(t=(x, FL + 0.28, 0.50)))
        cols.append(col_box(x - 0.30, x + 0.30, 0.0, FL + 0.70, 0.20, 0.80))
    return mk.merge(*parts), cols


def floor():
    parts = []
    x = XI0
    k = 0
    while x < XI1 - 1e-6:
        x1 = min(x + 0.34, XI1)
        mat = ("M_Stn_Floor", "M_Stn_FloorAlt")[k % 2]
        parts.append(rbox(x + 0.006, x1 - 0.006, FL - 0.07, FL, ZI0, ZI1 + 0.01, 0.012, mat))
        x = x1
        k += 1
    return mk.merge(*parts)


# ------------------------------------------------------------------ overkapping
def canopy():
    parts, cols = [], []
    x_in, LX, D = CX1, CX0, CD
    parts.append(rbox(LX + 0.10, x_in + 0.04, 0.0, FL - 0.10, -D + 0.10, -0.10, 0.0, "M_Stn_StoneDark"))
    edges = [((LX, 0.0), (LX, -D), (-1, 0), (-0.16, -0.16), (-0.36, -0.36)),
             ((x_in, 0.0), (LX, 0.0), (0, 1), (0.0, 0.06), (0.0, 0.08)),
             ((LX, -D), (x_in, -D), (0, -1), (0.06, 0.0), (0.08, 0.0))]
    for (p0, p1, n, ext_s, ext_t) in edges:
        p0, p1, n = np.array(p0, float), np.array(p1, float), np.array(n, float)
        L = np.linalg.norm(p1 - p0)
        e = (p1 - p0) / L
        q0, q1 = p0 - e * ext_s[0], p1 + e * ext_s[1]
        parts.append(plan_slab([tuple(q0 - n * 0.16), tuple(q1 - n * 0.16), tuple(q1 + n * 0.06), tuple(q0 + n * 0.06)],
                               0.0, FL - 0.11, 0.02, "M_Stn_Stone"))
        cuts = [0.0] + list(np.arange(0.76, L - 0.2, 0.76)) + [L]
        for a, b in zip(cuts[:-1], cuts[1:]):
            a_, b_ = a - (ext_t[0] if a == 0 else 0), b + (ext_t[1] if b == L else 0)
            if b_ - a_ < 0.10:                             # rest-stukje na de afknipping bij een hoek
                continue
            q0, q1 = p0 + e * a_, p0 + e * b_
            parts.append(plan_slab([tuple(q0 - n * 0.36), tuple(q1 - n * 0.36), tuple(q1 + n * 0.08), tuple(q0 + n * 0.08)],
                                   FL - 0.14, FL, 0.018, "M_Stn_PavingLight"))
    for x0 in np.arange(LX + 0.36, x_in - 1e-6, 0.76):
        for z0 in np.arange(-D + 0.36, -0.36 - 1e-6, 0.76):
            x1, z1 = min(x0 + 0.76, x_in), min(z0 + 0.76, -0.36)
            parts.append(rbox(x0, x1, FL - 0.10, FL, z0, z1, 0.018, "M_Stn_Paving"))
    cols.append(col_box(LX, x_in, 0.0, FL, -D, 0.0))
    # dak: zadeldak met de nok langs x, binnenkant 10 cm in de vleugelgevel
    half = D / 2 + C_OVER
    rise = half * math.tan(C_PITCH)
    rx0, rx1 = LX - C_OVER, -WX1 + 0.10
    roof, rcol = props.gable_roof(rx1 - rx0, 2 * half, C_Y0, rise, ridge_axis="x", rows=5, tile_mat="M_Stn_RoofRed",
                                  ridge_mat="M_Stn_RoofRedDark")
    parts.append(roof.transform(t=((rx0 + rx1) / 2, 0, -D / 2)))
    cols.append(rcol.transform(t=((rx0 + rx1) / 2, 0, -D / 2)))

    def under(z):
        return C_Y0 + rise - abs(z + D / 2) * math.tan(C_PITCH) - THICK / math.cos(C_PITCH)
    post_x = (LX + 0.32, LX + 2.70, x_in - 2.60, x_in - 0.40)
    post_z = (-0.30, -D + 0.30)
    beam_top = under(post_z[0] + 0.14) - 0.01
    beam_bot = beam_top - 0.26
    for z in post_z:
        for x in post_x:
            parts.append(rbox(x - 0.20, x + 0.20, FL, FL + 0.16, z - 0.20, z + 0.20, 0.04, "M_Stn_Stone"))
            parts.append(rbox(x - 0.13, x + 0.13, FL + 0.12, beam_bot + 0.02, z - 0.13, z + 0.13, 0.04, "M_Stn_Timber"))
            cols.append(col_box(x - 0.20, x + 0.20, FL, FL + 0.16, z - 0.20, z + 0.20))
            cols.append(col_box(x - 0.13, x + 0.13, FL + 0.12, beam_bot + 0.02, z - 0.13, z + 0.13))
        parts.append(rbox(LX + 0.10, -WX1 + 0.03, beam_bot, beam_top, z - 0.14, z + 0.14, 0.04, "M_Stn_Timber"))
        cols.append(col_box(LX + 0.10, x_in, beam_bot, beam_top, z - 0.14, z + 0.14))
        for x in post_x:                                   # korbelen langs de balk
            for dx in (-0.55, 0.55):
                if LX + 0.3 < x + dx < x_in - 0.1:
                    parts.append(capsule((x, beam_bot - 0.55, z), (x + dx, beam_bot + 0.04, z), 0.06, mat="M_Stn_Timber",
                                         seg=6, rings=1))
    for x in post_x:                                       # dwarsbalken met makelaar naar de nok
        # 3 cm onder de bovenkant van de langsbalken (geen bovenvlakken in één vlak)
        parts.append(rbox(x - 0.11, x + 0.11, beam_top - 0.25, beam_top - 0.03, post_z[1] - 0.18, post_z[0] + 0.18, 0.03,
                          "M_Stn_TimberDark"))
        parts.append(rbox(x - 0.09, x + 0.09, beam_top - 0.03, under(-D / 2) + 0.02, -D / 2 - 0.09, -D / 2 + 0.09, 0.03,
                          "M_Stn_TimberDark"))
    # inrichting: twee banken met de rug naar achteren, bagagekar, hangbord en lantaarn
    b, bc = props.bench(1.7)
    for x in (LX + 1.50, x_in - 1.55):
        parts.append(b.transform(t=(x, FL, -D + 0.75)))
        cols.append(bc.transform(t=(x, FL, -D + 0.75)))
    cart, cc = props.luggage_cart()
    cc = mk.merge(*cc) if isinstance(cc, tuple) else cc     # losse botsvormen (beweegbare prop) -> één vorm hier
    R = mk.rot_y(0.35)
    xc = (LX + x_in) / 2
    parts.append(cart.transform(R=R, t=(xc, FL, -D + 1.05)))
    cols.append(cc.transform(R=R, t=(xc, FL, -D + 1.05)))
    sign, _ = props.hanging_sign(0.62, 0.48, 0.28)
    parts.append(sign.transform(t=(xc, beam_bot + 0.01, post_z[0])))
    parts.append(capsule((post_x[2] + 0.6, beam_top - 0.22, -D / 2), (post_x[2] + 0.6, beam_top - 0.55, -D / 2), 0.012,
                         mat="M_Stn_Dark", seg=5, rings=1))
    parts.append(props.lantern(1.0).transform(t=(post_x[2] + 0.6, beam_top - 1.05, -D / 2)))
    light = (post_x[2] + 0.6, beam_top - 0.85, -D / 2)
    return mk.merge(*parts), mk.merge(*cols), light


# ------------------------------------------------------------------ gevel-details
def facade():
    parts = []
    board, sign = props.sign_nodes("Sign_Name", "WHISTLETON", 5.0, 0.92, 0.14, (0.0, FL + 4.58, 0.20))
    parts.append(board)
    for x in (-1.6, 1.6):                                  # ophangstangen naar de bindbalk
        parts.append(capsule((x, FL + 5.04, 0.20), (x, H_EAVE - 0.30, 0.20), 0.02, mat="M_Stn_Dark", seg=6, rings=1))
    wl, _ = props.wall_lantern()
    for x in (-1.98, 1.98):
        parts.append(wl.transform(t=(x, FL + 2.35, 0.0)))
    ban, _ = mp.banner()
    for s in (-1, 1):
        x = s * (W_WINS[0][1] + W_WINS[1][0]) / 2
        parts.append(ban.transform(t=(x, W_EAVE - 0.42, WZ0 + 0.10)))
    return mk.merge(*parts), sign


# ------------------------------------------------------------------ interieur: de briefingruimte
def _wall_timetable():
    board, _ = props.timetable()
    keep = [i for i, f in enumerate(board.F) if board.V[list(f), 1].min() > 0.86]
    return mk.Mesh(board.V, [board.F[i] for i in keep], [board.M[i] for i in keep])


def benches():
    """Twee rijen banken met het gezicht naar het routebord (-Z). Geeft ook de zitplekken terug."""
    parts, cols, seats = [], [], []
    b, bc = props.bench(1.9)
    R = mk.rot_y(math.pi)
    for z in (-4.90, -6.20):
        for x in (-1.25, 1.25):
            parts.append(b.transform(R=R, t=(x, FL, z)))
            cols.append(bc.transform(R=R, t=(x, FL, z)))
            for dx in (-0.45, 0.45):
                seats.append((x + dx, FL + 0.48, z + 0.02))
    return mk.merge(*parts), cols, seats


def interior():
    parts, cols = [], []
    # lambrisering langs de binnenwanden (onderbroken bij de deur)
    for (a, b) in ((XI0, DOOR[0] - 0.10), (DOOR[1] + 0.10, XI1)):
        parts.append(rbox(a, b, FL, FL + 0.90, ZI1 - 0.04, ZI1, 0.015, "M_Stn_TimberDark"))
    parts.append(rbox(XI0, XI1, FL, FL + 0.90, ZI0, ZI0 + 0.04, 0.015, "M_Stn_TimberDark"))
    for x0, x1 in ((XI0, XI0 + 0.04), (XI1 - 0.04, XI1)):
        parts.append(rbox(x0, x1, FL, FL + 0.90, ZI0 + 0.04, ZI1 - 0.04, 0.015, "M_Stn_TimberDark"))
    # achterwand: prikbord, routebord met ROUTE-bord, vertrekbord met klok
    zb = ZI0 + 0.04                                         # voorkant lambrisering / wand
    parts.append(mp.route_board(3.2, 1.60).transform(t=(0.0, FL + 1.88, zb + 0.06)))
    hub, hub_sign = props.sign_nodes("Sign_Route", "ROUTE", 2.2, 0.56, 0.10, (0.0, FL + 3.12, ZI0 + 0.08))
    parts.append(hub)
    parts.append(mp.notice_board(1.30, 1.05).transform(t=(-3.05, FL + 1.78, zb + 0.05)))
    parts.append(_wall_timetable().transform(t=(3.05, FL + 0.62, ZI0 + 0.10)))
    cols.append(col_box(-1.62, 1.62, FL + 1.0, FL + 2.7, ZI0, zb + 0.16))
    # koffiebar langs de linkerwand: werkbank met espressomachine tegen de muur, balie ervoor, bordje en plank
    R = mk.rot_y(math.pi / 2)
    bc_m, bc_c = mp.back_counter(2.4)
    xb = XI0 + 0.04 + 0.015 + 0.25
    parts.append(bc_m.transform(R=R, t=(xb, FL, -3.40)))
    cols.append(bc_c.transform(R=R, t=(xb, FL, -3.40)))
    cc_m, cc_c = mp.coffee_counter(2.6)
    parts.append(cc_m.transform(R=R, t=(-2.85, FL, -3.40)))
    cols.append(cc_c.transform(R=R, t=(-2.85, FL, -3.40)))
    parts.append(mp.cup_shelf(1.4).transform(R=R, t=(XI0, FL + 1.55, -3.40)))
    parts.append(mp.coffee_sign(0.70, 0.56).transform(R=R, t=(XI0 + 0.04, FL + 2.30, -3.40)))
    # rechterwand: kluisjes, kapstok, kratten
    R2 = mk.rot_y(-math.pi / 2)
    lk, lkc = mp.lockers(5)
    xl = XI1 - 0.04 - 0.015 - 0.23
    parts.append(lk.transform(R=R2, t=(xl, FL, -4.60)))
    cols.append(lkc.transform(R=R2, t=(xl, FL, -4.60)))
    parts.append(mp.coat_rack_wall(1.25).transform(R=R2, t=(XI1, FL + 1.62, -1.85)))
    cs, csc = props.crate_stack()
    parts.append(cs.transform(R=mk.rot_y(0.2), t=(3.75, FL, -6.65)))
    cols.append(csc.transform(R=mk.rot_y(0.2), t=(3.75, FL, -6.65)))
    # planten, kleed, banken
    for (x, z, seed) in ((-3.95, -6.95, 7), (3.95, -0.90, 3), (-3.95, -0.90, 5)):
        pm, pc = mp.potted_plant(seed)
        parts.append(pm.transform(t=(x, FL, z)))
        cols.append(pc.transform(t=(x, FL, z)))
    parts.append(mp.round_rug(1.25).transform(t=(0.0, FL, -2.95)))
    bm, bcols, seats = benches()
    parts.append(bm)
    cols += bcols
    return mk.merge(*parts), cols, seats, hub_sign


def roof_frame():
    """Open kap: bindbalken (laag) met korbelen naar de zijwanden, makelaars naar de nokbalk, schoren, hanglampen."""
    parts, lights = [], []
    ridge_y = h_roof_under(0.0)
    for z in TIE_Z:
        parts.append(rbox(-HW + 0.10, HW - 0.10, TIE_Y - 0.13, TIE_Y + 0.13, z - 0.13, z + 0.13, 0.04, "M_Stn_Timber"))
        parts.append(rbox(-0.11, 0.11, TIE_Y + 0.13, ridge_y + 0.02, z - 0.11, z + 0.11, 0.03, "M_Stn_Timber"))
        for xs in (-1, 1):
            parts.append(capsule((xs * (XI1 - 0.05), TIE_Y - 0.95, z), (xs * (XI1 - 0.85), TIE_Y - 0.10, z), 0.065,
                                 mat="M_Stn_TimberDark", seg=6, rings=1))
            parts.append(rbox(xs * XI1 - 0.09 if xs > 0 else -XI1 - 0.01, xs * XI1 + 0.01 if xs > 0 else -XI1 + 0.09,
                              TIE_Y - 1.05, TIE_Y - 0.13, z - 0.10, z + 0.10, 0.02, "M_Stn_Timber"))     # muurstijl
            parts.append(capsule((xs * 0.10, ridge_y - 1.6, z), (xs * 2.6, TIE_Y + 0.15, z), 0.065, mat="M_Stn_TimberDark",
                                 seg=6, rings=1))
    parts.append(rbox(-0.13, 0.13, ridge_y - 0.24, ridge_y + 0.03, -HD + 0.02, -0.02, 0.03, "M_Stn_TimberDark"))
    for z in TIE_Z[:2]:
        for x in (-1.75, 1.75):
            parts.append(mp.pendant_lamp(1.25).transform(t=(x, TIE_Y - 0.13, z)))
            lights.append((x, TIE_Y - 1.35, z))
    for x in (-3.05, 3.05):                               # boven prikbord en vertrekbord, vrij van het ROUTE-bord
        parts.append(mp.pendant_lamp(1.05).transform(t=(x, TIE_Y - 0.13, TIE_Z[2])))
        lights.append((x, TIE_Y - 1.15, TIE_Z[2]))
    return mk.merge(*parts), lights


# ------------------------------------------------------------------ samenstellen
def build(rng=None):
    """Knopenlijst voor export(): zichtbare delen, deurbladen met scharnier, botsvormen, licht- en gameplay-ankers."""
    walls = mk.merge(hall_walls(), wing_walls(), mirror_x(wing_walls()))
    timber = mk.merge(hall_timber(), wing_timber(), mirror_x(wing_timber()))
    openings = mk.merge(door_frame(), fanlight(), hall_windows(), wing_windows(), mirror_x(wing_windows()))
    pl = plinth()
    pots, pot_cols = door_pots()
    fl = floor()
    hroof, hcol = hall_roof()
    wroof, wcol = wing_roof()
    can, can_col, can_light = canopy()
    fac, name_sign = facade()
    inter, inter_cols, seats, hub_sign = interior()
    frame, lamp_lights = roof_frame()
    leaf_l, col_l = door_leaf(-1)
    leaf_r, col_r = door_leaf(1)

    cols = [col_box(-HW, HW, 0.0, FL, -HD, 0.0)]
    for s in (-1, 1):                                     # vleugels: dicht blok + plint
        x0, x1 = sorted((s * WX0, s * WX1))
        cols.append(col_box(x0, x1, 0.0, W_EAVE, WZ1, WZ0))
    top_door = DOOR[2] + (DOOR[1] - DOOR[0]) / 2 + 0.05
    cols += [col_box(-HW, DOOR[0], FL, H_EAVE, -T, 0.0), col_box(DOOR[1], HW, FL, H_EAVE, -T, 0.0),
             col_box(DOOR[0], DOOR[1], top_door, H_EAVE, -T, 0.0),
             col_box(-HW, HW, FL, H_EAVE, -HD, -HD + T),
             col_box(HW - T, HW, FL, H_EAVE, -HD, 0.0), col_box(-HW, -HW + T, FL, H_EAVE, -HD, 0.0)]
    cols += [hcol, wcol, mirror_x(wcol), can_col] + pot_cols + inter_cols
    col = mk.merge(*cols)

    x0, x1, _ = DOOR
    hz = -T + 0.05
    nodes = [
        ("Walls", mk.merge(walls, timber), None, (0, 0, 0), None, True),
        ("Plinth", mk.merge(pl, fl, pots), None, (0, 0, 0), None, True),
        ("Openings", openings, None, (0, 0, 0), None, True),
        ("Roof", mk.merge(hroof, wroof, mirror_x(wroof)), None, (0, 0, 0), None, True),
        ("Canopy", can, None, (0, 0, 0), None, True),
        ("Facade", fac, None, (0, 0, 0), None, True),
        ("BriefingRoom", inter, None, (0, 0, 0), None, True),
        ("RoofFrame", frame, None, (0, 0, 0), None, True),
        ("Door_L", leaf_l, None, (x0 + 0.03, FL + 0.005, hz), mk.rot_y(96 * DEG), True),
        ("Col_Door_L", col_l, "Door_L", (0, 0, 0), None, False),
        ("Door_R", leaf_r, None, (x1 - 0.03, FL + 0.005, hz), mk.rot_y(-96 * DEG), True),
        ("Col_Door_R", col_r, "Door_R", (0, 0, 0), None, False),
        ("Col_Main", col, None, (0, 0, 0), None, False),
        ("Light_Lantern_L", None, None, (-1.98, FL + 2.58, 0.30), None, True),
        ("Light_Lantern_R", None, None, (1.98, FL + 2.58, 0.30), None, True),
        ("Light_Fanlight", None, None, (0.0, FAN_Y + 1.0, -0.8), None, True),
        ("Light_Canopy", None, None, can_light, None, True),
        ("Spawn_Briefing", None, None, (0.0, FL, -2.2), None, True),
        ("Briefing_Board", None, None, (0.0, FL + 1.88, ZI0 + 0.20), None, True),
        ("Spot_Coffee", None, None, (-3.68, FL, -3.40), None, True),
        ("Spot_Lockers", None, None, (3.45, FL, -4.60), None, True),
    ]
    for k, p in enumerate(lamp_lights):
        nodes.append((f"Light_Pendant_{k + 1}", None, None, p, None, True))
    for k, p in enumerate(seats):
        nodes.append((f"Briefing_Seat_{k + 1}", None, None, p, None, True))
    return nodes + name_sign + hub_sign
