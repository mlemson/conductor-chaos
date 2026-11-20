"""Stoker — derde speelbare crew-skin (naast conducteur en spoormonteur), naar 'The Crew' op het conceptbord.

Referentie: Docs/References/conductor-chaos-concept.png (derde crewlid: paarse muts, rode sjaal).
Kenmerken (silhouet eerst):
  * paarse gebreide muts met omgeslagen rand, ribbels en een pompon
  * dikke rode gebreide sjaal om de hals met een hangende punt en franje
  * crème gebreide trui met geribde zoom en manchetten, donkere werkbroek, bruine laarzen
  * roetvegen op de wang (stoker schept kolen), leren werkhandschoenen
Kleuren: ramps beanie, scarf, cloth_dark (crème trui en donkere broek), boot_brown, leather.

Zelfde lijf en ankers als de conducteur (Body, Arm_L/R, Hand_L/R, Leg_L/R), zodat CrewOutfit in Unity
de onderdelen kan wisselen zonder dat de physics-rig verandert.

Uitvoer: Assets/Art/Models/Characters/Stoker/Stoker.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/Stoker/build_stoker.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "Stoker"
P = "M_Stoker_"
GLOVE_CUT = -0.140
HEM_Y = 0.300                     # onderkant trui


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Sweater", "cloth_dark", 0.32, 0.20, paint=1.2)
    ck.material(sc, P + "SweaterRib", "cloth_dark", 0.18, 0.20)
    ck.material(sc, P + "Trousers", "cloth_dark", -0.38, 0.22)
    ck.material(sc, P + "Boot", "boot_brown", -0.10, 0.25)
    ck.material(sc, P + "Glove", "leather", 0.0, 0.25, paint=1.1)
    ck.material(sc, P + "Beanie", "beanie", 0.0, 0.18, paint=1.2)
    ck.material(sc, P + "BeanieDark", "beanie", -0.30, 0.18)
    ck.material(sc, P + "Scarf", "scarf", 0.05, 0.18, paint=1.2)
    ck.material(sc, P + "ScarfDark", "scarf", -0.28, 0.18)
    ck.material(sc, P + "Soot", "ui_bg", 0.05, 0.10)


# ---------------------------------------------------------------------- kleding
def build_sweater():
    parts = []
    # geribde zoom: dikke rand rond de heupen
    hem = [(0.0, HEM_Y - 0.004), (0.014, HEM_Y - 0.002), (0.018, HEM_Y + 0.012), (0.018, HEM_Y + 0.040),
           (0.014, HEM_Y + 0.050), (0.0, HEM_Y + 0.052)]
    parts.append(ck.surface_lathe(hem, P + "SweaterRib", seg=56))
    # breiribbels in de zoom (staande streepjes), net uit de rand
    for k in range(28):
        th = 2 * math.pi * k / 28
        S, N = ck.surface(np.array([th, th]), np.array([HEM_Y + 0.006, HEM_Y + 0.044]))
        parts.append(ck.tube(S + N * 0.019, 0.0045, P + "Sweater", n=4, seg=6, smooth=False))
    # twee ronde elleboogstukken zijn er niet; wel een borstzak met een kolenkrijtje
    pocket = ck.plate([(-0.045, -0.040), (0.045, -0.040), (0.043, 0.036), (-0.043, 0.036)], 0.009,
                      P + "SweaterRib", chamfer=0.003)
    parts.append(ck.wrap(pocket, 14 * DEG, 0.415, 0.0))
    return mk.merge(*parts)


def build_scarf():
    loop = []
    for k in range(18):
        a = 2 * math.pi * k / 18
        loop.append((0.026 + 0.054 * math.cos(a), 0.540 + 0.060 * math.sin(a)))
    ring = ck.surface_lathe(loop, P + "Scarf", seg=60)
    # twee donkere breistrepen om de lus
    stripes = []
    for y in (0.512, 0.568):
        band = [(0.0, y - 0.008), (0.072, y - 0.007), (0.084, y), (0.072, y + 0.007), (0.0, y + 0.008)]
        stripes.append(ck.surface_lathe(band, P + "ScarfDark", seg=60))
    # hangende punt voor op de linkerborst (+X), iets wapperend, met donkere band en franje
    tail = ck.strap([(18 * DEG, 0.520), (22 * DEG, 0.440), (25 * DEG, 0.350)], 0.108, 0.032, P + "Scarf",
                    lift=0.030, expo=6.0)
    tail_band = ck.strap([(23.2 * DEG, 0.395), (24.0 * DEG, 0.378)], 0.112, 0.036, P + "ScarfDark", lift=0.030,
                         expo=6.0, n=8)
    fringe = []
    for k in range(5):
        th = (25 + (k - 2) * 3.4) * DEG
        S, N = ck.surface(np.array([th, th]), np.array([0.352, 0.305]))
        fringe.append(ck.tube(S + N * 0.046, 0.0090, P + "Scarf", n=6, seg=8))
    return mk.merge(ring, *stripes, tail, tail_band, *fringe)


def build_soot():
    """Twee roetvegen: op de rechterwang en boven het linkeroog. Plat geschilderd, net boven de huid."""
    parts = []
    for th, y, w, h, rot in ((-38 * DEG, 0.735, 0.034, 0.014, 0.35),):
        S, N = ck.surface(np.array([th]), np.array([y]))
        smear = mk.ellipsoid(w, h, 0.004, 16, 6, mat=P + "Soot")
        parts.append(smear.transform(R=ck.frame_at(N[0], spin=rot), t=S[0] + N[0] * 0.001))
    return mk.merge(*parts)


# ----------------------------------------------------------------------- muts
def _head_r(y):
    return bc.body_radius(y)


def build_beanie():
    """Lokaal: oorsprong = onderkant omslag (wereld y 0,880), midden van het hoofd."""
    y0 = 0.880
    a, b, n = bc.BODY_R, bc.BODY_R * bc.BODY_DEPTH, 2.4
    # profiel = het hoofdprofiel 3,5 cm naar buiten verschoven (2D-normaal), plus een iets slappe kruin
    ys = np.linspace(y0 + 0.04, bc.Y_TOP, 40)
    C = np.array([(bc.BODY_R * _head_r(y), y) for y in ys])
    C[-1, 0] = 0.0
    T = np.gradient(C, axis=0)
    N = np.stack([T[:, 1], -T[:, 0]], 1)
    N /= np.linalg.norm(N, axis=1, keepdims=True)
    O = C + N * 0.036
    O = O[O[:, 0] > 0.004]
    top = float(O[:, 1].max()) + 0.025
    prof = [(0.0, 0.050), (O[0, 0] / bc.BODY_R, 0.050)]
    prof += [(x / bc.BODY_R, y - y0) for x, y in O[::2]]
    prof.append((0.0, top - y0))
    dome = mk.lathe(prof, 40, a, b, n, mat=P + "Beanie")
    # omgeslagen rand (dik, iets bol)
    r0 = _head_r(y0 + 0.03) + 0.135
    cuff = mk.lathe([(0.0, -0.004), (r0 - 0.02, -0.004), (r0, 0.006), (r0 + 0.012, 0.040), (r0, 0.074),
                     (r0 - 0.03, 0.082), (0.0, 0.082)], 40, a, b, n, mat=P + "BeanieDark")
    # staande ribbels op de omslag
    ribs = []
    for k in range(32):
        phi = 2 * math.pi * k / 32
        sx, cz = math.sin(phi), math.cos(phi)
        p0 = np.array([a * (r0 + 0.012) * mk.spow(sx, 2 / n), 0.012, b * (r0 + 0.012) * mk.spow(cz, 2 / n)])
        p1 = p0 + np.array([0, 0.058, 0])
        ribs.append(ck.tube([p0, p1], 0.0065, P + "BeanieDark", n=3, seg=6, smooth=False))
    # pompon van gebreide bolletjes
    top = top - y0 - 0.02
    pom = ck.tufts([((0.0, top + 0.055, -0.01), 0.068), ((0.03, top + 0.075, 0.01), 0.045),
                    ((-0.03, top + 0.070, 0.02), 0.045), ((0.0, top + 0.095, -0.02), 0.045)], P + "Beanie", seed=4)
    return mk.merge(dome, cuff, *ribs, pom)


def build_arm(side):
    arm = ck.build_arm(P + "Sweater", P + "Glove", cut=GLOVE_CUT, hand_scale=1.15, sleeve_bulge=1.06)
    cuff = mk.torus(0.084, 0.018, 32, 8, mat=P + "SweaterRib", a=1.0, b=0.88).transform(t=(0, GLOVE_CUT + 0.020, 0))
    return mk.merge(arm, cuff)


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Boot"), (ck.SHOE_Y, HEM_Y, P + "Trousers"),
                     (HEM_Y, ck.Y_COLLAR, P + "Sweater"), (ck.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Sweater", build_sweater())
    ck.add_detail(sc, "Scarf", build_scarf())
    ck.add_detail(sc, "Soot", build_soot(), ao_group="soot", gradient=0.0)
    ck.add_face(sc, P, default_mouth="Smile")
    sc.add(mk.Part("Beanie", build_beanie(), (0.0, 0.880 - bc.HIP_Y, 0.0), "Body", mk.rot_z(5 * DEG),
                   ao_group="body", gradient=0.0))
    ck.add_arms(sc, build_arm, {"L": ck.arm_pose(50, 0, side=1), "R": ck.arm_pose(50, 0, side=-1)})
    ck.add_leg_anchors(sc)
    return sc


def main():
    ck.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
