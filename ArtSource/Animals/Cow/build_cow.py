"""Koe — vriendelijk, nieuwsgierig en altijd in de weg op het spoor (concept: Docs/References/Animals/cow-concept.png).

Silhouet eerst: een ronde crèmekleurige romp met donkerbruine vlekken op vier stompe poten, een grote kop met
brede perzikkleurige snuit, een donkere kuif tussen twee korte hoorns, oren opzij en een staart met pluim.

De vlekken zijn IN het oppervlak gesneden (`animal_kit.paint_field`): romp en kop blijven elk één oppervlak met
twee materialen, dus geen dubbele vlakken.

Hiërarchie: Cow › Body › Head › (Muzzle, Tuft, Horn_L/R, Ear_L/R, Eye_L/R › Pupil_L/R, EyeClosed_L/R, Blink_L/R,
            Mouth_Neutral|Surprised|Happy, Fx_Alert), Leg_FL/FR/BL/BR, Tail.
Standaard verborgen: EyeClosed_*, Blink_*, Mouth_Surprised, Mouth_Happy, Fx_Alert.
Poten, kop, oren en staart zijn losse knopen met het draaipunt op de aanhechting (eenvoudig te animeren).

Kleuren: ramps parchment (vacht), hair_brown (vlekken, hoeven), skin (snuit, binnenoor), scarf_cream (hoorns).
Uitvoer: Assets/Art/Models/Animals/Cow/Cow.fbx (+ ArtSource/Exports/Animals/Cow.glb en .materials.json)
Gebruik:  python ArtSource/Animals/Cow/build_cow.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import animal_kit as ak  # noqa: E402
from animal_kit import ck, mk, DEG  # noqa: E402

ASSET = "Cow"
P = "M_Cow_"

BODY_C = np.array([0.0, 0.455, -0.13])
BODY_R = (0.385, 0.340, 0.500)
HEAD_C = np.array([0.0, 0.655, 0.400])
HEAD_R = (0.315, 0.245, 0.225)
NECK = np.array([0.0, 0.62, 0.22])
MUZZLE_C = np.array([0.0, 0.535, 0.545])
MUZZLE_R = (0.232, 0.128, 0.135)
EYE_X, EYE_Y, EYE_R = 0.100, 0.750, 0.100
LEGS = {"FL": (0.190, 0.16), "FR": (-0.190, 0.16), "BL": (0.200, -0.40), "BR": (-0.200, -0.40)}
LEG_TOP = 0.270

# vlekken op de romp: groepjes bollen die het oppervlak snijden (links = +X)
PATCHES = [
    [((0.40, 0.58, -0.02), 0.17), ((0.38, 0.48, -0.16), 0.13)],            # linkerflank, groot
    [((0.34, 0.29, -0.40), 0.12)],                                          # links achter-onder
    [((-0.42, 0.46, -0.28), 0.19), ((-0.36, 0.60, -0.40), 0.12)],          # rechterflank
    [((-0.34, 0.70, 0.02), 0.12)],                                          # rechts schouder
    [((0.10, 0.80, -0.30), 0.15), ((-0.04, 0.80, -0.42), 0.11)],           # rug
    [((0.20, 0.56, -0.62), 0.15)],                                          # bil links
    [((-0.16, 0.32, -0.60), 0.11)],                                         # bil rechts-onder
]


def materials(sc):
    ak.face_materials(sc, P)
    ck.material(sc, P + "Coat", "parchment", 0.30, 0.22, paint=1.0, cells=0.35)
    ck.material(sc, P + "Patch", "hair_brown", 0.0, 0.22, paint=1.0, cells=0.5)
    ck.material(sc, P + "Muzzle", "skin", 0.25, 0.25, paint=1.0, cells=0.4)
    ck.material(sc, P + "Nostril", "skin", -0.35, 0.25)
    ck.material(sc, P + "EarInner", "skin", 0.0, 0.25)
    ck.material(sc, P + "Horn", "scarf_cream", 0.10, 0.28)
    ck.material(sc, P + "Hoof", "hair_brown", -0.10, 0.25)
    ck.material(sc, P + "Alert", "accent_yellow", 0.0, 0.30, noShadow=True)


def patch_field(V):
    d = np.full(len(V), 1e9)
    for grp in PATCHES:
        d = np.minimum(d, ak.smooth_union(V, grp, k=0.035))
    return d


def build_body():
    body = ak.superellipsoid(*BODY_R, seg=56, rings=34, mat=P + "Coat", nv=2.7, nh=2.25).transform(t=BODY_C)
    return ak.paint_field(body, patch_field, P + "Patch")


def head_patch(V):
    """Donker 'masker': bovenkant van de kop tot tussen de ogen, taps toelopend naar de snuit."""
    cap = np.linalg.norm((V - np.array([0.0, 0.950, 0.36])) / np.array([1.30, 1.0, 1.25]), axis=1) - 0.215
    bridge = ak.capsule_dist(V, (0.0, 0.86, 0.62), (0.0, 0.62, 0.66), 0.078)
    k = 0.03
    m = np.minimum(cap, bridge)
    return m - k * np.log(np.exp(-(cap - m) / k) + np.exp(-(bridge - m) / k))


def build_head():
    head = ak.superellipsoid(*HEAD_R, seg=52, rings=30, mat=P + "Coat", nv=2.5, nh=2.4).transform(t=HEAD_C)
    return ak.paint_field(head, head_patch, P + "Patch")


def build_muzzle():
    mz = ak.superellipsoid(*MUZZLE_R, seg=64, rings=32, mat=P + "Muzzle", nv=2.5, nh=2.6).transform(t=MUZZLE_C)

    def nostrils(V):
        d = np.full(len(V), 1e9)
        for s in (1, -1):
            q = (V - np.array([s * 0.092, 0.572, MUZZLE_C[2] + MUZZLE_R[2]])) / np.array([0.017, 0.030, 0.08])
            d = np.minimum(d, np.linalg.norm(q, axis=1) - 1.0)
        return d
    return ak.paint_field(mz, nostrils, P + "Nostril")


def build_tuft():
    c = [((0.0, 0.895, 0.430), 0.105), ((-0.085, 0.890, 0.415), 0.085), ((0.090, 0.890, 0.430), 0.085),
         ((0.030, 0.925, 0.480), 0.070), ((-0.030, 0.910, 0.350), 0.090), ((0.050, 0.870, 0.520), 0.060)]
    tuft = ck.blob([(np.array(p), r) for p, r in c], P + "Patch", spacing=0.016, k=0.012)
    hr = np.array(HEAD_R) - 0.012

    def in_head(V):
        return (np.abs((V - HEAD_C) / hr) ** 2.4).sum(1) < 1.0
    return ak.cull_inside(tuft, in_head)


def build_horn(side):
    """Korte, stompe hoorn (lokaal: voet in de oorsprong, omhoog en licht naar binnen krullend)."""
    pts = [(0.0, -0.03, 0.0), (side * 0.026, 0.030, 0.0), (side * 0.040, 0.080, 0.0), (side * 0.028, 0.118, 0.0)]
    return ck.tube(pts, 0.058, P + "Horn", n=14, seg=12, r_end=0.030)


def build_ear(side):
    """Oor: donkere buitenkant, roze binnenkant (in het oppervlak gesneden). Lokaal: aanhechting in de oorsprong."""
    ear = ak.superellipsoid(0.105, 0.058, 0.036, 28, 14, mat=P + "Patch")
    V = ear.V.copy()
    t = np.clip((V[:, 0] + 0.105) / 0.21, 0, 1)
    V[:, 1] *= 0.62 + 0.50 * np.sin(t * math.pi * 0.85)
    V[:, 0] += 0.085
    ear.V = V

    def inner(Q):
        q = (Q - np.array([0.095, 0.0, 0.05])) / np.array([0.066, 0.036, 0.06])
        return np.linalg.norm(q, axis=1) - 1.0
    ear = ak.paint_field(ear, inner, P + "EarInner")
    return ear if side > 0 else ear.transform(s=(-1, 1, 1))


def build_tail():
    """Staart (lokaal: aanhechting in de oorsprong): dunne crème staart met donkere pluim."""
    pts = [(0, 0.02, 0.03), (0, 0.0, -0.035), (0, -0.09, -0.075), (0, -0.22, -0.085), (0, -0.30, -0.075)]
    stem = ck.tube(pts, 0.024, P + "Coat", n=18, seg=10, r_end=0.020)
    plume = ak.superellipsoid(0.052, 0.068, 0.052, 16, 10, mat=P + "Patch").transform(t=(0, -0.345, -0.072))
    return mk.merge(stem, plume)


def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    sc.add(mk.Part(ASSET, None))
    sc.add(mk.Part("Body", build_body().transform(t=-BODY_C), tuple(BODY_C), ASSET, ao_group="body", gradient=0.16))
    head = build_head()
    sc.add(mk.Part("Head", head.transform(t=-NECK), tuple(NECK - BODY_C), "Body", ao_group="head",
                   ao_receive=("tuft", "muzzle"), gradient=0.08))
    muzzle = build_muzzle()
    sc.add(mk.Part("Muzzle", muzzle.transform(t=-NECK), (0, 0, 0), "Head", ao_group="muzzle", gradient=0.10))
    sc.add(mk.Part("Tuft", build_tuft().transform(t=-NECK), (0, 0, 0), "Head", ao_group="tuft", gradient=0.0))
    for side, nm in ((1, "L"), (-1, "R")):
        hroot = np.array([side * 0.195, 0.858, 0.385])
        sc.add(mk.Part("Horn_" + nm, build_horn(side), tuple(hroot - NECK), "Head", mk.rot_z(-side * 20 * DEG),
                       ao_group="head", gradient=0.0))
        eroot = np.array([side * 0.278, 0.752, 0.375])
        R = mk.rot_y(side * 10 * DEG) @ mk.rot_z(-side * 6 * DEG)
        sc.add(mk.Part("Ear_" + nm, build_ear(side), tuple(eroot - NECK), "Head", R, ao_group="head", gradient=0.0))
    ak.add_face(sc, P, head, "Head", NECK, EYE_X, EYE_Y, EYE_R, (0.0, 0.480), lid_mat=P + "Patch",
                mouth_mesh=muzzle, mouth_scale=1.1, eye_sink=0.06, eye_fwd=0.55)
    marks = ak.alert_marks(P + "Alert", r0=0.06, length=0.09, width=0.026)
    sc.add(mk.Part("Fx_Alert", marks, tuple(np.array([-0.30, 0.97, 0.40]) - NECK), "Head", mk.rot_z(40 * DEG),
                   visible=False, ao_group="fx", gradient=0.0))
    for nm, (x, z) in LEGS.items():
        sc.add(mk.Part("Leg_" + nm, ak.leg(0.076, 0.090, 0.105, LEG_TOP, P + "Coat", P + "Hoof", seg=24),
                       tuple(np.array([x, LEG_TOP, z]) - BODY_C), "Body", ao_group="legs", ao_receive=("body",),
                       gradient=0.0))
    troot = np.array([0.0, 0.66, BODY_C[2] - BODY_R[2] + 0.035])
    sc.add(mk.Part("Tail", build_tail(), tuple(troot - BODY_C), "Body", ao_group="tail", ao_receive=("body",),
                   gradient=0.0))
    return sc


def main():
    ak.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
