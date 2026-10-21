"""The mimic's animation clips, each starting at frame 0 (30 frames a second). The game plays these; the showreel
strings the same clips together, so what you watch in Blender is what the game gets.

Walking is in place: the game moves the creature and plays the hop. The lunge carries its own forward movement on the
root bone, which the game applies as root motion. Attack clips name the frame where the bite lands.
"""
from poses import AWAKE, DORMANT, pose

SLEEP = [(0, DORMANT), (30, DORMANT)]

AMBUSH = [(0, DORMANT),
          (3, pose(lid=55, lean=-8, lift=0.10, curl=12, stick=0.1)),
          (7, pose(lid=62, lean=-10, lift=0.14, curl=18, stick=0.12)),
          (10, pose(lid=0, lean=7, lift=0.02)),
          (13, pose(lid=9, lean=2)),
          (18, pose(lid=4)),
          (26, AWAKE)]

IDLE = [(0, AWAKE), (10, pose(lid=8, squash=1.02)), (20, pose(lid=4, squash=0.99)),
        (30, pose(lid=8, squash=1.02)), (40, AWAKE)]

WALK = [(0, pose(squash=0.92, lid=3)),
        (5, pose(lift=0.16, lean=8, lid=14, squash=1.04)),
        (11, pose(lift=0.03, lean=-2, lid=6)),
        (13, pose(squash=0.9, lid=2)),
        (16, pose(squash=0.92, lid=3))]
HOP_LENGTH = 0.35                  # metres a hop covers when the showreel moves it

# A bound: the run. Longer and higher than the hop, lid flapping; the game blends hop into bound as it speeds up.
RUN = [(0, pose(squash=0.88, lid=3, lean=-4)),
       (3, pose(lift=0.1, lean=10, lid=18, squash=1.06)),
       (6, pose(lift=0.32, lean=6, lid=22, squash=1.02)),
       (9, pose(lift=0.1, lean=-6, lid=10)),
       (11, pose(squash=0.86, lid=2, lean=-4)),
       (12, pose(squash=0.88, lid=3, lean=-4))]

LUNGE_DISTANCE = 2.55
_SLACK = dict(forward=LUNGE_DISTANCE, lean=7, stick=0.4, droop=60)
LUNGE = [(0, AWAKE),
         (10, pose(forward=-0.08, lean=-14, lid=75, lift=0.02, curl=14, stick=0.1)),
         (16, pose(forward=-0.08, lean=-14, lid=76, lift=0.02, curl=22, stick=0.12, roll=2)),
         (19, pose(forward=-0.08, lean=-14, lid=76, lift=0.02, curl=4, stick=0.1, roll=-2)),
         (22, pose(forward=0.0, lean=-6, lid=78, lift=0.05, curl=10)),
         (28, pose(forward=1.5, lift=0.32, lean=10, lid=82, curl=6)),
         (31, pose(forward=LUNGE_DISTANCE - 0.1, lift=0.02, lean=6, lid=0)),
         (33, pose(forward=LUNGE_DISTANCE, lid=6, lean=2, squash=0.94)),
         (40, pose(lid=38, squash=0.95, **_SLACK)),
         (48, pose(lid=30, squash=0.97, **_SLACK)),
         (56, pose(lid=40, squash=0.94, **_SLACK)),
         (64, pose(lid=30, squash=0.97, **_SLACK)),
         (72, pose(lid=36, squash=0.95, **_SLACK)),
         (84, pose(forward=LUNGE_DISTANCE))]

STAGGER = [(0, AWAKE), (3, pose(roll=6, lid=12, lift=0.04)), (7, pose(roll=-5, lid=3)),
           (11, pose(roll=2, lid=8)), (15, AWAKE)]

_DEAD = dict(lean=4, stick=0.42, droop=75)
DEATH = [(0, AWAKE),
         (8, pose(lid=100, roll=14, eyes=0.8, **_DEAD)),
         (16, pose(lid=108, roll=16, squash=0.97, eyes=0.6, **_DEAD)),
         (30, pose(lid=106, roll=16, squash=0.97, eyes=0.0, teeth=0.7, **_DEAD)),
         (60, pose(lid=106, roll=16, squash=0.97, eyes=0.0, teeth=0.7, **_DEAD))]

# name: (keys, loops, the game's state tag, frames where the bite lands)
CLIPS = {
    "sleep": (SLEEP, True, "", ()),
    "ambush": (AMBUSH, False, "attack", (10,)),
    "idle": (IDLE, True, "", ()),
    "walk": (WALK, True, "", ()),
    "run": (RUN, True, "", ()),
    "lunge": (LUNGE, False, "attack", (31,)),
    "stagger": (STAGGER, False, "stagger", ()),
    "death": (DEATH, False, "", ()),
}


def placed(keys, start, forward=0.0, per_frame=0.0):
    """A clip's keys moved to `start`, carried `forward` metres plus `per_frame` metres each frame."""
    return [(start + f, dict(v, forward=v["forward"] + forward + per_frame * f)) for f, v in keys]
