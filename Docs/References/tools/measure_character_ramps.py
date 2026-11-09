"""Meet kleurramps (schaduw -> licht) voor de nieuwe personages uit hun conceptbladen.

Per ramp: een uitsnede (box) in een referentiebeeld en een 'zaad'-tint. Alleen pixels waarvan de
kleurtoon (a*, b* in CIELAB) dicht bij het zaad ligt tellen mee, zodat schaduw en licht van hetzelfde
materiaal worden gemeten en randen, ogen of achtergrond niet. De ramp = mediaankleur rond de
5/25/50/75/95e percentiel van de lichtheid, dezelfde methode als de stijlkaart.

    python Docs/References/tools/measure_character_ramps.py          # druk ramps af

De uitkomst staat (met box en bron) in build_style_card.py, zodat palette.json één bron houdt.
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REF = os.path.join(HERE, "..")

# key: (beeld, box (x0, y0, x1, y1), zaadtint, tolerantie in a*b*)
SPECS = {
    # Oudere passagier (Docs/References/Characters/elderly-passenger-concept.png)
    "coat_plum": ("Characters/elderly-passenger-concept.png", (300, 640, 700, 900), "#7D4758", 16),
    "beret_plum": ("Characters/elderly-passenger-concept.png", (230, 150, 620, 330), "#7E3F7C", 14),
    "hair_grey": ("Characters/elderly-passenger-concept.png", (200, 250, 760, 560), "#C9C4CC", 7),
    "scarf_cream": ("Characters/elderly-passenger-concept.png", (290, 590, 700, 760), "#F1DDB5", 10),
    # Toerist (Docs/References/Characters/tourist-concept.png)
    "safari_hat": ("Characters/tourist-concept.png", (200, 190, 830, 420), "#E5C38E", 10),
    "shirt_coral": ("Characters/tourist-concept.png", (220, 690, 820, 1000), "#D9503F", 12),
    "shorts_olive": ("Characters/tourist-concept.png", (330, 990, 790, 1095), "#776939", 9),
    "backpack_teal": ("Characters/tourist-concept.png", (110, 690, 330, 835), "#25485C", 9),
    # Spoormonteur (Docs/References/Characters/rail-mechanic-concept.png); overall = gemeten #353859..#42426A = uniform_navy
    "leather": ("Characters/rail-mechanic-concept.png", (380, 520, 770, 730), "#7A4A2C", 12),
    "boot_brown": ("Characters/rail-mechanic-concept.png", (250, 560, 360, 680), "#4A2E28", 12),
    # Stationschef, zakenreiziger, kind, bagagedrager (issue #204)
    "cap_maroon": ("Characters/station-master-concept.png", (320, 30, 540, 110), "#6D2432", 12),
    "master_navy": ("Characters/station-master-concept.png", (270, 360, 340, 470), "#382B49", 10),
    "mustache_cream": ("Characters/station-master-concept.png", (380, 230, 460, 270), "#E3C7B7", 6),
    "hair_brown": ("Characters/business-traveler-concept.png", (320, 110, 560, 190), "#603A38", 10),
    "hoodie_red": ("Characters/child-passenger-concept.png", (380, 460, 520, 560), "#AD3E3F", 12),
    "luggage_blue": ("Characters/baggage-helper-concept.png", (780, 385, 1000, 440), "#4F5B7D", 10),
    "luggage_red": ("Characters/baggage-helper-concept.png", (790, 300, 1000, 350), "#A0434D", 12),
    "dino_green": ("Characters/child-passenger-concept.png", (950, 1150, 1090, 1290), "#6FA83A", 16),
}


def srgb_to_lab(rgb):
    c = rgb / 255.0
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ M.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float)


def measure(img_rel, box, seed, tol):
    im = np.asarray(Image.open(os.path.join(REF, img_rel)).convert("RGB").crop(box), float).reshape(-1, 3)
    lab = srgb_to_lab(im)
    s = srgb_to_lab(hex_rgb(seed)[None])[0]
    d = np.hypot(lab[:, 1] - s[1], lab[:, 2] - s[2])
    # chroma-tolerantie groeit iets met de lichtheidsafstand (schaduwen zijn minder verzadigd)
    sel = d < tol + 0.12 * np.abs(lab[:, 0] - s[0])
    sel &= np.abs(lab[:, 0] - s[0]) < 38
    px, L = im[sel], lab[sel, 0]
    if len(px) < 200:
        raise SystemExit(f"te weinig pixels voor {img_rel} {box} ({len(px)})")
    ramp = []
    for q in (5, 25, 50, 75, 95):
        lo, hi = np.percentile(L, max(0, q - 3)), np.percentile(L, min(100, q + 3))
        band = px[(L >= lo) & (L <= hi)]
        c = np.median(band, axis=0).round().astype(int)
        ramp.append("#%02X%02X%02X" % tuple(c))
    return ramp, int(sel.sum())


if __name__ == "__main__":
    for key, (img, box, seed, tol) in SPECS.items():
        ramp, n = measure(img, box, seed, tol)
        print(f'    "{key}": {ramp},  # {n} px')
