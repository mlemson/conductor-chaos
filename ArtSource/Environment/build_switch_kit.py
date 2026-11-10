"""Spoorwissel in de stijl van de spoorkit (issue #214), met kapotte en herstelde toestand (#215).

Eén stuk van 20 m met twee routes vanaf hetzelfde begin:
  * rechtdoor: 20 m recht (de hoofd-TrackPiece, sockets Socket_*), zodat de Track Builder er gewoon aan verder bouwt;
  * afbuigend: S-bocht (R20 30 graden uit, R20 30 graden terug) naar een parallel spoor 5,36 m opzij, eindigt weer
    recht vooruit (sockets DivSocket_* en DivPath_00..12 voor TrackPiece in Unity).
Zo liggen de vervolgstukken van beide routes los van elkaar (geen overlappende beddingen).

Onderdelen (knopen):
  Mesh            vaste delen: bedding, lange dwarsliggers, stokrails, sluitrails, puntstuk, wisselbok
  Blade_S/Blade_D tongen (pivot = hiel); draaien samen een paar graden: Toe_Neutral -> Toe_Straight/Toe_Diverge
  Tie_Bar         trekstang tussen de tongen (schuift mee met de tongpunt)
  Switch_Lever    hendel op de wisselbok (pivot = as); Lever_Tip* geven de stand per route en kapot
  Switch_Target   richtingsschijf (pivot = voet van de staaf), draait 90 graden per route
  Repair_Bolt     dikke bout die bij een kapotte wissel uitsteekt; Repair_Bolt_Home = helemaal erin
  Broken_Bits     losse trekstang, moeren, scheve kap: alleen zichtbaar als de wissel kapot is
  Col_Main        botsvorm (beide routes)

A buigt af naar +z, B is gespiegeld; de prefabbouwer noemt ze Left/Right na meten in Unity.
Gebruik: python ArtSource/Environment/build_switch_kit.py
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_track_kit as T  # noqa: E402
from build_track_kit import mk, rbox, RAIL_Z, RAIL_W, RAIL_TOP, SLEEPER_TOP, SLEEPER, BED_TOP  # noqa: E402

R_DIV = 20.0
A_DIV = math.radians(30.0)
L_STRAIGHT = 20.0
BLADE_GAP = RAIL_W + 0.012          # hart tong tot hart stokrail als de tong aanligt (1,2 cm lucht tussen de koppen)
THROW = 0.06                         # tongpunt schuift +-6 cm vanuit de middenstand
LONG_UNTIL = 3.75                    # lange dwarsliggers zolang de routes minder dan dit uit elkaar liggen


class SPath:
    """Afbuigende route: R20 30 graden naar +z, dan R20 30 graden terug (eindigt evenwijdig)."""
    sloped = False
    radius = None

    def __init__(self):
        self.l1 = R_DIV * A_DIV
        self.length = 2 * self.l1
        self.p1 = np.array([R_DIV * math.sin(A_DIV), R_DIV * (1 - math.cos(A_DIV))])

    def elev(self, s):
        return np.zeros_like(np.asarray(s, float))

    def slope(self, s):
        return 0.0

    def frame(self, s):
        s = float(np.clip(s, 0.0, self.length))
        if s <= self.l1:
            phi = s / R_DIV
            return np.array([R_DIV * math.sin(phi), R_DIV * (1 - math.cos(phi))]), phi
        h1 = A_DIV
        c = self.p1 + R_DIV * np.array([math.sin(h1), -math.cos(h1)])
        h = h1 - (s - self.l1) / R_DIV
        return c + R_DIV * np.array([-math.sin(h), math.cos(h)]), h

    def frames(self, S):
        P, H = zip(*(self.frame(s) for s in np.ravel(S)))
        return np.array(P), np.array(H)

    def point(self, s, lateral=0.0):
        p, h = self.frame(s)
        return p + lateral * np.array([-math.sin(h), math.cos(h)])

    def bend(self, m):
        m = m.copy()
        V = m.V
        P, H = self.frames(V[:, 0])
        n = np.stack([-np.sin(H), np.cos(H)], 1)
        xz = P + V[:, 2:3] * n
        m.V = np.stack([xz[:, 0], V[:, 1], xz[:, 1]], 1)
        return m

    def place(self, m, s, pitch=True, lift=True):
        p, h = self.frame(s)
        return m.transform(R=mk.rot_y(-h), t=(p[0], 0.0, p[1]))


def solve(f, a, b, it=60):
    """Nulpunt van f op [a, b] (bisectie)."""
    fa = f(a)
    for _ in range(it):
        m = (a + b) / 2
        fm = f(m)
        if (fm > 0) == (fa > 0):
            a, fa = m, fm
        else:
            b = m
    return (a + b) / 2


def rail_between(points, mat="M_Track_Rail", taper=False):
    """Rail langs een polylijn in xz (bijv. een tong), met het gewone railprofiel."""
    pts = np.asarray(points, float)
    seg = np.diff(pts, axis=0)
    lens = np.linalg.norm(seg, axis=1)
    s = np.concatenate([[0.0], np.cumsum(lens)])
    L = s[-1]
    prof = [(pz, py + SLEEPER_TOP) for pz, py in T.rail_profile()]
    m = mk.extrude_profile(prof, 0.0, L, nx=max(2, len(pts) - 1), rings=1, mat=mat)
    V = m.V.copy()
    if taper:                                   # tongpunt: smaller en iets lager, loopt op tot het volle profiel
        f = np.clip(V[:, 0] / min(1.6, L), 0, 1)
        V[:, 2] *= 0.55 + 0.45 * f
        V[:, 1] = SLEEPER_TOP + (V[:, 1] - SLEEPER_TOP) * (0.92 + 0.08 * f)
    x = np.interp(V[:, 0], s, pts[:, 0])
    z = np.interp(V[:, 0], s, pts[:, 1])
    d = seg / lens[:, None]
    k = np.clip(np.searchsorted(s, V[:, 0], side="right") - 1, 0, len(d) - 1)
    n = np.stack([-d[k, 1], d[k, 0]], 1)
    m.V = np.stack([x + V[:, 2] * n[:, 0], V[:, 1], z + V[:, 2] * n[:, 1]], 1)
    return m


def rect(cx, cz, half_l, half_w, ang):
    """Hoekpunten van een rechthoek in xz (lengte langs de hoek ang, + = naar +z)."""
    c, s = math.cos(ang), math.sin(ang)
    u, v = np.array([c, s]), np.array([-s, c])
    p = np.array([cx, cz])
    return np.array([p + u * a * half_l + v * b * half_w for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))])


def overlap(a, b, margin=0.03):
    """Overlappen twee rechthoeken (separating axis)?"""
    for poly in (a, b):
        for i in range(4):
            e = poly[(i + 1) % 4] - poly[i]
            n = np.array([-e[1], e[0]]) / np.linalg.norm(e)
            pa, pb = a @ n, b @ n
            if pa.max() + margin < pb.min() or pb.max() + margin < pa.min():
                return False
    return True


def build(rng, side=1):
    P = T.Path(L_STRAIGHT)
    D = SPath()
    parts, broken, cols = [], [], []

    # ---------------------------------------------------------------- belangrijke punten
    # hiel van de rechte tong: tong ligt evenwijdig aan de gebogen stokrail tot hij op z = +RAIL_Z uitkomt
    sh_s = solve(lambda s: D.point(s, RAIL_Z - BLADE_GAP)[1] - RAIL_Z, 0.1, 6.0)
    heel_s = D.point(sh_s, RAIL_Z - BLADE_GAP)
    # hiel van de afbuigende tong: tong ligt tegen de rechte stokrail tot de afbuigende binnenrail daar uitkomt
    sh_d = solve(lambda s: D.point(s, -RAIL_Z)[1] - (-RAIL_Z + BLADE_GAP), 0.1, 6.0)
    heel_d = D.point(sh_d, -RAIL_Z)
    # puntstuk: afbuigende binnenrail kruist de rechte buitenrail (z = +RAIL_Z)
    sc = solve(lambda s: D.point(s, -RAIL_Z)[1] - RAIL_Z, 2.0, 14.0)
    cross = D.point(sc, -RAIL_Z)
    hc = D.frame(sc)[1]

    # ---------------------------------------------------------------- bedding (afbuigende iets lager waar ze overlappen)
    parts.append(T.bed(P, rng))
    db = bed_from(D, rng, 0.6)                      # begint binnen de rechte bedding (geen samenvallende kopvlakken)
    # bed(D) is al gebogen: zoek per vertex de afstand van de routes via x (route ligt monotoon in x)
    xs = np.linspace(0, D.length, 400)
    Pd, _ = D.frames(xs)
    dz = np.interp(db.V[:, 0], Pd[:, 0], Pd[:, 1])
    db.V[:, 1] -= 0.035 * np.clip((5.0 - dz) / 0.6, 0, 1)     # onder de rechte bedding waar ze overlappen
    parts.append(db)

    # ---------------------------------------------------------------- rails
    parts.append(T.rail(P, -RAIL_Z))                                     # rechte stokrail
    parts.append(T.rail(D, RAIL_Z))                                      # gebogen stokrail
    gap = 0.55                                                           # rails stoppen in het puntstuk
    parts.append(T.rail(P, RAIL_Z, heel_s[0] + 0.006, cross[0] - gap))   # rechte sluitrail tot het puntstuk
    parts.append(T.rail(P, RAIL_Z, cross[0] + gap, L_STRAIGHT))
    parts.append(T.rail(D, -RAIL_Z, sh_d + 0.006, sc - gap / math.cos(hc)))
    parts.append(T.rail(D, -RAIL_Z, sc + gap / math.cos(hc), D.length))
    # puntstuk: één gietstuk rond de kruising, 8 mm boven de railkoppen (verbergt de railuiteinden)
    c, s_ = math.cos(hc), math.sin(hc)
    corners = []
    for (ux, uz) in ((1.0, 0.0), (c, s_)):
        for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            corners.append(cross + np.array([ux, uz]) * a * 0.75 + np.array([-uz, ux]) * b * 0.13)
    hull = convex_hull(np.array(corners))
    frog = mk.prism(mk.chamfer_poly(hull, 0.04), SLEEPER_TOP + 0.02, RAIL_TOP + 0.008, 0.025, mat="M_Track_Rail")
    parts.append(xz_prism(frog))
    # vleugelrails (geleiding) naast het puntstuk
    for lat in (-0.30, 0.30):
        pts = [cross + np.array([1.0, 0.0]) * -1.6 + np.array([0.0, lat]), cross + np.array([1.0, 0.0]) * -0.95 + np.array([0.0, lat])]
        parts.append(rail_between(pts, mat="M_Track_Plate"))

    # ---------------------------------------------------------------- tongen (middenstand), pivot op de hiel
    S_s = np.linspace(0.0, sh_s, 14)
    pts_s = np.array([D.point(s, RAIL_Z - BLADE_GAP) for s in S_s])
    pts_d = np.array([[x, -RAIL_Z + BLADE_GAP] for x in np.linspace(0.0, heel_d[0], 14)])
    toe_s, toe_d = pts_s[0].copy(), pts_d[0].copy()

    def rotate_about(pts, pivot, ang):
        c_, s2 = math.cos(ang), math.sin(ang)
        q = pts - pivot
        return pivot + np.stack([q[:, 0] * c_ - q[:, 1] * s2, q[:, 0] * s2 + q[:, 1] * c_], 1)
    # rotate_about: + draait x naar +z; de tongpunt ligt aan de -x-kant van de hiel, dus + = punt naar -z
    ang_s = math.atan2(THROW, heel_s[0])                  # rechte tong: punt 6 cm naar -z, los van de gebogen stokrail
    ang_d = -math.atan2(THROW, heel_d[0])                 # afbuigende tong: punt 6 cm naar +z, los van de rechte stokrail
    # beide tongen draaien in de middenstand dezelfde kant op: toe_s -> -z, toe_d -> +z is tegengesteld;
    # de trekstang koppelt ze, dus beide tongpunten liggen in de middenstand op dezelfde verschuiving van hun
    # 'rechtdoor'-stand: rechtdoor = tong S aan (0), tong D los (+THROW*2); middenstand: S -THROW, D +THROW
    pts_s_n = rotate_about(pts_s, heel_s, ang_s)
    pts_d_n = rotate_about(pts_d, heel_d, ang_d)
    blade_s = rail_between(pts_s_n, taper=True)
    blade_d = rail_between(pts_d_n, taper=True)

    # trekstang tussen de tongen, net achter de punt
    xt = 0.42
    z_s = np.interp(xt, pts_s_n[:, 0], pts_s_n[:, 1])
    z_d = np.interp(xt, pts_d_n[:, 0], pts_d_n[:, 1])
    tie_pivot = np.array([xt, (z_s + z_d) / 2])
    tie = rbox(xt - 0.05, xt + 0.05, SLEEPER_TOP + 0.03, SLEEPER_TOP + 0.08, z_d - 0.06, z_s + 0.06, 0.015, "M_Track_Plate", div=1)
    rod = mk.cylinder(0.03, 1.15, 6, 0.0, mat="M_Track_Plate", axis="z")
    rod.sharp = True
    tie = mk.merge(tie, rod.transform(t=(xt, SLEEPER_TOP + 0.055, -RAIL_Z - 0.62)))

    # ---------------------------------------------------------------- dwarsliggers
    rects = []
    timbers = []
    stand_z = -RAIL_Z - 1.30                              # wisselbok naast de tongpunt, buiten het spoor

    def rail_zs(x):
        """z van alle rails op x (voor de onderlegplaatjes)."""
        zs = [-RAIL_Z]
        if x > heel_s[0]:
            zs.append(RAIL_Z)
        else:
            zs.append(np.interp(x, pts_s_n[:, 0], pts_s_n[:, 1]))
        for lat in (-RAIL_Z, RAIL_Z):
            s = solve(lambda s: D.point(s, lat)[0] - x, 0.0, D.length) if x <= D.point(D.length, lat)[0] else None
            if s is not None and s >= 0:
                zs.append(D.point(s, lat)[1])
        if x <= heel_d[0]:
            zs.append(np.interp(x, pts_d_n[:, 0], pts_d_n[:, 1]))
        zs = sorted(zs)
        merged = [[zs[0], zs[0]]]
        for z in zs[1:]:
            if z - merged[-1][1] < 0.40:
                merged[-1][1] = z
            else:
                merged.append([z, z])
        return merged
    for x in T.stations(L_STRAIGHT):
        d = np.interp(x, Pd[:, 0], Pd[:, 1])
        if d < LONG_UNTIL:
            z0 = -SLEEPER[2] / 2 - (1.25 if x < 1.6 else 0.0)            # eerste liggers dragen de wisselbok
            z1 = d + SLEEPER[2] / 2
            mat = rng.choice(["M_Track_Sleeper", "M_Track_SleeperLight", "M_Track_SleeperOld"], p=[0.6, 0.25, 0.15])
            w, h = SLEEPER[0], SLEEPER[1]
            tm = rbox(x - w / 2, x + w / 2, SLEEPER_TOP - h, SLEEPER_TOP, z0, z1, 0.05, mat, div=1)
            plates = []
            for za, zb in rail_zs(x):
                plates.append(rbox(x - 0.15, x + 0.15, SLEEPER_TOP - 0.01, SLEEPER_TOP + 0.022, za - 0.17, zb + 0.17, 0.012,
                                   "M_Track_Plate", div=1))
                for dx, dzz in ((-0.07, -0.12), (0.07, 0.12)):
                    zz = (za - 0.12) if dzz < 0 else (zb + 0.12)
                    bolt = mk.cylinder(0.03, 0.05, 4, 0.0, mat="M_Track_Plate", axis="y").transform(t=(x + dx, SLEEPER_TOP + 0.035, zz))
                    plates.append(bolt)
            timbers.append(mk.merge(tm, *plates))
            rects.append(rect(x, (z0 + z1) / 2, w / 2, (z1 - z0) / 2, 0.0))
        else:
            sl = T.sleeper(rng).transform(t=(x, 0, 0))
            timbers.append(sl)
            rects.append(rect(x, 0.0, SLEEPER[0] / 2 + 0.02, SLEEPER[2] / 2 + 0.06, 0.0))
    for s in T.stations(D.length):
        p, h = D.frame(s)
        if p[1] < LONG_UNTIL - 0.3:
            continue
        r = rect(p[0], p[1], SLEEPER[0] / 2 + 0.03, SLEEPER[2] / 2 + 0.06, h)
        if any(overlap(r, o) for o in rects):
            continue
        rects.append(r)
        timbers.append(D.place(T.sleeper(rng), s))
    parts += timbers

    # ---------------------------------------------------------------- wisselbok (naast de tongpunt)
    xs_ = 0.75
    stand = [rbox(xs_ - 0.34, xs_ + 0.34, SLEEPER_TOP - 0.01, SLEEPER_TOP + 0.10, stand_z - 0.30, stand_z + 0.30, 0.03,
                  "M_Track_Beam", div=1),
             rbox(xs_ - 0.24, xs_ + 0.24, SLEEPER_TOP + 0.10, SLEEPER_TOP + 0.52, stand_z - 0.20, stand_z + 0.20, 0.05,
                  "M_Track_Ink", div=2)]
    pivot_y = SLEEPER_TOP + 0.58
    for sz in (-1, 1):                                     # lagerbokjes van de hendelas
        stand.append(rbox(xs_ - 0.07, xs_ + 0.07, SLEEPER_TOP + 0.50, pivot_y + 0.06, stand_z + sz * 0.13 - 0.03,
                          stand_z + sz * 0.13 + 0.03, 0.015, "M_Track_Plate", div=1))
    axle = mk.cylinder(0.035, 0.34, 8, 0.008, mat="M_Track_Plate", axis="z")
    stand.append(axle.transform(t=(xs_, pivot_y, stand_z)))
    parts += stand
    # hendel: rood-wit gestreepte staaf met gele knop, staat rechtop (middenstand); pivot op de as
    lever_len = 0.58                                       # knop op ~1,4 m: binnen handbereik
    lever = []
    n = 5
    for k in range(n):
        y0, y1 = 0.04 + (lever_len - 0.04) * k / n, 0.04 + (lever_len - 0.04) * (k + 1) / n
        lever.append(rbox(-0.045, 0.045, y0 + 0.003, y1 - 0.003, -0.045, 0.045, 0.015,
                          "M_Track_Red" if k % 2 == 0 else "M_Track_White", div=1))
    lever.append(rbox(-0.06, 0.06, -0.07, 0.07, -0.06, 0.06, 0.02, "M_Track_Plate", div=1))
    knob = mk.ellipsoid(0.085, 0.075, 0.085, 10, 6, mat="M_Track_Warn")
    lever.append(knob.transform(t=(0, lever_len + 0.04, 0)))
    lever_mesh = mk.merge(*lever)
    lever_pivot = np.array([xs_, pivot_y, stand_z])
    # richtingsschijf op een staaf naast de bok
    tgt_pivot = np.array([xs_ + 0.62, SLEEPER_TOP - 0.01, stand_z])
    post = mk.cylinder(0.035, 1.30, 8, 0.01, mat="M_Track_Ink", axis="y").transform(t=(0, 0.65, 0))
    disc = mk.cylinder(0.24, 0.04, 16, 0.012, mat="M_Track_Red", axis="x").transform(t=(0.0, 1.32, 0))
    ring = mk.torus(0.235, 0.036, 18, 6, mat="M_Track_White").transform(R=mk.rot_z(math.pi / 2), t=(0.0, 1.32, 0))
    dot = mk.cylinder(0.08, 0.06, 12, 0.01, mat="M_Track_White", axis="x").transform(t=(0.0, 1.32, 0))
    target = mk.merge(post, disc, ring, dot)
    # bout die bij een kapotte wissel uitsteekt (pivot onder de kop; Repair_Bolt_Home = helemaal erin)
    bolt_home = np.array([xs_ - 0.13, SLEEPER_TOP + 0.52, stand_z - 0.08])
    bolt_out = 0.15
    hexhead = mk.cylinder(0.075, 0.06, 6, 0.012, mat="M_Track_Warn", axis="y").transform(t=(0, 0.03, 0))
    shank = mk.cylinder(0.032, bolt_out + 0.02, 8, 0.0, mat="M_Track_Plate", axis="y").transform(t=(0, -(bolt_out + 0.02) / 2 + 0.001, 0))
    washer = mk.cylinder(0.05, 0.012, 10, 0.0, mat="M_Track_Plate", axis="y").transform(t=(0, -bolt_out + 0.007, 0))
    bolt = mk.merge(hexhead, shank, washer)
    # kapotte boel: trekstang los op de liggers, moeren, een hangende kap
    bt = tie.transform(t=(-xt, -SLEEPER_TOP - 0.055, -tie_pivot[1]))
    broken.append(bt.transform(R=mk.rot_y(0.55) @ mk.rot_x(0.12), t=(1.55, SLEEPER_TOP + 0.06, -0.35)))
    for k in range(4):
        nut = mk.cylinder(0.04, 0.035, 6, 0.006, mat="M_Track_Plate", axis="y")
        nut.sharp = True
        broken.append(nut.transform(R=mk.rot_y(rng.uniform(0, 1)), t=(rng.uniform(0.2, 2.2), BED_TOP + 0.02, stand_z + rng.uniform(0.45, 0.85))))
    lid = rbox(-0.26, 0.26, -0.015, 0.015, -0.22, 0.22, 0.01, "M_Track_Ink", div=1)
    broken.append(lid.transform(R=mk.rot_x(-1.1), t=(xs_, SLEEPER_TOP + 0.40, stand_z - 0.40)))

    # ---------------------------------------------------------------- botsvorm
    col = T.collider_track(P, extra=(T.collider_track(D, with_bed=False),
                                      rbox(xs_ - 0.34, xs_ + 0.34, SLEEPER_TOP - 0.01, SLEEPER_TOP + 0.52,
                                           stand_z - 0.30, stand_z + 0.30, 0.0, "M_Track_Collider", div=1)))

    # ---------------------------------------------------------------- knopen
    def pos3(p2, y=SLEEPER_TOP):
        return (float(p2[0]), float(y), float(p2[1]))
    nodes = {
        "Mesh": mk.merge(*parts),
        "Blade_S": (blade_s.transform(t=(-heel_s[0], 0, -heel_s[1])), pos3(heel_s, 0.0)),
        "Blade_D": (blade_d.transform(t=(-heel_d[0], 0, -heel_d[1])), pos3(heel_d, 0.0)),
        "Tie_Bar": (tie.transform(t=(-tie_pivot[0], 0, -tie_pivot[1])), pos3(tie_pivot, 0.0)),
        "Switch_Lever": (lever_mesh, tuple(lever_pivot)),
        "Switch_Target": (target, tuple(tgt_pivot)),
        "Repair_Bolt": (bolt, tuple(bolt_home + [0, bolt_out, 0])),
        "Broken_Bits": mk.merge(*broken),
        "Col_Main": col,
    }
    # tongpunt in de drie standen: middenstand (model), rechtdoor (tong S aan), afbuigend (tong D aan)
    toe_n = pts_s_n[0]
    markers = {
        "Toe_Neutral": pos3(toe_n, RAIL_TOP), "Toe_Straight": pos3(toe_s, RAIL_TOP),
        "Toe_Diverge": pos3(rotate_about(pts_s[:1], heel_s, 2 * ang_s)[0], RAIL_TOP),
        "Repair_Bolt_Home": tuple(bolt_home),
    }
    # hendeltop: middenstand rechtop; rechtdoor/afbuigend 35 graden opzij, kapot hangend
    for name, ang in (("Lever_Tip_Straight", math.radians(35)), ("Lever_Tip_Diverge", math.radians(-35)),
                      ("Lever_Tip_Broken", math.radians(-100))):
        markers[name] = (float(lever_pivot[0]), float(lever_pivot[1] + lever_len * math.cos(ang)),
                         float(lever_pivot[2] + lever_len * math.sin(ang)))
    markers["Lever_Tip_Neutral"] = (float(lever_pivot[0]), float(lever_pivot[1] + lever_len), float(lever_pivot[2]))
    socks = T.sockets(P)
    p1, h1 = D.frame(D.length)
    div = [("DivSocket_Start", (0.0, RAIL_TOP, 0.0)), ("DivSocket_Start_Fwd", (1.0, RAIL_TOP, 0.0)),
           ("DivSocket_End", (p1[0], RAIL_TOP, p1[1])),
           ("DivSocket_End_Fwd", (p1[0] + math.cos(h1), RAIL_TOP, p1[1] + math.sin(h1)))]
    for k in range(13):
        pk, _ = D.frame(D.length * k / 12)
        div.append(("DivPath_%02d" % k, (pk[0], RAIL_TOP, pk[1])))
    info = {"crossing": [round(float(cross[0]), 3), round(float(cross[1]), 3)], "heelS": round(float(heel_s[0]), 3),
            "heelD": round(float(heel_d[0]), 3), "divergeLength": round(D.length, 3), "divergeOffset": round(float(p1[1]), 3)}
    return P, D, nodes, markers, socks + div, info


def bed_from(path, rng, s0):
    """Zoals T.bed, maar pas vanaf booglengte s0 (de afbuigende bedding begint binnen de rechte)."""
    wt, wb = 1.5, 2.15
    prof = [(-wb, -0.06), (wb, -0.06), (wb - 0.10, 0.06), (wt + 0.05, BED_TOP - 0.02), (wt - 0.15, BED_TOP),
            (-wt + 0.15, BED_TOP), (-wt - 0.05, BED_TOP - 0.02), (-wb + 0.10, 0.06)]
    nx = max(2, int(math.ceil((path.length - s0) / 0.75)))
    parts = [path.bend(mk.extrude_profile(prof, s0, path.length, nx=nx, rings=1, mat="M_Track_Bed"))]
    for _ in range(int(path.length * 2.2)):
        s = rng.uniform(s0 + 2.0, path.length - 0.2)
        z = rng.choice([-1, 1]) * rng.uniform(wt - 0.1, wb - 0.25)
        r = rng.uniform(0.06, 0.13)
        peb = mk.ellipsoid(r, r * 0.6, r * rng.uniform(0.8, 1.2), 6, 3, mat="M_Track_Pebble")
        peb.sharp = True
        y = np.interp(abs(z), [wt - 0.15, wb - 0.1], [BED_TOP, 0.06])
        parts.append(path.place(peb.transform(R=mk.rot_y(rng.uniform(0, math.pi)), t=(0, y, z)), s))
    return mk.merge(*parts)


def convex_hull(pts):
    pts = sorted(map(tuple, pts))
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return np.array(lower[:-1] + upper[:-1])


def xz_prism(m):
    """mk.prism ligt in xy met dikte langs z; leg hem plat: (x, y, z) -> (x, z, y)."""
    m = m.copy()
    V = m.V.copy()
    m.V = np.stack([V[:, 0], V[:, 2], V[:, 1]], 1)
    m.F = [tuple(reversed(f)) for f in m.F]                 # spiegeling: windingsrichting omdraaien
    return m


def mirror(nodes, markers, socks):
    """B-versie: gespiegeld in z (afbuigend naar -z)."""
    S = (1, 1, -1)
    out = {}
    for k, v in nodes.items():
        if isinstance(v, tuple):
            m, p = v
            out[k] = (m.transform(s=S), (p[0], p[1], -p[2]))
        else:
            out[k] = v.transform(s=S)
    mk_ = {k: (p[0], p[1], -p[2]) for k, p in markers.items()}
    sk = [(n, (p[0], p[1], -p[2])) for n, p in socks]
    return out, mk_, sk


HIDDEN = {"Col_Main"}


def scene(name, nodes, markers, socks):
    sc = mk.Scene(name)
    meshes = [v[0] if isinstance(v, tuple) else v for v in nodes.values()]
    used = sorted(set().union(*(set(m.M) for m in meshes)))
    for mname in used:
        ramp, sm, sh, op = T.MATERIALS[mname]
        sc.material(mname, ramp[2], ramp, sm, ramp_shift=sh, opts=dict(op))
    sc.add(mk.Part(name, None))
    for k, v in nodes.items():
        m, p = (v if isinstance(v, tuple) else (v, (0, 0, 0)))
        sc.add(mk.Part(k, m, tuple(float(c) for c in p), name, visible=k not in HIDDEN))
        if k == "Mesh":
            sc.parts[-1].gradient = 0.25
    for k, p in list(markers.items()) + list(socks):
        sc.add(mk.Part(k, None, tuple(float(c) for c in p), name))
    sc.facet_all()
    sc.bake_shading(density=700, radius=0.22, noise_freq=0.8, edge_range=(3.0, 12.0), max_samples=60000)
    return sc


def build_all():
    import unity_assets
    rng = np.random.default_rng([2026, 214])
    P, D, nodes, markers, socks, info = build(rng)
    kit_path = os.path.join(T.EXP_DIR, "TrackKit.json")
    kit = json.load(open(kit_path, encoding="utf-8"))
    guids = {n: unity_assets.material(T.ROOT, f"{T.MAT_DIR}/{n}.mat", n, ramp[2], sm, None, None, ramp, sh, op)
             for n, (ramp, sm, sh, op) in T.MATERIALS.items()}
    for tag, data in (("A", (nodes, markers, socks)), ("B", mirror(nodes, markers, socks))):
        name = f"Track_Switch_20m_{tag}"
        sc = scene(name, *data)
        fbx_rel = f"{T.MODEL_DIR}/{name}.fbx"
        sc.write_fbx_ascii(os.path.join(T.ROOT, fbx_rel))
        sc.write_glb(os.path.join(T.EXP_DIR, name + ".glb"), include_hidden=False)
        unity_assets.model_meta(T.ROOT, fbx_rel, {m: guids[m] for m in sc.materials})
        mesh = data[0]["Mesh"]
        tris = sum((v[0] if isinstance(v, tuple) else v).tri_count() for k, v in data[0].items() if k != "Col_Main")
        kit["assets"][name] = {"fbx": fbx_rel, "tris": int(tris), "length": L_STRAIGHT, "radius": None, "angleDeg": 0.0,
                               "rise": 0.0, "hill": 0.0, "side": 1 if tag == "A" else -1, "switch": info,
                               "min": np.round(mesh.V.min(0), 3).tolist(), "max": np.round(mesh.V.max(0), 3).tolist()}
        print(f"{name:28s} {tris:6d} tris  {info}")
    json.dump(kit, open(kit_path, "w", encoding="utf-8"), indent=2)


if __name__ == "__main__":
    build_all()
