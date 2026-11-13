"""Bestuurderslessenaars voor de treinsets — naar de conceptbladen 'Cabine jaren 60', 'Cabine jaren 2020' en
'Cabine jaren 2040' van @mlemson.

Elke lessenaar is één vast blok (`CabDesk`: romp met schuin blad en een opstaand instrumentenbord) met daarop losse,
bedienbare knopen. Alle losse knopen staan in het vlak van hun paneel (lokale Y = uit het paneel, lokale X = van de
bestuurder af, lokale Z = naar rechts), zodat je ze in Unity simpel kunt animeren:

  ThrottleLever   brede, lage rijhendel; kantelt om lokale Z (naar voren = vooruit / gas, naar achteren = achteruit / rem)
  SpeedNeedle     wijzer van de snelheidsmeter; draait om lokale Y
  Button_*        grote drukknoppen; indrukken = 2 cm langs lokale -Y
  Switch_*        tuimelschakelaars (alleen jaren 60); kantelen om lokale Z
  Lamp_Green/Yellow/Red   indicatielampjes (materiaal wisselen of aan/uit zetten)

Knoppen per stijl:
  60s   Button_Horn (geel), Button_Alarm (rood), Switch_Lights / Switch_Wiper / Switch_Fan
  2020  Button_Start (groen), Button_Stop (rood), Button_Doors (crème, twee deurbladen met pijltjes — het
        pauzesymbool uit het blad is vervangen omdat het nergens op sloeg), Button_Horn (geel),
        Button_Lights en Button_Wiper (rond)
  2040  Button_Horn (groen), Button_Lights (geel), Button_Stop (rood), Button_Direction (blauw, heen-en-weer)

Geen tekst of cijfers op de modellen (er zijn geen texturen): schalen zijn streepjes, pictogrammen zijn kleine
verhoogde vormen. Labels horen in de UI.
"""
import math

import numpy as np

import train_kit as tk
from train_kit import mk, box, ZC, FLOOR, WALL

# materiaal per onderdeel, per stijl (namen uit de materiaalset van het voertuig)
STYLES = {
    "60s": dict(body="Body", rim="Cream", top="Panel", dark="Wheel", dial="Cream", arm="Cream", bar="Body", cap="Body",
                strip=None, glyph="Cream", glow=None),
    "2020": dict(body="Cream", rim=None, top="BodyLow", dark="Wheel", dial="Wheel", arm="Wheel", bar="BodyLow", cap="Interact",
                 strip=None, glyph="Cream", glow="Lamp"),
    "2040": dict(body="BodyLow", rim=None, top="Wheel", dark="Wheel", dial="Wheel", arm="Wheel", bar="Interact", cap="Interact",
                 strip="GlowY", glyph="Glow", glow="GlowY"),
}
H_TOP, H_SUB = 0.024, 0.038          # bovenkant van het bovenblad en van een verdiept/verhoogd paneel erop


class Surface:
    """Een hellend vlak van de lessenaar: plaatst onderdelen in (s = langs het vlak omhoog, u = naar rechts, h = eruit)."""

    def __init__(self, origin, angle):
        self.o, self.a = origin, angle
        self.R = mk.rot_z(angle)

    def at(self, s, u, h=0.0):
        c, sn = math.cos(self.a), math.sin(self.a)
        return (self.o[0] + s * c - h * sn, self.o[1] + s * sn + h * c, ZC + u)

    def put(self, mesh, s=0.0, u=0.0, h=0.0):
        return mesh.transform(R=self.R, t=self.at(s, u, h))

    def plate(self, s0, s1, u0, u1, h0, h1, mat, r=0.012, div=1):
        return self.put(box(s0, s1, h0, h1, u0, u1, r, mat, div=div))


def glyph(kind, size, mat, h=0.011):
    """Pictogram als kleine verhoogde vorm (lokale X = omhoog op de knop, Z = naar rechts)."""
    a = size / 2

    def bar(x0, x1, z0, z1):
        return box(x0, x1, 0.0, h, z0, z1, 0.0015, mat)

    def tri(pts):
        V = np.array([[x, h, z] for (x, z) in pts] + [[x, 0.0, z] for (x, z) in pts], float)
        n = len(pts)
        cen = V[:n].mean(0)
        top = list(range(n))
        if np.cross(V[1] - V[0], V[2] - V[0])[1] < 0:      # bovenvlak moet omhoog wijzen
            top.reverse()
        F = [tuple(top)]
        for i in range(n):
            q = [i, (i + 1) % n, n + (i + 1) % n, n + i]
            out = (V[q[0]] + V[q[1]]) / 2 - cen
            if np.dot(np.cross(V[q[1]] - V[q[0]], V[q[3]] - V[q[0]]), out) < 0:
                q.reverse()
            F.append(tuple(q))
        m = mk.Mesh(V, F, [mat] * len(F))
        m.sharp = True
        return m
    if kind == "go":            # pijl naar voren
        return tri([(a, 0.0), (-a * 0.7, -a * 0.8), (-a * 0.7, a * 0.8)])
    if kind == "stop":
        return bar(-a * 0.7, a * 0.7, -a * 0.7, a * 0.7)
    if kind == "doors":         # twee deurbladen die uit elkaar schuiven
        return mk.merge(bar(-a, a, -a * 0.55, -a * 0.10), bar(-a, a, a * 0.10, a * 0.55),
                        tri([(0.0, -a * 1.25), (a * 0.35, -a * 0.75), (-a * 0.35, -a * 0.75)]),
                        tri([(0.0, a * 1.25), (-a * 0.35, a * 0.75), (a * 0.35, a * 0.75)]))
    if kind == "horn":          # toeter
        return mk.merge(tri([(-a * 0.5, -a), (a * 0.5, -a), (a * 0.9, a), (-a * 0.9, a)]), bar(-a * 0.25, a * 0.25, -a * 1.35, -a))
    if kind == "alarm":         # zwaailicht
        return mk.merge(bar(-a, -a * 0.6, -a * 0.9, a * 0.9), tri([(-a * 0.6, -a * 0.6), (a * 0.5, -a * 0.35), (a * 0.5, a * 0.35), (-a * 0.6, a * 0.6)]),
                        bar(a * 0.7, a * 1.05, -a * 0.08, a * 0.08))
    if kind == "lights":        # koplamp met stralen
        return mk.merge(tri([(-a * 0.8, a * 0.1), (-a * 0.5, a * 0.8), (a * 0.5, a * 0.8), (a * 0.8, a * 0.1)]),
                        *[bar(x - a * 0.09, x + a * 0.09, -a, -a * 0.15) for x in (-a * 0.6, 0.0, a * 0.6)])
    if kind == "wiper":
        return mk.merge(bar(-a * 0.8, -a * 0.55, -a, a), bar(-a * 0.55, a * 0.9, -a * 0.09, a * 0.09).transform(R=mk.rot_y(0.5)))
    if kind == "fan":
        return mk.merge(bar(-a, a, -a * 0.16, a * 0.16), bar(-a * 0.16, a * 0.16, -a, a))
    if kind == "swap":          # heen-en-weer
        return mk.merge(bar(a * 0.2, a * 0.5, -a * 0.6, a * 0.5), tri([(a * 0.35, a * 1.0), (a * 0.8, a * 0.45), (-a * 0.1, a * 0.45)]),
                        bar(-a * 0.5, -a * 0.2, -a * 0.5, a * 0.6), tri([(-a * 0.35, -a * 1.0), (-a * 0.8, -a * 0.45), (a * 0.1, -a * 0.45)]))
    if kind == "up":
        return tri([(a, 0.0), (-a, -a), (-a, a)])
    if kind == "down":
        return tri([(-a, 0.0), (a, a), (a, -a)])
    return bar(-a * 0.25, a * 0.25, -a, a)      # streepje


def add_console(car, style, xw=None):
    S = STYLES[style]
    m = car.m
    xw = car.L / 2 - WALL - 0.01 if xw is None else xw
    half = 0.86
    desk = Surface((xw - 0.60, 0.84), math.atan2(0.06, 0.36))       # schuin blad
    face = Surface((xw - 0.24, 0.90), math.atan2(0.34, 0.18))       # instrumentenbord
    ld, lf = 0.365, 0.385
    prof = [(xw - 0.62, FLOOR + 0.02), (xw, FLOOR + 0.02), (xw, 1.24), (xw - 0.06, 1.24), (xw - 0.24, 0.90), (xw - 0.60, 0.84),
            (xw - 0.62, 0.80)]
    static = [mk.prism(mk.chamfer_poly(prof, 0.012), ZC - half, ZC + half, 0.02, mat=m(S["body"]))]
    for (sf, ln) in ((desk, ld), (face, lf)):
        if S["rim"]:
            static.append(sf.plate(-0.015, ln + 0.01, -half - 0.02, half + 0.02, -0.03, 0.010, m(S["rim"]), 0.01))
        a0, a1 = (0.02, ln - 0.005) if sf is desk else (0.03, ln - 0.03)
        static.append(sf.plate(a0, a1, -half + 0.05, half - 0.05, -0.015, H_TOP, m(S["top"])))
    if S["glow"] and style == "2040":           # lichtranden langs de zijkanten
        for u in (-half - 0.004, half + 0.004):
            static.append(desk.plate(0.02, ld - 0.02, u - 0.012, u + 0.012, -0.02, 0.016, m(S["glow"]), 0.004))
    parts = []

    def part(name, mesh, sf, s, u, h, extra=None):
        parts.append((name, mesh, sf.at(s, u, h), sf.R if extra is None else sf.R @ extra))

    def button(name, sf, s, u, mat, icon, size=0.15, round_=False, icon_mat=None):
        b = size / 2
        static.append(sf.plate(s - b - 0.02, s + b + 0.02, u - b - 0.02, u + b + 0.02, -0.001, H_SUB, m(S["dark"] if style != "60s" else "Cream")))
        if round_:
            cap = mk.cylinder(b, 0.04, 12, 0.012, mat=m(mat), axis="y").transform(t=(0, 0.02, 0))
        else:
            cap = box(-b, b, 0.0, 0.045, -b, b, 0.03, m(mat), div=2)
        top = 0.04 if round_ else 0.045
        ic = glyph(icon, size * 0.5, m(icon_mat or "Wheel")).transform(t=(0, top - 0.0005, 0))
        part(name, mk.merge(cap, ic), sf, s, u, H_SUB)

    def lever(s, u, posts, length, right=0.16):
        arms = [box(-0.035, 0.035, 0.0, 0.17, p - 0.03, p + 0.03, 0.012, m(S["arm"])) for p in posts]
        bar = mk.cylinder(0.055, length, 12, 0.012, mat=m(S["bar"]), axis="z").transform(t=(0, 0.185, 0))
        caps = [mk.cylinder(0.062, 0.05, 12, 0.014, mat=m(S["cap"]), axis="z").transform(t=(0, 0.185, sg * (length / 2 - 0.01))) for sg in (-1, 1)]
        bits = arms + [bar] + caps
        for x in bits:
            x.sharp = True
        if S["strip"]:
            bits.append(box(-0.012, 0.012, 0.236, 0.246, -length / 2 + 0.07, length / 2 - 0.07, 0.004, m(S["strip"])))
        static.append(desk.plate(s - 0.12, s + 0.12, u - length / 2 - 0.10, u + length / 2 + right, -0.001, H_SUB, m(S["dark"])))
        static.append(desk.plate(s - 0.10, s + 0.10, u - max(posts) - 0.06, u + max(posts) + 0.06, 0.020, H_SUB + 0.014, m(S["arm"]), 0.01))
        part("ThrottleLever", mk.merge(*bits), desk, s, u, H_SUB + 0.012)

    def gauge(s, u, r, needle=None, a0=2.3, ticks=9):
        bez = mk.cylinder(r, 0.03, 16, 0.008, mat=m("Trim" if style == "60s" else S["dark"]), axis="y").transform(t=(0, 0.015, 0))
        dial = mk.cylinder(r - 0.018, 0.03, 16, 0.0, mat=m(S["dial"]), axis="y").transform(t=(0, 0.021, 0))
        bez.sharp = dial.sharp = True
        bits = [bez, dial]
        for k in range(ticks):                  # schaalstreepjes in een boog
            a = -2.2 + 4.4 * k / (ticks - 1)
            col = m(S["glyph"]) if style != "60s" else m("Wheel")
            if style == "2040":
                col = m("Go") if k < ticks * 0.6 else (m("Lamp") if k < ticks * 0.85 else m("Hot"))
            t = box(r * 0.60, r * 0.80, 0.034, 0.047, -r * 0.045, r * 0.045, 0.001, col)
            bits.append(t.transform(R=mk.rot_y(a)))
        static.append(face.put(mk.merge(*bits), s, u, H_TOP))
        ndl = mk.merge(box(-r * 0.12, r * 0.62, 0.0, 0.008, -r * 0.045, r * 0.045, 0.002, m("Hot")),
                       mk.cylinder(r * 0.13, 0.014, 10, 0.003, mat=m("Trim"), axis="y").transform(t=(0, 0.007, 0)))
        if needle:
            part(needle, ndl, face, s, u, H_TOP + 0.049, mk.rot_y(a0))
        else:
            static.append(face.put(ndl.transform(R=mk.rot_y(a0 - 1.2)), s, u, H_TOP + 0.049))

    def lamps(sf, pts, k=1.0):
        for (name, mat, s, u) in pts:
            static.append(sf.put(mk.cylinder(0.045 * k, 0.02, 10, 0.006, mat=m("Trim"), axis="y").transform(t=(0, 0.01, 0)), s, u, H_TOP))
            dome = mk.ellipsoid(0.034 * k, 0.022, 0.034 * k, 10, 5, mat=m(mat))
            part(name, dome, sf, s, u, H_TOP + 0.02)

    def screen(s0, s1, u0, u1):
        static.append(face.plate(s0, s1, u0, u1, -0.001, H_SUB, m(S["dark"]), 0.02, div=2))

    if style == "60s":
        gauge(0.20, -0.58, 0.085)
        gauge(0.20, -0.36, 0.085)
        gauge(0.205, 0.0, 0.155, needle="SpeedNeedle")
        lamps(face, [("Lamp_Green", "Go", 0.20, 0.30), ("Lamp_Yellow", "Lamp", 0.20, 0.43), ("Lamp_Red", "Hot", 0.20, 0.56)])
        static.append(face.plate(0.13, 0.27, 0.64, 0.78, -0.001, H_SUB, m(S["dark"]), 0.02))        # luidspreker
        static += [face.plate(0.15 + 0.035 * k, 0.165 + 0.035 * k, 0.66, 0.76, 0.03, H_SUB + 0.011, m("Trim"), 0.003) for k in range(3)]
        button("Button_Horn", desk, 0.25, -0.60, "Interact", "horn")
        button("Button_Alarm", desk, 0.25, -0.36, "Hot", "alarm", icon_mat="Cream")
        static.append(desk.plate(0.045, 0.14, -0.74, -0.22, -0.001, H_SUB, m(S["dark"])))           # schakelaars
        for (name, u, icon) in (("Switch_Lights", -0.63, "lights"), ("Switch_Wiper", -0.48, "wiper"), ("Switch_Fan", -0.33, "fan")):
            sw = mk.merge(mk.cylinder(0.022, 0.085, 8, 0.008, mat=m("Cream"), axis="y").transform(t=(0, 0.04, 0)),
                          mk.cylinder(0.032, 0.012, 8, 0.004, mat=m("Trim"), axis="y").transform(t=(0, 0.004, 0)))
            part(name, sw, desk, 0.108, u, H_SUB, mk.rot_z(-0.30))
            static.append(desk.put(glyph(icon, 0.04, m("Cream")), 0.066, u, H_SUB - 0.0005))
        lever(0.185, 0.24, (-0.10, 0.10), 0.34)
        for (icon, s) in (("up", 0.265), ("dash", 0.185), ("down", 0.105)):
            static.append(desk.put(glyph(icon, 0.05, m("Cream")), s, 0.54, H_SUB - 0.0005))
        # microfoon op een voetje met krulsnoer
        mic = [mk.cylinder(0.05, 0.02, 10, 0.006, mat=m("Trim"), axis="y").transform(t=(0, 0.01, 0)),
               tk.tube((0, 0.01, 0), (0, 0.16, 0), 0.016, m("Trim")),
               box(-0.045, 0.045, 0.14, 0.27, -0.055, 0.055, 0.03, m("Metal"), div=2)]
        mic += [box(-0.05, -0.04, 0.165 + 0.03 * k, 0.18 + 0.03 * k, -0.04, 0.04, 0.002, m("Wheel")) for k in range(3)]
        mic += tk.pipe_run([(0.03, 0.02, 0.04), (0.06, 0.05, 0.08), (0.03, 0.08, 0.11), (0.06, 0.11, 0.08), (0.04, 0.15, 0.05)], 0.011, m("Wheel"))
        static.append(desk.put(mk.merge(*mic), 0.20, 0.75, H_TOP))
    elif style == "2020":
        screen(0.07, 0.31, -0.70, -0.40)                                                           # treininfo
        static.append(face.put(mk.merge(box(-0.05, 0.05, 0.0, 0.011, -0.11, -0.03, 0.002, m("Cream")),
                                        *[box(0.03 - 0.035 * k, 0.045 - 0.035 * k, 0.0, 0.011, 0.0, 0.11, 0.001, m("Glow" if "Glow" in car.sc.materials else "Cream")) for k in range(3)]),
                               0.19, -0.55, H_SUB - 0.0005))
        screen(0.05, 0.34, -0.30, 0.30)                                                            # snelheid
        gauge(0.165, 0.0, 0.135, needle="SpeedNeedle", ticks=11)
        screen(0.07, 0.31, 0.40, 0.72)                                                             # status
        for k, (name, mat) in enumerate((("Lamp_Green", "Go"), ("Lamp_Yellow", "Lamp"), ("Lamp_Red", "Hot"))):
            s = 0.26 - 0.07 * k
            part(name, mk.ellipsoid(0.026, 0.016, 0.026, 10, 5, mat=m(mat)), face, s, 0.47, H_SUB)
            static.append(face.plate(s - 0.01, s + 0.01, 0.53, 0.66, 0.03, H_SUB + 0.011, m("Cream"), 0.002))
        button("Button_Start", desk, 0.27, -0.62, "Go", "go", size=0.12)
        button("Button_Stop", desk, 0.27, -0.40, "Door", "stop", size=0.12, icon_mat="Cream")
        button("Button_Doors", desk, 0.11, -0.62, "Cream", "doors", size=0.12)
        button("Button_Horn", desk, 0.11, -0.40, "Interact", "horn", size=0.12)
        lever(0.185, 0.10, (0.0,), 0.36)
        static += [desk.plate(0.085 + 0.036 * k, 0.103 + 0.036 * k, 0.33, 0.39, 0.03, H_SUB + 0.011, m("Interact"), 0.002) for k in range(6)]
        button("Button_Lights", desk, 0.27, 0.66, "Cream", "lights", size=0.12, round_=True)
        button("Button_Wiper", desk, 0.11, 0.66, "Cream", "wiper", size=0.12, round_=True)
        for u in (-0.80, 0.80):                                                                    # lichtbalkjes
            static.append(face.plate(0.30, 0.325, u - 0.05, u + 0.05, 0.0, H_TOP + 0.008, m("Lamp"), 0.004))
    else:
        screen(0.08, 0.31, -0.72, -0.38)                                                           # accu + trein
        static += [face.plate(0.21, 0.27, -0.66 + 0.055 * k, -0.62 + 0.055 * k, 0.03, H_SUB + 0.011, m("Go"), 0.002) for k in range(5)]
        static.append(face.plate(0.12, 0.16, -0.66, -0.44, 0.03, H_SUB + 0.011, m("Glow"), 0.004))
        screen(0.04, 0.36, -0.30, 0.30)                                                            # snelheidsboog
        gauge(0.165, 0.0, 0.15, needle="SpeedNeedle", ticks=13)
        screen(0.08, 0.31, 0.38, 0.72)                                                             # volgende halte
        static.append(face.plate(0.215, 0.235, 0.44, 0.66, 0.03, H_SUB + 0.011, m("Glow"), 0.002))
        static += [face.put(mk.cylinder(0.022, 0.012, 8, 0.002, mat=m("Glow"), axis="y"), 0.225, u, H_SUB + 0.017) for u in (0.44, 0.55, 0.66)]
        static.append(face.plate(0.13, 0.16, 0.46, 0.64, 0.03, H_SUB + 0.011, m("GlowY"), 0.004))
        lamps(desk, [("Lamp_Green", "Go", 0.325, -0.09), ("Lamp_Yellow", "Lamp", 0.325, 0.0), ("Lamp_Red", "Hot", 0.325, 0.09)], k=0.62)
        button("Button_Horn", desk, 0.20, -0.66, "Go", "horn", size=0.17)
        button("Button_Lights", desk, 0.20, -0.43, "Interact", "lights", size=0.17)
        lever(0.165, 0.0, (-0.10, 0.10), 0.40, right=0.10)
        for sg in (-1, 1):
            static += [desk.plate(0.085 + 0.04 * k, 0.103 + 0.04 * k, sg * 0.262 - 0.022, sg * 0.262 + 0.022, 0.03, H_SUB + 0.011, m("Go"), 0.002) for k in range(4)]
        button("Button_Stop", desk, 0.20, 0.43, "Door", "stop", size=0.17, icon_mat="Cream")
        button("Button_Direction", desk, 0.20, 0.66, "Glow", "swap", size=0.17, icon_mat="Cream")
    car.add("CabDesk", mk.merge(*static))
    for (name, mesh, pivot, R) in parts:
        mesh.sharp = True
        car.add(name, mesh, pivot, rotation=R)
