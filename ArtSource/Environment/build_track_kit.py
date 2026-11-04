"""Spoorkit in de stijl van het concept (issue #53, plus #82 brug en een visueel kapot stuk voor #87).

Concept: dikke, lichte houten bielzen die ver onder de rails uitsteken, donkere chunky rails, een lage bedding,
en voor de 'high stakes'-routes een donkere houten schraagbrug met X-schoren. Alles low-poly met facetten,
kleuren uit palette.json, grote kleurvlakken uit de StylizedRamp-shader.

Maatvoering (1 unit = 1 m, Y omhoog):
  * Elk stuk begint in de oorsprong en loopt langs +X; pivot = hartlijn op maaiveldhoogte (y = 0).
  * Spoorwijdte 2,58 m hart-op-hart (rails op z = +-1,29), gelijk aan de wielen van loc en tender.
  * Bovenkant rail op y = RAIL_TOP (0,43 m). De treinmodellen hebben hun railhoogte op y = -0,62, dus
    een trein staat goed met zijn pivot 1,05 m boven de spoor-pivot (zie TrackKit.json).
  * Bielzen elke ~0,75 m, gelijk verdeeld zodat stukken naadloos op elkaar aansluiten.
  * Elk stuk heeft lege nodes Socket_Start/Socket_End (+ *_Fwd, 1 m verder in rijrichting): de Unity-kant
    (TrackPiece) leidt daar positie en richting uit af, los van as-conventies bij het importeren.
  * Col_*-onderdelen zijn eenvoudige botsvormen; de prefabbouwer maakt er MeshColliders van en verbergt ze.

Assets (Assets/Art/Models/Environment/Track):
  Track_Straight_1_5m, _3m, _6m, _12m       rechte stukken
  Track_Curve_R20_15_A/B, Track_Curve_R20_30_A/B   bochten (A en B buigen naar tegengestelde kanten;
                                            de prefabbouwer noemt ze Left/Right na meten in Unity)
  Track_BufferStop                           3 m eindstuk met stootblok (rood-wit, ronde buffers)
  Track_Broken_6m                            kapotte rail + gebroken bielzen (alleen visueel)
  Track_Bridge_Trestle_6m                    houten schraagbrug, poten 10 m lang (laat ze in het terrein zakken)
  Prop_WarningSign, Prop_Barrier             waarschuwingsbord en slagboom uit het concept

Gebruik: python ArtSource/Environment/build_track_kit.py
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import meshkit as mk  # noqa: E402
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
R = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

MAT_DIR = "Assets/Art/Materials/Environment/Track"
MODEL_DIR = "Assets/Art/Models/Environment/Track"
EXP_DIR = os.path.join(ROOT, "ArtSource", "Exports", "Environment", "Track")

# ------------------------------------------------------------------ maten
GAUGE = 2.58                 # hart-op-hart rails = wielen loc/tender (2 x 1,29)
RAIL_Z = GAUGE / 2
RAIL_W, RAIL_H = 0.155, 0.15
SLEEPER_TOP = 0.28
RAIL_TOP = SLEEPER_TOP + RAIL_H  # 0,43
SLEEPER = (0.50, 0.17, 3.50)  # breedte (langs spoor), hoogte, lengte (dwars): dicht op elkaar, zoals het concept
SPACING = 0.75
BED_TOP = 0.20
TRAIN_PIVOT_ABOVE_TRACK = RAIL_TOP + 0.62

MATERIALS = {
    # naam: (ramp, smoothness, ramp_shift, opts)
    "M_Track_Sleeper": (R["track_wood"], 0.10, -0.02, {"cells": 0.6, "cellSize": 0.5}),
    "M_Track_SleeperLight": (R["track_wood"], 0.10, 0.08, {"cells": 0.6, "cellSize": 0.5}),
    "M_Track_SleeperOld": (R["track_wood"], 0.10, -0.16, {"cells": 0.7, "cellSize": 0.5}),
    "M_Track_Rail": (R["metal"], 0.40, -0.30, {"cells": 0.3, "cellSize": 1.2, "rim": 0.25}),
    "M_Track_Plate": (R["ui_bg"], 0.30, 0.10, {"cells": 0.2}),
    "M_Track_Bed": (R["cliff"], 0.05, -0.12, {"cells": 0.8, "cellSize": 0.7, "grime": 0.6}),
    "M_Track_Pebble": (R["metal"], 0.05, 0.02, {"cells": 0.5, "cellSize": 0.4}),
    "M_Track_Beam": (R["track_wood"], 0.10, -0.30, {"cells": 0.6, "cellSize": 0.8, "grime": 0.8}),
    "M_Track_BeamDark": (R["track_wood"], 0.10, -0.42, {"cells": 0.6, "cellSize": 0.8, "grime": 0.8}),
    "M_Track_Red": (R["train_red"], 0.25, -0.02, {"cells": 0.5, "cellSize": 0.6}),
    "M_Track_White": (R["cloud_snow"], 0.20, 0.05, {"cells": 0.4, "cellSize": 0.6}),
    "M_Track_Warn": (R["warning"], 0.25, 0.02, {"cells": 0.4, "cellSize": 0.6}),
    "M_Track_Ink": (R["ink"], 0.20, 0.10, {"cells": 0.0}),
    "M_Track_Collider": (R["ui_bg"], 0.0, 0.0, {"cells": 0.0}),
}


# ------------------------------------------------------------------ helpers
def rbox(x0, x1, y0, y1, z0, z1, r, mat, div=2):
    m = mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, min(r, (x1 - x0) / 2.1, (y1 - y0) / 2.1, (z1 - z0) / 2.1), div, mat=mat)
    return m.transform(t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


class Path:
    """Hartlijn van een stuk: recht (radius None) of een cirkelboog die naar +z (side=+1) of -z (side=-1) buigt."""

    def __init__(self, length=None, radius=None, angle_deg=None, side=1):
        self.radius, self.side = radius, side
        if radius is None:
            self.length, self.angle = length, 0.0
        else:
            self.angle = math.radians(angle_deg)
            self.length = radius * self.angle

    def frame(self, s):
        """Positie (x, z) en koers (rad, + = naar +z) op booglengte s."""
        if self.radius is None:
            return np.array([s, 0.0]), 0.0
        phi = s / self.radius
        return np.array([self.radius * math.sin(phi), self.side * self.radius * (1 - math.cos(phi))]), self.side * phi

    def bend(self, m):
        """Vervorm een recht gemodelleerde mesh (x = booglengte, z = dwars) naar deze hartlijn."""
        if self.radius is None:
            return m
        m = m.copy()
        V = m.V
        phi = V[:, 0] / self.radius
        r = self.radius - self.side * V[:, 2]
        V2 = np.stack([r * np.sin(phi), V[:, 1], self.side * (self.radius - r * np.cos(phi))], axis=1)
        m.V = V2
        return m

    def place(self, m, s):
        """Plaats een star onderdeel (gemodelleerd rond x = 0) op booglengte s, meegedraaid met de koers."""
        p, h = self.frame(s)
        return m.transform(R=mk.rot_y(-h), t=(p[0], 0.0, p[1]))


def stations(length, spacing=SPACING):
    n = max(1, int(round(length / spacing)))
    step = length / n
    return [(k + 0.5) * step for k in range(n)]


def rail_profile():
    """Chunky rail: brede voet, smallere lijf, ronde kop (doorsnede z, y; tegen de klok in)."""
    w, h = RAIL_W, RAIL_H
    return [(-w * 0.75, 0.0), (w * 0.75, 0.0), (w * 0.75, 0.025), (w * 0.30, 0.045), (w * 0.30, h * 0.62),
            (w * 0.50, h * 0.68), (w * 0.50, h - 0.02), (w * 0.38, h), (-w * 0.38, h), (-w * 0.50, h - 0.02),
            (-w * 0.50, h * 0.68), (-w * 0.30, h * 0.62), (-w * 0.30, 0.045), (-w * 0.75, 0.025)]


def rail(path, z, s0=0.0, s1=None, mat="M_Track_Rail"):
    s1 = path.length if s1 is None else s1
    prof = [(pz + z, py + SLEEPER_TOP) for pz, py in rail_profile()]
    nx = max(2, int(math.ceil((s1 - s0) / 0.5)))
    m = mk.extrude_profile(prof, s0, s1, nx=nx, round0=0.0, round1=0.0, rings=1, mat=mat)
    return path.bend(m)


def sleeper(rng, mat=None, length=SLEEPER[2], y_top=SLEEPER_TOP, plates=True):
    """Eén biels met kleine variatie (hand gelegd), plus donkere onderlegplaatjes met spijkerkoppen."""
    w, h = SLEEPER[0], SLEEPER[1]
    mat = mat or rng.choice(["M_Track_Sleeper", "M_Track_Sleeper", "M_Track_SleeperLight", "M_Track_SleeperOld"],
                            p=[0.45, 0.2, 0.2, 0.15])
    L = length * rng.uniform(0.96, 1.03)
    m = rbox(-w / 2, w / 2, y_top - h, y_top, -L / 2, L / 2, 0.05, mat, div=1)
    m.V[:, 1] += (m.V[:, 1] > y_top - 0.01) * rng.uniform(-0.012, 0.012) * np.sign(m.V[:, 2])   # licht scheef
    parts = [m]
    if plates:
        for zr in (-RAIL_Z, RAIL_Z):
            parts.append(rbox(-0.15, 0.15, y_top - 0.01, y_top + 0.022, zr - 0.17, zr + 0.17, 0.012, "M_Track_Plate", div=1))
            for dx, dz in ((-0.07, -0.12), (0.07, 0.12)):
                parts.append(mk.cylinder(0.03, 0.05, 4, 0.0, mat="M_Track_Plate", axis="y").transform(
                    t=(dx, y_top + 0.035, zr + dz)))
    out = mk.merge(*parts)
    return out.transform(R=mk.rot_y(rng.uniform(-0.035, 0.035)), t=(0, 0, rng.uniform(-0.05, 0.05)))


def bed(path, rng, width_top=3.0, width_bot=4.3):
    """Lage bedding (grind/aarde) met afgeschuinde zijkanten en wat steentjes langs de rand."""
    wt, wb = width_top / 2, width_bot / 2
    prof = [(-wb, -0.06), (wb, -0.06), (wb - 0.10, 0.06), (wt + 0.05, BED_TOP - 0.02), (wt - 0.15, BED_TOP),
            (-wt + 0.15, BED_TOP), (-wt - 0.05, BED_TOP - 0.02), (-wb + 0.10, 0.06)]
    nx = max(2, int(math.ceil(path.length / 0.75)))
    m = path.bend(mk.extrude_profile(prof, 0.0, path.length, nx=nx, rings=1, mat="M_Track_Bed"))
    parts = [m]
    for _ in range(int(path.length * 2.2)):
        s = rng.uniform(0.2, path.length - 0.2)
        z = rng.choice([-1, 1]) * rng.uniform(wt - 0.1, wb - 0.25)
        r = rng.uniform(0.06, 0.13)
        peb = mk.ellipsoid(r, r * 0.6, r * rng.uniform(0.8, 1.2), 6, 3, mat="M_Track_Pebble")
        peb.sharp = True
        y = np.interp(abs(z), [wt - 0.15, wb - 0.1], [BED_TOP, 0.06])
        parts.append(path.place(peb.transform(R=mk.rot_y(rng.uniform(0, math.pi)), t=(0, y, z)), s))
    return mk.merge(*parts)


def track(path, rng, with_bed=True, sleeper_len=SLEEPER[2]):
    parts = [rail(path, -RAIL_Z), rail(path, RAIL_Z)]
    for s in stations(path.length):
        parts.append(path.place(sleeper(rng, length=sleeper_len), s))
    if with_bed:
        parts.append(bed(path, rng))
    return mk.merge(*parts)


def collider_slab(path, half_w=1.9, y0=0.0, y1=RAIL_TOP, step=1.5):
    """Simpele botsvorm: een plaat over de volle breedte tot railhoogte, met weinig driehoeken."""
    nx = max(1, int(math.ceil(path.length / step)))
    prof = [(-half_w, y0), (half_w, y0), (half_w, y1), (-half_w, y1)]
    return path.bend(mk.extrude_profile(prof, 0.0, path.length, nx=nx, rings=1, mat="M_Track_Collider"))


def sockets(path):
    """Lege nodes aan begin en eind (+ een punt 1 m verder in rijrichting) voor TrackPiece in Unity."""
    p_end, h_end = path.frame(path.length)
    fwd = np.array([math.cos(h_end), math.sin(h_end)])
    return [("Socket_Start", (0.0, RAIL_TOP, 0.0)), ("Socket_Start_Fwd", (1.0, RAIL_TOP, 0.0)),
            ("Socket_End", (p_end[0], RAIL_TOP, p_end[1])),
            ("Socket_End_Fwd", (p_end[0] + fwd[0], RAIL_TOP, p_end[1] + fwd[1]))]


# ------------------------------------------------------------------ bijzondere stukken
def buffer_stop(rng):
    path = Path(3.0)
    parts = [track(path, rng)]
    x = 2.45
    yb = RAIL_TOP + 0.80                                   # bufferhoogte van de treinen
    for zs in (-RAIL_Z, RAIL_Z):
        # staander op de rail + schuine schoor naar achteren, beide hout
        parts.append(rbox(x - 0.14, x + 0.14, SLEEPER_TOP, yb + 0.35, zs - 0.14, zs + 0.14, 0.05, "M_Track_Beam"))
        strut = mk.capsule_between((x + 0.05, yb + 0.05, zs), (x + 1.25, SLEEPER_TOP - 0.05, zs), 0.11, 0.11, 6, 1,
                                   mat="M_Track_BeamDark")
        strut.sharp = True
        parts.append(strut)
        parts.append(rbox(x + 1.05, x + 1.55, -0.02, SLEEPER_TOP + 0.05, zs - 0.30, zs + 0.30, 0.06, "M_Track_Beam"))
    # dwarsbalk met rood-witte vlakken
    n = 6
    z0, z1 = -RAIL_Z - 0.45, RAIL_Z + 0.45
    for k in range(n):
        za, zb = z0 + (z1 - z0) * k / n, z0 + (z1 - z0) * (k + 1) / n
        parts.append(rbox(x - 0.30, x - 0.02, yb - 0.24, yb + 0.24, za + 0.004, zb - 0.004, 0.05,
                          "M_Track_Red" if k % 2 == 0 else "M_Track_White"))
    for zb in (-0.62, 0.62):                                # ronde buffers op hoogte van de treinbuffers
        parts.append(mk.cylinder(0.20, 0.16, 10, 0.05, mat="M_Track_Plate", axis="x").transform(t=(x - 0.40, yb, zb)))
        parts.append(mk.cylinder(0.08, 0.14, 8, 0.02, mat="M_Track_Plate", axis="x").transform(t=(x - 0.28, yb, zb)))
    col = mk.merge(collider_slab(path), rbox(x - 0.32, x + 1.6, 0.0, yb + 0.35, z0, z1, 0.0, "M_Track_Collider", div=1))
    return path, mk.merge(*parts), col


def broken(rng):
    """Kapot stuk: één rail gebroken met een gat en een omhoog gebogen eind, gespleten en ontbrekende bielzen."""
    path = Path(6.0)
    parts = [rail(path, RAIL_Z), bed(path, rng)]
    gap0, gap1 = 2.85, 3.35
    parts.append(rail(path, -RAIL_Z, 0.0, gap0 - 0.55))
    parts.append(rail(path, -RAIL_Z, gap1 + 0.6, path.length))
    # omhoog/uitgebogen stompjes
    stub_a = rail(Path(0.6), 0.0, 0.0, 0.6).transform(t=(0, -SLEEPER_TOP, 0))
    stub_a = stub_a.transform(R=mk.rot_z(math.radians(14)) @ mk.rot_y(math.radians(-8)), t=(gap0 - 0.55, SLEEPER_TOP, -RAIL_Z))
    stub_b = rail(Path(0.6), 0.0, 0.0, 0.6).transform(t=(-0.6, -SLEEPER_TOP, 0))
    stub_b = stub_b.transform(R=mk.rot_z(math.radians(-9)) @ mk.rot_y(math.radians(12)), t=(gap1 + 0.6, SLEEPER_TOP + 0.02, -RAIL_Z))
    parts += [stub_a, stub_b]
    for i, s in enumerate(stations(path.length)):
        if abs(s - 3.0) < 0.5:
            # gespleten biels: twee helften, scheef
            for sgn, ang in ((-1, 0.04), (1, -0.07)):
                half = rbox(-0.20, 0.20, SLEEPER_TOP - 0.17, SLEEPER_TOP, 0.06, SLEEPER[2] / 2 - 0.1, 0.05, "M_Track_SleeperOld", div=1)
                if sgn < 0:
                    half = half.transform(s=(1, 1, -1))
                half = half.transform(R=mk.rot_x(sgn * ang) @ mk.rot_y(sgn * 0.10), t=(0, -0.02, sgn * 0.10))
                parts.append(path.place(half, s))
        elif abs(s - 2.2) < 0.3:
            continue                                           # ontbrekende biels
        else:
            parts.append(path.place(sleeper(rng, mat="M_Track_SleeperOld" if rng.random() < 0.5 else None), s))
    # losse spijkers en een plaatje naast het spoor
    for _ in range(4):
        sp = mk.cylinder(0.025, 0.22, 6, 0.0, mat="M_Track_Plate", axis="x")
        sp.sharp = True
        parts.append(sp.transform(R=mk.rot_y(rng.uniform(0, math.pi)), t=(rng.uniform(2.2, 4.0), BED_TOP + 0.03, rng.uniform(-2.0, -1.4))))
    parts.append(rbox(3.6, 3.9, BED_TOP, BED_TOP + 0.03, -1.75, -1.4, 0.01, "M_Track_Plate", div=1).transform(R=mk.rot_y(0.4)))
    return path, mk.merge(*parts), collider_slab(path)


def trestle(rng, length=6.0, leg=10.0):
    """Houten schraagbrug: bielzen op langsliggers, één schraag (vier poten, X-schoren) per module."""
    path = Path(length)
    parts = [rail(path, -RAIL_Z), rail(path, RAIL_Z)]
    for s in stations(length):
        parts.append(path.place(sleeper(rng, length=3.9), s))
    stringer_top = SLEEPER_TOP - SLEEPER[1]
    for zs in (-RAIL_Z, -RAIL_Z + 0.55, RAIL_Z - 0.55, RAIL_Z):
        parts.append(rbox(-0.02, length + 0.02, stringer_top - 0.34, stringer_top, zs - 0.15, zs + 0.15, 0.05, "M_Track_Beam"))
    # randbalk op de bielzen (zoals het concept)
    for zs in (-1.78, 1.78):
        parts.append(rbox(0.0, length, SLEEPER_TOP, SLEEPER_TOP + 0.14, zs - 0.10, zs + 0.10, 0.04, "M_Track_BeamDark"))
    # schraag in het midden
    xc = length / 2
    cap_top = stringer_top - 0.34
    parts.append(rbox(xc - 0.24, xc + 0.24, cap_top - 0.32, cap_top, -2.35, 2.35, 0.06, "M_Track_BeamDark"))
    bottom = cap_top - leg
    legs = [(-0.75, -0.75), (0.75, 0.75), (-1.55, -2.6), (1.55, 2.6)]       # (z boven, z onder): buitenste poten schuin
    for zt, zb in legs:
        post = mk.capsule_between((xc, cap_top - 0.2, zt), (xc, bottom, zb), 0.17, 0.19, 6, 1, mat="M_Track_Beam")
        post.sharp = True
        parts.append(post)
    # horizontale klossen + X-schoren per 3,3 m
    tiers = np.arange(cap_top - 0.3, bottom, -3.3)
    for k in range(len(tiers) - 1):
        ya, yb = tiers[k], tiers[k + 1]
        za = np.interp(ya, [bottom, cap_top], [2.6, 1.55])
        zb_ = np.interp(yb, [bottom, cap_top], [2.6, 1.55])
        parts.append(rbox(xc - 0.12, xc + 0.12, yb - 0.10, yb + 0.10, -zb_ - 0.2, zb_ + 0.2, 0.04, "M_Track_BeamDark"))
        for sgn in (1, -1):
            br = mk.capsule_between((xc + 0.22 * sgn, ya, -za), (xc + 0.22 * sgn, yb, zb_), 0.08, 0.08, 5, 1, mat="M_Track_BeamDark")
            br2 = mk.capsule_between((xc + 0.22 * sgn, ya, za), (xc + 0.22 * sgn, yb, -zb_), 0.08, 0.08, 5, 1, mat="M_Track_BeamDark")
            br.sharp = br2.sharp = True
            parts += [br, br2]
    # langsschoren tussen deze schraag en de volgende (laag, zodat stukken aansluiten)
    for zs in (-0.75, 0.75):
        b = mk.capsule_between((0.05, cap_top - 1.0, zs), (length - 0.05, cap_top - 1.0, zs), 0.07, 0.07, 5, 1, mat="M_Track_BeamDark")
        b.sharp = True
        parts.append(b)
    col = mk.merge(collider_slab(path, half_w=2.0, y0=cap_top - 0.3),
                   *[rbox(xc - 0.2, xc + 0.2, bottom, cap_top - 0.3, z - 0.2, z + 0.2, 0.0, "M_Track_Collider", div=1)
                     for z in (-1.4, 1.4)])
    return path, mk.merge(*parts), col


def warning_sign(rng):
    """Driehoekig geel waarschuwingsbord met rode rand en een '!' op een houten paal."""
    parts = [rbox(-0.09, 0.09, 0.0, 2.1, -0.09, 0.09, 0.04, "M_Track_BeamDark")]

    def tri(r, depth, mat, x0):
        pts = [(math.cos(a) * r, math.sin(a) * r) for a in (math.pi / 2, math.pi / 2 + 2 * math.pi / 3, math.pi / 2 + 4 * math.pi / 3)]
        m = mk.prism(mk.chamfer_poly(pts, r * 0.18), 0.0, depth, 0.02, mat=mat)   # in xy-vlak, dikte langs z
        return m.transform(R=mk.rot_y(-math.pi / 2), t=(x0, 2.0, 0))
    parts.append(tri(0.78, 0.07, "M_Track_Red", 0.10))
    parts.append(tri(0.58, 0.04, "M_Track_Warn", 0.17))
    parts.append(rbox(0.19, 0.23, 1.92, 2.28, -0.055, 0.055, 0.02, "M_Track_Ink"))
    parts.append(rbox(0.19, 0.23, 1.76, 1.86, -0.055, 0.055, 0.02, "M_Track_Ink"))
    m = mk.merge(*parts)
    col = rbox(-0.12, 0.25, 0.0, 2.7, -0.7, 0.7, 0.0, "M_Track_Collider", div=1)
    return m, col


def barrier(rng):
    """Slagboom/afzetting: twee houten paaltjes met een rood-witte plank (concept, rechtsboven)."""
    parts = []
    for z in (-1.1, 1.1):
        parts.append(rbox(-0.10, 0.10, 0.0, 1.15, z - 0.10, z + 0.10, 0.04, "M_Track_BeamDark"))
        parts.append(rbox(-0.16, 0.16, 0.0, 0.12, z - 0.22, z + 0.22, 0.04, "M_Track_Beam"))
    n = 6
    for row, y in enumerate((0.95, 0.55)):
        for k in range(n):
            za, zb = -1.45 + 2.9 * k / n, -1.45 + 2.9 * (k + 1) / n
            mat = "M_Track_Red" if (k + row) % 2 == 0 else "M_Track_White"
            parts.append(rbox(0.11, 0.19, y - 0.13, y + 0.13, za + 0.003, zb - 0.003, 0.03, mat, div=1))
    m = mk.merge(*parts).transform(R=mk.rot_z(0.0))
    col = rbox(-0.2, 0.2, 0.0, 1.15, -1.5, 1.5, 0.0, "M_Track_Collider", div=1)
    return m, col


# ------------------------------------------------------------------ export
def make_scene(name, mesh, col, socks, density, radius):
    sc = mk.Scene(name)
    used = sorted(set(mesh.M) | (set(col.M) if col is not None else set()))
    for mname in used:
        ramp, sm, sh, op = MATERIALS[mname]
        sc.material(mname, ramp[2], ramp, sm, ramp_shift=sh, opts=dict(op))
    sc.add(mk.Part(name, None))
    sc.add(mk.Part("Mesh", mesh, (0, 0, 0), name))
    sc.parts[-1].gradient = 0.25
    if col is not None:
        sc.add(mk.Part("Col_Main", col, (0, 0, 0), name, visible=False))
    for sname, p in socks:
        sc.add(mk.Part(sname, None, tuple(float(v) for v in p), name))
    sc.facet_all()
    sc.bake_shading(density=density, radius=radius, noise_freq=0.8, edge_range=(3.0, 12.0), max_samples=40000)
    return sc


def build_all():
    A = {}
    rng = np.random.default_rng([2026, 53])
    for L, tag in ((1.5, "1_5m"), (3.0, "3m"), (6.0, "6m"), (12.0, "12m")):
        p = Path(L)
        A[f"Track_Straight_{tag}"] = (p, track(p, rng), collider_slab(p), sockets(p))
    for ang in (15, 30):
        for side, tag in ((1, "A"), (-1, "B")):
            p = Path(radius=20.0, angle_deg=ang, side=side)
            A[f"Track_Curve_R20_{ang}_{tag}"] = (p, track(p, rng), collider_slab(p), sockets(p))
    p, m, c = buffer_stop(rng)
    A["Track_BufferStop"] = (p, m, c, sockets(p))
    p, m, c = broken(rng)
    A["Track_Broken_6m"] = (p, m, c, sockets(p))
    rng = np.random.default_rng([2026, 82])
    p, m, c = trestle(rng)
    A["Track_Bridge_Trestle_6m"] = (p, m, c, sockets(p))
    rng = np.random.default_rng([2026, 87])
    m, c = warning_sign(rng)
    A["Prop_WarningSign"] = (None, m, c, [])
    m, c = barrier(rng)
    A["Prop_Barrier"] = (None, m, c, [])

    os.makedirs(EXP_DIR, exist_ok=True)
    unity_assets.folder_meta(ROOT, MAT_DIR)
    os.makedirs(os.path.join(ROOT, MODEL_DIR), exist_ok=True)
    unity_assets.folder_meta(ROOT, MODEL_DIR)
    guids = {n: unity_assets.material(ROOT, f"{MAT_DIR}/{n}.mat", n, ramp[2], sm, None, None, ramp, sh, op)
             for n, (ramp, sm, sh, op) in MATERIALS.items()}
    manifest = {}
    for name, (path, mesh, col, socks) in A.items():
        sc = make_scene(name, mesh, col, socks, density=700, radius=0.22)
        fbx_rel = f"{MODEL_DIR}/{name}.fbx"
        sc.write_fbx_ascii(os.path.join(ROOT, fbx_rel))
        sc.write_glb(os.path.join(EXP_DIR, name + ".glb"), include_hidden=False)
        unity_assets.model_meta(ROOT, fbx_rel, {m: guids[m] for m in sc.materials})
        V = mesh.V
        manifest[name] = {"fbx": fbx_rel, "tris": int(mesh.tri_count()),
                          "length": round(path.length, 4) if path else None,
                          "radius": path.radius if path else None,
                          "angleDeg": round(math.degrees(path.angle), 3) if path else None,
                          "side": path.side if path and path.radius else 0,
                          "min": np.round(V.min(0), 3).tolist(), "max": np.round(V.max(0), 3).tolist()}
        print(f"{name:28s} {mesh.tri_count():6d} tris")
    meta = {"gauge": GAUGE, "railTop": RAIL_TOP, "trainPivotAboveTrack": TRAIN_PIVOT_ABOVE_TRACK, "sleeperSpacing": SPACING}
    json.dump({"track": meta, "assets": manifest}, open(os.path.join(EXP_DIR, "TrackKit.json"), "w", encoding="utf-8"), indent=2)


if __name__ == "__main__":
    build_all()
