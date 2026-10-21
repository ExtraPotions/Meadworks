"""Blender rigs for the kraken preview: the head and the long tentacle rebuilt as armatures from the model's own rig
(out/<part>/<asset>.json, Unity axes) over the baked meshes (out/<part>/<asset>.blend, which carry the bone weights as
vertex groups), and posing that sets every bone's keyframe from a pose computed in the world, exactly as the mod does:
each bone at its point, turned about axes of the part it belongs to, whatever roll Blender gives the bone.
"""
import json
import math
import os

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")


def from_unity(v):
    return Vector((-v[0], -v[2], v[1]))


# ---- loading
def append_objects(blend, names):
    with bpy.data.libraries.load(blend, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in names]
    folder = os.path.dirname(blend)
    for image in bpy.data.images:
        if image.filepath.startswith("//") and not os.path.exists(bpy.path.abspath(image.filepath)):
            image.filepath = os.path.normpath(os.path.join(folder, image.filepath[2:].replace("\\", "/")))
            image.reload()
    found = {}
    for obj in dst.objects:
        if obj is not None:
            bpy.context.scene.collection.objects.link(obj)
            found[obj.name.split(".")[0]] = obj
    return found


def rig_json(part, asset):
    with open(os.path.join(OUT, part, asset + ".json")) as f:
        return json.load(f)


def build_armature(name, bones, tails):
    """bones: [(name, parent, head in Blender)], tails: name -> tail. Returns the armature object at the origin."""
    data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for bone_name, parent, head in bones:
        eb = data.edit_bones.new(bone_name)
        eb.head, eb.tail = head, tails[bone_name]
        eb.roll = 0.0
        if parent:
            eb.parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm


def skin(mesh_obj, arm):
    mesh_obj.parent = arm
    mod = mesh_obj.modifiers.new("rig", "ARMATURE")
    mod.object = arm


# ---- the tentacle
def tentacle_rig(index, mesh_template):
    rig = rig_json("tentacle", "ecp_kraken_tentacle")
    bones, tails = [], {}
    for b in rig["bones"]:
        head = from_unity(b["position"])
        bones.append((b["name"], b["parent"], head))
        tails[b["name"]] = head + Vector((0.0, -0.5, 0.0))
    arm = build_armature(f"tentacle_{index}", bones, tails)
    mesh = mesh_template.copy()
    mesh.name = f"tentacle_{index}_mesh"
    bpy.context.scene.collection.objects.link(mesh)
    skin(mesh, arm)
    return Tentacle(arm, mesh)


def frame_matrix(direction, back):
    d = direction.normalized()
    b = (back - d * back.dot(d)).normalized()
    return Matrix((d.cross(b), d, b)).transposed()


class Tentacle:
    """Keys a tentacle's bones onto a pose of world points (the armature sits at the origin, unrotated)."""

    REST_FRAME = frame_matrix(Vector((0, -1, 0)), Vector((0, 0, 1)))

    def __init__(self, arm, mesh):
        self.arm, self.mesh = arm, mesh
        self.bones = [arm.pose.bones[f"kt_{i:02d}"] for i in range(16)]
        self.rest = [b.bone.matrix_local.copy() for b in self.bones]
        self.offset = [self.REST_FRAME.inverted() @ r.to_3x3() for r in self.rest]

    def pose(self, points, side, frame):
        previous = (points[1] - points[0]).normalized()
        back = side.cross(previous)
        if back.length < 1e-4:
            back = Vector((0, 0, 1))
        back.normalize()
        parent_world = None
        for i, pb in enumerate(self.bones):
            d = points[i + 1] - points[i]
            d = d.normalized() if d.length > 1e-6 else previous
            back = previous.rotation_difference(d) @ back
            back = (back - d * back.dot(d)).normalized()
            world = (frame_matrix(d, back) @ self.offset[i]).to_4x4()
            world.translation = points[i]
            parent_rest = self.rest[i - 1] if i > 0 else None
            basis = (self.rest[i].inverted() @ parent_rest @ parent_world.inverted() @ world) if i > 0 else self.rest[i].inverted() @ world
            key_bone(pb, basis, frame, scale=False)
            parent_world, previous = world, d

    def visible(self, shown, frame):
        self.mesh.hide_viewport = self.mesh.hide_render = not shown
        self.mesh.keyframe_insert("hide_viewport", frame=frame)
        self.mesh.keyframe_insert("hide_render", frame=frame)


def key_bone(pb, basis, frame, scale=True):
    loc, rot, size = basis.decompose()
    pb.location, pb.rotation_quaternion = loc, rot
    pb.keyframe_insert("location", frame=frame)
    pb.keyframe_insert("rotation_quaternion", frame=frame)
    if scale:
        pb.scale = size
        pb.keyframe_insert("scale", frame=frame)


# ---- the head
class Head:
    """The head's armature (placed as a whole by its object) and its bones turned about the head's own axes."""

    def __init__(self, meshes):
        rig = rig_json("head", "ecp_kraken_head")
        bones, tails = [], {}
        for b in rig["bones"]:
            head = from_unity(b["position"])
            bones.append((b["name"], b["parent"], head))
            tails[b["name"]] = head + Vector((0.0, 0.0, 0.3))
        self.arm = build_armature("kraken_head", bones, tails)
        for mesh in meshes:
            skin(mesh, self.arm)
        self.markers = {m["name"]: (m["parent"], from_unity(m["position"])) for m in rig["markers"]}
        self.rest = {b.name: b.matrix_local.copy() for b in self.arm.data.bones}
        self.body = [b for b in ("kh_body_1", "kh_body_2", "kh_body_3") if b in self.rest]
        heads = [self.rest[b].translation for b in self.body]
        self.reach = [(heads[1] - heads[0]).length, (heads[2] - heads[0]).length] if len(heads) == 3 else []
        self.reach += [self.reach[-1] + 1.2] if self.reach else []
        self.body_length = -heads[0].z + self.reach[-1] if self.reach else 0.0

    def place(self, world, frame):
        self.arm.matrix_world = world
        self.arm.keyframe_insert("location", frame=frame)
        self.arm.keyframe_insert("rotation_euler", frame=frame)

    def _turn(self, name, axis, degrees, scale=1.0):
        rest = self.rest[name]
        h = rest.translation
        delta = Matrix.Translation(h) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Scale(scale, 4) @ Matrix.Translation(-h)
        return rest.inverted() @ delta @ rest

    def pose(self, lean, beak, siphon, time, life, frame, body_end=None):
        x = Vector((1, 0, 0))
        turns = {
            "kh_neck": self._turn("kh_neck", x, lean),
            "kh_mantle": self._turn("kh_mantle", Vector((0, -1, 0)), 3 * life * math.sin(time * 0.7), 1 + 0.035 * life * math.sin(time * 1.6)),
            "kh_beak_upper": self._turn("kh_beak_upper", x, -35 * beak),
            "kh_beak_lower": self._turn("kh_beak_lower", x, 35 * beak),
            "kh_siphon": self._turn("kh_siphon", x, 0, 1 + 0.45 * siphon),
        }
        turns.update(self._bend(body_end))
        for name, basis in turns.items():
            key_bone(self.arm.pose.bones[name], basis, frame)
        self._turns = turns

    def _bend(self, end_world):
        """HeadRig.Bend: the spine bones aimed along a curve leaving the head straight down and arriving at the end."""
        if len(self.body) != 3:
            return {}
        if end_world is None:
            return {b: Matrix.Identity(4) for b in self.body}
        h = [self.rest[b].translation.copy() for b in self.body]
        end = self.arm.matrix_world.inverted() @ end_world
        control = h[0] + Vector((0, 0, -1)) * ((end - h[0]).length * 0.5)
        curve = [h[0].lerp(control, t).lerp(control.lerp(end, t), t) for t in (i / 24 for i in range(25))]
        out, parent_delta, pos, rot = {}, Matrix.Identity(4), h[0], Matrix.Identity(3)
        for i, name in enumerate(self.body):
            if i > 0:
                pos = pos + rot @ (h[i] - h[i - 1])
            down = rot @ Vector((0, 0, -1))
            rot = (down.rotation_difference(_along(curve, self.reach[i]) - pos)).to_matrix() @ rot
            delta = Matrix.Translation(pos) @ rot.to_4x4() @ Matrix.Translation(-h[i])
            out[name] = self.rest[name].inverted() @ parent_delta.inverted() @ delta @ self.rest[name]
            parent_delta = delta
        return out

    def world_of(self, marker):
        """Where a marker is now, in the world (its parent bone's turn and the head's placement applied)."""
        parent, rest = self.markers[marker]
        chain, name = Matrix.Identity(4), parent
        while name:
            bone = self.arm.data.bones[name]
            turn = getattr(self, "_turns", {}).get(name)
            if turn is not None:
                chain = self.rest[name] @ turn @ self.rest[name].inverted() @ chain
            name = bone.parent.name if bone.parent else None
        return self.arm.matrix_world @ chain @ rest

    def joint(self, i):
        return self.world_of(f"kh_tentacle_{i}")


def _along(curve, distance):
    for i in range(1, len(curve)):
        step = (curve[i] - curve[i - 1]).length
        if step >= distance:
            return curve[i - 1].lerp(curve[i], distance / step if step > 1e-5 else 1.0)
        distance -= step
    return curve[-1]
