"""Meet kleurramps (schaduw -> licht) voor het stationnetje en de perronkit uit hun conceptbladen.

Zelfde methode als measure_character_ramps.py: per ramp een uitsnede en een zaadtint; alleen pixels met
dezelfde kleurtoon tellen mee. De uitkomst staat in build_style_card.py (groep 'Station & perron').
Hergebruikt (niet opnieuw gemeten, past al): parchment (pleisterwerk), accent_yellow (geel), train_red
(overweg), headlight (lampgloed), grass/pine (planten), crate (licht hout).

    python Docs/References/tools/measure_station_ramps.py
"""
from measure_character_ramps import measure

SPECS = {
    # Stationnetje (Docs/References/Environment/station-concept.png)
    "station_navy": ("Environment/station-concept.png", (380, 150, 1040, 320), "#435394", 8),
    "station_timber": ("Environment/station-concept.png", (130, 330, 900, 560), "#C3793F", 10),
    "plinth_stone": ("Environment/station-concept.png", (420, 480, 880, 560), "#AD9BA4", 7),
    # Perronkit (Docs/References/Environment/platform-kit-concept.png)
    "platform_stone": ("Environment/platform-kit-concept.png", (0, 400, 1122, 520), "#B69B9A", 7),
    # Hoofdstation met briefingruimte (Docs/References/Environment/main-station-concept.png)
    "roof_terracotta": ("Environment/main-station-concept.png", (520, 30, 1100, 250), "#A5503F", 9),
    "chalk_green": ("Environment/main-station-concept.png", (450, 815, 660, 905), "#5F7042", 8),
    "locker_blue": ("Environment/main-station-concept.png", (805, 830, 955, 975), "#5B6493", 8),
}


if __name__ == "__main__":
    for key, (img, box, seed, tol) in SPECS.items():
        ramp, n = measure(img, box, seed, tol)
        print(f'    "{key}": {ramp},  # {n} px')
