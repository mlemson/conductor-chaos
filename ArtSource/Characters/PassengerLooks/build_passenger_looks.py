"""Reizigerslooks: per reizigerstype twee extra looks op het bestaande model (herkleuring + eigen silhouetstuk).

Per type één FBX (`<Type>Looks.fbx`) met een lege `Body`-knoop en de nieuwe onderdelen als `Look_*`-kinderen op hun
plek op het lijf (zoals CrewAccessories), plus herkleurde materialen. PlaygroundPassenger in Unity verbergt de
vervangen onderdelen, kopieert de `Look_*`-knopen naar de Body van de reiziger en wisselt materialen op naam.
De look-spec (verbergen / toevoegen / materialen) staat in ArtSource/Exports/<Type>Looks.looks.json.

  Zakenreiziger  Bun      zakenvrouw: kastanjebruin haar met knot, bordeauxrood pak, gele das, rode koffer
                 Bowler   bolhoed met kort haar, grijs pak, petrol das, groene koffer
  Toerist        Bucket   crème vissershoedje, lichtblauw bloemenhemd, kaki broek, rode rugzak
                 Straw    strohoed met rood lint, groen bloemenhemd, marine broek, oranje rugzak
  Oudere         Opa      flatcap, grijze krans en walrussnor, lodengroene jas, rode sjaal, leren tas
                 Navy     oma in blauwe jas, rode baret, gele sjaal, zilverwit haar
  Kind           Pigtails gemberrode staartjes, groene hoodie met crème mouwen, pruimkleurige broek
                 Beanie   petrol pompommuts, gele hoodie, olijf broek, rode rugzak

Alle kleuren uit Docs/References/palette.json. Elk lookonderdeel is gebakken in de volledige reiziger (AO klopt)
en wordt bij de bouw ook als complete reiziger naar ArtSource/Exports/Looks/ geschreven voor de z-fightcontrole.

Gebruik:  python ArtSource/Characters/PassengerLooks/build_passenger_looks.py [Type ...]
"""
import importlib.util
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, DEG  # noqa: E402

BAKE = dict(density=4500, radius=0.10, noise_freq=5.0, edge_range=(14.0, 45.0))
HEAD_Y = 0.905                    # onderkant hoofddeksel (wereld, volwassen maat)


def load(folder, script):
    spec = importlib.util.spec_from_file_location(script, os.path.join(HERE, "..", folder, script + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


# ------------------------------------------------------------------ hulpmiddelen
def ring_spheres(rows, cx=0.0):
    """[(y, straal, lift, hoeken in graden)] -> bollen net boven het lijfoppervlak (beide kanten)."""
    out = []
    for y, r, lift, angles in rows:
        th = sorted({a * DEG for a in angles} | {-a * DEG for a in angles})
        S, N = ck.surface(np.array(th), np.full(len(th), y), cx)
        for s_, n in zip(S, N):
            out.append((s_ + n * lift, r))
    return out


def head_band(a_k, h, mat, seg=44):
    """Bandje rond de hoed: lathe op (a, b) met hoogte h."""
    return mk.lathe([(0.0, h[0]), (a_k + 0.006, h[0]), (a_k + 0.014, h[0] + 0.008), (a_k + 0.014, h[1] - 0.008),
                     (a_k + 0.006, h[1]), (0.0, h[1])], seg, 1.0, 1.0, 2.2, mat=mat)


def flower5(r, mat, heart_mat):
    parts = []
    for k in range(5):
        ang = 2 * math.pi * k / 5
        parts.append(mk.ellipsoid(r, r * 0.70, r * 0.30, 12, 6, mat=mat)
                     .transform(R=mk.rot_z(ang), t=(0.8 * r * math.cos(ang), 0.8 * r * math.sin(ang), 0)))
    parts.append(mk.ellipsoid(r * 0.5, r * 0.5, r * 0.42, 10, 5, mat=heart_mat))
    return mk.merge(*parts)


def on_crown(mesh, a, b, phi, y, n_exp=2.2, out=1.0):
    """Leg een plat onderdeel (lokaal +Z naar buiten) tegen een lathe-wand met halve assen a, b."""
    s, c = math.sin(phi), math.cos(phi)
    x = a * out * np.sign(s) * abs(s) ** (2 / n_exp)
    z = b * out * np.sign(c) * abs(c) ** (2 / n_exp)
    return mesh.transform(R=ck.frame_at(np.array([s, 0.0, c])), t=(x, y, z))


# ============================================================ zakenreiziger
def business_bun(P):
    """Glad kastanjebruin haar met scheiding en een knot achter op het hoofd (wereldcoördinaten)."""
    rows = ((0.780, 0.046, 0.000, (116, 138, 160, 180)),
            (0.850, 0.052, 0.002, (96, 116, 138, 160, 180)),
            (0.915, 0.054, 0.002, (74, 96, 118, 140, 162, 180)),
            (0.965, 0.052, 0.002, (44, 70, 96, 122, 148, 174)),
            (1.000, 0.050, 0.000, (0, 30, 60, 90, 120, 150, 180)))
    sph = ring_spheres(rows)
    sph += [(np.array([0.0, 0.990, -0.010]), 0.090)]
    # schuine pony: van links hoog (scheiding, +X) naar rechts laag over het voorhoofd, boven de ogen
    for x, y, z, r in ((0.165, 0.990, 0.105, 0.040), (0.105, 1.000, 0.135, 0.046), (0.035, 0.990, 0.160, 0.048),
                       (-0.040, 0.968, 0.172, 0.046), (-0.110, 0.945, 0.162, 0.044), (-0.170, 0.918, 0.135, 0.040),
                       (-0.215, 0.885, 0.095, 0.036)):
        sph.append((np.array([x, y, z]), r))
    hair = ck.blob(sph, P + "Bun_Hair", spacing=0.024, k=0.030, margin=0.12)
    axis = np.array([0.0, 0.55, -0.835])
    axis /= np.linalg.norm(axis)
    base = np.array([0.0, 1.000, -0.090])
    bun = ck.blob([(base + axis * 0.085, 0.084), (base + axis * 0.125 + np.array([0.02, 0.0, 0.0]), 0.066)],
                  P + "Bun_Hair", spacing=0.014, k=0.022)
    R = ck.frame_at(axis) @ mk.rot_x(math.pi / 2)            # torus ligt in XZ: as langs lokale Y -> langs axis
    tie = mk.torus(0.064, 0.016, 28, 8, mat=P + "Bun_Tie").transform(R=R, t=base + axis * 0.040)
    return mk.merge(hair, bun, tie)


def business_short_hair(P):
    """Kort haar aan zijkant en achter, net onder de rand van de bolhoed."""
    rows = ((0.775, 0.030, -0.004, (100, 120, 140, 160, 180)),
            (0.828, 0.034, -0.004, (86, 104, 122, 140, 158, 176)),
            (0.868, 0.032, -0.004, (80, 98, 116, 134, 152, 170)))
    sph = ring_spheres(rows)
    for side in (1, -1):                                   # bakkebaarden
        S, N = ck.surface(np.array([side * 78 * DEG]), np.array([0.785]))
        sph.append((S[0] - N[0] * 0.004, 0.026))
    return ck.blob(sph, P + "Bowler_Hair", spacing=0.016, k=0.022, margin=0.12)


def bowler(P):
    """Bolhoed. Lokaal: oorsprong = onderkant band, midden van het hoofd."""
    a, b = 0.282, 0.262
    prof = [(0.0, 0.0), (1.0, 0.0), (1.0, 0.040)]
    for k in range(1, 11):
        t = k / 10 * math.pi / 2
        prof.append((max(0.0, 0.98 * math.cos(t) ** 0.70) if k < 10 else 0.0, 0.040 + 0.215 * math.sin(t)))
    crown = mk.lathe(prof, 44, a, b, 2.2, mat=P + "Bowler_Felt")
    band = mk.lathe([(0.0, 0.004), (1.012, 0.004), (1.022, 0.012), (1.022, 0.052), (1.012, 0.060), (0.0, 0.060)],
                    44, a, b, 2.2, mat=P + "Bowler_Band")
    brim = mk.lathe([(0.97, 0.014), (1.20, 0.000), (1.31, 0.018), (1.345, 0.036), (1.320, 0.044), (1.27, 0.026),
                     (1.18, 0.016), (0.97, 0.030)], 56, a, b, 2.2, closed=True, mat=P + "Bowler_FeltDark")
    V = brim.V.copy()                                       # rand aan de zijkant iets omhoog gekruld
    side = (V[:, 0] / (a * 1.3)) ** 2
    V[:, 1] += 0.030 * np.clip(side, 0, 1) * np.clip((np.hypot(V[:, 0] / a, V[:, 2] / b) - 1.0) / 0.3, 0, 1)
    brim.V = V
    bow = mk.ellipsoid(0.020, 0.016, 0.010, 12, 6, mat=P + "Bowler_Band")
    bow = on_crown(bow, a * 1.022, b * 1.022, 100 * DEG, 0.032)
    return mk.merge(crown, band, brim, bow)


# ================================================================== toerist
def bucket_hat(P):
    """Vissershoedje. Lokaal: oorsprong = onderkant bol (zoals de safarihoed), midden van het hoofd."""
    a, b = 0.300, 0.285
    crown = mk.lathe([(0.0, 0.0), (1.0, 0.0), (0.985, 0.08), (0.95, 0.15), (0.90, 0.195), (0.78, 0.222),
                      (0.50, 0.236), (0.0, 0.240)], 44, a, b, 2.2, mat=P + "Bucket_Hat")
    band = mk.lathe([(0.0, 0.010), (1.016, 0.010), (1.022, 0.018), (1.012, 0.062), (1.000, 0.070), (0.0, 0.070)],
                    44, a, b, 2.2, mat=P + "Bucket_Band")
    brim = mk.lathe([(0.97, 0.030), (1.12, 0.002), (1.30, -0.052), (1.40, -0.084), (1.43, -0.080), (1.425, -0.064),
                     (1.33, -0.036), (1.15, 0.010), (0.97, 0.048)], 56, a, b, 2.2, closed=True, mat=P + "Bucket_Hat")
    # stiksels: lage ribbels op de rand (steken > 5 mm uit, geen dubbel vlak)
    stitches = []
    for r_, y_ in ((1.20, -0.020), (1.31, -0.050)):
        stitches.append(mk.torus(1.0, 0.0065, 56, 6, mat=P + "Bucket_Stitch", a=a * r_, b=b * r_)
                        .transform(t=(0, y_ + 0.0105, 0)))
    eyelets = []
    for phi in (70 * DEG, -70 * DEG):
        ring = mk.torus(0.010, 0.0045, 12, 6, mat=P + "Bucket_Stitch").transform(R=mk.rot_x(math.pi / 2))
        eyelets.append(on_crown(ring, a * 0.955, b * 0.955, phi, 0.135, out=1.0))
    return mk.merge(crown, band, brim, *stitches, *eyelets)


def straw_sun_hat(P):
    """Strohoed met brede rand, rood lint en bloemetje. Lokaal zoals de safarihoed."""
    a, b = 0.300, 0.285
    H = 0.150
    crown = mk.lathe([(0.0, 0.0), (1.0, 0.0), (1.0, H * 0.7), (0.985, H), (0.93, H + 0.016), (0.0, H + 0.022)],
                     44, a, b, 2.3, mat=P + "Straw_Straw")
    band = mk.lathe([(0.0, 0.012), (1.015, 0.012), (1.025, 0.020), (1.025, 0.062), (1.015, 0.070), (0.0, 0.070)],
                    44, a, b, 2.3, mat=P + "Straw_Ribbon")
    brim = mk.lathe([(0.97, 0.020), (1.30, 0.004), (1.56, -0.020), (1.66, -0.030), (1.69, -0.020), (1.66, -0.008),
                     (1.30, 0.020), (0.97, 0.040)], 60, a, b, 2.3, closed=True, mat=P + "Straw_StrawDark")
    weave = [mk.lathe([(1.002, y - 0.005), (1.012, y), (1.002, y + 0.005)], 44, a, b, 2.3, closed=True,
                      mat=P + "Straw_StrawDark") for y in (0.092, 0.122)]
    weave += [mk.torus(1.0, 0.006, 60, 6, mat=P + "Straw_Straw", a=a * r_, b=b * r_).transform(t=(0, y_, 0))
              for r_, y_ in ((1.42, 0.010), (1.58, -0.012))]
    # lintstrik met twee staartjes aan de linkerzijkant (+X)
    knot = on_crown(mk.ellipsoid(0.026, 0.022, 0.014, 12, 6, mat=P + "Straw_Ribbon"), a * 1.03, b * 1.03,
                    96 * DEG, 0.040)
    tails = []
    for dz, ang in ((0.030, 0.25), (-0.030, -0.20)):
        p0 = np.array([a * 1.04, 0.032, dz * 0.3])
        tails.append(ck.tube([p0, p0 + np.array([0.022, -0.050, dz]), p0 + np.array([0.030, -0.090, dz * 1.6])],
                             0.0105, P + "Straw_Ribbon", n=12, seg=8))
    fl = on_crown(flower5(0.020, P + "Straw_Flower", P + "Straw_FlowerHeart"), a * 1.035, b * 1.035, 62 * DEG, 0.044)
    return mk.merge(crown, band, brim, *weave, knot, *tails, fl)


# =================================================================== oudere
def opa_hair(P):
    """Grijze krans rond achterhoofd en slapen (kale kruin onder de pet)."""
    sph = []
    for y, r, lift, angles in ((0.770, 0.050, 0.010, (88, 112, 136, 158, 180)),
                               (0.835, 0.056, 0.016, (80, 102, 124, 146, 168)),
                               (0.880, 0.046, 0.010, (84, 108, 132, 156, 180))):
        sph += ring_spheres(((y, r, lift, angles),))
    return ck.tufts(sph, P + "Opa_Hair", seg=12, rings=7, seed=7)


def opa_mustache(P):
    """Volle grijze walrussnor tussen ogen en mond."""
    sph = []
    for side in (1, -1):
        for t in np.linspace(0.0, 1.0, 7):
            x = side * (0.012 + 0.125 * t)
            y = bc.MOUTH_Y + 0.064 - 0.052 * t ** 1.5
            z, n = bc.body_surface(x, y)
            r = 0.046 * (1 - 0.40 * t)
            sph.append((np.array([x, y, z]) + n * (r * 0.50), r))
            if t < 0.8:                                    # tweede, lagere rij: hangt over de bovenlip
                y2 = y - 0.026
                z2, n2 = bc.body_surface(x * 0.95, y2)
                sph.append((np.array([x * 0.95, y2, z2]) + n2 * (r * 0.40), r * 0.70))
    return ck.tufts(sph, P + "Opa_Hair", seg=12, rings=7, seed=11)


def flat_cap(P):
    """Platte pet met korte klep. Lokaal: oorsprong = onderkant band, midden van het hoofd."""
    a, b = 0.290, 0.268
    band = mk.lathe([(0.0, 0.0), (1.0, 0.0), (1.0, 0.040), (0.0, 0.040)], 44, a, b, 2.3, mat=P + "Opa_Cap")
    prof = [(0.0, 0.038), (1.0, 0.038), (1.06, 0.060), (1.08, 0.085), (1.05, 0.105), (0.92, 0.120), (0.60, 0.128),
            (0.0, 0.130)]
    crown = mk.lathe(prof, 44, a, b, 2.3, mat=P + "Opa_Cap")
    V = crown.V.copy()                                      # bol schuift naar voren over de klep
    V[:, 2] += 0.34 * np.clip(V[:, 1] - 0.038, 0, None) + 0.02 * np.clip(V[:, 2] / b, 0, 1) * (V[:, 1] > 0.05)
    V[:, 1] -= 0.035 * np.clip(V[:, 2] / b, 0, 1.2) ** 2 * (V[:, 1] > 0.06)
    crown.V = V
    seam = mk.torus(1.0, 0.0065, 44, 6, mat=P + "Opa_CapDark", a=a * 1.005, b=b * 1.005).transform(t=(0, 0.040, 0))
    button = mk.ellipsoid(0.022, 0.012, 0.022, 12, 6, mat=P + "Opa_CapDark").transform(t=(0, 0.118, 0.055))
    brim = ck.remap(bc.build_brim(a, b), {"M_Conductor_UniformDark": P + "Opa_CapDark"})
    Vb = brim.V.copy()
    fwd = np.clip(Vb[:, 2] - b * 0.9, 0, None)
    Vb[:, 2] -= fwd * 0.45                                  # korte klep
    Vb[:, 1] += 0.012
    brim.V = Vb
    return mk.merge(band, crown, seam, button, brim)


# ===================================================================== kind
def pigtail_hair(P):
    """Haar over de hele kruin met pony, plus twee staartjes met gele elastiekjes (wereld, volwassen maat)."""
    rows = ((0.790, 0.044, 0.000, (108, 130, 152, 174)),
            (0.860, 0.052, 0.002, (90, 112, 134, 156, 178)),
            (0.925, 0.054, 0.002, (66, 90, 114, 138, 162)),
            (0.975, 0.052, 0.002, (30, 60, 90, 120, 150, 180)),
            (1.005, 0.050, 0.000, (0, 45, 90, 135, 180)))
    sph = ring_spheres(rows) + [(np.array([0.0, 0.985, -0.005]), 0.095)]
    for x, y, z, r in ((-0.140, 0.948, 0.138, 0.044), (-0.075, 0.962, 0.162, 0.048), (0.0, 0.966, 0.170, 0.050),
                       (0.075, 0.962, 0.162, 0.048), (0.140, 0.948, 0.138, 0.044)):
        sph.append((np.array([x, y, z]), r))
    parts = [ck.blob(sph, P + "Pigtails_Hair", spacing=0.024, k=0.028, margin=0.12)]
    for side in (1, -1):
        S, N = ck.surface(np.array([side * 102 * DEG]), np.array([0.930]))
        root, n = S[0], N[0]
        out = np.array([side * 1.0, -0.45, -0.20])
        out /= np.linalg.norm(out)
        tie_c = root + n * 0.055
        R = ck.frame_at(out) @ mk.rot_x(math.pi / 2)
        parts.append(mk.torus(0.034, 0.014, 20, 8, mat=P + "Pigtails_Tie").transform(R=R, t=tie_c))
        tail = [(tie_c + out * 0.050, 0.050), (tie_c + out * 0.105 + np.array([0, -0.040, 0]), 0.068),
                (tie_c + out * 0.140 + np.array([0, -0.105, 0]), 0.064),
                (tie_c + out * 0.150 + np.array([0, -0.170, 0.008]), 0.048),
                (tie_c + out * 0.140 + np.array([0, -0.215, 0.012]), 0.030)]
        parts.append(ck.blob([(root + n * 0.012, 0.040), (tie_c - out * 0.010, 0.030)], P + "Pigtails_Hair",
                             spacing=0.010, k=0.012, margin=0.12))
        parts.append(ck.blob(tail, P + "Pigtails_Hair", spacing=0.016, k=0.020, margin=0.12))
    return mk.merge(*parts)


def beanie_fringe(P, child):
    """De pony-rand van het kind (zonder het plukje door de petopening): past onder de muts."""
    sph = []
    for th in range(-150, 181, 15):
        S, N = ck.surface(np.array([th * DEG]), np.array([0.905 if abs(th) > 60 else 0.925]))
        sph.append((S[0] + N[0] * 0.012, 0.040))
    for th, y, r in ((0, 0.905, 0.038), (16, 0.900, 0.036), (-16, 0.902, 0.036), (34, 0.893, 0.034),
                     (-34, 0.895, 0.034), (52, 0.885, 0.032), (-52, 0.888, 0.032), (72, 0.860, 0.034),
                     (-72, 0.860, 0.034), (90, 0.830, 0.032), (-90, 0.830, 0.032)):
        S, N = ck.surface(np.array([th * DEG]), np.array([y]))
        sph.append((S[0] + N[0] * 0.022, r))
    return ck.blob(sph, P + "Beanie_Hair", spacing=0.018, k=0.010, margin=0.12)


def beanie(P):
    """Gebreide muts met omgeslagen boord en pompon. Lokaal: oorsprong = onderkant boord, midden hoofd."""
    a, b = 0.302, 0.288
    prof = [(0.0, 0.0), (1.0, 0.0)]
    for k in range(1, 13):
        t = k / 12 * math.pi / 2
        prof.append((math.cos(t) ** 0.75 if k < 12 else 0.0, 0.070 + 0.200 * math.sin(t)))
    dome = mk.lathe(prof, 44, a, b, 2.2, mat=P + "Beanie_Knit")
    cuff = mk.lathe([(0.98, -0.006), (1.05, -0.004), (1.075, 0.020), (1.075, 0.060), (1.05, 0.084), (0.98, 0.086)],
                    44, a, b, 2.2, closed=True, mat=P + "Beanie_KnitDark")
    ribs = []                                               # verticale boordribbels die uit de boord steken
    for k in range(28):
        phi = 2 * math.pi * k / 28
        rib = mk.ellipsoid(0.010, 0.036, 0.008, 8, 6, mat=P + "Beanie_KnitDark")
        ribs.append(on_crown(rib, a * 1.072, b * 1.072, phi, 0.040, out=1.0))
    rows = [mk.torus(1.0, 0.007, 44, 6, mat=P + "Beanie_KnitDark", a=a * f, b=b * f).transform(t=(0, y, 0))
            for f, y in ((0.985, 0.130), (0.90, 0.195))]
    rng = np.random.default_rng(5)
    pom = []
    for _ in range(9):
        d = rng.normal(0, 1, 3)
        d /= np.linalg.norm(d)
        pom.append((np.array([0.0, 0.310, 0.0]) + d * np.array([0.050, 0.044, 0.050]), rng.uniform(0.046, 0.056)))
    pompom = ck.tufts(pom, P + "Beanie_Pompom", seg=12, rings=7, seed=3)
    return mk.merge(dome, cuff, *ribs, *rows, pompom)


# ===================================================================== typen
TYPES = {
    "BusinessTraveler": dict(folder="BusinessTraveler", script="build_business_traveler", scale=1.0),
    "Tourist": dict(folder="Tourist", script="build_tourist", scale=1.0),
    "ElderlyPassenger": dict(folder="ElderlyPassenger", script="build_elderly_passenger", scale=1.0),
    "ChildPassenger": dict(folder="ChildPassenger", script="build_child_passenger", scale=0.80),
}


def looks_for(type_name, P):
    """[(look, hide, [(node, mesh, pivot_world|None, R|None, ao)], mats{name: (ramp, shift, opts)},
        recolour{orig: (ramp, shift|None)})]."""
    head = (0.0, HEAD_Y, 0.0)
    if type_name == "BusinessTraveler":
        return [
            ("Bun", ["Hair", "Glasses"], [("Look_Bun_Hair", business_bun(P), None, None, "body")],
             {"Bun_Hair": ("interior_wood", -0.10, dict(rim=0.14)), "Bun_Tie": ("gold_trim", 0.0, {})},
             {"Blazer": ("cap_maroon", None), "BlazerDark": ("cap_maroon", None), "Trousers": ("cap_maroon", None),
              "Tie": ("accent_yellow", 0.0), "Suitcase": ("luggage_red", None), "SuitcaseDark": ("luggage_red", None)}),
            ("Bowler", ["Hair", "Glasses"],
             [("Look_Bowler_Hair", business_short_hair(P), None, None, "body"),
              ("Look_Bowler_Hat", bowler(P), head, mk.rot_x(-5 * DEG) @ mk.rot_z(3 * DEG), "body")],
             {"Bowler_Hair": ("hair_brown", 0.10, dict(rim=0.14)), "Bowler_Felt": ("ui_bg", 0.10, dict(paint=1.2)),
              "Bowler_FeltDark": ("ui_bg", -0.15, {}), "Bowler_Band": ("cap_maroon", 0.0, {})},
             {"Blazer": ("metal", None), "BlazerDark": ("metal", None), "Trousers": ("metal", -0.30),
              "Tie": ("backpack_teal", 0.10), "Suitcase": ("chalk_green", 0.20), "SuitcaseDark": ("chalk_green", -0.10)}),
        ]
    if type_name == "Tourist":
        R = mk.rot_x(-11 * DEG) @ mk.rot_z(-4 * DEG)
        return [
            ("Bucket", ["Hat"], [("Look_Bucket_Hat", bucket_hat(P), head, mk.rot_x(-8 * DEG) @ mk.rot_z(5 * DEG),
                                  "body")],
             {"Bucket_Hat": ("parchment", 0.20, dict(paint=1.2)), "Bucket_Band": ("backpack_teal", 0.0, {}),
              "Bucket_Stitch": ("safari_hat", -0.10, {})},
             {"Shirt": ("water", -0.10), "ShirtDark": ("water", -0.40), "PrintFlower": ("parchment", 0.40),
              "PrintHeart": ("accent_yellow", 0.10), "Shorts": ("safari_hat", -0.20), "ShortsDark": ("safari_hat", -0.45),
              "Backpack": ("luggage_red", None), "BackpackDark": ("luggage_red", None)}),
            ("Straw", ["Hat"], [("Look_Straw_Hat", straw_sun_hat(P), head, R, "body")],
             {"Straw_Straw": ("crate", 0.30, dict(paint=1.3)), "Straw_StrawDark": ("crate", 0.0, {}),
              "Straw_Ribbon": ("title_red", 0.0, {}), "Straw_Flower": ("parchment", 0.35, {}),
              "Straw_FlowerHeart": ("accent_yellow", 0.10, {})},
             {"Shirt": ("dino_green", -0.10), "ShirtDark": ("dino_green", -0.40), "PrintFlower": ("parchment", 0.35),
              "PrintLeaf": ("pine", 0.20), "Shorts": ("uniform_navy", 0.05), "ShortsDark": ("uniform_navy", -0.25),
              "Backpack": ("vest", 0.0), "BackpackDark": ("vest", -0.30)}),
        ]
    if type_name == "ElderlyPassenger":
        return [
            ("Opa", ["Hair", "Beret"],
             [("Look_Opa_Hair", opa_hair(P), None, None, "body"),
              ("Look_Opa_Mustache", opa_mustache(P), None, None, "mustache"),
              ("Look_Opa_Cap", flat_cap(P), (0.0, HEAD_Y + 0.020, 0.0), mk.rot_x(-7 * DEG), "body")],
             {"Opa_Hair": ("hair_grey", 0.20, dict(rim=0.14)), "Opa_Cap": ("wagon_brown", 0.0, dict(paint=1.2)),
              "Opa_CapDark": ("wagon_brown", -0.35, {})},
             {"Coat": ("chalk_green", 0.10), "CoatDark": ("chalk_green", -0.20), "Trousers": ("master_navy", 0.10),
              "Button": ("gs_brass", 0.0), "Scarf": ("scarf", 0.10), "Glasses": ("ink", 0.10),
              "Bag": ("leather", 0.05), "BagPrint": ("parchment", 0.25)}),
            ("Navy", [], [],
             {},
             {"Coat": ("locker_blue", 0.10), "CoatDark": ("locker_blue", -0.20), "Trousers": ("locker_blue", -0.40),
              "Button": ("parchment", 0.30), "Beret": ("title_red", 0.0), "BeretDark": ("title_red", -0.30),
              "Glasses": ("ink", 0.10), "Scarf": ("accent_yellow", 0.05), "Hair": ("wool", 0.10),
              "BagPrint": ("locker_blue", 0.0)}),
        ]
    if type_name == "ChildPassenger":
        return [
            ("Pigtails", ["Cap", "Hair"], [("Look_Pigtails_Hair", pigtail_hair(P), None, None, "body")],
             {"Pigtails_Hair": ("crate", -0.15, dict(rim=0.14)), "Pigtails_Tie": ("accent_yellow", 0.10, {})},
             {"Hoodie": ("dino_green", -0.05), "HoodieLight": ("dino_green", 0.30), "HoodieDark": ("dino_green", -0.35),
              "Hood": ("parchment", 0.25), "Shorts": ("beret_plum", 0.0), "Backpack": ("coat_plum", 0.10),
              "BackpackDark": ("coat_plum", -0.20)}),
            ("Beanie", ["Cap", "Hair"],
             [("Look_Beanie_Hair", beanie_fringe(P, None), None, None, "body"),
              ("Look_Beanie_Hat", beanie(P), head, mk.rot_x(-8 * DEG) @ mk.rot_z(-5 * DEG), "body")],
             {"Beanie_Hair": ("hair_brown", 0.15, dict(rim=0.14)), "Beanie_Knit": ("backpack_teal", 0.10, dict(paint=1.3)),
              "Beanie_KnitDark": ("backpack_teal", -0.20, {}), "Beanie_Pompom": ("parchment", 0.30, {})},
             {"Hoodie": ("accent_yellow", -0.05), "HoodieLight": ("accent_yellow", 0.30),
              "HoodieDark": ("accent_yellow", -0.35), "Hood": ("backpack_teal", 0.10), "Shorts": ("shorts_olive", 0.05),
              "Backpack": ("title_red", 0.0), "BackpackDark": ("title_red", -0.30)}),
        ]
    raise ValueError(type_name)


# ------------------------------------------------------------------------ bouw
def look_part(name, mesh, pivot_world, R, ao, s):
    """Onderdeel als kind van Body (Body-pivot = HIP); met s = schaal van het hele personage (kind)."""
    if pivot_world is None:
        mesh_local, pivot = mesh.transform(t=-ck.HIP), (0.0, 0.0, 0.0)
    else:
        mesh_local, pivot = mesh, tuple(np.asarray(pivot_world, float) - ck.HIP)
    return mk.Part(name, mesh_local.transform(s=(s, s, s)), tuple(np.asarray(pivot) * s), "Body", R,
                   ao_group=ao, gradient=0.0)


def build_type(type_name):
    info = TYPES[type_name]
    base = load(info["folder"], info["script"])
    s = info["scale"]
    asset = type_name + "Looks"
    P = f"M_{asset}_"
    bp = base.P
    exp = mk.Scene(asset)
    exp.add(mk.Part(asset, None))
    exp.add(mk.Part("Body", None, tuple(ck.HIP * s), asset))
    spec = {"type": type_name, "looks": []}
    check_dir = os.path.join(ck.ROOT, "ArtSource", "Exports", "Looks")
    os.makedirs(check_dir, exist_ok=True)
    for look, hide, parts, mats, recolour in looks_for(type_name, P):
        full = base.build_scene()                         # complete reiziger: bakken mét lijf, en z-fightcontrole
        for p in full.parts:
            if p.name in hide:
                p.visible = False
        for key, (ramp, shift, opts) in mats.items():
            ck.material(full, P + key, ramp, shift, 0.22 if "Hair" not in key else 0.25, **opts)
        for node, mesh, pivot, R, ao in parts:
            full.add(look_part(node, mesh, pivot, R, ao, s))
        mapping = {}
        for orig, (ramp, shift) in recolour.items():
            src = full.materials[bp + orig]
            new = f"{P}{look}_{orig}"
            ck.material(full, new, ramp, src["ramp_shift"] if shift is None else shift, src["smoothness"],
                        **src["opts"])
            mapping[bp + orig] = new
        full.bake_shading(**BAKE)
        for p in full.parts:                              # voorbeeld met de herkleuring, voor controle/renders
            if p.mesh is not None:
                p.mesh.M = [mapping.get(m, m) for m in p.mesh.M]
        full.write_glb(os.path.join(check_dir, f"{type_name}_{look}.glb"))
        for node, *_ in parts:
            exp.add(full.get(node))
        for name in set(mapping.values()) | {P + k for k in mats}:
            exp.materials[name] = full.materials[name]
        spec["looks"].append({"name": look, "hide": hide, "add": [n for n, *_ in parts],
                              "materials": [{"from": k, "to": v} for k, v in mapping.items()]})
        print(type_name, look, "klaar")
    fbx_rel = f"Assets/Art/Models/Characters/{asset}/{asset}.fbx"
    os.makedirs(os.path.join(ck.ROOT, os.path.dirname(fbx_rel)), exist_ok=True)
    exp.write_fbx_ascii(os.path.join(ck.ROOT, fbx_rel))
    exp.write_glb(os.path.join(ck.ROOT, "ArtSource", "Exports", asset + ".glb"))
    ck.unity_assets.export_unity(ck.ROOT, exp, fbx_rel, f"Assets/Art/Materials/Characters/{asset}")
    with open(os.path.join(ck.ROOT, "ArtSource", "Exports", asset + ".looks.json"), "w", encoding="utf-8") as f:
        json.dump(spec, f, indent=2)
    print(asset, "stats", exp.stats())


def main():
    for t in sys.argv[1:] or list(TYPES):
        build_type(t)


if __name__ == "__main__":
    main()
