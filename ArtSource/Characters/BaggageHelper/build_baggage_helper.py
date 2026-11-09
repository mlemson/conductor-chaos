"""Bagagedrager (kruier / stationsassistent) + bagagekar — personagevariant op het conducteur-basismodel (issue #204).

Referentie: Docs/References/Characters/baggage-helper-concept.png
Kenmerken (silhouet eerst):
  * bordeaux pet met gouden band, bordeaux klep en goudovaal embleem met koffertje
  * crème overhemd met korte mouwen en puntkraag, marine tuinbroek met bretels, twee gouden knopen
    en een gouden ID-badge met koffertje op het borststuk
  * grote donkerbruine werkhandschoenen (grip & carry)
  * aparte bagagekar (eigen model BaggageTrolley): geel frame met duwbeugel, vier wielen en
    drie gestapelde koffers (blauw, rood, bruin) met lichte hoekbeschermers en riemen
Kleuren: ramps cap_maroon, uniform_navy, parchment, boot_brown, gold_trim, luggage_blue, luggage_red, leather,
safari_hat (palette.json).

Uitvoer: Assets/Art/Models/Characters/BaggageHelper/BaggageHelper.fbx en
         Assets/Art/Models/Characters/BaggageTrolley/BaggageTrolley.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/BaggageHelper/build_baggage_helper.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "BaggageHelper"
TROLLEY = "BaggageTrolley"
P = "M_BaggageHelper_"
T = "M_BaggageTrolley_"
GLOVE_CUT = -0.078
BIB_TOP = 0.505
HANDLE_Y = 0.520         # hoogte duwbeugel bagagekar (borsthoogte van de drager)
WAIST = 0.440            # tot hier loopt de tuinbroek rondom; daarboven alleen het borststuk


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Overall", "uniform_navy", 0.05, 0.22, paint=1.3)
    ck.material(sc, P + "OverallDark", "uniform_navy", -0.22, 0.22)
    ck.material(sc, P + "Shirt", "parchment", 0.30, 0.25, paint=1.1)
    ck.material(sc, P + "Glove", "boot_brown", 0.05, 0.25, paint=1.1)
    ck.material(sc, P + "Shoe", "boot_brown", -0.10, 0.25)
    ck.material(sc, P + "Cap", "cap_maroon", 0.05, 0.22, paint=1.2)
    ck.material(sc, P + "CapDark", "cap_maroon", -0.30, 0.22)
    ck.material(sc, P + "Gold", "gold_trim", 0.0, 0.35)
    ck.material(sc, P + "Ink", "cap_maroon", -0.45, 0.25)
    ck.material(sc, P + "Sweat", "water", 0.20, 0.40, spec=0.15)


# ---------------------------------------------------------------------- kleding
def build_workwear():
    parts = []
    # puntkraag van het overhemd
    for s in (1, -1):
        tip = [(s * 0.008, 0.522), (s * 0.078, bc.Y_COLLAR + 0.004), (s * 0.058, 0.478)]
        parts.append(ck.front_plate(tip if s > 0 else tip[::-1], 0.009, P + "Shirt", lift=0.006, chamfer=0.003))
    # borststuk van de tuinbroek (sluit aan op de taille-rand) met twee gouden knopen en de ID-badge
    bib = [(-0.135, WAIST - 0.010), (0.135, WAIST - 0.010), (0.128, BIB_TOP), (-0.128, BIB_TOP)]
    parts.append(ck.front_plate(bib, 0.010, P + "Overall", lift=0.003, chamfer=0.004))
    for s in (1, -1):
        z, n = bc.body_surface(s * 0.098, BIB_TOP - 0.022)
        parts.append(mk.ellipsoid(0.016, 0.016, 0.009, 14, 7, mat=P + "Gold").transform(
            R=bc.face_frame(n), t=(s * 0.098, BIB_TOP - 0.022, z + 0.017)))
    badge = mk.merge(mk.rounded_box(0.078, 0.058, 0.012, 0.008, 2, mat=P + "Gold").transform(t=(0, 0, 0.006)),
                     ck.flat_icon("case", 0.046, 0.006, P + "Ink").transform(t=(0, 0.002, 0.012)))
    parts.append(ck.place(badge, 0.0, 0.462, lift=0.014))
    # taille-rand rondom en bretels over de schouders naar achteren
    rim = [(0.0, WAIST - 0.004), (0.010, WAIST - 0.002), (0.012, WAIST + 0.006), (0.010, WAIST + 0.012),
           (0.0, WAIST + 0.014)]
    parts.append(ck.surface_lathe(rim, P + "Overall", seg=48))
    for s in (1, -1):
        parts.append(ck.strap([(s * 21 * DEG, BIB_TOP - 0.010), (s * 30 * DEG, 0.548), (s * 70 * DEG, 0.565),
                               (s * 125 * DEG, 0.540), (s * 152 * DEG, WAIST + 0.006)], 0.044, 0.012, P + "Overall",
                              lift=0.004))
    # zakken op de pijpen (marine, donkerder klep)
    for s in (1, -1):
        flap = ck.plate([(-0.040, -0.012), (0.040, -0.012), (0.038, 0.012), (-0.038, 0.012)], 0.009,
                        P + "OverallDark", chamfer=0.003)
        parts.append(ck.wrap(flap, s * 60 * DEG, 0.360, 0.002))
    return mk.merge(*parts)


def build_arm(side):
    arm = ck.build_arm(P + "Shirt", P + "Glove", cut=GLOVE_CUT, hand_scale=1.22, sleeve_bulge=1.12)
    cuff = mk.torus(0.086, 0.017, 32, 8, mat=P + "Glove", a=1.0, b=0.88).transform(t=(0, GLOVE_CUT - 0.008, 0))
    hem = mk.torus(0.090, 0.010, 32, 6, mat=P + "Shirt", a=1.0, b=0.88).transform(t=(0, GLOVE_CUT + 0.022, 0))
    return mk.merge(arm, cuff, hem)


def build_sweat():
    prof = [(0.0, -0.020), (0.010, -0.018), (0.016, -0.010), (0.017, 0.000), (0.012, 0.012), (0.005, 0.024),
            (0.0, 0.030)]
    drop = mk.lathe(prof, 16, mat=P + "Sweat")
    S, N = ck.surface(np.array([66 * DEG]), np.array([0.840]))
    return drop.transform(s=(1.7, 1.7, 1.0), R=ck.frame_at(N[0]) @ mk.rot_z(0.35)).transform(t=S[0] + N[0] * 0.018)


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Shoe"), (ck.SHOE_Y, WAIST, P + "Overall"), (WAIST, bc.Y_COLLAR, P + "Shirt"),
                     (bc.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Workwear", build_workwear())
    ck.add_face(sc, P, default_mouth="Happy")
    ck.add_detail(sc, "Prop_Sweat", build_sweat(), ao_group="sweat", visible=False, gradient=0.0)
    cap = ck.peaked_cap(P + "Cap", P + "Gold", P + "CapDark", emblem="case", emblem_mat=P + "Gold",
                        emblem_ink=P + "Ink", crown_k=1.30, crown_h=0.16)
    sc.add(mk.Part("Cap", cap, (0.0, 0.905 - bc.HIP_Y, 0.0), "Body", mk.rot_x(-10 * DEG) @ mk.rot_z(-3 * DEG),
                   ao_group="body", gradient=0.0))
    # linkerhand naar voren en opzij op de duwbeugel van de kar, rechterarm ontspannen
    ck.add_arms(sc, build_arm, {"L": ck.arm_pose(96, 22, 0, side=1), "R": ck.arm_pose(26, 6, side=-1)})
    ck.add_leg_anchors(sc)
    return sc


# --------------------------------------------------------------------- bagagekar
def trolley_materials(sc):
    ck.material(sc, T + "Frame", "accent_yellow", -0.05, 0.30, paint=1.1)
    ck.material(sc, T + "FrameDark", "accent_yellow", -0.40, 0.30)
    ck.material(sc, T + "Wheel", "ui_bg", 0.10, 0.30)
    ck.material(sc, T + "Hub", "metal", 0.10, 0.35)
    ck.material(sc, T + "Grip", "ui_bg", 0.25, 0.30)
    ck.material(sc, T + "Blue", "luggage_blue", 0.05, 0.25, paint=1.2)
    ck.material(sc, T + "Red", "luggage_red", 0.05, 0.25, paint=1.2)
    ck.material(sc, T + "Brown", "leather", 0.15, 0.25, paint=1.2)
    ck.material(sc, T + "Trim", "safari_hat", 0.0, 0.25)
    ck.material(sc, T + "Buckle", "gold_trim", 0.10, 0.35)
    for m in sc.materials.values():
        m["opts"].update({"cells": 0.4, "cellSize": 0.6})


def case(w, h, d, mat, trim=T + "Trim"):
    """Koffer (oorsprong onderkant midden): afgeronde romp, lichte hoekbeschermers die 1 cm uitsteken,
    twee riemen met gesp en een handgreep bovenop."""
    parts = [mk.rounded_box(w, h, d, min(0.040, h / 4), 3, mat=mat).transform(t=(0, h / 2, 0))]
    c = 0.075
    for sx in (-1, 1):
        for sy in (0, 1):
            for sz in (-1, 1):
                p = np.array([sx * (w / 2 - c / 2 + 0.006), sy * h + (0.5 - sy) * (c - 0.012), sz * (d / 2 - c / 2 + 0.006)])
                parts.append(mk.rounded_box(c, c, c, 0.020, 2, mat=trim).transform(t=p))
    for x in (-w * 0.26, w * 0.26):
        parts.append(mk.rounded_box(0.040, h + 0.016, d + 0.016, 0.010, 1, mat=trim).transform(t=(x, h / 2, 0)))
        parts.append(mk.rounded_box(0.034, 0.040, 0.014, 0.004, 1, mat=T + "Buckle").transform(
            t=(x, h * 0.62, d / 2 + 0.012)))
    parts.append(ck.tube([(-0.085, h - 0.004, 0), (-0.070, h + 0.058, 0), (0.070, h + 0.058, 0), (0.085, h - 0.004, 0)],
                         0.015, trim, n=14, seg=8))
    return mk.merge(*parts)


def build_trolley():
    """Lokaal: oorsprong op de grond onder het midden van de laadvloer; duwbeugel aan -X."""
    L, W, yd = 1.00, 0.62, 0.200
    parts = [mk.rounded_box(L, 0.050, W, 0.018, 2, mat=T + "Frame").transform(t=(0, yd - 0.025, 0))]
    for z in (-W / 2 + 0.030, W / 2 - 0.030):                       # langsliggers onder de vloer
        parts.append(mk.rounded_box(L - 0.06, 0.050, 0.040, 0.012, 1, mat=T + "FrameDark").transform(
            t=(0, yd - 0.070, z)))
    for x in (-L / 2 + 0.13, L / 2 - 0.13):
        for z in (-W / 2 + 0.07, W / 2 - 0.07):
            wheel = mk.cylinder(0.085, 0.050, 16, 0.014, mat=T + "Wheel", axis="z")
            hub = mk.cylinder(0.032, 0.066, 10, 0.006, mat=T + "Hub", axis="z")
            fork = mk.rounded_box(0.040, 0.075, 0.074, 0.010, 1, mat=T + "FrameDark").transform(t=(0, 0.060, 0))
            parts.append(mk.merge(wheel, hub, fork).transform(t=(x, 0.085, z)))
    # duwbeugel: twee buizen en een greep met donkere handvatten
    xb = -L / 2 + 0.020
    for z in (-W / 2 + 0.045, W / 2 - 0.045):
        parts.append(ck.tube([(xb + 0.015, yd - 0.030, z), (xb - 0.004, 0.34, z), (xb - 0.030, 0.46, z),
                              (xb - 0.090, HANDLE_Y, z)], 0.024, T + "Frame", n=20, seg=10))
    parts.append(mk.capsule_between((xb - 0.090, HANDLE_Y, -W / 2 + 0.045), (xb - 0.090, HANDLE_Y, W / 2 - 0.045),
                                    0.026, 0.026, 12, 3, mat=T + "Frame"))
    for z in (-W / 2 + 0.090, W / 2 - 0.090):
        parts.append(mk.capsule_between((xb - 0.090, HANDLE_Y, z - 0.050), (xb - 0.090, HANDLE_Y, z + 0.050), 0.034,
                                        0.034, 12, 3, mat=T + "Grip"))
    parts.append(ck.tube([(xb, 0.40, -W / 2 + 0.045), (xb, 0.40, W / 2 - 0.045)], 0.018, T + "Frame", n=6, seg=8,
                         ))                                           # dwarsbuis halverwege
    # drie gestapelde koffers, elk iets gedraaid
    y = yd
    for (w, h, d, mat, rot, dx) in ((0.84, 0.240, 0.52, T + "Blue", 0.0, 0.04), (0.72, 0.220, 0.46, T + "Red", 0.05, 0.05),
                                    (0.50, 0.180, 0.32, T + "Brown", -0.07, 0.06)):
        parts.append(case(w, h, d, mat).transform(R=mk.rot_y(rot), t=(dx, y + 0.002, 0)))
        y += h + 0.002 + 0.004
    return mk.merge(*parts)


def build_trolley_scene():
    sc = mk.Scene(TROLLEY)
    trolley_materials(sc)
    sc.add(mk.Part(TROLLEY, None))
    sc.add(mk.Part("Trolley", build_trolley(), (0, 0, 0), TROLLEY, ao_group="trolley", gradient=0.12))
    hx, hy = -0.50 + 0.020 - 0.090, HANDLE_Y
    sc.add(mk.Part("Handle", None, (hx, hy, 0.0), "Trolley"))           # anker voor de hand van de drager
    return sc


def main():
    ck.finish(build_scene(), ASSET)
    ck.finish(build_trolley_scene(), TROLLEY, bake=dict(density=2500, radius=0.12, noise_freq=4.0,
                                                        edge_range=(14.0, 45.0)))


if __name__ == "__main__":
    main()
