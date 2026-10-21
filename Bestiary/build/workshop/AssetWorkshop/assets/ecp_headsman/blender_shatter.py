"""The thrown axe breaking into its own pieces, and the pieces flying into a new skeleton, for the headsman scene
(blender_scene.py).

pieces(mesh): the axe (the thrown axe's mesh, in the model's own coordinates: Z up the haft, the blade out along +X)
split into what it is made of. The mesh is some three hundred islands: each vertebra of the haft is a dozen parts
(body, arches, processes, the joint that interlocks it), so the parts are grouped by the vertebra they sit at into
22 vertebra pieces; the blade is one big island, fractured into shards (Voronoi cells: cut by the plane halfway to
each other seed, the cut capped); the socket and hooked back at the top are split in two.

animate(...): for each summon (sequence.json), the pieces start where the axe was on its last frame in the air, fly
apart with a little of its speed and a burst of their own, fall, bounce and lie; just before the skeleton begins to
form (at the moment the boss's new axe begins to form: the two come into being together) they lift, in their own
colour, into a swirl round the spot, and one at a time each darts into the skeleton where a bone is just appearing
(blender_reveal.skeleton fades them in from the feet up) and is gone: the skeleton is built out of the axe's bones.
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

FPS = 30
VERTEBRAE, FIRST, STEP = 22, 0.065, 1.102 / 21     # the haft's vertebrae (ecp_bone_greataxe/model.py)
TOP = 1.19             # above this the socket and hooked back
BLADE_SEEDS = [Vector(v) for v in ((0.16, 0, 0.98), (0.42, 0, 0.95), (0.26, 0, 1.2), (0.47, 0, 1.28), (0.22, 0, 1.42))]
BACK_SEEDS = [Vector(v) for v in ((-0.14, 0, 1.25), (0.0, 0, 1.25))]


def pieces(mesh):
    """[(name, mesh, centre)] for every piece of the axe, each mesh centred on its own middle."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    groups = {}
    for faces in _islands(bm):
        groups.setdefault(_group(faces), []).extend(f.index for f in faces)
    made = []
    for key, faces in sorted(groups.items()):
        seeds = BLADE_SEEDS if key == 'blade' else BACK_SEEDS if key == 'back' else None
        if seeds is None:
            made.append(_piece(f'piece_{key}', _subset(bm, faces)))
            continue
        for i, seed in enumerate(seeds):
            cell = _cell(_subset(bm, faces), seed, seeds)
            if cell.faces:
                made.append(_piece(f'piece_{key}_{i}', cell))
    bm.free()
    return made


def _islands(bm):
    bm.faces.index_update()
    bm.faces.ensure_lookup_table()
    seen, out = set(), []
    for face in bm.faces:
        if face.index in seen:
            continue
        stack, group = [face], []
        seen.add(face.index)
        while stack:
            f = stack.pop()
            group.append(f)
            for edge in f.edges:
                for g in edge.link_faces:
                    if g.index not in seen:
                        seen.add(g.index)
                        stack.append(g)
        out.append(group)
    return out


def _group(faces):
    """'blade' for the blade, 'back' for the socket and hook at the top, else the vertebra it sits at."""
    if len(faces) > 500:
        return 'blade'
    verts = {v for f in faces for v in f.verts}
    z = sum(v.co.z for v in verts) / len(verts)
    if z > TOP:
        return 'back'
    return f'vertebra_{min(VERTEBRAE - 1, max(0, round((z - FIRST) / STEP))):02d}'


def _subset(bm, keep):
    part = bm.copy()
    part.faces.index_update()
    part.faces.ensure_lookup_table()
    keep = set(keep)
    bmesh.ops.delete(part, geom=[f for f in part.faces if f.index not in keep], context='FACES')
    return part


def _cell(part, seed, seeds):
    """What of `part` is nearer `seed` than any other seed, the cuts capped."""
    for other in seeds:
        if other == seed:
            continue
        normal = (other - seed).normalized()
        geom = part.verts[:] + part.edges[:] + part.faces[:]
        bmesh.ops.bisect_plane(part, geom=geom, plane_co=(seed + other) / 2, plane_no=normal, clear_outer=True)
    open_edges = [e for e in part.edges if e.is_boundary]
    if open_edges:
        bmesh.ops.holes_fill(part, edges=open_edges, sides=0)
    return part


def _piece(name, part):
    centre = sum((v.co for v in part.verts), Vector()) / max(1, len(part.verts))
    bmesh.ops.translate(part, vec=-centre, verts=part.verts)
    mesh = bpy.data.meshes.new(name)
    part.to_mesh(mesh)
    part.free()
    return name, mesh, centre


def animate(made, material, axe_matrices, summon, keyer):
    """One summon's pieces. `axe_matrices` is the thrown axe's world matrix per baked frame; summon['hit'] the baked
    frame it hit on (the axe is gone from it), so the pieces show from Blender frame hit + 1; summon['form'] the Blender
    frame the skeleton begins to form, ['spot'] where it stands, ['landings'] and ['targets'] when and where each piece
    strikes into it."""
    last, before = axe_matrices[summon['hit'] - 1], axe_matrices[summon['hit'] - 2]
    velocity = (last.translation - before.translation) * FPS
    hit = summon['hit'] + 1
    rng = random.Random(hit)
    for i, (name, mesh, centre) in enumerate(made):
        obj = bpy.data.objects.new(f'{name}_{summon["index"]}', mesh)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.clear()
        obj.data.materials.append(material)
        obj.rotation_mode = 'QUATERNION'
        start = last @ Matrix.Translation(centre)
        timing = (hit, max(hit + 12, summon['form'] - 8) + rng.randint(0, 6), summon['landings'][i] - STRIKE)
        keyer(obj, _path(start, velocity, Vector(summon['spot']), summon['targets'][i], timing, rng))


STRIKE = 5             # frames a piece takes to dart from its orbit into the skeleton


def landings(count, first, final, seed):
    """The frame each of `count` pieces strikes, one at a time in a random order, spread from `first` to `final`."""
    order = list(range(count))
    random.Random(seed).shuffle(order)
    frames = [0] * count
    for k, i in enumerate(order):
        frames[i] = round(first + k * (final - first) / max(1, count - 1))
    return frames


def _path(start, velocity, spot, target, timing, rng):
    """Every frame's location, rotation and size: hidden; scattered and lying; lifting into a swirl round the forming
    skeleton; darting into it at its strike; gone."""
    hit, lift, strike = timing
    location, rotation, size = start.decompose()
    speed = velocity * 0.12 + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)).normalized() * rng.uniform(1.5, 3.5)
    speed.z += rng.uniform(1.5, 3.5)
    spin = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * 12.0
    keys = {'location': [[] for _ in range(3)], 'rotation_quaternion': [[] for _ in range(4)], 'scale': [[] for _ in range(3)]}
    _add(keys, hit - 1, location, rotation, Vector((0, 0, 0)))
    for frame in range(hit, lift):
        location, rotation, speed, spin = _fall(location, rotation, speed, spin, size)
        _add(keys, frame, location, rotation, size)
    orbit = _orbit(location, spot, rng)
    tumble = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized()
    at = location
    for frame in range(lift, strike):
        t = (frame - lift) / FPS
        s = min(1.0, t / 0.5)
        at = location.lerp(orbit(t), s * s * (3 - 2 * s)) + Vector((0, 0, 0.3 * math.sin(math.pi * s)))
        _add(keys, frame, at, Quaternion(tumble, 4.0 * t) @ rotation, size)
    for step in range(STRIKE + 1):
        u = (step / STRIKE) ** 2
        _add(keys, strike + step, at.lerp(target, u), rotation, size * (1.0 - 0.3 * u))
    _add(keys, strike + STRIKE + 1, target, rotation, Vector((0, 0, 0)))
    return keys


def _orbit(rest, spot, rng):
    """A circle round the skeleton's upright line, entered where the piece lies, tightening a little as it turns."""
    radius, height = rng.uniform(0.7, 1.1), rng.uniform(0.25, 1.9)
    speed, start = rng.uniform(3.2, 4.4), math.atan2(rest.y - spot.y, rest.x - spot.x)

    def at(t):
        r = radius * (1.0 - 0.3 * min(1.0, t / 2.0))
        a = start + speed * t
        return Vector((spot.x + r * math.cos(a), spot.y + r * math.sin(a), height + 0.08 * math.sin(5 * t + start)))
    return at


def _fall(location, rotation, speed, spin, size):
    """One frame of a falling piece: gravity, a bounce off the ground that loses most of its speed, then at rest."""
    dt = 1.0 / FPS
    floor = 0.03 * size.x
    if location.z <= floor + 1e-4 and speed.length < 0.4:
        return location, rotation, Vector((0, 0, 0)), Vector((0, 0, 0))
    speed = speed + Vector((0, 0, -9.8 * dt))
    location = location + speed * dt
    if spin.length > 1e-4:
        rotation = Quaternion(spin.normalized(), spin.length * dt) @ rotation
    if location.z < floor:
        location.z = floor
        speed = Vector((speed.x * 0.45, speed.y * 0.45, -speed.z * 0.3))
        spin = spin * 0.5
    return location, rotation, speed, spin


def _add(keys, frame, location, rotation, size):
    for path, value in (('location', location), ('rotation_quaternion', rotation), ('scale', size)):
        for i, v in enumerate(value):
            keys[path][i] += [frame, v]
