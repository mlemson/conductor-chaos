"""Gereedschap van de conducteur (issues #215, #216): moersleutel, kaartjestang en een treinkaartje.

Alle drie liggen in het xy-vlak (lengte langs +x, dikte langs z), gecentreerd rond de oorsprong.
  * Prop_Wrench       grote cartoonsleutel; bek aan +x (knoop Wrench_Head), ringkant aan -x, gele greep
  * Prop_TicketPunch  kniptang; bek met kniptapje aan +x (knoop Punch_Jaw), marine handvatten aan -x
  * Prop_Ticket       crème kaartje met geel streepje en marine strook; Ticket_Hole = het geknipte gaatje (verborgen)
Sleutel en tang zijn beweegbare netwerkprops (StationKitEditor: MovableMasses) en worden in Unity met de gewone
handgrip opgepakt; het kaartje is alleen een model dat reizigers in hun hand houden.
"""
import math

import numpy as np

from station_common import capsule, col_box, mk, rbox


def wrench():
    L = 0.62
    xc, r = L / 2 - 0.075, 0.075                      # middelpunt en straal van de open bek
    a0 = math.asin(0.032 / r)
    pts = [(xc + r * math.cos(a), r * math.sin(a)) for a in np.linspace(a0, 2 * math.pi - a0, 22)]
    pts += [(xc + 0.005, -0.032), (xc + 0.005, 0.032)]   # bekopening naar +x
    head = mk.prism(pts, -0.026, 0.026, 0.008, mat="M_Stn_Metal")
    shaft = rbox(-L / 2 + 0.07, xc - 0.04, -0.022, 0.022, -0.012, 0.012, 0.006, "M_Stn_Metal")
    ring = mk.torus(0.048, 0.022, 16, 6, mat="M_Stn_Metal").transform(R=mk.rot_x(math.pi / 2), t=(-L / 2 + 0.05, 0, 0))
    grip = capsule((-L / 2 + 0.13, 0, 0), (0.09, 0, 0), 0.032, seg=12, rings=3, mat="M_Stn_Yellow")
    mesh = mk.merge(head, shaft, ring, grip)
    col = col_box(-L / 2 - 0.005, L / 2 + 0.005, -0.08, 0.08, -0.035, 0.035)
    return mesh, col, [("Wrench_Head", None, None, (xc, 0.0, 0.0), None, True)]


def ticket_punch():
    parts = []
    for s in (1, -1):
        parts.append(capsule((-0.17, 0.05 * s, 0), (-0.01, 0.018 * s, 0), 0.017, 0.015, 10, 3, "M_Stn_Navy"))
        parts.append(capsule((-0.005, 0.017 * s, 0), (0.06, 0.013 * s, 0), 0.010, seg=8, rings=2, mat="M_Stn_Metal"))
        parts.append(rbox(0.055, 0.155, 0.009 * s if s > 0 else -0.032, 0.032 if s > 0 else -0.009, -0.014, 0.014,
                          0.006, "M_Stn_Metal"))
    rivet = mk.cylinder(0.021, 0.036, 12, 0.006, mat="M_Stn_Yellow", axis="z").transform(t=(0.055, 0, 0))
    pin = mk.cylinder(0.008, 0.012, 8, 0.002, mat="M_Stn_Dark", axis="y").transform(t=(0.135, 0.004, 0))
    parts += [rivet, pin]
    mesh = mk.merge(*parts)
    col = col_box(-0.19, 0.16, -0.07, 0.07, -0.025, 0.025)
    return mesh, col, [("Punch_Jaw", None, None, (0.13, 0.0, 0.0), None, True)]


def ticket():
    w, h, t = 0.14, 0.075, 0.003
    x0, x1, x2, x3 = -w / 2, 0.018, 0.028, w / 2
    body = rbox(x0, x1, -h / 2, h / 2, -t, t, 0.002, "M_Stn_Cream")
    stripe = rbox(x1, x2, -h / 2, h / 2, -t, t, 0.0, "M_Stn_Yellow")
    stub = rbox(x2, x3, -h / 2, h / 2, -t, t, 0.002, "M_Stn_Navy")
    hole = mk.cylinder(0.0095, 0.010, 10, 0.0, mat="M_Stn_Dark", axis="z").transform(t=(-0.035, 0.012, 0))
    return mk.merge(body, stripe, stub), None, [("Ticket_Hole", hole.transform(t=(0.035, -0.012, 0)), None,
                                                 (-0.035, 0.012, 0.0), None, False)]
