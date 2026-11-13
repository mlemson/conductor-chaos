"""Renderscènes voor de treinsets uit train_kit (Diesel jaren '60, Modern Express, Futuristic Express).

    python ArtSource/Render/train_set_scenes.py            # schrijft Render/scenes/<Set>_sheet.json
    python ArtSource/Render/render_scenes.py ArtSource/Render/scenes/DieselSet_sheet.json ArtSource/Render/out
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ENV = {"url": "../Exports/ArtCheck_Environment.glb", "pose": {"Bench": {"visible": False}, "Lantern": {"visible": False}, "Platform": {"visible": False}}}

# per set: (voertuig, x-positie, onderhoudspunten); de loc/stuurstand staat voorop (+X)
SETS = {
    "DieselSet": [("DieselLocomotive_Blockout", 3.6, ["Fan", "EngineHatch", "AxleBox", "BrakeHose"]),
                  ("DieselCoach_Blockout", -4.16, ["Vent", "AxleBox", "BrakeHose"])],
    "ModernSet": [("ModernCabCar_Blockout", 3.6, ["Pantograph", "Converter", "AxleBox", "BrakeHose"]),
                  ("ModernCoach_Blockout", -4.16, ["Aircon", "AxleBox", "BrakeHose"])],
    "FuturisticSet": [("FuturisticCabCar_Blockout", 3.6, ["Pantograph", "PowerCell", "AxleBox", "BrakeHose"]),
                      ("FuturisticCoach_Blockout", -4.16, ["Cooler", "AxleBox", "BrakeHose"])],
    "FreightSet": [("TankWagon_Blockout", 4.0, ["Valve", "AxleBox", "BrakeHose"]),
                   ("CargoWagon_Blockout", -4.16, ["DoorLatch", "AxleBox", "BrakeHose"])],
}


HATCH_X = {"DieselSet": 1.7, "ModernSet": 4.76, "FuturisticSet": 4.76, "FreightSet": 4.0}      # x van het dakluik in de scène


def items(name, broken=False, env=True, only=None):
    out = [dict(ENV)] if env else []
    for (model, x, repairs) in SETS[name]:
        if only and model != only:
            continue
        pose = {}
        if broken:
            for r in repairs:
                pose[f"Fixed_Rail__{r}"] = {"visible": False}
                pose[f"Broken_Bits__{r}"] = {"visible": True}
        out.append({"url": f"../Exports/{model}.glb", "pos": [x, 0, 0], "pose": pose})
    return out


def cam(w, h, yaw, pitch, dist, target, fov=30, **kw):
    p = {"w": w, "h": h, "ss": 1.5, "fov": fov, "yaw": yaw, "pitch": pitch, "dist": dist, "target": target,
         "lightAz": -40, "lightEl": 45, "shadowSize": 14, "ground": "0", "fogNear": 30, "fogFar": 140}
    p.update(kw)
    return p


def scenes(name):
    return [
        {"name": f"{name}_hero", "params": dict(cam(1600, 900, 30, 7, 23, "0.6,1.0,-1.3"), items=items(name))},
        {"name": f"{name}_roof", "params": dict(cam(1600, 900, -38, 30, 23, "-0.6,1.6,-1.3"), items=items(name))},
        {"name": f"{name}_side", "params": dict(cam(1900, 400, 0, 2, 18.5, "0.7,0.95,-1.3", fov=22), items=items(name, env=False))},
        {"name": f"{name}_front", "params": dict(cam(900, 900, 78, 6, 15, "5.5,1.0,-1.3"), items=items(name))},
        {"name": f"{name}_far", "params": dict(cam(1600, 900, 150, 12, 24, "0.0,1.0,-1.3"), items=items(name, env=False))},
        {"name": f"{name}_broken", "params": dict(cam(1600, 900, -30, 24, 22, "-0.2,1.4,-1.3"), items=items(name, True))},
        {"name": f"{name}_top", "params": dict(cam(1900, 380, 0, 88, 18.5, "0.7,2.5,-1.3", fov=22), items=items(name, env=False))},
        {"name": f"{name}_hatch", "params": dict(cam(1200, 900, -25, 52, 7.5, f"{HATCH_X[name]},2.3,-1.9"),
                                                 items=items(name, env=False))},
        {"name": f"{name}_ladder", "params": dict(cam(1200, 900, 28, 14, 8.5, "-7.2,1.5,-0.6"), items=items(name, env=False))},
    ]


def one(model, pose=None, env=False):
    return ([dict(ENV)] if env else []) + [{"url": f"../Exports/{model}.glb", "pos": [0, 0, 0], "pose": pose or {}}]


def extras():
    """Lessenaars, rijtuigversies, vulklep van de ketelwagon en kopdeuren (blad TrainSet_Cabs_Variants)."""
    out = [{"name": f"Console_{k}", "params": dict(cam(1440, 930, -90, 38, 1.55, "2.85,1.02,-1.3", fov=52), items=one(m))}
           for k, m in (("60s", "DieselLocomotive_Blockout"), ("2020", "ModernCabCar_Blockout"), ("2040", "FuturisticCabCar_Blockout"))]
    out += [{"name": f"Variant_{m}", "params": dict(cam(1440, 780, 24, 9, 13.5, "0,1.0,-1.3"), items=one(m, env=True))}
            for m in ("DieselCoach_Doors_Blockout", "ModernCoach_Closed_Blockout", "FuturisticCoach_Closed_Blockout")]
    opened = {"TankHatch": {"rot": [-105, 0, 0]}, "TankLiquid": {"addPos": [0, 0.30, 0]}}
    for nm, pose in (("closed", {}), ("open", opened)):
        out.append({"name": f"TankHatch_{nm}", "params": dict(cam(1080, 720, -24, 50, 4.6, "0,2.45,-1.3"), items=one("TankWagon_Blockout", pose))})
    slide = {"EndDoorLeaf_L_Rear": {"addPos": [0, 0, 0.46]}, "EndDoorLeaf_R_Rear": {"addPos": [0, 0, -0.46]}}
    for m, tag in (("ModernCoach_Blockout", "auto"), ("DieselCoach_Blockout", "manual")):
        for nm, pose in (("closed", {}), ("open", slide)):
            out.append({"name": f"EndDoor_{tag}_{nm}", "params": dict(cam(720, 720, -108, 9, 5.6, "-3.7,1.15,-1.3"), items=one(m, pose))})
    return out


def main():
    out = os.path.join(HERE, "scenes")
    json.dump(extras(), open(os.path.join(out, "TrainExtras_sheet.json"), "w", encoding="utf-8"), indent=2)
    for name in SETS:
        json.dump(scenes(name), open(os.path.join(out, f"{name}_sheet.json"), "w", encoding="utf-8"), indent=2)
        print("scenes", name)


if __name__ == "__main__":
    main()
