"""Briefingruimte, crew-uitrusting: kledingrek met de drie crew-tenues en een tafel met extraatjes.

  * Prop_CrewWardrobe     rek (marine poten, koperen stang, plank) met drie hangers: Outfit_Conductor (marine
                          jasje met gouden knopen), Outfit_Engineer (overall met reflectiebanden en bretels),
                          Outfit_Stoker (crème trui met rode sjaal); op de plank pet, helm en muts; bord CREW.
                          Elke Outfit_* heeft een eigen botsvorm: in Unity pak je een tenue vast om te wisselen.
  * Prop_AccessoryTable   houten tafel met marine kleed en gele zoom, staande ovale spiegel en bordje EXTRA;
                          de echte accessoires (CrewAccessories.fbx) worden er in Unity op gelegd.
Pivot = midden onder, voorkant +Z.
"""
import math

import numpy as np

from station_common import ball, capsule, col_box, cyl, disc, mk, rbox, slab, text_mesh, tube, circle_loop
import main_station_props as mp

RAIL_Y = 1.78
HOOK_X = (-0.78, 0.0, 0.78)


def _hanger(mat="M_Stn_Metal"):
    """Haak + kleerhanger, pivot = bovenkant haak op de stang; schouders op y = -0.16."""
    hook = tube([(0.0, -0.06, 0.0), (0.0, 0.02, 0.0), (0.03, 0.06, 0.0), (0.06, 0.03, 0.0)], 0.009, mat, smooth=True,
                n=12, seg=6)
    bar = tube([(-0.22, -0.16, 0.0), (0.0, -0.06, 0.0), (0.22, -0.16, 0.0)], 0.012, "M_Stn_Timber", smooth=True, n=14,
               seg=6)
    return mk.merge(hook, bar)


def _garment_shape(w_sh=0.22, w_hem=0.21, length=0.76, depth=0.07):
    return [(-w_hem, -length), (w_hem, -length), (w_sh, -0.22), (w_sh - 0.06, -0.06), (0.05, -0.04), (0.0, -0.12),
            (-0.05, -0.04), (-w_sh + 0.06, -0.06), (-w_sh, -0.22)], depth


def conductor_outfit():
    return mp.hanging_jacket().transform(t=(0, -0.12, 0))


def engineer_outfit():
    pts, d = _garment_shape(0.21, 0.23, 0.95)
    parts = [slab(pts, [], -d, d, 0.03, "M_Stn_Navy").transform(t=(0, -0.12, 0))]
    for s in (1, -1):
        parts.append(capsule((s * 0.17, -0.20, 0.0), (s * 0.22, -0.66, 0.012), 0.062, 0.054, seg=10, rings=2,
                             mat="M_Stn_Navy"))
        parts.append(mk.torus(0.058, 0.012, 10, 4, mat="M_Stn_White").transform(R=mk.rot_z(s * 0.11), t=(s * 0.215, -0.56, 0.012)))
        parts.append(rbox(s * 0.11 - 0.025, s * 0.11 + 0.025, -0.62, -0.18, d - 0.004, d + 0.012, 0.008, "M_Stn_Timber"))
        parts.append(ball(0.018, "M_Stn_Yellow", 8, 5).transform(t=(s * 0.11, -0.30, d + 0.02)))
    for y in (-0.98, -0.90):                                   # reflectiebanden onderaan
        parts.append(rbox(-0.235, 0.235, y - 0.022, y + 0.022, -d - 0.008, d + 0.008, 0.008, "M_Stn_White"))
    parts.append(rbox(-0.06, 0.06, -0.24, -0.14, -0.012, d + 0.004, 0.01, "M_Stn_Cream"))   # V-hals shirt
    parts.append(rbox(-0.12, 0.12, -0.70, -0.62, -d - 0.02, d + 0.02, 0.02, "M_Stn_Timber"))  # gordel
    return mk.merge(*parts)


def stoker_outfit():
    pts, d = _garment_shape(0.23, 0.22, 0.70, 0.08)
    parts = [slab(pts, [], -d, d, 0.035, "M_Stn_Cream").transform(t=(0, -0.12, 0))]
    for s in (1, -1):
        parts.append(capsule((s * 0.18, -0.20, 0.0), (s * 0.23, -0.62, 0.012), 0.068, 0.060, seg=10, rings=2,
                             mat="M_Stn_Cream"))
        parts.append(mk.torus(0.062, 0.016, 10, 4, mat="M_Stn_PlasterIn").transform(R=mk.rot_z(s * 0.11),
                                                                                     t=(s * 0.226, -0.60, 0.012)))
    parts.append(rbox(-0.225, 0.225, -0.84, -0.77, -d - 0.012, d + 0.012, 0.02, "M_Stn_PlasterIn"))     # geribde zoom
    # sjaal: lus over de hanger en twee hangende punten met franje
    loop = [(0.22 * math.cos(a), -0.15 + 0.06 * math.sin(a), (d + 0.04) * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 25)]
    parts.append(tube(loop[:-1], 0.04, "M_Stn_Scarf", closed=True, seg=8))
    for x, L in ((0.10, 0.55), (-0.13, 0.42)):
        parts.append(rbox(x - 0.055, x + 0.055, -0.15 - L, -0.13, d + 0.03, d + 0.085, 0.02, "M_Stn_Scarf"))
        for k in range(4):
            fx = x - 0.04 + k * 0.027
            parts.append(capsule((fx, -0.15 - L, d + 0.058), (fx, -0.21 - L, d + 0.058), 0.008, mat="M_Stn_Scarf", seg=5,
                                 rings=1))
    return mk.merge(*parts)


def helmet():
    dome = mk.lathe([(0.0, 0.0), (0.15, 0.0), (0.16, 0.02), (0.15, 0.08), (0.11, 0.14), (0.0, 0.16)], 18, mat="M_Stn_Yellow")
    brim = mk.lathe([(0.15, 0.0), (0.20, -0.005), (0.20, 0.012), (0.15, 0.018)], 18, closed=True, mat="M_Stn_Yellow")
    lamp = cyl(0.035, 0.03, 12, 0.008, "M_Stn_Glow", axis="z").transform(t=(0, 0.06, 0.155))
    return mk.merge(dome, brim, lamp)


def beanie():
    body = mk.lathe([(0.0, 0.0), (0.14, 0.0), (0.145, 0.07), (0.12, 0.15), (0.06, 0.19), (0.0, 0.20)], 16,
                    mat="M_Stn_Beanie")
    cuff = mk.torus(0.145, 0.025, 16, 6, mat="M_Stn_BeanieDark").transform(t=(0, 0.03, 0))
    pom = ball(0.05, "M_Stn_Beanie", 10, 6).transform(t=(0, 0.23, 0))
    return mk.merge(body, cuff, pom)


def wardrobe():
    W = 2.3
    parts, cols = [], []
    for x in (-W / 2, W / 2):                                   # poten met voet en gele bol
        parts.append(rbox(x - 0.05, x + 0.05, 0.0, 0.06, -0.32, 0.32, 0.02, "M_Stn_Navy"))
        for z in (-0.30, 0.30):
            parts.append(ball(0.04, "M_Stn_Dark", 8, 5).transform(t=(x, 0.03, z)))
        parts.append(cyl(0.035, 2.1, 12, 0.0, "M_Stn_Navy", axis="y").transform(t=(x, 1.08, 0.0)))
        parts.append(ball(0.06, "M_Stn_Yellow", 10, 6).transform(t=(x, 2.17, 0.0)))
        cols.append(col_box(x - 0.06, x + 0.06, 0.0, 2.2, -0.34, 0.34))
    parts.append(cyl(0.025, W, 14, 0.0, "M_Stn_Yellow", axis="x").transform(t=(0.0, RAIL_Y, 0.0)))   # koperen stang
    parts.append(rbox(-W / 2 - 0.08, W / 2 + 0.08, 2.03, 2.08, -0.22, 0.22, 0.01, "M_Stn_Timber"))     # plank
    cols.append(col_box(-W / 2 - 0.08, W / 2 + 0.08, 2.03, 2.08, -0.22, 0.22))
    parts.append(mp.conductor_cap().transform(R=mk.rot_y(0.2), t=(HOOK_X[0], 2.08, 0.0)))
    parts.append(helmet().transform(R=mk.rot_y(-0.15), t=(HOOK_X[1], 2.08, 0.0)))
    parts.append(beanie().transform(R=mk.rot_y(0.3), t=(HOOK_X[2], 2.08, 0.0)))
    # bord CREW boven de plank
    board = mp.hub_sign("CREW", 1.3, 0.38)
    parts.append(board.transform(t=(0.0, 2.55, 0.05)))
    for x in (-0.4, 0.4):
        parts.append(capsule((x, 2.08, 0.0), (x, 2.40, 0.0), 0.012, mat="M_Stn_Dark", seg=5, rings=1))
    nodes = [("Rack", mk.merge(*parts), None, (0, 0, 0), None, True),
             ("Col_Rack", mk.merge(*cols), None, (0, 0, 0), None, False)]
    hanger = _hanger()
    for (name, garment), x in zip((("Outfit_Conductor", conductor_outfit()), ("Outfit_Engineer", engineer_outfit()),
                                    ("Outfit_Stoker", stoker_outfit())), HOOK_X):
        nodes.append((name, mk.merge(hanger, garment), None, (x, RAIL_Y, 0.0), None, True))
        nodes.append((f"Col_{name}", col_box(-0.27, 0.27, -1.10, 0.06, -0.12, 0.12), name, (0, 0, 0), None, False))
    return nodes


def accessory_table():
    L, D, H = 1.5, 0.72, 0.84
    parts = [rbox(-L / 2, L / 2, H - 0.05, H, -D / 2, D / 2, 0.02, "M_Stn_Timber")]
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(capsule((sx * (L / 2 - 0.10), 0.0, sz * (D / 2 - 0.10)), (sx * (L / 2 - 0.10), H - 0.05,
                                 sz * (D / 2 - 0.10)), 0.035, 0.03, seg=8, rings=1, mat="M_Stn_TimberDark"))
    # kleed: loper over het blad met een overhang aan de voorkant, gele zoom
    parts.append(rbox(-L / 2 + 0.12, L / 2 - 0.12, H, H + 0.008, -D / 2 - 0.004, D / 2 + 0.004, 0.003, "M_Stn_Navy"))
    parts.append(rbox(-L / 2 + 0.12, L / 2 - 0.12, H - 0.26, H + 0.006, D / 2 + 0.004, D / 2 + 0.014, 0.003, "M_Stn_Navy"))
    parts.append(rbox(-L / 2 + 0.11, L / 2 - 0.11, H - 0.29, H - 0.24, D / 2 + 0.006, D / 2 + 0.020, 0.005, "M_Stn_Yellow"))
    # staande spiegel achterop
    mirror = mk.merge(slab(circle_loop(0, 0, 0.30, 28), [circle_loop(0, 0, 0.25, 28)], -0.02, 0.02, 0.008, "M_Stn_Yellow")
                      .transform(s=(0.75, 1.0, 1.0)),
                      disc(0.255, -0.008, 0.008, "M_Stn_Glass", 28, 0.003).transform(s=(0.75, 1.0, 1.0)))
    parts.append(mirror.transform(R=mk.rot_x(-0.12), t=(0.42, H + 0.42, -D / 2 + 0.12)))
    parts.append(capsule((0.42, H, -D / 2 + 0.17), (0.42, H + 0.13, -D / 2 + 0.13), 0.02, mat="M_Stn_Yellow", seg=6, rings=1))
    parts.append(rbox(0.30, 0.54, H, H + 0.02, -D / 2 + 0.06, -D / 2 + 0.26, 0.008, "M_Stn_Yellow"))
    # bordje EXTRA
    txt, tw = text_mesh("EXTRA", 0.10, 0.012, "M_Stn_Navy")
    plate = rbox(-tw / 2 - 0.06, tw / 2 + 0.06, -0.08, 0.08, -0.012, 0.0, 0.01, "M_Stn_Cream")
    parts.append(mk.merge(plate, txt).transform(R=mk.rot_x(-0.35), t=(-0.42, H + 0.10, -D / 2 + 0.16)))
    parts.append(rbox(-0.56, -0.28, H, H + 0.02, -D / 2 + 0.08, -D / 2 + 0.24, 0.006, "M_Stn_Timber"))
    col = mk.merge(col_box(-L / 2, L / 2, 0.0, H + 0.01, -D / 2, D / 2 + 0.02),
                   col_box(0.18, 0.66, H, H + 0.75, -D / 2 + 0.04, -D / 2 + 0.24))
    return [("Table", mk.merge(*parts), None, (0, 0, 0), None, True),
            ("Col_Table", col, None, (0, 0, 0), None, False),
            ("Spot_Item_1", None, None, (-0.40, H + 0.01, 0.02), None, True),
            ("Spot_Item_2", None, None, (0.14, H + 0.01, 0.20), None, True)]


CREW_PROPS = {"Prop_CrewWardrobe": wardrobe, "Prop_AccessoryTable": accessory_table}
