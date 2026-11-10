"""Stationnetje + hoofdstation + perronkit + props bouwen (issues #182, #207). Zie de modules in deze map voor maten
en conventies.

    python ArtSource/Environment/Station/build_station_kit.py                 # alles
    python ArtSource/Environment/Station/build_station_kit.py main briefing   # alleen hoofdstation + briefingprops

Uitvoer:
  Assets/Art/Models/Environment/Station/Station_Building.fbx
  Assets/Art/Models/Environment/Station/Station_Main.fbx             (hoofdstation met briefingruimte)
  Assets/Art/Models/Environment/SignGlyphs/StationGlyphs.fbx + .json   (bordletters voor aanpasbare namen, #209)
  Assets/Art/Models/Environment/Station/Platform/Platform_*.fbx      (zelfde pivot als een spoorstuk)
  Assets/Art/Models/Environment/Station/Props/Prop_*.fbx             (pivot midden-onder, voorkant +Z)
  Assets/Art/Models/Environment/Station/Farm/Farm_*.fbx              (boerderijdecor, zie farm_props.py)
  Assets/Art/Materials/Environment/Station/M_Stn_*.mat
  ArtSource/Exports/Environment/Station/*.glb + StationKit.json
Col_*-knopen zijn verborgen botsvormen; de prefabbouwer in Unity (Conductor Chaos > Art > Station) maakt er
MeshColliders van. Light_*-knopen zijn ankers voor lampen. Door, Door_L/Door_R, LockerDoor_* en Gate_* draaien om hun
scharnier (Y-as van de knoop); de prefabbouwer hangt er SwingDoor aan zodat je ze met je handen open en dicht doet.
Spawn_*, Briefing_* en Spot_* zijn lege ankers voor gameplay (briefingruimte).
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import main_station as main_station  # noqa: E402
import farm_props as farm  # noqa: E402
import bell_props as bell  # noqa: E402
import main_station_props as mprops  # noqa: E402
import station_building as building  # noqa: E402
import station_platform as platform  # noqa: E402
import station_props as props  # noqa: E402
import tool_props as tools  # noqa: E402
from station_common import GLYPHS, ROOT, mk, text_mesh, write_manifest  # noqa: E402
from station_common import export as _export  # noqa: E402

GLYPH_DIR = "Assets/Art/Models/Environment/SignGlyphs"     # geen prefabs: alleen letters voor StationSign


def export(name, sub, nodes, **kw):
    """export() + de maten van aanpasbare borden (Sign_*) in het manifest, voor StationSign in Unity."""
    signs = [props.SIGN_INFO[id(n)][1] for n in nodes if props.SIGN_INFO.get(id(n), (None,))[0] is n]
    if signs:
        kw["extra"] = dict(kw.get("extra") or {}, signs=signs)
    return _export(name, sub, nodes, **kw)


def build_glyphs(manifest):
    """Bordletters (kaphoogte 1, diepte 1) als losse knopen G_<code>: StationSign zet er tekst mee in elkaar."""
    nodes = []
    for k, ch in enumerate(sorted(GLYPHS)):
        w = GLYPHS[ch][0]
        m, _ = text_mesh(ch, 1.0, 1.0, "M_Stn_Cream")
        nodes.append((f"G_{ord(ch)}", m.transform(t=(w / 2, 0.5, 0.0)), None, (k * 1.6, 0.0, 0.0), None, True))
    _export("StationGlyphs", None, nodes, density=900, radius=0.05, model_dir=GLYPH_DIR, readable=True)


def write_sign_data():
    """StationGlyphs.json naast de letters: letterbreedtes + alle borden uit StationKit.json."""
    from station_common import EXP_DIR
    import unity_assets
    kit = json.load(open(os.path.join(EXP_DIR, "StationKit.json"), encoding="utf-8"))["assets"]
    data = {"spacing": 0.30, "space": 0.50,
            "glyphs": [{"c": ch, "advance": GLYPHS[ch][0]} for ch in sorted(GLYPHS)],
            "signs": [dict(asset=name, **s) for name, a in sorted(kit.items()) for s in a.get("signs", [])]}
    rel = GLYPH_DIR + "/StationGlyphs.json"
    unity_assets.folder_meta(ROOT, GLYPH_DIR)
    json.dump(data, open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="\n"), indent=1)
    unity_assets.write(os.path.join(ROOT, rel + ".meta"), "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n"
                       "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                       % unity_assets.guid_for(rel))


def simple(name, mesh, col, extra_nodes=()):
    nodes = [("Mesh", mesh, None, (0, 0, 0), None, True)]
    if col is not None:
        colliders = col if isinstance(col, tuple) else (col,)
        for i, part in enumerate(colliders):
            node = "Col_Main" if i == 0 else f"Col_{i + 1}"
            nodes.append((node, part, None, (0, 0, 0), None, False))
    nodes += list(extra_nodes)
    return nodes


def build_props(manifest):
    P = {}
    P["Prop_Bench"] = simple("Prop_Bench", *props.bench(split_colliders=True))
    P["Prop_Barrel"] = simple("Prop_Barrel", *props.barrel())
    P["Prop_CrateStack"] = simple("Prop_CrateStack", *props.crate_stack(split_colliders=True))
    m, c = props.crate()
    P["Prop_Crate"] = simple("Prop_Crate", m, c)
    P["Prop_Planter"] = simple("Prop_Planter", *props.planter())
    P["Prop_TrashBin"] = simple("Prop_TrashBin", *props.trash_bin())
    m, c, light = props.lamp_post()
    P["Prop_LampPost"] = simple("Prop_LampPost", m, c, [("Light_Lamp", None, None, light, None, True)])
    m, _ = props.wall_lantern()
    P["Prop_WallLantern"] = simple("Prop_WallLantern", m, None, [("Light_Lamp", None, None, (0, 0.12, 0.30), None, True)])
    m, _ = props.hanging_sign()
    P["Prop_HangingSign"] = simple("Prop_HangingSign", m, None)
    P["Prop_StationSign"] = simple("Prop_StationSign", *props.station_sign())
    P["Prop_Timetable"] = simple("Prop_Timetable", *props.timetable())
    P["Prop_TicketMachine"] = simple("Prop_TicketMachine", *props.ticket_machine())
    P["Prop_LuggageCart"] = simple("Prop_LuggageCart", *props.luggage_cart())
    m, c, boom, boom_col, pivot = props.level_crossing()
    P["Prop_LevelCrossing"] = simple("Prop_LevelCrossing", m, c, [
        ("Boom", boom, None, pivot, None, True), ("Col_Boom", boom_col, "Boom", (0, 0, 0), None, False)])
    P["Prop_Fence_2m"] = simple("Prop_Fence_2m", *props.fence())
    P["Prop_Shelter"] = simple("Prop_Shelter", *props.shelter())
    for name, nodes in P.items():
        export(name, "Props", nodes, density=900, radius=0.12, manifest=manifest)


def build_briefing_props(manifest):
    """Losse props uit de briefingruimte van het hoofdstation (issue #207), voor hergebruik."""
    P = {}
    lk, lk_cols, lk_doors = mprops.lockers()              # deurtjes los: LockerDoor_1..4 draaien om hun scharnier
    P["Prop_Lockers"] = simple("Prop_Lockers", lk, lk_cols, mprops.locker_door_nodes(lk_doors))
    P["Prop_RouteBoard"] = simple("Prop_RouteBoard", *mprops.route_board_prop())
    P["Prop_NoticeBoard"] = simple("Prop_NoticeBoard", *mprops.notice_board_prop())
    P["Prop_CoffeeCounter"] = simple("Prop_CoffeeCounter", *mprops.coffee_counter_prop())
    P["Prop_CoatStand"] = simple("Prop_CoatStand", *mprops.coat_stand())
    P["Prop_TicketBooth"] = simple("Prop_TicketBooth", *mprops.ticket_booth())
    m, _ = mprops.banner()
    P["Prop_Banner"] = simple("Prop_Banner", m, None)
    P["Prop_PendantLamp"] = simple("Prop_PendantLamp", mprops.pendant_lamp(1.2), None,
                                   [("Light_Lamp", None, None, (0, -1.22, 0), None, True)])
    for name, nodes in P.items():
        export(name, "Props", nodes, density=900, radius=0.12, manifest=manifest)


def build_tools(manifest):
    """Gereedschap van de conducteur (issues #215, #216): moersleutel en kaartjestang (beweegbaar) en het kaartje."""
    P = {"Prop_Wrench": simple("Prop_Wrench", *tools.wrench()),
         "Prop_TicketPunch": simple("Prop_TicketPunch", *tools.ticket_punch()),
         "Prop_Ticket": simple("Prop_Ticket", *tools.ticket())}
    for name, nodes in P.items():
        export(name, "Props", nodes, density=3000, radius=0.03, manifest=manifest)


def build_farm(manifest):
    """Boerderijdecor (schuur, stal, hek, poort, drinkbak, hooiberg, ...): zelfde prefabstraat als de stationsprops."""
    for name, (make, density, radius) in farm.ASSETS.items():
        export(name, "Farm", make(), density=density, radius=radius, manifest=manifest)


def build_service(manifest):
    """Vertrekbel op het perron (bel + paal), voor de dienstregeling."""
    for name, (make, density, radius) in bell.ASSETS.items():
        export(name, "Service", make(), density=density, radius=radius, manifest=manifest)


def build_main(manifest):
    export("Station_Main", None, main_station.build(), density=170, radius=0.30, manifest=manifest,
           extra={"floor": main_station.FL, "footprint": [main_station.CX0, main_station.WX1, -main_station.HD, 0.0],
                  "briefingRoom": [main_station.XI0, main_station.XI1, main_station.ZI0, main_station.ZI1]})


def build_platform(manifest, *, tactile=True):
    rng = np.random.default_rng([2026, 182])
    mods = {f"Platform_Straight_{length}m": platform.straight(length, rng, tactile=tactile) for length in (3, 6)}
    # eindstukken in twee varianten: _End (aan het eind van een perron, uitlopend naar +X) en _Start
    # (gespiegeld, aan het begin). Beide liggen tussen x = 0 en x = lengte, dus ze klikken op hetzelfde raster.
    for name, build in (("EndCap", platform.end_cap), ("Corner", platform.corner),
                        ("Ramp", platform.ramp), ("Stairs", platform.stairs)):
        m, c = build(rng, tactile=tactile)
        L = round(float(c.V[:, 0].max()), 3)
        mods[f"Platform_{name}_End"] = (m, c)
        mods[f"Platform_{name}_Start"] = (m.transform(s=(-1, 1, 1), t=(L, 0, 0)), c.transform(s=(-1, 1, 1), t=(L, 0, 0)))
    for name, (m, c) in mods.items():
        if not tactile:
            name += "_NoStripe"
        export(name, "Platform", simple(name, m, c), density=260, radius=0.22, manifest=manifest,
               extra={"pivot": "hart spoor, maaiveld", "platformTop": platform.TOP, "edge": platform.E,
                      "depth": platform.DEPTH})


def build_building(manifest):
    rng = np.random.default_rng([2026, 1821])
    export("Station_Building", None, building.build(rng), density=170, radius=0.30, manifest=manifest,
           extra={"floor": building.FL, "footprint": [building.LX, building.HW, -building.D, 0.0]})


ALL = ("props", "briefing", "tools", "farm", "service", "platform", "building", "main", "glyphs")


def main(which=ALL):
    manifest = {}
    if "props" in which:
        build_props(manifest)
    if "briefing" in which:
        build_briefing_props(manifest)
    if "tools" in which:
        build_tools(manifest)
    if "farm" in which:
        build_farm(manifest)
    if "service" in which:
        build_service(manifest)
    if "platform" in which:
        build_platform(manifest)
    if "platform" in which or "platform-no-stripe" in which:
        build_platform(manifest, tactile=False)
    if "building" in which:
        build_building(manifest)
    if "main" in which:
        build_main(manifest)
    if "glyphs" in which:
        build_glyphs(manifest)
    write_manifest(manifest)
    write_sign_data()


if __name__ == "__main__":
    main(tuple(sys.argv[1:]) or ALL)
