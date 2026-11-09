"""Zakenreiziger — personagevariant op het conducteur-basismodel (issue #204).

Referentie: Docs/References/Characters/business-traveler-concept.png
Kenmerken (silhouet eerst):
  * groot bruin haar met een kuif die naar voren en opzij zwiept, zwarte bril bovenop het haar
  * marine blazer met revers en één knoop, wit overhemd met puntkraag, rode das, bruine riem met gouden gesp
  * bruine schoudertas schuin over het lijf (riem van linkerschouder naar rechterheup) met papieren erin
  * blauwe rolkoffer met uitgetrokken handgreep en geel label met treintje, aan de linkerhand
  * expressies: gehaast/paniek (met zweetdruppels), gefocust, opgelucht, uitgeput
Kleuren: ramps uniform_navy, hair_brown, hoodie_red, leather, luggage_blue, accent_yellow, parchment (palette.json).

Uitvoer: Assets/Art/Models/Characters/BusinessTraveler/BusinessTraveler.fbx (+ .glb/.materials.json in Exports)
Gebruik:  python ArtSource/Characters/BusinessTraveler/build_business_traveler.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

ASSET = "BusinessTraveler"
P = "M_BusinessTraveler_"
HEM_Y = 0.290
OPEN_X = 0.072           # halve breedte van de blazeropening onderaan (overhemd zichtbaar)


def materials(sc):
    ck.face_materials(sc, P)
    ck.material(sc, P + "Blazer", "uniform_navy", 0.05, 0.22, paint=1.3)
    ck.material(sc, P + "BlazerDark", "uniform_navy", -0.22, 0.22)
    ck.material(sc, P + "Trousers", "uniform_navy", -0.08, 0.22)
    ck.material(sc, P + "Shirt", "parchment", 0.40, 0.25)
    ck.material(sc, P + "Tie", "hoodie_red", 0.0, 0.22)
    ck.material(sc, P + "Belt", "leather", -0.20, 0.25)
    ck.material(sc, P + "Gold", "gold_trim", 0.0, 0.35)
    ck.material(sc, P + "Shoe", "boot_brown", 0.0, 0.25)
    ck.material(sc, P + "Hair", "hair_brown", 0.10, 0.25, rim=0.14)
    ck.material(sc, P + "Frame", "ink", 0.10, 0.30)
    ck.material(sc, P + "Lens", "metal", 0.35, 0.40, spec=0.15)
    ck.material(sc, P + "Bag", "leather", 0.05, 0.25, paint=1.1)
    ck.material(sc, P + "BagDark", "leather", -0.30, 0.25)
    ck.material(sc, P + "Paper", "parchment", 0.45, 0.25)
    ck.material(sc, P + "PaperInk", "station_navy", 0.0, 0.25)
    ck.material(sc, P + "Suitcase", "luggage_blue", 0.05, 0.25, paint=1.1)
    ck.material(sc, P + "SuitcaseDark", "luggage_blue", -0.30, 0.25)
    ck.material(sc, P + "Wheel", "ui_bg", 0.10, 0.30)
    ck.material(sc, P + "Tag", "accent_yellow", 0.0, 0.25)
    ck.material(sc, P + "TagInk", "uniform_navy", -0.20, 0.25)
    ck.material(sc, P + "Sweat", "water", 0.20, 0.40, spec=0.15)


# ---------------------------------------------------------------------- kleding
def build_shirt_and_tie():
    parts = []
    shirt = [(-OPEN_X - 0.006, 0.305), (OPEN_X + 0.006, 0.305), (0.098, 0.470), (0.074, bc.Y_COLLAR + 0.003),
             (-0.074, bc.Y_COLLAR + 0.003), (-0.098, 0.470)]
    parts.append(ck.front_plate(shirt, 0.006, P + "Shirt", lift=0.004, chamfer=0.002))
    for s in (1, -1):                                       # puntkraag
        tip = [(s * 0.010, 0.518), (s * 0.074, bc.Y_COLLAR + 0.006), (s * 0.058, 0.474)]
        parts.append(ck.front_plate(tip if s > 0 else tip[::-1], 0.008, P + "Shirt", lift=0.012, chamfer=0.003))
    zk, nk = bc.body_surface(0.0, 0.508)
    parts.append(mk.ellipsoid(0.021, 0.018, 0.012, 14, 7, mat=P + "Tie").transform(R=bc.face_frame(nk),
                                                                                    t=(0, 0.508, zk + 0.020)))
    # das zwaait iets naar opzij (gehaast)
    blade = [(-0.016, 0.496), (0.016, 0.496), (0.034, 0.380), (0.024, 0.352), (0.004, 0.374)]
    parts.append(ck.front_plate(blade, 0.007, P + "Tie", lift=0.016, chamfer=0.002))
    # riem met gouden gesp in de opening van de blazer
    th = math.asin((OPEN_X + 0.030) / bc.BODY_R)
    belt = [(0.0, 0.292), (0.010, 0.294), (0.012, 0.300), (0.012, 0.318), (0.010, 0.324), (0.0, 0.326)]
    parts.append(ck.surface_lathe(belt, P + "Belt", seg=14, theta0=-th, theta1=th))
    S, N = ck.surface(np.array([0.0]), np.array([0.309]))
    buckle = mk.merge(ck.squircle_ring(0.050, 0.036, 0.0065, P + "Gold"),
                      mk.rounded_box(0.008, 0.032, 0.008, 0.003, 1, mat=P + "Gold").transform(t=(0.008, 0, 0.002)))
    parts.append(buckle.transform(R=ck.frame_at(N[0]), t=S[0] + N[0] * 0.020))
    return mk.merge(*parts)


def build_blazer():
    parts = []
    for s in (1, -1):
        lap = [(s * 0.074, bc.Y_COLLAR + 0.006), (s * 0.150, 0.522), (s * 0.170, 0.488), (s * 0.135, 0.470),
               (s * 0.150, 0.448), (s * (OPEN_X + 0.034), 0.395), (s * 0.098, 0.468)]
        parts.append(ck.front_plate(lap if s < 0 else lap[::-1], 0.010, P + "Blazer", lift=0.016, chamfer=0.004))
        # voorpand: smalle strook van de revers tot de zoom die het overhemd afbakent
        panel = [(s * 0.098, 0.468), (s * (OPEN_X + 0.034), 0.395), (s * (OPEN_X + 0.040), HEM_Y),
                 (s * (OPEN_X - 0.002), HEM_Y + 0.004), (s * (OPEN_X + 0.002), 0.40)]
        parts.append(ck.front_plate(panel if s < 0 else panel[::-1], 0.010, P + "Blazer", lift=0.010, chamfer=0.003))
    z, n = bc.body_surface(-(OPEN_X + 0.020), 0.372)
    parts.append(mk.ellipsoid(0.014, 0.014, 0.008, 14, 7, mat=P + "Gold").transform(
        R=bc.face_frame(n), t=(-(OPEN_X + 0.020), 0.372, z + 0.024)))
    # zakkleppen en een iets uitlopende zoom rondom (behalve de opening)
    for s in (1, -1):
        flap = ck.plate([(-0.050, -0.014), (0.050, -0.014), (0.048, 0.014), (-0.048, 0.014)], 0.010,
                        P + "BlazerDark", chamfer=0.003)
        parts.append(ck.wrap(flap, s * 56 * DEG, 0.335, 0.002))
    th0 = math.asin((OPEN_X + 0.040) / bc.BODY_R)
    hem = [(0.0, HEM_Y + 0.030), (0.010, HEM_Y + 0.010), (0.012, HEM_Y - 0.004), (0.005, HEM_Y - 0.010), (0.0, HEM_Y)]
    parts.append(ck.surface_lathe(hem, P + "Blazer", seg=48, theta0=th0, theta1=2 * math.pi - th0))
    return mk.merge(*parts)


def build_arm(side):
    arm = ck.build_arm(P + "Blazer", P + "Skin", sleeve_bulge=1.04)
    cuff = mk.torus(0.080, 0.008, 32, 8, mat=P + "Shirt", a=1.0, b=0.88).transform(t=(0, -0.144, 0))
    return mk.merge(arm, cuff)


# ------------------------------------------------------------------------- haar
def build_hair():
    """Bruin haar: kort aan de zijkanten en achter, veel volume bovenop en een kuif die schuin naar voren en
    naar links (+X) zwiept, boven de ogen. Eén gladde massa."""
    spheres = []
    for y, r, lift, angles in ((0.820, 0.060, 0.008, (95, 120, 145, 170)),
                               (0.900, 0.070, 0.014, (70, 95, 120, 145, 170)),
                               (0.975, 0.082, 0.020, (55, 80, 105, 130, 155, 180)),
                               (1.020, 0.085, 0.020, (0, 40, 80, 120, 160))):
        th = sorted({a * DEG for a in angles} | {-a * DEG for a in angles})
        S, N = ck.surface(np.array(th), np.full(len(th), y))
        for s_, n in zip(S, N):
            spheres.append((s_ + n * lift, r))
    spheres += [(np.array([0.0, 1.075, -0.03]), 0.125), (np.array([0.05, 1.10, 0.06]), 0.10)]
    # kuif: van rechtsvoor (-X) schuin omhoog naar linksvoor (+X), net boven de ogen
    for x, y, z, r in ((-0.150, 1.000, 0.150, 0.058), (-0.075, 1.030, 0.195, 0.070), (0.010, 1.058, 0.215, 0.076),
                       (0.095, 1.082, 0.205, 0.070), (0.170, 1.105, 0.165, 0.056), (0.225, 1.125, 0.110, 0.040)):
        spheres.append((np.array([x, y, z]), r))
    return ck.blob(spheres, P + "Hair", spacing=0.022, k=0.026)


def build_glasses():
    """Zwarte bril omhoog geschoven op het haar (rechthoekige glazen, schuin naar boven)."""
    parts = []
    R = mk.rot_x(-58 * DEG)
    for s in (1, -1):
        c = np.array([s * 0.083, 1.125, 0.150])
        frame = ck.squircle_ring(0.118, 0.076, 0.0105, P + "Frame", expo=5.0)
        lens = mk.rounded_box(0.106, 0.064, 0.006, 0.0028, 1, mat=P + "Lens")
        parts.append(mk.merge(frame, lens).transform(R=R, t=c))
    parts.append(ck.tube([(-0.024, 1.140, 0.156), (0.0, 1.146, 0.160), (0.024, 1.140, 0.156)], 0.009, P + "Frame",
                         n=10))
    for s in (1, -1):                                          # pootjes verdwijnen in het haar
        parts.append(ck.tube([(s * 0.142, 1.118, 0.128), (s * 0.172, 1.105, 0.060), (s * 0.182, 1.085, -0.010)],
                             0.0075, P + "Frame", n=12))
    return mk.merge(*parts)


# ---------------------------------------------------------------------- tassen
def build_messenger_bag():
    """Bruine schoudertas op de rechterheup (-X) met papieren, riem schuin over het lijf."""
    parts = []
    strap = [(120 * DEG, 0.560), (62 * DEG, 0.555), (36 * DEG, 0.505), (10 * DEG, 0.452), (-20 * DEG, 0.402),
             (-50 * DEG, 0.362), (-80 * DEG, 0.336)]
    parts.append(ck.strap(strap, 0.044, 0.012, P + "BagDark", lift=0.020))
    # achterlangs: hoeken doorlopend (210 i.p.v. -150), anders snijdt de gladde lijn dwars over de voorkant
    strap_b = [(120 * DEG, 0.560), (150 * DEG, 0.522), (180 * DEG, 0.472), (210 * DEG, 0.420), (238 * DEG, 0.372),
               (262 * DEG, 0.336)]
    parts.append(ck.strap(strap_b, 0.044, 0.012, P + "BagDark", lift=0.020))
    body = mk.rounded_box(0.205, 0.165, 0.070, 0.026, 3, mat=P + "Bag").transform(t=(0, 0, 0.036))
    flap = mk.rounded_box(0.215, 0.090, 0.018, 0.012, 2, mat=P + "BagDark").transform(t=(0, 0.045, 0.079))
    clasp = mk.rounded_box(0.026, 0.030, 0.012, 0.004, 1, mat=P + "Gold").transform(t=(0, 0.004, 0.092))
    papers = []
    for k, (x, ang, h) in enumerate(((-0.040, -8, 0.085), (0.010, 6, 0.100), (0.055, -3, 0.075))):
        sheet = mk.rounded_box(0.075, h, 0.004, 0.0015, 1, mat=P + "Paper").transform(
            R=mk.rot_z(ang * DEG), t=(x, 0.060 + h / 2 - 0.030, 0.050 + 0.010 * k))
        papers.append(sheet)
        for j in range(3):
            papers.append(mk.rounded_box(0.044, 0.004, 0.003, 0.001, 1, mat=P + "PaperInk").transform(
                t=(0, 0.010 + 0.014 * j, 0.006)).transform(R=mk.rot_z(ang * DEG),
                                                          t=(x, 0.060 + h / 2 - 0.030, 0.050 + 0.010 * k)))
    bag = mk.merge(body, flap, clasp, *papers)
    parts.append(ck.place(bag, -90 * DEG, 0.255, lift=0.010, spin=0.05))
    return mk.merge(*parts)


def build_suitcase(grip_y):
    """Blauwe rolkoffer met ribbels, wieltjes, uitgetrokken greep en geel treinlabel.
    Lokaal: oorsprong = midden van de handgreep (waar de hand vastpakt), koffer staat langs -Y;
    grip_y = hoogte van de greep boven de grond, zodat de wieltjes precies op de grond staan."""
    H, Wd, D = 0.46, 0.32, 0.20
    yb = -grip_y + 0.040                                   # onderkant koffer t.o.v. de greep (wiel r=0,034)
    parts = [mk.rounded_box(Wd, H, D, 0.045, 3, mat=P + "Suitcase").transform(t=(0, yb + H / 2, 0))]
    for x in (-0.090, -0.030, 0.030, 0.090):               # ribbels op de voorkant (steken 1 cm uit)
        parts.append(mk.rounded_box(0.024, H - 0.090, 0.020, 0.008, 1, mat=P + "SuitcaseDark").transform(
            t=(x, yb + H / 2, D / 2 + 0.002)))
    parts.append(mk.rounded_box(Wd + 0.012, 0.030, D + 0.012, 0.012, 1, mat=P + "SuitcaseDark").transform(
        t=(0, yb + H / 2, 0)))
    for x in (-0.115, 0.115):                               # wieltjes
        for z in (-0.060, 0.060):
            parts.append(mk.cylinder(0.034, 0.030, 14, 0.008, mat=P + "Wheel", axis="x").transform(
                t=(x, yb - 0.006, z)))
    # telescoopgreep
    top = yb + H
    for x in (-0.075, 0.075):
        y0, y1 = top - 0.020, -0.004                          # stangen: van in de koffer tot in de greep
        parts.append(mk.cylinder(0.011, y1 - y0, 10, 0.0, mat=P + "Wheel", axis="y").transform(
            t=(x, (y0 + y1) / 2, -0.060)))
    parts.append(mk.capsule_between((-0.090, 0.0, -0.060), (0.090, 0.0, -0.060), 0.020, 0.020, 12, 3, mat=P + "Wheel"))
    # geel label met treintje aan een lusje
    tag = mk.merge(mk.rounded_box(0.075, 0.095, 0.010, 0.0045, 1, mat=P + "Tag"),
                   ck.flat_icon("loco", 0.050, 0.006, P + "TagInk").transform(
                                                                            t=(0, -0.006, 0.005)))
    parts.append(tag.transform(R=mk.rot_z(0.20), t=(0.110, top - 0.085, D / 2 + 0.030)))
    parts.append(ck.tube([(0.095, top - 0.030, D / 2 + 0.032), (0.100, top + 0.010, D / 2 - 0.010),
                          (0.105, top - 0.010, -0.010)], 0.005, P + "TagInk", n=10))
    return mk.merge(*parts).transform(t=(0, 0, 0.060))


def build_sweat():
    prof = [(0.0, -0.020), (0.010, -0.018), (0.016, -0.010), (0.017, 0.000), (0.012, 0.012), (0.005, 0.024),
            (0.0, 0.030)]
    drop = mk.lathe(prof, 16, mat=P + "Sweat")
    parts = []
    for th, y, s in ((-66 * DEG, 0.900, 1.9), (-76 * DEG, 0.820, 1.4)):
        S, N = ck.surface(np.array([th]), np.array([y]))
        parts.append(drop.transform(s=(s, s, s * 0.6), R=ck.frame_at(N[0]) @ mk.rot_z(-0.35))
                     .transform(t=S[0] + N[0] * 0.020))
    return mk.merge(*parts)


# ----------------------------------------------------------------------- scène
def build_scene():
    sc = mk.Scene(ASSET)
    materials(sc)
    ck.add_body(sc, [(None, ck.SHOE_Y, P + "Shoe"), (ck.SHOE_Y, HEM_Y, P + "Trousers"),
                     (HEM_Y, bc.Y_COLLAR, P + "Blazer"), (bc.Y_COLLAR, None, P + "Skin")])
    ck.add_detail(sc, "Shirt", build_shirt_and_tie())
    ck.add_detail(sc, "Blazer", build_blazer())
    ck.add_detail(sc, "Bag", build_messenger_bag())
    ck.add_detail(sc, "Hair", build_hair(), gradient=0.0)
    ck.add_detail(sc, "Glasses", build_glasses(), ao_group="glasses", gradient=0.0)
    ck.add_face(sc, P, lids=False)
    ck.add_detail(sc, "Prop_Sweat", build_sweat(), ao_group="sweat", visible=False, gradient=0.0)
    ck.add_arms(sc, build_arm, {"L": ck.arm_pose(16, -18, 0, side=1), "R": ck.arm_pose(30, 34, side=-1)})
    hl = sc.world_matrix(sc.get("Hand_L"))[:3, 3]
    pos = np.array([hl[0] + 0.040, hl[1], hl[2] - 0.020])
    case = build_suitcase(pos[1] - 0.002)
    ck.attach(sc, "Prop_Suitcase", case, "Hand_L", pos, mk.rot_y(-12 * DEG))
    ck.add_leg_anchors(sc)
    return sc


def main():
    ck.finish(build_scene(), ASSET)


if __name__ == "__main__":
    main()
