"""Controleert het stationnetje, het hoofdstation en de perronkit (issues #182, #207). Draai na build_station_kit.py:

    python ArtSource/Tests/test_station_kit.py

* perronrand vrij van loc, tender en wagons (hun echte geometrie uit de GLB's, op het hart van het spoor)
* perronmodules: bovenkant 1,05, voorrand 1,65, begin op x = 0 en einde op x = lengte (sluiten aan)
* gebouw: vloer op perronhoogte, deur en loket ruim genoeg voor de personages, vrije hoogte onder de luifel
* elk asset met een botsvorm Col_*, kleuren alleen uit palette.json, budget per asset
* geen dubbele vlakken (z-fighting): niet binnen een asset, niet tussen de assets in de voorbeeldopstelling,
  en aan elkaar geklikte hekken/perronstukken vallen niet samen (ArtSource/Tools/zfight.py)
* spullen onder de luifel staan los van elkaar, van de palen en van de plint
* hoofdstation: dubbele deur ruim genoeg, briefingruimte met ankers op de vloer en binnen de muren
* aanpasbare borden (#209): letterset dekt de voorbeeldnamen, de Unity-indeling (StationSign) geeft precies de
  gebakken tekst, bordletters zonder dubbele vlakken
"""
import json
import math
import os
import re
import struct
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Environment", "Station"))
import main_station as MS  # noqa: E402
import station_building as B  # noqa: E402
import station_common as C  # noqa: E402
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Render"))
import zfight as Z  # noqa: E402

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
    decorative = {"Prop_WallLantern", "Prop_HangingSign", "Prop_Banner", "Prop_PendantLamp", "Prop_Ticket"}
    budgets = {"Station_Building": 50000, "Station_Main": 130000}   # hoofdstation: gevel, vleugels én interieur
    for name, a in KIT["assets"].items():
        cols = [n for n in a["nodes"] if n.startswith("Col_")]
        if name not in decorative:
            assert cols, f"{name}: geen botsvorm"
        budget = budgets.get(name, 8000)
        assert a["tris"] < budget, f"{name}: {a['tris']} driehoeken"
    for mname, m in KIT["materials"].items():
        assert m["ramp"] in RAMPS, f"{mname}: ramp niet uit palette.json"
    assert "Door" in KIT["assets"]["Station_Building"]["nodes"]
    assert "Col_Door" in KIT["assets"]["Station_Building"]["nodes"]
    assert "Boom" in KIT["assets"]["Prop_LevelCrossing"]["nodes"]
    for n in ("Door_L", "Door_R", "Col_Door_L", "Col_Door_R", "Spawn_Briefing", "Briefing_Board"):
        assert n in KIT["assets"]["Station_Main"]["nodes"], f"Station_Main mist {n}"
    print(f"PASS assets: {len(KIT['assets'])} modellen, {len(KIT['materials'])} materialen uit palette.json")


def test_no_double_faces():
    """Elk asset op zich: geen twee zichtbare vlakken die (bijna) samenvallen en overlappen."""
    station = os.path.join(EXP, "Environment", "Station")
    bad = []
    for name in KIT["assets"]:
        res = Z.find(Z.glb_triangles(os.path.join(station, name + ".glb")))
        if res:
            la, lb, ar, dz, c, n = res[0]
            bad.append(f"{name}: {len(res)}x, bv. {la} <> {lb} {ar * 1e4:.0f} cm² rond {np.round(c, 2).tolist()}")
    assert not bad, "dubbele vlakken:\n  " + "\n  ".join(bad)
    print(f"PASS dubbele vlakken: {len(KIT['assets'])} assets schoon (afstand < {Z.GAP * 1000:.0f} mm, "
          f"overlap > {Z.MIN_AREA * 1e4:.0f} cm²)")


def test_layout_no_double_faces():
    """De voorbeeldopstelling (perron, gebouw, props) en aan elkaar geklikte stukken: niets valt samen."""
    import station_scenes as S
    groups = []
    items = [it for it in S.station_scene()[0]["params"]["items"] if it["url"].startswith(S.S)]
    # extra: elk perronstuk en het hek tegen een kopie van zichzelf geklikt
    for name, a in KIT["assets"].items():
        if name.startswith("Platform_Straight") or name == "Prop_Fence_2m":
            L = float(re.search(r"_(\d+(?:\.\d+)?)m(?:_|$)", name).group(1))   # Straight_3m(_NoStripe) / Fence_2m
            items += [{"url": S.S + name + ".glb", "pos": [100.0, 0, 50.0 + 10 * len(items)]},
                      {"url": S.S + name + ".glb", "pos": [100.0 + L, 0, 50.0 + 10 * len(items)]}]
    for k, it in enumerate(items):
        path = os.path.normpath(os.path.join(ROOT, "ArtSource", "Render", it["url"]))
        for _, T in Z.glb_triangles(path):
            groups.append((f"{k}:{os.path.basename(path)[:-4]}", Z.place(T, it.get("pos", [0, 0, 0]), it.get("rotY", 0))))
    res = Z.find(groups, same_group=False, ground=0.0)
    assert not res, "dubbele vlakken tussen assets: " + "; ".join(f"{a} <> {b} {ar * 1e4:.0f} cm²" for a, b, ar, *_ in res[:5])
    print(f"PASS opstelling: {len(items)} geplaatste stukken zonder samenvallende vlakken")


def test_leanto_furniture_free():
    """Bank, tonnen en kratten onder de luifel: geen overlap met elkaar, de palen of de plint van het gebouw."""
    boxes = []
    for name, (m, c), R, t in B._furniture():
        V = np.vstack([shape.transform(R=R, t=t).V for shape in (c if isinstance(c, tuple) else (c,))])
        boxes.append((name, V.min(0), V.max(0)))
    for z in (-0.30, -B.D / 2, -B.D + 0.30):
        boxes.append((f"paal z={z:.2f}", np.array([B.LPOST_X - 0.2, 0, z - 0.2]), np.array([B.LPOST_X + 0.2, 9, z + 0.2])))
    boxes.append(("plint", np.array([-B.HW - 0.16, 0, -B.D - 0.2]), np.array([B.HW, 9, 0.2])))
    for i in range(len(boxes)):
        for j in range(i + 1, len(boxes)):
            (na, a0, a1), (nb, b0, b1) = boxes[i], boxes[j]
            if na.startswith(("paal", "plint")) and nb.startswith(("paal", "plint")):
                continue
            gap = np.max(np.maximum(b0 - a1, a0 - b1))
            assert gap > 0.02, f"{na} en {nb} staan in elkaar (speling {gap * 100:.1f} cm)"
    print(f"PASS luifel: {len(B._furniture())} spullen staan vrij")


def test_loose_station_props():
    expected = {
        "Station_Building": {"Bench": 3, "Barrel": 2, "CrateStack": 1, "Stool": 1, "PottedPlant": 1, "Suitcase": 1},
        "Station_Main": {"Bench": 6, "LuggageCart": 1, "CrateStack": 1, "PottedPlant": 3, "Bell": 1, "Mug": 2},
    }
    for name, nodes in (("Station_Building", B.build(np.random.default_rng([2026, 1821]))), ("Station_Main", MS.build())):
        counts = {}
        bounds = {}
        for node, mesh, parent, position, rotation, visible in nodes:
            if not node.startswith("Loose_"):
                continue
            kind = node.split("_")[1]
            counts[kind] = counts.get(kind, 0) + 1
            assert node in KIT["assets"][name]["nodes"], f"{node}: ontbreekt in export"
            assert parent is None and mesh is not None and visible, node
            colliders = [n for n in nodes if n[0].startswith("Col_") and n[2] == node]
            assert colliders and all(n[1] is not None and not n[5] for n in colliders), node
            vertices = np.vstack([n[1].transform(R=rotation, t=position).V for n in colliders])
            lo, hi = vertices.min(0), vertices.max(0)
            for other, (other_lo, other_hi) in bounds.items():
                assert np.max(np.maximum(other_lo - hi, lo - other_hi)) >= -0.001, (node, other)
            bounds[node] = lo, hi
        assert counts == expected[name], (name, counts)
    print("PASS losse stationsprops: eigen zichtbare mesh en botsvorm in beide stations")


def test_main_station():
    """Hoofdstation: deur, vrije hoogte binnen, briefingankers op de vloer en binnen de muren."""
    heights = [json.load(open(os.path.join(EXP, f + ".materials.json"), encoding="utf-8"))["height_m"]
               for f in ("Conductor", "ElderlyPassenger", "Tourist", "RailMechanic")]
    tallest = max(heights)
    assert abs(MS.FL - C.PLATFORM_TOP) < 1e-9, "vloer niet op perronhoogte"
    door_w = MS.DOOR[1] - MS.DOOR[0]
    door_h = MS.DOOR[2] + door_w / 2 - MS.FL
    assert door_w >= 2.0 and door_h >= 1.8 * tallest, (door_w, door_h)
    assert MS.TIE_Y - MS.FL >= 2.2 * tallest, "trekbalken te laag"
    nodes = {n[0]: n for n in MS.build()}
    seats = [k for k in nodes if k.startswith("Briefing_Seat_")]
    assert len(seats) >= 6, seats
    for k in ["Spawn_Briefing", "Briefing_Board", "Spot_Coffee", "Spot_Lockers"] + seats:
        _, _, parent, position, _, _ = nodes[k]
        if k in seats:
            assert parent.startswith("Loose_Bench_"), k
            position = nodes[parent][4] @ position + nodes[parent][3]
        x, y, z = position
        m = 0.05 if k == "Briefing_Board" else 0.2          # het bord hangt tegen de achterwand
        assert MS.XI0 + 0.3 < x < MS.XI1 - 0.3 and MS.ZI0 + m < z < MS.ZI1 - 0.2, f"{k} buiten de ruimte: {x, z}"
        assert MS.FL - 0.01 <= y <= MS.FL + 3.5, f"{k} op hoogte {y:.2f}"
    for k in ("Door_L", "Door_R"):
        assert "Col_" + k in nodes and nodes["Col_" + k][2] == k, f"{k}: botsvorm hangt niet aan de deur"
    print(f"PASS hoofdstation: deur {door_w:.2f}x{door_h:.2f} m, {len(seats)} zitplaatsen, "
          f"ruimte {MS.XI1 - MS.XI0:.1f}x{MS.ZI1 - MS.ZI0:.1f} m")


def test_swing_doors():
    """Elk draaiend blad (deur, kluisdeurtje, staldeur, hekpoort) heeft een botsvorm die meedraait en de twee
    scharnierstanden waar SwingDoor in Unity zijn grenzen uit haalt; de beginstand ligt tussen dicht en open."""
    import farm_props as FP
    import main_station_props as MP

    def yaw(R):
        return math.degrees(math.atan2(-R[2, 0], R[0, 0]))                 # mk.rot_y(a) -> a
    models = {"Station_Main": MS.build(), "Station_Building": B.build(np.random.default_rng([2026, 1821])),
              "Farm_Barn": FP.barn(), "Farm_Gate": FP.gate()}
    lk, lk_cols, lk_doors = MP.lockers()
    models["Prop_Lockers"] = MP.locker_door_nodes(lk_doors)
    expect = {"Station_Main": {"Door_L", "Door_R"} | {f"LockerDoor_{k}" for k in range(1, 6)},
              "Station_Building": {"Door"}, "Farm_Barn": {"Door_L", "Door_R"}, "Farm_Gate": {"Gate"},
              "Prop_Lockers": {f"LockerDoor_{k}" for k in range(1, 5)}}
    count = 0
    for model, nodes in models.items():
        by = {n[0]: n for n in nodes}
        doors = {k[len("Hinge_"):-len("_Closed")] for k in by if k.startswith("Hinge_") and k.endswith("_Closed")}
        assert doors == expect[model], f"{model}: draaiende bladen {sorted(doors)}"
        for d in doors:
            leaf, closed, opened = by[d], by[f"Hinge_{d}_Closed"], by[f"Hinge_{d}_Open"]
            assert by[f"Col_{d}"][2] == d, f"{model}/{d}: botsvorm hangt niet aan het blad"
            assert leaf[2] is None and closed[2] is None and opened[2] is None, f"{model}/{d}: knopen horen onder de wortel"
            assert np.allclose(leaf[3], closed[3]) and np.allclose(leaf[3], opened[3]), f"{model}/{d}: scharnier verschilt"
            assert abs(leaf[1].V[:, 0]).min() < 0.05 and abs(leaf[1].V[:, 2]).max() < 0.20, f"{model}/{d}: pivot niet op het scharnier"
            rel = lambda n: yaw(closed[4].T @ n[4])
            span, start = rel(opened), rel(leaf)
            assert 80 <= abs(span) <= 120, f"{model}/{d}: draait {span:.0f} graden"
            assert min(0, span) - 0.5 <= start <= max(0, span) + 0.5, f"{model}/{d}: beginstand {start:.0f} buiten 0..{span:.0f}"
            count += 1
    assert len(lk_cols) >= 6, "kluisjes: de kast hoort losse botsvormen te hebben (open vakken)"
    print(f"PASS draaiende bladen: {count} deuren, kluisdeurtjes en poorten met botsvorm en scharnierstanden")


def test_station_signs():
    """StationSign in Unity bouwt de tekst op zoals de generator; alle voorbeeldnamen zijn te zetten."""
    import station_props as P
    from station_common import GLYPHS, text_mesh
    data = json.load(open(os.path.join(ROOT, "Assets", "Art", "Models", "Environment", "SignGlyphs", "StationGlyphs.json"),
                          encoding="utf-8"))
    adv = {g["c"]: g["advance"] for g in data["glyphs"]}
    cs = open(os.path.join(ROOT, "Assets", "Scripts", "Stations", "StationSign.cs"), encoding="utf-8").read()
    names = cs[cs.index("ExampleNames ="):cs.index("};", cs.index("ExampleNames ="))]
    import re as _re
    for name in _re.findall(r'"([^"]+)"', names):
        missing = {c for c in name.upper() if c != " " and c not in adv}
        assert not missing, f"{name}: geen letter voor {missing}"
    want = {("Station_Main", "Sign_Name"): "WHISTLETON", ("Station_Main", "Sign_Route"): "ROUTE",
            ("Station_Building", "Sign_Name"): "STATION", ("Prop_StationSign", "Sign_Name"): "STATION"}
    got = {(s["asset"], s["node"]): s for s in data["signs"]}
    assert set(want) <= set(got), set(want) - set(got)
    sizes = {("Station_Main", "Sign_Name"): (5.0, 0.92, 0.14), ("Station_Main", "Sign_Route"): (2.2, 0.56, 0.10),
             ("Station_Building", "Sign_Name"): (3.10, 0.70, 0.12), ("Prop_StationSign", "Sign_Name"): (2.3, 0.66, 0.10)}
    glyph = {c: text_mesh(c, 1.0, 1.0, "M_Stn_Cream")[0].transform(t=(GLYPHS[c][0] / 2, 0.5, 0.0)) for c in adv}
    for key, text in want.items():
        s = got[key]
        assert s["text"] == text, (key, s["text"])
        # zelfde rekensom als StationSign.Rebuild (X niet gespiegeld)
        x, placed = 0.0, []
        for c in text:
            if c == " ":
                x += data["space"]
                continue
            placed.append((c, x))
            x += adv[c] + data["spacing"]
        units = x - data["spacing"]
        th, iw, gap = s["letterHeight"], s["iconWidth"], s["iconGap"]
        total = iw + gap + units * th
        k = s["room"] / total if total > s["room"] else 1.0
        th, iw, gap, total = th * k, iw * k, gap * k, total * k
        start = -total / 2 + iw + gap
        V = np.vstack([glyph[c].V * [th, th, s["textDepth"]] + [start + px * th, -0.5 * th, s["textZ"]] for c, px in placed])
        _, _, txt, i = P.station_board_parts(text, *sizes[key])
        B = txt.transform(t=(i["textX"], 0.0, i["textZ"])).V
        assert np.abs(np.sort(V, 0) - np.sort(B, 0)).max() < 1e-4, f"{key}: Unity-indeling wijkt af"
        assert abs(i["fit"] - s["iconScale"]) < 1e-4
    res = Z.find(Z.glb_triangles(os.path.join(EXP, "Environment", "Station", "StationGlyphs.glb")))
    assert not res, f"bordletters: {len(res)} dubbele vlakken"
    print(f"PASS borden: {len(got)} aanpasbare borden, {len(adv)} letters, indeling gelijk aan de generator")


if __name__ == "__main__":
    test_train_clearance()
    test_platform_modules()
    test_building_walkable()
    test_assets()
    test_no_double_faces()
    test_layout_no_double_faces()
    test_leanto_furniture_free()
    test_loose_station_props()
    test_main_station()
    test_swing_doors()
    test_station_signs()
