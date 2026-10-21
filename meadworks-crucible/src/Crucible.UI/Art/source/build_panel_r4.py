"""Revision 4: Cairn's iron-bound compass plank, its heading inset and an iron rivet (user decision 2026-10-13, compass option 2).
Same clean-room rules and tools as r2/r3: CC0 maps only (assets/cc0), every shape drawn here, no game, mod or AI pixels.
The orange rim is the colour of the game's iron button rim, written down as a number (186, 112, 28); no pixels are read.
Run after build_panel_r3.py: python build_panel_r4.py  -> ../textures/r4, ../provenance/meadworks_panel_r4.json
"""
import hashlib, json, os
import numpy as np
from PIL import Image
import build_panel_art as B
import build_panel_set as S
import build_panel_r2 as R
from build_panel_art import blur, noise, norm, ramp

HERE = os.path.dirname(os.path.abspath(__file__))
R4 = os.path.join(HERE, '..', 'textures', 'r4')
os.makedirs(R4, exist_ok=True)
ORANGE = (186, 112, 28)
# Source pixels per UI unit: the plank is drawn 240 x 51 units on Cairn's HUD (HudLayout), so 960 x 204 here.
PX = 4


def save(name, arr):
    Image.fromarray(arr, 'RGBA').save(os.path.join(R4, name + '.png'), optimize=True)


def line(di, at, width):
    """A soft line along the inside-distance contour di == at."""
    return np.clip(1 - np.abs(di - at) / width, 0, 1)


def iron(w, h, seed):
    """Forged iron: the set's steel, warmed towards the game's brown-grey iron and hammered (soft dents from blurred noise)."""
    st = S.steel(w, h) * np.array([1.62, 1.45, 1.18], np.float32)
    dents = norm(blur(noise(w, h, 9, seed + 400), 1.6))
    return st * (1 + 0.07 * dents)[..., None] + np.array([6, 3, 0], np.float32)


def rivet(rgb, cx, cy, r):
    yy, xx = S.grid(rgb.shape[1], rgb.shape[0])
    col, a, seat = S.sphere(yy, xx, cx, cy, r, (104, 106, 108))
    rgb[:] = S.over(rgb, (8, 8, 8), seat * 0.7)
    rgb[:] = rgb * (1 - a[..., None]) + col * a[..., None]


def compass_plank(w=240 * PX, h=51 * PX, cap=22 * PX, band=3 * PX, seed=61):
    """One horizontal plank between two forged-iron end caps, thin iron bands along the top and bottom, rivets, and the orange rim
    where iron meets wood. The wood stays calm in the middle so the clock, ticks and numbers read."""
    yy, xx = S.grid(w, h)
    outer = S.box_sdf(yy, xx, 0, 0, w, h, r=0, chamfer=PX * 2.5)
    body = np.clip(outer + 0.5, 0, 1)
    # the plank: one board, grain along its length, a lit top edge and a shaded bottom edge
    wood = S.wood(w, h, False, 0.95, seed) * 0.82
    wood *= (1 + 0.05 * norm(blur(noise(w, h, 40, seed + 1, 4.0), 6)))[..., None]
    vy = (yy - band) / max(1.0, h - 2 * band)
    wood *= (1.06 - 0.18 * np.clip(vy, 0, 1) ** 1.5)[..., None]
    rgb = wood
    # iron bands: full length under the caps
    bands = ((yy < band) | (yy >= h - band)).astype(np.float32)
    bd = np.minimum(yy, h - 1 - yy)                                   # distance from the outer long edge
    band_shade = 1 + 0.35 * np.where(yy < h / 2, 1 - bd / band, -(1 - bd / band) * 0.6)
    rgb = rgb * (1 - bands[..., None]) + iron(w, h, seed) * band_shade[..., None] * bands[..., None]
    # shadow the bands cast on the wood, then the orange rim on the wood side of each band
    rgb *= (1 - 0.45 * np.clip(1 - (np.minimum(yy, h - 1 - yy) - band) / (PX * 2.5), 0, 1) * (1 - bands))[..., None]
    for edge in (band + 0.5, h - band - 1.5):
        rgb = S.over(rgb, ORANGE, np.clip(1 - np.abs(yy - edge) / 2.2, 0, 1) * 0.85 * (xx > cap) * (xx < w - cap))
    # end caps: forged iron plates over the band ends, each with a bevel, a dark seam, an orange inset rim and two rivets
    for x0 in (0, w - cap):
        cd = S.box_sdf(yy, xx, x0, 0, x0 + cap, h, r=0, chamfer=PX * 2.5)
        m = np.clip(cd + 0.5, 0, 1)
        plate = iron(w, h, seed + x0) * (1 + S.bevel(cd, 0, PX * 2.2) * 0.6)[..., None]
        plate = S.over(plate, ORANGE, line(cd, PX * 2.6, 1.6) * 0.9)
        plate = S.over(plate, (14, 10, 6), line(cd, PX * 2.6 + 3, 1.4) * 0.6)
        rgb = rgb * (1 - m[..., None]) + plate * m[..., None]
        seam = line(cd, 0.5, 2.2) * (np.abs(xx - (x0 + cap if x0 == 0 else x0)) < 3)
        rgb = S.over(rgb, (8, 6, 4), seam * 0.85)
        cx = x0 + cap / 2
        for cy in (PX * 6.5, h - PX * 6.5):
            rivet(rgb, cx, cy, PX * 1.9)
    # band rivets beside each cap (inside the sliced corners, so they never stretch)
    for cx in (cap + PX * 4, w - cap - PX * 4):
        for cy in (band / 2, h - band / 2):
            rivet(rgb, cx, cy, PX * 1.05)
    R.wear(rgb, outer, band, seed, 0.5)
    rgb = S.over(rgb, (10, 8, 6), line(outer, 1.5, 2.2) * 0.85)
    rgb = rgb * (1 + 0.012 * norm(noise(w, h, 2, 77 + seed)))[..., None]
    return np.concatenate([np.clip(rgb, 0, 255), body[..., None] * 255], -1).astype(np.uint8)


def compass_inset(w=64 * PX, h=14 * PX, seed=62):
    """The notch in the plank's lower band that holds the heading: a dark recessed well framed in iron with the orange rim,
    chamfered at the top so it reads as cut into the band."""
    yy, xx = S.grid(w, h)
    # flat bottom, chamfered top corners
    di = S.box_sdf(yy, xx, 0, 0, w, h + PX * 4, r=0, chamfer=PX * 3.5)
    body = np.clip(di + 0.5, 0, 1)
    rim = PX * 1.6
    well = S.wood(w, h, False, 0.8, seed) * 0.32
    well *= (1 - 0.4 * np.clip(1 - (di - rim) / (PX * 2.5), 0, 1))[..., None]
    frame = iron(w, h, seed) * (1 + S.bevel(di, 0, rim) * 0.6)[..., None]
    f = np.clip((rim - di) / 1.2, 0, 1)
    rgb = well * (1 - f[..., None]) + frame * f[..., None]
    rgb = S.over(rgb, ORANGE, line(di, rim + 0.5, 1.6) * 0.9)
    rgb = S.over(rgb, (10, 8, 6), line(di, 1.5, 2.0) * 0.85)
    return np.concatenate([np.clip(rgb, 0, 255), body[..., None] * 255], -1).astype(np.uint8)


def iron_rivet(size=32):
    """A single domed iron rivet with its dark seat, transparent around it (corners of the iron sleep plate)."""
    rgb = np.zeros((size, size, 3), np.float32)
    yy, xx = S.grid(size, size)
    c = size / 2
    col, a, seat = S.sphere(yy, xx, c, c, size * 0.3, (104, 106, 108))
    rgb = rgb * (1 - a[..., None]) + col * a[..., None]
    alpha = np.maximum(a, seat * 0.7)
    rgb = rgb * (a / np.maximum(alpha, 1e-6))[..., None]           # the seat is dark: colour 0 at its alpha
    return np.concatenate([np.clip(rgb, 0, 255), alpha[..., None] * 255], -1).astype(np.uint8)


def build_all():
    P = {'compass_plank': compass_plank(), 'compass_inset': compass_inset(), 'iron_rivet': iron_rivet()}
    for n, a in P.items():
        save(n, a)
    return P


if __name__ == '__main__':
    build_all()
    files = {}
    for f in sorted(os.listdir(R4)):
        p = os.path.join(R4, f)
        files[f] = {'size': list(Image.open(p).size), 'sha256': hashlib.sha256(open(p, 'rb').read()).hexdigest()}
    info = {'item': 'meadworks_panel_r4', 'licence': 'CC BY-NC-SA 4.0 (MeadWorks asset licence); CC0 inputs are public domain',
            'method': 'Python (Pillow/numpy); no pixels of PackPanel, of any game or mod texture or of the concept sketches, no AI tools; build/build_panel_r4.py (+ r2/set modules)',
            'cc0_inputs': sorted(B.used), 'cc0_sha256': B.used,
            'changes': 'Cairn compass (user decision 2026-10-13, option 2 iron-bound wood): one plank of the set wood between two forged-iron end caps '
                       '(the set steel, hammered), thin iron bands top and bottom, domed rivets, and an orange rim (186, 112, 28, the colour of the game\'s '
                       'iron button rim written down as a number) where iron meets wood; a heading inset cut into the lower band; a lone iron rivet for '
                       'the corners of the sleep row\'s iron plate.',
            'files': files}
    json.dump(info, open(os.path.join(HERE, '..', 'provenance', 'meadworks_panel_r4.json'), 'w'), indent=2)
    print('ok', len(files))
