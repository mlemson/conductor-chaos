"""Ondeugend kind (passagier) — personagevariant op het conducteur-basismodel, op 80 % schaal (issue #204).

Referentie: Docs/References/Characters/child-passenger-concept.png
Kenmerken (silhouet eerst):
  * achterstevoren pet: marine kroon met crème voorpanelen (nu achter), rode klep naar achteren,
    rood verstelbandje met gaatjes boven het voorhoofd, bruine pony eronder
  * ondeugende blik: schuine oogleden + brede grijns, pleister op de rechterwang
  * rode hoodie met gele capuchon (om de hals) en gele mouwen, buidelzak, crème koordjes
  * marine korte broek, crème sokken, bruine schoenen
  * marine rugzak met gele ster en een groene dino-knuffel die eruit kijkt; papieren vliegtuigje in de hand
Kleuren: ramps hoodie_red, accent_yellow, uniform_navy, parchment, hair_brown, scarf_cream, dino_green (palette.json).

Uitvoer: Assets/Art/Models/Characters/ChildPassenger/ChildPassenger.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/ChildPassenger/build_child_passenger.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "ChildPassenger"
P = "M_ChildPassenger_"
SCALE = 0.80
SLEEVE_CUT = -0.150
SOCK_Y = 0.118
SHORTS_Y = 0.300


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Hoodie", "hoodie_red", 0.0, 0.22, paint=1.3)
    ck.material(sc, P + "HoodieLight", "hoodie_red", 0.30, 0.22)
    ck.material(sc, P + "HoodieDark", "hoodie_red", -0.25, 0.22)
    ck.material(sc, P + "Hood", "accent_yellow", 0.0, 0.22, paint=1.2)
    ck.material(sc, P + "String", "parchment", 0.40, 0.25)
    ck.material(sc, P + "Shorts", "uniform_navy", 0.05, 0.22, paint=1.2)
    ck.material(sc, P + "Sock", "parchment", 0.15, 0.25)
    ck.material(sc, P + "Shoe", "boot_brown", 0.0, 0.25)
    ck.material(sc, P + "Cap", "uniform_navy", 0.05, 0.22)
    ck.material(sc, P + "CapCream", "parchment", 0.30, 0.22)
    ck.material(sc, P + "CapRed", "hoodie_red", -0.05, 0.22)
    ck.material(sc, P + "Hole", "ink", 0.10, 0.25)
    ck.material(sc, P + "Hair", "hair_brown", 0.15, 0.25, rim=0.14)
    ck.material(sc, P + "Bandaid", "scarf_cream", 0.0, 0.25)
    ck.material(sc, P + "BandaidDot", "scarf_cream", -0.45, 0.25)
    ck.material(sc, P + "Backpack", "uniform_navy", -0.08, 0.22, paint=1.2)
    ck.material(sc, P + "BackpackDark", "uniform_navy", -0.30, 0.22)
    ck.material(sc, P + "Star", "accent_yellow", 0.10, 0.25)
    ck.material(sc, P + "Dino", "dino_green", 0.0, 0.25, paint=1.1)
    ck.material(sc, P + "DinoLight", "dino_green", 0.35, 0.25)
    ck.material(sc, P + "DinoBelly", "scarf_cream", 0.10, 0.25)
    ck.material(sc, P + "DinoEye", "ink", 0.0, 0.30)
    ck.material(sc, P + "Paper", "wool", 0.35, 0.25)
    ck.material(sc, P + "PaperFold", "wool", 0.0, 0.25)


# ---------------------------------------------------------------------- hoodie
def build_hoodie():
    parts = []
    # gele capuchon als dikke rol om de hals, aan de voorkant iets lager
    # platte, brede kraag (omgeslagen capuchon): ovale doorsnede, voor smal en laag, achter breed en hoog
    loop = [(0.012 + 0.022 * math.cos(a), 0.540 + 0.032 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 14,
                                                                                            endpoint=False)]
    hood = ck.surface_lathe(loop, P + "Hood", seg=48)
    V = hood.V.copy()
    front = np.clip(V[:, 2] / bc.BODY_R, 0, 1) ** 2
    V[:, 1] -= 0.016 * front
    hood.V = V
    parts.append(hood)
    # de capuchon zelf ligt plat op de rug, onder de rugzak uit
    back = ck.plate([(-0.120, -0.060), (0.120, -0.060), (0.135, 0.020), (0.0, 0.050), (-0.135, 0.020)], 0.022,
                    P + "Hood", chamfer=0.008)
    parts.append(ck.wrap(back, math.pi, 0.500, 0.004))
    # koordjes met crème uiteinden
    for s in (1, -1):
        a, _ = ck.front_point(s * 0.040, 0.520, 0.030)
        b, _ = ck.front_point(s * 0.046, 0.455, 0.016)
        c, _ = ck.front_point(s * 0.050, 0.415, 0.014)
        parts.append(ck.tube([a, b, c], 0.0075, P + "String", n=12, seg=8))
        parts.append(mk.ellipsoid(0.016, 0.019, 0.016, 12, 6, mat=P + "String").transform(t=c + np.array([0, -0.014, 0.004])))
    # buidelzak (lichter rood) en een donkerdere boord onderaan
    pocket = [(-0.120, 0.322), (0.120, 0.322), (0.086, 0.405), (-0.086, 0.405)]
    parts.append(ck.front_plate(pocket, 0.010, P + "HoodieLight", lift=0.004, chamfer=0.004))
    rib = [(0.0, SHORTS_Y - 0.006), (0.012, SHORTS_Y - 0.002), (0.014, SHORTS_Y + 0.012), (0.012, SHORTS_Y + 0.026),
           (0.0, SHORTS_Y + 0.030)]
    parts.append(ck.surface_lathe(rib, P + "HoodieDark", seg=48))
    return mk.merge(*parts)


def build_shorts():
    parts = []
    for s in (1, -1):
        cuff = [(0.0, 0.158), (0.012, 0.152), (0.015, 0.140), (0.011, 0.128), (0.0, 0.131)]
        parts.append(ck.surface_lathe(cuff, P + "Shorts", seg=36, cx=s * ck.LEG_X, rmax=0.2))
    return mk.merge(*parts)


def build_arm(side):
    arm = ck.build_arm(P + "Hood", P + "Skin", cut=SLEEVE_CUT, sleeve_bulge=1.08)
    cuff = mk.torus(0.082, 0.012, 32, 8, mat=P + "HoodieDark", a=1.0, b=0.88).transform(t=(0, SLEEVE_CUT + 0.008, 0))
    return mk.merge(arm, cuff)


# ------------------------------------------------------------------- pet & haar
def build_cap():
    """Achterstevoren pet. Lokaal: oorsprong = onderkant kroon, midden hoofd; klep wijst naar -Z (achter)."""
    a, b = 0.300, 0.286
    prof = [(0.0, 0.0), (1.0, 0.0), (1.0, 0.04), (0.985, 0.10), (0.94, 0.150), (0.86, 0.195), (0.72, 0.228),
            (0.48, 0.246), (0.0, 0.252)]
    crown = mk.lathe(prof, 48, a, b, 2.2, mat=P + "Cap")
    # voorpanelen (nu achter): crème binnen +-55 graden rond -Z
    mats = []
    for f in crown.F:
        c = crown.V[list(f)].mean(0)
        phi = math.atan2(c[0], -c[2])
        mats.append(P + "CapCream" if abs(phi) < 55 * DEG and c[1] > 0.012 else P + "Cap")
    crown.M = mats
    button = mk.ellipsoid(0.030, 0.016, 0.030, 14, 6, mat=P + "Cap").transform(t=(0, 0.250, 0))
    # klep naar achteren
    V, F = [], []
    seg, sec = 28, 10
    for i in range(seg + 1):
        ang = -1.10 + 2.20 * i / seg
        inner = np.array([a * 0.98 * math.sin(ang), 0.010, -b * 0.98 * math.cos(ang)])
        reach = 0.165 * math.cos(ang) ** 0.8
        outer = inner + np.array([0.02 * math.sin(ang), -0.040 * math.cos(ang), -reach])
        for j in range(sec):
            th = 2 * math.pi * j / sec
            t = (1 - math.cos(th)) / 2
            p = inner * (1 - t) + outer * t
            p[1] += 0.010 * math.sin(th)
            V.append(p)
    for i in range(seg):
        for j in range(sec):
            k = (j + 1) % sec
            F.append((i * sec + j, (i + 1) * sec + j, (i + 1) * sec + k, i * sec + k))
    F.append(tuple(reversed(range(sec))))
    F.append(tuple(seg * sec + j for j in range(sec)))
    brim = mk.orient_outward(mk.Mesh(np.asarray(V), F, [P + "CapRed"] * len(F)))
    # verstelbandje met gaatjes boven het voorhoofd (+Z), in een opening van de kroon
    strap = mk.rounded_box(0.150, 0.034, 0.016, 0.008, 2, mat=P + "CapRed")
    holes = [mk.cylinder(0.007, 0.010, 10, 0.0, mat=P + "Hole", axis="z").transform(t=(x, 0, 0.006))
             for x in (-0.034, 0.0, 0.034)]
    snap = mk.merge(strap, *holes).transform(R=mk.rot_x(-12 * DEG), t=(0, 0.052, b * 1.0 + 0.010))
    return mk.merge(crown, button, brim, snap)


def build_hair():
    """Bruine pony in dikke lokken onder de pet en zijhaar, plus een plukje door de opening boven het bandje.
    Eén gladde massa per groep (blob), zodat het als haar leest en niet als losse balletjes."""
    spheres = []
    for th in range(-150, 181, 15):                   # basis: rand onder de pet rondom
        S, N = ck.surface(np.array([th * DEG]), np.array([0.905 if abs(th) > 60 else 0.925]))
        spheres.append((S[0] + N[0] * 0.012, 0.040))
    for th, y, r in ((0, 0.905, 0.038), (16, 0.900, 0.036), (-16, 0.902, 0.036), (34, 0.893, 0.034),
                     (-34, 0.895, 0.034), (52, 0.885, 0.032), (-52, 0.888, 0.032), (72, 0.860, 0.034),
                     (-72, 0.860, 0.034), (90, 0.830, 0.032), (-90, 0.830, 0.032)):
        S, N = ck.surface(np.array([th * DEG]), np.array([y]))
        spheres.append((S[0] + N[0] * 0.022, r))            # lokken die over de rand vallen
    hair = ck.blob(spheres, P + "Hair", spacing=0.0145, k=0.010)
    tuft = ck.blob([(np.array([0.012, 0.998, 0.252]), 0.030), (np.array([-0.018, 1.012, 0.240]), 0.026),
                    (np.array([0.030, 1.010, 0.238]), 0.022)], P + "Hair", spacing=0.008, k=0.008)
    return mk.merge(hair, tuft)


def build_bandaid():
    """Pleister schuin op de rechterwang (-X)."""
    pad = ck.plate([(-0.046, -0.016), (0.046, -0.016), (0.046, 0.016), (-0.046, 0.016)], 0.007, P + "Bandaid",
                   chamfer=0.003)
    mid = ck.plate([(-0.017, -0.012), (0.017, -0.012), (0.017, 0.012), (-0.017, 0.012)], 0.008, P + "Bandaid",
                   chamfer=0.0025).transform(t=(0, 0, 0.005))
    dots = [mk.ellipsoid(0.0035, 0.0035, 0.002, 8, 4, mat=P + "BandaidDot").transform(t=(x, y, 0.008))
            for x in (-0.033, 0.033) for y in (-0.006, 0.006)]
    return ck.place(mk.merge(pad, mid, *dots), -34 * DEG, 0.700, lift=0.002, spin=0.45)


# -------------------------------------------------------------------- rugzak
def star(r_out, r_in, depth, mat):
    pts = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5
        r = r_out if k % 2 == 0 else r_in
        pts.append((r * math.cos(a), r * math.sin(a)))
    m = mk.prism(pts, 0.0, depth, c=0.003, mat=mat)
    m.sharp = False
    m.FS = None
    return m


def build_dino():
    """Groene dino-knuffel (kop en schouders) die uit de rugzak kijkt. Lokaal: kijkt naar -Z (weg van de rug)."""
    head = mk.ellipsoid(0.072, 0.064, 0.068, 18, 10, mat=P + "Dino").transform(t=(0, 0.060, 0))
    snout = mk.ellipsoid(0.050, 0.038, 0.040, 14, 8, mat=P + "Dino").transform(t=(0, 0.044, -0.060))
    body = mk.ellipsoid(0.080, 0.060, 0.070, 16, 8, mat=P + "Dino").transform(t=(0, -0.020, 0.008))
    belly = mk.ellipsoid(0.052, 0.040, 0.020, 14, 6, mat=P + "DinoBelly").transform(t=(0, -0.016, -0.060))
    eyes = [mk.ellipsoid(0.010, 0.013, 0.007, 10, 5, mat=P + "DinoEye").transform(t=(s * 0.030, 0.082, -0.058))
            for s in (1, -1)]
    spikes = [mk.lathe([(0.0, 0.0), (0.020, 0.0), (0.0, 0.034)], 8, mat=P + "DinoLight").transform(
        R=mk.rot_x(a), t=(0, 0.060 + 0.066 * math.cos(a), 0.066 * math.sin(a))) for a in (-0.3, 0.25, 0.80)]
    return mk.merge(head, snout, body, belly, *eyes, *spikes)


def build_backpack():
    z0 = -0.300
    parts = [mk.rounded_box(0.36, 0.34, 0.15, 0.070, 3, mat=P + "Backpack").transform(t=(0, 0.420, z0)),
             mk.rounded_box(0.26, 0.15, 0.060, 0.030, 3, mat=P + "BackpackDark").transform(t=(0, 0.355, z0 - 0.085))]
    parts.append(star(0.060, 0.026, 0.014, P + "Star").transform(R=mk.rot_y(math.pi), t=(0, 0.360, z0 - 0.112)))
    parts.append(mk.torus(0.020, 0.006, 14, 6, mat=P + "BackpackDark").transform(R=mk.rot_x(math.pi / 2),
                                                                                 t=(0, 0.605, z0)))
    parts.append(build_dino().transform(R=mk.rot_x(-0.15), t=(0.040, 0.600, z0 - 0.010)))
    for s in (1, -1):
        ctrl = [(s * 150 * DEG, 0.50), (s * 110 * DEG, 0.555), (s * 70 * DEG, 0.548), (s * 42 * DEG, 0.505),
                (s * 34 * DEG, 0.420), (s * 33 * DEG, 0.330)]
        parts.append(ck.strap(ctrl, 0.046, 0.014, P + "BackpackDark", lift=0.034))
    return mk.merge(*parts)


def build_plane():
    """Papieren vliegtuigje: twee vleugels en een kiel, elk een dun gevouwen vlak. Neus langs +X."""
    t = 0.006
    L = 0.330
    wing = mk.prism([(0.0, 0.0), (-L, 0.012), (-L, 0.090)], 0.0, t, c=0.0, mat=P + "Paper")
    w1 = wing.transform(R=mk.rot_x(-math.pi / 2 - 0.22))          # vleugel naar -Z, iets omhoog geknikt
    w2 = w1.transform(s=(1, 1, -1))                                # spiegelbeeld naar +Z
    keel = mk.prism([(0.0, 0.0), (-L, -0.045), (-L, 0.0)], -t / 2, t / 2, c=0.0, mat=P + "PaperFold")
    out = mk.merge(w1, w2, keel)
    out.sharp = True
    return out


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Shoe"), (ck.SHOE_Y, SOCK_Y, P + "Sock"), (SOCK_Y, SHORTS_Y, P + "Shorts"),
                     (SHORTS_Y, bc.Y_COLLAR, P + "Hoodie"), (bc.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Hoodie", build_hoodie())
    ck.add_detail(sc, "Shorts", build_shorts())
    ck.add_detail(sc, "Backpack", build_backpack())
    ck.add_detail(sc, "Hair", build_hair(), gradient=0.0)
    ck.add_detail(sc, "Bandaid", build_bandaid(), gradient=0.0)
    ck.add_face(sc, P, extra_mouths=("Flat", "Hmm", "Smile", "Grin"), lids=True, lid_drop=0.40,
                default_mouth="Grin")
    sc.add(mk.Part("Cap", build_cap(), (0.0, 0.905 - bc.HIP_Y, 0.0), "Body",
                   mk.rot_x(-12 * DEG) @ mk.rot_z(6 * DEG) @ mk.rot_y(-14 * DEG), ao_group="body", gradient=0.0))
    ck.add_arms(sc, build_arm, {"L": ck.arm_pose(132, 26, 0, side=1), "R": ck.arm_pose(30, -8, side=-1)})
    hl = sc.world_matrix(sc.get("Hand_L"))[:3, 3]
    ck.attach(sc, "Prop_Plane", build_plane(), "Hand_L", hl + np.array([0.120, 0.120, 0.090]),
              mk.rot_y(-25 * DEG) @ mk.rot_z(25 * DEG) @ mk.rot_x(-60 * DEG))
    ck.add_leg_anchors(sc)
    ck.scale_scene(sc, SCALE)
    return sc


def main():
    ck.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
