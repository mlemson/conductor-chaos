"""Wagonhoek — blockout voor de P0 art proof (issue #9).

Eén hoek van een regionale reizigerswagon: kopsectie, eerste instapdeur (open
schuifdeur), twee zijramen, dak, onderstel, en een stukje interieur dat door de
deur zichtbaar is (vloer, stoel, gele paal). Het sleutelslot naast de deur en de
gele drempel zijn de interactie-ankers voor P0-05 (gedeelde deur).

Bewust een *blockout*: grote zachte volumes, geen detailmodellering. Het is een
beeldtest van stijl, kleur en schaal naast de conducteur, geen productie-wagon.

Uitvoer:
  Assets/Art/Models/Train/WagonCorner/WagonCorner_Blockout.fbx  Unity
  ArtSource/Exports/WagonCorner_Blockout.glb                    Blender/preview
  ArtSource/Exports/ArtCheck_Environment.glb                    alleen voor de art-check render
                                                                (perron + achtergrond, geen game-asset)
Assen: lengte langs X, buitenkant zijwand kijkt naar +Z, vloer op y = 0.10.
"""
import json
import math
import os
import random
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import meshkit as mk  # noqa: E402
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

FLOOR = 0.10            # vloerhoogte t.o.v. perron (gelijkvloers instappen)
WALL_TOP = 2.05
X0, X1 = -3.6, 1.95     # lengte van het stuk wagon (kop biedt ruimte voor het rechter deurblad)
DOOR = (-0.20, 0.95)    # deuropening (x)
DOOR_TOP = 1.80
WIN_Y = (0.95, 1.62)
WIDTH = 2.6
T = 0.10                # wanddikte
LOCK_Y = 0.72           # sleutelslot op reikhoogte van de conducteur (schouder ~0,49 m)


def box(x0, x1, y0, y1, z0, z1, r, mat, div=2):
    return mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, r, div, mat=mat).transform(
        t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


def build_wagon():
    sc = mk.Scene("WagonCorner_Blockout")
    R_ = RAMPS
    sc.material("M_Train_Red", R_["train_red"][2], R_["train_red"], 0.25)
    sc.material("M_Train_Trim", R_["train_trim"][1], R_["train_trim"], 0.20)
    sc.material("M_Train_Roof", R_["metal"][2], R_["metal"], 0.20)
    sc.material("M_Train_Glass", R_["sky"][0], R_["sky"], 0.60, ramp_shift=-0.30, opts={"rim": 0.45, "spec": 1.4})
    sc.material("M_Train_Interior", R_["parchment"][2], R_["parchment"], 0.15)
    sc.material("M_Train_Floor", R_["cab_interior"][2], R_["cab_interior"], 0.15)
    sc.material("M_Train_Seat", R_["uniform_navy"][3], R_["uniform_navy"], 0.15, ramp_shift=0.05)
    sc.material("M_Train_Interact", R_["accent_yellow"][2], R_["accent_yellow"], 0.30)
    sc.material("M_Train_Lamp", R_["headlight"][2], R_["headlight"], 0.50, emissive=R_["headlight"][3],
                opts={"emissive": 0.85, "rim": 0.0})
    sc.material("M_Train_Metal", R_["metal"][3], R_["metal"], 0.40)
    z_out, z_in = 0.0, -T
    shell = []
    # onderstel / skirt (donkere trim) en vloer
    shell.append(box(X0, X1, -0.42, FLOOR + 0.02, -WIDTH + 0.12, -0.06, 0.08, "M_Train_Trim"))
    # onderste rode band (met deuropening)
    for (a, b) in ((X0, DOOR[0]), (DOOR[1], X1)):
        shell.append(box(a, b, FLOOR, WIN_Y[0], z_in, z_out, 0.04, "M_Train_Red"))
    # raamstijlen
    wins = [(-3.25, -2.05), (-1.80, -0.55)]
    pill = [(X0, wins[0][0]), (wins[0][1], wins[1][0]), (wins[1][1], DOOR[0])]
    for (a, b) in pill:
        shell.append(box(a, b, WIN_Y[0] - 0.02, WIN_Y[1] + 0.02, z_in, z_out, 0.04, "M_Train_Red"))
    # bovenband boven de ramen, boven de deur, kopsectie
    shell.append(box(X0, DOOR[0] + 0.02, WIN_Y[1], WALL_TOP, z_in, z_out, 0.04, "M_Train_Red"))
    shell.append(box(DOOR[0] - 0.02, DOOR[1] + 0.02, DOOR_TOP, WALL_TOP, z_in, z_out, 0.04, "M_Train_Red"))
    shell.append(box(DOOR[1], X1, WIN_Y[0] - 0.02, WALL_TOP, z_in, z_out, 0.04, "M_Train_Red"))
    # donkere raamband-lijn (livrei-accent) onder de ramen
    shell.append(box(X0, DOOR[0] - 0.01, WIN_Y[0] - 0.13, WIN_Y[0] - 0.06, z_out - 0.005, z_out + 0.022, 0.012, "M_Train_Trim"))
    shell.append(box(DOOR[1] + 0.01, X1, WIN_Y[0] - 0.13, WIN_Y[0] - 0.06, z_out - 0.005, z_out + 0.022, 0.012, "M_Train_Trim"))
    # kopwand (de 'hoek'): afgeronde eindstijl die naar achter loopt
    shell.append(box(X1 - 0.14, X1 + 0.06, FLOOR, WALL_TOP, -WIDTH + 0.02, z_out, 0.09, "M_Train_Red", 3))
    # dak: brede, bolle kap
    roof = mk.rounded_box(X1 - X0 + 0.16, 0.42, WIDTH + 0.12, 0.2, 3, mat="M_Train_Roof")
    shell.append(roof.transform(t=((X0 + X1) / 2, WALL_TOP + 0.13, -WIDTH / 2)))
    sc.add(mk.Part("WagonCorner", None))
    sc.add(mk.Part("Shell", mk.merge(*shell), (0, 0, 0), "WagonCorner"))
    # ramen (glas iets verzonken) + kopraam
    glass = [box(a + 0.01, b - 0.01, WIN_Y[0] + 0.01, WIN_Y[1] - 0.01, z_in + 0.02, z_in + 0.05, 0.05, "M_Train_Glass")
             for (a, b) in wins]
    frames = []
    for (a, b) in wins:
        for (x0, x1, y0, y1) in ((a, b, WIN_Y[0] - 0.02, WIN_Y[0] + 0.05), (a, b, WIN_Y[1] - 0.05, WIN_Y[1] + 0.02),
                                 (a - 0.02, a + 0.05, WIN_Y[0], WIN_Y[1]), (b - 0.05, b + 0.02, WIN_Y[0], WIN_Y[1])):
            frames.append(box(x0, x1, y0, y1, z_in + 0.03, z_out + 0.012, 0.02, "M_Train_Trim"))
    sc.add(mk.Part("Windows", mk.merge(*glass, *frames), (0, 0, 0), "WagonCorner"))
    # interieur (door de deur zichtbaar)
    inner = [
        box(X0 + 0.1, X1 - 0.15, FLOOR - 0.04, FLOOR, -WIDTH + 0.1, z_in, 0.01, "M_Train_Floor"),
        box(X0 + 0.1, X1 - 0.15, FLOOR, WALL_TOP, -WIDTH + 0.05, -WIDTH + 0.15, 0.02, "M_Train_Interior"),
        box(X0 + 0.1, X1 - 0.15, WALL_TOP - 0.06, WALL_TOP, -WIDTH + 0.1, z_in, 0.02, "M_Train_Interior"),
        # stoelrij achter de deur (bank + rugleuning)
        box(-0.15, 0.95, FLOOR + 0.38, FLOOR + 0.52, -WIDTH + 0.2, -WIDTH + 0.75, 0.06, "M_Train_Seat"),
        box(-0.15, 0.95, FLOOR + 0.5, FLOOR + 1.05, -WIDTH + 0.16, -WIDTH + 0.34, 0.07, "M_Train_Seat"),
        box(0.30, 0.42, FLOOR, FLOOR + 0.40, -WIDTH + 0.4, -WIDTH + 0.6, 0.03, "M_Train_Metal"),
    ]
    sc.add(mk.Part("Interior", mk.merge(*inner), (0, 0, 0), "WagonCorner"))
    pole = mk.capsule_between((0.0, FLOOR, 0.0), (0.0, WALL_TOP - 0.05, 0.0), 0.032, 0.032, 12, 3, mat="M_Train_Interact")
    sc.add(mk.Part("HandPole", pole, ((DOOR[0] + DOOR[1]) / 2, 0, -0.55), "WagonCorner"))
    # deurbladen: schuiven open ín de wand (pocket); alleen de voorrand steekt 0,10 m de opening in.
    # Unity: animeer DoorLeaf_L/R langs lokale X: open = huidig, dicht = +0,475 (L) / -0,475 (R).
    for nm, cx in (("DoorLeaf_L", DOOR[0] + 0.10 - 0.29), ("DoorLeaf_R", DOOR[1] - 0.10 + 0.29)):
        edge = 0.29 if nm.endswith("L") else -0.29
        leaf = mk.merge(
            box(-0.29, 0.29, 0.0, DOOR_TOP - FLOOR - 0.02, -0.025, 0.025, 0.02, "M_Train_Red"),
            box(edge - 0.03, edge + 0.03, 0.02, DOOR_TOP - FLOOR - 0.04, -0.03, 0.03, 0.02, "M_Train_Trim"),  # rubber rand
        )
        sc.add(mk.Part(nm, leaf, (cx, FLOOR, -0.05), "WagonCorner"))
    # drempel met gele rand (interactie/veiligheid) en instaprand
    sill = mk.merge(box(DOOR[0], DOOR[1], FLOOR - 0.03, FLOOR + 0.012, -0.30, 0.10, 0.012, "M_Train_Floor"),
                    box(DOOR[0] + 0.02, DOOR[1] - 0.02, FLOOR + 0.004, FLOOR + 0.02, 0.0, 0.09, 0.008, "M_Train_Interact"))
    sc.add(mk.Part("DoorSill", sill, (0, 0, 0), "WagonCorner"))
    # sleutelslot: geel paneel met donker sleutelgat (anker voor conducteurssleutel)
    lock = mk.merge(box(-0.075, 0.075, -0.11, 0.11, -0.012, 0.035, 0.03, "M_Train_Interact"),
                    mk.ellipsoid(0.026, 0.026, 0.012, 14, 6, mat="M_Train_Trim").transform(t=(0, 0.02, 0.036)),
                    box(-0.008, 0.008, -0.045, 0.02, 0.028, 0.042, 0.006, "M_Train_Trim"))
    sc.add(mk.Part("KeyLock", lock, (DOOR[0] - 0.13, LOCK_Y, z_out), "WagonCorner"))
    # deurlamp boven de deur (warm licht: deur open)
    lamp = mk.merge(box(-0.14, 0.14, -0.05, 0.05, -0.02, 0.03, 0.03, "M_Train_Trim"),
                    mk.ellipsoid(0.11, 0.035, 0.03, 16, 6, mat="M_Train_Lamp").transform(t=(0, 0, 0.03)))
    sc.add(mk.Part("DoorLamp", lamp, ((DOOR[0] + DOOR[1]) / 2, DOOR_TOP + 0.12, z_out + 0.01), "WagonCorner"))
    # koplamp op de kop (sfeer, conceptverwijzing)
    head = mk.merge(mk.lathe([(0, 0), (0.12, 0), (0.12, 0.05), (0.0, 0.05)], 20, mat="M_Train_Trim"),
                    mk.ellipsoid(0.095, 0.03, 0.095, 18, 6, mat="M_Train_Lamp").transform(t=(0, 0.05, 0)))
    head = head.transform(R=mk.rot_z(-math.pi / 2), t=(0, 0, 0))
    sc.add(mk.Part("HeadLight", head, (X1 + 0.06, 0.62, -0.42), "WagonCorner"))
    return sc


def build_env():
    """Art-check decor: perron + landschap. Alleen voor de render, geen game-asset."""
    random.seed(4)
    sc = mk.Scene("ArtCheck_Environment")
    R_ = RAMPS
    sc.material("M_Env_Platform", R_["parchment"][1], R_["parchment"], 0.1, ramp_shift=-0.30)
    sc.material("M_Env_PlatformEdge", R_["accent_yellow"][2], R_["accent_yellow"], 0.2)
    sc.material("M_Env_Ballast", R_["cliff"][1], R_["cliff"], 0.1, ramp_shift=-0.1)
    sc.material("M_Env_Sleeper", R_["track_wood"][2], R_["track_wood"], 0.1)
    sc.material("M_Env_Rail", R_["metal"][2], R_["metal"], 0.4)
    sc.material("M_Env_Grass", R_["grass"][2], R_["grass"], 0.05)
    sc.material("M_Env_Pine", R_["pine"][2], R_["pine"], 0.05)
    sc.material("M_Env_Trunk", R_["interior_wood"][1], R_["interior_wood"], 0.05)
    sc.material("M_Env_Rock", R_["mountain"][2], R_["mountain"], 0.05, opts={"rim": 0.1})
    sc.material("M_Env_Snow", R_["cloud_snow"][2], R_["cloud_snow"], 0.05, opts={"rim": 0.1})
    sc.material("M_Env_Bench", R_["interior_wood"][3], R_["interior_wood"], 0.1)
    sc.material("M_Env_Lamp", R_["headlight"][2], R_["headlight"], 0.5, opts={"emissive": 0.8, "rim": 0})
    sc.material("M_Env_Post", R_["train_trim"][1], R_["train_trim"], 0.2)
    sc.add(mk.Part("ArtCheck_Environment", None))
    plat = mk.merge(box(-9, 7, -0.75, 0.0, 0.10, 4.2, 0.05, "M_Env_Platform"),
                    box(-9, 7, -0.02, 0.008, 0.36, 0.50, 0.01, "M_Env_PlatformEdge"))
    sc.add(mk.Part("Platform", plat, (0, 0, 0), "ArtCheck_Environment"))
    # spoor onder de wagon (deels zichtbaar aan de kop)
    trk = [box(-9, 9, -1.05, -0.78, -WIDTH - 0.6, 0.05, 0.05, "M_Env_Ballast")]
    for x in np.arange(-8.5, 9, 0.62):
        trk.append(box(x - 0.13, x + 0.13, -0.80, -0.70, -WIDTH - 0.25, -0.05, 0.03, "M_Env_Sleeper"))
    for zr in (-0.55, -WIDTH + 0.55):
        trk.append(box(-9, 9, -0.70, -0.60, zr - 0.04, zr + 0.04, 0.015, "M_Env_Rail"))
    sc.add(mk.Part("Track", mk.merge(*trk), (0, 0, 0), "ArtCheck_Environment"))
    # bankje + lantaarn op het perron
    bench = mk.merge(box(-0.7, 0.7, 0.38, 0.46, -0.25, 0.25, 0.04, "M_Env_Bench"),
                     box(-0.7, 0.7, 0.5, 0.86, 0.16, 0.26, 0.04, "M_Env_Bench"),
                     box(-0.6, -0.5, 0.0, 0.4, -0.2, 0.2, 0.03, "M_Env_Post"),
                     box(0.5, 0.6, 0.0, 0.4, -0.2, 0.2, 0.03, "M_Env_Post"))
    sc.add(mk.Part("Bench", bench, (-4.4, 0, 2.7), "ArtCheck_Environment"))
    lantern = mk.merge(mk.capsule_between((0, 0, 0), (0, 2.6, 0), 0.05, 0.04, 10, 2, mat="M_Env_Post"),
                       mk.ellipsoid(0.16, 0.2, 0.16, 14, 8, mat="M_Env_Lamp").transform(t=(0, 2.7, 0)),
                       mk.lathe([(0, 2.92), (0.22, 2.86), (0.24, 2.83), (0.0, 2.83)], 12, mat="M_Env_Post"))
    sc.add(mk.Part("Lantern", lantern, (-2.2, 0, 3.4), "ArtCheck_Environment"))
    # landschap
    ground = box(-60, 60, -1.2, -0.9, -60, -WIDTH - 0.5, 0.2, "M_Env_Grass")
    ground2 = box(-60, 60, -1.2, -0.76, 4.2, 40, 0.2, "M_Env_Grass")
    sc.add(mk.Part("Ground", mk.merge(ground, ground2), (0, 0, 0), "ArtCheck_Environment"))
    pines = []
    for _ in range(26):
        x = random.uniform(-26, 18)
        z = random.uniform(-26, -5.0)
        s = random.uniform(1.6, 3.6)
        layers = [mk.lathe([(0, 0), (1.0, 0.0), (0.0, 1.1)], 7, mat="M_Env_Pine").transform(s=(0.55 * s, 0.55 * s, 0.55 * s), t=(0, s * (0.35 + 0.42 * k), 0))
                  for k in range(3) for _ in [0]]
        layers = [l.transform(s=(1 - 0.22 * k, 1, 1 - 0.22 * k)) for k, l in enumerate(layers)]
        trunk = mk.capsule_between((0, -0.6, 0), (0, 0.5 * s, 0), 0.08 * s, 0.07 * s, 6, 1, mat="M_Env_Trunk")
        tree = mk.merge(trunk, *layers).transform(R=mk.rot_z(random.uniform(-0.08, 0.08)), t=(x, -0.9, z))
        pines.append(tree)
    sc.add(mk.Part("Pines", mk.merge(*pines), (0, 0, 0), "ArtCheck_Environment"))
    mts = []
    for (x, z, h, w) in ((-34, -60, 26, 22), (-8, -70, 34, 26), (18, -62, 24, 20), (40, -75, 30, 24), (-56, -72, 22, 22)):
        prof = [(0, 0), (1.0, 0), (0.55, 0.45), (0.22, 0.82), (0.0, 1.0)]
        m = mk.lathe(prof, 7, mat="M_Env_Rock", phase=random.uniform(0, 1)).transform(s=(w, h, w * 0.7), t=(x, -1, z))
        m.sharp = True
        cap = mk.lathe([(0.24, 0.80), (0.11, 0.9), (0.0, 1.0)], 7, mat="M_Env_Snow").transform(s=(w * 1.04, h * 1.005, w * 0.73), t=(x, -1, z))
        cap.sharp = True
        mts += [m, cap]
    mtn = mk.merge(*mts)
    mtn.sharp = True
    sc.add(mk.Part("Mountains", mtn, (0, 0, 0), "ArtCheck_Environment"))
    return sc


def main():
    wag = build_wagon()
    env = build_env()
    out_dir = os.path.join(ROOT, "Assets", "Art", "Models", "Train", "WagonCorner")
    exp_dir = os.path.join(ROOT, "ArtSource", "Exports")
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(exp_dir, exist_ok=True)
    wag.write_fbx_ascii(os.path.join(out_dir, "WagonCorner_Blockout.fbx"))
    wag.write_glb(os.path.join(exp_dir, "WagonCorner_Blockout.glb"))
    unity_assets.export_unity(ROOT, wag, "Assets/Art/Models/Train/WagonCorner/WagonCorner_Blockout.fbx", "Assets/Art/Materials/Train/WagonCorner")
    env.write_glb(os.path.join(exp_dir, "ArtCheck_Environment.glb"))
    mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"],
                "opts": v["opts"]} for k, v in wag.materials.items()}
    json.dump({"model": "WagonCorner_Blockout", "stats": wag.stats(), "materials": mats},
              open(os.path.join(exp_dir, "WagonCorner_Blockout.materials.json"), "w", encoding="utf-8"), indent=2)
    print("wagon", wag.stats())
    print("env", env.stats())


if __name__ == "__main__":
    main()
