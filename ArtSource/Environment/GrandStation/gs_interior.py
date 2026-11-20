"""Interieur- en bordmodules van Grand Whistleton (nieuw; de bestaande stationsprops worden hergebruikt).

Alle modules: pivot midden onder (vloer = y FL, dus pivot op y = 0 met de vloer op FL zoals de rest), voorkant +z.
Borden met aanpasbare tekst zijn Sign_*-knopen (StationSign in Unity), geen vaste letters in het model.
"""
import math

import numpy as np

from gs_kit import (FL, Boxes, arch_loop, ball, capsule, circle_loop, clock, cyl, disc, mk, rbox, rounded_rect_loop, slab,
                    text_mesh, train_icon, tube)
import gs_kit as kit
import station_props as props
import main_station_props as mp


def sign(name, text, w, h, pivot, R=None):
    return props.sign_nodes(name, text, w, h, 0.10, pivot, R)


# ------------------------------------------------------------------ loketten
def ticket_counter(L=9.0):
    """Lange loketbalie (tegen een wand, achterkant z = 0, publiek aan +z): zandstenen voet, houten balie met
    panelen, drie loketten met glazen ruiten en messing spreekroosters, houten kroonlijst met bord KAARTJES."""
    parts, boxes = [], Boxes()
    D = 2.4                                                    # wand -> voorkant balie (personeel erachter)
    zf = D
    parts.append(rbox(-L / 2, L / 2, FL, FL + 0.15, zf - 0.75, zf, 0.03, "M_GS_SandstoneDark"))
    parts.append(rbox(-L / 2, L / 2, FL + 0.15, FL + 1.05, zf - 0.70, zf - 0.04, 0.04, "M_GS_Wood"))
    for x in np.arange(-L / 2 + 0.5, L / 2, 1.0):              # panelen
        parts.append(rbox(x - 0.36, x + 0.36, FL + 0.32, FL + 0.90, zf - 0.05, zf - 0.01, 0.02, "M_GS_WoodDark"))
    parts.append(rbox(-L / 2 - 0.05, L / 2 + 0.05, FL + 1.05, FL + 1.12, zf - 0.85, zf + 0.10, 0.02, "M_GS_Sandstone"))
    # bovenbouw: stijlen, ruiten met boogje, spreekrooster en lade per loket
    windows = (-3.0, 0.0, 3.0)
    zg = zf - 0.45
    for x, w in ((-L / 2 + 0.15, 0.15), (-1.5, 0.25), (1.5, 0.25), (L / 2 - 0.15, 0.15)):   # stijlen tussen de loketten
        parts.append(rbox(x - w, x + w, FL + 1.12, FL + 2.75, zg - 0.08, zg + 0.08, 0.03, "M_GS_Wood"))
    for x in windows:
        parts.append(slab(arch_loop(x - 1.25, x + 1.25, FL + 1.25, FL + 2.25, 12), [], zg - 0.01, zg + 0.01, 0.004, "M_GS_Glass"))
        parts.append(disc(0.12, zg + 0.012, zg + 0.03, "M_GS_Brass", 14, 0.005).transform(t=(x, FL + 1.55, 0)))
        parts.append(rbox(x - 0.35, x + 0.35, FL + 1.12, FL + 1.22, zg - 0.05, zg + 0.25, 0.02, "M_GS_Brass"))
        parts.append(train_icon(0.28, 0.02, "M_GS_Brass").transform(t=(x, FL + 2.05, zg + 0.03)))
    parts.append(rbox(-L / 2 - 0.1, L / 2 + 0.1, FL + 2.75, FL + 3.10, zg - 0.25, zg + 0.30, 0.04, "M_GS_Wood"))
    parts.append(rbox(-L / 2 - 0.15, L / 2 + 0.15, FL + 3.10, FL + 3.22, zg - 0.30, zg + 0.38, 0.03, "M_GS_WoodDark"))
    board, nodes = sign("Sign_Tickets", "KAARTJES", 4.2, 0.62, (0.0, FL + 3.65, zg + 0.22))
    parts.append(board)
    for x in (-1.6, 1.6):
        parts.append(capsule((x, FL + 3.22, zg + 0.1), (x, FL + 3.35, zg + 0.1), 0.03, mat="M_GS_Steel", seg=5, rings=1))
    # personeelskant: werkblad, krukken, kasten tegen de wand
    parts.append(rbox(-L / 2 + 0.05, L / 2 - 0.05, FL + 0.75, FL + 0.82, zg - 0.65, zg - 0.05, 0.02, "M_GS_Wood"))
    for x in windows:
        parts.append(rbox(x - 0.2, x + 0.2, FL, FL + 0.6, zg - 1.2, zg - 0.8, 0.05, "M_GS_WoodDark"))
    parts.append(rbox(-L / 2, L / 2, FL, FL + 2.2, 0.05, 0.55, 0.04, "M_GS_Wood"))
    for x in np.arange(-L / 2 + 0.5, L / 2, 1.0):
        parts.append(rbox(x - 0.4, x + 0.4, FL + 0.2, FL + 2.0, 0.55, 0.58, 0.02, "M_GS_WoodDark"))
    boxes.add(-L / 2, L / 2, FL, FL + 2.75, zf - 0.85, zf + 0.1)
    boxes.add(-L / 2, L / 2, FL, FL + 2.2, 0.05, 0.6)
    anchors = [("Spot_TicketWindow_%d" % (k + 1), (x, FL, zf + 0.6), None) for k, x in enumerate(windows)]
    return mk.merge(*parts), boxes, anchors, nodes


def info_desk():
    """Ronde informatiebalie met een paal en een rond bord met 'i'-teken en klokje erop."""
    parts, boxes = [], Boxes()
    r = 1.3
    ring = slab(circle_loop(0, 0, r, 32), [circle_loop(0, 0, r - 0.55, 28)], 0.0, 1.05, 0.04, "M_GS_Wood")
    parts.append(ring.transform(R=mk.rot_x(-math.pi / 2), t=(0, FL, 0)))
    top = slab(circle_loop(0, 0, r + 0.1, 32), [circle_loop(0, 0, r - 0.65, 28)], 0.0, 0.07, 0.02, "M_GS_Sandstone")
    parts.append(top.transform(R=mk.rot_x(-math.pi / 2), t=(0, FL + 1.05, 0)))
    for k in range(10):
        a = 2 * math.pi * (k + 0.5) / 10
        p = (r * math.cos(a), r * math.sin(a))
        parts.append(rbox(-0.25, 0.25, 0.15, 0.9, -0.02, 0.02, 0.01, "M_GS_WoodDark")
                     .transform(R=mk.rot_y(-a + math.pi / 2), t=(p[0], FL, p[1])))
    parts.append(cyl(0.07, 2.4, 10, 0.0, "M_GS_Steel", axis="y").transform(t=(0, FL + 1.2 + 1.05, 0)))
    disc_ = mk.merge(disc(0.55, -0.05, 0.05, "M_GS_Steel", 28, 0.02), disc(0.48, 0.05, 0.08, "M_GS_Cream", 28, 0.01),
                     disc(0.48, -0.08, -0.05, "M_GS_Cream", 28, 0.01))
    for sgn in (1, -1):
        disc_ = mk.merge(disc_, rbox(-0.06, 0.06, -0.25, 0.12, 0.0, 0.03, 0.01, "M_GS_Steel").transform(t=(0, 0, 0.09 if sgn > 0 else -0.12)),
                         ball(0.075, "M_GS_Steel", 10, 6).transform(t=(0, 0.26, sgn * 0.10)))
    parts.append(disc_.transform(t=(0, FL + 3.85, 0)))
    parts.append(mk.merge(clock(0.28, 0.05, rim_mat="M_GS_Brass", face_mat="M_GS_Cream", hand_mat="M_GS_Steel"),
                          clock(0.28, 0.05, rim_mat="M_GS_Brass", face_mat="M_GS_Cream", hand_mat="M_GS_Steel").transform(R=mk.rot_y(math.pi)))
                 .transform(t=(0, FL + 3.05, 0)))
    boxes.add(-r, r, FL, FL + 1.1, -r, r)
    return mk.merge(*parts), boxes, [("Spot_InfoDesk", (0, FL, r + 0.6), None)], []


# ------------------------------------------------------------------ kiosk + café
def kiosk():
    """Vrijstaande koffiekiosk: houten kast met zandstenen voet, balie aan de voorkant (+z), gestreepte luifel,
    koffiemachine, taartvitrine, bord KOFFIE. 3,2 x 2,2 m."""
    parts, boxes = [], Boxes()
    W, D, H = 3.2, 2.2, 2.7
    parts.append(rbox(-W / 2 + 0.01, W / 2 - 0.01, FL, FL + 0.18, -D / 2 + 0.01, D / 2 - 0.01, 0.04, "M_GS_SandstoneDark"))
    for sx in (-1, 1):
        parts.append(rbox(sx * W / 2 - 0.12, sx * W / 2 + 0.12, FL, FL + H, -D / 2, D / 2, 0.04, "M_GS_Wood"))
    parts.append(rbox(-W / 2 + 0.01, W / 2 - 0.01, FL + 0.19, FL + H - 0.005, -D / 2 + 0.02, -D / 2 + 0.12, 0.03, "M_GS_WoodDark"))
    parts.append(rbox(-W / 2 + 0.12, W / 2 - 0.12, FL + 0.18, FL + 1.05, D / 2 - 0.45, D / 2, 0.03, "M_GS_Wood"))
    for x in (-0.8, 0.0, 0.8):
        parts.append(rbox(x - 0.32, x + 0.32, FL + 0.32, FL + 0.92, D / 2 - 0.01, D / 2 + 0.02, 0.02, "M_GS_WoodDark"))
    parts.append(rbox(-W / 2 + 0.05, W / 2 - 0.05, FL + 1.05, FL + 1.12, D / 2 - 0.55, D / 2 + 0.12, 0.02, "M_GS_Sandstone"))
    parts.append(rbox(-W / 2 - 0.1, W / 2 + 0.1, FL + H, FL + H + 0.25, -D / 2 - 0.1, D / 2 + 0.1, 0.05, "M_GS_WoodDark"))
    # luifel: banen in roomwit en marine
    n = 8
    for k in range(n):
        x0 = -W / 2 - 0.15 + (W + 0.3) * k / n
        x1 = -W / 2 - 0.15 + (W + 0.3) * (k + 1) / n
        mat = "M_GS_Cream" if k % 2 == 0 else "M_GS_Steel"
        poly = [(-D / 2, FL + H - 0.02), (-D / 2 - 0.95, FL + H - 0.45), (-D / 2 - 0.95, FL + H - 0.51), (-D / 2, FL + H - 0.08)]
        parts.append(slab(poly, [], x0, x1, 0.01, mat).transform(R=mk.rot_y(math.pi / 2)))
        parts.append(ball(0.06, mat, 8, 5).transform(t=((x0 + x1) / 2, FL + H - 0.58, D / 2 + 0.95)))
    # inhoud: koffiemachine, mokken, taartvitrine
    parts.append(mp.espresso_machine().transform(R=mk.rot_y(math.pi), t=(-0.6, FL + 1.12, D / 2 - 0.9)))
    for k, x in enumerate((0.3, 0.55, 0.8)):
        parts.append(mp.mug("M_GS_Cream" if k % 2 else "M_GS_Red").transform(t=(x, FL + 1.12, D / 2 - 0.15)))
    dome = mk.lathe([(0.0, 0.0), (0.26, 0.0), (0.26, 0.04), (0.24, 0.22), (0.14, 0.32), (0.0, 0.34)], 16, mat="M_GS_Glass")
    parts.append(dome.transform(t=(1.05, FL + 1.12, D / 2 - 0.25)))
    parts.append(disc(0.17, 0.0, 0.08, "M_GS_Brass", 14, 0.02).transform(R=mk.rot_x(-math.pi / 2), t=(1.05, FL + 1.12, D / 2 - 0.25)))
    board, nodes = sign("Sign_Kiosk", "KOFFIE", 2.2, 0.5, (0.0, FL + H + 0.55, D / 2 + 0.12))
    parts.append(board)
    boxes.add(-W / 2, W / 2, FL, FL + H + 0.25, -D / 2, D / 2)
    return mk.merge(*parts), boxes, [("Spot_KioskCounter", (0, FL, D / 2 + 0.7), None)], nodes


def cafe_table():
    """Rond tafeltje met twee stoeltjes (gietijzer + hout)."""
    parts = [mk.lathe([(0.0, 0.0), (0.28, 0.0), (0.26, 0.04), (0.05, 0.12), (0.04, 0.70), (0.0, 0.72)], 12, mat="M_GS_Steel")
             .transform(t=(0, FL, 0)),
             disc(0.42, 0.0, 0.05, "M_GS_Sandstone", 22, 0.015).transform(R=mk.rot_x(-math.pi / 2), t=(0, FL + 0.72, 0))]
    for sx in (-1, 1):
        x = sx * 0.75
        parts.append(rbox(x - 0.22, x + 0.22, FL + 0.44, FL + 0.50, -0.22, 0.22, 0.02, "M_GS_Wood"))
        parts.append(rbox(x + sx * 0.18, x + sx * 0.24, FL + 0.50, FL + 0.95, -0.2, 0.2, 0.02, "M_GS_Wood"))
        for dx in (-0.17, 0.17):
            for dz in (-0.17, 0.17):
                parts.append(capsule((x + dx, FL, dz), (x + dx, FL + 0.45, dz), 0.022, mat="M_GS_Steel", seg=5, rings=1))
    parts.append(mp.mug("M_GS_Cream").transform(t=(0.1, FL + 0.77, 0.05)))
    return mk.merge(*parts), Boxes().add(-1.0, 1.0, FL, FL + 0.8, -0.45, 0.45), [], []


# ------------------------------------------------------------------ klok, vertrekbord
def pedestal_clock():
    """Stationsklok op een gecanneleerde kolom: vier wijzerplaten, messing ringen, zandstenen voet (concept)."""
    parts = [mk.lathe([(0.0, 0.0), (0.75, 0.0), (0.75, 0.25), (0.6, 0.35), (0.55, 0.6), (0.0, 0.6)], 8, mat="M_GS_Sandstone",
                      phase=math.pi / 8).transform(t=(0, FL, 0))]
    H = 3.0
    parts.append(mk.lathe([(0.0, 0.0), (0.26, 0.0), (0.24, 0.12), (0.18, 0.3), (0.16, H - 0.4), (0.24, H - 0.2), (0.3, H),
                           (0.0, H)], 16, mat="M_GS_Steel").transform(t=(0, FL + 0.6, 0)))
    for k in range(8):
        a = 2 * math.pi * k / 8
        parts.append(capsule((0.17 * math.cos(a), FL + 1.0, 0.17 * math.sin(a)), (0.15 * math.cos(a), FL + H + 0.1, 0.15 * math.sin(a)),
                             0.025, mat="M_GS_SteelLight", seg=4, rings=1))
    for y in (FL + 1.2, FL + H + 0.3):
        parts.append(mk.torus(0.21, 0.035, 16, 5, mat="M_GS_Brass").transform(t=(0, y, 0)))
    yc = FL + 0.6 + H + 0.75
    box = rbox(-0.72, 0.72, yc - 0.75, yc + 0.75, -0.72, 0.72, 0.18, "M_GS_Steel", div=2)
    parts.append(box)
    for q in range(4):
        R = mk.rot_y(q * math.pi / 2)
        parts.append(clock(0.6, 0.10, rim_mat="M_GS_Brass", face_mat="M_GS_Cream", hand_mat="M_GS_Steel", hour=-1.1)
                     .transform(t=(0, 0, 0.70)).transform(R=R, t=(0, yc, 0)))
    parts.append(mk.lathe([(0.0, 0.0), (0.55, 0.0), (0.2, 0.4), (0.0, 0.55)], 4, mat="M_GS_Steel", phase=math.pi / 4)
                 .transform(t=(0, yc + 0.75, 0)))
    parts.append(ball(0.16, "M_GS_Brass", 12, 7).transform(t=(0, yc + 1.35, 0)))
    return mk.merge(*parts), Boxes().add(-0.75, 0.75, FL, FL + 0.6, -0.75, 0.75).add(-0.3, 0.3, FL, yc + 1.4, -0.3, 0.3), \
        [("Spot_MeetingPoint", (0, FL, 1.6), None)], []


def departure_board(w=7.0, h=2.3, rows=6, seed=3):
    """Groot dubbelzijdig split-flapbord (hangt aan stangen): marine kast, messing rand, VERTREK-kop, klokje."""
    rng = np.random.default_rng(seed)
    parts = [rbox(-w / 2, w / 2, -h / 2, h / 2, -0.22, 0.22, 0.08, "M_GS_Steel")]
    for side in (1, -1):
        R = mk.rot_y(0 if side > 0 else math.pi)
        face = [slab(rounded_rect_loop(-w / 2 - 0.06, w / 2 + 0.06, -h / 2 - 0.06, h / 2 + 0.06, 0.1),
                     [rounded_rect_loop(-w / 2 + 0.08, w / 2 - 0.08, -h / 2 + 0.08, h / 2 - 0.08, 0.06)], 0.20, 0.27, 0.015,
                     "M_GS_Brass")]
        title, tw = text_mesh("VERTREK", 0.30, 0.03, "M_GS_Cream")
        face.append(title.transform(t=(-w / 2 + 0.35 + tw / 2, h / 2 - 0.32, 0.22)))
        face.append(clock(0.22, 0.05, rim_mat="M_GS_Brass", face_mat="M_GS_Cream", hand_mat="M_GS_Steel")
                    .transform(t=(w / 2 - 0.45, h / 2 - 0.32, 0.22)))
        row_h = (h - 0.7) / rows
        for r in range(rows):
            y = h / 2 - 0.62 - (r + 0.5) * row_h
            x = -w / 2 + 0.25
            for n_cells in (2, 2, 1, 11, 1, 2):
                for k in range(n_cells):
                    face.append(rbox(x, x + 0.14, y - row_h * 0.38, y + row_h * 0.38, 0.20, 0.235, 0.012, "M_GS_Dark"))
                    if n_cells > 1 and rng.random() < 0.85:
                        hh = row_h * rng.uniform(0.22, 0.38)
                        face.append(rbox(x + 0.035, x + 0.105, y - hh, y + hh, 0.235, 0.243, 0.004, "M_GS_Flap"))
                    x += 0.165
                x += 0.10
        parts.append(mk.merge(*face).transform(R=R))
    for x in (-w / 2 + 0.6, w / 2 - 0.6):
        parts.append(capsule((x, h / 2, 0), (x, h / 2 + 4.0, 0), 0.04, mat="M_GS_Steel", seg=6, rings=1))
    return mk.merge(*parts), Boxes().add(-w / 2, w / 2, -h / 2, h / 2, -0.25, 0.25), [], []


# ------------------------------------------------------------------ wanden en kamers
def partition(L, door_x, sign_name, sign_text, glass=True):
    """Scheidingswand (x -L/2..L/2, dikte langs z rond 0): houten onderwand, glazen bovenvak met roedes, brede
    deuropening (1,8 m) met een open deur, bordje boven de deur (Sign_*)."""
    parts, boxes = [], Boxes()
    H = 3.7
    t = 0.14
    dw = 1.8
    d0, d1 = door_x - dw / 2, door_x + dw / 2
    for a, b in ((-L / 2, d0), (d1, L / 2)):
        if b - a < 0.05:
            continue
        parts.append(rbox(a, b, FL, FL + 1.0, -t / 2, t / 2, 0.02, "M_GS_Wood"))
        if glass:
            parts.append(rbox(a + 0.01, b - 0.01, FL + 1.04, FL + H - 0.06, -0.012, 0.012, 0.0, "M_GS_Glass"))
            for x in np.arange(a, b + 1e-6, 1.0):
                parts.append(rbox(x - 0.05, x + 0.05, FL + 1.06, FL + H - 0.05, -0.05, 0.05, 0.02, "M_GS_WoodDark"))
            parts.append(rbox(a, b, FL + 1.0, FL + 1.08, -0.07, 0.07, 0.02, "M_GS_WoodDark"))
        else:
            parts.append(rbox(a + 0.02, b - 0.02, FL + 1.0, FL + H - 0.06, -t / 2 + 0.02, t / 2 - 0.02, 0.02, "M_GS_Interior"))
        parts.append(rbox(a, b, FL + H - 0.12, FL + H, -0.08, 0.08, 0.02, "M_GS_WoodDark"))
        boxes.add(a, b, FL, FL + H, -t / 2, t / 2)
    parts.append(rbox(d0 - 0.1, d1 + 0.1, FL + 2.5, FL + H - 0.03, -0.095, 0.095, 0.02, "M_GS_WoodDark"))
    for x in (d0, d1):
        parts.append(rbox(x - 0.08, x + 0.08, FL, FL + 2.5, -0.085, 0.085, 0.02, "M_GS_WoodDark"))
    pane = [(0.15, 1.2), (dw - 0.2, 1.2), (dw - 0.2, 2.2), (0.15, 2.2)]
    leaf = mk.merge(slab([(0.0, 0.02), (dw - 0.05, 0.02), (dw - 0.05, 2.42), (0.0, 2.42)], [pane], -0.03, 0.03, 0.01, "M_GS_Wood"),
                    rbox(0.16, dw - 0.21, 1.21, 2.19, -0.01, 0.01, 0.0, "M_GS_Glass"),
                    ball(0.05, "M_GS_Brass", 8, 5).transform(t=(dw - 0.2, 1.05, 0.06)))
    parts.append(leaf.transform(R=mk.rot_y(-math.radians(100)), t=(d0, FL, 0.0)))
    boxes.add(d0 - 0.1, d1 + 0.1, FL + 2.5, FL + H, -0.09, 0.09)
    board, nodes = sign(sign_name, sign_text, 1.9, 0.38, (door_x, FL + 3.05, 0.11))
    parts.append(board)
    back, back_nodes = sign(sign_name + "_Back", sign_text, 1.9, 0.38, (door_x, FL + 3.05, -0.11), mk.rot_y(math.pi))
    parts.append(back)
    return mk.merge(*parts), boxes, [], nodes + back_nodes


def office_furniture():
    """Kantoor van de stationschef: bureau met lamp, papieren en telefoon, stoel, archiefkasten, kluis, kaart."""
    parts, boxes = [], Boxes()
    parts.append(rbox(-1.0, 1.0, FL + 0.72, FL + 0.80, -0.5, 0.5, 0.02, "M_GS_Wood"))
    for a, b in ((-0.95, -0.55), (0.55, 0.95)):
        parts.append(rbox(a, b, FL, FL + 0.72, -0.45, 0.45, 0.03, "M_GS_WoodDark"))
    parts.append(rbox(-0.5, 0.2, FL + 0.80, FL + 0.83, -0.2, 0.25, 0.005, "M_GS_Cream"))
    lamp = mk.merge(disc(0.1, 0.0, 0.03, "M_GS_Brass", 12, 0.01).transform(R=mk.rot_x(-math.pi / 2)),
                    capsule((0, 0.03, 0), (0.05, 0.35, 0.0), 0.015, mat="M_GS_Brass", seg=5, rings=1),
                    mk.lathe([(0.0, 0.0), (0.14, -0.1), (0.15, -0.14), (0.0, -0.05)], 12, mat="M_GS_Chalk").transform(t=(0.05, 0.42, 0)))
    parts.append(lamp.transform(t=(0.65, FL + 0.80, -0.2)))
    chair = mk.merge(rbox(-0.25, 0.25, FL + 0.45, FL + 0.52, -0.25, 0.25, 0.03, "M_GS_Red"),
                     rbox(-0.25, 0.25, FL + 0.52, FL + 1.05, -0.3, -0.24, 0.03, "M_GS_Red"),
                     cyl(0.04, 0.42, 8, 0.0, "M_GS_Steel", axis="y").transform(t=(0, FL + 0.24, 0)),
                     disc(0.25, 0.0, 0.04, "M_GS_Steel", 10, 0.01).transform(R=mk.rot_x(-math.pi / 2), t=(0, FL, 0)))
    parts.append(chair.transform(t=(0, 0, -0.9)))
    for k, x in enumerate((-2.4, -1.8)):
        parts.append(rbox(x - 0.28, x + 0.28, FL, FL + 1.3, -1.9, -1.35, 0.03, "M_GS_SteelLight"))
        for y in (FL + 0.35, FL + 0.75, FL + 1.1):
            parts.append(rbox(x - 0.06, x + 0.06, y - 0.02, y + 0.02, -1.36, -1.32, 0.01, "M_GS_Brass"))
    safe = mk.merge(rbox(-0.4, 0.4, FL, FL + 0.9, -0.35, 0.35, 0.06, "M_GS_Dark"),
                    disc(0.12, 0.35, 0.39, "M_GS_Brass", 14, 0.01).transform(t=(0.0, FL + 0.5, 0.0)))
    parts.append(safe.transform(t=(2.3, 0, -1.5)))
    parts.append(mp.route_board(1.6, 1.0).transform(t=(0.0, FL + 1.9, -1.95)))
    boxes.add(-1.0, 1.0, FL, FL + 0.8, -0.5, 0.5).add(-2.7, -1.5, FL, FL + 1.3, -1.9, -1.35).add(1.9, 2.7, FL, FL + 0.9, -1.85, -1.15)
    return mk.merge(*parts), boxes, [("Spot_Stationmaster", (0.0, FL, -0.9), None)], []


def podium():
    """Laag houten podium met een spreekgestoelte (crew-briefing), 3,6 x 1,8 m; spreker kijkt naar +z."""
    parts = [rbox(-1.8, 1.8, FL - 0.01, FL + 0.18, -0.9, 0.9, 0.05, "M_GS_Wood"),
             rbox(-1.85, 1.85, FL + 0.18, FL + 0.22, -0.95, 0.95, 0.02, "M_GS_WoodDark"),
             rbox(-1.25, -0.65, FL + 0.22, FL + 0.95, 0.10, 0.50, 0.05, "M_GS_WoodDark"),      # laag gestoelte opzij
             rbox(-1.32, -0.58, FL + 0.90, FL + 0.98, 0.0, 0.60, 0.03, "M_GS_Wood"),
             train_icon(0.30, 0.02, "M_GS_Brass").transform(t=(-0.95, FL + 0.6, 0.51))]
    # de stationschef (1,25 m) staat midden op het podium; het gestoelte ernaast blokkeert hem niet
    return mk.merge(*parts), Boxes().add(-1.8, 1.8, FL, FL + 0.22, -0.9, 0.9).add(-1.25, -0.65, FL, FL + 0.95, 0.10, 0.50), \
        [("Spot_Speaker", (0.2, FL + 0.22, 0.0), None)], []


def mission_table():
    """Kaarttafel voor de briefing (missiekeuze): houten tafel met groene routekaart en pionnetjes."""
    parts = [rbox(-1.2, 1.2, FL + 0.82, FL + 0.9, -0.7, 0.7, 0.03, "M_GS_Wood")]
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(rbox(sx * 1.05 - 0.06, sx * 1.05 + 0.06, FL, FL + 0.82, sz * 0.55 - 0.06, sz * 0.55 + 0.06, 0.02, "M_GS_WoodDark"))
    parts.append(rbox(-1.1, 1.1, FL + 0.9, FL + 0.915, -0.6, 0.6, 0.005, "M_GS_Chalk"))
    pts = [(-0.9, -0.4), (-0.4, 0.1), (0.1, -0.2), (0.5, 0.3), (0.9, 0.0)]
    parts.append(tube([(x, FL + 0.925, z) for x, z in pts], 0.012, "M_GS_Cream", smooth=True, n=20))
    for k, (x, z) in enumerate(pts):
        parts.append(ball(0.04, "M_GS_Yellow", 8, 5).transform(t=(x, FL + 0.95, z)))
    for k, (x, z) in enumerate(((-0.6, 0.35), (0.3, -0.45), (0.75, 0.4))):
        parts.append(mk.lathe([(0.0, 0.0), (0.05, 0.0), (0.03, 0.06), (0.025, 0.1), (0.04, 0.13), (0.0, 0.16)], 10,
                              mat=("M_GS_Red", "M_GS_Steel", "M_GS_Brass")[k]).transform(t=(x, FL + 0.915, z)))
    return mk.merge(*parts), Boxes().add(-1.2, 1.2, FL, FL + 0.95, -0.7, 0.7), \
        [("Spot_MissionSelect", (0.0, FL, 1.2), mk.rot_y(math.pi))], []


def storage_shelf():
    """Stellingkast met dozen, blikken en een kist (opslag)."""
    parts = []
    W, D, H = 2.4, 0.6, 2.2
    for x in (-W / 2, W / 2):
        for z in (-D / 2, D / 2):
            parts.append(rbox(x - 0.03, x + 0.03, FL, FL + H, z - 0.03, z + 0.03, 0.01, "M_GS_Steel"))
    rng = np.random.default_rng(4)
    for y in (FL + 0.15, FL + 0.85, FL + 1.55, FL + 2.15):
        parts.append(rbox(-W / 2, W / 2, y - 0.03, y, -D / 2, D / 2, 0.01, "M_GS_Wood"))
        if y > FL + 2:
            continue
        x = -W / 2 + 0.1
        while x < W / 2 - 0.4:
            w = rng.uniform(0.3, 0.55)
            h = rng.uniform(0.25, 0.55)
            mat = ("M_GS_Sandstone", "M_GS_Wood", "M_GS_Steel", "M_GS_Red")[int(rng.integers(0, 4))]
            parts.append(rbox(x, x + w, y, y + h, -D / 2 + 0.05, D / 2 - 0.05, 0.03, mat))
            x += w + 0.06
    return mk.merge(*parts), Boxes().add(-W / 2, W / 2, FL, FL + H, -D / 2, D / 2), [], []


# ------------------------------------------------------------------ borden en posters
def platform_sign():
    """Hangend perronbord met nummer (aanpasbaar, Sign_Platform) aan twee stangen; pivot = ophangpunt, hangt -y."""
    parts = []
    w, h = 0.9, 0.9
    for x in (-0.3, 0.3):
        parts.append(capsule((x, 0.0, 0), (x, -0.5, 0), 0.02, mat="M_GS_Steel", seg=5, rings=1))
    plate = mk.merge(slab(rounded_rect_loop(-w / 2, w / 2, -h / 2, h / 2, 0.12), [], -0.05, 0.05, 0.02, "M_GS_Steel"),
                     slab(rounded_rect_loop(-w / 2 + 0.05, w / 2 - 0.05, -h / 2 + 0.05, h / 2 - 0.05, 0.09),
                          [rounded_rect_loop(-w / 2 + 0.1, w / 2 - 0.1, -h / 2 + 0.1, h / 2 - 0.1, 0.07)], -0.06, 0.06, 0.01, "M_GS_Brass"))
    parts.append(plate.transform(t=(0, -0.5 - h / 2, 0)))
    nodes = []
    for side, R in ((1, None), (-1, mk.rot_y(math.pi))):
        f, n = props.sign_nodes("Sign_Platform" if side > 0 else "Sign_Platform_Back", "1", 0.6, 0.6, 0.02,
                                (0, -0.5 - h / 2, side * 0.06), R)
        nodes += n
    return mk.merge(*parts), Boxes(), [], nodes


def direction_sign():
    """Hangend richtingbord (aanpasbaar, Sign_Direction: PERRONS / UITGANG ...) met pijl; pivot = ophangpunt."""
    parts = []
    w, h = 3.2, 0.62
    for x in (-1.2, 1.2):
        parts.append(capsule((x, 0.0, 0), (x, -0.6, 0), 0.02, mat="M_GS_Steel", seg=5, rings=1))
    parts.append(rbox(-w / 2, w / 2, -0.6 - h, -0.6, -0.05, 0.05, 0.03, "M_GS_Steel"))
    for side in (1, -1):
        arrow = slab([(0.0, 0.0), (0.22, 0.18), (0.22, 0.07), (0.45, 0.07), (0.45, -0.07), (0.22, -0.07), (0.22, -0.18)], [],
                     0.0, 0.02, 0.005, "M_GS_Yellow")
        parts.append(arrow.transform(R=mk.rot_y(0 if side > 0 else math.pi), t=(side * (w / 2 - 0.62), -0.6 - h / 2, side * 0.05)))
    nodes = []
    for side, R in ((1, None), (-1, mk.rot_y(math.pi))):
        f, n = props.sign_nodes("Sign_Direction" if side > 0 else "Sign_Direction_Back", "PERRONS", 2.4, 0.45, 0.02,
                                (-0.3 * side, -0.6 - h / 2, side * 0.055), R)
        nodes += n
    return mk.merge(*parts), Boxes(), [], nodes


def poster():
    """Ingelijste reisposter (CONDUCTOR CHAOS met treintje en bergen) in een messing lijst; tegen een wand, +z."""
    w, h = 1.2, 1.7
    parts = [slab(rounded_rect_loop(-w / 2, w / 2, 0.0, h, 0.05), [rounded_rect_loop(-w / 2 + 0.08, w / 2 - 0.08, 0.08, h - 0.08, 0.03)],
                  0.0, 0.06, 0.015, "M_GS_Brass"),
             rbox(-w / 2 + 0.08, w / 2 - 0.08, 0.08, h - 0.08, 0.0, 0.02, 0.0, "M_GS_Cream"),
             rbox(-w / 2 + 0.095, w / 2 - 0.095, 0.095, 0.75, 0.006, 0.026, 0.0, "M_GS_Leaf")]
    for k, (x, hh) in enumerate(((-0.25, 0.55), (0.18, 0.75))):
        parts.append(slab([(x - 0.3, 0.7), (x + 0.3, 0.7), (x, 0.7 + hh)], [], 0.034 + k * 0.01, 0.044 + k * 0.01, 0.0, "M_GS_Roof"))
    parts.append(train_icon(0.5, 0.02, "M_GS_Red").transform(t=(0.0, 0.42, 0.03)))
    for (txt, y, sz) in (("CONDUCTOR", h - 0.28, 0.13), ("CHAOS", h - 0.5, 0.17)):
        m, tw = text_mesh(txt, sz, 0.015, "M_GS_Red")
        parts.append(m.transform(t=(0.0, y, 0.025)))
    return mk.merge(*parts), Boxes(), [], []


def wall_sign():
    """Wandbord (aanpasbaar, Sign_Wall: BRIEFING, ...) met messing rand; pivot = midden, achterkant op de wand (z = 0)."""
    w, h = 3.0, 0.62
    parts = [slab(rounded_rect_loop(-w / 2, w / 2, -h / 2, h / 2, 0.14), [], 0.0, 0.07, 0.02, "M_GS_Steel"),
             slab(rounded_rect_loop(-w / 2 + 0.04, w / 2 - 0.04, -h / 2 + 0.04, h / 2 - 0.04, 0.11),
                  [rounded_rect_loop(-w / 2 + 0.09, w / 2 - 0.09, -h / 2 + 0.09, h / 2 - 0.09, 0.08)], 0.06, 0.09, 0.01, "M_GS_Brass")]
    _, nodes = props.sign_nodes("Sign_Wall", "BRIEFING", 2.5, 0.42, 0.02, (0.0, 0.0, 0.075))
    return mk.merge(*parts), Boxes(), [], nodes


def rug():
    """Rond blauw kleed met lichtere rand en treinpictogram (briefing), 1 cm boven de vloer."""
    return mp.round_rug(1.7).transform(t=(0, FL, 0)), Boxes(), [], []
