"""Stationnetje + perronkit + props bouwen (issue #182). Zie de modules in deze map voor maten en conventies.

    python ArtSource/Environment/Station/build_station_kit.py

Uitvoer:
  Assets/Art/Models/Environment/Station/Station_Building.fbx
  Assets/Art/Models/Environment/Station/Platform/Platform_*.fbx      (zelfde pivot als een spoorstuk)
  Assets/Art/Models/Environment/Station/Props/Prop_*.fbx             (pivot midden-onder, voorkant +Z)
  Assets/Art/Materials/Environment/Station/M_Stn_*.mat
  ArtSource/Exports/Environment/Station/*.glb + StationKit.json
Col_*-knopen zijn verborgen botsvormen; de prefabbouwer in Unity (Conductor Chaos > Art > Station) maakt er
MeshColliders van. Light_*-knopen zijn ankers voor lampen, Door draait om zijn scharnier (Y-as).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import station_building as building  # noqa: E402
import station_platform as platform  # noqa: E402
import station_props as props  # noqa: E402
from station_common import export, mk, write_manifest  # noqa: E402


def simple(name, mesh, col, extra_nodes=()):
    nodes = [("Mesh", mesh, None, (0, 0, 0), None, True)]
    if col is not None:
        nodes.append(("Col_Main", col, None, (0, 0, 0), None, False))
    nodes += list(extra_nodes)
    return nodes


def build_props(manifest):
    P = {}
    P["Prop_Bench"] = simple("Prop_Bench", *props.bench())
    P["Prop_Barrel"] = simple("Prop_Barrel", *props.barrel())
    P["Prop_CrateStack"] = simple("Prop_CrateStack", *props.crate_stack())
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


def main(which=("props", "platform", "building")):
    manifest = {}
    if "props" in which:
        build_props(manifest)
    if "platform" in which:
        build_platform(manifest)
    if "platform" in which or "platform-no-stripe" in which:
        build_platform(manifest, tactile=False)
    if "building" in which:
        build_building(manifest)
    write_manifest(manifest)


if __name__ == "__main__":
    main(tuple(sys.argv[1:]) or ("props", "platform", "building"))
