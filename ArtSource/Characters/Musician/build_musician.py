"""Muzikant (reiziger, 5e type) — personagevariant op het conducteur-basismodel.

Kenmerken (silhouet eerst):
  * bruine slappe hoed (pork-pie met deuk) met gele band, donkere krullen eronder, klein sikje
  * crème overhemd met opgerolde mouwen, donkergroen gilet met knoopjes, rode halsdoek met knoop
  * marine broek, bruine schoenen
  * zwarte gitaarkoffer (gitaarvorm, gouden sloten, sticker met noot) rechtop aan de linkerhand
  * rechterhand vrij voor het treinkaartje (PassengerTicket hangt het daar in)
Kleuren: ramps wagon_brown, accent_yellow, hair_brown, parchment, chalk_green, title_red, master_navy, boot_brown,
ui_bg, gold_trim (palette.json).

Uitvoer: Assets/Art/Models/Characters/Musician/Musician.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/Musician/build_musician.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "Musician"
P = "M_Musician_"
HEM_Y = 0.300
OPEN_X = 0.060           # halve breedte van de gilet-opening onderaan
SLEEVE_END = -0.100      # opgerolde mouw


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Shirt", "parchment", 0.30, 0.25, paint=1.1)
    ck.material(sc, P + "ShirtDark", "parchment", 0.0, 0.25)
    ck.material(sc, P + "Vest", "chalk_green", 0.10, 0.22, paint=1.3)
    ck.material(sc, P + "VestDark", "chalk_green", -0.25, 0.22)
    ck.material(sc, P + "Button", "gold_trim", 0.0, 0.35)
    ck.material(sc, P + "Trousers", "master_navy", 0.10, 0.22, paint=1.2)
    ck.material(sc, P + "Shoe", "boot_brown", 0.0, 0.25)
    ck.material(sc, P + "Scarf", "title_red", 0.0, 0.22, paint=1.1)
    ck.material(sc, P + "ScarfDot", "parchment", 0.35, 0.25)
    ck.material(sc, P + "Hair", "hair_brown", -0.05, 0.25, rim=0.14)
    ck.material(sc, P + "Hat", "wagon_brown", 0.0, 0.22, paint=1.2)
    ck.material(sc, P + "HatDark", "wagon_brown", -0.30, 0.22)
    ck.material(sc, P + "HatBand", "accent_yellow", 0.0, 0.22)
    ck.material(sc, P + "Case", "ui_bg", 0.15, 0.30, paint=1.1)
    ck.material(sc, P + "CaseDark", "ui_bg", -0.20, 0.30)
    ck.material(sc, P + "Latch", "gold_trim", 0.05, 0.40)
    ck.material(sc, P + "Sticker", "accent_yellow", 0.10, 0.25)
    ck.material(sc, P + "StickerInk", "ink", 0.10, 0.25)
    ck.material(sc, P + "StickerRed", "title_red", 0.05, 0.25)


# ---------------------------------------------------------------------- kleding
def build_shirt_and_scarf():
    parts = []
    shirt = [(-OPEN_X - 0.006, HEM_Y + 0.004), (OPEN_X + 0.006, HEM_Y + 0.004), (0.098, 0.470),
             (0.070, bc.Y_COLLAR + 0.003), (-0.070, bc.Y_COLLAR + 0.003), (-0.098, 0.470)]
    parts.append(ck.front_plate(shirt, 0.006, P + "Shirt", lift=0.004, chamfer=0.002))
    for y in (0.445, 0.395, 0.345):                               # knoopjes van het overhemd
        z, n = bc.body_surface(0.0, y)
        parts.append(mk.ellipsoid(0.010, 0.010, 0.006, 12, 6, mat=P + "ShirtDark").transform(
            R=bc.face_frame(n), t=(0, y, z + 0.011)))
    # halsdoek: rol om de hals, knoop links voor, twee punten die naar beneden hangen
    loop = [(0.014 + 0.020 * math.cos(a), 0.538 + 0.024 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 12,
                                                                                            endpoint=False)]
    parts.append(ck.surface_lathe(loop, P + "Scarf", seg=48))
    S, N = ck.surface(np.array([22 * DEG]), np.array([0.522]))
    knot = mk.ellipsoid(0.034, 0.030, 0.022, 16, 8, mat=P + "Scarf").transform(R=ck.frame_at(N[0]),
                                                                               t=S[0] + N[0] * 0.034)
    parts.append(knot)
    for ctrl in ([(24 * DEG, 0.505), (28 * DEG, 0.465), (31 * DEG, 0.430)],
                 [(18 * DEG, 0.505), (13 * DEG, 0.470), (9 * DEG, 0.445)]):
        parts.append(ck.strap(ctrl, 0.046, 0.012, P + "Scarf", lift=0.030, expo=6.0))
    for th, y in ((40 * DEG, 0.548), (-30 * DEG, 0.552), (-80 * DEG, 0.546), (120 * DEG, 0.550), (180 * DEG, 0.548),
                  (75 * DEG, 0.536), (-140 * DEG, 0.540)):
        Sd, Nd = ck.surface(np.array([th]), np.array([y]))
        parts.append(mk.ellipsoid(0.009, 0.009, 0.004, 10, 5, mat=P + "ScarfDot").transform(
            R=ck.frame_at(Nd[0]), t=Sd[0] + Nd[0] * 0.0355))
    return mk.merge(*parts)


def build_vest():
    """Gilet: het lijf is gilet-groen tot de kraag; revers-loze voorpanden met knoopjes en een zoompje."""
    parts = []
    for s in (1, -1):
        panel = [(s * 0.070, bc.Y_COLLAR + 0.004), (s * 0.120, 0.505), (s * (OPEN_X + 0.040), HEM_Y),
                 (s * (OPEN_X - 0.002), HEM_Y + 0.004), (s * 0.090, 0.470)]
        parts.append(ck.front_plate(panel if s < 0 else panel[::-1], 0.010, P + "Vest", lift=0.008, chamfer=0.003))
        flap = ck.plate([(-0.040, -0.010), (0.040, -0.010), (0.038, 0.010), (-0.038, 0.010)], 0.009,
                        P + "VestDark", chamfer=0.003)
        parts.append(ck.wrap(flap, s * 52 * DEG, 0.360, 0.002))
    for y in (0.430, 0.385, 0.340):
        x = -(OPEN_X + 0.022)
        z, n = bc.body_surface(x, y)
        parts.append(mk.ellipsoid(0.012, 0.012, 0.007, 12, 6, mat=P + "Button").transform(
            R=bc.face_frame(n), t=(x, y, z + 0.022)))
    th0 = math.asin((OPEN_X + 0.040) / bc.BODY_R)
    hem = [(0.0, HEM_Y + 0.026), (0.010, HEM_Y + 0.010), (0.012, HEM_Y - 0.004), (0.005, HEM_Y - 0.010),
           (0.0, HEM_Y)]
    parts.append(ck.surface_lathe(hem, P + "VestDark", seg=48, theta0=th0, theta1=2 * math.pi - th0))
    return mk.merge(*parts)


def build_arm(side):
    arm = ck.build_arm(P + "Shirt", P + "Skin", cut=SLEEVE_END, sleeve_bulge=1.10)
    roll = mk.torus(0.084, 0.016, 32, 8, mat=P + "ShirtDark", a=1.0, b=0.88).transform(t=(0, SLEEVE_END + 0.010, 0))
    return mk.merge(arm, roll)


# ------------------------------------------------------------------ hoofd
def build_hair():
    """Donkere krullen onder de hoedrand (zijkant en achter) en een klein sikje onder de mond."""
    sph = []
    for y, r, lift, angles in ((0.770, 0.044, 0.012, (84, 106, 128, 150, 172)),
                               (0.835, 0.050, 0.018, (78, 98, 118, 138, 158, 178)),
                               (0.880, 0.042, 0.010, (88, 110, 132, 154, 176))):
        th = sorted({a * DEG for a in angles} | {-a * DEG for a in angles})
        S, N = ck.surface(np.array(th), np.full(len(th), y))
        for s_, n in zip(S, N):
            sph.append((s_ + n * lift, r))
    curls = ck.tufts(sph, P + "Hair", seg=12, rings=7, seed=21)
    z, n = bc.body_surface(0.0, bc.MOUTH_Y - 0.062)
    goatee = ck.tufts([(np.array([0.0, bc.MOUTH_Y - 0.060, z]) + n * 0.010, 0.022),
                       (np.array([0.0, bc.MOUTH_Y - 0.082, z - 0.004]) + n * 0.010, 0.016)], P + "Hair",
                      seg=10, rings=6, seed=4)
    return mk.merge(curls, goatee)


def build_hat():
    """Slappe pork-pie: lage kroon met ronde deuk, brede band, rand achter iets omhoog.
    Lokaal: oorsprong = onderkant band, midden van het hoofd."""
    a, b = 0.296, 0.276
    prof = [(0.0, 0.130), (0.55, 0.128), (0.74, 0.140), (0.86, 0.150), (0.96, 0.140), (1.0, 0.115), (1.0, 0.0),
            (0.0, 0.0)]
    crown = mk.lathe(prof[::-1], 44, a, b, 2.3, mat=P + "Hat")
    band = mk.lathe([(0.0, 0.006), (1.014, 0.006), (1.024, 0.014), (1.024, 0.062), (1.014, 0.070), (0.0, 0.070)],
                    44, a, b, 2.3, mat=P + "HatBand")
    brim = mk.lathe([(0.97, 0.018), (1.24, 0.004), (1.40, 0.000), (1.47, 0.012), (1.44, 0.026), (1.24, 0.022),
                     (0.97, 0.036)], 56, a, b, 2.3, closed=True, mat=P + "HatDark")
    V = brim.V.copy()                                       # voor iets omlaag, achter iets omhoog
    reach = np.clip((np.hypot(V[:, 0] / a, V[:, 2] / b) - 1.0) / 0.45, 0, 1)
    V[:, 1] += reach * (-0.030 * np.clip(V[:, 2] / b, 0, 1) + 0.045 * np.clip(-V[:, 2] / b, 0, 1))
    brim.V = V
    # klein veertje achter de band (rechts, -X)
    phi = -118 * DEG
    s_, c_ = math.sin(phi), math.cos(phi)
    root = np.array([a * 1.03 * np.sign(s_) * abs(s_) ** (2 / 2.3), 0.040, b * 1.03 * np.sign(c_) * abs(c_) ** (2 / 2.3)])
    feather = ck.tube([root, root + np.array([-0.020, 0.060, -0.030]), root + np.array([-0.030, 0.120, -0.070])],
                      0.012, P + "HatBand", n=14, r_end=0.004)
    return mk.merge(crown, band, brim, feather)


# ---------------------------------------------------------------- gitaarkoffer
def case_outline(n=120):
    """Gitaarvorm (u = breedte, v = lengte vanaf de onderkant), als ster-vormige omtrek rond de taille."""
    circles = [((0.0, 0.140), 0.140), ((0.0, 0.365), 0.112)]
    neck = ((0.0, 0.40), (0.0, 0.700), 0.050)

    def sdf(u, v):
        d = [np.hypot(u - c[0], v - c[1]) - r for c, r in circles]
        (au, av), (bu, bv), r = neck
        t = np.clip((v - av) / (bv - av), 0, 1)
        d.append(np.hypot(u - au, v - (av + t * (bv - av))) - r)
        d = np.stack(d)
        k = 0.035
        m = d.min(0)
        return m - k * np.log(np.exp(-(d - m) / k).sum(0))
    c = np.array([0.0, 0.255])
    pts = []
    for ang in np.linspace(0, 2 * math.pi, n, endpoint=False):
        dvec = np.array([math.sin(ang), math.cos(ang)])
        rs = np.linspace(0.0, 0.6, 1200)
        P_ = c[None] + rs[:, None] * dvec[None]
        inside = sdf(P_[:, 0], P_[:, 1]) < 0
        out_i = np.argmax(~inside)
        pts.append(tuple(P_[out_i]))
    return pts


def build_case(grip_y, side=1):
    """Lokaal: oorsprong = de handgreep (waar de hand vastpakt), koffer staat rechtop op de grond langs +X (weg
    van het lijf), brede kant naar voren. grip_y = hoogte van de greep boven de grond."""
    T = 0.120
    poly = case_outline()
    W = 0.140                                              # halve breedte onderkant: greep op de binnenrand
    body = mk.prism([(u + W, v) for u, v in poly], -T / 2, T / 2, c=0.030, mat=P + "Case")
    body.sharp = False
    body.FS = None
    # naad rondom (iets uitstekend) en een donkere bodemrand
    seam_pts = [(u * 1.0 + W + 0.0 * u, v, 0.0) for u, v in poly] + [(poly[0][0] + W, poly[0][1], 0.0)]
    seam = ck.tube(seam_pts, 0.010, P + "CaseDark", smooth=False, seg=8)
    parts = [body, seam]
    # gouden slotjes op de buitenrand
    for v in (0.110, 0.330, 0.600):
        u = max(pu for pu, pv in poly if abs(pv - v) < 0.03) + W
        parts.append(mk.rounded_box(0.026, 0.040, 0.050, 0.008, 1, mat=P + "Latch").transform(t=(u + 0.004, v, 0)))
    # greep op de binnenrand bij de taille
    vg = 0.262
    ug = min(pu for pu, pv in poly if abs(pv - vg) < 0.02) + W
    handle = ck.tube([(ug + 0.010, vg - 0.060, 0), (ug - 0.026, vg - 0.045, 0), (ug - 0.034, vg, 0),
                      (ug - 0.026, vg + 0.045, 0), (ug + 0.010, vg + 0.060, 0)], 0.014, P + "CaseDark", n=20)
    parts.append(handle)
    # sticker met een muzieknoot op de voorkant (+Z), half in het deksel gezakt
    zf = T / 2 - 0.004
    sticker = mk.ellipsoid(0.072, 0.072, 0.010, 28, 8, mat=P + "Sticker").transform(t=(W, 0.140, zf))
    note = [mk.ellipsoid(0.020, 0.015, 0.008, 14, 6, mat=P + "StickerInk").transform(
                R=mk.rot_z(0.4), t=(W - 0.016, 0.110, zf + 0.008)),
            mk.rounded_box(0.008, 0.070, 0.010, 0.003, 1, mat=P + "StickerInk").transform(t=(W - 0.002, 0.148, zf + 0.007)),
            mk.rounded_box(0.034, 0.010, 0.010, 0.003, 1, mat=P + "StickerInk").transform(
                R=mk.rot_z(-0.35), t=(W + 0.012, 0.177, zf + 0.007))]
    star = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5
        r = 0.032 if k % 2 == 0 else 0.014
        star.append((r * math.cos(a), r * math.sin(a)))
    st = mk.prism(star, 0.0, 0.016, c=0.002, mat=P + "StickerRed")
    st.sharp = False
    st.FS = None
    parts += [sticker, *note, st.transform(t=(W + 0.010, 0.440, T / 2 - 0.007))]
    case = mk.merge(*parts)
    case = case.transform(t=(-ug + 0.034, -vg, 0))                # greep in de oorsprong
    V = case.V.copy()
    V[:, 0] *= side
    case.V = V
    if side < 0:
        case.F = [tuple(reversed(f)) for f in case.F]
    return case, grip_y - vg                                     # (mesh, hoogte onderkant boven de grond)


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Shoe"), (ck.SHOE_Y, HEM_Y, P + "Trousers"),
                     (HEM_Y, bc.Y_COLLAR, P + "Vest"), (bc.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Shirt", build_shirt_and_scarf())
    ck.add_detail(sc, "Vest", build_vest())
    ck.add_detail(sc, "Hair", build_hair(), gradient=0.0)
    ck.add_face(sc, P, lids=False, default_mouth="Neutral")
    sc.add(mk.Part("Hat", build_hat(), (0.0, 0.905 - bc.HIP_Y, 0.0), "Body",
                   mk.rot_x(-9 * DEG) @ mk.rot_z(7 * DEG), ao_group="body", gradient=0.0))
    ck.add_arms(sc, build_arm, {"L": ck.arm_pose(20, 0, 0, side=1), "R": ck.arm_pose(30, 34, side=-1)})
    hl = sc.world_matrix(sc.get("Hand_L"))[:3, 3]
    grip = np.array([hl[0] + 0.010, hl[1], hl[2]])
    case, bottom = build_case(grip[1])
    print("gitaarkoffer: onderkant op", round(float(bottom), 3), "m")
    ck.attach(sc, "Prop_GuitarCase", case, "Hand_L", grip, mk.rot_y(-4 * DEG))
    ck.add_leg_anchors(sc)
    return sc


def main():
    ck.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
