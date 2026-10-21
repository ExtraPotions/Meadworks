"""Skin weights for a body on a game skeleton (gamerig.py): Blender's automatic weights as a start, and explicit ones
for what heat diffusion gets wrong (rigid horns and plates, a jaw, a limb blended exactly at its joints).

    gamerig_weights.auto(body, rig)                                  # heat weights from every deform bone
    gamerig_weights.rigid(tusk, "Jaw")                               # one bone, weight 1
    gamerig_weights.fixed(lip, {"Head": 0.5, "Jaw": 0.5})            # fixed weights
    gamerig_weights.blend(neck, rig, "Neck", "Head", 0.3, 0.8)       # slides from Neck to Head along the neck
    gamerig_weights.chain(arm, rig, ["LeftArm", "LeftForeArm", "LeftHand"], blend_m=0.05)
    gamerig_weights.region(body, (0, -0.08, 1.82), (0.17, 0.2, 0.14), {"Head": 1.0})   # soft-edged rigid skull
    gamerig_weights.smooth(body)                                     # Blender's weight smoothing
    gamerig_weights.finish(body, rig)                                # four strongest, normalised, deform bones only

Every function takes `where`, a test on a vertex's world position (a mathutils Vector) or a set of vertex indices, to
change only some vertices; the others keep what they had. Vertex groups are named after the bones, as Blender's armature modifier expects.
"""
import bpy
from mathutils.geometry import intersect_point_line

from . import gamerig, scene

LIMIT = 4                    # bones a vertex may follow: Unity's default skin quality


def rigid(obj, bone, where=None):
    """The vertices (all, or those `where` accepts) follow one bone only."""
    _assign(obj, {i: {bone: 1.0} for i in _chosen(obj, where)})


def fixed(obj, weights, where=None):
    """The vertices follow these bones by these weights ({bone: weight}, normalised later by finish())."""
    _assign(obj, {i: dict(weights) for i in _chosen(obj, where)})


def region(obj, centre, radii, weights, soft=0.3, where=None):
    """Vertices inside an ellipsoid (centre and half-axes in metres, world) follow `weights` ({bone: weight}); across
    the outer `soft` part of it they fade back into the weights they had, so a rigid skull or jaw blends into a
    neck instead of tearing from it."""
    table = {}
    for i, co in _positions(obj, where):
        d = sum(((co[k] - centre[k]) / radii[k]) ** 2 for k in range(3)) ** 0.5
        f = 1.0 - _smooth((d - (1.0 - soft)) / max(soft, 1e-6))
        if f > 0.0:
            had = _weights_of(obj, i)
            table[i] = {b: (1 - f) * had.get(b, 0.0) + f * weights.get(b, 0.0) for b in set(had) | set(weights)}
    _assign(obj, table)


def smooth(obj, factor=0.5, repeat=2, where=None):
    """Weight smoothing over every group: each pass moves a vertex's weights `factor` of the way to its neighbours'
    mean; softens the steps heat diffusion leaves at shoulders and hips."""
    chosen = set(_chosen(obj, where))
    near = {i: set() for i in chosen}
    for edge in obj.data.edges:
        a, b = edge.vertices
        for x, y in ((a, b), (b, a)):
            if x in near:
                near[x].add(y)
    table = {i: _weights_of(obj, i) for i in range(len(obj.data.vertices))}
    for _ in range(repeat):
        table.update({i: _toward(table[i], [table[j] for j in near[i]], factor) for i in chosen if near[i]})
    _assign(obj, {i: table[i] for i in chosen})


def _toward(own, others, factor):
    bones = set(own).union(*others)
    return {b: (1 - factor) * own.get(b, 0.0) + factor * sum(o.get(b, 0.0) for o in others) / len(others) for b in bones}


def blend(obj, rig, a, b, start=0.0, end=1.0, where=None):
    """Weights sliding from bone `a` to bone `b` along the line from a's head to b's head (0 at a's head, 1 at b's):
    all `a` before `start`, all `b` past `end`, a smooth step between."""
    p, q = gamerig.head(rig, a), gamerig.head(rig, b)
    weights = {}
    for i, co in _positions(obj, where):
        t = intersect_point_line(co, p, q)[1]
        w = _smooth((t - start) / max(end - start, 1e-6))
        weights[i] = {a: 1.0 - w, b: w}
    _assign(obj, weights)


def chain(obj, rig, bones, blend_m=0.05, where=None):
    """A limb, tail or neck along a chain of bones: each vertex goes to the bone whose stretch of the chain it lies
    beside (bone heads, then the last bone's tail), blended over `blend_m` metres either side of every joint."""
    points = [gamerig.head(rig, b) for b in bones] + [rig.matrix_world @ rig.data.bones[bones[-1]].tail_local]
    joints = [0.0]
    for p, q in zip(points, points[1:]):
        joints.append(joints[-1] + (q - p).length)
    weights = {}
    for i, co in _positions(obj, where):
        weights[i] = _along(bones, joints, _arc(points, joints, co), blend_m)
    _assign(obj, weights)


def auto(obj, rig, bones=None):
    """Blender's automatic (bone heat) weights from these deform bones only (default: every deform bone); the object
    ends up parented to the rig with an armature modifier, as the build expects."""
    keep = set(bones or gamerig.deform_bones(rig))
    saved = {b.name: b.use_deform for b in rig.data.bones}
    for bone in rig.data.bones:
        bone.use_deform = saved[bone.name] and bone.name in keep
    scene.select_only([obj, rig], active=rig)
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    for bone in rig.data.bones:
        bone.use_deform = saved[bone.name]
    missing = unweighted(obj, rig)
    if missing:
        nearest(obj, rig, keep, where=set(missing))
    return len(missing)


def nearest(obj, rig, bones=None, where=None):
    """Each vertex on the bone whose segment (head to tail) is nearest: a plain fallback where heat diffusion fails."""
    names = list(bones or gamerig.deform_bones(rig))
    segments = [(n, gamerig.head(rig, n), rig.matrix_world @ rig.data.bones[n].tail_local) for n in names]
    weights = {}
    for i, co in _positions(obj, where):
        weights[i] = {min(segments, key=lambda s: _distance(co, s[1], s[2]))[0]: 1.0}
    _assign(obj, weights)


def finish(obj, rig, limit=LIMIT):
    """Keeps only deform-bone groups, the `limit` strongest weights a vertex has, normalised to sum 1; returns the
    number of vertices left without any weight (the export refuses those)."""
    deform = set(gamerig.deform_bones(rig))
    names = {g.index: g.name for g in obj.vertex_groups}
    table = {}
    for v in obj.data.vertices:
        pairs = sorted(((g.weight, names[g.group]) for g in v.groups if names[g.group] in deform and g.weight > 1e-4),
                       reverse=True)[:limit]
        total = sum(w for w, _ in pairs)
        table[v.index] = {n: w / total for w, n in pairs} if total > 0 else {}
    for group in [g for g in obj.vertex_groups if g.name not in deform]:
        obj.vertex_groups.remove(group)
    _assign(obj, table)
    return sum(1 for w in table.values() if not w)


def unweighted(obj, rig):
    """Indices of vertices with no weight on any deform bone."""
    deform = set(gamerig.deform_bones(rig))
    names = {g.index: g.name for g in obj.vertex_groups}
    return [v.index for v in obj.data.vertices
            if not any(names[g.group] in deform and g.weight > 1e-4 for g in v.groups)]


def _weights_of(obj, index):
    names = {g.index: g.name for g in obj.vertex_groups}
    return {names[g.group]: g.weight for g in obj.data.vertices[index].groups}


def _positions(obj, where):
    """(index, world position) of the vertices `where` accepts: a test on the position, a set of indices, or None."""
    matrix = obj.matrix_world
    for v in obj.data.vertices:
        co = matrix @ v.co
        if where is None or (v.index in where if isinstance(where, (set, frozenset)) else where(co)):
            yield v.index, co


def _chosen(obj, where):
    return [i for i, _ in _positions(obj, where)]


def _assign(obj, weights):
    """Replaces the listed vertices' weights: {vertex index: {bone: weight}}; other vertices are untouched."""
    if not weights:
        return
    indices = list(weights)
    for group in obj.vertex_groups:
        group.remove(indices)
    for index, bones in weights.items():
        for bone, weight in bones.items():
            if weight > 1e-4:
                group = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
                group.add([index], weight, 'REPLACE')


def _arc(points, joints, co):
    """How far along the chain (metres from its start) the point nearest `co` lies."""
    best = None
    for k, (p, q) in enumerate(zip(points, points[1:])):
        closest, t = intersect_point_line(co, p, q)
        t = min(1.0, max(0.0, t))
        distance = (co - (p + (q - p) * t)).length
        if best is None or distance < best[0]:
            best = (distance, joints[k] + t * (joints[k + 1] - joints[k]))
    return best[1]


def _along(bones, joints, s, blend_m):
    """{bone: weight} at arc length s: the bone of that stretch, blended with a neighbour near a joint."""
    k = max(0, min(len(bones) - 1, next((j for j in range(len(bones)) if s < joints[j + 1]), len(bones) - 1)))
    if k > 0 and s - joints[k] < blend_m:
        w = _smooth((s - joints[k] + blend_m) / (2 * blend_m))
        return {bones[k - 1]: 1.0 - w, bones[k]: w}
    if k < len(bones) - 1 and joints[k + 1] - s < blend_m:
        w = _smooth((s - joints[k + 1] + blend_m) / (2 * blend_m))
        return {bones[k]: 1.0 - w, bones[k + 1]: w}
    return {bones[k]: 1.0}


def _distance(co, p, q):
    closest, t = intersect_point_line(co, p, q)
    return (co - (p + (q - p) * min(1.0, max(0.0, t)))).length


def _smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3.0 - 2.0 * x)


