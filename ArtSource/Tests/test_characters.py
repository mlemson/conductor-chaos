"""Controleert de personagevarianten (oudere passagier, toerist, spoormonteur).

    python ArtSource/Tests/test_characters.py

* knoopstructuur zoals de conducteur (Body, Eye/Pupil, Mouth_*, Arm/Hand, Leg-ankers) + props aan de handen
* precies één mond en één oogset standaard zichtbaar
* elke materiaalramp komt letterlijk uit Docs/References/palette.json (geen verzonnen tinten)
* niets steekt door de grond, budget per personage < 70k driehoeken
"""
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Characters"))
for sub in ("ElderlyPassenger", "Tourist", "RailMechanic"):
    sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Characters", sub))

import build_elderly_passenger  # noqa: E402
import build_rail_mechanic  # noqa: E402
import build_tourist  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = [v["ramp"] for g in PALETTE["groups"].values() for v in g.values()]
COMMON = ["Body", "Eye_L", "Eye_R", "Pupil_L", "Pupil_R", "EyeClosed_L", "EyeClosed_R", "Lid_L", "Lid_R",
          "Mouth_Neutral", "Mouth_Surprised", "Mouth_Happy", "Mouth_Flat", "Mouth_Hmm", "Mouth_Smile",
          "Arm_L", "Arm_R", "Hand_L", "Hand_R", "Leg_L", "Leg_R"]
CASES = {
    "ElderlyPassenger": (build_elderly_passenger, ["Hair", "Beret", "Glasses", "Scarf", "Coat", "Prop_Bag"], "Neutral", False),
    "Tourist": (build_tourist, ["Hat", "Shirt", "Shorts", "Backpack", "Prop_Map", "Prop_Ticket"], "Neutral", False),
    "RailMechanic": (build_rail_mechanic, ["Helmet", "Workwear", "Prop_Sweat"], "Flat", True),
}


def check(asset, module, extra, mouth, lids):
    sc = module.build_scene()
    names = {p.name for p in sc.parts}
    missing = [n for n in COMMON + extra if n not in names]
    assert not missing, f"{asset}: ontbrekende knopen {missing}"
    vis = {p.name: p.visible for p in sc.parts}
    mouths = [n for n in names if n.startswith("Mouth_") and vis[n]]
    assert mouths == ["Mouth_" + mouth], f"{asset}: zichtbare monden {mouths}"
    assert vis["Eye_L"] and vis["Eye_R"] and not vis["EyeClosed_L"] and not vis["EyeClosed_R"]
    assert vis["Lid_L"] == lids and vis["Lid_R"] == lids, f"{asset}: oogleden"
    for p in sc.parts:
        if p.name.startswith("Prop_") and p.name != "Prop_Sweat":
            assert p.parent in ("Hand_L", "Hand_R"), f"{asset}: {p.name} hangt niet aan een hand"
    for name, m in sc.materials.items():
        assert m["ramp"] in RAMPS, f"{asset}: {name} gebruikt een ramp die niet in palette.json staat"
        assert name.startswith(f"M_{asset}_"), f"{asset}: materiaalnaam {name}"
    used = {mat for p in sc.parts if p.mesh is not None for mat in p.mesh.M}
    assert used <= set(sc.materials), f"{asset}: onbekende materialen {used - set(sc.materials)}"
    pts = [p.mesh.V @ sc.world_matrix(p)[:3, :3].T + sc.world_matrix(p)[:3, 3]
           for p in sc.parts if p.mesh is not None and p.visible]
    low = float(np.vstack(pts)[:, 1].min())
    assert low > -0.002, f"{asset}: steekt {low:.3f} m door de grond"
    tris = sc.stats()["tris"]
    assert tris < 70000, f"{asset}: {tris} driehoeken"
    print(f"PASS {asset}: {len(names)} knopen, {len(sc.materials)} materialen, {tris} tris, laagste punt {low:.3f} m")


if __name__ == "__main__":
    for asset, (module, extra, mouth, lids) in CASES.items():
        check(asset, module, extra, mouth, lids)
