"""Gedeelde bouwstenen voor alle bonen-personages (één basismodel met variaties).

Het lijf, de ogen, pupillen, monden en armen zijn exact die van de conducteur
(`Conductor/build_conductor.py`); dit kit voegt toe wat varianten nodig hebben:

* `surface()`          punten + normalen op het echte (impliciete) lijfoppervlak, op hoek/hoogte
* `surface_lathe()`    een profiel rond het lijf of een been vegen (zomen, gordels, sjaal, banden)
* `strap()`            een band die het lijf volgt (bretels, rugzakbanden, sjaalpunt)
* `tube()`             een buis langs een pad (hengsels, montuur, gesloten ogen)
* `blob()`             gladde 'metaball'-massa uit bollen (krullend haar), één gesloten mesh
* `add_face()`         ogen, pupillen en alle expressies (verborgen varianten, net als de conducteur)
* `add_arms()`         armen met hand-ankers voor props
* `finish()`           schaduw bakken en FBX/GLB/materialen/Unity-bestanden schrijven

Conventies zoals in ArtSource/README.md: Y omhoog, kijkt naar +Z, 1 unit = 1 m, linkerkant = +X.
Hoek theta: 0 = voorkant (+Z), +90 graden = linkerzijde (+X), 180 = rug.
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
sys.path.insert(0, os.path.join(HERE, "Conductor"))
import meshkit as mk  # noqa: E402
import implicit_body  # noqa: E402
import unity_assets  # noqa: E402
import build_conductor as bc  # noqa: E402

RAMPS = bc.RAMPS
BODY_R, BODY_DEPTH, Y_COLLAR = bc.BODY_R, bc.BODY_DEPTH, bc.Y_COLLAR
HIP = np.array([0.0, bc.HIP_Y, 0.0])
LEG_X = 0.17                     # middellijn van de broekspijpen in implicit_body
SHOE_Y = 0.085
DEG = math.pi / 180.0


# ------------------------------------------------------------------ materialen
def material(sc, name, ramp, shift=0.0, smooth=0.25, **opts):
    """Materiaal uit een palette-ramp (key of lijst); standaard mat zoals de conducteur (issue #114)."""
    r = RAMPS[ramp] if isinstance(ramp, str) else list(ramp)
    base = r[int(round(np.clip(2 + shift * 4, 0, 4)))]
    o = {"spec": 0.0, "rim": 0.10, "edge": 0.35, "flat": 0.15}
    o.update(opts)
    sc.material(name, base, r, smooth, ramp_shift=shift, opts=o)


def face_materials(sc, prefix):
    """Huid, ogen en mond: identiek aan de conducteur, alleen andere namen."""
    material(sc, prefix + "Skin", "skin", 0.0, 0.30, paint=1.1)
    material(sc, prefix + "EyeWhite", "wool", 0.30, 0.35, flat=0.45, noShadow=True)
    sc.materials[prefix + "EyeWhite"]["base"] = RAMPS["wool"][3]
    material(sc, prefix + "Pupil", "ink", -0.30, 0.45, spec=0.25, noShadow=True)
    sc.materials[prefix + "Pupil"]["base"] = RAMPS["face"][0]
    material(sc, prefix + "Mouth", "mouth", -0.55, 0.20)
    sc.materials[prefix + "Mouth"]["base"] = RAMPS["mouth"][1]
    material(sc, prefix + "Tongue", "mouth", 0.05, 0.25)
    sc.materials[prefix + "Tongue"]["base"] = RAMPS["mouth"][3]


def remap(mesh, mapping):
    out = mesh.copy()
    out.M = [mapping.get(m, m) for m in out.M]
    return out


def conductor_map(prefix):
    return {"M_Conductor_Skin": prefix + "Skin", "M_Conductor_EyeWhite": prefix + "EyeWhite",
            "M_Conductor_Pupil": prefix + "Pupil", "M_Conductor_Mouth": prefix + "Mouth",
            "M_Conductor_Tongue": prefix + "Tongue"}


# ------------------------------------------------------------- lijfoppervlak
def field(x, y, z):
    return implicit_body._body_field(x, y, z, BODY_R, BODY_DEPTH)


def _grad(P):
    e = 1e-3
    g = []
    for a in range(3):
        d = np.zeros(3)
        d[a] = e
        p, m = P + d, P - d
        g.append((field(p[:, 0], p[:, 1], p[:, 2]) - field(m[:, 0], m[:, 1], m[:, 2])) / (2 * e))
    g = np.stack(g, 1)
    return g / np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-12)


def surface(theta, y, cx=0.0, rmax=0.6):
    """Buitenste snijpunt van een horizontale straal vanaf (cx, y, 0) in richting theta met het lijf.
    theta in radialen; geeft (punten Nx3, normalen Nx3)."""
    th = np.atleast_1d(np.asarray(theta, float))
    yy = np.broadcast_to(np.asarray(y, float), th.shape).astype(float)
    cxx = np.broadcast_to(np.asarray(cx, float), th.shape).astype(float)
    dx, dz = np.sin(th), np.cos(th)
    rs = np.linspace(rmax, 0.0, int(rmax / 0.004) + 1)
    X = cxx[:, None] + rs[None] * dx[:, None]
    Z = rs[None] * dz[:, None]
    F = field(X, np.repeat(yy[:, None], len(rs), 1), Z)
    inside = F < 0
    idx = np.argmax(inside, axis=1)
    if not inside[np.arange(len(th)), idx].all():
        raise ValueError("surface(): straal mist het lijf (y=%s)" % yy[~inside[np.arange(len(th)), idx]][:3])
    lo, hi = rs[idx], rs[np.maximum(idx - 1, 0)]          # lo binnen, hi buiten
    for _ in range(22):
        mid = (lo + hi) / 2
        f = field(cxx + mid * dx, yy, mid * dz)
        lo = np.where(f < 0, mid, lo)
        hi = np.where(f < 0, hi, mid)
    r = (lo + hi) / 2
    P = np.stack([cxx + r * dx, yy, r * dz], 1)
    return P, _grad(P)


def frame_at(normal, spin=0.0):
    """Rotatie: lokale +Z langs de normaal, +Y zo veel mogelijk omhoog; spin draait om de normaal."""
    return bc.face_frame(np.asarray(normal, float)) @ mk.rot_z(spin)


def place(mesh, theta, y, lift=0.0, spin=0.0, cx=0.0, tilt=0.0):
    """Leg een onderdeel (lokaal: +Z = naar buiten) op het lijfoppervlak."""
    P, N = surface(theta, y, cx)
    R = frame_at(N[0], spin) @ mk.rot_x(tilt)
    return mesh.transform(R=R, t=P[0] + N[0] * lift)


def wrap(mesh, theta, y, lift=0.0, cx=0.0):
    """Buig een plat onderdeel (lokaal XY, +Z naar buiten) om het lijf: x wordt booglengte rond het lijf,
    y hoogte, z afstand boven het oppervlak. Voor zakken, kleppen en stofprints."""
    P0, _ = surface(theta, y, cx)
    r0 = math.hypot(P0[0, 0] - cx, P0[0, 2])
    V = mesh.V
    S, N = surface(theta + V[:, 0] / r0, y + V[:, 1], cx)
    out = mesh.copy()
    out.V = S + N * (lift + V[:, 2])[:, None]
    out.N = None
    return out


def _orient_by(mesh, probe_face, outward):
    f = mesh.F[probe_face]
    p = mesh.V[list(f)]
    n = np.cross(p[1] - p[0], p[2] - p[0])
    if np.dot(n, outward) < 0:
        mesh.F = [tuple(reversed(ff)) for ff in mesh.F]
    return mesh


def surface_lathe(profile, mat, seg=48, cx=0.0, theta0=None, theta1=None, rmax=0.6, closed_profile=True):
    """Veeg een profiel [(offset langs normaal, y), ...] rond het lijf (cx=0) of een been (cx=+-LEG_X).
    theta0/theta1 = alleen een boog (met dichte uiteinden)."""
    full = theta0 is None
    th = np.linspace(0, 2 * math.pi, seg, endpoint=False) if full else np.linspace(theta0, theta1, seg)
    off = np.array([p[0] for p in profile])
    ys = np.array([p[1] for p in profile])
    m = len(profile)
    TT, YY = np.meshgrid(th, ys, indexing="ij")
    S, N = surface(TT.ravel(), YY.ravel(), cx, rmax)
    V = S + N * np.tile(off, seg)[:, None]
    F = []
    rows = seg if full else seg - 1
    cols = m if closed_profile else m - 1
    for i in range(rows):
        i2 = (i + 1) % seg
        for j in range(cols):
            j2 = (j + 1) % m
            F.append((i * m + j, i2 * m + j, i2 * m + j2, i * m + j2))
    if not full:
        verts = list(V)
        for i, sgn in ((0, 1), (seg - 1, -1)):
            c = V[i * m:(i + 1) * m].mean(0)
            ci = len(verts)
            verts.append(c)
            for j in range(m):
                a, b = i * m + j, i * m + (j + 1) % m
                F.append((ci, a, b) if sgn > 0 else (ci, b, a))
        V = np.array(verts)
    mesh = mk.Mesh(np.asarray(V, float), F, [mat] * len(F))
    jo = int(np.argmax(off))
    probe = jo if jo < cols else 0
    return _orient_by(mesh, probe, N[jo])


def _smooth_path(points, n=40):
    P = np.asarray(points, float)
    for _ in range(3):                                   # Chaikin: zachte bocht door de controlepunten
        Q = [P[0]]
        for a, b in zip(P[:-1], P[1:]):
            Q += [0.75 * a + 0.25 * b, 0.25 * a + 0.75 * b]
        Q.append(P[-1])
        P = np.array(Q)
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]
    t = np.linspace(0, d[-1], n)
    return np.stack([np.interp(t, d, P[:, k]) for k in range(P.shape[1])], 1)


def _section(n_pts, hw, hh, expo=4.0):
    out = []
    for k in range(n_pts):
        a = 2 * math.pi * k / n_pts
        c, s = math.cos(a), math.sin(a)
        out.append((hw * mk.spow(c, 2 / expo), hh * mk.spow(s, 2 / expo)))
    return out


def _sweep(C, B, U, sec, mat):
    """Veeg een doorsnede (u langs B, v langs U) langs middenpunten C; dichte uiteinden."""
    n, k = len(C), len(sec)
    V = [C[i] + B[i] * u + U[i] * v for i in range(n) for (u, v) in sec]
    F = []
    for i in range(n - 1):
        for j in range(k):
            j2 = (j + 1) % k
            F.append((i * k + j, i * k + j2, (i + 1) * k + j2, (i + 1) * k + j))
    c0, c1 = len(V), len(V) + 1
    V += [C[0], C[-1]]
    for j in range(k):
        j2 = (j + 1) % k
        F.append((c0, j2, j))
        F.append((c1, (n - 1) * k + j, (n - 1) * k + j2))
    mesh = mk.Mesh(np.array(V, float), F, [mat] * len(F))
    # oriëntatie: zijvlak bij het midden moet weg van de as wijzen
    mid = (n // 2) * k
    f = F[(n // 2) * k]
    p = mesh.V[list(f)]
    nrm = np.cross(p[1] - p[0], p[2] - p[0])
    if np.dot(nrm, p.mean(0) - C[n // 2]) < 0:
        mesh.F = [tuple(reversed(ff)) for ff in mesh.F]
    del mid
    return mesh


def strap(ctrl, width, thick, mat, lift=0.004, n=36, cx=0.0, expo=5.0):
    """Platte band over het lijf langs controlepunten [(theta_rad, y), ...]."""
    path = _smooth_path(ctrl, n)
    S, N = surface(path[:, 0], path[:, 1], cx)
    C = S + N * (lift + thick / 2)
    T = np.gradient(C, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True)
    B = np.cross(N, T)
    B /= np.linalg.norm(B, axis=1, keepdims=True)
    U = np.cross(T, B)
    return _sweep(C, B, U, _section(12, width / 2, thick / 2, expo), mat)


def tube(points, radius, mat, n=None, seg=10, smooth=True, r_end=None):
    """Ronde buis langs een pad (parallel transport); r_end = taps toelopen."""
    P = _smooth_path(points, n or max(12, 6 * len(points))) if smooth else np.asarray(points, float)
    T = np.gradient(P, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True)
    ref = np.array([0, 1.0, 0]) if abs(T[0][1]) < 0.9 else np.array([1.0, 0, 0])
    B = [np.cross(ref, T[0])]
    B[0] /= np.linalg.norm(B[0])
    for i in range(1, len(P)):
        b = B[-1] - np.dot(B[-1], T[i]) * T[i]
        B.append(b / np.linalg.norm(b))
    B = np.array(B)
    U = np.cross(T, B)
    rr = np.linspace(radius, radius if r_end is None else r_end, len(P))
    sec = [(math.cos(2 * math.pi * k / seg), math.sin(2 * math.pi * k / seg)) for k in range(seg)]
    n_, k_ = len(P), seg
    V = [P[i] + (B[i] * u + U[i] * v) * rr[i] for i in range(n_) for (u, v) in sec]
    F = []
    for i in range(n_ - 1):
        for j in range(k_):
            j2 = (j + 1) % k_
            F.append((i * k_ + j, i * k_ + j2, (i + 1) * k_ + j2, (i + 1) * k_ + j))
    # halve bolletjes als eindkap
    V = list(V)
    for end, sgn in ((0, -1), (n_ - 1, 1)):
        base = end * k_
        rings = []
        for q in range(1, 4):
            a = math.pi / 2 * q / 4
            ring = []
            for (u, v) in sec:
                ring.append(len(V))
                V.append(P[end] + (B[end] * u + U[end] * v) * rr[end] * math.cos(a) + T[end] * sgn * rr[end] * math.sin(a))
            rings.append(ring)
        tip = len(V)
        V.append(P[end] + T[end] * sgn * rr[end])
        prev = list(range(base, base + k_))
        for ring in rings:
            for j in range(k_):
                j2 = (j + 1) % k_
                F.append((prev[j], prev[j2], ring[j2], ring[j]) if sgn > 0 else (prev[j], ring[j], ring[j2], prev[j2]))
            prev = ring
        for j in range(k_):
            j2 = (j + 1) % k_
            F.append((prev[j], prev[j2], tip) if sgn > 0 else (prev[j], tip, prev[j2]))
    mesh = mk.Mesh(np.array(V, float), F, [mat] * len(F))
    f = F[0]
    p = mesh.V[list(f)]
    if np.dot(np.cross(p[1] - p[0], p[2] - p[0]), p.mean(0) - P[0]) < 0:
        mesh.F = [tuple(reversed(ff)) for ff in mesh.F]
    return mesh


def blob(spheres, mat, spacing=0.011, k=0.03):
    """Gladde massa uit overlappende bollen [(centrum, straal)], als één gesloten oppervlak."""
    C = np.array([s[0] for s in spheres], float)
    R = np.array([s[1] for s in spheres], float)

    def fld(x, y, z):
        shp = x.shape
        p = np.stack([x.ravel(), y.ravel(), z.ravel()], 1)
        d = np.linalg.norm(p[:, None, :] - C[None], axis=2) - R[None]
        # gladde minimum (exponentieel) zodat bollen in elkaar overvloeien zonder naden
        m = d.min(1)
        s = np.exp(-(d - m[:, None]) / k).sum(1)
        return (m - k * np.log(s)).reshape(shp)

    lo = (C - R[:, None]).min(0) - 0.03
    hi = (C + R[:, None]).max(0) + 0.03
    xs, ys, zs = (np.arange(lo[a], hi[a] + spacing, spacing) for a in range(3))
    verts, faces = implicit_body.polygonize(fld, xs, ys, zs)
    V = np.asarray(verts)
    e = 1e-3
    g = []
    for a in range(3):
        d = np.zeros(3)
        d[a] = e
        p, m = V + d, V - d
        g.append(fld(p[:, 0], p[:, 1], p[:, 2]) - fld(m[:, 0], m[:, 1], m[:, 2]))
    Nn = np.stack(g, 1)
    Nn /= np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-12)
    return mk.Mesh(V, [tuple(f) for f in faces], [mat] * len(faces), N=Nn)


def puffs(spheres, mat, seg=18, rings=10, squash=0.92):
    """Wolk van overlappende bolletjes (krullen); de naden ertussen geven de geschilderde krul-look."""
    parts = []
    for c, r in spheres:
        c = np.asarray(c, float)
        parts.append(mk.ellipsoid(r, r * squash, r, seg, rings, mat=mat).transform(t=c))
    return mk.merge(*parts)


def plate(poly_xy, thick, mat, chamfer=0.006):
    """Vlakke, licht afgeschuinde plaat (lokaal XY, dikte langs +Z), zacht genormaliseerd."""
    m = mk.prism(poly_xy, 0.0, thick, c=chamfer, mat=mat)
    m.sharp = False
    m.FS = None
    return m


def front_plate(poly_xy, thick, mat, lift=0.002, chamfer=0.005):
    """Plaat die de bolling van de voorkant volgt (zoals de revers van de conducteur)."""
    return bc.conform(plate(poly_xy, thick, mat, chamfer), lift)


def body_band(a_scale, profile, mat, seg=40, n=2.6):
    """Ring met de superellips-doorsnede van de romp: profile = [(straalfactor, y)] als gesloten lus."""
    a = BODY_R * a_scale
    return mk.lathe(profile, seg, a, a * BODY_DEPTH, n, closed=True, mat=mat)


def squircle_ring(w, h, r_tube, mat, seg=28, expo=4.0):
    """Rechthoekige gesp/beugel (torus met vierkante loop) in het XY-vlak."""
    pts = []
    for k in range(seg + 1):
        a = 2 * math.pi * k / seg
        pts.append((w / 2 * mk.spow(math.cos(a), 2 / expo), h / 2 * mk.spow(math.sin(a), 2 / expo), 0.0))
    return tube(pts, r_tube, mat, smooth=False)


# ----------------------------------------------------------------- lijf & gezicht
def add_body(sc, bands):
    sc.add(mk.Part(sc.name, None))
    body = implicit_body.make_body(BODY_R, BODY_DEPTH, Y_COLLAR, bands=bands)
    sc.add(mk.Part("Body", body.transform(t=-HIP), tuple(HIP), sc.name, ao_group="body"))


def add_detail(sc, name, mesh_world, ao_group="body", visible=True, gradient=0.18):
    """Voeg een onderdeel toe dat in wereldcoördinaten is gebouwd, als kind van Body."""
    sc.add(mk.Part(name, mesh_world.transform(t=-HIP), (0, 0, 0), "Body", ao_group=ao_group,
                   visible=visible, gradient=gradient))


def attach(sc, name, mesh_local, parent, world_pos, world_R=None, ao_group=None, visible=True, gradient=0.0):
    """Hang een onderdeel aan een knoop (bv. Hand_L) met een gewenste positie/oriëntatie in wereldruimte."""
    Mp = sc.world_matrix(sc.get(parent))
    Rp, tp = Mp[:3, :3], Mp[:3, 3]
    world_R = np.eye(3) if world_R is None else np.asarray(world_R, float)
    sc.add(mk.Part(name, mesh_local, tuple(Rp.T @ (np.asarray(world_pos, float) - tp)), parent, Rp.T @ world_R,
                   ao_group=ao_group or name.lower(), visible=visible, gradient=gradient))


def eye_frame(side):
    x = side * bc.EYE_X
    z, nrm = bc.body_surface(x, bc.EYE_Y)
    return np.array([x, bc.EYE_Y, z - bc.EYE_R * 0.20]), bc.face_frame(nrm)


def build_closed_eye(prefix, side, mood="happy"):
    """Dichtgeknepen oog: een donkere boog (happy = omhoog gebogen, zoals ^ in het concept)."""
    pts = []
    for k in range(9):
        a = math.radians(25 + 130 * k / 8)
        y = 0.040 * math.sin(a) - 0.012 if mood == "happy" else -0.040 * math.sin(a) + 0.012
        pts.append((0.062 * math.cos(a), y, 0.0))
    return tube(pts, 0.011, prefix + "Pupil", n=16, seg=8)


def build_lid(prefix, side, drop=0.36):
    """Ooglid in huidkleur dat het bovenste deel van het oog afdekt, schuin aflopend naar de neus
    (vastberaden blik zonder wenkbrauwen). Lokaal in het oogframe."""
    m = mk.ellipsoid(bc.EYE_R * 1.07, bc.EYE_R * 1.08 * 1.07, bc.EYE_R * bc.EYE_DEPTH * 1.35, 24, 14,
                     mat=prefix + "Skin")
    V = m.V.copy()
    # lijn: y = y0 + drop * x * side  (binnenkant = -x*side lager)
    y0 = 0.030
    line = y0 + drop * V[:, 0] * side
    low = V[:, 1] < line
    V[low, 1] = line[low]
    V[low, 2] *= 0.92
    m.V = V
    return m


def build_flat_mouth(prefix, kind):
    if kind == "Flat":         # klein, vastberaden streepje
        return mk.ellipsoid(0.034, 0.010, 0.010, 14, 6, mat=prefix + "Mouth")
    if kind == "Hmm":          # scheef, peinzend mondje
        return mk.ellipsoid(0.030, 0.009, 0.010, 14, 6, mat=prefix + "Mouth").transform(
            R=mk.rot_z(math.radians(-12)), t=(0.018, 0.004, 0))
    if kind == "Smile":        # dichte glimlach
        pts = [(0.050 * math.cos(a), 0.024 * math.sin(a) + 0.012, 0.0)
               for a in np.linspace(math.radians(200), math.radians(340), 8)]
        return tube(pts, 0.010, prefix + "Mouth", n=16, seg=8)
    raise ValueError(kind)


def add_face(sc, prefix, extra_mouths=("Flat", "Hmm", "Smile"), lids=False, lid_drop=0.36, default_mouth="Neutral"):
    """Ogen + pupillen (conducteur), dichte ogen, optionele oogleden en monden.
    Alleen Mouth_Neutral (en de open ogen) zijn standaard zichtbaar."""
    mp = conductor_map(prefix)
    for side, nm in ((1, "L"), (-1, "R")):
        pos, Rf = eye_frame(side)
        sc.add(mk.Part("Eye_" + nm, remap(bc.build_eye(), mp), tuple(pos - HIP), "Body", Rf, ao_group="eyes",
                       gradient=0.0))
        sc.add(mk.Part("Pupil_" + nm, remap(bc.build_pupil(side), mp), (0, 0, 0), "Eye_" + nm, ao_group="eyes",
                       gradient=0.0))
        z, _ = bc.body_surface(side * bc.EYE_X, bc.EYE_Y)
        cpos = np.array([side * bc.EYE_X, bc.EYE_Y - 0.004, z + 0.006])
        sc.add(mk.Part("EyeClosed_" + nm, build_closed_eye(prefix, side), tuple(cpos - HIP), "Body", Rf,
                       visible=False, ao_group="eyes", gradient=0.0))
        sc.add(mk.Part("Lid_" + nm, build_lid(prefix, side, lid_drop), tuple(pos - HIP), "Body", Rf,
                       visible=lids, ao_group="lids", gradient=0.0))
    zm, nm_ = bc.body_surface(0.0, bc.MOUTH_Y)
    for kind in ("Neutral", "Surprised", "Happy") + tuple(extra_mouths):
        mesh = remap(bc.build_mouth(kind), mp) if kind in ("Neutral", "Surprised", "Happy") \
            else build_flat_mouth(prefix, kind)
        sc.add(mk.Part("Mouth_" + kind, mesh, (0, bc.MOUTH_Y - bc.HIP_Y, zm - 0.004), "Body",
                       bc.face_frame(nm_), visible=(kind == default_mouth), ao_group="body"))


def build_arm(sleeve, hand, cut=-0.148, hand_scale=1.0, sleeve_bulge=1.0):
    """Arm van de conducteur; materiaal wisselt op `cut` (lokale y). hand_scale = dikkere wanten."""
    prof = [(0.0, -0.270), (0.025, -0.267), (0.041, -0.260), (0.057, -0.251), (0.069, -0.238), (0.078, -0.218),
            (0.079, -0.190), (0.074, -0.164), (0.071, -0.148),
            (0.073, -0.120), (0.077, -0.070), (0.079, -0.025), (0.071, 0.018), (0.0, 0.045)]
    out = []
    for r, y in prof:
        if y < -0.15:
            s = hand_scale
            y = -0.15 + (y + 0.15) * (0.6 + 0.4 * hand_scale)
        elif y > cut:
            s = sleeve_bulge
        else:
            s = 1.0
        out.append((r * s, y))
    arm = mk.lathe(out, 32, a=1.0, b=0.88, n=2.0, mat=sleeve)
    arm.M = [hand if arm.V[list(f), 1].mean() < cut else sleeve for f in arm.F]
    return arm


def add_arms(sc, arm_mesh, poses):
    """poses: {'L': 3x3 rotatie, 'R': ...} t.o.v. het lijf. Hand_L/R-ankers zoals de conducteur."""
    for side, nm in ((1, "L"), (-1, "R")):
        sh = np.array([side * bc.SHOULDER[0], bc.SHOULDER[1], bc.SHOULDER[2]]) - HIP
        mesh = arm_mesh(side) if callable(arm_mesh) else arm_mesh
        sc.add(mk.Part("Arm_" + nm, mesh, tuple(sh), "Body", poses[nm], ao_group="arm_" + nm, gradient=0.0))
        sc.add(mk.Part("Hand_" + nm, None, (0, -0.21, 0.0), "Arm_" + nm, ao_group="arm_" + nm))


def arm_pose(out_deg, fwd_deg=0.0, twist_deg=0.0, side=1):
    """Arm naar buiten (om Z) en naar voren (om X); twist draait om de armas."""
    return mk.rot_x(-math.radians(fwd_deg)) @ mk.rot_z(side * math.radians(out_deg)) @ mk.rot_y(math.radians(twist_deg))


def add_leg_anchors(sc):
    for side, nm in ((1, "L"), (-1, "R")):
        sc.add(mk.Part("Leg_" + nm, None, (side * 0.162, bc.HIP_Y, 0.0), sc.name))


# ------------------------------------------------------------------- export
def finish(sc, asset, height=None, bake=None):
    """height=None: gemeten hoogte van alle zichtbare onderdelen (incl. hoofddeksel)."""
    bake = bake or dict(density=4500, radius=0.10, noise_freq=5.0, edge_range=(14.0, 45.0))
    sc.bake_shading(**bake)
    pts = []
    for p in sc.parts:
        if p.mesh is None or not p.visible:
            continue
        M = sc.world_matrix(p)
        pts.append(p.mesh.V @ M[:3, :3].T + M[:3, 3])
    P = np.vstack(pts)
    height = round(float(P[:, 1].max()), 2) if height is None else height
    fbx_rel = f"Assets/Art/Models/Characters/{asset}/{asset}.fbx"
    fbx = os.path.join(ROOT, fbx_rel)
    exp_dir = os.path.join(ROOT, "ArtSource", "Exports")
    os.makedirs(os.path.dirname(fbx), exist_ok=True)
    os.makedirs(exp_dir, exist_ok=True)
    sc.write_fbx_ascii(fbx)
    sc.write_glb(os.path.join(exp_dir, asset + ".glb"))
    unity_assets.export_unity(ROOT, sc, fbx_rel, f"Assets/Art/Materials/Characters/{asset}")
    mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"],
                "opts": v["opts"]} for k, v in sc.materials.items()}
    json.dump({"model": asset, "height_m": height, "stats": sc.stats(), "materials": mats},
              open(os.path.join(exp_dir, asset + ".materials.json"), "w", encoding="utf-8"), indent=2)
    print(asset, "stats", sc.stats())
    print(asset, "bbox min", P.min(0).round(3), "max", P.max(0).round(3))
    return sc
