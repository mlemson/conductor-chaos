"""Schaap — pluizig, lief en een beetje sullig (concept: Docs/References/Animals/sheep-concept.png).

Silhouet eerst: een wolk van wol op vier stompe pootjes, een grote zandkleurige kop met de ogen van de
personages, een wolkuif bovenop en twee oren die opzij steken.

Hiërarchie: Sheep › Body (wol) › Head › (Tuft, Ear_L/R, Eye_L/R › Pupil_L/R, EyeClosed_L/R, Blink_L/R,
            Mouth_Neutral|Surprised|Happy, Fx_Alert), Leg_FL/FR/BL/BR.
Standaard verborgen: EyeClosed_*, Blink_*, Mouth_Surprised, Mouth_Happy, Fx_Alert.
Poten, kop en oren zijn losse knopen met het draaipunt op de aanhechting (eenvoudig te animeren).

Kleuren: ramps wool, safari_hat (kop/poten), hair_brown (hoeven), accent_yellow (palette.json).
Uitvoer: Assets/Art/Models/Animals/Sheep/Sheep.fbx (+ ArtSource/Exports/Animals/Sheep.glb en .materials.json)
Gebruik:  python ArtSource/Animals/Sheep/build_sheep.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import animal_kit as ak  # noqa: E402
from animal_kit import ck, mk, DEG  # noqa: E402

ASSET = "Sheep"
P = "M_Sheep_"

BODY_C = np.array([0.0, 0.365, -0.06])          # midden van de wolbal
BODY_R = np.array([0.335, 0.270, 0.345])        # halve maten van de wolbal (buitenkant)
PUFF = 0.150                                    # straal van een wolpluk
HEAD_C = np.array([0.0, 0.425, 0.285])          # midden van de kop
HEAD_R = (0.240, 0.200, 0.182)
NECK = np.array([0.0, 0.43, 0.14])              # draaipunt van de kop
EYE_X, EYE_Y, EYE_R = 0.091, 0.497, 0.092
MOUTH_Y = 0.330
LEGS = {"FL": (0.135, 0.13), "FR": (-0.135, 0.13), "BL": (0.150, -0.25), "BR": (-0.150, -0.25)}
LEG_TOP = 0.200


def materials(sc):
    ak.face_materials(sc, P)
    ck.material(sc, P + "Wool", "wool", 0.40, 0.20, paint=1.0, cells=0.30)
    ck.material(sc, P + "Face", "safari_hat", 0.05, 0.25, paint=1.1)
    ck.material(sc, P + "Hoof", "hair_brown", -0.05, 0.25)
    ck.material(sc, P + "Alert", "accent_yellow", 0.0, 0.30, noShadow=True)


def build_wool():
    """Wolbal: plukken op een ellipsoïde rond een dichte kern, als één gesloten oppervlak."""
    shell = BODY_R - PUFF
    sph = [(BODY_C, float(shell.min()) + 0.075)]
    sph += [(BODY_C + np.array([0, 0, dz]), float(shell.min()) + 0.06) for dz in (-0.07, 0.07)]
    rng = np.random.default_rng(4)
    for c in ak.fib_points(22, shell, BODY_C, seed=7, jitter=0.010):
        sph.append((c, PUFF + rng.uniform(-0.012, 0.016)))
    sph.append((BODY_C + np.array([0, 0.03, -BODY_R[2] + 0.03]), 0.085))          # staartplukje
    for x, z in LEGS.values():                                                    # wol hangt tot vlak boven de hoeven
        sph.append((np.array([x * 1.12, 0.185, z * 1.05 + 0.01]), 0.098))
    return ck.blob(sph, P + "Wool", spacing=0.024, k=0.012)


def build_head():
    return ak.superellipsoid(*HEAD_R, seg=44, rings=26, mat=P + "Face", nv=2.5, nh=2.5).transform(t=HEAD_C)


def build_tuft():
    """Wolkuif op de kop: reikt tot net boven de ogen en loopt naar achteren door in de wolbal."""
    c = [((0.0, 0.672, 0.300), 0.098), ((0.105, 0.640, 0.292), 0.090), ((-0.105, 0.640, 0.292), 0.090),
         ((0.185, 0.600, 0.250), 0.070), ((-0.185, 0.600, 0.250), 0.070),
         ((0.060, 0.690, 0.215), 0.092), ((-0.060, 0.690, 0.215), 0.092), ((0.0, 0.650, 0.150), 0.110),
         ((0.130, 0.640, 0.180), 0.085), ((-0.130, 0.640, 0.180), 0.085)]
    tuft = ck.blob([(np.array(p), r) for p, r in c], P + "Wool", spacing=0.018, k=0.010)
    hr = np.array(HEAD_R) - 0.012                                                 # wat in de kop verdwijnt weglaten

    def in_head(V):
        return (np.abs((V - HEAD_C) / hr) ** 2.5).sum(1) < 1.0
    return ak.cull_inside(tuft, in_head)


def build_ear(side):
    """Oor (lokaal: aanhechting in de oorsprong, punt naar +X voor links)."""
    ear = ak.superellipsoid(0.085, 0.046, 0.034, 20, 12, mat=P + "Face", nv=2.0, nh=2.0)
    V = ear.V.copy()
    t = np.clip((V[:, 0] + 0.085) / 0.17, 0, 1)
    V[:, 1] *= 0.70 + 0.45 * np.sin(t * math.pi * 0.85)        # smal bij de aanhechting, breed in het midden
    V[:, 0] += 0.070
    ear.V = V
    return ear if side > 0 else ear.transform(s=(-1, 1, 1))


def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    sc.add(mk.Part(ASSET, None))
    sc.add(mk.Part("Body", build_wool().transform(t=-BODY_C), tuple(BODY_C), ASSET, ao_group="body", gradient=0.14))
    head = build_head()
    sc.add(mk.Part("Head", head.transform(t=-NECK), tuple(NECK - BODY_C), "Body", ao_group="head", ao_receive=("tuft",),
                   gradient=0.10))
    sc.add(mk.Part("Tuft", build_tuft().transform(t=-NECK), (0, 0, 0), "Head", ao_group="tuft", gradient=0.0))
    for side, nm in ((1, "L"), (-1, "R")):
        root = np.array([side * 0.214, 0.510, 0.275])
        R = mk.rot_y(side * 12 * DEG) @ mk.rot_z(-side * 14 * DEG)
        sc.add(mk.Part("Ear_" + nm, build_ear(side), tuple(root - NECK), "Head", R, ao_group="head", gradient=0.0))
    ak.add_face(sc, P, head, "Head", NECK, EYE_X, EYE_Y, EYE_R, (0.0, MOUTH_Y), lid_mat=P + "Face")
    marks = ak.alert_marks(P + "Alert")
    sc.add(mk.Part("Fx_Alert", marks, tuple(np.array([-0.215, 0.730, 0.300]) - NECK), "Head", mk.rot_z(42 * DEG),
                   visible=False, ao_group="fx", gradient=0.0))
    for nm, (x, z) in LEGS.items():
        sc.add(mk.Part("Leg_" + nm, ak.leg(0.052, 0.066, 0.078, LEG_TOP, P + "Face", P + "Hoof"),
                       tuple(np.array([x, LEG_TOP, z]) - BODY_C), "Body", ao_group="legs", ao_receive=("body",),
                       gradient=0.0))
    return sc


def main():
    ak.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
