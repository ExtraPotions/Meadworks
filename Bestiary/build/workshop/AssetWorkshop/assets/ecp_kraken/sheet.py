"""Combines the kraken's best preview stills into kraken_sheet.png (plain Python, Pillow).

    python assets/ecp_kraken/sheet.py <preview folder>
"""
import os
import sys

from PIL import Image, ImageDraw

TILE = (640, 480)
VIEWS = [("scene_wide", "attack pose: base 0.6 m under water, raised 2.5 m"), ("scene_deck", "from the deck"),
         ("scene_column", "the column under the head"), ("karve_water", "beside the Karve"),
         ("head_front", "head"), ("head_three_quarter", "three-quarter"), ("head_side", "side"), ("head_back", "back"),
         ("head_lean_forward_35", "kh_neck +35"), ("head_lean_back_18", "kh_neck -18"), ("beak_open", "jaws open"),
         ("scene_surfaced", "surfaced, not raised"), ("tentacle_side_straight", "tentacle at rest (8 m along +Z)"),
         ("tentacle_side_curl", "tentacle curled"), ("tentacle_below_curl", "suckers from below (-Y)"),
         ("karve_deck", "from the Karve's deck"), ("column_bent_x", "kh_body_1..3 +25 about X"),
         ("column_bent_z", "kh_body_1..3 +25 about Z"), ("scene_lunge", "lunging over the rail, column bent back"),
         ("scene_lunge_deck", "the lunge from the deck")]


def main():
    folder = sys.argv[1]
    columns = 4
    rows = (len(VIEWS) + columns - 1) // columns
    sheet = Image.new("RGB", (TILE[0] * columns, TILE[1] * rows), (30, 32, 36))
    draw = ImageDraw.Draw(sheet)
    for index, (name, label) in enumerate(VIEWS):
        path = os.path.join(folder, name + ".png")
        if not os.path.exists(path):
            continue
        x, y = (index % columns) * TILE[0], (index // columns) * TILE[1]
        sheet.paste(Image.open(path).convert("RGB").resize(TILE, Image.LANCZOS), (x, y))
        draw.rectangle((x + 8, y + 8, x + 16 + 7 * len(label), y + 28), fill=(20, 22, 26))
        draw.text((x + 12, y + 11), label, fill=(235, 230, 220))
    sheet.save(os.path.join(folder, "kraken_sheet.png"))
    print("WORKSHOP sheet:", os.path.join(folder, "kraken_sheet.png"))


main()
