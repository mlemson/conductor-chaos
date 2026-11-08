"""Ontwerpbladen voor het stationnetje en de perronkit (issue #182) -> Docs/Art/.

    python ArtSource/Render/station_sheets.py      (na het renderen van Render/scenes/station_sheet.json)

* Station_DesignSheet.png   gebouw van alle kanten, doorkijk zonder dak, deur, concept, kleurramps, kenmerken
* Station_PlatformKit.png   perronmodules + props als thumbnails, met concept
* Station_InScene.png       het station bij de trein, met personages
"""
import json
import os

from PIL import Image, ImageDraw

from make_sheets import BG, DOCS, MUTED, OUT, PANEL, PARCH, ROOT, SUB, TXT, YEL, crop_alpha, fit, font, label, load, sky

PAL = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
RAMPS = {k: v["ramp"] for g in PAL["groups"].values() for k, v in g.items()}
KIT = json.load(open(os.path.join(ROOT, "ArtSource", "Exports", "Environment", "Station", "StationKit.json"),
                     encoding="utf-8"))["assets"]
REF = os.path.join(ROOT, "Docs", "References", "Environment")

NAMES = {
    "Prop_Bench": "Bank", "Prop_Barrel": "Ton", "Prop_Crate": "Krat", "Prop_CrateStack": "Kratten",
    "Prop_Planter": "Plantenbak", "Prop_TrashBin": "Prullenbak", "Prop_LampPost": "Lantaarnpaal",
    "Prop_WallLantern": "Wandlantaarn", "Prop_HangingSign": "Hangbord", "Prop_StationSign": "Stationsbord",
    "Prop_Timetable": "Vertrekbord", "Prop_TicketMachine": "Kaartautomaat", "Prop_LuggageCart": "Bagagekar",
    "Prop_LevelCrossing": "Overweg", "Prop_Fence_2m": "Hek 2 m", "Prop_Shelter": "Wachthuisje",
    "Platform_Straight_3m": "Recht 3 m", "Platform_Straight_6m": "Recht 6 m", "Platform_EndCap_End": "Kopeinde (eind)",
    "Platform_EndCap_Start": "Kopeinde (begin)", "Platform_Corner_End": "Hoek (eind)", "Platform_Corner_Start": "Hoek (begin)",
    "Platform_Ramp_End": "Helling (eind)", "Platform_Ramp_Start": "Helling (begin)", "Platform_Stairs_End": "Trap (eind)",
    "Platform_Stairs_Start": "Trap (begin)",
}


def ramps_panel(d, x, y, w, items):
    d.text((x + 24, y + 20), "Kleurramps", font=font(28), fill=YEL)
    d.text((x + 24, y + 56), "schaduw → licht (palette.json)", font=font(17, False), fill=MUTED)
    yy = y + 96
    for cap, key in items:
        d.text((x + 24, yy), cap, font=font(19), fill=TXT)
        d.text((x + w - 24, yy + 2), key, font=font(14, False), fill=MUTED, anchor="ra")
        for j, hx in enumerate(RAMPS[key]):
            col = tuple(int(hx[k:k + 2], 16) for k in (1, 3, 5))
            d.rounded_rectangle([x + 24 + j * 50, yy + 30, x + 24 + j * 50 + 44, yy + 60], 6, fill=col)
        yy += 104
    return yy


def concept_box(sheet, d, path, x, y, w, h):
    c = fit(Image.open(path).convert("RGBA"), w, h)
    d.rounded_rectangle([x, y, x + c.width + 24, y + c.height + 54], 14, fill=PARCH)
    sheet.alpha_composite(c, (x + 12, y + 12))
    d.text((x + (c.width + 24) // 2, y + c.height + 20), "bron: conceptblad", font=font(19, False), fill=MUTED, anchor="ma")


def building_sheet():
    W, H = 3200, 2050
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 46), "Stationnetje · ontwerpblad v1", 58)
    d.text((64, 156), "Begaanbaar: hal 6,4 × 5,4 m met loket en wachtruimte, vloer op perronhoogte (1,05 m), luifel met bank · "
                      "kleuren gemeten in het conceptblad", font=font(25, False), fill=SUB)
    # held
    d.rounded_rectangle([60, 210, 1560, 1290], 22, fill=PANEL)
    hero = fit(crop_alpha(load("stn_bld_front")), 1440, 1000)
    sheet.alpha_composite(hero, (60 + (1500 - hero.width) // 2, 260 + (1010 - hero.height) // 2))
    label(d, (84, 226), "Voorkant (naar het spoor)", 28)
    # drie kleinere aanzichten
    views = (("stn_bld_back", "Achter + rechts"), ("stn_bld_left", "Luifel (links)"), ("stn_bld_inside", "Binnen (dak weg)"))
    for i, (n, cap) in enumerate(views):
        x0, y0 = 1600 + (i % 2) * 790, 210 + (i // 2) * 540
        d.rounded_rectangle([x0, y0, x0 + 760, y0 + 520], 22, fill=PANEL)
        im = fit(crop_alpha(load(n)), 720, 440)
        sheet.alpha_composite(im, (x0 + (760 - im.width) // 2, y0 + 64 + (440 - im.height) // 2))
        label(d, (x0 + 20, y0 + 14), cap, 24)
    x0, y0 = 1600 + 790, 210 + 540
    d.rounded_rectangle([x0, y0, x0 + 760, y0 + 520], 22, fill=PANEL)
    im = fit(load("stn_bld_door"), 720, 440)
    sheet.alpha_composite(im, (x0 + (760 - im.width) // 2, y0 + 64))
    label(d, (x0 + 20, y0 + 14), "Loket + deur", 24)
    # onderstrook: concept, kenmerken, Unity, ramps
    by = 1330
    concept_box(sheet, d, os.path.join(REF, "station-concept.png"), 60, by, 380, 610)
    d.rounded_rectangle([510, by, 2380, H - 50], 22, fill=PANEL)
    label(d, (534, by + 18), "Kenmerken", 30)
    traits = [
        "Topgevel naar het spoor met klok, STATION-bord op de bindbalk, vakwerk met korbelen, gele nokbol",
        "Boogloket met toonbank en kassa (loketruimte achter een L-toonbank met doorgang) + open boogdeur",
        "Deur is een eigen knoop 'Door' met scharnier (Y-as) en botsvorm; staat 96° open, kan dicht in Unity",
        "Luifel op drie palen met korbelen: bank, tonnen, kratten, hangbord; deur van de hal naar de luifel",
        "Wachtruimte: twee banken, vertrekbord, plant, koffer, twee hanglampen, open kap met nokbalk",
        "Stenen plint, dikke dakpannen in rijen, schoorsteen, ramen met warm glas, bloembak onder het zijraam",
        "Gebouw is 2,4× zo hoog binnen als het grootste personage (1,26 m); deur 1,30 × 2,20 m",
    ]
    for k, t in enumerate(traits):
        d.text((540, by + 90 + k * 38), "✓  " + t, font=font(21, False), fill=TXT)
    a = KIT["Station_Building"]
    tech = [f"{a['tris']:,} driehoeken (incl. interieur) · 0 textures · materialen M_Stn_* (StylizedRamp)".replace(",", "."),
            "Pivot: midden voorgevel op maaiveld, voorkant +Z → 180° draaien, voorgevel 5,15 m uit het hart van het spoor",
            "Unity: Col_Main / Col_Door → MeshColliders, Light_* → warme puntlichten (Conductor Chaos > Art > Station)"]
    for k, t in enumerate(tech):
        d.text((540, by + 90 + (len(traits) + 0.4 + k) * 38), "•  " + t, font=font(19, False), fill=SUB)
    d.rounded_rectangle([2420, by, W - 50, H - 50], 22, fill=PANEL)
    ramps_panel(d, 2420, by, 730, [("Dak, deur, borden", "station_navy"), ("Vakwerk & palen", "station_timber"),
                                   ("Pleisterwerk", "parchment"), ("Plint & schoorsteen", "plinth_stone"),
                                   ("Geel (bol, klok, bord)", "accent_yellow")])
    path = os.path.join(DOCS, "Station_DesignSheet.png")
    sheet.convert("RGB").save(path, optimize=True)
    return path


def kit_sheet():
    plat = [n for n in NAMES if n.startswith("Platform")]
    prop = [n for n in NAMES if n.startswith("Prop")]
    W = 3200
    cw, ch = 380, 330
    cols = 7
    rows_pl = (len(plat) + cols - 1) // cols
    rows_pr = (len(prop) + cols - 1) // cols
    H = 260 + rows_pl * (ch + 20) + 120 + rows_pr * (ch + 20) + 80
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 46), "Perronkit & props · ontwerpblad v1", 58)
    d.text((64, 156), "Perronmodules op het spoorraster (pivot = hart spoor), bovenkant 1,05 m, rand 1,65 m · "
                      "_Start/_End = begin/eind van een perron", font=font(25, False), fill=SUB)
    concept = fit(Image.open(os.path.join(REF, "platform-kit-concept.png")).convert("RGBA"), 340, 425)
    x_c = W - 60 - concept.width - 24
    d.rounded_rectangle([x_c, 230, W - 60, 230 + concept.height + 54], 14, fill=PARCH)
    sheet.alpha_composite(concept, (x_c + 12, 242))
    d.text((x_c + (concept.width + 24) // 2, 230 + concept.height + 20), "bron: conceptblad", font=font(19, False),
           fill=MUTED, anchor="ma")
    usable_cols = cols

    def grid(names, y0, title):
        label(d, (60, y0), title, 32)
        y0 += 80
        for i, n in enumerate(names):
            r, c = divmod(i, usable_cols)
            x = 60 + c * (cw + 14)
            y = y0 + r * (ch + 20)
            if x + cw > x_c - 20 and y < 230 + concept.height + 60:
                continue
            d.rounded_rectangle([x, y, x + cw, y + ch], 18, fill=PANEL)
            im = fit(crop_alpha(load("stn_" + n)), cw - 30, ch - 80)
            sheet.alpha_composite(im, (x + (cw - im.width) // 2, y + 14 + (ch - 80 - im.height) // 2))
            d.text((x + cw // 2, y + ch - 56), NAMES[n], font=font(23), fill=TXT, anchor="ma")
            d.text((x + cw // 2, y + ch - 26), f"{KIT[n]['tris']:,} tris".replace(",", "."), font=font(15, False),
                   fill=MUTED, anchor="ma")
        return y0 + ((len(names) + usable_cols - 1) // usable_cols) * (ch + 20)
    usable_cols = 6
    y = grid(plat, 230, "Perronmodules")
    usable_cols = 7
    grid(prop, y + 30, "Props")
    path = os.path.join(DOCS, "Station_PlatformKit.png")
    sheet.convert("RGB").save(path, optimize=True)
    return path


def in_scene():
    out = []
    for n, title in (("stn_scene_wide", "Stationnetje bij de trein (three.js-stijlrender)"),
                     ("stn_scene_close", "Op het perron")):
        if not os.path.exists(os.path.join(OUT, n + ".png")):
            continue
        im = load(n)
        bg = sky(im.size)
        bg.alpha_composite(im)
        d = ImageDraw.Draw(bg)
        label(d, (40, 34), title, 44)
        out.append(bg)
    W = max(i.width for i in out)
    sheet = Image.new("RGB", (W, sum(i.height for i in out)), BG)
    y = 0
    for i in out:
        sheet.paste(i.convert("RGB"), ((W - i.width) // 2, y))
        y += i.height
    path = os.path.join(DOCS, "Station_InScene.png")
    sheet.save(path, optimize=True)
    return path


def main():
    print("\n".join([building_sheet(), kit_sheet(), in_scene()]))


if __name__ == "__main__":
    main()
