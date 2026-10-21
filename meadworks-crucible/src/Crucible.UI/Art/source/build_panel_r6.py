"""Revision 6: Cairn's compass plank redone as "iron-bound wood" (user re-pick 2026-10-14 10:39, compass option 2).
One calm plank (no board seams) held by dark forged-iron straps along the top and bottom with rivets out along them, and an
iron end cap at each end that stands proud of the straps, with an orange rim and three rivets (the middle one is placed by Cairn
as KitHudArt.IronRivet, because the plank's middle rows stretch). The heading inset is restyled in the same strap iron.
Same clean-room rules and tools as r2-r5: CC0 maps only (assets/cc0), every shape drawn here, no game, mod, sketch or AI pixels.
Run after build_panel_r4.py: python build_panel_r6.py  -> ../textures/r6, ../provenance/meadworks_panel_r6.json
"""
import hashlib, json, os
import numpy as np
from PIL import Image
import build_panel_art as B
import build_panel_set as S
import build_panel_r2 as R
import build_panel_r4 as R4
from build_panel_art import blur, noise, norm, ramp

HERE = os.path.dirname(os.path.abspath(__file__))
R6 = os.path.join(HERE, '..', 'textures', 'r6')
os.makedirs(R6, exist_ok=True)
ORANGE = R4.ORANGE
PX = R4.PX
# Plank geometry in source pixels (4 per UI unit; Cairn draws the plank 240 x 51 units). The nine-slice border stays
# (120, 40, 120, 40): the caps, the straps and the top and bottom cap rivets sit inside it and never stretch.
CAP = 22 * PX          # HudLayout.StripCap
PROUD = 3              # the straps sit this far inside the caps' top and bottom edges
BAND = 4 * PX          # strap thickness
CAP_RIVET_Y = 24       # top and bottom cap rivets; the middle one is Cairn's (height / 2)
STRAP_RIVET_X = (0.27, 0.73)


def save(name, arr):
    Image.fromarray(arr, 'RGBA').save(os.path.join(R6, name + '.png'), optimize=True)


def one_plank(w, h, seed):
    """One horizontal board: the set's CC0 wood maps without the board seams, long painted grain streaks and a few soft rings."""
    c1 = S.lum(S.cover('ambientcg/Wood066/Wood066_1K-JPG_Color.jpg', w, h, False, 1.0))
    c2 = S.lum(S.cover('ambientcg/Wood051/Wood051_1K-JPG_Color.jpg', w, h, False, 1.0, True, 300))
    rl = S.cover('ambientcg/Wood049/Wood049_1K-JPG_Displacement.jpg', w, h, True, 1.6)
    streaks = norm(blur(noise(w, h, 6, seed + 10, 24.0), 1.2))          # long, thin, along the length
    broad = norm(blur(noise(w, h, 60, seed + 11, 3.0), 10))            # slow tone drift, like a painted wash
    t = 0.5 + 0.06 * norm(c1) + 0.04 * norm(c2) + 0.035 * norm(rl) + 0.03 * streaks + 0.035 * broad
    return ramp(t, [(0, (40, 27, 18)), (0.5, (104, 70, 44)), (1, (156, 112, 74))])


def strap_iron(w, h, seed):
    """The straps' iron: the r4 forged iron, darker and cooler, so the binding reads against the wood."""
    return R4.iron(w, h, seed) * np.array([0.6, 0.6, 0.64], np.float32)


def compass_plank(w=240 * PX, h=51 * PX, seed=71):
    yy, xx = S.grid(w, h)
    rgb = one_plank(w, h, seed)
    # the board: lit along the top, shaded towards the bottom, darker where the straps press it
    vy = (yy - PROUD - BAND) / max(1.0, h - 2 * (PROUD + BAND))
    rgb *= (1.08 - 0.2 * np.clip(vy, 0, 1) ** 1.4)[..., None]
    inner = np.minimum(yy - PROUD - BAND, h - PROUD - BAND - 1 - yy)        # distance from the straps into the wood
    rgb *= (1 - 0.5 * np.clip(1 - inner / (PX * 2.2), 0, 1) * (inner >= 0))[..., None]
    # straps: dark hammered iron, a lit upper edge and a dark lower edge on each, full length under the caps
    top = (yy >= PROUD) & (yy < PROUD + BAND)
    bottom = (yy >= h - PROUD - BAND) & (yy < h - PROUD)
    straps = (top | bottom).astype(np.float32)
    sy = np.where(top, yy - PROUD, yy - (h - PROUD - BAND)) / BAND             # 0 at a strap's top edge, 1 at its bottom
    shade = 1 + 0.45 * np.clip(1 - sy / 0.18, 0, 1) - 0.45 * np.clip((sy - 0.8) / 0.2, 0, 1)
    rgb = rgb * (1 - straps[..., None]) + strap_iron(w, h, seed) * shade[..., None] * straps[..., None]
    for x in STRAP_RIVET_X:
        for cy in (PROUD + BAND / 2, h - PROUD - BAND / 2):
            R4.rivet(rgb, x * w, cy, PX * 1.2)
    wood_and_straps = ((yy >= PROUD) & (yy < h - PROUD)).astype(np.float32)
    # end caps: full height, standing proud of the straps, chamfered, an orange rim near the edge and two rivets each
    alpha = wood_and_straps
    for x0 in (0, w - CAP):
        cd = S.box_sdf(yy, xx, x0, 0, x0 + CAP, h, r=0, chamfer=PX * 2.5)
        m = np.clip(cd + 0.5, 0, 1)
        plate = R4.iron(w, h, seed + 7 + x0) * np.array([0.72, 0.72, 0.75], np.float32)
        plate *= (1 + S.bevel(cd, 0, PX * 1.6) * 0.55)[..., None]
        plate = S.over(plate, ORANGE, R4.line(cd, PX * 1.1, 1.7) * 0.95)
        plate = S.over(plate, (12, 9, 6), R4.line(cd, PX * 1.1 + 3, 1.3) * 0.55)
        R.wear(plate, cd, PX * 2, seed + x0, 0.35)
        # the cap's shadow on the straps and wood beside it
        side = (xx - (x0 + CAP)) if x0 == 0 else (x0 - xx)
        rgb *= (1 - 0.45 * np.clip(1 - side / (PX * 1.5), 0, 1) * (side >= 0))[..., None]
        rgb = rgb * (1 - m[..., None]) + plate * m[..., None]
        for cy in (CAP_RIVET_Y, h - CAP_RIVET_Y):
            R4.rivet(rgb, x0 + CAP / 2, cy, PX * 1.7)
        alpha = np.maximum(alpha, m)
    outer = np.minimum(S.box_sdf(yy, xx, 0, 0, w, h, r=0, chamfer=PX * 2.5), np.where(wood_and_straps > 0, np.minimum(yy - PROUD, h - PROUD - 1 - yy) + 1, 999))
    rgb = S.over(rgb, (10, 8, 6), R4.line(outer, 1.2, 1.8) * 0.8 * (np.abs(xx - w / 2) < w / 2 - CAP + 2))
    rgb = rgb * (1 + 0.012 * norm(noise(w, h, 2, 77 + seed)))[..., None]
    return np.concatenate([np.clip(rgb, 0, 255), alpha[..., None] * 255], -1).astype(np.uint8)


def compass_inset(w=64 * PX, h=14 * PX, seed=72):
    """The heading notch: a dark recessed well framed in the strap iron, chamfered at the top, ending where the bottom strap ends."""
    yy, xx = S.grid(w, h)
    bottom = h - PROUD
    di = S.box_sdf(yy, xx, 0, 0, w, bottom + PX * 4, r=0, chamfer=PX * 3.5)
    di = np.minimum(di, bottom - yy)
    body = np.clip(di + 0.5, 0, 1)
    rim = PX * 1.6
    well = one_plank(w, h, seed) * 0.3
    well *= (1 - 0.4 * np.clip(1 - (di - rim) / (PX * 2.5), 0, 1))[..., None]
    frame = strap_iron(w, h, seed) * (1 + S.bevel(di, 0, rim) * 0.6)[..., None]
    f = np.clip((rim - di) / 1.2, 0, 1)
    rgb = well * (1 - f[..., None]) + frame * f[..., None]
    rgb = S.over(rgb, ORANGE, R4.line(di, rim + 0.5, 1.4) * 0.8)
    rgb = S.over(rgb, (10, 8, 6), R4.line(di, 1.2, 1.8) * 0.8)
    return np.concatenate([np.clip(rgb, 0, 255), body[..., None] * 255], -1).astype(np.uint8)


def build_all():
    P = {'compass_plank': compass_plank(), 'compass_inset': compass_inset()}
    for n, a in P.items():
        save(n, a)
    return P


if __name__ == '__main__':
    build_all()
    files = {}
    for f in sorted(os.listdir(R6)):
        p = os.path.join(R6, f)
        files[f] = {'size': list(Image.open(p).size), 'sha256': hashlib.sha256(open(p, 'rb').read()).hexdigest()}
    info = {'item': 'meadworks_panel_r6', 'licence': 'CC BY-NC-SA 4.0 (MeadWorks asset licence); CC0 inputs are public domain',
            'method': 'Python (Pillow/numpy); no pixels of PackPanel, of any game or mod texture or of the concept sketches, no AI tools; build/build_panel_r6.py (+ r2/r4/set modules)',
            'cc0_inputs': sorted(B.used), 'cc0_sha256': B.used,
            'changes': 'Cairn compass re-picked as option 2, iron-bound wood (user, 2026-10-14 10:39): one seamless plank of the set wood, dark forged-iron '
                       'straps along the top and bottom with two rivets each, and iron end caps that stand proud of the straps with an orange rim '
                       '(186, 112, 28) and a rivet top and bottom (Cairn adds the middle one); the heading inset restyled in the strap iron. '
                       'Replaces r4\'s compass_plank and compass_inset; r4\'s iron_rivet is unchanged.',
            'files': files}
    json.dump(info, open(os.path.join(HERE, '..', 'provenance', 'meadworks_panel_r6.json'), 'w'), indent=2)
    print('ok', len(files))
