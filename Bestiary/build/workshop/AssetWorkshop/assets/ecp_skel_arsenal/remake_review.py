"""Coloured review grid and icon references for the low-poly bone weapon family."""
import json
import math
import os
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
OUT = HERE / 'out' / 'remake'
sys.path[:0] = [str(HERE), str(ROOT.parent / 'blender')]
import bpy
from mathutils import Vector, Matrix, Quaternion
from workshop import scene, materials
import icons

ITEMS = [
    ('ecp_bone_greataxe','Executioner', (0,0,1),(0,-1,0)),
    ('ecp_skel_dagger','Dagger',(0,-1,0),(0,0,1)),
    ('ecp_skel_sword','Sword',(0,-1,0),(0,0,1)),
    ('ecp_skel_axe','Axe',(0,-1,0),(0,0,1)),
    ('ecp_skel_mace','Mace',(0,-1,0),(0,0,1)),
    ('ecp_skel_spear','Spear',(0,1,0),(0,0,1)),
    ('ecp_skel_atgeir','Atgeir',icons.HOLD,icons.BLADE_SIDE.cross(icons.HOLD)),
    ('ecp_skel_bow_player','Bow',icons.BOW_LIMBS,icons.BOW_STRING.cross(icons.BOW_LIMBS)),
    ('ecp_xbow_crossbow','Crossbow',(0,-1,0),(0,0,1)),
    ('ecp_skel_arrow','Arrow',(0,-1,0),(0,0,1)),
    ('ecp_xbow_bolt','Blunt bolt',(0,-1,0),(0,0,1)),
]


def load_item(name):
    with bpy.data.libraries.load(str(ROOT/name/'out'/f'{name}.blend')) as (src,dst):
        dst.objects = [name]
    obj = dst.objects[0]
    bpy.context.collection.objects.link(obj)
    # Strings are runtime objects, shown here for a complete weapon preview only.
    paths = []
    if name == 'ecp_skel_bow_player':
        points = json.loads((ROOT/name/'out'/f'{name}_points.json').read_text())
        p = points.get('blender', points)
        paths = [[p['tip_top'], p['tip_bottom']]]
    if name == 'ecp_xbow_crossbow':
        paths = [[(-.352,-.398,.024),(0,-.1,.036),(.352,-.398,.024)]]
    for path in paths:
        curve = bpy.data.curves.new('Preview_sinew_string','CURVE')
        curve.dimensions = '3D'
        curve.bevel_depth = .0015
        curve.bevel_resolution = 0
        spline = curve.splines.new('POLY')
        spline.points.add(len(path)-1)
        for pt,co in zip(spline.points,path):
            pt.co = (*co,1)
        string = bpy.data.objects.new('Preview_only_string',curve)
        bpy.context.collection.objects.link(string)
        curve.materials.append(materials.flat('String',(.22,.17,.10)))
        bpy.ops.object.select_all(action='DESELECT')
        string.select_set(True)
        bpy.context.view_layer.objects.active = string
        bpy.ops.object.convert(target='MESH')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.join()
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE':
                node.interpolation = 'Closest'
                node.image.pack()
    return obj


def orient(obj, head, face, diagonal=False):
    head = Vector(head).normalized()
    face = (Vector(face)-head*Vector(face).dot(head)).normalized()
    source = Matrix((head,face,head.cross(face))).transposed()
    up = Vector((1,0,1)).normalized() if diagonal else Vector((0,0,1))
    front = Vector((0,-1,0))
    obj.matrix_world = (Matrix((up,front,up.cross(front))).transposed() @ source.transposed()).to_4x4()


def text(label, location, size=.10):
    font = bpy.data.curves.new(label,'FONT')
    font.body = label
    font.align_x = 'CENTER'
    font.size = size
    obj = bpy.data.objects.new(label,font)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (math.pi/2,0,0)
    font.materials.append(materials.flat('Label',(.65,.60,.48)))


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for name,label,head,face in ITEMS:
        scene.clear()
        obj = load_item(name)
        orient(obj,head,face,True)
        bpy.context.view_layer.update()
        icons._light()
        icons._camera(obj)
        icons.SIZE = 768
        icons._render(str(OUT/f'{name}_icon_reference.png'))
    scene.clear()
    objects=[]
    for i,(name,label,head,face) in enumerate(ITEMS):
        obj=load_item(name)
        orient(obj,head,face)
        bpy.context.view_layer.update()
        low,high=scene.bounds([obj])
        scale=1.65/max(high.z-low.z,high.x-low.x)
        obj.matrix_world=Matrix.Scale(scale,4)@obj.matrix_world
        bpy.context.view_layer.update()
        low,high=scene.bounds([obj])
        x=(i%6)*1.55
        z=2.6-(i//6)*2.6
        obj.location += Vector((x-(low.x+high.x)/2,0,z-low.z))
        manifest=json.loads((ROOT/name/'out'/f'{name}.json').read_text())
        obj['source_asset']=name
        obj['export_triangles']=manifest['triangles']
        text(label,(x,-.18,z-.22),.115)
        text(f"{manifest['triangles']:,} triangles",(x,-.18,z-.37),.08)
        objects.append(obj)
    text('BONE ARSENAL  /  COLOURED MODEL REVIEW',(3.875,0,4.70),.18)
    text('Each weapon scaled to fit its panel; source models retain their actual dimensions.',(3.875,0,-.70),.09)
    icons._light()
    cam=bpy.data.objects.new('Review_camera',bpy.data.cameras.new('Review_camera'))
    bpy.context.collection.objects.link(cam)
    cam.data.type='ORTHO'
    cam.data.ortho_scale=10.0
    cam.location=(3.875,-15,2.0)
    cam.rotation_euler=(math.pi/2,0,0)
    s=bpy.context.scene
    s.camera=cam
    s.render.engine='BLENDER_EEVEE'
    s.render.resolution_x=2200
    s.render.resolution_y=1400
    s.render.resolution_percentage=100
    s.render.film_transparent=False
    s.view_settings.view_transform='Standard'
    s.world.color=(.08,.08,.08)
    s.world.node_tree.nodes['Background'].inputs[0].default_value=(.075,.085,.09,1)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active
                space.shading.type='MATERIAL'
                space.shading.use_scene_world=True
                space.shading.use_scene_lights=True
                space.overlay.show_overlays=False
                space.region_3d.view_perspective='CAMERA'
    bpy.ops.object.select_all(action='DESELECT')
    objects[0].select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'bone_arsenal_review.blend'))
    s.render.filepath=str(OUT/'bone_arsenal_review.png')
    bpy.ops.render.render(write_still=True)


if __name__=='__main__':
    main()
