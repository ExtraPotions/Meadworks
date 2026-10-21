"""The mimic's skeleton, with the game's crypt chest (preview only) and our parts hung on its bones.

Blender axes: Z up, the chest's front faces -Y, the pivot is on the ground under its centre. The chest is 2.02 m wide,
0.87 m deep and 0.65 m tall; its 0.12 m stone lid hinges on the back top edge.

    root        ground centre, lying flat and pointing forward; moves for the lunge (root motion)
      body      hop, lean, squash, tip over
        lid     hinge on the back top edge
          teeth_up, eyes
        teeth_low
        tongue_1 > tongue_2 > tongue_3
"""
import bpy

from workshop import reference, scene

HINGE = (0.0, 0.435, 0.54)
BONES = (
    # name, head, tail, parent
    ("root", (0, 0, 0), (0, -0.25, 0), None),   # flat, pointing at the front: its frame is the rig's
    ("body", (0, 0, 0), (0, 0, 0.4), "root"),
    ("lid", HINGE, (0, 0.435, 0.8), "body"),
    ("teeth_low", (0, 0, 0.543), (0, 0, 0.7), "body"),
    ("teeth_up", (0, 0, 0.53), (0, 0, 0.7), "lid"),
    ("eyes", (0, -0.34, 0.53), (0, -0.34, 0.7), "lid"),
    ("tongue_1", (0, 0.28, 0.565), (0, 0.06, 0.565), "body"),
    ("tongue_2", (0, 0.06, 0.565), (0, -0.15, 0.565), "tongue_1"),
    ("tongue_3", (0, -0.15, 0.565), (0, -0.36, 0.565), "tongue_2"),
)
CHEST = "world/Props/Chests/"


def armature():
    data = bpy.data.armatures.new("mimic_rig")
    obj = bpy.data.objects.new("mimic_rig", data)
    bpy.context.scene.collection.objects.link(obj)
    scene.select_only([obj])
    bpy.ops.object.mode_set(mode='EDIT')
    for name, head, tail, parent in BONES:
        bone = data.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent:
            bone.parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    for pose_bone in obj.pose.bones:
        pose_bone.rotation_mode = 'XYZ'
    data.display_type = 'STICK'
    return obj


def attach(obj, rig, bone):
    """Parents obj rigidly to a bone without moving it."""
    bpy.context.view_layer.update()   # a freshly placed object's matrix_world is stale until evaluated
    world = obj.matrix_world.copy()
    obj.parent, obj.parent_type, obj.parent_bone = rig, 'BONE', bone
    bpy.context.view_layer.update()
    obj.matrix_world = world
    return obj


def chest(rig):
    """The game's own crypt chest from the reference export: for previews only, never exported."""
    stone = reference.material("stonechest", CHEST + "materials/stonechest_d.png", CHEST + "materials/stonechest_n.png")
    attach(reference.mesh(CHEST + "models/stonechest.asset", "chest_base", stone), rig, "body")
    attach(reference.mesh(CHEST + "models/stonechesttop.asset", "chest_lid", stone), rig, "lid")


def skin(obj, rig):
    """Binds a mesh with bone-named vertex groups to the rig."""
    obj.parent = rig
    modifier = obj.modifiers.new("rig", 'ARMATURE')
    modifier.object = rig
    return obj
