"""Bakes the bake mesh's paint onto the game mesh (selected to active), and writes the game mesh as JSON for Unity.

The JSON carries the mesh in Unity's axes (x, y, z) = (-bx, bz, -by), triangles wound for Unity (the axis change is a
mirror), up to four bone weights per vertex, the rig and its markers. Unity builds the mesh, the bones and the
SkinnedMeshRenderer from it (unity/Assets/Editor/Kraken), so no FBX importer settings stand between the contract's
numbers and the prefab.
"""
import json
import os

import bmesh
import bpy
import numpy as np

import paint
from geo import to_unity
from workshop import bake as workshop_bake
from workshop import export


def unwrap_strips(obj):
    """Angle-based unwrap along the seams the geometry marked, islands scaled to their area, packed tightly."""
    _edit(obj)
    bpy.ops.uv.unwrap(method='ANGLE_BASED', margin=0.002)
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')


def unwrap_smart(obj):
    """Smart projection with the pieces tagged `uvscale` blown up about their `uvpivot` first, so they get more texels."""
    positions = _positions(obj)
    scale, pivot = _attr_values(obj, 'uvscale', 1, 1.0), _attr_values(obj, 'uvpivot', 3, 0.0)
    _set_positions(obj, pivot + (positions - pivot) * scale)
    _edit(obj)
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.002, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')
    _set_positions(obj, positions)


def _edit(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')


def _positions(obj):
    data = np.empty(len(obj.data.vertices) * 3, np.float64)
    obj.data.vertices.foreach_get("co", data)
    return data.reshape(-1, 3)


def _set_positions(obj, positions):
    obj.data.vertices.foreach_set("co", positions.astype(np.float64).ravel())
    obj.data.update()


def _attr_values(obj, name, width, default):
    attr = obj.data.attributes.get(name)
    if attr is None:
        return np.full((len(obj.data.vertices), width), default)
    data = np.empty(len(obj.data.vertices) * width, np.float64)
    attr.data.foreach_get("vector" if width == 3 else "value", data)
    return data.reshape(-1, width)


def pose(obj, transforms):
    """Moves vertices by linear blend skinning: transforms maps bone name -> 4x4 Blender matrix (others stay)."""
    positions = _positions(obj)
    names = {g.index: g.name for g in obj.vertex_groups}
    out = positions.copy()
    for v in obj.data.vertices:
        total, moved = 0.0, np.zeros(3)
        for g in v.groups:
            m = transforms.get(names[g.group])
            p = positions[v.index] if m is None else np.array(m @ v.co.to_4d())[:3]
            moved += g.weight * p
            total += g.weight
        if total > 0:
            out[v.index] = moved / total
    _set_positions(obj, out)
    return positions


def bake(low, high, name, size, normal_size, extrusion, ray):
    """Albedo (colour with occlusion) and tangent-space normals from `high` onto `low`'s UVs. Returns the images."""
    workshop_bake.setup()
    target = bpy.data.materials.new(name + "_target")
    low.data.materials.clear()
    low.data.materials.append(target)
    albedo = workshop_bake.new_image(name + "_albedo", size)
    normal = workshop_bake.new_image(name + "_normal", normal_size, data=True)
    _pass(low, high, target, albedo, 'emit', 'EMIT', 48, extrusion, ray)
    _pass(low, high, target, normal, 'bsdf', 'NORMAL', 8, extrusion, ray)
    return albedo, normal


def _pass(low, high, target, image, output, kind, samples, extrusion, ray):
    for slot in high.material_slots:
        paint.use_output(slot.material, output)
    node = target.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = image
    target.node_tree.nodes.active = node
    bpy.context.scene.cycles.samples = samples
    bpy.ops.object.select_all(action='DESELECT')
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    extra = {'normal_space': 'TANGENT'} if kind == 'NORMAL' else {}
    bpy.ops.object.bake(type=kind, use_selected_to_active=True, cage_extrusion=extrusion, max_ray_distance=ray,
                        margin=6, use_clear=True, **extra)
    target.node_tree.nodes.remove(node)


def final_material(obj, name, albedo, normal=None):
    """The baked look on the game mesh, for the .blend and the Blender previews."""
    mat = bpy.data.materials.new(name)
    tree = mat.node_tree
    bsdf = next(n for n in tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.7
    tex = tree.nodes.new('ShaderNodeTexImage')
    tex.image = albedo
    tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    if normal is not None:
        ntex, nmap = tree.nodes.new('ShaderNodeTexImage'), tree.nodes.new('ShaderNodeNormalMap')
        ntex.image = normal
        tree.links.new(ntex.outputs['Color'], nmap.inputs['Color'])
        tree.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return mat


def mesh_json(obj, part_name, material, bones, textures):
    """One SkinnedMeshRenderer's mesh: vertices split where normals or UVs differ, Unity axes and winding."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers.active.data
    normals = mesh.corner_normals
    index = {b: i for i, b in enumerate(bones)}
    groups = {g.index: g.name for g in obj.vertex_groups}
    keys, remap = {}, []
    positions, out_normals, uvs, bone_index, bone_weight = [], [], [], [], []
    for loop in mesh.loops:
        v = loop.vertex_index
        n, t = normals[loop.index].vector, uv[loop.index].uv
        key = (v, round(n.x, 4), round(n.y, 4), round(n.z, 4), round(t.x, 5), round(t.y, 5))
        if key not in keys:
            keys[key] = len(positions)
            positions.append([round(c, 6) for c in to_unity(mesh.vertices[v].co)])
            out_normals.append([round(c, 5) for c in to_unity(n)])
            uvs.append([round(t.x, 6), round(t.y, 6)])
            ids, ws = _weights(mesh.vertices[v], groups, index)
            bone_index.append(ids)
            bone_weight.append(ws)
        remap.append(keys[key])
    triangles = []
    for tri in mesh.loop_triangles:
        a, b, c = (remap[i] for i in tri.loops)
        triangles += [a, c, b]
    return dict(textures, name=part_name, material=material, positions=_flat(positions), normals=_flat(out_normals),
                uvs=_flat(uvs), triangles=triangles, boneIndex=_flat(bone_index), boneWeight=_flat(bone_weight))


def _weights(vertex, groups, index):
    pairs = sorted(((g.weight, index[groups[g.group]]) for g in vertex.groups if groups[g.group] in index and g.weight > 1e-4),
                   reverse=True)[:4]
    if not pairs:
        raise SystemExit(f"vertex {vertex.index} has no bone weight")
    total = sum(w for w, _ in pairs)
    pairs += [(0.0, 0)] * (4 - len(pairs))
    return [i for _, i in pairs], [round(w / total, 5) for w, _ in pairs]


def _flat(rows):
    return [x for row in rows for x in row]


def rig_json(entries):
    return [{"name": n, "parent": p, "position": [round(c, 6) for c in pos]} for n, p, pos in entries]


def write(out, asset, bones, markers, parts, notes):
    data = {"asset": asset, "bones": rig_json(bones), "markers": rig_json(markers), "parts": parts, "notes": notes}
    with open(os.path.join(out, asset + ".json"), "w", encoding="utf-8") as handle:
        json.dump(data, handle, separators=(",", ":"))


def save_png(image, path):
    export.png(image, path)


def triangulate(obj):
    """Triangulates in place (so what Unity gets is exactly what was baked and previewed)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    bm.to_mesh(obj.data)
    bm.free()
