"""Things coming into being piece by piece, for the headsman scene (blender_scene.py).

axe(obj, scales): the new axe forming in the raised hands (the baked ghost axe, shown from the throw's Ghost to its
Solid): pale and see-through - whitish, greenish, bluish - and small for HOLD (HeadsmanGhost holds its size), then, as
it grows, its pieces turn to their own colour one at a time and quickly, from the bottom vertebra of the haft up to
the top of the head.

skeleton(obj, form, solid): the skeleton rising where the thrown axe broke (the game's Skeleton is 49 separate bones):
nothing at first, then its bones fade in one at a time from the feet up, each through the same pale ghost to its own
colour, over the same frames as the axe's pieces. Returns when and where each bone appears, for the axe's shattered
pieces to strike into it.

Both go by a face attribute 'reveal' (the piece's place in the order, 0 first) and a keyed value in the material: a
piece turns from when the value passes its place, over FADE places, so a few overlap.
"""
import bpy
from mathutils import Vector

import blender_shatter

HOLD = 15                     # frames the new axe stays small and ghostly (HeadsmanGhost.Hold, 0.5 s)
END = 3                       # frames before solid the last piece is done
FADE = 2.5                    # places a piece takes to turn
GHOST = (0.5, 0.92, 0.85)     # whitish, greenish, bluish (strong glows render washed out to white)


def axe(obj, scales):
    """The ghost axe: `scales` its size per frame from frame 1 (0 while hidden), which gives the forming windows."""
    groups = _axe_groups(obj.data)
    _order(obj.data, groups)
    value = _dress(obj, appear=False)
    keys = []
    for start, end in _windows(scales):
        keys += [(start, 0.0), (start + HOLD, 0.0), (end - END, len(groups) - 1 + FADE)]
    _keys(value, keys)


def skeleton(obj, form, solid):
    """The rising skeleton; returns (first, final, where(frame)): its bones appear from `first` to `final`, and where
    gives the middle of the bone that appears at a frame."""
    depsgraph = _at(solid)
    heights = _positions(obj, depsgraph)
    groups = sorted(_islands(obj.data), key=lambda faces: min(heights[v].z for v in _verts(obj.data, faces)))
    _order(obj.data, groups)
    first, final = form + HOLD, solid - END
    top = len(groups) - 1 + FADE
    _keys(_dress(obj, appear=True), [(first, 0.0), (final, top)])

    def where(frame):
        place = min(len(groups) - 1, int(top * (frame - first) / max(1, final - first)))
        at = _positions(obj, _at(frame))
        verts = _verts(obj.data, groups[place])
        return sum((at[v] for v in verts), Vector()) / len(verts)
    return first, final, where


def _axe_groups(mesh):
    """The axe's pieces as face lists, bottom up: each vertebra of the haft, the blade in shards, the back in two
    (the shards and halves split by the same seeds the shattered axe breaks along, blender_shatter)."""
    groups = {}
    for faces in _islands(mesh):
        verts = _verts(mesh, faces)
        middle = sum((mesh.vertices[v].co for v in verts), Vector()) / len(verts)
        if len(faces) > 500 or middle.z > blender_shatter.TOP:
            seeds = blender_shatter.BLADE_SEEDS if len(faces) > 500 else blender_shatter.BACK_SEEDS
            for f in faces:
                centre = mesh.polygons[f].center
                nearest = min(range(len(seeds)), key=lambda i: (seeds[i] - centre).length)
                groups.setdefault(('blade' if len(faces) > 500 else 'back', nearest), []).append(f)
            continue
        slot = min(blender_shatter.VERTEBRAE - 1, max(0, round((middle.z - blender_shatter.FIRST) / blender_shatter.STEP)))
        groups.setdefault(('vertebra', slot), []).extend(faces)
    return sorted(groups.values(), key=lambda faces: _middle_z(mesh, faces))


def _middle_z(mesh, faces):
    verts = _verts(mesh, faces)
    return sum(mesh.vertices[v].co.z for v in verts) / len(verts)


def _islands(mesh):
    """Face lists of the mesh's separate parts: faces joined where they share a corner's position (the bake splits
    corners along UV seams, so indices alone would break a bone apart)."""
    parent = list(range(len(mesh.polygons)))

    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    owner = {}
    for poly in mesh.polygons:
        for v in poly.vertices:
            key = tuple(round(c, 5) for c in mesh.vertices[v].co)
            other = owner.setdefault(key, poly.index)
            parent[root(other)] = root(poly.index)
    islands = {}
    for poly in mesh.polygons:
        islands.setdefault(root(poly.index), []).append(poly.index)
    return list(islands.values())


def _verts(mesh, faces):
    return sorted({v for f in faces for v in mesh.polygons[f].vertices})


def _order(mesh, groups):
    """Each face's place in the reveal, as the face attribute 'reveal'."""
    places = [0.0] * len(mesh.polygons)
    for place, faces in enumerate(groups):
        for f in faces:
            places[f] = float(place)
    if 'reveal' in mesh.attributes:
        mesh.attributes.remove(mesh.attributes['reveal'])
    mesh.attributes.new('reveal', 'FLOAT', 'FACE').data.foreach_set('value', places)


def _at(frame):
    bpy.context.scene.frame_set(frame)
    return bpy.context.evaluated_depsgraph_get()


def _positions(obj, depsgraph):
    """The object's vertices in the world at the depsgraph's frame (its point cache played)."""
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    at = [obj.matrix_world @ v.co for v in mesh.vertices]
    evaluated.to_mesh_clear()
    return at


def _dress(obj, appear):
    """The object's own material with the reveal in it; returns the value to key. Before its turn a piece is the pale
    ghost (`appear` False: the axe) or nothing (`appear` True: the skeleton, whose bones show as the ghost in the first
    third of their turn)."""
    mat = obj.data.materials[0].copy()
    obj.data.materials[0] = mat
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    out = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
    own = out.inputs['Surface'].links[0].from_socket
    value = nodes.new('ShaderNodeValue')
    turn = _clamped(nodes, links, _math(nodes, links, 'SUBTRACT', value.outputs[0], _place(nodes).outputs['Fac']), FADE)
    clear = nodes.new('ShaderNodeBsdfTransparent')
    ghost = _mix(nodes, links, 0.55, clear.outputs[0], _glow(nodes).outputs[0])
    shader = _mix(nodes, links, turn, ghost.outputs[0], own)
    if appear:
        shader = _mix(nodes, links, _math(nodes, links, 'MULTIPLY', turn, 3.0, clamp=True), clear.outputs[0],
                      shader.outputs[0])
    links.new(shader.outputs[0], out.inputs['Surface'])
    _blended(mat)
    return value.outputs[0]


def _place(nodes):
    node = nodes.new('ShaderNodeAttribute')
    node.attribute_type = 'GEOMETRY'
    node.attribute_name = 'reveal'
    return node


def _glow(nodes):
    node = nodes.new('ShaderNodeEmission')
    node.inputs['Color'].default_value = (*GHOST, 1.0)
    node.inputs['Strength'].default_value = 0.75
    return node


def _math(nodes, links, operation, a, b, clamp=False):
    node = nodes.new('ShaderNodeMath')
    node.operation = operation
    node.use_clamp = clamp
    for socket, given in zip(node.inputs, (a, b)):
        if isinstance(given, (int, float)):
            socket.default_value = given
        else:
            links.new(given, socket)
    return node.outputs[0]


def _clamped(nodes, links, delta, width):
    return _math(nodes, links, 'DIVIDE', delta, width, clamp=True)


def _mix(nodes, links, factor, first, second):
    node = nodes.new('ShaderNodeMixShader')
    if isinstance(factor, (int, float)):
        node.inputs[0].default_value = factor
    else:
        links.new(factor, node.inputs[0])
    links.new(first, node.inputs[1])
    links.new(second, node.inputs[2])
    return node


def _blended(mat):
    for attribute, value in (('surface_render_method', 'BLENDED'), ('blend_method', 'BLEND')):
        try:
            setattr(mat, attribute, value)
            return
        except (AttributeError, TypeError):
            continue


def _windows(scales):
    """(first, last + 1) Blender frames of each run of frames the part is shown."""
    runs, start = [], None
    for f, size in enumerate(scales + [0.0]):
        if size > 0.0 and start is None:
            start = f + 1
        elif size <= 0.0 and start is not None:
            runs.append((start, f + 1))
            start = None
    return runs


def _keys(socket, frames):
    for frame, value in frames:
        socket.default_value = value
        socket.keyframe_insert('default_value', frame=frame)
    tree = socket.id_data
    for curve in _fcurves(tree):
        for point in curve.keyframe_points:
            point.interpolation = 'LINEAR'


def _fcurves(tree):
    action = tree.animation_data.action
    if hasattr(action, 'layers') and action.layers:
        slot = tree.animation_data.action_slot
        return [c for layer in action.layers for strip in layer.strips
                for c in (strip.channelbag(slot).fcurves if strip.channelbag(slot) else [])]
    return list(action.fcurves)
