"""Perronmodules (issue #182), naar Docs/References/Environment/platform-kit-concept.png.

Assenstelsel = dat van een spoorstuk: pivot op de hartlijn van het spoor op maaiveld (y = 0), het module
loopt langs +X vanaf x = 0, het perron ligt aan de +Z-kant. Zet een module dus met dezelfde positie en
draaiing neer als het spoorstuk ernaast. Andere kant van het spoor: 180 graden draaien.
  * bovenkant perron y = 1,05 (= bodem wagonbak), voorrand z = 1,65 (loc steekt tot 1,58 uit)
  * diepte 3,5 m (achterrand z = 5,15)
  * opbouw: donkere kern, stenen blokken langs de zichtbare zijden, randtegels (band van 0,45 m),
    gele geleidelijn met noppen langs de spoorkant, tegelvloer in een raster van 0,75 m
  * 'joint'-zijden (aansluitingen op het volgende module) hebben geen rand en geen blokken
"""
import math

import numpy as np

from station_common import (PLATFORM_EDGE, PLATFORM_TOP, _area, col_box, mk, rbox, slab)

E = PLATFORM_EDGE
TOP = PLATFORM_TOP
DEPTH = 3.5
B = E + DEPTH
TILE = 0.75
COPING = 0.45
TACT = (0.52, 0.80)
TILE_T = 0.10
COPE_T = 0.14
GAP = 0.024


# ------------------------------------------------------------------ geometrie in het grondvlak
def plan_slab(poly_xz, y0, y1, c=0.02, mat="M_Stn_Paving"):
    """Polygoon in het grondvlak (x, z) verticaal geëxtrudeerd van y0 tot y1."""
    m = slab([(x, -z) for (x, z) in poly_xz], (), y0, y1, c, mat)
    return m.transform(R=mk.rot_x(-math.pi / 2))


def _clip(poly, a, n, d):
    """Sutherland-Hodgman: houd het deel met dot(p - a, n) >= d."""
    out = []
    for i in range(len(poly)):
        p, q = np.asarray(poly[i - 1], float), np.asarray(poly[i], float)
        fp, fq = np.dot(p - a, n) - d, np.dot(q - a, n) - d
        if fq >= 0:
            if fp < 0:
                out.append(tuple(p + (q - p) * fp / (fp - fq)))
            out.append(tuple(q))
        elif fp >= 0:
            out.append(tuple(p + (q - p) * fp / (fp - fq)))
    return out


def _ccw_xz(P):
    """CCW gezien van bovenaf (Y omhoog) = CW in (x, z)-wiskunde; wij werken met 'binnen = links' in (x, -z)."""
    return P


def inset_region(outline, kinds, insets):
    """Convex gebied: de omtrek met per zijde een eigen inspringing (kinds[i] hoort bij zijde i -> i+1)."""
    region = [(-100, -100), (100, -100), (100, 100), (-100, 100)]
    for i in range(len(outline)):
        a = np.array(outline[i], float)
        b = np.array(outline[(i + 1) % len(outline)], float)
        e = (b - a) / np.linalg.norm(b - a)
        n = _inward(e)
        region = _clip(region, a, n, insets[kinds[i]])
        if not region:
            break
    return region


def _sign():
    return 1.0


def _inward(e):
    # omtrekken zijn CCW in het (x, z)-vlak (z naar 'boven'), binnen ligt links: n = (-e_z, e_x)
    return np.array([-e[1], e[0]])


def _orient(outline):
    P = [tuple(p) for p in outline]
    if _area(P) < 0:
        raise ValueError("omtrek moet CCW zijn in (x, z)")
    return P


def runs(outline, kinds, want):
    """Aaneengesloten reeksen zijden met kind in `want`, als lijst van vertex-indexreeksen."""
    n = len(outline)
    flags = [kinds[i] in want for i in range(n)]
    if all(flags):
        return [list(range(n)) + [0]]
    out = []
    start = next(i for i in range(n) if not flags[i])
    i = (start + 1) % n
    cur = None
    for _ in range(n):
        if flags[i]:
            if cur is None:
                cur = [i]
            cur.append((i + 1) % n)
        elif cur is not None:
            out.append(cur)
            cur = None
        i = (i + 1) % n
    if cur is not None:
        out.append(cur)
    return out


def _offset_run(outline, run, d, kinds=None):
    """Binnenlijn op afstand d van een reeks zijden; uiteinden schuiven langs een aangrenzende joint-zijde,
    anders loodrecht op de laatste zijde (de band stopt dan recht)."""
    n = len(outline)
    out = []
    for k, vi in enumerate(run):
        p = np.array(outline[vi], float)
        prev_v = np.array(outline[(vi - 1) % n], float)
        next_v = np.array(outline[(vi + 1) % n], float)
        e_in = (p - prev_v) / np.linalg.norm(p - prev_v)
        e_out = (next_v - p) / np.linalg.norm(next_v - p)
        closed_loop = run[0] == run[-1]
        if k == 0 and not closed_loop:               # begin
            n_run = _inward(e_out)
            if kinds is None or kinds[(vi - 1) % n] == "joint":
                along = -e_in
                out.append(p + along * d / max(np.dot(along, n_run), 0.2))
            else:
                out.append(p + n_run * d)
        elif k == len(run) - 1 and not closed_loop:
            n_run = _inward(e_in)
            if kinds is None or kinds[vi % n] == "joint":
                along = e_out
                out.append(p + along * d / max(np.dot(along, n_run), 0.2))
            else:
                out.append(p + n_run * d)
        else:
            n1, n2 = _inward(e_in), _inward(e_out)
            m = n1 + n2
            m /= np.linalg.norm(m)
            out.append(p + m * d / max(np.dot(m, n1), 0.3))
    return out


def _resample(line_a, line_b, seg_len, phase=0.0):
    """Knip de band tussen twee evenwijdige lijnen in stukken van ~seg_len (langs line_a)."""
    A = np.array(line_a, float)
    Bl = np.array(line_b, float)
    s = np.r_[0, np.cumsum(np.linalg.norm(np.diff(A, axis=0), axis=1))]
    total = s[-1]
    cuts = list(np.arange(phase, total, seg_len))
    if not cuts or cuts[0] > 1e-6:
        cuts = [0.0] + cuts
    cuts = [c for c in cuts if c < total - 0.15] + [total]

    def at(L, t):
        return np.array([np.interp(t, s, L[:, 0]), np.interp(t, s, L[:, 1])])
    pieces = []
    for a, b in zip(cuts[:-1], cuts[1:]):
        mids = [i for i in range(len(s)) if a < s[i] < b]
        outer = [at(A, a)] + [A[i] for i in mids] + [at(A, b)]
        inner = [at(Bl, a)] + [Bl[i] for i in mids] + [at(Bl, b)]
        pieces.append([tuple(p) for p in outer] + [tuple(p) for p in inner[::-1]])
    return pieces


def _shrink(poly, g):
    P = np.array(poly, float)
    c = P.mean(0)
    out = []
    for p in P:
        v = p - c
        L = np.linalg.norm(v)
        out.append(tuple(p - v / max(L, 1e-9) * g))
    return out


def _band(outline, run, d0, d1, seg_len, phase=0.0, kinds=None):
    a = [outline[i] for i in run] if d0 == 0 else _offset_run(outline, run, d0, kinds)
    b = _offset_run(outline, run, d1, kinds)
    return _resample(a, b, seg_len, phase)


def _poly_ok(p):
    return len(p) >= 3 and abs(_area(p)) > 1e-4


# ------------------------------------------------------------------ module opbouw
def build_module(outline, kinds, rng, front_kinds=("front",), tactile=True, extra_cols=()):
    """outline: CCW (x, z)-punten; kinds per zijde: 'front' (spoorkant: rand + geleidelijn), 'edge' (rand),
    'joint' (aansluiting, open)."""
    outline = _orient(outline)
    parts = []
    # kern
    core = inset_region(outline, kinds, {"joint": 0.0, "edge": 0.10, "front": 0.10})
    parts.append(plan_slab(core, 0.0, TOP - TILE_T + 0.004, 0.02, "M_Stn_StoneDark"))
    # stenen blokken langs zichtbare zijden (twee lagen, verspringend)
    for run in runs(outline, kinds, ("front", "edge")):
        for row, (y0, y1) in enumerate(((0.0, 0.46), (0.46, TOP - COPE_T))):
            for k, poly in enumerate(_band(outline, run, 0.0, 0.20, 0.72, phase=0.36 * row, kinds=kinds)):
                if _poly_ok(poly):
                    mat = "M_Stn_Stone" if rng.random() > 0.3 else "M_Stn_StoneDark"
                    parts.append(plan_slab(_shrink(poly, 0.012), y0 + 0.008, y1 - 0.008, 0.035, mat))
    # randtegels
    for run in runs(outline, kinds, ("front", "edge")):
        for poly in _band(outline, run, 0.0, COPING, 0.80, kinds=kinds):
            if _poly_ok(poly):
                parts.append(plan_slab(_shrink(poly, GAP), TOP - COPE_T, TOP, 0.03, "M_Stn_PavingLight"))
    # gele geleidelijn met noppen
    if tactile:
        for run in runs(outline, kinds, front_kinds):
            for poly in _band(outline, run, TACT[0], TACT[1], 0.90, kinds=kinds):
                if _poly_ok(poly):
                    parts.append(plan_slab(_shrink(poly, 0.006), TOP - TILE_T, TOP + 0.012, 0.012, "M_Stn_Yellow"))
            for d in (TACT[0] + 0.07, (TACT[0] + TACT[1]) / 2, TACT[1] - 0.07):
                line = _offset_run(outline, run, d, kinds)
                A = np.array(line, float)
                s = np.r_[0, np.cumsum(np.linalg.norm(np.diff(A, axis=0), axis=1))]
                for t in np.arange(0.06 + (0.05 if d == (TACT[0] + TACT[1]) / 2 else 0.0), s[-1] - 0.04, 0.10):
                    p = np.array([np.interp(t, s, A[:, 0]), np.interp(t, s, A[:, 1])])
                    parts.append(rbox(p[0] - 0.022, p[0] + 0.022, TOP + 0.010, TOP + 0.030, p[1] - 0.022, p[1] + 0.022,
                                      0.0, "M_Stn_YellowDark"))
    # tegelvloer in een raster, geknipt op het binnengebied
    inner = inset_region(outline, kinds, {"joint": 0.0, "edge": COPING, "front": (TACT[1] + 0.02) if tactile else COPING})
    xs = [p[0] for p in inner]
    zs = [p[1] for p in inner]
    for i, x0 in enumerate(np.arange(math.floor(min(xs) / TILE) * TILE, max(xs), TILE)):
        for j, z0 in enumerate(np.arange(math.floor(min(zs) / TILE) * TILE - (TILE / 2) * (i % 2), max(zs), TILE)):
            sq = [(x0, z0), (x0 + TILE, z0), (x0 + TILE, z0 + TILE), (x0, z0 + TILE)]
            poly = sq
            for k in range(len(inner)):
                a = np.array(inner[k], float)
                b = np.array(inner[(k + 1) % len(inner)], float)
                poly = _clip(poly, a, _inward((b - a) / np.linalg.norm(b - a)), 0.0)
                if not poly:
                    break
            if poly and _poly_ok(poly):
                mat = ("M_Stn_Paving", "M_Stn_Paving", "M_Stn_PavingDark", "M_Stn_PavingLight")[int(rng.integers(0, 4))]
                parts.append(plan_slab(_shrink(poly, GAP), TOP - TILE_T, TOP, 0.025, mat))
    col = plan_slab(outline, 0.0, TOP, 0.0, "M_Stn_Collider")
    if extra_cols:
        col = mk.merge(col, *extra_cols)
    return mk.merge(*parts), col


def rect(x0, x1, z0, z1):
    return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]


def straight(L, rng):
    return build_module(rect(0.0, L, E, B), ["front", "joint", "edge", "joint"], rng)


def _arc_pts(cx, cz, r, a0, a1, n):
    return [(cx + r * math.cos(math.radians(a)), cz + r * math.sin(math.radians(a))) for a in np.linspace(a0, a1, n)]


def end_cap(rng, L=2.4, r=1.6):
    """Kopeinde: afgeronde hoeken aan de +X-kant; rand en geleidelijn lopen met de bocht mee."""
    xc = L - r
    pts = [(0.0, E)] + _arc_pts(xc, E + r, r, -90, 0, 7)[:-1] + _arc_pts(xc, B - r, r, 0, 90, 7)[1:] + [(0.0, B)]
    kinds = []
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        if abs(a[0]) < 1e-6 and abs(b[0]) < 1e-6:
            kinds.append("joint")
        elif abs(a[1] - B) < 1e-6 and abs(b[1] - B) < 1e-6:
            kinds.append("edge")
        else:
            kinds.append("front")                   # spoorkant + de hele boog: geleidelijn loopt mee om de kop
    return build_module(pts, kinds, rng)


def corner(rng, r=0.9):
    """Hoekstuk: het perron maakt een slag van 90 graden weg van het spoor.
    Aansluitingen aan de -X-kant (recht stuk langs het spoor) en aan de +Z-kant (recht stuk, 90 graden gedraaid);
    rand + geleidelijn aan de spoorkant en de +X-kant, met een afgeronde buitenhoek."""
    S = DEPTH
    pts = [(0.0, E)] + _arc_pts(S - r, E + r, r, -90, 0, 6) + [(S, B), (0.0, B)]
    kinds = []
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        if (abs(a[1] - B) < 1e-6 and abs(b[1] - B) < 1e-6) or (abs(a[0]) < 1e-6 and abs(b[0]) < 1e-6):
            kinds.append("joint")
        else:
            kinds.append("front")
    return build_module(pts, kinds, rng)


def ramp(rng, L=5.0):
    """Helling van perronhoogte (x = 0) naar maaiveld (x = L), volle perronbreedte, gele lijn langs de spoorkant."""
    parts = []

    def ytop(x):
        return TOP * (1.0 - x / L)
    # zijwanden (spoorkant + achterkant) als wigvormige stenen platen, in blokken
    for (z0, z1) in ((E, E + 0.22), (B - 0.22, B)):
        for x0 in np.arange(0.0, L, 0.9):
            x1 = min(L, x0 + 0.9)
            prof = [(x0 + 0.01, 0.0), (x1 - 0.01, 0.0), (x1 - 0.01, max(ytop(x1) - COPE_T, 0.0)),
                    (x0 + 0.01, max(ytop(x0) - COPE_T, 0.0))]
            if prof[3][1] - prof[0][1] < 0.03:
                continue
            if prof[2][1] < 0.02:
                prof = [prof[0], (x1 - 0.01, 0.0), prof[3]]
                prof = [(x0 + 0.01, 0.0), (x1 - 0.01, 0.0), (x0 + 0.01, max(ytop(x0) - COPE_T, 0.0))]
            parts.append(slab(prof, (), z0, z1, 0.02, "M_Stn_Stone" if rng.random() > 0.3 else "M_Stn_StoneDark"))
    core = [(0.0, 0.0), (L, 0.0), (0.0, TOP - TILE_T)]
    parts.append(slab(core, (), E + 0.15, B - 0.15, 0.0, "M_Stn_StoneDark"))
    # randstenen en tegels als schuine platen (parallellogram-profiel per stuk)
    bands = [((E - 0.02, E + COPING), "M_Stn_PavingLight", COPE_T), ((B - COPING, B + 0.02), "M_Stn_PavingLight", COPE_T)]
    zz = np.arange(E + COPING, B - COPING - 1e-6, TILE)
    for k, z0 in enumerate(zz):
        z1 = min(z0 + TILE, B - COPING)
        mat = ("M_Stn_Paving", "M_Stn_PavingDark", "M_Stn_PavingLight", "M_Stn_Paving")[k % 4]
        bands.append(((z0, z1), mat, TILE_T))
    for (z0, z1), mat, th in bands:
        for i, x0 in enumerate(np.arange(0.0, L, 0.8)):
            x1 = min(L, x0 + 0.8)
            ya0, ya1 = ytop(x0), ytop(x1)
            prof = [(x0 + GAP, max(ya0 - th, 0.0)), (x1 - GAP, max(ya1 - th, 0.0)), (x1 - GAP, max(ya1, 0.02)),
                    (x0 + GAP, ya0)]
            parts.append(slab(prof, (), z0 + GAP, z1 - GAP, 0.02, mat))
    # gele lijn langs de spoorkant
    for x0 in np.arange(0.0, L, 1.0):
        x1 = min(L, x0 + 1.0)
        prof = [(x0 + 0.005, ytop(x0) - 0.06), (x1 - 0.005, max(ytop(x1) - 0.06, 0.0)), (x1 - 0.005, ytop(x1) + 0.012),
                (x0 + 0.005, ytop(x0) + 0.012)]
        parts.append(slab(prof, (), E + TACT[0], E + TACT[0] + 0.14, 0.01, "M_Stn_Yellow"))
    col = slab([(0.0, 0.0), (L, 0.0), (L, 0.02), (0.0, TOP)], (), E, B, 0.0, "M_Stn_Collider")
    return mk.merge(*parts), col


def stairs(rng, steps=6):
    """Trap van perronhoogte (x = 0) naar maaiveld langs +X, gele neuzen op elke trede, stenen trapwangen."""
    rise = TOP / steps
    tread = 0.36
    L = (steps - 1) * tread + 0.30
    parts = []
    for k in range(steps - 1):
        x0, x1 = k * tread, (k + 1) * tread
        ytop = TOP - (k + 1) * rise
        parts.append(rbox(x0 + 0.01, x1 + 0.04, ytop - 0.16, ytop, E + 0.24, B - 0.24, 0.03, "M_Stn_Paving"))
        parts.append(rbox(x1 - 0.08, x1 + 0.05, ytop - 0.005, ytop + 0.012, E + 0.30, B - 0.30, 0.008, "M_Stn_Yellow"))
        parts.append(rbox(x0, x1, 0.0, ytop - 0.15, E + 0.30, B - 0.30, 0.0, "M_Stn_StoneDark"))
    # trapwangen: getrapt profiel met een schuine afdeklijst
    for (z0, z1) in ((E, E + 0.24), (B - 0.24, B)):
        prof = [(0.0, 0.0), (L, 0.0), (L, 0.18), (0.0, TOP)]
        parts.append(slab(prof, (), z0, z1, 0.03, "M_Stn_Stone"))
        cap = [(0.0, TOP - 0.02), (L, 0.16), (L, 0.28), (0.0, TOP + 0.10)]
        parts.append(slab(cap, (), z0 - 0.02, z1 + 0.02, 0.03, "M_Stn_PavingLight"))
    # bovenste rand: gele neus op de perronrand
    parts.append(rbox(-0.06, 0.06, TOP - 0.004, TOP + 0.012, E + 0.30, B - 0.30, 0.008, "M_Stn_Yellow"))
    col = mk.merge(slab([(0.0, 0.0), (L, 0.0), (L, 0.02), (0.0, TOP)], (), E + 0.24, B - 0.24, 0.0, "M_Stn_Collider"),
                   slab([(0.0, 0.0), (L, 0.0), (L, 0.28), (0.0, TOP + 0.10)], (), E - 0.02, E + 0.24, 0.0, "M_Stn_Collider"),
                   slab([(0.0, 0.0), (L, 0.0), (L, 0.28), (0.0, TOP + 0.10)], (), B - 0.24, B + 0.02, 0.0, "M_Stn_Collider"))
    return mk.merge(*parts), col
