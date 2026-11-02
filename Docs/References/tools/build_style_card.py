"""Bouwt de Conductor Chaos stijlkaart (SVG + PNG-preview) en palette.json
uit kleuren die rechtstreeks uit de conceptafbeelding zijn gemeten."""
import base64, io, json, math, os, random
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REF = os.path.join(HERE, "..")  # Docs/References
CONCEPT = Image.open(os.path.join(REF, "conductor-chaos-concept.png")).convert("RGB")
random.seed(7)

# ---------------------------------------------------------------- palette
# Elke ramp loopt van diepste schaduw -> hoogste licht (gemeten percentielen).
P = {
  "Personages": [
    ("skin", "Huid (warm, ook in schaduw)", ["#BA7756", "#D28658", "#DB9966", "#E7A773", "#EFB27C"], (590, 150, 690, 240)),
    ("uniform_navy", "Conducteur-uniform & pet", ["#1D1C2D", "#34335B", "#3E3D70", "#454377", "#565191"], (40, 790, 170, 990)),
    ("gold_trim", "Petband, knopen, embleem", ["#9D662F", "#AD7031", "#DA993A", "#FBBA39", "#FCCF53"], (600, 40, 780, 130)),
    ("helmet", "Bouwhelm monteur", ["#B57326", "#E59C2F", "#F5B038", "#FDBF40", "#FDC443"], (180, 820, 280, 880)),
    ("vest", "Veiligheidshesje", ["#B5531F", "#DA6B2C", "#E2722B", "#EA7931", "#B79282"], (175, 890, 265, 975)),
    ("beanie", "Muts passagier", ["#5A457E", "#7958AF", "#7A59B0", "#A370D2", "#B17BDD"], (300, 800, 400, 880)),
    ("scarf", "Sjaal passagier", ["#783638", "#9C4140", "#A44543", "#B24946", "#B64B48"], (290, 880, 410, 960)),
    ("cloth_dark", "Broek, schoenen, cream shirt", ["#393540", "#4E3F3F", "#BB9A89", "#D1B29E", "#E8D3C2"], (300, 920, 410, 1005)),
    ("face", "Oogwit, pupil, mond", ["#0D0B0C", "#662C23", "#B84A3C", "#E0C9C0", "#FBF0E3"], (650, 100, 790, 240)),
    ("mouth", "Mondholte & tong", ["#50221A", "#662C23", "#AB4337", "#B84A3C", "#C15648"], (660, 180, 730, 240)),
  ],
  "Trein": [
    ("train_red", "Treinrood (livrei)", ["#5E271F", "#8D3432", "#C54D3E", "#DA523F", "#FC7C69"], (420, 250, 800, 440)),
    ("train_trim", "Donkere trim, onderstel", ["#3B2524", "#482826", "#4B1F18", "#634330", "#7A5A48"], (420, 330, 560, 440)),
    ("wagon_brown", "Goederenwagon / tweede tint", ["#503330", "#995F53", "#A3685D", "#B27261", "#BB8472"], (820, 110, 1060, 330)),
    ("headlight", "Koplamp & warm licht", ["#F9DA7C", "#FDE080", "#FEECA1", "#FEF2B0", "#FFF8DA"], (440, 350, 540, 430)),
    ("metal", "Metaal, sleutel, wijzers", ["#6F696E", "#7B7582", "#8E8796", "#958E9C", "#B5AAAC"], (250, 870, 300, 950)),
  ],
  "Omgeving": [
    ("sky", "Lucht (verloop naar horizon)", ["#6E83BA", "#7D99D5", "#85A4EC", "#92BAF8", "#98C4FC"], (1040, 0, 1536, 120)),
    ("cloud_snow", "Wolken & sneeuw", ["#C2CAEA", "#C7D0F0", "#DFDDEE", "#E7E1EA", "#FDF2EB"], (1220, 0, 1360, 90)),
    ("mountain", "Bergrots (koel, blauw-violet)", ["#5D6EAC", "#6475B3", "#747FB3", "#8187B4", "#878EBC"], (1040, 20, 1300, 200)),
    ("pine", "Dennen (blauwgroen, nooit puur groen)", ["#2B3B37", "#395251", "#3D5655", "#466162", "#5B7A7A"], (1060, 80, 1260, 200)),
    ("grass", "Gras & mos (geelgroen)", ["#566D3A", "#6B7F38", "#8B9F3B", "#A1B044", "#ADBC4A"], (490, 470, 730, 600)),
    ("cliff", "Klifrots (warm)", ["#6A4C4B", "#906959", "#AC7F75", "#BE907B", "#CA957A"], (1320, 240, 1536, 440)),
    ("track_wood", "Bielzen & schraagwerk", ["#885642", "#9A6146", "#DB945C", "#EBA462", "#F4B06C"], (1010, 240, 1210, 440)),
    ("water", "Watervallen", ["#6B8CB6", "#83C2EB", "#8EC7EC", "#9ACCEC", "#ADD7ED"], (1230, 320, 1290, 440)),
    ("warning", "Waarschuwingsbord & hekken", ["#853A33", "#C28C2F", "#C6902F", "#F2B23A", "#FABF34"], (1360, 110, 1510, 270)),
  ],
  "Interieur & cargo": [
    ("interior_wood", "Wagoninterieur hout", ["#6C422A", "#814C32", "#8D543A", "#A56945", "#E59843"], (1150, 460, 1530, 730)),
    ("cab_interior", "Cabine / dashboard", ["#462A21", "#553022", "#593323", "#723F29", "#A66739"], (330, 460, 730, 730)),
    ("crate", "Kratten & koffers", ["#684029", "#C9813B", "#DE913F", "#F5AC4A", "#F9B44F"], (1330, 480, 1450, 570)),
    ("wool", "Schaap (lavendel schaduw)", ["#352F3C", "#BDABC1", "#DCC7C5", "#FDEEDC", "#FDF6E8"], (1360, 550, 1495, 660)),
    ("haze_purple", "Paarse rook / sfeer", ["#6B4269", "#684E6B", "#A7689C", "#B297B8", "#D2BCD0"], (1150, 520, 1330, 660)),
  ],
  "UI & logo": [
    ("ui_bg", "Achtergrond (pruim-grafiet)", ["#2A2730", "#302629", "#33303B", "#403E4B", "#4A4852"], (0, 780, 420, 1010)),
    ("parchment", "Perkament-labels", ["#9C8978", "#CAA791", "#F2D8BB", "#F4DCC0", "#FAECDC"], (10, 285, 390, 405)),
    ("ink", "Inkt / tekst (warm, nooit zwart)", ["#1C0F0E", "#291D1B", "#351B13", "#382520", "#5A4038"], (70, 80, 410, 150)),
    ("title_red", "Logo-rood 'Chaos'", ["#9E3A22", "#B8432A", "#CD4C2C", "#E06A48", "#E79E80"], (90, 140, 390, 240)),
    ("accent_yellow", "Accentgeel (actiestreepjes)", ["#C68C2C", "#F6BC33", "#FABF34", "#FBC838", "#FDDB78"], (120, 760, 200, 840)),
  ],
}

def thumb(box, w=150, h=96):
    im = CONCEPT.crop(box)
    sc = max(w / im.width, h / im.height)
    im = im.resize((max(w, round(im.width * sc)), max(h, round(im.height * sc))), Image.LANCZOS)
    l, t = (im.width - w) // 2, (im.height - h) // 2
    im = im.crop((l, t, l + w, t + h))
    buf = io.BytesIO(); im.save(buf, "JPEG", quality=82)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()

def lum(hexc):
    r, g, b = (int(hexc[i:i+2], 16) / 255 for i in (1, 3, 5))
    return .3 * r + .59 * g + .11 * b

from xml.sax.saxutils import escape as esc

def rough_rect(x, y, w, h, j=3.0, fill="#F4DCC0"):
    """Perkament-label met licht gescheurde randen zoals in het concept."""
    pts = []
    n = max(6, int(w / 40))
    for i in range(n + 1): pts.append((x + w * i / n, y + random.uniform(-j, j)))
    m = max(3, int(h / 40))
    for i in range(1, m + 1): pts.append((x + w + random.uniform(-j, j), y + h * i / m))
    for i in range(n - 1, -1, -1): pts.append((x + w * i / n, y + h + random.uniform(-j, j)))
    for i in range(m - 1, 0, -1): pts.append((x + random.uniform(-j, j), y + h * i / m))
    d = "M" + " L".join(f"{a:.1f},{b:.1f}" for a, b in pts) + " Z"
    return f'<path d="{d}" fill="{fill}"/>'

FONT = "'Baloo 2','Fredoka','Nunito','Trebuchet MS','Verdana',sans-serif"
W = 1800
out = []
def add(s): out.append(s)

def label(x, y, w, h, text, size=30, anchor="start", fill="#F4DCC0", ink="#351B13", rot=0):
    g = f'<g transform="rotate({rot} {x + w/2} {y + h/2})">' + rough_rect(x, y, w, h, fill=fill)
    tx = x + 18 if anchor == "start" else x + w / 2
    g += f'<text x="{tx}" y="{y + h/2 + size*0.36}" font-family="{FONT}" font-weight="700" font-size="{size}" fill="{ink}" text-anchor="{anchor}">{esc(text)}</text></g>'
    add(g)

def text(x, y, s, size=20, fill="#F4DCC0", weight=400, anchor="start", italic=False):
    st = ' font-style="italic"' if italic else ""
    add(f'<text x="{x}" y="{y}" font-family="{FONT}" font-size="{size}" font-weight="{weight}" fill="{fill}" text-anchor="{anchor}"{st}>{esc(s)}</text>')

# ------------------------------------------------------------------ header
y = 40
label(40, y, 760, 92, "Conductor Chaos · Stijlkaart v2", size=46, rot=-1.2)
text(44, y + 140, "Alle kleuren zijn gemeten in de conceptafbeelding (schaduw → licht). Gebruik de ramps, niet losse tinten.", 22, "#E7D3BE")
text(44, y + 172, "Referentie: Docs/References/conductor-chaos-concept.png  ·  machine-leesbaar: Docs/References/palette.json", 18, "#A29087")

# concept thumbnail rechtsboven
ref = CONCEPT.resize((420, 280), Image.LANCZOS); buf = io.BytesIO(); ref.save(buf, "JPEG", quality=85)
add(rough_rect(1330, 30, 440, 300, j=4, fill="#F4DCC0"))
add(f'<image x="1340" y="40" width="420" height="280" href="data:image/jpeg;base64,{base64.b64encode(buf.getvalue()).decode()}"/>')
text(1550, 352, "bron: conceptafbeelding", 16, "#A29087", anchor="middle", italic=True)

# ------------------------------------------------------- 5 kernprincipes
y = 260
principles = [
  ("Warm licht, koele schaduw", "Key-light perzik/goud, schaduwen schuiven naar blauw-violet (#5D6EAC / #3E3D70). Nooit grijs of zwart."),
  ("Zacht, rond, chunky", "Afgeronde bevels op álles: cabine, planken, rotsen. Geen scherpe 90°-randen, geen dunne onderdelen."),
  ("Gemiddelde saturatie", "Kleurrijk maar gedempt (hoofdtonen 45–70% saturatie). Fel alleen voor licht, gevaar en interactie."),
  ("Geschilderde verlopen", "Grote vlakken hebben een zacht verloop + subtiele penseelstructuur, geen fotorealistische textures."),
  ("Leesbaarheid eerst", "Personages en interactieve objecten zijn warmer/feller dan hun achtergrond, omgeving wordt koeler naar achteren."),
]
pw = (W - 80 - 4 * 16) / 5
for i, (t, d) in enumerate(principles):
    x = 40 + i * (pw + 16)
    add(f'<rect x="{x}" y="{y + 100}" width="{pw}" height="150" rx="14" fill="#403E4B"/>')
    add(f'<circle cx="{x + 30}" cy="{y + 132}" r="15" fill="#FABF34"/>')
    text(x + 30, y + 139, str(i + 1), 20, "#351B13", 700, "middle")
    text(x + 54, y + 140, t, 20, "#F4DCC0", 700)
    # word wrap
    words, line, ly = d.split(), "", y + 174
    for w_ in words:
        if len(line + " " + w_) > 36:
            text(x + 18, ly, line.strip(), 16, "#DAC3B2"); ly += 22; line = ""
        line += " " + w_
    text(x + 18, ly, line.strip(), 16, "#DAC3B2")

# --------------------------------------------------- material bollen
y = 540
label(40, y, 410, 56, "Zo vallen licht en schaduw", size=27, rot=0.8)
text(470, y + 38, "Elke bol = één materiaal uit het concept: warm licht van linksboven, koele violette omgevingsschaduw, zachte rimlight.", 18, "#DAC3B2")
defs = ['<defs>',
        '<filter id="soft" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="6"/></filter>']
spheres = ["skin", "uniform_navy", "train_red", "helmet", "beanie", "track_wood", "mountain", "pine", "wool", "parchment"]
allramps = {k: r for g in P.values() for (k, n, r, b) in g}
names = {k: n for g in P.values() for (k, n, r, b) in g}
sw = (W - 80) / len(spheres)
for i, k in enumerate(spheres):
    r = allramps[k]
    rr = sorted(r, key=lum)  # donker -> licht
    defs.append(f'<radialGradient id="g_{k}" cx="0.36" cy="0.32" r="0.78">'
                f'<stop offset="0" stop-color="{rr[4]}"/><stop offset="0.28" stop-color="{rr[3]}"/>'
                f'<stop offset="0.6" stop-color="{rr[2]}"/><stop offset="0.86" stop-color="{rr[1]}"/>'
                f'<stop offset="1" stop-color="{rr[0]}"/></radialGradient>')
    cx, cy, R = 40 + sw * i + sw / 2, y + 170, 62
    add(f'<ellipse cx="{cx + 6}" cy="{cy + R + 10}" rx="{R * 0.85}" ry="12" fill="#1F1C24" opacity="0.6" filter="url(#soft)"/>')
    add(f'<circle cx="{cx}" cy="{cy}" r="{R}" fill="url(#g_{k})"/>')
    # rim light rechtsonder (koel)
    add(f'<path d="M {cx + R*0.55} {cy + R*0.82} A {R} {R} 0 0 0 {cx + R*0.95} {cy + R*0.3}" stroke="#B2C5F3" stroke-width="4" fill="none" opacity="0.45" stroke-linecap="round"/>')
    text(cx, cy + R + 44, names[k].split(" (")[0].split(",")[0], 16, "#F4DCC0", 700, "middle")

# --------------------------------------------------------------- ramps
y = 840
rowh = 118
for gi, (group, items) in enumerate(P.items()):
    label(40, y, 30 + len(group) * 17, 54, group, size=28, rot=-0.8 if gi % 2 else 0.9)
    y += 74
    cols = 2
    colw = (W - 80 - 30) / cols
    for i, (key, name, ramp, box) in enumerate(items):
        cx = 40 + (i % cols) * (colw + 30)
        cy = y + (i // cols) * rowh
        add(f'<rect x="{cx}" y="{cy}" width="{colw}" height="{rowh - 14}" rx="14" fill="#3B3843"/>')
        add(f'<clipPath id="c_{key}"><rect x="{cx + 8}" y="{cy + 8}" width="150" height="{rowh - 30}" rx="10"/></clipPath>')
        add(f'<image x="{cx + 8}" y="{cy + 8}" width="150" height="{rowh - 30}" preserveAspectRatio="xMidYMid slice" clip-path="url(#c_{key})" href="{thumb(box, 150, rowh - 30)}"/>')
        text(cx + 176, cy + 32, name, 20, "#F4DCC0", 700)
        text(cx + colw - 14, cy + 32, key, 14, "#857771", 400, "end")
        swx, sww = cx + 176, (colw - 176 - 14) / 5
        for j, hx in enumerate(ramp):
            add(f'<rect x="{swx + j * sww}" y="{cy + 44}" width="{sww - 6}" height="34" rx="7" fill="{hx}"/>')
            tc = "#351B13" if lum(hx) > .55 else "#F4DCC0"
            text(swx + j * sww + (sww - 6) / 2, cy + 66, hx, 13, tc, 700, "middle")
        add(f'<text x="{swx}" y="{cy + 96}" font-family="{FONT}" font-size="12" fill="#857771">schaduw</text>')
        add(f'<text x="{swx + 5 * sww - 6}" y="{cy + 96}" font-family="{FONT}" font-size="12" fill="#857771" text-anchor="end">licht</text>')
    y += math.ceil(len(items) / cols) * rowh + 26

# ------------------------------------------------- vormentaal personages
label(40, y, 400, 56, "Vormentaal personages", size=28, rot=1.0)
y += 80
bx = 40
add(f'<rect x="{bx}" y="{y}" width="{W - 80}" height="360" rx="16" fill="#3B3843"/>')
# proportie-figuur
fx, fy = bx + 150, y + 40
defs.append('<linearGradient id="bodyg" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#D28658"/><stop offset="0.55" stop-color="#E7A773"/><stop offset="1" stop-color="#BA7756"/></linearGradient>')
defs.append('<linearGradient id="unig" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#34335B"/><stop offset="0.5" stop-color="#454377"/><stop offset="1" stop-color="#1D1C2D"/></linearGradient>')
add(f'<rect x="{fx}" y="{fy + 40}" width="120" height="230" rx="60" fill="url(#bodyg)"/>')
add(f'<rect x="{fx}" y="{fy + 150}" width="120" height="120" rx="30" fill="url(#unig)"/>')
add(f'<rect x="{fx + 4}" y="{fy + 180}" width="112" height="90" rx="40" fill="url(#unig)"/>')
add(f'<path d="M {fx - 14} {fy + 44} Q {fx + 60} {fy - 10} {fx + 134} {fy + 44} L {fx + 120} {fy + 58} L {fx} {fy + 58} Z" fill="#3E3D70"/>')
add(f'<rect x="{fx - 2}" y="{fy + 52}" width="124" height="12" rx="5" fill="#DA993A"/>')
add(f'<ellipse cx="{fx + 60}" cy="{fy + 66}" rx="78" ry="12" fill="#1D1C2D"/>')
add(f'<circle cx="{fx + 60}" cy="{fy + 22}" r="9" fill="#FBBA39"/>')
for ex in (fx + 38, fx + 84):
    add(f'<circle cx="{ex}" cy="{fy + 104}" r="22" fill="#FBF0E3"/><circle cx="{ex + 5}" cy="{fy + 106}" r="8" fill="#0D0B0C"/><circle cx="{ex + 8}" cy="{fy + 102}" r="2.5" fill="#FBF0E3"/>')
add(f'<ellipse cx="{fx + 62}" cy="{fy + 140}" rx="12" ry="9" fill="#B84A3C"/>')
for bx_ in (fx + 60,):
    for by in (fy + 190, fy + 215):
        add(f'<circle cx="{bx_}" cy="{by}" r="4" fill="#DA993A"/>')
add(f'<circle cx="{fx - 18}" cy="{fy + 205}" r="20" fill="#E7A773"/><circle cx="{fx + 138}" cy="{fy + 205}" r="20" fill="#E7A773"/>')
add(f'<rect x="{fx + 14}" y="{fy + 262}" width="40" height="22" rx="10" fill="#393540"/><rect x="{fx + 66}" y="{fy + 262}" width="40" height="22" rx="10" fill="#393540"/>')
# maatlijnen
mx = fx + 190
add(f'<line x1="{mx}" y1="{fy + 40}" x2="{mx}" y2="{fy + 284}" stroke="#FABF34" stroke-width="2" stroke-dasharray="6 5"/>')
add(f'<line x1="{mx - 8}" y1="{fy + 150}" x2="{mx + 8}" y2="{fy + 150}" stroke="#FABF34" stroke-width="2"/>')
text(mx + 14, fy + 100, "hoofd ≈ 45%", 16, "#FABF34", 700)
text(mx + 14, fy + 220, "lijf + benen ≈ 55%", 16, "#FABF34", 700)
text(fx + 60, fy + 312, "~2 koppen hoog · geen nek", 15, "#DAC3B2", 400, "middle")

rules_do = [
  "Boonvormig capsule-lijf: hoofd en romp zijn één vorm",
  "Grote ronde ogen die elkaar raken (elk ≈ 37% van de breedte)",
  "Pupillen klein en puntvormig, één wit glimlicht",
  "Stompe armen zonder ellebogen, ronde mitten-handen",
  "Korte blokvormige benen, brede schoenen",
  "Kleding = grote simpele kleurvlakken + 1 accent (band, knopen)",
  "Rol herkenbaar aan hoofddeksel: pet, helm, muts",
]
rules_dont = [
  "Geen neus, oren, wenkbrauwen of tanden",
  "Geen zwarte contourlijnen in 3D",
  "Geen realistische huid- of stoftextures",
  "Geen dunne vingers of fijne details",
  "Geen pure #000 of #FFF (ook pupillen zijn #0D0B0C)",
]
tx = bx + 560
text(tx, y + 50, "Wel", 24, "#ADBC4A", 700)
for i, r in enumerate(rules_do):
    text(tx, y + 84 + i * 34, "✓  " + r, 18, "#F4DCC0")
tx2 = bx + 1240
text(tx2, y + 50, "Niet", 24, "#E06A48", 700)
for i, r in enumerate(rules_dont):
    text(tx2, y + 84 + i * 34, "✕  " + r, 18, "#F4DCC0")
y += 390

# ---------------------------------------------- vormentaal wereld + trein
label(40, y, 470, 56, "Wereld, trein & interieur", size=28, rot=-0.9)
y += 80
add(f'<rect x="40" y="{y}" width="{W - 80}" height="300" rx="16" fill="#3B3843"/>')
world = [
  ("Trein", ["Moderne regionale reizigerstrein, maar chunky en speelgoedachtig",
             "Treinrood als hoofdkleur, donkere trim, grote ronde koplampen",
             "Ramen groot en rond-rechthoekig, dikke afgeronde randen",
             "Licht versleten: zachte vlekken, geen roest-realisme"]),
  ("Landschap", ["Low-poly rotsen met zachte facetten + geschilderd verloop",
                 "Dennen = gestapelde kegels, blauwgroen, iets schuin",
                 "Atmosferisch: verder weg = blauwer en lichter (→ #91A8E6)",
                 "Overdreven verticaal: kloven, watervallen, smalle bruggen"]),
  ("Interieur & props", ["Warm hout + paarse/roze sfeerschaduwen",
                         "Props groot en leesbaar: kratten, koffers, schaap",
                         "Interactieve objecten warmer en lichter dan de wand",
                         "Gevaar = geel/rood (#FABF34 · #CD4C2C), spaarzaam"]),
]
cw = (W - 80) / 3
for i, (t, lines) in enumerate(world):
    x = 40 + i * cw + 30
    text(x, y + 50, t, 24, "#FABF34", 700)
    for j, l in enumerate(lines):
        text(x, y + 92 + j * 46, "•  " + l, 17, "#F4DCC0")
y += 330

# ------------------------------------------------ unity / URP vertaling
label(40, y, 470, 56, "Vertaling naar Unity (URP)", size=28, rot=0.7)
y += 80
add(f'<rect x="40" y="{y}" width="{W - 80}" height="330" rx="16" fill="#3B3843"/>')
urp = [
  ("Directional light", "#FFE3BC", "kleur warm perzik, intensiteit ~1.2, hoek ≈ 40° van linksboven"),
  ("Ambient (gradient) – lucht", "#85A4EC", "sky-kleur; equator #C7D0F0; ground #6A4C4B"),
  ("Schaduwkleur / toon-ramp", "#5D5A8C", "schaduw nooit grijs: tint richting blauw-violet; huid houdt warme schaduw #BA7756"),
  ("Mist / atmosfeer", "#91A8E6", "lineaire fog, alleen voor verre bergen; start ver weg"),
  ("Rim light (koel)", "#B2C5F3", "smalle fresnel-rand op personages en trein, zwak (≈ 0.3)"),
  ("Koplamp / lampen", "#FEF2B0", "emissive + lichte bloom (threshold ~1.0, intensiteit laag)"),
]
for i, (n, c, d) in enumerate(urp):
    yy = y + 40 + i * 47
    add(f'<rect x="80" y="{yy - 22}" width="60" height="32" rx="7" fill="{c}"/>')
    text(160, yy, n, 19, "#F4DCC0", 700)
    text(520, yy, f"{c}  ·  {d}", 17, "#DAC3B2")
y += 360

text(W / 2, y + 10, "Bijwerken? Pas de ramps aan in Docs/References/tools/build_style_card.py en draai het opnieuw — SVG en palette.json blijven dan gelijk.", 16, "#857771", 400, "middle", True)
H = y + 50

defs.append('</defs>')
svg = (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{W}" height="{H}" viewBox="0 0 {W} {H}">'
       f'<rect width="{W}" height="{H}" fill="#33303B"/>' + "".join(defs) + "".join(out) + "</svg>")
open(os.path.join(REF, "STYLE_PALETTE.svg"), "w", encoding="utf-8").write(svg)

# ------------------------------------------------------------- JSON
pj = {
  "name": "Conductor Chaos",
  "version": 2,
  "source": "Docs/References/conductor-chaos-concept.png",
  "note": "Gemeten met k-means/percentielen per materiaalgebied in de conceptafbeelding; enkele uiterste tinten (diepste schaduw of hoogste licht) zijn handmatig doorgetrokken waar het concept te weinig pixels had.",
  "ramp_order": "index 0 = diepste schaduw ... index 4 = hoogste licht (index 4 bij 'vest' en 'cloth_dark' is een tweede materiaal, zie name)",
  "groups": {g: {k: {"name": n, "ramp": r} for (k, n, r, b) in items} for g, items in P.items()},
  "lighting": {n: c for (n, c, d) in urp},
}
json.dump(pj, open(os.path.join(REF, "palette.json"), "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print("ok", W, H, len(svg) // 1024, "KB")
