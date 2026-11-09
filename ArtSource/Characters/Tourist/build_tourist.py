"""Toerist / backpacker — personagevariant op het conducteur-basismodel.

Referentie: Docs/References/Characters/tourist-concept.png
Kenmerken (silhouet eerst):
  * safarihoed met brede rand, rode band en een zonnebril op de bol
  * rood hawaïhemd met bloemenprint, open kraag met crème shirt eronder, korte mouwen
  * olijfkleurige korte broek met beenzakken, blote beentjes, bruine wandelschoenen
  * grote petrol rugzak met opgerold slaapmatje en een waterfles in de zijzak
  * kaart in de linkerhand, treinkaartje in de rechterhand
Kleuren: ramps safari_hat, shirt_coral, shorts_olive, backpack_teal, boot_brown (palette.json, 'Reizigers & crew').

Uitvoer: Assets/Art/Models/Characters/Tourist/Tourist.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/Tourist/build_tourist.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "Tourist"
P = "M_Tourist_"
SLEEVE_END = -0.078      # lokale arm-y waar de korte mouw ophoudt


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Shirt", "shirt_coral", 0.0, 0.22, paint=1.2)
    ck.material(sc, P + "ShirtDark", "shirt_coral", -0.30, 0.22)
    ck.material(sc, P + "Undershirt", "parchment", 0.20, 0.25)
    ck.material(sc, P + "Button", "parchment", 0.35, 0.30)
    ck.material(sc, P + "PrintFlower", "crate", 0.15, 0.25)
    ck.material(sc, P + "PrintFlowerAlt", "accent_yellow", 0.05, 0.25)
    ck.material(sc, P + "PrintHeart", "parchment", 0.40, 0.25)
    ck.material(sc, P + "PrintLeaf", "grass", -0.15, 0.25)
    ck.material(sc, P + "Shorts", "shorts_olive", 0.0, 0.22, paint=1.2)
    ck.material(sc, P + "ShortsDark", "shorts_olive", -0.30, 0.22)
    ck.material(sc, P + "Boot", "boot_brown", 0.05, 0.25)
    ck.material(sc, P + "Hat", "safari_hat", 0.0, 0.22, paint=1.1)
    ck.material(sc, P + "HatBand", "train_red", 0.0, 0.22)
    ck.material(sc, P + "Lens", "ui_bg", 0.15, 0.45, spec=0.20)
    ck.material(sc, P + "Frame", "ink", 0.10, 0.30)
    ck.material(sc, P + "Backpack", "backpack_teal", 0.0, 0.22, paint=1.2)
    ck.material(sc, P + "BackpackDark", "backpack_teal", -0.30, 0.22)
    ck.material(sc, P + "Strap", "boot_brown", 0.0, 0.25)
    ck.material(sc, P + "Buckle", "gold_trim", 0.0, 0.35)
    ck.material(sc, P + "Bedroll", "helmet", 0.0, 0.25)
    ck.material(sc, P + "BedrollDark", "helmet", -0.45, 0.25)
    ck.material(sc, P + "BedrollStrap", "leather", -0.10, 0.25)
    ck.material(sc, P + "Bottle", "water", 0.10, 0.40, spec=0.15)
    ck.material(sc, P + "BottleCap", "ui_bg", 0.10, 0.30)
    ck.material(sc, P + "Map", "parchment", 0.05, 0.25)
    ck.material(sc, P + "MapGreen", "grass", 0.10, 0.25)
    ck.material(sc, P + "MapPath", "parchment", 0.45, 0.25)
    ck.material(sc, P + "MapPin", "title_red", 0.10, 0.30)
    ck.material(sc, P + "MapIcon", "cab_interior", 0.0, 0.25)
    ck.material(sc, P + "Ticket", "accent_yellow", 0.0, 0.25)
    ck.material(sc, P + "TicketInk", "cab_interior", -0.10, 0.25)


# ------------------------------------------------------------------- stofprint
def flower(size=1.0, alt=False):
    """Plat hawaï-bloemetje (lokaal XY, +Z naar buiten)."""
    mat = P + ("PrintFlowerAlt" if alt else "PrintFlower")
    parts = []
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.4
        parts.append(mk.ellipsoid(0.019 * size, 0.013 * size, 0.0035, 12, 6, mat=mat)
                     .transform(R=mk.rot_z(a), t=(0.016 * size * math.cos(a), 0.016 * size * math.sin(a), 0.0)))
    parts.append(mk.ellipsoid(0.0085 * size, 0.0085 * size, 0.004, 10, 5, mat=P + "PrintHeart")
                 .transform(t=(0, 0, 0.0015)))
    return mk.merge(*parts)


def leaf(size=1.0, angle=0.0):
    return mk.ellipsoid(0.024 * size, 0.0095 * size, 0.003, 12, 6, mat=P + "PrintLeaf").transform(
        R=mk.rot_z(angle), t=(0.030 * size * math.cos(angle), 0.030 * size * math.sin(angle), -0.0008))


# ------------------------------------------------------------------------ shirt
def build_shirt():
    parts = []
    # losse hemdzoom over de broek
    parts.append(ck.body_band(1.0, [(0.99, 0.352), (1.03, 0.325), (1.058, 0.302), (1.052, 0.288),
                                    (1.02, 0.284), (0.99, 0.30)], P + "Shirt"))
    # crème shirt in de V van de open kraag
    v = np.array([(x, y, bc.body_surface(x, y)[0] + 0.004) for x, y in ((-0.074, 0.534), (0.0, 0.438), (0.074, 0.534))])
    parts.append(mk.Mesh(v, [(0, 1, 2)], [P + "Undershirt"]))
    # opstaande, open kraag (revers) in hemdkleur
    for side in (1, -1):
        lap = [(side * 0.070, 0.540), (side * 0.150, 0.522), (side * 0.118, 0.478), (side * 0.010, 0.432)]
        parts.append(ck.front_plate(lap if side > 0 else lap[::-1], 0.011, P + "Shirt", lift=0.005, chamfer=0.004))
    parts.append(ck.front_plate([(-0.011, 0.436), (0.011, 0.436), (0.011, 0.300), (-0.011, 0.300)], 0.005,
                                P + "ShirtDark", lift=0.001, chamfer=0.002))
    for y in (0.405, 0.345):
        z, nrm = bc.body_surface(0.0, y)
        parts.append(mk.ellipsoid(0.014, 0.014, 0.007, 14, 7, mat=P + "Button").transform(
            R=bc.face_frame(nrm), t=(0, y, z + 0.006)))
    # bloemenprint (rug zit grotendeels onder de rugzak)
    spots = [(-52, 0.355, 1.0, False), (50, 0.420, 1.05, True), (-60, 0.470, 0.9, True), (15, 0.325, 0.8, False),
             (78, 0.335, 0.9, False), (-84, 0.395, 1.0, False), (110, 0.36, 0.95, True), (-118, 0.33, 0.9, True),
             (160, 0.318, 0.8, False), (-165, 0.320, 0.8, True)]
    for th, y, s, alt in spots:
        decal = mk.merge(flower(s, alt), leaf(s, -0.6), leaf(0.85 * s, 2.3))
        parts.append(ck.wrap(decal.transform(R=mk.rot_z(th * 0.013)), th * DEG, y, 0.003))
    return mk.merge(*parts)


def build_arm(side):
    arm = ck.build_arm(P + "Shirt", P + "Skin", cut=SLEEVE_END, sleeve_bulge=1.14)
    cuff = mk.torus(0.083, 0.012, 32, 8, mat=P + "Shirt", a=1.0, b=0.88).transform(t=(0, SLEEVE_END + 0.004, 0))
    prints = []
    for nrm, y, alt in (((side, 0, 0.15), -0.020, False), ((0.25 * side, 0, 1.0), -0.050, True)):
        n = np.array(nrm, float)
        n /= np.linalg.norm(n)
        pos = np.array([n[0] * 0.090, y, n[2] * 0.090 * 0.88])
        prints.append(mk.merge(flower(0.75, alt), leaf(0.7, 2.0)).transform(R=ck.frame_at(n), t=pos))
    return mk.merge(arm, cuff, *prints)


# ------------------------------------------------------------------ broek & benen
def build_shorts():
    parts = []
    for side in (1, -1):
        cx = side * ck.LEG_X
        hem = [(0.0, 0.214), (0.012, 0.209), (0.016, 0.197), (0.012, 0.187), (0.0, 0.190)]
        parts.append(ck.surface_lathe(hem, P + "Shorts", seg=36, cx=cx, rmax=0.2))
        cuff = [(0.0, 0.112), (0.014, 0.106), (0.017, 0.095), (0.012, 0.084), (0.0, 0.088)]
        parts.append(ck.surface_lathe(cuff, P + "Boot", seg=36, cx=cx, rmax=0.2))
        pocket = ck.plate([(-0.038, -0.032), (0.038, -0.032), (0.036, 0.030), (-0.036, 0.030)], 0.013,
                          P + "ShortsDark", chamfer=0.004)
        flap = ck.plate([(-0.041, -0.010), (0.041, -0.010), (0.039, 0.012), (-0.039, 0.012)], 0.010,
                        P + "Shorts", chamfer=0.003).transform(t=(0, 0.022, 0.012))
        parts.append(ck.wrap(mk.merge(pocket, flap), side * 92 * DEG, 0.252, 0.0, cx=cx))
    # broekband net onder de hemdzoom
    parts.append(ck.body_band(1.0, [(0.99, 0.300), (1.012, 0.292), (1.012, 0.272), (0.99, 0.266)], P + "ShortsDark"))
    return mk.merge(*parts)


# ------------------------------------------------------------------------- hoed
def build_hat():
    """Lokaal: oorsprong = onderkant bol, midden van het hoofd (zoals de pet van de conducteur)."""
    a, b = 0.300, 0.285
    crown = mk.lathe([(0.0, 0.0), (1.0, 0.0), (1.0, 0.05), (0.985, 0.11), (0.945, 0.160), (0.865, 0.204),
                      (0.71, 0.236), (0.45, 0.252), (0.0, 0.257)], 44, a, b, 2.2, mat=P + "Hat")
    band = mk.lathe([(0.0, 0.006), (1.022, 0.006), (1.028, 0.014), (1.028, 0.054), (1.018, 0.062), (0.0, 0.062)],
                    44, a, b, 2.2, mat=P + "HatBand")
    brim = mk.lathe([(0.97, 0.022), (1.25, 0.006), (1.48, -0.024), (1.565, -0.036), (1.605, -0.026),
                     (1.60, -0.010), (1.565, 0.000), (1.26, 0.030), (0.97, 0.046)], 56, a, b, 2.2, closed=True,
                    mat=P + "Hat")
    # zonnebril op de bol, boven de band
    glasses = []
    for side in (1, -1):
        ang = side * 17 * DEG
        r = 0.975
        pos = np.array([a * r * math.sin(ang), 0.112, b * r * math.cos(ang) + 0.012])
        R = mk.rot_y(ang) @ mk.rot_x(-12 * DEG)
        lens = mk.ellipsoid(0.070, 0.050, 0.016, 20, 8, mat=P + "Lens", n=2.8).transform(R=R, t=pos)
        loop = [(0.072 * mk.spow(math.cos(t), 2 / 2.8), 0.052 * mk.spow(math.sin(t), 2 / 2.8), 0.004)
                for t in np.linspace(0, 2 * math.pi, 33)]
        rim = ck.tube(loop, 0.0085, P + "Frame", smooth=False, seg=8).transform(R=R, t=pos)
        glasses += [lens, rim]
    bridge = ck.tube([(-0.020, 0.126, b * 0.99 + 0.020), (0.0, 0.132, b * 0.99 + 0.024), (0.020, 0.126, b * 0.99 + 0.020)],
                     0.008, P + "Frame", n=8)
    return mk.merge(crown, band, brim, *glasses, bridge)


# ----------------------------------------------------------------------- rugzak
def build_backpack():
    z0 = -0.335
    parts = [mk.rounded_box(0.40, 0.40, 0.19, 0.070, 3, mat=P + "Backpack").transform(t=(0, 0.505, z0)),
             mk.rounded_box(0.29, 0.17, 0.075, 0.030, 3, mat=P + "BackpackDark").transform(t=(0, 0.395, z0 - 0.11)),
             mk.rounded_box(0.30, 0.055, 0.085, 0.022, 2, mat=P + "Backpack").transform(t=(0, 0.474, z0 - 0.112)),
             mk.rounded_box(0.37, 0.085, 0.215, 0.034, 2, mat=P + "BackpackDark").transform(t=(0, 0.690, z0 - 0.005)),
             ck.squircle_ring(0.034, 0.026, 0.005, P + "Buckle").transform(t=(0, 0.455, z0 - 0.157))]
    # slaapmatje
    yb = 0.790
    parts.append(mk.cylinder(0.078, 0.44, 32, 0.022, mat=P + "Bedroll", axis="x").transform(t=(0, yb, z0)))
    for sx in (1, -1):
        for R_, r_ in ((0.050, 0.011), (0.024, 0.009)):
            parts.append(mk.torus(R_, r_, 24, 6, mat=P + "BedrollDark").transform(
                R=mk.rot_z(math.pi / 2), t=(sx * 0.219, yb, z0)))
        parts.append(mk.torus(0.081, 0.010, 32, 6, mat=P + "BedrollStrap", ry=0.018).transform(
            R=mk.rot_z(math.pi / 2), t=(sx * 0.115, yb, z0)))
    # zijzak met waterfles (rechterkant, -X)
    parts.append(mk.rounded_box(0.075, 0.130, 0.135, 0.030, 2, mat=P + "Backpack").transform(t=(-0.220, 0.375, z0)))
    parts.append(mk.capsule_between((-0.226, 0.360, z0), (-0.226, 0.505, z0), 0.037, 0.034, 16, 4, mat=P + "Bottle"))
    parts.append(mk.cylinder(0.024, 0.040, 16, 0.006, mat=P + "BottleCap", axis="y").transform(t=(-0.226, 0.552, z0)))
    # schouderbanden over de 'schouders' naar voren, met gesp
    for s in (1, -1):
        ctrl = [(s * 150 * DEG, 0.55), (s * 112 * DEG, 0.565), (s * 72 * DEG, 0.560), (s * 42 * DEG, 0.525),
                (s * 33 * DEG, 0.430), (s * 31 * DEG, 0.330)]
        parts.append(ck.strap(ctrl, 0.052, 0.016, P + "Strap", lift=0.010))
        S, N = ck.surface(np.array([s * 33 * DEG]), np.array([0.445]))
        buckle = ck.squircle_ring(0.062, 0.040, 0.0065, P + "Buckle")
        parts.append(buckle.transform(R=ck.frame_at(N[0]), t=S[0] + N[0] * 0.030))
    return mk.merge(*parts)


# ------------------------------------------------------------------------ props
def build_map():
    """Harmonicakaart (3 panelen), lokaal: midden van de kaart, voorkant +Z."""
    w, h, t = 0.105, 0.205, 0.006
    panels, prints = [], []
    for k, (x, ang) in enumerate(((-w, 14), (0.0, -14), (w, 14))):
        R = mk.rot_y(ang * DEG)
        off = np.array([x * math.cos(14 * DEG), 0.0, 0.012 if k == 1 else 0.0])
        panels.append(mk.rounded_box(w, h, t, 0.0028, 1, mat=P + "Map").transform(R=R, t=off))
        rng = np.random.default_rng(3 + k)
        for _ in range(2):
            pw, ph = rng.uniform(0.04, 0.07), rng.uniform(0.035, 0.06)
            pos = np.array([rng.uniform(-0.02, 0.02), rng.uniform(-0.07, 0.07), t / 2])
            prints.append(mk.ellipsoid(pw / 2, ph / 2, 0.0026, 18, 8, mat=P + "MapGreen", n=2.6).transform(t=pos)
                          .transform(R=R, t=off))
        for j in range(5):                         # stippelpad
            yy = -0.08 + 0.04 * j + 0.012 * k
            xx = 0.025 * math.sin(j * 1.3 + k)
            prints.append(mk.ellipsoid(0.007, 0.004, 0.0025, 8, 4, mat=P + "MapPath")
                          .transform(t=(xx, yy, t / 2 + 0.004)).transform(R=R, t=off))
    # locatiepin en stationnetje
    R2 = mk.rot_y(14 * DEG)
    off2 = np.array([w * math.cos(14 * DEG), 0, 0])
    pin = mk.merge(mk.ellipsoid(0.014, 0.014, 0.008, 12, 6, mat=P + "MapPin").transform(t=(0, 0.012, 0)),
                   mk.capsule_between((0, -0.010, 0), (0, 0.006, 0), 0.002, 0.009, 10, 3, mat=P + "MapPin"))
    prints.append(pin.transform(t=(0.020, 0.060, t / 2 + 0.008)).transform(R=R2, t=off2))
    station = mk.merge(mk.ellipsoid(0.021, 0.013, 0.0030, 14, 6, mat=P + "MapIcon", n=3.0),
                       mk.ellipsoid(0.025, 0.006, 0.0034, 14, 6, mat=P + "MapIcon", n=3.0).transform(t=(0, 0.018, 0)))
    prints.append(station.transform(t=(-0.012, -0.050, t / 2 + 0.002)).transform(R=mk.rot_y(-14 * DEG),
                                                                                  t=(0, 0, 0.012)))
    return mk.merge(*panels, *prints)


def build_ticket():
    t = 0.006
    card = mk.rounded_box(0.125, 0.068, t, 0.0028, 2, mat=P + "Ticket")
    scale = 1.18
    # inkt als lage koepeltjes half in het kaartje: geen plat vlakje vlak boven het papier (z-fighting)
    def dab(w, h, x, y, d=0.0020):
        return mk.ellipsoid(w / 2, h / 2, d, 14, 6, mat=P + "TicketInk", n=2.8).transform(t=(x, y, t / 2))
    ink = [dab(0.040, 0.020, 0.012, 0.004), dab(0.018, 0.026, -0.012, 0.008), dab(0.006, 0.012, -0.006, 0.026)]
    for x in (-0.014, 0.004, 0.024):
        ink.append(dab(0.012, 0.012, x, -0.012))
    for k in range(6):                                   # perforatie
        ink.append(mk.ellipsoid(0.0028, 0.0028, 0.0018, 6, 3, mat=P + "TicketInk").transform(
            t=(-0.040, -0.026 + 0.0105 * k, t / 2)))
    return mk.merge(card, *ink).transform(s=(scale, scale, 1.0))


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Boot"), (ck.SHOE_Y, 0.195, P + "Skin"), (0.195, 0.300, P + "Shorts"),
                     (0.300, ck.Y_COLLAR, P + "Shirt"), (ck.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Shirt", build_shirt())
    ck.add_detail(sc, "Shorts", build_shorts())
    ck.add_detail(sc, "Backpack", build_backpack(), ao_group="body")
    ck.add_face(sc, P)
    sc.add(mk.Part("Hat", build_hat(), (0.0, 0.905 - bc.HIP_Y, 0.0), "Body",
                   mk.rot_x(-11 * DEG) @ mk.rot_z(-4 * DEG), ao_group="body", gradient=0.0))
    ck.add_arms(sc, build_arm, {"L": ck.arm_pose(26, 52, -20, side=1), "R": ck.arm_pose(44, 10, side=-1)})
    hl = sc.world_matrix(sc.get("Hand_L"))[:3, 3]
    ck.attach(sc, "Prop_Map", build_map(), "Hand_L", hl + np.array([0.010, 0.085, 0.040]),
              mk.rot_y(-24 * DEG) @ mk.rot_x(-8 * DEG) @ mk.rot_z(-6 * DEG))
    hr = sc.world_matrix(sc.get("Hand_R"))[:3, 3]
    ck.attach(sc, "Prop_Ticket", build_ticket(), "Hand_R", hr + np.array([-0.035, -0.035, 0.050]),
              mk.rot_y(18 * DEG) @ mk.rot_z(28 * DEG) @ mk.rot_x(-10 * DEG))
    ck.add_leg_anchors(sc)
    return sc


def main():
    ck.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
