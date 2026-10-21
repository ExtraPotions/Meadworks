"""The kraken's flat textures, painted with numpy (plain Python 3, numpy and Pillow; no Blender):

    ecp_kraken_eye_albedo.png   256 px: pale amber iris, black horizontal slit pupil, dark rim, a catch-light;
                                the eyeball's UVs look straight down its gaze, so the pupil is the texture's middle
    ecp_kraken_ink_0..3.png     512 px RGBA, straight alpha: screen ink splats for the HUD overlay - a big blob,
                                splash arms, droplets, drips running down, a glossy highlight or two

    python assets/ecp_kraken/textures.py [out folder]
"""
import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
INK_SIZE, SCALE = 512, 2                    # splats are drawn at twice the size, then filtered down: soft edges
EDGE_INK, DEEP_INK = np.array([46, 20, 64]), np.array([12, 7, 20])
GLOSS = np.array([176, 164, 214])


def noise(rng, size, cells):
    """Smooth value noise in [0, 1]: a random grid of `cells` squared, upsampled bicubically."""
    grid = (rng.random((cells, cells)) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(grid).resize((size, size), Image.BICUBIC), np.float32) / 255.0


def soft(distance, width=1.5):
    """Coverage from a signed distance (positive inside), anti-aliased over `width` pixels."""
    return np.clip(distance / width + 0.5, 0.0, 1.0)


# ---------------------------------------------------------------- eye

def eye(path, size=256):
    rng = np.random.default_rng(3)
    v, u = np.mgrid[0:size, 0:size].astype(np.float32)
    x, y = (u + 0.5) / size * 2 - 1, 1 - (v + 0.5) / size * 2      # y up, like the UVs
    d, angle = np.hypot(x, y), np.arctan2(y, x)
    grain = noise(rng, size, 24)
    iris = np.stack([225 - 45 * d, 188 - 60 * d, 92 - 40 * d], -1)
    fibres = 0.9 + 0.12 * np.sin(angle * 57 + grain * 6) + 0.06 * (grain - 0.5)
    colour = iris * fibres[..., None]
    halo = np.exp(-((np.hypot(x / 0.5, y / 0.14) - 1.0) / 0.25) ** 2)
    colour = colour * (1 - 0.35 * halo[..., None])
    rim = np.clip((d - 0.58) / 0.08, 0, 1)[..., None]
    colour = colour * (1 - rim) + np.array([74, 46, 22]) * rim
    flesh = np.clip((d - 0.7) / 0.04, 0, 1)[..., None]
    colour = colour * (1 - flesh) + (np.array([44, 19, 17]) * (0.85 + 0.3 * grain[..., None])) * flesh
    pupil = soft((1 - ((np.abs(x) / 0.46) ** 2.3 + (np.abs(y) / 0.105) ** 2)) * 40, 1.5)[..., None]
    colour = colour * (1 - pupil) + np.array([7, 4, 6]) * pupil
    for cx, cy, r, strength in ((-0.2, 0.27, 0.075, 0.9), (0.2, -0.2, 0.035, 0.5)):
        spot = soft((r - np.hypot(x - cx, (y - cy) * 1.2)) * size / 2, 1.5)[..., None] * strength
        colour = colour * (1 - spot) + np.array([250, 244, 226]) * spot
    Image.fromarray(np.clip(colour, 0, 255).astype(np.uint8), 'RGB').save(path)


# ---------------------------------------------------------------- ink

class Splat:
    """One splat's coverage and thickness, built up from a blob, droplets and drips (pixel units at SCALE)."""

    def __init__(self, seed):
        self.rng = np.random.default_rng(seed)
        self.size = INK_SIZE * SCALE
        v, u = np.mgrid[0:self.size, 0:self.size].astype(np.float32)
        self.u, self.v = u + 0.5, v + 0.5
        self.cover = np.zeros((self.size, self.size), np.float32)
        self.gloss = np.zeros_like(self.cover)

    def px(self, value):
        return value * SCALE

    def blob(self, cx, cy, radius, arms, arm_length, wobble=0.12):
        """A lumpy disc with splash arms (in the directions given, or random)."""
        cx, cy, radius = self.px(cx), self.px(cy), self.px(radius)
        du, dv = self.u - cx, self.v - cy
        theta, dist = np.arctan2(dv, du), np.hypot(du, dv)
        r = np.ones_like(theta)
        for k in range(2, 9):
            r += self.rng.uniform(-wobble, wobble) / k ** 0.6 * np.cos(k * theta + self.rng.uniform(0, 2 * math.pi))
        tips = []
        for direction in arms:
            width, length = self.rng.uniform(0.05, 0.13), self.rng.uniform(0.5, 1.0) * arm_length
            gap = np.angle(np.exp(1j * (theta - direction)))
            r += length * np.exp(-(gap / width) ** 2)
            tips.append((direction, 1 + length))
        self.cover = np.maximum(self.cover, soft(radius * r - dist, 1.5 * SCALE))
        self._highlight(cx - radius * 0.38, cy - radius * 0.42, radius * 0.34, radius * 0.16, -0.6, 0.75)
        self._highlight(cx + radius * 0.25, cy + radius * 0.3, radius * 0.12, radius * 0.06, -0.6, 0.35)
        return [(cx + math.cos(a) * radius * reach, cy + math.sin(a) * radius * reach, a) for a, reach in tips]

    def drop(self, cx, cy, radius, stretch=1.0, angle=0.0, shine=True):
        """A droplet (pixel units at SCALE), stretched along `angle`."""
        reach, margin = radius * stretch, self.px(10)
        if min(cx, cy) - reach < margin or max(cx, cy) + reach > self.size - margin:
            return
        du, dv = self.u - cx, self.v - cy
        along = du * math.cos(angle) + dv * math.sin(angle)
        across = -du * math.sin(angle) + dv * math.cos(angle)
        self.cover = np.maximum(self.cover, soft(radius - np.hypot(along / stretch, across), 1.4 * SCALE))
        if shine and radius > 5 * SCALE:
            self._highlight(cx - radius * 0.35, cy - radius * 0.35, radius * 0.3, radius * 0.2, -0.7, 0.6)

    def spray(self, tips, count, reach):
        """Droplets flung past the arms' tips and scattered round the blob."""
        for k in range(count):
            x, y, a = tips[k % len(tips)] if tips and k % 3 else (self.cx, self.cy, self.rng.uniform(0, 2 * math.pi))
            a += self.rng.normal(0, 0.12)
            out = self.px(self.rng.uniform(0.2, 1.0) * reach) * (0.5 if tips and k % 3 else 1.6)
            r = self.px(self.rng.uniform(2.5, 10.0)) * (1.25 - out / self.px(reach * 1.8))
            self.drop(x + math.cos(a) * out, y + math.sin(a) * out, max(r, self.px(1.6)), self.rng.uniform(1.0, 2.2), a)

    def drip(self, x, top, length, width):
        """A run of ink straight down from the blob, thinning, ending in a bead."""
        x, top, length, width = self.px(x), self.px(top), self.px(length), self.px(width)
        phase = self.rng.uniform(0, 6)
        t = np.clip((self.v - top) / length, 0, 1)
        centre = x + np.sin(t * 5 + phase) * width * 0.25
        half = width * (1 - 0.45 * t)
        inside = np.where((self.v >= top) & (self.v <= top + length), half - np.abs(self.u - centre), -1e3)
        self.cover = np.maximum(self.cover, soft(inside, 1.4 * SCALE))
        self.drop(x + math.sin(5 + phase) * width * 0.25, top + length, width * 0.95, 1.2, math.pi / 2)

    def _highlight(self, cx, cy, rx, ry, angle, strength):
        du, dv = self.u - cx, self.v - cy
        a = du * math.cos(angle) + dv * math.sin(angle)
        b = -du * math.sin(angle) + dv * math.cos(angle)
        self.gloss = np.maximum(self.gloss, strength * np.exp(-((a / rx) ** 2 + (b / ry) ** 2) * 2.2))

    def image(self):
        edge = np.minimum.reduce([self.u, self.v, self.size - self.u, self.size - self.v]) / self.px(8)
        cover = self._down(self.cover * np.clip(edge, 0, 1))
        gloss = self._down(self.gloss) * np.clip((cover - 0.6) / 0.4, 0, 1)
        thick = np.asarray(Image.fromarray((cover * 255).astype(np.uint8)).filter(_blur(9)), np.float32) / 255.0
        grain = noise(self.rng, INK_SIZE, 18)
        depth = np.clip(thick ** 2 * (0.8 + 0.4 * grain), 0, 1)[..., None]
        colour = EDGE_INK * (1 - depth) + DEEP_INK * depth
        ring = np.clip(1 - np.abs(thick - 0.45) / 0.12, 0, 1)[..., None] * 0.35
        colour = colour * (1 - ring) + DEEP_INK * ring
        colour = colour * (1 - gloss[..., None]) + GLOSS * gloss[..., None]
        alpha = cover * (0.93 + 0.05 * thick)
        rgba = np.dstack([np.clip(colour, 0, 255), alpha * 255]).astype(np.uint8)
        return Image.fromarray(rgba, 'RGBA')

    def _down(self, field):
        return np.asarray(Image.fromarray((field * 255).astype(np.uint8)).resize((INK_SIZE, INK_SIZE), Image.LANCZOS),
                          np.float32) / 255.0


def _blur(radius):
    from PIL import ImageFilter
    return ImageFilter.GaussianBlur(radius)


def splat(seed, blobs, arm_count, arm_length, droplets, drips, bias=None):
    """blobs: (x, y, radius) in 512-px units; bias: a direction the splash was thrown towards (radians), or None."""
    s = Splat(seed)
    tips = []
    for index, (x, y, radius) in enumerate(blobs):
        count = arm_count if index == 0 else max(arm_count // 3, 2)
        arms = [s.rng.normal(bias, 0.7) if bias is not None and k % 3 else s.rng.uniform(0, 2 * math.pi)
                for k in range(count)]
        tips += s.blob(x, y, radius, arms, arm_length)
    s.cx, s.cy = s.px(blobs[0][0]), s.px(blobs[0][1])
    s.spray(tips, droplets, blobs[0][2] * 1.5)
    for k in range(drips):
        x, y, radius = blobs[k % len(blobs)]
        offset = s.rng.uniform(-0.75, 0.75) * radius
        s.drip(x + offset, y + radius * 0.5, s.rng.uniform(0.6, 1.5) * radius, s.rng.uniform(4.0, 8.5))
    return s.image()


SPLATS = [
    dict(seed=11, blobs=[(256, 222, 118)], arm_count=8, arm_length=0.55, droplets=26, drips=4),
    dict(seed=23, blobs=[(214, 246, 100)], arm_count=9, arm_length=0.8, droplets=30, drips=3, bias=-0.6),
    dict(seed=37, blobs=[(284, 208, 92), (190, 292, 64)], arm_count=7, arm_length=0.5, droplets=22, drips=5),
    dict(seed=53, blobs=[(250, 236, 84)], arm_count=14, arm_length=1.1, droplets=34, drips=5),
]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "out", "textures")
    os.makedirs(out, exist_ok=True)
    eye(os.path.join(out, "ecp_kraken_eye_albedo.png"))
    for index, recipe in enumerate(SPLATS):
        splat(**recipe).save(os.path.join(out, f"ecp_kraken_ink_{index}.png"))
    print("WORKSHOP textures:", out)


if __name__ == "__main__":
    main()
