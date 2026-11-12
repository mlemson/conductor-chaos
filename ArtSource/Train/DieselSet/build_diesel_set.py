"""Treinset 'Diesel jaren '60' (upgrade na de stoomtrein) — naar het conceptblad 'Trein upgrades' van @mlemson.

Twee voertuigen in dezelfde maatvoering als de regionale wagon (zie train_kit.py):

DieselLocomotive_Blockout — rode diesel met een rechte ronde kop naar de Nederlandse dieseltreinstellen uit de jaren
  '50/'60: raamband rondom, crème vleugels met de koplampen erin, twee lampjes boven de ruiten, zwarte schort, grijs
  rond dak; roosters in de flank en een cabinedeur aan beide kanten (`SideDoorLeaf[_Far]`, dicht).
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

def front_levels():
    """Ronde kop naar de Nederlandse dieseltreinstellen uit de jaren '50/'60: rechte flanken, raamband rondom,
    dak dat rond naar voren afloopt. Lagen van schort tot dak; a = halve breedte, d = diepte naar voren."""
    out = []
    for y in (-0.30, -0.14, 0.02, 0.16, 0.30, 0.44, 0.58, 0.68, 0.78, 0.88, 0.98, 1.12, 1.27, 1.93, 2.04, 2.10):
        d = 0.80 + 0.10 * min(1.0, (y + 0.30) / 0.5) if y < 1.2 else 0.90 - 0.12 * (y - 1.2) / 0.9
        out.append(dict(y=y, a=1.27 + 0.04 * min(1.0, (y + 0.30) / 0.32), d=d, n=3.6))
    for k in range(1, 8):                       # dak: zelfde welving als vault_roof, 1,2 cm erbuiten
        t = k / 7.0
        y = tk.ROOF_BASE + (ROOF_TOP - tk.ROOF_BASE) * math.sin(t * math.pi / 2)
        c = math.cos(t * math.pi / 2)
        out.append(dict(y=y + 0.012, a=tk.ROOF_FLAT + (tk.ROOF_A - tk.ROOF_FLAT) * c + 0.012, d=0.24 + 0.54 * c, n=3.6 - 1.0 * t))
    out[15]["a"] = tk.ROOF_A + 0.012
    return out


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
    door = (1.20, 1.90)                                   # cabinedeur aan beide kanten, vlak achter de kop
    shield = (1, 1.26, 1.94, ZC - 1.05, ZC + 1.05)
    c.body(win_near=[(-2.95, -2.15), (2.30, 2.90)], win_far=[(-3.00, -2.52), (-1.20, -0.40), (2.30, 2.90)],
           win_y=(1.02, 1.70), doors_near=[door], doors_far=[door], ends=(-1,), end_holes=[shield], roof_holes=[hole])
    c.windows()
    for far in (False, True):
        c.side_door(door[0], door[1], far=far, leaf="Body", frame_mat="Cream", panel="Panel", single=True)
    xe = L / 2 - 0.26                                     # banden stoppen waar de kop begint
    cut = [(door[0] - 0.28, door[1] + 0.28)]
    specs = []
    for (y0, y1, mat, depth) in ((0.80, 0.94, "Cream", 0.030), (0.715, 0.765, "Gold", 0.036), (1.91, 1.99, "BodyLow", 0.030)):
        specs += [(a_, b_, y0, y1, mat, depth) for (a_, b_) in tk.segs(-L / 2 + 0.12, xe, cut)]
    specs += [(-3.00, -1.45, 0.22, 0.64, "Panel", 0.028), (-0.15, 0.85, 0.22, 0.64, "Panel", 0.028)]
    c.bands("Livery", specs)
    g = mk.merge(grille(c, -1.80, -0.70, 1.08, 1.66), grille(c, -0.40, 0.70, 1.08, 1.66))
    c.add("Grilles", mk.merge(g, mirror_far(grille(c, -0.25, 0.85, 1.08, 1.66))))

    # kop: zwarte schort, rode romp, crème vleugels met de koplampen erin, raamband rondom, grijs rond dak
    lv = front_levels()
    x0 = L / 2 - 0.22
    N = 24

    def front_mat(i, j, jj, p):
        y = (lv[i]["y"] + lv[i + 1]["y"]) / 2
        if i >= 15:
            return c.m("Roof")
        if i == 14:
            return c.m("Cream")                           # crème lijn onder het dak
        if i == 12:
            return c.m("Body") if jj in (0, 4, 8) else c.m("GlassDark")     # raamband met stijlen
        if y < 0.0:
            return c.m("Trim")
        lo = 0.58 if jj >= 3 else (0.68 if jj == 2 else 9.9)        # vleugels lopen smal uit naar de zijkant
        hi = 0.98 if jj >= 3 else 0.88
        return c.m("Cream") if lo < y < hi else c.m("Body")
    extra = [tk.vshell(x0, lv, N=N, mat_fn=front_mat)]
    for dz in (-0.74, 0.74):                              # koplampen in de vleugels
        px, py, pz = tk.shell_point(x0, dict(lv[8], y=0.78), dz)
        house = mk.cylinder(0.17, 0.20, 12, 0.03, mat=c.m("Trim"), axis="x")
        lens = mk.cylinder(0.125, 0.10, 12, 0.015, mat=c.m("Lamp"), axis="x").transform(t=(0.07, 0, 0))
        house.sharp = lens.sharp = True
        extra.append(mk.merge(house, lens).transform(t=(px - 0.05, py, pz)))
    for dz in (-0.50, 0.50):                              # twee kleine lampjes boven de ruiten
        px, py, pz = tk.shell_point(x0, dict(lv[13], y=2.02), dz)
        ring = mk.cylinder(0.085, 0.10, 10, 0.02, mat=c.m("Cream"), axis="x")
        lens = mk.cylinder(0.055, 0.06, 10, 0.01, mat=c.m("Lamp"), axis="x").transform(t=(0.03, 0, 0))
        ring.sharp = lens.sharp = True
        extra.append(mk.merge(ring, lens).transform(t=(px - 0.03, py, pz)))
    for k in range(3):                                    # snelheidsstrepen in de vleugels
        for sg in (-1, 1):
            px, py, pz = tk.shell_point(x0, dict(lv[8], y=0.70 + 0.09 * k), sg * 1.10)
            extra.append(box(px - 0.02, px + 0.03, py - 0.014, py + 0.014, pz - 0.14, pz + 0.14, 0.006, c.m("Body")))
    # de V tussen de vleugels: één crème plaat met schuine randen, 1,5 cm voor de kop
    vee = mk.prism([(-0.56, 0.60), (0.0, 0.08), (0.56, 0.60)], -0.05, 0.015, 0.004, mat=c.m("Cream"))
    extra.append(tk.to_end(vee, 1, x0 + 0.90).transform(t=(0, 0, ZC)))
    shelf = box(L / 2 - 0.02, L / 2 + 0.50, 0.96, 1.20, ZC - 1.14, ZC + 1.14, 0.08, c.m("Desk"), div=2)
    buffer_ = box(L / 2 + 0.50, L / 2 + 0.80, -0.20, 0.06, ZC - 0.22, ZC + 0.22, 0.05, c.m("Panel"), div=2)
    c.add("Nose", mk.merge(*extra, shelf, buffer_))
    tip = x0 + 0.90

    # dak: gewelfd loopdek met luik, dakventilator (onderhoudspunt), uitlaat en een ronde dakkast
    fan_house = c.fan("Fan", 1.05, ZC)
    stack = mk.lathe([(0.0, 0.0), (0.20, 0.0), (0.20, 0.10), (0.15, 0.16), (0.15, 0.36), (0.19, 0.42), (0.0, 0.42)],
                     10, mat=c.m("Trim"), phase=math.pi / 10).transform(t=(2.20, ROOF_TOP - 0.02, ZC))
    vent = box(-0.75, 0.15, ROOF_TOP - 0.04, ROOF_TOP + 0.16, ZC - 0.48, ZC + 0.48, 0.09, c.m("Trim"), div=3)
    c.add("Roof", mk.merge(c.roof([hole], front_open=True), fan_house, stack, vent, c.hatch("Engine", *hole)))

    # interieur: vloer, dakladder, motorblok langs de overkant, cabine met lessenaar en stoel
    eng = [box(-0.60, 0.90, FLOOR + 0.02, 1.30, -W + WALL + 0.02, -1.50, 0.10, c.m("Engine"), div=2)]
    for xx in (-0.36, 0.04, 0.44):
        eng.append(box(xx, xx + 0.26, 1.28, 1.52, -W + WALL + 0.14, -1.62, 0.06, c.m("BodyLow"), div=2))
    fly = mk.cylinder(0.34, 0.14, 10, 0.03, mat=c.m("Metal"), axis="x")
    fly.sharp = True
    eng.append(fly.transform(t=(-0.66, 0.72, -2.00)))
    eng += [tk.tube((0.86, 0.50, -1.80), (1.02, 0.50, -1.80), 0.04, c.m("Interact")),
            tk.tube((1.02, 0.44, -1.80), (1.02, 1.90, -1.80), 0.055, c.m("Interact"))]
    c.add("Interior", mk.merge(c.floor(), c.roof_ladder(ladder_x), *eng, c.cab_seat(2.50)))
    c.desk(L / 2 - WALL - 0.02, 1.10)

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
