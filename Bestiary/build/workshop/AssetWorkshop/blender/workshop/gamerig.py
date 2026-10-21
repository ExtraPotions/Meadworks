"""Route (b) of codex/rigs/README.md: a new body on a game skeleton. The body is modelled and weighted in Blender on an
exact copy of one of the game's skeletons, so the game's own avatar, animator controller and clips play it unchanged,
and a mod puts it on a copy of the game creature (BundlePrefabs.CreatureBody).

    rig = gamerig.armature("Characters/Skeleton/Skeleton.prefab")      # the game prefab, not a model prefab
    gamerig.head(rig, "LeftForeArm")                                  # a bone's rest position (Blender, metres)
    gamerig.socket(rig, "Mouth", "Jaw", (0, -0.12, 1.62))             # a new socket the mod can find by name

The copy: every bone, socket and end below the game's armature, with the game's names, hierarchy and parents; each
bone's head at the game's rest position and its axes the game bone's own (up to one fixed turn per skeleton, `axis`:
Blender bones lie along their Y, the game's lie along Unity Y on Blender-made rigs such as the player's and the
Skeleton's, along X on the Wolf's; `frame()` gives the game's own axes back).
The armature object stays at the origin at scale 1 in metres; the game's armature transform (the Skeleton's 100 under
a 0.95, turned -90 degrees about X) is kept in the contract, which is written from the prefab's own numbers
(gamerig_export). Facing is the workshop's: the creature looks down -Y (Unity's +Z).

Weights: gamerig_weights (automatic, rigid, blends, chains). Build and export: gamerig_build (gamerig_run.py is the
command line). Nothing of the game's is exported: the skeleton's layout goes into the contract as names and numbers.
"""
import json

import bpy
from mathutils import Matrix, Vector

from . import gamerig_codex, gamerig_source, scene

AXES = {"Y": Matrix.Identity(3), "-Y": Matrix.Rotation(3.14159265358979, 3, 'Z'),
        "X": Matrix.Rotation(-1.5707963267949, 3, 'Z'), "-X": Matrix.Rotation(1.5707963267949, 3, 'Z'),
        "Z": Matrix.Rotation(1.5707963267949, 3, 'X'), "-Z": Matrix.Rotation(-1.5707963267949, 3, 'X')}
UNITY_AXIS = {"X": "-X", "-X": "X", "Y": "-Z", "-Y": "Z", "Z": "Y", "-Z": "-Y"}   # a local axis in the Blender frame
KIND, PATH, NEW = "gamerig_kind", "gamerig_path", "gamerig_new"
_SKELETONS = {}


def armature(source, name="gamerig"):
    """An armature that is an exact copy of the game prefab's skeleton (see the module doc), cross-checked against the
    codex's rigs.json; the body's bones deform, sockets and ends do not."""
    skeleton = gamerig_source.Skeleton(source)
    found = gamerig_codex.check(skeleton)
    axis = bone_axis(skeleton)
    rig = bpy.data.objects.new(name, bpy.data.armatures.new(name))
    bpy.context.scene.collection.objects.link(rig)
    scene.select_only([rig])
    bpy.ops.object.mode_set(mode='EDIT')
    lengths = {}
    for node in skeleton.nodes:
        _edit_bone(rig.data, skeleton, node, axis, lengths)
    bpy.ops.object.mode_set(mode='OBJECT')
    _tag(rig, skeleton)
    rig["gamerig_source"], rig["gamerig_axis"], rig["gamerig_codex"] = source, axis, json.dumps(found)
    rig.data.display_type, rig.show_in_front = 'STICK', True
    _SKELETONS[source] = skeleton
    print(f"WORKSHOP gamerig: {source}, {len(skeleton.bones)} bones along their Unity {UNITY_AXIS[axis]} axis, "
          f"codex {found}", flush=True)
    return rig


def skeleton_of(rig):
    """The game skeleton (gamerig_source.Skeleton) the rig was copied from."""
    source = rig["gamerig_source"]
    if source not in _SKELETONS:
        _SKELETONS[source] = gamerig_source.Skeleton(source)
    return _SKELETONS[source]


def head(rig, bone):
    """A bone's (or socket's) rest position, Blender world, metres."""
    return rig.matrix_world @ rig.data.bones[bone].head_local


def frame(rig, bone):
    """A bone's rest frame as the game has it (its own axes, not Blender's bone axes), Blender world, no scale."""
    turn = AXES[rig["gamerig_axis"]].to_4x4().inverted()
    return rig.matrix_world @ rig.data.bones[bone].matrix_local @ turn


def deform_bones(rig):
    """The names of the bones that deform (the game body's bones), in the armature's order."""
    return [b.name for b in rig.data.bones if b.use_deform]


def socket(rig, name, parent, location, rotation=None, length=0.04):
    """A new socket: a bone that never deforms, under `parent`, at `location` (Blender world, metres), in the frame
    `rotation` (a Blender-axes 3x3 or quaternion; default the creature's own axes at rest, as a held item expects: Unity
    +Z forward is Blender -Y, up is +Z). The mod finds it by name; BundlePrefabs adds it to the game creature."""
    if name in rig.data.bones:
        raise SystemExit(f"gamerig: {name} already exists on the skeleton")
    turn = _rotation(rotation)
    scene.select_only([rig])
    bpy.ops.object.mode_set(mode='EDIT')
    bone = rig.data.edit_bones.new(name)
    bone.head, bone.tail = (0, 0, 0), (0, length, 0)
    bone.matrix = Matrix.LocRotScale(Vector(location), (turn @ AXES[rig["gamerig_axis"]]).to_quaternion(), None)
    bone.parent, bone.use_deform, bone.use_connect = rig.data.edit_bones[parent], False, False
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.data.bones[name][KIND], rig.data.bones[name][NEW] = "socket", True
    return rig.data.bones[name]


def _rotation(value):
    """A Blender-axes 3x3 rotation from None (identity), a Quaternion or Euler, or a matrix."""
    if value is None:
        return Matrix.Identity(3)
    return value.to_matrix() if hasattr(value, "to_matrix") else Matrix(value).to_3x3()


def new_sockets(rig):
    """The names of the sockets made with socket(), which the game creature does not have."""
    return [b.name for b in rig.data.bones if b.get(NEW)]


def bone_axis(skeleton):
    """The local axis, in the Blender-axes frame, the game's bones lie along ('Z' there is Unity's Y, as on the
    Blender-made rigs), by a vote over the bones with children."""
    votes = {}
    for node in skeleton.nodes:
        axis = _axis_to_child(skeleton, node)
        if axis:
            votes[axis] = votes.get(axis, 0) + 1
    return max(votes, key=votes.get) if votes else "Y"


def _axis_to_child(skeleton, node):
    """The local axis pointing closest to one of the node's kept children, or None when it has none."""
    world = skeleton.world(node)
    rotation = world.to_quaternion().to_matrix()
    best, best_cos = None, 0.9
    for child in (c for c in skeleton.tree.children(node) if c in skeleton.nodes):
        along = skeleton.world(child).translation - world.translation
        if along.length < 1e-4:
            continue
        for axis, turn in AXES.items():
            cos = (rotation @ turn @ Vector((0, 1, 0))).dot(along.normalized())
            if cos > best_cos:
                best, best_cos = axis, cos
    return best


def _edit_bone(data, skeleton, node, axis, lengths):
    """A bone at the game transform's rest place and axes, as long as the way to the child it points at."""
    world = skeleton.world(node)
    rotation = world.to_quaternion().to_matrix() @ AXES[axis]
    name = skeleton.name(node)
    lengths[node] = _length(skeleton, node, rotation, lengths)
    bone = data.edit_bones.new(name)
    bone.head, bone.tail = (0, 0, 0), (0, lengths[node], 0)
    bone.matrix = Matrix.LocRotScale(world.translation, rotation.to_quaternion(), None)
    parent = skeleton.parent(node)
    if parent in skeleton.nodes:
        bone.parent = data.edit_bones[skeleton.name(parent)]
    bone.use_connect, bone.use_deform = False, node in skeleton.bones


def _length(skeleton, node, rotation, lengths):
    """Distance along the bone to the child it points at most directly; a leaf gets half its parent's, 2 to 8 cm."""
    origin, along = skeleton.world(node).translation, rotation @ Vector((0, 1, 0))
    best, reach = 0.9, None
    for child in (c for c in skeleton.tree.children(node) if c in skeleton.nodes):
        offset = skeleton.world(child).translation - origin
        if offset.length > 0.005 and offset.normalized().dot(along) > best:
            best, reach = offset.normalized().dot(along), offset.dot(along)
    if reach:
        return reach
    return max(0.02, min(0.08, lengths.get(skeleton.parent(node), 0.08) * 0.5))


def _tag(rig, skeleton):
    for node in skeleton.nodes:
        bone = rig.data.bones[skeleton.name(node)]
        bone[KIND], bone[PATH] = skeleton.kind(node), skeleton.path(node)
