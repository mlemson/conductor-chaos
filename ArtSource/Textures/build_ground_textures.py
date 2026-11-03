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
    v = 0.52 + 0.16 * pnoise(170) + 0.07 * pnoise(60)
    img = ramp("grass", 0.18 + 0.55 * soft_bands(np.clip(v, 0, 1), 5, 0.45))
    # gestileerde graspluk: 3 korte schuine streepjes, iets donkerder, met een lichte top
    dark, light = ramp("grass", np.array(0.12)), ramp("grass", np.array(0.85))
    for _ in range(170):
        cx, cy = rng.uniform(0, N, 2)
        s = rng.uniform(7, 13)
        for k, ang in enumerate((-0.45, 0.0, 0.45)):
            ln = s * (1.3 if k == 1 else 1.0)
            bx = (k - 1) * 2.5
            tx, ty = bx + np.sin(ang) * ln, -np.cos(ang) * ln
            stamp(img, cx, cy, capsule(bx, 0, tx, ty, 1.8), dark, ln + 4)
            stamp(img, cx, cy, capsule(tx, ty, tx, ty, 1.3), light, ln + 4)
    if flowers:
        petal, heart = rgb(R["cloud_snow"][4]), rgb(R["accent_yellow"][2])
        for _ in range(110):
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


def path_stones():
    ids, edge, vec, n = voronoi(64, 0.55)
    tone = rng.uniform(0.35, 0.75, n)[ids]                 # elke kei een eigen effen tint
    img = ramp("metal", tone)
    gap = 7.0
    # afgeschuinde rand: licht linksboven, donker rechtsonder (licht komt van linksboven)
    L = np.array([-0.6, -0.8])
    dirn = vec / (np.linalg.norm(vec, axis=-1, keepdims=True) + 1e-6)
    lam = (dirn @ L)
    bevel = np.clip(1 - (edge - gap) / 14.0, 0, 1)
    img = img * (1 + 0.16 * bevel[..., None] * np.clip(-lam, 0, 1)[..., None])     # binnenkant naar het licht
    img = img * (1 - 0.22 * bevel[..., None] * np.clip(lam, 0, 1)[..., None])
    # voegen: grond en hier en daar gras
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
    save("T_Ground_Grass", grass(False))
    save("T_Ground_GrassFlowers", grass(True))
    save("T_Ground_Dirt", dirt())
    save("T_Ground_PathStones", path_stones())
    save("T_Ground_Rock", rock())


if __name__ == "__main__":
    main()
