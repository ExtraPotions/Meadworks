"""A second crossbowman beside the first, doing the same animation with another crossbow, for comparing the two models.

Used by blender_scene.py --compare. The first skeleton wears ours as baked. The second is the same
baked skeleton and quiver moved OFFSET to its left, holding another crossbow where ours is, frame by frame (from the
bake's tracks.json: our crossbow's and bolts' world matrices), its grip moved onto ours (GRIP). That crossbow comes as
two models, string drawn and string let go; the loaded one shows except from the shot until the string is back in the
nut. Its own bolt stands wherever ours does: in the groove (on its own rails), in the fingers, in the quiver (raised,
as it is longer) and in flight. The default is the "Gravebranch" set in assets/bone_crossbow_set (ecp_bone_crossbow,
ecp_bone_crossbow_unloaded, ecp_bone_quarrel). A label floats over each skeleton.
"""
import json
import math
import os

import bpy
from mathutils import Matrix, Vector

from workshop import materials

ASSETS = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
OFFSET = Vector((1.7, 0.0, 0.0))            # the second skeleton, to the first one's left
FIRE, SPANNED = 0.32, 2.12                  # AssetWorkshop Crossbow/XbowClips: the string let go, and back in the nut

# The Gravebranch crossbow in our crossbow's frame: its wrapped grip (y 0.025 to 0.17 behind its origin) onto ours,
# its quarrel's nock (y +0.02) on a slot's origin, and its groove: the nock at its nut (y -0.055), on its rails (z 0.05).
GRIP = Matrix.Translation((0.0, -0.10, 0.025))
NOCK = Matrix.Translation((0.0, -0.02, 0.0))
GROOVE = Matrix.Translation((0.0, -0.075, 0.05))
QUIVER_RAISE = Matrix.Translation((0.0, 0.08, 0.0))   # it is 8 cm longer than ours: raise it so it stays in the case


def build(folder):
    tracks = json.load(open(os.path.join(folder, 'tracks.json'), encoding='utf-8'))
    track = {t['name']: t for t in tracks['tracks']}
    frames = len(tracks['fireTimes'])
    twin = bpy.data.objects.new('second skeleton', None)
    twin.location = OFFSET
    bpy.context.collection.objects.link(twin)
    for obj in [o for o in bpy.context.scene.objects if _body(o.name)]:
        copy = obj.copy()
        copy.name = obj.name + ' (second)'
        copy.parent = twin
        bpy.context.collection.objects.link(copy)
    loaded = [not (FIRE <= t < SPANNED) for t in tracks['fireTimes']]
    _follow(_append('ecp_bone_crossbow'), track['ecp_xbow_crossbow'], GRIP, loaded, frames)
    _follow(_append('ecp_bone_crossbow_unloaded'), track['ecp_xbow_crossbow'], GRIP, [not s for s in loaded], frames)
    _follow(_append('ecp_bone_quarrel'), track['ecp_xbow_crossbow'], GRIP @ GROOVE, track['ecp_xbow_bolt_groove']['active'], frames)
    for name in [n for n in track if n.startswith('ecp_xbow_bolt_quiver_')]:
        _follow(_append('ecp_bone_quarrel'), track[name], QUIVER_RAISE @ NOCK, track[name]['active'], frames)
    for name in ('ecp_xbow_bolt_hand', 'flying_bolt'):
        _follow(_append('ecp_bone_quarrel'), track[name], NOCK, track[name]['active'], frames)
    _label('ours', Vector((0.0, 0.0, 2.3)))
    _label('Gravebranch', OFFSET + Vector((0.0, 0.0, 2.3)))


def camera():
    """Both skeletons in view, from the front and a little to their right."""
    cam = bpy.context.scene.camera
    target = OFFSET / 2 + Vector((0.0, -0.3, 1.05))
    cam.location = target + Vector((-2.4, -5.6, 0.6))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = 36


def _body(name):
    return 'Skeleton' in name or 'eye_' in name or 'ecp_xbow_quiver' in name


_appended = {}


def _append(asset):
    """A copy of the asset's joined model from its own workshop .blend (appended once), with its baked texture."""
    if asset not in _appended:
        path = os.path.join(ASSETS, asset, 'out', asset + '.blend')
        with bpy.data.libraries.load(path) as (source, target):
            target.objects = [asset]
        _appended[asset] = target.objects[0]
    obj = _appended[asset].copy()
    bpy.context.collection.objects.link(obj)
    obj.rotation_mode = 'QUATERNION'
    return obj


def _follow(obj, track, local, shown, frames):
    """Keys the object onto the tracked transform (moved to the second skeleton), shown only when `shown` says."""
    m = track['matrices']
    for f in range(frames):
        world = Matrix([m[16 * f + 4 * r:16 * f + 4 * r + 4] for r in range(4)])
        obj.matrix_world = Matrix.Translation(OFFSET) @ world @ local
        for path in ('location', 'rotation_quaternion', 'scale'):
            obj.keyframe_insert(path, frame=f + 1)
        obj.hide_viewport = obj.hide_render = not shown[f]
        obj.keyframe_insert('hide_viewport', frame=f + 1)
        obj.keyframe_insert('hide_render', frame=f + 1)


def _label(text, at):
    curve = bpy.data.curves.new('label', 'FONT')
    curve.body = text
    curve.align_x = 'CENTER'
    curve.size = 0.16
    obj = bpy.data.objects.new('label: ' + text, curve)
    obj.location = at
    obj.rotation_euler = (math.radians(90), 0.0, math.radians(-20))
    obj.visible_shadow = False
    mat = materials.flat('label', (0.95, 0.92, 0.8))
    materials.principled(mat).inputs['Emission Color'].default_value = (0.95, 0.92, 0.8, 1.0)
    materials.principled(mat).inputs['Emission Strength'].default_value = 1.5
    curve.materials.append(mat)
    bpy.context.collection.objects.link(obj)
