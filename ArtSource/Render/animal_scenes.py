"""Schrijft de renderscènes (turnaround, expressies, held-pose) voor de dieren.

    python ArtSource/Render/animal_scenes.py      -> Render/scenes/<asset>_sheet.json

Poses zijn knoop-overrides (zichtbaarheid, rotatie, positie) op de geëxporteerde .glb, net als bij de personages.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
HIDE, SHOW = {"visible": False}, {"visible": True}


def item(asset, pose=None):
    return {"url": f"../Exports/Animals/{asset}.glb", "mats": f"../Exports/Animals/{asset}.materials.json",
            "pose": pose or {}}


def turn(asset, name, yaw, cfg):
    return {"name": f"{asset}_{name}", "params": {
        "w": 640, "h": 640, "yaw": yaw, "pitch": 8, "dist": 6, "ortho": cfg["ortho"], "target": cfg["target"],
        "lightRel": 1, "groundShadowOnly": 1, "items": [item(asset)]}}


def face(asset, name, pose, cfg):
    return {"name": f"{asset}_expr_{name}", "params": {
        "w": 460, "h": 460, "yaw": -10, "pitch": 4, "dist": cfg.get("face_dist", 2.6), "fov": 21, "target": cfg["face"],
        "lightRel": 1, "ground": "0", "items": [item(asset, pose)]}}


def hero(asset, pose, cfg):
    return {"name": f"{asset}_hero", "params": {
        "w": 1000, "h": 900, "yaw": -30, "pitch": 9, "dist": cfg["hero_dist"], "fov": 22, "target": cfg["target"],
        "lightRel": 1, "groundShadowOnly": 1, "items": [item(asset, pose)]}}


def prop(asset, yaw=-32):
    return {"name": f"{asset}_hero", "params": {
        "w": 600, "h": 600, "yaw": yaw, "pitch": 14, "dist": 3.2, "fov": 22, "target": "0,0.25,0",
        "lightRel": 1, "groundShadowOnly": 1, "items": [item(asset)]}}


def lineup():
    """Maatvergelijking: conducteur, schaap, koe en de props op één rij (zelfde schaal)."""
    cond = {"url": "../Exports/Conductor.glb", "mats": "../Exports/Conductor.materials.json", "pos": [-1.75, 0, 0],
            "rotY": 18}
    items = [cond, dict(item("Sheep", SHEETS["Sheep"]["hero"]), pos=[-0.80, 0, 0], rotY=24),
             dict(item("Cow", SHEETS["Cow"]["hero"]), pos=[0.40, 0, 0], rotY=-28),
             dict(item("Prop_HayBale"), pos=[1.75, 0, -0.25], rotY=20),
             dict(item("Prop_MilkCan"), pos=[1.28, 0, 0.45], rotY=0)]
    return {"name": "Animals_lineup", "params": {
        "w": 1600, "h": 700, "yaw": 0, "pitch": 7, "dist": 8, "ortho": 1.0, "target": "0,0.62,0",
        "lightRel": 1, "groundShadowOnly": 1, "items": items}}


def mouth(kind, pose=None):
    pose = dict(pose or {})
    for other in ("Neutral", "Surprised", "Happy"):
        pose["Mouth_" + other] = HIDE
    pose["Mouth_" + kind] = SHOW
    return pose


def closed_eyes(pose):
    pose.update({"Eye_L": HIDE, "Eye_R": HIDE, "EyeClosed_L": SHOW, "EyeClosed_R": SHOW})
    return pose


def look(dx, dy, pose=None, scale=None):
    pose = dict(pose or {})
    for nm in ("L", "R"):
        p = {"addPos": [dx, dy, 0]}
        if scale:
            p["scale"] = [scale, scale, 1]
        pose["Pupil_" + nm] = p
    return pose


def alert(pose):
    pose["Fx_Alert"] = SHOW
    return pose


SHEETS = {
    "Sheep": {"ortho": 0.56, "target": "0,0.40,0", "face": "0,0.47,0.25", "hero_dist": 3.4,
              "expr": [("clueless", look(0.0, 0.012)),
                       ("curious", alert(look(-0.018, 0.026))),
                       ("happy", closed_eyes(mouth("Happy"))),
                       ("surprised", alert(look(0.0, 0.004, mouth("Surprised"), 0.80)))],
              "hero": look(-0.008, 0.018)},
    "Cow": {"ortho": 0.74, "target": "0,0.50,0", "face": "0,0.70,0.35", "face_dist": 3.2, "hero_dist": 4.4,
            "expr": [("calm", look(0.0, 0.004)),
                     ("curious", alert(look(-0.018, 0.026))),
                     ("happy", closed_eyes(mouth("Happy"))),
                     ("startled", alert(look(0.0, 0.004, mouth("Surprised"), 0.80)))],
            "hero": look(-0.008, 0.018)},
}
PROPS = ("Prop_HayBale", "Prop_MilkCan")


def main(only=None):
    out_dir = os.path.join(HERE, "scenes")
    for asset, cfg in SHEETS.items():
        if only and asset not in only:
            continue
        scenes = [turn(asset, n, y, cfg) for n, y in (("front", 0), ("34", -38), ("side", -90), ("back", 180))]
        scenes += [face(asset, n, p, cfg) for n, p in cfg["expr"]]
        scenes.append(hero(asset, cfg["hero"], cfg))
        json.dump(scenes, open(os.path.join(out_dir, f"{asset}_sheet.json"), "w"), indent=1)
    json.dump([prop(p) for p in PROPS] + [lineup()], open(os.path.join(out_dir, "animal_props_sheet.json"), "w"),
              indent=1)


if __name__ == "__main__":
    import sys
    main(sys.argv[1:] or None)
