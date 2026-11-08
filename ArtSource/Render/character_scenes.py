"""Schrijft de renderscènes (turnaround, expressies, held-pose) voor de personagevarianten.

    python ArtSource/Render/character_scenes.py      -> Render/scenes/<asset>_sheet.json

Poses zijn knoop-overrides (zichtbaarheid, rotatie, positie) op de geëxporteerde .glb, net als conductor_sheet.json.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def turn(asset, name, yaw):
    return {"name": f"{asset}_{name}", "params": {
        "w": 560, "h": 860, "yaw": yaw, "pitch": 3, "dist": 6, "ortho": 0.74, "target": "0,0.62,0",
        "lightRel": 1, "groundShadowOnly": 1,
        "items": [{"url": f"../Exports/{asset}.glb", "mats": f"../Exports/{asset}.materials.json"}]}}


def face(asset, name, pose, yaw=-14, aim=None):
    return {"name": f"{asset}_expr_{name}", "params": {
        "w": 460, "h": 460, "yaw": yaw, "pitch": 2, "dist": 2.5, "fov": 21, "target": "0,0.80,0",
        "lightRel": 1, "ground": "0",
        "items": [{"url": f"../Exports/{asset}.glb", "mats": f"../Exports/{asset}.materials.json", "pose": pose,
                   "aim": aim or {}}]}}


def hero(asset, pose=None, yaw=-24):
    return {"name": f"{asset}_hero", "params": {
        "w": 900, "h": 1100, "yaw": yaw, "pitch": 6, "dist": 3.7, "fov": 22, "target": "0,0.60,0",
        "lightRel": 1, "groundShadowOnly": 1,
        "items": [{"url": f"../Exports/{asset}.glb", "mats": f"../Exports/{asset}.materials.json",
                   "pose": pose or {}}]}}


HIDE = {"visible": False}
SHOW = {"visible": True}


def closed_eyes(pose):
    pose.update({"Eye_L": HIDE, "Eye_R": HIDE, "EyeClosed_L": SHOW, "EyeClosed_R": SHOW,
                 "Lid_L": HIDE, "Lid_R": HIDE})
    return pose


def mouth(kind, pose=None):
    pose = dict(pose or {})
    for other in ("Neutral", "Surprised", "Happy", "Flat", "Hmm", "Smile"):
        pose["Mouth_" + other] = HIDE
    pose["Mouth_" + kind] = SHOW
    return pose


def look(dx, dy, pose=None, scale=None):
    pose = dict(pose or {})
    for nm in ("L", "R"):
        p = {"addPos": [dx, dy, 0]}
        if scale:
            p["scale"] = [scale, scale, 1]
        pose["Pupil_" + nm] = p
    return pose


SHEETS = {
    "ElderlyPassenger": {
        "expr": [
            ("neutral", {}),
            ("happy", closed_eyes(mouth("Happy"))),
            ("confused", look(0.004, 0.030, mouth("Surprised"), 0.85)),
            ("thinking", look(-0.020, 0.028, mouth("Hmm")), {"Arm_R": [-0.30, 0.76, 0.30]}),
        ],
        "hero": look(-0.012, 0.022),
    },
    "Tourist": {
        "expr": [
            ("neutral", {}),
            ("excited", look(0.0, 0.020, mouth("Happy"), 1.05)),
            ("surprised", look(0.0, 0.0, mouth("Surprised"), 0.8)),
            ("happy", closed_eyes(mouth("Happy"))),
        ],
        "hero": look(0.006, 0.026, mouth("Happy")),
    },
    "RailMechanic": {
        "expr": [
            ("determined", {}),
            ("surprised", look(0.0, 0.0, mouth("Surprised", {"Lid_L": HIDE, "Lid_R": HIDE}), 0.8)),
            ("happy", closed_eyes(mouth("Happy"))),
            ("working", closed_eyes(mouth("Smile", {"Prop_Sweat": SHOW}))),
        ],
        "hero": {},
    },
}


def scenes(asset):
    cfg = SHEETS[asset]
    out = [turn(asset, "front", 0), turn(asset, "34", -38), turn(asset, "side", -90), turn(asset, "back", 180),
           turn(asset, "side_r", 90)]
    out += [face(asset, e[0], e[1], aim=e[2] if len(e) > 2 else None) for e in cfg["expr"]]
    out.append(hero(asset, cfg["hero"]))
    return out


def in_scene():
    """De drie varianten naast het spoor in de bestaande voorbeeldscène (zelfde plekken als de Unity-showcase)."""
    import copy
    env = json.load(open(os.path.join(HERE, "scenes", "env_sheet.json"), encoding="utf-8"))
    ex = copy.deepcopy(next(x for x in env if x["name"] == "env_example"))
    ex["name"] = "characters_in_scene"
    for asset, pos, rot in (("Tourist", [-5.0, -0.68, 2.0], 35), ("ElderlyPassenger", [-3.2, -0.68, 2.2], 20),
                            ("RailMechanic", [-1.4, -0.68, 2.4], 5)):
        ex["params"]["items"].append({"url": f"../Exports/{asset}.glb", "mats": f"../Exports/{asset}.materials.json",
                                      "pos": pos, "rotY": rot})
    ex["params"].update({"target": "-3.2,0.05,2.2", "dist": 7, "fov": 35})
    return [ex]


def main(assets=None):
    paths = []
    for asset in assets or SHEETS:
        path = os.path.join(HERE, "scenes", f"{asset}_sheet.json")
        json.dump(scenes(asset), open(path, "w", encoding="utf-8"), indent=1)
        paths.append(path)
    path = os.path.join(HERE, "scenes", "characters_in_scene.json")
    json.dump(in_scene(), open(path, "w", encoding="utf-8"), indent=1)
    paths.append(path)
    return paths


if __name__ == "__main__":
    print("\n".join(main()))
