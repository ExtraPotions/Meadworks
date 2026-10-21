"""Builds MeadWorks' own Timber panel art (panel_background.png 1254x1254, panel_button.png 1983x793).

Clean-room: nothing here reads PackPanel's timber_*.png. Inputs are CC0 maps only (assets/cc0, Poly Haven and
ambientCG), merged, regraded to a palette written down from a verbal look description, shaded and painted in code.
Run: python build_panel_art.py   (writes ../textures and ../provenance)
"""
import hashlib, json, os
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..', '..'))
CC0 = os.path.join(ROOT, 'assets', 'cc0')
OUT = os.path.join(HERE, '..', 'textures')
PROV = os.path.join(HERE, '..', 'provenance')
os.makedirs(OUT, exist_ok=True)
os.makedirs(PROV, exist_ok=True)
used = {}


def load(rel, gray=False):
    p = os.path.join(CC0, rel)
    used[rel] = hashlib.sha256(open(p, 'rb').read()).hexdigest()
    im = Image.open(p).convert('L' if gray else 'RGB')
    return np.asarray(im, dtype=np.float32) / 255.0


def resize(a, w, h):
    if a.ndim == 2:
        return np.asarray(Image.fromarray((a * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), dtype=np.float32) / 255.0
    return np.stack([resize(a[..., i], w, h) for i in range(a.shape[2])], -1)


def mirror_fill(a, w, h):
    """Cover w x h by mirrored tiling so there are no visible seams."""
    th, tw = a.shape[:2]
    ny, nx = -(-h // th), -(-w // tw)
    rows = []
    for j in range(ny):
        row = []
        for i in range(nx):
            t = a
            if i % 2:
                t = t[:, ::-1]
            if j % 2:
                t = t[::-1]
            row.append(t)
        rows.append(np.concatenate(row, 1))
    return np.concatenate(rows, 0)[:h, :w]


def blur(a, r):
    return np.asarray(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0


def noise(w, h, scale, seed, sx=1.0):
    rng = np.random.default_rng(seed)
    gw, gh = max(2, int(w / scale / sx)), max(2, int(h / scale))
    g = rng.random((gh, gw)).astype(np.float32)
    return np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), dtype=np.float32) / 255.0


def norm(a):
    return (a - a.mean()) / (a.std() + 1e-6)


def ramp(t, stops):
    t = np.clip(t, 0, 1)
    xs = [s[0] for s in stops]
    return np.stack([np.interp(t, xs, [s[1][c] for s in stops]) for c in range(3)], -1).astype(np.float32)


def build_background():
    W = H = 1254
    c1 = load('ambientcg/Wood066/Wood066_1K-JPG_Color.jpg')
    c2 = load('ambientcg/Wood049/Wood049_1K-JPG_Color.jpg')
    d1 = load('ambientcg/Wood049/Wood049_1K-JPG_Displacement.jpg', True)
    d2 = load('ambientcg/Wood051/Wood051_1K-JPG_Displacement.jpg', True)
    d3 = load('polyhaven/brown_leather/brown_leather_disp_1k.png', True)
    lum = lambda c: c @ np.array([0.3, 0.59, 0.11], np.float32)

    wy = (norm(blur(noise(W, H, 300, 5, 3.0), 30)) * 22).astype(np.float32)
    wx = (norm(blur(noise(W, H, 260, 6, 1.0), 30)) * 12).astype(np.float32)
    gyy, gxx = np.mgrid[0:H, 0:W]
    iy = np.clip(gyy + wy, 0, H - 1).astype(int)
    ix = np.clip(gxx + wx, 0, W - 1).astype(int)

    def stretch(a, fx):
        return mirror_fill(resize(a, int(a.shape[1] * fx), a.shape[0]), W, H)[iy, ix]

    L1 = stretch(lum(c1), 1.6)
    L2 = stretch(np.roll(lum(c2)[::-1], 400, 1), 1.9)
    R1 = stretch(d1, 2.2)
    R2 = stretch(np.roll(d2, 300, 0), 2.6)
    pore = mirror_fill(resize(d3, 640, 640), W, H)
    mix = blur(noise(W, H, 260, 7, 2.0), 40)
    mix = np.clip((norm(mix)) * 0.9 + 0.5, 0, 1)
    base = norm(L1) * (1 - mix) + norm(L2) * mix
    relief = norm(R1) * 0.6 + norm(R2) * 0.4
    drift = norm(blur(noise(W, H, 380, 11, 1.8), 60))
    tone = 0.50 + 0.060 * norm(base) + 0.040 * relief + 0.035 * drift + 0.012 * norm(pore)
    rng = np.random.default_rng(3)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    for _ in range(5):
        cx, cy = rng.uniform(100, W - 100), rng.uniform(100, H - 100)
        r = rng.uniform(26, 50)
        g = np.exp(-(((xx - cx) / (r * 2.4)) ** 2 + ((yy - cy) / r) ** 2))
        tone -= 0.05 * g
    img = ramp(tone, [(0.0, (60, 40, 29)), (0.5, (101, 71, 52)), (1.0, (146, 106, 76))])
    img *= (1 + 0.012 * norm(noise(W, H, 3, 21)))[..., None]
    img[..., 2] *= 1 + 0.02 * drift
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB')


def build_button():
    W, Hh = 1983, 793
    X0, Y0, X1, Y1 = 57, 105, 1927, 656   # frame outer rectangle (the 9-slice area)
    CH = 34
    FR = 42
    yy, xx = np.mgrid[0:Hh, 0:W].astype(np.float32)
    dx = np.maximum(X0 - xx, xx - (X1 - 1))
    dy = np.maximum(Y0 - yy, yy - (Y1 - 1))
    d = np.maximum(dx, dy)
    for cx, cy, sx, sy in [(X0, Y0, 1, 1), (X1 - 1, Y0, -1, 1), (X0, Y1 - 1, 1, -1), (X1 - 1, Y1 - 1, -1, -1)]:
        diag = ((xx - cx) * -sx + (yy - cy) * -sy + CH) / 1.4142
        d = np.maximum(d, diag)
    di = -d
    # normalised so blur() (8-bit) keeps resolution
    dn = np.clip(di, -20, 120) / 140.0 + 0.15
    gy, gx = np.gradient(blur(dn, 1.2) * 140.0)
    gl = np.hypot(gx, gy) + 1e-6
    nx_, ny_ = -gx / gl, -gy / gl
    light = (-nx_ * 0.62 - ny_ * 0.78)

    m1 = load('ambientcg/Metal046A/Metal046A_1K-JPG_Color.jpg', True)
    m2 = load('ambientcg/Metal024/Metal024_1K-JPG_Color.jpg', True)
    mr = load('ambientcg/Metal046A/Metal046A_1K-JPG_Roughness.jpg', True)
    M1 = mirror_fill(resize(m1, 1536, 768), W, Hh)
    M2 = mirror_fill(np.roll(resize(m2, 1280, 640), 200, 1), W, Hh)
    MR = mirror_fill(resize(mr, 1536, 768), W, Hh)
    streak = blur(noise(W, Hh, 5, 31, 14.0), 0.8)
    mt = 0.40 + 0.05 * norm(M1) + 0.02 * norm(M2) + 0.03 * norm(MR) + 0.03 * norm(streak)
    band = np.clip((di - 3) / FR, 0, 1)
    crown = 1 - np.abs(band * 2 - 1)
    tone = mt + 0.20 * light * np.clip(1 - np.abs(di - 7) / 6, 0, 1)
    tone = tone - 0.20 * light * np.clip(1 - np.abs(di - (FR - 2)) / 5, 0, 1)
    tone = tone + 0.10 * crown
    bronze = ramp(tone, [(0.0, (34, 24, 16)), (0.32, (74, 52, 31)), (0.55, (112, 80, 47)), (0.80, (160, 118, 70)), (1.0, (206, 160, 104))])
    wear = norm(blur(noise(W, Hh, 9, 41, 3.0), 0.6)) + 0.6 * norm(M2)
    crest = np.clip(1 - np.abs(di - 5) / 4, 0, 1)
    bronze = bronze + (np.array([84, 56, 28], np.float32) * (np.clip(wear - 1.5, 0, 1.0) * crest * 0.6)[..., None])
    patina = np.clip(norm(blur(noise(W, Hh, 40, 51, 3.0), 3)) - 1.2, 0, 1) * (1 - crest)
    bronze = bronze * (1 - 0.32 * patina)[..., None] + np.array([46, 58, 44], np.float32) * (0.32 * patina)[..., None]

    l1 = load('ambientcg/Leather031/Leather031_1K-JPG_Color.jpg', True)
    l2 = load('ambientcg/Leather030/Leather030_1K-JPG_Displacement.jpg', True)
    L1 = mirror_fill(resize(l1, 768, 768), W, Hh)
    L2 = mirror_fill(resize(l2, 640, 640), W, Hh)
    lt = 0.5 + 0.10 * norm(L1) + 0.10 * norm(L2) + 0.05 * norm(blur(noise(W, Hh, 120, 61, 2.0), 20))
    inset = ramp(lt, [(0.0, (12, 9, 7)), (0.5, (27, 22, 17)), (1.0, (48, 39, 30))])
    inner = np.clip((di - FR) / 26, 0, 1)
    sh = 1 - 0.55 * (1 - inner) * (0.55 + 0.45 * np.clip(light, -1, 1))
    inset = inset * sh[..., None]

    in_band = np.clip((di - 0.5) / 1.5, 0, 1) * np.clip((FR + 1 - di) / 1.5, 0, 1)
    in_inset = np.clip((di - FR) / 1.5, 0, 1)
    rgb = bronze * in_band[..., None] + inset * in_inset[..., None]
    outline = np.clip(1 - np.abs(di - 1.5) / 2.2, 0, 1)
    rgb = rgb * (1 - 0.8 * outline[..., None]) + np.array([14, 10, 7], np.float32) * (0.8 * outline[..., None])
    groove = np.clip(1 - np.abs(di - (FR + 1)) / 2.0, 0, 1)
    rgb = rgb * (1 - 0.7 * groove[..., None]) + np.array([10, 7, 5], np.float32) * (0.7 * groove[..., None])

    for cx, cy in [(X0 + 46, Y0 + 46), (X1 - 47, Y0 + 46), (X0 + 46, Y1 - 47), (X1 - 47, Y1 - 47)]:
        r = np.hypot(xx - cx, yy - cy)
        seat = np.clip(1 - np.abs(r - 16) / 3.0, 0, 1) * (r > 13)
        rgb = rgb * (1 - 0.75 * seat[..., None]) + np.array([12, 9, 6], np.float32) * (0.75 * seat[..., None])
        k = np.sqrt(np.clip(1 - (r / 14.0) ** 2, 0, 1))
        hx, hy = (xx - cx) / 14.0, (yy - cy) / 14.0
        shade = 0.55 + 0.55 * np.clip(-hx * 0.55 - hy * 0.7 + k * 0.6, 0, 1.2)
        rv = ramp(shade * 0.62, [(0.0, (30, 21, 14)), (0.5, (110, 78, 46)), (1.0, (222, 178, 118))])
        spec = np.exp(-(((hx + 0.35) ** 2 + (hy + 0.4) ** 2) / 0.03)) * 90
        rv = rv + spec[..., None]
        a = np.clip(14.5 - r, 0, 1)[..., None]
        rgb = rgb * (1 - a) + rv * a

    body = np.clip(0.5 + di, 0, 1)
    sdist = np.maximum(np.maximum(X0 - xx, xx - (X1 - 1)), np.maximum(Y0 + 7 - yy, yy - (Y1 - 1 + 7)))
    shadow = np.clip(1 - sdist / 14.0, 0, 1) * 0.30 * (sdist > -1)
    alpha = body + (1 - body) * shadow
    col = rgb * body[..., None] + np.array([8, 6, 4], np.float32) * (1 - body[..., None])
    out = np.concatenate([np.clip(col, 0, 255), alpha[..., None] * 255], -1).astype(np.uint8)
    return Image.fromarray(out, 'RGBA')


if __name__ == '__main__':
    bg = build_background()
    used_bg = dict(used)
    used.clear()
    bg.save(os.path.join(OUT, 'panel_background.png'), optimize=True)
    btn = build_button()
    used_btn = dict(used)
    btn.save(os.path.join(OUT, 'panel_button.png'), optimize=True)

    def entry(name, size, inputs, changes):
        h = hashlib.sha256(open(os.path.join(OUT, name), 'rb').read()).hexdigest()
        return {'file': name, 'size': size, 'sha256': h, 'cc0_inputs': sorted(inputs), 'cc0_sha256': inputs, 'changes': changes}

    prov = {
        'item': 'meadworks_panel_timber',
        'licence': 'CC BY-NC-SA 4.0 (MeadWorks asset licence); CC0 inputs are public domain',
        'method': 'Python (Pillow/numpy) merge and repaint of CC0 maps; no PackPanel pixels, no AI tools; build/build_panel_art.py',
        'assets': [
            entry('panel_background.png', [1254, 1254], used_bg,
                  'Two walnut colour maps reduced to luminance, stretched 1.6x/1.9x along the grain, flipped and offset so grains never align, '
                  'blended by a slow noise mask; two relief maps and the leather pore map added as low-gain detail; hand-set palette ramp '
                  '(101,71,52 mid), slow tonal drift, five soft knots, paint grain. Not a recolour of any single input.'),
            entry('panel_button.png', [1983, 793], used_btn,
                  'Chamfered-rectangle signed-distance frame lit from the top left; bronze made from a dark-steel map merged with an oxidised-steel '
                  'map, brushed streaks, edge wear and patina painted from noise; recessed inset from two black-leather maps with inner shadow; '
                  'four domed rivets and dark outline/groove drawn in code; soft drop shadow in the alpha margin.'),
        ]}
    json.dump(prov, open(os.path.join(PROV, 'meadworks_panel_timber.json'), 'w'), indent=2)
    print('ok')
