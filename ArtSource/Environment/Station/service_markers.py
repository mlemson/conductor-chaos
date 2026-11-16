"""Vloermarkeringen en bakens voor de dienst (issue #229): vervangen de kale Unity-kubussen in de levels.

  Service_ZoneMark_Cargo     geel vrachtvak 3,2 x 1,7 m: geschilderd kader, gestippelde binnenlijn, hoekbeugels,
                             kratpictogram en VRACHT
  Service_ZoneMark_Luggage   groen bagagevak 2,5 x 1,7 m met kofferpictogram en BAGAGE
  Service_ZoneMark_Practice  geel oefenvak 1,75 x 1,25 m (introductie) met kofferpictogram en OEFEN
  Service_StopMarker         groen stopbaken: paal met een rond bord (locomotiefje) aan twee kanten
  Service_SwitchMarker       geel wisselbaken: lagere paal met een bord met wisselpijlen aan twee kanten

Vloermarkeringen: pivot midden-onder op het perron (y = 0), lange kant langs X, de tekst leest van +Z (de kant van
het stationsgebouw, waar de spelers vandaan komen). De verf ligt 4 mm boven het perron en is 12-16 mm dik; onderdelen
die elkaar raken verschillen in hoogte (geen dubbele vlakken).
Bakens: pivot midden-onder de paal; het bord staat in het XY-vlak (zichtbaar langs het spoor, vanaf +Z en -Z) en
steekt naar +X uit, dus +X wijst van het spoor af. Col_Main is alleen de paal (door de rest loop je niet tegenaan).
"""
import math

from station_common import DEG, ball, capsule, col_box, disc, mk, rbox, rounded_rect_loop, slab, text_mesh, train_icon, tube

Y0 = 0.004                      # onderkant van de verf, net boven het perron
PAINT_TOP = 0.016
ICON_TOP = 0.023                # >= 6 mm boven de verf waar ze elkaar raken


def _flat(mesh):
    """slab()/text_mesh() liggen in het XY-vlak met dikte langs Z: plat leggen (Z -> Y, letterboven -> -Z)."""
    return mesh.transform(R=mk.rot_x(-90 * DEG))


def _paint(loop, holes, y1, mat):
    """Platte verflaag: veelhoek (x, z) van Y0 tot y1."""
    return _flat(slab([(x, -z) for x, z in loop], [[(x, -z) for x, z in h] for h in holes], Y0, y1, 0.004, mat))


def _rrect(x0, x1, z0, z1, r, seg=4):
    return rounded_rect_loop(x0, x1, z0, z1, r, seg)


def _crate_icon(size, mat):
    """Kratje van bovenaf als pictogram: kader, kruisbalk en spijkerkopjes (balk iets hoger: geen dubbel vlak)."""
    h = size / 2
    w = 0.13 * size
    parts = [_paint(_rrect(-h, h, -h, h, 0.08 * size), [_rrect(-h + w, h - w, -h + w, h - w, 0.03 * size)], PAINT_TOP, mat)]
    # diagonale plank van hoek tot hoek, als een gedraaide balk
    L = math.hypot(2 * (h - w * 0.6), 2 * (h - w * 0.6))
    plank = _paint(_rrect(-L / 2, L / 2, -w * 0.45, w * 0.45, 0.02 * size), [], ICON_TOP, mat)
    parts.append(plank.transform(R=mk.rot_y(45 * DEG)))
    for sx in (-1, 1):
        for sz in (-1, 1):                                  # spijkerkopjes: hoger dan kader en plank
            parts.append(_flat(disc(0.03 * size, Y0, ICON_TOP + 0.007, mat, 8, 0.0)).transform(t=(sx * (h - w / 2), 0, sz * (h - w / 2))))
    return mk.merge(*parts)


def _suitcase_icon(size, mat):
    """Koffer van voren als pictogram: kast met hoeken, twee riemen (hoger), handvat erboven."""
    w, h = size, 0.72 * size
    t = 0.11 * size
    body = _paint(_rrect(-w / 2, w / 2, -h / 2, h / 2, 0.12 * size), [_rrect(-w / 2 + t, w / 2 - t, -h / 2 + t, h / 2 - t, 0.05 * size)],
                  PAINT_TOP, mat)
    straps = [_paint(_rrect(x - 0.045 * size, x + 0.045 * size, -h / 2 + t * 0.5, h / 2 - t * 0.5, 0.01), [], ICON_TOP, mat)
              for x in (-0.24 * size, 0.24 * size)]
    # handvat: boogje boven de koffer (in het vlak = richting -Z, net als de letterboven)
    hw, hh = 0.20 * size, 0.17 * size
    handle = _paint(_rrect(-hw, hw, -h / 2 - hh, -h / 2 + 0.02 * size, 0.06 * size),
                    [_rrect(-hw + 0.07 * size, hw - 0.07 * size, -h / 2 - hh + 0.07 * size, -h / 2 + 0.05 * size, 0.02 * size)],
                    ICON_TOP, mat)                          # hoger: overlapt de bovenrand van de koffer
    return mk.merge(body, *straps, handle)


def zone_mark(L, W, mat, icon, word, text_h=0.17):
    """Doelvak van L x W m: kader, gestippelde binnenlijn, hoekbeugels, pictogram en woord."""
    frame_w = 0.085
    parts = [_paint(_rrect(-L / 2, L / 2, -W / 2, W / 2, 0.16), [_rrect(-L / 2 + frame_w, L / 2 - frame_w, -W / 2 + frame_w,
                                                                       W / 2 - frame_w, 0.10)], PAINT_TOP, mat)]
    # hoekbeugels: L-vormen net binnen het kader (eigen hoogte: raken het kader niet)
    inset, arm, bw = 0.16, min(0.42, L * 0.16), 0.07
    for sx in (-1, 1):
        for sz in (-1, 1):
            x0, z0 = sx * (L / 2 - inset), sz * (W / 2 - inset)
            leg_x = _rrect(min(x0, x0 - sx * arm), max(x0, x0 - sx * arm), min(z0, z0 - sz * bw), max(z0, z0 - sz * bw), 0.02)
            leg_z = _rrect(min(x0, x0 - sx * bw), max(x0, x0 - sx * bw), min(z0 - sz * bw, z0 - sz * arm),
                           max(z0 - sz * bw, z0 - sz * arm), 0.02)
            parts.append(_paint(leg_x, [], PAINT_TOP, mat))
            parts.append(_paint(leg_z, [], PAINT_TOP, mat))
    # gestippelde binnenlijn tussen de hoekbeugels
    d_in = inset + 0.035
    dash, gap, lw = 0.20, 0.14, 0.045
    for sz in (-1, 1):                                    # lange kanten
        z = sz * (W / 2 - d_in)
        x = -L / 2 + inset + arm + gap
        while x + dash <= L / 2 - inset - arm - gap + 1e-6:
            parts.append(_paint(_rrect(x, x + dash, z - lw / 2, z + lw / 2, 0.018), [], PAINT_TOP, mat))
            x += dash + gap
    for sx in (-1, 1):                                    # korte kanten
        x = sx * (L / 2 - d_in)
        z = -W / 2 + inset + arm + gap
        while z + dash <= W / 2 - inset - arm - gap + 1e-6:
            parts.append(_paint(_rrect(x - lw / 2, x + lw / 2, z, z + dash, 0.018), [], PAINT_TOP, mat))
            z += dash + gap
    # pictogram links, woord rechts (samen gecentreerd)
    text, tw = text_mesh(word, text_h, PAINT_TOP - Y0, mat, stroke=0.24)
    size = min(0.56, W * 0.38)
    gap_iw = 0.20
    total = size + gap_iw + tw
    ix = -total / 2 + size / 2
    tx = -total / 2 + size + gap_iw + tw / 2
    parts.append((icon(size, mat)).transform(t=(ix, 0, 0)))
    parts.append(_flat(text.transform(t=(0, 0, Y0))).transform(t=(tx, 0, 0)))
    return mk.merge(*parts)


# ---------------------------------------------------------------------- bakens
def _post(height, board_x0, mat_post="M_Stn_Navy"):
    """Gietijzeren paal: achtkante voet, taps toelopend, twee gele sierringen en een gele knop bovenop."""
    parts = [
        mk.lathe([(0.12, 0.0), (0.12, 0.04), (0.085, 0.075), (0.0, 0.075)], 8, mat="M_Stn_Dark", phase=math.pi / 8),
        mk.lathe([(0.045, 0.06), (0.036, height)], 12, mat=mat_post),
        mk.lathe([(0.040, 0.16), (0.058, 0.17), (0.058, 0.19), (0.040, 0.20)], 12, mat="M_Stn_Yellow"),
        mk.lathe([(0.033, height - 0.02), (0.050, height - 0.01), (0.050, height + 0.015), (0.033, height + 0.025)], 12,
                 mat="M_Stn_Yellow"),
        ball(0.055, "M_Stn_Yellow", 14, 8).transform(t=(0, height + 0.07, 0)),
    ]
    return parts


def _two_sided(face, cx):
    """Een bordvoorkant voor +Z en dezelfde, om de as van het bord gedraaid, voor -Z (zichtbaar uit beide richtingen)."""
    return mk.merge(face, face.transform(t=(-cx, 0, 0)).transform(R=mk.rot_y(math.pi), t=(cx, 0, 0)))


def stop_marker():
    """Groen stopbaken: paal 1,30 m met een rond bord: crème rand, groen veld, locomotiefje."""
    H = 1.30
    r = 0.21
    cx, cy = 0.20, H - 0.20
    parts = _post(H, cx - r)
    board = slab(rounded_rect_loop(cx - r - 0.02, cx + r + 0.02, cy - r - 0.02, cy + r + 0.02, 0.10, 4), [], -0.025, 0.025,
                 0.012, "M_Stn_Navy")                                                # achterplaat, steekt rond uit
    parts.append(board)
    # beugels van paal naar bord
    for y in (cy - 0.12, cy + 0.12):
        parts.append(rbox(0.0, cx - r + 0.04, y - 0.025, y + 0.025, -0.018, 0.018, 0.008, "M_Stn_Dark"))
    face = mk.merge(disc(r, 0.020, 0.036, "M_Stn_Cream", 32, 0.006).transform(t=(cx, cy, 0)),
                    disc(r - 0.035, 0.034, 0.048, "M_Stn_Leaf", 32, 0.005).transform(t=(cx, cy, 0)),
                    train_icon(0.22, 0.012, "M_Stn_Cream").transform(t=(cx, cy, 0.046)))
    parts.append(_two_sided(face, cx))
    col = col_box(-0.12, 0.12, 0.0, H + 0.12, -0.12, 0.12)
    return [("Mesh", mk.merge(*parts), None, (0, 0, 0), None, True), ("Col_Main", col, None, (0, 0, 0), None, False)]


def _switch_arrows(size, depth, mat):
    """Wisselpictogram: rechte pijl omhoog en een tak die naar rechts afbuigt, met pijlpunten."""
    s = size
    r = 0.045 * s
    stem = tube([(0.0, -0.45 * s, 0.0), (0.0, 0.30 * s, 0.0)], r, mat, seg=8, phase=math.pi / 8)
    branch = tube([(0.0, -0.18 * s, 0.0), (0.06 * s, 0.02 * s, 0.0), (0.20 * s, 0.17 * s, 0.0), (0.30 * s, 0.24 * s, 0.0)],
                  r, mat, seg=8, smooth=True, n=10, phase=math.pi / 8, caps=(False, True))
    head = slab([(-0.14 * s, 0.28 * s), (0.14 * s, 0.28 * s), (0.0, 0.48 * s)], [], -r, r, 0.004, mat)
    a = math.atan2(0.07, 0.10)
    tip = slab([(-0.14 * s, 0.0), (0.14 * s, 0.0), (0.0, 0.20 * s)], [], -r, r, 0.004, mat).transform(
        R=mk.rot_z(-(math.pi / 2 - a)), t=(0.29 * s, 0.235 * s, 0.0))
    m = mk.merge(stem, branch, head, tip)
    m.V[:, 2] *= (depth / 2) / r                       # plat: dikte = depth
    return m.transform(t=(-0.05 * s, 0, depth / 2))


def switch_marker():
    """Geel wisselbaken: lage paal (0,95 m) met een geel ruitbord met wisselpijlen."""
    H = 0.95
    cx, cy, a = 0.20, H - 0.16, 0.20
    parts = _post(H, cx - a)
    back = slab(rounded_rect_loop(cx - a - 0.025, cx + a + 0.025, cy - a - 0.025, cy + a + 0.025, 0.07, 4), [], -0.025, 0.025,
                0.012, "M_Stn_Navy")
    parts.append(back)
    for y in (cy - 0.10, cy + 0.10):
        parts.append(rbox(0.0, cx - a + 0.04, y - 0.025, y + 0.025, -0.018, 0.018, 0.008, "M_Stn_Dark"))
    face = mk.merge(slab(rounded_rect_loop(cx - a, cx + a, cy - a, cy + a, 0.06, 4), [], 0.020, 0.036, 0.006, "M_Stn_Yellow"),
                    _switch_arrows(0.30, 0.012, "M_Stn_Navy").transform(t=(cx, cy, 0.034)))
    parts.append(_two_sided(face, cx))
    col = col_box(-0.12, 0.12, 0.0, H + 0.12, -0.12, 0.12)
    return [("Mesh", mk.merge(*parts), None, (0, 0, 0), None, True), ("Col_Main", col, None, (0, 0, 0), None, False)]


def _mark_nodes(L, W, mat, icon, word, text_h=0.17):
    return [("Mesh", zone_mark(L, W, mat, icon, word, text_h), None, (0, 0, 0), None, True)]


ASSETS = {
    "Service_ZoneMark_Cargo": (lambda: _mark_nodes(3.2, 1.7, "M_Stn_Yellow", _crate_icon, "VRACHT"), 700, 0.06),
    "Service_ZoneMark_Luggage": (lambda: _mark_nodes(2.5, 1.7, "M_Stn_Leaf", _suitcase_icon, "BAGAGE"), 700, 0.06),
    "Service_ZoneMark_Practice": (lambda: _mark_nodes(1.75, 1.25, "M_Stn_Yellow", _suitcase_icon, "OEFEN", 0.13), 700, 0.06),
    "Service_StopMarker": (stop_marker, 1400, 0.08),
    "Service_SwitchMarker": (switch_marker, 1400, 0.08),
}
