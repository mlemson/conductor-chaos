"""Werkt de thumbnail-renders in scenes/env_sheet.json bij uit het kit-manifest (EnvironmentKit.json).

De voorbeeldscène ('env_example') en eventuele combinatie-renders ('env_combo_*') blijven staan;
voor elk asset uit de kit komt één camera die het asset netjes in beeld brengt.
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SCENES = os.path.join(HERE, "scenes", "env_sheet.json")
E = "../Exports/Environment/"


def main():
    man = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", "Environment", "EnvironmentKit.json"), encoding="utf-8"))["assets"]
    keep = [s for s in json.load(open(SCENES, encoding="utf-8")) if not s["name"].startswith("env_") or
            s["name"] == "env_example" or s["name"].startswith("env_combo")]
    thumbs = []
    for name, a in man.items():
        mn, mx = a["min"], a["max"]
        c = [(mn[i] + mx[i]) / 2 for i in range(3)]
        size = [mx[i] - mn[i] for i in range(3)]
        rad = 0.5 * math.sqrt(sum(s * s for s in size))
        big = a["category"] in ("Mountains", "Sky")
        dist = 1.25 * rad / math.tan(math.radians(15))
        thumbs.append({"name": "env_" + name, "params": {
            "w": 700, "h": 600, "ss": 1.3, "fov": 30, "yaw": 8 if big else 28, "pitch": 6 if big else 14,
            "dist": round(dist, 2), "target": "%.2f,%.2f,%.2f" % tuple(c), "lightAz": -35, "lightEl": 50,
            "groundShadowOnly": 1, "groundR": round(rad * 2.5, 2), "shadowSize": round(rad * 1.6, 2),
            "far": round(dist * 4 + 50, 1), "items": [{"url": E + name + ".glb"}]}})
    json.dump(thumbs + keep, open(SCENES, "w", encoding="utf-8"), indent=1)
    print("env scenes:", len(thumbs), "thumbnails +", len(keep))


if __name__ == "__main__":
    main()
