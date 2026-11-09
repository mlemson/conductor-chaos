"""Kolenwagen (tender) direct achter de locomotief — P0 art proof (issue #22).

Naar het voorbeeld van @mlemson en in de stijl van de locomotief v2: rode bak met gouden rand en
gouden hoekstrips, geschilderde panelen ('vierkantjes') met gouden klinknagels, een berg gefacetteerde
kolen, donker onderstel met twee assen (rode naven), gouden ladder voorin, twee gloeiende
sluitlampen achterop en bufferbalk/koppeling.

Beloopbaar (issue #24): voordek op cabinevloerhoogte (0,74 m) met een loopplaat naar de cabine-achterdeur,
een opstap naar de kolenberg, en een achterdek met reling om naar de volgende wagon te springen.
De bak is één doorlopend stuk (stapvormig zijprofiel, geëxtrudeerd).

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

L = 3.80                    # lengte bak incl. dekken
W = 2.30                    # breedte frame (gelijk aan de loc)
ZC = -1.35                  # hartlijn (gelijk aan de loc)
RAIL_Y = -0.62
FRAME_Y = (-0.06, 0.34)
BODY_W = 2.36
BODY_TOP = 1.52
WHEEL_R = 0.36
WHEEL_W = 0.20
WHEEL_X = (-1.05, 1.05)
DECK = 0.74                 # dekhoogte = cabinevloer van de loc
STEP = 1.13                 # opstap naar de kolenberg
BASE = FRAME_Y[1] - 0.02
XF = L / 2 - 0.62           # voordek: XF .. L/2
XR = -L / 2 + 0.50          # achterdek: -L/2 .. XR
BUNK = (XR + 0.30, XF - 0.30)   # kolenbunker (volle hoogte)
WHEEL_Z = W / 2 + 0.13


def rbox(x0, x1, y0, y1, z0, z1, r, mat, grid=None, div=2):
    if grid:
        m = mk.rounded_box_grid(x1 - x0, y1 - y0, z1 - z0, r, div=div, step=grid, mat=mat)
    else:
        m = mk.rounded_box(x1 - x0, y1 - y0, z1 - z0, r, div, mat=mat)
    return m.transform(t=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


def build_body():
    """Bak uit één stuk: stapvormig zijprofiel (achterdek, opstap, bunker, opstap, voordek) geëxtrudeerd."""
    z0, z1 = ZC - BODY_W / 2, ZC + BODY_W / 2
    b0, b1 = BUNK
    prof = [(-L / 2, BASE), (L / 2, BASE), (L / 2, DECK), (XF, DECK), (XF, STEP), (b1, STEP), (b1, BODY_TOP),
            (b0, BODY_TOP), (b0, STEP), (XR, STEP), (XR, DECK), (-L / 2, DECK)]
    parts = [mk.prism(mk.chamfer_poly(prof, 0.05), z0, z1, 0.06, mat="M_Tender_Red")]
    # gouden bovenrand rond de bunker (één stuk, plat liggend kader)
    rim = mk.frame_ring(b0 + 0.09, b1 - 0.09, -(z1 - 0.09), -(z0 + 0.09), 0.12, BODY_TOP - 0.04, BODY_TOP + 0.08, 0.03,
                        mat="M_Tender_Gold")
    parts.append(rim.transform(R=mk.rot_x(-math.pi / 2)))
    # gouden hoekstrips (eindigen onzichtbaar in de rand) en gouden onderrand
    for xs in (b0 - 0.03, b1 - 0.09):
        for zs in (z0 - 0.03, z1 - 0.09):
            parts.append(rbox(xs, xs + 0.12, STEP - 0.02, BODY_TOP - 0.04, zs, zs + 0.12, 0.035, "M_Tender_Gold"))  # tot onder de rand
    for (za, zb) in ((z0 - 0.025, z0 + 0.05), (z1 - 0.05, z1 + 0.025)):
        parts.append(rbox(-L / 2 + 0.04, L / 2 - 0.04, BASE - 0.03, BASE + 0.08, za, zb, 0.03, "M_Tender_Gold"))
    # geschilderde panelen met klinknagels: twee per bunkerzijde
    for zf, sgn in ((z1, 1), (z0, -1)):
        za, zb = (zf - 0.01, zf + 0.012) if sgn > 0 else (zf - 0.012, zf + 0.01)
        mid = (b0 + b1) / 2
        for (pa, pb) in ((b0 + 0.18, mid - 0.06), (mid + 0.06, b1 - 0.18)):
            ya, yb = DECK - 0.20, BODY_TOP - 0.18
            parts.append(rbox(pa, pb, ya, yb, za, zb, 0.01, "M_Tender_Panel"))
            for (rx, ry) in ((pa + 0.08, ya + 0.08), (pb - 0.08, ya + 0.08), (pa + 0.08, yb - 0.08), (pb - 0.08, yb - 0.08)):
                parts.append(mk.ellipsoid(0.028, 0.028, 0.018, 10, 5, mat="M_Tender_Gold").transform(t=(rx, ry, zf + sgn * 0.014)))
    return mk.merge(*parts)


def build_coal():
    """Kolenberg (beloopbaar): lage koepel als vulling + onregelmatige gefacetteerde brokken erop."""
    b0, b1 = BUNK
    cx = (b0 + b1) / 2
    a, b, h = (b1 - b0) / 2 - 0.10, BODY_W / 2 - 0.12, 0.44
    base = mk.ellipsoid(a, h, b, 16, 8, mat="M_Tender_Coal").transform(t=(cx, BODY_TOP - 0.05, ZC))
    base.V[:, 1] = np.maximum(base.V[:, 1], BODY_TOP - 0.06)
    rng = np.random.default_rng(7)
    lumps = []
    for _ in range(64):
        while True:
            x, z = rng.uniform(-1, 1), rng.uniform(-1, 1)
            if x * x + z * z < 0.92:
                break
        y = BODY_TOP - 0.05 + h * math.sqrt(max(0.0, 1 - x * x - z * z)) * 0.92
        s = rng.uniform(0.12, 0.22)
        lump = mk.ellipsoid(s * rng.uniform(0.9, 1.3), s * rng.uniform(0.6, 0.9), s * rng.uniform(0.8, 1.2), 7, 4,
                            mat="M_Tender_Coal", n=2.3)
        lumps.append(lump.transform(R=mk.rot_y(rng.uniform(0, math.pi)) @ mk.rot_z(rng.uniform(-0.5, 0.5)),
                                    t=(cx + x * a * 0.95, y, ZC + z * b * 0.95)))
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
    """Gouden ladder op de voorhoek van de bunker (perronkant), van het onderstel tot de bovenrand."""
    x = BUNK[1] - 0.32
    z = ZC + BODY_W / 2 + 0.07
    parts = []
    for dx in (-0.17, 0.17):
        parts.append(mk.capsule_between((x + dx, -0.24, z), (x + dx, BODY_TOP + 0.10, z), 0.03, 0.03, 10, 2, mat="M_Tender_Gold"))
    for k in range(5):
        y = -0.12 + k * (BODY_TOP - 0.05) / 4
        parts.append(mk.capsule_between((x - 0.17, y, z), (x + 0.17, y, z), 0.024, 0.024, 8, 2, mat="M_Tender_Gold"))
    for y in (0.55, BODY_TOP - 0.10):
        for dx in (-0.17, 0.17):
            parts.append(mk.capsule_between((x + dx, y, z), (x + dx, y, z - 0.08), 0.024, 0.024, 8, 1, mat="M_Tender_Gold"))
    return mk.merge(*parts)


def build_rails():
    """Gele dekrelingen (één gebogen stang per kant: uit de opstap, langs, omlaag naar het dek)."""
    parts = []
    # de bovenregel begint in de bunkerwand (tot BODY_TOP), niet boven de lagere opstap: geen los zwevend eind
    for (xa, xb) in ((BUNK[1] - 0.04, L / 2 - 0.07), (BUNK[0] + 0.04, -L / 2 + 0.07)):
        for dz in (-(BODY_W / 2 - 0.07), BODY_W / 2 - 0.07):
            z = ZC + dz
            parts += [mk.capsule_between((xa, 1.38, z), (xb, 1.38, z), 0.03, 0.03, 10, 2, mat="M_Tender_Interact"),
                      mk.capsule_between((xb, 1.38, z), (xb, DECK - 0.01, z), 0.03, 0.03, 10, 2, mat="M_Tender_Interact")]
    return mk.merge(*parts)


def build_taillights():
    """Twee gloeiende sluitlampen aan de achterste relingstaanders."""
    x = -L / 2 + 0.07
    parts = []
    for dz in (-(BODY_W / 2 - 0.07), BODY_W / 2 - 0.07):
        house = mk.cylinder(0.15, 0.10, 18, 0.035, mat="M_Tender_Frame").transform(t=(x - 0.08, 1.12, ZC + dz))
        lens = mk.ellipsoid(0.06, 0.115, 0.115, 18, 9, mat="M_Tender_TailLamp").transform(t=(x - 0.13, 1.12, ZC + dz))
        parts += [house, lens]
    return mk.merge(*parts)


def build_fallplate():
    """Loopplaat (scharnier aan de voorkant van het voordek) die over de koppeling naar de cabinevloer reikt."""
    plate = rbox(0.0, 0.62, -0.035, 0.012, ZC - 0.46, ZC + 0.46, 0.012, "M_Tender_Frame", div=1)
    stripe = rbox(0.48, 0.60, 0.006, 0.022, ZC - 0.44, ZC + 0.44, 0.004, "M_Tender_Interact", div=1)   # 1 cm boven de plaat
    return mk.merge(plate, stripe)


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
    sc.material("M_Tender_Interact", R_["accent_yellow"][2], R_["accent_yellow"], 0.30, opts={"paint": 0.6})
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
    sc.add(mk.Part("DeckRails", build_rails(), (0, 0, 0), "Body"))
    sc.add(mk.Part("FallPlate", build_fallplate(), (L / 2, DECK + 0.003, 0), "Tender"))   # anker: loopplaat naar de loc
    for p in sc.parts:
        p.gradient = 0.30
        if p.name in ("Frame", "Body", "Coal", "Ladder", "TailLights", "DeckRails", "FallPlate"):
            p.ao_group = "body"
    sc.get("Wheels").gradient = 0.12
    sc.facet_all()
    sc.style_all(grime=1.0, **mk.TRAIN_CELLS)
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
