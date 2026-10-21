"""Revision 2: wavy knotted grain, built-looking timber pieces (plank seams, carved bevels, end-grain corner blocks, pegs, nails, edge wear)
and procedural Norse knotwork embossed in bronze (corner caps, plait trim, divider, tab and button ends).
CC0 inputs only; every shape is drawn here. Run: python build_panel_r2.py   (writes ../textures/r2, ../textures/set, ../textures/kit untouched)
"""
import hashlib, json, os
import numpy as np
from PIL import Image
import build_panel_art as B
import build_panel_set as S
import knotwork as K
import wood_r2 as W
from build_panel_art import blur, noise, norm, ramp

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, '..', 'textures')
R2 = os.path.join(TEX, 'r2')
os.makedirs(R2, exist_ok=True)


# ----------------------------------------------------------------------------------------------- helpers
def paste_canvas(rgb, cv, x, y, shadow=0.45, soft=2.0):
    """Composite a knotwork Canvas onto rgb at (x, y), with a soft drop shadow."""
    h, w = cv.cov.shape
    H, Wd = rgb.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(Wd, x + w), min(H, y + h)
    if x0 >= x1 or y0 >= y1:
        return
    c = cv.cov[y0 - y:y1 - y, x0 - x:x1 - x]
    col = cv.rgb[y0 - y:y1 - y, x0 - x:x1 - x]
    sh = np.zeros_like(rgb[..., 0])
    sh[y0:y1, x0:x1] = c
    sh = np.roll(np.roll(sh, 1, 0), 1, 1)
    sh = blur(sh, soft) * shadow
    rgb *= (1 - sh)[..., None]
    reg = rgb[y0:y1, x0:x1]
    rgb[y0:y1, x0:x1] = reg * (1 - c[..., None]) + col * c[..., None]


def plate_disc(rgb, cx, cy, r, tex_seed=3):
    """A bronze round plate with a bevelled rim (cap for a knot), drawn into rgb in place."""
    H, Wd = rgb.shape[:2]
    yy, xx = np.mgrid[0:H, 0:Wd].astype(np.float32)
    d = np.hypot(xx - cx, yy - cy)
    a = np.clip(r + 0.5 - d, 0, 1)
    if a.max() <= 0:
        return
    t = 0.40 + 0.04 * norm(B.noise(Wd, H, 3, tex_seed))
    rim = np.clip(1 - np.abs(d - (r - 2.5)) / 2.2, 0, 1)
    lit = ((-(xx - cx) * 0.62 - (yy - cy) * 0.78) / max(r, 1)) * rim
    col = S.bronze(t + 0.22 * lit + 0.05 * (d < r - 5))
    seat = np.clip(1 - np.abs(d - (r + 1.6)) / 1.8, 0, 1) * 0.7
    rgb[:] = rgb * (1 - seat[..., None]) + np.array([10, 7, 5], np.float32) * seat[..., None]
    rgb[:] = rgb * (1 - a[..., None]) + col * a[..., None]


def knot_at(rgb, cx, cy, R, strand=None, n=3, rot=0.0):
    r = strand or max(1.5, R * 0.17)
    size = int(R * 2 + 12)
    cv = K.Canvas(size, size)
    K.draw_knot(cv, K.torus_knot(n, size / 2, size / 2, R, rot=rot), r, halo=max(1.2, r * 0.45))
    paste_canvas(rgb, cv, int(cx - size / 2), int(cy - size / 2), shadow=0.5, soft=max(1.0, r * 0.6))


def plait_strip(rgb, x0, y0, length, band, vertical=False, period=None):
    """Bronze liner with a two-strand plait embossed along it."""
    period = period or band * 2.6
    r = max(1.4, band * 0.19)
    amp = band * 0.5 - r - 1.0
    if vertical:
        cv = K.Canvas(int(band), int(length))
        tmp = K.Canvas(int(length), int(band))
        K.plait(tmp, 0, length, band / 2, amp, period, r, step=0.5, halo=max(1.0, r * 0.5))
        cv.rgb, cv.cov = np.transpose(tmp.rgb, (1, 0, 2)).copy(), tmp.cov.T.copy()
    else:
        cv = K.Canvas(int(length), int(band))
        K.plait(cv, 0, length, band / 2, amp, period, r, step=0.5, halo=max(1.0, r * 0.5))
    base = S.bronze(0.34 + 0.04 * norm(noise(int(length) if not vertical else int(band), int(band) if not vertical else int(length), 4, 17)))
    h, w = cv.cov.shape
    region = rgb[y0:y0 + h, x0:x0 + w]
    liner = base * 0.9
    edge = np.zeros((h, w), np.float32)
    edge[:2] = 0.5
    edge[-2:] = 0.35
    edge[:, :2] = 0.5
    edge[:, -2:] = 0.35
    liner = liner * (1 + 0.25 * (edge - 0.3))[..., None] if False else liner
    rgb[y0:y0 + h, x0:x0 + w] = liner
    paste_canvas(rgb, cv, x0, y0, shadow=0.5, soft=1.2)
    rgb[y0:y0 + h, x0:x0 + w] *= 1.0


def end_grain(rgb, x0, y0, w, h, seed=1):
    """A square cut face with off-centre growth rings (the grain end of a corner block)."""
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cx, cy = w * rng.uniform(0.3, 0.7), h * rng.uniform(0.3, 0.7)
    d = np.hypot((xx - cx) * 1.0, (yy - cy) * 1.15)
    d += 1.6 * norm(blur(noise(w, h, 6, seed + 40), 2))
    ring = 0.5 + 0.5 * np.cos(d / 2.6)
    t = 0.46 + 0.07 * ring + 0.025 * norm(noise(w, h, 2, seed + 7)) - 0.05 * np.exp(-(d / 4) ** 2)
    col = ramp(t, [(0, (44, 30, 21)), (0.5, (96, 68, 47)), (1, (140, 104, 74))])
    # split lines (checks) radiating from the pith
    ang = np.arctan2(yy - cy, xx - cx)
    chk = np.clip(1 - np.abs(np.sin(ang * 1.5 + seed) ) * 9, 0, 1) * (d > 5) * 0.28
    col = col * (1 - chk[..., None])
    rgb[y0:y0 + h, x0:x0 + w] = col


def peg(rgb, cx, cy, r, seed=1):
    H, Wd = rgb.shape[:2]
    yy, xx = np.mgrid[0:H, 0:Wd].astype(np.float32)
    d = np.hypot(xx - cx, yy - cy)
    a = np.clip(r + 0.5 - d, 0, 1)
    seat = np.clip(1 - np.abs(d - (r + 1.2)) / 1.4, 0, 1) * 0.75
    rgb[:] = rgb * (1 - seat[..., None]) + np.array([14, 9, 6], np.float32) * seat[..., None]
    ring = 0.5 + 0.5 * np.cos(d * 1.9 + seed)
    shade = 0.5 + 0.14 * ring + 0.25 * np.clip((-(xx - cx) * 0.6 - (yy - cy) * 0.7) / max(r, 1), -1, 1)
    col = ramp(shade, [(0, (60, 42, 28)), (0.5, (128, 92, 62)), (1, (186, 146, 104))])
    rgb[:] = rgb * (1 - a[..., None]) + col * a[..., None]


def nail(rgb, cx, cy, r):
    col, a, seat = S.sphere(*np.mgrid[0:rgb.shape[0], 0:rgb.shape[1]].astype(np.float32), cx, cy, r, (92, 94, 96))
    rgb[:] = rgb * (1 - 0.7 * seat[..., None]) + np.array([8, 8, 8], np.float32) * 0.7 * seat[..., None]
    rgb[:] = rgb * (1 - a[..., None]) + col * a[..., None]


def wear(rgb, di, fw, seed=1, amount=1.0):
    """Worn edges: light bare-wood chips on the outer crest, dark grime in the lower corners of the groove."""
    h, w = di.shape
    n = norm(blur(noise(w, h, 5, seed + 60, 2.0), 0.6)) + 0.4 * norm(noise(w, h, 2, seed + 61))
    crest = np.clip(1 - np.abs(di - 4) / 4.5, 0, 1)
    chips = np.clip(n - 1.15, 0, 1.2) * crest
    rgb += np.array([62, 46, 30], np.float32) * (chips * amount)[..., None]
    grime = np.clip(norm(blur(noise(w, h, 30, seed + 62), 3)) - 0.7, 0, 1) * np.clip(1 - np.abs(di - fw * 0.5) / (fw * 0.5 + 1), 0, 1)
    rgb *= (1 - 0.28 * grime)[..., None]


def planks(w, h, vertical, seed, warm=1.0, tone=1.0):
    """Planked field with bevelled seams (dark groove, lit lip), per-plank tone and slightly different grain per plank."""
    base = S.wood(w, h, vertical, warm, seed) * tone
    pw = max(18, (w if vertical else h) // 6)
    axis = np.arange(w if vertical else h)
    pos = axis % pw
    groove = np.clip(1 - np.abs(pos - 1.0) / 1.6, 0, 1)
    lip = np.clip(1 - np.abs(pos - 3.2) / 1.6, 0, 1)
    f = (1 - 0.55 * groove + 0.16 * lip)
    f = f[None, :] if vertical else f[:, None]
    # butt joints: a short horizontal (or vertical) seam on some planks
    rng = np.random.default_rng(seed)
    out = base * f[..., None]
    n = (w if vertical else h) // pw + 1
    for i in range(n):
        if rng.random() < 0.7:
            c = int(rng.uniform(0.2, 0.8) * (h if vertical else w))
            lo, hi = i * pw, min((i + 1) * pw, w if vertical else h)
            if vertical:
                out[max(0, c - 1):c + 1, lo:hi] *= 0.5
                out[c + 1:c + 3, lo:hi] *= 1.1
            else:
                out[lo:hi, max(0, c - 1):c + 1] *= 0.5
                out[lo:hi, c + 1:c + 3] *= 1.1
    return out


def timber_build(w, h, fw, liner=0, caps=False, plaited=False, pegs_per_bar=2, nails=True, seed=1, warm_edge=0.0, plank_tone=1.0, inner_dark=0.5):
    """Timber panel/slot: planked field inside a wooden frame (top/bottom bars run full width, side bars between, so the bar joints and
    end-grain corner blocks show), carved groove, bevels, pegs, optional bronze liner with plait, corner caps with a knot, nails, wear."""
    yy, xx = S.grid(w, h)
    di = S.box_sdf(yy, xx, 0, 0, w, h, r=3.0)
    body = np.clip(di + 0.5, 0, 1)
    rgb = planks(w, h, True, seed, 1.0, plank_tone)
    inner = np.clip((di - fw - liner) / 30, 0, 1)
    rgb *= (1 - inner_dark * (1 - inner))[..., None]
    # frame bars
    bar_h = S.wood(w, fw, False, 1.0, seed + 3)
    bar_v = S.wood(fw, h, True, 1.0, seed + 4)
    frame = np.zeros_like(rgb)
    frame[:fw] = bar_h[:fw]
    frame[h - fw:] = bar_h[:fw][::-1]
    frame[fw:h - fw, :fw] = bar_v[fw:h - fw, :fw]
    frame[fw:h - fw, w - fw:] = bar_v[fw:h - fw, :fw]
    fmask = np.zeros((h, w), np.float32)
    fmask[:fw] = fmask[h - fw:] = 1
    fmask[:, :fw] = fmask[:, w - fw:] = 1
    fmask = blur(fmask, 0.8)
    light = S.bevel(di, 0, fw * 0.3) * 0.5 - S.bevel(di, fw * 0.72, fw) * 0.5
    carve = np.clip(1 - np.abs(di - fw * 0.5) / 1.4, 0, 1)
    frame = frame * (1 + light)[..., None]
    frame *= (1 - 0.5 * carve)[..., None]
    frame += (np.clip(1 - np.abs(di - (fw * 0.5 + 2.2)) / 1.2, 0, 1) * 18)[..., None]   # lit lip of the carved groove
    rgb = rgb * (1 - fmask[..., None]) + frame * fmask[..., None]
    # joint lines where the side bars meet the top/bottom bars
    for y in (fw, h - fw):
        rgb[y - 1:y + 1, fw:w - fw] *= 0.55
    # end-grain corner blocks
    for bx, by, sd in ((0, 0, 1), (w - fw, 0, 2), (0, h - fw, 3), (w - fw, h - fw, 4)):
        blk = np.zeros((fw, fw, 3), np.float32)
        end_grain(rgb, bx, by, fw, fw, seed + sd)
        sub = rgb[by:by + fw, bx:bx + fw]
        lb = S.bevel(di[by:by + fw, bx:bx + fw], 0, fw * 0.3) * 0.4
        rgb[by:by + fw, bx:bx + fw] = sub * (1 + lb)[..., None]
    # bronze liner just inside the frame
    if liner:
        lin = np.clip(1 - np.abs(di - (fw + liner / 2)) / (liner / 2 + 0.6), 0, 1)
        rgb = rgb * (1 - lin[..., None]) + S.bronze(0.34 + 0.05 * norm(noise(w, h, 4, seed + 21))) * lin[..., None]
        rgb = S.over(rgb, (10, 7, 5), np.clip(1 - np.abs(di - (fw + liner + 0.8)) / 1.4, 0, 1) * 0.7)
        if plaited:
            plait_strip(rgb, fw, fw, w - 2 * fw, liner, False)
            plait_strip(rgb, fw, h - fw - liner, w - 2 * fw, liner, False)
            plait_strip(rgb, fw, fw + liner, h - 2 * fw - 2 * liner, liner, True)
            plait_strip(rgb, w - fw - liner, fw + liner, h - 2 * fw - 2 * liner, liner, True)
    # pegs through the bars and into the planks
    for i in range(pegs_per_bar):
        t = (i + 1) / (pegs_per_bar + 1)
        x = fw + (w - 2 * fw) * t
        y = fw + (h - 2 * fw) * t
        for cx, cy in ((x, fw * 0.25), (x, h - fw * 0.25), (fw * 0.25, y), (w - fw * 0.25, y)):
            peg(rgb, cx, cy, max(2.6, fw * 0.075), seed + i)
    if nails:
        pw = max(18, w // 6)
        for k in range(1, w // pw):
            for cy in (fw + liner + 9, h - fw - liner - 9):
                nail(rgb, k * pw - pw / 2 + 0.0 if False else (k - 0.5) * pw + 0.5 * pw, cy, 2.4)
    # corner caps with a knot
    if caps:
        for cx, cy, rot in ((fw * 0.62, fw * 0.62, 0), (w - fw * 0.62, fw * 0.62, 0.9), (fw * 0.62, h - fw * 0.62, 1.8), (w - fw * 0.62, h - fw * 0.62, 2.7)):
            plate_disc(rgb, cx, cy, fw * 0.5, int(cx))
            knot_at(rgb, cx, cy, fw * 0.34, n=3, rot=rot)
    if warm_edge:
        edge = np.clip(1 - np.abs(di - (fw + liner + 3)) / 4.5, 0, 1) * warm_edge
        rgb = S.over(rgb, (214, 158, 88), edge * 0.5)
    wear(rgb, di, fw, seed)
    dark = np.clip(1 - np.abs(di - 1.5) / 2.2, 0, 1) * 0.8
    rgb = S.over(rgb, (14, 10, 7), dark)
    rgb = rgb * (1 + 0.012 * norm(noise(w, h, 2, seed + 77)))[..., None]
    return np.concatenate([np.clip(rgb, 0, 255), body[..., None] * 255], -1).astype(np.uint8)


# ----------------------------------------------------------------------------------------------- pieces
def timber_button_plate(w, h, fw, chamfer, state, seed):
    """Wood plate (two planks) in a bevelled bronze frame; a trefoil knot on a round cap at each end; pegs; hover = warm edge; pressed = darker."""
    base = S.framed(w, h, planks(w, h, False, seed, 0.9, {'n': 0.85, 'h': 1.0, 'p': 0.62}[state]), S.bronze(S.bronze_tex(w, h) + {'n': 0.0, 'h': 0.04, 'p': -0.05}[state]),
                    fw, chamfer=chamfer, inner_shadow={'n': 0.3, 'h': 0.25, 'p': 0.7}[state], warm=0.8 if state == 'h' else 0, seed=seed)
    rgb = base[..., :3].astype(np.float32)
    for cx in (chamfer + h * 0.27, w - chamfer - h * 0.27):
        plate_disc(rgb, cx, h / 2, h * 0.27, seed)
        knot_at(rgb, cx, h / 2, h * 0.175, n=3, rot=0.4 if cx < w / 2 else 1.4)
    for cx in (w * 0.3, w * 0.7):
        for cy in (fw + 6, h - fw - 6):
            nail(rgb, cx, cy, 2.2)
    if state == 'p':
        rgb *= 0.9
    base[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    return base


def tab_piece(w, h, fw, seed):
    base = S.framed(w, h, planks(w, h, False, seed, 0.95, 0.95), S.bronze(S.bronze_tex(w, h)), fw, chamfer=22, inner_shadow=0.25, warm=0.6, seed=seed)
    rgb = base[..., :3].astype(np.float32)
    for cx in (22 + h * 0.3, w - 22 - h * 0.3):
        plate_disc(rgb, cx, h / 2, h * 0.30, seed)
        knot_at(rgb, cx, h / 2, h * 0.2, n=3, rot=0.2 if cx < w / 2 else 1.2)
    base[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    return base


def divider(w=424, h=70):
    cv = K.Canvas(w, h)
    # a plait runs to each side of a pentafoil knot, with a small trefoil at each end
    K.plait(cv, 60, w / 2 - 36, h / 2, 10, 28, 3.2, step=0.4, halo=1.6)
    K.plait(cv, w / 2 + 36, w - 60, h / 2, 10, 28, 3.2, step=0.4, halo=1.6)
    K.draw_knot(cv, K.torus_knot(5, w / 2, h / 2, 31), 4.4, halo=2.0, tone=0.02)
    for cx, rot in ((30, 0.3), (w - 30, 1.4)):
        K.draw_knot(cv, K.torus_knot(3, cx, h / 2, 22), 3.8, halo=1.8)
    rgb = cv.rgb.copy()
    # cast shadow so it reads as relief on whatever it is laid on
    out = np.concatenate([np.clip(rgb, 0, 255), cv.cov[..., None] * 255], -1).astype(np.uint8)
    return out


def timber_button_large():
    """The wired timber_button (1983x793): the r1 bronze frame with a trefoil knot on a round bronze plate in each corner instead of a rivet."""
    im = Image.open(os.path.join(TEX, 'r1', 'panel_button.png')).convert('RGBA')
    a = np.array(im).astype(np.float32)
    rgb = a[..., :3].copy()
    X0, Y0, X1, Y1 = 57, 105, 1927, 656
    for i, (cx, cy) in enumerate([(X0 + 46, Y0 + 46), (X1 - 47, Y0 + 46), (X0 + 46, Y1 - 47), (X1 - 47, Y1 - 47)]):
        plate_disc(rgb, cx, cy, 21, 5 + i)
        knot_at(rgb, cx, cy, 14.5, strand=2.0, n=3, rot=0.7 * i)
    a[..., :3] = rgb
    return np.clip(a, 0, 255).astype(np.uint8)


def build_all():
    out = {}
    img, kn = W.background(knots=8)
    Image.fromarray(img, 'RGB').save(os.path.join(R2, 'panel_background.png'), optimize=True)
    Image.fromarray(timber_button_large(), 'RGBA').save(os.path.join(R2, 'panel_button.png'), optimize=True)
    P = {}
    P['panel_timber'] = timber_build(380, 356, 46, liner=16, caps=True, plaited=True, pegs_per_bar=3, seed=1)
    P['slot_timber'] = timber_build(260, 252, 36, liner=6, caps=False, pegs_per_bar=2, nails=False, seed=5, plank_tone=0.7, inner_dark=0.6)
    P['slot_selected'] = timber_build(262, 254, 36, liner=6, caps=False, pegs_per_bar=2, nails=False, seed=5, plank_tone=0.7, inner_dark=0.5, warm_edge=1.0)
    P['button_normal'] = timber_button_plate(468, 156, 28, 34, 'n', 8)
    P['button_hover'] = timber_button_plate(468, 156, 28, 34, 'h', 9)
    P['button_pressed'] = timber_button_plate(468, 156, 28, 34, 'p', 10)
    P['tab_active'] = tab_piece(316, 96, 20, 11)
    P['divider_bronze'] = divider()
    for n, arr in P.items():
        Image.fromarray(arr, 'RGBA').save(os.path.join(R2, n + '.png'), optimize=True)
    return P


if __name__ == '__main__':
    build_all()
    files = {}
    for f in sorted(os.listdir(R2)):
        p = os.path.join(R2, f)
        files[f] = {'size': list(Image.open(p).size), 'sha256': hashlib.sha256(open(p, 'rb').read()).hexdigest()}
    info = {'item': 'meadworks_panel_r2', 'licence': 'CC BY-NC-SA 4.0 (MeadWorks asset licence); CC0 inputs are public domain',
            'method': 'Python (Pillow/numpy); no pixels of PackPanel or of the uploaded look reference, no AI tools; build/build_panel_r2.py, wood_r2.py, knotwork.py',
            'cc0_inputs': sorted(B.used), 'cc0_sha256': B.used,
            'changes': 'Background: three CC0 wood maps merged, then sampled through a periodic domain-warp flow field (FFT-filtered noise) so the grain bends; eight knots each pull the grain '
                       'round them (sample rows move towards the knot row) and add growth rings and a dark heart; colour and tone drift from low-frequency noise; seamless. Timber pieces: planks with '
                       'bevelled seams and butt joints, frame bars with carved groove, end-grain corner blocks with off-centre rings, pegs, nail heads, edge chips. Knotwork: (2,3) and (2,5) torus-knot '
                       'projections and two-strand plaits drawn procedurally as shaded bronze tubes with an alternating over-under gap, embossed on corner caps, plait liner, divider, tab and button ends '
                       'and the four corners of the timber button. Hover is a subtle warm edge.',
            'files': files}
    json.dump(info, open(os.path.join(HERE, '..', 'provenance', 'meadworks_panel_r2.json'), 'w'), indent=2)
    print('ok', len(files))
