"""Revision 2 wood: periodic domain-warped figured grain with knots the grain flows around. CC0 inputs only (ambientCG Wood066/049/051).
The result is seamless (every field is periodic), so it also tiles."""
import numpy as np
from build_panel_art import load, resize, norm, ramp


def fft_noise(w, h, wavelength_x, wavelength_y, seed):
    """Periodic band-limited noise, unit std. Wavelengths in pixels."""
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((h, w)))
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    sx, sy = 1.0 / wavelength_x, 1.0 / wavelength_y
    f *= np.exp(-0.5 * ((fx / sx) ** 2 + (fy / sy) ** 2))
    n = np.fft.ifft2(f).real.astype(np.float32)
    return (n - n.mean()) / (n.std() + 1e-9)


def periodic_source(rel, W, H, stretch, roll_x=0, roll_y=0, flip=False):
    a = load(rel, True)
    a = resize(a, int(a.shape[1] * stretch * 1.5), int(a.shape[0] * 1.5))
    a = a[:H // 2 if H // 2 <= a.shape[0] else a.shape[0], :W // 2 if W // 2 <= a.shape[1] else a.shape[1]]
    if a.shape != (H // 2, W // 2):
        a = resize(a, W // 2, H // 2)
    if flip:
        a = a[::-1]
    top = np.concatenate([a, a[:, ::-1]], 1)
    full = np.concatenate([top, top[::-1]], 0)
    return np.roll(np.roll(full, roll_x, 1), roll_y, 0)


def sample(src, yy, xx):
    """Periodic bilinear sample."""
    H, W = src.shape
    x0 = np.floor(xx).astype(int)
    y0 = np.floor(yy).astype(int)
    fx = (xx - x0).astype(np.float32)
    fy = (yy - y0).astype(np.float32)
    x0 %= W
    y0 %= H
    x1 = (x0 + 1) % W
    y1 = (y0 + 1) % H
    return (src[y0, x0] * (1 - fx) * (1 - fy) + src[y0, x1] * fx * (1 - fy) + src[y1, x0] * (1 - fx) * fy + src[y1, x1] * fx * fy)


def tdist(a, size):
    d = np.abs(a)
    return np.minimum(d, size - d)


def background(W=1254, H=1254, seed=5, knots=6, contrast=1.0, palette=None):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # slow bends and a finer ripple: lines wander by tens of pixels
    dy = 15 * fft_noise(W, H, 420, 600, seed) + 3.5 * fft_noise(W, H, 150, 240, seed + 1)
    dx = 6 * fft_noise(W, H, 420, 300, seed + 2)
    rng = np.random.default_rng(seed + 9)
    kn = []
    for _ in range(knots):
        kn.append((rng.uniform(80, W - 80), rng.uniform(80, H - 80), rng.uniform(34, 56)))
    # keep knots apart
    ring = np.zeros((H, W), np.float32)
    core = np.zeros((H, W), np.float32)
    wy, wx = yy + dy, xx + dx
    for cx, cy, R in kn:
        ddx = ((xx - cx + W / 2) % W) - W / 2
        ddy = ((yy - cy + H / 2) % H) - H / 2
        g = np.exp(-(ddx ** 2 / (2 * (2.2 * R) ** 2) + ddy ** 2 / (2 * (1.15 * R) ** 2)))
        # lines open up around the knot: the sample row is pulled towards the knot's own row, so the grain bows round it
        wy = wy - ddy * 0.86 * g
        wx = wx - ddx * 0.18 * g
        e = np.sqrt((ddx / 1.7) ** 2 + ddy ** 2) / R
        rings = 0.5 + 0.5 * np.cos(2 * np.pi * e * 1.9)
        env = np.clip(1.3 - e, 0, 1)
        ring += (rings - 0.5) * env
        core += np.exp(-(e / 0.42) ** 2) * 1.0 + 0.45 * np.exp(-((e - 0.95) / 0.12) ** 2)
    a = periodic_source('ambientcg/Wood066/Wood066_1K-JPG_Color.jpg', W, H, 1.6)
    b = periodic_source('ambientcg/Wood049/Wood049_1K-JPG_Color.jpg', W, H, 1.9, W // 2 + 177, H // 2 + 91, True)
    r = periodic_source('ambientcg/Wood051/Wood051_1K-JPG_Displacement.jpg', W, H, 2.4, 211, 333)
    la = sample(a, wy, wx)
    lb = sample(b, wy, wx)
    lr = sample(r, wy * 1.0, wx * 1.0)
    mix = 0.5 + 0.28 * fft_noise(W, H, 260, 360, seed + 3)
    mix = np.clip(mix, 0, 1)
    t = 0.5 + contrast * (0.050 * norm(la) * (1 - mix) + 0.050 * norm(lb) * mix + 0.026 * norm(lr))
    t = t + contrast * 0.050 * ring - contrast * 0.095 * core          # growth rings and the dark heart of each knot
    t = t + 0.028 * fft_noise(W, H, 380, 380, seed + 4)               # slow colour/tone drift across the sheet
    t = t + 0.010 * fft_noise(W, H, 10, 2.2, seed + 5)                 # fine pores along the grain
    pal = palette or [(0.0, (56, 38, 27)), (0.5, (101, 71, 52)), (1.0, (148, 108, 78))]
    img = ramp(t, pal)
    img[..., 0] *= 1 + 0.010 * fft_noise(W, H, 300, 300, seed + 6)
    img[..., 2] *= 1 + 0.018 * fft_noise(W, H, 360, 280, seed + 7)
    return np.clip(img, 0, 255).astype(np.uint8), kn
