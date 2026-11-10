"""Boerderijdecor: plaatsbare stukken voor langs het spoor, in de stijl van de stationskit.

    Farm_Barn         schuur waar je in kunt lopen, met twee staldeuren die open en dicht kunnen (Door_L / Door_R)
    Farm_Shed         open afdak/stal met stro
    Farm_Fence_3m     weidehek, klikt aan elkaar langs +X (zoals Prop_Fence_2m)
    Farm_Gate         hekpoort van 2 m die open en dicht kan (Gate)
    Farm_Trough       drinkbak met water
    Farm_Haystack     hooiberg met paal
    Farm_HayRack      voerruif met hooi
    Farm_MilkStand    melkbussenplank met drie bussen
    Farm_Wheelbarrow  kruiwagen (leeg; _Hay met hooibaal, _Grain met graanzak)
    Farm_Scarecrow    vrolijke vogelverschrikker
    Farm_Wheat_*      graanveld in modules: Straight_3m, Edge_3m, Corner, Harvested_3m (raster van 1,5 m)
    Farm_FieldPath_3m veldpad · Farm_WheatSheaf graanbos · Farm_GrainSacks graanzakken · Farm_FieldSign veldbord

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


def _tray(bw, bl, tw, tl, h, t=0.035, mat="M_Stn_Red"):
    """Open bak die naar boven wijder wordt, als één gesloten vorm met binnenkant: bodem bw x bl, rand tw x tl."""
    def ring(w, l, y):
        return [(-w / 2, y, -l / 2), (w / 2, y, -l / 2), (w / 2, y, l / 2), (-w / 2, y, l / 2)]
    k = t / h
    V = ring(bw, bl, 0.0) + ring(tw, tl, h) + ring(tw - 2 * t, tl - 2 * t, h) + \
        ring(bw + (tw - bw) * k - 2 * t, bl + (tl - bl) * k - 2 * t, t)
    F = [(0, 1, 2, 3)]                                                   # onderkant (normaal omlaag)
    for i in range(4):
        j = (i + 1) % 4
        F += [(i, 4 + i, 4 + j, j), (4 + i, 8 + i, 8 + j, 4 + j), (8 + i, 12 + i, 12 + j, 8 + j)]
    F.append((15, 14, 13, 12))                                           # binnenbodem (normaal omhoog)
    m = mk.Mesh(np.array(V, float), F, [mat] * len(F))
    m.sharp = True
    return mk._orient_closed(m)


def wheat_icon(size, mat, depth=0.012):
    """Korenaar als reliëf (XY-vlak, dikte langs +Z vanaf 0): steel met drie paar korrels en een topkorrel."""
    def kernel(cx, cy, ang):
        loop = [(0.20 * size * math.cos(a) * 0.34, 0.19 * size * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 9)[:-1]]
        c, s_ = math.cos(ang), math.sin(ang)
        return slab([(cx + x * c - y * s_, cy + x * s_ + y * c) for x, y in loop], [], 0.0, depth, 0.003, mat)
    parts = [slab([(-0.022 * size, -0.50 * size), (0.022 * size, -0.50 * size), (0.022 * size, 0.30 * size),
                   (-0.022 * size, 0.30 * size)], [], 0.0, depth * 0.7, 0.002, mat)]
    for i, y in enumerate((-0.12, 0.08, 0.28)):
        for sgn in (-1, 1):
            parts.append(kernel(sgn * 0.155 * size, y * size, -sgn * 0.6))
    parts.append(kernel(0.0, 0.50 * size, 0.0))
    return mk.merge(*parts)


def grain_sack(w=0.46, h=0.62, d=0.30, seed=1, icon=True):
    """Staande graanzak: bolle jute zak met dichtgebonden hals en een korenaar op de voorkant. Onderkant y = 0."""
    rng = np.random.default_rng(seed)
    body = mk.rounded_box(w, h, d, min(w, d) * 0.42, 3, mat="M_Stn_Sack")
    V = body.V.copy()
    V[:, 0] *= 1.0 + 0.10 * np.cos(V[:, 1] / h * math.pi)                 # buik in het midden
    V += rng.uniform(-0.008, 0.008, V.shape)
    body.V = V
    parts = [body.transform(t=(0, h / 2, 0))]
    parts.append(mk.lathe([(0.0, h - 0.05), (0.075, h - 0.04), (0.055, h + 0.03), (0.11, h + 0.10), (0.0, h + 0.07)], 7,
                          b=0.7, mat="M_Stn_Sack"))
    parts.append(mk.torus(0.062, 0.016, 8, 5, mat="M_Stn_Timber", b=0.7).transform(t=(0, h + 0.03, 0)))
    if icon:
        parts.append(wheat_icon(0.26, "M_Stn_SackDark").transform(t=(0, h * 0.48, d / 2 - 0.004)))
    return mk.merge(*parts)


def _wheel(r=0.24, wdt=0.13):
    return mk.merge(cyl(r, wdt, 14, 0.035, "M_Stn_Dark", axis="x"), cyl(r * 0.42, wdt + 0.05, 10, 0.012, "M_Stn_Yellow", axis="x"))


def wheelbarrow(load=None):
    """Kruiwagen (wiel naar +Z, handvatten naar -Z): rode bak die naar boven wijder wordt, houten onderstel uit twee
    doorlopende bomen, rode grepen, één stevig wiel met gele naaf. load: None, "hay" of "grain"."""
    ty = 0.40                                                            # onderkant bak
    parts = [_tray(0.50, 0.66, 0.78, 1.00, 0.32).transform(t=(0, ty, -0.02))]
    for s in (-1, 1):
        beam = [(s * 0.31, 0.60, -1.12), (s * 0.29, 0.47, -0.52), (s * 0.24, ty - 0.035, -0.20), (s * 0.15, 0.27, 0.56),
                (s * 0.105, 0.24, 0.80)]
        parts.append(tube(beam, 0.036, TIMBER, seg=4, phase=0.5))
        parts.append(capsule((s * 0.312, 0.612, -1.17), (s * 0.303, 0.565, -0.95), 0.050, mat="M_Stn_Red", seg=8, rings=1))
        parts.append(tube([(s * 0.25, ty - 0.02, -0.26), (s * 0.29, 0.0, -0.44)], 0.036, TIMBER_D, seg=4, phase=0.5))
        for z in (-0.22, 0.22):                                           # bouten op de zijkant van de bak
            parts.append(ball(0.028, "M_Stn_Yellow", 6, 4).transform(t=(s * (0.305 + 0.07 * (0.16 / 0.32)), ty + 0.16, z - 0.02)))
    parts.append(rbox(-0.30, 0.30, 0.12, 0.19, -0.415, -0.355, 0.015, TIMBER_D))                  # dwarslat tussen de poten
    parts.append(cyl(0.030, 0.30, 6, 0.008, "M_Stn_Dark", axis="x").transform(t=(0, 0.24, 0.80)))  # as
    parts.append(_wheel().transform(t=(0, 0.24, 0.80)))
    cols = [col_box(-0.39, 0.39, 0.0, ty + 0.32, -0.55, 0.50), col_box(-0.10, 0.10, 0.0, 0.48, 0.50, 1.04),
            col_box(-0.35, 0.35, 0.42, 0.66, -1.20, -0.55)]
    if load == "hay":
        parts.append(_hay_bale(0.62, 0.34, 0.44).transform(R=mk.rot_y(math.pi / 2 + 0.06), t=(0, ty + 0.03, -0.02)))
        cols.append(col_box(-0.24, 0.24, ty + 0.3, ty + 0.44, -0.34, 0.30))
    elif load == "grain":
        parts.append(grain_sack(0.40, 0.50, 0.30, seed=4).transform(R=mk.rot_y(0.25), t=(0.0, ty + 0.03, -0.04)))
        cols.append(col_box(-0.22, 0.22, ty + 0.3, ty + 0.66, -0.20, 0.16))
    return _nodes(mk.merge(*parts), cols)


def wheat_ear(h=0.20, r=0.045, mat="M_Stn_Wheat", seg=4):
    """Dikke korenaar: drie bolle korrellagen boven elkaar (één gesloten vorm, afgeplat zodat hij als aar leest)."""
    prof = [(0.0, 0.0), (r, h * 0.16), (r * 0.62, h * 0.31), (r * 0.94, h * 0.46), (r * 0.56, h * 0.61),
            (r * 0.78, h * 0.76), (0.0, h)]
    return mk.lathe(prof, seg, 1.0, 0.62, mat=mat, phase=0.5)


def scarecrow():
    """Vrolijke vogelverschrikker: strohoed met rode band en korenaren, rond hoofd met dichtgeknepen lachogen,
    rode jas met uitlopende zoom, gele knopen, blauwe lap, geknoopte touwriem, strohanden en strofranje. Eén paal."""
    RD, YEL = "M_Stn_Red", "M_Stn_Yellow"
    zs = 0.72                                                            # jas is platter dan breed
    parts = [cyl(0.055, 1.05, 6, 0.012, TIMBER_D).transform(t=(0, 0.525, 0))]
    coat = mk.lathe([(0.0, 0.84), (0.33, 0.84), (0.31, 0.90), (0.215, 1.14), (0.225, 1.42), (0.17, 1.60), (0.0, 1.63)], 10,
                    b=zs, mat=RD)
    parts.append(coat)
    for i in range(12):                                                  # strofranje onder de zoom (zigzag)
        a = 2 * math.pi * (i + 0.5) / 12
        spike = mk.lathe([(0.0, -0.15), (0.062, -0.01), (0.0, 0.03)], 4, b=0.5, mat=STRAW)
        parts.append(spike.transform(R=mk.rot_y(-a + math.pi / 2), t=(math.cos(a) * 0.27, 0.86, math.sin(a) * 0.27 * zs)))
    for s in (-1, 1):
        parts.append(capsule((s * 0.15, 1.47, 0), (s * 0.63, 1.50, 0), 0.115, 0.10, seg=8, rings=2, mat=RD))        # mouw
        for k, (dy, dz) in enumerate(((0.07, 0.0), (0.0, 0.06), (-0.06, 0.0), (0.0, -0.06), (0.02, 0.02))):    # strohand
            parts.append(capsule((s * 0.66, 1.50 + dy * 0.5, dz * 0.5), (s * (0.90 - 0.03 * (k == 4)), 1.50 + dy * 1.6, dz * 1.6),
                                 0.034, 0.026, seg=5, rings=1, mat=STRAW))
        for k in range(5):                                               # franje rond de manchet
            a = 2 * math.pi * k / 5 + 0.3
            cuff = mk.lathe([(0.0, 0.0), (0.045, 0.02), (0.0, 0.12)], 3, mat=STRAW)
            parts.append(cuff.transform(R=mk.rot_z(-s * 75 * DEG), t=(s * 0.66, 1.50 + 0.085 * math.sin(a), 0.085 * math.cos(a))))
    for y in (1.02, 1.22, 1.40):                                         # gele knopen
        parts.append(ball(0.036, YEL, 8, 5).transform(t=(0.0, y, 0.215 * zs + 0.012 - 0.02 * (y > 1.3))))
    patch = rbox(-0.055, 0.055, -0.055, 0.055, -0.03, 0.012, 0.008, "M_Stn_Locker")
    parts.append(patch.transform(R=mk.rot_z(0.25), t=(0.115, 1.30, 0.20 * zs)))
    # touwriem met knoop en twee losse eindjes
    parts.append(mk.torus(0.218, 0.030, 12, 6, mat="M_Stn_Sack", b=zs).transform(t=(0, 1.13, 0)))
    parts.append(ball(0.048, "M_Stn_Sack", 7, 5).transform(t=(0.0, 1.13, 0.218 * zs + 0.03)))
    for s in (-1, 1):
        parts.append(capsule((s * 0.02, 1.12, 0.218 * zs + 0.035), (s * 0.075, 0.93, 0.25 * zs + 0.03), 0.026, 0.022, seg=5, rings=1,
                             mat="M_Stn_Sack"))
    # hoofd met lachend gezicht
    hy, hr = 1.83, 0.215
    parts.append(ball(hr, "M_Stn_Cream", 12, 8).transform(t=(0, hy, 0)))

    def on_head(x, y, lift=0.004):
        return (x, y, math.sqrt(max(1e-6, hr * hr - x * x - (y - hy) ** 2)) + lift)
    for s in (-1, 1):                                                    # dichtgeknepen lachogen (boogjes)
        arc = [on_head(s * 0.085 + 0.046 * math.cos(a), hy + 0.035 + 0.036 * math.sin(a)) for a in np.linspace(0.35, math.pi - 0.35, 7)]
        parts.append(tube(arc, 0.013, "M_Stn_Dark", seg=5))
        parts.append(ball(0.034, "M_Stn_SackDark", 7, 4, sy=0.7).transform(t=on_head(s * 0.135, hy - 0.035, -0.012)))   # wangetjes
    mouth = [(0.058 * math.cos(a), hy - 0.040 + 0.050 * math.sin(a)) for a in np.linspace(math.pi, 2 * math.pi, 8)]
    parts.append(slab(mouth, [], 0.12, on_head(0.0, hy - 0.06)[2] + 0.004, 0.004, "M_Stn_BarnRedDark"))
    for i in range(10):                                                  # strohaar onder de hoed
        a = 2 * math.pi * i / 10 + math.pi / 10
        if abs(math.sin(a) - 1.0) < 0.06:
            continue
        front = math.sin(a) > 0.30                                       # voor: korte pony, het gezicht blijft vrij
        lock = mk.lathe([(0.0, -0.07 if front else -0.18), (0.055, -0.02), (0.0, 0.04)], 4, b=0.5, mat=STRAW)
        parts.append(lock.transform(R=mk.rot_y(-a + math.pi / 2), t=(math.cos(a) * 0.20, hy + (0.115 if front else 0.07), math.sin(a) * 0.20)))
    # hoed met band en korenaren
    by = hy + 0.10
    hat = mk.lathe([(0.0, by + 0.02), (0.43, by - 0.035), (0.45, by - 0.005), (0.215, by + 0.045), (0.185, by + 0.24), (0.0, by + 0.28)],
                   10, mat="M_Stn_Wheat")
    band = mk.lathe([(0.19, by + 0.05), (0.232, by + 0.05), (0.212, by + 0.125), (0.19, by + 0.125)], 10, closed=True, mat=RD)
    tilt = lambda m: m.transform(t=(0, -by, 0)).transform(R=mk.rot_z(-0.07) @ mk.rot_x(-0.05), t=(0, by, 0))   # scheef op het hoofd
    parts += [tilt(hat), tilt(band)]
    for k, (ang, ln) in enumerate(((-0.5, 0.26), (-0.85, 0.22), (-0.2, 0.20))):
        ear = mk.merge(cyl(0.012, 0.10, 4, 0.0, "M_Stn_WheatDark").transform(t=(0, 0.05, 0)),
                       wheat_ear(ln, 0.05, STRAW).transform(t=(0, 0.08, 0)))
        parts.append(ear.transform(R=mk.rot_z(ang), t=(0.185, by + 0.07, 0.04 - 0.03 * k)))
    cols = [col_box(-0.30, 0.30, 0.0, 2.25, -0.22, 0.22), col_box(-0.92, 0.92, 1.36, 1.62, -0.12, 0.12)]
    return _nodes(mk.merge(*parts), cols)


# ---------------------------------------------------------------------- graanveld
SOIL, DIRT, WHEAT, STEM = "M_Stn_FieldSoil", "M_Stn_Dirt", "M_Stn_Wheat", "M_Stn_WheatDark"
TILE_L, TILE_D, BED_H = 3.0, 1.5, 0.07


def _stalk(rng, h):
    """Eén tarwehalm: stevige driekantige steel, dikke aar die iets overhangt, twee bladeren. Voet in de oorsprong."""
    stem = mk.lathe([(0.0, 0.0), (0.026, 0.0), (0.019, h - 0.22), (0.0, h - 0.20)], 3, mat=STEM)
    ear = wheat_ear(rng.uniform(0.34, 0.42), rng.uniform(0.075, 0.090), WHEAT).transform(
        R=mk.rot_x(rng.uniform(0.04, 0.26)), t=(0, h - 0.27, 0))
    leaves = [mk.lathe([(0.0, 0.0), (0.055, 0.15), (0.0, 0.44)], 3, b=0.25, mat=STEM).transform(
        R=mk.rot_y(a0 + rng.uniform(-0.5, 0.5)) @ mk.rot_x(rng.uniform(0.5, 0.85)), t=(0, h * rng.uniform(0.18, 0.42), 0))
        for a0 in (0.0, math.pi)]
    return mk.merge(stem, ear, *leaves).transform(R=mk.rot_y(rng.uniform(0, 6.28)) @ mk.rot_x(rng.uniform(-0.07, 0.07)))


def _plant(inside, x0, x1, z0, z1, rng, pitch=0.25, margin=0.12):
    """Halmen in rijen (om en om verspringend) binnen het gebied waar inside(x, z) waar is."""
    out = []
    nz = int(round((z1 - z0 - 2 * margin) / pitch)) + 1
    nx = int(round((x1 - x0 - 2 * margin) / pitch)) + 1
    for j in range(nz):
        for i in range(nx):
            x = x0 + margin + i * (x1 - x0 - 2 * margin) / max(1, nx - 1) + (0.5 * pitch if j % 2 else 0.0) * 0.6
            z = z0 + margin + j * (z1 - z0 - 2 * margin) / max(1, nz - 1)
            x += rng.uniform(-0.035, 0.035)
            z += rng.uniform(-0.035, 0.035)
            if not inside(x, z, margin * 0.75):
                continue
            out.append(_stalk(rng, rng.uniform(0.86, 1.04)).transform(t=(x, BED_H - 0.01, z)))
    return out


def _bed(poly, mat=SOIL):
    """Akkerbed: polygoon (x, z) als lage plak aarde met afgeschuinde rand."""
    return _xz(slab([(x, -z) for x, z in poly], [], 0.0, BED_H, 0.025, mat))


def wheat_field(kind="straight", seed=1):
    """Module van het graanveld. straight: 3,0 x 1,5 m · edge: 3,0 x 0,5 m (één rij, voor de rand) ·
    corner: L-vorm van 3 x 3 m · harvested: 3,0 x 1,5 m stoppelveld. Pivot midden-onder; modules sluiten op een
    raster van 1,5 m op elkaar aan. Door het graan loop je heen (alleen het aardbed heeft een botsvorm)."""
    rng = np.random.default_rng([2026, seed])
    L, D = TILE_L, TILE_D
    if kind == "corner":
        poly = [(-L / 2, -L / 2), (L / 2, -L / 2), (L / 2, L / 2), (0.0, L / 2), (0.0, 0.0), (-L / 2, 0.0)]
        inside = lambda x, z, m: (abs(x) < L / 2 - m and -L / 2 + m < z < L / 2 - m) and not (x < m and z > -m)
        box = (-L / 2, L / 2, -L / 2, L / 2)
        cols = [col_box(-L / 2, L / 2, 0, BED_H, -L / 2, 0.0), col_box(0.0, L / 2, 0, BED_H, 0.0, L / 2)]
    else:
        d = 0.5 if kind == "edge" else D
        poly = [(-L / 2, -d / 2), (L / 2, -d / 2), (L / 2, d / 2), (-L / 2, d / 2)]
        inside = lambda x, z, m: abs(x) < L / 2 - m and abs(z) < d / 2 - m
        box = (-L / 2, L / 2, -d / 2, d / 2)
        cols = [col_box(-L / 2, L / 2, 0, BED_H, -d / 2, d / 2)]
    parts = [_bed(poly)]
    if kind == "harvested":
        for x in np.linspace(-L / 2 + 0.3, L / 2 - 0.3, 7):
            for z in (-0.45, 0.0, 0.45):
                h = rng.uniform(0.10, 0.17)
                st = mk.lathe([(0.0, 0.0), (0.045, 0.0), (0.040, h), (0.0, h)], 5, mat=WHEAT)
                parts.append(st.transform(R=mk.rot_y(rng.uniform(0, 6.28)), t=(x + rng.uniform(-0.08, 0.08), BED_H - 0.01, z + rng.uniform(-0.08, 0.08))))
    else:
        parts += _plant(inside, *box, rng, margin=0.13 if kind != "edge" else 0.25 - 0.0)
    return _nodes(mk.merge(*parts), cols)


def field_path(L=3.0, W=1.0):
    """Smal veldpad: aangestampte aarde met een graskant aan weerszijden en een paar keitjes. Vlak (4-6 cm)."""
    rng = np.random.default_rng(77)
    parts = [rbox(-L / 2, L / 2, 0.0, 0.040, -W / 2 + 0.14, W / 2 - 0.14, 0.012, DIRT)]
    for s in (-1, 1):
        z0, z1 = sorted((s * (W / 2 - 0.17), s * W / 2))
        parts.append(rbox(-L / 2, L / 2, 0.0, 0.058, z0, z1, 0.015, "M_Stn_Leaf"))
    for _ in range(5):
        r = rng.uniform(0.05, 0.09)
        parts.append(ball(r, "M_Stn_Stone", 6, 4, sy=0.45).transform(t=(rng.uniform(-L / 2 + 0.2, L / 2 - 0.2), 0.035, rng.uniform(-0.22, 0.22))))
    return _nodes(mk.merge(*parts), [col_box(-L / 2, L / 2, 0.0, 0.05, -W / 2, W / 2)])


def wheat_sheaf():
    """Graanbos: een staande schoof met rode band en een tweede die ernaast ligt."""
    def sheaf(seed):
        rng = np.random.default_rng(seed)
        rods = [mk.lathe([(0.0, 0.0), (0.085, 0.0), (0.06, 0.36), (0.10, 0.74), (0.0, 0.78)], 6, mat=WHEAT)]
        for k in range(8):
            a = 2 * math.pi * k / 8 + rng.uniform(-0.1, 0.1)
            rod = mk.lathe([(0.0, 0.0), (0.040, 0.0), (0.036, 0.80), (0.0, 0.86)], 4, mat=STRAW if k % 2 else WHEAT)
            lean = rng.uniform(0.17, 0.24)
            rod = rod.transform(t=(0, -0.38, 0)).transform(R=mk.rot_y(-a) @ mk.rot_z(-lean), t=(math.cos(a) * 0.075, 0.385, math.sin(a) * 0.075))
            rods.append(rod)
        rods.append(mk.torus(0.108, 0.026, 10, 5, mat="M_Stn_Red").transform(t=(0, 0.385, 0)))
        return mk.merge(*rods)
    stand = sheaf(3)
    lying = sheaf(5).transform(t=(0, -0.385, 0)).transform(R=mk.rot_y(0.5) @ mk.rot_z(math.pi / 2 - 0.06), t=(0.52, 0.125, 0.10))
    cols = [col_box(-0.16, 0.16, 0, 0.82, -0.16, 0.16), col_box(0.14, 0.92, 0, 0.26, -0.20, 0.36)]
    return _nodes(mk.merge(stand, lying), cols)


def grain_sacks():
    """Graanzakken: een staande zak met korenaar en een zak die ertegenaan ligt."""
    stand = grain_sack(0.48, 0.64, 0.32, seed=2).transform(R=mk.rot_y(0.15))
    lying = grain_sack(0.44, 0.58, 0.30, seed=7, icon=False).transform(t=(0, -0.29, 0)).transform(
        R=mk.rot_y(-0.5) @ mk.rot_z(math.pi / 2), t=(0.50, 0.155, 0.12))
    cols = [col_box(-0.26, 0.26, 0, 0.72, -0.18, 0.18), col_box(0.18, 0.86, 0, 0.32, -0.16, 0.40)]
    return _nodes(mk.merge(stand, lying), cols)


def field_sign():
    """Veldbord: houten paal met een plank en een korenaar erop, graspollen aan de voet."""
    parts = [rbox(-0.06, 0.06, 0.0, 1.18, -0.055, 0.055, 0.02, TIMBER_D)]
    board = [(-0.46, 0.62), (0.44, 0.60), (0.47, 0.86), (0.45, 1.10), (-0.44, 1.12), (-0.48, 0.88)]
    parts.append(slab(board, [], 0.045, 0.105, 0.015, TIMBER))
    parts.append(wheat_icon(0.40, "M_Stn_Cream", 0.020).transform(R=mk.rot_z(-0.5), t=(0.0, 0.86, 0.100)))
    for x, yb in ((-0.33, 0.70), (0.33, 0.69), (-0.33, 1.03), (0.33, 1.02)):
        parts.append(ball(0.022, "M_Stn_Dark", 6, 4).transform(t=(x, yb, 0.108)))
    rng = np.random.default_rng(9)
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.4
        blade = mk.lathe([(0.0, 0.0), (0.035, 0.07), (0.0, rng.uniform(0.20, 0.30))], 3, b=0.3, mat="M_Stn_Leaf")
        parts.append(blade.transform(R=mk.rot_y(a) @ mk.rot_x(rng.uniform(0.25, 0.6)), t=(math.cos(a) * 0.10, 0.0, math.sin(a) * 0.10)))
    cols = [col_box(-0.06, 0.06, 0, 1.18, -0.06, 0.06), col_box(-0.48, 0.47, 0.60, 1.12, 0.04, 0.11)]
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
    "Farm_Wheelbarrow_Hay": (lambda: wheelbarrow("hay"), 900, 0.12),
    "Farm_Wheelbarrow_Grain": (lambda: wheelbarrow("grain"), 900, 0.12),
    "Farm_Scarecrow": (scarecrow, 900, 0.12),
    "Farm_Wheat_Straight_3m": (lambda: wheat_field("straight", 1), 500, 0.10),
    "Farm_Wheat_Edge_3m": (lambda: wheat_field("edge", 2), 500, 0.10),
    "Farm_Wheat_Corner": (lambda: wheat_field("corner", 3), 500, 0.10),
    "Farm_Wheat_Harvested_3m": (lambda: wheat_field("harvested", 4), 500, 0.10),
    "Farm_FieldPath_3m": (field_path, 600, 0.12),
    "Farm_WheatSheaf": (wheat_sheaf, 900, 0.12),
    "Farm_GrainSacks": (grain_sacks, 900, 0.12),
    "Farm_FieldSign": (field_sign, 900, 0.12),
}
