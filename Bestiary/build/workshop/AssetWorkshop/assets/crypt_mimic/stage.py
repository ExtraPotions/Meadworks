"""The set for the showreel: a crypt floor and back wall, torchlight, cameras and a 1.8 m stand-in player with a sword."""
import math

import bpy
from mathutils import Vector

from workshop import materials, reference, shapes


ROCK = "GameElements/Items/_res/stone/rock_256.png"   # the rock the burial chambers are built from


def build(floor=(14, 14), floor_y=-2.0, torches=()):
    """The set; `torches` adds more torch positions to the two by the wall."""
    _world()
    rock = reference.material("crypt_rock", ROCK, roughness=0.9)
    ground = reference.uv_tiles(shapes.box("floor", (*floor, 0.1), (0, floor_y, -0.05), material=rock), 2.5)
    wall = reference.uv_tiles(shapes.box("wall", (floor[0], 0.4, 5), (0, 2.6, 2.5), material=rock), 2.5)
    _torch("torch_left", (-2.4, -3.2, 2.3), 320)
    _torch("torch_right", (2.8, 0.9, 2.4), 220)
    for i, spot in enumerate(torches):
        _torch(f"torch_{i}", spot, 300)
    _sun()
    return ground, wall


def _world():
    world = bpy.data.worlds.new("crypt")
    bpy.context.scene.world = world
    if world.node_tree is None:
        world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.bl_idname == 'ShaderNodeBackground')
    background.inputs['Color'].default_value = (0.05, 0.075, 0.09, 1.0)
    background.inputs['Strength'].default_value = 1.0


def _torch(name, location, watts):
    light = bpy.data.lights.new(name, 'POINT')
    light.energy = watts
    light.color = (1.0, 0.62, 0.32)
    light.shadow_soft_size = 0.25
    obj = bpy.data.objects.new(name, light)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location


def _sun():
    light = bpy.data.lights.new("moonlight", 'SUN')
    light.energy = 1.4
    light.color = (0.75, 0.85, 1.0)
    obj = bpy.data.objects.new("moonlight", light)
    bpy.context.scene.collection.objects.link(obj)
    obj.rotation_euler = (math.radians(55), 0.0, math.radians(-30))


def camera(name, location, target, lens):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    bpy.context.scene.collection.objects.link(cam)
    cam.data.lens = lens
    cam.location = location
    look = Vector(target) - Vector(location)
    cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
    return cam


def player():
    """An empty carrying a capsule body, a head and a sword on a shoulder pivot. Faces +Y, towards the chest."""
    root = bpy.data.objects.new("player", None)
    bpy.context.scene.collection.objects.link(root)
    cloth = materials.flat("player_cloth", (0.16, 0.2, 0.3))
    parts = [shapes.cylinder("player_body", 0.21, 1.45, (0, 0, 0.725), material=cloth, vertices=16),
             shapes.sphere("player_head", 0.15, (0, 0, 1.63), cloth)]
    shoulder = bpy.data.objects.new("player_shoulder", None)
    bpy.context.scene.collection.objects.link(shoulder)
    shoulder.location = (0.27, 0.0, 1.3)
    steel = materials.flat("player_sword", (0.55, 0.57, 0.6), roughness=0.3, metallic=0.9)
    sword = shapes.box("player_sword", (0.05, 0.02, 0.95), (0.27, 0.0, 1.3 + 0.45), material=steel)
    for part in parts + [shoulder]:
        part.parent = root
    sword.parent = shoulder
    sword.matrix_parent_inverse = shoulder.matrix_world.inverted()
    return root, shoulder


def viewport():
    """The saved file opens looking through the wide camera with lighting on, bones hidden."""
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.shading.type = 'RENDERED'
                space.overlay.show_bones = False
                space.region_3d.view_perspective = 'CAMERA'
