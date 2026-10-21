"""Writes a body on a game skeleton for Unity (unity/Assets/Editor/GameRig) as one JSON file in Unity's axes, the way
the Kraken is exported: no importer settings stand between the contract and the prefab.

- `transforms`: the game's own transforms from Visual down to every bone, socket and end, and the body renderer, each
  with its path, parent and Unity's exact local position, rotation and scale, copied as numbers from the game prefab;
  then the new sockets, their local transforms worked out under their game parents (a unit-scale frame in metres).
- `bones`: the body mesh's bone order: the game body's own (`BONE_ORDER = "game"`, so the game's attach_skin gear fits
  it) or the deform bones in hierarchy order ("own").
- `rest`: where Blender's armature has every bone and socket, root space, for the check.
- `mesh`: vertices split where normals or UVs differ, root space in Unity's axes ((x, y, z) = (-bx, bz, -by),
  triangles rewound for the mirror), four bone weights each; Unity moves it into the renderer's own space.
- `reference`: the game assets the Unity preview stages (never bundled): prefab, avatar, controller and its clips.
"""
import json
import os

from . import gamerig, gamerig_source, prefab_parts, scene

C = prefab_parts.UNITY_TO_BLENDER


def contract(name, rig, body, textures, settings):
    """The whole contract as a dict (see the module doc)."""
    skeleton = gamerig.skeleton_of(rig)
    order = skeleton.order if settings.get("BONE_ORDER", "game") == "game" else _own_order(rig, skeleton)
    data = {"asset": name, "source": skeleton.source, "codex": json.loads(rig["gamerig_codex"]),
            "category": settings.get("CATEGORY") or "", "boneOrder": settings.get("BONE_ORDER", "game"),
            "transforms": _transforms(skeleton) + _sockets(rig, skeleton),
            "renderer": skeleton.path(skeleton.body_node), "rootBone": skeleton.root_bone, "bones": order,
            "sockets": gamerig.new_sockets(rig), "rest": _rest(rig), "mesh": _mesh(body, order),
            "material": dict(textures, name=name, smoothness=settings.get("SMOOTHNESS", 0.2)),
            "reference": _reference(skeleton), "triangles": scene.triangles(body)}
    return data


def write(path, data):
    """Saves the contract as compact JSON and logs what it holds."""
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(data, handle, separators=(",", ":"))
    print(f"WORKSHOP contract: {os.path.basename(path)}, {len(data['transforms'])} transforms, "
          f"{len(data['bones'])} bones ({data['boneOrder']} order), sockets {data['sockets']}", flush=True)


def to_unity(v):
    """A Blender-axes point or direction in Unity's axes."""
    return [-v[0], v[2], -v[1]]


def _own_order(rig, skeleton):
    deform = set(gamerig.deform_bones(rig))
    return [skeleton.name(n) for n in skeleton.nodes if skeleton.name(n) in deform]


def _transforms(skeleton):
    """Visual's chain, every kept transform below the armature and the body renderer, Unity's own numbers."""
    rows = []
    for node in skeleton.chain + skeleton.nodes:
        parent = skeleton.parent(node)
        position, rotation, scale = skeleton.local(node)
        rows.append({"path": skeleton.path(node), "name": skeleton.name(node),
                     "parent": "" if parent == skeleton.tree.root() else skeleton.path(parent),
                     "kind": "chain" if node in skeleton.chain else skeleton.kind(node),
                     "position": list(position), "rotation": list(rotation), "scale": list(scale)})
    return rows


def _sockets(rig, skeleton):
    """The new sockets, each under its game parent: local = parent's world inverted times the socket's frame."""
    paths = {skeleton.name(n): n for n in skeleton.nodes}
    rows = []
    for name in gamerig.new_sockets(rig):
        parent = rig.data.bones[name].parent.name
        if parent not in paths:
            raise SystemExit(f"gamerig: socket {name} must hang from a game bone, not {parent}")
        local = skeleton.world(paths[parent]).inverted() @ gamerig.frame(rig, name)
        position, rotation, scale = (C.inverted() @ local @ C).decompose()
        rows.append({"path": skeleton.path(paths[parent]) + "/" + name, "name": name,
                     "parent": skeleton.path(paths[parent]), "kind": "new", "position": list(position),
                     "rotation": [rotation.x, rotation.y, rotation.z, rotation.w], "scale": list(scale)})
    return rows


def _rest(rig):
    return [{"name": b.name, "position": [round(c, 6) for c in to_unity(gamerig.head(rig, b.name))]}
            for b in rig.data.bones]


def _mesh(obj, order):
    """Vertices split where normals or UVs differ, Unity axes and winding, weights as indices into `order`."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv, normals = mesh.uv_layers.active.data, mesh.corner_normals
    index, groups = {b: i for i, b in enumerate(order)}, {g.index: g.name for g in obj.vertex_groups}
    keys, remap, rows = {}, [], {"positions": [], "normals": [], "uvs": [], "boneIndex": [], "boneWeight": []}
    for loop in mesh.loops:
        v, n, t = loop.vertex_index, normals[loop.index].vector, uv[loop.index].uv
        key = (v, round(n.x, 4), round(n.y, 4), round(n.z, 4), round(t.x, 5), round(t.y, 5))
        if key not in keys:
            keys[key] = len(keys)
            _vertex(rows, mesh.vertices[v], n, t, groups, index)
        remap.append(keys[key])
    rows["triangles"] = [remap[i] for tri in mesh.loop_triangles for i in (tri.loops[0], tri.loops[2], tri.loops[1])]
    return rows


def _vertex(rows, vertex, normal, uv, groups, index):
    rows["positions"] += [round(c, 6) for c in to_unity(vertex.co)]
    rows["normals"] += [round(c, 5) for c in to_unity(normal)]
    rows["uvs"] += [round(uv.x, 6), round(uv.y, 6)]
    pairs = sorted(((g.weight, index[groups[g.group]]) for g in vertex.groups
                    if groups[g.group] in index and g.weight > 1e-4), reverse=True)[:4]
    if not pairs:
        raise SystemExit(f"gamerig: vertex {vertex.index} has no weight on a bone of the body's bone order")
    total = sum(w for w, _ in pairs)
    pairs += [(0.0, 0)] * (4 - len(pairs))
    rows["boneIndex"] += [i for _, i in pairs]
    rows["boneWeight"] += [round(w / total, 5) for w, _ in pairs]


def _reference(skeleton):
    """Game assets the Unity preview stages from the reference export; never bundled."""
    animator, body = skeleton.animator(), skeleton.body_assets()
    return {"prefab": skeleton.source, "animatorNode": animator["node"], "avatar": animator["avatar"],
            "controller": animator["controller"], "clips": gamerig_source.controller_clips(animator["controller"]),
            "bodyMesh": body["mesh"], "bodyMaterial": body["material"], "bodyAlbedo": body["albedo"]}

