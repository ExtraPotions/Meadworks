"""Opens the greataxe showcase for looking round in Blender's window:

    blender assets/ecp_spine_greataxe/out/showcase/greataxe_showcase.blend --python assets/ecp_spine_greataxe/showcase_open.py

Once the window is up every 3D view shows material preview with the scene's lights and sky, looking through the
main camera. Numpad 0 goes back to it; the other cameras ('camera_head', 'camera_guard') are in the outliner.
"""
import bpy


def _look():
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
    return None


bpy.app.timers.register(_look, first_interval=1.0)
