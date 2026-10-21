"""The kraken fight preview's stage and director: loading the ship and the kraken, the crew on deck, the ink drops,
the fight's timeline (the mod's phases and blows, sped up a little), the head's placement (HeadMotion.cs), the water,
light and camera, and saving.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

import fight_motion as fm
import fight_rigs as fr

U = fr.from_unity
HERE = os.path.dirname(os.path.abspath(__file__))
SHIPS = "GameElements/Ships/{}.prefab"

# The timeline, in seconds: one attack every ~3 s (the mod: a random 2.5 to 4), six under the ship (the third a grab),
# then six from the head, as in the mod.
HUNT_END, DIVE_END = 3.2, 5.0
SLAM_KINDS = ("slam", "slam", "grab", "slam", "slam", "slam")
SLAMS = [(6.6 + 3.1 * k + (1.0 if k > 2 else 0.0), arm, SLAM_KINDS[k]) for k, arm in enumerate((3, 0, 5, 1, 4, 2))]
HEAD_START = SLAMS[-1][0] + 3.0 + 0.3
HEAD_SIDE, HEAD_ALONG = 1, 1.0
HEAD_EVENTS = [(HEAD_START + 2.2 + 3.1 * k, kind, who) for k, (kind, who) in
               enumerate((("bite", "A"), ("ink", "C"), ("smash", 1), ("bite", "B"), ("ink", "C"), ("smash", 2)))]
END = HEAD_EVENTS[-1][0] + 4.5
CREW_PARTS = {}


def reset(fps):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = fps
    scene.frame_start = 1


# ---------------------------------------------------------------- loading
def load_ship(name="VikingShip"):
    try:
        from workshop import prefab
        root = prefab.load(SHIPS.format(name))
        meshes = [o for o in root.children_recursive if o.type == "MESH" and not o.hide_render]
        print(f"FIGHT ship: the game's {name}, {len(meshes)} meshes")
        return root, meshes
    except Exception as e:  # the importer is optional: a plain hull stands in
        print(f"FIGHT ship: no game longship ({e}); a stand-in hull")
        return stand_in()


def stand_in():
    root = bpy.data.objects.new("ship", None)
    bpy.context.scene.collection.objects.link(root)
    wood = material("wood", (0.33, 0.22, 0.12, 1))
    parts = [((0, 0, 0.55), (5.0, 16.0, 0.2)), ((2.55, 0, 0.3), (0.2, 16.0, 2.0)), ((-2.55, 0, 0.3), (0.2, 16.0, 2.0)),
             ((0, -8.2, 0.6), (5.2, 0.4, 2.4)), ((0, 8.2, 0.6), (5.2, 0.4, 2.4)), ((0, -1, 5), (0.3, 0.3, 9))]
    meshes = []
    for i, (at, size) in enumerate(parts):
        bpy.ops.mesh.primitive_cube_add(location=at)
        obj = bpy.context.active_object
        obj.name, obj.scale, obj.parent = f"hull_{i}", Vector(size) / 2, root
        obj.data.materials.append(wood)
        bpy.ops.object.transform_apply(scale=True)
        meshes.append(obj)
    return root, meshes


def load_kraken():
    head_parts = fr.append_objects(os.path.join(fr.OUT, "head", "ecp_kraken_head.blend"), ["ecp_kraken_head", "ecp_kraken_eyes"])
    tentacle = fr.append_objects(os.path.join(fr.OUT, "tentacle", "ecp_kraken_tentacle.blend"), ["ecp_kraken_tentacle"])["ecp_kraken_tentacle"]
    head = fr.Head([head_parts["ecp_kraken_head"], head_parts["ecp_kraken_eyes"]])
    tentacles = [fr.tentacle_rig(i, tentacle) for i in range(6)]
    bpy.context.scene.collection.objects.unlink(tentacle)
    return {"head": head, "tentacles": tentacles}


def material(name, colour, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = colour
    bsdf.inputs["Roughness"].default_value = 0.6
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "BLENDED"
    return mat


def players(ship):
    """Three of the crew on deck, in the ship's space: A at the rail on the head's side, B amidships, C aft on the far side."""
    hw, hl, mid = ship.half_width, ship.half_length, ship.middle_z
    spots = {"A": (hw - 0.8, mid + 0.25 * hl), "B": (0.3, mid - 0.1 * hl), "C": (-hw + 0.9, mid - 0.4 * hl)}
    skin = material("crew", (0.55, 0.45, 0.35, 1))
    crew = {}
    for name, (x, z) in spots.items():
        y = ship.top(x, z) or 0.6
        crew[name] = Vector((x, y, z))
        body(name, ship.root, U((x, y, z)), skin)
    return crew


def body(name, parent, feet, mat):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.28, depth=1.3, location=feet + Vector((0, 0, 0.75)))
    trunk = bpy.context.active_object
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.2, location=feet + Vector((0, 0, 1.6)))
    head = bpy.context.active_object
    for part in (trunk, head):
        part.name, part.parent = f"crew_{name}", parent
        part.data.materials.append(mat)
    CREW_PARTS[name] = [(trunk, 0.75), (head, 1.6)]


def ink_drops():
    ink = material("ink", (0.01, 0.01, 0.02, 1))
    drops = []
    for i in range(9):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.22 * (1.3 - 0.07 * i), segments=12, ring_count=8)
        drop = bpy.context.active_object
        drop.name = f"ink_{i}"
        drop.data.materials.append(ink)
        drops.append(drop)
    return drops


# ---------------------------------------------------------------- the director
class Director:
    def __init__(self, ship, head, arms, crew, drops):
        self.ship, self.head, self.arms, self.crew, self.drops = ship, head, arms, crew, drops
        self.phase, self.slams, self.events = None, list(SLAMS), list(HEAD_EVENTS)
        self.at, self.facing, self.in_ship, self.action, self.jet, self.grab = None, None, False, None, None, None
        self.rot = None

    def run(self, fps, limit=None):
        frames = int(END * fps) if limit is None else limit
        for f in range(frames):
            self.step(f / fps, 1.0 / fps, f + 1)
            if f % 48 == 0:
                print(f"FIGHT frame {f + 1}/{frames}", flush=True)
        return frames

    def step(self, t, dt, frame):
        self.ship.m = ship_motion(self.ship.rest, t)
        self.ship.root.matrix_world = self.ship.m
        self.ship.root.keyframe_insert("location", frame=frame)
        self.ship.root.keyframe_insert("rotation_euler", frame=frame)
        self.enter(phase_at(t), t)
        self.orders(t)
        self.place_head(t, dt, frame)
        for arm in self.arms:
            if self.phase in ("hunt", "dive"):
                arm.trailing(self.trail_frame(arm.i), t, dt)
            else:
                arm.at_ship(self.ship, t, dt)
            arm.key(frame)
        self.key_ink(t, frame)
        self.carry(t, dt, frame)

    def enter(self, phase, t):
        if phase == self.phase:
            return
        self.phase = phase
        for arm in self.arms:
            grip = grip_along(self.ship, arm.i) if phase == "head" else None
            mode = {"tentacles": "up", "head": "grip" if grip is not None else "down"}.get(phase, "trail")
            arm.set(mode, grip)

    def orders(self, t):
        while self.slams and self.slams[0][0] <= t:
            _, i, kind = self.slams.pop(0)
            target = self.nearest(self.ship.anchor(i))
            self.arms[i].begin(self.ship, target, kind, t)
            if kind == "grab":
                who = min(self.crew, key=lambda n: (self.crew[n] - Vector(target)).length)
                self.grab = dict(arm=self.arms[i], who=who, impact=t + fm.impact_of("grab"), release=t + fm.release_of("grab"))
        while self.events and self.events[0][0] <= t:
            _, kind, who = self.events.pop(0)
            if kind == "smash":
                a = self.ship.anchor(who)
                self.arms[who].begin(self.ship, (HEAD_SIDE * self.ship.half_width * 0.6, 0.0, a.z), "smash", t)
            else:
                self.begin_action(kind, self.crew[who] + Vector((0, 1.1, 0)), t)

    def carry(self, t, dt, frame):
        """KrakenHeld: the grabbed crew member hangs from the tip, is flung out at the release, and lands in the sea."""
        g = self.grab
        if not g or t < g["impact"]:
            return
        if t < g["release"]:
            g["at"] = g["arm"].shown[13] - Vector((0, 0, 1.0))
            out = self.ship.frame(g["arm"].i, g["arm"].strike["target"]).out if g["arm"].strike else Vector((1, 0, 0))
            g["velocity"] = Vector((-out.x, -out.y, 0)).normalized() * 3.0 + Vector((0, 0, 3.0))
        elif g["at"].z > -1.0:
            g["velocity"].z -= 20.0 * dt
            g["at"] = g["at"] + g["velocity"] * dt
            g["at"].z = max(g["at"].z, -1.0)
        local = self.ship.m.inverted() @ g["at"]
        for part, up in CREW_PARTS.get(g["who"], []):
            part.location = local + Vector((0, 0, up))
            part.keyframe_insert("location", frame=frame)

    def nearest(self, anchor):
        best = min(self.crew.values(), key=lambda p: (Vector((p.x - anchor.x, p.z - anchor.z))).length)
        return (best.x, best.y, best.z)

    def begin_action(self, kind, target, t):
        lunge = fm.REACH
        if kind == "bite":
            level = Vector((target.x - self.at.x, target.z - self.at.z))
            lunge = min(fm.MAX_LUNGE, max(fm.MIN_LUNGE, level.length - 1.9))
        self.action = dict(kind=kind, start=t, target=target, lunge=lunge, fired=False)

    # ---- the head (HeadMotion.cs)
    def place_head(self, t, dt, frame):
        if self.phase == "hunt":
            self.swim(t, frame)
        else:
            self.at_ship(t, dt, frame)
        self.fire(t)

    def swim(self, t, frame):
        s = self.ship
        start = s.world((s.half_width + 14.0, 0.0, 6.0))
        end = s.world((s.half_width + 2.5, 0.0, 1.0))
        at = start.lerp(end, t / HUNT_END)
        at.z = -2.0
        heading = (end - start).normalized()
        world = head_matrix(at, heading, Vector((0, 0, 1))) @ Matrix.Rotation(math.radians(-35), 4, "X")
        self.world_at, self.world_heading = at, heading
        self.rot = world.to_quaternion()
        self.head.place(world, frame)
        self.head.pose(4 * math.sin(t * 0.8), 0.1, 0.0, t, 1.0, frame)

    def at_ship(self, t, dt, frame):
        s = self.ship
        if not self.in_ship:
            self.at = U_back(s.m.inverted() @ self.world_at)
            self.in_ship = True
        beside = self.phase == "head"
        target = Vector((HEAD_SIDE * (s.half_width + 1.8), s.waterline() - 0.6, HEAD_ALONG)) if beside \
            else Vector((0.0, s.waterline() - (1.5 + self.head.body_length), s.middle_z))
        h, v = fm.follow(2.5, dt), fm.follow(1.3, dt)
        self.at = Vector((self.at.x + (target.x - self.at.x) * h, self.at.y + (target.y - self.at.y) * v,
                          self.at.z + (target.z - self.at.z) * h))
        o = self.offsets(t) if beside else fm.REST
        facing = Vector((-HEAD_SIDE, 0, 0))
        if self.action and o["turn"] > 0:
            towards = Vector((self.action["target"].x - self.at.x, 0, self.action["target"].z - self.at.z)).normalized()
            facing = facing.lerp(towards, o["turn"]).normalized()
        at = self.at + Vector((0, o["raise_"] + (0.12 * math.sin(t * 0.9) if beside else 0), 0)) + facing * o["forward"]
        want = head_matrix(s.world(at), s.direction(facing if beside else Vector((0, 0, -1))).normalized(),
                           s.up if beside else -s.up)
        self.rot = want.to_quaternion() if self.rot is None else self.rot.slerp(want.to_quaternion(), v)
        world = self.rot.to_matrix().to_4x4()
        world.translation = s.world(at)
        self.head.place(world, frame)
        body = s.world(self.at - Vector((0, self.head.body_length, 0))) if beside else None
        self.head.pose(o["lean"] + 4 * math.sin(t * 0.8), max(o["beak"], 0.08 + 0.08 * math.sin(t * 1.3)), o["siphon"],
                       t, 1.0, frame, body)

    def offsets(self, t):
        a = self.action
        if not a:
            return fm.REST
        age = t - a["start"]
        if age >= (fm.BITE_END if a["kind"] == "bite" else fm.INK_END):
            self.action = None
            return fm.REST
        return fm.bite(age, a["lunge"]) if a["kind"] == "bite" else fm.ink(age)

    def fire(self, t):
        a = self.action
        if a and a["kind"] == "ink" and not a["fired"] and t - a["start"] >= fm.INK_WINDUP:
            a["fired"] = True
            start = self.head.world_of("kh_mouth")
            aim = self.ship.world(a["target"]) - start
            self.jet = dict(start=t, origin=start, direction=aim.normalized(), length=min(26.0, aim.length + 3.0))

    def trail_frame(self, i):
        joint = self.head.joint(i)
        m = self.head.arm.matrix_world
        forward = -(m.to_3x3() @ Vector((0, 1, 0)))
        back = Vector((-forward.x, -forward.y, 0)).normalized()
        local = m.inverted() @ joint
        fan = max(-0.8, min(0.8, -local.x * 0.35))
        return fm.Frame(joint, back + Vector((0, 0, 1)).cross(back) * fan, Vector((0, 0, 1)))

    def key_ink(self, t, frame):
        j = self.jet
        for i, drop in enumerate(self.drops):
            if j is None or (t - j["start"] - 0.45) * 28.0 >= j["length"]:
                drop.location = Vector((0, 0, -50))
            else:
                head = min((t - j["start"]) * 28.0, j["length"])
                tail = min(max(0.0, t - j["start"] - 0.45) * 28.0, j["length"])
                drop.location = j["origin"] + j["direction"] * (head + (tail - head) * i / 8)
            drop.keyframe_insert("location", frame=frame)


def phase_at(t):
    if t < HUNT_END:
        return "hunt"
    if t < DIVE_END:
        return "dive"
    return "tentacles" if t < HEAD_START else "head"


def grip_along(ship, i):
    """KrakenTargets.GripAlong: the two on the head's side away from it grip the rail 3 m either side of the head."""
    if (-1 if i < 3 else 1) != HEAD_SIDE:
        return None
    first = 0 if HEAD_SIDE < 0 else 3
    nearest = min(range(first, first + 3), key=lambda k: abs(ship.anchor(k).z - HEAD_ALONG))
    if i == nearest:
        return None
    end = ship.half_length * 0.9
    spot = HEAD_ALONG + (3.0 if ship.anchor(i).z > ship.anchor(nearest).z else -3.0)
    return max(ship.middle_z - end, min(ship.middle_z + end, spot))


def ship_motion(rest, t):
    bob = Matrix.Translation((0, 0, 0.08 * math.sin(t * 0.9)))
    roll = Matrix.Rotation(math.radians(1.2 * math.sin(t * 0.7)), 4, "Y")
    pitch = Matrix.Rotation(math.radians(0.8 * math.sin(t * 0.5)), 4, "X")
    return rest @ bob @ roll @ pitch


def head_matrix(at, forward, up):
    y = -forward.normalized()
    z = (up - y * up.dot(y)).normalized()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = at
    return m


def U_back(v):
    return Vector((-v[0], v[2], -v[1]))


# ---------------------------------------------------------------- finishing
def finish(ship, frames, out, stills):
    scene = bpy.context.scene
    scene.frame_end = frames
    water()
    light_and_camera(ship)
    os.makedirs(out, exist_ok=True)
    viewports()
    scene.frame_set(1)
    bpy.ops.file.pack_all()
    path = os.path.join(out, "kraken_fight.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"FIGHT saved {path}")
    if stills:
        render_stills(out, frames)


def viewports():
    """Every 3D view opens textured (Material Preview) and looking through the camera; the timeline plays at 24 fps."""
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != "VIEW_3D":
                continue
            space = area.spaces.active
            space.shading.type = "MATERIAL"
            space.region_3d.view_perspective = "CAMERA"
            space.clip_end = 2000


def water():
    bpy.ops.mesh.primitive_plane_add(size=600, location=(0, 0, 0))
    sea = bpy.context.active_object
    sea.name = "sea"
    sea.data.materials.append(material("sea", (0.03, 0.12, 0.16, 1), alpha=0.82))


def light_and_camera(ship):
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy, sun.rotation_euler = 3.5, (math.radians(50), 0, math.radians(35))
    bpy.context.scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("sky")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.64, 0.72, 1)
    bpy.context.scene.world = world
    cam = bpy.data.objects.new("camera", bpy.data.cameras.new("camera"))
    bpy.context.scene.collection.objects.link(cam)
    eye, look = ship.world((ship.half_width + 13.0, 10.0, -15.0)), ship.world((1.0, 1.0, 0.0))
    cam.location = eye
    cam.rotation_euler = (look - eye).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 28
    bpy.context.scene.camera = cam


def render_stills(out, frames):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.view_settings.view_transform = "Standard"
    grab = SLAMS[2][0]
    first_bite, first_ink, lunge = HEAD_EVENTS[0][0], HEAD_EVENTS[1][0], HEAD_EVENTS[3][0]
    for name, second in (("hunt", 1.6), ("under", 6.2), ("slam", SLAMS[0][0] + 1.25), ("grab_warning", grab + 1.2),
                         ("grab_hoist", grab + 2.3), ("grab_throw", grab + 3.0), ("head_bite", first_bite + 0.8),
                         ("ink", first_ink + 1.3), ("lunge", lunge + 0.8)):
        f = min(frames, int(second * scene.render.fps) + 1)
        scene.frame_set(f)
        scene.render.filepath = os.path.join(out, f"still_{name}.png")
        bpy.ops.render.render(write_still=True)
        print(f"FIGHT still {scene.render.filepath}")
