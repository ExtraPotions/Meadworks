"""MeadWorks' own UI piece set, built from CC0 maps (second commit after panel_background / panel_button).

Pieces (textures/set/): panel_timber, panel_leather, panel_forged_iron, panel_parchment, slot_timber, slot_iron, slot_selected,
button_normal, button_hover, button_pressed, tab_active, divider_bronze  (layout sizes and 9-slice borders follow the uploaded
look reference's manifest as guidance only; no pixel of it is read), and the small images the Crucible.UI kit uses today:
kit/panel.png (128), kit/cell.png (128), kit/button.png, button_hover.png, button_pressed.png (64x32).
Hover and selected get a subtle warm edge, not a glow. Run: python build_panel_set.py
"""
import hashlib, json, os
import numpy as np
from PIL import Image
import build_panel_art as B
from build_panel_art import load, resize, mirror_fill, blur, noise, norm, ramp

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'textures', 'set')
KIT = os.path.join(HERE, '..', 'textures', 'kit')
os.makedirs(OUT, exist_ok=True)
os.makedirs(KIT, exist_ok=True)
SS = 2   # supersample for the layout pieces; the small kit images use 4


def cover(rel, w, h, gray=True, scale=1.0, flip=False, roll=0):
    a = load(rel, gray)
    if scale != 1.0:
        a = resize(a, int(a.shape[1] * scale), int(a.shape[0] * scale))
    if flip:
        a = a[::-1]
    if roll:
        a = np.roll(a, roll, 1)
    return mirror_fill(a, w, h)


def lum(c):
    return c @ np.array([0.3, 0.59, 0.11], np.float32)


# ------------------------------------------------------------------ materials (all return float32 RGB 0..255)
def wood(w, h, vertical=True, warm=1.0, seed=1):
    cw, ch = (h, w) if vertical else (w, h)
    c1 = lum(cover('ambientcg/Wood066/Wood066_1K-JPG_Color.jpg', cw, ch, False, 1.0))
    c2 = lum(cover('ambientcg/Wood051/Wood051_1K-JPG_Color.jpg', cw, ch, False, 1.0, True, 300))
    rl = cover('ambientcg/Wood049/Wood049_1K-JPG_Displacement.jpg', cw, ch, True, 1.6)
    t = 0.5 + 0.07 * norm(c1) + 0.05 * norm(c2) + 0.04 * norm(rl)
    if vertical:
        t = t.T
    # planks: seams every ~w/6 px with their own tone offset
    pw = max(16, (w if vertical else h) // 6)
    rng = np.random.default_rng(seed)
    axis = np.arange(w if vertical else h)
    idx = axis // pw
    off = rng.normal(0, 0.05, idx.max() + 2)[idx]
    seam = np.clip(1 - np.abs((axis % pw) - 0) / 1.8, 0, 1) * 0.5
    if vertical:
        t = t + off[None, :] - seam[None, :]
    else:
        t = t + off[:, None] - seam[:, None]
    return ramp(t, [(0, (38, 26, 18)), (0.5, (96 * warm, 66, 44)), (1, (150 * warm, 108, 74))])


def leather(w, h):
    a = lum(cover('ambientcg/Leather033C/Leather033C_1K-JPG_Color.jpg', w, h, False))
    d = cover('ambientcg/Leather033C/Leather033C_1K-JPG_Displacement.jpg', w, h)
    t = 0.5 + 0.12 * norm(a) + 0.06 * norm(d) + 0.05 * norm(blur(noise(w, h, 90, 5), 18))
    return ramp(t, [(0, (52, 32, 22)), (0.5, (96, 60, 40)), (1, (146, 100, 68))])


def steel(w, h):
    a = cover('ambientcg/Metal046A/Metal046A_1K-JPG_Color.jpg', w, h)
    r = cover('ambientcg/Metal046A/Metal046A_1K-JPG_Roughness.jpg', w, h)
    t = 0.45 + 0.10 * norm(a) + 0.05 * norm(r) + 0.03 * norm(blur(noise(w, h, 30, 9), 4))
    return ramp(t, [(0, (22, 23, 24)), (0.5, (44, 46, 48)), (1, (82, 84, 84))])


def paper(w, h):
    a = lum(cover('ambientcg/Paper001/Paper001_1K-JPG_Color.jpg', w, h, False))
    t = 0.5 + 0.08 * norm(a) + 0.07 * norm(blur(noise(w, h, 70, 13), 14))
    return ramp(t, [(0, (150, 122, 84)), (0.5, (196, 168, 120)), (1, (226, 204, 156))])


def bronze_tex(w, h):
    m = cover('ambientcg/Metal046A/Metal046A_1K-JPG_Color.jpg', w, h)
    s = blur(noise(w, h, 4, 31, 6.0), 0.7)
    return 0.40 + 0.05 * norm(m) + 0.03 * norm(s)


def bronze(t):
    return ramp(t, [(0, (34, 24, 16)), (0.32, (74, 52, 31)), (0.55, (112, 80, 47)), (0.80, (160, 118, 70)), (1, (206, 160, 104))])


def over(base, color, a):
    a = a[..., None] if a.ndim == 2 else a
    return base * (1 - a) + np.asarray(color, np.float32) * a


# ------------------------------------------------------------------ geometry
def grid(w, h):
    return np.mgrid[0:h, 0:w].astype(np.float32)


def box_sdf(yy, xx, x0, y0, x1, y1, r=0.0, chamfer=0.0):
    """Inside distance (positive inside) of a rect with rounded or chamfered corners."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2, (y1 - y0) / 2
    if chamfer > 0:
        qx, qy = np.abs(xx - cx) - hx, np.abs(yy - cy) - hy
        d = np.maximum(qx, qy)
        d = np.maximum(d, (qx + qy + chamfer) / 1.4142)
        return -d
    qx, qy = np.abs(xx - cx) - (hx - r), np.abs(yy - cy) - (hy - r)
    out = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
    return -out


def bevel(di, lo, hi, light_dir=(-0.62, -0.78)):
    """Directional bevel shading term from an inside-distance field between lo..hi."""
    gy, gx = np.gradient(blur(np.clip(di, -20, 120) / 140 + 0.15, 1.0) * 140)
    gl = np.hypot(gx, gy) + 1e-6
    face = (gx / gl) * light_dir[0] + (gy / gl) * light_dir[1]     # inward gradient dotted with light
    band = np.clip(1 - np.abs(di - (lo + hi) / 2) / ((hi - lo) / 2 + 1e-6), 0, 1)
    return face * band


def sphere(yy, xx, cx, cy, r, base=(160, 118, 70)):
    d = np.hypot(xx - cx, yy - cy)
    k = np.sqrt(np.clip(1 - (d / r) ** 2, 0, 1))
    hx, hy = (xx - cx) / r, (yy - cy) / r
    sh = np.clip(0.55 + 0.55 * (-hx * 0.5 - hy * 0.65 + k * 0.6), 0, 1.3)
    col = np.asarray(base, np.float32)[None, None, :] * sh[..., None] * 0.9
    col += (np.exp(-(((hx + 0.35) ** 2 + (hy + 0.4) ** 2) / 0.03)) * 70)[..., None]
    a = np.clip(r + 0.5 - d, 0, 1)
    seat = np.clip(1 - np.abs(d - (r + 1.5)) / 1.6, 0, 1) * (d > r - 0.5)
    return col, a, seat


def diamond(yy, xx, cx, cy, r):
    d = (np.abs(xx - cx) + np.abs(yy - cy))
    k = np.clip(1 - d / r, 0, 1)
    col = bronze(0.35 + 0.55 * k + 0.12 * np.clip((-(xx - cx) - (yy - cy)) / r, -1, 1))
    return col, np.clip(r + 0.5 - d, 0, 1)


# ------------------------------------------------------------------ framed pieces
def framed(w, h, fill, trim, fw, outer=3, corner=None, mid=False, stitch=False, rivets=False, warm=0.0,
           chamfer=0.0, inner_shadow=0.5, seed=0):
    """A framed plate. fill/trim are float RGB maps (h,w,3). fw = frame band width. Returns RGBA uint8."""
    yy, xx = grid(w, h)
    di = box_sdf(yy, xx, 0, 0, w, h, r=outer * 2.2 if chamfer == 0 else 0, chamfer=chamfer)
    body = np.clip(di + 0.5, 0, 1)
    light = bevel(di, 0, fw * 0.22) * 0.55 - bevel(di, fw * 0.78, fw) * 0.55
    band = np.clip((fw - di) / 1.5, 0, 1)
    rgb = fill.copy()
    inner = np.clip((di - fw) / 28, 0, 1)
    rgb = rgb * (1 - inner_shadow * (1 - inner))[..., None]
    t = trim * (1 + light)[..., None]
    rgb = rgb * (1 - band[..., None]) + t * band[..., None]
    dark = np.clip(1 - np.abs(di - 1.5) / 2.2, 0, 1) * 0.8
    rgb = over(rgb, (14, 10, 7), dark)
    groove = np.clip(1 - np.abs(di - fw) / 1.8, 0, 1) * 0.7
    rgb = over(rgb, (10, 7, 5), groove)
    if warm > 0:                                       # subtle warm edge on the inside of the frame (hover/selected)
        edge = np.clip(1 - np.abs(di - (fw + 3)) / 5, 0, 1) * warm
        rgb = over(rgb, (214, 158, 88), edge * 0.55)
        rgb = rgb + (np.clip(1 - np.abs(di - (fw * 0.5)) / (fw * 0.5), 0, 1) * warm * 14)[..., None]
    if stitch:
        ring = np.abs(di - (fw * 0.62))
        dash = ((xx + yy) % 9) < 5
        s = np.clip(1 - ring / 1.0, 0, 1) * dash
        rgb = over(rgb, (214, 176, 120), s * 0.55)
    S = SS
    studs = []
    if corner in ('cap', 'stud', 'bracket'):
        studs += [(fw * 0.55, fw * 0.55), (w - fw * 0.55, fw * 0.55), (fw * 0.55, h - fw * 0.55), (w - fw * 0.55, h - fw * 0.55)]
    for cx, cy in studs:
        if corner == 'cap':                            # leather triangle cap tucked into the corner
            sx, sy = (1 if cx < w / 2 else -1), (1 if cy < h / 2 else -1)
            ex, ey = (0 if sx > 0 else w), (0 if sy > 0 else h)
            tri = ((np.abs(xx - ex) + np.abs(yy - ey)) < fw * 1.35)
            cap = leather(w, h)
            rgb = over(rgb, cap * 0.85, tri * body * 0.95)
            rgb = over(rgb, (20, 12, 8), np.clip(1 - np.abs((np.abs(xx - ex) + np.abs(yy - ey)) - fw * 1.35) / 1.6, 0, 1) * 0.6 * body)
        if corner == 'bracket':
            sx, sy = (1 if cx < w / 2 else -1), (1 if cy < h / 2 else -1)
            ex, ey = (0 if sx > 0 else w), (0 if sy > 0 else h)
            arm = (np.abs(xx - ex) < fw * 1.9) & (np.abs(yy - ey) < fw * 0.9) | (np.abs(yy - ey) < fw * 1.9) & (np.abs(xx - ex) < fw * 0.9)
            rgb = over(rgb, steel(w, h) * 1.25, arm * body * 0.95)
        col, a, seat = sphere(yy, xx, cx, cy, max(4.0, fw * 0.22), (170, 128, 78) if corner != 'bracket' else (120, 124, 126))
        rgb = over(rgb, (12, 9, 6), seat * 0.7)
        rgb = rgb * (1 - a[..., None]) + col * a[..., None]
    if mid:                                            # diamond studs at the edge midpoints
        for cx, cy in [(w / 2, fw * 0.5), (w / 2, h - fw * 0.5), (fw * 0.5, h / 2), (w - fw * 0.5, h / 2)]:
            col, a = diamond(yy, xx, cx, cy, fw * 0.62)
            rgb = rgb * (1 - a[..., None]) + col * a[..., None]
    if rivets:
        for cx, cy in [(fw * 0.5, fw * 0.5), (w - fw * 0.5, fw * 0.5), (fw * 0.5, h - fw * 0.5), (w - fw * 0.5, h - fw * 0.5)]:
            col, a, seat = sphere(yy, xx, cx, cy, max(4.0, fw * 0.24), (118, 122, 124))
            rgb = over(rgb, (10, 10, 10), seat * 0.7)
            rgb = rgb * (1 - a[..., None]) + col * a[..., None]
    # tiny paint grain so it reads painted, not rendered
    rgb = rgb * (1 + 0.012 * norm(noise(w, h, 2, 77 + seed)))[..., None]
    return np.concatenate([np.clip(rgb, 0, 255), body[..., None] * 255], -1).astype(np.uint8)


def finish(arr, name, folder=OUT):
    Image.fromarray(arr, 'RGBA').save(os.path.join(folder, name + '.png'), optimize=True)


def build_layout():
    P = {}
    # --- panels (sizes after the reference manifest, borders noted in the table written to provenance)
    w, h, fw = 380, 356, 46
    P['panel_timber'] = framed(w, h, wood(w, h, True, 1.0, 3), bronze(bronze_tex(w, h)), fw, corner='cap', mid=True, seed=1)
    w, h, fw = 384, 286, 40
    P['panel_leather'] = framed(w, h, leather(w, h) * 0.95, leather(w, h) * 1.05, fw, corner='stud', stitch=True, inner_shadow=0.25, seed=2)
    w, h, fw = 376, 280, 42
    P['panel_forged_iron'] = framed(w, h, steel(w, h) * 0.9, steel(w, h) * 1.2, fw, corner='bracket', inner_shadow=0.45, seed=3)
    w, h, fw = 368, 298, 38
    P['panel_parchment'] = framed(w, h, paper(w, h), wood(w, h, False, 0.8, 4) * 0.8, fw, corner='stud', inner_shadow=0.30, seed=4)
    # --- slots
    w, h, fw = 260, 252, 38
    P['slot_timber'] = framed(w, h, wood(w, h, True, 0.85, 5) * 0.8, wood(w, h, False, 0.9, 6) * 0.7, fw, inner_shadow=0.6, seed=5)
    P['slot_iron'] = framed(w, h, wood(w, h, True, 0.8, 7) * 0.75, steel(w, h) * 1.15, fw, rivets=True, inner_shadow=0.6, seed=6)
    P['slot_selected'] = framed(w, h, wood(w, h, True, 0.85, 5) * 0.8, wood(w, h, False, 0.9, 6) * 0.75, fw, inner_shadow=0.5, warm=1.0, seed=7)
    # --- buttons (chamfered wood plate in a bronze frame); hover = warm edge, pressed = darker and inset
    w, h, fw = 468, 156, 28
    base = wood(w, h, False, 0.9, 8)
    P['button_normal'] = framed(w, h, base * 0.85, bronze(bronze_tex(w, h)), fw, chamfer=34, inner_shadow=0.3, seed=8)
    P['button_hover'] = framed(w, h, base * 1.0, bronze(bronze_tex(w, h) + 0.04), fw, chamfer=34, inner_shadow=0.25, warm=0.8, seed=9)
    pr = framed(w, h, base * 0.65, bronze(bronze_tex(w, h) - 0.05), fw, chamfer=34, inner_shadow=0.7, seed=10)
    pr[..., :3] = (pr[..., :3] * 0.86).astype(np.uint8)
    P['button_pressed'] = pr
    # --- active tab
    w, h, fw = 316, 96, 20
    P['tab_active'] = framed(w, h, base[:h, :w] * 0.95, bronze(bronze_tex(w, h)), fw, chamfer=22, inner_shadow=0.25, warm=0.6, seed=11)
    # --- divider: bronze rope with a diamond knot in the middle and a stud at each end
    w, h = 424, 70
    yy, xx = grid(w, h)
    cy = h / 2
    rope = np.sin((xx / 7.0) + 0 * yy) * 3.0
    d = np.abs(yy - cy - rope * 0.5)
    t1 = np.clip(1 - d / 5, 0, 1)
    d2 = np.abs(yy - cy + rope * 0.5)
    t2 = np.clip(1 - d2 / 5, 0, 1)
    tex = bronze_tex(w, h)
    rgb = np.zeros((h, w, 3), np.float32)
    a = np.zeros((h, w), np.float32)
    for tt, shift in ((t1, 0.0), (t2, 0.12)):
        c = bronze(tex + 0.18 * tt + shift)
        rgb = rgb * (1 - tt[..., None]) + c * tt[..., None]
        a = np.maximum(a, tt)
    for cx, r in [(w / 2, 24), (34, 14), (w - 34, 14)]:
        col, aa = diamond(yy, xx, cx, cy, r)
        rgb = rgb * (1 - aa[..., None]) + col * aa[..., None]
        a = np.maximum(a, aa)
        col2, aa2 = diamond(yy, xx, cx, cy, r * 0.45)
        rgb = rgb * (1 - aa2[..., None] * 0.7) + (col2 * 0.55) * aa2[..., None] * 0.7
    rgb = over(rgb, (12, 8, 5), np.clip(1 - np.abs(np.hypot(0, 0) + 0) , 0, 0))
    P['divider_bronze'] = np.concatenate([np.clip(rgb, 0, 255), a[..., None] * 255], -1).astype(np.uint8)
    return P


# ------------------------------------------------------------------ the small images the kit uses today
def small(w, h, border, kind, state=''):
    S4 = 4
    W, H = w * S4, h * S4
    fw = border * S4
    wd = wood(W, H, kind == 'panel', 0.9, 21 + len(state))
    tr = bronze(bronze_tex(W, H))
    if kind == 'panel':
        arr = framed(W, H, wd * 0.5, tr, S4 * 4, outer=8, inner_shadow=0.15, seed=31)
    elif kind == 'cell':
        arr = framed(W, H, wd * 0.34, wd * 0.5, S4 * 3, outer=6, inner_shadow=0.5, seed=32)
    else:
        mult = {'': 0.46, 'hover': 0.62, 'pressed': 0.30}[state]
        arr = framed(W, H, wd * mult, tr * (1.0 if state != 'pressed' else 0.8), S4 * 3, outer=3, inner_shadow=0.25 if state != 'pressed' else 0.6,
                     warm=0.8 if state == 'hover' else 0, seed=33)
    return np.asarray(Image.fromarray(arr, 'RGBA').resize((w, h), Image.LANCZOS))


if __name__ == '__main__':
    P = build_layout()
    for name, arr in P.items():
        finish(arr, name)
    finish(small(128, 128, 32, 'panel'), 'panel', KIT)
    finish(small(128, 128, 32, 'cell'), 'cell', KIT)
    finish(small(64, 32, 12, 'button'), 'button', KIT)
    finish(small(64, 32, 12, 'button', 'hover'), 'button_hover', KIT)
    finish(small(64, 32, 12, 'button', 'pressed'), 'button_pressed', KIT)
    borders = {'panel_timber': [45, 45, 53, 53], 'panel_leather': [46, 46, 43, 43], 'panel_forged_iron': [45, 45, 42, 42],
               'panel_parchment': [44, 44, 45, 45], 'slot_timber': [49, 49, 48, 48], 'slot_iron': [49, 49, 48, 48],
               'slot_selected': [50, 50, 48, 48], 'button_normal': [66, 66, 44, 44], 'button_hover': [66, 66, 44, 44],
               'button_pressed': [66, 66, 44, 44], 'tab_active': [44, 44, 27, 27]}
    used = dict(B.used)
    info = {'item': 'meadworks_ui_set', 'licence': 'CC BY-NC-SA 4.0 (MeadWorks asset licence); CC0 inputs are public domain',
            'method': 'Python (Pillow/numpy) merge and repaint of CC0 maps; no pixels of PackPanel or of the uploaded look reference, no AI tools; build/build_panel_set.py',
            'cc0_inputs': sorted(used), 'cc0_sha256': used,
            'changes': 'Plank, leather, steel, paper and bronze materials are each merged from two or three CC0 maps (regraded to hand-set palettes, planks and seams added in code); frames, bevels, studs, '
                       'diamonds, stitching, brackets and the rope divider are drawn from signed-distance fields; hover and selected add a subtle warm inner edge, no glow.',
            'files': {}}
    for folder, label in ((OUT, 'set'), (KIT, 'kit')):
        for f in sorted(os.listdir(folder)):
            p = os.path.join(folder, f)
            im = Image.open(p)
            n = f[:-4]
            info['files'][label + '/' + f] = {'size': list(im.size), 'sha256': hashlib.sha256(open(p, 'rb').read()).hexdigest(),
                                               'nine_slice_border_px_lrtb': borders.get(n)}
    json.dump(info, open(os.path.join(HERE, '..', 'provenance', 'meadworks_ui_set.json'), 'w'), indent=2)
    print('ok', len(info['files']))
