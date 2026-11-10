"""Props voor het hoofdstation en de briefingruimte (issue #207), naar
Docs/References/Environment/main-station-concept.png.

Zelfde conventies als station_props.py: elke functie geeft (mesh, collider) terug, pivot midden-onder op de
grond (of het ophangpunt bij wandspullen), Y omhoog, voorkant naar +Z, 1 unit = 1 m. Personages zijn
1,2-1,3 m; werkbladen liggen op ~0,9 m, borden beginnen boven de lambrisering.

Regel tegen dubbele vlakken (sectie G van de art style guide): opgelegde plaatjes (papiertjes, ventilatiesleuven,
icoontjes) staan minstens 6 mm voor het vlak waar ze op liggen, of zijn bol (koepeltjes, buizen).
"""
import math

import numpy as np

from station_common import (DEG, arch_loop, ball, capsule, circle_loop, clock, col_box, cyl, disc, lantern, mk,
                            rbox, rounded_rect_loop, slab, text_mesh, train_icon, tube)
import station_props as props


# ------------------------------------------------------------------ kluisjes
LOCKER_OPEN = 112                                   # graden: zo ver draait een kluisdeurtje open (om zijn scharnier)


def locker_door(wd, h):
    """Eén kluisdeurtje, lokaal: scharnier op x = 0 (linkerrand), blad naar +X, y = hoogte in de kast, voorkant +Z.
    Ventilatiesleuven, naambordje en gele knop zitten op het blad en draaien mee. Geeft (mesh, botsvorm)."""
    parts = [slab(rounded_rect_loop(0.0, wd, 0.12, h - 0.06, 0.03, 2), [], -0.011, 0.011, 0.008, "M_Stn_Locker")]
    for j in range(3):                                   # ventilatiesleuven (bolle staafjes)
        y = h - 0.22 - j * 0.07
        parts.append(capsule((0.09, y, 0.009), (wd - 0.09, y, 0.009), 0.014, mat="M_Stn_LockerDark", seg=6, rings=1))
    xc = wd / 2
    parts.append(slab(rounded_rect_loop(xc - 0.08, xc + 0.08, h - 0.48, h - 0.38, 0.02, 2), [], 0.003, 0.019, 0.004,
                      "M_Stn_Cream"))
    parts.append(ball(0.032, "M_Stn_Yellow", 10, 6).transform(t=(wd - 0.07, 0.86, 0.023)))
    for y in (0.34, h - 0.30):                           # scharnieren op de rand
        parts.append(rbox(-0.012, 0.035, y - 0.035, y + 0.035, -0.018, 0.018, 0.006, "M_Stn_LockerDark"))
    col = col_box(0.0, wd, 0.12, h - 0.06, -0.012, 0.012)
    return mk.merge(*parts), col


def _locker_contents(k, xa, xb, y_floor, y_shelf, z0, z1):
    """Wat er in kluisje k staat (vast, hoort bij de kast): laarzen, opgevouwen uniform, broodtrommel, lantaarn."""
    xm, zm = (xa + xb) / 2, (z0 + z1) / 2
    kind = k % 5
    if kind == 0:                                        # werklaarzen: schacht smaller dan de voet
        return [mk.merge(rbox(x - 0.045, x + 0.045, y_floor + 0.06, y_floor + 0.25, zm - 0.065, zm + 0.03, 0.025, "M_Stn_Dark"),
                         rbox(x - 0.058, x + 0.058, y_floor - 0.004, y_floor + 0.10, zm - 0.075, zm + 0.13, 0.03, "M_Stn_Dark"))
                for x in (xm - 0.075, xm + 0.075)]
    if kind == 1:                                        # stapel opgevouwen uniformen, elke laag iets kleiner
        return [rbox(xm - 0.15 + j * 0.008, xm + 0.15 - j * 0.008, y_shelf - 0.004 + j * 0.062, y_shelf + 0.066 + j * 0.062,
                     zm - 0.13 + j * 0.008, zm + 0.11 - j * 0.008, 0.025, ("M_Stn_Navy", "M_Stn_NavyLight", "M_Stn_Navy")[j])
                for j in range(3)]
    if kind == 2:                                        # broodtrommel met hengsel
        return [rbox(xm - 0.13, xm + 0.13, y_shelf - 0.004, y_shelf + 0.16, zm - 0.09, zm + 0.09, 0.03, "M_Stn_Red"),
                rbox(xm - 0.06, xm + 0.06, y_shelf + 0.15, y_shelf + 0.19, zm - 0.02, zm + 0.02, 0.012, "M_Stn_Cream")]
    if kind == 3:                                        # handlantaarn
        return [cyl(0.085, 0.05, 10, 0.012, "M_Stn_Metal").transform(t=(xm, y_floor + 0.021, zm)),
                cyl(0.062, 0.17, 10, 0.0, "M_Stn_Glow").transform(t=(xm, y_floor + 0.125, zm)),
                cyl(0.090, 0.05, 10, 0.014, "M_Stn_Metal").transform(t=(xm, y_floor + 0.23, zm))]
    return [rbox(xm - 0.14, xm + 0.14, y_floor - 0.004, y_floor + 0.30, zm - 0.11, zm + 0.10, 0.04, "M_Stn_Timber")]   # tas


def lockers(n=4, w=0.46, h=1.62, d=0.46, top_crate=True):
    """Rij blauwe personeelskluisjes die open kunnen. Voorkant +Z, pivot midden-onder.

    Geeft (kast, botsvormen, deurtjes):
      kast        open kast: achterwand, bodem, tussenschotten, per vak een schap en iets erin, plint en kroonlijst
      botsvormen  tuple losse blokken (achterwand, bodem, kap, schotten, schappen): je kunt in een open vak reiken
      deurtjes    [(naam, mesh, botsvorm, scharnierpositie)] in kastcoördinaten, dicht; elk deurtje draait om zijn
                  eigen Y-as (scharnier links) tot LOCKER_OPEN graden naar buiten
    """
    W = n * w
    zf = d / 2 - 0.02                                   # voorkant kast
    zb = -d / 2
    t = 0.028                                           # plaatdikte
    y_floor, y_shelf = 0.130, h - 0.44
    # Kast als schil van platen die in elkaar steken (nergens twee vlakken in hetzelfde vlak): zijwanden buitenom,
    # achterwand en bodem ertussen, tussenschotten en schappen daar weer in.
    parts = [rbox(-W / 2 + 0.010, W / 2 - 0.010, 0.092, h - 0.012, zb + 0.008, zb + 0.008 + t, 0.008, "M_Stn_LockerDark"),
             rbox(-W / 2 + 0.010, W / 2 - 0.010, 0.100, y_floor, zb + 0.016, zf - 0.014, 0.006, "M_Stn_LockerDark"),
             rbox(-W / 2 + 0.02, W / 2 - 0.02, 0.0, 0.09, zb + 0.02, d / 2 - 0.05, 0.015, "M_Stn_Dark"),  # plint
             rbox(-W / 2 - 0.03, W / 2 + 0.03, h - 0.02, h + 0.06, zb - 0.01, d / 2 + 0.02, 0.02, "M_Stn_Locker")]
    cols = [col_box(-W / 2, W / 2, 0.0, h, zb, zb + 0.008 + t),
            col_box(-W / 2, W / 2, 0.0, y_floor, zb, zf - 0.006),
            col_box(-W / 2 - 0.03, W / 2 + 0.03, h - 0.02, h + 0.06, zb - 0.01, d / 2 + 0.02)]
    for k in range(n + 1):                               # zijwanden (buitenom) en tussenschotten, net achter het deurvlak
        xs = -W / 2 + k * w
        if k in (0, n):
            xa, xb = (xs, xs + t) if k == 0 else (xs - t, xs)
            parts.append(rbox(xa, xb, 0.085, h - 0.005, zb, zf - 0.006, 0.010, "M_Stn_Locker"))
        else:
            xa, xb = xs - t / 2, xs + t / 2
            parts.append(rbox(xa, xb, 0.108, h - 0.006, zb + 0.024, zf - 0.006, 0.008, "M_Stn_Locker"))
        cols.append(col_box(xa, xb, 0.0, h, zb, zf - 0.006))
    doors = []
    for k in range(n):
        x0, x1 = -W / 2 + k * w + 0.025, -W / 2 + (k + 1) * w - 0.025
        xa, xb = -W / 2 + k * w + t / 2 + 0.004, -W / 2 + (k + 1) * w - t / 2 - 0.004
        parts.append(rbox(xa - 0.012, xb + 0.012, y_shelf - 0.024, y_shelf, zb + 0.030, zf - 0.07, 0.008, "M_Stn_LockerDark"))
        cols.append(col_box(xa, xb, y_shelf - 0.024, y_shelf, zb, zf - 0.07))
        parts.append(ball(0.020, "M_Stn_Yellow", 8, 5).transform(t=((xa + xb) / 2, y_shelf - 0.06, zb + t + 0.020)))   # haakje
        parts += _locker_contents(k, xa, xb, y_floor, y_shelf, zb + t, zf - 0.02)
        leaf, lcol = locker_door(x1 - x0, h)
        doors.append((f"LockerDoor_{k + 1}", leaf, lcol, (x0, 0.0, zf + 0.007)))
    if top_crate:
        m, _ = props.crate(0.42)
        parts.append(m.transform(R=mk.rot_y(0.12), t=(-W / 4, h + 0.06, -0.02)))
        parts.append(rbox(0.05, 0.45, h + 0.06, h + 0.24, -0.16, 0.12, 0.03, "M_Stn_Timber"))     # doos
        cols.append(col_box(-W / 4 - 0.24, 0.45, h + 0.06, h + 0.24, -0.22, 0.20))
    return mk.merge(*parts), tuple(cols), doors


def locker_door_nodes(doors, position=(0, 0, 0), rotation=None, opened=()):
    """Knopen voor export(): elk deurtje met botsvorm en scharnierstanden, geplaatst met de kast mee.
    opened = {nummer: graden} zet een deurtje in het model alvast op een kier of open."""
    opened = dict(opened or {})
    R = rotation if rotation is not None else mk.rot_y(0)
    nodes = []
    for k, (name, leaf, col, hinge) in enumerate(doors, 1):
        nodes += props.swing_door(name, leaf, col, R @ hinge + position, -LOCKER_OPEN, -opened.get(k, 0), closed=R)
    return nodes


# ------------------------------------------------------------------ borden
def _framed(w, h, inner_mat, frame_mat="M_Stn_Timber", depth=0.06, frame=0.08):
    """Lijst met paneel, voorkant +Z; paneel ligt 1 cm terug in de lijst. Geeft (mesh, voorkant paneel)."""
    fr = slab(rounded_rect_loop(-w / 2, w / 2, -h / 2, h / 2, 0.05, 2),
              [rounded_rect_loop(-w / 2 + frame, w / 2 - frame, -h / 2 + frame, h / 2 - frame, 0.02, 2)],
              -depth / 2, depth / 2, 0.015, frame_mat)
    zp = depth / 2 - 0.012
    panel = rbox(-w / 2 + frame - 0.01, w / 2 - frame + 0.01, -h / 2 + frame - 0.01, h / 2 - frame + 0.01,
                 -depth / 2 + 0.006, zp, 0.0, inner_mat)
    return mk.merge(fr, panel), zp


def route_board(w=3.0, h=1.55, seed=4):
    """Groen routebord (krijtbord) uit het concept: houten lijst, een kronkelende route met gele stations,
    boompjes en een krijtgootje. Pivot midden van het bord, voorkant +Z (wandversie)."""
    rng = np.random.default_rng(seed)
    m, zp = _framed(w, h, "M_Stn_Chalk", depth=0.07, frame=0.10)
    parts = [m]
    iw, ih = w / 2 - 0.30, h / 2 - 0.28
    route = [(-iw, -ih * 0.55), (-iw * 0.55, -ih * 0.05), (-iw * 0.15, -ih * 0.45), (iw * 0.25, ih * 0.15),
             (iw * 0.55, -ih * 0.25), (iw, ih * 0.55)]
    parts.append(tube([(x, y, zp) for x, y in route], 0.026, "M_Stn_Cream", smooth=True, n=48, seg=8))
    for x, y in route:                                    # stations: gele schijfjes met crème rand
        parts.append(disc(0.085, zp - 0.004, zp + 0.030, "M_Stn_Cream", 16, 0.008).transform(t=(x, y, 0)))
        parts.append(disc(0.058, zp + 0.024, zp + 0.040, "M_Stn_Yellow", 16, 0.006).transform(t=(x, y, 0)))
    for k in range(7):                                    # boompjes (krijtgroen, donkerder), los van de route
        x = -iw + 0.15 + k * (2 * iw - 0.3) / 6 + rng.uniform(-0.08, 0.08)
        y = ih * (0.62 if k % 2 else -0.82) + rng.uniform(-0.04, 0.04)
        s = rng.uniform(0.85, 1.15)
        tri = [(-0.09 * s, -0.08 * s), (0.09 * s, -0.08 * s), (0.0, 0.14 * s)]
        parts.append(slab(tri, [], zp - 0.004, zp + 0.010, 0.004, "M_Stn_ChalkDark").transform(t=(x, y, 0)))
        parts.append(rbox(x - 0.012, x + 0.012, y - 0.14 * s, y - 0.082 * s, zp - 0.004, zp + 0.008, 0.0, "M_Stn_Timber"))
    # krijtgootje met twee krijtjes en een wisser
    parts.append(rbox(-w / 2 + 0.25, w / 2 - 0.25, -h / 2 - 0.05, -h / 2 + 0.01, 0.0, 0.12, 0.015, "M_Stn_Timber"))
    for x, mat in ((-0.6, "M_Stn_Cream"), (-0.45, "M_Stn_Yellow")):
        parts.append(capsule((x, -h / 2 + 0.03, 0.07), (x + 0.10, -h / 2 + 0.03, 0.07), 0.012, mat=mat, seg=6, rings=1))
    parts.append(rbox(0.35, 0.60, -h / 2 + 0.01, -h / 2 + 0.07, 0.03, 0.10, 0.015, "M_Stn_Dark"))
    return mk.merge(*parts)


def notice_board(w=1.30, h=1.05, seed=2):
    """Prikbord: lijst, kurk, briefjes (crème/geel/wit, iets scheef) met punaises. Pivot midden, voorkant +Z."""
    rng = np.random.default_rng(seed)
    m, zp = _framed(w, h, "M_Stn_WoodLight", depth=0.06, frame=0.07)
    parts = [m]
    spots = [(-0.33, 0.20), (0.02, 0.24), (0.34, 0.16), (-0.30, -0.18), (0.05, -0.16), (0.36, -0.22)]
    for k, (x, y) in enumerate(spots):
        pw, ph = rng.uniform(0.20, 0.26), rng.uniform(0.24, 0.30)
        mat = ("M_Stn_Cream", "M_Stn_Yellow", "M_Stn_White")[k % 3]
        a = rng.uniform(-0.12, 0.12)
        z0 = zp - 0.002 + 0.007 * (k % 2)                # overlappende briefjes niet in één vlak
        note = slab(rounded_rect_loop(-pw / 2, pw / 2, -ph / 2, ph / 2, 0.012, 1), [], z0, z0 + 0.008, 0.002, mat)
        lines = []
        for j in range(3):                                # tekstregels als bolle streepjes
            yy = ph / 2 - 0.08 - j * 0.06
            lines.append(capsule((-pw / 2 + 0.05, yy, z0 + 0.008), (pw / 2 - 0.05 - 0.04 * (j % 2), yy, z0 + 0.008),
                                 0.007, mat="M_Stn_Navy", seg=5, rings=1))
        pin = ball(0.022, ("M_Stn_Red", "M_Stn_Navy", "M_Stn_Yellow")[k % 3], 8, 5).transform(t=(0, ph / 2 - 0.03, z0 + 0.018))
        parts.append(mk.merge(note, *lines, pin).transform(R=mk.rot_z(a), t=(x, y, 0)))
    return mk.merge(*parts)


def board_on_legs(board_mesh, w, h, y_mid, feet=0.32):
    """Zet een wandbord op twee poten met voetjes (vrijstaande versie)."""
    parts = [board_mesh.transform(t=(0, y_mid, 0))]
    for x in (-w / 2 + 0.12, w / 2 - 0.12):
        parts += [rbox(x - 0.045, x + 0.045, 0.0, y_mid + h / 2 - 0.05, -0.085, -0.035, 0.015, "M_Stn_Timber"),
                  rbox(x - 0.06, x + 0.06, 0.0, 0.06, -feet / 2 - 0.06, feet / 2 - 0.06, 0.015, "M_Stn_TimberDark")]
    col = mk.merge(col_box(-w / 2, w / 2, y_mid - h / 2, y_mid + h / 2, -0.10, 0.06),
                   col_box(-w / 2 + 0.05, w / 2 - 0.05, 0, y_mid - h / 2, -feet / 2 - 0.06, feet / 2 - 0.06))
    return mk.merge(*parts), col


def notice_board_prop():
    return board_on_legs(notice_board(), 1.30, 1.05, 1.15)


def route_board_prop():
    return board_on_legs(route_board(1.6, 1.0), 1.6, 1.0, 1.15)


def hub_sign(text="ROUTE", width=2.6, height=0.56):
    """Marine bord met gele rand, treinpictogram en tekst (zelfde stijl als het STATION-bord)."""
    return props.station_board(text=text, width=width, height=height, depth=0.10)


def coffee_sign(w=0.62, h=0.50):
    """Houten bordje met koffiekopje en stoompluimpjes; pivot midden, voorkant +Z."""
    m, zp = _framed(w, h, "M_Stn_Dark", frame_mat="M_Stn_Timber", depth=0.05, frame=0.06)
    cup = slab([(-0.12, 0.06), (0.10, 0.06), (0.08, -0.10), (-0.10, -0.10)], [], zp - 0.003, zp + 0.016, 0.004, "M_Stn_Cream")
    handle = mk.torus(0.045, 0.014, 12, 5, mat="M_Stn_Cream").transform(R=mk.rot_x(math.pi / 2), t=(0.11, -0.01, zp + 0.008))
    saucer = capsule((-0.16, -0.12, zp + 0.008), (0.14, -0.12, zp + 0.008), 0.014, mat="M_Stn_Cream", seg=6, rings=1)
    steam = [tube([(x, 0.10, zp + 0.008), (x + 0.025, 0.14, zp + 0.008), (x - 0.01, 0.18, zp + 0.008)], 0.009,
                  "M_Stn_Cream", smooth=True, n=8, seg=6) for x in (-0.06, 0.0, 0.06)]
    return mk.merge(m, cup, handle, saucer, *steam)


# ------------------------------------------------------------------ koffiebar
def mug(mat="M_Stn_Cream"):
    body = mk.lathe([(0.0, 0.0), (0.040, 0.0), (0.045, 0.01), (0.045, 0.10), (0.036, 0.10), (0.036, 0.02), (0.0, 0.02)],
                    12, mat=mat)
    handle = mk.torus(0.028, 0.009, 10, 5, mat=mat).transform(R=mk.rot_x(math.pi / 2), t=(0.052, 0.055, 0))
    return mk.merge(body, handle)


def espresso_machine():
    """Grijze espressomachine met donkere bovenplaat, twee uitlopen, lekbak, knoppen en kopjes erop."""
    parts = [rbox(-0.30, 0.30, 0.0, 0.42, -0.20, 0.18, 0.04, "M_Stn_Metal"),
             rbox(-0.32, 0.32, 0.42, 0.47, -0.22, 0.20, 0.02, "M_Stn_Dark"),
             rbox(-0.26, 0.26, 0.0, 0.05, 0.12, 0.30, 0.015, "M_Stn_Dark"),                      # lekbak
             rbox(-0.24, 0.24, 0.30, 0.38, 0.17, 0.215, 0.012, "M_Stn_Dark")]                     # bedieningsstrook
    for x in (-0.13, 0.13):
        parts.append(cyl(0.045, 0.06, 10, 0.01, "M_Stn_Dark", axis="y").transform(t=(x, 0.25, 0.20)))
        parts.append(capsule((x, 0.22, 0.20), (x, 0.16, 0.20), 0.012, mat="M_Stn_Metal", seg=6, rings=1))
        parts.append(capsule((x, 0.27, 0.215), (x, 0.27, 0.32), 0.016, mat="M_Stn_Dark", seg=6, rings=1))   # hendel
    for x in (-0.16, 0.0, 0.16):
        parts.append(ball(0.022, "M_Stn_Yellow", 8, 5).transform(t=(x, 0.34, 0.22)))
    for k, x in enumerate((-0.18, 0.0, 0.18)):
        parts.append(mug(("M_Stn_Cream", "M_Stn_White", "M_Stn_Cream")[k]).transform(t=(x, 0.47, -0.02)))
    return mk.merge(*parts)


def counter_bell():
    return mk.merge(mk.lathe([(0.0, 0.0), (0.075, 0.0), (0.075, 0.012), (0.0, 0.012)], 14, mat="M_Stn_Dark"),
                    mk.lathe([(0.0, 0.012), (0.062, 0.012), (0.058, 0.04), (0.042, 0.068), (0.016, 0.08), (0.0, 0.082)],
                             14, mat="M_Stn_Yellow"),
                    ball(0.014, "M_Stn_Yellow", 8, 5).transform(t=(0, 0.092, 0)))


def counter_screen():
    """Kassa-/computerschermpje op een voetje (concept), scherm naar +Z."""
    return mk.merge(rbox(-0.10, 0.10, 0.0, 0.02, -0.08, 0.06, 0.008, "M_Stn_Dark"),
                    rbox(-0.025, 0.025, 0.02, 0.16, -0.04, 0.0, 0.008, "M_Stn_Dark"),
                    rbox(-0.22, 0.22, 0.12, 0.42, -0.03, 0.02, 0.015, "M_Stn_Dark"),
                    rbox(-0.19, 0.19, 0.15, 0.39, 0.015, 0.026, 0.004, "M_Stn_NavyLight"))


def coffee_counter_items(L, h):
    return [
        ("Bell_Counter", counter_bell(), col_box(-0.075, 0.075, 0, 0.105, -0.075, 0.075),
         (L / 2 - 0.30, h, 0.12), mk.rot_y(0)),
        *[(f"Mug_Counter{k + 1}", mug(mat), col_box(-0.045, 0.085, 0, 0.10, -0.045, 0.045),
           (x, h, 0.15), mk.rot_y(0.6 * k))
          for k, (x, mat) in enumerate(((0.05, "M_Stn_Cream"), (0.22, "M_Stn_Red")))],
    ]


def coffee_counter(L=2.6, d=0.62, h=0.92, *, include_loose=True):
    """Koffiebar/balie: houten kast met panelen, licht houten werkblad met overstek naar voren (+Z), bel,
    kassaschermpje en mokken. Pivot midden-onder."""
    parts = [rbox(-L / 2, L / 2, 0.06, h - 0.05, -d / 2, d / 2 - 0.06, 0.03, "M_Stn_Timber"),
             rbox(-L / 2 + 0.03, L / 2 - 0.03, 0.0, 0.08, -d / 2 + 0.03, d / 2 - 0.09, 0.02, "M_Stn_TimberDark"),
             rbox(-L / 2 - 0.05, L / 2 + 0.05, h - 0.06, h, -d / 2 - 0.03, d / 2 + 0.04, 0.02, "M_Stn_WoodLight")]
    n = max(2, int(round(L / 0.62)))
    pw = (L - 0.12) / n
    zf = d / 2 - 0.06
    for k in range(n):                                    # panelen op de voorkant (6 mm voor de kast)
        x0 = -L / 2 + 0.06 + k * pw
        parts.append(slab(rounded_rect_loop(x0 + 0.05, x0 + pw - 0.05, 0.18, h - 0.17, 0.03, 2),
                          [rounded_rect_loop(x0 + 0.10, x0 + pw - 0.10, 0.23, h - 0.22, 0.02, 2)],
                          zf - 0.004, zf + 0.010, 0.004, "M_Stn_TimberDark"))
    if include_loose:
        parts.extend(mesh.transform(R=rot, t=pos) for _, mesh, _, pos, rot in coffee_counter_items(L, h))
    parts.append(counter_screen().transform(R=mk.rot_y(math.pi + 0.35), t=(-L / 2 + 0.45, h, -0.02)))
    col = col_box(-L / 2 - 0.05, L / 2 + 0.05, 0.0, h, -d / 2 - 0.03, d / 2 + 0.04)
    return mk.merge(*parts), col


def back_counter(L=2.4, d=0.50, h=0.90):
    """Werkbank tegen de muur (achter de bar) met espressomachine; rug naar -Z."""
    parts = [rbox(-L / 2, L / 2, 0.0, h - 0.05, -d / 2, d / 2 - 0.03, 0.03, "M_Stn_TimberDark"),
             rbox(-L / 2 - 0.02, L / 2 + 0.02, h - 0.05, h, -d / 2, d / 2, 0.02, "M_Stn_WoodLight")]
    for k in range(int(L / 0.6)):                         # laatjes (6 mm voor de kast)
        x = -L / 2 + 0.30 + k * 0.6
        parts.append(rbox(x - 0.25, x + 0.25, h - 0.30, h - 0.12, d / 2 - 0.035, d / 2 - 0.023, 0.006, "M_Stn_Timber"))
        parts.append(ball(0.02, "M_Stn_Yellow", 8, 5).transform(t=(x, h - 0.21, d / 2 - 0.018)))
    parts.append(espresso_machine().transform(t=(-L / 2 + 0.55, h, -0.02)))
    for k in range(3):
        parts.append(mug(("M_Stn_Cream", "M_Stn_White", "M_Stn_Red")[k]).transform(t=(0.2 + 0.14 * k, h, 0.05)))
    col = col_box(-L / 2 - 0.02, L / 2 + 0.02, 0.0, h + 0.5, -d / 2, d / 2)
    return mk.merge(*parts), col


def cup_shelf(L=1.4):
    """Wandplankje met mokken en een pot koffiebonen; pivot = muur (achterkant op z = 0), plank op y = 0."""
    parts = [rbox(-L / 2, L / 2, -0.04, 0.0, 0.0, 0.24, 0.01, "M_Stn_WoodLight")]
    for x in (-L / 2 + 0.15, L / 2 - 0.15):
        parts.append(slab([(0.0, 0.0), (0.20, 0.0), (0.0, -0.20)], [], -0.02, 0.02, 0.006, "M_Stn_Timber")
                     .transform(R=mk.rot_y(-math.pi / 2), t=(x, -0.04, 0.0)))
    for k, x in enumerate(np.arange(-L / 2 + 0.22, L / 2 - 0.30, 0.17)):
        parts.append(mug(("M_Stn_Cream", "M_Stn_Red", "M_Stn_White", "M_Stn_NavyLight")[k % 4]).transform(t=(x, 0.0, 0.12)))
    parts.append(mk.lathe([(0.0, 0.0), (0.07, 0.0), (0.07, 0.16), (0.05, 0.18), (0.0, 0.18)], 12, mat="M_Stn_Window")
                 .transform(t=(L / 2 - 0.16, 0.0, 0.12)))
    parts.append(cyl(0.055, 0.03, 12, 0.006, "M_Stn_Timber", axis="y").transform(t=(L / 2 - 0.16, 0.195, 0.12)))
    return mk.merge(*parts)


def coffee_counter_prop():
    """Vrijstaande koffiebar (balie + bel + scherm + mokken) voor hergebruik."""
    return coffee_counter(2.0)


# ------------------------------------------------------------------ kapstok
def conductor_cap():
    """Conducteurspet (zoals het personage): marine kroon, gouden band, donkere klep. Pivot = onderkant midden."""
    crown = mk.lathe([(0.0, 0.03), (0.12, 0.03), (0.14, 0.08), (0.17, 0.13), (0.15, 0.16), (0.0, 0.17)], 16,
                     mat="M_Stn_Navy").transform(s=(1.0, 1.0, 0.92))
    band = mk.torus(0.122, 0.022, 16, 5, mat="M_Stn_Yellow").transform(t=(0, 0.05, 0), s=(1.0, 1.0, 0.92))
    visor = slab([(-0.10, 0.0), (0.10, 0.0), (0.08, 0.09), (0.0, 0.11), (-0.08, 0.09)], [], -0.012, 0.012, 0.006,
                 "M_Stn_Dark").transform(R=mk.rot_x(math.pi / 2 - 0.25), t=(0, 0.035, 0.10))
    emblem = disc(0.028, 0.0, 0.012, "M_Stn_Yellow", 10, 0.004).transform(R=mk.rot_x(-0.25), t=(0, 0.10, 0.128))
    return mk.merge(crown, band, visor, emblem)


def hanging_jacket():
    """Marine conducteursjasje aan een haak: romp met revers, mouwen, gouden knopen. Pivot = haakpunt, hangt -Y."""
    body = slab([(-0.20, -0.78), (0.20, -0.78), (0.21, -0.20), (0.15, -0.04), (0.05, -0.02), (0.0, -0.10), (-0.05, -0.02),
                 (-0.15, -0.04), (-0.21, -0.20)], [], -0.07, 0.07, 0.03, "M_Stn_Navy")
    parts = [body]
    for s in (1, -1):
        parts.append(capsule((s * 0.17, -0.10, 0.0), (s * 0.23, -0.62, 0.015), 0.065, 0.055, seg=10, rings=2, mat="M_Stn_Navy"))
        parts.append(mk.torus(0.058, 0.014, 10, 4, mat="M_Stn_Yellow").transform(R=mk.rot_z(s * 0.11),
                                                                                 t=(s * 0.231, -0.58, 0.014)))
        parts.append(slab([(s * 0.0, -0.10), (s * 0.13, -0.05), (s * 0.06, -0.36), (s * 0.0, -0.40)], [], 0.06, 0.082,
                          0.006, "M_Stn_NavyLight"))                                              # revers
    for y in (-0.44, -0.56, -0.68):
        parts.append(ball(0.022, "M_Stn_Yellow", 8, 5).transform(t=(0.035, y, 0.075)))
    parts.append(tube([(0.0, -0.06, 0.0), (0.0, 0.02, 0.0)], 0.01, "M_Stn_Dark", seg=6))       # lusje
    return mk.merge(*parts)


def coat_rack_wall(L=1.2):
    """Kapstokplank aan de muur met drie gele haken, een conducteurspet en een jasje. Pivot = muur op hoogte van de
    plank (achterkant z = 0), voorkant +Z."""
    parts = [rbox(-L / 2, L / 2, -0.07, 0.07, 0.0, 0.05, 0.02, "M_Stn_Timber")]
    xs = (-L / 3, 0.0, L / 3)
    for x in xs:
        parts.append(tube([(x, 0.0, 0.05), (x, -0.01, 0.14), (x, 0.05, 0.18)], 0.016, "M_Stn_Yellow", smooth=True, n=8, seg=6))
        parts.append(ball(0.024, "M_Stn_Yellow", 8, 5).transform(t=(x, 0.055, 0.18)))
    # petten hangen aan hun band op de haak, licht voorover gekanteld (klep naar voren)
    parts.append(conductor_cap().transform(R=mk.rot_x(0.35) @ mk.rot_z(0.12), t=(xs[0], -0.06, 0.26)))
    parts.append(hanging_jacket().transform(t=(xs[1], 0.0, 0.17)))
    parts.append(conductor_cap().transform(R=mk.rot_x(0.35) @ mk.rot_z(-0.12), t=(xs[2], -0.06, 0.26)))
    return mk.merge(*parts)


def coat_stand():
    """Vrijstaande staande kapstok (concept, onderste rij): paal met voet en haken, pet en jasje."""
    H = 1.75
    parts = [mk.lathe([(0.0, 0.0), (0.24, 0.0), (0.26, 0.03), (0.10, 0.07), (0.05, 0.10), (0.0, 0.10)], 14, mat="M_Stn_Timber"),
             capsule((0, 0.08, 0), (0, H, 0), 0.04, seg=10, rings=1, mat="M_Stn_Timber"),
             ball(0.055, "M_Stn_Yellow", 10, 6).transform(t=(0, H + 0.04, 0))]
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        d = np.array([math.cos(a), 0, math.sin(a)])
        parts.append(tube([(0, H - 0.20, 0), tuple(d * 0.12 + [0, H - 0.12, 0]), tuple(d * 0.16 + [0, H - 0.05, 0])],
                          0.015, "M_Stn_Yellow", smooth=True, n=8, seg=6))
    parts.append(conductor_cap().transform(R=mk.rot_x(-0.3), t=(0.0, H - 0.02, 0.12)))
    parts.append(hanging_jacket().transform(R=mk.rot_y(-0.5), t=(0.12, H - 0.10, -0.13)))
    col = col_box(-0.26, 0.26, 0, H + 0.1, -0.26, 0.26)
    return mk.merge(*parts), col


# ------------------------------------------------------------------ loket, wimpel, lamp, kleed
def ticket_booth():
    """Kaartjesloket (concept, onderste rij): houten kiosk op een stenen voet, boogloket met toonbankje,
    crème binnenkant, TICKETS-bord met gele rand op het dak. Voorkant +Z."""
    W, D, H = 1.50, 1.10, 2.10
    parts = [rbox(-W / 2 - 0.05, W / 2 + 0.05, 0.0, 0.16, -D / 2 - 0.05, D / 2 + 0.05, 0.04, "M_Stn_Stone")]
    front = slab(rounded_rect_loop(-W / 2, W / 2, 0.16, H, 0.04, 2), [arch_loop(-0.42, 0.42, 0.95, 1.55, 12)],
                 D / 2 - 0.08, D / 2, 0.02, "M_Stn_Timber")
    parts.append(front)
    parts.append(rbox(-W / 2, W / 2, 0.16, H, -D / 2, D / 2 - 0.08, 0.03, "M_Stn_TimberDark"))   # romp
    parts.append(rbox(-0.40, 0.40, 0.95, 1.55, D / 2 - 0.62, D / 2 - 0.10, 0.0, "M_Stn_Cream"))   # nis (binnen)
    parts.append(rbox(-0.55, 0.55, 0.90, 0.97, D / 2 - 0.12, D / 2 + 0.16, 0.02, "M_Stn_WoodLight"))  # toonbankje
    for x in (-W / 2 + 0.06, W / 2 - 0.06):               # hoekstijlen
        parts.append(rbox(x - 0.07, x + 0.07, 0.16, H + 0.02, D / 2 - 0.06, D / 2 + 0.04, 0.02, "M_Stn_Timber"))
    parts.append(rbox(-W / 2 - 0.08, W / 2 + 0.08, H - 0.04, H + 0.10, -D / 2 - 0.08, D / 2 + 0.12, 0.03, "M_Stn_Timber"))
    roof, _ = props.gable_roof(W + 0.4, D + 0.5, H + 0.10, 0.42, ridge_axis="x", tile_mat="M_Stn_RoofRed",
                               ridge_mat="M_Stn_RoofRedDark", rows=3)
    parts.append(roof)
    sign = props.station_board(text="TICKETS", width=1.25, height=0.36, depth=0.08)
    parts.append(sign.transform(t=(0, H + 0.42, D / 2 + 0.28)))
    for x in (-0.40, 0.40):
        parts.append(capsule((x, H + 0.08, D / 2 + 0.22), (x, H + 0.26, D / 2 + 0.22), 0.016, mat="M_Stn_Dark", seg=6, rings=1))
    parts.append(lantern(0.7).transform(t=(0, 1.60, D / 2 - 0.32)))
    col = mk.merge(col_box(-W / 2 - 0.05, W / 2 + 0.05, 0, H + 0.1, -D / 2 - 0.05, D / 2 + 0.05),
                   col_box(-0.55, 0.55, 0.90, 0.97, D / 2, D / 2 + 0.16))
    return mk.merge(*parts), col


def banner(w=0.52, h=0.95, arm=0.62):
    """Marine wimpel met gele rand en crème treinpictogram aan een smeedijzeren arm. Pivot = muurplaat
    (muur in het XY-vlak), arm steekt naar +Z, wimpel hangt eronder (vlak in YZ-vlak)."""
    cloth_out = [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, -h + 0.18), (0.0, -h), (-w / 2, -h + 0.18)]
    cloth_in = [(-w / 2 + 0.05, -0.05), (w / 2 - 0.05, -0.05), (w / 2 - 0.05, -h + 0.21), (0.0, -h + 0.07),
                (-w / 2 + 0.05, -h + 0.21)]
    cloth = mk.merge(slab(cloth_out, [], -0.008, 0.008, 0.003, "M_Stn_Yellow"),
                     slab(cloth_in, [], -0.020, 0.020, 0.005, "M_Stn_Navy"))
    for s in (1, -1):
        cloth = mk.merge(cloth, train_icon(w * 0.62, 0.012, "M_Stn_Cream").transform(
            R=mk.rot_y(0 if s > 0 else math.pi), t=(0, -h * 0.42, s * 0.024)))
    cloth = cloth.transform(R=mk.rot_y(math.pi / 2), t=(0, -0.04, arm * 0.5 + 0.12))
    parts = [cloth,
             rbox(-0.06, 0.06, -0.14, 0.14, 0.0, 0.03, 0.012, "M_Stn_Navy"),
             capsule((0, 0.0, 0.02), (0, 0.0, arm + 0.12), 0.022, mat="M_Stn_Navy", seg=8, rings=1),
             tube([(0, -0.14, 0.03), (0, -0.06, 0.09), (0, -0.01, 0.15)], 0.014, "M_Stn_Navy", smooth=True, n=8, seg=6),
             ball(0.045, "M_Stn_Yellow", 10, 6).transform(t=(0, 0.0, arm + 0.16))]
    for z in (arm * 0.5 + 0.12 - w / 2 + 0.06, arm * 0.5 + 0.12 + w / 2 - 0.06):
        parts.append(mk.torus(0.028, 0.008, 8, 4, mat="M_Stn_Dark").transform(R=mk.rot_x(math.pi / 2), t=(0, -0.01, z)))
    return mk.merge(*parts), None


def pendant_lamp(drop=1.0):
    """Hanglamp uit het concept: snoer, marine koepelkap met gele rand, gloeiende lamp. Pivot = ophangpunt."""
    parts = [capsule((0, 0, 0), (0, -drop + 0.20, 0), 0.010, mat="M_Stn_Dark", seg=5, rings=1),
             mk.lathe([(0.0, -drop + 0.22), (0.05, -drop + 0.22), (0.07, -drop + 0.16), (0.24, -drop + 0.02),
                       (0.30, -drop - 0.04), (0.28, -drop - 0.05), (0.0, -drop + 0.12)], 16, mat="M_Stn_Navy"),
             mk.torus(0.29, 0.016, 16, 4, mat="M_Stn_Yellow").transform(t=(0, -drop - 0.04, 0)),
             ball(0.085, "M_Stn_Glow", 12, 8).transform(t=(0, -drop - 0.02, 0))]
    return mk.merge(*parts)


def round_rug(r=1.25):
    """Rond kleed (concept): blauw met een lichtere rand en een crème treinpictogram. 2 cm dik, onderkant 1 cm in
    de vloer; rand en pictogram liggen 6 mm boven het kleed."""
    parts = [disc(r, -0.010, 0.020, "M_Stn_Locker", 40, 0.008),
             slab(circle_loop(0, 0, r - 0.10, 40), [circle_loop(0, 0, r - 0.20, 40)], 0.014, 0.026, 0.004, "M_Stn_NavyLight"),
             train_icon(r * 0.85, 0.012, "M_Stn_Cream").transform(t=(0, 0, 0.014))]
    return mk.merge(*parts).transform(R=mk.rot_x(-math.pi / 2))


def potted_plant(seed=7, h=1.0):
    """Grote kamerplant in een houten pot (zelfde opbouw als in het stationnetje)."""
    s = h / 1.0
    pot = mk.merge(mk.lathe([(0.0, 0.0), (0.18, 0.0), (0.24, 0.40), (0.0, 0.40)], 12, mat="M_Stn_Timber"),
                   mk.lathe([(0.0, 0.36), (0.22, 0.36), (0.22, 0.38), (0.0, 0.38)], 12, mat="M_Stn_Soil"),
                   props.bush_mass(np.random.default_rng(seed), 0.35 * s, 0.35 * s, 0.45 * s, 3).transform(t=(0, 0.30, 0)),
                   props.bush_mass(np.random.default_rng(seed + 2), 0.30 * s, 0.30 * s, 0.40 * s, 3)
                   .transform(R=mk.rot_y(1.3), t=(0, 0.30 + 0.22 * s, 0)))
    col = col_box(-0.25, 0.25, 0, 0.9 * s, -0.25, 0.25)
    return pot, col
