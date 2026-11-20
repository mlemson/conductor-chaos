"""Bouwt de modules van Grand Whistleton en de opstelling voor Unity.

    python ArtSource/Environment/GrandStation/build_grand_station.py            # alles
    python ArtSource/Environment/GrandStation/build_grand_station.py hall tower # alleen deze modules

Uitvoer: Assets/Art/Models/Environment/GrandStation/GS_*.fbx (+ meta), materialen in de stationsmap,
Assets/Art/Layouts/GrandStationKit.json (botsboxen per module) en Assets/Art/Layouts/GrandStation.json (opstelling).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gs_kit as kit  # noqa: E402
import gs_building as bld  # noqa: E402
import gs_site as site  # noqa: E402
import gs_interior as gi  # noqa: E402

MODULES = {}


def module(name, density=150, radius=0.30):
    def deco(fn):
        MODULES[name] = (fn, density, radius)
        return fn
    return deco


def nodes_for(name, mesh, lights=(), anchors=(), extra_nodes=()):
    nodes = [("Mesh", mesh, None, (0, 0, 0), None, True)]
    for lname, p in lights:
        nodes.append((lname, None, None, tuple(p), None, True))
    for aname, p, rot in anchors:
        nodes.append((aname, None, None, tuple(p), rot, True))
    return nodes + list(extra_nodes)


@module("GS_Hall_Facade", 140, 0.35)
def hall_facade():
    m, boxes, lights = bld.hall_facade()
    import station_props as props
    board, sign = props.sign_nodes("Sign_Name", "WHISTLETON", 11.5, 1.0, 0.20, (0.0, (bld.NAME_Y[0] + bld.NAME_Y[1]) / 2, 0.42))
    return nodes_for("GS_Hall_Facade", kit.mk.merge(m, board), lights, extra_nodes=sign), boxes


@module("GS_Hall_Shell", 110, 0.40)
def hall_shell():
    m, boxes, lights = bld.hall_shell()
    off = (0.0, 0.0, -20.0)
    m = m.transform(t=off)
    lights = [("Light_Hall_%d" % (k + 1), (x, y, z - 20.0)) for k, (x, y, z) in enumerate(lights)]
    return nodes_for("GS_Hall_Shell", m, lights), boxes.placed(0, off)


@module("GS_Wing_Link", 140, 0.30)
def link():
    m, boxes, lights = bld.link_wing()
    return nodes_for("GS_Wing_Link", m, lights), boxes


@module("GS_Wing_Pavilion", 140, 0.30)
def pavilion():
    m, boxes, lights = bld.pavilion()
    return nodes_for("GS_Wing_Pavilion", m, lights), boxes


@module("GS_ClockTower", 160, 0.25)
def tower():
    m, boxes, lights = bld.clock_tower()
    return nodes_for("GS_ClockTower", m, lights), boxes


def simple(name, fn, density=700, radius=0.14):
    """Module uit een functie die (mesh, boxes, ankers, signknopen) of (mesh, boxes, lights) teruggeeft."""
    def build():
        out = fn()
        m, boxes = out[0], out[1]
        lights, anchors, signs = [], [], []
        for item in out[2:]:
            for e in item if isinstance(item, (list, tuple)) else []:
                if isinstance(e, tuple) and len(e) == 3 and isinstance(e[0], str) and e[0].startswith(("Spot_", "Spawn_")):
                    anchors.append(e)
                elif isinstance(e, tuple) and len(e) == 2 and isinstance(e[0], str):
                    lights.append(e)
                elif isinstance(e, tuple) and len(e) == 6:
                    signs.append(e)
        return nodes_for(name, m, lights, anchors, signs), boxes
    MODULES[name] = (build, density, radius)


def _lights(prefix, pts):
    return [("%s_%d" % (prefix, k + 1), p) for k, p in enumerate(pts)]


simple("GS_Apron_6m", lambda: site.apron(*site.APRON, "a"), 400, 0.25)
simple("GS_Apron_Hall_6m", lambda: site.apron(*site.APRON_HALL, "h"), 400, 0.25)
simple("GS_Platform_Island_6m", site.island_platform, 400, 0.25)
simple("GS_Platform_Island_End", site.island_end, 400, 0.25)
simple("GS_PlatformCanopy_Side_6m", lambda: (lambda m, b, l: (m, b, _lights("Light_Canopy", l)))(*site.canopy_side()), 500, 0.2)
simple("GS_PlatformCanopy_Island_6m", lambda: (lambda m, b, l: (m, b, _lights("Light_Canopy", l)))(*site.canopy_island()), 500, 0.2)
simple("GS_Footbridge_Landing", site.bridge_landing, 500, 0.2)
simple("GS_Footbridge_Stair", lambda: site.bridge_stair()[:2], 500, 0.2)
simple("GS_Footbridge_Span", lambda: site.bridge_span(SPAN), 500, 0.2)
simple("GS_EntranceCanopy_6m", site.entrance_canopy_bay, 500, 0.2)
simple("GS_EntranceCanopy_Column", site.entrance_column, 600, 0.15)
simple("GS_Forecourt", site.forecourt, 90, 0.4)
simple("GS_Square", site.square, 60, 0.5)
simple("GS_StreetLamp", site.street_lamp, 900, 0.1)
simple("GS_TicketCounter", gi.ticket_counter)
simple("GS_InfoDesk", gi.info_desk)
simple("GS_Kiosk", gi.kiosk)
simple("GS_CafeTable", gi.cafe_table, 900, 0.1)
simple("GS_PedestalClock", gi.pedestal_clock)
simple("GS_DepartureBoard", gi.departure_board)
simple("GS_Partition_Office", lambda: gi.partition(11.4, -3.0, "Sign_Room", "PERSONEEL"))
simple("GS_Partition_Storage", lambda: gi.partition(11.4, 0.0, "Sign_Room", "OPSLAG", glass=False))
simple("GS_OfficeFurniture", gi.office_furniture)
simple("GS_Podium", gi.podium)
simple("GS_MissionTable", gi.mission_table)
simple("GS_StorageShelf", gi.storage_shelf, 800, 0.12)
simple("GS_PlatformSign", gi.platform_sign, 900, 0.1)
simple("GS_DirectionSign", gi.direction_sign, 900, 0.1)
simple("GS_WallSign", gi.wall_sign, 900, 0.1)
simple("GS_Poster", gi.poster, 900, 0.1)
simple("GS_Rug", gi.rug, 900, 0.1)

FB_X = 33.0                                    # loopbrug
FB_Z1, FB_Z2 = 8.0, -4.65
SPAN = (FB_Z1 - 1.5) - (FB_Z2 + 1.5)
STAIR_RUN = 26 * 0.30


# ------------------------------------------------------------------ opstelling
def layout():
    """Modules, hergebruikte stationsprops en gameplayankers in stationsruimte (spoor 1 op z = 0, plein +z).
    yaw in graden zoals Unity (lokale +z wijst dan naar (sin yaw, 0, cos yaw))."""
    M, P, A = [], [], []

    def mod(name, x, z, yaw=0, y=0.0, **kw):
        e = {"module": name, "position": [x, y, z], "yaw": yaw}
        e.update(kw)
        M.append(e)

    def prop(name, x, z, yaw=0, y=1.05, path="Props"):
        P.append({"prop": "%s/%s" % (path, name), "position": [x, y, z], "yaw": yaw})

    def anchor(name, x, z, yaw=0, y=1.05):
        A.append({"anchor": name, "position": [x, y, z], "yaw": yaw})

    # gebouw
    mod("GS_Hall_Shell", 0, 20)
    mod("GS_Hall_Facade", 0, bld.Z_SQ)
    mod("GS_Hall_Facade", 0, bld.Z_RAIL, 180, sign="WHISTLETON")
    mod("GS_Wing_Link", 18, 20)
    mod("GS_Wing_Link", -18, 20, 180)
    mod("GS_Wing_Pavilion", 28.5, 20)
    mod("GS_Wing_Pavilion", -28.5, 20, 180)
    mod("GS_ClockTower", 15.0, 29.2)
    # pleinkant: terras, luifels, plein, lantaarns
    mod("GS_Forecourt", 0, 0)
    mod("GS_Square", 0, 0)
    for x in (-9, -3, 3, 9):
        mod("GS_EntranceCanopy_6m", x, bld.Z_SQ)
    mod("GS_EntranceCanopy_Column", -12, bld.Z_SQ, 0, 0.0, offset_x=-3.0)
    for x in (-21, -15):
        mod("GS_EntranceCanopy_6m", x, 29.0)
    mod("GS_EntranceCanopy_6m", 21, 29.0)
    for x in (-31.0, -27.0, 27.0, 31.0, -15.0, 15.0):                  # tussen de luifelkolommen
        mod("GS_StreetLamp", x, 34.9, 0, 1.05)                         # op het terras
    for x, z in ((-20, 40.5), (20, 40.5), (-38, 46), (38, 46), (-38, 62), (38, 62), (-14, 64), (14, 64)):
        mod("GS_StreetLamp", x, z, 0, 0.06)                            # op de pleintegels
    # perrons, overkappingen, loopbrug (perron 1 zelf = bestaande perronkit in de scène)
    for x in range(-36, 46, 6):
        mod("GS_Apron_Hall_6m" if abs(x + 3) < 12 else "GS_Apron_6m", x + 3, 0)
    for x in range(-36, 48, 6):
        mod("GS_Platform_Island_6m", x + 3, 0)
    mod("GS_Platform_Island_End", 48, 0, 0)
    mod("GS_Platform_Island_End", -36, 0, 180)
    for x in range(-30, 30, 6):
        mod("GS_PlatformCanopy_Side_6m", x + 3, 0)
        mod("GS_PlatformCanopy_Island_6m", x + 3, 0)
    mod("GS_Footbridge_Landing", FB_X, FB_Z1)
    mod("GS_Footbridge_Landing", FB_X, FB_Z2)
    mod("GS_Footbridge_Span", FB_X, FB_Z2 + 1.5)
    mod("GS_Footbridge_Stair", FB_X + 1.5 + STAIR_RUN, FB_Z1, -90)
    mod("GS_Footbridge_Stair", FB_X + 1.5 + STAIR_RUN, FB_Z2, -90)
    for x, txt in ((-15, "1"), (15, "1")):
        mod("GS_PlatformSign", x, 3.4, 0, 1.05 + 4.15, sign=txt)
    for x in (-15, 15):
        mod("GS_PlatformSign", x, -2.6, 180, 1.05 + 4.15, sign="2", sign_back="3")
    mod("GS_DirectionSign", 0, 7.4, 180, 1.05 + 4.3, sign="UITGANG")
    # hal
    mod("GS_PedestalClock", 0, 20)
    mod("GS_DepartureBoard", 0, 12.6, 0, 1.05 + 5.6)
    mod("GS_TicketCounter", 11.4, 22.0, -90)
    mod("GS_InfoDesk", -6.6, 24.6)
    mod("GS_Kiosk", -9.2, 13.4, 90)
    for z in (11.6, 15.4):
        mod("GS_CafeTable", -5.4, z)
    for x in (-5.6, 5.6):
        mod("GS_DirectionSign", x, 11.0, 0, 1.05 + 5.0, sign="PERRONS")
    mod("GS_WallSign", -11.35, 18.0, 90, 1.05 + 7.4, sign="BRIEFING")
    for z in (25.6, 28.4):                                             # tussen de pilasters
        mod("GS_Poster", -11.38, z, 90, 1.05 + 0.9)
    mod("GS_Poster", 11.38, 17.2, -90, 1.05 + 0.9)
    for (x, z) in ((-2.8, 15.6), (-2.8, 23.6), (2.8, 15.6), (2.8, 23.6)):
        prop("Prop_Bench", x - 0.45, z, -90)
        prop("Prop_Bench", x + 0.45, z, 90)
    for (x, z) in ((-1.4, 13.4), (1.4, 26.6), (6.8, 27.8)):
        prop("Prop_TrashBin", x, z)
    for (x, z) in ((-10.6, 29.6), (10.6, 29.6), (-10.6, 10.4), (10.6, 10.4)):
        prop("Prop_Planter", x, z, 90 if x < 0 else -90)
    prop("Prop_LuggageCart", 8.0, 12.2, 20)
    prop("Prop_LuggageCart", -9.6, 28.2, 160)
    prop("Prop_Timetable", -11.0, 27.0, 90)
    # rechtervleugel: kantoor van de stationschef + personeelsruimte/opslag
    mod("GS_Partition_Office", 17.75, 18.6)
    mod("GS_OfficeFurniture", 19.5, 14.8, 90)
    prop("Prop_CoatStand", 13.0, 17.6)
    for x in (15.5, 18.5, 21.5):
        mod("GS_StorageShelf", x, 28.0, 180)
    prop("Prop_CrateStack", 22.6, 22.0, 30)
    prop("Prop_Barrel", 13.2, 27.6)
    prop("Prop_Lockers", 23.0, 22.5, -90)
    # linkervleugel: crew-briefing (+ opslag aan de spoorkant)
    mod("GS_Partition_Storage", -17.75, 14.4)
    for x in (-21.5, -18.0):
        mod("GS_StorageShelf", x, 11.9)
    prop("Prop_Barrel", -14.0, 12.4)
    prop("Prop_CrateStack", -13.2, 13.4, -20)
    mod("GS_Podium", -21.6, 21.5, 90)
    prop("Prop_RouteBoard", -23.1, 21.5, 90)
    mod("GS_WallSign", -23.42, 21.5, 90, 1.05 + 3.05, sign="BRIEFING")
    prop("Prop_NoticeBoard", -23.0, 25.8, 90)
    for x in (-19.5, -18.2):
        for z in (19.6, 23.4):
            prop("Prop_Bench", x, z, -90)
    mod("GS_MissionTable", -14.3, 25.0)
    for x in (-22.2, -20.3):
        prop("Prop_Lockers", x, 28.2, 180)
    prop("Prop_CrewWardrobe", -17.4, 28.05, 180)
    prop("Prop_AccessoryTable", -14.5, 27.95, 180)
    prop("Prop_CoffeeCounter", -22.6, 16.3, 90)
    prop("Prop_CoatStand", -12.7, 27.6)
    mod("GS_Rug", -17.0, 18.6)
    for x in (-20.5, -16.0):
        for z in (16.8, 21.5, 26.2):
            prop("Prop_PendantLamp", x, z, 0, 1.05 + 3.62)
    # perronzijde
    for x in (-21, -9, 9, 21):
        prop("Prop_Bench", x, 7.9, 180)
        prop("Prop_TrashBin", x + 1.6, 8.4)
    for x in (-15, 15):
        prop("Prop_Bench", x, -4.2, 0)
        prop("Prop_Bench", x, -5.1, 180)
    prop("Prop_LuggageCart", 13.5, 8.3, 80)
    prop("Prop_LuggageCart", -26.0, 8.6, -70)
    prop("Prop_TicketMachine", -3.0, 8.6, 180)
    prop("Prop_TicketMachine", 3.0, 8.6, 180)
    # plein
    for x in (-8, 8):
        prop("Prop_Bench", x, 44.0, 180, 0.06)
    for x in (-30, 30):
        prop("Prop_Planter", x, 39.0, 0, 0.06)
    # gameplay
    anchor("Spawn_Crew", -16.2, 18.0, -90)
    anchor("Spot_CrewGather", -17.0, 21.5, -90)
    anchor("Spot_PassengerSpawn_Square", 0.0, 42.0, 180, 0.06)
    anchor("Spot_PassengerSpawn_Hall", 0.0, 27.5, 180)
    for k, (x, z) in enumerate(((-2.8, 15.6), (2.8, 23.6), (-9, 7.9), (9, 7.9), (-15, -4.6), (15, -4.6))):
        anchor("Spot_PassengerWait_%d" % (k + 1), x, z)
    anchor("Spot_TicketInspection", 0.0, 10.6, 180)
    anchor("Spot_LuggageLoading", 8.0, 6.2, 180)
    anchor("Spot_TrainPrep", 26.0, 3.0, 180)
    anchor("Spot_TrainStop_P1", 0.0, 0.0, 90, 0.43)
    anchor("Spot_TrainStop_P2", 0.0, -9.3, 90, 0.43)
    anchor("Spot_Announcer", 3.0, 5.6, 180, 1.05 + 3.6)
    anchor("Spot_DepotKiosk", -6.6, 22.8, 180)
    for k, (x, z, y) in enumerate(((0, 48, 0.06), (0, 18, 1.05), (-20, 4, 1.05), (20, -4.6, 1.05))):
        anchor("Spot_Event_%d" % (k + 1), x, z, 0, y)
    return {"modules": M, "props": P, "anchors": A}


def main(which=None):
    kit.write_palette_group()
    for name, (fn, density, radius) in MODULES.items():
        if which and not any(w.lower() in name.lower() for w in which):
            continue
        nodes, boxes = fn()
        kit.export(name, nodes, boxes, density=density, radius=radius)
    kit.write_manifest()
    import build_station_kit
    build_station_kit.write_sign_data()
    path = os.path.join(kit.ROOT, "Assets", "Art", "Layouts", "GrandStation.json")
    json.dump(layout(), open(path, "w", encoding="utf-8"), indent=1)
    json.dump(layout(), open(os.path.join(kit.EXP_DIR, "GrandStation.json"), "w", encoding="utf-8"), indent=1)
    print("layout:", path)


if __name__ == "__main__":
    main(sys.argv[1:] or None)
