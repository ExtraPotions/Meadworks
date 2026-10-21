"""Opens the baked crossbowman scene playing, for looking at it in Blender's window:

    blender assets/ecp_crossbowman/out/blender/crossbowman_animations.blend --python assets/ecp_crossbowman/blender_play.py

Once the window is up, every 3D view looks through the camera in material preview and the timeline plays on a loop
(Space stops it). The timeline's markers name each part: carry (idle), raise and aim, fire, lower, span the string,
bolt from the quiver, load the bolt, carry again, walk, run.
"""
import bpy


def _play():
    window = bpy.context.window_manager.windows[0] if bpy.context.window_manager.windows else None
    if window is None:
        return 0.5
    for area in window.screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.region_3d.view_perspective = 'CAMERA'
            with bpy.context.temp_override(window=window, area=area):
                bpy.ops.screen.animation_play()
            return None
    return 0.5


bpy.app.timers.register(_play, first_interval=1.5)
