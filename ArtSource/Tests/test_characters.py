"""Controleert de personagevarianten (passagiers, stationspersoneel, monteur) en de bagagekar.

    python ArtSource/Tests/test_characters.py

* knoopstructuur zoals de conducteur (Body, Eye/Pupil, Mouth_*, Arm/Hand, Leg-ankers) + props aan de handen
* precies één mond en één oogset standaard zichtbaar
* elke materiaalramp komt letterlijk uit Docs/References/palette.json (geen verzonnen tinten)
* niets steekt door de grond, budget per personage < 70k driehoeken
* knipperoogleden (Blink_L/R) bij elk personage, ook de conducteur; standaard verborgen
* geen dubbele vlakken (z-fighting) in alle personages en de bagagekar (ArtSource/Tools/zfight.py)
"""
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Characters"))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
for sub in ("ElderlyPassenger", "Tourist", "RailMechanic", "StationMaster", "BusinessTraveler", "ChildPassenger",
            "BaggageHelper"):
    sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Characters", sub))

import build_baggage_helper  # noqa: E402
import build_business_traveler  # noqa: E402
import build_child_passenger  # noqa: E402
import build_elderly_passenger  # noqa: E402
import build_rail_mechanic  # noqa: E402
import build_station_master  # noqa: E402
import build_tourist  # noqa: E402
import zfight as Z  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = [v["ramp"] for g in PALETTE["groups"].values() for v in g.values()]
EYES = ["Eye_L", "Eye_R", "Pupil_L", "Pupil_R", "EyeClosed_L", "EyeClosed_R", "Lid_L", "Lid_R", "Blink_L", "Blink_R"]
COMMON = ["Body"] + EYES + [
          "Mouth_Neutral", "Mouth_Surprised", "Mouth_Happy", "Mouth_Flat", "Mouth_Hmm", "Mouth_Smile",
          "Arm_L", "Arm_R", "Hand_L", "Hand_R", "Leg_L", "Leg_R"]
CASES = {
    "ElderlyPassenger": (build_elderly_passenger, ["Hair", "Beret", "Glasses", "Scarf", "Coat", "Prop_Bag"], "Neutral", False),
    "Tourist": (build_tourist, ["Hat", "Shirt", "Shorts", "Backpack", "Prop_Map", "Prop_Ticket"], "Neutral", False),
    "RailMechanic": (build_rail_mechanic, ["Helmet", "Workwear", "Prop_Sweat"], "Flat", True),
    "StationMaster": (build_station_master, ["Vest", "Jacket", "Watch", "Hair", "Mustache", "Brows", "Cap"],
                      "Smile", True),
    "BusinessTraveler": (build_business_traveler, ["Shirt", "Blazer", "Bag", "Hair", "Glasses", "Prop_Sweat",
                                                   "Prop_Suitcase"], "Neutral", False),
    "ChildPassenger": (build_child_passenger, ["Hoodie", "Shorts", "Backpack", "Hair", "Bandaid", "Cap",
                                               "Prop_Plane", "Mouth_Grin"], "Grin", True),
    "BaggageHelper": (build_baggage_helper, ["Workwear", "Cap", "Prop_Sweat"], "Happy", False),
}
# Alle personages en de bagagekar: geen dubbele vlakken (ook niet in de standaard zichtbare stand).
ZFIGHT_CLEAN = ["Conductor", "ElderlyPassenger", "Tourist", "RailMechanic", "StationMaster", "BusinessTraveler", "ChildPassenger", "BaggageHelper",
                "BaggageTrolley"]
EXP = os.path.join(ROOT, "ArtSource", "Exports")


def check(asset, module, extra, mouth, lids):
    sc = module.build_scene()
    names = {p.name for p in sc.parts}
    missing = [n for n in COMMON + extra if n not in names]
    assert not missing, f"{asset}: ontbrekende knopen {missing}"
    vis = {p.name: p.visible for p in sc.parts}
    mouths = [n for n in names if n.startswith("Mouth_") and vis[n]]
    assert mouths == ["Mouth_" + mouth], f"{asset}: zichtbare monden {mouths}"
    assert vis["Eye_L"] and vis["Eye_R"] and not vis["EyeClosed_L"] and not vis["EyeClosed_R"]
    assert not vis["Blink_L"] and not vis["Blink_R"], f"{asset}: knipperoogleden horen verborgen te zijn"
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


def check_trolley():
    """Bagagekar: eigen materialen uit palette.json, staat op de grond en de duwbeugel zit op handhoogte."""
    sc = build_baggage_helper.build_trolley_scene()
    names = {p.name for p in sc.parts}
    assert {"Trolley", "Handle"} <= names, f"BaggageTrolley: knopen {sorted(names)}"
    for name, m in sc.materials.items():
        assert m["ramp"] in RAMPS and name.startswith("M_BaggageTrolley_"), f"BaggageTrolley: materiaal {name}"
    pts = np.vstack([p.mesh.V @ sc.world_matrix(p)[:3, :3].T + sc.world_matrix(p)[:3, 3]
                     for p in sc.parts if p.mesh is not None])
    assert pts[:, 1].min() > -0.002, "BaggageTrolley steekt door de grond"
    handle = next(p for p in sc.parts if p.name == "Handle")
    hy = sc.world_matrix(handle)[1, 3]
    helper = build_baggage_helper.build_scene()
    hand = next(p for p in helper.parts if p.name == "Hand_L")
    gy = helper.world_matrix(hand)[1, 3]
    assert abs(hy - gy) < 0.08, f"duwbeugel {hy:.2f} m vs hand {gy:.2f} m"
    tris = sc.stats()["tris"]
    assert tris < 40000, f"BaggageTrolley: {tris} driehoeken"
    print(f"PASS BaggageTrolley: {tris} tris, duwbeugel {hy:.2f} m, hand {gy:.2f} m")


def check_conductor_eyes():
    """De conducteur heeft dezelfde wisselbare oogstanden als de varianten (CharacterFace in Unity)."""
    import build_conductor
    sc = build_conductor.build_scene()
    vis = {p.name: p.visible for p in sc.parts}
    missing = [n for n in EYES if n not in vis]
    assert not missing, f"Conductor: ontbrekende oogknopen {missing}"
    assert vis["Eye_L"] and not any(vis[n] for n in EYES if n.startswith(("EyeClosed_", "Lid_", "Blink_")))
    print("PASS Conductor: oogstanden EyeClosed/Lid/Blink aanwezig en verborgen")


def check_zfight(asset):
    res = Z.find(Z.glb_triangles(os.path.join(EXP, asset + ".glb")))
    assert not res, f"{asset}: {len(res)} dubbele vlakken, bv. {res[0][:2]}"
    print(f"PASS {asset}: geen dubbele vlakken")


if __name__ == "__main__":
    for asset, (module, extra, mouth, lids) in CASES.items():
        check(asset, module, extra, mouth, lids)
    check_trolley()
    check_conductor_eyes()
    for asset in ZFIGHT_CLEAN:
        check_zfight(asset)
