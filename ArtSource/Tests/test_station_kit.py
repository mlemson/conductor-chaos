"""Controleert het stationnetje en de perronkit (issue #182). Draai na build_station_kit.py:

    python ArtSource/Tests/test_station_kit.py

* perronrand vrij van loc, tender en wagons (hun echte geometrie uit de GLB's, op het hart van het spoor)
* perronmodules: bovenkant 1,05, voorrand 1,65, begin op x = 0 en einde op x = lengte (sluiten aan)
* gebouw: vloer op perronhoogte, deur en loket ruim genoeg voor de personages, vrije hoogte onder de luifel
* elk asset met een botsvorm Col_*, kleuren alleen uit palette.json, budget per asset
"""
import json
import os
import struct
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Environment", "Station"))
import station_building as B  # noqa: E402
import station_common as C  # noqa: E402

EXP = os.path.join(ROOT, "ArtSource", "Exports")
KIT = json.load(open(os.path.join(EXP, "Environment", "Station", "StationKit.json"), encoding="utf-8"))
PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = [v["ramp"] for g in PALETTE["groups"].values() for v in g.values()]


def _quat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def glb_vertices(path):
    """Alle vertices van zichtbare meshes in wereldcoördinaten (knooptransformaties toegepast)."""
    b = open(path, "rb").read()
    n = struct.unpack("<I", b[12:16])[0]
    js = json.loads(b[20:20 + n])
    off = 20 + n
    bl = struct.unpack("<I", b[off:off + 4])[0]
    data = b[off + 8:off + 8 + bl]

    def acc(i):
        a = js["accessors"][i]
        bv = js["bufferViews"][a["bufferView"]]
        st = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        return np.frombuffer(data[st:st + a["count"] * 12], dtype="<f4").reshape(-1, 3)
    out = []

    def walk(i, M):
        nd = js["nodes"][i]
        L = np.eye(4)
        if "matrix" in nd:
            L = np.array(nd["matrix"]).reshape(4, 4).T
        else:
            R = np.eye(4)
            R[:3, :3] = _quat(nd.get("rotation", [0, 0, 0, 1]))
            T = np.eye(4)
            T[:3, 3] = nd.get("translation", [0, 0, 0])
            L = T @ R @ np.diag(list(nd.get("scale", [1, 1, 1])) + [1])
        W = M @ L
        if "mesh" in nd:
            for pr in js["meshes"][nd["mesh"]]["primitives"]:
                V = acc(pr["attributes"]["POSITION"])
                out.append(V @ W[:3, :3].T + W[:3, 3])
        for c in nd.get("children", []):
            walk(c, W)
    for r in js["scenes"][0]["nodes"]:
        walk(r, np.eye(4))
    return np.vstack(out)


def test_train_clearance():
    # treinframe: railhoogte y = -0,62 -> +1,05 boven de spoorpivot; hartlijn z = -1,35 (loc/tender) of -1,30 (wagens)
    for name, zc in (("Locomotive_Blockout", -1.35), ("Tender_Blockout", -1.35), ("RegionalWagon_Blockout", -1.30),
                     ("CattleWagon_Blockout", -1.30)):
        V = glb_vertices(os.path.join(EXP, name + ".glb"))
        y = V[:, 1] + C.PLATFORM_TOP
        dz = np.abs(V[:, 2] - zc)
        below = y < C.PLATFORM_TOP + 0.01                 # alles wat lager hangt dan de perronvloer
        worst = float(dz[below].max())
        assert worst < C.PLATFORM_EDGE - 0.04, f"{name} steekt {worst:.2f} m uit (perronrand {C.PLATFORM_EDGE})"
        print(f"PASS clearance {name}: max {worst:.2f} m onder perronhoogte, rand op {C.PLATFORM_EDGE} m")


def test_platform_modules():
    for name, a in KIT["assets"].items():
        if not name.startswith("Platform_"):
            continue
        V = glb_vertices(os.path.join(EXP, "Environment", "Station", name + ".glb"))
        top = float(V[:, 1].max())
        assert abs(a["platformTop"] - C.PLATFORM_TOP) < 1e-6
        if "Stairs" not in name and "Ramp" not in name:
            assert C.PLATFORM_TOP - 0.001 <= top <= C.PLATFORM_TOP + 0.04, f"{name}: bovenkant {top:.3f}"
        zmin = float(V[:, 2].min())
        assert zmin >= C.PLATFORM_EDGE - 0.04, f"{name}: voorrand op {zmin:.3f}"
        xmin = float(V[:, 0].min())
        assert xmin >= -0.07, f"{name}: begint op x = {xmin:.3f}"
        print(f"PASS {name}: x {xmin:.2f}..{V[:, 0].max():.2f}, z {zmin:.2f}..{V[:, 2].max():.2f}, top {top:.3f}")


def test_building_walkable():
    heights = [json.load(open(os.path.join(EXP, f + ".materials.json"), encoding="utf-8"))["height_m"]
               for f in ("Conductor", "ElderlyPassenger", "Tourist", "RailMechanic")]
    tallest = max(heights)
    assert abs(B.FL - C.PLATFORM_TOP) < 1e-9, "vloer niet op perronhoogte"
    door_w, door_h = B.DOOR[1] - B.DOOR[0], B.DOOR[2] + (B.DOOR[1] - B.DOOR[0]) / 2 - B.FL
    side_w, side_h = B.SIDE_DOOR[1] - B.SIDE_DOOR[0], B.SIDE_DOOR[2] + (B.SIDE_DOOR[1] - B.SIDE_DOOR[0]) / 2 - B.FL
    assert door_w >= 1.2 and door_h >= 1.6 * tallest, (door_w, door_h)
    assert side_w >= 1.1 and side_h >= 1.6 * tallest, (side_w, side_h)
    assert B.EAVE - B.FL >= 2.3 * tallest
    # vrije hoogte onder de luifelbalk
    x = B.LPOST_X + 0.15
    y_hi = B.L_TOP_AT_GUTTER + (B.ROOF_HALF - B.HW) * np.tan(B.L_PITCH)
    beam_bot = y_hi - (-B.HW - x) * np.tan(B.L_PITCH) - B.ROOF_THICK / np.cos(B.L_PITCH) - 0.26
    assert beam_bot - B.FL >= 1.1 * tallest, f"luifel: {beam_bot - B.FL:.2f} m vrij"
    # luifeldak loopt onder de goot van het hoofddak door
    assert B.L_TOP_AT_GUTTER + 0.12 < B.roof_y_under(B.ROOF_HALF) + 0.0, "luifel raakt hoofddak"
    print(f"PASS gebouw: deur {door_w:.2f}x{door_h:.2f} m, zijdeur {side_w:.2f}x{side_h:.2f} m, "
          f"luifel {beam_bot - B.FL:.2f} m vrij (grootste personage {tallest:.2f} m)")


def test_assets():
    decorative = {"Prop_WallLantern", "Prop_HangingSign"}
    for name, a in KIT["assets"].items():
        cols = [n for n in a["nodes"] if n.startswith("Col_")]
        if name not in decorative:
            assert cols, f"{name}: geen botsvorm"
        budget = 50000 if name == "Station_Building" else 8000
        assert a["tris"] < budget, f"{name}: {a['tris']} driehoeken"
    for mname, m in KIT["materials"].items():
        assert m["ramp"] in RAMPS, f"{mname}: ramp niet uit palette.json"
    assert "Door" in KIT["assets"]["Station_Building"]["nodes"]
    assert "Col_Door" in KIT["assets"]["Station_Building"]["nodes"]
    assert "Boom" in KIT["assets"]["Prop_LevelCrossing"]["nodes"]
    print(f"PASS assets: {len(KIT['assets'])} modellen, {len(KIT['materials'])} materialen uit palette.json")


if __name__ == "__main__":
    test_train_clearance()
    test_platform_modules()
    test_building_walkable()
    test_assets()
