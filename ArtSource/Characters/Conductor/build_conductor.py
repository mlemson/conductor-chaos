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
import implicit_body  # noqa: E402
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

# ------------------------------------------------------------------ maten (m)
H_TOTAL = 1.25
BODY_R = 0.28           # halve lijfbreedte
BODY_DEPTH = 0.86       # diepte t.o.v. breedte (iets platte voorkant)
BODY_N = 2.6            # superellips-doorsnede: zacht vierkant, 'speelgoed'
Y_BOTTOM = 0.235        # hoogte van het kruis; broekspijpen vormen één mesh met het lijf
Y_DOME = 0.80           # begin van de koepel
Y_TOP = 1.02            # kruin (zit onder de pet)
Y_COLLAR = 0.53         # grens huid/uniform
EYE_Y, EYE_R, EYE_X = 0.835, 0.106, 0.099
EYE_DEPTH = 0.55         # bolling van het oog (t.o.v. straal)
MOUTH_Y = 0.632
SHOULDER = (0.240, 0.460, 0.0)   # bovenkant mouw sluit aan binnen de uniformrand
HIP_Y = 0.265
LEG_X = 0.162

SEG = 32                # omtrek-segmenten lijf (rond silhouet, nog steeds low-poly)


def body_radius(y):
    """Straalfactor van het lijf op hoogte y (1.0 = BODY_R)."""
    if y >= Y_DOME:
        t = min(1.0, (y - Y_DOME) / (Y_TOP - Y_DOME))
        return max(0.0, (1 - t ** 2.4)) ** (1 / 2.4) * 0.965
    return 1.0


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
    return implicit_body.make_body(BODY_R, BODY_DEPTH, Y_COLLAR)


def conform(mesh, lift=0.003):
    """Leg een dun, plat (XY) onderdeel tegen de voorkant van het lijf: z_lokaal + oppervlakte-z."""
    out = mesh.copy()
    for i, (x, y, z) in enumerate(out.V):
        zs, _ = body_surface(x, y)
        out.V[i, 2] = zs + lift + z
    return out


def build_neckline():
    """Warm huidkleurig V-halsje tussen de donkere revers, zoals in de concepttekening."""
    xy = [(-0.070, Y_COLLAR + 0.003), (0.0, Y_COLLAR - 0.057), (0.070, Y_COLLAR + 0.003)]
    vertices = np.array([(x, y, body_surface(x, y)[0] + 0.006) for x, y in xy])
    return mk.Mesh(vertices, [(0, 1, 2)], ['M_Conductor_Skin'])


def build_collar():
    """Vlakke revers langs de V-hals, zonder opstaande kraagrol."""
    parts=[]
    for side in (-1,1):
        xy=[(side*0.070,Y_COLLAR),(side*0.120,Y_COLLAR-0.036),
            (side*0.070,Y_COLLAR-0.064),(0.0,Y_COLLAR-0.057)]
        v=np.array([(x,y,body_surface(x,y)[0]+0.008) for x,y in xy])
        faces=[(0,1,2),(0,2,3)] if side < 0 else [(2,1,0),(3,2,0)]
        parts.append(mk.Mesh(v,faces,['M_Conductor_UniformDark']*2))
    return mk.merge(*parts)


def build_buttons():
    outline=[]
    for cy,start in ((0.420,0.0),(0.336,math.pi)):
        for theta in np.linspace(start,start+math.pi,13):
            outline.append((0.011*math.cos(theta),cy+0.011*math.sin(theta),0.0))
    v=np.array([(0,0.378,0)]+outline)
    f=[(0,i+1,(i+1)%len(outline)+1) for i in range(len(outline))]
    placket=mk.Mesh(v,f,["M_Conductor_UniformDark"]*len(f))
    parts=[conform(placket,0.003)]
    for y,r,material in ((0.425,0.022,"M_Conductor_Gold"),(0.350,0.014,"M_Conductor_GoldLow")):
        z, nrm = body_surface(0.0, y)
        b = mk.ellipsoid(r, r, r*0.5, 20, 10, mat=material)
        parts.append(b.transform(R=face_frame(nrm), t=(0, y, z+0.009)))
    return mk.merge(*parts)


def build_eye():
    """Oogwit: licht verticale, bolle ellipsoïde."""
    return mk.ellipsoid(EYE_R, EYE_R * 1.08, EYE_R * EYE_DEPTH, 24, 12, mat="M_Conductor_EyeWhite")


def build_pupil(side):
    """Pupil + glimlicht, op het oogoppervlak, licht naar binnen kijkend (zoals concept)."""
    px, py = -side * 0.022, 0.006
    ez = EYE_R * EYE_DEPTH * math.sqrt(max(0.0, 1 - (px / EYE_R) ** 2 - (py / (EYE_R * 1.08)) ** 2))
    pupil = mk.ellipsoid(0.029, 0.041, 0.011, 14, 8, mat="M_Conductor_Pupil").transform(t=(px, py, ez - 0.002))
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
    inner = mk.ellipsoid(0.074, 0.080, 0.022, 20, 10, mat="M_Conductor_Mouth")
    tongue = mk.ellipsoid(0.048, 0.028, 0.014, 14, 6, mat="M_Conductor_Tongue").transform(t=(0.006, -0.044, 0.010))
    return mk.merge(inner, tongue)


def build_brim(band_a,band_b):
    """Thin crescent attached below the band; no hidden rear half crossing it."""
    vertices=[]; faces=[]; segments=40; section=12
    for i in range(segments+1):
        angle=-1.40+2.80*i/segments
        inner=np.array([band_a*0.98*math.sin(angle),-0.010,band_b*0.98*math.cos(angle)])
        reach=0.155*math.cos(angle)**0.8
        outer=inner+np.array([0.020*math.sin(angle),-0.075*math.cos(angle),reach])
        for j in range(section):
            theta=2*math.pi*j/section
            t=(1-math.cos(theta))/2
            point=inner*(1-t)+outer*t
            point[1]+=0.008*math.sin(theta)
            vertices.append(point)
    for i in range(segments):
        for j in range(section):
            k=(j+1)%section
            faces.append((i*section+j,(i+1)*section+j,(i+1)*section+k,i*section+k))
    faces.append(tuple(reversed(range(section))))
    faces.append(tuple(segments*section+j for j in range(section)))
    return mk.orient_outward(mk.Mesh(np.asarray(vertices),faces,["M_Conductor_UniformDark"]*len(faces)))


def build_cap():
    """Pet: bolle kroon (~1,3x lijfbreedte), gouden band, donkere klep, embleem.
    Lokale oorsprong = onderkant band, midden van het hoofd."""
    band_a = BODY_R * 1.0 + 0.012
    band_b = band_a * BODY_DEPTH + 0.02
    BAND_H = 0.066
    band = mk.lathe([(0.0, 0.0), (0.96, 0.0), (1.0, 0.009), (1.0, BAND_H - 0.009), (0.96, BAND_H), (0.0, BAND_H)],
                    SEG, band_a, band_b, 2.3, mat="M_Conductor_Gold")
    # kroon: bolt boven de band ruim uit (~1,28x) en loopt hoog door tot een zachte, platte bovenkant
    crown_prof = [(0.0, BAND_H + 0.001), (0.97, BAND_H + 0.001)]
    for k in range(1, 9):
        t = k / 8
        crown_prof.append((0.97 + 0.31 * math.sin(t * math.pi / 2) ** 0.9, BAND_H + 0.11 * t))
    for k in range(1, 4):
        crown_prof.append((1.28 + 0.01 * math.sin(k / 3 * math.pi), BAND_H + 0.11 + 0.06 * k / 3))
    for k in range(1, 9):
        th = math.pi / 2 * k / 8
        crown_prof.append((1.28 * math.cos(th) ** 0.5 if k < 8 else 0.0, BAND_H + 0.17 + 0.085 * math.sin(th)))
    crown = mk.lathe(crown_prof, SEG, band_a, band_b * 1.04, 2.2, mat="M_Conductor_Uniform")
    under = mk.lathe([(0.0, -0.012), (1.02, -0.012), (1.03, 0.0), (0.0, 0.0)], SEG, band_a, band_b, 2.3,
                     mat="M_Conductor_UniformDark")
    brim = build_brim(band_a,band_b)
    # Embleem zit op de gouden band, overlapt de bovenrand licht zoals het concept.
    badge = mk.ellipsoid(0.059, 0.043, 0.026, 20, 10, mat="M_Conductor_Gold", n=2.1)
    badge = badge.transform(R=mk.rot_x(math.radians(-10)), t=(0, BAND_H * 0.62, band_b + 0.014))
    return mk.merge(band, crown, under, brim, badge)


def build_arm():
    """Eén doorlopend mouw-handvolume; het materiaal wisselt bij de pols."""
    profile=[(0.0,-0.270),(0.025,-0.267),(0.041,-0.260),(0.057,-0.251),(0.069,-0.238),(0.078,-0.218),
             (0.079,-0.190),(0.074,-0.164),(0.071,-0.148),
             (0.073,-0.120),(0.077,-0.070),(0.079,-0.025),(0.071,0.018),(0.0,0.045)]
    arm=mk.lathe(profile,32,a=1.0,b=0.88,n=2.0,mat="M_Conductor_Uniform")
    arm.M=["M_Conductor_Skin" if arm.V[list(f),1].mean() < -0.148 else "M_Conductor_Uniform" for f in arm.F]
    return arm


def build_key():
    """Conducteurssleutel (prop, standaard verborgen): ring in de hand, schacht langs lokale -Y."""
    ring = mk.torus(0.030, 0.010, 16, 6, mat="M_Conductor_Gold").transform(R=mk.rot_x(math.pi / 2), t=(0, 0.0, 0.0))
    shaft = mk.capsule_between((0, -0.03, 0), (0, -0.19, 0), 0.011, 0.011, 8, 2, mat="M_Conductor_Gold")
    bit = mk.rounded_box(0.035, 0.03, 0.012, 0.005, 1, mat="M_Conductor_Gold").transform(t=(0.02, -0.17, 0))
    return mk.merge(ring, shaft, bit).transform(t=(0, -0.02, 0.05))


# ----------------------------------------------------------------------- scène
def build_scene(arm_angle_deg=50.0):
    sc = mk.Scene("Conductor")
    R_ = RAMPS
    sc.material("M_Conductor_Skin", R_["skin"][2], R_["skin"], 0.30, opts={"paint": 1.1})
    sc.material("M_Conductor_Uniform", R_["uniform_navy"][2], R_["uniform_navy"], 0.22, opts={"paint": 1.3})
    sc.material("M_Conductor_UniformDark", R_["uniform_navy"][1], R_["uniform_navy"], 0.22, ramp_shift=-0.22)
    sc.material("M_Conductor_Gold", R_["gold_trim"][2], R_["gold_trim"], 0.40)
    sc.material("M_Conductor_GoldLow", R_["gold_trim"][1], R_["gold_trim"], 0.30, ramp_shift=-0.24)
    sc.material("M_Conductor_EyeWhite", R_["wool"][3], R_["wool"], 0.35, ramp_shift=0.30,
                opts={"flat": 0.45, "noShadow": True})
    sc.material("M_Conductor_Pupil", R_["face"][0], R_["ink"], 0.45, ramp_shift=-0.30, opts={"noShadow": True})
    sc.material("M_Conductor_Mouth", R_["mouth"][1], R_["mouth"], 0.20, ramp_shift=-0.55)
    sc.material("M_Conductor_Tongue", R_["mouth"][3], R_["mouth"], 0.25, ramp_shift=0.05)
    sc.material("M_Conductor_Shoe", R_["ui_bg"][2], R_["ui_bg"], 0.25)

    hip = np.array([0.0, HIP_Y, 0.0])
    sc.add(mk.Part("Conductor", None))
    sc.add(mk.Part("Body", build_body().transform(t=-hip), tuple(hip), "Conductor"))
    sc.add(mk.Part("Neckline", build_neckline().transform(t=-hip), (0, 0, 0), "Body"))
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

    cap_pivot = (0.0, 0.905 - HIP_Y, 0.0)
    sc.add(mk.Part("Cap", build_cap(), cap_pivot, "Body", mk.rot_x(math.radians(-10)) @ mk.rot_z(math.radians(4))))

    for side, nm in ((1, "L"), (-1, "R")):
        sh = np.array([side * SHOULDER[0], SHOULDER[1], SHOULDER[2]]) - hip
        Ra = mk.rot_z(side * math.radians(arm_angle_deg))
        sc.add(mk.Part("Arm_" + nm, build_arm(), tuple(sh), "Body", Ra, ao_group="arm_" + nm, gradient=0.0))
        # Hand-pivot blijft beschikbaar als anker; huid en mouw zijn één mesh op Arm.
        sc.add(mk.Part("Hand_" + nm, None, (0, -0.21, 0.0), "Arm_" + nm, ao_group="arm_" + nm))
    sc.add(mk.Part("Prop_Key", build_key(), (0, 0, 0), "Hand_L", visible=False))

    for side, nm in ((1, "L"), (-1, "R")):
        sc.add(mk.Part("Leg_" + nm, None, (side * LEG_X, HIP_Y, 0.0), "Conductor"))
    # ogen en pupillen blijven helder (concept): eigen AO-groep zonder occluders
    for p in sc.parts:
        if p.name.startswith(("Eye_", "Pupil_")):
            p.ao_group, p.gradient = "eyes", 0.0
        elif p.name in ("Body", "Neckline", "Collar", "Buttons", "Cap") or p.name.startswith("Mouth_"):
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

