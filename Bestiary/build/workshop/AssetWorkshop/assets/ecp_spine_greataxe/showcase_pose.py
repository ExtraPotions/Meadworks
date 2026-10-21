"""Posing the game's Skeleton (showcase_rig) round the axe: world-space bone turns, hands closed on the haft through IK
and a copied rotation (their targets ride the axe, so moving the axe moves the hands), feet planted through IK.

A hand on the haft: the palm against the grip on the side facing the shoulder, the thumb along the haft towards
the head, the fingers wrapped round the far side. Elbow poles go behind and out, knee poles in front; each IK's pole
angle is the one that puts the joint nearest its pole.
"""
import math

import bpy
from mathutils import Matrix, Vector

FINGERS = ("Index", "Middle", "Ring", "Pinky")
CURL = (62, 72, 48)                           # degrees at the three joints of each finger
THUMB_CURL = (25, 35, 30)


def turn(rig, bone, axis, degrees):
    """Turns a bone (and so everything below it) about its head by `degrees` round a world axis."""
    pb = rig.pose.bones[bone]
    bpy.context.view_layer.update()
    m = pb.matrix.copy()
    head = m.translation.copy()
    pb.matrix = Matrix.Translation(head) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Translation(-head) @ m
    bpy.context.view_layer.update()


def shift(rig, bone, offset):
    pb = rig.pose.bones[bone]
    bpy.context.view_layer.update()
    pb.matrix = Matrix.Translation(Vector(offset)) @ pb.matrix
    bpy.context.view_layer.update()


def hold(rig, side, axe, grip, axis, radius, thumb=1):
    """`side` hand ('Left'/'Right') closes on the axe at `grip` (axe-local point) round the local haft `axis`."""
    g = axe.matrix_world @ Vector(grip)
    a = (axe.matrix_world.to_3x3() @ Vector(axis)).normalized() * thumb
    shoulder = rig.matrix_world @ rig.pose.bones[side + "Arm"].head
    n = (shoulder - g) - a * (shoulder - g).dot(a)
    n.normalize()
    rotation, finger = _hand_rotation(rig, side, -n, a)
    wrist = g + n * (radius + 0.018) - finger * 0.095
    target = _empty(f"{rig.name}_{side}_hand", Matrix.Translation(wrist) @ rotation.to_4x4(), axe)
    pole = _empty(f"{rig.name}_{side}_elbow", Matrix.Translation(shoulder + _outward(side) * 0.45 + Vector((0, 0.45, -0.45))), axe)
    _ik(rig, side + "ForeArm", target, pole)
    rig.pose.bones[side + "Hand"].constraints.new('COPY_ROTATION').target = target
    curl(rig, side)


def plant(rig, side, where, yaw=0.0):
    """`side` foot flat on the ground at world (x, y), turned `yaw` degrees."""
    bone = rig.data.bones[side + "Foot"]
    rest = rig.matrix_world @ bone.matrix_local
    at = Vector((where[0], where[1], rest.translation.z))
    turned = Matrix.Rotation(math.radians(yaw), 4, 'Z') @ rest.to_3x3().to_4x4()
    target = _empty(f"{rig.name}_{side}_foot", Matrix.Translation(at) @ turned, None)
    pole = _empty(f"{rig.name}_{side}_knee", Matrix.Translation(at + Vector((0, -0.8, 0.55))), None)
    _ik(rig, side + "Leg", target, pole)
    rig.pose.bones[side + "Foot"].constraints.new('COPY_ROTATION').target = target


def curl(rig, side, scale=1.0):
    """Fingers closed as round a haft, the thumb over them."""
    palm = _palm(rig, side)
    for finger in FINGERS:
        for joint, degrees in zip((1, 2, 3), CURL):
            _bend(rig, f"{side}Hand{finger}{joint}", palm, degrees * scale)
    for joint, degrees in zip((1, 2, 3), THUMB_CURL):
        _bend(rig, f"{side}HandThumb{joint}", palm, degrees * scale)


def settle_poles(rig):
    """For every IK on the rig, the pole angle that brings the bent joint nearest its pole."""
    for pb in rig.pose.bones:
        for c in pb.constraints:
            if c.type == 'IK' and c.pole_target is not None:
                c.pole_angle = min((math.radians(a) for a in (-90, 90, 0, 180)), key=lambda a: _pole_miss(rig, pb, c, a))
    bpy.context.view_layer.update()


def _pole_miss(rig, pb, c, angle):
    c.pole_angle = angle
    bpy.context.view_layer.update()
    joint = rig.matrix_world @ pb.head
    return (joint - c.pole_target.matrix_world.translation).length


def _hand_rotation(rig, side, palm_to, thumb_to):
    """World rotation of the hand bone with its palm facing `palm_to` and thumb along `thumb_to`; and its finger
    direction there."""
    rest = (rig.matrix_world @ rig.data.bones[side + "Hand"].matrix_local).to_3x3()
    finger_r, palm_r, thumb_r, sign = _hand_frame(rig, side)
    palm_t = palm_to.normalized()
    thumb_t = (thumb_to - palm_t * thumb_to.dot(palm_t)).normalized()
    finger_t = (palm_t.cross(thumb_t) * sign).normalized()
    turn = Matrix((finger_t, palm_t, thumb_t)).transposed() @ Matrix((finger_r, palm_r, thumb_r))
    return turn @ rest, finger_t


def _hand_frame(rig, side):
    """In the rest pose: finger direction, palm normal (down), thumb direction, and the frame's handedness."""
    b = rig.data.bones
    hand = b[side + "Hand"].head_local
    finger = (b[side + "HandMiddle1"].head_local - hand).normalized()
    across = (b[side + "HandIndex1"].head_local - hand).cross(b[side + "HandPinky1"].head_local - hand)
    palm = (-across if across.z > 0 else across).normalized()
    palm = (palm - finger * palm.dot(finger)).normalized()
    thumb_raw = finger.cross(palm)
    sign = 1.0 if (b[side + "HandThumb1"].head_local - hand).dot(thumb_raw) > 0 else -1.0
    return finger, palm, (thumb_raw * sign).normalized(), sign


def _palm(rig, side):
    return _hand_frame(rig, side)[1]


def _bend(rig, name, palm, degrees):
    """Curls a finger bone towards the palm, in its own rest frame."""
    if name not in rig.pose.bones:
        return
    bone = rig.data.bones[name]
    along = (bone.tail_local - bone.head_local).normalized()
    axis = along.cross(palm).normalized()
    local = bone.matrix_local.to_3x3().inverted() @ axis
    rig.pose.bones[name].rotation_quaternion = Matrix.Rotation(math.radians(degrees), 3, local).to_quaternion()


def _ik(rig, bone, target, pole):
    c = rig.pose.bones[bone].constraints.new('IK')
    c.target, c.pole_target, c.chain_count, c.use_tail = target, pole, 2, True


def _outward(side):
    return Vector((1, 0, 0)) if side == "Left" else Vector((-1, 0, 0))


def _empty(name, matrix, parent):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = 'ARROWS'
    obj.empty_display_size = 0.06
    bpy.context.collection.objects.link(obj)
    obj.matrix_world = matrix
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj
