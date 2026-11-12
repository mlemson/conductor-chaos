"""Treinsets 'Modern Express' en 'Futuristic Express' — naar de conceptbladen 'Train Upgrade 2' en
'Train Upgrade 3' van @mlemson. Elke set heeft een stuurstandrijtuig (neus aan +X) en een tussenrijtuig; een tweede
stuurstandrijtuig kan omgekeerd achteraan (TrainDrive kan wagons omgekeerd koppelen).

Modern Express (ModernCabCar_Blockout, ModernCoach_Blockout): blauw met gele band, crème schort en daklijn, rode
  schuifdeuren in een geel kader, schuine glazen kap op een ronde neus, blauwe banken met gele hoofdsteunen.
  Onderhoudspunten: Pantograph (stroomafnemer), Converter (omvormerluik), AxleBox, BrakeHose; rijtuig: Aircon.

Futuristic Express (FuturisticCabCar_Blockout, FuturisticCoach_Blockout): marine met crème raamband en gele bies,
  terracotta deurportalen met lichtgevend kader, cyaan lichtlijnen (neus, schort, deurpijlen), lange lage neus met
  grote glazen kap, dakvin. Onderhoudspunten: Pantograph, PowerCell (energiecel), AxleBox, BrakeHose; rijtuig: Cooler.

Beide: binnen doorloopbaar tot in de cabine (`CabDesk`, `ThrottleLever`), zijdeuren als losse bladen
(`SideDoorLeaf_L/R[_Far]`, dicht), dakopgang via een gele ladder binnen naar dakluik `RoofHatch_Cab` en via de gele
ladder op de perronkant van het tussenrijtuig. Neus = vloeiende romp + glazen kap (train_kit.loft), gewelfd dak,
ronde buik. Maten en assen: zie train_kit.py.

    python ArtSource/Train/ExpressSets/build_express_sets.py [modern|futuristic]
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
import train_kit as tk  # noqa: E402
from train_kit import mk, box, mirror_far, ZC, W, ROOF_TOP  # noqa: E402

GLOW = {"emissive": 0.9, "rim": 0.0, "paint": 0, "cells": 0.0, "grime": 0.0}

MODERN = dict(
    cab=("ModernCabCar_Blockout", "ModernCabCar", "Modern", "ModernCabCar"),
    coach=("ModernCoach_Blockout", "ModernCoach", "Modern", "ModernCoach"),
    colors={
        "Body": ("station_navy", 2, 0.25, 0.0, {"paint": 1.5}),
        "BodyLow": ("station_navy", 0, 0.25, -0.10, {"paint": 1.3}),
        "Roof": ("station_navy", 1, 0.20, -0.10, {"paint": 1.6}),
        "Yellow": ("accent_yellow", 2, 0.25, 0.0, {"paint": 1.2}),
        "Cream": ("parchment", 3, 0.20, 0.0, {"paint": 1.2}),
        "Door": ("title_red", 2, 0.25, 0.0, {"paint": 1.3}),
        "DoorPanel": ("title_red", 3, 0.25, 0.05, {"paint": 1.3}),
        "Seat": ("luggage_blue", 2, 0.15, 0.0),
        "Head": ("accent_yellow", 1, 0.15, 0.0),
        "Step": ("metal", 2, 0.25, 0.0),
        "Desk": ("locker_blue", 1, 0.15, 0.0),
    },
    # (y0, y1, materiaal, diepte): schort, gele band, biesje, daklijn
    bands=[(0.17, 0.36, "Cream", 0.034), (0.36, 0.66, "Yellow", 0.028), (0.80, 0.85, "Yellow", 0.034),
           (1.91, 1.99, "Cream", 0.030)],
    wband=None,
    door=dict(leaf="Door", frame_mat="Yellow", panel="DoorPanel", mark="Cream"),
    portal=None,
    # ronde kogelneus: romp (crème schort, gele band, blauw) + lange schuine kap
    hood=[dict(x=-0.05, a=1.31, yb=-0.28, yt=1.12, n=5.0), dict(x=0.50, a=1.31, yb=-0.28, yt=1.10, n=5.0),
          dict(x=1.00, a=1.26, yb=-0.28, yt=1.02, n=4.4), dict(x=1.40, a=1.12, yb=-0.25, yt=0.92, n=3.8),
          dict(x=1.68, a=0.88, yb=-0.17, yt=0.80, n=3.2), dict(x=1.84, a=0.54, yb=-0.02, yt=0.66, n=2.7),
          dict(x=1.90, a=0.12, yb=0.22, yt=0.48, n=2.2)],
    dome=[dict(x=-0.03, a=1.33, yb=1.02, yt=2.52, n=4.5), dict(x=0.14, a=1.32, yb=1.02, yt=2.50, n=4.5),
          dict(x=0.45, a=1.26, yb=1.00, yt=2.30, n=3.8), dict(x=0.85, a=1.12, yb=0.94, yt=1.90, n=3.3),
          dict(x=1.20, a=0.92, yb=0.86, yt=1.44, n=3.0), dict(x=1.45, a=0.66, yb=0.80, yt=1.04, n=2.8)],
    hood_bands=[(0.36, "Cream"), (0.66, "Yellow")],      # tot die hoogte; daarboven Body
    lamp=(0.20, 0.62),
    seat=("Seat", "Head"),
    belly="Cream",
    side_repair=("Converter", "Yellow", 0.51, 0.22),
    coach_fan="Aircon",
)

FUTURISTIC = dict(
    cab=("FuturisticCabCar_Blockout", "FuturisticCabCar", "Future", "FuturisticCabCar"),
    coach=("FuturisticCoach_Blockout", "FuturisticCoach", "Future", "FuturisticCoach"),
    colors={
        "Body": ("uniform_navy", 3, 0.30, 0.0, {"paint": 1.4}),
        "BodyLow": ("uniform_navy", 1, 0.30, -0.05, {"paint": 1.2}),
        "Roof": ("uniform_navy", 1, 0.25, -0.08, {"paint": 1.5}),
        "Yellow": ("accent_yellow", 2, 0.25, 0.0, {"paint": 1.2}),
        "Cream": ("parchment", 4, 0.20, -0.04, {"paint": 1.1}),
        "Door": ("roof_terracotta", 2, 0.25, 0.0, {"paint": 1.3}),
        "Glow": ("water", 3, 0.50, 0.0, GLOW),
        "GlowY": ("headlight", 1, 0.50, 0.0, GLOW),
        "Seat": ("helmet", 2, 0.15, 0.0),
        "Head": ("parchment", 4, 0.15, 0.0),
        "Step": ("metal", 3, 0.25, 0.0),
        "Desk": ("master_navy", 2, 0.15, 0.0),
    },
    bands=[(0.17, 0.235, "Glow", 0.038), (0.62, 0.74, "Yellow", 0.030), (1.91, 1.99, "BodyLow", 0.030)],
    wband=(0.90, 1.82, "Cream"),
    door=dict(leaf="Body", frame_mat="GlowY", panel=None, mark="Glow"),
    portal="Door",
    # lange lage neus: marine romp met gele bies en crème rug, grote glazen koepel
    hood=[dict(x=-0.05, a=1.31, yb=-0.28, yt=1.04, n=5.0), dict(x=0.60, a=1.31, yb=-0.28, yt=1.02, n=5.0),
          dict(x=1.20, a=1.27, yb=-0.28, yt=0.94, n=4.4), dict(x=1.75, a=1.14, yb=-0.25, yt=0.82, n=3.8),
          dict(x=2.12, a=0.90, yb=-0.18, yt=0.68, n=3.2), dict(x=2.34, a=0.55, yb=-0.04, yt=0.54, n=2.7),
          dict(x=2.42, a=0.12, yb=0.16, yt=0.38, n=2.2)],
    dome=[dict(x=-0.03, a=1.33, yb=0.96, yt=2.52, n=4.5), dict(x=0.16, a=1.32, yb=0.96, yt=2.50, n=4.5),
          dict(x=0.60, a=1.26, yb=0.94, yt=2.26, n=3.6), dict(x=1.10, a=1.12, yb=0.86, yt=1.82, n=3.2),
          dict(x=1.55, a=0.90, yb=0.76, yt=1.32, n=3.0), dict(x=1.85, a=0.62, yb=0.68, yt=0.92, n=2.8)],
    hood_bands=[(0.60, "Body"), (0.74, "Yellow")],       # daarboven Cream (zie hood_top)
    hood_top="Cream",
    lamp=None,
    seat=("Seat", "Head"),
    belly="BodyLow",
    side_repair=("PowerCell", "Glow", 0.44, 0.26),
    coach_fan="Cooler",
)


def livery(c, S, excl, panel=None):
    """Banden om deuren (en het onderhoudsluik) heen."""
    L = c.L
    specs = []
    for (y0, y1, mat, depth) in S["bands"]:
        ex = list(excl)
        if panel and y0 < panel[3] and y1 > panel[2]:
            ex.append((panel[0], panel[1]))
        specs += [(a, b, y0, y1, mat, depth) for (a, b) in tk.segs(-L / 2 + 0.12, L / 2 - 0.12, ex)]
    c.bands("Livery", specs)


def doors(c, S, door):
    for far in (False, True):
        c.side_door(door[0], door[1], far=far, **S["door"])
    if S["portal"]:
        p = mk.frame_ring(door[0] - 0.10, door[1] + 0.10, 0.17, c.door_top - 0.07, 0.20, -0.02, 0.040, 0.02,
                          mat=c.m(S["portal"]), open_bottom=True)
        c.add("DoorPortal", mk.merge(p, mirror_far(p)))


def cab_car(S):
    name, root, prefix, folder = S["cab"]
    c = tk.Car(name, root, prefix, 6.4)
    c.materials(S["colors"])
    L = c.L
    door = (-2.15, -0.95)
    wins = [(-3.04, -2.56), (-0.54, 0.42), (0.66, 1.66), (2.05, 2.90)]
    far = [w for w in wins if w != (0.66, 1.66)]            # daar staat de dakladder tegen de wand
    ladder_x = 1.16
    hole = c.ladder_hole(ladder_x)
    shield = (1, S["hood"][0]["yt"] + 0.08, 2.04, ZC - 1.05, ZC + 1.05)
    c.body(wins, far, (0.98, 1.74), doors_near=[door], doors_far=[door], ends=(-1,), end_holes=[shield], roof_holes=[hole])
    if S["wband"]:
        c.window_band("WindowBand", *S["wband"], doors_near=[(door[0] - 0.36, door[1] + 0.36)],
                      doors_far=[(door[0] - 0.36, door[1] + 0.36)])
    c.windows()
    key, lid_mat, py, ph = S["side_repair"]
    px, pw = 1.16, 0.72
    livery(c, S, [(door[0] - 0.36, door[1] + 0.36)], (px - pw / 2 - 0.08, px + pw / 2 + 0.30, py - ph / 2 - 0.04, py + ph / 2 + 0.04))
    doors(c, S, door)
    c.add("ServiceBay", c.side_panel(key, px, py, w=pw, h=ph, mat=lid_mat))

    # neus: vloeiende romp met doorlopende kleurbanden + glazen kap van dak tot neus
    glow = "Glow" in S["colors"]
    tip = L / 2 + S["hood"][-1]["x"]
    bands = c.ring_bands(S["hood"], S["hood_bands"], S.get("hood_top", "Body"))
    smile = c.ring_bands(S["hood"], [(0.06, "x"), (0.30, "Glow")], "x") if glow else None

    def hood_mat(i, j, p):
        if glow and i >= 11 and smile[j] == c.m("Glow"):
            return c.m("Glow")                                # lichtlijn om de neuspunt
        return bands[j]
    extra = []
    if S["lamp"]:
        ly, lz = S["lamp"]
        for dz in (-lz, lz):
            lx = c.surface_x(S["hood"], ly, dz)
            house = mk.cylinder(0.15, 0.36, 10, 0.03, mat=c.m("Trim"), axis="x")
            lens = mk.cylinder(0.11, 0.10, 10, 0.015, mat=c.m("Lamp"), axis="x").transform(t=(0.15, 0, 0))
            house.sharp = lens.sharp = True
            extra.append(mk.merge(house, lens).transform(t=(L / 2 + lx - 0.13, ly, ZC + dz)))
    bar = box(L / 2 + 0.04, L / 2 + 0.34, 2.33, 2.45, ZC - 0.46, ZC + 0.46, 0.05, c.m("Lamp"), div=2)
    c.add("Nose", mk.merge(c.hood(S["hood"], hood_mat), c.dome(S["dome"], c.dome_mat()), bar, *extra))

    # dak: gewelfd loopdek met luik, stroomafnemer (onderhoudspunt) en een ronde klimaatkast
    panto = c.pantograph("Pantograph", -1.75)
    ac = mk.merge(box(-0.80, 0.30, ROOF_TOP - 0.04, ROOF_TOP + 0.20, ZC - 0.50, ZC + 0.50, 0.10, c.m("BodyLow"), div=3),
                  *[box(-0.62 + k * 0.17, -0.52 + k * 0.17, ROOF_TOP + 0.19, ROOF_TOP + 0.215, ZC - 0.34, ZC + 0.34, 0.006, c.m("Trim"))
                    for k in range(5)])
    gear = [panto, ac, c.hatch("Cab", *hole)]
    if glow:      # dakvin met lichtrand
        fin = mk.prism([(-3.05, ROOF_TOP - 0.06), (-2.35, ROOF_TOP - 0.06), (-2.35, ROOF_TOP + 0.10), (-2.62, ROOF_TOP + 0.44),
                        (-2.90, ROOF_TOP + 0.30)], ZC - 0.07, ZC + 0.07, 0.03, mat=c.m("BodyLow"))
        gear += [fin, box(-2.86, -2.46, ROOF_TOP + 0.14, ROOF_TOP + 0.24, ZC - 0.085, ZC + 0.085, 0.02, c.m("GlowY"))]
    c.add("Roof", mk.merge(c.roof([hole], front_open=True), *gear))

    near = c.benches([(-0.50, 1), (0.42, -1), (0.66, 1), (1.60, -1)], *S["seat"], sides=(1,))
    far_b = c.benches([(-0.50, 1), (0.42, -1)], *S["seat"], sides=(-1,))
    c.add("Interior", mk.merge(c.floor(), c.roof_ladder(ladder_x), c.cab_seat(2.45), *near, *far_b))
    c.desk(L / 2 - tk.WALL - 0.02, S["hood"][0]["yt"] - 0.06)
    c.underframe((-2.0, 2.0), x1=L / 2 + 0.10, belly=S["belly"])
    c.axle_box("AxleBox", 2.0)
    c.add("Ends", mk.merge(*c.end(-1), c.hose("BrakeHose", -1)))
    c.couplers(-L / 2 - 0.12, tip - 0.34)
    c.finish()
    return c, folder


def coach(S):
    name, root, prefix, folder = S["coach"]
    c = tk.Car(name, root, prefix, 7.2)
    c.materials(S["colors"])
    L = c.L
    door = (-0.60, 0.60)
    wins = [(-3.18, -2.20), (-1.96, -0.98), (0.98, 1.96), (2.20, 3.18)]
    near = [(-2.72, -2.20)] + wins[1:]                      # smal raam naast de dakladder
    c.body(near, wins, (0.98, 1.74), doors_near=[door], doors_far=[door])
    if S["wband"]:
        c.window_band("WindowBand", *S["wband"], doors_near=[(door[0] - 0.36, door[1] + 0.36)],
                      doors_far=[(door[0] - 0.36, door[1] + 0.36)])
    c.windows()
    livery(c, S, [(door[0] - 0.36, door[1] + 0.36)])
    doors(c, S, door)
    fan = c.fan(S["coach_fan"], 2.10, ZC, r=0.36)
    ac = mk.merge(box(-2.70, -1.50, ROOF_TOP - 0.04, ROOF_TOP + 0.20, ZC - 0.50, ZC + 0.50, 0.10, c.m("BodyLow"), div=3),
                  *[box(-2.56 + k * 0.19, -2.45 + k * 0.19, ROOF_TOP + 0.19, ROOF_TOP + 0.215, ZC - 0.34, ZC + 0.34, 0.006,
                        c.m("Trim")) for k in range(6)])
    c.add("Roof", mk.merge(c.roof(), fan, ac))
    c.add("RoofLadder", c.side_ladder(-3.04))
    seats = [(-3.37, 1), (-2.32, -1), (-2.08, 1), (-1.00, -1), (1.00, 1), (2.08, -1), (2.32, 1), (3.37, -1)]
    c.add("Interior", mk.merge(c.floor(), *c.benches(seats, *S["seat"])))
    c.underframe((-2.30, 2.30), belly=S["belly"])
    c.axle_box("AxleBox", -2.30)
    c.add("Ends", mk.merge(*c.end(1), *c.end(-1), c.hose("BrakeHose", 1)))
    c.couplers(-L / 2 - 0.12, L / 2 + 0.12)
    c.finish()
    return c, folder


def main():
    which = sys.argv[1:] or ["modern", "futuristic"]
    for key, S, mats in (("modern", MODERN, "ModernSet"), ("futuristic", FUTURISTIC, "FuturisticSet")):
        if key in which:
            for build in (cab_car, coach):
                c, folder = build(S)
                c.export(folder, mats)


if __name__ == "__main__":
    main()
