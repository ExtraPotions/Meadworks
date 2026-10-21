"""A Blender scene of the kraken's whole fight against the game's longship, animated with the mod's own motion
(fight_motion.py, a port of EliteCreaturesPack/Kraken) and baked to keyframes, so it plays in Blender with no script:

    blender --background --factory-startup --python assets/ecp_kraken/fight.py -- [--stills] [--frames N] [--standin] [--ship Karve|Raft|VikingShip|VikingShip_Ashlands]

Writes out/fight/kraken_fight.blend (textures packed) and, with --stills, a few rendered frames. The ship comes from
the game's own prefab through workshop.prefab when it is there, otherwise a plain stand-in hull. Everything the kraken
does round the ship is worked out in the ship's own space in Unity's axes (x right, y up, z forward), exactly as in the
mod, and turned into Blender's at the end.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path[:0] = [HERE, os.path.join(HERE, "..", "..", "blender")]

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import fight_motion as fm  # noqa: E402
import fight_rigs as fr  # noqa: E402
import fight_scene as fs  # noqa: E402

FPS = 24
OUT = os.path.join(HERE, "out", "fight")
U = fr.from_unity


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    frames = int(argv[argv.index("--frames") + 1]) if "--frames" in argv else None
    ship = argv[argv.index("--ship") + 1] if "--ship" in argv else "VikingShip"
    return "--stills" in argv, frames, "--standin" in argv, ship


# ---------------------------------------------------------------- the ship, in its own space (Unity axes)
class Ship:
    def __init__(self, root, meshes):
        bpy.context.view_layer.update()   # new objects' transforms are only worked out on an update
        self.root, self.meshes = root, meshes
        self.rest = root.matrix_world.copy()
        self._measure()
        self._bvh()
        self.m = self.rest.copy()

    def _measure(self):
        lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
        inv = self.rest.inverted()
        for obj in self.meshes:
            m = inv @ obj.matrix_world
            points = [U_back(m @ v.co) for v in obj.data.vertices]
            if not points:
                continue
            axes = list(zip(*points))
            p_lo, p_hi = Vector([min(a) for a in axes]), Vector([max(a) for a in axes])
            if (p_lo.y + p_hi.y) / 2 < 3.5:
                lo, hi = Vector(map(min, lo, p_lo)), Vector(map(max, hi, p_hi))
        self.half_width = max(abs(lo.x), abs(hi.x))
        self.half_length = (hi.z - lo.z) / 2
        self.middle_z = (hi.z + lo.z) / 2
        self.rail = hi.y
        print(f"FIGHT ship: half width {self.half_width:.2f}, half length {self.half_length:.2f}, middle {self.middle_z:.2f}, rail {self.rail:.2f}")

    def _bvh(self):
        verts, polys, inv = [], [], self.rest.inverted()
        for obj in self.meshes:
            m, base = inv @ obj.matrix_world, len(verts)
            verts += [m @ v.co for v in obj.data.vertices]
            polys += [[base + i for i in p.vertices] for p in obj.data.polygons]
        self.bvh = BVHTree.FromPolygons(verts, polys)

    def top(self, x, z):
        """The ship's top at (x, z) of its own space, as its y; None over open water."""
        hit = self.bvh.ray_cast(U((x, self.rail + 4.0, z)), Vector((0, 0, -1)), 12.0)
        return None if hit[0] is None else hit[0].z

    def world(self, p):
        return self.m @ U(p)

    def direction(self, d):
        return self.m.to_3x3() @ U(d)

    @property
    def up(self):
        return (self.m.to_3x3() @ Vector((0, 0, 1))).normalized()

    def waterline(self):
        at = self.m.translation
        return U_back(self.m.inverted() @ Vector((at.x, at.y, 0.0))).y

    def anchor(self, i, along=None):
        side = -1.0 if i < 3 else 1.0
        spacing = max(self.half_length * 0.8, 3.5)
        z = self.middle_z + (1.0, 0.0, -1.0)[i % 3] * spacing if along is None else along
        return Vector((side * (self.half_width + 1.8), self.waterline(), z))

    def frame(self, i, towards, along=None):
        a = self.anchor(i, along)
        d = Vector((towards[0] - a.x, 0.0, towards[2] - a.z))
        return fm.Frame(self.world(a), self.direction(d), self.up)

    def across(self, i, along=None):
        return self.frame(i, (0.0, 0.0, self.anchor(i, along).z), along)

    def deck(self, i, towards, along=None):
        a = self.anchor(i, along)
        d = Vector((towards[0] - a.x, 0.0, towards[2] - a.z)).normalized()
        heights = []
        for k in range(36):
            p = a + d * (k * 0.4)
            h = self.top(p.x, p.z)
            heights.append(None if h is None else h - a.y)
        return fm.Deck(0.4, heights)


def U_back(v):
    return Vector((-v[0], v[2], -v[1]))


# ---------------------------------------------------------------- one tentacle (TentacleMotion.cs)
class Arm:
    def __init__(self, i, rig):
        self.i, self.rig = i, rig
        self.mode, self.emerged, self.shown, self.strike, self.grip, self.grip_along = "trail", 0.0, None, None, None, None
        self.side = Vector((1, 0, 0))

    def set(self, mode, grip_along=None):
        if mode == "grip" and (mode != self.mode or grip_along != self.grip_along):
            self.grip = None
        self.grip_along = grip_along if mode == "grip" else None
        if self.mode == "trail" and mode != "trail":
            self.emerged = 0.0
        self.mode = mode

    def begin(self, ship, target, kind, t):
        self.strike = dict(start=t, target=target, kind=kind, deck=ship.deck(self.i, target),
                           start_pose=[p.copy() for p in self.shown])

    def at_ship(self, ship, t, dt):
        want = 1.0 if self.mode in ("up", "grip") else 0.0
        step = ((1 / 1.3) if want > self.emerged else 1.0) * dt
        self.emerged = want if abs(want - self.emerged) <= step else self.emerged + math.copysign(step, want - self.emerged)
        across = ship.across(self.i, self.grip_along)
        rest = self._rest(ship, across, t)
        if self.strike:
            s = self.strike
            frame = ship.frame(self.i, s["target"])
            lying = fm.deck_run(frame, s["deck"], fm.LENGTH)
            self.shown = fm.strike(t - s["start"], frame, s["kind"], self.i, s["start_pose"], lying, rest, t)
            self._show(frame.side, dt)
            if t - s["start"] >= fm.end_of(s["kind"]):
                self.strike = None
        else:
            self._ease(rest, dt, 7.0)
            self._show(across.side, dt)

    def trailing(self, frame, t, dt):
        self.strike, self.emerged = None, 1.0
        self._ease(fm.trail(frame, t, self.i * 1.7), dt, 14.0)
        self._show(frame.side, dt)

    def _rest(self, ship, across, t):
        if self.mode == "grip":
            if self.grip is None:
                self.grip = ship.deck(self.i, (0.0, 0.0, ship.anchor(self.i, self.grip_along).z), self.grip_along)
            return fm.emerge(across, self.emerged, fm.deck_run(across, self.grip, 1.3))
        return fm.rising(across, fm.shape_for(fm.IDLE, self.i), self.emerged, t, self.i)

    def _ease(self, pose, dt, rate):
        if self.shown is None:
            self.shown = [p.copy() for p in pose]
            return
        self.shown = fm.straighten(fm.lerp(self.shown, pose, fm.follow(rate, dt)))

    def _show(self, side, dt):
        self.side = self.side.lerp(side, fm.follow(7.0, dt)).normalized()

    def drawn(self):
        return self.strike is not None or self.mode == "trail" or self.emerged > 0.001

    def key(self, frame):
        self.rig.pose(self.shown, self.side, frame)
        self.rig.visible(self.drawn(), frame)


def main():
    stills, limit, standin, ship_name = args()
    fs.reset(FPS)
    ship = Ship(*(fs.stand_in() if standin else fs.load_ship(ship_name)))
    kraken = fs.load_kraken()
    arms = [Arm(i, kraken["tentacles"][i]) for i in range(6)]
    fs.HEAD_ALONG = ship.middle_z + 0.1 * ship.half_length
    director = fs.Director(ship, kraken["head"], arms, fs.players(ship), fs.ink_drops())
    last = director.run(FPS, limit)
    fs.finish(ship, last, OUT if ship_name == "VikingShip" else os.path.join(OUT, ship_name), stills)


if __name__ == "__main__":
    main()
