"""The meshes and materials of a prefab, for prefab.load: a Mesh .asset through reference.mesh plus what a whole prefab
needs on top (the game's own normals, one material per submesh, skinned meshes posed by their bones, Unity's built-in
cube), and a Blender material from a .mat on the game's textures through reference.material.

Blender data made here is reused by the next load: meshes carry "reference_mesh", materials "reference_mat".
"""
import os
import re

import bmesh
import bpy
import numpy as np
from mathutils import Matrix

from . import reference, unity

UNITY_TO_BLENDER = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # (x, y, z) -> (-x, -z, y)
BUILTIN_CUBE = "10202"
_DTYPES = {0: "<f4", 1: "<f2", 2: "u1", 3: "i1", 4: "<u2", 5: "<i2", 6: "u1", 7: "i1", 8: "<u2", 9: "<i2", 10: "<u4",
           11: "<i4"}
_NORMALIZED = {2: 255.0, 3: 127.0, 4: 65535.0, 5: 32767.0}
NORMAL, BLEND_WEIGHT, BLEND_INDICES = 1, 12, 13


def to_blender(matrix):
    """A 4x4 Unity-space matrix (a bind pose) in the workshop's Blender axes."""
    return UNITY_TO_BLENDER @ matrix @ UNITY_TO_BLENDER.inverted()


def mesh(asset, materials):
    """Mesh data for a Mesh .asset worn with these materials (one per submesh; None leaves a submesh undrawn, as Unity
    does when a renderer lists fewer materials than the mesh has submeshes). Shared between identical uses."""
    key = f"{asset}|{'|'.join(m.name if m else '-' for m in materials)}"
    data = next((d for d in bpy.data.meshes if d.get("reference_mesh") == key), None)
    if data is None:
        data = _copy(asset)
        data.name = os.path.splitext(os.path.basename(asset))[0]
        _submeshes(data, unity.read(asset), materials)
        data["reference_mesh"] = key
    return data


def skinned(asset, materials, bones):
    """Mesh data for a skinned mesh, its vertices moved to where the bones put them. bones[i] maps bone i's bind-pose
    space to the renderer's local space (Blender axes), None for the bind pose. Never shared: the pose is its own."""
    data = _copy(asset)
    data.name = os.path.splitext(os.path.basename(asset))[0]
    text = unity.read(asset)
    posed = _pose(data, text, bones)
    _submeshes(data, text, materials)
    data["reference_posed"] = posed
    return data


def bind_poses(asset):
    """The mesh's bind poses (Unity axes) in bone order."""
    text = unity.read(asset)
    block = text[text.index("m_BindPose:"):text.index("m_BoneNameHashes:")]
    values = [float(v) for v in re.findall(rf"e\d\d: ({unity.NUMBER})", block)]
    return [Matrix([values[i + row * 4:i + row * 4 + 4] for row in range(4)]) for i in range(0, len(values), 16)]


def cube(materials):
    """Unity's built-in cube: 1 m, centred on the origin, every face mapped to the whole texture."""
    key = f"builtin:cube|{'|'.join(m.name if m else '-' for m in materials)}"
    data = next((d for d in bpy.data.meshes if d.get("reference_mesh") == key), None)
    if data is None:
        data = bpy.data.meshes.new("builtin_cube")
        corners = [(x, y, z) for x in (-0.5, 0.5) for y in (-0.5, 0.5) for z in (-0.5, 0.5)]
        faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        data.from_pydata(corners, [], faces)
        uvs = data.uv_layers.new(name="UVMap")
        uvs.data.foreach_set("uv", [c for _ in faces for c in (0, 0, 1, 0, 1, 1, 0, 1)])
        data.materials.append(materials[0] if materials else None)
        data["reference_mesh"] = key
    return data


def line(name, points, width, closed, materials):
    """A line renderer as curve data: a round tube `width` thick through the points, straight between them as in the
    game."""
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = width / 2
    curve.bevel_resolution = 1
    curve.use_fill_caps = True
    spline = curve.splines.new('POLY')
    spline.points.add(len(points) - 1)
    for point, co in zip(spline.points, points):
        point.co = (*co, 1.0)
    spline.use_cyclic_u = closed
    curve.materials.append(materials[0] if materials else None)
    return curve


def material(path):
    """A Blender material for a .mat: albedo (tinted, tiled, cut out where the game cuts it) and normal map."""
    mat = next((m for m in bpy.data.materials if m.get("reference_mat") == path), None)
    if mat is not None:
        return mat
    info = unity.material(path)
    mat = _textured(info) or _plain(info)
    mat["reference_mat"] = path
    mat["reference_shader"] = info["shader"]
    mat.use_backface_culling = info["cull"] == 2
    return mat


def _copy(asset):
    """A copy of the decoded source to dress. Blender copies ID properties too, so the copy's "reference_source" goes:
    left on, a later _decoded would find the dressed copy instead of the source (a rug in another rug's material)."""
    data = _decoded(asset).copy()
    if "reference_source" in data:
        del data["reference_source"]
    return data


def _decoded(asset):
    """The asset decoded once by reference.mesh, with the game's normals; later calls copy it."""
    data = next((d for d in bpy.data.meshes if d.get("reference_source") == asset), None)
    if data is not None:
        return data
    obj = reference.mesh(asset)
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    data.name = f"{data.name} (source)"   # never drawn (no users, not saved); its copies take the plain name
    _normals(data, unity.read(asset))
    data["reference_source"] = asset
    return data


def _vertex_data(text):
    count = int(reference._field(text, "m_VertexCount"))
    channels = reference._channels(text)
    starts, strides = reference._streams(channels, count)
    return count, bytes.fromhex(reference._field(text, "_typelessdata")), channels, starts, strides


def _channel(vertex_data, index):
    """One vertex attribute as a (vertices, dimension) float array, or None when the mesh has none."""
    count, raw, channels, starts, strides = vertex_data
    if index >= len(channels) or not channels[index][3] & 0xF:
        return None
    stream, offset, form, dimension = channels[index]
    dimension &= 0xF   # the high bits are flags (the Ashlands longship's half-float normals say 52 for 4)
    dtype = np.dtype(_DTYPES.get(form, "<f4"))
    values = np.ndarray((count, dimension), dtype, raw, starts[stream] + offset, (strides[stream], dtype.itemsize))
    return values.astype(np.float64) / _NORMALIZED.get(form, 1.0)


def _normals(data, text):
    """The game's own vertex normals as custom normals, so hard and soft edges shade as in the game."""
    normals = _channel(_vertex_data(text), NORMAL)
    if normals is None or len(normals) != len(data.vertices) or normals.shape[1] < 3:
        data.shade_smooth()
        return
    normals = np.column_stack((-normals[:, 0], -normals[:, 2], normals[:, 1]))   # Unity -> Blender axes
    data.shade_smooth()
    data.normals_split_custom_set_from_vertices([tuple(n) for n in normals])


def _submeshes(data, text, materials):
    """One material slot per submesh; the faces of submeshes without a material are removed."""
    for mat in materials:
        data.materials.append(mat)
    ranges = _submesh_ranges(text)
    if len(ranges) < 2 and materials and materials[0] is not None:
        return
    owner = _face_submesh(data, text, ranges)
    undrawn = [i for i, sub in enumerate(owner) if sub < 0 or sub >= len(materials) or materials[sub] is None]
    data.polygons.foreach_set("material_index", [max(0, min(sub, len(materials) - 1)) for sub in owner])
    if undrawn:
        _delete_faces(data, undrawn)


def _submesh_ranges(text):
    """(first triangle, triangles, first vertex, vertices) of every submesh."""
    block = text[text.index("m_SubMeshes:"):text.index("m_Shapes:")]
    rows = re.findall(r"firstByte: (\d+)\s+indexCount: (\d+)\s+topology: \d+\s+baseVertex: \d+\s+"
                      r"firstVertex: (\d+)\s+vertexCount: (\d+)", block)
    size = 4 if reference._field(text, "m_IndexFormat") == "1" else 2
    return [(int(b) // size // 3, int(c) // 3, int(v), int(n)) for b, c, v, n in rows]


def _face_submesh(data, text, ranges):
    """The submesh each face came from: by triangle order, or by vertex range when validate() dropped faces."""
    triangles = len(bytes.fromhex(reference._field(text, "m_IndexBuffer"))) // (
        4 if reference._field(text, "m_IndexFormat") == "1" else 2) // 3
    if len(data.polygons) == triangles:
        owner = np.full(triangles, -1)
        for i, (first, count, _, _) in enumerate(ranges):
            owner[first:first + count] = i
        return owner.tolist()
    by_vertex = np.full(len(data.vertices), -1)
    for i, (_, _, first, count) in enumerate(ranges):
        by_vertex[first:first + count] = i
    return [int(by_vertex[p.vertices[0]]) for p in data.polygons]


def _delete_faces(data, faces):
    mesh = bmesh.new()
    mesh.from_mesh(data)
    mesh.faces.ensure_lookup_table()
    bmesh.ops.delete(mesh, geom=[mesh.faces[i] for i in faces], context='FACES_ONLY')
    mesh.to_mesh(data)
    mesh.free()


def _pose(data, text, bones):
    """Linear blend skinning of the vertices with the bone matrices; False (bind pose kept) without weights."""
    vertex_data = _vertex_data(text)
    weights, indices = _channel(vertex_data, BLEND_WEIGHT), _channel(vertex_data, BLEND_INDICES)
    if weights is None or indices is None or all(b is None for b in bones):
        return False
    co = np.empty(len(data.vertices) * 3)
    data.vertices.foreach_get("co", co)
    co = np.column_stack((co.reshape(-1, 3), np.ones(len(data.vertices))))
    moved, total = np.zeros((len(co), 3)), weights.sum(axis=1)
    for slot in range(weights.shape[1]):
        for bone, matrix in enumerate(bones):
            chosen = indices[:, slot] == bone
            if matrix is None:
                matrix = Matrix()
            moved[chosen] += weights[chosen, slot:slot + 1] * (co[chosen] @ np.array(matrix).T)[:, :3]
    unweighted = total <= 1e-6
    moved[~unweighted] /= total[~unweighted, None]
    moved[unweighted] = co[unweighted, :3]
    data.vertices.foreach_set("co", moved.ravel())
    if "custom_normal" in data.attributes:   # the game's normals were for the bind pose; shade from the posed shape
        data.attributes.remove(data.attributes["custom_normal"])
    data.update()
    return True


def _textured(info):
    albedo, scale, offset = info["albedo"]
    if not albedo:
        return None
    normal = info["normal"][0]
    try:
        mat = reference.material(info["name"], albedo, normal)
    except RuntimeError:   # a texture Blender cannot read
        return None
    tree = mat.node_tree
    textures = [n for n in tree.nodes if n.bl_idname == 'ShaderNodeTexImage']
    if tuple(scale) != (1.0, 1.0) or tuple(offset) != (0.0, 0.0):
        _tile(tree, textures, scale, offset)
    bsdf = next(n for n in tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    if tuple(info["color"][:3]) != (1.0, 1.0, 1.0):
        _tint(tree, textures[0], bsdf, info["color"])
    if info["cutout"] or "Vegetation" in info["shader"] or "Grass" in info["shader"]:
        _cut_out(tree, textures[0], bsdf, info["cutoff"])
    return mat


def _plain(info):
    mat = bpy.data.materials.new(info["name"])
    bsdf = next(n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = (*info["color"][:3], 1.0)
    bsdf.inputs['Roughness'].default_value = 0.85
    return mat


def _tile(tree, textures, scale, offset):
    """Unity's texture scale and offset (uv * scale + offset), shared by every texture of the material as in the game."""
    coords = tree.nodes.new('ShaderNodeTexCoord')
    mapping = tree.nodes.new('ShaderNodeMapping')
    mapping.inputs['Scale'].default_value = (scale[0], scale[1], 1.0)
    mapping.inputs['Location'].default_value = (offset[0], offset[1], 0.0)
    tree.links.new(coords.outputs['UV'], mapping.inputs['Vector'])
    for texture in textures:
        tree.links.new(mapping.outputs['Vector'], texture.inputs['Vector'])


def _tint(tree, albedo, bsdf, color):
    multiply = tree.nodes.new('ShaderNodeVectorMath')
    multiply.operation = 'MULTIPLY'
    multiply.inputs[1].default_value = color[:3]
    tree.links.new(albedo.outputs['Color'], multiply.inputs[0])
    tree.links.new(multiply.outputs['Vector'], bsdf.inputs['Base Color'])


def _cut_out(tree, albedo, bsdf, cutoff):
    """Alpha test like the game's cutout shaders (clip(alpha - cutoff)): kept where alpha reaches the cutoff, gone
    below it. Greater-or-equal matters: materials with a cutoff of 1.0 (ground cover, heath grass) keep their opaque
    texels in the game and would vanish under a strict greater-than."""
    test = tree.nodes.new('ShaderNodeMath')
    test.operation = 'GREATER_THAN'
    test.inputs[1].default_value = cutoff - 0.001
    tree.links.new(albedo.outputs['Alpha'], test.inputs[0])
    tree.links.new(test.outputs['Value'], bsdf.inputs['Alpha'])
