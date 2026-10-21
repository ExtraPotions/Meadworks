"""Named pose controls for the mimic rig. A pose is a dict of these values; key() writes a complete pose, every bone and
every channel, so clips never bleed into one another.

    lid      degrees open (0 closed)          teeth, eyes, tongue   0 hidden .. 1 out
    lean     degrees, + tips the front down   curl     tongue tip up (degrees), droop hangs it down
    lift     metres up                        stick    metres the tongue slides out of the mouth
    squash   height scale (1 normal)          roll     degrees tipped onto its right side
    forward  metres travelled forward (root)  side     metres sideways (root)
    x, y     where it stands in the world     heading  degrees turned left (0 faces -Y)

forward and side are measured along its heading from (x, y); the clips leave x, y and heading at 0, so the game gets
them as root motion relative to wherever the creature is.

Bones pointing up have local X = world X, local Y = world Z, local Z = world -Y; tongue bones point forward, so their
local X is world -X (a positive X rotation lifts the tip).
"""
import math

from mathutils import Vector

DORMANT = dict(lid=0.0, teeth=0.0, eyes=0.0, tongue=0.0, curl=0.0, droop=0.0, stick=0.0,
               lean=0.0, lift=0.0, squash=1.0, roll=0.0, forward=0.0, side=0.0, x=0.0, y=0.0, heading=0.0)
AWAKE = dict(DORMANT, lid=5.0, teeth=1.0, eyes=1.0, tongue=1.0)
HALF_DEPTH, HALF_WIDTH = 0.43, 0.95
TINY = 0.001


def pose(base=AWAKE, **changes):
    return dict(base, **changes)


def key(rig, frame, values):
    bones = rig.pose.bones
    _root(bones["root"], values)
    _body(bones["body"], values)
    bones["lid"].rotation_euler = (-math.radians(values["lid"]), 0.0, 0.0)
    for name in ("teeth_low", "teeth_up"):
        bones[name].scale = (1.0, max(values["teeth"], TINY), 1.0)
    bones["eyes"].scale = (max(values["eyes"], TINY),) * 3
    _tongue(bones, values)
    for bone in bones:
        bone.keyframe_insert("location", frame=frame)
        bone.keyframe_insert("rotation_euler", frame=frame)
        bone.keyframe_insert("scale", frame=frame)


def world_position(values):
    """Where the root stands in Blender's XY plane: (x, y) plus forward and side turned by the heading."""
    h = math.radians(values["heading"])
    side, back = values["side"], -values["forward"]
    return (values["x"] + side * math.cos(h) - back * math.sin(h),
            values["y"] + side * math.sin(h) + back * math.cos(h))


def _root(bone, values):
    """
    The root bone lies flat along the rig's forward axis, so its local Z is the world's up and turning is about it;
    where it stands is written through its rest frame, so the keys mean the same before and after the export's turn.
    """
    x, y = world_position(values)
    bone.location = bone.bone.matrix_local.to_3x3().inverted() @ Vector((x, y, 0.0))
    bone.rotation_euler = (0.0, 0.0, math.radians(values["heading"]))


def _body(bone, values):
    """Lean and roll pivot on the ground centre, so the body is lifted by however far a corner would sink."""
    lean, roll = math.radians(values["lean"]), math.radians(values["roll"])
    sink = HALF_DEPTH * abs(math.sin(lean)) + HALF_WIDTH * abs(math.sin(roll))
    bone.location = (0.0, values["lift"] + sink, 0.0)
    bone.rotation_euler = (lean, 0.0, -roll)
    wide = 1.0 / math.sqrt(values["squash"])
    bone.scale = (wide, values["squash"], wide)


def _tongue(bones, values):
    curl, droop = math.radians(values["curl"]), math.radians(values["droop"])
    first = bones["tongue_1"]
    first.scale = (max(values["tongue"], TINY),) * 3
    first.location = (0.0, values["stick"], 0.0)
    first.rotation_euler = (curl * 0.3 - droop * 0.05, 0.0, 0.0)
    bones["tongue_2"].rotation_euler = (curl * 0.6 - droop * 0.5, 0.0, 0.0)
    bones["tongue_3"].rotation_euler = (curl * 1.0 - droop * 1.0, 0.0, 0.0)
