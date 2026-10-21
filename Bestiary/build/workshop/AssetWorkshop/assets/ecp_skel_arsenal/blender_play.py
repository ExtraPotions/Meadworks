"""Opens the skeleton arsenal showcase playing, for watching it in Blender's window:

    blender assets/ecp_skel_arsenal/out/blender/skeleton_arsenal.blend --python assets/ecp_skel_arsenal/blender_play.py

Once the window is up, the 3D view looks through the scene camera in material preview and the timeline plays on a
loop (Space stops it). The markers switch cameras: the whole row, then each weapon's skeleton close up (Dagger, Sword,
Axe, Mace, Spear, Atgeir, Bow), then the dropped vertebra. Every skeleton repeats its attack all the while; the
cameras are cam_* in the outliner (select one and press Ctrl+Numpad 0 to look through it).
"""
import bpy
import json
from pathlib import Path


def _play():
    window = bpy.context.window_manager.windows[0] if bpy.context.window_manager.windows else None
    if window is None:
        return 0.5
    for area in window.screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.shading.use_scene_lights = True
            space.shading.use_scene_world = True
            space.overlay.show_extras = False
            space.region_3d.view_perspective = 'CAMERA'
            with bpy.context.temp_override(window=window, area=area):
                if not window.screen.is_animation_playing:
                    bpy.ops.screen.animation_play()
            bpy.app.timers.register(_status, first_interval=3.0)
            return None
    return 0.5


def _status():
    Path(bpy.data.filepath).with_suffix('.playback.json').write_text(json.dumps({
        'file': bpy.data.filepath,
        'frame': bpy.context.scene.frame_current,
        'playing': any(w.screen.is_animation_playing for w in bpy.context.window_manager.windows),
    }, indent=2), encoding='utf-8')


bpy.app.timers.register(_play, first_interval=1.5)
