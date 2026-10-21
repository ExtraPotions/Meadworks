"""Open directly into the textured idle preview and start playback."""
import bpy
def start():
    if not bpy.context.window_manager.windows:
        return .5
    window = bpy.context.window_manager.windows[0]
    for area in window.screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.shading.use_scene_lights = True
            space.shading.use_scene_world = True
            space.overlay.show_overlays = False
            space.region_3d.view_perspective = 'CAMERA'
            with bpy.context.temp_override(window=window, area=area):
                bpy.ops.screen.animation_play()
            return None
bpy.app.timers.register(start, first_interval=1)
