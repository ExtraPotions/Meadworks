"""The players' bone weapons tour in Blender, from the Unity bake (unity/Assets/Editor/SkelArsenal/ArsenalPlayerBake):

    blender --background --factory-startup --python assets/ecp_skel_arsenal/player_scene.py -- --bake <folder>

The same scene as the skeletons' showcase (blender_scene.py: the game's player, preview only, on its texture, each
player looping its bake), but each weapon's close-up lasts its whole tour (stance, walk, jog, run, stop, crouch, sneak,
block, jump, the fight, the secondary attack), and the timeline is marked with each part ("Sword: run"). Saves
<bake>/player_weapons.blend; blender_play.py opens it playing.
"""
import json
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import blender_scene as bs  # noqa: E402

LINEUP_SECONDS, SPINE_SECONDS = 6.0, 3.0


def main():
    folder = bs._argument('--bake', os.path.join(HERE, 'out', 'player'))
    lineup = json.load(open(os.path.join(folder, 'arsenal.json'), encoding='utf-8'))
    bs.scene.clear()
    fps = lineup['fps']
    slots = _slots(lineup, fps)
    for group in lineup['groups']:
        bs._group(group, folder, slots[group['label']])
    bs._stage()
    bs._drops(lineup['groups'])
    bs._cameras(lineup['groups'], slots)
    bs._timeline(fps, slots)
    bs._views()
    _parts(lineup['groups'], folder, slots)
    _alone(lineup['groups'], slots)
    _save(folder, len(lineup['groups']))


def _slots(lineup, fps):
    """The row, then each player's whole tour once, then the spine."""
    slots, frame = {'Lineup': 1}, 1 + round(LINEUP_SECONDS * fps)
    for group in lineup['groups']:
        slots[group['label']] = frame
        frame += group['frames']
    slots['Spine'] = frame
    slots['end'] = frame + round(SPINE_SECONDS * fps)
    return slots


def _parts(groups, folder, slots):
    """A timeline marker at each part of each player's tour, in its close-up."""
    markers = bpy.context.scene.timeline_markers
    for group in groups:
        data = json.load(open(os.path.join(folder, group['folder'], 'scene.json'), encoding='utf-8'))
        for name, frame in zip(data.get('markerNames', []), data.get('markerFrames', [])):
            if frame > 0:
                markers.new(f"{group['label']}: {name}", frame=slots[group['label']] + frame)


def _alone(groups, slots):
    """Each player shown in the row and in its own close-up only, so a close-up has no neighbours at its edges."""
    for group in groups:
        own = bpy.data.collections[group['label']]
        for obj in [o for o in own.all_objects if o is not None]:
            for frame, hidden in [(1, False)] + [(slots[g['label']], g is not group) for g in groups] + [(slots['Spine'], True)]:
                obj.hide_viewport = obj.hide_render = hidden
                obj.keyframe_insert('hide_viewport', frame=frame)
                obj.keyframe_insert('hide_render', frame=frame)
            for curve in bs._fcurves(obj):
                if curve.data_path.startswith('hide'):
                    for key in curve.keyframe_points:
                        key.interpolation = 'CONSTANT'


def _save(folder, count):
    path = os.path.join(folder, 'player_weapons.blend')
    bpy.ops.file.pack_all()
    for obj in bpy.context.scene.objects:
        for modifier in obj.modifiers:
            if modifier.type == 'MESH_CACHE':
                modifier.filepath = '//' + os.path.relpath(modifier.filepath, folder).replace(os.sep, '/')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"WORKSHOP blend {path}: {count} players, {bpy.context.scene.frame_end} frames")


main()
