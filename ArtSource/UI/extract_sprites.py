#!/usr/bin/env python3
"""NON-DESTRUCTIVE review for the 44 already exported Conductor Chaos UI sprites.

Do not re-crop the AI concept sheets into Assets/: adjacent objects/edges overlap.
The reviewed PNGs and their GUID-preserving .meta files are the shipped assets.
"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ASSET_ROOT = ROOT / "Assets" / "Art" / "UI"
REVIEW = ROOT / "ArtSource" / "UI" / "Review"

def main():
    icons = sorted((ASSET_ROOT / "Icons").glob("*.png"))
    buttons = sorted((ASSET_ROOT / "Buttons").glob("*.png"))
    if len(icons) != 24 or len(buttons) != 20:
        raise RuntimeError(
            f"Expected 24 icons and 20 UI elements; found {len(icons)} and {len(buttons)}."
        )
    missing = [str(p) for p in icons + buttons if not Path(str(p) + ".meta").exists()]
    if missing:
        raise RuntimeError("Missing Unity .meta for: " + ", ".join(missing))
    REVIEW.mkdir(parents=True, exist_ok=True)
    for label, files, columns in (("icons", icons, 6), ("components", buttons, 4)):
        rows = (len(files) + columns - 1) // columns
        cell_w, cell_h = 245, 230
        sheet = Image.new("RGB", (columns * cell_w, rows * cell_h), "#302C39")
        draw = ImageDraw.Draw(sheet)
        for i, path in enumerate(files):
            im = Image.open(path).convert("RGBA")
            im.thumbnail((cell_w - 24, cell_h - 48), Image.Resampling.LANCZOS)
            x = (i % columns) * cell_w + (cell_w - im.width) // 2
            y = (i // columns) * cell_h + 5
            sheet.paste(im, (x, y), im)
            draw.text((i % columns * cell_w + 12, (i // columns) * cell_h + cell_h - 31),
                      path.stem, fill="white")
        dest = REVIEW / (label + "-contact.png")
        sheet.save(dest, optimize=True)
        print(f"Wrote {dest.relative_to(ROOT)}")
    print("No source artwork or production sprites modified.")

if __name__ == "__main__":
    main()
