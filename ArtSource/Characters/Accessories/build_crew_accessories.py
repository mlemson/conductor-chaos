"""Crew-accessoires: losse extraatjes die een speler in de briefingruimte pakt en meteen opzet.

Alle accessoires staan als kind van een lege `Body`-knoop op dezelfde plek als op het personage (conducteur-lijf,
gedeeld door alle crew-skins). CrewOutfit in Unity kopieert de lokale stand van `Body/Acc_*` naar de Body van de
speler; dezelfde meshes liggen als pakbare props in de briefingruimte.

  * Acc_Sunglasses  ronde zonnebril met donkere glazen (gezicht)
  * Acc_Mustache    flinke nepsnor (gezicht)
  * Acc_TopHat      hoge hoed met rode band (hoofd; vervangt pet/helm/muts)
  * Acc_StrawHat    strooien hoed (canotier) met marine band (hoofd)
  * Acc_Bowtie      rode vlinderdas met stippen (hals)

Uitvoer: Assets/Art/Models/Characters/CrewAccessories/CrewAccessories.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/Accessories/build_crew_accessories.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "CrewAccessories"
P = "M_CrewAcc_"
HEAD_Y = 0.905                    # onderkant van de pet van de conducteur (wereld)


def materials(sc):
    ck.material(sc, P + "Frame", "ink", 0.10, 0.40)
    ck.material(sc, P + "Lens", "ui_bg", -0.10, 0.85, spec=0.6, rim=0.35)
    ck.material(sc, P + "Gold", "gold_trim", 0.05, 0.40)
    ck.material(sc, P + "Felt", "ui_bg", 0.05, 0.15, paint=1.1)
    ck.material(sc, P + "FeltDark", "ui_bg", -0.25, 0.15)
    ck.material(sc, P + "Red", "title_red", 0.0, 0.25, paint=1.1)
    ck.material(sc, P + "RedDark", "title_red", -0.30, 0.25)
    ck.material(sc, P + "Straw", "crate", 0.30, 0.15, paint=1.3)
    ck.material(sc, P + "StrawDark", "crate", 0.00, 0.15)
    ck.material(sc, P + "Navy", "uniform_navy", 0.0, 0.22)
    ck.material(sc, P + "Cream", "parchment", 0.30, 0.25)
    ck.material(sc, P + "Hair", "hair_brown", 0.0, 0.20)


def head_halfwidth(y):
    return bc.BODY_R * bc.body_radius(y)


# ------------------------------------------------------------------ gezicht
def sunglasses():
    """Wereldcoördinaten (op het lijf). Ronde glazen net voor de ogen, brug en pootjes naar de zijkant."""
    parts, inner, outer = [], {}, {}
    for side in (1, -1):
        pos, Rf = ck.eye_frame(side)
        c_local = np.array([0.0, -0.004, 0.082])
        loop = [(0.098 * math.cos(a), 0.088 * math.sin(a), 0.0) for a in np.linspace(0, 2 * math.pi, 41)]
        parts.append(ck.tube(loop, 0.013, P + "Frame", smooth=False, seg=8).transform(R=Rf, t=pos + Rf @ c_local))
        lens = mk.ellipsoid(0.094, 0.084, 0.018, 28, 8, mat=P + "Lens")
        parts.append(lens.transform(R=Rf, t=pos + Rf @ (c_local + np.array([0, 0, -0.002]))))
        inner[side] = pos + Rf @ (c_local + np.array([-side * 0.098, 0.012, 0.0]))
        outer[side] = pos + Rf @ (c_local + np.array([side * 0.098, 0.014, -0.006]))
    zb, _ = bc.body_surface(0.0, bc.EYE_Y + 0.01)
    parts.append(ck.tube([inner[1], np.array([0.0, bc.EYE_Y + 0.030, zb + 0.070]), inner[-1]], 0.011, P + "Frame", n=14))
    for side in (1, -1):
        S, N = ck.surface(np.array([side * 64 * DEG, side * 90 * DEG]), np.array([bc.EYE_Y + 0.01, bc.EYE_Y + 0.01]))
        parts.append(ck.tube([outer[side], S[0] + N[0] * 0.014, S[1] + N[1] * 0.008], 0.009, P + "Frame", n=12))
    return mk.merge(*parts)


def mustache():
    """Krulsnor boven de mond (wereldcoördinaten)."""
    y = bc.MOUTH_Y + 0.052
    parts = []
    for side in (1, -1):
        pts = []
        for t in np.linspace(0, 1, 9):
            x = side * (0.006 + 0.105 * t)
            yy = y + 0.010 * math.sin(t * math.pi) - 0.020 * t + 0.045 * max(0.0, t - 0.75) ** 1.5 * 8
            pts.append((x, yy))
        P3 = []
        for i, (x, yy) in enumerate(pts):
            z, n = bc.body_surface(x, yy)
            r = 0.024 * (1 - 0.65 * (i / 8) ** 1.4)
            P3.append((np.array([x, yy, z]) + n * (r + 0.004), r))
        for (a, ra), (b, rb) in zip(P3[:-1], P3[1:]):
            parts.append(mk.capsule_between(a, b, ra, rb, 10, 3, mat=P + "Hair"))
    return mk.merge(*parts)


def bowtie():
    """Vlinderdas op de kraag (wereldcoördinaten)."""
    y = bc.Y_COLLAR - 0.020
    z, n = bc.body_surface(0.0, y)
    R = bc.face_frame(n)
    wing = mk.ellipsoid(0.068, 0.046, 0.020, 18, 8, mat=P + "Red")
    parts = []
    for side in (1, -1):
        parts.append(wing.transform(R=mk.rot_z(side * 0.10), t=(side * 0.060, 0.0, 0.0)))
        for k, (dx, dy) in enumerate(((0.050, 0.014), (0.080, -0.012), (0.045, -0.020))):
            parts.append(mk.ellipsoid(0.010, 0.010, 0.006, 10, 5, mat=P + "Cream")
                         .transform(t=(side * dx, dy, 0.017)))
    parts.append(mk.ellipsoid(0.024, 0.030, 0.024, 14, 7, mat=P + "RedDark"))
    tie = mk.merge(*parts)
    return tie.transform(R=R, t=np.array([0.0, y, z]) + n * 0.026)


# ------------------------------------------------------------------ hoofd (lokaal: oorsprong = onderkant band)
def top_hat():
    a = head_halfwidth(HEAD_Y + 0.03) + 0.030
    b = a * bc.BODY_DEPTH + 0.015
    H = 0.30
    crown = mk.lathe([(0.0, 0.0), (1.0, 0.0), (1.0, 0.02), (0.97, H * 0.45), (1.03, H * 0.92), (1.04, H),
                      (0.98, H + 0.012), (0.0, H + 0.012)], 36, a, b, 2.4, mat=P + "Felt")
    band = mk.lathe([(0.0, 0.028), (1.012, 0.028), (1.022, 0.036), (1.022, 0.082), (1.012, 0.090), (0.0, 0.090)],
                    36, a, b, 2.4, mat=P + "Red")
    brim = mk.lathe([(1.0, -0.010), (1.42, -0.004), (1.50, 0.012), (1.44, 0.020), (1.0, 0.014)], 36, a, b, 2.4,
                    closed=True, mat=P + "FeltDark")
    buckle = ck.squircle_ring(0.060, 0.046, 0.007, P + "Gold").transform(t=(0.0, 0.059, b * 1.022 + 0.006))
    return mk.merge(crown, band, brim, buckle)


def straw_hat():
    a = head_halfwidth(HEAD_Y + 0.02) + 0.032
    b = a * bc.BODY_DEPTH + 0.018
    H = 0.120
    crown = mk.lathe([(0.0, 0.0), (1.0, 0.0), (1.0, H), (0.97, H + 0.010), (0.0, H + 0.012)], 40, a, b, 2.3,
                     mat=P + "Straw")
    band = mk.lathe([(0.0, 0.016), (1.015, 0.016), (1.025, 0.024), (1.025, 0.066), (1.015, 0.074), (0.0, 0.074)],
                    40, a, b, 2.3, mat=P + "Navy")
    brim = mk.lathe([(1.0, -0.006), (1.62, -0.012), (1.74, -0.002), (1.70, 0.012), (1.0, 0.010)], 40, a, b, 2.3,
                    closed=True, mat=P + "StrawDark")
    # gevlochten ringen op de kroon
    rings = [mk.lathe([(1.004, y - 0.004), (1.012, y), (1.004, y + 0.004)], 40, a, b, 2.3, closed=True,
                      mat=P + "StrawDark") for y in (0.090, 0.106)]
    flower = []
    for k in range(5):
        ang = 2 * math.pi * k / 5
        flower.append(mk.ellipsoid(0.020, 0.014, 0.006, 10, 5, mat=P + "Cream")
                      .transform(R=mk.rot_z(ang), t=(0.016 * math.cos(ang), 0.016 * math.sin(ang), 0)))
    flower.append(mk.ellipsoid(0.010, 0.010, 0.008, 10, 5, mat=P + "Gold"))
    phi = 52 * DEG
    fl = mk.merge(*flower).transform(R=ck.frame_at(np.array([math.sin(phi), 0.0, math.cos(phi)])),
                                     t=(a * 1.03 * math.sin(phi) ** (2 / 2.3), 0.045, b * 1.03 * math.cos(phi) ** (2 / 2.3)))
    return mk.merge(crown, band, brim, *rings, fl)


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    sc.add(mk.Part(ASSET, None))
    sc.add(mk.Part("Body", None, tuple(ck.HIP), ASSET))
    for name, mesh in (("Acc_Sunglasses", sunglasses()), ("Acc_Mustache", mustache()), ("Acc_Bowtie", bowtie())):
        sc.add(mk.Part(name, mesh.transform(t=-ck.HIP), (0, 0, 0), "Body", ao_group=name, gradient=0.0))
    head = (0.0, HEAD_Y - bc.HIP_Y, 0.0)
    sc.add(mk.Part("Acc_TopHat", top_hat(), head, "Body", mk.rot_x(-6 * DEG) @ mk.rot_z(-5 * DEG),
                   ao_group="tophat", gradient=0.0))
    sc.add(mk.Part("Acc_StrawHat", straw_hat(), (0.0, HEAD_Y + 0.010 - bc.HIP_Y, 0.0), "Body",
                   mk.rot_x(-8 * DEG) @ mk.rot_z(6 * DEG), ao_group="strawhat", gradient=0.0))
    return sc


def main():
    ck.finish(build_scene(), ASSET, bake=dict(density=3500, radius=0.06, noise_freq=5.0, edge_range=(14.0, 45.0)))


if __name__ == "__main__":
    main()
