"""Gedeelde bouwstenen voor de dieren (schaap, koe, ...) in de stijl van de bonen-personages.

De dieren gebruiken dezelfde grote ogen, pupillen, monden en knoopnamen als de personages
(`Characters/character_kit.py`), zodat `CharacterFace` in Unity ze zonder aanpassing kan aansturen:
`Eye_L/R › Pupil_L/R`, `EyeClosed_L/R`, `Blink_L/R`, `Mouth_Neutral|Surprised|Happy`.

Dit kit voegt toe wat een viervoeter nodig heeft:

* `superellipsoid()`   zacht-vierkante bol (kop, romp, snuit)
* `front_hit()`        punt + normaal op de voorkant van een mesh (ogen/neus op een kop leggen)
* `paint_field()`      een kleurvlek IN het oppervlak snijden (koeienvlekken, binnenoor): één oppervlak, twee
                       materialen, dus geen dubbele vlakken (z-fighting)
* `add_face()`         ogen, pupillen, expressies en monden op een kop
* `leg()`              pootje met hoef als één oppervlak
* `finish()`           schaduw bakken en FBX/GLB/materialen/Unity-bestanden schrijven

Conventies zoals in ArtSource/README.md: Y omhoog, kijkt naar +Z, 1 unit = 1 m, linkerkant = +X, voeten op y = 0.
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Characters"))
import character_kit as ck  # noqa: E402
from character_kit import mk, bc, unity_assets  # noqa: E402

DEG = math.pi / 180.0
EYE_R0 = bc.EYE_R            # oogstraal van de conducteur; dierenogen zijn hiervan een schaal


# ------------------------------------------------------------------ vormen
def superellipsoid(rx, ry, rz, seg=40, rings=24, mat="Default", nv=2.0, nh=2.0):
    """Ellipsoïde met superellips-profiel (nv, verticaal) en -doorsnede (nh, horizontaal): 2 = rond, >2 = zacht vierkant."""
    prof = []
    for i in range(rings + 1):
        t = -math.pi / 2 + math.pi * i / rings
        prof.append((mk.spow(math.cos(t), 2.0 / nv) if i not in (0, rings) else 0.0, ry * mk.spow(math.sin(t), 2.0 / nv)))
    return mk.lathe(prof, seg, rx, rz, nh, mat=mat)


def triangulate(mesh):
    out = mesh.copy()
    F, M = [], []
    for f, m in zip(mesh.F, mesh.M):
        for k in range(1, len(f) - 1):
            F.append((f[0], f[k], f[k + 1]))
            M.append(m)
    out.F, out.M = F, M
    out.FS = None
    return out


def front_hit(mesh, x, y, axis_dir=(0, 0, 1)):
    """Verste snijpunt van de lijn door (x, y) langs +Z met de mesh: (punt, naar-buiten-normaal)."""
    T = triangulate(mesh)
    V = T.V
    F = np.array(T.F)
    a, b, c = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    d = np.asarray(axis_dir, float)
    o = np.array([x, y, 0.0])
    e1, e2 = b - a, c - a
    p = np.cross(d, e2)
    det = (e1 * p).sum(1)
    ok = np.abs(det) > 1e-12
    inv = np.where(ok, 1.0 / np.where(ok, det, 1.0), 0.0)
    tv = o - a
    u = (tv * p).sum(1) * inv
    q = np.cross(tv, e1)
    v = (q * d).sum(1) * inv
    t = (e2 * q).sum(1) * inv
    hit = ok & (u >= -1e-9) & (v >= -1e-9) & (u + v <= 1 + 1e-9)
    if not hit.any():
        raise ValueError("front_hit(): lijn door (%.3f, %.3f) mist de mesh" % (x, y))
    i = np.where(hit)[0][np.argmax(t[hit])]
    n = np.cross(e1[i], e2[i])
    n /= np.linalg.norm(n)
    if np.dot(n, d) < 0:
        n = -n
    return o + d * t[i], n


def paint_field(mesh, fn, mat, only=None):
    """Geef alle stukken oppervlak waar fn(P) < 0 het materiaal `mat`. Driehoeken die de grens kruisen worden
    langs de grens doorgesneden, zodat de vlek een strakke, gladde rand krijgt. Het blijft één oppervlak (gedeelde
    hoekpunten, dus gladde normalen over de rand heen) en er ontstaan geen dubbele vlakken.
    only = alleen faces met dit materiaal mogen overschilderd worden."""
    T = triangulate(mesh)
    V = [tuple(p) for p in T.V]
    val = list(np.asarray(fn(T.V), float))
    cache = {}

    def cut(i, j):
        key = (i, j) if i < j else (j, i)
        if key not in cache:
            fi, fj = val[i], val[j]
            t = fi / (fi - fj)
            cache[key] = len(V)
            V.append(tuple(np.asarray(V[i]) * (1 - t) + np.asarray(V[j]) * t))
            val.append(0.0)
        return cache[key]

    F, M = [], []
    for f, m in zip(T.F, T.M):
        if only is not None and m != only:
            F.append(f)
            M.append(m)
            continue
        ins = [val[i] < 0 for i in f]
        if all(ins) or not any(ins):
            F.append(f)
            M.append(mat if all(ins) else m)
            continue
        # draai zo dat hoek 0 de 'eenling' is (enige binnen of enige buiten)
        lone = ins.index(True) if sum(ins) == 1 else ins.index(False)
        i0, i1, i2 = f[lone], f[(lone + 1) % 3], f[(lone + 2) % 3]
        a, b = cut(i0, i1), cut(i0, i2)
        m_lone, m_rest = (mat, m) if val[i0] < 0 else (m, mat)
        F += [(i0, a, b), (a, i1, i2), (a, i2, b)]
        M += [m_lone, m_rest, m_rest]
    out = mk.Mesh(np.array(V, float), F, M)
    return out


def cull_inside(mesh, inside_fn):
    """Verwijder driehoeken die helemaal binnen een ander (meebewegend) onderdeel liggen: scheelt onzichtbare polygonen."""
    ins = np.asarray(inside_fn(mesh.V), bool)
    keep = [i for i, f in enumerate(mesh.F) if not all(ins[j] for j in f)]
    used = sorted({j for i in keep for j in mesh.F[i]})
    idx = {j: n for n, j in enumerate(used)}
    out = mk.Mesh(mesh.V[used], [tuple(idx[j] for j in mesh.F[i]) for i in keep], [mesh.M[i] for i in keep],
                  N=None if mesh.N is None else mesh.N[used])
    return out


def smooth_union(P, spheres, k=0.04):
    """Gladde afstand tot een stel bollen [(centrum, straal)] (negatief = binnen); voor organische vlekken."""
    C = np.array([s[0] for s in spheres], float)
    R = np.array([s[1] for s in spheres], float)
    d = np.linalg.norm(P[:, None, :] - C[None], axis=2) - R[None]
    m = d.min(1)
    return m - k * np.log(np.exp(-(d - m[:, None]) / k).sum(1))


def capsule_dist(P, a, b, r):
    a, b = np.asarray(a, float), np.asarray(b, float)
    ab = b - a
    t = np.clip(((P - a) @ ab) / (ab @ ab), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1) - r


def fib_points(n, radii, center, seed=0, jitter=0.0):
    """n punten gelijkmatig op een ellipsoïde (Fibonacci-spiraal), licht verstrooid."""
    rng = np.random.default_rng(seed)
    out = []
    ga = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        y = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(max(0.0, 1 - y * y))
        th = ga * i
        p = np.array([math.cos(th) * r, y, math.sin(th) * r]) * np.asarray(radii, float)
        out.append(np.asarray(center, float) + p + rng.uniform(-jitter, jitter, 3))
    return out


def leg(r_leg, r_hoof, h_hoof, h, leg_mat, hoof_mat, seg=20):
    """Pootje met hoef als één oppervlak (lokaal: oorsprong = bovenkant poot, hoef naar -Y, lengte h)."""
    y0 = -h
    prof = [(0.0, y0), (r_hoof * 0.78, y0), (r_hoof * 0.96, y0 + 0.012), (r_hoof, y0 + 0.030),
            (r_hoof, y0 + h_hoof - 0.016), (r_hoof * 0.94, y0 + h_hoof - 0.004), (r_leg, y0 + h_hoof + 0.004),
            (r_leg, -0.03), (r_leg * 0.85, -0.008), (0.0, 0.0)]
    m = mk.lathe(prof, seg, mat=leg_mat)
    cut = y0 + h_hoof
    m.M = [hoof_mat if m.V[list(f), 1].mean() < cut else leg_mat for f in m.F]
    return m


# ------------------------------------------------------------------ gezicht
def face_materials(sc, prefix):
    """Ogen en mond: dezelfde tinten als de personages."""
    # cells=0: geen kleurvlakken over het oogwit (bij deze grote ogen zie je de celgrens anders als streep)
    ck.material(sc, prefix + "EyeWhite", "wool", 0.30, 0.35, flat=0.45, noShadow=True, cells=0.0)
    sc.materials[prefix + "EyeWhite"]["base"] = ck.RAMPS["wool"][3]
    ck.material(sc, prefix + "Pupil", "ink", -0.30, 0.45, spec=0.25, noShadow=True)
    sc.materials[prefix + "Pupil"]["base"] = ck.RAMPS["face"][0]
    ck.material(sc, prefix + "Mouth", "mouth", -0.55, 0.20)
    sc.materials[prefix + "Mouth"]["base"] = ck.RAMPS["mouth"][1]
    ck.material(sc, prefix + "Tongue", "mouth", 0.05, 0.25)
    sc.materials[prefix + "Tongue"]["base"] = ck.RAMPS["mouth"][3]


def _face_map(prefix, lid_mat):
    return {"M_Conductor_EyeWhite": prefix + "EyeWhite", "M_Conductor_Pupil": prefix + "Pupil",
            "M_Conductor_Mouth": prefix + "Mouth", "M_Conductor_Tongue": prefix + "Tongue",
            "M_Conductor_Skin": lid_mat, "X_Skin": lid_mat, "X_Pupil": prefix + "Pupil"}


def build_mouth(prefix, kind, s=1.0):
    """Monden op dierformaat: Neutral = klein 'o'-tje, Surprised = grotere O, Happy = open lach met tong."""
    if kind == "Neutral":
        return mk.ellipsoid(0.021 * s, 0.026 * s, 0.012 * s, 14, 8, mat=prefix + "Mouth")
    if kind == "Surprised":
        return mk.ellipsoid(0.036 * s, 0.046 * s, 0.016 * s, 18, 10, mat=prefix + "Mouth")
    if kind == "Happy":
        inner = mk.ellipsoid(0.062 * s, 0.050 * s, 0.018 * s, 20, 10, mat=prefix + "Mouth")
        V = inner.V.copy()
        V[:, 1] = np.minimum(V[:, 1], 0.012 * s)                      # platte bovenkant: D-vormige lach
        inner.V = V
        tongue = mk.ellipsoid(0.036 * s, 0.018 * s, 0.011 * s, 14, 6, mat=prefix + "Tongue").transform(
            t=(0, -0.028 * s, 0.009 * s))
        return mk.merge(inner, tongue)
    raise ValueError(kind)


def add_face(sc, prefix, head_mesh, parent, parent_pos, eye_x, eye_y, eye_r, mouth_xy, lid_mat, mouth_mesh=None,
             mouth_scale=1.0, eye_sink=0.20, pupil_in=0.022, eye_fwd=0.0):
    """Ogen, pupillen, dichte ogen, knipperoogleden en monden op `head_mesh` (in wereldcoördinaten).
    `parent`/`parent_pos` = knoop waar het gezicht aan hangt en diens wereldpositie.
    mouth_mesh = mesh waarop de mond ligt (bv. de snuit van de koe); standaard de kop zelf.
    eye_fwd = hoeveel de ogen recht vooruit kijken i.p.v. de bolling van de kop te volgen (0..1)."""
    mp = _face_map(prefix, lid_mat)
    s = eye_r / EYE_R0
    pp = np.asarray(parent_pos, float)
    for side, nm in ((1, "L"), (-1, "R")):
        P, n = front_hit(head_mesh, side * eye_x, eye_y)
        n = n * (1 - eye_fwd) + np.array([0, 0, 1.0]) * eye_fwd
        n /= np.linalg.norm(n)
        Rf = bc.face_frame(n)
        pos = P - n * eye_r * eye_sink
        eye = ck.remap(bc.build_eye(), mp).transform(s=(s, s, s))
        sc.add(mk.Part("Eye_" + nm, eye, tuple(pos - pp), parent, Rf, ao_group="eyes", gradient=0.0))
        pupil = ck.remap(bc.build_pupil(side), mp).transform(t=(side * (0.022 - pupil_in), 0, 0)).transform(s=(s, s, s))
        sc.add(mk.Part("Pupil_" + nm, pupil, (0, 0, 0), "Eye_" + nm, ao_group="eyes", gradient=0.0))
        closed = ck.build_closed_eye("X_", side).transform(s=(s, s, s))
        cpos = P + n * 0.006 - np.array([0, 0.004, 0])
        sc.add(mk.Part("EyeClosed_" + nm, ck.remap(closed, mp), tuple(cpos - pp), parent, Rf, visible=False,
                       ao_group="eyes", gradient=0.0))
        blink = ck.build_blink("X_", side).transform(s=(s, s, s))
        sc.add(mk.Part("Blink_" + nm, ck.remap(blink, mp), tuple(pos - pp), parent, Rf, visible=False,
                       ao_group="lids", gradient=0.0))
    Pm, nm_ = front_hit(mouth_mesh if mouth_mesh is not None else head_mesh, mouth_xy[0], mouth_xy[1])
    for kind in ("Neutral", "Surprised", "Happy"):
        sc.add(mk.Part("Mouth_" + kind, build_mouth(prefix, kind, mouth_scale), tuple(Pm - nm_ * 0.004 - pp), parent,
                       bc.face_frame(nm_), visible=(kind == "Neutral"), ao_group="mouth", gradient=0.0))


def alert_marks(mat, n=3, r0=0.05, length=0.075, width=0.022, spread=34):
    """Drie gele streepjes ('!'-accent uit het concept) als waaier; lokaal: oorsprong = draaipunt, wijst naar +Y."""
    parts = []
    for k in range(n):
        a = math.radians((k - (n - 1) / 2) * spread)
        prof = [(0.0, 0.0), (width * 0.30, 0.006), (width * 0.5, length * 0.75), (width * 0.42, length - 0.008),
                (0.0, length)]
        dash = mk.lathe(prof, 10, 1.0, 0.55, mat=mat).transform(t=(0, r0, 0)).transform(R=mk.rot_z(-a))
        parts.append(dash)
    return mk.merge(*parts)


# ------------------------------------------------------------------- export
def finish(sc, asset, category="Animals", bake=None):
    bake = bake or dict(density=4500, radius=0.10, noise_freq=5.0, edge_range=(14.0, 45.0))
    sc.bake_shading(**bake)
    pts = []
    for p in sc.parts:
        if p.mesh is None or not p.visible:
            continue
        M = sc.world_matrix(p)
        pts.append(p.mesh.V @ M[:3, :3].T + M[:3, 3])
    P = np.vstack(pts)
    height = round(float(P[:, 1].max()), 2)
    fbx_rel = f"Assets/Art/Models/{category}/{asset}/{asset}.fbx"
    fbx = os.path.join(ROOT, fbx_rel)
    exp_dir = os.path.join(ROOT, "ArtSource", "Exports", "Animals")
    os.makedirs(os.path.dirname(fbx), exist_ok=True)
    os.makedirs(exp_dir, exist_ok=True)
    sc.write_fbx_ascii(fbx)
    sc.write_glb(os.path.join(exp_dir, asset + ".glb"))
    unity_assets.export_unity(ROOT, sc, fbx_rel, f"Assets/Art/Materials/{category}/{asset}")
    mats = {k: {"base": v["base"], "ramp": v["ramp"], "rampShift": v["ramp_shift"], "smoothness": v["smoothness"],
                "opts": v["opts"]} for k, v in sc.materials.items()}
    json.dump({"model": asset, "height_m": height, "stats": sc.stats(), "materials": mats},
              open(os.path.join(exp_dir, asset + ".materials.json"), "w", encoding="utf-8"), indent=2)
    print(asset, "stats", sc.stats())
    print(asset, "bbox min", P.min(0).round(3), "max", P.max(0).round(3))
    return sc
