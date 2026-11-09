"""Stationnetje (issue #182), naar Docs/References/Environment/station-concept.png — begaanbaar.

Iets groter dan op het conceptblad zodat je naar binnen kunt (personages zijn ~1,25 m), maar knus:
  * hal 6,4 x 5,4 m buitenmaat, wanden 3,0 m hoog, steil zadeldak (40 graden) met de topgevel naar het spoor
  * vloer op perronhoogte (1,05 m) op een stenen plint, zodat de voordeur direct op het perron uitkomt
  * voorgevel: boogloket met toonbank en kassa, boogdeur (open, eigen knoop 'Door' met scharnier),
    STATION-bord op de bindbalk, klok in de topgevel, wandlantaarns, vakwerk met korbelen, gele nokbol
  * links een overkapping (luifel) op drie palen met bank, ton en hangbord; deur van de hal naar de luifel
  * binnen: loketruimte achter een L-toonbank (met doorgang), wachtruimte met twee banken, vertrekbord,
    plant, koffer, hanglampen aan de bindbalken, open kap met nokbalk
  * rechts een raam met bloembak, achter een raam en een rond gevelraampje, schoorsteen op het rechterdakvlak
Pivot: midden van de voorgevel op maaiveld (y = 0); voorkant kijkt naar +Z (draai 180 graden naar het spoor).
Zet de voorgevel tegen de achterrand van het perron (5,15 m uit het hart van het spoor).
"""
import math

import numpy as np

from station_common import (DEG, PLATFORM_TOP, arch_band, arch_loop, ball, capsule, circle_loop, clock, col_box, cyl,
                            disc, mk, rbox, rounded_rect_loop, slab, tube, wall_outline)
import station_props as props
from station_platform import plan_slab

FL = PLATFORM_TOP                 # vloerhoogte binnen = perronhoogte
HW = 3.2                          # halve breedte hal (buitenmaat)
D = 5.4                           # diepte hal (buitenmaat), voorgevel op z = 0
T = 0.24                          # wanddikte
EAVE = FL + 3.0                   # bovenkant zijwanden
PITCH = 40 * DEG
RIDGE_WALL = EAVE + HW * math.tan(PITCH)
YB = FL - 0.30                    # onderkant wanden (in de plint)
PLINTH = FL + 0.40                # bovenkant stenen plint buiten

DOOR = (0.75, 2.05, FL + 1.55)    # voordeur: x0, x1, aanzet boog
WIN = (-2.05, -0.75, FL + 0.85, FL + 1.55)   # loket: x0, x1, onderkant, aanzet boog
SIDE_DOOR = (3.10, 4.30, FL + 1.50)          # deur naar de luifel, in u = -z van de linkerwand
RIGHT_WIN = (2.15, 3.25, FL + 1.00, FL + 2.05)
BACK_WIN = (0.85, 1.95, FL + 1.00, FL + 2.05)

# dak
ROOF_OVER_SIDE = 0.65
ROOF_OVER_END = 0.60
ROOF_THICK = 0.16
ROOF_HALF = HW + ROOF_OVER_SIDE
ROOF_RISE = ROOF_HALF * math.tan(PITCH)
ROOF_Y0 = EAVE - ROOF_OVER_SIDE * math.tan(PITCH) + ROOF_THICK / math.cos(PITCH)
ROOF_L = D + 2 * ROOF_OVER_END
ROOF_TOP_RIDGE = ROOF_Y0 + ROOF_RISE

# luifel
LX = -HW - 3.0                    # buitenkant luifelvloer
LPOST_X = LX + 0.28


def _furniture():
    """Losse spullen onder de luifel: (naam, (mesh, collider), rotatie, plek). Ook gebruikt door de test."""
    x_in = -HW
    return [("Bench_Canopy", props.bench(1.7, split_colliders=True), mk.rot_y(-math.pi / 2), (x_in - 0.62, FL, -1.75)),
            ("Barrel_Canopy", props.barrel(), mk.rot_y(0.4), (x_in - 0.60, FL, -0.45)),
            ("Barrel_Small", props.barrel(0.70, 0.29), mk.rot_y(1.1), (LX + 0.95, FL, -D + 0.62)),
            ("CrateStack_Canopy", props.crate_stack(split_colliders=True), mk.rot_y(0.5 * math.pi + 0.15),
             (LX + 1.05, FL, -D + 1.75))]
L_PITCH = 14 * DEG
L_TOP_AT_GUTTER = 3.36            # bovenkant luifeldak waar het onder de hoofddakgoot doorloopt (x = -3.85)


def roof_y_under(x):
    """Onderkant dakbeschot boven x (hoofddak)."""
    return ROOF_TOP_RIDGE - abs(x) * math.tan(PITCH) - ROOF_THICK / math.cos(PITCH)


def side_slab(outline_uz, holes, x0, x1, c=0.03, mat="M_Stn_Plaster"):
    """Plaat in een zijwand: omtrek in (u = -z, y), dikte langs x van x0 tot x1."""
    return slab(outline_uz, holes, x0, x1, c, mat).transform(R=mk.rot_y(math.pi / 2))


def back_slab(outline, holes, z0, z1, c=0.03, mat="M_Stn_Plaster"):
    return slab(outline, holes, z0, z1, c, mat)


# ------------------------------------------------------------------ wanden & vakwerk
def walls():
    parts = []
    front = slab(wall_outline(-HW, HW, YB, EAVE, [DOOR], RIDGE_WALL), [arch_loop(WIN[0], WIN[1], WIN[2], WIN[3])],
                 -T, 0.0, 0.03, "M_Stn_Plaster")
    back = slab(wall_outline(-HW, HW, YB, EAVE, [], RIDGE_WALL),
                [rounded_rect_loop(BACK_WIN[0], BACK_WIN[1], BACK_WIN[2], BACK_WIN[3], 0.06, 2),
                 circle_loop(0.0, EAVE + 1.25, 0.36, 20)], -D, -D + T, 0.03, "M_Stn_Plaster")
    right = side_slab(wall_outline(T, D - T, YB, EAVE), [rounded_rect_loop(RIGHT_WIN[0], RIGHT_WIN[1], RIGHT_WIN[2],
                                                                          RIGHT_WIN[3], 0.06, 2)], HW - T, HW)
    left = side_slab(wall_outline(T, D - T, YB, EAVE, [SIDE_DOOR]), [], -HW, -HW + T)
    parts += [front, back, right, left]
    return mk.merge(*parts)


def timber_frame():
    parts = []
    # hoekstijlen
    for xs in (-1, 1):
        for zc in (0.0, -D):
            zo = 0.10 if zc == 0.0 else -0.10
            x0, x1 = xs * HW - 0.17 + 0.05 * xs, xs * HW + 0.17 + 0.05 * xs
            z0, z1 = zc - 0.17 + zo * 0.5, zc + 0.17 + zo * 0.5
            parts.append(rbox(min(x0, x1), max(x0, x1), PLINTH - 0.02, EAVE, z0, z1, 0.05, "M_Stn_Timber"))
    # bindbalken voor/achter en muurplaten opzij
    parts.append(rbox(-HW - 0.28, HW + 0.28, EAVE - 0.30, EAVE + 0.02, -0.06, 0.15, 0.05, "M_Stn_Timber"))
    parts.append(rbox(-HW - 0.28, HW + 0.28, EAVE - 0.30, EAVE + 0.02, -D - 0.15, -D + 0.06, 0.05, "M_Stn_Timber"))
    for xs in (-1, 1):
        parts.append(rbox(xs * HW - 0.08 if xs > 0 else -HW - 0.10, xs * HW + 0.10 if xs > 0 else -HW + 0.08,
                          EAVE - 0.30, EAVE + 0.02, -D, 0.0, 0.05, "M_Stn_Timber"))
    # korbelen onder de bindbalk (voorgevel)
    for xs in (-1, 1):
        parts.append(capsule((xs * (HW - 0.10), EAVE - 1.05, 0.08), (xs * (HW - 0.95), EAVE - 0.22, 0.08), 0.075,
                             mat="M_Stn_Timber", seg=6, rings=1))
    # topgevel: twee stijlen naast de klok en schuine schoren naar de hoeken
    for xs in (-1, 1):
        x = xs * 1.30
        parts.append(rbox(x - 0.10, x + 0.10, EAVE, roof_y_under(x) + 0.05, -0.04, 0.09, 0.03, "M_Stn_Timber"))
        parts.append(capsule((xs * 2.55, EAVE + 0.05, 0.05), (xs * 1.40, EAVE + 0.95, 0.05), 0.07, mat="M_Stn_Timber",
                             seg=6, rings=1))
    # windveren (boeiboord) langs de dakrand voor en achter
    for zc, z0, z1 in ((ROOF_OVER_END, ROOF_OVER_END - 0.12, ROOF_OVER_END + 0.02),
                       (-D - ROOF_OVER_END, -D - ROOF_OVER_END - 0.02, -D - ROOF_OVER_END + 0.12)):
        yg = roof_y_under(ROOF_HALF)
        yr = roof_y_under(0.0)
        prof = [(-ROOF_HALF, yg - 0.26), (0.0, yr - 0.26), (ROOF_HALF, yg - 0.26), (ROOF_HALF, yg + 0.02),
                (0.0, yr + 0.02), (-ROOF_HALF, yg + 0.02)]
        parts.append(slab(prof, (), z0, z1, 0.03, "M_Stn_Timber"))
    # zijwanden: één tussenstijl en een regel onder het raam (rechts) / naast de deur (links)
    for xs in (-1, 1):
        x0, x1 = (HW - 0.02, HW + 0.08) if xs > 0 else (-HW - 0.08, -HW + 0.02)
        parts.append(rbox(x0, x1, PLINTH - 0.02, EAVE - 0.25, -1.25, -1.05, 0.03, "M_Stn_Timber"))
    return mk.merge(*parts)


def plinth(rng):
    """Stenen plint rond de hal (drie lagen, verspringend) en een kern onder de vloer."""
    parts = [rbox(-HW + 0.05, HW - 0.05, 0.0, FL - 0.06, -D + 0.05, -0.05, 0.0, "M_Stn_StoneDark")]
    rows = ((0.0, FL - 0.02), (FL - 0.02, PLINTH))     # onderlaag + band boven de vloer (onderbroken bij deuren)

    def run(p0, p1, normal, row_i, y0, y1, skip=(), ext=0.10):
        """skip: [(as, lo, hi)] -> boven de vloer geen blokken in dat bereik (deuren); de rij wordt daar afgeknipt.
        ext: verlenging voorbij de hoekpunten. Voor- en achterkant lopen door over de hoek (+0,10), de zijkanten
        stoppen tegen hun binnenkant (-0,12), zodat de hoekblokken elkaar niet overlappen (geen dubbele vlakken)."""
        p0, p1 = np.array(p0, float), np.array(p1, float)
        L = np.linalg.norm(p1 - p0)
        e = (p1 - p0) / L
        cuts = set([0.0, L] + list(np.arange(0.55 + 0.6 * (row_i % 2), L - 0.4, 1.2)))
        holes = []
        if y0 >= FL - 0.1:
            for ax, lo, hi in skip:
                ta, tb = sorted(((lo - p0[ax]) / e[ax], (hi - p0[ax]) / e[ax]))
                holes.append((ta - 0.14, tb + 0.14))
        for ta, tb in holes:
            cuts = {c for c in cuts if not (ta - 0.2 < c < tb + 0.2)} | {ta, tb}
        cuts = sorted(cuts)
        for a, b in zip(cuts[:-1], cuts[1:]):
            mid = (a + b) / 2
            if any(ta <= mid <= tb for ta, tb in holes):
                continue
            q = [p0 + e * (a - (ext if a == 0 else 0)), p0 + e * (b + (ext if b == L else 0))]
            n = np.array(normal, float)
            poly = [tuple(q[0] - n * 0.12), tuple(q[1] - n * 0.12), tuple(q[1] + n * 0.10), tuple(q[0] + n * 0.10)]
            parts.append(plan_slab(poly, y0, y1, 0.025, "M_Stn_Stone"))
    for i, (y0, y1) in enumerate(rows):
        run((-HW - 0.06, 0.06), (HW + 0.06, 0.06), (0, 1), i, y0, y1, skip=[(0, DOOR[0], DOOR[1])])
        run((HW + 0.06, -D - 0.06), (-HW - 0.06, -D - 0.06), (0, -1), i, y0, y1)
        run((HW + 0.06, 0.06), (HW + 0.06, -D - 0.06), (1, 0), i, y0, y1, ext=-0.12)
        run((-HW - 0.06, -D - 0.06), (-HW - 0.06, 0.06), (-1, 0), i, y0, y1, skip=[(1, -SIDE_DOOR[1], -SIDE_DOOR[0])],
            ext=-0.12)
    # drempel voor de voordeur: ligt op de onderste laag, vult de onderbreking in de plint precies en steekt 4 cm
    # voor de plint uit (geen voorvlak in hetzelfde vlak als de plint)
    parts.append(rbox(DOOR[0] - 0.14, DOOR[1] + 0.14, FL - 0.02, FL + 0.015, -T - 0.04, 0.20, 0.015, "M_Stn_PavingLight"))
    return mk.merge(*parts)


def floor():
    parts = []
    x = -HW + T
    k = 0
    while x < HW - T - 1e-6:
        x1 = min(x + 0.32, HW - T)
        mat = ("M_Stn_Floor", "M_Stn_FloorAlt")[k % 2]
        parts.append(rbox(x + 0.006, x1 - 0.006, FL - 0.07, FL, -D + T, -T + 0.01, 0.012, mat))
        x = x1
        k += 1
    # deurdorpel naar de luifel
    u0, u1 = SIDE_DOOR[0], SIDE_DOOR[1]
    parts.append(rbox(-HW - 0.10, -HW + T + 0.02, FL - 0.20, FL + 0.012, -u1 - 0.08, -u0 + 0.08, 0.02, "M_Stn_PavingLight"))
    return mk.merge(*parts)


# ------------------------------------------------------------------ openingen
def window_unit(x0, x1, y0, y1, depth0, depth1):
    """Raam in het XY-vlak (muur van z = depth0 tot depth1, buiten = depth1): houten kozijn door de hele dagkant,
    glas met marine roedes, buiten een vensterbank."""
    w = 0.10
    frame = slab(rounded_rect_loop(x0 - w, x1 + w, y0 - w, y1 + w, 0.10, 2), [rounded_rect_loop(x0 + 0.05, x1 - 0.05, y0 + 0.05,
                                                                                               y1 - 0.05, 0.05, 2)],
                 depth0 - 0.03, depth1 + 0.06, 0.02, "M_Stn_Timber")
    zc = (depth0 + depth1) / 2
    glass = slab(rounded_rect_loop(x0 + 0.04, x1 - 0.04, y0 + 0.04, y1 - 0.04, 0.05, 2), [], zc - 0.012, zc + 0.012, 0.004,
                 "M_Stn_Window")
    xm, ym = (x0 + x1) / 2, (y0 + y1) / 2 + 0.06
    bars = [rbox(xm - 0.035, xm + 0.035, y0 + 0.04, y1 - 0.04, zc - 0.03, zc + 0.03, 0.012, "M_Stn_Navy"),
            rbox(x0 + 0.04, x1 - 0.04, ym - 0.035, ym + 0.035, zc - 0.022, zc + 0.022, 0.010, "M_Stn_Navy")]  # dunner: kruist
    sill = rbox(x0 - 0.18, x1 + 0.18, y0 - 0.20, y0 - 0.08, depth1 - 0.02, depth1 + 0.16, 0.03, "M_Stn_TimberDark")
    return mk.merge(frame, glass, *bars, sill)


def ticket_window():
    x0, x1, y0, ys = WIN
    parts = [slab(arch_loop(x0 - 0.15, x1 + 0.15, y0 - 0.16, ys, 14), [arch_loop(x0, x1, y0, ys, 14)], -0.02, 0.08, 0.025,
                  "M_Stn_Timber")]
    # toonbank door de opening (buiten loket + binnen werkblad)
    parts.append(rbox(x0 - 0.22, x1 + 0.22, y0 - 0.06, y0 + 0.05, -T - 0.42, 0.34, 0.03, "M_Stn_Timber"))
    parts.append(rbox(x0 - 0.10, x1 + 0.10, y0 - 0.30, y0 - 0.06, 0.04, 0.22, 0.03, "M_Stn_TimberDark"))
    # kassa (grijs kistje uit het concept) met geldla en gleuf
    parts.append(rbox(-1.60, -1.18, y0 + 0.05, y0 + 0.27, -0.30, -0.02, 0.04, "M_Stn_Stone"))
    parts.append(rbox(-1.55, -1.23, y0 + 0.27, y0 + 0.36, -0.26, -0.10, 0.03, "M_Stn_PavingLight"))
    parts.append(rbox(-1.50, -1.28, y0 + 0.09, y0 + 0.14, -0.03, 0.0, 0.01, "M_Stn_StoneDark"))
    # loketbordje met kaartje, aan twee stangetjes in de boog
    board = mk.merge(slab(rounded_rect_loop(-0.25, 0.25, -0.13, 0.13, 0.05, 2), [], -0.03, 0.03, 0.012, "M_Stn_Yellow"),
                     props.ticket_icon(0.28, 0.015, "M_Stn_Cream").transform(t=(0.0, 0.0, 0.028)))
    xc = (x0 + x1) / 2
    yb = ys + 0.18
    parts.append(board.transform(R=mk.rot_z(-0.06), t=(xc, yb, -0.10)))
    for dx in (-0.16, 0.16):
        parts.append(capsule((xc + dx, yb + 0.10, -0.10), (xc + dx, ys + (x1 - x0) / 2 - 0.02, -0.10), 0.012,
                             mat="M_Stn_Dark", seg=6, rings=1))
    return mk.merge(*parts)


def door_frame():
    x0, x1, ys = DOOR
    return mk.merge(slab(arch_band(x0, x1, FL - 0.01, ys, 0.15, 14), [], -0.02, 0.09, 0.025, "M_Stn_Timber"),
                    slab(arch_band(x0, x1, FL - 0.01, ys, 0.06, 14), [], -T - 0.02, -T + 0.04, 0.015, "M_Stn_Timber"))


def door_leaf():
    """Deurblad, lokaal: scharnier op x = 0 (rechterstijl), blad naar -X, onderkant y = 0, dikte langs Z."""
    w = DOOR[1] - DOOR[0] - 0.03
    spring = DOOR[2] - FL
    parts = [slab(arch_loop(-w, 0.0, 0.0, spring - 0.005, 14), [arch_loop(-w + 0.27, -0.27, 1.02, spring + 0.02, 10)],
                  -0.035, 0.035, 0.015, "M_Stn_Navy")]
    zc = 0.0
    parts.append(slab(arch_loop(-w + 0.25, -0.25, 1.00, spring + 0.02, 10), [], zc - 0.012, zc + 0.012, 0.004, "M_Stn_Window"))
    xm = -w / 2
    parts.append(rbox(xm - 0.03, xm + 0.03, 1.02, spring + 0.02 + (w / 2 - 0.27), -0.028, 0.028, 0.01, "M_Stn_Navy"))
    parts.append(rbox(-w + 0.27, -0.27, spring - 0.02, spring + 0.04, -0.021, 0.021, 0.008, "M_Stn_Navy"))
    for sgn in (1, -1):
        parts.append(slab(rounded_rect_loop(-w + 0.17, -0.17, 0.22, 0.86, 0.05, 2), [], 0.0, 0.016, 0.006, "M_Stn_NavyLight")
                     .transform(s=(1, 1, sgn), t=(0, 0, sgn * 0.034)))
        parts.append(ball(0.055, "M_Stn_Yellow", 12, 8).transform(t=(-w + 0.13, 1.00, sgn * 0.075)))
        parts.append(capsule((-w + 0.13, 1.0, sgn * 0.03), (-w + 0.13, 1.0, sgn * 0.07), 0.018, mat="M_Stn_Yellow", seg=6, rings=1))
    for y in (0.30, spring - 0.10):
        parts.append(rbox(-0.15, 0.02, y - 0.04, y + 0.04, -0.045, 0.045, 0.012, "M_Stn_Dark"))   # naast het paneel
    col = slab(arch_loop(-w, 0.0, 0.0, spring, 8), [], -0.05, 0.05, 0.0, "M_Stn_Collider")
    return mk.merge(*parts), col


# ------------------------------------------------------------------ dak
def main_roof():
    roof, col = props.gable_roof(ROOF_L, 2 * ROOF_HALF, ROOF_Y0, ROOF_RISE, ridge_axis="z", rows=9)
    roof = roof.transform(t=(0, 0, -D / 2))
    col = col.transform(t=(0, 0, -D / 2))
    parts = [roof]
    # nokbol + houten voet aan de voorkant van de nok
    z = ROOF_OVER_END
    parts.append(mk.lathe([(0.0, 0.0), (0.16, 0.0), (0.22, 0.10), (0.24, 0.24), (0.0, 0.24)], 10, mat="M_Stn_Timber")
                 .transform(t=(0.0, ROOF_TOP_RIDGE + 0.12, z - 0.10)))
    parts.append(ball(0.26, "M_Stn_Yellow", 20, 12).transform(t=(0.0, ROOF_TOP_RIDGE + 0.58, z - 0.10)))
    # schoorsteen op het rechterdakvlak
    cx, cz = 1.75, -3.92
    y_roof = ROOF_TOP_RIDGE - cx * math.tan(PITCH)
    parts.append(rbox(cx - 0.34, cx + 0.34, y_roof - 1.2, y_roof + 1.35, cz - 0.34, cz + 0.34, 0.06, "M_Stn_Stone"))
    for k, y in enumerate(np.arange(y_roof - 0.2, y_roof + 1.3 - 0.32, 0.34)):       # niet tot in de kap
        for side in range(4):
            off = 0.12 if (k + side) % 2 else -0.12
            b = rbox(-0.17 + off, 0.17 + off, y + 0.015, y + 0.32, 0.34, 0.37, 0.02, "M_Stn_StoneDark")
            parts.append(b.transform(R=mk.rot_y(side * math.pi / 2), t=(cx, 0, cz)))
    parts.append(rbox(cx - 0.44, cx + 0.44, y_roof + 1.30, y_roof + 1.48, cz - 0.44, cz + 0.44, 0.05, "M_Stn_PavingLight"))
    parts.append(cyl(0.13, 0.30, 12, 0.03, "M_Stn_Timber", axis="y").transform(t=(cx, y_roof + 1.62, cz)))
    col = mk.merge(col, col_box(cx - 0.34, cx + 0.34, y_roof - 0.2, y_roof + 1.48, cz - 0.34, cz + 0.34))
    return mk.merge(*parts), col


def mono_roof(x_hi, x_lo, y_hi, pitch, z0, z1, rows=6):
    """Lessenaarsdak (luifel): hoog bij x_hi, laag bij x_lo (x_lo < x_hi), van z0 tot z1."""
    run = x_hi - x_lo
    slope_len = run / math.cos(pitch)
    L = z1 - z0
    parts = [rbox(-L / 2, L / 2, -ROOF_THICK, 0.0, -slope_len, 0.0, 0.03, "M_Stn_TimberDark")]
    row = slope_len / rows
    n_t = max(2, int(round(L / 0.62)))
    tw = L / n_t
    for r in range(rows):
        s0 = -slope_len + r * row
        off = (tw / 2) * (r % 2)
        for x in np.arange(-L / 2 - off, L / 2 + 1e-6, tw):
            xa, xb = max(x, -L / 2), min(x + tw, L / 2)
            if xb - xa < 0.12:
                continue
            top = s0 + row + 0.10
            e_ins = 0.02 * (r % 2)                               # overlappende rijen: kopse kanten verspringen
            xa_, xb_ = xa + (e_ins if xa <= -L / 2 + 1e-6 else 0), xb - (e_ins if xb >= L / 2 - 1e-6 else 0)
            t = rbox(xa_ + 0.012, xb_ - 0.012, 0.0, 0.10, s0 - 0.02 - top, 0.0, 0.045, "M_Stn_Roof")
            parts.append(t.transform(R=mk.rot_x(0.09), t=(0, 0.004, top)))
    m = mk.merge(*parts).transform(R=mk.rot_x(-pitch))
    m = m.transform(R=mk.rot_y(math.pi / 2), t=(x_hi, y_hi, (z0 + z1) / 2))
    col = col_box(-L / 2, L / 2, -ROOF_THICK, 0.12, -slope_len, 0.0).transform(R=mk.rot_x(-pitch))
    col = col.transform(R=mk.rot_y(math.pi / 2), t=(x_hi, y_hi, (z0 + z1) / 2))
    return m, col


# ------------------------------------------------------------------ luifel
def lean_to(rng):
    parts, cols = [], []
    x_in = -HW
    # vloer: kern, stenen blokken, randtegels, tegels
    parts.append(rbox(LX + 0.10, x_in, 0.0, FL - 0.10, -D + 0.10, -0.10, 0.0, "M_Stn_StoneDark"))
    # randen: voor- en achterkant lopen over de buitenhoek door, de buitenzijde stopt ertegen (hoeken overlappen
    # niet, dus geen dubbele vlakken); aan de muurkant eindigen ze in de plint van het gebouw
    edges = [((LX, 0.0), (LX, -D), (-1, 0), (-0.16, -0.16), (-0.36, -0.36)),
             ((x_in, 0.0), (LX, 0.0), (0, 1), (0.0, 0.06), (0.0, 0.08)),
             ((LX, -D), (x_in, -D), (0, -1), (0.06, 0.0), (0.08, 0.0))]
    for (p0, p1, n, ext_s, ext_t) in edges:
        p0, p1, n = np.array(p0, float), np.array(p1, float), np.array(n, float)
        L = np.linalg.norm(p1 - p0)
        e = (p1 - p0) / L
        q0, q1 = p0 - e * ext_s[0], p1 + e * ext_s[1]          # één doorlopend vlak
        poly = [tuple(q0 - n * 0.16), tuple(q1 - n * 0.16), tuple(q1 + n * 0.06), tuple(q0 + n * 0.06)]
        parts.append(plan_slab(poly, 0.0, FL - 0.11, 0.02, "M_Stn_Stone"))
        cuts = [0.0] + list(np.arange(0.76, L - 0.2, 0.76)) + [L]
        for a, b in zip(cuts[:-1], cuts[1:]):
            q0, q1 = p0 + e * (a - (ext_t[0] if a == 0 else 0)), p0 + e * (b + (ext_t[1] if b == L else 0))
            poly = [tuple(q0 - n * 0.36), tuple(q1 - n * 0.36), tuple(q1 + n * 0.08), tuple(q0 + n * 0.08)]
            parts.append(plan_slab(poly, FL - 0.14, FL, 0.018, "M_Stn_PavingLight"))
    for i, x0 in enumerate(np.arange(LX + 0.36, x_in - 1e-6, 0.76)):
        for j, z0 in enumerate(np.arange(-D + 0.36, -0.36 - 1e-6, 0.76)):
            x1, z1 = min(x0 + 0.76, x_in), min(z0 + 0.76, -0.36)
            parts.append(rbox(x0, x1, FL - 0.10, FL, z0, z1, 0.018, "M_Stn_Paving"))
    cols.append(col_box(LX, x_in, 0.0, FL, -D, 0.0))
    # dak
    x_hi = x_in
    y_hi = L_TOP_AT_GUTTER + (ROOF_HALF - HW) * math.tan(L_PITCH)
    x_lo = LX - 0.45
    roof, rcol = mono_roof(x_hi, x_lo, y_hi, L_PITCH, -D - 0.45, 0.45, rows=6)
    parts.append(roof)
    cols.append(rcol)

    def top_at(x):
        return y_hi - (x_hi - x) * math.tan(L_PITCH)
    beam_top = top_at(LPOST_X + 0.15) - ROOF_THICK / math.cos(L_PITCH)
    beam_bot = beam_top - 0.26
    post_z = (-0.30, -D / 2, -D + 0.30)
    for z in post_z:
        parts.append(rbox(LPOST_X - 0.20, LPOST_X + 0.20, FL, FL + 0.16, z - 0.20, z + 0.20, 0.04, "M_Stn_Stone"))
        parts.append(rbox(LPOST_X - 0.13, LPOST_X + 0.13, FL + 0.12, beam_bot + 0.02, z - 0.13, z + 0.13, 0.04, "M_Stn_Timber"))
        cols.append(col_box(LPOST_X - 0.20, LPOST_X + 0.20, FL, FL + 0.16, z - 0.20, z + 0.20))
        cols.append(col_box(LPOST_X - 0.13, LPOST_X + 0.13, FL + 0.12, beam_bot + 0.02, z - 0.13, z + 0.13))
    # buitenbalk langs z en dwarsbalken naar de muur (voor, midden, achter), met korbelen
    parts.append(rbox(LPOST_X - 0.14, LPOST_X + 0.14, beam_bot, beam_top, -D - 0.25, 0.25, 0.04, "M_Stn_Timber"))
    cols.append(col_box(LPOST_X - 0.14, LPOST_X + 0.14, beam_bot, beam_top, -D - 0.25, 0.25))
    for z in post_z:
        y_wall = top_at(x_in) - ROOF_THICK / math.cos(L_PITCH)
        prof = [(LPOST_X - 0.08, beam_bot), (x_in + 0.02, y_wall - 0.26), (x_in + 0.02, y_wall), (LPOST_X - 0.08, beam_top)]
        parts.append(slab(prof, (), z - 0.11, z + 0.11, 0.03, "M_Stn_TimberDark"))
        parts.append(capsule((LPOST_X + 0.06, beam_bot - 0.55, z), (LPOST_X + 0.62, beam_bot + 0.06, z), 0.065,
                             mat="M_Stn_Timber", seg=6, rings=1))
        for dz in (0.55, -0.55):
            if -D + 0.2 < z + dz < -0.2:
                parts.append(capsule((LPOST_X, beam_bot - 0.55, z + dz * 0.1), (LPOST_X, beam_bot + 0.06, z + dz),
                                     0.065, mat="M_Stn_Timber", seg=6, rings=1))
    sign, _s = props.hanging_sign(0.62, 0.48, 0.28)
    xs = -4.65                                           # aan de voorste dwarsbalk, naar het perron gericht
    y_wall = top_at(x_in) - ROOF_THICK / math.cos(L_PITCH) - 0.26
    y_sign = beam_bot + (y_wall - beam_bot) * (xs - (LPOST_X - 0.14)) / (x_in - (LPOST_X - 0.14))
    parts.append(sign.transform(t=(xs, y_sign + 0.01, post_z[0])))
    return mk.merge(*parts), mk.merge(*cols)


# ------------------------------------------------------------------ interieur
def interior():
    parts, cols, loose = [], [], []
    xi0, xi1 = -HW + T, HW - T
    zi0, zi1 = -D + T, -T
    # lambrisering (houten lambrisering onderaan de binnenwanden), met onderbrekingen voor deuren
    for (a, b) in ((xi0, DOOR[0] - 0.08), (DOOR[1] + 0.08, xi1)):
        parts.append(rbox(a, b, FL, FL + 0.82, zi1 - 0.04, zi1, 0.015, "M_Stn_TimberDark"))
    parts.append(rbox(xi0, xi1, FL, FL + 0.82, zi0, zi0 + 0.04, 0.015, "M_Stn_TimberDark"))
    parts.append(rbox(xi1 - 0.04, xi1, FL, FL + 0.82, zi0 + 0.04, zi1 - 0.04, 0.015, "M_Stn_TimberDark"))   # stoot tegen voor/achter
    for (a, b) in ((T + 0.04, SIDE_DOOR[0] - 0.08), (SIDE_DOOR[1] + 0.08, D - T - 0.04)):
        parts.append(rbox(xi0, xi0 + 0.04, FL, FL + 0.82, -b, -a, 0.015, "M_Stn_TimberDark"))
    # loket: L-vormige toonbank met doorgang in de hoek
    za, zb = -1.98, -1.74
    parts.append(rbox(xi0, -1.30, FL, FL + 0.78, za, zb, 0.03, "M_Stn_Timber"))
    parts.append(rbox(xi0, -1.24, FL + 0.78, FL + 0.86, za - 0.06, zb + 0.04, 0.025, "M_Stn_WoodLight"))
    parts.append(rbox(-0.62, -0.38, FL, FL + 0.78, -1.02, zi1, 0.03, "M_Stn_Timber"))
    parts.append(rbox(-0.68, -0.34, FL + 0.78, FL + 0.86, -1.08, zi1, 0.025, "M_Stn_WoodLight"))
    cols += [col_box(xi0, -1.24, FL, FL + 0.86, za - 0.06, zb + 0.04), col_box(-0.68, -0.34, FL, FL + 0.86, -1.08, zi1)]
    # schap met kaartjes op de linkerwand in het loket + krukje
    for y in (FL + 1.25, FL + 1.65):
        parts.append(rbox(xi0, xi0 + 0.30, y - 0.04, y, -1.60, -0.55, 0.015, "M_Stn_WoodLight"))
        for k, z in enumerate(np.arange(-1.50, -0.65, 0.20)):
            col = ("M_Stn_Yellow", "M_Stn_Cream", "M_Stn_NavyLight", "M_Stn_Red")[(k + int(y * 10)) % 4]
            parts.append(rbox(xi0 + 0.04, xi0 + 0.22, y, y + 0.12 + 0.03 * (k % 2), z - 0.07, z + 0.07, 0.015, col))
    stool = [cyl(0.19, 0.06, 14, 0.02, "M_Stn_WoodLight").transform(t=(0, 0.55, 0))]
    for k in range(3):
        a = 2 * math.pi * k / 3
        stool.append(capsule((0.13 * math.cos(a), 0.0, 0.13 * math.sin(a)), (0.07 * math.cos(a), 0.53, 0.07 * math.sin(a)),
                             0.025, mat="M_Stn_Timber", seg=6, rings=1))
    loose += props.loose_prop("Stool_TicketOffice", mk.merge(*stool),
                              col_box(-0.19, 0.19, 0, 0.58, -0.19, 0.19), (-1.55, FL, -0.85))
    # wachtruimte: twee banken, vertrekbord, plant, koffer
    loose += props.loose_prop("Bench_Back", *props.bench(1.8, split_colliders=True), (1.40, FL, zi0 + 0.42))
    loose += props.loose_prop("Bench_Side", *props.bench(1.6, split_colliders=True),
                              (xi1 - 0.42, FL, -3.20), mk.rot_y(-math.pi / 2))
    board, _ = props.timetable()
    # alleen het bord + klokje (zonder poten) aan de achterwand
    keep = [i for i, f in enumerate(board.F) if board.V[list(f), 1].min() > 0.86]
    wall_board = mk.Mesh(board.V, [board.F[i] for i in keep], [board.M[i] for i in keep])
    parts.append(wall_board.transform(t=(-1.55, FL + 0.55, zi0 + 0.10)))
    pot = mk.merge(mk.lathe([(0.0, 0.0), (0.18, 0.0), (0.24, 0.40), (0.0, 0.40)], 12, mat="M_Stn_Timber"),
                   mk.lathe([(0.0, 0.36), (0.22, 0.36), (0.22, 0.38), (0.0, 0.38)], 12, mat="M_Stn_Soil"),
                   props.bush_mass(np.random.default_rng(7), 0.35, 0.35, 0.45, 3).transform(t=(0, 0.30, 0)),
                   props.bush_mass(np.random.default_rng(9), 0.30, 0.30, 0.40, 3).transform(R=mk.rot_y(1.3), t=(0, 0.52, 0)))
    loose += props.loose_prop("PottedPlant_WaitingRoom", pot,
                              col_box(-0.25, 0.25, 0, 0.88, -0.25, 0.25), (xi1 - 0.38, FL, zi0 + 0.38))
    case = props.suitcase(0.56, 0.42, 0.22, "M_Stn_Red", strap="M_Stn_TimberDark")
    loose += props.loose_prop("Suitcase_WaitingRoom", case,
                              col_box(-0.28, 0.28, 0, 0.49, -0.12, 0.12), (0.15, FL, zi0 + 0.55), mk.rot_y(0.3))
    # bindbalken met makelaars naar de nokbalk, en twee hanglampen (eigen knoop 'RoofFrame')
    truss = []
    for z in (-1.80, -3.60):
        truss.append(rbox(-HW, HW, EAVE - 0.05, EAVE + 0.22, z - 0.12, z + 0.12, 0.04, "M_Stn_Timber"))
        truss.append(rbox(-0.10, 0.10, EAVE + 0.22, roof_y_under(0.0), z - 0.10, z + 0.10, 0.03, "M_Stn_Timber"))
        for xs in (-1, 1):
            truss.append(capsule((xs * 0.08, EAVE + 1.30, z), (xs * 1.55, EAVE + 0.20, z), 0.06, mat="M_Stn_TimberDark",
                                 seg=6, rings=1))
        truss.append(capsule((0.65, EAVE - 0.05, z), (0.65, EAVE - 0.52, z), 0.012, mat="M_Stn_Dark", seg=5, rings=1))
        truss.append(props.lantern(1.0).transform(t=(0.65, EAVE - 1.05, z)))
    # nokbalk eindigt 2 cm binnen de topgevels (kopse kant niet in het muurvlak)
    truss.append(rbox(-0.12, 0.12, roof_y_under(0.0) - 0.22, roof_y_under(0.0) + 0.04, -D + 0.02, -0.02, 0.03,
                      "M_Stn_TimberDark"))
    return mk.merge(*parts), mk.merge(*truss), cols, loose


# ------------------------------------------------------------------ gevel-details
def facade():
    parts = []
    # STATION-bord op de bindbalk en de klok in de topgevel
    board, sign = props.sign_nodes("Sign_Name", "STATION", 3.10, 0.70, 0.12, (0.0, EAVE + 0.02, 0.24))
    parts.append(board)
    parts.append(disc(0.68, 0.0, 0.06, "M_Stn_Timber", 28, 0.02).transform(t=(0.0, EAVE + 1.42, 0.0)))
    parts.append(props.clock(0.56, 0.12).transform(t=(0.0, EAVE + 1.42, 0.05)))
    # wandlantaarns naast loket en deur
    wl, _ = props.wall_lantern()
    for x in (-2.62, 2.62):
        parts.append(wl.transform(t=(x, FL + 1.62, 0.0)))
    # bloembak onder het rechterraam (op twee steunen)
    pl, _ = props.planter(1.25, seed=11)
    zc = -(RIGHT_WIN[0] + RIGHT_WIN[1]) / 2
    parts.append(pl.transform(R=mk.rot_y(math.pi / 2), t=(HW + 0.30, RIGHT_WIN[2] - 0.62, zc)))
    for dz in (-0.42, 0.42):
        parts.append(capsule((HW + 0.06, RIGHT_WIN[2] - 0.85, zc + dz), (HW + 0.36, RIGHT_WIN[2] - 0.63, zc + dz), 0.03,
                             mat="M_Stn_Navy", seg=6, rings=1))
    # vensters
    parts.append(_side_window(RIGHT_WIN, HW - T, HW))
    back = window_unit(BACK_WIN[0], BACK_WIN[1], BACK_WIN[2], BACK_WIN[3], -T, 0.0)
    parts.append(back.transform(R=mk.rot_y(math.pi), t=(BACK_WIN[0] + BACK_WIN[1], 0, -D)))
    # rond gevelraampje achter
    ring = slab(circle_loop(0, 0, 0.47, 24), [circle_loop(0, 0, 0.34, 24)], -T - 0.03, 0.06, 0.02, "M_Stn_Timber")
    glass = disc(0.36, -T / 2 - 0.012, -T / 2 + 0.012, "M_Stn_Window", 24, 0.004)
    bars = [rbox(-0.34, 0.34, -0.03, 0.03, -T / 2 - 0.025, -T / 2 + 0.025, 0.01, "M_Stn_Navy"),
            rbox(-0.03, 0.03, -0.34, 0.34, -T / 2 - 0.018, -T / 2 + 0.018, 0.008, "M_Stn_Navy")]     # dunner: kruist
    parts.append(mk.merge(ring, glass, *bars).transform(R=mk.rot_y(math.pi), t=(0, EAVE + 1.25, -D)))
    # kozijn van de zijdeur naar de luifel
    u0, u1, us = SIDE_DOOR
    fr = slab(arch_band(u0, u1, FL - 0.01, us, 0.14, 14), [], -0.02, 0.08, 0.025, "M_Stn_Timber")
    parts.append(fr.transform(s=(1, 1, -1)).transform(R=mk.rot_y(math.pi / 2), t=(-HW, 0, 0)))
    return mk.merge(*parts), sign


def _side_window(win, x0, x1):
    u0, u1, y0, y1 = win
    w = window_unit(u0, u1, y0, y1, x0, x1)          # gebouwd met 'z' = x, 'x' = u
    return w.transform(R=mk.rot_y(math.pi / 2))


# ------------------------------------------------------------------ samenstellen
def build(rng):
    """Geeft de knopenlijst voor export(): zichtbare delen, deur met scharnier, botsvormen, lichtankers."""
    w = walls()
    frame = timber_frame()
    pl = plinth(rng)
    fl = floor()
    tw = ticket_window()
    df = door_frame()
    roof, roof_col = main_roof()
    lean, lean_col = lean_to(rng)
    inter, truss, inter_cols, loose = interior()
    for name, (mesh, colliders), rotation, position in _furniture():
        loose += props.loose_prop(name, mesh, colliders, position, rotation)
    fac, sign = facade()
    leaf, leaf_col = door_leaf()

    cols = [col_box(-HW, HW, 0.0, FL, -D, 0.0)]
    # muren (dicht, behalve deuren; het loket blijft dicht voor de botsing)
    cols += [col_box(-HW, DOOR[0], FL, EAVE, -T, 0.0), col_box(DOOR[1], HW, FL, EAVE, -T, 0.0),
             col_box(DOOR[0], DOOR[1], DOOR[2] + 0.62, EAVE, -T, 0.0),
             col_box(-HW, HW, FL, EAVE, -D, -D + T), col_box(HW - T, HW, FL, EAVE, -D, 0.0),
             col_box(-HW, -HW + T, FL, EAVE, -SIDE_DOOR[0], 0.0), col_box(-HW, -HW + T, FL, EAVE, -D, -SIDE_DOOR[1]),
             col_box(-HW, -HW + T, SIDE_DOOR[2] + 0.58, EAVE, -SIDE_DOOR[1], -SIDE_DOOR[0])]
    cols += [roof_col, lean_col] + inter_cols
    cols.append(col_box(WIN[0] - 0.22, WIN[1] + 0.22, WIN[2] - 0.06, WIN[2] + 0.05, 0.0, 0.34))
    col = mk.merge(*cols)

    hinge = np.array([DOOR[1] - 0.03, FL + 0.005, -T + 0.05])
    door_R = mk.rot_y(-96 * DEG)
    nodes = [
        ("Walls", mk.merge(w, frame), None, (0, 0, 0), None, True),
        ("Plinth", mk.merge(pl, fl), None, (0, 0, 0), None, True),
        ("Openings", mk.merge(tw, df), None, (0, 0, 0), None, True),
        ("Roof", roof, None, (0, 0, 0), None, True),
        ("LeanTo", lean, None, (0, 0, 0), None, True),
        ("Interior", inter, None, (0, 0, 0), None, True),
        ("RoofFrame", truss, None, (0, 0, 0), None, True),
        ("Facade", fac, None, (0, 0, 0), None, True),
        ("Door", leaf, None, tuple(hinge), door_R, True),
        ("Col_Door", leaf_col, "Door", (0, 0, 0), None, False),
        ("Col_Main", col, None, (0, 0, 0), None, False),
        ("Light_Lantern_L", None, None, (-2.62, FL + 1.85, 0.30), None, True),
        ("Light_Lantern_R", None, None, (2.62, FL + 1.85, 0.30), None, True),
        ("Light_Hall_1", None, None, (0.65, EAVE - 0.85, -1.80), None, True),
        ("Light_Hall_2", None, None, (0.65, EAVE - 0.85, -3.60), None, True),
        ("Spawn_Ticket", None, None, (-1.40, FL, -0.75), None, True),
    ] + sign + loose
    return nodes
