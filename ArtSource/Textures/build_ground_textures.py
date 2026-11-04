"""Tegelbare grondtextures voor het Unity-terrein, in de stijl van het concept (issue #38).

Effen, rustige vlakken met grote zachte vlekken in een paar tinten (geen fotoruis), kleine gestileerde
graspluk-streepjes, en voor het pad: ronde keien met een lichte bovenrand en een donkere onderrand.
Alle kleuren komen uit Docs/References/palette.json.

Uitvoer (1024 x 1024, sRGB, naadloos tegelbaar):
  Assets/Art/Terrain/Textures/T_Ground_Grass.png         gras
  Assets/Art/Terrain/Textures/T_Ground_GrassFlowers.png  gras met bloemetjes
  Assets/Art/Terrain/Textures/T_Ground_Dirt.png          grond / spoorberm
  Assets/Art/Terrain/Textures/T_Ground_PathStones.png    stenen pad (keien)
  Assets/Art/Terrain/Textures/T_Ground_Rock.png          rots (steile hellingen)
  ArtSource/Exports/Terrain/*_preview.png                2x2 getegeld ter controle

Effen set (T_Flat_*): dezelfde look als het gras/de rots op de kliffen (StylizedRamp-cellen): een effen
kleur met grote, hoekige vlakken die net iets van tint verschillen, plus een heel zachte facetschaduw.
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy.spatial import cKDTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import unity_assets  # noqa: E402

PALETTE = json.load(open(os.path.join(ROOT, "Docs", "References", "palette.json"), encoding="utf-8"))
R = {k: v["ramp"] for g in PALETTE["groups"].values() for k, v in g.items()}
N = 1024
OUT = "Assets/Art/Terrain/Textures"
rng = np.random.default_rng(38)


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float)


def ramp(name, t):
    """t (array, 0..1) -> kleur uit een ramp van 5 tinten (lineair tussen de stops, in sRGB)."""
    stops = np.array([rgb(c) for c in R[name]])
    t = np.clip(t, 0, 1) * 4
    i = np.minimum(np.floor(t).astype(int), 3)
    f = (t - i)[..., None]
    return stops[i] * (1 - f) + stops[i + 1] * f


def pnoise(scale_px, aniso=1.0, angle=0.0):
    """Periodieke band-gefilterde ruis (FFT), genormaliseerd naar gemiddelde 0 / std 1."""
    w = rng.standard_normal((N, N))
    fy = np.fft.fftfreq(N)[:, None]
    fx = np.fft.fftfreq(N)[None, :]
    c, s = np.cos(angle), np.sin(angle)
    u, v = fx * c + fy * s, -fx * s + fy * c
    sig = 1.0 / (scale_px * 2.2)
    out = np.real(np.fft.ifft2(np.fft.fft2(w) * np.exp(-((u * aniso) ** 2 + (v / aniso) ** 2) / (2 * sig ** 2))))
    return (out - out.mean()) / (out.std() + 1e-9)


def soft_bands(x, levels=4, softness=0.35):
    """Zacht geposteriseerd: grote vlakken met zachte overgangen (stijl, geen ruis)."""
    q = np.floor(x * levels) / levels
    frac = x * levels - np.floor(x * levels)
    return q + (np.clip((frac - (1 - softness)) / softness, 0, 1)) / levels


def voronoi(n_seeds, jitter=1.0):
    """Periodieke Voronoi: index van de dichtstbijzijnde kern, afstand tot de rand (F2-F1) en richting."""
    g = int(np.sqrt(n_seeds))
    base = np.stack(np.meshgrid(np.arange(g), np.arange(g)), -1).reshape(-1, 2).astype(float)
    seeds = (base + 0.5 + (rng.uniform(-0.5, 0.5, base.shape) * jitter)) * (N / g)
    tiles = np.concatenate([seeds + np.array([dx, dy]) * N for dx in (-1, 0, 1) for dy in (-1, 0, 1)])
    ids = np.tile(np.arange(len(seeds)), 9)
    yy, xx = np.mgrid[0:N, 0:N]
    P = np.stack([xx.ravel() + 0.5, yy.ravel() + 0.5], -1)
    d, k = cKDTree(tiles).query(P, k=2)
    near = tiles[k[:, 0]]
    edge = ((d[:, 1] - d[:, 0]) / 2).reshape(N, N)
    vec = (P - near).reshape(N, N, 2)
    return ids[k[:, 0]].reshape(N, N), edge, vec, len(seeds)


def stamp(img, cx, cy, mask_fn, color, rad):
    """Teken een klein vormpje (mask 0..1) met wrap-around."""
    r = int(rad) + 2
    ys, xs = np.arange(int(cy) - r, int(cy) + r + 1), np.arange(int(cx) - r, int(cx) + r + 1)
    X, Y = np.meshgrid(xs - cx, ys - cy)
    m = np.clip(mask_fn(X, Y), 0, 1)[..., None]
    sub = img[np.ix_(ys % N, xs % N)]
    img[np.ix_(ys % N, xs % N)] = sub * (1 - m) + np.asarray(color) * m


def capsule(x0, y0, x1, y1, w):
    def f(X, Y):
        px, py = X - x0, Y - y0
        bx, by = x1 - x0, y1 - y0
        h = np.clip((px * bx + py * by) / (bx * bx + by * by + 1e-9), 0, 1)
        d = np.hypot(px - bx * h, py - by * h)
        return (w - d) / 1.2
    return f


def grass(flowers=False):
    """Rustig gras: alleen grote, zachte vlakken in een smal tintbereik (geen streepjes; die doen de 3D-plukjes)."""
    v = 0.5 + 0.12 * pnoise(240) + 0.04 * pnoise(110)
    img = ramp("grass", 0.32 + 0.30 * soft_bands(np.clip(v, 0, 1), 4, 0.55))
    if flowers:
        petal, heart = rgb(R["cloud_snow"][4]), rgb(R["accent_yellow"][2])
        for _ in range(60):
            cx, cy = rng.uniform(0, N, 2)
            r = rng.uniform(3.0, 4.5)
            for a in np.linspace(0, 2 * np.pi, 5, endpoint=False) + rng.uniform(0, 1):
                px, py = np.cos(a) * r, np.sin(a) * r
                stamp(img, cx, cy, lambda X, Y, px=px, py=py: (r * 0.75 - np.hypot(X - px, Y - py)) / 1.0, petal, r * 2)
            stamp(img, cx, cy, lambda X, Y: (r * 0.55 - np.hypot(X, Y)) / 1.0, heart, r)
    return img


def dirt():
    v = 0.5 + 0.17 * pnoise(150) + 0.08 * pnoise(45)
    img = ramp("cliff", 0.10 + 0.55 * soft_bands(np.clip(v, 0, 1), 5, 0.45))
    lo, hi = ramp("cliff", np.array(0.0)), ramp("cliff", np.array(0.85))
    for _ in range(420):                                     # kiezeltjes: lichte bovenkant, donkere schaduw
        cx, cy = rng.uniform(0, N, 2)
        rx, ry = rng.uniform(3, 7), rng.uniform(2, 5)
        stamp(img, cx + 1.5, cy + 1.5, lambda X, Y: (1 - (X / rx) ** 2 - (Y / ry) ** 2) * rx / 1.2, lo, rx + 2)
        stamp(img, cx, cy, lambda X, Y: (1 - (X / rx) ** 2 - (Y / ry) ** 2) * rx / 1.2, hi, rx + 2)
    return img


# Generatorstand waarmee de goedgekeurde v1 van het stenen pad (commit a5f7838) is gemaakt; zo blijft die exact gelijk.
PATHSTONES_V1_STATE = {"bit_generator": "PCG64", "state": {"state": 244382717133595458871174245543954773606,
                       "inc": 83116957542241617309460614540412402693}, "has_uint32": 0, "uinteger": 0}


def path_stones():
    """v1 (goedgekeurd): effen keien met een eigen tint, zachte afgeschuinde rand, grond en wat gras in de voegen."""
    ids, edge, vec, n = voronoi(64, 0.55)
    tone = rng.uniform(0.35, 0.75, n)[ids]                 # elke kei een eigen effen tint
    img = ramp("metal", tone)
    gap = 7.0
    L = np.array([-0.6, -0.8])
    dirn = vec / (np.linalg.norm(vec, axis=-1, keepdims=True) + 1e-6)
    lam = (dirn @ L)
    bevel = np.clip(1 - (edge - gap) / 14.0, 0, 1)
    img = img * (1 + 0.16 * bevel[..., None] * np.clip(-lam, 0, 1)[..., None])
    img = img * (1 - 0.22 * bevel[..., None] * np.clip(lam, 0, 1)[..., None])
    v = pnoise(90)
    joint = np.where((v > 0.35)[..., None], ramp("grass", 0.3 + 0.2 * np.clip(v, 0, 1))[..., :],
                     ramp("cliff", 0.15 + 0.1 * np.clip(v + 1, 0, 2)))
    m = np.clip((gap - edge) / 1.5, 0, 1)[..., None]
    return np.clip(img * (1 - m) + joint * m, 0, 255)


def rock():
    ids, edge, vec, n = voronoi(36, 0.65)
    tone = rng.uniform(0.30, 0.80, n)[ids]
    img = ramp("cliff", tone * 0.8 + 0.1 + 0.05 * pnoise(200))
    m = np.clip((3.0 - edge) / 1.5, 0, 1)[..., None]
    return img * (1 - 0.25 * m)


def flat(name, lo, hi, cells=100, jitter=0.9, facet=0.035, drift=0.025, joints=0.0):
    """Effen kleur met grote hoekige vlakken (zoals de cellen van de StylizedRamp-shader op de kliffen):
    elk vlak een eigen tint tussen lo en hi, een lichte schuine facetschaduw, en een heel zachte drift."""
    ids, edge, vec, n = voronoi(cells, jitter)
    tone = rng.uniform(lo, hi, n)[ids]
    ang = rng.uniform(0, 2 * np.pi, n)
    cell_r = N / np.sqrt(n)
    tilt = (vec[..., 0] * np.cos(ang)[ids] + vec[..., 1] * np.sin(ang)[ids]) / cell_r
    tone = tone + facet * tilt + drift * pnoise(320)
    img = ramp(name, tone)
    if joints > 0:                                           # dunne, zachte voeg (stenen vloer)
        img = img * (1 - joints * np.clip((2.5 - edge) / 1.5, 0, 1))[..., None]
    return img


def flowers(img, count, rmin=3.0, rmax=4.5):
    petal, heart = rgb(R["cloud_snow"][4]), rgb(R["accent_yellow"][2])
    for _ in range(count):
        cx, cy = rng.uniform(0, N, 2)
        r = rng.uniform(rmin, rmax)
        for a in np.linspace(0, 2 * np.pi, 5, endpoint=False) + rng.uniform(0, 1):
            px, py = np.cos(a) * r, np.sin(a) * r
            stamp(img, cx, cy, lambda X, Y, px=px, py=py: (r * 0.75 - np.hypot(X - px, Y - py)) / 1.0, petal, r * 2)
        stamp(img, cx, cy, lambda X, Y: (r * 0.55 - np.hypot(X, Y)) / 1.0, heart, r)
    return img


# Terreingras ijken op het gras bovenop de klifmodellen (issue #118). Het terrein gebruikt URP Terrain Lit: het warme
# zonlicht vermenigvuldigt de textuur en trekt het gras naar olijf. Gemeten in de playground (zelfde zon): een
# terreintextuur van #93B84E wordt #A3B03B in beeld, de kliftop (StylizedRamp, toont de ramp zelf) is #AFC852.
# Doel-textuur = kliftop / (beeld / textuur) = ~#9ED16C; de winst hieronder brengt de ramp-mediaan (#A7B647) daarheen.
GRASS_TERRAIN_GAIN = np.array([158 / 167, 209 / 182, 108 / 71])


def terrain_grass(lo, hi, **kw):
    return flat("grass", lo, hi, **kw) * GRASS_TERRAIN_GAIN


# naam -> (maker, tegelgrootte in m voor de TerrainLayer); de tegelgroottes staan ook in StylizedTerrainTools.cs
FLAT = {
    "T_Flat_Grass": lambda: terrain_grass(0.80, 0.94),                       # zoals het gras op de kliffen
    "T_Flat_GrassSmall": lambda: terrain_grass(0.76, 0.96, cells=256),      # kleinere vlakken, iets levendiger
    "T_Flat_GrassShade": lambda: terrain_grass(0.50, 0.66),                  # donkerder gras (onder bomen, randen)
    "T_Flat_GrassFlowers": lambda: flowers(terrain_grass(0.80, 0.94), 35),
    "T_Flat_Dirt": lambda: flat("cliff", 0.30, 0.48),                        # grond / spoorberm
    "T_Flat_Sand": lambda: flat("parchment", 0.10, 0.22, facet=0.025),                    # zandpad
    "T_Flat_Rock": lambda: flat("cliff", 0.55, 0.80, cells=49, facet=0.05),  # zoals de kliffen
    "T_Flat_Stone": lambda: flat("metal", 0.40, 0.80, cells=64, facet=0.05, joints=0.22),   # stenen vloer, effen platen
    "T_Flat_Snow": lambda: flat("cloud_snow", 0.70, 0.92, cells=64, facet=0.025),
}


def save(name, img):
    rel = f"{OUT}/{name}.png"
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB").save(path, optimize=True)
    unity_assets.folder_meta(ROOT, OUT)
    unity_assets.texture_meta(ROOT, rel, srgb=True)
    prev_dir = os.path.join(ROOT, "ArtSource", "Exports", "Terrain")
    os.makedirs(prev_dir, exist_ok=True)
    tile = Image.open(path)
    prev = Image.new("RGB", (N * 2, N * 2))
    for dx in (0, 1):
        for dy in (0, 1):
            prev.paste(tile, (dx * N, dy * N))
    prev.resize((N, N)).save(os.path.join(prev_dir, name + "_preview.png"))
    print("texture", rel)


def main():
    global rng
    def seeded(seed):
        global rng
        rng = np.random.default_rng([38, seed])              # eigen reeks per textuur: wijzigingen lopen niet door
    seeded(1); save("T_Ground_Grass", grass(False))
    seeded(2); save("T_Ground_GrassFlowers", grass(True))
    seeded(3); save("T_Ground_Dirt", dirt())
    rng = np.random.default_rng(); rng.bit_generator.state = PATHSTONES_V1_STATE
    save("T_Ground_PathStones", path_stones())
    seeded(5); save("T_Ground_Rock", rock())
    for k, (name, make) in enumerate(FLAT.items()):
        seeded(100 + k)
        save(name, make())


if __name__ == "__main__":
    main()
