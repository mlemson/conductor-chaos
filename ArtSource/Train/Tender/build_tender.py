"""Kolenwagen (tender) direct achter de locomotief — P0 art proof (issue #22).

Naar het voorbeeld van @mlemson en in de stijl van de locomotief v2: rode bak met gouden rand en
gouden hoekstrips, geschilderde panelen ('vierkantjes') met gouden klinknagels, een berg gefacetteerde
kolen, donker onderstel met twee assen (rode naven), gouden ladder voorin, twee gloeiende
sluitlampen achterop en bufferbalk/koppeling. Zachte afrondingen, geschilderde variatie.

Assen: rijrichting +X (voorkant = kant van de loc), perronkant op z = 0, spoorstaaf op y = -0.62.
Lokale oorsprong: midden van de wagen in X. In de scène staat de tender direct achter de loc.
Gameplay-anker: 'Coal' (kolenberg) = bron voor het stoken in de cabine (later gedeeld, host-autoritatief).

Uitvoer:
  Assets/Art/Models/Train/Tender/Tender_Blockout.fbx   Unity
  ArtSource/Exports/Tender_Blockout.glb                Blender/preview
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import meshkit as mk  # noqa: E402
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}

L = 3.40                    # lengte bak
W = 2.30                    # breedte frame (gelijk aan de loc)
ZC = -1.35                  # hartlijn (gelijk aan de loc)
RAIL_Y = -0.62
FRAME_Y = (-0.06, 0.34)
BODY_W = 2.36
BODY_TOP = 1.52
WHEEL_R = 0.36
WHEEL_W = 0.20
WHEEL_X = (-0.92, 0.92)
WHEEL_Z = W / 2 + 0.13


def rbox(x0, x1, y0, y1, z0, z1, r, mat, grid=None, div=2):
    if grid:
        m = mk.rounded_box_grid(x1 - x0, y1 - y0, z1 - z0, r, div=div, step=grid, mat=mat)
    else:
        m = mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, r, div, mat=mat)
    return m.transform(t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


def build_body():
    x0, x1 = -L / 2, L / 2
    z0, z1 = ZC - BODY_W / 2, ZC + BODY_W / 2
    parts = [rbox(x0, x1, FRAME_Y[1] - 0.02, BODY_TOP, z0, z1, 0.09, "M_Tender_Red", 0.5, div=3)]
    # gouden rand bovenop (rondom) en gouden hoekstrips
    t = 0.09
    parts += [rbox(x0 - 0.03, x1 + 0.03, BODY_TOP - 0.03, BODY_TOP + 0.08, z1 - t, z1 + 0.03, 0.035, "M_Tender_Gold", div=3),
              rbox(x0 - 0.03, x1 + 0.03, BODY_TOP - 0.03, BODY_TOP + 0.08, z0 - 0.03, z0 + t, 0.035, "M_Tender_Gold", div=3),
              rbox(x1 - t, x1 + 0.03, BODY_TOP - 0.03, BODY_TOP + 0.08, z0, z1, 0.035, "M_Tender_Gold", div=3),
              rbox(x0 - 0.03, x0 + t, BODY_TOP - 0.03, BODY_TOP + 0.08, z0, z1, 0.035, "M_Tender_Gold", div=3)]
    for xs in (x0 - 0.02, x1 - 0.10):
        for zs in (z0 - 0.02, z1 - 0.10):
            parts.append(rbox(xs, xs + 0.12, FRAME_Y[1] + 0.02, BODY_TOP - 0.02, zs, zs + 0.12, 0.04, "M_Tender_Gold"))
    # gouden onderrand
    parts.append(rbox(x0 - 0.02, x1 + 0.02, FRAME_Y[1] - 0.03, FRAME_Y[1] + 0.07, z0 - 0.02, z1 + 0.02, 0.03, "M_Tender_Gold"))
    # geschilderde panelen met klinknagels: twee per zijkant, één achter
    def panel_side(zf, sgn):
        za, zb = (zf - 0.01, zf + 0.012) if sgn > 0 else (zf - 0.012, zf + 0.01)
        out = []
        for (pa, pb) in ((x0 + 0.22, -0.06), (0.06, x1 - 0.22)):
            ya, yb = FRAME_Y[1] + 0.20, BODY_TOP - 0.18
            out.append(rbox(pa, pb, ya, yb, za, zb, 0.01, "M_Tender_Panel"))
            for (rx, ry) in ((pa + 0.08, ya + 0.08), (pb - 0.08, ya + 0.08), (pa + 0.08, yb - 0.08), (pb - 0.08, yb - 0.08)):
                out.append(mk.ellipsoid(0.028, 0.028, 0.018, 10, 5, mat="M_Tender_Gold").transform(t=(rx, ry, zf + sgn * 0.014)))
        return out
    parts += panel_side(z1, 1) + panel_side(z0, -1)
    ya, yb = FRAME_Y[1] + 0.40, BODY_TOP - 0.18
    parts.append(rbox(x0 - 0.012, x0 + 0.01, ya, yb, ZC - 0.55, ZC + 0.55, 0.01, "M_Tender_Panel"))
    for (rz, ry) in ((ZC - 0.47, ya + 0.08), (ZC + 0.47, ya + 0.08), (ZC - 0.47, yb - 0.08), (ZC + 0.47, yb - 0.08)):
        parts.append(mk.ellipsoid(0.018, 0.028, 0.028, 10, 5, mat="M_Tender_Gold").transform(t=(x0 - 0.014, ry, rz)))
    return mk.merge(*parts)


def build_coal():
    """Kolenberg: donkere koepel als vulling + onregelmatige gefacetteerde brokken erop."""
    a, b, h = L / 2 - 0.12, BODY_W / 2 - 0.10, 0.64
    base = mk.ellipsoid(a, h, b, 16, 8, mat="M_Tender_Coal").transform(t=(0, BODY_TOP - 0.05, ZC))
    base.V[:, 1] = np.maximum(base.V[:, 1], BODY_TOP - 0.06)
    base.sharp = True
    rng = np.random.default_rng(7)
    lumps = []
    for _ in range(78):
        while True:
            x, z = rng.uniform(-1, 1), rng.uniform(-1, 1)
            if x * x + z * z < 0.92:
                break
        y = BODY_TOP - 0.05 + h * math.sqrt(max(0.0, 1 - x * x - z * z)) * 0.92
        s = rng.uniform(0.13, 0.25)
        lump = mk.ellipsoid(s * rng.uniform(0.9, 1.3), s * rng.uniform(0.6, 0.9), s * rng.uniform(0.8, 1.2), 7, 4,
                            mat="M_Tender_Coal", n=2.3)
        lump.sharp = True
        lumps.append(lump.transform(R=mk.rot_y(rng.uniform(0, math.pi)) @ mk.rot_z(rng.uniform(-0.5, 0.5)),
                                    t=(x * a * 0.95, y, ZC + z * b * 0.95)))
    return mk.merge(base, *lumps)


def build_frame():
    x0, x1 = -L / 2, L / 2
    parts = [rbox(x0 - 0.10, x1 + 0.10, FRAME_Y[0], FRAME_Y[1] - 0.04, ZC - W / 2, ZC + W / 2, 0.12, "M_Tender_Frame", div=3),
             rbox(x0 + 0.35, x1 - 0.35, -0.36, FRAME_Y[0] + 0.04, ZC - W / 2 + 0.10, ZC - W / 2 + 0.28, 0.07, "M_Tender_Frame", div=3),
             rbox(x0 + 0.35, x1 - 0.35, -0.36, FRAME_Y[0] + 0.04, ZC + W / 2 - 0.28, ZC + W / 2 - 0.10, 0.07, "M_Tender_Frame", div=3)]
    # achter: bufferbalk + blokbuffers + koppeling; voor: koppeling naar de loc
    xb = x0 - 0.10
    parts.append(rbox(xb - 0.14, xb + 0.02, -0.04, 0.36, ZC - 0.95, ZC + 0.95, 0.06, "M_Tender_Frame", div=3))
    for dz in (-0.62, 0.62):
        parts.append(mk.cylinder(0.06, 0.14, 12, 0.02, mat="M_Tender_Frame").transform(t=(xb - 0.20, 0.17, ZC + dz)))
        parts.append(rbox(xb - 0.38, xb - 0.24, 0.06, 0.28, ZC + dz - 0.11, ZC + dz + 0.11, 0.06, "M_Tender_Trim", div=3))
    parts.append(rbox(xb - 0.48, xb - 0.10, 0.10, 0.24, ZC - 0.09, ZC + 0.09, 0.05, "M_Tender_Frame"))
    parts.append(rbox(x1 + 0.08, x1 + 0.34, 0.10, 0.24, ZC - 0.09, ZC + 0.09, 0.05, "M_Tender_Frame"))
    return mk.merge(*parts)


def build_wheels():
    parts = []
    yc = RAIL_Y + WHEEL_R
    for sgn in (1, -1):
        zs = ZC + sgn * WHEEL_Z
        for x in WHEEL_X:
            tire = mk.cylinder(WHEEL_R, WHEEL_W, 16, 0.05, mat="M_Tender_Wheel", axis="z")
            hub = mk.cylinder(WHEEL_R * 0.70, WHEEL_W + 0.03, 14, 0.04, mat="M_Tender_Red", axis="z")
            cap = mk.cylinder(0.10, WHEEL_W + 0.07, 12, 0.03, mat="M_Tender_RedDark", axis="z")
            parts += [m.transform(t=(x, yc, zs)) for m in (tire, hub, cap)]
        # askast tussen de wielen (donker, met gouden deksel)
        parts.append(rbox(-0.32, 0.32, yc - 0.06, yc + 0.20, zs - sgn * 0.03 - 0.07, zs - sgn * 0.03 + 0.07, 0.04, "M_Tender_Frame"))
        parts.append(rbox(-0.12, 0.12, yc - 0.02, yc + 0.14, zs + sgn * 0.05 - 0.02, zs + sgn * 0.05 + 0.02, 0.015, "M_Tender_Gold"))
    return mk.merge(*parts)


def build_ladder():
    """Gouden ladder op de voorhoek (perronkant)."""
    x = L / 2 - 0.30
    z = ZC + BODY_W / 2 + 0.07
    parts = []
    for dx in (-0.17, 0.17):
        parts.append(mk.capsule_between((x + dx, -0.24, z), (x + dx, BODY_TOP + 0.10, z), 0.03, 0.03, 10, 2, mat="M_Tender_Gold"))
    for k in range(5):
        y = -0.12 + k * (BODY_TOP - 0.05) / 4
        parts.append(mk.capsule_between((x - 0.17, y, z), (x + 0.17, y, z), 0.024, 0.024, 8, 2, mat="M_Tender_Gold"))
    for y in (0.55, BODY_TOP - 0.10):
        parts.append(mk.capsule_between((x - 0.17, y, z), (x - 0.17, y, z - 0.08), 0.024, 0.024, 8, 1, mat="M_Tender_Gold"))
        parts.append(mk.capsule_between((x + 0.17, y, z), (x + 0.17, y, z - 0.08), 0.024, 0.024, 8, 1, mat="M_Tender_Gold"))
    return mk.merge(*parts)


def build_taillights():
    x = -L / 2 - 0.02
    parts = []
    for dz in (-0.78, 0.78):
        house = mk.cylinder(0.19, 0.12, 18, 0.04, mat="M_Tender_Frame").transform(t=(x - 0.04, 0.98, ZC + dz))
        lens = mk.ellipsoid(0.08, 0.145, 0.145, 18, 9, mat="M_Tender_TailLamp").transform(t=(x - 0.10, 0.98, ZC + dz))
        parts += [house, lens]
    return mk.merge(*parts)


def build_tender():
    sc = mk.Scene("Tender_Blockout")
    R_ = RAMPS
    sc.material("M_Tender_Red", R_["train_red"][2], R_["train_red"], 0.25, opts={"paint": 1.4, "edge": 1.2})
    sc.material("M_Tender_RedDark", R_["train_red"][1], R_["train_red"], 0.25, ramp_shift=-0.26, opts={"paint": 1.3, "edge": 1.2})
    sc.material("M_Tender_Panel", R_["train_red"][1], R_["train_red"], 0.20, ramp_shift=-0.22, opts={"paint": 1.6, "edge": 0.0})
    sc.material("M_Tender_Gold", R_["gold_trim"][3], R_["gold_trim"], 0.40, ramp_shift=0.04, opts={"paint": 0.8, "edge": 1.4})
    sc.material("M_Tender_Frame", R_["ui_bg"][2], R_["ui_bg"], 0.25, ramp_shift=0.06, opts={"paint": 1.0, "edge": 1.4})
    sc.material("M_Tender_Trim", R_["train_trim"][1], R_["train_trim"], 0.20, ramp_shift=-0.05, opts={"paint": 1.2, "edge": 1.4})
    sc.material("M_Tender_Wheel", R_["ui_bg"][2], R_["ui_bg"], 0.30, opts={"paint": 0.8, "edge": 1.4})
    sc.material("M_Tender_Coal", R_["ui_bg"][3], R_["ui_bg"], 0.35, ramp_shift=0.08, opts={"paint": 0.9, "edge": 1.8})
    tail_ramp = [R_["title_red"][1], R_["title_red"][2], R_["vest"][1], R_["vest"][3], R_["helmet"][2]]
    sc.material("M_Tender_TailLamp", R_["vest"][2], tail_ramp, 0.5, emissive=R_["vest"][3],
                opts={"emissive": 1.0, "rim": 0.0, "paint": 0.2})
    sc.add(mk.Part("Tender", None))
    sc.add(mk.Part("Frame", build_frame(), (0, 0, 0), "Tender"))
    sc.add(mk.Part("Body", build_body(), (0, 0, 0), "Tender"))
    sc.add(mk.Part("Coal", build_coal(), (0, 0, 0), "Body"))
    sc.add(mk.Part("Wheels", build_wheels(), (0, 0, 0), "Tender"))
    sc.add(mk.Part("Ladder", build_ladder(), (0, 0, 0), "Body"))
    sc.add(mk.Part("TailLights", build_taillights(), (0, 0, 0), "Body"))
    for p in sc.parts:
        p.gradient = 0.30
        if p.name in ("Frame", "Body", "Coal", "Ladder", "TailLights"):
            p.ao_group = "body"
    sc.get("Wheels").gradient = 0.12
    sc.facet_all()
    sc.bake_shading(density=1100, radius=0.30, noise_freq=1.3, edge_range=(4.0, 16.0))
    return sc


def main():
    sc = build_tender()
    rel = "Assets/Art/Models/Train/Tender/Tender_Blockout.fbx"
    exp_dir = os.path.join(ROOT, "ArtSource", "Exports")
    os.makedirs(os.path.join(ROOT, os.path.dirname(rel)), exist_ok=True)
    sc.write_fbx_ascii(os.path.join(ROOT, rel))
    sc.write_glb(os.path.join(exp_dir, "Tender_Blockout.glb"))
    unity_assets.export_unity(ROOT, sc, rel, "Assets/Art/Materials/Train/Tender")
    mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"],
                "opts": v["opts"]} for k, v in sc.materials.items()}
    json.dump({"model": "Tender_Blockout", "stats": sc.stats(), "materials": mats},
              open(os.path.join(exp_dir, "Tender_Blockout.materials.json"), "w", encoding="utf-8"), indent=2)
    print("tender", sc.stats())


if __name__ == "__main__":
    main()
