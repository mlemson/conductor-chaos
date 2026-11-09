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


def hero(asset, pose=None, yaw=-24, extra=None, target="0,0.60,0", dist=3.7):
    return {"name": f"{asset}_hero", "params": {
        "w": 900, "h": 1100, "yaw": yaw, "pitch": 6, "dist": dist, "fov": 22, "target": target,
        "lightRel": 1, "groundShadowOnly": 1,
        "items": [{"url": f"../Exports/{asset}.glb", "mats": f"../Exports/{asset}.materials.json",
                   "pose": pose or {}}] + list(extra or [])}}


HIDE = {"visible": False}
SHOW = {"visible": True}


def closed_eyes(pose):
    pose.update({"Eye_L": HIDE, "Eye_R": HIDE, "EyeClosed_L": SHOW, "EyeClosed_R": SHOW,
                 "Lid_L": HIDE, "Lid_R": HIDE})
    return pose


def mouth(kind, pose=None):
    pose = dict(pose or {})
    for other in ("Neutral", "Surprised", "Happy", "Flat", "Hmm", "Smile", "Grin"):
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
    "StationMaster": {
        "expr": [
            ("proud", look(0.0, 0.012)),
            ("kind", closed_eyes(mouth("Happy"))),
            ("thinking", look(-0.022, 0.026, mouth("Hmm"))),
            ("surprised", look(0.0, 0.0, mouth("Surprised", {"Lid_L": HIDE, "Lid_R": HIDE}), 0.8)),
        ],
        "hero": look(-0.010, 0.014, mouth("Happy")),
    },
    "BusinessTraveler": {
        "expr": [
            ("rushed", look(0.012, 0.020, mouth("Surprised", {"Prop_Sweat": SHOW}), 0.8)),
            ("focused", look(0.0, 0.0, mouth("Flat", {"Lid_L": SHOW, "Lid_R": SHOW}))),
            ("relieved", closed_eyes(mouth("Smile"))),
            ("exhausted", look(-0.010, -0.022, mouth("Hmm", {"Lid_L": SHOW, "Lid_R": SHOW, "Prop_Sweat": SHOW}))),
        ],
        "hero": look(0.010, 0.022, mouth("Surprised", {"Prop_Sweat": SHOW}), 0.85),
    },
    "ChildPassenger": {
        "expr": [
            ("mischievous", {}),
            ("excited", look(0.0, 0.020, mouth("Happy", {"Lid_L": HIDE, "Lid_R": HIDE}), 1.05)),
            ("caught", look(0.0, 0.0, mouth("Surprised", {"Lid_L": HIDE, "Lid_R": HIDE}), 0.75)),
            ("sly", look(0.026, 0.0, mouth("Smile"))),
        ],
        "hero": look(0.012, 0.016),
        "scale": 0.80,
    },
    "BaggageHelper": {
        "expr": [
            ("helpful", {}),
            ("focused", look(0.0, -0.006, mouth("Flat", {"Lid_L": SHOW, "Lid_R": SHOW}))),
            ("cheerful", closed_eyes(mouth("Happy"))),
            ("surprised", look(0.0, 0.020, mouth("Surprised", {"Prop_Sweat": SHOW}), 0.8)),
        ],
        "hero": look(0.010, 0.016),
        "extra": [{"url": "../Exports/BaggageTrolley.glb", "mats": "../Exports/BaggageTrolley.materials.json",
                   "pos": [1.06, 0, -0.01], "rotY": 0}],
        "hero_target": "0.55,0.52,0", "hero_dist": 5.6, "hero_yaw": -30, "hero_w": 1300,
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
    k = cfg.get("scale", 1.0)                           # kleinere personages: camera mee laten zakken
    if k != 1.0:
        for sc in out[5:]:
            sc["params"].update({"target": f"0,{0.80 * k:.3f},0", "dist": 2.5 * k})
    out.append(hero(asset, cfg["hero"], yaw=cfg.get("hero_yaw", -24), extra=cfg.get("extra"),
                    target=cfg.get("hero_target", "0,0.60,0"), dist=cfg.get("hero_dist", 3.7)))
    out[-1]["params"]["w"] = cfg.get("hero_w", 900)
    if asset == "BaggageHelper":                        # de bagagekar los, voor het ontwerpblad
        t = turn("BaggageTrolley", "34", -38)
        t["params"].update({"ortho": 0.78, "target": "0,0.50,0", "w": 760})
        out.append(t)
    return out


IN_SCENE = (("Tourist", [-5.0, -0.68, 2.0], 35), ("ElderlyPassenger", [-3.2, -0.68, 2.2], 20),
            ("RailMechanic", [-1.4, -0.68, 2.4], 5), ("StationMaster", [0.65, -0.68, 2.3], -5),
            ("BusinessTraveler", [2.0, -0.68, 2.6], -15), ("ChildPassenger", [-0.45, -0.68, 3.0], 10),
            ("BaggageHelper", [3.6, -0.68, 2.5], -25), ("BaggageTrolley", [4.56, -0.68, 2.95], -25))


def in_scene():
    """De varianten naast het spoor in de bestaande voorbeeldscène (zelfde plekken als de Unity-showcase)."""
    import copy
    env = json.load(open(os.path.join(HERE, "scenes", "env_sheet.json"), encoding="utf-8"))
    ex = copy.deepcopy(next(x for x in env if x["name"] == "env_example"))
    ex["name"] = "characters_in_scene"
    for asset, pos, rot in IN_SCENE:
        ex["params"]["items"].append({"url": f"../Exports/{asset}.glb", "mats": f"../Exports/{asset}.materials.json",
                                      "pos": pos, "rotY": rot})
    ex["params"].update({"target": "0.1,0.05,2.6", "dist": 11.5, "fov": 35})
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
