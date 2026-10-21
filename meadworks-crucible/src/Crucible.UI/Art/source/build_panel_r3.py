"""Revision 3: map frames (round minimap, large map), Mantle equipment panel pieces, Cairn HUD pieces. Same clean-room rules and tools as r2.
Run after build_panel_r2.py: python build_panel_r3.py  -> ../textures/r3, ../provenance/meadworks_panel_r3.json
"""
import hashlib, json, os
import numpy as np
from PIL import Image
import build_panel_art as B
import build_panel_set as S
import knotwork as K
import wood_r2 as W
import build_panel_r2 as R
from build_panel_art import blur, noise, norm, ramp

HERE = os.path.dirname(os.path.abspath(__file__))
R3 = os.path.join(HERE, '..', 'textures', 'r3')
os.makedirs(R3, exist_ok=True)


def save(name, arr):
    Image.fromarray(arr, 'RGBA').save(os.path.join(R3, name + '.png'), optimize=True)


def map_frame_large(w=1024, h=576, fw=44, liner=12):
    """Rectangular frame for the large map: timber bars, plait liner, knot caps; the whole inside is transparent (map contents stay vanilla)."""
    a = R.timber_build(w, h, fw, liner=liner, caps=True, plaited=True, pegs_per_bar=6, nails=False, seed=21)
    yy, xx = S.grid(w, h)
    di = S.box_sdf(yy, xx, 0, 0, w, h, r=3.0)
    inside = np.clip((di - (fw + liner)) / 1.5, 0, 1)
    a[..., 3] = (a[..., 3].astype(np.float32) * (1 - inside)).astype(np.uint8)
    # soft inner shadow on the frame's inner lip so the map seems to sit below the wood
    lip = np.clip(1 - np.abs(di - (fw + liner + 1)) / 3.0, 0, 1) * 0.0
    return a


def map_frame_round(size=512):
    """Ring frame for the round minimap: wavy-grain wood ring, bronze inner rim with a plait, a trefoil-knot plate at N, E, S and W; transparent inside and outside."""
    c = size / 2.0
    yy, xx = S.grid(size, size)
    d = np.hypot(xx - c, yy - c)
    ro, ri = c - 2.0, c * 0.80          # outer radius, inner (map) radius
    rim = 13.0                          # bronze rim width
    img, _ = W.background(W=size, H=size, seed=9, knots=2)
    wood_rgb = img.astype(np.float32) * 0.95
    ring = np.clip(ro + 0.5 - d, 0, 1) * np.clip(d - (ri - rim) + 0.5, 0, 1)
    rgb = wood_rgb.copy()
    # bevel: lit on the outer top-left, shaded below; carved groove at the middle of the wood
    ang = np.arctan2(yy - c, xx - c)
    facing = -(np.cos(ang) * 0.62 + np.sin(ang) * 0.78)
    wood_mid = (ro + ri) / 2
    outer = np.clip(1 - np.abs(d - (ro - 4)) / 4.5, 0, 1)
    rgb *= (1 + 0.45 * facing * outer)[..., None]
    carve = np.clip(1 - np.abs(d - wood_mid) / 1.5, 0, 1)
    rgb *= (1 - 0.45 * carve)[..., None]
    rgb += (np.clip(1 - np.abs(d - (wood_mid + 2.4)) / 1.2, 0, 1) * 16)[..., None]
    # bronze rim with plait
    rimmask = np.clip(1 - np.abs(d - (ri - rim / 2)) / (rim / 2 + 0.5), 0, 1)
    bron = S.bronze(0.34 + 0.05 * norm(noise(size, size, 4, 7)) + 0.16 * np.clip(1 - np.abs(d - (ri - rim / 2)) / (rim / 2), 0, 1) * facing)
    rgb = rgb * (1 - rimmask[..., None]) + bron * rimmask[..., None]
    rgb = S.over(rgb, (10, 7, 5), np.clip(1 - np.abs(d - (ri - rim - 0.8)) / 1.4, 0, 1) * 0.7)
    rgb = S.over(rgb, (10, 7, 5), np.clip(1 - np.abs(d - ri) / 1.2, 0, 1) * 0.6)
    # plait around the rim: draw straight into a strip and wrap it
    L = int(2 * np.pi * (ri - rim / 2))
    strip = K.Canvas(L, int(rim))
    r_s = rim * 0.19
    K.plait(strip, 0, L, rim / 2, rim * 0.5 - r_s - 1.0, L / round(L / (rim * 2.6)), r_s, step=0.5, halo=max(1.0, r_s * 0.5))
    rr = ri - rim / 2
    th = np.arctan2(yy - c, xx - c) % (2 * np.pi)
    u = (th / (2 * np.pi) * L).astype(int) % L
    v = np.clip((d - (ri - rim)).astype(int), 0, int(rim) - 1)
    onrim = (d >= ri - rim) & (d <= ri)
    cov = np.where(onrim, strip.cov[v, u], 0)
    col = strip.rgb[v, u]
    rgb = rgb * (1 - cov[..., None]) + col * cov[..., None]
    # four cardinal knot plates sitting on the wood
    for ca in (-np.pi / 2, 0, np.pi / 2, np.pi):
        px, py = c + np.cos(ca) * (wood_mid + 1), c + np.sin(ca) * (wood_mid + 1)
        R.plate_disc(rgb, px, py, 21, int(px))
        R.knot_at(rgb, px, py, 14, strand=2.1, n=3, rot=ca)
    # edge wear
    n = norm(blur(noise(size, size, 4, 33), 0.6))
    chips = np.clip(n - 1.2, 0, 1) * np.clip(1 - np.abs(d - (ro - 3)) / 3.5, 0, 1)
    rgb += np.array([60, 44, 28], np.float32) * chips[..., None]
    rgb = S.over(rgb, (14, 10, 7), np.clip(1 - np.abs(d - ro) / 1.6, 0, 1) * 0.8 * (d <= ro + 1))
    a = np.clip(ro + 0.5 - d, 0, 1) * np.clip(d - (ri - rim) + 0.5, 0, 1)
    return np.concatenate([np.clip(rgb, 0, 255), a[..., None] * 255], -1).astype(np.uint8)


def iron_slot(w, h, fw=34, seed=31, selected=False):
    """Consumable slot: planked well in a forged-iron frame with four riveted corners and a bronze edge when selected."""
    yy, xx = S.grid(w, h)
    di = S.box_sdf(yy, xx, 0, 0, w, h, r=3.0)
    body = np.clip(di + 0.5, 0, 1)
    rgb = R.planks(w, h, True, seed, 0.8, 0.7)
    inner = np.clip((di - fw) / 26, 0, 1)
    rgb *= (1 - 0.6 * (1 - inner))[..., None]
    steel = S.steel(w, h) * 1.15
    band = np.clip((fw - di) / 1.5, 0, 1)
    light = S.bevel(di, 0, fw * 0.3) * 0.5 - S.bevel(di, fw * 0.72, fw) * 0.5
    rgb = rgb * (1 - band[..., None]) + steel * (1 + light)[..., None] * band[..., None]
    rgb = S.over(rgb, (10, 10, 10), np.clip(1 - np.abs(di - fw) / 1.6, 0, 1) * 0.7)
    for cx, cy in ((fw * 0.5, fw * 0.5), (w - fw * 0.5, fw * 0.5), (fw * 0.5, h - fw * 0.5), (w - fw * 0.5, h - fw * 0.5)):
        R.nail(rgb, cx, cy, fw * 0.2)
    if selected:
        edge = np.clip(1 - np.abs(di - (fw + 3)) / 4.5, 0, 1)
        rgb = S.over(rgb, (214, 158, 88), edge * 0.5)
    R.wear(rgb, di, fw, seed, 0.7)
    rgb = S.over(rgb, (14, 10, 7), np.clip(1 - np.abs(di - 1.5) / 2.2, 0, 1) * 0.8)
    return np.concatenate([np.clip(rgb, 0, 255), body[..., None] * 255], -1).astype(np.uint8)


def icon_plate(size=96):
    """Round bronze plate behind a slot icon, with a knot ring on its rim."""
    rgb = np.zeros((size, size, 3), np.float32)
    c = size / 2
    R.plate_disc(rgb, c, c, c - 3, 5)
    yy, xx = S.grid(size, size)
    d = np.hypot(xx - c, yy - c)
    inner = np.clip(c * 0.62 + 0.5 - d, 0, 1)
    rgb = rgb * (1 - 0.8 * inner[..., None]) + np.array([22, 16, 11], np.float32) * (0.8 * inner[..., None])
    a = np.clip(c - 1.5 - d, 0, 1)
    return np.concatenate([np.clip(rgb, 0, 255), a[..., None] * 255], -1).astype(np.uint8)


def compass_strip(w=480, h=56):
    """Strip compass backdrop: dark wood well in a thin bronze frame with a trefoil-knot plate at each end; the middle is calm so ticks and labels read."""
    base = S.framed(w, h, R.planks(w, h, False, 41, 0.75, 0.5), S.bronze(S.bronze_tex(w, h)), 9, chamfer=14, inner_shadow=0.35, seed=41)
    rgb = base[..., :3].astype(np.float32)
    for cx in (h * 0.62, w - h * 0.62):
        R.plate_disc(rgb, cx, h / 2, h * 0.42, 41)
        R.knot_at(rgb, cx, h / 2, h * 0.27, strand=2.2, n=3, rot=0.5 if cx < w / 2 else 1.5)
    # centre notch (the heading pointer)
    yy, xx = S.grid(w, h)
    notch = np.clip(1 - np.abs(xx - w / 2) / 2.2, 0, 1) * np.clip((yy - 0) / 1.0, 0, 1) * (yy < 14)
    rgb = S.over(rgb, (200, 150, 90), notch * 0.9)
    base[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    return base


def round_plate(size=64, rot=0.0):
    rgb = np.zeros((size, size, 3), np.float32)
    c = size / 2
    R.plate_disc(rgb, c, c, c - 2.5, 3)
    R.knot_at(rgb, c, c, c * 0.52, strand=2.1, n=3, rot=rot)
    yy, xx = S.grid(size, size)
    a = np.clip(c - 1.5 - np.hypot(xx - c, yy - c), 0, 1)
    return np.concatenate([np.clip(rgb, 0, 255), a[..., None] * 255], -1).astype(np.uint8)


def heading_plate(w=170, h=46):
    base = S.framed(w, h, R.planks(w, h, False, 43, 0.75, 0.55), S.bronze(S.bronze_tex(w, h)), 7, chamfer=12, inner_shadow=0.35, seed=43)
    rgb = base[..., :3].astype(np.float32)
    for cx in (h * 0.5, w - h * 0.5):
        R.plate_disc(rgb, cx, h / 2, h * 0.38, 43)
        R.knot_at(rgb, cx, h / 2, h * 0.24, strand=1.9, n=3, rot=0.8)
    base[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    return base


def build_all():
    P = {}
    P['map_frame_large'] = map_frame_large()
    P['map_frame_round'] = map_frame_round()
    P['equipment_panel'] = R.timber_build(420, 320, 40, liner=14, caps=True, plaited=True, pegs_per_bar=3, nails=True, seed=51)
    P['slot_equipment'] = R.timber_build(128, 128, 20, liner=5, caps=False, pegs_per_bar=1, nails=False, seed=52, plank_tone=0.7, inner_dark=0.55)
    P['slot_consumable'] = iron_slot(128, 128, 18, 53)
    P['slot_consumable_selected'] = iron_slot(128, 128, 18, 53, True)
    P['icon_plate'] = icon_plate()
    P['hud_panel'] = R.timber_build(360, 128, 26, liner=8, caps=True, plaited=False, pegs_per_bar=2, nails=False, seed=54)
    P['compass_strip'] = compass_strip()
    P['cardinal_plate'] = round_plate()
    P['heading_plate'] = heading_plate()
    for n, a in P.items():
        save(n, a)
    return P


if __name__ == '__main__':
    build_all()
    files = {}
    for f in sorted(os.listdir(R3)):
        p = os.path.join(R3, f)
        files[f] = {'size': list(Image.open(p).size), 'sha256': hashlib.sha256(open(p, 'rb').read()).hexdigest()}
    info = {'item': 'meadworks_panel_r3', 'licence': 'CC BY-NC-SA 4.0 (MeadWorks asset licence); CC0 inputs are public domain',
            'method': 'Python (Pillow/numpy); no pixels of PackPanel, of the uploaded look reference or of any game or mod texture, no AI tools; build/build_panel_r3.py (+ r2 modules)',
            'cc0_inputs': sorted(B.used), 'cc0_sha256': B.used,
            'changes': 'Map frames: the r2 domain-warped wood and bronze plait liner re-used for a wooden ring (round minimap) and a rectangular frame (large map), both transparent inside, '
                       'with trefoil knot plates; equipment panel, equipment and consumable slot frames, icon plate; Cairn HUD panel, compass strip backdrop, cardinal plate, heading plate with knot ends.',
            'files': files}
    json.dump(info, open(os.path.join(HERE, '..', 'provenance', 'meadworks_panel_r3.json'), 'w'), indent=2)
    print('ok', len(files))
