"""Conducteur — basismodel (P0 art proof, issue #9).

Bouwt het conducteurpersonage parametrisch uit de maten van de conceptafbeelding
(`Docs/References/conductor-chaos-concept.png`, 'The Crew', conducteur links) en
kleurt het met de gemeten ramps uit `Docs/References/palette.json`.

Gemeten in het concept (pixels, conducteur 190 px hoog incl. pet):
  pet-top 803 · oogmidden 866 · mondmidden 897 · kraag 913 · schoudel ~920
  knopen 930/945 · jasonderkant ~970 · schoenzool 993 · lijfbreedte ~83 px
  ogen Ø ~32 px en raken elkaar · handen Ø ~28 px · petkroon ~1,3x lijfbreedte
Schaal: totale hoogte 1,25 m -> 1 px ≈ 6,6 mm.

Uitvoer (relatief aan de repo-root):
  Assets/Art/Models/Characters/Conductor/Conductor.fbx   Unity (FBX 7.4 ASCII)
  ArtSource/Exports/Conductor.glb                        Blender/preview
  ArtSource/Exports/Conductor.materials.json             ramps per materiaal

Gebruik:  python ArtSource/Characters/Conductor/build_conductor.py
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import meshkit as mk  # noqa: E402
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

# ------------------------------------------------------------------ maten (m)
H_TOTAL = 1.25
BODY_R = 0.28           # halve lijfbreedte
BODY_DEPTH = 0.86       # diepte t.o.v. breedte (iets platte voorkant)
BODY_N = 2.6            # superellips-doorsnede: zacht vierkant, 'speelgoed'
Y_BOTTOM = 0.15         # onderkant jas
Y_DOME = 0.80           # begin van de koepel
Y_TOP = 1.02            # kruin (zit onder de pet)
Y_COLLAR = 0.53         # grens huid/uniform
JACKET_STEP = 0.030     # hoogte van de jasrand-overgang (schouderrand)
HEAD_R = 0.97           # gezicht iets smaller dan de jas
JACKET_FLARE = 0.08     # jas loopt naar de zoom 8% verder uit
EYE_Y, EYE_R, EYE_X = 0.812, 0.108, 0.100
EYE_DEPTH = 0.55         # bolling van het oog (t.o.v. straal)
MOUTH_Y = 0.625
SHOULDER = (0.250, 0.468, 0.0)   # mouw komt hoog en dik uit de bovenkant van de jas (concept)
HAND_OFFSET = 0.215
CROWN_BULGE = 0.20      # kroon ≈ 1,2x de petrand (concept: ronde, bolle kroon — geen platte schijf)
HIP_Y = 0.20
LEG_X = 0.128

SEG = 32                # omtrek-segmenten lijf (rond silhouet, nog steeds low-poly)


def body_radius(y):
    """Straalfactor van het lijf op hoogte y (1.0 = BODY_R)."""
    if y >= Y_DOME:
        t = min(1.0, (y - Y_DOME) / (Y_TOP - Y_DOME))
        return max(0.0, (1 - t ** 2.4)) ** (1 / 2.4) * HEAD_R
    if y >= Y_COLLAR:
        return HEAD_R                                  # hoofd/gezicht
    if y >= Y_COLLAR - JACKET_STEP:                    # jasrand: zachte overgang = schouders
        t = (Y_COLLAR - y) / JACKET_STEP
        return HEAD_R + (1.0 - HEAD_R) * math.sin(t * math.pi / 2)
    # jas: klokvorm, duidelijk wijder naar de zoom (concept: jas ≈ 1,2x zo breed als het gezicht)
    t = 1 - (y - Y_BOTTOM) / (Y_COLLAR - JACKET_STEP - Y_BOTTOM)
    return 1.0 + JACKET_FLARE * max(0.0, t) ** 1.2


def body_surface(x, y):
    """z-coördinaat van de voorkant van het lijf op (x, y) + oppervlaktenormaal."""
    a = BODY_R * body_radius(y)
    b = a * BODY_DEPTH
    u = min(abs(x) / a, 0.999)
    z = b * (1 - u ** BODY_N) ** (1 / BODY_N)
    n = np.array([np.sign(x) * u ** (BODY_N - 1) / a, 0.0, (z / b) ** (BODY_N - 1) / b])
    # helling in y meenemen (koepel)
    dy = 0.004
    a2 = BODY_R * body_radius(y + dy)
    z2 = a2 * BODY_DEPTH * (1 - min(abs(x) / a2, 0.999) ** BODY_N) ** (1 / BODY_N)
    n[1] = -(z2 - z) / dy * n[2]
    return z, n / np.linalg.norm(n)


def face_frame(nrm):
    """Rotatiematrix die lokale +Z op de oppervlaktenormaal legt (Y blijft ~omhoog)."""
    z = nrm / np.linalg.norm(nrm)
    x = np.cross([0, 1, 0], z)
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], axis=1)


# ------------------------------------------------------------------- onderdelen
def build_body():
    prof = [(0.0, Y_BOTTOM)]
    fil = 0.07
    for k in range(0, 6):
        th = math.pi / 2 * k / 5
        r = (BODY_R - fil + fil * math.sin(th)) / BODY_R
        y = Y_BOTTOM + fil - fil * math.cos(th)
        prof.append((r * body_radius(y) / body_radius(Y_BOTTOM + fil), y))
    ys = list(np.linspace(Y_BOTTOM + fil + 0.04, Y_DOME, 16)) + [Y_COLLAR, Y_COLLAR + 0.012] + \
        [Y_COLLAR - JACKET_STEP * k / 4 for k in range(1, 5)]
    ys = sorted(set(round(v, 4) for v in ys))
    prof += [(body_radius(y), y) for y in ys]
    for k in range(1, 9):
        y = Y_DOME + (Y_TOP - Y_DOME) * (k / 8) ** 0.8
        prof.append((body_radius(y) if k < 8 else 0.0, y))
    m = mk.lathe(prof, SEG, BODY_R, BODY_R * BODY_DEPTH, BODY_N, mat="M_Conductor_Skin")
    # materiaalsplitsing op de kraaglijn
    m.M = ["M_Conductor_Uniform" if m.V[list(f)][:, 1].mean() < Y_COLLAR else "M_Conductor_Skin" for f in m.F]
    # jas-split tussen de benen: klein inkepinkje aan de voorkant is niet nodig in low-poly; benen tonen het al
    return m


def conform(mesh, lift=0.003):
    """Leg een dun, plat (XY) onderdeel tegen de voorkant van het lijf: z_lokaal + oppervlakte-z."""
    out = mesh.copy()
    for i, (x, y, z) in enumerate(out.V):
        zs, _ = body_surface(x, y)
        out.V[i, 2] = zs + lift + z
    return out


def build_collar():
    """Kraag (concept): smalle jasrand rond de hals + twee platte revers die in een V samenkomen."""
    a = BODY_R * body_radius(Y_COLLAR)
    roll = mk.torus(1.0, 0.011, SEG, 6, mat="M_Conductor_Uniform", a=a + 0.002, b=a * BODY_DEPTH + 0.002, n=BODY_N)
    roll = roll.transform(t=(0, Y_COLLAR + 0.002, 0))
    parts = [roll]
    v_y = Y_COLLAR - 0.075          # punt van de V
    for sx in (-1, 1):
        ang = math.atan2(0.085, 0.105)
        lap = mk.rounded_box(0.150, 0.072, 0.022, 0.014, 2, mat="M_Conductor_Uniform").transform(t=(0, 0, 0.011))
        lap = lap.transform(R=mk.rot_z(sx * ang), t=(sx * 0.058, v_y + 0.040, 0.0))
        parts.append(conform(lap, 0.004))
    return mk.merge(*parts)


def build_buttons():
    parts = []
    for y in (0.425, 0.325):
        z, nrm = body_surface(0.0, y)
        b = mk.ellipsoid(0.022, 0.022, 0.011, 12, 6, mat="M_Conductor_Gold")
        parts.append(b.transform(R=face_frame(nrm), t=(0, y, z)))
    return mk.merge(*parts)


def build_eye():
    """Oogwit: licht verticale, bolle ellipsoïde."""
    return mk.ellipsoid(EYE_R, EYE_R * 1.08, EYE_R * EYE_DEPTH, 24, 12, mat="M_Conductor_EyeWhite")


def build_pupil(side):
    """Pupil + glimlicht, op het oogoppervlak, licht naar binnen kijkend (zoals concept)."""
    px, py = -side * 0.022, 0.006
    ez = EYE_R * EYE_DEPTH * math.sqrt(max(0.0, 1 - (px / EYE_R) ** 2 - (py / (EYE_R * 1.08)) ** 2))
    pupil = mk.ellipsoid(0.034, 0.048, 0.012, 14, 8, mat="M_Conductor_Pupil").transform(t=(px, py, ez - 0.002))
    gx, gy = px + 0.011, py + 0.016
    gz = EYE_R * EYE_DEPTH * math.sqrt(max(0.0, 1 - (gx / EYE_R) ** 2 - (gy / (EYE_R * 1.08)) ** 2))
    glint = mk.ellipsoid(0.0075, 0.0075, 0.004, 8, 4, mat="M_Conductor_EyeWhite").transform(t=(gx, gy, gz + 0.006))
    return mk.merge(pupil, glint)


def build_mouth(kind):
    if kind == "Neutral":   # kleine, rustige ovale mond
        inner = mk.ellipsoid(0.040, 0.020, 0.014, 16, 8, mat="M_Conductor_Mouth")
        return inner
    if kind == "Happy":     # brede, open lach met tong
        inner = mk.ellipsoid(0.078, 0.046, 0.020, 20, 10, mat="M_Conductor_Mouth").transform(t=(0, -0.006, 0))
        tongue = mk.ellipsoid(0.042, 0.020, 0.012, 14, 6, mat="M_Conductor_Tongue").transform(t=(0, -0.030, 0.010))
        return mk.merge(inner, tongue)
    # Surprised (concept): grote open O-mond met tong
    inner = mk.ellipsoid(0.088, 0.094, 0.026, 20, 10, mat="M_Conductor_Mouth")
    tongue = mk.ellipsoid(0.058, 0.034, 0.016, 14, 6, mat="M_Conductor_Tongue").transform(t=(0.006, -0.052, 0.012))
    return mk.merge(inner, tongue)


def build_cap():
    """Pet (concept): navy petrand op het hoofd, smalle gouden streep, ronde bolle kroon die naar achter
    afloopt, grote donkere klep naar voren en een ovale gouden knop op de voorkant van de kroon.
    Lokale oorsprong = onderkant petrand, midden van het hoofd."""
    base_a = BODY_R * 1.0 + 0.010
    base_b = base_a * BODY_DEPTH + 0.02
    BASE_H, GOLD_H = 0.050, 0.030
    # navy petrand + gouden streep
    base = mk.lathe([(0.0, 0.0), (0.97, 0.0), (1.0, 0.008), (1.0, BASE_H), (0.0, BASE_H)],
                    SEG, base_a, base_b, 2.3, mat="M_Conductor_UniformDark")
    gold = mk.lathe([(0.0, BASE_H - 0.002), (1.012, BASE_H - 0.002), (1.02, BASE_H + 0.006),
                     (1.02, BASE_H + GOLD_H - 0.006), (1.012, BASE_H + GOLD_H), (0.0, BASE_H + GOLD_H)],
                    SEG, base_a, base_b, 2.3, mat="M_Conductor_Gold")
    # kroon: ronde bol (geen platte schijf), loopt over de streep heen uit tot ~1,2x en is bovenop gewelfd
    y0 = BASE_H + GOLD_H - 0.004
    prof = [(0.0, y0), (1.0, y0)]
    for k in range(1, 7):
        t = k / 6
        prof.append((1.0 + CROWN_BULGE * math.sin(t * math.pi / 2), y0 + 0.07 * t))
    top = 1.0 + CROWN_BULGE
    for k in range(1, 11):
        th = math.pi / 2 * k / 10
        prof.append((top * math.cos(th) ** 0.75 if k < 10 else 0.0, y0 + 0.07 + 0.16 * math.sin(th) ** 0.85))
    crown = mk.lathe(prof, SEG, base_a, base_b * 1.06, 2.15, mat="M_Conductor_Uniform")
    # voorkant van de kroon hoger dan de achterkant (klassieke 'peaked cap')
    V = crown.V
    lift = np.clip(V[:, 2] / (base_b * top), -1, 1)
    V[:, 1] = np.where(V[:, 1] > y0 + 0.02, V[:, 1] + 0.05 * lift * (V[:, 1] - y0) / 0.23, V[:, 1])
    V[:, 2] += 0.015
    # klep: groot, donker, naar voren en licht omlaag
    brim = mk.ellipsoid(base_a * 1.02, 0.022, 0.235, 30, 6, mat="M_Conductor_UniformDark", n=2.2)
    brim = brim.transform(R=mk.rot_x(math.radians(20)), t=(0, 0.004, base_b - 0.03))
    # ovale gouden knop op de voorkant van de kroon, net boven de goudstreep
    zc = base_b * 1.06 * (1.0 + CROWN_BULGE * math.sin(0.5 * math.pi / 2)) + 0.015
    badge = mk.ellipsoid(0.064, 0.042, 0.030, 20, 10, mat="M_Conductor_Gold", n=2.3)
    badge = badge.transform(R=mk.rot_x(math.radians(-22)), t=(0, y0 + 0.042, zc - 0.012))
    return mk.merge(base, gold, crown, brim, badge)


def build_arm():
    """Mouw vanaf schouder-pivot; wijst in rust langs lokale -Y."""
    sleeve = mk.capsule_between((0, 0.03, 0), (0, -0.150, 0), 0.088, 0.082, 18, 4, mat="M_Conductor_Uniform")
    return sleeve


def build_hand():
    return mk.ellipsoid(0.108, 0.110, 0.100, 20, 12, mat="M_Conductor_Skin")


def build_key():
    """Conducteurssleutel (prop, standaard verborgen): ring in de hand, schacht langs lokale -Y."""
    ring = mk.torus(0.030, 0.010, 16, 6, mat="M_Conductor_Gold").transform(R=mk.rot_x(math.pi / 2), t=(0, 0.0, 0.0))
    shaft = mk.capsule_between((0, -0.03, 0), (0, -0.19, 0), 0.011, 0.011, 8, 2, mat="M_Conductor_Gold")
    bit = mk.rounded_box(0.035, 0.03, 0.012, 0.005, 1, mat="M_Conductor_Gold").transform(t=(0.02, -0.17, 0))
    return mk.merge(ring, shaft, bit).transform(t=(0, -0.02, 0.05))


def build_leg():
    """Dik, kort beentje + ronde 'kiezel'-voet met platte zool (concept); pivot = heup (lokale y=0 op HIP_Y)."""
    leg = mk.capsule_between((0, 0.10 - HIP_Y, 0), (0, 0.25 - HIP_Y, 0), 0.100, 0.100, 16, 3, mat="M_Conductor_Uniform")
    foot = mk.ellipsoid(0.126, 0.086, 0.158, 20, 10, mat="M_Conductor_Shoe", n=2.5).transform(t=(0, 0.066 - HIP_Y, 0.035))
    foot.V[:, 1] = np.maximum(foot.V[:, 1], -HIP_Y)   # platte zool op de grond
    return mk.merge(leg, foot)


# ----------------------------------------------------------------------- scène
def build_scene(arm_angle_deg=66.0):
    sc = mk.Scene("Conductor")
    R_ = RAMPS
    sc.material("M_Conductor_Skin", R_["skin"][2], R_["skin"], 0.30, opts={"paint": 1.1})
    sc.material("M_Conductor_Uniform", R_["uniform_navy"][2], R_["uniform_navy"], 0.22, opts={"paint": 1.3})
    sc.material("M_Conductor_UniformDark", R_["uniform_navy"][1], R_["uniform_navy"], 0.22, ramp_shift=-0.22)
    sc.material("M_Conductor_Gold", R_["gold_trim"][2], R_["gold_trim"], 0.40)
    sc.material("M_Conductor_EyeWhite", R_["wool"][3], R_["wool"], 0.35, ramp_shift=0.30,
                opts={"flat": 0.45, "noShadow": True})
    sc.material("M_Conductor_Pupil", R_["face"][0], R_["ink"], 0.45, ramp_shift=-0.30, opts={"noShadow": True})
    sc.material("M_Conductor_Mouth", R_["mouth"][1], R_["mouth"], 0.20, ramp_shift=-0.55)
    sc.material("M_Conductor_Tongue", R_["mouth"][3], R_["mouth"], 0.25, ramp_shift=0.05)
    sc.material("M_Conductor_Shoe", R_["ui_bg"][2], R_["ui_bg"], 0.25)

    hip = np.array([0.0, HIP_Y, 0.0])
    sc.add(mk.Part("Conductor", None))
    sc.add(mk.Part("Body", build_body().transform(t=-hip), tuple(hip), "Conductor"))
    sc.add(mk.Part("Collar", build_collar().transform(t=-hip), (0, 0, 0), "Body"))
    sc.add(mk.Part("Buttons", build_buttons().transform(t=-hip), (0, 0, 0), "Body"))

    for side, nm in ((1, "L"), (-1, "R")):
        x = side * EYE_X
        z, nrm = body_surface(x, EYE_Y)
        Rf = face_frame(nrm)
        eye_pos = np.array([x, EYE_Y, z - EYE_R * 0.20]) - hip
        sc.add(mk.Part("Eye_" + nm, build_eye(), tuple(eye_pos), "Body", Rf))
        sc.add(mk.Part("Pupil_" + nm, build_pupil(side), (0, 0, 0), "Eye_" + nm))

    zm, nm_ = body_surface(0.0, MOUTH_Y)
    for kind in ("Neutral", "Surprised", "Happy"):
        sc.add(mk.Part("Mouth_" + kind, build_mouth(kind), (0, MOUTH_Y - HIP_Y, zm - 0.004), "Body",
                       face_frame(nm_), visible=(kind == "Neutral")))

    cap_pivot = (0.0, 0.912 - HIP_Y, 0.0)
    sc.add(mk.Part("Cap", build_cap(), cap_pivot, "Body", mk.rot_x(math.radians(-15)) @ mk.rot_z(math.radians(8))))

    for side, nm in ((1, "L"), (-1, "R")):
        sh = np.array([side * SHOULDER[0], SHOULDER[1], SHOULDER[2]]) - hip
        Ra = mk.rot_z(side * math.radians(arm_angle_deg))
        sc.add(mk.Part("Arm_" + nm, build_arm(), tuple(sh), "Body", Ra, ao_group="arm_" + nm, gradient=0.0))
        sc.add(mk.Part("Hand_" + nm, build_hand(), (0, -HAND_OFFSET, 0.0), "Arm_" + nm, ao_group="arm_" + nm))
    sc.add(mk.Part("Prop_Key", build_key(), (0, 0, 0), "Hand_L", visible=False))

    for side, nm in ((1, "L"), (-1, "R")):
        sc.add(mk.Part("Leg_" + nm, build_leg(), (side * LEG_X, HIP_Y, 0.0), "Conductor",
                       ao_group="leg_" + nm, ao_receive=("body",), gradient=0.25))
    # ogen en pupillen blijven helder (concept): eigen AO-groep zonder occluders
    for p in sc.parts:
        if p.name.startswith(("Eye_", "Pupil_")):
            p.ao_group, p.gradient = "eyes", 0.0
        elif p.name in ("Body", "Collar", "Buttons", "Cap") or p.name.startswith("Mouth_"):
            p.ao_group = "body"
        if p.name == "Prop_Key":
            p.ao_group, p.gradient = "key", 0.0
    sc.bake_shading(density=4500, radius=0.10, noise_freq=5.0, edge_range=(14.0, 45.0))
    return sc


def main():
    sc = build_scene()
    fbx_dir = os.path.join(ROOT, "Assets", "Art", "Models", "Characters", "Conductor")
    exp_dir = os.path.join(ROOT, "ArtSource", "Exports")
    os.makedirs(fbx_dir, exist_ok=True)
    os.makedirs(exp_dir, exist_ok=True)
    sc.write_fbx_ascii(os.path.join(fbx_dir, "Conductor.fbx"))
    sc.write_glb(os.path.join(exp_dir, "Conductor.glb"))
    unity_assets.export_unity(ROOT, sc, "Assets/Art/Models/Characters/Conductor/Conductor.fbx", "Assets/Art/Materials/Characters/Conductor")
    mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"], "opts": v["opts"]}
            for k, v in sc.materials.items()}
    json.dump({"model": "Conductor", "height_m": H_TOTAL, "stats": sc.stats(), "materials": mats},
              open(os.path.join(exp_dir, "Conductor.materials.json"), "w", encoding="utf-8"), indent=2)
    # bounding box in wereldruimte
    pts = []
    for p in sc.parts:
        if p.mesh is None or not p.visible:
            continue
        M = sc.world_matrix(p)
        pts.append(p.mesh.V @ M[:3, :3].T + M[:3, 3])
    P = np.vstack(pts)
    print("stats", sc.stats())
    print("bbox min", P.min(0).round(3), "max", P.max(0).round(3))


if __name__ == "__main__":
    main()
