"""The game skeleton a route (b) body is built on, read from the game's own prefab in the reference export: every
transform from Visual down to the bones with its exact local transform (Unity's numbers, as the prefab file writes
them), the body renderer (its place, bone order and root bone), and the Animator's avatar and controller.

    skeleton = gamerig_source.Skeleton("Characters/Skeleton/Skeleton.prefab")

Blender-side matrices (skeleton.world) are in the workshop's axes (prefab.py: Unity (x, y, z) -> (-x, -z, y)), metres,
the prefab root at the origin with its position and rotation dropped. Nothing is built in Blender here; gamerig.py
makes the armature from it and gamerig_export.py writes the contract Unity checks.

Kinds of transform kept under the armature: "bone" (in the body renderer's bone list, deforms), "socket" (a plain
transform the game hangs things from: RightHand_Attach, Helmet_attach), "end" (a leaf named *_end). Transforms carrying
other components (cloth colliders, eye meshes, particles) are left out with everything below them.
"""
import re

from . import prefab, unity

SKINNED, ANIMATOR, SCRIPT = 137, 95, 114


class Skeleton:
    """One game creature's skeleton and body renderer, read once from its prefab."""

    def __init__(self, prefab_path):
        self.source = prefab_path
        self.tree = prefab._Prefab(prefab_path)
        self._objects = {unity.ref(self.field(t, "m_GameObject"))[0]: t for t in self._transforms()}
        self.visual = self._named_child(self.tree.root(), "Visual")
        self.body = self._body_renderer()
        self.body_node = self._owner(self.body)
        self.bones = [unity.ref(item)[0] for item in unity.items(self.tree.docs[self.body][1], "m_Bones")]
        self.order = [self.name(b) for b in self.bones]
        self.root_bone = self.name(unity.ref(self.field(self.body, "m_RootBone"))[0])
        self.armature = self.parent(self._top_bone())
        self.nodes = self._kept(self.armature)
        self.chain = self._chain()

    # -- lookups --------------------------------------------------------------------------------------------------

    def name(self, node):
        """The transform's GameObject name ("" for a transform outside this prefab)."""
        return self.tree.name(node) if node in self.tree.docs else ""

    def field(self, doc, name):
        """A top-level field of one of the prefab's documents, raw."""
        return unity.field(self.tree.docs[doc][1], name)

    def parent(self, node):
        """The transform's parent in this prefab, or None at the root."""
        father = unity.ref(self.field(node, "m_Father"))[0]
        return father if self.tree.is_local(father) else None

    def path(self, node):
        """The transform's path below the prefab root ('Visual/_skeleton_base/Armature/Hips')."""
        up = self.parent(node)
        return self.name(node) if up is None or up == self.tree.root() else self.path(up) + "/" + self.name(node)

    def world(self, node):
        """The rest matrix relative to the prefab root, Blender axes (scale included: the armature's 100 and all)."""
        return self.tree.world(node)

    def local(self, node):
        """Unity's own local position, rotation (x, y, z, w) and scale, exactly as the prefab writes them."""
        return tuple(unity.numbers(self.field(node, key)) for key in ("m_LocalPosition", "m_LocalRotation", "m_LocalScale"))

    def kind(self, node):
        """"bone", "socket" or "end" (see the module doc)."""
        if node in self.bones:
            return "bone"
        return "end" if self.name(node).endswith("_end") else "socket"

    def deform_names(self):
        """The body's bones in hierarchy order (parents first)."""
        return [self.name(n) for n in self.nodes if n in self.bones]

    # -- reading the prefab ---------------------------------------------------------------------------------------

    def _transforms(self):
        return [i for i, (kind, body) in self.tree.docs.items() if kind in prefab.TRANSFORMS and self.tree.is_local(i)]

    def _owner(self, component):
        """The transform of the GameObject a component sits on."""
        return self._objects[unity.ref(self.field(component, "m_GameObject"))[0]]

    def _named_child(self, node, name):
        found = [c for c in self.tree.children(node) if self.name(c) == name]
        if not found:
            raise SystemExit(f"gamerig: {self.source} has no child named {name!r} under its root")
        return found[0]

    def _body_renderer(self):
        """The renderer LevelEffects.m_mainRender names, else VisEquipment.m_bodyModel, else the most-boned skin."""
        for key in ("m_mainRender", "m_bodyModel"):
            for kind, body in self.tree.docs.values():
                if kind == SCRIPT and f"  {key}: " in body:
                    target = unity.ref(unity.field(body, key))[0]
                    if target in self.tree.docs and self.tree.docs[target][0] == SKINNED:
                        return target
        skins = [i for i, (kind, _) in self.tree.docs.items() if kind == SKINNED]
        if not skins:
            raise SystemExit(f"gamerig: {self.source} has no SkinnedMeshRenderer")
        return max(skins, key=lambda i: len(unity.items(self.tree.docs[i][1], "m_Bones")))

    def _top_bone(self):
        """The body's highest bone: its root bone walked up while the parent is still a bone."""
        node = next((b for b in self.bones if self.name(b) == self.root_bone), self.bones[0])
        while self.parent(node) in self.bones:
            node = self.parent(node)
        return node

    def _pure(self, node):
        """True when the GameObject has nothing but its Transform (a socket or an end, not a collider or a mesh)."""
        return len(unity.items(self.tree.game_object(node), "m_Component")) == 1

    def _kept(self, top):
        """Every bone, socket and end below `top`, parents before children."""
        kept = []

        def visit(node):
            """Keeps the node's bone, socket and end children, then theirs."""
            for child in self.tree.children(node):
                if self.tree.is_local(child) and (child in self.bones or self._pure(child)):
                    kept.append(child)
                    visit(child)
        visit(top)
        missing = [self.name(b) for b in self.bones if b not in kept]
        if missing:
            raise SystemExit(f"gamerig: bones outside the armature or under other components: {missing}")
        return kept

    def _chain(self):
        """The transforms from Visual down to the armature and to the body renderer, parents first."""
        chain = []
        for end in (self.armature, self.body_node):
            steps, node = [], end
            while node is not None and node != self.tree.root():
                steps.append(node)
                node = self.parent(node)
            chain += [s for s in reversed(steps) if s not in chain]
        return chain

    # -- the Animator (for the Unity preview, which stages the game's creature) ------------------------------------

    def animator(self):
        """{node, avatar, controller}: the Animator's place and the reference paths of its avatar and controller."""
        for node in self._transforms():
            components = self.tree.components(node)
            if ANIMATOR in components:
                body = components[ANIMATOR][1]
                return {"node": self.path(node), "avatar": _asset(unity.field(body, "m_Avatar")),
                        "controller": _asset(unity.field(body, "m_Controller"))}
        return {"node": "", "avatar": "", "controller": ""}

    def body_assets(self):
        """{mesh, material, albedo}: reference paths of the game body's mesh, its first material and that albedo."""
        body = self.tree.docs[self.body][1]
        materials = [_asset(m) for m in unity.items(body, "m_Materials")]
        material = next((m for m in materials if m and m.endswith(".mat")), "")
        albedo = unity.material(material)["albedo"][0] if material else ""
        return {"mesh": _asset(unity.field(body, "m_Mesh")), "material": material, "albedo": albedo or ""}


def controller_clips(controller):
    """Reference paths of every clip a game controller plays (found by the GUIDs its states and blend trees name)."""
    if not controller:
        return []
    guids = sorted(set(re.findall(r"guid: ([0-9a-f]{32})", unity.read(controller))))
    return sorted(p for p in (unity.asset_path(g) for g in guids) if p and p.endswith(".anim"))


def _asset(value):
    _, guid = unity.ref(value)
    return unity.asset_path(guid) or "" if guid and guid not in (unity.BUILTIN_ASSETS, unity.BUILTIN_MESHES) else ""
