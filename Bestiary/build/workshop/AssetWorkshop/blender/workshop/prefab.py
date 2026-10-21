"""Loads a whole game prefab from the local reference export (rip-reference.ps1) into Blender, for preview scenes only:
the longship a kraken attacks, a vanilla piece beside a new one. Everything made is tagged reference.TAG, so the
pipeline never joins or exports it and nothing of the game's reaches a bundle.

    root = prefab.load("GameElements/Ships/VikingShip.prefab")

gives an empty at the origin with the prefab's hierarchy under it: an empty for every GameObject on the way to
something drawn, and a mesh object for every drawn mesh, on the game's own textures. Local transforms are Unity's,
turned into the workshop's axes (Z up; Unity's forward +Z becomes -Y), so moving or animating any node moves its
part as in the game. The prefab root's own position and rotation are dropped (it sits at the origin); its scale stays.

Left out: inactive GameObjects (with their children) unless include_inactive, whatever skip(name, layer) rejects
(with its children), disabled renderers, renderers only in a lower LOD, materials on HIDDEN_SHADERS (the water mask,
shadow blobs), Unity's built-in meshes other than the cube, particle systems, lights and nested prefab instances.
Skinned meshes are posed by the prefab's own bone transforms and kept as static meshes. Line renderers (ropes) become
curve objects under the root, drawn through the points the game's LineAttach script would give them.
"""
import os
import re
import struct

import bpy
from mathutils import Matrix, Quaternion, Vector

from . import prefab_parts, reference, unity

HIDDEN_SHADERS = ("WaterMask", "ShadowBlob", "Invis", "DepthWrite", "BlitCameraDepth", "Distortion")
TRANSFORMS = (4, 224)                                   # Transform, RectTransform
MESH_FILTER, MESH_RENDERER, SKINNED_RENDERER = 33, 23, 137
LINE_RENDERER, SCRIPT, LOD_GROUP = 120, 114, 205


def load(prefab_path, root_name=None, include_inactive=False, skip=lambda go_name, layer: False):
    """prefab_path: under the reference Assets folder, e.g. 'GameElements/Ships/VikingShip.prefab'. skip(name, layer)
    returning True leaves that GameObject and its children out. Returns the root empty; every object made is also in a
    new collection named after it."""
    tree = _Prefab(prefab_path)
    chooser = _Chooser(tree, include_inactive, skip)
    chooser.visit(tree.root(), is_root=True)
    name = root_name or tree.name(tree.root())
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    builder = _Builder(tree, chooser.kept, collection)
    root = builder.build(tree.root(), None, name)
    for transform, renderer, materials in chooser.lines:
        builder.line(root, transform, renderer, materials)
    return root


def meshes(root):
    """The mesh objects under a loaded prefab."""
    return [o for o in root.children_recursive if o.type == 'MESH']


def bounds(objects):
    """World-space (min, max) over the vertices of the mesh objects given (or under the root given)."""
    if not isinstance(objects, (list, tuple)):
        objects = meshes(objects)
    bpy.context.view_layer.update()
    points = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


class _Chooser:
    """Walks the prefab and marks what to build: kept[transform] is the mesh part drawn there, or None for an empty on
    the way to one; lines are the ropes."""

    def __init__(self, tree, include_inactive, skip):
        self.tree, self.include_inactive, self.skip = tree, include_inactive, skip
        self.kept, self.lines = {}, []

    def visit(self, transform, is_root=False):
        if not self.tree.is_local(transform):
            print(f"prefab: {transform} belongs to a nested prefab instance, not loaded")
            return False
        name, layer = self.tree.name(transform), self.tree.layer(transform)
        active = unity.field(self.tree.game_object(transform), "m_IsActive") != "0"
        if not is_root and ((not active and not self.include_inactive) or self.skip(name, layer)):
            return False
        below = [self.visit(child) for child in self.tree.children(transform)]
        components = self.tree.components(transform)
        part = _mesh_part(self.tree, components)
        self._line(transform, components)
        if part is None and not any(below) and not is_root:
            return False
        self.kept[transform] = part
        return True

    def _line(self, transform, components):
        renderer = components.get(LINE_RENDERER)
        if renderer is None or not _drawn(self.tree, renderer):
            return
        materials = [_material_path(item) for item in unity.items(renderer[1], "m_Materials")]
        if any(materials) and unity.field(renderer[1], "m_UseWorldSpace") != "1":
            self.lines.append((transform, renderer[1], materials))


def _drawn(tree, renderer):
    return unity.field(renderer[1], "m_Enabled") != "0" and renderer[0] not in tree.lod_hidden


def _mesh_part(tree, components):
    """(mesh, material paths, skinned renderer body or None) drawn by these components, or None."""
    renderer = components.get(SKINNED_RENDERER) or components.get(MESH_RENDERER)
    if renderer is None or not _drawn(tree, renderer):
        return None
    materials = [_material_path(item) for item in unity.items(renderer[1], "m_Materials")]
    if not any(materials):
        return None
    skinned = SKINNED_RENDERER in components
    source = renderer[1] if skinned else components.get(MESH_FILTER, (None, ""))[1]
    mesh = _mesh_path(unity.field(source, "m_Mesh"))
    return (mesh, materials, renderer[1] if skinned else None) if mesh else None


def _material_path(item):
    """The .mat a renderer lists, or None for Unity's built-in materials and the game's invisible shaders."""
    _, guid = unity.ref(item)
    path = unity.asset_path(guid) if guid and guid != unity.BUILTIN_ASSETS else None
    if not path or not path.endswith(".mat"):
        return None
    shader = unity.material(path)["shader"]
    return None if any(os.path.basename(shader).startswith(h) for h in HIDDEN_SHADERS) else path


def _mesh_path(value):
    """A Mesh .asset path, 'builtin:cube' for Unity's cube, or None (other built-ins, meshes inside model files)."""
    file_id, guid = unity.ref(value)
    if guid == unity.BUILTIN_MESHES:
        return "builtin:cube" if file_id == prefab_parts.BUILTIN_CUBE else None
    path = unity.asset_path(guid) if guid else None
    return path if path and path.endswith(".asset") else None


class _Prefab:
    """The documents of one prefab file, with the lookups the loader needs."""

    def __init__(self, path):
        self.docs = unity.documents(path)
        self.lod_hidden = self._lod_hidden()
        self._world = {}

    def root(self):
        return next(i for i, (cls, body) in self.docs.items() if cls in TRANSFORMS and self.is_local(i)
                    and unity.ref(unity.field(body, "m_Father"))[0] == "0")

    def is_local(self, transform):
        """False for the stripped stand-ins a nested prefab instance leaves in the file."""
        return transform in self.docs and "m_GameObject:" in self.docs[transform][1]

    def game_object(self, transform):
        return self.docs[unity.ref(unity.field(self.docs[transform][1], "m_GameObject"))[0]][1]

    def name(self, transform):
        return unity.field(self.game_object(transform), "m_Name")

    def layer(self, transform):
        return int(unity.field(self.game_object(transform), "m_Layer") or 0)

    def children(self, transform):
        return [unity.ref(item)[0] for item in unity.items(self.docs[transform][1], "m_Children")]

    def components(self, transform):
        """{class ID: (fileID, body)} of the GameObject's components (the first of each class)."""
        found = {}
        for item in unity.items(self.game_object(transform), "m_Component"):
            file_id = unity.ref(item)[0]
            if file_id in self.docs:
                found.setdefault(self.docs[file_id][0], (file_id, self.docs[file_id][1]))
        return found

    def scripts(self, transform):
        """The bodies of every MonoBehaviour on the GameObject."""
        ids = [unity.ref(item)[0] for item in unity.items(self.game_object(transform), "m_Component")]
        return [self.docs[i][1] for i in ids if i in self.docs and self.docs[i][0] == SCRIPT]

    def local(self, transform):
        """(location, rotation, scale) in Blender axes: Unity (x, y, z) -> (-x, -z, y), and a rotation turns the other
        way about the mirrored axis, so its quaternion (x, y, z, w) -> (w, x, z, -y)."""
        body = self.docs[transform][1]
        px, py, pz = unity.numbers(unity.field(body, "m_LocalPosition"))
        qx, qy, qz, qw = unity.numbers(unity.field(body, "m_LocalRotation"))
        sx, sy, sz = unity.numbers(unity.field(body, "m_LocalScale"))
        return Vector((-px, -pz, py)), Quaternion((qw, qx, qz, -qy)), Vector((sx, sz, sy))

    def world(self, transform):
        """The transform's matrix relative to the prefab root (at the origin, its scale kept), Blender axes."""
        if transform not in self._world:
            location, rotation, scale = self.local(transform)
            father = unity.ref(unity.field(self.docs[transform][1], "m_Father"))[0]
            if father == "0" or not self.is_local(father):
                self._world[transform] = Matrix.LocRotScale(None, None, scale)
            else:
                self._world[transform] = self.world(father) @ Matrix.LocRotScale(location, rotation, scale)
        return self._world[transform]

    def _lod_hidden(self):
        """Renderers a LODGroup lists only below LOD0: lower-detail stand-ins the game swaps in at a distance."""
        hidden = set()
        for cls, body in self.docs.values():
            if cls == LOD_GROUP:
                levels = [set(re.findall(r"renderer: \{fileID: (-?\d+)\}", level))
                          for level in body.split("screenRelativeHeight")[1:]]
                if levels:
                    hidden |= set().union(*levels[1:]) - levels[0]
        return hidden


class _Builder:
    """Makes the Blender objects for the chosen transforms, parents them like the prefab and tags them."""

    def __init__(self, tree, kept, collection):
        self.tree, self.kept, self.collection = tree, kept, collection
        self.materials = {}

    def build(self, transform, parent, name=None):
        part = self.kept[transform]
        obj = self._object(name or self.tree.name(transform), self._data(transform, part), transform)
        if obj.type == 'EMPTY':
            obj.empty_display_type, obj.empty_display_size = 'PLAIN_AXES', 0.25
        obj.parent = parent
        location, rotation, scale = self.tree.local(transform)
        obj.rotation_mode = 'QUATERNION'
        obj.location, obj.rotation_quaternion, obj.scale = (location, rotation, scale) if parent else (
            Vector(), Quaternion(), scale)
        for child in self.tree.children(transform):
            if child in self.kept:
                self.build(child, obj)
        return obj

    def line(self, root, transform, renderer, material_paths):
        """A rope: a tube through the line's points, in the root's space (Unity's line width is in world units)."""
        into_root = self.tree.world(self.tree.root()).inverted() @ self.tree.world(transform)
        points = [into_root @ p for p in self._line_points(transform, renderer)]
        width = float(re.search(rf"widthMultiplier: ({unity.NUMBER})", renderer).group(1))
        curve_value = re.search(rf"widthCurve:.*?value: ({unity.NUMBER})", renderer, re.DOTALL)
        width *= float(curve_value.group(1)) if curve_value else 1.0
        closed = unity.field(renderer, "m_Loop") == "1"
        data = prefab_parts.line(self.tree.name(transform), points, width, closed,
                                 [self._material(p) for p in material_paths])
        obj = self._object(self.tree.name(transform), data, transform)
        obj.parent = root
        return obj

    def _line_points(self, transform, renderer):
        """The line's points in its own space (Blender axes), each replaced by its LineAttach transform if it has one."""
        block = renderer[renderer.index("m_Positions:"):renderer.index("m_Parameters:")]
        points = [Vector((-x, -z, y)) for x, y, z in (unity.numbers(v) for v in re.findall(r"- (\{.*?\})", block))]
        into_line = self.tree.world(transform).inverted()
        for script in self.tree.scripts(transform):
            for i, item in enumerate(unity.items(script, "m_attachments")[:len(points)]):
                attached = unity.ref(item)[0]
                if self.tree.is_local(attached):
                    points[i] = into_line @ self.tree.world(attached).translation
        return points

    def _object(self, name, data, transform):
        obj = bpy.data.objects.new(name, data)
        self.collection.objects.link(obj)
        obj[reference.TAG] = True
        obj["unity_layer"] = self.tree.layer(transform)
        return obj

    def _data(self, transform, part):
        """The mesh data drawn at the transform; None (an empty) when reference.mesh cannot decode the mesh."""
        if part is None:
            return None
        mesh, material_paths, skinned = part
        materials = [self._material(p) for p in material_paths]
        try:
            if mesh == "builtin:cube":
                return prefab_parts.cube(materials)
            if skinned is None:
                return prefab_parts.mesh(mesh, materials)
            return prefab_parts.skinned(mesh, materials, self._bones(transform, mesh, skinned))
        except (struct.error, ValueError, IndexError, KeyError) as error:
            print(f"prefab: {self.tree.name(transform)} left out, {mesh} does not decode ({error})")
            return None

    def _material(self, path):
        if path is None:
            return None
        if path not in self.materials:
            self.materials[path] = prefab_parts.material(path)
        return self.materials[path]

    def _bones(self, transform, mesh, renderer):
        """For each bone: renderer-local <- bone world <- bind pose, all in Blender axes (None: bone not in the prefab)."""
        poses = prefab_parts.bind_poses(mesh)
        into_renderer = self.tree.world(transform).inverted()
        bones = []
        for i, item in enumerate(unity.items(renderer, "m_Bones")):
            bone = unity.ref(item)[0]
            known = self.tree.is_local(bone) and i < len(poses)
            bones.append(into_renderer @ self.tree.world(bone) @ prefab_parts.to_blender(poses[i]) if known else None)
        return bones
