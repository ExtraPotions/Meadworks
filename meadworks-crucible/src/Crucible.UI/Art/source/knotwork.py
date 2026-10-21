"""Procedural Norse knotwork, drawn from scratch (no source image): over-under strand interlace stamped as shaded bronze tubes.

curves: torus_knot(2, n) closed knots (trefoil n=3, pentafoil n=5) and plait() two-strand braids. Crossings come from the polyline
itself; the over strand alternates along the curve, so the pattern is a true alternating interlace.
"""
import numpy as np

LIGHT = np.array([-0.5, -0.6, 0.62], np.float32)
LIGHT /= np.linalg.norm(LIGHT)


def bronze_color(t):
    t = np.clip(t, 0, 1)
    xs = [0, 0.32, 0.55, 0.8, 1.0]
    cols = [(34, 24, 16), (74, 52, 31), (112, 80, 47), (160, 118, 70), (206, 160, 104)]
    return np.stack([np.interp(t, xs, [c[i] for c in cols]) for i in range(3)], -1).astype(np.float32)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rgb = np.zeros((h, w, 3), np.float32)
        self.cov = np.zeros((h, w), np.float32)
        self.height = np.zeros((h, w), np.float32)

    def tube(self, pts, r, bias=0.0, halo=0.0, tone=0.0, color_fn=bronze_color):
        """Stamp a shaded round strand along pts (N,2). halo>0 first darkens, once, a gap of that extra width around the strand."""
        R = int(np.ceil(r + halo)) + 2
        oy, ox = np.mgrid[-R:R + 1, -R:R + 1].astype(np.float32)

        def patches():
            for px, py in pts:
                ix, iy = int(round(px)), int(round(py))
                x0, y0 = ix - R, iy - R
                cx0, cy0, cx1, cy1 = max(0, x0), max(0, y0), min(self.w, ix + R + 1), min(self.h, iy + R + 1)
                if cx0 >= cx1 or cy0 >= cy1:
                    continue
                sx0, sy0 = cx0 - x0, cy0 - y0
                sx1, sy1 = sx0 + (cx1 - cx0), sy0 + (cy1 - cy0)
                dx = ox[sy0:sy1, sx0:sx1] - (px - ix)
                dy = oy[sy0:sy1, sx0:sx1] - (py - iy)
                yield (cx0, cy0, cx1, cy1), dx, dy, np.hypot(dx, dy)

        if halo > 0:
            mask = np.zeros((self.h, self.w), bool)
            for (cx0, cy0, cx1, cy1), dx, dy, d in patches():
                mask[cy0:cy1, cx0:cx1] |= d < r + halo
            self.rgb[mask] *= 0.45
            self.cov[mask] = 1.0
        for (cx0, cy0, cx1, cy1), dx, dy, d in patches():
            inside = d <= r
            hgt = np.sqrt(np.clip(1 - (d / r) ** 2, 0, 1))
            lit = (dx / r) * LIGHT[0] + (dy / r) * LIGHT[1] + hgt * LIGHT[2]
            col = color_fn(0.30 + 0.55 * np.clip(lit, 0, 1) + tone)
            col = col * (0.55 + 0.45 * np.clip(hgt * 3, 0, 1))[..., None]
            top = inside & ((hgt + bias) >= self.height[cy0:cy1, cx0:cx1] - 1e-6)
            self.rgb[cy0:cy1, cx0:cx1][top] = col[top]
            self.height[cy0:cy1, cx0:cx1][top] = (hgt + bias)[top]
            self.cov[cy0:cy1, cx0:cx1][inside] = 1.0


def torus_knot(n, cx, cy, R, samples=1400, rot=0.0):
    """(2, n) torus knot projection: n crossings, alternating."""
    t = np.linspace(0, 4 * np.pi, samples, endpoint=False)   # odd n closes after two turns
    a = 2 + np.cos(n * t / 2)
    x = a * np.cos(t + rot)
    y = a * np.sin(t + rot)
    s = R / 3.0
    return np.stack([cx + x * s, cy + y * s], -1)


def crossings(pts):
    """Self-intersections of a closed polyline: list of (i, j) segment indices, i<j, and the crossing point."""
    n = len(pts)
    a = pts
    b = np.roll(pts, -1, 0)
    out = []
    d1 = b - a
    for i in range(n):
        p, r = a[i], d1[i]
        js = np.arange(i + 3, n if i > 0 else n - 1)
        if len(js) == 0:
            continue
        q, s = a[js], d1[js]
        den = r[0] * s[:, 1] - r[1] * s[:, 0]
        ok = np.abs(den) > 1e-9
        qp = q - p
        t = np.where(ok, (qp[:, 0] * s[:, 1] - qp[:, 1] * s[:, 0]) / np.where(ok, den, 1), -1)
        u = np.where(ok, (qp[:, 0] * r[1] - qp[:, 1] * r[0]) / np.where(ok, den, 1), -1)
        hit = ok & (t >= 0) & (t < 1) & (u >= 0) & (u < 1)
        for j, tt in zip(js[hit], t[hit]):
            out.append((i, int(j), p + r * tt))
    return out


def draw_knot(canvas, pts, r, halo=2.2, tone=0.0):
    """Interlace: whole strand first, then at every crossing the 'over' visit is re-stamped with a dark gap."""
    canvas.tube(np.vstack([pts, pts[:1]]), r, tone=tone)
    xs = crossings(pts)
    visits = sorted([(i, k) for k, (i, j, _) in enumerate(xs)] + [(j, k) for k, (i, j, _) in enumerate(xs)])
    over = {}
    for pos, (idx, k) in enumerate(visits):
        if pos % 2 == 0:
            over[k] = idx
    n = len(pts)
    win = int(max(6, 2.6 * r / max(1e-6, np.mean(np.linalg.norm(np.roll(pts, -1, 0) - pts, axis=1)))))
    for k, idx in over.items():
        seg = np.array([pts[(idx + o) % n] for o in range(-win, win + 1)])
        canvas.tube(seg, r, bias=5.0, halo=halo, tone=tone + 0.05)
    return len(xs)


def plait(canvas, x0, x1, cy, amp, period, r, step=0.5, halo=2.0, tone=0.0):
    """Two-strand braid along x with alternating over-under at each crossing."""
    xs = np.arange(x0, x1, step)
    ph = 2 * np.pi * (xs - x0) / period
    s1 = np.stack([xs, cy + amp * np.sin(ph)], -1)
    s2 = np.stack([xs, cy - amp * np.sin(ph)], -1)
    canvas.tube(s1, r, tone=tone)
    canvas.tube(s2, r, tone=tone)
    half = period / 2.0
    k = 0
    cx = x0 + half
    while cx < x1 - 1:
        sel = s1 if k % 2 == 0 else s2
        m = (xs > cx - r * 2.2) & (xs < cx + r * 2.2)
        canvas.tube(sel[m], r, bias=5.0, halo=halo, tone=tone + 0.05)
        cx += half
        k += 1
