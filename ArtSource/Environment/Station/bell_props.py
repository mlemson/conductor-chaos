"""Vertrekbel op het perron (dienstregeling): een messing tafelbel op een gietijzeren paal.

Twee losse modellen, omdat de bel in het spel los van de paal omlaag veert als je erop drukt
(ServiceRun.DepressBell verplaatst de knoop 'Vertrekbel' 5,5 cm):

  Service_BellPost   paal met voet, sierringen, belbordje aan twee kanten en een kom waar de bel in veert.
                     Pivot = midden van de paal (0,39 m boven het perron), gelijk aan de oude blokpaal (0,14 x 0,78).
  Service_Bell       bel met drukknop op een veerstift. Pivot = midden van het drukvlak van ServiceRun
                     (BoxCollider 0,28 x 0,20 x 0,28): de bovenkant van de knop ligt op +0,10.

Geen Col_-knopen: de botsvormen staan al op de knopen in de scène (Belpaal / Vertrekbel).
"""
import math

import numpy as np

from station_common import ball, cyl, mk, rbox

SEG = 12
POST_H = 0.78          # hoogte van de oude blokpaal; de bel hangt 0,52 boven het midden daarvan
BELL_ABOVE_POST = 0.52


def bell():
    plate = mk.lathe([(0.0, -0.100), (0.128, -0.100), (0.136, -0.090), (0.128, -0.076), (0.0, -0.076)], SEG, mat="M_Stn_Dark")
    dome = mk.lathe([(0.118, -0.088), (0.124, -0.060), (0.116, -0.022), (0.096, 0.016), (0.062, 0.044),
                     (0.026, 0.056), (0.0, 0.058)], SEG, mat="M_Stn_Yellow")
    lip = mk.lathe([(0.112, -0.084), (0.134, -0.070), (0.134, -0.056), (0.112, -0.046)], SEG, mat="M_Stn_YellowDark")
    pin = cyl(0.016, 0.05, 8, 0.0, "M_Stn_Dark").transform(t=(0, 0.062, 0))            # 0,037 .. 0,087
    knob = mk.lathe([(0.0, 0.072), (0.040, 0.072), (0.046, 0.082), (0.040, 0.094), (0.022, 0.100), (0.0, 0.100)],
                    SEG, mat="M_Stn_Red")
    stem = cyl(0.034, 0.16, 8, 0.0, "M_Stn_Metal").transform(t=(0, -0.168, 0))        # veerstift, zakt in de kom
    return mk.merge(plate, dome, lip, pin, knob, stem)


def _bell_icon(z_front):
    """Klein geel belletje (silhouet) dat 8 mm uit het bordje steekt; z_front = voorvlak van het bordje."""
    outline = [(-0.046, -0.030), (0.046, -0.030), (0.034, -0.016), (0.030, 0.018), (0.016, 0.038), (-0.016, 0.038),
               (-0.030, 0.018), (-0.034, -0.016)]
    body = mk.prism(outline, z_front - 0.006, z_front + 0.008, c=0.004, mat="M_Stn_Yellow")
    clapper = ball(0.012, "M_Stn_Yellow", 8, 5).transform(t=(0, -0.038, z_front + 0.002))
    cap = ball(0.009, "M_Stn_Yellow", 8, 5).transform(t=(0, 0.043, z_front + 0.002))
    return mk.merge(body, clapper, cap)


def bell_post():
    y0 = -POST_H / 2
    parts = [
        mk.lathe([(0.135, y0), (0.135, y0 + 0.045), (0.098, y0 + 0.085), (0.0, y0 + 0.085)], 8, mat="M_Stn_Dark",
                 phase=math.pi / 8),                                                       # achtkante voet, onder open
        mk.lathe([(0.056, y0 + 0.070), (0.044, 0.215)], SEG, mat="M_Stn_Navy"),            # taps toelopende paal
        mk.lathe([(0.050, y0 + 0.105), (0.070, y0 + 0.118), (0.070, y0 + 0.140), (0.050, y0 + 0.153)], SEG,
                 mat="M_Stn_Yellow"),                                                      # sierring onder
        mk.lathe([(0.040, 0.150), (0.060, 0.162), (0.060, 0.182), (0.040, 0.194)], SEG, mat="M_Stn_Yellow"),
        mk.lathe([(0.040, 0.205), (0.104, 0.262), (0.122, 0.330), (0.108, 0.360), (0.0, 0.360)], SEG,
                 mat="M_Stn_Dark"),                                                        # kom onder de bel
    ]
    sign_y = (-0.060, 0.090)
    for s in (1, -1):                                                                      # bordje aan beide kanten
        side = [rbox(-0.085, 0.085, sign_y[0], sign_y[1], 0.030, 0.064, 0.012, "M_Stn_Navy"),
                rbox(-0.071, 0.071, sign_y[0] + 0.014, sign_y[1] - 0.014, 0.058, 0.072, 0.006, "M_Stn_Cream"),
                _bell_icon(0.072).transform(t=(0, 0.012, 0))]
        m = mk.merge(*side)
        parts.append(m if s == 1 else m.transform(R=mk.rot_y(math.pi)))
    return mk.merge(*parts)


def bell_nodes():
    return [("Mesh", bell(), None, (0, 0, 0), None, True)]


def bell_post_nodes():
    return [("Mesh", bell_post(), None, (0, 0, 0), None, True)]


ASSETS = {"Service_Bell": (bell_nodes, 2600, 0.06), "Service_BellPost": (bell_post_nodes, 1400, 0.10)}
