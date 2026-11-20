"""Bouwset 'Grand Whistleton' — groot station als losse, herbruikbare modules.

Conventies (zoals de stationskit):
  * 1 unit = 1 m, Y omhoog. Vloer van station en perrons op PLATFORM_TOP (1,05 m).
  * Stationsruimte: X langs het spoor, spoor 1 op z = 0, de stad (stationsplein) aan de +Z-kant.
  * Elke module heeft een eigen pivot; build_grand_station.py schrijft de opstelling naar
    Assets/Art/Layouts/GrandStation.json en Unity zet daar per module een prefab neer.
  * Botsvormen zijn eenvoudige boxen in het manifest ('boxes': [cx, cy, cz, sx, sy, sz, rx]) -> BoxColliders in
    Unity, geen MeshColliders. Lichtankers Light_*, gameplayankers Spot_*/Spawn_*, aanpasbare borden Sign_*.
  * Materialen M_GS_* uit het palet van het ontwerpbriefje (Docs/References/palette.json, groep 'Groot station').
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Station"))
import station_common as sc  # noqa: E402
from station_common import (DEG, PLATFORM_TOP, PLATFORM_EDGE, arch_band, arch_loop, ball, capsule, circle_loop,  # noqa
                            clock, cyl, disc, mk, rbox, rounded_rect_loop, slab, text_mesh, train_icon, tube)

FL = PLATFORM_TOP
ROOT = sc.ROOT
MODEL_DIR = "Assets/Art/Models/Environment/GrandStation"
EXP_DIR = os.path.join(ROOT, "ArtSource", "Exports", "Environment", "GrandStation")


# ------------------------------------------------------------------ palet (ontwerpbriefje Grand Station)
def _hex(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def _to_hex(c):
    c = np.clip(c, 0, 1)
    return "#%02X%02X%02X" % tuple(int(round(v * 255)) for v in c)


def ramp(base):
    """Vijf tinten zoals de andere ramps: koele blauw-violette schaduw -> basis -> warm licht."""
    b = _hex(base)
    shade = b * 0.58 * 0.65 + np.array([0.20, 0.20, 0.32]) * 0.35
    warm = np.array([1.0, 0.95, 0.86])
    return [_to_hex(shade), _to_hex(0.5 * shade + 0.5 * b), _to_hex(b), _to_hex(b * 0.78 + warm * 0.22),
            _to_hex(b * 0.62 + warm * 0.38)]


PALETTE = {                      # naam: (basis, omschrijving)
    "gs_brick": ("#AA5948", "Baksteen"),
    "gs_sandstone": ("#D6C2A0", "Zandsteen en pleister"),
    "gs_roof": ("#536C92", "Metalen dak (leisteenblauw)"),
    "gs_steel": ("#34465C", "Constructie (marine staal)"),
    "gs_brass": ("#C1A15B", "Messingaccenten"),
    "gs_tiles": ("#A9A49A", "Perrontegels"),
    "gs_wood": ("#956C48", "Hout"),
    "gs_light": ("#F2DAA2", "Verlichting"),
}
R = {k: ramp(v[0]) for k, v in PALETTE.items()}
R.update({k: v for k, v in sc.R.items() if k in ("roof_terracotta", "parchment", "sky", "grass", "ui_bg", "train_red",
                                                  "chalk_green", "locker_blue", "interior_wood", "pine", "cloud_snow",
                                                  "plinth_stone", "accent_yellow")})

MATTE = {"cells": 0.5, "cellSize": 0.8, "grime": 0.30, "rim": 0.12}
GS_MATERIALS = {
    "M_GS_Brick": (R["gs_brick"], 0.05, 0.00, {"cells": 0.8, "cellSize": 0.35, "grime": 0.40, "paint": 1.1}),
    "M_GS_BrickDark": (R["gs_brick"], 0.05, -0.25, {"cells": 0.7, "cellSize": 0.35, "grime": 0.40}),
    "M_GS_Sandstone": (R["gs_sandstone"], 0.05, 0.00, {"cells": 0.6, "cellSize": 0.9, "grime": 0.35}),
    "M_GS_SandstoneDark": (R["gs_sandstone"], 0.05, -0.22, {"cells": 0.6, "cellSize": 0.9, "grime": 0.40}),
    "M_GS_Plaster": (R["gs_sandstone"], 0.05, 0.18, {"cells": 0.4, "cellSize": 1.2, "grime": 0.20}),
    "M_GS_Roof": (R["gs_roof"], 0.10, 0.00, {"cells": 0.6, "cellSize": 1.0, "grime": 0.25, "rim": 0.20}),
    "M_GS_RoofDark": (R["gs_roof"], 0.10, -0.25, {"cells": 0.5, "cellSize": 1.0}),
    "M_GS_Steel": (R["gs_steel"], 0.15, 0.00, {"cells": 0.3, "grime": 0.15}),
    "M_GS_SteelLight": (R["gs_steel"], 0.15, 0.25, {"cells": 0.3, "grime": 0.15}),
    "M_GS_Brass": (R["gs_brass"], 0.25, 0.00, {"cells": 0.3}),
    "M_GS_Tiles": (R["gs_tiles"], 0.05, 0.00, {"cells": 0.9, "cellSize": 0.8, "grime": 0.40}),
    "M_GS_TilesDark": (R["gs_tiles"], 0.05, -0.25, {"cells": 0.8, "cellSize": 0.8, "grime": 0.50}),
    "M_GS_TilesLight": (R["gs_tiles"], 0.05, 0.22, {"cells": 0.8, "cellSize": 0.8, "grime": 0.30}),
    "M_GS_Wood": (R["gs_wood"], 0.10, 0.00, {"cells": 0.6, "cellSize": 0.5, "grime": 0.30}),
    "M_GS_WoodDark": (R["gs_wood"], 0.10, -0.30, {"cells": 0.6, "cellSize": 0.5, "grime": 0.30}),
    "M_GS_Terracotta": (R["roof_terracotta"], 0.10, 0.00, {"cells": 0.7, "cellSize": 0.9, "rim": 0.22}),
    "M_GS_TerracottaDark": (R["roof_terracotta"], 0.10, -0.30, {"cells": 0.6, "cellSize": 0.9}),
    # glas: getint, half doorzichtig; warm licht komt van de kamers erachter, niet van het glas zelf
    "M_GS_Glass": (R["sky"], 0.55, -0.35, {"opacity": 0.38, "flat": 0.6, "cells": 0.0, "grime": 0.0, "rim": 0.25}),
    "M_GS_RoomGlow": (R["gs_light"], 0.05, 0.00, {"emissive": 0.55, "cells": 0.2, "rim": 0.0, "grime": 0.15}),
    "M_GS_Lamp": (R["gs_light"], 0.30, 0.25, {"emissive": 0.95, "cells": 0.0, "rim": 0.0, "paint": 0.0}),
    "M_GS_Interior": (R["parchment"], 0.05, 0.05, {"cells": 0.4, "cellSize": 1.2, "grime": 0.15}),
    "M_GS_Dark": (R["ui_bg"], 0.20, 0.10, {"cells": 0.0}),
    "M_GS_Flap": (R["gs_light"], 0.40, 0.10, {"emissive": 0.6, "cells": 0.0, "rim": 0.0, "paint": 0.0}),
    "M_GS_Leaf": (R["grass"], 0.05, -0.05, {"cells": 0.6, "cellSize": 0.4, "rim": 0.25}),
    "M_GS_LeafDark": (R["pine"], 0.05, 0.05, {"cells": 0.6, "cellSize": 0.4, "rim": 0.25}),
    "M_GS_Soil": (R["interior_wood"], 0.05, -0.30, {"cells": 0.5}),
    "M_GS_Asphalt": (R["gs_tiles"], 0.02, -0.55, {"cells": 0.9, "cellSize": 1.4, "grime": 0.45}),
    "M_GS_Yellow": (R["accent_yellow"], 0.20, -0.05, {"cells": 0.3}),
    "M_GS_Chalk": (R["chalk_green"], 0.05, 0.00, {"cells": 0.5, "cellSize": 0.8, "grime": 0.2}),
    "M_GS_Red": (R["train_red"], 0.20, 0.00, {"cells": 0.4}),
    "M_GS_Cream": (R["parchment"], 0.20, 0.35, {"cells": 0.2}),
    "M_GS_Flower": (R["cloud_snow"], 0.10, 0.10, {"cells": 0.2}),
}
sc.MATERIALS.update(GS_MATERIALS)


def write_palette_group():
    """Leg de briefjeskleuren vast in palette.json (groep 'Groot station'), zodat ze bij de andere ramps staan."""
    path = os.path.join(ROOT, "Docs", "References", "palette.json")
    data = json.load(open(path, encoding="utf-8"))
    data["groups"]["Groot station"] = {k: {"name": v[1], "ramp": R[k]} for k, v in PALETTE.items()}
    json.dump(data, open(path, "w", encoding="utf-8"), indent=2, ensure_ascii=False)


# ------------------------------------------------------------------ botsboxen
class Boxes(list):
    def add(self, x0, x1, y0, y1, z0, z1, rx=0.0):
        x0, x1 = sorted((x0, x1))
        y0, y1 = sorted((y0, y1))
        z0, z1 = sorted((z0, z1))
        self.append([round((x0 + x1) / 2, 4), round((y0 + y1) / 2, 4), round((z0 + z1) / 2, 4),
                     round(x1 - x0, 4), round(y1 - y0, 4), round(z1 - z0, 4), round(rx, 3)])
        return self

    def placed(self, yaw_quarter=0, t=(0, 0, 0)):
        """Kopie, gedraaid in kwartslagen om Y en verschoven (voor herbruikte onderdelen binnen een module)."""
        out = Boxes()
        for cx, cy, cz, sx, sy, sz, rx in self:
            for _ in range(yaw_quarter % 4):
                cx, cz, sx, sz = cz, -cx, sz, sx
            out.append([round(cx + t[0], 4), round(cy + t[1], 4), round(cz + t[2], 4), sx, sy, sz, rx])
        return out


def yaw(q):
    """Rotatie in kwartslagen om Y (q = 1: +X wordt -Z), passend bij Boxes.placed."""
    return mk.rot_y(q * math.pi / 2)


# ------------------------------------------------------------------ wanden met echte diepte
def opening_loop(o):
    kind = o.get("kind", "arch")
    x0, x1, y0, ys = o["x0"], o["x1"], o["y0"], o["ys"]
    if kind == "arch":
        return arch_loop(x0, x1, y0, ys, 16)
    if kind == "seg":                                       # segmentboog: lage boog boven een rechte opening
        rise = o.get("rise", (x1 - x0) * 0.18)
        w = (x1 - x0) / 2
        r = (w * w + rise * rise) / (2 * rise)
        cx, cy = (x0 + x1) / 2, ys + rise - r
        a = math.asin(w / r)
        return [(x0, y0), (x1, y0)] + [(cx + r * math.sin(t), cy + r * math.cos(t)) for t in np.linspace(a, -a, 11)]
    if kind == "round":
        return circle_loop((x0 + x1) / 2, (y0 + ys) / 2, (x1 - x0) / 2, 22)
    return [(x0, y0), (x1, y0), (x1, ys), (x0, ys)]


def wall(L, H, openings=(), t=0.5, mat="M_GS_Brick", y0=FL - 0.3, outline=None):
    """Muurplaat in lokaal XY (x 0..L), buitenkant op z = 0, binnenkant z = -t. Openingen worden echte gaten,
    de dagkanten zijn dus zo diep als de muur."""
    pts = outline or [(0.0, y0), (L, y0), (L, H), (0.0, H)]
    holes = [opening_loop(o) for o in openings if not o.get("door")]
    doors = [o for o in openings if o.get("door")]
    if doors:                                               # deuren: uitsparing in de onderrand
        pts = sc.wall_outline(0.0, L, y0, H, [(o["x0"], o["x1"], o["ys"]) for o in doors], None, 14)
        if outline:
            raise ValueError("deuren en een eigen omtrek tegelijk worden niet ondersteund")
    return slab(pts, holes, -t, 0.0, 0.03, mat)


def window(o, t=0.5, glass_depth=0.28, surround="M_GS_Sandstone", frame="M_GS_Steel", bars=True, sill=True,
           keystone=True, glow=None):
    """Raam in een muur (zelfde lokaal stelsel als wall()): zandstenen omlijsting die naar voren steekt, kozijn
    en glas teruggezet in de dagkant, marine roedes, lekdorpel. glow = diepte van een verlichte kamerwand erachter."""
    x0, x1, y0, ys = o["x0"], o["x1"], o["y0"], o["ys"]
    kind = o.get("kind", "arch")
    w = x1 - x0
    parts = []
    zg = -glass_depth
    loop = opening_loop(o)
    if kind == "arch":                                     # 2 cm de opening in: geen dubbel vlak met de dagkant
        parts.append(slab(arch_band(x0 + 0.02, x1 - 0.02, y0, ys, 0.18, 16), [], -0.02, 0.10, 0.03, surround))
    elif kind == "round":
        r = w / 2
        cx, cy = (x0 + x1) / 2, (y0 + ys) / 2
        parts.append(slab(circle_loop(cx, cy, r + 0.18, 24), [circle_loop(cx, cy, r - 0.02, 24)], -0.02, 0.10, 0.03, surround))
    else:
        top = max(p[1] for p in loop)
        parts.append(slab([(x0 - 0.16, y0), (x1 + 0.16, y0), (x1 + 0.16, top + 0.18), (x0 - 0.16, top + 0.18)],
                          [sc._offset_loop(sc._ccw(sc.clean_loop(loop)), 0.02)], -0.02, 0.10, 0.03, surround))
    # kozijn in de dagkant + glas
    inset = sc._offset_loop(sc._ccw(sc.clean_loop(loop)), 0.06)
    parts.append(slab(loop, [inset], zg - 0.05, zg + 0.05, 0.012, frame))
    parts.append(slab(inset, [], zg - 0.012, zg + 0.012, 0.004, "M_GS_Glass"))
    if bars and kind != "round":
        xm = (x0 + x1) / 2
        top = max(p[1] for p in loop)
        parts.append(rbox(xm - 0.035, xm + 0.035, y0 + 0.05, top - 0.05, zg - 0.035, zg + 0.035, 0.012, frame))
        yb = ys if kind == "arch" else (y0 + ys) / 2
        parts.append(rbox(x0 + 0.05, x1 - 0.05, yb - 0.03, yb + 0.03, zg - 0.022, zg + 0.022, 0.01, frame))
        if w > 1.3:
            for xq in (x0 + w * 0.25, x1 - w * 0.25):
                parts.append(rbox(xq - 0.025, xq + 0.025, y0 + 0.05, yb - 0.035, zg - 0.025, zg + 0.025, 0.008, frame))
    elif kind == "round" and bars:
        cx, cy, r = (x0 + x1) / 2, (y0 + ys) / 2, w / 2 - 0.06
        parts.append(rbox(cx - r, cx + r, cy - 0.03, cy + 0.03, zg - 0.03, zg + 0.03, 0.01, frame))
        parts.append(rbox(cx - 0.03, cx + 0.03, cy - r, cy + r, zg - 0.018, zg + 0.018, 0.01, frame))
    if sill:
        parts.append(rbox(x0 - 0.22, x1 + 0.22, y0 - 0.16, y0 - 0.02, -0.06, 0.18, 0.03, surround))
    if keystone and kind == "arch":
        r = w / 2
        xm, top = (x0 + x1) / 2, ys + r
        parts.append(slab([(xm - 0.16, top + 0.30), (xm + 0.16, top + 0.30), (xm + 0.11, top - 0.05), (xm - 0.11, top - 0.05)],
                          [], -0.02, 0.16, 0.025, surround))
    if glow:
        top = max(p[1] for p in loop)
        j = 0.006 * ((int(round(abs(x0) * 10)) * 3 + int(round(abs(y0) * 10))) % 7)   # buren niet in hetzelfde vlak
        parts.append(rbox(x0 - 0.1, x1 + 0.1, y0 - 0.1 - j, top + 0.15 + j, -t - glow - 0.02 - j, -t - glow - j, 0.0, "M_GS_RoomGlow"))
    return mk.merge(*parts)


def place(mesh, R=None, t=(0, 0, 0)):
    return mesh.transform(R=R if R is not None else np.eye(3), t=t)


def face_frame(axis):
    """Rotatie die een in lokaal XY gebouwde muur (buiten +Z) naar een buitenrichting draait:
    '+z' (geen draaiing), '-z', '+x', '-x'. Lokale x loopt dan langs de gevel van links naar rechts gezien van buiten."""
    return {"+z": np.eye(3), "-z": mk.rot_y(math.pi), "+x": mk.rot_y(math.pi / 2), "-x": mk.rot_y(-math.pi / 2)}[axis]


def frustum(x0, x1, z0, z1, y0, ins, y1, mat, cap=True):
    """Afgeknotte piramide (mansarde/schilddak-vlak) van rechthoek op y0 naar een met `ins` ingesprongen rechthoek
    op y1; cap = bovenvlak dicht."""
    a = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    b = [(x0 + ins, z0 + ins), (x1 - ins, z0 + ins), (x1 - ins, z1 - ins), (x0 + ins, z1 - ins)]
    V = [(x, y0, z) for x, z in a] + [(x, y1, z) for x, z in b]
    F = [(0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0), (0, 1, 2, 3)]
    if cap:
        F.append((4, 7, 6, 5))
    m = mk.Mesh(np.array(V, float), F, [mat] * len(F))
    m.sharp = True
    return mk._orient_closed(m)


def seam_lines(x0, x1, z0, z1, y0, ins, y1, step, mat, w=0.035, d=0.03):
    """Felsnaden (staande naden) op de schuine vlakken van een frustum, om het metalen dak te lezen."""
    parts = []
    for (px0, pz0, px1, pz1, nx, nz) in ((x0, z0, x1, z0, 0, -1), (x0, z1, x1, z1, 0, 1)):
        L = abs(px1 - px0)
        for k in np.arange(step / 2, L - 0.1, step):
            x = px0 + k
            xi = min(max(x, x0 + ins), x1 - ins)
            p0 = (x, y0 + 0.02, pz0 + nz * 0.0)
            p1 = (xi, y1, pz0 - nz * ins)
            parts.append(capsule(p0, p1, w, mat=mat, seg=4, rings=1))
    for (pz0, pz1, px, nx) in ((z0, z1, x0, -1), (z0, z1, x1, 1)):
        L = abs(pz1 - pz0)
        for k in np.arange(step / 2, L - 0.1, step):
            z = pz0 + k
            zi = min(max(z, z0 + ins), z1 - ins)
            parts.append(capsule((px, y0 + 0.02, z), (px - nx * ins, y1, zi), w, mat=mat, seg=4, rings=1))
    del d
    return mk.merge(*parts)


def quoins(x, z_face, y0, y1, out_x, out_z, mat="M_GS_Sandstone", course=0.5):
    """Hoekblokken op een buitenhoek (x, z_face): om en om lang/kort, 4 cm uit beide gevels.
    out_x/out_z = +-1: richting naar buiten langs x en z."""
    parts = []
    for k, y in enumerate(np.arange(y0, y1 - course * 0.6, course)):
        Lx, Lz = (0.62, 0.34) if k % 2 else (0.34, 0.62)
        xa, xb = sorted((x + out_x * 0.05, x - out_x * Lx))
        za, zb = sorted((z_face + out_z * 0.05, z_face - out_z * Lz))
        parts.append(rbox(xa, xb, y, y + course - 0.07, za, zb, 0.035, mat))
    return mk.merge(*parts)


# ------------------------------------------------------------------ export
MANIFEST = {}


def export(name, nodes, boxes=(), density=160, radius=0.30, extra=None):
    """nodes: [(knoop, mesh, ouder, pivot, rotatie, zichtbaar)]; boxes in modulecoördinaten."""
    os.makedirs(EXP_DIR, exist_ok=True)
    old_exp = sc.EXP_DIR
    sc.EXP_DIR = EXP_DIR
    try:
        import station_props as props
        info = {"boxes": [list(b) for b in boxes]}
        signs = [props.SIGN_INFO[id(n)][1] for n in nodes if props.SIGN_INFO.get(id(n), (None,))[0] is n]
        if signs:
            info["signs"] = signs
        if extra:
            info.update(extra)
        sc.export(name, None, nodes, density=density, radius=radius, manifest=MANIFEST, extra=info, model_dir=MODEL_DIR)
    finally:
        sc.EXP_DIR = old_exp


def write_manifest():
    path = os.path.join(EXP_DIR, "GrandStationKit.json")
    old = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {"assets": {}}
    old["assets"].update(MANIFEST)
    json.dump(old, open(path, "w", encoding="utf-8"), indent=1)
    # Unity (JsonUtility): lijst van modules met platte boxlijst [cx, cy, cz, sx, sy, sz, rx, rz] per box
    mods = [{"name": n, "boxes": [float(v) for b in a.get("boxes", []) for v in (list(b) + [0.0, 0.0])[:8]]}
            for n, a in sorted(old["assets"].items())]
    unity = os.path.join(ROOT, "Assets", "Art", "Layouts", "GrandStationKit.json")
    json.dump({"modules": mods}, open(unity, "w", encoding="utf-8"), indent=0)
