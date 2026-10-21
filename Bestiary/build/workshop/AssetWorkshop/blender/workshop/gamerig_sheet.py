"""Contact sheets of a route (b) playback's stills (unity/Assets/Editor/GameRig/GameRigPreview.cs writes them), plain
Python with Pillow:

    python blender/workshop/gamerig_sheet.py <preview folder>

reads <folder>/stills/stills.txt ("<file>|<caption>" per still, in playback order) and writes <folder>/sheet_pair.png
(the game's body beside ours in every phase) and <folder>/sheet_ours.png (ours alone), three stills a row, each
captioned.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

COLUMNS = 3
SCALE = 0.6


def sheet(folder, entries, out):
    """The stills in `entries` [(file, caption)] tiled and captioned into `out`."""
    tiles = [_tile(os.path.join(folder, "stills", name + ".png"), caption) for name, caption in entries]
    if not tiles:
        return None
    width, height = tiles[0].size
    rows = (len(tiles) + COLUMNS - 1) // COLUMNS
    page = Image.new("RGB", (width * COLUMNS, height * rows), (30, 30, 30))
    for i, tile in enumerate(tiles):
        page.paste(tile, ((i % COLUMNS) * width, (i // COLUMNS) * height))
    page.save(out)
    return out


def _tile(path, caption):
    image = Image.open(path).convert("RGB")
    image = image.resize((int(image.width * SCALE), int(image.height * SCALE)), Image.LANCZOS)
    draw = ImageDraw.Draw(image)
    font = _font()
    box = draw.textbbox((6, 4), caption, font=font)
    draw.rectangle((box[0] - 4, box[1] - 2, box[2] + 4, box[3] + 2), fill=(20, 20, 20))
    draw.text((6, 4), caption, fill=(235, 225, 200), font=font)
    return image


def _font():
    for name in ("arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, 14)
        except OSError:
            continue
    return ImageFont.load_default()


def main(folder):
    """Writes both sheets for the playback stills in `folder`."""
    with open(os.path.join(folder, "stills", "stills.txt"), encoding="utf-8") as handle:
        entries = [tuple(line.rstrip("\n").split("|", 1)) for line in handle if "|" in line]
    for kind in ("pair", "ours"):
        chosen = [(n, c.split(":")[0] + (" - game (left), ours (right)" if kind == "pair" else "")) for n, c in entries
                  if n.endswith("_" + kind)]
        print("WORKSHOP sheet:", sheet(folder, chosen, os.path.join(folder, f"sheet_{kind}.png")))


if __name__ == "__main__":
    main(sys.argv[1])
