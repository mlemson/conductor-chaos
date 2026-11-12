"""Treinsets uit train_kit (Diesel jaren '60, Modern Express, Futuristic Express): knopen, maten en dubbele vlakken.

    python ArtSource/Tests/test_train_sets.py
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import zfight  # noqa: E402

EXPORTS = os.path.join(ROOT, "ArtSource", "Exports")
# model -> (onderhoudspunten, dakluik, dakladder buiten)
MODELS = {
    "DieselLocomotive_Blockout": (["Fan", "EngineHatch", "AxleBox", "BrakeHose"], "RoofHatch_Engine", None),
    "DieselCoach_Blockout": (["Vent", "AxleBox", "BrakeHose"], None, "RoofLadder"),
    "ModernCabCar_Blockout": (["Pantograph", "Converter", "AxleBox", "BrakeHose"], "RoofHatch_Cab", None),
    "ModernCoach_Blockout": (["Aircon", "AxleBox", "BrakeHose"], None, "RoofLadder"),
    "FuturisticCabCar_Blockout": (["Pantograph", "PowerCell", "AxleBox", "BrakeHose"], "RoofHatch_Cab", None),
    "FuturisticCoach_Blockout": (["Cooler", "AxleBox", "BrakeHose"], None, "RoofLadder"),
}


def nodes(path):
    with open(path, "rb") as f:
        f.read(12)
        n, _ = struct.unpack("<II", f.read(8))
        return json.loads(f.read(n))["nodes"]


def main():
    fails = []
    for model, (repairs, hatch, ladder) in MODELS.items():
        path = os.path.join(EXPORTS, model + ".glb")
        if not os.path.exists(path):
            fails.append(f"{model}: GLB ontbreekt")
            continue
        names = [n["name"] for n in nodes(path)]
        if len(names) != len(set(names)):
            fails.append(f"{model}: knoopnamen niet uniek (FBX koppelt op naam)")
        need = ["Body", "Roof", "Underframe", "Coupler_Front", "Coupler_Rear"] + [x for x in (hatch, ladder) if x]
        for r in repairs:      # wat RepairPoint (met achtervoegsel) zoekt
            need += [f"RepairPoint_{r}", f"Fixed_Rail__{r}", f"Broken_Bits__{r}", f"Repair_Bolt__{r}", f"Repair_Bolt_Home__{r}"]
        fails += [f"{model}: knoop {n} ontbreekt" for n in need if n not in names]
        found = sorted(n[len("RepairPoint_"):] for n in names if n.startswith("RepairPoint_"))
        if found != sorted(repairs):
            fails.append(f"{model}: onderhoudspunten {found} != {sorted(repairs)}")
        if any(n.startswith("DoorLeaf_") for n in names):
            fails.append(f"{model}: DoorLeaf_* is voorbehouden aan de regionale wagon (TrainDrive bedient er maar één)")
        hits = zfight.find(zfight.glb_triangles(path))
        fails += [f"{model}: dubbel vlak {h}" for h in hits[:3]]
        print(model, len(names), "knopen,", len(repairs), "onderhoudspunten,", len(hits), "dubbele vlakken")
    for f in fails:
        print("FAIL", f)
    print("OK" if not fails else f"{len(fails)} fout(en)")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
