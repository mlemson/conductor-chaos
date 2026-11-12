"""Bouwt alle art-proof bronnen opnieuw: modellen -> renders -> ontwerpbladen.

    python ArtSource/build_all.py            # modellen + renders + bladen
    python ArtSource/build_all.py --models   # alleen modellen (FBX/GLB), geen renders

Renders vereisen Node.js + Playwright (Chromium) en three.js r187 in
ArtSource/Render/three (zie ArtSource/README.md). Modellen vereisen alleen
Python 3 + numpy; de bladen Pillow.
"""
import http.server
import os
import subprocess
import sys
import threading
import functools

HERE = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable


def run(*args, **kw):
    print(">", " ".join(args))
    subprocess.run(args, check=True, **kw)


def main():
    run(PY, os.path.join(HERE, "Characters", "Conductor", "build_conductor.py"))
    for script in ("ElderlyPassenger/build_elderly_passenger.py", "Tourist/build_tourist.py",
                   "RailMechanic/build_rail_mechanic.py", "StationMaster/build_station_master.py",
                   "BusinessTraveler/build_business_traveler.py", "ChildPassenger/build_child_passenger.py",
                   "BaggageHelper/build_baggage_helper.py"):
        run(PY, os.path.join(HERE, "Characters", *script.split("/")))
    for script in ("Sheep/build_sheep.py", "Cow/build_cow.py", "build_animal_props.py"):
        run(PY, os.path.join(HERE, "Animals", *script.split("/")))
    run(PY, os.path.join(HERE, "Train", "RegionalWagon", "build_regional_wagon.py"))
    run(PY, os.path.join(HERE, "Train", "Locomotive", "build_locomotive.py"))
    run(PY, os.path.join(HERE, "Train", "Tender", "build_tender.py"))
    run(PY, os.path.join(HERE, "Train", "CattleWagon", "build_cattle_wagon.py"))
    run(PY, os.path.join(HERE, "Train", "DieselSet", "build_diesel_set.py"))
    run(PY, os.path.join(HERE, "Train", "ExpressSets", "build_express_sets.py"))
    run(PY, os.path.join(HERE, "Train", "FreightSet", "build_freight_set.py"))
    run(PY, os.path.join(HERE, "Environment", "build_environment_kit.py"))
    run(PY, os.path.join(HERE, "Environment", "Station", "build_station_kit.py"))
    run(PY, os.path.join(HERE, "Textures", "build_ground_textures.py"))
    if "--models" in sys.argv:
        return
    if not os.path.exists(os.path.join(HERE, "Render", "three", "build", "three.module.js")):
        sys.exit("three.js ontbreekt: zie ArtSource/README.md (Render/three)")
    class Quiet(http.server.SimpleHTTPRequestHandler):
        def log_message(self, *a):
            pass
    handler = functools.partial(Quiet, directory=HERE)
    srv = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{srv.server_address[1]}/Render/render.html"
    out = os.path.join(HERE, "Render", "out")
    run(PY, os.path.join(HERE, "Render", "env_scenes.py"))
    run(PY, os.path.join(HERE, "Render", "character_scenes.py"))
    run(PY, os.path.join(HERE, "Render", "station_scenes.py"))
    run(PY, os.path.join(HERE, "Render", "animal_scenes.py"))
    for scenes in ("conductor_sheet.json", "art_check.json", "loco_sheet.json", "cattle_sheet.json", "env_sheet.json",
                   "ElderlyPassenger_sheet.json", "Tourist_sheet.json", "RailMechanic_sheet.json",
                   "StationMaster_sheet.json", "BusinessTraveler_sheet.json", "ChildPassenger_sheet.json",
                   "BaggageHelper_sheet.json",
                   "Sheep_sheet.json", "Cow_sheet.json", "animal_props_sheet.json",
                   "characters_in_scene.json", "station_sheet.json"):
        run("node", os.path.join(HERE, "Render", "render.cjs"), os.path.join(HERE, "Render", "scenes", scenes), out, base)
    srv.shutdown()
    run(PY, os.path.join(HERE, "Render", "export_unity_layout.py"))
    run(PY, os.path.join(HERE, "Render", "make_sheets.py"))
    run(PY, os.path.join(HERE, "Render", "character_sheets.py"), cwd=os.path.join(HERE, "Render"))
    run(PY, os.path.join(HERE, "Render", "station_sheets.py"), cwd=os.path.join(HERE, "Render"))
    run(PY, os.path.join(HERE, "Render", "animal_sheets.py"), cwd=os.path.join(HERE, "Render"))


if __name__ == "__main__":
    main()
