"""Stationschef — personagevariant op het conducteur-basismodel (issue #204).

Referentie: Docs/References/Characters/station-master-concept.png
Kenmerken (silhouet eerst):
  * hoge bordeaux uniformpet met gouden band, zwarte klep en goudovaal embleem met treintje
  * witte snor met opkrullende punten, witte zijlokken en borstelige witte wenkbrauwen (losse knoop 'Brows')
  * paars-marine jas met gouden biezen langs revers, voorpanden, zoom, zakkleppen en manchetten
  * bordeaux gilet met gouden knopen, wit overhemd met puntkraag en bordeaux das
  * gouden zakhorloge aan een ketting, goudkleurige naambadge met treintje op de linkerborst
  * achterkant: halve ceintuur met twee gouden knopen
Kleuren: ramps master_navy, cap_maroon, mustache_cream, gold_trim, parchment, boot_brown (palette.json).

Uitvoer: Assets/Art/Models/Characters/StationMaster/StationMaster.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/StationMaster/build_station_master.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "StationMaster"
P = "M_StationMaster_"
HEM_Y = 0.285            # onderkant jas
OPEN_X = 0.118           # halve breedte van de jasopening (gilet zichtbaar)


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Jacket", "master_navy", 0.05, 0.22, paint=1.3)
    ck.material(sc, P + "JacketDark", "master_navy", -0.20, 0.22)
    ck.material(sc, P + "Trousers", "master_navy", -0.10, 0.22)
    ck.material(sc, P + "Gold", "gold_trim", 0.0, 0.35)
    ck.material(sc, P + "GoldLow", "gold_trim", -0.20, 0.30)
    ck.material(sc, P + "Vest", "cap_maroon", 0.05, 0.22, paint=1.2)
    ck.material(sc, P + "Tie", "cap_maroon", -0.15, 0.22)
    ck.material(sc, P + "Shirt", "parchment", 0.40, 0.25)
    ck.material(sc, P + "Cap", "cap_maroon", 0.05, 0.22, paint=1.2)
    ck.material(sc, P + "Visor", "ink", 0.10, 0.30)
    ck.material(sc, P + "Hair", "mustache_cream", 0.10, 0.25, rim=0.14)
    ck.material(sc, P + "Shoe", "boot_brown", -0.10, 0.25)
    ck.material(sc, P + "Ink", "cap_maroon", -0.45, 0.25)
    ck.material(sc, P + "WatchFace", "parchment", 0.45, 0.30)


# ---------------------------------------------------------------------- uniform
def build_vest_and_shirt():
    parts = []
    # wit overhemd in de V boven het gilet, met twee puntkraagjes
    v = np.array([ck.front_point(x, y, 0.008)[0] for x, y in ((-0.074, bc.Y_COLLAR + 0.002), (0.0, 0.430),
                                                              (0.074, bc.Y_COLLAR + 0.002))])
    parts.append(mk.Mesh(v, [(0, 1, 2)], [P + "Shirt"]))
    for s in (1, -1):
        tip = [(s * 0.008, 0.520), (s * 0.072, bc.Y_COLLAR + 0.004), (s * 0.050, 0.478)]
        parts.append(ck.front_plate(tip if s > 0 else tip[::-1], 0.008, P + "Shirt", lift=0.010, chamfer=0.003))
    # bordeaux das: knoop + blad dat onder het gilet verdwijnt
    zk, nk = bc.body_surface(0.0, 0.512)
    parts.append(mk.ellipsoid(0.022, 0.019, 0.012, 14, 7, mat=P + "Tie").transform(R=bc.face_frame(nk),
                                                                                    t=(0, 0.512, zk + 0.020)))
    parts.append(ck.front_plate([(-0.017, 0.500), (0.017, 0.500), (0.026, 0.440), (0.0, 0.418), (-0.026, 0.440)],
                                0.007, P + "Tie", lift=0.016, chamfer=0.002))
    # gilet: V-hals, twee punten onderaan
    vest = [(-OPEN_X - 0.004, 0.300), (-0.068, 0.268), (0.0, 0.292), (0.068, 0.268), (OPEN_X + 0.004, 0.300),
            (OPEN_X + 0.004, 0.505), (0.0, 0.425), (-OPEN_X - 0.004, 0.505)]
    parts.append(ck.front_plate(vest, 0.010, P + "Vest", lift=0.006, chamfer=0.004))
    for y in (0.395, 0.355, 0.315):
        z, n = bc.body_surface(0.0, y)
        parts.append(mk.ellipsoid(0.013, 0.013, 0.007, 14, 7, mat=P + "Gold").transform(R=bc.face_frame(n),
                                                                                         t=(0, y, z + 0.020)))
    return mk.merge(*parts)


def build_jacket():
    parts = []
    # revers met gouden bies (bovenop het gilet, naar buiten)
    for s in (1, -1):
        lap = [(s * OPEN_X, 0.505), (s * 0.072, bc.Y_COLLAR + 0.006), (s * 0.150, 0.522), (s * 0.198, 0.505),
               (s * 0.150, 0.430), (s * OPEN_X, 0.395)]
        parts.append(ck.front_plate(lap if s < 0 else lap[::-1], 0.010, P + "Jacket", lift=0.018, chamfer=0.004))
        edge = [(s * 0.074, bc.Y_COLLAR + 0.006), (s * 0.150, 0.522), (s * 0.198, 0.505), (s * 0.150, 0.430),
                (s * OPEN_X, 0.395)]
        parts.append(ck.front_tube(edge, 0.0065, P + "Gold", lift=0.024, n=30))
        # gouden bies langs het voorpand, van de revers tot de zoom
        parts.append(ck.front_tube([(s * OPEN_X, 0.398), (s * (OPEN_X + 0.002), 0.34), (s * (OPEN_X + 0.004), HEM_Y)],
                                   0.0065, P + "Gold", lift=0.016, n=16))
    # gouden zoombies rondom (behalve de opening) en een iets uitlopende zoom
    th0 = math.asin((OPEN_X + 0.004) / bc.BODY_R)
    hem = [(0.0, HEM_Y + 0.030), (0.010, HEM_Y + 0.012), (0.014, HEM_Y - 0.004), (0.006, HEM_Y - 0.010), (0.0, HEM_Y)]
    parts.append(ck.surface_lathe(hem, P + "Jacket", seg=48, theta0=th0, theta1=2 * math.pi - th0))
    piping = [(0.012, HEM_Y + 0.002), (0.020, HEM_Y - 0.004), (0.020, HEM_Y - 0.012), (0.012, HEM_Y - 0.016)]
    parts.append(ck.surface_lathe(piping, P + "Gold", seg=48, theta0=th0, theta1=2 * math.pi - th0))
    # zakkleppen met gouden bies
    for s in (1, -1):
        flap = ck.plate([(-0.055, -0.017), (0.055, -0.017), (0.052, 0.017), (-0.052, 0.017)], 0.010,
                        P + "JacketDark", chamfer=0.003)
        trim = ck.tube([(-0.052, -0.018, 0.012), (0.0, -0.020, 0.012), (0.052, -0.018, 0.012)], 0.0055, P + "Gold",
                       n=12, seg=8)
        parts.append(ck.wrap(mk.merge(flap, trim), s * 52 * DEG, 0.335, 0.002))
    # naambadge met treintje op de linkerborst (+X)
    badge = mk.merge(mk.rounded_box(0.090, 0.056, 0.012, 0.008, 2, mat=P + "Gold").transform(t=(0, 0, 0.006)),
                     ck.flat_icon("loco", 0.058, 0.006, P + "Ink").transform(t=(0, 0.002, 0.012)))
    parts.append(ck.place(badge, 37 * DEG, 0.455, lift=0.004, spin=-0.10))
    # halve ceintuur met twee gouden knopen op de rug
    belt = ck.plate([(-0.085, -0.017), (0.085, -0.017), (0.085, 0.017), (-0.085, 0.017)], 0.010, P + "Jacket",
                    chamfer=0.004)
    knobs = [mk.ellipsoid(0.014, 0.014, 0.008, 14, 7, mat=P + "Gold").transform(t=(x, 0, 0.012)) for x in (-0.050, 0.050)]
    parts.append(ck.wrap(mk.merge(belt, *knobs), math.pi, 0.335, 0.002))
    return mk.merge(*parts)


def build_watch():
    """Gouden zakhorloge aan een ketting vanaf de middelste gileknoop naar het zakje (+X)."""
    parts = []
    chain = [(0.004, 0.355), (0.030, 0.336), (0.058, 0.326), (0.080, 0.332)]
    parts.append(ck.front_tube(chain, 0.0045, P + "Gold", lift=0.020, n=24))
    pts = [ck.front_point(x, y, 0.025)[0] for x, y in chain]
    for a, b in zip(pts[:-1], pts[1:]):                      # schakels: bolletjes op de ketting
        for t in (0.33, 0.66):
            parts.append(mk.ellipsoid(0.007, 0.007, 0.007, 8, 5, mat=P + "Gold").transform(t=a + (b - a) * t))
    c, n = ck.front_point(0.092, 0.300, 0.012)
    R = bc.face_frame(n) @ mk.rot_z(-0.25)
    case = mk.cylinder(0.044, 0.016, 28, 0.005, mat=P + "Gold", axis="z").transform(t=(0, 0, 0.008))
    face = mk.cylinder(0.035, 0.006, 28, 0.002, mat=P + "WatchFace", axis="z").transform(t=(0, 0, 0.019))
    hands = [mk.rounded_box(0.005, 0.026, 0.004, 0.0015, 1, mat=P + "Ink").transform(t=(0, 0.011, 0.024)),
             mk.rounded_box(0.019, 0.005, 0.004, 0.0015, 1, mat=P + "Ink").transform(t=(-0.008, 0, 0.0285))]
    ticks = [mk.cylinder(0.0028, 0.004, 8, 0.0, mat=P + "Ink", axis="z").transform(
        t=(0.027 * math.sin(a), 0.027 * math.cos(a), 0.024)) for a in np.linspace(0, 2 * math.pi, 4, endpoint=False)]
    crown = mk.merge(mk.cylinder(0.009, 0.012, 12, 0.002, mat=P + "Gold", axis="y").transform(t=(0, 0.049, 0.008)),
                     mk.torus(0.010, 0.0035, 14, 6, mat=P + "Gold").transform(R=mk.rot_x(math.pi / 2), t=(0, 0.062, 0.008)))
    parts.append(mk.merge(case, face, *hands, *ticks, crown).transform(R=R, t=c))
    return mk.merge(*parts)


def build_arm(side):
    arm = ck.build_arm(P + "Jacket", P + "Skin", sleeve_bulge=1.04)
    cuff = mk.torus(0.080, 0.009, 32, 8, mat=P + "Gold", a=1.0, b=0.88).transform(t=(0, -0.130, 0))
    stripe = mk.torus(0.081, 0.006, 32, 6, mat=P + "Gold", a=1.0, b=0.88).transform(t=(0, -0.104, 0))
    return mk.merge(arm, cuff, stripe)


# --------------------------------------------------------------------- haar & pet
def build_hair():
    """Witte zijlokken (dikke plukken boven de 'oren') en een smallere rand achterlangs onder de pet."""
    puffs = []
    rng = np.random.default_rng(31)
    for y, r, lift, angles in ((0.775, 0.070, 0.020, (80, 100, 120)),
                               (0.860, 0.078, 0.026, (78, 98, 120)),
                               (0.905, 0.056, 0.012, (88, 112, 136))):
        th = [a * DEG for a in angles] + [-a * DEG for a in angles]
        S, N = ck.surface(np.array(th), np.full(len(th), y))
        for s_, n in zip(S, N):
            puffs.append((s_ + n * lift + rng.normal(0, 0.004, 3), r * rng.uniform(0.94, 1.06)))
    th = np.array([146, 166, -166, -146]) * DEG                      # achterkant: één zachte rij
    S, N = ck.surface(th, np.full(len(th), 0.872))
    for s_, n in zip(S, N):
        puffs.append((s_ + n * 0.004, 0.062))
    return ck.tufts(puffs, P + "Hair", seed=5)


def build_mustache():
    """Grote witte snor met opkrullende punten, als één gladde massa boven de mond."""
    spheres = []
    for s in (1, -1):
        for x, y, r in ((0.022, 0.700, 0.034), (0.058, 0.692, 0.038), (0.098, 0.684, 0.034), (0.132, 0.690, 0.026),
                        (0.152, 0.708, 0.019), (0.154, 0.728, 0.014)):
            z, n = bc.body_surface(s * x, y)
            spheres.append((np.array([s * x, y, z]) + n * (r * 0.45), r))
    return ck.blob(spheres, P + "Hair", spacing=0.0105, k=0.016)


def build_brows():
    """Borstelige witte wenkbrauwen, schuin naar binnen (trots/streng), op de rand van de oogleden."""
    spheres = []
    for s_ in (1, -1):
        for x, y, r in ((0.045, 0.872, 0.018), (0.080, 0.889, 0.021), (0.118, 0.901, 0.020), (0.150, 0.903, 0.015)):
            z, n = bc.body_surface(s_ * x, y)
            spheres.append((np.array([s_ * x, y, z]) + n * 0.066, r))
    return ck.blob(spheres, P + "Hair", spacing=0.0095, k=0.012)


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Shoe"), (ck.SHOE_Y, HEM_Y, P + "Trousers"),
                     (HEM_Y, bc.Y_COLLAR, P + "Jacket"), (bc.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Vest", build_vest_and_shirt())
    ck.add_detail(sc, "Jacket", build_jacket())
    ck.add_detail(sc, "Watch", build_watch(), ao_group="body")
    ck.add_detail(sc, "Hair", build_hair(), gradient=0.0)
    ck.add_face(sc, P, lids=True, lid_drop=0.16, default_mouth="Smile")
    ck.add_detail(sc, "Mustache", build_mustache(), gradient=0.0)
    ck.add_detail(sc, "Brows", build_brows(), gradient=0.0)
    cap = ck.peaked_cap(P + "Cap", P + "Gold", P + "Visor", emblem="loco", emblem_mat=P + "Gold", emblem_ink=P + "Ink",
                        crown_k=1.34, crown_h=0.19)
    # pet iets hoger en verder achterover dan bij de conducteur: de klep komt boven de wenkbrauwen
    sc.add(mk.Part("Cap", cap, (0.0, 0.935 - bc.HIP_Y, -0.010), "Body", mk.rot_x(-15 * DEG) @ mk.rot_z(3 * DEG),
                   ao_group="body", gradient=0.0))
    ck.add_arms(sc, build_arm, {"L": ck.arm_pose(150, 18, 10, side=1), "R": ck.arm_pose(24, 6, side=-1)})
    ck.add_leg_anchors(sc)
    return sc


def main():
    ck.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
