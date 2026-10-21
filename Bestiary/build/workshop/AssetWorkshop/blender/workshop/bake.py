"""Bakes the visual mesh's materials into one texture atlas with Cycles (GPU when there is one).

Albedo is baked through an emission pass, so metallic materials keep their colour; ambient occlusion is multiplied
into it; the normal map comes from the materials' bump nodes, in tangent space for Unity.
"""
import bpy
import numpy as np

from . import materials, scene

ALBEDO_SAMPLES = 8
AO_SAMPLES = 64


def setup():
    """Switches the scene to Cycles on the best device; returns the device kind for the log."""
    kind = use_gpu()
    bpy.context.scene.render.engine = 'CYCLES'
    bpy.context.scene.cycles.device = 'GPU' if kind != 'CPU' else 'CPU'
    return kind


def use_gpu():
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for kind in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI', 'METAL'):
        try:
            prefs.compute_device_type = kind
        except TypeError:
            continue
        prefs.get_devices()
        gpus = [d for d in prefs.devices if d.type != 'CPU']
        if gpus:
            for device in prefs.devices:
                device.use = device.type != 'CPU'
            return kind
    return 'CPU'


def unwrap(obj, margin=0.01):
    scene.select_only([obj])
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=margin, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')


def new_image(name, size, data=False):
    image = bpy.data.images.new(name, size, size, alpha=False, is_data=data)
    if data:
        image.colorspace_settings.name = 'Non-Color'
    return image


def albedo(obj, image):
    restores = [_emit_base_color(m) for m in _materials(obj)]
    _bake(obj, image, ALBEDO_SAMPLES, type='EMIT')
    for restore in restores:
        restore()


def normal(obj, image):
    _bake(obj, image, ALBEDO_SAMPLES, type='NORMAL', normal_space='TANGENT')


def occlusion(obj, image):
    _bake(obj, image, AO_SAMPLES, type='AO')


def multiply(albedo_image, ao_image, strength):
    """albedo *= lerp(1, ao, strength), on the RGB channels."""
    size = len(albedo_image.pixels)
    colour, ao = np.empty(size, np.float32), np.empty(size, np.float32)
    albedo_image.pixels.foreach_get(colour)
    ao_image.pixels.foreach_get(ao)
    shade = 1.0 - strength * (1.0 - ao)
    colour = colour.reshape(-1, 4)
    colour[:, :3] *= shade.reshape(-1, 4)[:, :1]
    albedo_image.pixels.foreach_set(colour.ravel())
    albedo_image.update()


def _materials(obj):
    return list({slot.material for slot in obj.material_slots if slot.material})


def _bake(obj, image, samples, **kwargs):
    bpy.context.scene.cycles.samples = samples
    targets = [_target_node(m, image) for m in _materials(obj)]
    scene.select_only([obj])
    bpy.ops.object.bake(margin=8, use_clear=True, **kwargs)
    for mat, node in targets:
        mat.node_tree.nodes.remove(node)


def _target_node(mat, image):
    node = mat.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = image
    mat.node_tree.nodes.active = node
    return mat, node


def _emit_base_color(mat):
    """Routes Base Color into an emission shader on the output; returns the function that undoes it."""
    tree, out, base = mat.node_tree, materials.output(mat), materials.principled(mat).inputs['Base Color']
    previous = out.inputs['Surface'].links[0].from_socket if out.inputs['Surface'].links else None
    emit = tree.nodes.new('ShaderNodeEmission')
    if base.links:
        tree.links.new(base.links[0].from_socket, emit.inputs['Color'])
    else:
        emit.inputs['Color'].default_value = base.default_value
    tree.links.new(emit.outputs['Emission'], out.inputs['Surface'])

    def restore():
        tree.nodes.remove(emit)
        if previous is not None:
            tree.links.new(previous, out.inputs['Surface'])
    return restore
