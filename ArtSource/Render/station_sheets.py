"""Ontwerpbladen voor het stationnetje, het hoofdstation en de perronkit (issues #182, #207) -> Docs/Art/.

    python ArtSource/Render/station_sheets.py      (na het renderen van Render/scenes/station_sheet.json)

* Station_DesignSheet.png   gebouw van alle kanten, doorkijk zonder dak, deur, concept, kleurramps, kenmerken
* Station_PlatformKit.png   perronmodules + props als thumbnails, met concept
* Station_InScene.png       het station bij de trein, met personages
* StationMain_DesignSheet.png   hoofdstation van buiten, doorkijk, concept, kleurramps, kenmerken
* StationMain_BriefingRoom.png  de briefingruimte van binnen, plattegrond met gameplay-ankers
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
    "Prop_Lockers": "Kluisjes", "Prop_RouteBoard": "Routebord", "Prop_NoticeBoard": "Prikbord",
    "Prop_CoffeeCounter": "Koffiebar", "Prop_CoatStand": "Kapstok", "Prop_TicketBooth": "Kaartjeshuisje",
    "Prop_Banner": "Banier", "Prop_PendantLamp": "Hanglamp",
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


def _panel(sheet, d, name, cap, x0, y0, w, h, interior=False):
    d.rounded_rectangle([x0, y0, x0 + w, y0 + h], 22, fill=PANEL)
    if interior:                                   # binnenaanzicht: vullen, met lucht achter deur en ramen
        im = load(name)
        bg = sky(im.size)
        bg.alpha_composite(im)
        im = fit(bg, w - 40, h - 84)
    else:
        im = fit(crop_alpha(load(name)), w - 40, h - 84)
    sheet.alpha_composite(im, (x0 + (w - im.width) // 2, y0 + 64 + (h - 84 - im.height) // 2))
    label(d, (x0 + 20, y0 + 14), cap, 24)


def main_sheet():
    W, H = 3200, 2150
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 46), "Hoofdstation · ontwerpblad v1", 58)
    d.text((64, 156), "Kopstation met klokgevel, twee vleugels met dakkapellen en een perronluifel · de hal is de briefingruimte "
                      "(9,1 × 8,5 m), vloer op perronhoogte (1,05 m)", font=font(25, False), fill=SUB)
    _panel(sheet, d, "stn_main_front", "Voorkant (naar het spoor)", 60, 210, 1900, 1100)
    _panel(sheet, d, "stn_main_entrance", "Ingang: klok, bord, dubbele deur", 2000, 210, 1140, 540)
    _panel(sheet, d, "stn_main_back", "Achterkant", 2000, 770, 1140, 540)
    by = 1350
    _panel(sheet, d, "stn_main_canopy", "Perronluifel (links)", 60, by, 860, 750)
    concept_box(sheet, d, os.path.join(REF, "main-station-concept.png"), 950, by, 520, 640)
    d.rounded_rectangle([1530, by, 2420, H - 50], 22, fill=PANEL)
    label(d, (1554, by + 18), "Kenmerken", 30)
    traits = [
        "Klokgevel met waaierraam en klok, MAIN STATION-bord, gele nokbol",
        "Rondboog-dubbeldeur: knopen Door_L / Door_R met scharnier en botsvorm",
        "Vleugels met dakkapellen en schoorsteen (dicht, niet begaanbaar)",
        "Rode pannendaken (eigen ramp), vakwerk met korbelen, stenen plint",
        "Perronluifel met banken, bagagekar, lantaarn en hangbord",
        "Banieren, wandlantaarns en bloempotten bij de ingang",
    ]
    for k, t in enumerate(traits):
        d.text((1560, by + 88 + k * 40), "✓  " + t, font=font(20, False), fill=TXT)
    a = KIT["Station_Main"]
    tech = [f"{a['tris']:,} driehoeken (incl. interieur) · 0 textures · M_Stn_*".replace(",", "."),
            "Pivot: midden voorgevel op maaiveld, voorkant +Z (net als Station_Building)",
            "Unity: Col_* → MeshColliders, Light_* → warme puntlichten"]
    for k, t in enumerate(tech):
        d.text((1560, by + 88 + (len(traits) + 0.5 + k) * 40), "•  " + t, font=font(18, False), fill=SUB)
    d.rounded_rectangle([2460, by, W - 50, H - 50], 22, fill=PANEL)
    ramps_panel(d, 2460, by, 690, [("Pannendak", "roof_terracotta"), ("Vakwerk & deuren", "station_timber"),
                                   ("Pleisterwerk", "parchment"), ("Deur, borden", "station_navy"),
                                   ("Plint", "plinth_stone"), ("Krijtbord", "chalk_green")])
    path = os.path.join(DOCS, "StationMain_DesignSheet.png")
    sheet.convert("RGB").save(path, optimize=True)
    return path


def briefing_sheet():
    W, H = 3200, 2200
    sheet = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(sheet)
    label(d, (60, 46), "Briefingruimte · Training Hub", 58)
    d.text((64, 156), "De hal van het hoofdstation als speelbare ruimte: routebord, prikbord, vertrekbord, koffiebar, kluisjes, "
                      "kapstok, 4 banken (8 zitplaatsen), hanglampen", font=font(25, False), fill=SUB)
    _panel(sheet, d, "stn_main_brief_back", "Achterwand: routebord + TRAINING HUB", 60, 210, 1560, 1000, interior=True)
    _panel(sheet, d, "stn_main_brief_door", "Naar de deur", 1660, 210, 1480, 1000, interior=True)
    _panel(sheet, d, "stn_main_brief_coffee", "Koffiebar (links)", 60, 1250, 1060, 900, interior=True)
    _panel(sheet, d, "stn_main_brief_lockers", "Kluisjes + kapstok (rechts)", 1150, 1250, 1060, 900, interior=True)
    x0, y0, w = 2240, 1250, W - 50 - 2240
    d.rounded_rectangle([x0, y0, x0 + w, H - 50], 22, fill=PANEL)
    label(d, (x0 + 20, y0 + 14), "Plattegrond (dak weg)", 24)
    im = fit(crop_alpha(load("stn_main_top")), w - 40, 520)
    sheet.alpha_composite(im, (x0 + (w - im.width) // 2, y0 + 70))
    anchors = ["Spawn_Briefing — startplek midden in de hal",
               "Briefing_Board — routebord (camera/UI-focus)",
               "Briefing_Seat_1..8 — zitplaatsen op de banken",
               "Spot_Coffee / Spot_Lockers — interactieplekken",
               "Light_Pendant_1..6 — hanglampen (helder)"]
    for k, t in enumerate(anchors):
        d.text((x0 + 24, y0 + 610 + k * 34), "•  " + t, font=font(18, False), fill=TXT)
    path = os.path.join(DOCS, "StationMain_BriefingRoom.png")
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
    print("\n".join([building_sheet(), main_sheet(), briefing_sheet(), kit_sheet(), in_scene()]))


if __name__ == "__main__":
    main()
