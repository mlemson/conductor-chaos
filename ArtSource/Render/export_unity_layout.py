"""Exporteert de voorbeeldscène van de natuurkit (scenes/env_sheet.json -> 'env_example') als Unity-layout.

Uitvoer: Assets/Art/Layouts/ArtShowcase.json (+ .meta), gelezen door
Assets/Editor/ConductorChaos/ArtShowcaseBuilder.cs (menu Conductor Chaos > Art > Build Showcase Scene).
Coördinaten blijven in de bronassen (rechtshandig, zoals de FBX); de C#-builder spiegelt X voor Unity.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import unity_assets  # noqa: E402

OUT = "Assets/Art/Layouts/ArtShowcase.json"
FIXED = {
    "Locomotive_Blockout": "Assets/Art/Models/Train/Locomotive/Locomotive_Blockout.fbx",
    "Tender_Blockout": "Assets/Art/Models/Train/Tender/Tender_Blockout.fbx",
    "RegionalWagon_Blockout": "Assets/Art/Models/Train/RegionalWagon/RegionalWagon_Blockout.fbx",
    "CattleWagon_Blockout": "Assets/Art/Models/Train/CattleWagon/CattleWagon_Blockout.fbx",
    "Conductor": "Assets/Art/Models/Characters/Conductor/Conductor.fbx",
    "ArtCheck_Environment": "Assets/Art/Models/Environment/Showcase/ArtCheck_Environment.fbx",
}


def main():
    kit = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", "Environment", "EnvironmentKit.json"), encoding="utf-8"))["assets"]
    scenes = json.load(open(os.path.join(HERE, "scenes", "env_sheet.json"), encoding="utf-8"))
    ex = next(s for s in scenes if s["name"] == "env_example")["params"]
    items = []
    for it in ex["items"]:
        name = os.path.splitext(os.path.basename(it["url"]))[0]
        path = kit[name]["fbx"] if name in kit else FIXED.get(name)
        if not path:
            print("overgeslagen (geen FBX):", name)
            continue
        pose = it.get("pose", {})
        items.append({"path": path, "pos": it.get("pos", [0, 0, 0]), "rotY": it.get("rotY", 0.0),
                      "scale": it.get("scale", [1, 1, 1]),
                      "hide": [k for k, v in pose.items() if v.get("visible") is False],
                      "show": [k for k, v in pose.items() if v.get("visible") is True]})
    layout = {
        "camera": {"target": [float(v) for v in ex["target"].split(",")], "yaw": ex["yaw"], "pitch": ex["pitch"],
                   "dist": ex["dist"], "fov": ex["fov"], "far": ex.get("far", 700)},
        "light": {"az": ex["lightAz"], "el": ex["lightEl"], "intensity": 1.15, "color": "#FFE3BC"},
        "fog": {"color": "#91A8E6", "near": ex.get("fogNear", 28), "far": ex.get("fogFar", 240)},
        "sky": "#85A4EC",
        "items": items,
    }
    path = os.path.join(ROOT, OUT)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    json.dump(layout, open(path, "w", encoding="utf-8", newline="\n"), indent=1)
    unity_assets.folder_meta(ROOT, os.path.dirname(OUT))
    unity_assets.write(path + ".meta", "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n"
                                      "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % unity_assets.guid_for(OUT))
    print("layout:", OUT, len(items), "items")


if __name__ == "__main__":
    main()
