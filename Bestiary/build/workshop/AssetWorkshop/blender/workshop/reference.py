"""Loads the game's own meshes and textures from the local reference export (rip-reference.ps1) into Blender, for
previews only: seeing a vanilla prop next to ours, or animating ours on top of it.

Reference objects are tagged and the pipeline never joins or exports them, so nothing of the game's ends up in a bundle.
Unity is Y up and left-handed; the importer converts to the workshop's Blender axes (Z up, front -Y).
"""
import os
import re
import struct

import bpy

from . import scene
from .unity import ROOT  # noqa: F401  (kept importable as reference.ROOT)

TAG = "workshop_reference"
_FLOAT = {0: "f", 1: "e"}                                                                  # Unity VertexFormat Float32, Float16
FORMAT_SIZES = {0: 4, 1: 2, 2: 1, 3: 1, 4: 2, 5: 2, 6: 1, 7: 1, 8: 2, 9: 2, 10: 4, 11: 4}   # Unity VertexFormat bytes


def is_reference(obj):
    return bool(obj.get(TAG))


def mesh(asset, name=None, material=None):
    """asset: path of a Mesh .asset under the reference Assets folder, e.g. 'world/Props/Chests/models/stonechest.asset'."""
    text = open(os.path.join(ROOT, asset), encoding="utf-8").read()
    vertices, uvs = _vertices(text)
    triangles = _triangles(text)
    data = bpy.data.meshes.new(name or os.path.splitext(os.path.basename(asset))[0])
    data.from_pydata(vertices, [], triangles)
    layer = data.uv_layers.new(name="UVMap")
    for loop in data.loops:
        layer.data[loop.index].uv = uvs[loop.vertex_index]
    data.validate()
    obj = bpy.data.objects.new(data.name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj[TAG] = True
    if material is not None:
        data.materials.append(material)
    return obj


def material(name, albedo, normal=None, roughness=0.85):
    """A material on the game's own textures (paths under the reference Assets folder), point-filtered like the game.

    The texture paths are kept on the material ("reference_albedo", "reference_normal") so an export can tell the
    mod which game texture to borrow at runtime instead of shipping a copy.
    """
    mat = bpy.data.materials.new(name)
    tree = mat.node_tree
    bsdf = next(n for n in tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = roughness
    tree.links.new(_texture(tree, albedo, data=False).outputs['Color'], bsdf.inputs['Base Color'])
    mat["reference_albedo"] = albedo
    if normal:
        nmap = tree.nodes.new('ShaderNodeNormalMap')
        tree.links.new(_texture(tree, normal, data=True).outputs['Color'], nmap.inputs['Color'])
        tree.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
        mat["reference_normal"] = normal
    return mat


def uv_tiles(obj, metres):
    """World-scale UVs by cube projection: a tiling texture repeats every `metres`, whatever the part's size."""
    scene.select_only([obj])
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=metres, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


def _texture(tree, path, data):
    node = tree.nodes.new('ShaderNodeTexImage')
    node.image = bpy.data.images.load(os.path.join(ROOT, path), check_existing=True)
    node.interpolation = 'Closest'
    if data:
        node.image.colorspace_settings.name = 'Non-Color'
    return node


def _field(text, name):
    return re.search(rf"^\s*{name}: (.*)$", text, re.MULTILINE).group(1).strip()


def _channels(text):
    block = text[text.index("m_Channels:"):text.index("m_DataSize:")]
    rows = re.findall(r"stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)", block)
    # Unity packs flag bits above the low nibble of `dimension` (52 is 4 components plus flags): keep the count only.
    return [(int(s), int(o), int(f), int(d) & 0xF) for s, o, f, d in rows]


def _vertices(text):
    """Positions and uv0. Static props keep everything in stream 0; skinned meshes (the player's body) split
    positions, uvs and bone weights into separate streams, each starting on a 16-byte boundary."""
    count, raw = int(_field(text, "m_VertexCount")), bytes.fromhex(_field(text, "_typelessdata"))
    channels = _channels(text)
    starts, strides = _streams(channels, count)
    position, uv = channels[0], channels[4]
    vertices, uvs = [], []
    xyz, st = f"<3{_FLOAT.get(position[2], 'f')}", f"<2{_FLOAT.get(uv[2], 'f')}"   # float32 or half (the Ashlands ship's uvs)
    for i in range(count):
        at = starts[position[0]] + i * strides[position[0]] + position[1]
        x, y, z = struct.unpack_from(xyz, raw, at)
        vertices.append((-x, -z, y))                       # Unity (x, y, z) -> Blender (-x, -z, y)
        at = starts[uv[0]] + i * strides[uv[0]] + uv[1]
        uvs.append(struct.unpack_from(st, raw, at) if uv[3] else (0.0, 0.0))
    return vertices, uvs


def _streams(channels, count):
    """Byte offset and stride of every vertex stream, from the channels that use it."""
    strides = {}
    for stream, offset, form, dimension in channels:
        if dimension:
            size = offset + dimension * FORMAT_SIZES.get(form, 4)
            strides[stream] = max(strides.get(stream, 0), size)
    starts, at = {}, 0
    for stream in sorted(strides):
        starts[stream] = at
        at = (at + strides[stream] * count + 15) // 16 * 16
    return starts, strides


def _triangles(text):
    raw = bytes.fromhex(_field(text, "m_IndexBuffer"))
    wide = _field(text, "m_IndexFormat") == "1"
    indices = struct.unpack(f"<{len(raw) // (4 if wide else 2)}{'I' if wide else 'H'}", raw)
    return [(indices[i], indices[i + 2], indices[i + 1]) for i in range(0, len(indices) - 2, 3)]  # mirror flips winding
