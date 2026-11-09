"""Renderscènes voor het stationnetje en de perronkit (issue #182) -> scenes/station_sheet.json.

* stn_<asset>: één thumbnail per asset uit StationKit.json (camera uit de bounding box)
* stn_bld_*: het gebouw van voor, achter, links, een doorkijk zonder dak en een close-up van de deur
* stn_scene_*: het gebouw achter een perron van spoorstukken, met trein, props en personages

    python ArtSource/Render/station_scenes.py
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "scenes", "station_sheet.json")
S = "../Exports/Environment/Station/"
TR = "../Exports/Environment/Track/"
EX = "../Exports/"
GRASS = '["#566D3A","#6B7F38","#8B9F3B","#A1B044","#ADBC4A"]'


def item(name, pos=None, rot=None, base=S, pose=None):
    it = {"url": base + name + ".glb"}
    if pos is not None:
        it["pos"] = list(pos)
    if rot:
        it["rotY"] = rot
    if pose:
        it["pose"] = pose
    return it


def thumbs(man):
    out = []
    for name, a in man.items():
        if name == "Station_Building":
            continue
        mn, mx = a["min"], a["max"]
        c = [(mn[i] + mx[i]) / 2 for i in range(3)]
        size = [mx[i] - mn[i] for i in range(3)]
        rad = 0.5 * math.sqrt(sum(s * s for s in size))
        plat = name.startswith("Platform")
        dist = 1.22 * rad / math.tan(math.radians(15))
        out.append({"name": "stn_" + name, "params": {
            "w": 640, "h": 540, "ss": 1.3, "fov": 30, "yaw": -30 if plat else 24, "pitch": 26 if plat else 12,
            "dist": round(dist, 2), "target": "%.2f,%.2f,%.2f" % tuple(c), "lightRel": 1, "groundShadowOnly": 1,
            "groundR": round(rad * 2.5, 2), "shadowSize": round(rad * 1.6, 2), "far": round(dist * 4 + 50, 1),
            "items": [item(name)]}})
    return out


def building_views():
    def v(n, yaw, pitch, dist, tgt, pose=None, w=1400, h=1050, fov=35):
        return {"name": n, "params": {"w": w, "h": h, "yaw": yaw, "pitch": pitch, "dist": dist, "fov": fov, "target": tgt,
                                      "lightRel": 1, "groundShadowOnly": 1, "groundR": 14, "shadowSize": 9,
                                      "items": [item("Station_Building", pose=pose)]}}
    return [v("stn_bld_front", -25, 12, 20, "-1.4,3.2,-2.4"),
            v("stn_bld_back", 150, 14, 20, "-1.4,3.0,-2.4"),
            v("stn_bld_left", -75, 6, 15, "-3.5,2.4,-2.6"),
            v("stn_bld_inside", 15, 74, 17, "-1.3,1.2,-2.7", pose={"Roof": {"visible": False}, "RoofFrame": {"visible": False}}),
            v("stn_bld_door", -5, 6, 7.5, "0.6,2.3,-0.5")]


def station_scene():
    """Perron langs twee spoorstukken van 12 m, gebouw erachter (180 graden gedraaid), trein + personages."""
    its = [item("Track_Straight_12m", (-12, 0, 0), base=TR), item("Track_Straight_12m", (0, 0, 0), base=TR),
           item("Track_Straight_12m", (12, 0, 0), base=TR)]
    # perron: trap - 6 - 6 - 3 - kopeinde (x van -11,1 tot 10,4)
    its += [item("Platform_Stairs_Start", (-11.1, 0, 0)), item("Platform_Straight_6m", (-9.0, 0, 0)),
            item("Platform_Straight_6m", (-3.0, 0, 0)), item("Platform_Straight_3m", (3.0, 0, 0)),
            item("Platform_Straight_3m", (6.0, 0, 0)), item("Platform_EndCap_End", (9.0, 0, 0))]
    y = 1.05
    its += [item("Station_Building", (-1.2, 0, 5.30), 180)]
    its += [item("Prop_LampPost", (-8.2, y, 4.6)), item("Prop_LampPost", (6.6, y, 4.6)),
            item("Prop_Bench", (4.6, y, 4.55), 180), item("Prop_TrashBin", (3.3, y, 4.75)),
            item("Prop_Timetable", (8.4, y, 4.65), 180), item("Prop_TicketMachine", (-6.6, y, 4.75), 180),
            item("Prop_LuggageCart", (-4.9, y, 3.6), 70), item("Prop_StationSign", (-9.3, y, 4.75), 180),
            item("Prop_Planter", (1.6, y, 4.8), 180)]
    its += [item("Prop_Fence_2m", (10.6, 0, 6.3)), item("Prop_Fence_2m", (12.6, 0, 6.3)),
            item("Prop_LevelCrossing", (13.5, 0, -2.5), 0), item("Prop_CrateStack", (11.5, 0, 4.2), 15)]
    # trein: loc + tender + regionale wagon (hart spoor = z 1,35 / 1,30 in hun eigen frame)
    its += [item("Locomotive_Blockout", (0.6, y, 1.35), base=EX), item("Tender_Blockout", (-4.3, y, 1.35), base=EX),
            item("RegionalWagon_Blockout", (-10.6, y, 1.40), base=EX)]
    its += [item("Conductor", (-2.6, y, 3.0), 200, base=EX), item("ElderlyPassenger", (-0.2, y, 3.9), 160, base=EX),
            item("Tourist", (2.4, y, 3.5), 210, base=EX), item("RailMechanic", (6.2, y, 2.6), 230, base=EX)]
    # wat natuur rond het perron
    E = "../Exports/Environment/"
    its += [item("Tree_Pine_Large_01", (-14, 0, 12), base=E), item("Tree_Pine_Medium_01", (-9.5, 0, 11.5), base=E),
            item("Tree_Pine_Large_01", (9.0, 0, 12.5), 40, base=E), item("Tree_Pine_Medium_01", (14.5, 0, 9.0), base=E),
            item("Bush_01", (-12.6, 0, 5.4), base=E), item("Bush_01", (11.8, 0, 7.6), 60, base=E),
            item("Flower_Patch_01", (-13.2, 0, 3.6), base=E), item("Rock_Medium_01", (15.5, 0, 4.0), 30, base=E)]
    base = {"lightRel": 0, "groundRamp": GRASS, "groundR": 60, "far": 300, "lightAz": -40, "lightEl": 48, "items": its}
    no_train = [i for i in its if "Blockout" not in i["url"]]
    return [{"name": "stn_scene_wide", "params": dict(base, w=2400, h=1300, yaw=-198, pitch=16, dist=34, fov=36,
                                                       target="-0.5,2.2,3.0")},
            {"name": "stn_scene_close", "params": dict(base, w=1800, h=1200, yaw=-205, pitch=9, dist=15, fov=38,
                                                        target="0.0,2.8,4.0", items=no_train)}]


LAYOUT = "Assets/Art/Layouts/StationExample.json"


def unity_layout(scene, man):
    """Zelfde scène als Unity-layout (prefabs/FBX-paden) voor Conductor Chaos > Art > Station > Build Station Example."""
    import sys
    sys.path.insert(0, HERE)
    sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
    import unity_assets
    from export_unity_layout import FIXED, character_hidden
    env = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", "Environment", "EnvironmentKit.json"), encoding="utf-8"))["assets"]
    chars = {"ElderlyPassenger": ("Neutral", False), "Tourist": ("Neutral", False), "RailMechanic": ("Flat", True)}
    p = scene["params"]
    items = []
    for it in p["items"]:
        name = os.path.splitext(os.path.basename(it["url"]))[0]
        hide = []
        if it["url"].startswith(S):
            cat = man[name]["category"]
            path = f"Assets/Art/Prefabs/Station/{name}.prefab" if cat == "Building" else \
                f"Assets/Art/Prefabs/Station/{cat}/{name}.prefab"
        elif it["url"].startswith(TR):
            path = f"Assets/Art/Prefabs/Track/{name}.prefab"
        elif name in FIXED:
            path = FIXED[name]
        elif name in chars:
            path = f"Assets/Art/Models/Characters/{name}/{name}.fbx"
            hide = character_hidden(*chars[name])
        elif name in env:
            path = env[name]["fbx"]
        else:
            print("overgeslagen:", name)
            continue
        items.append({"path": path, "pos": it.get("pos", [0, 0, 0]), "rotY": it.get("rotY", 0.0), "scale": [1, 1, 1],
                      "hide": hide, "show": []})
    layout = {"camera": {"target": [float(v) for v in p["target"].split(",")], "yaw": p["yaw"], "pitch": p["pitch"],
                         "dist": p["dist"], "fov": p["fov"], "far": 400},
              "light": {"az": p["lightAz"], "el": p["lightEl"], "intensity": 1.15, "color": "#FFE3BC"},
              "fog": {"color": "#91A8E6", "near": 40, "far": 260}, "sky": "#85A4EC", "items": items}
    path = os.path.join(ROOT, LAYOUT)
    json.dump(layout, open(path, "w", encoding="utf-8", newline="\n"), indent=1)
    unity_assets.write(path + ".meta", "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n"
                                      "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % unity_assets.guid_for(LAYOUT))
    print("layout:", LAYOUT, len(items), "items")


def main():
    man = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", "Environment", "Station", "StationKit.json"),
                         encoding="utf-8"))["assets"]
    scene = station_scene()
    scenes = thumbs(man) + building_views() + scene
    json.dump(scenes, open(OUT, "w", encoding="utf-8"), indent=1)
    print("station scenes:", len(scenes))
    unity_layout(scene[0], man)


if __name__ == "__main__":
    main()
