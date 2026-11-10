"""Boerderijdecor: plaatsbare stukken voor langs het spoor, in de stijl van de stationskit.

    Farm_Barn         schuur waar je in kunt lopen, met twee staldeuren die open en dicht kunnen (Door_L / Door_R)
    Farm_Shed         open afdak/stal met stro
    Farm_Fence_3m     weidehek, klikt aan elkaar langs +X (zoals Prop_Fence_2m)
    Farm_Gate         hekpoort van 2 m die open en dicht kan (Gate)
    Farm_Trough       drinkbak met water
    Farm_Haystack     hooiberg met paal
    Farm_HayRack      voerruif met hooi
    Farm_MilkStand    melkbussenplank met drie bussen
    Farm_Wheelbarrow  kruiwagen met hooi
    Farm_Scarecrow    vogelverschrikker

Elke functie geeft de knopenlijst voor export() terug: (naam, mesh, parent, pivot, rotatie, zichtbaar). Pivot
midden-onder (hek en poort: begin van het stuk, zoals het stationshek), voorkant +Z, 1 unit = 1 m.
Bewegende bladen komen uit station_props.swing_door(): SwingDoor in Unity doet de rest.
Vlakken liggen nergens samen in één vlak (zie Tools/zfight.py): onderdelen steken in elkaar of liggen >= 6 mm uit elkaar.
"""
import math

import numpy as np

from station_common import DEG, ball, capsule, col_box, cyl, mk, rbox, rounded_rect_loop, slab, tube
import station_props as props

STRAW, STRAW_D = "M_Stn_Straw", "M_Stn_StrawDark"
RED, RED_D = "M_Stn_BarnRed", "M_Stn_BarnRedDark"
CREAM, TIMBER, TIMBER_D = "M_Stn_Cream", "M_Stn_Timber", "M_Stn_TimberDark"


def _nodes(mesh, cols, extra=()):
    nodes = [("Mesh", mesh, None, (0, 0, 0), None, True)]
    for i, c in enumerate(cols):
        nodes.append(("Col_Main" if i == 0 else f"Col_{i + 1}", c, None, (0, 0, 0), None, False))
    return nodes + list(extra)


def _xz(mesh):
    """slab() extrudeert langs Z; dit legt hem plat: polygoon in XZ, dikte langs Y (z0..z1 wordt y0..y1)."""
    return mesh.transform(R=mk.rot_x(-90 * DEG))


def _hay_bale(L=0.84, H=0.40, D=0.52):
    parts = [mk.rounded_box(L, H, D, 0.05, 2, mat=STRAW).transform(t=(0, H / 2 + 0.008, 0))]
    for x in (-0.21, 0.21):
        parts.append(mk.rounded_box(0.06, H + 0.018, D + 0.02, 0.018, 2, mat="M_Stn_Timber").transform(t=(x, H / 2 + 0.009, 0)))
    return mk.merge(*parts)


def _heap(rx, ry, rz, seed, mat=STRAW):
    """Hoopje hooi: afgeplatte bol met een rommelige rand (lage resolutie, grote facetten)."""
    rng = np.random.default_rng(seed)
    m = ball(1.0, mat, 9, 5)
    V = m.V.copy()
    V *= 1.0 + rng.uniform(-0.10, 0.12, (len(V), 1))
    V[:, 1] = np.maximum(V[:, 1], -0.35)
    m.V = V * np.array([rx, ry, rz])
    return m


def _milk_can(h=0.58, r=0.16, mat="M_Stn_Metal"):
    prof = [(0.0, 0.0), (r - 0.02, 0.0), (r, 0.03), (r, h * 0.62), (r - 0.03, h * 0.74), (r * 0.66, h * 0.80),
            (r * 0.66, h * 0.92), (r * 0.84, h * 0.95), (r * 0.84, h * 0.985), (r * 0.30, h), (0.0, h)]
    can = mk.lathe(prof, 12, mat=mat)
    handles = [tube([(s * r * 0.92, h * 0.66, 0), (s * (r + 0.07), h * 0.72, 0), (s * (r + 0.07), h * 0.86, 0),
                     (s * r * 0.70, h * 0.90, 0)], 0.016, "M_Stn_Yellow", seg=6) for s in (1, -1)]
    return mk.merge(can, *handles)


# ---------------------------------------------------------------------- schuur
def barn(W=4.6, D=4.0, He=2.30, Hr=3.70, dw=0.96, dh=2.12):
    """Rode schuur met crème hoekstijlen en pannendak. Voorgevel +Z met dubbele staldeur (draait naar buiten),
    hooiluik erboven, binnen stro op de vloer en een stapel hooibalen. Pivot midden-onder."""
    hw, hd, t = W / 2, D / 2, 0.12
    front = [(-hw, 0), (-dw, 0), (-dw, dh), (dw, dh), (dw, 0), (hw, 0), (hw, He), (0, Hr), (-hw, He)]
    back = [(-hw, 0), (hw, 0), (hw, He), (0, Hr), (-hw, He)]
    parts = [slab(front, [], hd - t, hd, 0.02, RED), slab(back, [], -hd, -hd + t, 0.02, RED)]
    for s in (-1, 1):                                    # zijwanden tussen de gevels, 2 cm terug
        x0, x1 = sorted((s * (hw - 0.02), s * (hw - 0.02 - t)))
        parts.append(rbox(x0, x1, 0.0, He - 0.03, -hd + 0.05, hd - 0.05, 0.02, RED))
        for z in (-hd, hd):                              # hoekstijlen
            parts.append(rbox(s * hw - 0.13, s * hw + 0.13, 0.0, He + 0.02, z - 0.13, z + 0.13, 0.03, CREAM)
                         .transform(t=(-s * 0.10, 0, -np.sign(z) * 0.10)))
    # donkere plint rond de voet (verzonken paneel) en deurkozijn
    for s in (-1, 1):
        parts.append(rbox(s * dw - 0.09, s * dw + 0.09, 0.0, dh + 0.09, hd - t - 0.02, hd + 0.008, 0.02, CREAM)
                     .transform(t=(s * 0.078, 0, 0)))                     # stijl steekt 12 mm de opening in
    parts.append(rbox(-dw - 0.18, dw + 0.18, dh - 0.012, dh + 0.17, hd - t - 0.027, hd + 0.015, 0.02, CREAM))
    # hooiluik in de voorgevel: crème lijst met donker paneel en een baal die uitsteekt
    parts.append(rbox(-0.50, 0.50, 2.55, 3.20, hd - 0.03, hd + 0.03, 0.02, CREAM))
    parts.append(rbox(-0.40, 0.40, 2.63, 3.12, hd - 0.02, hd + 0.045, 0.012, RED_D))
    parts.append(_heap(0.34, 0.20, 0.16, 5).transform(t=(0.0, 2.72, hd + 0.07)))
    # dak: één doorlopende kap (chevron) met donkere nokbalk
    ov, th = 0.32, 0.17
    roof = [(-hw - ov, He - 0.10), (0, Hr + 0.17), (hw + ov, He - 0.10),
            (hw + ov, He - 0.10 - th), (0, Hr + 0.17 - th - 0.02), (-hw - ov, He - 0.10 - th)]
    parts.append(slab(roof, [], -hd - 0.28, hd + 0.28, 0.03, "M_Stn_RoofRed"))
    parts.append(rbox(-0.13, 0.13, Hr + 0.10, Hr + 0.25, -hd - 0.31, hd + 0.31, 0.04, "M_Stn_RoofRedDark"))
    # binnen: stro op de vloer en hooibalen in de hoek
    parts.append(rbox(-hw + 0.20, hw - 0.20, 0.0, 0.045, -hd + 0.18, hd - 0.20, 0.02, STRAW_D))
    bale = _hay_bale()
    stack = [(-1.55, 0.04, -1.45, 0.0), (-0.62, 0.04, -1.50, 0.08), (-1.10, 0.455, -1.47, -0.05), (1.50, 0.04, -1.20, 1.45)]
    for (x, y, z, a) in stack:
        parts.append(bale.transform(R=mk.rot_y(a), t=(x, y, z)))
    cols = [col_box(-hw, -dw, 0, He, hd - t, hd), col_box(dw, hw, 0, He, hd - t, hd), col_box(-hw, hw, dh, He, hd - t, hd),
            col_box(-hw, hw, 0, He, -hd, -hd + t), col_box(-hw, -hw + t, 0, He, -hd, hd), col_box(hw - t, hw, 0, He, -hd, hd),
            slab(roof, [], -hd - 0.28, hd + 0.28, 0.0, "M_Stn_Collider"),
            slab([(-hw, He), (hw, He), (0, Hr)], [], hd - t, hd, 0.0, "M_Stn_Collider"),
            slab([(-hw, He), (hw, He), (0, Hr)], [], -hd, -hd + t, 0.0, "M_Stn_Collider"),
            col_box(-2.0, -0.18, 0, 0.86, -1.80, -1.15), col_box(1.20, 1.80, 0, 0.45, -1.65, -0.75)]
    # staldeuren: scharnier aan de stijl, blad naar het midden; ze draaien naar buiten
    lw, lh = dw - 0.012, dh - 0.045
    hole = rounded_rect_loop(0.11, lw - 0.11, 0.11, lh - 0.11, 0.02, 2)
    leaf = [slab([(0, 0), (lw, 0), (lw, lh), (0, lh)], [hole], -0.030, 0.030, 0.012, CREAM),        # kader uit één stuk
            rbox(0.05, lw - 0.05, 0.05, lh - 0.05, -0.008, 0.008, 0.004, RED)]                      # verzonken paneel
    L = math.hypot(lw - 0.22, lh - 0.22)
    ang = math.atan2(lh - 0.22, lw - 0.22)
    for k, sgn in enumerate((1, -1)):                    # kruis: tweede schoor 8 mm dunner (geen samenvallende vlakken)
        brace = rbox(-L / 2, L / 2, -0.055, 0.055, -0.024 + 0.008 * k, 0.024 - 0.008 * k, 0.010, CREAM)
        leaf.append(brace.transform(R=mk.rot_z(sgn * ang), t=(lw / 2, lh / 2, 0)))
    for y in (0.30, lh - 0.30):
        leaf.append(rbox(-0.02, 0.20, y - 0.035, y + 0.035, -0.040, 0.040, 0.01, "M_Stn_Dark"))
    leaf.append(rbox(lw - 0.16, lw - 0.06, 0.98, 1.12, 0.024, 0.07, 0.015, "M_Stn_Yellow"))            # grendel/greep
    leaf_l = mk.merge(*leaf)
    leaf_r = leaf_l.transform(s=(-1, 1, 1))
    lcol = col_box(0, lw, 0, lh, -0.035, 0.035)
    hz = hd + 0.052
    doors = props.swing_door("Door_L", leaf_l, lcol, (-dw + 0.006, 0.02, hz), -108, -96) + \
        props.swing_door("Door_R", leaf_r, lcol.transform(s=(-1, 1, 1)), (dw - 0.006, 0.02, hz), 108, 22)
    light = [("Light_Barn", None, None, (0.0, He - 0.25, 0.0), None, True)]
    return _nodes(mk.merge(*parts), cols, doors + light)


# ------------------------------------------------------------------ afdak / stal
def shed(W=3.4, D=2.2):
    """Open stal: vier palen, dichte achterwand, halfhoge zijwanden, lessenaarsdak, stro op de grond."""
    hw, hd = W / 2, D / 2
    parts, cols = [], []
    for x in (-hw + 0.10, hw - 0.10):
        for z, h in ((-hd + 0.10, 1.95), (hd - 0.10, 2.35)):
            parts.append(rbox(x - 0.09, x + 0.09, 0.0, h, z - 0.09, z + 0.09, 0.03, TIMBER))
            cols.append(col_box(x - 0.09, x + 0.09, 0.0, h, z - 0.09, z + 0.09))
    parts.append(rbox(-hw + 0.13, hw - 0.13, 0.10, 1.86, -hd + 0.07, -hd + 0.14, 0.02, RED))                # achterwand
    cols.append(col_box(-hw + 0.10, hw - 0.10, 0.0, 1.90, -hd + 0.05, -hd + 0.15))
    for s in (-1, 1):
        x0, x1 = sorted((s * (hw - 0.07), s * (hw - 0.14)))
        parts.append(rbox(x0, x1, 0.10, 1.10, -hd + 0.13, hd - 0.13, 0.02, RED))
        parts.append(rbox(x0 - 0.025, x1 + 0.025, 1.09, 1.17, -hd + 0.12, hd - 0.12, 0.02, CREAM))            # afdeklat
        cols.append(col_box(x0, x1, 0.0, 1.17, -hd + 0.10, hd - 0.10))
    a = math.atan2(0.40, D - 0.20)
    roof = rbox(-hw - 0.25, hw + 0.25, -0.07, 0.07, -hd - 0.30, hd + 0.40, 0.03, "M_Stn_RoofRed")
    roof_col = col_box(-hw - 0.25, hw + 0.25, -0.07, 0.07, -hd - 0.30, hd + 0.40)
    R, t = mk.rot_x(-a), (0, 2.22, 0)
    parts.append(roof.transform(R=R, t=t))
    cols.append(roof_col.transform(R=R, t=t))
    parts.append(rbox(-hw + 0.02, hw - 0.02, 2.20, 2.34, hd - 0.16, hd - 0.04, 0.03, TIMBER_D))            # voorbalk
    parts.append(rbox(-hw + 0.20, hw - 0.20, 0.0, 0.05, -hd + 0.18, hd - 0.05, 0.02, STRAW_D))
    parts.append(_heap(0.55, 0.30, 0.45, 11).transform(t=(-0.85, 0.12, -0.45)))
    parts.append(_hay_bale().transform(R=mk.rot_y(0.2), t=(0.85, 0.045, -0.55)))
    cols.append(col_box(0.40, 1.30, 0, 0.46, -0.85, -0.25))
    return _nodes(mk.merge(*parts), cols)


# ------------------------------------------------------------------ hek & poort
def _post(x, h, r=0.075, k=1.0, cap=True):
    """Ronde weidepaal met schuin afgezaagde kop; k < 1 = fractie kleiner (verdwijnt in de paal van het volgende stuk)."""
    r, h = r * (0.86 + 0.14 * k), h - 0.012 * (1 - k)
    prof = [(0.0, 0.0), (r, 0.0), (r, h - 0.05), (r * 0.72, h), (0.0, h)]
    p = mk.lathe(prof, 8, mat=TIMBER_D if cap else TIMBER).transform(t=(x, 0, 0))
    return p


def fence(length=3.0):
    """Weidehek van drie liggers tussen ronde palen; sluit aan op een volgend stuk langs +X (pivot = beginpaal)."""
    L = length
    parts = [_post(0.0, 1.20), _post(L, 1.20, k=0.0), _post(L / 2, 1.08, r=0.06)]
    for j, y in enumerate((0.34, 0.66, 0.98)):
        parts.append(rbox(-0.02, L + 0.02, y - 0.055, y + 0.055, 0.02 + 0.006 * j, 0.075 + 0.006 * j, 0.02, TIMBER))
    col = col_box(-0.08, L + 0.08, 0.0, 1.2, -0.08, 0.10)
    return _nodes(mk.merge(*parts), [col])


def gate(width=2.0):
    """Hekpoort: twee hoge palen en een blad met kruisschoor dat om de beginpaal draait (knoop Gate, naar +Z open)."""
    Wd = width
    posts = [_post(0.0, 1.42, r=0.09), _post(Wd, 1.42, r=0.09)]
    for x in (0.0, Wd):
        posts.append(ball(0.085, "M_Stn_Yellow", 8, 5).transform(t=(x, 1.45, 0)))
    cols = [col_box(-0.09, 0.09, 0, 1.45, -0.09, 0.09), col_box(Wd - 0.09, Wd + 0.09, 0, 1.45, -0.09, 0.09)]
    lw, y0, y1 = Wd - 0.24, 0.22, 1.12
    hole = [(0.10, y0 + 0.10), (lw - 0.10, y0 + 0.10), (lw - 0.10, y1 - 0.10), (0.10, y1 - 0.10)]
    leaf = [slab([(0, y0), (lw, y0), (lw, y1), (0, y1)], [hole], -0.032, 0.032, 0.012, TIMBER)]       # raam uit één stuk
    ym = (y0 + y1) / 2
    leaf.append(rbox(0.06, lw - 0.06, ym - 0.05, ym + 0.05, -0.024, 0.024, 0.012, TIMBER))            # middenligger
    L = math.hypot(lw - 0.16, y1 - y0 - 0.16)
    ang = math.atan2(y1 - y0 - 0.16, lw - 0.16)
    leaf.append(rbox(-L / 2, L / 2, -0.045, 0.045, -0.016, 0.016, 0.01, TIMBER_D).transform(R=mk.rot_z(ang), t=(lw / 2, ym, 0)))
    for y in (y0 + 0.14, y1 - 0.14):                                                                   # scharnierbanden
        leaf.append(rbox(-0.10, 0.22, y - 0.03, y + 0.03, -0.042, 0.042, 0.01, "M_Stn_Dark"))
    leaf.append(rbox(lw - 0.10, lw + 0.09, ym + 0.12, ym + 0.19, 0.020, 0.062, 0.012, "M_Stn_Yellow"))  # klink
    lcol = col_box(0, lw, y0, y1, -0.035, 0.035)
    door = props.swing_door("Gate", mk.merge(*leaf), lcol, (0.12, 0.0, 0.0), -110, 0)
    return _nodes(mk.merge(*posts), cols, door)


# ------------------------------------------------------------------ kleine stukken
def trough(L=1.60, D=0.52, H=0.46):
    """Houten drinkbak op twee schragen, gevuld met water."""
    wall = 0.06
    outer = [(-L / 2, -D / 2), (L / 2, -D / 2), (L / 2, D / 2), (-L / 2, D / 2)]
    inner = [(-L / 2 + wall, -D / 2 + wall), (L / 2 - wall, -D / 2 + wall), (L / 2 - wall, D / 2 - wall), (-L / 2 + wall, D / 2 - wall)]
    parts = [_xz(slab(outer, [inner], 0.16, H, 0.015, TIMBER)),                                       # wanden uit één stuk
             rbox(-L / 2 + 0.02, L / 2 - 0.02, 0.13, 0.20, -D / 2 + 0.02, D / 2 - 0.02, 0.012, TIMBER_D),  # bodem
             rbox(-L / 2 + 0.03, L / 2 - 0.03, 0.22, H - 0.07, -D / 2 + 0.03, D / 2 - 0.03, 0.0, "M_Stn_Water")]
    for x in (-L / 2 + 0.22, L / 2 - 0.22):
        parts.append(rbox(x - 0.06, x + 0.06, 0.0, 0.17, -D / 2 - 0.05, D / 2 + 0.05, 0.02, TIMBER_D))
    for x in (-L / 2 + 0.05, L / 2 - 0.05):                                                            # ijzeren banden
        parts.append(_xz(slab([(x - 0.03, -D / 2 - 0.008), (x + 0.03, -D / 2 - 0.008), (x + 0.03, D / 2 + 0.008), (x - 0.03, D / 2 + 0.008)],
                              [], 0.20, H - 0.04, 0.0, "M_Stn_Dark")))
    return _nodes(mk.merge(*parts), [col_box(-L / 2, L / 2, 0.0, H, -D / 2, D / 2)])


def haystack(r=1.15, h=2.0):
    """Hooiberg: bolle berg hooi rond een paal, met een donkerder rand onderaan."""
    rng = np.random.default_rng(31)
    prof = [(0.0, 0.0), (r * 0.96, 0.0), (r, 0.22), (r * 0.94, h * 0.42), (r * 0.72, h * 0.70), (r * 0.36, h * 0.92), (0.0, h)]
    m = mk.lathe(prof, 9, mat=STRAW)
    V = m.V.copy()
    rad = np.hypot(V[:, 0], V[:, 2])
    k = 1.0 + rng.uniform(-0.07, 0.07, len(V)) * (rad > 0.05)
    V[:, 0] *= k
    V[:, 2] *= k
    m.V = V
    skirt = mk.lathe([(0.0, 0.0), (r * 1.05, 0.0), (r * 1.07, 0.10), (r * 0.80, 0.30), (0.0, 0.30)], 9, mat=STRAW_D, phase=0.3)
    pole = cyl(0.055, 0.75, 6, 0.015, TIMBER_D).transform(t=(0.04, h + 0.20, 0.0))
    tufts = [_heap(0.22, 0.10, 0.20, 40 + i).transform(t=(math.cos(a) * r * 1.02, 0.07, math.sin(a) * r * 1.02))
             for i, a in enumerate((0.6, 2.4, 4.1, 5.3))]
    col = mk.lathe([(0.0, 0.0), (r, 0.0), (r * 0.9, h * 0.5), (r * 0.4, h * 0.92), (0.0, h)], 8, mat="M_Stn_Collider")
    return _nodes(mk.merge(m, skirt, pole, *tufts), [col])


def hay_rack(L=1.5):
    """Voerruif: V-vormige ruif van twee spijlenramen op kruispoten, vol hooi."""
    parts = []
    for s in (-1, 1):
        holes = [[(x - 0.045, 0.07), (x + 0.045, 0.07), (x + 0.045, 0.53), (x - 0.045, 0.53)]
                 for x in np.linspace(-L / 2 + 0.16, L / 2 - 0.16, 7)]
        frame = slab([(-L / 2, 0), (L / 2, 0), (L / 2, 0.60), (-L / 2, 0.60)], holes, -0.02, 0.02, 0.008, TIMBER)
        parts.append(frame.transform(R=mk.rot_x(s * 24 * DEG), t=(0, 0.62, s * 0.05)))
    for x in (-L / 2 + 0.08, L / 2 - 0.08):
        for s in (-1, 1):
            leg = rbox(-0.04, 0.04, 0.0, 1.32, -0.03 - 0.008 * s, 0.03 - 0.008 * s, 0.015, TIMBER_D)
            parts.append(leg.transform(R=mk.rot_x(s * 22 * DEG), t=(x + 0.006 * s, 0.0, -s * 0.25)))
    parts.append(rbox(-L / 2 + 0.04, L / 2 - 0.04, 0.58, 0.66, -0.05, 0.05, 0.02, TIMBER_D))            # bodemlat
    hay = _heap(L / 2 - 0.10, 0.26, 0.26, 17).transform(t=(0, 1.02, 0))
    return _nodes(mk.merge(*parts, hay), [col_box(-L / 2, L / 2, 0.0, 1.25, -0.34, 0.34)])


def milk_stand(L=1.30, D=0.70, H=0.46):
    """Melkbussenplank langs het spoor: laag houten platform met drie melkbussen (hier haalt de trein de melk op)."""
    parts = [rbox(-L / 2, L / 2, H - 0.07, H, -D / 2, D / 2, 0.02, TIMBER)]
    for x in (-L / 2 + 0.10, L / 2 - 0.10):
        for z in (-D / 2 + 0.10, D / 2 - 0.10):
            parts.append(rbox(x - 0.055, x + 0.055, 0.0, H - 0.03, z - 0.055, z + 0.055, 0.02, TIMBER_D))
    parts.append(rbox(-L / 2 + 0.06, L / 2 - 0.06, 0.14, 0.21, -D / 2 + 0.075, -D / 2 + 0.125, 0.015, TIMBER_D))
    for i, (x, z, a) in enumerate(((-0.40, 0.02, 0.2), (0.0, -0.08, 1.1), (0.40, 0.06, 2.3))):
        parts.append(_milk_can().transform(R=mk.rot_y(a), t=(x, H - 0.004, z)))
    cols = [col_box(-L / 2, L / 2, 0.0, H, -D / 2, D / 2), col_box(-0.58, 0.58, H, H + 0.58, -0.26, 0.24)]
    return _nodes(mk.merge(*parts), cols)


def wheelbarrow():
    """Kruiwagen met een lading hooi (voorkant/wiel naar +Z)."""
    tray = [(-0.42, 0.34), (0.50, 0.30), (0.62, 0.62), (-0.52, 0.62)]                # zijaanzicht (z, y)
    side = lambda pts, x0, x1, c, mat: slab([(-z, y) for z, y in pts][::-1], [], x0, x1, c, mat).transform(R=mk.rot_y(-90 * DEG))
    parts = [side(tray, -0.30, 0.30, 0.02, "M_Stn_Red")]
    parts.append(_heap(0.27, 0.20, 0.44, 23).transform(t=(0.0, 0.66, 0.05)))
    parts.append(cyl(0.19, 0.09, 12, 0.02, "M_Stn_Dark", axis="x").transform(t=(0.0, 0.19, 0.74)))
    parts.append(cyl(0.07, 0.11, 8, 0.01, "M_Stn_Yellow", axis="x").transform(t=(0.0, 0.19, 0.74)))
    for s in (-1, 1):
        parts.append(tube([(s * 0.24, 0.50, -1.05), (s * 0.24, 0.42, -0.40), (s * 0.10, 0.26, 0.60), (s * 0.075, 0.19, 0.74)],
                          0.026, TIMBER, seg=6))
        parts.append(rbox(s * 0.22 - 0.03, s * 0.22 + 0.03, 0.0, 0.40, -0.40, -0.33, 0.012, "M_Stn_Dark"))
        parts.append(capsule((s * 0.24, 0.50, -1.07), (s * 0.24, 0.48, -0.90), 0.036, mat=TIMBER_D, seg=6, rings=1))
    cols = [col_box(-0.32, 0.32, 0.0, 0.80, -0.55, 0.92)]
    return _nodes(mk.merge(*parts), cols)


def scarecrow():
    """Vogelverschrikker: kruis van palen, jutezak-hoofd met strohoed, rode jas, stro uit de mouwen."""
    parts = [cyl(0.05, 2.05, 6, 0.01, TIMBER_D).transform(t=(0, 1.025, 0)),
             cyl(0.04, 1.70, 6, 0.01, TIMBER_D, axis="x").transform(t=(0, 1.42, 0))]
    parts.append(rbox(-0.26, 0.26, 0.86, 1.56, -0.13, 0.13, 0.07, "M_Stn_Red"))                      # jas
    for s in (-1, 1):
        parts.append(rbox(s * 0.22 - 0.26, s * 0.22 + 0.26, 1.33, 1.52, -0.095, 0.095, 0.05, "M_Stn_Red").transform(t=(s * 0.26, 0, 0)))
        parts.append(_heap(0.13, 0.09, 0.09, 60 + s).transform(t=(s * 0.80, 1.42, 0)))
        parts.append(ball(0.028, "M_Stn_Dark", 6, 4).transform(t=(s * 0.075, 1.80, 0.165)))           # knoopogen
    parts.append(_heap(0.30, 0.10, 0.16, 71).transform(t=(0, 0.84, 0)))                                 # stro onder de jas
    parts.append(ball(0.19, "M_Stn_Cream", 9, 6, sy=1.05).transform(t=(0, 1.77, 0)))                    # hoofd
    for y in (1.02, 1.20, 1.38):
        parts.append(ball(0.03, "M_Stn_Yellow", 6, 4).transform(t=(0.0, y, 0.135)))
    hat = mk.lathe([(0.0, 1.90), (0.40, 1.90), (0.42, 1.93), (0.19, 1.96), (0.15, 2.14), (0.0, 2.18)], 9, mat=STRAW)
    band = mk.lathe([(0.168, 1.975), (0.205, 1.975), (0.195, 2.03), (0.168, 2.03)], 9, closed=True, mat="M_Stn_Red")
    parts += [hat, band]
    cols = [col_box(-0.28, 0.28, 0.0, 2.15, -0.15, 0.15), col_box(-0.90, 0.90, 1.32, 1.52, -0.10, 0.10)]
    return _nodes(mk.merge(*parts), cols)


ASSETS = {
    "Farm_Barn": (barn, 260, 0.25),
    "Farm_Shed": (shed, 400, 0.22),
    "Farm_Fence_3m": (fence, 900, 0.12),
    "Farm_Gate": (gate, 900, 0.12),
    "Farm_Trough": (trough, 900, 0.12),
    "Farm_Haystack": (haystack, 500, 0.20),
    "Farm_HayRack": (hay_rack, 900, 0.12),
    "Farm_MilkStand": (milk_stand, 900, 0.12),
    "Farm_Wheelbarrow": (wheelbarrow, 900, 0.12),
    "Farm_Scarecrow": (scarecrow, 900, 0.12),
}
