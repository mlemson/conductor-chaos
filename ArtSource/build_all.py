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
    run(PY, os.path.join(HERE, "Train", "WagonCorner", "build_wagon_corner.py"))
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
    for scenes in ("conductor_sheet.json", "art_check.json"):
        run("node", os.path.join(HERE, "Render", "render.cjs"), os.path.join(HERE, "Render", "scenes", scenes), out, base)
    srv.shutdown()
    run(PY, os.path.join(HERE, "Render", "make_sheets.py"))


if __name__ == "__main__":
    main()
