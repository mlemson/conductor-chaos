"""Moderne goederenwagens bij de treinsets uit train_kit.py — op verzoek van @mlemson.

TankWagon_Blockout — ketelwagon (olie/water): donkergroene ketel met bolle koppen en spanbanden op zadels, vlakke
  kopbordessen, een gele ladder aan elke kant die over de ketelkop naar de loopbrug bovenop loopt, mangat bovenop.
  Je loopt er dus óverheen naar de volgende wagon. Onderhoudspunten: `RepairPoint_Valve` (lekkende afsluiter met geel
  handwiel, perronkant), `RepairPoint_AxleBox`, `RepairPoint_BrakeHose`.

CargoWagon_Blockout — moderne gesloten goederenwagon (schuifwandwagen): blauwe bak met ribben en gele bies, gewelfd
  lichtgrijs dak, binnen doorloopbaar langs kratten, gele ladder op de perronkant naar het dak.
  Onderhoudspunten: `RepairPoint_DoorLatch` (grendelkast), `RepairPoint_AxleBox`, `RepairPoint_BrakeHose`.

Zelfde maatvoering en koppelingen als de andere wagons (zie train_kit.py). Geen bedrijfslogo's.

Uitvoer: Assets/Art/Models/Train/{TankWagon,CargoWagon}/*.fbx en ArtSource/Exports/*.glb
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
import train_kit as tk  # noqa: E402
from train_kit import mk, box, ZC, W, FLOOR, WALL, ROOF_TOP  # noqa: E402

COLORS = {
    "Body": ("locker_blue", 2, 0.25, 0.0, {"paint": 1.4}),
    "BodyLow": ("locker_blue", 0, 0.25, -0.05, {"paint": 1.2}),
    "Panel": ("locker_blue", 3, 0.25, 0.05, {"paint": 1.4}),
    "Roof": ("metal", 3, 0.25, 0.0, {"paint": 1.5}),
    "Tank": ("pine", 1, 0.35, 0.0, {"paint": 1.6}),
    "TankLow": ("pine", 0, 0.30, -0.05, {"paint": 1.3}),
    "Yellow": ("accent_yellow", 2, 0.25, 0.0, {"paint": 1.2}),
    "Crate": ("crate", 2, 0.15, 0.0),
    "Deck": ("metal", 1, 0.25, 0.0, {"paint": 1.2}),
}
R, CY = 1.14, 1.44            # ketelstraal en hartlijn
TOP = CY + R                  # bovenkant ketel


def build_tank():
    c = tk.Car("TankWagon_Blockout", "TankWagon", "Freight", 7.2)
    c.materials(COLORS)
    L = c.L
    it, tr = c.m("Interact"), c.m("Trim")
    xc, ec = 2.40, 0.62
    st = []
    for sg in (-1, 1):
        ring = []
        for k in range(6, -1, -1) if sg < 0 else range(0, 7):
            t = k * math.pi / 2 / 6
            r = max(0.05, R * math.cos(t))
            ring.append(dict(x=sg * (xc + ec * math.sin(t)), a=r, yb=CY - r, yt=CY + r, n=2.0))
        st += ring
    tank = tk.loft(st, N=24, mat=c.m("Tank"), cap0=True, cap1=True)
    hoops = []
    for x in (-1.9, -0.65, 0.65, 1.9):
        h = mk.cylinder(R + 0.03, 0.09, 24, 0.01, mat=c.m("TankLow"), axis="x")
        h.sharp = True
        hoops.append(h.transform(t=(x, CY, ZC)))
    saddles = [box(x - 0.24, x + 0.24, 0.08, CY - 0.55, ZC - 0.92, ZC + 0.92, 0.06, tr, div=2) for x in (-1.9, 1.9)]
    c.add("Body", mk.merge(tank, *hoops, *saddles))

    # loopbrug bovenop met mangat (naast het loopvlak), kopbordessen en lage kopschotten
    walk = box(-2.45, 2.45, TOP - 0.06, TOP + 0.05, ZC - 0.40, ZC + 0.40, 0.02, c.m("Deck"))
    lid = mk.lathe([(0.0, 0.0), (0.30, 0.0), (0.30, 0.10), (0.24, 0.16), (0.0, 0.17)], 12, mat=tr,
                   phase=math.pi / 12).transform(t=(0.0, TOP - 0.10, ZC - 0.72))
    c.add("Roof", mk.merge(walk, lid))
    deck = [box(-L / 2, L / 2, 0.0, 0.12, ZC - 1.25, ZC + 1.25, 0.04, c.m("Deck"))]
    for sg in (-1, 1):
        xa, xb = sorted((sg * (L / 2 - 0.09), sg * L / 2))
        for (za, zb) in ((ZC - 1.22, ZC - 0.50), (ZC + 0.50, ZC + 1.22)):      # doorgang in het midden blijft vrij
            deck.append(box(xa, xb, 0.10, 0.62, za, zb, 0.03, tr))
    c.add("Deck", mk.merge(*deck))

    # ladders: recht omhoog vanaf het bordes en schuin over de ketelkop naar de loopbrug
    lad = []
    for sg in (-1, 1):
        xl = sg * 3.22
        top = (sg * 2.40, TOP + 0.08)
        for k, dz in enumerate((-0.22, 0.22)):
            lad += tk.pipe_run([(xl, 0.12, ZC + dz), (xl, 1.55, ZC + dz), (top[0], top[1], ZC + dz),
                                (top[0] - sg * 0.02, TOP - 0.02, ZC + dz)], 0.03 - 0.0004 * k, it)
        for y in (0.42, 0.70, 0.98, 1.26, 1.54):
            lad.append(tk.tube((xl, y, ZC - 0.22), (xl, y, ZC + 0.22), 0.024, it))
        for f in (0.25, 0.5, 0.75):
            x, y = xl + (top[0] - xl) * f, 1.55 + (top[1] - 1.55) * f
            lad.append(tk.tube((x, y, ZC - 0.22), (x, y, ZC + 0.22), 0.024, it))
    c.add("RoofLadder", mk.merge(*lad))

    # afsluiter: vaste leiding onder de ketel; heel = geel handwiel, kapot = wiel scheef + straal
    pipe = mk.merge(tk.tube((0.0, CY - R + 0.05, 0.75), (0.0, -0.04, 0.75), 0.075, tr), tk.tube((0.0, 0.0, 0.70), (0.0, 0.0, 1.22), 0.055, tr),
                    box(-0.11, 0.11, -0.11, 0.11, 1.16, 1.36, 0.03, c.m("Metal"), div=2))
    wheel = mk.merge(mk.lathe([(0.15, -0.025), (0.20, -0.025), (0.20, 0.025), (0.15, 0.025)], 10, mat=it, closed=True),
                     box(-0.17, 0.17, -0.015, 0.015, -0.025, 0.025, 0.008, it),
                     mk.cylinder(0.035, 0.09, 8, 0.008, mat=c.m("Metal"), axis="y").transform(t=(0, -0.05, 0)))
    good = wheel.transform(R=mk.rot_x(math.pi / 2), t=(0.0, 0.0, 0.10))
    bad = mk.merge(wheel.transform(R=mk.rot_x(math.pi / 2) @ mk.rot_z(0.5), t=(0.05, -0.10, 0.12)),
                   *c.puffs([(0.0, -0.26, 0.20, 0.10), (0.02, -0.46, 0.30, 0.14), (-0.03, -0.68, 0.42, 0.19)], "Steam"))
    c.add("ValvePipe", pipe.transform(t=(0.6, 0.28, ZC)))
    c.repair("Valve", (0.6, 0.28, ZC + 1.36), good, bad, (0.0, 0.11, -0.10), (0, 1, 0))

    c.underframe((-2.30, 2.30), belly="TankLow")
    c.axle_box("AxleBox", -2.30)
    c.add("Ends", c.hose("BrakeHose", 1, dz=0.86))
    c.couplers(-L / 2 - 0.12, L / 2 + 0.12)
    c.finish()
    return c


def build_cargo():
    c = tk.Car("CargoWagon_Blockout", "CargoWagon", "Freight", 7.2)
    c.materials(COLORS)
    L = c.L
    c.body([], [], (1.0, 1.7))
    ribs = [x for x in [-3.0 + 0.6 * k for k in range(11)] if not 0.9 < x < 2.2]
    specs = [(x - 0.035, x + 0.035, 0.26, 1.88, "BodyLow", 0.040) for x in ribs]
    specs += [(-L / 2 + 0.12, L / 2 - 0.12, 1.90, 1.99, "BodyLow", 0.055), (-L / 2 + 0.12, 0.92, 0.17, 0.25, "Yellow", 0.055),
              (2.18, L / 2 - 0.12, 0.17, 0.25, "Yellow", 0.055)]
    c.bands("Livery", specs)
    c.add("LatchBox", c.side_panel("DoorLatch", 1.50, 0.62, w=0.72, h=0.44, mat="Yellow"))
    c.add("Roof", c.roof())
    c.add("RoofLadder", c.side_ladder(-3.30))

    # lading langs de overkant; het gangpad aan de perronkant blijft vrij
    zf = -W + WALL + 0.04
    crates = []
    for (x0, x1, h, d) in ((-3.2, -2.3, 0.90, 0.95), (-2.2, -1.2, 1.30, 0.90), (-1.1, -0.3, 0.75, 1.00), (0.2, 1.3, 1.10, 0.95),
                           (1.4, 2.2, 0.80, 0.90), (2.3, 3.2, 1.40, 0.85)):
        crates.append(box(x0, x1, FLOOR + 0.02, FLOOR + 0.02 + h, zf, zf + d, 0.04, c.m("Crate"), div=2))
        xm = (x0 + x1) / 2
        crates.append(box(xm - 0.05, xm + 0.05, FLOOR + 0.02, FLOOR + 0.045 + h, zf + 0.02, zf + d + 0.025, 0.01, c.m("Yellow")))
    c.add("Interior", mk.merge(c.floor(), *crates))
    c.underframe((-2.30, 2.30))
    c.axle_box("AxleBox", -2.30)
    c.add("Ends", mk.merge(*c.end(1), *c.end(-1), c.hose("BrakeHose", 1)))
    c.couplers(-L / 2 - 0.12, L / 2 + 0.12)
    c.finish()
    return c


def main():
    build_tank().export("TankWagon", "FreightSet")
    build_cargo().export("CargoWagon", "FreightSet")


if __name__ == "__main__":
    main()
