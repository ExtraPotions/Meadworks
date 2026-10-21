"""The axe's head, all bone: a skull with a rawhide band round its brow, the jawbone crescent (axe_blade) set against
its one temple on the oval boss of its joint, a curved tusk driven through the other temple for the back hook, and a
fang driven into the crown. Everything lies in the axe's plane x = 0.

Head coordinates are the skull's (see axe_skull); `origin` in build() puts them on the spine.
"""
from mathutils import Matrix, Vector

import axe_blade
import axe_mesh as geo
import axe_skull

X = -0.012                                   # the axe's plane in head coordinates (the skull sits 12 mm forward)
SKULL_SCALE = 1.2                            # the skull, its band and fang, against a life-size skull
BAND = (0.047, 0.101, 0.081, -0.015)          # height, half depth, half width, centre x (life size)
HOOK = [(0.060, 0.040), (0.110, 0.050), (0.195, 0.080), (0.285, 0.066), (0.350, 0.006), (0.372, -0.076),
        (0.350, -0.150)]                     # the tusk's centre line (y, z), final size, from inside the skull
HOOK_RADII = [0.029, 0.029, 0.025, 0.020, 0.0145, 0.0080, 0.0012]
BOSS = ((-0.118, -0.004), (0.040, 0.034, 0.100))   # the blade's joint end at the temple: (y, z), half sizes


def build(mats, origin):
    """Skull, band, blade, boss, hook and fang, moved to `origin` (the skull's centre in axe coordinates). The skull,
    its band and the fang are modelled life size and grown by SKULL_SCALE; the rest is at final size."""
    m = Matrix.Translation(origin)
    hide = mats["leather"]
    grown = [_band(hide), _fang(mats["tusk"])]
    for part in grown:
        geo.place(part, grow())
    final = axe_blade.build(mats, X) + _knuckle(mats["blade"]) + [_hook(mats["tusk"]), _hook_binding(mats["sinew"])]
    for part in grown + final:
        geo.place(part, m)
    return axe_skull.build(mats, m @ grow()) + grown + final


def grow():
    """Life size to final size, about the axe's plane so everything stays in it."""
    return Matrix.Translation((X, 0, 0)) @ Matrix.Scale(SKULL_SCALE, 4) @ Matrix.Translation((-X, 0, 0))


def above_spine():
    """Where the skull's origin sits above the top of the spine: its underside just touching the last vertebra."""
    return Vector((-X, 0.0, 0.072 * SKULL_SCALE))


def _band(mat):
    """A rawhide strap round the brow."""
    height, depth, width, cx = BAND
    stations = [(height - 0.010, depth, width, cx, 0), (height - 0.008, depth + 0.004, width + 0.004, cx, 0),
                (height + 0.008, depth + 0.004, width + 0.004, cx, 0), (height + 0.010, depth, width, cx, 0)]
    return geo.loft_z("hide_band", stations, mat, sides=28, power=1.0)


def _fang(mat):
    """A great fang out of the crown, leaning a little towards the edge, round like the game's wolf fangs."""
    points = [(X, 0.0, 0.080), (X, -0.004, 0.150), (X, -0.018, 0.215), (X, -0.040, 0.280)]
    radii = [(0.020, 0.018), (0.016, 0.014), (0.009, 0.008), (0.0012, 0.0012)]
    return geo.sweep("bone_fang", points, radii, mat, hint=(1, 0, 0), sides=6, smooth=True)


def _knuckle(mat):
    """The jaw's joint: one oval boss against the skull's temple, where the blade's root meets it."""
    (y, z), half = BOSS
    return [geo.knob("bone_boss", (X, y, z), half, mat, segments=8, rings=6)]


def _hook(mat):
    """The back hook: a tusk driven through the temple, curling down to its point."""
    points = [(X, y, z) for y, z in HOOK]
    radii = [(r, r * 0.9) for r in HOOK_RADII]
    return geo.sweep("bone_hook", points, radii, mat, hint=(1, 0, 0), sides=6, smooth=True)


def _hook_binding(mat):
    """Turns of sinew round the tusk where it leaves the skull, each turn a bulge."""
    path = [p for p in geo.densify([(X, y, z) for y, z in HOOK[1:3]], 0.0045) if p.y <= 0.150]
    radii = [(0.033, 0.030) if k % 2 else (0.030, 0.027) for k in range(len(path))]
    return geo.sweep("hook_binding", path, radii, mat, hint=(1, 0, 0), sides=6, smooth=True)
