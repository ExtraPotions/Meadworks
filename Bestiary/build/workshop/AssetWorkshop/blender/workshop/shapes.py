"""Primitive parts with real sizes in metres. Each returns the new object; scale is applied so bevels stay even."""
import bpy

from . import scene


def box(name, size, location=(0, 0, 0), rotation=(0, 0, 0), material=None, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = _named(name)
    obj.scale = size
    return _finish(obj, material, bevel)


def cylinder(name, radius, depth, location=(0, 0, 0), rotation=(0, 0, 0), material=None, vertices=12, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                        location=location, rotation=rotation)
    return _finish(_named(name), material, bevel)


def sphere(name, radius, location=(0, 0, 0), material=None, segments=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=location)
    return _finish(_named(name), material, 0.0)


def collider_box(name, size, location=(0, 0, 0), rotation=(0, 0, 0)):
    """A box collider; Unity gets a BoxCollider fitted to it."""
    obj = box(scene.COLLIDER_PREFIX + "box_" + name, size, location, rotation)
    obj.display_type = 'WIRE'
    return obj


def collider_mesh(obj, convex=False):
    """Turns an existing object into a mesh collider; convex ones can sit on a Rigidbody (dropped items)."""
    obj.name = scene.COLLIDER_PREFIX + ("convex_" if convex else "mesh_") + obj.name
    obj.data.materials.clear()
    obj.display_type = 'WIRE'
    return obj


def _named(name):
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    return obj


def _finish(obj, material, bevel):
    scene.select_only([obj])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material is not None:
        obj.data.materials.append(material)
    if bevel > 0:
        mod = obj.modifiers.new("bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        mod.limit_method = 'ANGLE'
    return obj
