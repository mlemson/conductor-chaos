"""Treinset 'Diesel jaren '60' (upgrade na de stoomtrein) — naar het conceptblad 'Trein upgrades' van @mlemson.

Twee voertuigen in dezelfde maatvoering als de regionale wagon (zie train_kit.py):

DieselLocomotive_Blockout — bordeauxrode diesel met ronde 'bulldog'-neus uit één vloeiende romp, crème V op de neus
  die doorloopt als schouderlijn, twee voorruiten in een ronde kap, roosters in de flank, grijs gewelfd dak.
  Binnen doorloopbaar: van de kopse doorgang achter langs het motorblok naar de cabine voorin (lessenaar `CabDesk`,
  gashendel `ThrottleLever`). Dakopgang: gele ladder in de machinekamer naar dakluik `RoofHatch_Engine`.
  Onderhoudspunten: `RepairPoint_Fan` (dakventilator), `RepairPoint_EngineHatch` (motorluik perronkant),
  `RepairPoint_AxleBox` (warmgelopen aspot) en `RepairPoint_BrakeHose` (remslang achter).

DieselCoach_Blockout — bijpassend rijtuig: bordeaux met crème raamband en gouden bies, groene banken met crème
  hoofdsteunen. Dakopgang: gele ladder op de perronkant, met de dakwelving mee tot op het loopdek.
  Onderhoudspunten: `RepairPoint_Vent` (dakventilator), `RepairPoint_AxleBox`, `RepairPoint_BrakeHose` (voor).

Rijrichting +X (neus), perronkant op z = 0, spoorstaaf op y = -0.62, vloer op y = 0.10, loopdek op y = 2.50.

Uitvoer: Assets/Art/Models/Train/{DieselLocomotive,DieselCoach}/*.fbx en ArtSource/Exports/*.glb
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
import train_kit as tk  # noqa: E402
from train_kit import mk, box, mirror_far, ZC, W, FLOOR, WALL, ROOF_TOP  # noqa: E402

COLORS = {
    "Body": ("train_red", 2, 0.25, 0.0, {"paint": 1.5}),
    "BodyLow": ("train_red", 1, 0.25, -0.18, {"paint": 1.3}),
    "Panel": ("train_red", 3, 0.25, 0.06, {"paint": 1.5}),
    "Cream": ("parchment", 3, 0.20, 0.0, {"paint": 1.2}),
    "Roof": ("plinth_stone", 1, 0.20, -0.06, {"paint": 1.6}),
    "Gold": ("gold_trim", 2, 0.40, 0.0),
    "Seat": ("chalk_green", 2, 0.15, 0.0),
    "Head": ("parchment", 3, 0.15, 0.0),
    "Desk": ("cab_interior", 2, 0.15, 0.0),
    "Engine": ("metal", 0, 0.30, -0.05),
}

# hoge, stompe bulldog-neus (sleuteldoorsneden; train_kit.smooth maakt er een vloeiend verloop van)
HOOD = [dict(x=-0.05, a=1.31, yb=-0.28, yt=1.30, n=5.0), dict(x=0.40, a=1.31, yb=-0.28, yt=1.30, n=5.0),
        dict(x=0.80, a=1.27, yb=-0.28, yt=1.28, n=4.5), dict(x=1.05, a=1.14, yb=-0.26, yt=1.22, n=4.0),
        dict(x=1.22, a=0.90, yb=-0.18, yt=1.10, n=3.4), dict(x=1.32, a=0.55, yb=-0.02, yt=0.92, n=2.8),
        dict(x=1.36, a=0.12, yb=0.30, yt=0.66, n=2.2)]
# cabinekap: vrij rechte voorruit onder een ronde dakrand
DOME = [dict(x=-0.03, a=1.33, yb=1.22, yt=2.52, n=4.5), dict(x=0.12, a=1.32, yb=1.22, yt=2.50, n=4.5),
        dict(x=0.30, a=1.27, yb=1.22, yt=2.36, n=4.0), dict(x=0.46, a=1.18, yb=1.22, yt=1.98, n=3.6),
        dict(x=0.58, a=1.06, yb=1.22, yt=1.60, n=3.2), dict(x=0.66, a=0.92, yb=1.22, yt=1.34, n=3.0)]


def grille(c, x0, x1, y0, y1, slats=5):
    """Ventilatierooster in de flank: donker vak met lamellen in een kader uit één stuk."""
    out = [box(x0, x1, y0, y1, -0.02, 0.014, 0.01, c.m("Wheel")),
           tk.frame(x0 + 0.02, x1 - 0.02, y0 + 0.02, y1 - 0.02, 0.06, -0.01, 0.05, 0.02, c.m("Trim"))]
    h = (y1 - y0 - 0.10) / slats
    for k in range(slats):
        ya = y0 + 0.05 + k * h
        out.append(box(x0 + 0.05, x1 - 0.05, ya + 0.02, ya + h - 0.03, 0.0, 0.034, 0.008, c.m("BodyLow")))
    return mk.merge(*out)


def build_locomotive():
    c = tk.Car("DieselLocomotive_Blockout", "DieselLocomotive", "Diesel", 6.4)
    c.materials(COLORS)
    L = c.L
    ladder_x = -1.90
    hole = c.ladder_hole(ladder_x)
    shield = (1, 1.36, 2.04, ZC - 1.05, ZC + 1.05)
    c.body(win_near=[(-2.95, -2.15), (1.95, 2.85)], win_far=[(-3.00, -2.52), (-1.20, -0.40), (1.95, 2.85)],
           win_y=(1.02, 1.70), ends=(-1,), end_holes=[shield], roof_holes=[hole])
    c.windows()
    c.bands("Livery", [(-L / 2 + 0.12, L / 2 - 0.12, 0.80, 0.94, "Cream", 0.030),
                       (-L / 2 + 0.12, L / 2 - 0.12, 0.715, 0.765, "Gold", 0.036),
                       (-L / 2 + 0.12, L / 2 - 0.12, 1.91, 1.99, "BodyLow", 0.030),
                       (-3.00, -1.45, 0.22, 0.64, "Panel", 0.028), (0.35, 1.60, 0.22, 0.64, "Panel", 0.028),
                       (1.80, 3.00, 0.22, 0.64, "Panel", 0.028)])
    g = mk.merge(grille(c, -1.80, -0.70, 1.08, 1.66), grille(c, -0.40, 0.70, 1.08, 1.66), grille(c, 0.95, 1.65, 1.08, 1.66))
    c.add("Grilles", mk.merge(g, mirror_far(grille(c, 0.10, 1.50, 1.08, 1.66))))

    # neus: ronde bulldog-romp met crème V (loopt door in de schouderlijn), ronde kap met twee voorruiten
    # crème vleugels: een band die bij de kopwand op de schouderlijn (0,80-0,94) ligt en langs de romp naar de neuspunt loopt
    bands = c.ring_bands(HOOD, [(-0.06, "BodyLow"), (0.78, "Body"), (0.99, "Cream")], "Body")

    def hood_mat(i, j, p):
        return bands[j]
    dome_mat = c.dome_mat(pillar=True)
    tip = L / 2 + 1.36
    extra = []
    for dz in (-0.60, 0.60):
        lx = c.surface_x(HOOD, 0.24, dz)
        house = mk.cylinder(0.17, 0.30, 10, 0.03, mat=c.m("Trim"), axis="x")
        lens = mk.cylinder(0.125, 0.10, 10, 0.015, mat=c.m("Lamp"), axis="x").transform(t=(0.12, 0, 0))
        house.sharp = lens.sharp = True
        extra.append(mk.merge(house, lens).transform(t=(L / 2 + lx - 0.10, 0.24, ZC + dz)))
    badge = mk.cylinder(0.11, 0.08, 8, 0.02, mat=c.m("Gold"), axis="x")
    badge.sharp = True
    extra.append(badge.transform(t=(tip - 0.02, 0.50, ZC)))
    top_lamp = mk.cylinder(0.12, 0.16, 10, 0.02, mat=c.m("Trim"), axis="x")
    top_lens = mk.cylinder(0.085, 0.08, 10, 0.012, mat=c.m("Lamp"), axis="x").transform(t=(0.05, 0, 0))
    top_lamp.sharp = top_lens.sharp = True
    extra.append(mk.merge(top_lamp, top_lens).transform(t=(L / 2 + 0.30, 2.40, ZC)))
    c.add("Nose", mk.merge(c.hood(HOOD, hood_mat), c.dome(DOME, dome_mat), *extra))

    # dak: gewelfd loopdek met luik, dakventilator (onderhoudspunt), uitlaat en een ronde dakkast
    fan_house = c.fan("Fan", 1.05, ZC)
    stack = mk.lathe([(0.0, 0.0), (0.20, 0.0), (0.20, 0.10), (0.15, 0.16), (0.15, 0.36), (0.19, 0.42), (0.0, 0.42)],
                     10, mat=c.m("Trim"), phase=math.pi / 10).transform(t=(2.30, ROOF_TOP - 0.02, ZC))
    vent = box(-0.75, 0.15, ROOF_TOP - 0.04, ROOF_TOP + 0.16, ZC - 0.48, ZC + 0.48, 0.09, c.m("Trim"), div=3)
    c.add("Roof", mk.merge(c.roof([hole], front_open=True), fan_house, stack, vent, c.hatch("Engine", *hole)))

    # interieur: vloer, dakladder, motorblok langs de overkant, cabine met lessenaar en stoel
    eng = [box(-0.30, 1.30, FLOOR + 0.02, 1.30, -W + WALL + 0.02, -1.50, 0.10, c.m("Engine"), div=2)]
    for xx in (-0.02, 0.38, 0.78):
        eng.append(box(xx, xx + 0.26, 1.28, 1.52, -W + WALL + 0.14, -1.62, 0.06, c.m("BodyLow"), div=2))
    fly = mk.cylinder(0.34, 0.14, 10, 0.03, mat=c.m("Metal"), axis="x")
    fly.sharp = True
    eng.append(fly.transform(t=(-0.36, 0.72, -2.00)))
    eng += [tk.tube((1.26, 0.50, -1.80), (1.60, 0.50, -1.80), 0.04, c.m("Interact")),
            tk.tube((1.60, 0.44, -1.80), (1.60, 1.90, -1.80), 0.055, c.m("Interact"))]
    c.add("Interior", mk.merge(c.floor(), c.roof_ladder(ladder_x), *eng, c.cab_seat(2.40)))
    c.desk(L / 2 - WALL - 0.02, 1.16)

    c.add("EngineBay", c.side_panel("EngineHatch", -0.90, 0.44, w=0.78, h=0.40, mat="Panel"))
    c.underframe((-2.0, 2.0), x1=L / 2 + 0.10)
    c.axle_box("AxleBox", 2.0)
    c.add("Ends", mk.merge(*c.end(-1), c.hose("BrakeHose", -1)))
    c.couplers(-L / 2 - 0.12, tip - 0.30)
    c.finish()
    return c


def build_coach():
    c = tk.Car("DieselCoach_Blockout", "DieselCoach", "Diesel", 7.2)
    c.materials(COLORS)
    L = c.L
    wins = [(-3.18, -2.32), (-2.08, -1.22), (-0.98, -0.12), (0.12, 0.98), (1.22, 2.08), (2.32, 3.18)]
    near = [(-2.74, -2.32)] + wins[1:]              # smal raam naast de dakladder
    c.body(near, wins, (1.00, 1.68))
    c.window_band("WindowBand", 0.90, 1.78, "Cream")
    c.windows()
    c.bands("Livery", [(-L / 2 + 0.12, L / 2 - 0.12, 0.800, 0.850, "Gold", 0.036),
                       (-L / 2 + 0.12, L / 2 - 0.12, 1.91, 1.99, "BodyLow", 0.030)] +
            [(a - 0.06, b + 0.06, 0.24, 0.70, "Panel", 0.028) for (a, b) in wins])
    emblem = mk.cylinder(0.15, 0.06, 10, 0.015, mat=c.m("Gold"), axis="z")
    emblem.sharp = True
    c.add("Emblem", mk.merge(emblem.transform(t=(0.0, 0.47, 0.03)), emblem.transform(t=(0.0, 0.47, -W - 0.03))))

    vent_house = c.fan("Vent", 1.60, ZC, r=0.34)
    domes = [mk.lathe([(0.0, 0.0), (0.24, 0.0), (0.24, 0.06), (0.19, 0.13), (0.10, 0.17), (0.0, 0.18)], 10, mat=c.m("Trim"),
                      phase=math.pi / 10).transform(t=(xx, ROOF_TOP - 0.02, ZC)) for xx in (-1.6, 0.0)]
    c.add("Roof", mk.merge(c.roof(), vent_house, *domes))
    c.add("RoofLadder", c.side_ladder(-3.04))

    seats = [(-3.37, 1), (3.37, -1)]
    for xb in (-2.2, -1.1, 0.0, 1.1, 2.2):
        seats += [(xb - 0.12, -1), (xb + 0.12, 1)]
    c.add("Interior", mk.merge(c.floor(), *c.benches(seats, "Seat", "Head")))
    c.underframe((-2.30, 2.30))
    c.axle_box("AxleBox", -2.30)
    c.add("Ends", mk.merge(*c.end(1), *c.end(-1), c.hose("BrakeHose", 1)))
    c.couplers(-L / 2 - 0.12, L / 2 + 0.12)
    c.finish()
    return c


def main():
    build_locomotive().export("DieselLocomotive", "DieselSet")
    build_coach().export("DieselCoach", "DieselSet")


if __name__ == "__main__":
    main()
