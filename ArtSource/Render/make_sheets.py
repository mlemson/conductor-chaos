"""Stelt de ontwerpbladen samen uit de renders (ArtSource/Render/out) -> Docs/Art/.

  Docs/Art/Conductor_DesignSheet.png   turnaround, expressies, pose, ramps, proporties
  Docs/Art/ArtCheck_Wagon.png          conducteur bij de wagondeur (dicht shot) + wijd shot
  Docs/Art/ArtCheck_vs_Concept.png     render naast de conceptafbeelding
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "out")
DOCS = os.path.join(ROOT, "Docs", "Art")
os.makedirs(DOCS, exist_ok=True)

BG = (0x33, 0x30, 0x3B)
PANEL = (0x3B, 0x38, 0x43)
PARCH = (0xF4, 0xDC, 0xC0)
INK = (0x35, 0x1B, 0x13)
TXT = (0xF4, 0xDC, 0xC0)
SUB = (0xDA, 0xC3, 0xB2)
MUTED = (0x85, 0x77, 0x71)
YEL = (0xFA, 0xBF, 0x34)


def font(size, bold=True):
    name = "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"
    for d in ("/usr/share/fonts/truetype/dejavu", "C:/Windows/Fonts"):
        p = os.path.join(d, name)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def label(d, xy, text, size=40, pad=(22, 12), rot_jitter=0):
    f = font(size)
    x, y = xy
    w = d.textlength(text, font=f)
    box = [x, y, x + w + pad[0] * 2, y + size + pad[1] * 2]
    # licht gescheurde perkamentrand
    rng = np.random.default_rng(int(x + y))
    pts = []
    n = max(6, int((box[2] - box[0]) / 40))
    for i in range(n + 1):
        pts.append((box[0] + (box[2] - box[0]) * i / n, box[1] + rng.uniform(-3, 3)))
    pts.append((box[2] + rng.uniform(-3, 3), box[3]))
    for i in range(n, -1, -1):
        pts.append((box[0] + (box[2] - box[0]) * i / n, box[3] + rng.uniform(-3, 3)))
    d.polygon(pts, fill=PARCH)
    d.text((x + pad[0], y + pad[1] - 2), text, font=f, fill=INK)
    return box


def sky(size):
    W, H = size
    top, mid, hor = np.array([0x7D, 0x99, 0xD5]), np.array([0x92, 0xBA, 0xF8]), np.array([0xC7, 0xD0, 0xF0])
    y = np.linspace(0, 1, H)[:, None]
    col = np.where(y < 0.5, top + (mid - top) * (y / 0.5), mid + (hor - mid) * np.clip((y - 0.5) / 0.3, 0, 1))
    return Image.fromarray(np.repeat(col[:, None, :], W, axis=1).astype(np.uint8), "RGB").convert("RGBA")


def load(name):
    return Image.open(os.path.join(OUT, name + ".png")).convert("RGBA")


def fit(im, w, h):
    s = min(w / im.width, h / im.height)
    return im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)


def crop_alpha(im, pad=10):
    a = np.array(im)[:, :, 3]
    ys, xs = np.where(a > 8)
    return im.crop((max(0, xs.min() - pad), max(0, ys.min() - pad), min(im.width, xs.max() + pad), min(im.height, ys.max() + pad)))


# ------------------------------------------------------------- design sheet
def design_sheet():
    W, H = 3000, 1820
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 50), "Conducteur · ontwerpblad v1", 58)
    d.text((64, 160), "P0 art proof (issue #9) · basismodel voor alle personages · kleuren uit palette.json (gemeten in het concept)",
           font=font(26, False), fill=SUB)

    # concept-referentie rechtsboven
    concept = Image.open(os.path.join(ROOT, "Docs", "References", "conductor-chaos-concept.png")).convert("RGBA")
    crew = concept.crop((20, 790, 180, 1000))
    crew = crew.resize((int(crew.width * 1.25), int(crew.height * 1.25)), Image.LANCZOS)
    d.rounded_rectangle([W - 280, 40, W - 50, 340], 14, fill=PARCH)
    sheet.alpha_composite(fit(crew, 210, 260), (W - 268, 52))
    d.text((W - 165, 352), "bron: concept", font=font(20, False), fill=MUTED, anchor="ma")

    # hero links
    d.rounded_rectangle([60, 220, 920, 1340], 22, fill=PANEL)
    hero = fit(crop_alpha(load("hero")), 800, 1060)
    sheet.alpha_composite(hero, (60 + (860 - hero.width) // 2, 250 + (1060 - hero.height) // 2))
    label(d, (84, 236), "Pose: 'Wacht even!'", 30)

    # turnaround
    tx, ty, tw, th = 960, 220, 1700, 760
    d.rounded_rectangle([tx, ty, tx + tw, ty + th], 22, fill=PANEL)
    label(d, (tx + 24, ty + 16), "Turnaround", 30)
    names = [("turn_front", "Voor"), ("turn_34", "3/4"), ("turn_side", "Zij"), ("turn_back", "Achter")]
    cw = (tw - 60) // 4
    for i, (n, cap) in enumerate(names):
        im = load(n)
        im = fit(im, cw, th - 120)
        x = tx + 30 + i * cw + (cw - im.width) // 2
        sheet.alpha_composite(im, (x, ty + 80))
        d.text((tx + 30 + i * cw + cw // 2, ty + th - 40), cap, font=font(26), fill=TXT, anchor="ma")
        if n == "turn_front":
            # proportielijnen (ortho: half hoogte 0.74 m rond y=0.62)
            sc = im.height / (2 * 0.74)
            def py(yv):
                return ty + 80 + im.height / 2 - (yv - 0.62) * sc
            for yv, txt in ((1.26, "1,26 m (pet)"), (0.835, "ogen"), (0.53, "kraag"), (0.0, "0")):
                yy = py(yv)
                d.line([x + 10, yy, x + im.width - 10, yy], fill=YEL, width=2)
                d.text((x + 14, yy - 26), txt, font=font(18), fill=YEL)

    # expressies
    ex, ey, ew, eh = 960, 1010, 1700, 330
    d.rounded_rectangle([ex, ey, ex + ew, ey + eh], 22, fill=PANEL)
    label(d, (ex + 24, ey + 14), "Expressies", 30)
    exprs = [("expr_neutral", "Neutraal"), ("expr_surprised", "Verbaasd (concept)"), ("expr_happy", "Blij"), ("expr_look", "Opzij kijken")]
    cw = (ew - 300) // 4
    for i, (n, cap) in enumerate(exprs):
        im = fit(load(n), cw - 20, eh - 80)
        x = ex + 290 + i * cw
        sheet.alpha_composite(im, (x, ey + 20))
        d.text((x + im.width // 2, ey + eh - 44), cap, font=font(22), fill=TXT, anchor="ma")
    d.text((ex + 30, ey + 90), "Wisselbaar in Unity:", font=font(20), fill=SUB)
    for k, t in enumerate(["Mouth_Neutral", "Mouth_Surprised", "Mouth_Happy", "Pupil_L/R (positie)", "Cap (kanteling)"]):
        d.text((ex + 30, ey + 122 + k * 30), "• " + t, font=font(19, False), fill=SUB)

    # ramps + materialen
    mats = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", "Conductor.materials.json"), encoding="utf-8"))
    rx, ry = 2700, 400
    d.rounded_rectangle([rx, ry, W - 50, 1340], 22, fill=PANEL)
    d.text((rx + 24, ry + 22), "Materialen", font=font(28), fill=YEL)
    d.text((rx + 24, ry + 58), "ramp: schaduw → licht", font=font(17, False), fill=MUTED)
    yy = ry + 96
    for name, m in mats["materials"].items():
        short = name.replace("M_Conductor_", "")
        d.text((rx + 24, yy), short, font=font(19), fill=TXT)
        for j, hx in enumerate(m["ramp"]):
            c = tuple(int(hx[k:k + 2], 16) for k in (1, 3, 5))
            d.rounded_rectangle([rx + 24 + j * 46, yy + 28, rx + 24 + j * 46 + 40, yy + 58], 6, fill=c)
        yy += 92

    # onderste strook: regels + techniek
    by = 1380
    d.rounded_rectangle([60, by, W - 50, H - 50], 22, fill=PANEL)
    label(d, (84, by + 18), "Vormentaal & bouw", 30)
    colL = [
        "Boonlijf: hoofd en romp één vorm (superellips-doorsnede, iets platte voorkant), geen nek",
        "Ogen raken elkaar: elk oog ≈ 37% van de lijfbreedte, bol, warm-wit; kleine verticale pupillen",
        "Geen neus, oren of wenkbrauwen; expressie via mond, pupillen, pet en houding",
        "Pet = silhouet-sleutel: bolle kroon ≈ 1,3× lijfbreedte, gouden band + embleem, donkere klep",
        "Dikke mouwen hoog uit de schouder, grote ronde handen; korte dikke beentjes met ronde kiezelvoeten",
        "Kraag: platte revers in V-vorm; pet-embleem = bolle gouden knop op de kroon",
    ]
    for k, t in enumerate(colL):
        d.text((90, by + 96 + k * 40), "✓  " + t, font=font(22, False), fill=TXT)
    st = mats["stats"]
    colR = [
        f"Hoogte {mats['height_m']:.2f} m (incl. pet) · lijf 0,56 m breed · heup-pivot 0,20 m",
        f"{st['tris']:,} driehoeken (incl. 2 verborgen monden + sleutel) · {st['materials']} materialen · geen textures".replace(",", "."),
        "Hiërarchie: Body › Cap, Eye_L/R › Pupil, Mouth_*, Arm_L/R › Hand › Prop_Key · Leg_L/R",
        "Starre 'poppen'-rig: elk onderdeel draait om zijn eigen pivot (schouder, heup, pet)",
        "Geschilderde variatie gebakken in vertexkleur: R occlusie · G randlicht · B verfvlekken",
        "Bron: ArtSource/Characters/Conductor/build_conductor.py → Conductor.fbx (Unity)",
    ]
    for k, t in enumerate(colR):
        d.text((1560, by + 96 + k * 40), "•  " + t, font=font(22, False), fill=SUB)
    sheet.convert("RGB").save(os.path.join(DOCS, "Conductor_DesignSheet.png"), optimize=True)


# --------------------------------------------------------------- art check
def art_check():
    close = load("artcheck_close")
    wide = load("artcheck_wide")
    bg = sky(close.size)
    bg.alpha_composite(close)
    close_rgb = bg
    bgw = sky(wide.size)
    bgw.alpha_composite(wide)
    W = 3000
    cw = 1980
    ch = int(close_rgb.height * cw / close_rgb.width)
    H = ch + 260
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 40), "Art check · conducteur bij de wagondeur", 50)
    d.text((64, 140), "Regionale wagon in facetstijl (P0 blockout) · geel = interactie: sleutelslot, grepen, drempel (anker voor P0-05)",
           font=font(24, False), fill=SUB)
    sheet.alpha_composite(close_rgb.resize((cw, ch), Image.LANCZOS), (60, 200))
    ww = W - cw - 160
    wh = int(bgw.height * ww / bgw.width)
    sheet.alpha_composite(bgw.resize((ww, wh), Image.LANCZOS), (cw + 100, 200))
    d.text((cw + 100, 220 + wh), "Wijd shot: schaal t.o.v. wagon en landschap", font=font(22), fill=TXT)
    notes = ["Facetstijl zoals de concept-locomotief: afgeschuinde blokken, vlakken met eigen toon",
             "Conducteur 1,25 m · wagon 7,2 m · deur 1,76 m hoog, 1,20 m breed",
             "Slot op 0,72 m: binnen bereik van de korte armpjes",
             "Livrei: treinrood, lichtere inzetpanelen, gouden bies, donkerbruin dak",
             "Deurlamp boven de deur = warm 'open'-signaal",
             "Decor (perron, bergen, dennen) alleen voor deze render",
             "Render = doel-look (ramp-shader), nog geen Unity-screenshot"]
    for k, t in enumerate(notes):
        d.text((cw + 100, 280 + wh + k * 40), "•  " + t, font=font(22, False), fill=SUB)
    sheet.convert("RGB").save(os.path.join(DOCS, "ArtCheck_Wagon.png"), optimize=True)
    return close_rgb


def vs_concept(close_rgb):
    concept = Image.open(os.path.join(ROOT, "Docs", "References", "conductor-chaos-concept.png")).convert("RGBA")
    W, H = 3000, 1300
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 40), "Naast het concept", 46)
    # links: concept crew + hero uitsnede
    crew = concept.crop((24, 800, 180, 1000))
    crew = crew.resize((crew.width * 5, crew.height * 5), Image.LANCZOS)
    crew = fit(crew, 640, 1000)
    sheet.alpha_composite(crew, (60, 170))
    d.text((60 + crew.width // 2, 1190), "Concept (The Crew)", font=font(26), fill=TXT, anchor="ma")
    hero = fit(crop_alpha(load("hero")), 640, 1000)
    hb = Image.new("RGBA", (680, 1000), BG + (255,))
    hb.alpha_composite(hero, ((680 - hero.width) // 2, (1000 - hero.height) // 2))
    sheet.alpha_composite(hb, (740, 170))
    d.text((740 + 340, 1190), "3D-model (render)", font=font(26), fill=TXT, anchor="ma")
    cc = concept.crop((400, 0, 1000, 440))
    cc = fit(cc, 760, 520)
    sheet.alpha_composite(cc, (1500, 170))
    d.text((1500 + cc.width // 2, 700), "Concept (hero)", font=font(22), fill=TXT, anchor="ma")
    cl = fit(close_rgb, 760, 520)
    sheet.alpha_composite(cl, (2200, 170))
    d.text((2200 + cl.width // 2, 700), "Art check (render)", font=font(22), fill=TXT, anchor="ma")
    checks = [("Silhouet & proporties", "✓ boonlijf, grote pet, korte ledematen"),
              ("Ogen", "✓ groot, rakend, kleine pupillen"),
              ("Kleuren", "✓ gemeten ramps; geen #000/#FFF"),
              ("Licht", "✓ warm key, koele rim; schaduw → violet"),
              ("Materiaal", "✓ geschilderd: AO, randlicht, verfvlekken"),
              ("Interactie leesbaar", "✓ geel = slot, paal, drempel"),
              ("Unity in-engine", "✗ NOT RUN (vereist Editor)")]
    for k, (a, b) in enumerate(checks):
        d.text((1500, 780 + k * 56), a, font=font(24), fill=YEL)
        d.text((1880, 780 + k * 56), b, font=font(24, False), fill=TXT)
    sheet.convert("RGB").save(os.path.join(DOCS, "ArtCheck_vs_Concept.png"), optimize=True)


# --------------------------------------------------------------- locomotief
def loco_sheet():
    W, H = 3000, 2180
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 40), "Locomotief + kolenwagen · ontwerpblad v2.1", 56)
    d.text((64, 148), "P0 art proof (issue #22) · v2 na art review: rondere neus, geïntegreerde koplamp, stevigere cabine, sierlijsten · kleuren uit palette.json",
           font=font(25, False), fill=SUB)
    hero = sky(load("loco_hero").size)
    hero.alpha_composite(load("loco_hero"))
    hw = 1640
    hh = int(hero.height * hw / hero.width)
    sheet.alpha_composite(hero.resize((hw, hh), Image.LANCZOS), (60, 200))
    label(d, (84, 216), "In het spel: loc, kolenwagen en regionale wagon", 28)
    # textuur naast het concept
    concept = Image.open(os.path.join(ROOT, "Docs", "References", "conductor-chaos-concept.png")).convert("RGBA")
    rx = 60 + hw + 40
    rw = W - rx - 50
    d.rounded_rectangle([rx, 200, W - 50, 200 + hh], 22, fill=PANEL)
    d.text((rx + 24, 222), "Textuur naast het concept", font=font(28), fill=YEL)
    cc = fit(concept.crop((400, 250, 900, 440)), rw - 48, 400)
    sheet.alpha_composite(cc, (rx + 24, 270))
    d.text((rx + 24, 280 + cc.height), "concept", font=font(20, False), fill=MUTED)
    tex = sky(load("loco_tex").size)
    tex.alpha_composite(load("loco_tex"))
    tx = fit(tex.crop((0, int(tex.height * 0.22), tex.width, int(tex.height * 0.80))), rw - 48, 400)
    ty = 320 + cc.height
    sheet.alpha_composite(tx, (rx + 24, ty))
    d.text((rx + 24, ty + tx.height + 10), "3D-model (render)", font=font(20, False), fill=MUTED)
    notes = ["Doorloopbaar: cabine-achterdeur → loopplaat → kolenwagen → wagon",
             "Achthoekige rookkastring met 4 gouden bouten, lens geel → oranje",
             "Ruime cabine met kooloventje (gloeiend deurtje, drukmeter, hendel)",
             "Kolenwagen: rode bak, gouden rand/hoekstrips, panelen met klinknagels",
             "Low-poly: effen vlakken, harde facetten, donkerder ingezette panelen"]
    for k, t in enumerate(notes):
        d.text((rx + 24, ty + tx.height + 50 + k * 32), "•  " + t, font=font(19, False), fill=SUB)
    # aanzichten
    by = 200 + hh + 30
    d.rounded_rectangle([60, by, W - 50, by + 560], 22, fill=PANEL)
    label(d, (84, by + 14), "Aanzichten", 30)
    views = [("loco_front34", "Voor 3/4"), ("loco_side", "Zijaanzicht met kolenwagen"), ("loco_rear34", "Achter 3/4"),
             ("tender_front34", "Kolenwagen: ladder, sluitlampen"), ("loco_cab", "Cabine: kooloventje")]
    cw = (W - 170) // 5
    for i, (n, cap) in enumerate(views):
        im = fit(crop_alpha(load(n)), cw - 40, 430)
        x = 90 + i * cw + (cw - im.width) // 2
        sheet.alpha_composite(im, (x, by + 80 + (430 - im.height) // 2))
        d.text((90 + i * cw + cw // 2, by + 520), cap, font=font(24), fill=TXT, anchor="ma")
    # onderste strook: materialen + bouw
    ly = by + 590
    d.rounded_rectangle([60, ly, W - 50, H - 40], 22, fill=PANEL)
    mats = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", "Locomotive_Blockout.materials.json"), encoding="utf-8"))
    d.text((90, ly + 24), "Materialen (ramp schaduw → licht)", font=font(24), fill=YEL)
    for i, (name, m) in enumerate(mats["materials"].items()):
        x = 90 + (i % 7) * 205
        y = ly + 70 + (i // 7) * 80
        d.text((x, y), name.replace("M_Loco_", ""), font=font(18), fill=TXT)
        for j, hx in enumerate(m["ramp"]):
            c = tuple(int(hx[k:k + 2], 16) for k in (1, 3, 5))
            d.rounded_rectangle([x + j * 36, y + 28, x + j * 36 + 31, y + 54], 5, fill=c)
    st = mats["stats"]
    tst = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", "Tender_Blockout.materials.json"), encoding="utf-8"))["stats"]
    info = [f"Loc ≈ 6,8 m · cabine 2,6 m · vloer 0,74 m · kolenwagen 3,8 m",
            f"Loc {st['tris']:,} · kolenwagen {tst['tris']:,} driehoeken".replace(",", "."),
            "Low-poly: effen platte vlakken, schaduw per facet, donkere inzetpanelen",
            "Kolenwagen: Frame, Body › Coal/Ladder/TailLights/DeckRails, FallPlate, Wheels",
            "Bron: ArtSource/Train/{Locomotive,Tender}/build_*.py → *_Blockout.fbx"]
    for k, t in enumerate(info):
        d.text((1560, ly + 70 + k * 40), "•  " + t, font=font(20, False), fill=SUB)
    sheet.convert("RGB").save(os.path.join(DOCS, "Locomotive_DesignSheet.png"), optimize=True)


if __name__ == "__main__":
    design_sheet()
    c = art_check()
    vs_concept(c)
    if os.path.exists(os.path.join(OUT, "loco_hero.png")):
        loco_sheet()
    print("ok ->", DOCS)
