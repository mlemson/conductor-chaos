"""Bizon (+ kalf) — de kudde die in de woestijnlevels over het spoor stormt (concept: Docs/References/Desert/).

Silhouet eerst: een enorme donkere, ruige bult over schouders en kop, een kleinere warmbruine achterhand op vier
stevige poten, een lage brede kop met donkere snuit, een baard, korte crèmekleurige hoorns die opzij en omhoog
krullen en een dun staartje met een pluim. Het kalf is klein en goudbruin, zonder bult, met hoornstompjes en
grote ogen.

De manen zijn IN het oppervlak van romp en kop gesneden (`animal_kit.paint_field`, gekartelde rand); de bult zelf is
een eigen gesloten volume dat in de romp steekt. Geen dubbele vlakken.

Hiërarchie (gelijk aan de koe, zodat PlaygroundAnimal en CharacterFace hem zonder aanpassing aansturen):
  Bison › Body (+ Hump) › Head › (Muzzle, Beard, Tuft, Horn_L/R, Ear_L/R, Eye_L/R › Pupil_L/R, EyeClosed_L/R,
  Blink_L/R, Mouth_Neutral|Surprised|Happy, Fx_Alert), Leg_FL/FR/BL/BR, Tail.
Standaard verborgen: EyeClosed_*, Blink_*, Mouth_Surprised, Mouth_Happy, Fx_Alert.

Kleuren: ramps leather (achterhand), cab_interior (manen, baard), boot_brown (snuit, hoeven), scarf_cream (hoorns),
safari_hat (kalf), skin (binnenoor), accent_yellow (alert). Zie palette.json.
Uitvoer: Assets/Art/Models/Animals/Bison/Bison.fbx en Bison_Calf/Bison_Calf.fbx (+ GLB en .materials.json)
Gebruik:  python ArtSource/Animals/Bison/build_bison.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import animal_kit as ak  # noqa: E402
from animal_kit import ck, mk, DEG  # noqa: E402


class Shape:
    """Maten van één variant (volwassen bizon of kalf), in meters."""

    def __init__(self, calf):
        s = 0.56 if calf else 1.0
        self.calf = calf
        self.s = s
        self.body_c = np.array([0.0, 0.80, -0.22]) * s
        self.body_r = np.array([0.46, 0.44, 0.70]) * s
        self.head_c = np.array([0.0, 0.80, 0.72]) * s
        self.head_r = np.array([0.30, 0.31, 0.27]) * s * (1.18 if calf else 1.0)
        self.neck = np.array([0.0, 0.88, 0.48]) * s
        self.muzzle_c = np.array([0.0, 0.63, 0.92]) * s + (np.array([0, 0.03, 0.02]) if calf else 0)
        self.muzzle_r = np.array([0.205, 0.135, 0.13]) * s * (1.15 if calf else 1.0)
        self.eye = (0.135 * s * 1.1, 0.88 * s, (0.072 if not calf else 0.062))
        self.legs = {"FL": (0.23 * s, 0.20 * s), "FR": (-0.23 * s, 0.20 * s),
                     "BL": (0.22 * s, -0.58 * s), "BR": (-0.22 * s, -0.58 * s)}
        self.leg_r = (0.115 if not calf else 0.10) * s       # stevige poten (concept: kort en dik)


def underside(mesh, x, z, reach):
    """Laagste punt van het lijf boven (x, z): de poot begint daar ruim binnen het lijf (geen kier bij de heup)."""
    V = mesh.V
    near = (V[:, 0] - x) ** 2 + (V[:, 2] - z) ** 2 < reach * reach
    return float(V[near, 1].min())


def materials(sc, P, calf):
    ak.face_materials(sc, P)
    if calf:
        ck.material(sc, P + "Coat", "safari_hat", 0.05, 0.22, paint=1.0, cells=0.35)
        ck.material(sc, P + "Mane", "safari_hat", -0.30, 0.22, paint=1.0, cells=0.5)
    else:
        ck.material(sc, P + "Coat", "leather", 0.20, 0.22, paint=1.0, cells=0.40)
        ck.material(sc, P + "Mane", "cab_interior", 0.05, 0.22, paint=1.0, cells=0.55)
    ck.material(sc, P + "Muzzle", "boot_brown", 0.15 if not calf else 0.35, 0.25, paint=1.0, cells=0.3)
    ck.material(sc, P + "Nostril", "boot_brown", -0.45, 0.25)
    ck.material(sc, P + "EarInner", "skin", -0.05, 0.25)
    ck.material(sc, P + "Horn", "scarf_cream", 0.15, 0.30)
    ck.material(sc, P + "Hoof", "boot_brown", -0.20, 0.25)
    ck.material(sc, P + "Alert", "accent_yellow", 0.0, 0.30, noShadow=True)


def shaggy_edge(V, z0, amp, freq=9.0):
    """Gekartelde grens tussen manen (voor) en vacht (achter): negatief = manen."""
    a = np.arctan2(V[:, 1] - 0.8, V[:, 0])
    return (z0 + amp * np.sin(a * freq) + amp * 0.5 * np.sin(a * freq * 2.3 + 1.0)) - V[:, 2]


def build_body(sh, P):
    body = ak.superellipsoid(*sh.body_r, seg=56, rings=34, mat=P + "Coat", nv=2.5, nh=2.2).transform(t=sh.body_c)
    if sh.calf:                                                  # kalf: effen goudbruin, nog geen manen
        return body
    z0 = sh.body_c[2] + sh.body_r[2] * 0.05
    return ak.paint_field(body, lambda V: shaggy_edge(V, z0, 0.06 * sh.s), P + "Mane")


def build_hump(sh, P):
    """De ruige bult over de schouders (volwassen): gladde massa uit bollen, steekt in romp en kop."""
    s = sh.s
    spheres = [((0.0, 1.13, 0.12), 0.40), ((0.0, 1.20, 0.28), 0.36), ((0.20, 1.03, 0.20), 0.33), ((-0.20, 1.03, 0.20), 0.33),
               ((0.0, 1.05, -0.12), 0.34), ((0.27, 0.86, 0.32), 0.27), ((-0.27, 0.86, 0.32), 0.27), ((0.0, 1.07, 0.48), 0.30),
               ((0.17, 0.97, 0.50), 0.25), ((-0.17, 0.97, 0.50), 0.25)]
    hump = ck.blob([(np.array(p) * s, r * s) for p, r in spheres], P + "Mane", spacing=0.048 * s, k=0.035 * s)
    rng = np.random.default_rng(7)
    V = hump.V.copy()
    n = V - np.array([0, 1.0, 0.2]) * s
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-6)
    # ruige plukken: grove ruis naar buiten (alleen bovenkant/zijkant)
    shag = (np.sin(V[:, 0] * 23 / s) * np.sin(V[:, 2] * 19 / s + 1.3) * np.sin(V[:, 1] * 17 / s)) * 0.022 * s
    V += n * shag[:, None]
    hump.V = V
    br = sh.body_r - 0.02 * s

    def in_body(Q):
        return (np.abs((Q - sh.body_c) / br) ** 2.3).sum(1) < 1.0
    return ak.cull_inside(hump, in_body)


def build_chest(sh, P):
    """Ruige borstfranje onder de hals, tussen de voorpoten (volwassen)."""
    s = sh.s
    c = [((0.0, 0.56, 0.30), 0.22), ((0.0, 0.44, 0.28), 0.16), ((0.12, 0.50, 0.24), 0.15), ((-0.12, 0.50, 0.24), 0.15),
         ((0.0, 0.36, 0.25), 0.10)]
    chest = ck.blob([(np.array(p) * s, r * s) for p, r in c], P + "Mane", spacing=0.04 * s, k=0.03 * s)
    br = sh.body_r - 0.02 * s

    def in_body(Q):
        return (np.abs((Q - sh.body_c) / br) ** 2.3).sum(1) < 1.0
    return ak.cull_inside(chest, in_body)


def build_leg(sh, P, front, h):
    """Poot (lokaal: oorsprong = heup, hoef naar -Y, lengte h) met een ruige 'broek' (voor) of dij (achter) bovenaan.
    De bovenkant zit diep in het lijf, zodat er bij het lopen nooit een kier ontstaat."""
    s = sh.s
    r = sh.leg_r * (1.0 if front else 0.92)
    leg = ak.leg(r, r * 1.06, 0.11 * s, h, P + ("Mane" if front else "Coat"), P + "Hoof", seg=22)
    if front and not sh.calf:
        c = [((0.0, -0.10 * h, 0.0), 0.165), ((0.0, -0.30 * h, 0.01), 0.15), ((0.0, -0.46 * h, 0.02), 0.13)]
        mat = P + "Mane"
    else:
        c = [((0.0, -0.08 * h, -0.01), 0.17), ((0.0, -0.26 * h, -0.015), 0.14), ((0.0, -0.40 * h, -0.01), 0.11)]
        mat = P + "Coat"
    fur = ck.blob([(np.array(p) * np.array([s, 1.0, s]), rr * s) for p, rr in c], mat, spacing=0.034 * s, k=0.025 * s)
    if front and not sh.calf:                                   # ruige onderrand van de broek
        V = fur.V.copy()
        a = np.arctan2(V[:, 2], V[:, 0])
        low = V[:, 1] < -0.50 * h
        V[low, 1] -= (0.03 * s) * (0.5 + 0.5 * np.sin(a[low] * 7.0))
        fur.V = V
    rr = r - 0.004

    def in_leg(Q):
        return (Q[:, 0] ** 2 + Q[:, 2] ** 2 < rr * rr) & (Q[:, 1] < -0.04) & (Q[:, 1] > -h + 0.12 * s)
    return mk.merge(leg, ak.cull_inside(fur, in_leg))


def build_head(sh, P):
    # volwassen: donkere manenkop; kalf: goudbruine kop
    return ak.superellipsoid(*sh.head_r, seg=52, rings=30, mat=P + ("Coat" if sh.calf else "Mane"), nv=2.4,
                             nh=2.3).transform(t=sh.head_c)


def build_muzzle(sh, P):
    mz = ak.superellipsoid(*sh.muzzle_r, seg=56, rings=28, mat=P + "Muzzle", nv=2.5, nh=2.6).transform(t=sh.muzzle_c)

    def nostrils(V):
        d = np.full(len(V), 1e9)
        for side in (1, -1):
            c = np.array([side * 0.085 * sh.s, sh.muzzle_c[1] + 0.035 * sh.s, sh.muzzle_c[2] + sh.muzzle_r[2]])
            q = (V - c) / (np.array([0.02, 0.032, 0.08]) * sh.s)
            d = np.minimum(d, np.linalg.norm(q, axis=1) - 1.0)
        return d
    return ak.paint_field(mz, nostrils, P + "Nostril")


def build_tuft(sh, P):
    """Pony van ruig haar tussen de hoorns, over het voorhoofd."""
    s = sh.s
    c = [((0.0, 1.06, 0.74), 0.16), ((-0.11, 1.04, 0.72), 0.12), ((0.11, 1.04, 0.73), 0.12), ((0.0, 1.00, 0.86), 0.12),
         ((0.06, 1.10, 0.66), 0.10), ((-0.07, 1.09, 0.64), 0.10)]
    if sh.calf:
        c = [((0.0, 1.06, 0.73), 0.13), ((-0.07, 1.04, 0.70), 0.09), ((0.07, 1.04, 0.71), 0.09), ((0.0, 1.0, 0.82), 0.09)]
    tuft = ck.blob([(np.array(p) * s, r * s) for p, r in c], P + "Mane", spacing=0.026 * s, k=0.018 * s)
    hr = sh.head_r - 0.015 * s

    def in_head(V):
        return (np.abs((V - sh.head_c) / hr) ** 2.3).sum(1) < 1.0
    return ak.cull_inside(tuft, in_head)


def build_beard(sh, P):
    s = sh.s
    c = [((0.0, 0.52, 0.78), 0.13), ((0.0, 0.42, 0.76), 0.10), ((0.0, 0.34, 0.74), 0.07), ((0.06, 0.48, 0.74), 0.09),
         ((-0.06, 0.48, 0.74), 0.09)]
    beard = ck.blob([(np.array(p) * s, r * s) for p, r in c], P + "Mane", spacing=0.026 * s, k=0.02 * s)
    hr = sh.head_r - 0.015 * s

    def in_head(V):
        return (np.abs((V - sh.head_c) / hr) ** 2.3).sum(1) < 1.0
    return ak.cull_inside(beard, in_head)


def build_horn(sh, P, side):
    """Korte hoorn: eerst opzij, dan omhoog en licht naar binnen (lokaal: voet in de oorsprong)."""
    s = sh.s
    if sh.calf:
        return ak.superellipsoid(0.045 * s * 1.4, 0.06 * s * 1.4, 0.045 * s * 1.4, 12, 8, mat=P + "Horn")
    pts = [(0.0, 0.0, 0.0), (side * 0.10, 0.02, 0.0), (side * 0.17, 0.09, 0.02), (side * 0.18, 0.18, 0.05),
           (side * 0.14, 0.24, 0.07)]
    return ck.tube([tuple(np.array(p) * s) for p in pts], 0.062 * s, P + "Horn", n=16, seg=12, r_end=0.018 * s)


def build_ear(sh, P, side):
    s = sh.s
    ear = ak.superellipsoid(0.085 * s, 0.045 * s, 0.03 * s, 22, 12, mat=P + "Mane")
    V = ear.V.copy()
    V[:, 0] += 0.07 * s
    ear.V = V

    def inner(Q):
        q = (Q - np.array([0.08 * s, 0.0, 0.035 * s])) / (np.array([0.05, 0.026, 0.05]) * s)
        return np.linalg.norm(q, axis=1) - 1.0
    ear = ak.paint_field(ear, inner, P + "EarInner")
    return ear if side > 0 else ear.transform(s=(-1, 1, 1))


def build_tail(sh, P):
    s = sh.s
    pts = [(0, 0.02, 0.03), (0, 0.0, -0.04), (0, -0.12, -0.08), (0, -0.28, -0.09), (0, -0.38, -0.08)]
    stem = ck.tube([tuple(np.array(p) * s) for p in pts], 0.028 * s, P + "Coat", n=18, seg=10, r_end=0.022 * s)
    plume = ak.superellipsoid(0.06 * s, 0.085 * s, 0.06 * s, 16, 10, mat=P + "Mane").transform(t=np.array([0, -0.43, -0.08]) * s)
    return mk.merge(stem, plume)


def build_scene(calf=False):
    asset = "Bison_Calf" if calf else "Bison"
    P = "M_BisonCalf_" if calf else "M_Bison_"
    sh = Shape(calf)
    sc = mk.Scene(asset)
    materials(sc, P, calf)
    sc.add(mk.Part(asset, None))
    body = build_body(sh, P)
    if not calf:
        body = mk.merge(body, build_hump(sh, P), build_chest(sh, P))
    sc.add(mk.Part("Body", body.transform(t=-sh.body_c), tuple(sh.body_c), asset, ao_group="body", gradient=0.16))
    head = build_head(sh, P)
    sc.add(mk.Part("Head", head.transform(t=-sh.neck), tuple(sh.neck - sh.body_c), "Body", ao_group="head",
                   ao_receive=("tuft", "muzzle"), gradient=0.08))
    muzzle = build_muzzle(sh, P)
    sc.add(mk.Part("Muzzle", muzzle.transform(t=-sh.neck), (0, 0, 0), "Head", ao_group="muzzle", gradient=0.10))
    sc.add(mk.Part("Tuft", build_tuft(sh, P).transform(t=-sh.neck), (0, 0, 0), "Head", ao_group="tuft", gradient=0.0))
    if not calf:
        sc.add(mk.Part("Beard", build_beard(sh, P).transform(t=-sh.neck), (0, 0, 0), "Head", ao_group="tuft", gradient=0.0))
    s = sh.s
    for side, nm in ((1, "L"), (-1, "R")):
        hroot = np.array([side * 0.24, 1.0, 0.70]) * s
        sc.add(mk.Part("Horn_" + nm, build_horn(sh, P, side), tuple(hroot - sh.neck), "Head", None, ao_group="head", gradient=0.0))
        eroot = np.array([side * 0.27, 0.88, 0.64]) * s
        R = mk.rot_y(side * 15 * DEG) @ mk.rot_z(side * 22 * DEG)
        sc.add(mk.Part("Ear_" + nm, build_ear(sh, P, side), tuple(eroot - sh.neck), "Head", R, ao_group="head", gradient=0.0))
    ex, ey, er = sh.eye
    ak.add_face(sc, P, head, "Head", sh.neck, ex, ey, er, (0.0, sh.muzzle_c[1] - 0.045 * s), lid_mat=P + "Mane",
                mouth_mesh=muzzle, mouth_scale=1.25 * s / 0.8 if calf else 1.35, eye_sink=0.08, eye_fwd=0.5)
    marks = ak.alert_marks(P + "Alert", r0=0.07, length=0.11, width=0.03)
    sc.add(mk.Part("Fx_Alert", marks, tuple(np.array([-0.34, 1.32, 0.62]) * s - sh.neck), "Head", mk.rot_z(40 * DEG),
                   visible=False, ao_group="fx", gradient=0.0))
    for nm, (x, z) in sh.legs.items():
        front = nm.startswith("F")
        hip = underside(body, x, z, sh.leg_r * 0.8) + 0.15 * s           # heup ruim binnen het lijf
        leg = build_leg(sh, P, front, hip)
        sc.add(mk.Part("Leg_" + nm, leg, tuple(np.array([x, hip, z]) - sh.body_c), "Body", ao_group="legs",
                       ao_receive=("body",), gradient=0.0))
    troot = np.array([0.0, sh.body_c[1] + 0.22 * s, sh.body_c[2] - sh.body_r[2] + 0.04 * s])
    sc.add(mk.Part("Tail", build_tail(sh, P), tuple(troot - sh.body_c), "Body", ao_group="tail", ao_receive=("body",),
                   gradient=0.0))
    return sc, asset


def main():
    for calf in (False, True):
        sc, asset = build_scene(calf)
        ak.finish(sc, asset)


if __name__ == "__main__":
    main()
