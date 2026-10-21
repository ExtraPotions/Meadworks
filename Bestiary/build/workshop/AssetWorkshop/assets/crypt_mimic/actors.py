"""Bookkeeping for the combat sequence: where the mimic and the player stand and face, the keys that put them there, and
the HUD timeline (captions, health, damage numbers, the bite cooldown) that the video overlay draws.

Blender axes: Z up. The mimic's heading 0 faces -Y; the player's yaw 0 faces +Y.
"""
import math

from clips import CLIPS, WALK
from poses import AWAKE, DORMANT, world_position

FPS = 30
CLIP_NAMES = {id(keys): name for name, (keys, *_rest) in CLIPS.items()}


def heading_to(origin, target):
    """The mimic heading (degrees) that faces target from origin."""
    return math.degrees(math.atan2(target[0] - origin[0], -(target[1] - origin[1])))


class Mimic:
    def __init__(self):
        self.x, self.y, self.heading = 0.0, 0.0, 0.0
        self.keys = []
        self.cues = []      # (frame, clip name): what an animator has to be told, and when

    @property
    def pos(self):
        return (self.x, self.y)

    def facing(self):
        h = math.radians(self.heading)
        return (math.sin(h), -math.cos(h))

    def ahead(self, distance, right=0.0):
        """A point `distance` in front of it and `right` metres to its right (its own right: facing -Y, that is -X)."""
        fx, fy = self.facing()
        return (self.x + fx * distance + fy * right, self.y + fy * distance - fx * right)

    def play(self, clip, start, per_frame=0.0):
        """Places a clip at its current spot and heading; afterwards it stands where the clip left it."""
        self.cues.append((start, CLIP_NAMES[id(clip)]))
        for frame, values in clip:
            forward = values["forward"] + per_frame * frame
            self.keys.append((start + frame, dict(values, x=self.x, y=self.y, heading=self.heading, forward=forward)))
        last_frame, last = self.keys[-1]
        self.x, self.y = world_position(last)
        return start + clip[-1][0]

    def hop(self, start, hops):
        for _ in range(hops):
            start = self.play(WALK, start, per_frame=0.35 / 16)
        return start

    def turn_hop(self, start, target):
        """One hop on the spot, turning to face target."""
        begin, end = self.heading, heading_to(self.pos, target)
        end = begin + (end - begin + 180) % 360 - 180
        self.cues.append((start, "walk"))
        for frame, values in WALK:
            heading = begin + (end - begin) * frame / WALK[-1][0]
            self.keys.append((start + frame, dict(values, x=self.x, y=self.y, heading=heading)))
        self.heading = end
        return start + WALK[-1][0]

    def hold(self, start, end, values=AWAKE):
        self.cues.append((start, "sleep" if values is DORMANT else "idle"))
        self.keys += [(start, dict(values, x=self.x, y=self.y, heading=self.heading)),
                      (end, dict(values, x=self.x, y=self.y, heading=self.heading))]
        return end


class Player:
    """A stand-in: position, yaw, lean forward, tilt right, crouch (1 standing) and the sword's swing angle."""

    def __init__(self, start, facing_point):
        self.x, self.y = start
        self.yaw = self._yaw_to(facing_point)
        self.stance = (0.0, 0.0, 1.0)      # lean, tilt, crouch as last keyed
        self.keys, self.sword = [], [(1, 0.0)]

    @property
    def pos(self):
        return (self.x, self.y)

    def _yaw_to(self, point):
        return math.degrees(math.atan2(-(point[0] - self.x), point[1] - self.y))

    def key(self, frame, lean=0.0, tilt=0.0, crouch=1.0):
        self.stance = (lean, tilt, crouch)
        self.keys.append((frame, (self.x, self.y), self.yaw, lean, tilt, crouch))

    def move(self, start, end, to, face=None, lean=0.0, tilt=0.0, crouch=1.0):
        """Holds its stance until `start`, then moves to `to` (turning to `face`) with the new stance by `end`."""
        self.key(start, *self.stance)
        self.x, self.y = to
        if face is not None:
            self.yaw = self._yaw_to(face)
        self.key(end, lean, tilt, crouch)
        return end

    def swing(self, strike, heavy=False):
        """Raise, strike on `strike`, recover. Returns the strike frame."""
        back = -110.0 if heavy else -70.0
        self.sword += [(strike - (10 if heavy else 4), back), (strike, 60.0), (strike + 5, back * 0.3), (strike + 10, 0.0)]
        return strike


class Hud:
    """Everything the overlay draws, by frame."""

    def __init__(self, mimic_hp, player_hp):
        self.mimic_hp, self.player_hp = [(0, mimic_hp, mimic_hp)], [(0, player_hp, player_hp)]
        self.captions, self.popups, self.flashes, self.cooldowns = [], [], [], []
        self.prompt, self.reveal, self.death = None, None, None

    def caption(self, frame, text):
        self.captions.append((frame, text))

    def hit_mimic(self, frame, damage):
        value = max(self.mimic_hp[-1][1] - damage, 0)
        self.mimic_hp.append((frame, value, self.mimic_hp[0][2]))
        self.popups.append({"frame": frame, "target": "mimic", "text": f"-{damage}", "color": "white"})

    def hit_player(self, frame, damage):
        value = max(self.player_hp[-1][1] - damage, 0)
        self.player_hp.append((frame, value, self.player_hp[0][2]))
        self.popups.append({"frame": frame, "target": "player", "text": f"-{damage}", "color": "red"})

    def flash(self, frame, text, color):
        self.flashes.append({"frame": frame, "text": text, "color": color})

    def cooldown(self, start, seconds):
        self.cooldowns.append({"start": start, "ready": start + int(seconds * FPS)})
        return start + int(seconds * FPS)

    def data(self):
        return {"fps": FPS, "mimicHp": self.mimic_hp, "playerHp": self.player_hp, "captions": self.captions,
                "popups": self.popups, "flashes": self.flashes, "cooldowns": self.cooldowns,
                "prompt": self.prompt, "reveal": self.reveal, "death": self.death}
