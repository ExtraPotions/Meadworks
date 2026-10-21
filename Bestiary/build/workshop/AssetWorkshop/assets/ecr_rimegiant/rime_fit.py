"""The game's Troll as measured for the Rime Giant's parts: out/rimegiant/fit.json, written by the Unity step
(unity/Assets/Editor/RimeGiant/RimeFit.cs) from the reference export and never committed. build.ps1 measures first.

Unity axes and metres in the file; `blender` turns a Unity point into the workshop's Blender axes, which Unity's FBX
import turns back, so a part built from these numbers lands where they were measured.
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FIT = os.path.normpath(os.path.join(HERE, "..", "..", "out", "rimegiant", "fit.json"))
MISS = -90.0


def load():
    if not os.path.exists(FIT):
        raise SystemExit(f"no {FIT}: run assets/ecr_rimegiant/build.ps1, which measures the Troll first")
    with open(FIT, encoding="utf-8") as handle:
        return json.load(handle)


def blender(x, y, z):
    """Unity (x, y, z) -> Blender (-x, -z, y)."""
    return (-x, -z, y)


class Grid:
    """Heights on a regular grid (row by row, first row first), sampled bilinearly; None where any corner missed."""

    def __init__(self, values, columns, rows, x0, y0, step_x, step_y):
        self.values, self.columns, self.rows = values, columns, rows
        self.x0, self.y0, self.step_x, self.step_y = x0, y0, step_x, step_y

    def at(self, x, y):
        fx, fy = (x - self.x0) / self.step_x, (y - self.y0) / self.step_y
        if fx < 0 or fy < 0 or fx > self.columns - 1 or fy > self.rows - 1:
            return None
        i, j = min(int(fx), self.columns - 2), min(int(fy), self.rows - 2)
        tx, ty = fx - i, fy - j
        corners = [self.values[(j + b) * self.columns + i + a] for b in (0, 1) for a in (0, 1)]
        if min(corners) <= MISS:
            return None
        low = corners[0] + (corners[1] - corners[0]) * tx
        high = corners[2] + (corners[3] - corners[2]) * tx
        return low + (high - low) * ty


def plate_skin(plate):
    """The skin under a plate, in its own frame: height along +Z (out of the skin) at (x across, y up)."""
    w, h, c, r = plate["width"], plate["height"], plate["columns"], plate["rows"]
    return Grid(plate["depth"], c, r, -w / 2, -h / 2, w / (c - 1), h / (r - 1))


def slope(grid, x, y, step=0.04):
    """How steeply the grid rises at (x, y), as rise over run; None off the grid."""
    here = grid.at(x, y)
    around = [grid.at(x + step, y), grid.at(x - step, y), grid.at(x, y + step), grid.at(x, y - step)]
    if here is None or None in around:
        return None
    return math.hypot(around[0] - around[1], around[2] - around[3]) / (2 * step)
