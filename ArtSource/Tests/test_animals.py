"""Controleert de dieren (schaap, koe) en hun props: knopen voor CharacterFace en animatie aanwezig, voeten op de
grond, verborgen expressies echt verborgen, afmetingen passend bij de veewagen en geen dubbele vlakken.

    python ArtSource/Tests/test_animals.py        (na de build-scripts in ArtSource/Animals/)
"""
import os
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Animals"))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Animals", "Sheep"))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Animals", "Cow"))
import zfight as Z  # noqa: E402
import build_sheep  # noqa: E402
import build_cow  # noqa: E402
import build_animal_props  # noqa: E402

EXP = os.path.join(ROOT, "ArtSource", "Exports", "Animals")
FACE = ["Eye_L", "Eye_R", "Pupil_L", "Pupil_R", "EyeClosed_L", "EyeClosed_R", "Blink_L", "Blink_R",
        "Mouth_Neutral", "Mouth_Surprised", "Mouth_Happy"]
HIDDEN = ["EyeClosed_L", "EyeClosed_R", "Blink_L", "Blink_R", "Mouth_Surprised", "Mouth_Happy", "Fx_Alert"]
RIG = ["Body", "Head", "Ear_L", "Ear_R", "Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR"]
WAGON_DOOR_W = 1.2            # ruime ondergrens: een dier moet door een deur van de veewagen kunnen


def world_points(sc, visible_only=True):
    pts = []
    for p in sc.parts:
        if p.mesh is None or (visible_only and not p.visible):
            continue
        M = sc.world_matrix(p)
        pts.append(p.mesh.V @ M[:3, :3].T + M[:3, 3])
    return np.vstack(pts)


def check_animal(name, sc, extra, max_h, max_tris):
    names = {p.name for p in sc.parts}
    missing = [n for n in FACE + RIG + extra + ["Fx_Alert"] if n not in names]
    assert not missing, f"{name}: knopen ontbreken: {missing}"
    for n in HIDDEN:
        assert not sc.get(n).visible, f"{name}: {n} hoort standaard verborgen te zijn"
    for n in ("Eye_L", "Eye_R", "Mouth_Neutral"):
        assert sc.get(n).visible, f"{name}: {n} hoort standaard zichtbaar te zijn"
    P = world_points(sc)
    assert abs(P[:, 1].min()) < 1e-3, f"{name}: voeten niet op de grond (min y = {P[:, 1].min():.4f})"
    assert abs(P[:, 0].min() + P[:, 0].max()) < 0.01, f"{name}: niet symmetrisch rond x = 0"
    size = P.max(0) - P.min(0)
    assert size[1] <= max_h and size[0] <= WAGON_DOOR_W, f"{name}: te groot {size.round(2)}"
    tris = sc.stats()["tris"]
    assert tris <= max_tris, f"{name}: {tris} driehoeken (budget {max_tris})"
    for leg in ("Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR"):                     # hoef raakt de grond, draaipunt bovenaan
        p = sc.get(leg)
        M = sc.world_matrix(p)
        ys = (p.mesh.V @ M[:3, :3].T + M[:3, 3])[:, 1]
        assert abs(ys.min()) < 1e-3 and abs(ys.max() - M[1, 3]) < 1e-3, f"{name}: {leg} staat niet goed"
    mats = {m for p in sc.parts if p.mesh is not None for m in p.mesh.M}
    assert mats <= set(sc.materials), f"{name}: onbekende materialen {mats - set(sc.materials)}"
    print(f"PASS {name}: {len(names)} knopen, {tris} driehoeken, {size[0]:.2f} x {size[1]:.2f} x {size[2]:.2f} m")


def check_no_double_faces():
    paths = [os.path.join(EXP, n + ".glb") for n in ("Sheep", "Cow", "Prop_HayBale", "Prop_MilkCan")]
    bad = []
    for p in paths:
        assert os.path.exists(p), f"export ontbreekt: {p}"
        res = Z.find(Z.glb_triangles(p))
        if res:
            bad.append(f"{os.path.basename(p)}: {len(res)}x")
    assert not bad, "dubbele vlakken:\n  " + "\n  ".join(bad)
    print(f"PASS dieren: {len(paths)} modellen zonder dubbele vlakken")


def check_props():
    for nm, sc, h in (("Prop_HayBale", build_animal_props.hay_scene(), 0.50),
                      ("Prop_MilkCan", build_animal_props.milk_scene(), 0.70)):
        P = world_points(sc)
        assert abs(P[:, 1].min()) < 1e-3 and P[:, 1].max() <= h, f"{nm}: hoogte/grond klopt niet"
        print(f"PASS {nm}: op de grond, {P[:, 1].max():.2f} m hoog")


if __name__ == "__main__":
    check_animal("Sheep", build_sheep.build_scene(), ["Tuft"], 0.85, 45000)
    check_animal("Cow", build_cow.build_scene(), ["Tuft", "Muzzle", "Horn_L", "Horn_R", "Tail"], 1.10, 45000)
    check_props()
    check_no_double_faces()
