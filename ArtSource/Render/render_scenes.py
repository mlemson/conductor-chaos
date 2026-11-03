"""Rendert een scènebestand met een tijdelijke lokale webserver.
    python ArtSource/Render/render_scenes.py <scenes.json> <outdir>
"""
import functools
import http.server
import os
import subprocess
import sys
import threading

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)


def render(scenes, out):
    class Quiet(http.server.SimpleHTTPRequestHandler):
        def log_message(self, *a):
            pass
    srv = http.server.ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(Quiet, directory=ART))
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    try:
        base = f"http://127.0.0.1:{srv.server_address[1]}/Render/render.html"
        subprocess.run(["node", os.path.join(HERE, "render.cjs"), scenes, out, base], check=True)
    finally:
        srv.shutdown()


if __name__ == "__main__":
    render(os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2]))
