"""Open the coloured review grid; select a weapon and Numpad . to inspect it."""
import bpy


def start():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active
                space.shading.type='MATERIAL'
                space.shading.use_scene_lights=True
                space.shading.use_scene_world=True
                space.overlay.show_overlays=False
                space.region_3d.view_perspective='CAMERA'
                space.region_3d.view_camera_zoom=0
    return None


bpy.app.timers.register(start,first_interval=1)
