"""Z-fight-controle voor Grand Whistleton: per module en over de hele opstelling (GrandStation.json).

    python ArtSource/Environment/GrandStation/zcheck.py            # alles
    python ArtSource/Environment/GrandStation/zcheck.py --modules  # alleen binnen modules
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import zfight  # noqa: E402

EXP = os.path.join(ROOT, "ArtSource", "Exports", "Environment", "GrandStation")


def main():
    layout = json.load(open(os.path.join(EXP, "GrandStation.json"), encoding="utf-8"))
    names = sorted({m["module"] for m in layout["modules"]})
    cache = {n: zfight.glb_triangles(os.path.join(EXP, n + ".glb")) for n in names}
    bad = 0
    for n in names:
        res = zfight.find(cache[n])
        bad += len(res)
        if res:
            zfight.report(res, n)
    print("binnen modules:", bad)
    if "--modules" in sys.argv:
        return
    groups = []
    for k, m in enumerate(layout["modules"]):
        tris = [t for _, t in cache[m["module"]]]
        import numpy as np
        allt = np.concatenate(tris) if tris else np.zeros((0, 3, 3))
        groups.append((f'{m["module"]}#{k}', zfight.place(allt, m["position"], m["yaw"])))
    res = zfight.find(groups, same_group=False)
    # onderkanten op of onder maaiveld zijn nooit zichtbaar (staan op de grond of in een ander volume)
    res = [r for r in res if not (r[5][1] < -0.9 and r[4][1] < 0.02)]
    zfight.report(res, "tussen modules (zichtbare kanten)")


if __name__ == "__main__":
    main()
