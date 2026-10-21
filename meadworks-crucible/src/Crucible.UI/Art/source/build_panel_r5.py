"""Revision 5: the forged set (user decisions 2026-10-13 09:49 and 09:52, "Yes to all, I want something cohesive").
Calm window wood, a window frame of thin forged iron with riveted corner brackets, the dark inset behind reading areas,
carved-wood buttons with an iron trim (normal, hover, pressed), a small iron-cornered side plate, a round iron-rimmed
wooden dial frame and a painted boar for an empty list. Same wood, iron, orange rim and rivets as r4's compass plank.
Same clean-room rules and tools as r2-r4: CC0 maps only (assets/cc0), every shape drawn here, no game, mod or AI pixels.
Run after build_panel_r4.py: python build_panel_r5.py  -> ../textures/r5, ../provenance/meadworks_panel_r5.json
"""
import hashlib, json, os
import numpy as np
from PIL import Image
import build_panel_art as B
import build_panel_set as S
import build_panel_r2 as R
from build_panel_art import blur, noise, norm, ramp
from build_panel_r4 import ORANGE, line, iron, rivet

HERE = os.path.dirname(os.path.abspath(__file__))
R5 = os.path.join(HERE, '..', 'textures', 'r5')
os.makedirs(R5, exist_ok=True)
GOLD = (226, 178, 84)
# Source pixels per UI unit for the window pieces and buttons (Crucible.UI KitForgedArt.WindowPixelsPerUnit).
PX = 2


def save(name, arr):
    Image.fromarray(arr, 'RGBA').save(os.path.join(R5, name + '.png'), optimize=True)


def rgba(rgb, alpha):
    return np.concatenate([np.clip(rgb, 0, 255), np.clip(alpha, 0, 1)[..., None] * 255], -1).astype(np.uint8)


def calm_wood(w, h, seed, tone=1.0, flat=0.55):
    """The set wood with its contrast pulled towards the mean: long straight boards, no swirls."""
    wd = S.wood(w, h, False, 0.95, seed)
    mean = wd.reshape(-1, 3).mean(0)
    wd = mean + (wd - mean) * flat
    wd *= (1 + 0.035 * norm(blur(noise(w, h, 60, seed + 5, 5.0), 8)))[..., None]
    return wd * tone


def window_wood(w=1024, h=1024, seed=71):
    """Calm boards for kit windows: canvas-aligned like the timber wallpaper, but straight grain, wide boards, low contrast."""
    rgb = calm_wood(w, h, seed, 0.86, 0.5) * np.array([1.0, 0.95, 0.9], np.float32)
    yy, xx = S.grid(w, h)
    rng = np.random.default_rng(seed)
    edges = np.cumsum(rng.integers(150, 260, 12))
    edges = edges[edges < h]
    pos = np.full(h, 1e9, np.float32)
    idx = np.zeros(h, int)
    for k, e in enumerate(edges):
        pos = np.minimum(pos, np.abs(np.arange(h) - e))
        idx += (np.arange(h) >= e)
    pos = pos[:, None]
    groove = np.clip(1 - pos / 1.4, 0, 1)
    lip = np.clip(1 - np.abs(pos - 2.6) / 1.4, 0, 1) * (yy > 0)
    rgb *= (1 - 0.30 * groove + 0.05 * lip)[..., None]
    tones = rng.normal(0, 0.03, len(edges) + 2)
    rgb *= (1 + tones[idx])[:, None, None]
    return rgba(rgb, np.ones((h, w), np.float32))


def window_frame(size=320, border=96, edge=6 * PX, arm=46 * PX, thick=12 * PX, seed=72):
    """Nine-sliced overlay: a thin forged-iron edge round the window and an L-shaped iron bracket in each corner with an
    orange rim and three rivets. The centre is transparent; the wood shows through. Corners fit inside the slice border."""
    w = h = size
    yy, xx = S.grid(w, h)
    outer = S.box_sdf(yy, xx, 0, 0, w, h, r=0, chamfer=PX * 1.5)
    edge_d = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))
    band = np.clip(edge - edge_d + 0.5, 0, 1) * np.clip(outer + 0.5, 0, 1)
    metal = iron(w, h, seed)
    rgb = metal * (1 + 0.45 * np.clip(1 - edge_d / edge, 0, 1) * 0.6)[..., None]
    rgb = S.over(rgb, ORANGE, line(edge_d, edge - 1.2, 1.3) * 0.8)
    alpha = band.copy()
    # brackets
    for cx, cy in ((0, 0), (w, 0), (0, h), (w, h)):
        x0, x1 = (0, arm) if cx == 0 else (w - arm, w)
        y0, y1 = (0, arm) if cy == 0 else (h - arm, h)
        hx0, hx1 = x0, x1
        hy0, hy1 = (0, thick) if cy == 0 else (h - thick, h)
        vx0, vx1 = (0, thick) if cx == 0 else (w - thick, w)
        vy0, vy1 = y0, y1
        dh = S.box_sdf(yy, xx, hx0, hy0, hx1, hy1, r=0, chamfer=PX * 2)
        dv = S.box_sdf(yy, xx, vx0, vy0, vx1, vy1, r=0, chamfer=PX * 2)
        d = np.maximum(dh, dv)
        m = np.clip(d + 0.5, 0, 1)
        plate = iron(w, h, seed + cx + 3 * cy) * (1.12 + S.bevel(d, 0, PX * 2.2) * 0.6)[..., None]
        plate = S.over(plate, ORANGE, line(d, PX * 1.6, 1.4) * 0.95)
        plate = S.over(plate, (12, 9, 6), line(d, PX * 1.6 + 2.6, 1.2) * 0.55)
        plate = S.over(plate, (8, 6, 4), line(d, 0.6, 1.4) * 0.8)
        rgb = rgb * (1 - m[..., None]) + plate * m[..., None]
        alpha = np.maximum(alpha, m)
        # its shadow on the wood beside it
        sh = np.clip(1 - (-d) / (PX * 3), 0, 1) * (d < 0) * 0.45
        alpha = np.maximum(alpha, sh)
        rgb = rgb * (1 - (sh * (1 - m))[..., None]) + np.array([6, 4, 2], np.float32) * (sh * (1 - m))[..., None]
        sx = 1 if cx == 0 else -1
        sy = 1 if cy == 0 else -1
        ox, oy = (cx if cx == 0 else cx), (cy if cy == 0 else cy)
        mid = thick / 2
        for px, py in ((ox + sx * mid, oy + sy * mid), (ox + sx * (arm - PX * 5), oy + sy * mid), (ox + sx * mid, oy + sy * (arm - PX * 5))):
            rivet(rgb, px, py, PX * 2.6)
    R.wear(rgb, outer, edge, seed, 0.35)
    rgb = S.over(rgb, (8, 6, 4), line(outer, 0.8, 1.4) * 0.8)
    return rgba(rgb, alpha)


def window_inset(w=512, h=384, rim=12 * PX, seed=73):
    """The dark inset behind a window's reading area: dark-stained wood sunk into the panel. A shadowed top and left inner
    edge, a lit lip at the bottom and right, a thin dark rim. Nine-sliced with a 24-pixel border."""
    yy, xx = S.grid(w, h)
    di = S.box_sdf(yy, xx, 0, 0, w, h, r=PX * 3)
    body = np.clip(di + 0.5, 0, 1)
    stain = calm_wood(w, h, seed, 0.40, 0.45)
    stain = stain * np.array([1.0, 0.92, 0.82], np.float32)
    # sunk: the panel's top/left edge casts a shadow inward, the bottom/right edge catches light
    top = np.clip(1 - yy / (PX * 7), 0, 1) ** 1.6
    left = np.clip(1 - xx / (PX * 6), 0, 1) ** 1.6
    bottom = np.clip(1 - (h - 1 - yy) / (PX * 2.5), 0, 1)
    right = np.clip(1 - (w - 1 - xx) / (PX * 2.5), 0, 1)
    rgb = stain * (1 - 0.55 * np.maximum(top, left))[..., None]
    rgb = rgb + np.array([46, 34, 22], np.float32) * (np.maximum(bottom, right) * 0.6)[..., None]
    rgb = S.over(rgb, (6, 4, 2), line(di, 1.0, 1.6) * 0.85)
    return rgba(rgb, body)


def wood_button(state, w=256, h=80, trim=5 * PX, seed=74):
    """Carved wood in an iron trim. 'n' normal: a rivet at each end. 'h' hover: the trim gets the warm orange rim and a gold
    line inside it. 'p' pressed: darker and sunk (inner shadow, no lit top). 'd' disabled: faded, greyed wood and iron with no rim
    colour, so an unusable button reads as such. Nine-sliced with a 32-pixel border."""
    yy, xx = S.grid(w, h)
    outer = S.box_sdf(yy, xx, 0, 0, w, h, r=PX * 2.5)
    body = np.clip(outer + 0.5, 0, 1)
    tone = {'n': 0.98, 'h': 1.10, 'p': 0.70, 'd': 0.62}[state]
    face = calm_wood(w, h, seed, tone, 0.7)
    face *= np.array([1.04, 1.0, 0.94], np.float32)
    inner = outer - trim
    # carving: the face is a raised panel with a bevel just inside the trim (sunk when pressed)
    carve = S.bevel(inner, 0, PX * 3) * (0.55 if state != 'p' else -0.5)
    face *= (1 + carve)[..., None]
    if state == 'p':
        face *= (1 - 0.4 * np.clip(1 - inner / (PX * 5), 0, 1))[..., None]
    metal = iron(w, h, seed) * (1.25 + S.bevel(outer, 0, trim) * 0.7)[..., None]
    t = np.clip(trim - outer + 0.5, 0, 1)
    rgb = face * (1 - t[..., None]) + metal * t[..., None]
    rgb = S.over(rgb, (10, 7, 4), line(outer, trim + 0.6, 1.3) * 0.7)
    if state == 'h':
        rgb = S.over(rgb, ORANGE, line(outer, trim * 0.5, trim * 0.42) * 0.95)
        rgb = S.over(rgb, GOLD, line(outer, trim + 2.2, 1.2) * 0.9)
    elif state != 'd':
        rgb = S.over(rgb, ORANGE, line(outer, trim - 1.5, 1.1) * (0.55 if state == 'n' else 0.3))
    for cx in (trim + PX * 6.5, w - trim - PX * 6.5):
        rivet(rgb, cx, h / 2, PX * 2.6)
        if state == 'h':
            yy2, xx2 = yy, xx
            glow = np.exp(-((xx2 - cx) ** 2 + (yy2 - h / 2) ** 2) / (PX * 3.5) ** 2) * 0.35
            rgb = S.over(rgb, GOLD, glow)
    rgb = S.over(rgb, (8, 6, 4), line(outer, 0.7, 1.3) * 0.85)
    if state == 'd':
        grey = S.lum(rgb)[..., None]
        rgb = rgb * 0.45 + grey * 0.55
    return rgba(rgb, body)


def side_plate(size=192, border=40, seed=75):
    """A small plate for HUD and map side panels: calm dark wood with a thin iron edge and a riveted iron corner piece in each
    corner, the orange rim inside the iron. Nine-sliced with a 40-pixel border (20 units)."""
    w = h = size
    yy, xx = S.grid(w, h)
    outer = S.box_sdf(yy, xx, 0, 0, w, h, r=0, chamfer=PX * 1.5)
    body = np.clip(outer + 0.5, 0, 1)
    rgb = calm_wood(w, h, seed, 0.62, 0.5)
    edge = 3 * PX
    t = np.clip(edge - outer + 0.5, 0, 1)
    rgb = rgb * (1 - t[..., None]) + iron(w, h, seed)[..., :] * t[..., None]
    rgb = S.over(rgb, ORANGE, line(outer, edge - 0.8, 1.1) * 0.7)
    rgb *= (1 - 0.35 * np.clip(1 - (outer - edge) / (PX * 3), 0, 1) * (outer > edge))[..., None]
    c = 15 * PX
    for x0, y0 in ((0, 0), (w - c, 0), (0, h - c), (w - c, h - c)):
        d = S.box_sdf(yy, xx, x0, y0, x0 + c, y0 + c, r=0, chamfer=PX * 3)
        # a corner piece: an L-cut square (the inner quarter cut away diagonally)
        cx, cy = (x0 + c if x0 == 0 else x0), (y0 + c if y0 == 0 else y0)
        cut = (np.abs(xx - cx) + np.abs(yy - cy)) - c * 0.62
        d = np.minimum(d, cut)
        m = np.clip(d + 0.5, 0, 1)
        plate = iron(w, h, seed + x0 + y0) * (1.12 + S.bevel(d, 0, PX * 2) * 0.6)[..., None]
        plate = S.over(plate, ORANGE, line(d, PX * 1.4, 1.2) * 0.9)
        rgb = rgb * (1 - m[..., None]) + plate * m[..., None]
        rivet(rgb, x0 + (PX * 5 if x0 == 0 else c - PX * 5), y0 + (PX * 5 if y0 == 0 else c - PX * 5), PX * 2.0)
    rgb = S.over(rgb, (8, 6, 4), line(outer, 0.7, 1.3) * 0.85)
    return rgba(rgb, body)


def dial_frame(size=512, ring=40, seed=76):
    """A round dial: a wooden ring around a dark, sunk centre inside a forged-iron ring with the orange rim on
    its inner edge and eight rivets. Transparent outside the ring; a needle or rose is drawn on top by the product."""
    w = h = size
    yy, xx = S.grid(w, h)
    c = size / 2
    r = np.hypot(xx - c + 0.5, yy - c + 0.5)
    R0 = size / 2 - 2
    body = np.clip(R0 - r + 0.5, 0, 1)
    face = calm_wood(w, h, seed, 0.92, 0.6)
    face *= (1 - 0.35 * np.clip((r - (R0 - ring) * 0.55) / ((R0 - ring) * 0.45), 0, 1) ** 2)[..., None]
    di = R0 - r                                          # inside distance from the outer edge
    m = np.clip(ring - di + 0.5, 0, 1)
    metal = iron(w, h, seed) * (1.08 + S.bevel(di, 0, ring) * 0.55)[..., None]
    rgb = face * (1 - m[..., None]) + metal * m[..., None]
    rgb = S.over(rgb, ORANGE, line(di, ring - 2.5, 2.0) * 0.95)
    rgb = S.over(rgb, (10, 7, 4), line(di, ring + 1.5, 2.2) * 0.75)
    rgb *= (1 - 0.4 * np.clip(1 - (di - ring) / 14, 0, 1) * (di > ring))[..., None]
    # a dark, sunk centre inside a wooden ring, so a gold needle or the game's wind arrow reads on it
    inner = (R0 - ring) * 0.66
    ci = inner - r
    well = calm_wood(w, h, seed + 1, 0.36, 0.45) * np.array([1.0, 0.92, 0.82], np.float32)
    well *= (1 - 0.45 * np.clip(1 - ci / 18, 0, 1))[..., None]
    wm = np.clip(ci + 0.5, 0, 1)
    rgb = rgb * (1 - wm[..., None]) + well * wm[..., None]
    rgb = S.over(rgb, ORANGE, line(ci, -1.5, 1.6) * 0.6)
    rgb = S.over(rgb, (8, 6, 4), line(ci, 0.5, 1.6) * 0.8)
    rgb = S.over(rgb, (8, 6, 4), line(di, 1.0, 1.8) * 0.85)
    for k in range(8):
        a = k * np.pi / 4 + np.pi / 8
        rivet(rgb, c + np.cos(a) * (R0 - ring / 2), c + np.sin(a) * (R0 - ring / 2), 6.5)
    R.wear(rgb, di, ring, seed, 0.3)
    return rgba(rgb, body)


def ellipse(yy, xx, cx, cy, a, b, rot=0.0):
    ca, sa = np.cos(rot), np.sin(rot)
    x, y = (xx - cx) * ca + (yy - cy) * sa, -(xx - cx) * sa + (yy - cy) * ca
    return (1 - np.sqrt((x / a) ** 2 + (y / b) ** 2)) * min(a, b)


def smax(a, b, k):
    """Smooth union of two inside-distance fields."""
    h = np.clip(0.5 + 0.5 * (a - b) / k, 0, 1)
    return a * h + b * (1 - h) + k * h * (1 - h)


def empty_boar(w=384, h=256, seed=77):
    """A boar for an empty Herd Book page, painted: stocky soft-blended masses, a dark bristle mane along the back, soft fur
    strokes from a CC0 carpet scan, light from the upper left, a ground shadow. Muted so it sits quietly on the dark inset."""
    yy, xx = S.grid(w, h)
    parts = [ellipse(yy, xx, 214, 132, 114, 64), ellipse(yy, xx, 150, 112, 74, 60, -0.2), ellipse(yy, xx, 88, 134, 58, 46, 0.35),
             ellipse(yy, xx, 42, 158, 38, 23, 0.22)]
    d = parts[0]
    for p in parts[1:]:
        d = smax(d, p, 16)
    d = smax(d, ellipse(yy, xx, 14, 166, 9, 13, 0.2), 4)                     # snout disc
    d = smax(d, ellipse(yy, xx, 112, 72, 11, 27, -0.55), 5)                  # ear
    for x0, x1, y0, y1 in ((106, 134, 160, 226), (144, 168, 162, 222), (258, 286, 160, 226), (294, 318, 158, 220)):
        d = smax(d, S.box_sdf(yy, xx, x0, y0, x1, y1, r=10), 12)
    d = smax(d, ellipse(yy, xx, 330, 110, 16, 5, -0.9), 4)                   # tail
    back = 70 + 0.0011 * (xx - 165) ** 2
    mane = np.clip(1 - np.abs(yy - back) / 16, 0, 1) * np.clip((xx - 70) / 30, 0, 1) * np.clip((290 - xx) / 40, 0, 1)
    spikes = np.clip(norm(blur(noise(w, h, 2, seed + 2, 0.35), 0.5)), -1, 2.5)
    rough = norm(blur(noise(w, h, 5, seed, 0.8), 0.8)) * 0.9
    d = d + rough + mane * spikes * 5
    alpha = np.clip(d / 1.4 + 0.5, 0, 1)
    carpet = blur(S.cover('ambientcg/Carpet016/Carpet016_1K-JPG_Displacement.jpg', w, h, True, 0.5), 1.0)
    strokes = blur(noise(w, h, 2.2, seed + 3, 2.5), 0.9)
    fur = 0.5 + 0.045 * norm(carpet) + 0.04 * norm(strokes)
    # volume: a height field from the blurred inside distance, lit from the upper left (painted, not flat)
    height = blur(np.sqrt(np.clip(d, 0, 48) / 48), 5)
    gy, gx = np.gradient(height * 60)
    nz = 1.0 / np.sqrt(gx ** 2 + gy ** 2 + 1)
    lam = np.clip((gx * 0.5 + gy * 0.62) * nz + 0.6 * nz, 0, 1.2)
    shade = 0.42 + 0.78 * lam
    far = np.zeros_like(d)
    for x0, x1 in ((144, 168), (294, 318)):
        far = np.maximum(far, np.clip(S.box_sdf(yy, xx, x0, 150, x1, 230, r=10) / 4 + 0.5, 0, 1) * np.clip((yy - 160) / 18, 0, 1))
    shade *= 1 - 0.3 * far
    shade *= 1 - 0.28 * np.clip((yy - 150) / 70, 0, 1) * (1 - far)
    shade *= 1 - 0.35 * mane
    ear = np.clip(ellipse(yy, xx, 113, 76, 8, 20, -0.55) / 4, 0, 1)
    shade *= 1 - 0.3 * ear
    base = ramp(fur, [(0, (40, 28, 19)), (0.5, (104, 74, 49)), (1, (170, 132, 92))])
    rgb = base * shade[..., None]
    rim = np.clip(1 - np.abs(d - 2.5) / 2.5, 0, 1) * np.clip(gy * 2, 0, 1)
    rgb = S.over(rgb, (196, 160, 112), rim * 0.35)
    tusk = ellipse(yy, xx, 38, 170, 11, 3.8, -0.9)
    rgb = S.over(rgb, (210, 192, 156), np.clip(tusk + 0.5, 0, 1))
    rgb = S.over(rgb, (12, 9, 7), np.clip(ellipse(yy, xx, 80, 122, 3.8, 3.2) + 0.5, 0, 1))
    sh = np.clip(ellipse(yy, xx, 212, 228, 132, 9) / 9 + 0.3, 0, 1) * 0.5
    a = np.maximum(alpha, sh)
    rgb = rgb * (alpha / np.maximum(a, 1e-6))[..., None]
    return rgba(rgb, a)


def build_all():
    P = {'window_wood': window_wood(), 'window_frame': window_frame(), 'window_inset': window_inset(),
         'button_wood': wood_button('n'), 'button_wood_hover': wood_button('h'), 'button_wood_pressed': wood_button('p'), 'button_wood_disabled': wood_button('d'),
         'side_plate': side_plate(), 'dial_frame': dial_frame(), 'empty_boar': empty_boar()}
    for n, a in P.items():
        save(n, a)
    return P


if __name__ == '__main__':
    build_all()
    files = {}
    for f in sorted(os.listdir(R5)):
        p = os.path.join(R5, f)
        files[f] = {'size': list(Image.open(p).size), 'sha256': hashlib.sha256(open(p, 'rb').read()).hexdigest()}
    info = {'item': 'meadworks_panel_r5', 'licence': 'CC BY-NC-SA 4.0 (MeadWorks asset licence); CC0 inputs are public domain',
            'method': 'Python (Pillow/numpy); no pixels of PackPanel, of any game or mod texture or of the concept sketches, no AI tools; build/build_panel_r5.py (+ r2/r4/set modules)',
            'cc0_inputs': sorted(B.used), 'cc0_sha256': B.used,
            'changes': 'The forged set (user decisions 2026-10-13 09:49 and 09:52): calm window wood (the set wood, contrast pulled in, wide straight '
                       'boards); a nine-sliced window frame of thin forged iron with riveted corner brackets and the orange rim (186, 112, 28); a dark '
                       'stained, sunk inset for reading areas; carved-wood buttons in an iron trim (normal, hover with the orange rim and a gold line, '
                       'pressed darker and sunk, disabled faded and grey); a small iron-cornered side plate; a round iron-rimmed wooden dial frame; a painted boar (soft-blended '
                       'masses, bristles, CC0 carpet fur, upper-left light) for an empty Herd Book.',
            'files': files}
    json.dump(info, open(os.path.join(HERE, '..', 'provenance', 'meadworks_panel_r5.json'), 'w'), indent=2)
    print('ok', len(files))
