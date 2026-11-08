"""Oudere passagier ('oma') — personagevariant op het conducteur-basismodel.

Referentie: Docs/References/Characters/elderly-passenger-concept.png
Kenmerken (silhouet eerst, zie ART_STYLE_GUIDE 'Variatie in personages'):
  * grote wolk van grijs krullend haar rond het hoofd, pruimkleurige baret met bloem
  * rond paars brilletje, dikke crème sjaal met franjepunt
  * pruimkleurige jas met uitlopende zoom, twee grote knopen, klepzakken
  * crème draagtas met paars treintje, in de linkerhand
Kleuren: ramps coat_plum, beret_plum, hair_grey, scarf_cream, boot_brown (palette.json, 'Reizigers & crew').

Uitvoer: Assets/Art/Models/Characters/ElderlyPassenger/ElderlyPassenger.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/ElderlyPassenger/build_elderly_passenger.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "ElderlyPassenger"
P = "M_ElderlyPassenger_"


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Coat", "coat_plum", 0.0, 0.22, paint=1.2)
    ck.material(sc, P + "CoatDark", "coat_plum", -0.25, 0.22)
    ck.material(sc, P + "Trousers", "coat_plum", -0.40, 0.22)
    ck.material(sc, P + "Button", "haze_purple", 0.30, 0.30)
    ck.material(sc, P + "Shoe", "boot_brown", 0.0, 0.25)
    ck.material(sc, P + "Hair", "hair_grey", 0.05, 0.25, rim=0.14)
    ck.material(sc, P + "Beret", "beret_plum", 0.0, 0.22)
    ck.material(sc, P + "BeretDark", "beret_plum", -0.30, 0.22)
    ck.material(sc, P + "Glasses", "beret_plum", -0.15, 0.30)
    ck.material(sc, P + "Scarf", "scarf_cream", 0.0, 0.22, paint=1.1)
    ck.material(sc, P + "Flower", "parchment", 0.35, 0.25)
    ck.material(sc, P + "FlowerHeart", "accent_yellow", 0.10, 0.30)
    ck.material(sc, P + "Bag", "parchment", 0.15, 0.22)
    ck.material(sc, P + "BagPrint", "beret_plum", -0.10, 0.22)


# --------------------------------------------------------------------- kleding
def build_coat():
    parts = []
    # uitlopende jaszoom over de bovenbenen (romp-doorsnede, iets wijder)
    parts.append(ck.body_band(1.0, [(0.99, 0.345), (1.04, 0.31), (1.10, 0.268), (1.135, 0.244),
                                    (1.13, 0.226), (1.08, 0.220), (0.99, 0.245)], P + "Coat"))
    # knoopsluiting (smalle donkere strook) en twee grote knopen
    parts.append(ck.front_plate([(-0.013, 0.50), (0.013, 0.50), (0.013, 0.27), (-0.013, 0.27)], 0.006,
                                P + "CoatDark", lift=0.001, chamfer=0.003))
    for y, r in ((0.445, 0.030), (0.355, 0.030)):
        z, nrm = bc.body_surface(0.0, y)
        b = mk.ellipsoid(r, r, r * 0.45, 20, 10, mat=P + "Button", n=2.2)
        parts.append(b.transform(R=bc.face_frame(nrm), t=(0, y, z + 0.008)))
    # klepzakken, licht schuin naar buiten
    for side in (1, -1):
        flap = ck.plate([(-0.060, -0.018), (0.060, -0.018), (0.056, 0.018), (-0.056, 0.018)], 0.011,
                        P + "CoatDark", chamfer=0.004)
        parts.append(ck.wrap(flap.transform(R=mk.rot_z(-side * 4 * DEG)), side * 40 * DEG, 0.325, 0.001))
    # revers die onder de sjaal uitkomen
    for side in (1, -1):
        lap = [(side * 0.045, 0.505), (side * 0.115, 0.500), (side * 0.070, 0.452), (side * 0.020, 0.470)]
        parts.append(ck.front_plate(lap if side > 0 else lap[::-1], 0.008, P + "CoatDark", lift=0.002))
    return mk.merge(*parts)


def build_scarf():
    loop = []
    for k in range(16):
        a = 2 * math.pi * k / 16
        loop.append((0.020 + 0.046 * math.cos(a), 0.548 + 0.056 * math.sin(a)))
    ring = ck.surface_lathe(loop, P + "Scarf", seg=56)
    # hangende punt aan de rechterkant (-X) van het lijf, met franje
    tail = ck.strap([(-22 * DEG, 0.515), (-27 * DEG, 0.45), (-30 * DEG, 0.375)], 0.100, 0.030, P + "Scarf",
                    lift=0.018, expo=6.0)
    fringe = []
    for k in range(5):
        th = (-30 + (k - 2) * 3.3) * DEG
        S, N = ck.surface(np.array([th, th]), np.array([0.378, 0.335]))
        pts = S + N * 0.034
        fringe.append(ck.tube(pts, 0.0085, P + "Scarf", n=6, seg=8))
    return mk.merge(ring, tail, *fringe)


def build_hair():
    puffs = []
    rows = (  # y, straal, lift, hoeken (graden): grote, zachte krullen die het gezicht omlijsten
        (0.680, 0.090, 0.040, (76, 112, 148)),
        (0.790, 0.108, 0.052, (66, 100, 134, 166)),
        (0.900, 0.100, 0.036, (60, 94, 128, 162)),
    )
    for y, r, lift, angles in rows:
        th = [a * DEG for a in angles] + [-a * DEG for a in angles] + [math.pi]
        S, N = ck.surface(np.array(th), np.full(len(th), y))
        jitter = np.random.default_rng(int(y * 1000))
        for s, n in zip(S, N):
            puffs.append((s + n * lift + jitter.normal(0, 0.008, 3), r * jitter.uniform(0.92, 1.08)))
    # kruin onder de baret + een zachte pony van drie krullen boven de ogen
    th = np.array([62, -62, 105, -105, 145, -145, 180]) * DEG
    S, N = ck.surface(th, np.full(len(th), 0.975))
    for s, n in zip(S, N):
        puffs.append((s + n * 0.026, 0.088))
    for a, r in ((0, 0.066), (30, 0.064), (-30, 0.064)):
        S, N = ck.surface(np.array([a * DEG]), np.array([1.000]))
        puffs.append((S[0] + N[0] * 0.020 + np.array([0, 0.008, 0]), r))
    puffs.append((np.array([0.0, 1.035, -0.02]), 0.11))
    return ck.puffs(puffs, P + "Hair")


def build_beret():
    """Lokaal: oorsprong = onderkant baret, midden boven op het hoofd."""
    prof = [(0.0, 0.0), (0.55, -0.004), (0.84, 0.006), (0.98, 0.030), (1.07, 0.064), (1.085, 0.100),
            (1.02, 0.132), (0.84, 0.154), (0.55, 0.166), (0.25, 0.171), (0.0, 0.172)]
    body = mk.lathe(prof, 44, 0.300, 0.300, 2.0, mat=P + "Beret")
    rim = mk.torus(0.262, 0.024, 44, 10, mat=P + "BeretDark").transform(t=(0, 0.014, 0))
    stem = mk.capsule_between((0, 0.160, 0), (0.014, 0.205, 0.004), 0.019, 0.014, 12, 4, mat=P + "Beret")
    # bloem op de voorkant, rechts (-X), schuin naar buiten
    phi = -38 * DEG
    c = np.array([0.300 * 1.07 * math.sin(phi), 0.092, 0.300 * 1.07 * math.cos(phi)])
    R = ck.frame_at(np.array([math.sin(phi), 0.55, math.cos(phi)]))
    petals = []
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.3
        petals.append(mk.ellipsoid(0.044, 0.028, 0.011, 16, 8, mat=P + "Flower")
                      .transform(R=mk.rot_z(a), t=(0.035 * math.cos(a), 0.035 * math.sin(a), 0)))
    heart = mk.ellipsoid(0.024, 0.024, 0.015, 14, 7, mat=P + "FlowerHeart").transform(t=(0, 0, 0.008))
    flower = mk.merge(*petals, heart).transform(R=R, t=c)
    return mk.merge(body, rim, stem, flower)


def build_glasses():
    parts = []
    inner, outer = {}, {}
    for side in (1, -1):
        pos, Rf = ck.eye_frame(side)
        loop = [(0.084 * math.cos(a), 0.070 * math.sin(a), 0.0) for a in np.linspace(0, 2 * math.pi, 41)]
        ring = ck.tube(loop, 0.0115, P + "Glasses", smooth=False, seg=8)
        c_local = np.array([0.0, -0.022, 0.064])
        parts.append(ring.transform(R=Rf, t=pos + Rf @ c_local))
        inner[side] = pos + Rf @ (c_local + np.array([-side * 0.084, 0.004, -0.004]))
        outer[side] = pos + Rf @ (c_local + np.array([side * 0.084, 0.010, -0.008]))
    zb, _ = bc.body_surface(0.0, 0.83)
    parts.append(ck.tube([inner[1], np.array([0.0, 0.832, zb + 0.022]), inner[-1]], 0.0095, P + "Glasses", n=14))
    for side in (1, -1):
        S, N = ck.surface(np.array([side * 62 * DEG, side * 88 * DEG]), np.array([0.825, 0.83]))
        parts.append(ck.tube([outer[side], S[0] + N[0] * 0.012, S[1] + N[1] * 0.006], 0.0075, P + "Glasses", n=12))
    return mk.merge(*parts)


def build_bag():
    """Lokaal: oorsprong = waar de hand de hengsels vasthoudt; tas hangt langs -Y, print op +Z."""
    body = mk.rounded_box(0.250, 0.215, 0.085, 0.034, 3, mat=P + "Bag").transform(t=(0, -0.180, 0))
    handles = []
    for z in (0.021, -0.021):
        pts = [(-0.070, -0.076, z), (-0.052, -0.030, z * 0.7), (0.0, 0.004, z * 0.3), (0.052, -0.030, z * 0.7),
               (0.070, -0.076, z)]
        handles.append(ck.tube(pts, 0.0095, P + "BagPrint", n=20))
    zf = 0.042
    sy, sxy = -0.020, 1.25
    icon = [mk.rounded_box(0.112, 0.056, 0.010, 0.012, 2, mat=P + "BagPrint").transform(t=(0, -0.152, 0)),
            mk.rounded_box(0.122, 0.014, 0.010, 0.006, 2, mat=P + "BagPrint").transform(t=(0, -0.118, 0))]
    for x in (-0.032, 0.0, 0.032):
        icon.append(mk.rounded_box(0.020, 0.018, 0.006, 0.004, 1, mat=P + "Bag").transform(t=(x, -0.145, 0.005)))
    for x in (-0.030, 0.030):
        icon.append(mk.cylinder(0.012, 0.010, 14, 0.002, mat=P + "BagPrint", axis="z").transform(t=(x, -0.185, 0)))
    icon = mk.merge(*icon).transform(t=(0, 0.152, 0)).transform(s=(sxy, sxy, 1.0), t=(0, -0.152 * sxy + sy, zf))
    return mk.merge(body, *handles, icon)


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Shoe"), (ck.SHOE_Y, 0.27, P + "Trousers"),
                     (0.27, ck.Y_COLLAR, P + "Coat"), (ck.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Coat", build_coat())
    ck.add_detail(sc, "Scarf", build_scarf())
    ck.add_detail(sc, "Hair", build_hair(), gradient=0.0)
    ck.add_face(sc, P)
    ck.add_detail(sc, "Glasses", build_glasses(), ao_group="glasses", gradient=0.0)
    sc.add(mk.Part("Beret", build_beret(), (0.0, 1.035 - bc.HIP_Y, -0.012), "Body",
                   mk.rot_z(-11 * DEG) @ mk.rot_x(-9 * DEG), ao_group="body", gradient=0.0))
    ck.add_arms(sc, ck.build_arm(P + "Coat", P + "Skin"),
                {"L": ck.arm_pose(22, 20, side=1), "R": ck.arm_pose(42, 0, side=-1)})
    hand = sc.world_matrix(sc.get("Hand_L"))[:3, 3]
    ck.attach(sc, "Prop_Bag", build_bag(), "Hand_L", hand + np.array([0.0, 0.030, 0.012]), mk.rot_y(14 * DEG))
    ck.add_leg_anchors(sc)
    return sc


def main():
    ck.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
