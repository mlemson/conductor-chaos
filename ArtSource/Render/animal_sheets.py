"""Ontwerpbladen voor de dieren (schaap, koe) -> Docs/Art/.

Zelfde opbouw als de personagebladen: held-pose, turnaround, expressies, materiaalramps, kenmerken en
Unity-opbouw, met het conceptblad als bron rechtsboven en de bijbehorende wereldprop.

    python ArtSource/Render/animal_sheets.py          (na het renderen van Render/scenes/<Asset>_sheet.json)
"""
import json
import os

from PIL import Image, ImageDraw

from make_sheets import (BG, DOCS, MUTED, OUT, PANEL, PARCH, ROOT, SUB, TXT, YEL, crop_alpha, fit, font, label,
                         load)

RAMPS = {k: v["ramp"] for g in json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"),
                                              encoding="utf-8"))["groups"].values() for k, v in g.items()}
EXP = os.path.join(ROOT, "ArtSource", "Exports", "Animals")

SHEETS = {
    "Sheep": {
        "title": "Schaap", "concept": "sheep-concept.png", "hero": "Lief, nieuwsgierig en een beetje sullig",
        "expr": [("clueless", "Sullig (standaard)"), ("curious", "Nieuwsgierig"), ("happy", "Blij"),
                 ("surprised", "Verrast")],
        "prop": ("Prop_HayBale", "Hooibaal (wereldprop)"),
        "ramps": [("Wol", "wool"), ("Kop & poten", "safari_hat"), ("Hoeven", "hair_brown"),
                  ("Oogwit", "wool"), ("Mond", "mouth"), ("Accent", "accent_yellow")],
        "traits": [
            "Silhouet: een wolk van wol op vier stompe pootjes, wolkuif op de kop, oren opzij",
            "Wol is één gesloten oppervlak van zachte plukken (geen losse bollen), hangt tot vlak boven de hoeven",
            "Grote zandkleurige kop met de ogen en monden van de personages: past direct in de crew",
            "Klein 'o'-mondje als standaard: leest als 'geen idee wat hier gebeurt'",
            "Gele accentstreepjes (Fx_Alert) als losse knoop voor nieuwsgierig/verrast",
            "Past met gemak in de veewagen en op het spoor (0,72 m breed, 0,78 m hoog)",
        ],
        "nodes": "Sheep › Body › Head › (Tuft, Ear_L/R, Eye_L/R › Pupil, EyeClosed_*, Blink_*, Mouth_*, Fx_Alert), "
                 "Leg_FL/FR/BL/BR",
    },
    "Cow": {
        "title": "Koe", "concept": "cow-concept.png", "hero": "Goedig en altijd in de weg",
        "expr": [("calm", "Kalm (standaard)"), ("curious", "Nieuwsgierig"), ("happy", "Blij"),
                 ("startled", "Geschrokken")],
        "prop": ("Prop_MilkCan", "Melkbus (wereldprop)"),
        "ramps": [("Vacht", "parchment"), ("Vlekken", "hair_brown"), ("Snuit & oor", "skin"),
                  ("Hoorns", "scarf_cream"), ("Oogwit", "wool"), ("Accent", "accent_yellow")],
        "traits": [
            "Silhouet: ronde romp op vier stompe poten, brede snuit, donkere kuif tussen twee korte hoorns",
            "Vlekken zijn in het oppervlak gesneden: romp en kop blijven elk één oppervlak (geen dubbele vlakken)",
            "Donker 'masker' over de kop tot tussen de ogen; wangen crème, oren donker met roze binnenkant",
            "Brede perzikkleurige snuit met neusgaten en het kleine 'o'-mondje",
            "Staart met donkere pluim als losse knoop (kan zwiepen); poten en kop ook los te bewegen",
            "Breed genoeg om het hele spoor te blokkeren (0,93 m breed, 1,40 m lang, 1,00 m hoog)",
        ],
        "nodes": "Cow › Body › Head › (Muzzle, Tuft, Horn_L/R, Ear_L/R, Eye_L/R › Pupil, EyeClosed_*, Blink_*, "
                 "Mouth_*, Fx_Alert), Leg_FL/FR/BL/BR, Tail",
    },
}


def wrap(d, xy, text, fnt, maxw, fill, lead=34, bullet="•  "):
    x, y = xy
    line, first = "", True
    for w_ in text.split(" ") + [None]:
        if w_ is not None and d.textlength(line + " " + w_, font=fnt) < maxw:
            line = (line + " " + w_).strip()
            continue
        d.text((x, y), (bullet if first else " " * len(bullet) + " ") + line, font=fnt, fill=fill)
        y += lead
        first = False
        line = w_ or ""
    return y + 6


def animal_sheet(asset, cfg):
    W, H = 3000, 1900
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    mats = json.load(open(os.path.join(EXP, asset + ".materials.json"), encoding="utf-8"))
    label(d, (60, 50), f"{cfg['title']} · ontwerpblad v1", 58)
    d.text((64, 160), "Dier in de stijl van de bonen-personages · kleuren uit palette.json (geen nieuwe tinten)",
           font=font(26, False), fill=SUB)

    concept = Image.open(os.path.join(ROOT, "Docs", "References", "Animals", cfg["concept"])).convert("RGBA")
    c = fit(concept, 250, 312)
    d.rounded_rectangle([W - 50 - c.width - 24, 34, W - 50, 34 + c.height + 24], 14, fill=PARCH)
    sheet.alpha_composite(c, (W - 50 - c.width - 12, 46))
    d.text((W - 50 - (c.width + 24) // 2, 46 + c.height + 22), "bron: conceptblad", font=font(20, False),
           fill=MUTED, anchor="ma")

    d.rounded_rectangle([60, 220, 900, 1340], 22, fill=PANEL)
    hero = fit(crop_alpha(load(asset + "_hero")), 780, 760)
    sheet.alpha_composite(hero, (60 + (840 - hero.width) // 2, 300 + (760 - hero.height) // 2))
    label(d, (84, 236), cfg["hero"], 28)
    pname, pcap = cfg["prop"]
    prop = fit(crop_alpha(load(pname + "_hero")), 300, 230)
    sheet.alpha_composite(prop, (110, 1085 + (230 - prop.height) // 2))
    d.text((450, 1170), pcap, font=font(24), fill=TXT)
    pm = json.load(open(os.path.join(EXP, pname + ".materials.json"), encoding="utf-8"))
    d.text((450, 1206), f"{pm['stats']['tris']:,} driehoeken · {pm['height_m']:.2f} m hoog".replace(",", "."),
           font=font(19, False), fill=MUTED)

    tx, ty, tw, th = 940, 220, 1660, 760
    d.rounded_rectangle([tx, ty, tx + tw, ty + th], 22, fill=PANEL)
    label(d, (tx + 24, ty + 16), "Turnaround", 30)
    cw = (tw - 60) // 4
    for i, (n, cap) in enumerate((("front", "Voor"), ("34", "3/4"), ("side", "Zij"), ("back", "Achter"))):
        im = fit(load(f"{asset}_{n}"), cw, th - 150)
        x = tx + 30 + i * cw + (cw - im.width) // 2
        sheet.alpha_composite(im, (x, ty + 110))
        d.text((tx + 30 + i * cw + cw // 2, ty + th - 50), cap, font=font(26), fill=TXT, anchor="ma")

    ex, ey, ew, eh = 940, 1010, 1660, 330
    d.rounded_rectangle([ex, ey, ex + ew, ey + eh], 22, fill=PANEL)
    label(d, (ex + 24, ey + 14), "Expressies", 30)
    cw = (ew - 60) // len(cfg["expr"])
    for i, (key, cap) in enumerate(cfg["expr"]):
        im = fit(load(f"{asset}_expr_{key}"), cw - 30, eh - 100)
        sheet.alpha_composite(im, (ex + 30 + i * cw + (cw - im.width) // 2, ey + 62))
        d.text((ex + 30 + i * cw + cw // 2, ey + eh - 40), cap, font=font(22), fill=TXT, anchor="ma")

    rx, ry = 2640, 420
    d.rounded_rectangle([rx, ry, W - 50, 1340], 22, fill=PANEL)
    d.text((rx + 24, ry + 22), "Kleurramps", font=font(28), fill=YEL)
    d.text((rx + 24, ry + 58), "schaduw → licht", font=font(17, False), fill=MUTED)
    yy = ry + 100
    for cap, key in cfg["ramps"]:
        d.text((rx + 24, yy), cap, font=font(19), fill=TXT)
        d.text((W - 74, yy + 2), key, font=font(14, False), fill=MUTED, anchor="ra")
        for j, hx in enumerate(RAMPS[key]):
            col = tuple(int(hx[k:k + 2], 16) for k in (1, 3, 5))
            d.rounded_rectangle([rx + 24 + j * 50, yy + 30, rx + 24 + j * 50 + 44, yy + 62], 6, fill=col)
        yy += 112

    by = 1380
    d.rounded_rectangle([60, by, W - 50, H - 50], 22, fill=PANEL)
    label(d, (84, by + 18), "Kenmerken", 30)
    for k, t in enumerate(cfg["traits"]):
        d.text((90, by + 96 + k * 40), "✓  " + t, font=font(22, False), fill=TXT)
    st = mats["stats"]
    tech = [
        f"Hoogte {mats['height_m']:.2f} m · {st['tris']:,} driehoeken (incl. verborgen expressies) · "
        f"{st['materials']} materialen · geen textures".replace(",", "."),
        "Hiërarchie: " + cfg["nodes"],
        "CharacterFace werkt ongewijzigd (zelfde knoopnamen): oogstand, mond en knipperen",
        "Losse knopen met draaipunt op de aanhechting: kop, poten, oren (en staart) eenvoudig te animeren",
        f"Bron: ArtSource/Animals/{asset}/ → Assets/Art/Models/Animals/{asset}/{asset}.fbx",
    ]
    d.text((1700, by + 30), "Unity & bouw", font=font(26), fill=YEL)
    yy = by + 96
    for t in tech:
        yy = wrap(d, (1700, yy), t, font(20, False), W - 90 - 1730, SUB)
    path = os.path.join(DOCS, f"{asset}_DesignSheet.png")
    sheet.convert("RGB").save(path, optimize=True)
    return path


def lineup():
    if not os.path.exists(os.path.join(OUT, "Animals_lineup.png")):
        return None
    im = load("Animals_lineup")
    W, H = im.width, im.height + 150
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 40), "Dieren op schaal · conducteur, schaap, koe, hooibaal en melkbus", 46)
    sheet.alpha_composite(im, (0, 150))
    path = os.path.join(DOCS, "Animals_Lineup.png")
    sheet.convert("RGB").save(path, optimize=True)
    return path


def main():
    out = [animal_sheet(a, cfg) for a, cfg in SHEETS.items() if os.path.exists(os.path.join(OUT, a + "_hero.png"))]
    extra = lineup()
    print("\n".join(out + ([extra] if extra else [])))


if __name__ == "__main__":
    main()
