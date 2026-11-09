"""Geen dubbele vlakken (z-fighting) in de treinen, de natuurkit en de spoorkit (issue #203), en de railbotsvorm
loopt schuin over de onderlegplaatjes en bouten heen (je blijft er niet achter haken).

    python ArtSource/Tests/test_no_double_faces.py

De stationkit heeft zijn eigen controle in test_station_kit.py. Personages volgen nog (zie #203).
"""
import glob
import os
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Environment"))
import zfight as Z  # noqa: E402

EXP = os.path.join(ROOT, "ArtSource", "Exports")
KITS = {
    "treinen": [os.path.join(EXP, f"{n}_Blockout.glb") for n in ("Locomotive", "Tender", "RegionalWagon", "CattleWagon")],
    "natuur": sorted(glob.glob(os.path.join(EXP, "Environment", "*.glb"))),
    "spoor": sorted(glob.glob(os.path.join(EXP, "Environment", "Track", "*.glb"))),
}
FLOATING = ("Cloud_",)           # zweeft: ook de onderkant is zichtbaar


def check_kit(name, paths):
    assert paths, f"geen exports gevonden voor {name}"
    bad = []
    for p in paths:
        base = os.path.basename(p)
        ground = -1e9 if base.startswith(FLOATING) else None
        res = Z.find(Z.glb_triangles(p), ground=ground)
        if res:
            la, lb, ar, dz, c, n = res[0]
            bad.append(f"{base}: {len(res)}x, bv. {la} <> {lb} {ar * 1e4:.0f} cm² rond {np.round(c, 2).tolist()}")
    assert not bad, f"dubbele vlakken in {name}:\n  " + "\n  ".join(bad)
    print(f"PASS {name}: {len(paths)} modellen zonder dubbele vlakken")


def test_rail_collider_covers_bolts():
    """De railbotsvorm is onderaan breder dan de bouten en loopt schuin op naar de railkop."""
    import build_track_kit as T
    plate_half, bolt_dz, bolt_r = 0.17, 0.12, 0.03
    bolt_top = T.SLEEPER_TOP + 0.035 + 0.025
    hb, hw = T.RAIL_COL_BASE, T.RAIL_W * 0.5
    assert hb >= plate_half, "railbotsvorm smaller dan de onderlegplaatjes"
    # hoogte van het schuine vlak boven de buitenrand van de bout
    z = bolt_dz + bolt_r
    y = (T.SLEEPER_TOP - 0.01) + (T.RAIL_TOP - T.SLEEPER_TOP + 0.01) * (hb - z) / (hb - hw)
    assert y >= bolt_top - 0.005, f"bout steekt {100 * (bolt_top - y):.1f} cm door de railbotsvorm"
    import math
    slope = math.degrees(math.atan2(T.RAIL_TOP - T.SLEEPER_TOP, hb - hw))
    assert slope < 50, f"railbotsvorm te steil ({slope:.0f}°)"
    print(f"PASS railbotsvorm: ±{hb:.2f} m onderaan, helling {slope:.0f}°, bouten erbinnen")


if __name__ == "__main__":
    for kit, paths in KITS.items():
        check_kit(kit, paths)
    test_rail_collider_covers_bolts()
