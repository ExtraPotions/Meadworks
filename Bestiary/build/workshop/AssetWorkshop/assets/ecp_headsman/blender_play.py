"""Opens the baked headsman scene playing, for looking at it in Blender's window:

    blender assets/ecp_headsman/out/blender/headsman_moves.blend --python assets/ecp_headsman/blender_play.py

Once the window is up, every 3D view looks through the scene camera in material preview, fitted so the whole camera
frame shows (the boss, the targets and the ground round them), and the timeline plays from the start on a loop
(Space stops it). The markers name each attack and its moments; the camera cuts at each attack.
"""
import bpy


def _play():
    window = bpy.context.window_manager.windows[0] if bpy.context.window_manager.windows else None
    if window is None:
        return 0.5
    for area in window.screen.areas:
        if area.type != 'VIEW_3D':
            continue
        space = area.spaces.active
        space.shading.type = 'MATERIAL'
        space.region_3d.view_perspective = 'CAMERA'
        region = next(r for r in area.regions if r.type == 'WINDOW')
        bpy.context.scene.frame_set(1)
        with bpy.context.temp_override(window=window, area=area, region=region):
            bpy.ops.view3d.view_center_camera()
            bpy.ops.screen.animation_play()
        return None
    return 0.5


bpy.app.timers.register(_play, first_interval=1.5)
