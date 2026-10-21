"""The staged fight, at 30 frames a second: a player walks up and presses E, the mimic bites, wakes, hops after them,
winds up and lunges; the player dodges, hits it three times while it recovers, and it dies.

Timeline markers name each beat, so the same story reads in Blender's timeline and as captions in the video.
"""
import math

import bpy

from clips import AMBUSH, DEATH, HOP_LENGTH, IDLE, LUNGE, LUNGE_DISTANCE, WALK, placed
from poses import AWAKE, DORMANT, key

FPS, END = 30, 310
LUNGE_FROM = 3 * HOP_LENGTH        # where the hops leave it, metres forward
BEATS = (
    (1, "Dormant: just a crypt chest"),
    (40, "The player presses E: ambush bite"),
    (66, "Awake: teeth out, eyes lit"),
    (118, "It hops after the player"),
    (166, "Wind-up: the lid opens wide"),
    (188, "Lunge"),
    (197, "Snap: the bite lands here unless you dodged"),
    (204, "Recovery: the moment to hit it"),
    (250, "Death"),
)


def build(rig, player, shoulder):
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start, scene.frame_end = 1, END
    for frame, text in BEATS:
        scene.timeline_markers.new(text, frame=frame)
    for frame, values in mimic_keys():
        key(rig, frame, values)
    for frame, where, lean, tilt, yaw in player_keys():
        _player_key(player, frame, where, (lean, tilt, yaw))
    for frame, angle in swing_keys():
        shoulder.rotation_euler = (math.radians(-angle), 0.0, 0.0)
        shoulder.keyframe_insert("rotation_euler", frame=frame)


def caption(frame):
    return next(text for start, text in reversed(BEATS) if frame >= start)


def mimic_keys():
    """The clips in story order: sleep, ambush, idle, three hops forward, lunge, death."""
    keys = [(1, DORMANT), (40, DORMANT)] + placed(AMBUSH, 40) + placed(IDLE, 66) + [(118, AWAKE)]
    for hop in range(3):
        keys += placed(WALK, 118 + 16 * hop, forward=HOP_LENGTH * hop, per_frame=HOP_LENGTH / 16)
    return keys + placed(LUNGE, 166, forward=LUNGE_FROM) + placed(DEATH, 250, forward=LUNGE_FROM + LUNGE_DISTANCE)


def player_keys():
    """(frame, (x, y), lean forward, tilt right, turn left; degrees): walk up, press E, get knocked back, retreat,
    dodge right, turn to face the mimic's side and hit it."""
    return [(1, (0, -4.2), 0, 0, 0), (36, (0, -1.35), 0, 0, 0), (40, (0, -1.35), 12, 0, 0),
            (46, (0, -1.35), 0, 0, 0), (50, (0, -1.35), 0, 0, 0), (58, (0, -2.5), -22, 0, 0),
            (66, (0, -2.7), 0, 0, 0), (110, (0, -4.4), 0, 0, 0), (180, (0, -4.4), 0, 0, 0),
            (186, (1.7, -4.4), 0, 25, 0), (192, (1.7, -4.4), 0, 0, 0), (202, (1.45, -3.5), 8, 0, 90),
            (244, (1.45, -3.5), 8, 0, 90), (262, (1.8, -3.6), 0, 0, 90), (END, (1.8, -3.6), 0, 0, 90)]


def swing_keys():
    """Shoulder angle: raised behind (-70) to struck down in front (+60), three hits in the recovery."""
    keys = [(1, 0), (200, -70)]
    for hit in (212, 224, 236):
        keys += [(hit - 4, -70), (hit, 60), (hit + 4, -70)]
    return keys + [(250, 0)]


def _player_key(player, frame, where, angles):
    """Facing +Y, a forward lean is a negative X rotation and a tilt to the right a positive Y rotation."""
    lean, tilt, yaw = angles
    player.location = (where[0], where[1], 0.0)
    player.rotation_euler = (math.radians(-lean), math.radians(tilt), math.radians(yaw))
    player.keyframe_insert("location", frame=frame)
    player.keyframe_insert("rotation_euler", frame=frame)
