"""The game's Skeleton for the showcase, posable: its mesh (reference only, never exported) bound to an armature built
from its own bones and skin weights, the way assets/ecp_bone_reaver/body.py first did it.

The armature's rest pose is the prefab's T-pose, bones named as the game names them; the mesh keeps its game
materials. Blender axes: the skeleton faces -Y, its left hand is +X.
"""
import bpy
from mathutils import Matrix, Vector

from workshop import prefab, prefab_parts, scene, unity

SOURCE = 'Characters/Skeleton/Skeleton.prefab'
SKINNED = 137                                  # Unity class id of SkinnedMeshRenderer
TRANSFORM = 4


_FIRST = []


def skeleton(name, location=(0.0, 0.0, 0.0)):
    """A posable game Skeleton: (armature, body mesh). The armature stands at `location`. The prefab is loaded once;
    later skeletons are copies of the first (a second load reuses the first's posed mesh data and comes out wrong)."""
    if _FIRST:
        rig, body = _copy(*_FIRST, name)
    else:
        tree = prefab._Prefab(SOURCE)
        loaded = prefab.load(SOURCE, root_name=name + "_prefab")
        low, _ = prefab.bounds(prefab.meshes(loaded))
        lift = Vector((0.0, 0.0, -low.z))
        rig = _armature(tree, name, lift)
        body = _body(tree, loaded, rig, lift, name)
        _FIRST.extend((rig, body))
    rig.location = location
    return rig, body


def _copy(rig, body, name):
    """A fresh copy of a rigged skeleton, in its rest pose, with no constraints."""
    new_rig = rig.copy()
    new_rig.data = rig.data.copy()
    new_rig.name = new_rig.data.name = name
    new_rig.location = (0.0, 0.0, 0.0)
    for bone in new_rig.pose.bones:
        for c in list(bone.constraints):
            bone.constraints.remove(c)
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.collection.objects.link(new_rig)
    new_body = body.copy()
    new_body.data = body.data.copy()
    new_body.name = name + "_body"
    new_body.parent = new_rig
    new_body.modifiers['armature'].object = new_rig
    bpy.context.collection.objects.link(new_body)
    return new_rig, new_body


def _bones(tree):
    """Transform ids from Hips down, in parent-first order (the eye smoke left out)."""
    hips = next(i for i, (kind, _) in tree.docs.items() if kind == TRANSFORM and tree.name(i) == 'Hips')
    ids = []

    def descend(i):
        if tree.name(i) != 'evil_smoke':
            ids.append(i)
            for child in tree.children(i):
                descend(child)
    descend(hips)
    return ids


def _armature(tree, name, lift):
    ids = _bones(tree)
    heads = {tree.name(i): tree.world(i).translation + lift for i in ids}
    data = bpy.data.armatures.new(name)
    rig = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(rig)
    scene.select_only([rig])
    bpy.ops.object.mode_set(mode='EDIT')
    for i in ids:
        _edit_bone(tree, data, i, ids, heads)
    bpy.ops.object.mode_set(mode='OBJECT')
    data.display_type = 'STICK'
    rig.show_in_front = True
    for bone in rig.pose.bones:
        bone.rotation_mode = 'QUATERNION'
    return rig


def _edit_bone(tree, data, i, ids, heads):
    """A bone from the transform's position to its first child's (or a short stub), parented as in the prefab."""
    name = tree.name(i)
    bone = data.edit_bones.new(name)
    bone.head = heads[name]
    children = [c for c in tree.children(i) if c in ids]
    along = {'Hips': 'Spine', 'Spine2': 'Neck', 'Head': 'Helmet_attach', 'LeftHand': 'LeftHandMiddle1',
             'RightHand': 'RightHandMiddle1'}
    tail = heads[along[name]] if name in along else (heads[tree.name(children[0])] if children else None)
    if tail is None or (tail - bone.head).length < 0.005:
        tail = bone.head + Vector((0, 0, 0.05))
    bone.tail = tail
    father = unity.ref(unity.field(tree.docs[i][1], 'm_Father'))[0]
    if father in ids:
        bone.parent = data.edit_bones[tree.name(father)]
        bone.use_connect = False


def _body(tree, loaded, rig, lift, name):
    """The skinned mesh, in rest position, weighted by the game's own blend weights and deformed by the armature."""
    renderer_id = next(i for i, (kind, _) in tree.docs.items()
                       if kind == TRANSFORM and tree.is_local(i) and SKINNED in tree.components(i))
    renderer = tree.components(renderer_id)[SKINNED][1]
    path = unity.asset_path(unity.ref(unity.field(renderer, 'm_Mesh'))[1])
    body = next(o for o in prefab.meshes(loaded) if o.data.name.startswith('Skeleton'))
    bpy.context.view_layer.update()
    world = body.matrix_world.copy()
    body.parent = None
    body.data.transform(Matrix.Translation(lift) @ world)
    body.matrix_world = Matrix.Identity(4)
    body.name = name + "_body"
    _weights(body, tree, renderer, path)
    body.parent = rig
    body.modifiers.new('armature', 'ARMATURE').object = rig
    for obj in [loaded] + list(loaded.children_recursive):
        if obj is not body:
            bpy.data.objects.remove(obj, do_unlink=True)
    return body


def _weights(body, tree, renderer, path):
    raw = prefab_parts._vertex_data(unity.read(path))
    weights = prefab_parts._channel(raw, prefab_parts.BLEND_WEIGHT)
    indices = prefab_parts._channel(raw, prefab_parts.BLEND_INDICES)
    names = [tree.name(unity.ref(item)[0]) for item in unity.items(renderer, 'm_Bones')]
    body.vertex_groups.clear()
    groups = [body.vertex_groups.new(name=n) for n in names]
    for vertex, (ws, bs) in enumerate(zip(weights, indices)):
        total = float(sum(ws)) or 1.0
        for w, b in zip(ws, bs):
            if w > 0:
                groups[int(b)].add([vertex], float(w) / total, 'REPLACE')
