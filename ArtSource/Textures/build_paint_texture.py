"""Geschilderde verftextuur (tegelbaar) voor alle assets — 'gouache'-look van de conceptafbeelding.

Eén RGBA-textuur, lineaire data (geen kleur!) die de positie in de kleurramp van een materiaal verschuift:
  R  zachte, grote vlekken (lichter/donkerder plekken over een vlak)
  G  penseeltoetsen: korte, zachte, gerichte streken in wisselende richting
  B  fijne papier-/verfkorrel
  A  1 (ongebruikt; warm/koel-wisseling volgt R)
Alles is naadloos tegelbaar (periodieke ruis + omwikkelde penseeltoetsen), zodat de shader de
textuur triplanair in objectruimte kan projecteren: geen UV-uitvouw nodig.

Uitvoer:
  Assets/Art/Textures/T_PaintStrokes.png (+ .meta)   Unity (lineair, herhalen)
  ArtSource/Exports/T_PaintStrokes.png               render (three.js)
Gebruik: python ArtSource/Textures/build_paint_texture.py
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import unity_assets  # noqa: E402

N = 1024
rng = np.random.default_rng(20261102)


def periodic_noise(scale_px, aniso=1.0, angle=0.0):
    """Band-gefilterde witte ruis (FFT = automatisch tegelbaar). scale_px ~ grootte van de vlekken."""
    w = rng.standard_normal((N, N))
    fy = np.fft.fftfreq(N)[:, None]
    fx = np.fft.fftfreq(N)[None, :]
    c, s = np.cos(angle), np.sin(angle)
    u = fx * c + fy * s
    v = -fx * s + fy * c
    sigma = 1.0 / (scale_px * 2.2)
    G = np.exp(-((u * aniso) ** 2 + (v / aniso) ** 2) / (2 * sigma ** 2))
    out = np.real(np.fft.ifft2(np.fft.fft2(w) * G))
    out -= out.mean()
    return out / (out.std() + 1e-9)


def normalize(a, lo=0.03, hi=0.97):
    a = (a - np.percentile(a, 1)) / (np.percentile(a, 99) - np.percentile(a, 1) + 1e-9)
    return np.clip(a, 0, 1) * (hi - lo) + lo


def blur_periodic(a, sigma_px):
    fy = np.fft.fftfreq(N)[:, None]
    fx = np.fft.fftfreq(N)[None, :]
    G = np.exp(-(fx ** 2 + fy ** 2) * (2 * np.pi ** 2) * sigma_px ** 2)
    return np.real(np.fft.ifft2(np.fft.fft2(a) * G))


def brush_dabs(count, length, width, strength):
    """Zachte, gerichte penseeltoetsen (ellipsen met zachte rand), omwikkeld over de tegelrand."""
    acc = np.zeros((N, N))
    yy, xx = np.mgrid[-1:2, -1:2]
    # richting volgt een laag-frequent veld: toetsen in een gebied lopen ongeveer dezelfde kant op
    field = periodic_noise(260)
    for _ in range(count):
        cx, cy = rng.uniform(0, N), rng.uniform(0, N)
        ang = field[int(cy) % N, int(cx) % N] * 0.9 + rng.normal(0, 0.25)
        L = length * rng.uniform(0.6, 1.4)
        Wd = width * rng.uniform(0.6, 1.3)
        val = rng.choice([-1, 1]) * strength * rng.uniform(0.4, 1.0)
        R = int(L + 3)
        ys = np.arange(int(cy) - R, int(cy) + R + 1)
        xs = np.arange(int(cx) - R, int(cx) + R + 1)
        X, Y = np.meshgrid(xs - cx, ys - cy)
        c, s = np.cos(ang), np.sin(ang)
        u = X * c + Y * s
        v = -X * s + Y * c
        d = (u / L) ** 2 + (v / Wd) ** 2
        # zachte rand + iets 'droge kwast' (borstelhaartjes) in de lengterichting
        bristle = 0.92 + 0.08 * np.sin(v * 1.2 + rng.uniform(0, 6.28))
        dab = np.clip(1 - d, 0, 1) ** 1.2 * bristle
        acc[np.ix_(ys % N, xs % N)] += val * dab
    return acc


def main():
    blot = normalize(0.65 * periodic_noise(120) + 0.35 * periodic_noise(48))
    strokes = brush_dabs(count=1100, length=70, width=26, strength=1.0)
    strokes += 0.15 * periodic_noise(14, aniso=2.0, angle=0.6)
    strokes = normalize(blur_periodic(strokes, 3.0))
    grain = normalize(0.5 * periodic_noise(2.2) + 0.5 * periodic_noise(5.0))
    img = np.stack([blot, strokes, grain, np.ones_like(blot)], axis=-1)   # A = 1: geen voorvermenigvuldigings-problemen
    img8 = (img * 255).round().astype(np.uint8)
    rel = "Assets/Art/Textures/T_PaintStrokes.png"
    os.makedirs(os.path.join(ROOT, os.path.dirname(rel)), exist_ok=True)
    Image.fromarray(img8, "RGBA").save(os.path.join(ROOT, rel), optimize=True)
    Image.fromarray(img8, "RGBA").save(os.path.join(ROOT, "ArtSource", "Exports", "T_PaintStrokes.png"), optimize=True)
    unity_assets.folder_meta(ROOT, os.path.dirname(rel))
    unity_assets.texture_meta(ROOT, rel, srgb=False)
    # preview: hoe het eruitziet als verschuiving in de treinrood-ramp
    prev = (0.5 + (img[..., 0] - 0.5) * 0.45 + (img[..., 1] - 0.5) * 0.35 + (img[..., 2] - 0.5) * 0.12)
    Image.fromarray((np.clip(prev, 0, 1) * 255).astype(np.uint8), "L").save(
        os.path.join(ROOT, "ArtSource", "Exports", "T_PaintStrokes_preview.png"))
    print("ok", rel)


if __name__ == "__main__":
    main()
