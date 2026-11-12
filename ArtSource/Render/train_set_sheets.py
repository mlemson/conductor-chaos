"""Ontwerpbladen van de treinsets uit train_kit (Docs/Art/TrainSet_<Naam>_DesignSheet.png).

    python ArtSource/Render/train_set_scenes.py
    python ArtSource/Render/render_scenes.py ArtSource/Render/scenes/<Set>_sheet.json ArtSource/Render/out
    python ArtSource/Render/train_set_sheets.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "out")
BG, PANEL, PARCH, INK, YELLOW, SKY = "#33303B", "#403E4B", "#F4DCC0", "#351B13", "#FABF34", (133, 164, 236)

SHEETS = {
    "DieselSet": dict(
        title="Treinset · Diesel jaren '60", file="TrainSet_Diesel60_DesignSheet.png",
        sub="DieselLocomotive_Blockout + DieselCoach_Blockout · upgrade na de stoomtrein",
        access=["Gele ladder in de machinekamer naar dakluik RoofHatch_Engine",
                "Gele ladder op de perronkant van het rijtuig, over de dakschouder",
                "Gewelfd dak met vlak loopdek op 2,50 m, zonder hekjes"],
        repair=["Fan · dakventilator loc", "EngineHatch · motorluik (perronkant)", "AxleBox · warmgelopen aspot",
                "BrakeHose · remslang tussen loc en rijtuig", "Vent · dakventilator rijtuig"]),
    "ModernSet": dict(
        title="Treinset · Modern Express", file="TrainSet_ModernExpress_DesignSheet.png",
        sub="ModernCabCar_Blockout + ModernCoach_Blockout · Train Upgrade 2",
        access=["Gele ladder achter de cabine naar dakluik RoofHatch_Cab",
                "Gele ladder op de perronkant van het tussenrijtuig",
                "Gewelfd dak met vlak loopdek op 2,50 m, zonder hekjes"],
        repair=["Pantograph · stroomafnemer", "Converter · omvormerluik in de gele band", "AxleBox · warmgelopen aspot",
                "BrakeHose · remslang tussen de rijtuigen", "Aircon · dakventilator tussenrijtuig"]),
    "FuturisticSet": dict(
        title="Treinset · Futuristic Express", file="TrainSet_FuturisticExpress_DesignSheet.png",
        sub="FuturisticCabCar_Blockout + FuturisticCoach_Blockout · Train Upgrade 3",
        access=["Gele ladder achter de cabine naar dakluik RoofHatch_Cab",
                "Gele ladder op de perronkant van het tussenrijtuig",
                "Gewelfd dak met vlak loopdek op 2,50 m, zonder hekjes"],
        repair=["Pantograph · stroomafnemer", "PowerCell · lichtgevende energiecel", "AxleBox · warmgelopen aspot",
                "BrakeHose · remslang tussen de rijtuigen", "Cooler · dakventilator tussenrijtuig"]),
}


def font(size, bold=True):
    for d in ("/usr/share/fonts/truetype/dejavu", "C:/Windows/Fonts"):
        p = os.path.join(d, "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf")
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def shot(name, size, sky=True):
    im = Image.open(os.path.join(OUT, name + ".png")).convert("RGBA")
    bg = Image.new("RGBA", im.size, SKY + (255,) if sky else (64, 62, 75, 255))
    bg.alpha_composite(im)
    w, h = size
    s = max(w / bg.width, h / bg.height)
    bg = bg.resize((round(bg.width * s), round(bg.height * s)), Image.LANCZOS)
    x, y = (bg.width - w) // 2, (bg.height - h) // 2
    return bg.crop((x, y, x + w, y + h)).convert("RGB")


def pill(d, x, y, text, f, pad=(22, 12)):
    w = d.textlength(text, font=f)
    h = f.size
    d.rounded_rectangle((x, y, x + w + 2 * pad[0], y + h + 2 * pad[1]), 16, fill=PARCH)
    d.text((x + pad[0], y + pad[1] - 2), text, font=f, fill=INK)
    return y + h + 2 * pad[1]


def paste(sheet, d, im, x, y, label):
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, im.width, im.height), 22, fill=255)
    sheet.paste(im, (x, y), mask)
    f = font(26)
    w = d.textlength(label, font=f)
    d.rounded_rectangle((x + 16, y + 16, x + 16 + w + 28, y + 16 + 44), 12, fill=PARCH)
    d.text((x + 30, y + 23), label, font=f, fill=INK)


def build(key, S):
    W, H = 3000, 2060
    sheet = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(sheet)
    pill(d, 60, 44, S["title"], font(58))
    d.text((64, 150), S["sub"], font=font(28, False), fill=PARCH)
    paste(sheet, d, shot(key + "_hero", (1900, 960)), 60, 210, "Hero")
    paste(sheet, d, shot(key + "_front", (960, 620)), 1980, 210, "Voorkant")
    # tekstpaneel
    x0, y0 = 1980, 850
    d.rounded_rectangle((x0, y0, x0 + 960, y0 + 320 + 40 * len(S["repair"])), 22, fill=PANEL)
    y = pill(d, x0 + 24, y0 + 22, "Dakopgang", font(30), (16, 8)) + 14
    for t in S["access"]:
        d.ellipse((x0 + 32, y + 12, x0 + 44, y + 24), fill=YELLOW)
        d.text((x0 + 60, y), t, font=font(25, False), fill=PARCH)
        y += 38
    y = pill(d, x0 + 24, y + 16, "Onderhoudspunten (RepairPoint_…)", font(30), (16, 8)) + 14
    for k, t in enumerate(S["repair"]):
        d.ellipse((x0 + 26, y + 2, x0 + 58, y + 34), fill=YELLOW)
        d.text((x0 + 36, y + 4), str(k + 1), font=font(22), fill=INK)
        d.text((x0 + 72, y + 2), t, font=font(25, False), fill=PARCH)
        y += 40
    row = 1200
    paste(sheet, d, shot(key + "_side", (1900, 400), sky=False), 60, row, "Zijaanzicht (perronkant)")
    paste(sheet, d, shot(key + "_top", (1900, 380), sky=False), 60, row + 430, "Bovenaanzicht · loopdek")
    paste(sheet, d, shot(key + "_hatch", (470, 400), sky=False), 1980, row + 215, "Dakluik + ladder")
    paste(sheet, d, shot(key + "_ladder", (470, 400), sky=False), 2470, row + 215, "Dakladder rijtuig")
    paste(sheet, d, shot(key + "_broken", (960, 395)), 1980, row + 635, "Alles kapot (Broken_Bits)")
    paste(sheet, d, shot(key + "_roof", (930, 190)), 60, row + 840, "Dak, 3/4 achter")
    paste(sheet, d, shot(key + "_far", (950, 190), sky=False), 1010, row + 840, "Overkant")
    sheet = sheet.crop((0, 0, W, row + 1050))
    out = os.path.join(ROOT, "Docs", "Art", S["file"])
    sheet.save(out, optimize=True)
    print("sheet", out, sheet.size)


def main():
    for key, S in SHEETS.items():
        build(key, S)


if __name__ == "__main__":
    main()
