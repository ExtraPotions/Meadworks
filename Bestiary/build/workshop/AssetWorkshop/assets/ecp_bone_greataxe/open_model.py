"""Show the packed, coloured axe in a framed material-preview viewport."""
import bpy
from mathutils import Quaternion
import math


def configure():
    axe = bpy.data.objects.get('ecp_bone_greataxe')
    if axe is None:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    axe.select_set(True)
    bpy.context.view_layer.objects.active = axe
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != 'VIEW_3D':
                continue
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.shading.use_scene_world = False
            space.shading.use_scene_lights = False
            space.overlay.show_overlays = False
            space.region_3d.view_rotation = Quaternion((1, 0, 0), math.pi / 2)
            space.region_3d.view_perspective = 'ORTHO'
            space.region_3d.view_location = (.20, 0, .75)
            space.region_3d.view_distance = 2.5
    return None


if __name__ == '__main__':
    bpy.app.timers.register(configure, first_interval=1)
