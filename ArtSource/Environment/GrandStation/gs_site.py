"""Perrons, overkappingen, loopbrug, entreeluifel, terras en stationsplein van Grand Whistleton.

Stationsruimte: spoor 1 op z = 0, perron 1 (bestaande perronkit) tot z = 5,15, voorterrein tot de gevels,
eilandperron 2|3 tussen z = -1,65 en -7,65 (spoor 2 op z = -9,3), plein aan de +z-kant op maaiveld.
Modules zijn 6 m lang langs x met de pivot in het midden (x = 0) op het hart van spoor 1, zodat ze in stappen
van 6 m aan elkaar passen; kolommen staan in het midden van een travee (geen dubbele delen op de naden).
"""
import math

import numpy as np

from gs_kit import (FL, Boxes, arch_loop, ball, capsule, circle_loop, cyl, disc, mk, rbox, slab, text_mesh, train_icon,
                    tube)
import gs_kit as kit
from gs_building import globe_lamp

TRACK2 = -9.3
ISLAND = (-1.65, -7.65)
APRON = (5.15, 11.0)
APRON_HALL = (5.15, 9.0)
BRIDGE_Y = FL + 4.8                          # loopbrugdek (onderkant ruim boven de treinen)


# ------------------------------------------------------------------ bestrating
def tiles(x0, x1, z0, z1, top, size=1.0, mats=("M_Stn_Paving", "M_Stn_PavingLight"), seed=1, depth=0.12):
    rng = np.random.default_rng(seed)
    parts = []
    for x in np.arange(x0, x1 - 1e-6, size):
        for z in np.arange(z0, z1 - 1e-6, size):
            mat = mats[int(rng.random() < 0.28)]
            parts.append(rbox(x + 0.008, min(x + size, x1) - 0.008, top - depth, top, z + 0.008, min(z + size, z1) - 0.008,
                              0.012, mat))
    return mk.merge(*parts)


def apron(z0, z1, name):
    """Voorterrein tussen perron 1 en de gevels, op perronhoogte, met een goot langs de gevelkant."""
    parts = [rbox(-2.995, 2.995, 0.0, FL - 0.12, z0 + 0.02, z1 - 0.01, 0.0, "M_GS_SandstoneDark"),
             tiles(-3.0, 3.0, z0, z1 - 0.4, FL, 1.0, seed=hash(name) % 97),
             rbox(-3.0, 3.0, FL - 0.14, FL - 0.02, z1 - 0.4, z1, 0.02, "M_Stn_PavingDark")]
    for x in np.arange(-2.75, 3.0, 0.5):                       # roostertje in de goot
        parts.append(rbox(x - 0.06, x + 0.06, FL - 0.03, FL - 0.005, z1 - 0.34, z1 - 0.06, 0.008, "M_GS_Steel"))
    return mk.merge(*parts), Boxes().add(-3.0, 3.0, 0.0, FL, z0, z1)


def island_platform():
    """Eilandperron 6 m breed: zandstenen randen met gele lijn aan beide sporen, tegels ertussen."""
    a, b = ISLAND
    parts = [rbox(-2.995, 2.995, 0.0, FL - 0.12, b + 0.3, a - 0.3, 0.0, "M_GS_SandstoneDark")]
    parts.append(tiles(-3.0, 3.0, b + 0.65, a - 0.65, FL, 1.0, seed=5))
    for z_edge, inward in ((a, -1), (b, 1)):
        zo, zi = sorted((z_edge, z_edge + inward * 0.65))
        parts.append(rbox(-3.0, 3.0, 0.0, FL, zo, zi, 0.03, "M_GS_Sandstone"))
        zl = z_edge + inward * 0.42
        parts.append(rbox(-2.99, 2.99, FL - 0.01, FL + 0.016, zl - 0.07, zl + 0.07, 0.004, "M_GS_Yellow"))
        for x in np.arange(-2.7, 3.0, 0.6):                    # voegen in de rand
            parts.append(rbox(x - 0.02, x + 0.02, FL - 0.012, FL + 0.006, zo + 0.02, zi - 0.02, 0.004, "M_GS_SandstoneDark"))
    return mk.merge(*parts), Boxes().add(-3.0, 3.0, 0.0, FL, b, a)


def island_end():
    """Kop van het eilandperron: hellingbaan (1:6) met leuningen; lokaal loopt de helling naar +x omlaag."""
    a, b = ISLAND
    L = 6.0
    parts = []
    top = [(0.0, FL - 0.04), (L, 0.01), (L, 0.0), (0.0, 0.0)]          # 4 cm onder het loopvlak
    body = slab([(x, y) for x, y in top], [], b + 0.31, a - 0.31, 0.02, "M_GS_SandstoneDark")
    parts.append(body.transform(R=mk.rot_x(0)))
    ang = math.atan2(FL - 0.05, L)
    surf = rbox(0.0, math.hypot(L, FL - 0.05), -0.08, 0.0, b + 0.29, a - 0.29, 0.02, "M_Stn_PavingLight").transform(
        R=mk.rot_z(-ang), t=(0.0, FL, 0))
    parts.append(surf)
    for z in (a - 0.35, b + 0.35):
        rail = [(0.0, FL + 1.0, z), (L, 0.05 + 1.0, z)]
        parts.append(tube(rail, 0.04, "M_GS_Steel", seg=6))
        for x in np.arange(0.3, L, 1.2):
            y = FL - (FL - 0.05) * x / L
            parts.append(capsule((x, y, z), (x, y + 1.0, z), 0.03, mat="M_GS_Steel", seg=5, rings=1))
    boxes = Boxes()
    boxes.append([round(L / 2, 4), round(FL / 2 - 0.15, 4), round((a + b) / 2, 4), round(math.hypot(L, FL), 4), 0.3,
                  round(a - b - 0.6, 4), 0.0, round(-math.degrees(ang), 3)])   # [cx, cy, cz, sx, sy, sz, rx, rz]
    return mk.merge(*parts), boxes


# ------------------------------------------------------------------ perronoverkapping
def canopy_bay(z0, z1, zc):
    """Travee van 6 m: gietijzeren kolom in het midden (x = 0, z = zc) met vier gebogen schoren, licht gebogen
    leisteenblauw dak met felsnaden, marine dakranden met messing lijn, goten, twee globe-lampen."""
    parts, boxes, lights = [], Boxes(), []
    yb = FL + 4.05                                           # onderkant dak aan de randen
    rise = 0.55
    half = (z1 - z0) / 2
    zm = (z0 + z1) / 2
    r = (half ** 2 + rise ** 2) / (2 * rise)
    cy = yb + rise - r

    def under(z):
        return cy + math.sqrt(max(0.0, r * r - (z - zm) ** 2))
    prof_o = [(z, under(z) + 0.14) for z in np.linspace(z0 - 0.15, z1 + 0.15, 16)]
    prof_i = [(z, under(z)) for z in np.linspace(z1 + 0.15, z0 - 0.15, 16)]
    roof = slab([(-z, y) for z, y in prof_o + prof_i], [], -3.0, 3.0, 0.03, "M_GS_Roof").transform(R=mk.rot_y(math.pi / 2))
    parts.append(roof)
    for x in np.arange(-2.625, 3.0, 0.75):                   # felsnaden
        pts = [(x, under(z) + 0.16, z) for z in np.linspace(z0 - 0.1, z1 + 0.1, 12)]
        parts.append(tube(pts, 0.03, "M_GS_RoofDark", seg=4))
    for z, s in ((z0 - 0.15, -1), (z1 + 0.15, 1)):           # dakranden + goot + messing lijn
        y = under(z)
        za, zb = sorted((z, z + s * 0.10))
        parts.append(rbox(-3.0, 3.0, y - 0.32, y + 0.20, za, zb, 0.03, "M_GS_Steel"))
        parts.append(rbox(-3.0, 3.0, y - 0.24, y - 0.19, *sorted((zb if s > 0 else za, (zb if s > 0 else za) + s * 0.02)), 0.005,
                          "M_GS_Brass"))
    # kolom
    parts.append(rbox(-0.32, 0.32, FL, FL + 0.32, zc - 0.32, zc + 0.32, 0.06, "M_GS_SandstoneDark"))
    parts.append(mk.lathe([(0.0, 0.0), (0.26, 0.0), (0.26, 0.12), (0.17, 0.30), (0.15, 0.6), (0.13, 2.7), (0.18, 2.85),
                           (0.24, 3.0), (0.0, 3.0)], 14, mat="M_GS_Steel").transform(t=(0, FL + 0.32, zc)))
    parts.append(mk.torus(0.16, 0.035, 14, 5, mat="M_GS_Brass").transform(t=(0, FL + 0.95, zc)))
    top = FL + 3.32
    for dz in (-1, 1):                                       # schoren naar het dak (langs z) en de langsligger (x)
        z_end = zm + dz * (half - 0.3)
        pts = [(0.0, top - 0.6, zc), (0.0, top + 0.05, zc + dz * 0.6), (0.0, under(z_end) - 0.12, z_end)]
        parts.append(tube(pts, 0.07, "M_GS_Steel", seg=6, smooth=True, n=12))
    for dx in (-1, 1):
        pts = [(0.0, top - 0.4, zc), (dx * 0.6, top + 0.1, zc), (dx * 1.8, under(zc) - 0.24, zc)]
        parts.append(tube(pts, 0.06, "M_GS_Steel", seg=6, smooth=True, n=10))
    parts.append(rbox(-3.0, 3.0, under(zc) - 0.32, under(zc) - 0.06, zc - 0.09, zc + 0.09, 0.03, "M_GS_Steel"))
    rib = [(0.0, under(z) - 0.05, z) for z in np.linspace(z0, z1, 14)]
    parts.append(tube(rib, 0.07, "M_GS_Steel", seg=6))
    for z in (zm - half * 0.55, zm + half * 0.55):
        if abs(z - zc) < 0.8:
            continue
        parts.append(globe_lamp(0.85).transform(t=(0.0, under(z) - 0.04, z)))
        lights.append((0.0, under(z) - 0.9, z))
    boxes.add(-0.3, 0.3, FL, top, zc - 0.3, zc + 0.3)
    boxes.add(-3.0, 3.0, yb - 0.3, yb + rise + 0.2, z0 - 0.15, z1 + 0.15)
    return mk.merge(*parts), boxes, lights


def canopy_side():
    return canopy_bay(1.95, 9.0, 5.6)


def canopy_island():
    zm = (ISLAND[0] + ISLAND[1]) / 2
    return canopy_bay(ISLAND[1] + 0.3, ISLAND[0] - 0.3, zm)


# ------------------------------------------------------------------ loopbrug
def bridge_landing():
    """Bordes 3 x 3 m op vier kolommen, op dekhoogte; lokaal midden, pivot op het perron (y = 0 = grond)."""
    parts, boxes = [], Boxes()
    h = 1.5
    parts.append(rbox(-h, h, BRIDGE_Y - 0.4, BRIDGE_Y, -h, h, 0.05, "M_GS_Steel"))
    parts.append(tiles(-h + 0.05, h - 0.05, -h + 0.05, h - 0.05, BRIDGE_Y + 0.02, 0.5, ("M_GS_Wood", "M_GS_WoodDark")))
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * (h - 0.2), sz * (h - 0.2)
            parts.append(rbox(x - 0.14, x + 0.14, FL, BRIDGE_Y - 0.4, z - 0.14, z + 0.14, 0.04, "M_GS_Steel"))
            parts.append(rbox(x - 0.25, x + 0.25, FL, FL + 0.25, z - 0.25, z + 0.25, 0.05, "M_GS_SandstoneDark"))
            boxes.add(x - 0.14, x + 0.14, FL, BRIDGE_Y, z - 0.14, z + 0.14)
            parts.append(capsule((x, BRIDGE_Y, z), (x, BRIDGE_Y + 2.6, z), 0.08, mat="M_GS_Steel", seg=8, rings=1))
    # dakje (kruisgewelf-achtig: vier boogjes), leuningen op de vrije zijden komen van de modules ernaast
    roof = mk.lathe([(0.0, 0.0), (1.0, 0.0), (0.85, 0.35), (0.5, 0.62), (0.0, 0.72)], 4, (h + 0.25) * math.sqrt(2),
                    (h + 0.25) * math.sqrt(2), 2.0, mat="M_GS_Roof", phase=math.pi / 4)
    parts.append(roof.transform(t=(0, BRIDGE_Y + 2.63, 0)))
    parts.append(ball(0.18, "M_GS_Brass", 12, 7).transform(t=(0, BRIDGE_Y + 3.45, 0)))
    parts.append(globe_lamp(0.6).transform(t=(0, BRIDGE_Y + 2.55, 0)))
    boxes.add(-h, h, BRIDGE_Y - 0.4, BRIDGE_Y, -h, h)
    return mk.merge(*parts), boxes, [("Light_Landing", (0, BRIDGE_Y + 1.6, 0))]


def bridge_stair():
    """Trap van het perron naar het bordes, lokaal langs +z omhoog (z 0..run), breedte 2,4 m (x -1,2..1,2)."""
    parts, boxes = [], Boxes()
    rise_total = BRIDGE_Y - FL
    n = 26
    rh = rise_total / n
    tr = 0.30
    run = n * tr
    w = 1.2
    for k in range(n):
        y = FL + (k + 1) * rh
        parts.append(rbox(-w, w, y - 0.07, y, k * tr, (k + 1) * tr + 0.02, 0.02, "M_GS_Wood"))
        parts.append(rbox(-w + 0.05, w - 0.05, y - rh - 0.02, y - 0.07, (k + 1) * tr - 0.04, (k + 1) * tr, 0.01, "M_GS_Steel"))
    ang = math.atan2(rise_total, run)
    Ls = math.hypot(run, rise_total)
    for x in (-w - 0.08, w + 0.08):                          # bomen (stringers) en leuningen
        st = rbox(x - 0.07, x + 0.07, -0.2, 0.25, 0.0, Ls, 0.03, "M_GS_Steel").transform(R=mk.rot_x(-ang), t=(0, FL, 0))
        parts.append(st)
        parts.append(tube([(x, FL + 1.0, 0.0), (x, BRIDGE_Y + 1.0, run)], 0.045, "M_GS_Brass", seg=6))
        for k in range(0, n + 1, 4):
            z = k * tr
            y = FL + k * rh
            parts.append(capsule((x, y + 0.1, z), (x, y + 1.0, z), 0.03, mat="M_GS_Steel", seg=5, rings=1))
    parts.append(rbox(-w - 0.2, w + 0.2, 0.0, FL + 0.6, run - 0.3, run + 0.3, 0.05, "M_GS_SandstoneDark"))   # poer
    boxes.append([0.0, round(FL + rise_total / 2 - 0.15, 4), round(run / 2, 4), round(2 * w, 4), 0.3, round(Ls, 4),
                  round(-math.degrees(ang), 3)])
    return mk.merge(*parts), boxes, run


def bridge_span(length):
    """Overdekte loopbrug langs z (0..length): dek, vakwerkwanden met glas, gewelfd leisteendak, lampen."""
    parts, boxes, lights = [], Boxes(), []
    w = 1.5
    parts.append(rbox(-w, w, BRIDGE_Y - 0.45, BRIDGE_Y, 0.0, length, 0.04, "M_GS_Steel"))
    parts.append(tiles(-w + 0.08, w - 0.08, 0.0, length, BRIDGE_Y + 0.02, 0.5, ("M_GS_Wood", "M_GS_WoodDark"), seed=3, depth=0.05))
    H = 2.6
    for x in (-w, w):
        parts.append(rbox(x - 0.08, x + 0.08, BRIDGE_Y - 0.1, BRIDGE_Y + 0.9, 0.0, length, 0.03, "M_GS_Steel"))
        parts.append(rbox(x - 0.07, x + 0.07, BRIDGE_Y + H - 0.2, BRIDGE_Y + H, 0.0, length, 0.03, "M_GS_Steel"))
        nb = max(2, int(round(length / 1.6)))
        for k in range(nb + 1):
            z = length * k / nb
            parts.append(rbox(x - 0.06, x + 0.06, BRIDGE_Y + 0.9, BRIDGE_Y + H - 0.2, z - 0.06, z + 0.06, 0.02, "M_GS_Steel"))
            if k < nb:
                z1 = length * (k + 1) / nb
                parts.append(capsule((x, BRIDGE_Y - 0.05, z), (x, BRIDGE_Y + 0.85, z1), 0.04, mat="M_GS_Steel", seg=5, rings=1))
                parts.append(rbox(x - 0.012, x + 0.012, BRIDGE_Y + 0.95, BRIDGE_Y + H - 0.25, z + 0.07, z1 - 0.07, 0.0,
                                  "M_GS_Glass"))
        parts.append(tube([(x * 0.93, BRIDGE_Y + 1.0, 0.0), (x * 0.93, BRIDGE_Y + 1.0, length)], 0.04, "M_GS_Brass", seg=6))
    roof = slab(arch_loop(-w - 0.25, w + 0.25, 0.0, 0.0, 12), [arch_loop(-w - 0.1, w + 0.1, -0.001, 0.0, 12)], -0.0, length, 0.03,
                "M_GS_Roof")
    parts.append(roof.transform(t=(0, BRIDGE_Y + H, 0)))
    for z in np.arange(1.0, length, 2.0):
        parts.append(globe_lamp(0.5).transform(t=(0, BRIDGE_Y + H + 0.9 * 1.0 - 0.2, z)))
        lights.append(("Light_Span_%d" % (len(lights) + 1), (0, BRIDGE_Y + H - 0.6, z)))
    boxes.add(-w, w, BRIDGE_Y - 0.45, BRIDGE_Y, 0.0, length)
    boxes.add(-w - 0.1, -w + 0.1, BRIDGE_Y, BRIDGE_Y + H, 0.0, length)
    boxes.add(w - 0.1, w + 0.1, BRIDGE_Y, BRIDGE_Y + H, 0.0, length)
    return mk.merge(*parts), boxes, lights


# ------------------------------------------------------------------ entreeluifel (pleinkant)
def entrance_canopy_bay():
    """Travee 6 m langs de gevel (lokaal x -3..3), gevel op z = 0, voorkant z = 4,2. Licht gebogen leisteendak,
    marine dakrand met messing lijn en een gietijzeren kolom op x = +3 (de begin-kolom is een eigen module)."""
    parts, boxes, lights = [], Boxes(), []
    D = 4.2
    yb, yf = FL + 5.30, FL + 4.85

    def under(z):
        t = z / D
        return yb + (yf - yb) * t + 0.35 * math.sin(math.pi * t)
    prof = [(z, under(z)) for z in np.linspace(-0.05, D + 0.2, 14)]
    poly = [(-z, y + 0.13) for z, y in prof] + [(-z, y) for z, y in reversed(prof)]
    parts.append(slab(poly, [], -3.0, 3.0, 0.03, "M_GS_Roof").transform(R=mk.rot_y(math.pi / 2)))
    for x in np.arange(-2.625, 3.0, 0.75):
        parts.append(tube([(x, under(z) + 0.15, z) for z in np.linspace(0.05, D + 0.15, 10)], 0.03, "M_GS_RoofDark", seg=4))
    y = under(D + 0.2)
    parts.append(rbox(-2.99, 2.99, y - 0.45, y + 0.12, D + 0.12, D + 0.30, 0.03, "M_GS_Steel"))
    parts.append(rbox(-2.99, 2.99, y - 0.36, y - 0.30, D + 0.30, D + 0.33, 0.005, "M_GS_Brass"))
    parts.append(rbox(-2.99, 2.99, yb - 0.25, yb + 0.05, -0.08, 0.22, 0.03, "M_GS_Steel"))          # muurligger
    parts.append(rbox(-0.08, 0.08, under(D * 0.5) - 0.24, under(D * 0.5) - 0.02, 0.2, D + 0.15, 0.02, "M_GS_Steel"))
    parts.append(globe_lamp(0.7).transform(t=(0.0, under(D * 0.55) - 0.02, D * 0.55)))
    lights.append(("Light_Porch", (0.0, under(D * 0.55) - 0.9, D * 0.55)))
    col, cb = entrance_column()
    parts.append(col.transform(t=(3.0, 0, 0)))
    boxes.extend(cb.placed(0, (3.0, 0, 0)))
    boxes.add(-3.0, 3.0, yf - 0.4, yb + 0.5, 0.0, D + 0.3)
    return mk.merge(*parts), boxes, lights


def entrance_column():
    D = 4.2
    yf = FL + 4.85
    z = D - 0.05
    parts = [rbox(-0.34, 0.34, FL, FL + 0.38, z - 0.34, z + 0.34, 0.06, "M_GS_SandstoneDark"),
             mk.lathe([(0.0, 0.0), (0.28, 0.0), (0.28, 0.12), (0.18, 0.30), (0.16, 0.6), (0.14, yf - FL - 0.9), (0.20, yf - FL - 0.75),
                       (0.27, yf - FL - 0.55), (0.0, yf - FL - 0.55)], 14, mat="M_GS_Steel").transform(t=(0, FL + 0.38, z)),
             mk.torus(0.17, 0.035, 14, 5, mat="M_GS_Brass").transform(t=(0, FL + 1.0, z))]
    if True:
        parts.append(tube([(0.0, yf - 0.6, z), (0.0, yf - 0.1, z - 0.5), (0.0, yf + 0.25, z - 1.6)], 0.06, "M_GS_Steel", seg=6,
                          smooth=True, n=10))
    for dx in (-1, 1):
        parts.append(tube([(0.0, yf - 0.5, z), (dx * 0.5, yf - 0.05, z), (dx * 1.4, yf + 0.0, z)], 0.05, "M_GS_Steel", seg=6,
                          smooth=True, n=10))
    return mk.merge(*parts), Boxes().add(-0.3, 0.3, FL, yf, z - 0.3, z + 0.3)


# ------------------------------------------------------------------ terras + plein
def forecourt():
    """Verhoogd entreeterras op vloerhoogte langs de pleingevels, brede trappen naar het plein, hellingbanen opzij.
    In stationsruimte (pivot later 0, 0, 0)."""
    parts, boxes = [], Boxes()
    front = 35.5
    poly = [(-33.0, 30.0), (-24.0, 30.0), (-24.0, 29.0), (-12.0, 29.0), (-12.0, 31.0), (12.0, 31.0), (12.0, 29.0), (24.0, 29.0),
            (24.0, 30.0), (33.0, 30.0), (33.0, front), (-33.0, front)]
    import station_platform as sp
    parts.append(sp.plan_slab(poly, 0.0, FL - 0.10, 0.03, "M_GS_SandstoneDark"))
    # tegels in strookjes langs de gevels
    for (x0, x1, z0) in ((-33.0, -24.0, 30.0), (-24.0, -12.0, 29.0), (-12.0, 12.0, 31.0), (12.0, 24.0, 29.0), (24.0, 33.0, 30.0)):
        parts.append(tiles(x0, x1, z0, front, FL, 1.2, ("M_GS_TilesLight", "M_GS_Sandstone"), seed=int(x0) + 50, depth=0.1))
        boxes.add(x0, x1, 0.0, FL, z0, front)
    # trappen over de volle breedte
    n, rh, tr = 6, FL / 6, 0.36
    for k in range(n):
        y = FL - k * rh
        parts.append(rbox(-33.0, 33.0, y - rh - 0.02, y - 0.0 if k else y, front + k * tr - 0.01, front + (k + 1) * tr, 0.03,
                          "M_GS_Sandstone" if k % 2 == 0 else "M_GS_SandstoneDark"))
    run = n * tr
    ang = math.atan2(FL, run)
    boxes.append([0.0, round(FL / 2 - 0.2, 4), round(front + run / 2, 4), 66.0, 0.3, round(math.hypot(run, FL), 4),
                  round(math.degrees(ang), 3)])
    # bolders langs de trap, plantenbakken op de hoeken
    for x in np.arange(-31.5, 32.0, 3.0):
        if abs(x) < 1.0:
            continue
        parts.append(bollard().transform(t=(x, 0.0, front + run + 0.6)))
    return mk.merge(*parts), boxes


def bollard():
    return mk.merge(mk.lathe([(0.0, 0.0), (0.16, 0.0), (0.15, 0.55), (0.12, 0.66), (0.0, 0.70)], 12, mat="M_GS_Steel"),
                    mk.torus(0.15, 0.025, 12, 4, mat="M_GS_Brass").transform(t=(0, 0.48, 0)),
                    ball(0.07, "M_GS_Brass", 8, 5).transform(t=(0, 0.72, 0)))


def square():
    """Stationsplein op maaiveld: bestrating, rotonde met bloemeneiland, rijbaan naar de zijkanten. Stationsruimte."""
    parts, boxes = [], Boxes()
    z0, z1, x0, x1 = 37.7, 66.0, -46.0, 46.0
    cz, R_in, R_out = 54.0, 6.0, 11.0
    # bestrating: grote tegels, behalve op de rijbaan
    rng = np.random.default_rng(9)
    S = 2.0
    for x in np.arange(x0, x1 - 1e-6, S):
        for z in np.arange(z0, z1 - 1e-6, S):
            xc, zc_ = x + S / 2, z + S / 2
            d = math.hypot(xc, zc_ - cz)
            if d < R_out + 0.8 or (abs(zc_ - cz) < 4.2 and abs(xc) > R_out - 1):
                continue
            mat = "M_GS_Tiles" if rng.random() < 0.12 else "M_GS_TilesLight"
            parts.append(rbox(x + 0.01, x + S - 0.01, -0.02, 0.06, z + 0.01, z + S - 0.01, 0.02, mat))
    # rijbaan: ring + weg naar links en rechts, met stoeprand
    ring = slab(circle_loop(0, 0, R_out + 0.8, 48), [circle_loop(0, 0, R_in, 40)], -0.03, 0.03, 0.01, "M_GS_Asphalt")
    parts.append(ring.transform(R=mk.rot_x(-math.pi / 2), t=(0, 0, cz)))
    for sx in (-1, 1):
        a, b = sorted((sx * (R_out - 1.0), sx * x1))
        parts.append(rbox(a, b, -0.035, 0.022, cz - 4.2, cz + 4.2, 0.01, "M_GS_Asphalt"))   # 8 mm onder de ring
        for zz in (cz - 4.2, cz + 4.2):
            parts.append(rbox(a + 0.03, b - 0.03, -0.01, 0.092, zz - 0.15, zz + 0.15, 0.03,
                              "M_GS_SandstoneDark"))
    curb = slab(circle_loop(0, 0, R_out + 0.95, 48), [circle_loop(0, 0, R_out + 0.8, 48)], 0.0, 0.10, 0.01, "M_GS_SandstoneDark")
    parts.append(curb.transform(R=mk.rot_x(-math.pi / 2), t=(0, 0, cz)))
    # middeneiland: stenen rand, gras, hagen, bloemen, lantaarn
    edge = slab(circle_loop(0, 0, R_in, 40), [circle_loop(0, 0, R_in - 0.35, 40)], 0.0, 0.40, 0.04, "M_GS_Sandstone")
    parts.append(edge.transform(R=mk.rot_x(-math.pi / 2), t=(0, 0, cz)))
    parts.append(disc(R_in - 0.35, 0.0, 0.32, "M_GS_Leaf", 40, 0.02).transform(R=mk.rot_x(-math.pi / 2), t=(0, 0, cz)))
    for k in range(10):
        a = 2 * math.pi * k / 10
        rr = R_in - 1.4
        hedge = mk.ellipsoid(0.85, 0.65, 0.85, 12, 7, mat="M_GS_LeafDark")
        parts.append(hedge.transform(t=(rr * math.cos(a), 0.55, cz + rr * math.sin(a))))
    for k in range(40):
        a = rng.uniform(0, 2 * math.pi)
        rr = rng.uniform(1.2, R_in - 0.6)
        p = (rr * math.cos(a), 0.36, cz + rr * math.sin(a))
        parts.append(ball(0.12, "M_GS_Flower" if k % 3 else "M_GS_Yellow", 8, 5).transform(t=p))
    boxes.add(-R_in, R_in, 0.0, 0.4, cz - R_in, cz + R_in)
    return mk.merge(*parts), boxes


def street_lamp():
    """Dubbele straatlantaarn: zandstenen voet, marine paal met messing ringen, twee lantaarns aan een dwarsarm."""
    H = 4.2
    parts = [rbox(-0.3, 0.3, 0.0, 0.5, -0.3, 0.3, 0.06, "M_GS_SandstoneDark"),
             mk.lathe([(0.0, 0.0), (0.2, 0.0), (0.2, 0.25), (0.11, 0.5), (0.09, H - 0.6), (0.13, H - 0.45), (0.0, H - 0.4)], 12,
                      mat="M_GS_Steel").transform(t=(0, 0.5, 0)),
             mk.torus(0.12, 0.03, 12, 4, mat="M_GS_Brass").transform(t=(0, 1.6, 0))]
    for sx in (-1, 1):
        parts.append(tube([(0, H - 0.2, 0), (sx * 0.4, H + 0.05, 0), (sx * 0.62, H - 0.05, 0)], 0.04, "M_GS_Steel", seg=6, smooth=True, n=8))
        parts.append(kit.sc.lantern(1.1, "M_GS_Steel", "M_GS_Lamp").transform(t=(sx * 0.62, H - 0.62, 0)))
    parts.append(ball(0.09, "M_GS_Brass", 8, 5).transform(t=(0, H + 0.2, 0)))
    return mk.merge(*parts), Boxes().add(-0.3, 0.3, 0.0, H, -0.3, 0.3), [("Light_Lamp_L", (-0.62, H - 0.4, 0)),
                                                                        ("Light_Lamp_R", (0.62, H - 0.4, 0))]
