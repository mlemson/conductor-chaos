"""Props voor het stationnetje en de perronkit (issue #182), naar Docs/References/Environment/*.png.

Elke functie geeft (mesh, collider) terug, in lokale coördinaten: pivot midden-onder op de grond, Y omhoog,
voorkant naar +Z, 1 unit = 1 m. Props met bewegende delen geven extra knopen terug (bv. de slagboom).
De personages zijn 1,2-1,3 m hoog; zitting, hoogtes en doorgangen zijn daarop afgestemd.
"""
import math

import numpy as np

from station_common import (DEG, arch_loop, ball, capsule, circle_loop, clock, col_box, cyl, disc, lantern, mk,
                            rbox, rounded_rect_loop, slab, text_mesh, train_icon, tube)


# ------------------------------------------------------------------ zitten & opslag
def loose_prop(name, mesh, colliders, position, rotation=None):
    """A movable model node with its own compound collider, separate from the building."""
    root = f"Loose_{name}"
    shapes = colliders if isinstance(colliders, tuple) else (colliders,)
    return [(root, mesh, None, position, rotation, True)] + [
        (f"Col_{name}_{i}", shape, root, (0, 0, 0), None, False)
        for i, shape in enumerate(shapes, 1)
    ]


def swing_door(name, mesh, collider, hinge, open_deg, start_deg=0.0, closed=None):
    """Knopen voor een draaiend deurblad (deur, kluisdeurtje, hekpoort) dat SwingDoor in Unity aanstuurt.

    De knoop `name` heeft zijn pivot op het scharnier en draait om zijn eigen Y-as. Twee lege knopen leggen de
    uitersten vast, zodat Unity niets hoeft te raden: Hinge_<name>_Closed (dicht) en Hinge_<name>_Open (helemaal
    open, open_deg graden vanaf dicht). start_deg = de stand waarin het blad in het model (en dus de scène) staat.
    closed = rotatie van de dichte stand (3x3), standaard geen."""
    base = closed if closed is not None else mk.rot_y(0)

    def at(deg):
        return base @ mk.rot_y(deg * DEG)
    return [(name, mesh, None, tuple(hinge), at(start_deg), True),
            (f"Col_{name}", collider, name, (0, 0, 0), None, False),
            (f"Hinge_{name}_Closed", None, None, tuple(hinge), at(0.0), True),
            (f"Hinge_{name}_Open", None, None, tuple(hinge), at(open_deg), True)]


def bench(length=1.8, *, split_colliders=False):
    """Parkbank: drie dikke zittinglatten, twee rugleuninglatten, marine gietijzeren zijframes."""
    L = length
    parts = []
    for k, z in enumerate((-0.17, 0.0, 0.17)):
        parts.append(rbox(-L / 2, L / 2, 0.40, 0.48, z - 0.075, z + 0.075, 0.035, "M_Stn_Timber"))
    for y in (0.62, 0.80):
        b = rbox(-L / 2, L / 2, y - 0.07, y + 0.07, -0.04, 0.03, 0.03, "M_Stn_Timber")
        parts.append(b.transform(R=mk.rot_x(-12 * DEG), t=(0, 0, -0.24 + 0.12 * (y - 0.55))))
    for x in (-L / 2 + 0.22, L / 2 - 0.22):
        leg = mk.merge(
            rbox(x - 0.05, x + 0.05, 0.0, 0.40, 0.14, 0.24, 0.025, "M_Stn_Navy"),
            rbox(x - 0.05, x + 0.05, 0.0, 0.40, -0.28, -0.18, 0.025, "M_Stn_Navy"),
            rbox(x - 0.035, x + 0.035, 0.35, 0.42, -0.27, 0.23, 0.02, "M_Stn_Navy"),
            rbox(x - 0.06, x + 0.06, 0.0, 0.05, 0.10, 0.28, 0.02, "M_Stn_Navy"),
            rbox(x - 0.06, x + 0.06, 0.0, 0.05, -0.32, -0.14, 0.02, "M_Stn_Navy"),
            capsule((x, 0.40, -0.25), (x, 0.90, -0.36), 0.045, mat="M_Stn_Navy", seg=8, rings=1))
        parts.append(leg)
    cols = (col_box(-L / 2, L / 2, 0.0, 0.48, -0.30, 0.26), col_box(-L / 2, L / 2, 0.48, 0.92, -0.40, -0.22))
    return mk.merge(*parts), cols if split_colliders else mk.merge(*cols)


def barrel(h=0.86, r=0.34, staves=14):
    """Houten ton in de stijl van de prullenbak uit het concept: één bolle romp met platte duigen (facetten),
    twee brede marine hoepels die strak om de romp liggen, een verdiepte deksel met plankjes en een stop.
    Alles sluit op elkaar aan zonder samenvallende vlakken."""
    re = 0.80 * r                                               # straal aan de kopse kanten

    def rad(y):                                                 # bolling van de romp
        return re + (r - re) * math.sin(math.pi * min(max(y / h, 0.0), 1.0))
    ys = [0.0, 0.06, 0.16, 0.28, 0.40, 0.50, 0.60, 0.72, 0.84, 0.94, 1.0]
    lip, rec = 0.032, 0.045                                     # rand bovenop en diepte van de deksel
    prof = [(0.0, 0.0), (re - 0.025, 0.0), (re - 0.004, 0.012)] + [(rad(t * h), t * h) for t in ys[1:-1]] + \
           [(re - 0.004, h - 0.012), (re - 0.022, h), (re - lip, h), (re - lip - 0.006, h - rec), (0.0, h - rec)]
    # dubbel zoveel segmenten; elk tweede punt iets naar binnen: ondiepe V-voegjes tussen de duigen, alles in één romp
    body = mk.lathe(prof, 2 * staves, mat="M_Stn_Timber", phase=0.5 - math.pi / (2 * staves))
    ang = np.arctan2(body.V[:, 2], body.V[:, 0]) - (0.5 - math.pi / (2 * staves))
    k = np.round(ang / (math.pi / staves)).astype(int)
    rr = np.hypot(body.V[:, 0], body.V[:, 2])
    groove = (k % 2 != 0) & (rr > re - lip + 0.002) & (body.V[:, 1] > 0.004) & (body.V[:, 1] < h - 0.004)
    body.V[groove, 0] *= 0.975
    body.V[groove, 2] *= 0.975
    body.sharp = True                                           # platte duigen
    parts = [body]
    # deksel: twee plankhelften met een smalle naad, iets onder de rand, en een dwarslat eroverheen
    rl = re - lip - 0.012
    dl = math.asin(0.006 / rl)
    half = [(rl * math.cos(t), rl * math.sin(t)) for t in np.linspace(-math.pi / 2 + dl, math.pi / 2 - dl, 10)]
    lid = slab(half, [], 0.0, 0.016, 0.006, "M_Stn_WoodLight").transform(R=mk.rot_x(-math.pi / 2), t=(0, h - rec, 0))
    parts += [lid, lid.transform(s=(-1, 1, 1))]
    parts.append(rbox(-rl * 0.74, rl * 0.74, h - rec + 0.016, h - rec + 0.034, -0.035, 0.035, 0.008, "M_Stn_TimberDark"))
    # hoepels: band die de bolling volgt, binnenkant in de romp
    for yc in (0.20 * h, 0.80 * h):
        y0, y1 = yc - 0.038, yc + 0.038
        o0, o1, ch = rad(y0) + 0.016, rad(y1) + 0.016, 0.007
        hoop = [(rad(y0) - 0.03, y0), (o0 - ch, y0), (o0, y0 + ch), (o1, y1 - ch), (o1 - ch, y1), (rad(y1) - 0.03, y1)]
        band = mk.lathe(hoop, staves, mat="M_Stn_Navy", phase=0.5, closed=True)
        band.sharp = True
        parts.append(band)
    # stop in een duig, tussen de hoepels
    a = 2 * math.pi * 0.5 / staves
    rm = rad(h / 2) * math.cos(math.pi / staves)                # midden van een duig
    plug = cyl(0.036, 0.04, 10, 0.008, "M_Stn_TimberDark", axis="z").transform(t=(0, h / 2, rm + 0.004))
    parts.append(plug.transform(R=mk.rot_y(math.pi / 2 - a)))
    col = cyl(r + 0.01, h, 10, 0.0, "M_Stn_Collider").transform(t=(0, h / 2, 0))
    return mk.merge(*parts), col


def crate(s=0.7, seed=0):
    """Krat: frame van planken met een X-schoor op elke zijde (lichte houtkleur). De ribben stoten tegen elkaar
    (staande ribben op de hoeken, liggende ertussen), zodat er geen vlakken dubbel liggen."""
    t, e = 0.07, s / 2
    parts = [rbox(-e + 0.03, e - 0.03, 0.03, s - 0.03, -e + 0.03, e - 0.03, 0.02, "M_Stn_WoodLight")]
    for ax in (-1, 1):                                                  # staande ribben op de hoeken
        for az in (-1, 1):
            x0, x1 = sorted((ax * e, ax * (e - t)))
            z0, z1 = sorted((az * e, az * (e - t)))
            parts.append(rbox(x0, x1, 0, s, z0, z1, 0.015, "M_Stn_Timber"))
    for y in (0.0, s - t):                                              # liggende ribben tussen de staanders
        parts.append(rbox(-e + t, e - t, y, y + t, -e, -e + t, 0.015, "M_Stn_Timber"))
        parts.append(rbox(-e + t, e - t, y, y + t, e - t, e, 0.015, "M_Stn_Timber"))
        parts.append(rbox(-e, -e + t, y, y + t, -e + t, e - t, 0.015, "M_Stn_Timber"))
        parts.append(rbox(e - t, e, y, y + t, -e + t, e - t, 0.015, "M_Stn_Timber"))
    diag = math.hypot(s - 2 * t, s - 2 * t)
    for face in range(4):
        b = rbox(-diag / 2, diag / 2, -0.035, 0.035, -0.02, 0.02, 0.012, "M_Stn_Timber").transform(R=mk.rot_z(math.pi / 4))
        parts.append(b.transform(t=(0, s / 2, e - 0.005)).transform(R=mk.rot_y(face * math.pi / 2)))
    col = col_box(-e, e, 0, s, -e, e)
    return mk.merge(*parts), col


def crate_stack(*, split_colliders=False):
    a, ca = crate(0.72)
    b, cb = crate(0.72)
    c, cc = crate(0.62)
    parts = [a.transform(t=(-0.38, 0, 0.02)), b.transform(R=mk.rot_y(0.08), t=(0.38, 0, -0.03)),
             c.transform(R=mk.rot_y(-0.12), t=(-0.05, 0.72, 0.0))]
    cols = (ca.transform(t=(-0.38, 0, 0.02)), cb.transform(R=mk.rot_y(0.08), t=(0.38, 0, -0.03)),
            cc.transform(R=mk.rot_y(-0.12), t=(-0.05, 0.72, 0.0)))
    return mk.merge(*parts), cols if split_colliders else mk.merge(*cols)


def daisy(rng, h):
    parts = [capsule((0, 0, 0), (rng.uniform(-0.02, 0.02), h, rng.uniform(-0.02, 0.02)), 0.010, mat="M_Stn_Leaf",
                     seg=5, rings=1)]
    top = np.array(parts[0].V[np.argmax(parts[0].V[:, 1])])
    R = mk.rot_x(rng.uniform(-0.5, -0.2)) @ mk.rot_y(rng.uniform(0, math.pi))
    for k in range(6):
        a = 2 * math.pi * k / 6
        parts.append(mk.ellipsoid(0.034, 0.010, 0.018, 8, 4, mat="M_Stn_Cream")
                     .transform(t=(0.034 * math.cos(a), 0, 0.034 * math.sin(a))).transform(R=mk.rot_y(-a) @ mk.rot_y(a))
                     .transform(R=R, t=top))
    parts.append(ball(0.020, "M_Stn_Yellow", 10, 6, sy=0.7).transform(R=R, t=top + np.array([0, 0.010, 0])))
    return mk.merge(*parts)


def bush_mass(rng, w, d, h, n=6, mat="M_Stn_Leaf"):
    parts = []
    for k in range(n):
        x = (k + 0.5) / n * w - w / 2 + rng.uniform(-0.04, 0.04)
        r = rng.uniform(0.18, 0.24) * min(1.0, h / 0.35)
        y, z = r * 0.55 + rng.uniform(0, 0.05), rng.uniform(-d * 0.2, d * 0.2)
        # elk bolletje een eigen draaiing en kanteling: anders vallen facetten van even grote bollen samen
        R = mk.rot_y(2.399 * k + 0.3) @ mk.rot_x(0.11 * (k % 3) - 0.11)
        parts.append(mk.ellipsoid(r, r * 0.85, r, 10, 6, mat=mat).transform(R=R, t=(x, y, z)))
    return mk.merge(*parts)


def planter(length=1.2, seed=3):
    """Houten plantenbak met bolle struikjes en madeliefjes (concept: links bij het bord, rechts bij het station)."""
    rng = np.random.default_rng(seed)
    L, D, H = length, 0.42, 0.36
    parts = [rbox(-L / 2, L / 2, 0.0, H, -D / 2, D / 2, 0.04, "M_Stn_Timber"),
             rbox(-L / 2 - 0.03, L / 2 + 0.03, H - 0.06, H + 0.01, -D / 2 - 0.03, D / 2 + 0.03, 0.025, "M_Stn_TimberDark"),
             rbox(-L / 2 + 0.05, L / 2 - 0.05, H - 0.05, H - 0.01, -D / 2 + 0.05, D / 2 - 0.05, 0.0, "M_Stn_Soil")]
    for x in np.linspace(-L / 2 + 0.25, L / 2 - 0.25, max(2, int(L / 0.5))):          # verticale planken
        parts.append(rbox(x - 0.006, x + 0.006, 0.05, H - 0.07, D / 2 - 0.002, D / 2 + 0.006, 0.0, "M_Stn_TimberDark"))
    parts.append(bush_mass(rng, L - 0.15, D - 0.1, 0.35, max(3, int(L / 0.22))).transform(t=(0, H - 0.08, 0)))
    for k in range(max(2, int(L / 0.4))):
        x = (k + 0.5) / max(2, int(L / 0.4)) * (L - 0.2) - (L - 0.2) / 2
        parts.append(daisy(rng, rng.uniform(0.28, 0.40)).transform(t=(x, H - 0.05, rng.uniform(0.0, 0.10))))
    col = col_box(-L / 2 - 0.03, L / 2 + 0.03, 0, H + 0.2, -D / 2 - 0.03, D / 2 + 0.03)
    return mk.merge(*parts), col


def trash_bin():
    """Prullenbak: houten duigen met marine banden, bolle marine deksel met inwerpopening, crème pictogram."""
    r, h = 0.27, 0.78
    body = mk.lathe([(0.0, 0.0), (r * 0.86, 0.0), (r * 0.95, 0.06), (r, 0.40), (r * 1.0, h), (0.0, h)], 12,
                    mat="M_Stn_Timber", phase=0.5)
    parts = [body]
    for y in (0.10, h - 0.10):
        parts.append(mk.torus(r + 0.01, 0.028, 12, 4, mat="M_Stn_Navy", ry=0.05).transform(t=(0, y, 0)))
    lid = mk.lathe([(0.0, h), (r + 0.04, h), (r + 0.05, h + 0.04), (r * 0.8, h + 0.14), (r * 0.3, h + 0.19),
                    (0.0, h + 0.20)], 12, mat="M_Stn_Navy", phase=0.5)
    parts.append(lid)
    parts.append(rbox(-0.12, 0.12, h + 0.02, h + 0.09, r * 0.78, r + 0.06, 0.02, "M_Stn_Dark"))   # inwerpopening
    icon = [rbox(-0.065, 0.065, 0.0, 0.025, 0, 0.02, 0.008, "M_Stn_Cream").transform(t=(0, 0.17, 0)),
            rbox(-0.022, 0.022, 0.025, 0.045, 0, 0.02, 0.006, "M_Stn_Cream").transform(t=(0, 0.17, 0))]
    can = slab([(-0.055, 0.0), (0.055, 0.0), (0.065, 0.15), (-0.065, 0.15)],
               [rounded_rect_loop(-0.035, -0.012, 0.025, 0.125, 0.008, 1), rounded_rect_loop(0.012, 0.035, 0.025, 0.125, 0.008, 1)],
               0.0, 0.02, 0.004, "M_Stn_Cream")
    icon.append(can)
    ic = mk.merge(*icon).transform(t=(0, 0.26, 0))
    parts.append(ic.transform(R=mk.rot_x(-0.02), t=(0, 0, r - 0.006)))
    col = cyl(r + 0.04, h + 0.20, 10, 0.0, "M_Stn_Collider").transform(t=(0, (h + 0.20) / 2, 0))
    return mk.merge(*parts), col


# ------------------------------------------------------------------ verlichting & borden
def lamp_post(height=2.75):
    """Lantaarnpaal: grijze stenen voet, marine paal met kraag, gloeiende lantaarn bovenop."""
    H = height
    parts = [rbox(-0.18, 0.18, 0.0, 0.16, -0.18, 0.18, 0.04, "M_Stn_Stone"),
             mk.lathe([(0.0, 0.14), (0.12, 0.14), (0.12, 0.22), (0.08, 0.30), (0.06, 0.34), (0.0, 0.34)], 12, mat="M_Stn_Navy"),
             capsule((0, 0.30, 0), (0, H - 0.10, 0), 0.055, 0.045, seg=10, rings=1, mat="M_Stn_Navy"),
             mk.lathe([(0.0, H - 0.24), (0.07, H - 0.24), (0.10, H - 0.16), (0.12, H - 0.10), (0.0, H - 0.08)], 12,
                      mat="M_Stn_Navy"),
             mk.torus(0.065, 0.022, 12, 5, mat="M_Stn_Navy").transform(t=(0, 1.0, 0)),
             lantern(1.15).transform(t=(0, H - 0.12, 0))]
    col = mk.merge(col_box(-0.18, 0.18, 0, 0.34, -0.18, 0.18), col_box(-0.07, 0.07, 0, H + 0.5, -0.07, 0.07))
    return mk.merge(*parts), col, (0.0, H + 0.12, 0.0)


def wall_lantern():
    """Wandlantaarn met gebogen arm; pivot = muurplaat (muur in het XY-vlak, lantaarn naar +Z)."""
    parts = [rbox(-0.07, 0.07, -0.16, 0.16, 0.0, 0.035, 0.015, "M_Stn_Navy"),
             tube([(0, 0.06, 0.03), (0, 0.10, 0.16), (0, 0.06, 0.30)], 0.022, "M_Stn_Navy", smooth=True, n=10, seg=8),
             capsule((0, -0.10, 0.03), (0, 0.0, 0.26), 0.018, mat="M_Stn_Navy", seg=6, rings=1),
             lantern(0.95).transform(t=(0, -0.02, 0.30))]
    return mk.merge(*parts), None


def hanging_sign(w=0.62, h=0.48, rod=0.32):
    """Hangbord met treinpictogram; pivot = ophangpunt (bovenkant van de stangetjes), bord hangt naar -Y."""
    parts = [slab(rounded_rect_loop(-w / 2, w / 2, -h / 2, h / 2, 0.06, 3), [], -0.03, 0.03, 0.012, "M_Stn_Navy")]
    parts.append(slab(rounded_rect_loop(-w / 2 + 0.04, w / 2 - 0.04, -h / 2 + 0.04, h / 2 - 0.04, 0.04, 3),
                      [rounded_rect_loop(-w / 2 + 0.065, w / 2 - 0.065, -h / 2 + 0.065, h / 2 - 0.065, 0.03, 3)],
                      0.025, 0.04, 0.005, "M_Stn_NavyLight"))
    for side in (1, -1):
        parts.append(train_icon(w * 0.62, 0.02, "M_Stn_Cream").transform(R=mk.rot_y(0 if side > 0 else math.pi),
                                                                          t=(0, -0.01, side * 0.028)))
    sign = mk.merge(*parts).transform(t=(0, -rod - h / 2, 0))
    rods = [capsule((x, 0.0, 0), (x, -rod - 0.04, 0), 0.014, mat="M_Stn_Dark", seg=6, rings=1) for x in (-w * 0.3, w * 0.3)]
    rings = [mk.torus(0.03, 0.009, 10, 4, mat="M_Stn_Dark").transform(R=mk.rot_x(math.pi / 2), t=(x, -rod - 0.03, 0))
             for x in (-w * 0.3, w * 0.3)]
    return mk.merge(sign, *rods, *rings), None


SIGN_TEXT_DEPTH = 0.035
SIGN_INFO = {}      # id(Sign_*-knoop) -> (knoop, maten) voor de Unity-component StationSign (issue #209)


def station_board_parts(text="STATION", width=2.4, height=0.62, depth=0.10):
    """Bord in drie delen: (frame, pictogram, tekst, maten). Pictogram en tekst staan op hun plek voor het bord;
    `maten` beschrijft de indeling (lettergrootte, vrije breedte) zodat Unity de tekst opnieuw kan opbouwen."""
    w, h = width, height
    frame = [slab(rounded_rect_loop(-w / 2, w / 2, -h / 2, h / 2, h * 0.48, 6), [], -depth / 2, depth / 2, 0.025, "M_Stn_Yellow"),
             slab(rounded_rect_loop(-w / 2 + 0.07, w / 2 - 0.07, -h / 2 + 0.07, h / 2 - 0.07,
                                    min(h * 0.38, h / 2 - 0.075), 6), [],
                  -depth / 2 - 0.01, depth / 2 + 0.012, 0.02, "M_Stn_Navy")]
    for x in (-w / 2 + h * 0.30, w / 2 - h * 0.30):                         # gele 'klinknagels' links en rechts
        frame.append(ball(0.032, "M_Stn_Yellow", 10, 6, sy=0.7).transform(t=(x, 0.0, depth / 2 + 0.012)))
    th0 = th = h * 0.40
    txt, tw = text_mesh(text, th, SIGN_TEXT_DEPTH, "M_Stn_Cream")
    icon_w0 = icon_w = th * 1.30
    gap0 = gap = th * 0.45
    total = icon_w + gap + tw
    room = w - 2 * (h * 0.30 + 0.09)                       # vrij tussen de klinknagels
    k = 1.0
    if total > room:                                        # te lang: tekst en pictogram schalen mee
        k = room / total
        th *= k
        txt, tw = text_mesh(text, th, SIGN_TEXT_DEPTH, "M_Stn_Cream")
        icon_w, gap = icon_w * k, gap * k
        total = icon_w + gap + tw
    x0 = -total / 2
    z = depth / 2 + 0.006
    info = {"text": text, "letterHeight": round(th0, 5), "room": round(room, 5), "iconWidth": round(icon_w0, 5),
            "iconGap": round(gap0, 5), "textZ": round(z, 5), "textDepth": SIGN_TEXT_DEPTH, "fit": k,
            "iconX": x0 + icon_w / 2, "textX": x0 + icon_w + gap + tw / 2}
    return mk.merge(*frame), train_icon(icon_w, 0.03, "M_Stn_Cream"), txt, info


def station_board(text="STATION", width=2.4, height=0.62, depth=0.10):
    """Marine bord met gele rand, crème treinpictogram en tekst (voorkant +Z), gecentreerd in de oorsprong."""
    frame, icon, txt, i = station_board_parts(text, width, height, depth)
    return mk.merge(frame, icon.transform(t=(i["iconX"], 0.0, i["textZ"])), txt.transform(t=(i["textX"], 0.0, i["textZ"])))


def sign_nodes(name, text, width, height, depth, pivot, R=None, parent=None):
    """Bord met aanpasbare tekst: geeft het frame (in de ruimte van `parent`, om mee te voegen in een vaste mesh)
    en de knopen <name> (midden van het bord), <name>_Icon en <name>_Text. Unity (StationSign) bouwt de tekst
    opnieuw op uit de bordletters; de maten komen in StationKit.json onder "signs"."""
    frame, icon, txt, i = station_board_parts(text, width, height, depth)
    R3 = np.eye(3) if R is None else np.asarray(R)
    nodes = [(name, None, parent, tuple(pivot), R, True),
             (name + "_Icon", icon, name, (i["iconX"], 0.0, i["textZ"]), None, True),
             (name + "_Text", txt.transform(t=(i["textX"], 0.0, i["textZ"])), name, (0.0, 0.0, 0.0), None, True)]
    SIGN_INFO[id(nodes[0])] = (nodes[0], {"node": name, "iconScale": round(i["fit"], 5),   # pictogram = iconWidth × fit
                                          **{k: v for k, v in i.items() if k not in ("fit", "iconX", "textX")}})
    return frame.transform(R=R3, t=tuple(pivot)), nodes


def station_sign():
    """Vrijstaand stationsbord: twee marine palen met gele bollen op stenen voeten, bord ertussen."""
    W = 2.6
    frame, sign = sign_nodes("Sign_Name", "STATION", W - 0.30, 0.66, 0.10, (0.0, 1.48, 0.0))
    parts = [frame]
    for x in (-W / 2, W / 2):
        parts += [rbox(x - 0.17, x + 0.17, 0.0, 0.30, -0.17, 0.17, 0.05, "M_Stn_Stone"),
                  rbox(x - 0.07, x + 0.07, 0.25, 1.95, -0.07, 0.07, 0.03, "M_Stn_Navy"),
                  mk.lathe([(0.0, 1.92), (0.10, 1.93), (0.10, 1.98), (0.0, 1.99)], 12, mat="M_Stn_Navy").transform(t=(x, 0, 0)),
                  ball(0.13, "M_Stn_Yellow").transform(t=(x, 2.10, 0))]
        parts.append(rbox(min(x, x - 0.15 * np.sign(x)), max(x, x - 0.15 * np.sign(x)), 1.40, 1.56, -0.04, 0.04, 0.02,
                          "M_Stn_Navy"))
    col = mk.merge(col_box(-W / 2 - 0.17, W / 2 + 0.17, 0.0, 0.30, -0.17, 0.17),
                   col_box(-W / 2 - 0.07, W / 2 + 0.07, 0.25, 2.2, -0.10, 0.10))
    return mk.merge(*parts), col, sign


def timetable():
    """Dienstregelingbord: marine kast op twee poten, crème vertrekstaat, klokje bovenop."""
    W, H, y0 = 1.25, 1.00, 0.90
    parts = []
    for x in (-W / 2 + 0.18, W / 2 - 0.18):
        parts += [rbox(x - 0.06, x + 0.06, 0.0, y0 + 0.1, -0.06, 0.06, 0.025, "M_Stn_Navy"),
                  rbox(x - 0.10, x + 0.10, 0.0, 0.06, -0.12, 0.12, 0.02, "M_Stn_Navy")]
    frame = slab(rounded_rect_loop(-W / 2, W / 2, y0, y0 + H, 0.10, 3), [], -0.09, 0.07, 0.03, "M_Stn_Navy")
    board = slab(rounded_rect_loop(-W / 2 + 0.09, W / 2 - 0.09, y0 + 0.09, y0 + H - 0.09, 0.05, 2), [], 0.06, 0.085, 0.008,
                 "M_Stn_Cream")
    parts += [frame, board]
    for k in range(4):
        y = y0 + H - 0.24 - k * 0.18
        parts.append(rbox(-W / 2 + 0.16, -W / 2 + 0.29, y - 0.05, y + 0.05, 0.08, 0.10, 0.015, "M_Stn_Yellow"))
        parts.append(rbox(-W / 2 + 0.36, W / 2 - 0.18 - 0.12 * (k % 2), y - 0.03, y + 0.03, 0.08, 0.098, 0.012, "M_Stn_Navy"))
    # klokhuis op het bord
    parts.append(slab(circle_loop(0, 0, 0.21, 24), [], -0.08, 0.06, 0.03, "M_Stn_Navy").transform(t=(0, y0 + H + 0.10, 0)))
    parts.append(clock(0.16, 0.05).transform(t=(0, y0 + H + 0.10, 0.05)))
    parts.append(slab([(-0.26, 0.0), (0.26, 0.0), (0.12, 0.12), (-0.12, 0.12)], [], -0.065, 0.045, 0.02, "M_Stn_Navy")
                 .transform(t=(0, y0 + H - 0.03, 0)))
    col = mk.merge(col_box(-W / 2, W / 2, y0, y0 + H + 0.32, -0.10, 0.09), col_box(-W / 2 + 0.1, W / 2 - 0.1, 0, y0, -0.12, 0.12))
    return mk.merge(*parts), col


def ticket_icon(w, depth, mat):
    """Kaartje: afgeronde rechthoek met halfronde inkepingen opzij en een perforatielijn."""
    h = w * 0.55
    loop = []
    for (cx, cy, a0, a1) in ((w / 2 - 0.1 * w, -h / 2 + 0.1 * w, -90, 0), (w / 2 - 0.1 * w, h / 2 - 0.1 * w, 0, 90),
                             (-w / 2 + 0.1 * w, h / 2 - 0.1 * w, 90, 180), (-w / 2 + 0.1 * w, -h / 2 + 0.1 * w, 180, 270)):
        for k in range(4):
            a = math.radians(a0 + (a1 - a0) * k / 3)
            loop.append((cx + 0.1 * w * math.cos(a), cy + 0.1 * w * math.sin(a)))
        if a0 == 0:                                           # rechter inkeping
            pass
    m = slab(loop, [], 0.0, depth, 0.004, mat)
    dots = [disc(w * 0.022, depth, depth + 0.006, "M_Stn_Navy" if mat != "M_Stn_Navy" else "M_Stn_Cream", 8, 0.0)
            .transform(t=(w * 0.18, -h / 2 + h * (k + 0.5) / 5, 0)) for k in range(5)]
    star = rbox(-w * 0.22, w * 0.04, -h * 0.12, h * 0.12, depth, depth + 0.006, 0.006,
                "M_Stn_Navy" if mat != "M_Stn_Navy" else "M_Stn_Cream")
    return mk.merge(m, *dots, star)


def ticket_machine():
    """Kaartautomaat: marine kast, gele kop met kaartje, lichtblauw scherm, gele knoppen, uitgiftebak."""
    W, D, H = 0.66, 0.50, 1.50
    parts = [rbox(-W / 2 - 0.03, W / 2 + 0.03, 0.0, 0.10, -D / 2 - 0.03, D / 2 + 0.03, 0.03, "M_Stn_Dark"),
             rbox(-W / 2, W / 2, 0.08, H - 0.20, -D / 2, D / 2, 0.06, "M_Stn_Navy"),
             rbox(-W / 2 - 0.02, W / 2 + 0.02, H - 0.24, H, -D / 2 - 0.02, D / 2 + 0.05, 0.06, "M_Stn_Yellow")]
    parts.append(ticket_icon(0.26, 0.02, "M_Stn_Cream").transform(t=(0, H - 0.12, D / 2 + 0.045)))
    scr = slab(rounded_rect_loop(-0.21, 0.21, 0.86, 1.14, 0.04, 2), [], 0.0, 0.02, 0.006, "M_Stn_NavyLight")
    parts.append(scr.transform(t=(0, 0, D / 2 - 0.004)))
    glass = slab(rounded_rect_loop(-0.18, 0.18, 0.89, 1.11, 0.03, 2), [], 0.0, 0.015, 0.004, "M_Stn_Window")
    parts.append(glass.transform(t=(0, 0, D / 2 + 0.012)))
    for k, x in enumerate((-0.15, -0.05, 0.05, 0.15)):
        parts.append(rbox(x - 0.035, x + 0.035, 0.70, 0.76, D / 2 - 0.01, D / 2 + 0.04, 0.015, "M_Stn_Yellow"))
    parts.append(rbox(-0.17, 0.17, 0.30, 0.48, D / 2 - 0.02, D / 2 + 0.05, 0.03, "M_Stn_Dark"))
    parts.append(rbox(-0.13, 0.13, 0.38, 0.40, D / 2 + 0.04, D / 2 + 0.065, 0.005, "M_Stn_Yellow"))
    parts.append(rbox(0.16, 0.24, 0.62, 0.66, D / 2 - 0.01, D / 2 + 0.03, 0.01, "M_Stn_Dark"))           # muntgleuf
    col = col_box(-W / 2 - 0.03, W / 2 + 0.03, 0.0, H, -D / 2 - 0.03, D / 2 + 0.05)
    return mk.merge(*parts), col


def suitcase(w, h, d, mat, strap="M_Stn_Yellow", handle=True):
    parts = [rbox(-w / 2, w / 2, 0.0, h, -d / 2, d / 2, min(0.05, h / 4), mat)]
    for x in (-w * 0.28, w * 0.28):
        parts.append(rbox(x - 0.025, x + 0.025, -0.006, h + 0.006, -d / 2 - 0.008, d / 2 + 0.008, 0.012, strap))
    for x in (-w / 2 + 0.03, w / 2 - 0.03):
        for z in (-d / 2 + 0.03, d / 2 - 0.03):
            parts.append(ball(0.028, "M_Stn_Yellow", 8, 5).transform(t=(x, h - 0.03, z)))
    if handle:
        parts.append(tube([(-0.08, h, 0), (-0.06, h + 0.07, 0), (0.06, h + 0.07, 0), (0.08, h, 0)], 0.016,
                          "M_Stn_Dark", smooth=True, n=10, seg=6))
    return mk.merge(*parts)


def luggage_cart():
    """Bagagekar: geel buisframe met duwbeugel, houten laadvloer, vier wielen, bruine koffer + marine koffertje."""
    L, W = 1.10, 0.66
    y_deck = 0.28
    parts = [rbox(-L / 2, L / 2, y_deck - 0.05, y_deck, -W / 2, W / 2, 0.02, "M_Stn_WoodLight"),
             rbox(-L / 2 - 0.02, L / 2 + 0.02, y_deck - 0.10, y_deck - 0.04, -W / 2 - 0.02, W / 2 + 0.02, 0.02, "M_Stn_Yellow")]
    for x in (-L / 2 + 0.16, L / 2 - 0.16):
        for z in (-W / 2 + 0.04, W / 2 - 0.04):
            wheel = mk.merge(cyl(0.11, 0.06, 14, 0.015, "M_Stn_Dark", axis="z"),
                             cyl(0.045, 0.075, 10, 0.01, "M_Stn_Yellow", axis="z"))
            parts.append(wheel.transform(t=(x, 0.11, z + np.sign(z) * 0.04)))
            parts.append(rbox(x - 0.02, x + 0.02, 0.11, y_deck - 0.08, z - 0.02, z + 0.02, 0.01, "M_Stn_Yellow"))
    for z in (-W / 2 + 0.03, W / 2 - 0.03):                          # duwbeugel
        parts.append(tube([(L / 2 - 0.03, y_deck - 0.06, z), (L / 2 + 0.02, 0.70, z), (L / 2 + 0.02, 1.02, z)], 0.028,
                          "M_Stn_Yellow", smooth=True, n=14, seg=8))
    parts.append(tube([(L / 2 + 0.02, 1.02, -W / 2 + 0.03), (L / 2 + 0.02, 1.02, W / 2 - 0.03)], 0.032, "M_Stn_Yellow", seg=8))
    parts.append(tube([(L / 2 + 0.02, 0.62, -W / 2 + 0.03), (L / 2 + 0.02, 0.62, W / 2 - 0.03)], 0.022, "M_Stn_Yellow", seg=8))
    parts.append(suitcase(0.78, 0.34, 0.52, "M_Stn_Timber").transform(t=(-0.10, y_deck, 0.0)))
    parts.append(suitcase(0.52, 0.24, 0.36, "M_Stn_Navy", strap="M_Stn_TimberDark").transform(R=mk.rot_y(0.12), t=(-0.12, y_deck + 0.34, 0.02)))
    cols = (col_box(-L / 2 - 0.02, L / 2 + 0.06, 0.0, y_deck + 0.64, -W / 2 - 0.08, W / 2 + 0.08),
            col_box(L / 2 - 0.03, L / 2 + 0.06, 0.0, 1.06, -W / 2, W / 2))
    return mk.merge(*parts), cols


def level_crossing(arm=3.2):
    """Overweg: paal met Andreaskruis, twee rode knipperlichten, gele aandrijfkast en rood-witte slagboom.
    Geeft (mesh, collider, boom_mesh, boom_pivot): de boom draait om +Z bij boom_pivot (open = 80 graden)."""
    parts = [rbox(-0.22, 0.22, 0.0, 0.10, -0.22, 0.22, 0.03, "M_Stn_Stone"),
             rbox(-0.07, 0.07, 0.08, 2.75, -0.07, 0.07, 0.03, "M_Stn_Dark")]
    for i, ang in enumerate((35, -35)):                              # Andreaskruis: tweede plank ervóór
        L, w, dz = 1.05, 0.15, 0.065 * i
        board = [rbox(-L / 2, L / 2, -w / 2, w / 2, 0.08 + dz, 0.13 + dz, 0.03, "M_Stn_White")]
        for k in (-2, 0, 2):
            x0 = k * L / 6 - L / 12
            board.append(rbox(x0, x0 + L / 6, -w / 2 - 0.008, w / 2 + 0.008, 0.07 + dz, 0.14 + dz, 0.02, "M_Stn_Red"))
        parts.append(mk.merge(*board).transform(R=mk.rot_z(math.radians(ang)), t=(0, 2.55, 0)))
    parts.append(rbox(-0.55, 0.55, 1.73, 1.83, -0.05, 0.05, 0.02, "M_Stn_Dark"))           # lichtbalk
    for x in (-0.33, 0.33):
        parts.append(disc(0.21, -0.06, 0.06, "M_Stn_Dark", 24, 0.03).transform(t=(x, 1.78, 0.06)))
        parts.append(disc(0.135, 0.0, 0.05, "M_Stn_SignalRed", 20, 0.02).transform(t=(x, 1.78, 0.11)))
        hood = slab(arch_loop(-0.19, 0.19, -0.02, 0.0, 10), [arch_loop(-0.15, 0.15, -0.03, 0.0, 10)], 0.0, 0.16, 0.01,
                    "M_Stn_Dark")
        parts.append(hood.transform(t=(x, 1.81, 0.10)))
    # aandrijfkast
    parts.append(rbox(-0.24, 0.24, 0.08, 0.90, -0.20, 0.22, 0.06, "M_Stn_Yellow").transform(t=(0.36, 0, 0)))
    parts.append(rbox(-0.18, 0.18, 0.86, 0.94, -0.16, 0.18, 0.03, "M_Stn_YellowDark").transform(t=(0.36, 0, 0)))
    parts.append(cyl(0.09, 0.08, 14, 0.02, "M_Stn_Dark", axis="z").transform(t=(0.36, 0.70, 0.25)))
    # slagboom (aparte knoop): rood-wit, contragewicht aan de korte kant
    segs = 6
    boom = [cyl(0.05, 0.12, 12, 0.01, "M_Stn_Dark", axis="z")]
    for k in range(segs):
        x0 = 0.15 + k * (arm - 0.15) / segs
        x1 = 0.15 + (k + 1) * (arm - 0.15) / segs
        boom.append(rbox(x0 + 0.004, x1 - 0.004, -0.065, 0.065, -0.04, 0.04, 0.03, "M_Stn_Red" if k % 2 == 0 else "M_Stn_White"))
    boom.append(rbox(-0.45, 0.0, -0.09, 0.09, -0.05, 0.05, 0.04, "M_Stn_Dark"))
    boom.append(ball(0.06, "M_Stn_Red", 10, 6).transform(t=(arm + 0.02, 0, 0)))
    boom_mesh = mk.merge(*boom)
    col = mk.merge(col_box(-0.22, 0.22, 0.0, 2.95, -0.22, 0.22), col_box(0.12, 0.60, 0.08, 0.94, -0.20, 0.22))
    boom_col = col_box(-0.45, arm + 0.08, -0.09, 0.09, -0.06, 0.06)
    return mk.merge(*parts), col, boom_mesh, boom_col, (0.36, 0.70, 0.30)


def fence(length=2.0):
    """Houten hek: twee palen met puntige kop, twee liggers; sluit naadloos aan op een volgend stuk (+X)."""
    L = length
    parts = []
    # de paal aan het eind (x = L) is een fractie kleiner dan die aan het begin: aan elkaar geklikt zit hij dan
    # helemaal ín de beginpaal van het volgende stuk, in plaats van er precies samen mee te vallen (z-fighting)
    for x, h, k in ((0.0, 0.075, 1.0), (L, 0.064, 0.0)):
        parts.append(rbox(x - h, x + h, 0.0, 1.00 - 0.012 * (1 - k), -h, h, 0.03, "M_Stn_Timber"))
        cap = [(0.0, 0.99), (0.105, 0.99), (0.0, 1.10)] if k else [(0.0, 0.997), (0.090, 0.997), (0.0, 1.085)]
        parts.append(mk.lathe(cap, 4, mat="M_Stn_TimberDark", phase=0.5).transform(t=(x, 0, 0)))
    for y in (0.40, 0.75):
        parts.append(rbox(0.0, L, y - 0.07, y + 0.07, 0.05, 0.12, 0.03, "M_Stn_Timber"))
    col = col_box(-0.08, L + 0.08, 0.0, 1.1, -0.08, 0.13)
    return mk.merge(*parts), col


def shelter(W=3.4, D=2.2):
    """Wachthuisje: vier houten palen op stenen voeten, korbelen, marine zadeldak met nokbalk, hangbord en lantaarn."""
    H = 2.30
    parts = []
    xs, zs = (-W / 2 + 0.15, W / 2 - 0.15), (-D / 2 + 0.15, D / 2 - 0.15)
    for x in xs:
        for z in zs:
            parts.append(rbox(x - 0.20, x + 0.20, 0.0, 0.22, z - 0.20, z + 0.20, 0.05, "M_Stn_Stone"))
            parts.append(rbox(x - 0.12, x + 0.12, 0.18, H, z - 0.12, z + 0.12, 0.04, "M_Stn_Timber"))
    for z in zs:                                                      # langsbalken + korbelen
        parts.append(rbox(-W / 2 - 0.05, W / 2 + 0.05, H, H + 0.22, z - 0.12, z + 0.12, 0.04, "M_Stn_Timber"))
        for x, sx in ((xs[0], 1), (xs[1], -1)):
            parts.append(capsule((x + sx * 0.06, H - 0.55, z), (x + sx * 0.55, H, z), 0.065, mat="M_Stn_Timber", seg=6, rings=1))
    for x in xs:
        parts.append(rbox(x - 0.12, x + 0.12, H, H + 0.16, -D / 2 - 0.05, D / 2 + 0.05, 0.04, "M_Stn_TimberDark"))
    roof, rcol = gable_roof(W + 0.7, D + 0.9, H + 0.22, 1.0, ridge_axis="x")
    parts.append(roof)
    sign, _ = hanging_sign(0.62, 0.48, 0.30)
    parts.append(sign.transform(t=(0.0, H - 0.02, zs[1])))
    col = mk.merge(*[col_box(x - 0.2, x + 0.2, 0, H, z - 0.2, z + 0.2) for x in xs for z in zs], rcol)
    return mk.merge(*parts), col


def gable_roof(L, D, y0, rise, ridge_axis="x", tiles=True, thick=0.16, rows=None, tile_mat="M_Stn_Roof",
               ridge_mat="M_Stn_RoofDark"):
    """Zadeldak met dikke dakpannen in rijen (concept), nokbalk en windveren; nok langs `ridge_axis`.
    L = lengte langs de nok, D = totale breedte, y0 = onderkant dakrand, rise = hoogte tot de nok."""
    half = D / 2
    pitch = math.atan2(rise, half)
    slope_len = math.hypot(half, rise)
    parts = []
    n_rows = rows or max(3, int(round(slope_len / 0.45)))
    row = slope_len / n_rows
    for side in (1, -1):
        # dakbeschot (doorlopende plaat) onder de pannen; de dakvlakken kruisen elkaar bij de nok, dus kant -1 is
        # aan de kopse kanten 1 cm korter (anders vallen de kopse vlakken daar samen: z-fighting)
        ins = 0.01 if side < 0 else 0.0
        deck = rbox(-L / 2 + ins, L / 2 - ins, -thick, 0.0, -slope_len, 0.0, 0.03, "M_Stn_TimberDark")
        deck = deck.transform(R=mk.rot_x(-pitch))
        parts.append(_orient_slope(deck, side, pitch, half, y0, rise))
        if tiles:
            n_t = max(2, int(round(L / 0.62)))
            tw = L / n_t
            for r in range(n_rows):
                s0 = -slope_len + r * row                           # van de goot naar de nok
                off = (tw / 2) * (r % 2)
                xs = np.arange(-L / 2 - off, L / 2 + 1e-6, tw)
                for x in xs:
                    xa, xb = max(x, -L / 2), min(x + tw, L / 2)
                    if xb - xa < 0.12:
                        continue
                    top = s0 + row + 0.10
                    e_ins = ins + 0.02 * (r % 2)                    # overlappende rijen: kopse kanten verspringen
                    xa_, xb_ = xa + (e_ins if xa <= -L / 2 + 1e-6 else 0), xb - (e_ins if xb >= L / 2 - 1e-6 else 0)
                    t = rbox(xa_ + 0.012, xb_ - 0.012, 0.0, 0.10, s0 - 0.02 - top, 0.0, 0.045, tile_mat, div=1)
                    t = t.transform(R=mk.rot_x(0.09), t=(0, 0.004, top))      # onderrand iets opgetild: geschubd
                    parts.append(_orient_slope(t.transform(R=mk.rot_x(-pitch)), side, pitch, half, y0, rise))
    # nokbalk
    parts.append(cyl(0.13, L + 0.06, 16, 0.03, ridge_mat, axis="x").transform(t=(0, y0 + rise + 0.07, 0)))
    m = mk.merge(*parts)
    col_parts = []
    for side in (1, -1):
        c = col_box(-L / 2, L / 2, -thick, 0.14, -slope_len, 0.0).transform(R=mk.rot_x(-pitch))
        col_parts.append(_orient_slope(c, side, pitch, half, y0, rise))
    col = mk.merge(*col_parts)
    if ridge_axis == "z":
        m = m.transform(R=mk.rot_y(math.pi / 2))
        col = col.transform(R=mk.rot_y(math.pi / 2))
    return m, col


def _orient_slope(mesh, side, pitch, half, y0, rise):
    """Een dakvlak gebouwd met de nok in de oorsprong en 'naar beneden' langs -Z (na rot_x(-pitch))
    naar zijn plek: nok op (y0 + rise, z = 0), goot op z = side * half."""
    m = mesh if side < 0 else mesh.transform(s=(1, 1, -1))
    return m.transform(t=(0, y0 + rise, 0))
