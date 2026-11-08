"""Spoormonteur (engineer) — personagevariant op het conducteur-basismodel.

Referentie: Docs/References/Characters/rail-mechanic-concept.png
Kenmerken (silhouet eerst):
  * gele bouwhelm met bruine band, nokrand en hoofdlamp
  * vastberaden blik: schuine oogleden (geen wenkbrauwen), klein streepjesmondje
  * marineblauwe werkoverall met reflecterende band om de broekspijpen, crème shirt in de V-hals
  * leren bretels met gouden gespen, gereedschapsgordel met twee tasjes (moersleutel + schroevendraaier)
  * dikke bruine werkhandschoenen, borstzakje met sleutel
Kleuren: ramps helmet, uniform_navy (gemeten overall = conducteur-blauw), leather, boot_brown, gold_trim, metal.

Uitvoer: Assets/Art/Models/Characters/RailMechanic/RailMechanic.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/RailMechanic/build_rail_mechanic.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "RailMechanic"
P = "M_RailMechanic_"
GLOVE_CUT = -0.138


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Overall", "uniform_navy", 0.05, 0.22, paint=1.3)
    ck.material(sc, P + "OverallDark", "uniform_navy", -0.22, 0.22)
    ck.material(sc, P + "Undershirt", "parchment", 0.30, 0.25)
    ck.material(sc, P + "Reflect", "metal", 0.30, 0.30)
    ck.material(sc, P + "Boot", "boot_brown", -0.10, 0.25)
    ck.material(sc, P + "Glove", "boot_brown", 0.05, 0.25, paint=1.1)
    ck.material(sc, P + "Helmet", "helmet", 0.0, 0.30)
    ck.material(sc, P + "HelmetBand", "boot_brown", 0.0, 0.25)
    ck.material(sc, P + "LampHousing", "ui_bg", 0.10, 0.30)
    ck.material(sc, P + "LampBezel", "metal", -0.10, 0.35)
    ck.material(sc, P + "Lamp", "headlight", 0.20, 0.40, emissive=0.35)
    ck.material(sc, P + "Leather", "leather", 0.0, 0.25, paint=1.1)
    ck.material(sc, P + "LeatherDark", "leather", -0.30, 0.25)
    ck.material(sc, P + "Buckle", "gold_trim", 0.05, 0.35)
    ck.material(sc, P + "Metal", "metal", 0.10, 0.35)
    ck.material(sc, P + "ToolHandle", "accent_yellow", 0.0, 0.30)
    ck.material(sc, P + "Sweat", "water", 0.20, 0.40, spec=0.15)


# ----------------------------------------------------------------------- gereedschap
def wrench(length=0.13, mat=None):
    """Steeksleutel langs +Y: platte steel met een open bek (C-vorm) bovenaan."""
    mat = mat or P + "Metal"
    handle = mk.rounded_box(0.019, length, 0.008, 0.004, 1, mat=mat).transform(t=(0, length / 2, 0))
    jaw = [(0.023 * math.cos(a), length + 0.012 + 0.023 * math.sin(a), 0.0)
           for a in np.linspace(math.radians(120), math.radians(420), 16)]
    head = ck.tube(jaw, 0.0085, mat, n=18, seg=8)
    return mk.merge(handle, head)


def screwdriver():
    handle = mk.capsule_between((0, 0.0, 0), (0, 0.062, 0), 0.016, 0.014, 12, 3, mat=P + "ToolHandle")
    shaft = mk.cylinder(0.0045, 0.075, 10, 0.0, mat=P + "Metal", axis="y").transform(t=(0, -0.034, 0))
    return mk.merge(handle, shaft).transform(R=mk.rot_z(math.pi), t=(0, 0.062, 0))


# ---------------------------------------------------------------------- overall
def build_workwear():
    parts = []
    # V-hals met crème shirt en donkere revers (van de conducteur)
    v = np.array([(x, y, bc.body_surface(x, y)[0] + 0.004) for x, y in ((-0.066, 0.533), (0.0, 0.468), (0.066, 0.533))])
    parts.append(mk.Mesh(v, [(0, 1, 2)], [P + "Undershirt"]))
    parts.append(ck.remap(bc.build_collar(), {"M_Conductor_UniformDark": P + "OverallDark"}))
    # leren bretels voor en achter, met gouden gespen voor
    for s in (1, -1):
        parts.append(ck.strap([(s * 25 * DEG, 0.318), (s * 25.5 * DEG, 0.42), (s * 27 * DEG, 0.531)],
                              0.050, 0.013, P + "Leather", lift=0.003))
        parts.append(ck.strap([(s * 154 * DEG, 0.318), (s * 153 * DEG, 0.531)], 0.050, 0.013, P + "Leather",
                              lift=0.003))
        S, N = ck.surface(np.array([s * 25.6 * DEG]), np.array([0.462]))
        buckle = mk.merge(ck.squircle_ring(0.064, 0.050, 0.0075, P + "Buckle"),
                          mk.rounded_box(0.054, 0.008, 0.008, 0.003, 1, mat=P + "Buckle"))
        parts.append(buckle.transform(R=ck.frame_at(N[0]), t=S[0] + N[0] * 0.022))
    # borstzakje met sleutel
    pocket = ck.plate([(-0.042, -0.040), (0.042, -0.040), (0.040, 0.034), (-0.040, 0.034)], 0.009,
                      P + "OverallDark", chamfer=0.003)
    parts.append(ck.wrap(pocket, 6 * DEG, 0.420, 0.0))
    S, N = ck.surface(np.array([8 * DEG]), np.array([0.445]))
    parts.append(wrench(0.07).transform(R=ck.frame_at(N[0], spin=-0.25), t=S[0] + N[0] * 0.010 + np.array([0, -0.01, 0])))
    # gereedschapsgordel
    belt = [(0.0, 0.286), (0.017, 0.290), (0.021, 0.303), (0.021, 0.334), (0.017, 0.346), (0.0, 0.350)]
    parts.append(ck.surface_lathe(belt, P + "Leather", seg=56))
    S, N = ck.surface(np.array([0.0]), np.array([0.318]))
    big = mk.merge(ck.squircle_ring(0.080, 0.060, 0.0095, P + "Buckle"),
                   mk.rounded_box(0.062, 0.042, 0.008, 0.006, 1, mat=P + "LeatherDark"),
                   mk.rounded_box(0.010, 0.050, 0.010, 0.004, 1, mat=P + "Buckle").transform(t=(0.010, 0, 0.004)))
    parts.append(big.transform(R=ck.frame_at(N[0]), t=S[0] + N[0] * 0.026))
    # twee leren tasjes aan de gordel; links (+X) met sleutel en schroevendraaier
    for s in (1, -1):
        pouch = [mk.rounded_box(0.108, 0.122, 0.056, 0.020, 2, mat=P + "Leather").transform(t=(0, 0, 0.028)),
                 mk.rounded_box(0.114, 0.048, 0.066, 0.016, 2, mat=P + "LeatherDark").transform(t=(0, 0.044, 0.034)),
                 mk.ellipsoid(0.010, 0.010, 0.006, 10, 5, mat=P + "Buckle").transform(t=(0, 0.032, 0.068))]
        if s > 0:
            pouch.append(wrench(0.12).transform(R=mk.rot_z(-0.12), t=(0.026, 0.010, 0.030)))
            pouch.append(screwdriver().transform(R=mk.rot_z(0.18), t=(-0.022, 0.060, 0.028)))
        parts.append(ck.place(mk.merge(*pouch), s * 47 * DEG, 0.262, lift=0.004, spin=s * 0.06))
    return mk.merge(*parts)


# ----------------------------------------------------------------------- helm
def build_helmet():
    """Lokaal: oorsprong = onderkant helm, midden van het hoofd."""
    a, b, n = 0.305, 0.292, 2.2
    prof = [(0.0, 0.0), (1.0, 0.0), (1.0, 0.055), (0.99, 0.100), (0.955, 0.150), (0.88, 0.198), (0.76, 0.236),
            (0.58, 0.262), (0.32, 0.277), (0.0, 0.281)]
    dome = mk.lathe(prof, 48, a, b, n, mat=P + "Helmet")
    band = mk.lathe([(0.0, 0.034), (1.024, 0.034), (1.032, 0.043), (1.032, 0.094), (1.024, 0.102), (0.0, 0.102)],
                    48, a, b, n, mat=P + "HelmetBand")
    # rand: smal rondom, een korte klep aan de voorkant
    seg, sec = 64, 12
    V, F = [], []
    for i in range(seg):
        phi = 2 * math.pi * i / seg
        sx, cz = math.sin(phi), math.cos(phi)
        inner = np.array([a * 0.985 * mk.spow(sx, 2 / n), 0.004, b * 0.985 * mk.spow(cz, 2 / n)])
        w = 0.040 + 0.060 * max(cz, 0.0) ** 1.6
        outer = inner + np.array([sx, 0.0, cz]) * w + np.array([0, -0.010 - 0.008 * max(cz, 0), 0])
        for j in range(sec):
            th = 2 * math.pi * j / sec
            t = (1 - math.cos(th)) / 2
            p = inner * (1 - t) + outer * t
            p[1] += 0.011 * math.sin(th)
            V.append(p)
    for i in range(seg):
        i2 = (i + 1) % seg
        for j in range(sec):
            j2 = (j + 1) % sec
            F.append((i * sec + j, i2 * sec + j, i2 * sec + j2, i * sec + j2))
    brim = mk.orient_outward(mk.Mesh(np.array(V), F, [P + "Helmet"] * len(F)))
    # nok over de bovenkant
    ys = np.array([p[1] for p in prof])
    rs = np.array([p[0] for p in prof])
    ridge = []
    for y in np.linspace(0.120, 0.281, 9):
        ridge.append((0.0, y + 0.004, b * np.interp(y, ys, rs) + 0.006))
    ridge += [(0.0, y + 0.004, -b * np.interp(y, ys, rs) - 0.006) for y in np.linspace(0.281, 0.10, 9)[1:]]
    nok = ck.tube(ridge, 0.024, P + "Helmet", n=40, seg=12)
    # hoofdlamp op de band
    z = b * 1.032
    lamp = [mk.cylinder(0.064, 0.050, 28, 0.010, mat=P + "LampHousing", axis="z").transform(t=(0, 0.068, z + 0.018)),
            mk.torus(0.057, 0.012, 28, 8, mat=P + "LampBezel").transform(R=mk.rot_x(math.pi / 2), t=(0, 0.068, z + 0.044)),
            mk.ellipsoid(0.050, 0.050, 0.020, 24, 10, mat=P + "Lamp").transform(t=(0, 0.068, z + 0.042))]
    return mk.merge(dome, band, brim, nok, *lamp)


def build_arm(side):
    arm = ck.build_arm(P + "Overall", P + "Glove", cut=GLOVE_CUT, hand_scale=1.20, sleeve_bulge=1.04)
    cuff = mk.torus(0.084, 0.016, 32, 8, mat=P + "Glove", a=1.0, b=0.88).transform(t=(0, GLOVE_CUT + 0.006, 0))
    return mk.merge(arm, cuff)


def build_sweat():
    prof = [(0.0, -0.020), (0.010, -0.018), (0.016, -0.010), (0.017, 0.000), (0.012, 0.012), (0.005, 0.024),
            (0.0, 0.030)]
    drop = mk.lathe(prof, 16, mat=P + "Sweat")
    parts = []
    for th, y, s in ((63, 0.815, 1.7), (73, 0.745, 1.3)):
        S, N = ck.surface(np.array([th * DEG]), np.array([y]))
        parts.append(drop.transform(s=(s, s, s * 0.6), R=ck.frame_at(N[0]) @ mk.rot_z(0.35))
                     .transform(t=S[0] + N[0] * 0.012))
    return mk.merge(*parts)


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Boot"), (ck.SHOE_Y, 0.102, P + "Overall"), (0.102, 0.136, P + "Reflect"),
                     (0.136, ck.Y_COLLAR, P + "Overall"), (ck.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Workwear", build_workwear())
    ck.add_face(sc, P, lids=True, lid_drop=0.42, default_mouth="Flat")
    sc.add(mk.Part("Helmet", build_helmet(), (0.0, 0.900 - bc.HIP_Y, 0.0), "Body", mk.rot_x(-9 * DEG),
                   ao_group="body", gradient=0.0))
    ck.add_detail(sc, "Prop_Sweat", build_sweat(), ao_group="sweat", visible=False, gradient=0.0)
    ck.add_arms(sc, build_arm, {"L": ck.arm_pose(40, 4, side=1), "R": ck.arm_pose(40, 4, side=-1)})
    ck.add_leg_anchors(sc)
    return sc


def main():
    ck.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
