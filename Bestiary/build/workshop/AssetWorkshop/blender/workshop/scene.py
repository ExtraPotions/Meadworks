"""Scene housekeeping: an empty scene, selection, and joining the parts into one mesh."""
import bpy

COLLIDER_PREFIX = "col_"


def clear():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            block.remove(item)
    units = bpy.context.scene.unit_settings
    units.system = 'METRIC'
    units.scale_length = 1.0


def meshes():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def is_collider(obj):
    return obj.name.startswith(COLLIDER_PREFIX)


def select_only(objs, active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def bake_down(objs):
    """Applies modifiers and transforms, so mesh data is in world space and object coordinates equal world ones."""
    select_only(objs)
    bpy.ops.object.make_single_user(object=True, obdata=True)
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def join(objs, name):
    bake_down(objs)
    select_only(objs)
    if len(objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    return obj


def bounds(objs):
    """World-space (min, max) corners over the objects' bounding boxes."""
    from mathutils import Vector
    corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return low, high


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)
