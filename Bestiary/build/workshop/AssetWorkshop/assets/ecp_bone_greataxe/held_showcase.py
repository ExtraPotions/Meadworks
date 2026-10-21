"""Finished axe held in both hands by the original, skinned Valheim Skeleton."""
import math
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'blender'))
sys.path.insert(0, os.path.join(HERE, '..', 'ecp_spine_greataxe'))
import bpy
from mathutils import Matrix, Vector
import showcase_rig
import showcase_pose as pose
from workshop import materials, scene
sys.path.insert(0, HERE)
from model import spine_point, spine_axis

OUT = os.path.join(HERE, 'out')
scene.clear()
with bpy.data.libraries.load(os.path.join(OUT, 'ecp_bone_greataxe.blend'), link=False) as (source, dest):
    dest.objects = ['ecp_bone_greataxe']
axe = dest.objects[0]
bpy.context.collection.objects.link(axe)
rig, body = showcase_rig.skeleton('Original_Valheim_Skeleton')
pose.shift(rig, 'Hips', (0, 0, -.075))
pose.turn(rig, 'Spine', (1, 0, 0), 3)
pose.turn(rig, 'Spine1', (0, 0, 1), -5)
pose.turn(rig, 'Head', (0, 0, 1), 5)
lower = spine_point(.34)
upper = spine_point(.73)
turn = Matrix.Rotation(math.radians(-46), 4, 'Y') @ Matrix.Rotation(math.pi, 4, 'Z')
axe.matrix_world = Matrix.Translation(Vector((.17, -.29, 1.06)) - turn @ lower) @ turn
bpy.context.view_layer.update()
pose.hold(rig, 'Left', axe, lower, spine_axis(.34), .026)
pose.hold(rig, 'Right', axe, upper, spine_axis(.73), .028)
pose.plant(rig, 'Left', (.23, -.16), -12)
pose.plant(rig, 'Right', (-.23, .14), 15)
pose.settle_poles(rig)

# Five-second loop. Hand IK rides the axe; planted foot targets stay in world space.
s = bpy.context.scene
s.render.fps = 24
s.frame_start, s.frame_end = 1, 120
axe.rotation_mode = 'QUATERNION'
base_axe = axe.matrix_world.copy()
base_pose = {name: rig.pose.bones[name].matrix_basis.copy()
             for name in ('Hips', 'Spine', 'Spine1', 'Neck', 'Head')}
for frame in range(1, 122, 4):
    phase = (frame - 1) / 120 * math.tau
    sway, breath = math.sin(phase), (1 - math.cos(phase)) / 2
    anchor = base_axe @ lower
    swing = Matrix.Rotation(math.radians(1.4 * sway), 4, 'Y')
    axe.matrix_world = (Matrix.Translation(Vector((.008 * sway, .005 * breath, .012 * breath)))
                        @ Matrix.Translation(anchor) @ swing @ Matrix.Translation(-anchor) @ base_axe)
    axe.keyframe_insert('location', frame=frame)
    axe.keyframe_insert('rotation_quaternion', frame=frame)
    for name, base in base_pose.items():
        pb = rig.pose.bones[name]
        if name == 'Hips':
            pb.matrix_basis = Matrix.Translation(Vector((.008 * sway, 0, .007 * breath))) @ base
        else:
            amount = {'Spine': .7, 'Spine1': .4, 'Neck': -.4, 'Head': -.7}[name]
            pb.matrix_basis = base @ Matrix.Rotation(math.radians(amount * sway), 4, 'Y')
        pb.keyframe_insert('location', frame=frame)
        pb.keyframe_insert('rotation_quaternion', frame=frame)
s.frame_set(1)

bpy.ops.mesh.primitive_plane_add(size=200)
ground = bpy.context.object
ground.name = 'Ground'
ground.data.materials.append(materials.flat('Ground_matte', (.065, .078, .068), roughness=1))
s = bpy.context.scene
s.world = bpy.data.worlds.new('Soft_overcast')
s.world.use_nodes = True
s.world.node_tree.nodes['Background'].inputs[0].default_value = (.34, .40, .47, 1)
s.world.node_tree.nodes['Background'].inputs[1].default_value = .55
for name, energy, rot, color in [('Key', 2.5, (40, -20, -35), (1, .91, .79)),
                                 ('Fill', .65, (65, 10, 135), (.65, .77, 1))]:
    data = bpy.data.lights.new(name, 'SUN')
    data.energy = energy
    data.color = color
    data.angle = .12
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.rotation_euler = [math.radians(a) for a in rot]
camera = bpy.data.objects.new('Finished_product_camera', bpy.data.cameras.new('Finished_product_camera'))
bpy.context.collection.objects.link(camera)
camera.location = (.20, -6, 2.0)
look = Vector((-.12, -.10, 1.14))
camera.rotation_euler = (look-camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 2.75
s.camera = camera
s.render.engine = 'BLENDER_EEVEE'
s.eevee.taa_render_samples = 64
s.render.resolution_x = 1200
s.render.resolution_y = 1400
s.render.resolution_percentage = 100
s.view_settings.view_transform = 'Standard'
s.render.filepath = os.path.join(OUT, 'skeleton_holding_axe.png')
bpy.ops.render.render(write_still=True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.shading.use_scene_lights = True
            space.shading.use_scene_world = True
            space.overlay.show_overlays = False
            space.region_3d.view_perspective = 'CAMERA'
            space.region_3d.view_camera_zoom = 5
scene.select_only([axe])
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'skeleton_holding_axe.blend'))
