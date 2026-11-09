"""Ontwerpbladen voor de personagevarianten (passagiers, stationspersoneel, monteur) -> Docs/Art/.

Zelfde opbouw als het conducteur-ontwerpblad: held-pose, turnaround met maatlijnen, expressies,
materiaalramps, kenmerken en Unity-opbouw, met het conceptblad als bron rechtsboven.

    python ArtSource/Render/character_sheets.py          (na het renderen van Render/scenes/<Asset>_sheet.json)
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw

from make_sheets import (BG, DOCS, MUTED, OUT, PANEL, PARCH, ROOT, SUB, TXT, YEL, crop_alpha, fit, font, label,
                         load, sky)

RAMPS = {k: v["ramp"] for g in json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"),
                                              encoding="utf-8"))["groups"].values() for k, v in g.items()}

SHEETS = {
    "ElderlyPassenger": {
        "title": "Oudere passagier",
        "concept": "elderly-passenger-concept.png",
        "hero": "Pose: 'Welk perron was het ook alweer?'",
        "expr": [("neutral", "Neutraal"), ("happy", "Blij"), ("confused", "Verward"), ("thinking", "Nadenken")],
        "swaps": ["Mouth_Neutral / Surprised / Happy / Hmm / Smile / Flat", "EyeClosed_L/R (blije ogen)",
                  "Pupil_L/R (kijkrichting)", "Prop_Bag aan Hand_L", "Beret (kanteling)"],
        "ramps": [("Jas", "coat_plum"), ("Baret & bril", "beret_plum"), ("Haar", "hair_grey"),
                  ("Sjaal & tas", "scarf_cream"), ("Huid", "skin"), ("Schoenen", "boot_brown"),
                  ("Bloemhart", "accent_yellow")],
        "traits": [
            "Silhouet: brede wolk grijze krullen + baret met bloem = direct herkenbaar, ook van achteren",
            "Rond paars brilletje over de ogen (montuur, geen glazen); pootjes verdwijnen in het haar",
            "Dikke crème sjaal als rol om de hals, met hangende punt en franje aan de rechterkant",
            "Pruimkleurige jas met uitlopende zoom, twee grote mauve knopen en klepzakken",
            "Crème draagtas met paars treintje in de linkerhand: leest als 'reiziger' op afstand",
            "Zelfde boonlijf, ogen, monden en armen als de conducteur (één basismodel)",
        ],
    },
    "Tourist": {
        "title": "Toerist",
        "concept": "tourist-concept.png",
        "hero": "Pose: 'Kijk, hier moeten we heen!'",
        "expr": [("neutral", "Neutraal"), ("excited", "Enthousiast"), ("surprised", "Verbaasd"), ("happy", "Blij")],
        "swaps": ["Mouth_Neutral / Surprised / Happy / Hmm / Smile / Flat", "EyeClosed_L/R (blije ogen)",
                  "Pupil_L/R (kijkrichting)", "Prop_Map aan Hand_L, Prop_Ticket aan Hand_R", "Hat (kanteling)"],
        "ramps": [("Hawaïhemd", "shirt_coral"), ("Safarihoed", "safari_hat"), ("Korte broek", "shorts_olive"),
                  ("Rugzak", "backpack_teal"), ("Slaapmatje", "helmet"), ("Huid", "skin"),
                  ("Schoenen/banden", "boot_brown")],
        "traits": [
            "Silhouet: brede safarihoed + grote rugzak met opgerold slaapmatje (zij- en achteraanzicht)",
            "Zonnebril op de hoed, rode hoedband; de ogen blijven vrij voor expressie",
            "Rood hawaïhemd met bloemenprint (gele/oranje bloemen + blaadjes), open kraag met crème shirt",
            "Olijf korte broek met beenzakken, blote beentjes en bruine wandelschoenen",
            "Kaart in de linkerhand, treinkaartje in de rechterhand: de eerste passagier-props",
            "Waterfles in de zijzak, schouderbanden met gouden gespen over de 'schouders'",
        ],
    },
    "StationMaster": {
        "title": "Stationschef",
        "concept": "station-master-concept.png",
        "hero": "Pose: 'Welkom op mijn station!'",
        "expr": [("proud", "Trots"), ("kind", "Vriendelijk"), ("thinking", "Nadenken"), ("surprised", "Verbaasd")],
        "swaps": ["Mouth_Smile (standaard) / Happy / Hmm / Surprised / Flat", "Lid_L/R (rustige blik, standaard aan)",
                  "EyeClosed_L/R (vriendelijke ogen)", "Pupil_L/R (kijkrichting)", "Cap (kanteling)"],
        "ramps": [("Uniform", "master_navy"), ("Pet & vest", "cap_maroon"), ("Goud", "gold_trim"),
                  ("Snor & haar", "mustache_cream"), ("Overhemd", "parchment"), ("Huid", "skin"),
                  ("Klep", "ink")],
        "traits": [
            "Silhouet: hoge bordeauxrode stationspet met gouden band, klep en locomotief-embleem",
            "Grote witte krulsnor + witte wenkbrauwen en bakkebaarden (bewuste uitzondering: concept toont ze)",
            "Donker marineblauw uniformjasje met gouden biezen, revers, knoopjes en locomotief-speld",
            "Bordeauxrood gilet met gouden knoopjes en stropdas over een crème overhemd",
            "Gouden zakhorloge aan een ketting op het gilet; gouden manchetbanden aan de mouwen",
            "Zwaaiende linkerhand: leest op afstand als 'de baas van het station'",
        ],
    },
    "BusinessTraveler": {
        "title": "Zakenreiziger",
        "concept": "business-traveler-concept.png",
        "hero": "Pose: 'Ik ben te laat!'",
        "expr": [("rushed", "Gehaast"), ("focused", "Gefocust"), ("relieved", "Opgelucht"), ("exhausted", "Uitgeput")],
        "swaps": ["Mouth_Neutral (standaard) / Surprised / Flat / Smile / Hmm", "Lid_L/R, Prop_Sweat (zweetdruppels)",
                  "EyeClosed_L/R (opgelucht)", "Pupil_L/R (kijkrichting)", "Prop_Suitcase aan Hand_L"],
        "ramps": [("Colbert", "uniform_navy"), ("Koffer", "luggage_blue"), ("Haar", "hair_brown"),
                  ("Tas & riem", "leather"), ("Stropdas", "hoodie_red"), ("Overhemd", "parchment"),
                  ("Kofferlabel", "accent_yellow")],
        "traits": [
            "Silhouet: rolkoffer met uitgeschoven greep + schoudertas met papieren die eruit steken",
            "Strak blauw colbert met revers, gouden knoop en klepzakken; wit overhemd met rode stropdas",
            "Kort bruin haar met een kuif naar opzij, brilletje omhooggeschoven bovenop het hoofd",
            "Schuine tasriem over de borst (voor- en achteraanzicht), bruine tas op de heup",
            "Gele kofferlabel met locomotief-icoon, ribbels en wieltjes aan de koffer",
            "Zweetdruppels als losse knoop: de 'gehaaste reiziger' in één oogopslag",
        ],
    },
    "ChildPassenger": {
        "title": "Ondeugend kind",
        "concept": "child-passenger-concept.png",
        "hero": "Pose: 'Kijk eens wat ik kan!'",
        "expr": [("mischievous", "Ondeugend"), ("excited", "Opgewonden"), ("caught", "Betrapt"), ("sly", "Stiekem")],
        "swaps": ["Mouth_Grin (scheve grijns, standaard) / Happy / Surprised / Smile", "Lid_L/R (ondeugend, standaard aan)",
                  "EyeClosed_L/R", "Pupil_L/R (kijkrichting)", "Prop_Plane aan Hand_L", "Cap (kanteling)"],
        "ramps": [("Hoodie", "hoodie_red"), ("Kraag & mouw", "accent_yellow"), ("Broek & pet", "uniform_navy"),
                  ("Haar", "hair_brown"), ("Dino", "dino_green"), ("Pleister", "scarf_cream"), ("Huid", "skin")],
        "traits": [
            "Kleiner model (80% van het basismodel) met dezelfde ogen en monden: leest direct als kind",
            "Achterstevoren gedragen pet met rode klep naar achteren en sluitbandje op het voorhoofd",
            "Rode hoodie met gele kraag, capuchon, koordjes, buidelzak en gele mouwen",
            "Rugzak met gele ster en een groen dinootje dat over de schouder meekijkt",
            "Papieren vliegtuigje in de opgestoken hand; pleister op de wang",
            "Marineblauwe korte broek, sokjes en sneakers met witte zool",
        ],
    },
    "BaggageHelper": {
        "title": "Bagagehelper",
        "concept": "baggage-helper-concept.png",
        "hero": "Pose: 'Ik breng het wel even weg!'",
        "expr": [("helpful", "Behulpzaam"), ("focused", "Gefocust"), ("cheerful", "Vrolijk"), ("surprised", "Verbaasd")],
        "swaps": ["Mouth_Happy (standaard) / Flat / Surprised", "Lid_L/R, Prop_Sweat", "EyeClosed_L/R (vrolijk)",
                  "Pupil_L/R (kijkrichting)", "Cap (kanteling)", "BaggageTrolley: los prop met 'Handle'-anker"],
        "ramps": [("Werkkleding", "uniform_navy"), ("Pet", "cap_maroon"), ("Overhemd", "parchment"),
                  ("Handschoenen", "boot_brown"), ("Bagagekar", "accent_yellow"), ("Koffers", "luggage_red"),
                  ("Huid", "skin")],
        "traits": [
            "Marineblauwe tuinbroek met borststuk, bretels, gouden knopen en een koffer-badge",
            "Bordeauxrode pet met gouden band en koffer-embleem; korte mouwen en dikke handschoenen",
            "Losse gele bagagekar (BaggageTrolley.fbx) met vier wielen, duwbeugel en drie gestapelde koffers",
            "Duwbeugel op handhoogte: het anker 'Handle' van de kar valt in de linkerhand",
            "Koffers met hoekbeschermers, riemen, gespen en handvatten (rood, blauw, leer)",
            "Vrolijke open mond als standaard: de behulpzame kracht van het station",
        ],
    },
    "RailMechanic": {
        "title": "Spoormonteur",
        "concept": "rail-mechanic-concept.png",
        "hero": "Pose: 'Ik regel het wel.'",
        "expr": [("determined", "Vastberaden"), ("surprised", "Verbaasd"), ("happy", "Blij"), ("working", "Aan het werk")],
        "swaps": ["Lid_L/R (vastberaden, standaard aan)", "Mouth_Flat (standaard) / Surprised / Happy / Smile",
                  "EyeClosed_L/R, Prop_Sweat (zweetdruppels)", "Pupil_L/R (kijkrichting)", "Helmet (kanteling)"],
        "ramps": [("Helm", "helmet"), ("Overall", "uniform_navy"), ("Leer", "leather"),
                  ("Handschoen/laars", "boot_brown"), ("Gespen", "gold_trim"), ("Gereedschap", "metal"),
                  ("Lamp", "headlight")],
        "traits": [
            "Silhouet: gele bouwhelm met nok, smalle rand en hoofdlamp op een bruine band",
            "Vastberaden blik met schuine oogleden in huidkleur (geen wenkbrauwen, volgens de stijlregels)",
            "Marineblauwe overall (gemeten = conducteurblauw) met reflecterende band om de pijpen",
            "Leren bretels met gouden gespen, gereedschapsgordel met grote gesp en twee tasjes",
            "Sleutel + schroevendraaier in het linkertasje, sleutel in het borstzakje",
            "Dikke bruine werkhandschoenen: grotere 'wanten' met manchet, zelfde arm-rig",
        ],
    },
}


def character_sheet(asset, cfg):
    W, H = 3000, 1900
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    mats = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", asset + ".materials.json"), encoding="utf-8"))
    label(d, (60, 50), f"{cfg['title']} · ontwerpblad v1", 58)
    d.text((64, 160), "Personagevariant op het conducteur-basismodel · kleuren gemeten in het conceptblad "
                      "(palette.json, 'Reizigers & crew')", font=font(26, False), fill=SUB)

    # conceptblad rechtsboven
    concept = Image.open(os.path.join(ROOT, "Docs", "References", "Characters", cfg["concept"])).convert("RGBA")
    c = fit(concept, 250, 312)
    d.rounded_rectangle([W - 50 - c.width - 24, 34, W - 50, 34 + c.height + 24], 14, fill=PARCH)
    sheet.alpha_composite(c, (W - 50 - c.width - 12, 46))
    d.text((W - 50 - (c.width + 24) // 2, 46 + c.height + 22), "bron: conceptblad", font=font(20, False),
           fill=MUTED, anchor="ma")

    # held links
    d.rounded_rectangle([60, 220, 900, 1340], 22, fill=PANEL)
    hero = fit(crop_alpha(load(asset + "_hero")), 780, 1040)
    sheet.alpha_composite(hero, (60 + (840 - hero.width) // 2, 280 + (1040 - hero.height) // 2))
    label(d, (84, 236), cfg["hero"], 28)

    # turnaround
    tx, ty, tw, th = 940, 220, 1660, 760
    d.rounded_rectangle([tx, ty, tx + tw, ty + th], 22, fill=PANEL)
    label(d, (tx + 24, ty + 16), "Turnaround", 30)
    cw = (tw - 60) // 4
    for i, (n, cap) in enumerate((("front", "Voor"), ("34", "3/4"), ("side", "Zij"), ("back", "Achter"))):
        im = fit(load(f"{asset}_{n}"), cw, th - 120)
        x = tx + 30 + i * cw + (cw - im.width) // 2
        sheet.alpha_composite(im, (x, ty + 80))
        d.text((tx + 30 + i * cw + cw // 2, ty + th - 40), cap, font=font(26), fill=TXT, anchor="ma")
        if n == "front":
            sc = im.height / (2 * 0.74)
            for yv, txt in ((mats["height_m"], f"{mats['height_m']:.2f} m".replace(".", ",")), (0.835, "ogen"),
                            (0.53, "kraag"), (0.0, "0")):
                yy = ty + 80 + im.height / 2 - (yv - 0.62) * sc
                d.line([x + 6, yy, x + im.width - 6, yy], fill=YEL, width=2)
                d.text((x + 10, yy - 26), txt, font=font(18), fill=YEL)

    # expressies
    ex, ey, ew, eh = 940, 1010, 1660, 330
    d.rounded_rectangle([ex, ey, ex + ew, ey + eh], 22, fill=PANEL)
    label(d, (ex + 24, ey + 14), "Expressies", 30)
    n = len(cfg["expr"])
    cw = (ew - 60) // n
    for i, (key, cap) in enumerate(cfg["expr"]):
        im = fit(load(f"{asset}_expr_{key}"), cw - 30, eh - 100)
        x = ex + 30 + i * cw + (cw - im.width) // 2
        sheet.alpha_composite(im, (x, ey + 62))
        d.text((ex + 30 + i * cw + cw // 2, ey + eh - 40), cap, font=font(22), fill=TXT, anchor="ma")

    # ramps rechts
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

    # onderstrook
    by = 1380
    d.rounded_rectangle([60, by, W - 50, H - 50], 22, fill=PANEL)
    label(d, (84, by + 18), "Kenmerken", 30)
    for k, t in enumerate(cfg["traits"]):
        d.text((90, by + 96 + k * 40), "✓  " + t, font=font(22, False), fill=TXT)
    st = mats["stats"]
    tech = [
        f"Hoogte {mats['height_m']:.2f} m · {st['tris']:,} driehoeken (incl. verborgen expressies) · "
        f"{st['materials']} materialen · geen textures".replace(",", "."),
        "Hiërarchie: Body › Eye_L/R › Pupil, Lid_*, EyeClosed_*, Blink_*, Mouth_*, Arm_L/R › Hand › Prop_*",
        "CharacterFace: oogstand (open/ooglid/dicht) en mond kiezen, knippert af en toe vanzelf (Blink_L/R)",
        "Wisselbaar in Unity: " + " · ".join(cfg["swaps"]),
        "Starre poppen-rig zoals de conducteur; vertexkleur R occlusie · G randlicht",
        f"Bron: ArtSource/Characters/{asset}/ → Assets/Art/Models/Characters/{asset}/{asset}.fbx",
    ]
    d.text((1700, by + 30), "Unity & bouw", font=font(26), fill=YEL)
    f20, yy, maxw = font(20, False), by + 96, W - 90 - 1730
    for t in tech:
        words, line, first = t.split(" "), "", True
        for w_ in words + [None]:
            if w_ is not None and d.textlength(line + " " + w_, font=f20) < maxw:
                line = (line + " " + w_).strip()
                continue
            d.text((1700, yy), ("•  " if first else "    ") + line, font=f20, fill=SUB)
            yy += 34
            first = False
            line = w_ or ""
        yy += 6
    path = os.path.join(DOCS, f"{asset}_DesignSheet.png")
    sheet.convert("RGB").save(path, optimize=True)
    return path


def crew_lineup():
    """Alle personages naast elkaar (zelfde schaal) — de crew van Conductor Chaos."""
    names = [("turn_front", "Conducteur"), ("StationMaster_front", "Stationschef"),
             ("ElderlyPassenger_front", "Oudere passagier"), ("Tourist_front", "Toerist"),
             ("BusinessTraveler_front", "Zakenreiziger"), ("ChildPassenger_front", "Kind"),
             ("BaggageHelper_front", "Bagagehelper"), ("RailMechanic_front", "Spoormonteur")]
    if not all(os.path.exists(os.path.join(OUT, n + ".png")) for n, _ in names):
        return None
    W, H = 3400, 1200
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 44), "De crew · zelfde basismodel, eigen silhouet", 50)
    cw = (W - 120) // len(names)
    for i, (n, cap) in enumerate(names):
        im = load(n)                                    # zelfde ortho-frame: echte hoogteverschillen blijven
        xs = np.where(np.array(im)[:, :, 3].max(axis=0) > 8)[0]
        im = im.crop((max(0, xs.min() - 8), 0, min(im.width, xs.max() + 8), im.height))
        s = 900 / im.height
        im = im.resize((int(im.width * s), 900), Image.LANCZOS)
        x = 60 + i * cw + (cw - im.width) // 2
        sheet.alpha_composite(im, (x, 160))
        d.text((60 + i * cw + cw // 2, 1100), cap, font=font(30), fill=TXT, anchor="ma")
    path = os.path.join(DOCS, "Characters_Lineup.png")
    sheet.convert("RGB").save(path, optimize=True)
    return path


def in_scene():
    """Render van de varianten bij de trein, op de lucht van de stijlkaart."""
    if not os.path.exists(os.path.join(OUT, "characters_in_scene.png")):
        return None
    im = load("characters_in_scene")
    bg = sky(im.size)
    bg.alpha_composite(im)
    d = ImageDraw.Draw(bg)
    label(d, (40, 34), "Reizigers & crew bij de trein (three.js-stijlrender)", 44)
    path = os.path.join(DOCS, "Characters_InScene.png")
    bg.convert("RGB").save(path, optimize=True)
    return path


def main():
    out = []
    for asset, cfg in SHEETS.items():
        if os.path.exists(os.path.join(OUT, asset + "_hero.png")):
            out.append(character_sheet(asset, cfg))
    for extra in (crew_lineup(), in_scene()):
        if extra:
            out.append(extra)
    print("\n".join(out))


if __name__ == "__main__":
    main()
