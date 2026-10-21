"""Renders the full combat sequence (fight.py) from a camera that follows the fight.

    blender --background --factory-startup --python-exit-code 1 --python assets/crypt_mimic/combat.py -- [--render]

Saves out/combat/combat.blend. With --render, writes the frames to out/combat/frames/, plus out/combat/screen.json (where
the mimic and the player are on screen each frame) and hud.json, which hud.py draws over the frames.
"""
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path[:0] = [HERE, os.path.join(HERE, "..", "..", "blender")]

import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Vector  # noqa: E402

import parts  # noqa: E402
import rig as mimic_rig  # noqa: E402
import stage  # noqa: E402
from fight import choreograph  # noqa: E402
from poses import key, world_position  # noqa: E402
from workshop import materials, scene, shapes  # noqa: E402

OUT = os.path.join(HERE, "out", "combat")
CAMERA_OFFSET = Vector((4.6, -5.2, 2.9))


def build():
    scene.clear()
    rig = mimic_rig.armature()
    mimic_rig.chest(rig)
    parts.build(rig)
    stage.build(floor=(26, 26), floor_y=-4.0, torches=[(2.2, -8.4, 2.3), (-4.5, -6.5, 2.4)])
    player, shoulder = stage.player()
    mimic, actor, hud, end = choreograph()
    for frame, values in mimic.keys:
        key(rig, frame, values)
    _player(player, shoulder, actor)
    camera = _camera(mimic, actor)
    _loot(mimic, hud.death)
    s = bpy.context.scene
    s.render.fps, s.frame_start, s.frame_end, s.camera = 30, 1, end, camera
    stage.viewport()
    for frame, text in hud.captions:
        s.timeline_markers.new(text, frame=frame)
    return rig, player, camera, hud


def _player(player, shoulder, actor):
    for frame, (x, y), yaw, lean, tilt, crouch in actor.keys:
        player.location = (x, y, 0.0)
        player.rotation_euler = (math.radians(-lean), math.radians(tilt), math.radians(yaw))
        player.scale = (1.0, 1.0, crouch)
        for path in ("location", "rotation_euler", "scale"):
            player.keyframe_insert(path, frame=frame)
    for frame, angle in sorted(actor.sword):
        shoulder.rotation_euler = (math.radians(-angle), 0.0, 0.0)
        shoulder.keyframe_insert("rotation_euler", frame=frame)


def _camera(mimic, actor):
    """A camera on a fixed offset from a target that follows the fight, weighted towards the mimic."""
    target = bpy.data.objects.new("camera_target", None)
    bpy.context.scene.collection.objects.link(target)
    for frame in range(1, mimic.keys[-1][0] + 1, 15):
        m, p = _at(mimic.keys, frame, _mimic_xy), _at(actor.keys, frame, lambda k: k[1])
        target.location = (0.6 * m[0] + 0.4 * p[0], 0.6 * m[1] + 0.4 * p[1], 0.5)
        target.keyframe_insert("location", frame=frame)
    camera = bpy.data.objects.new("camera_follow", bpy.data.cameras.new("camera_follow"))
    bpy.context.scene.collection.objects.link(camera)
    camera.data.lens = 38
    camera.parent = target
    camera.location = CAMERA_OFFSET
    track = camera.constraints.new('TRACK_TO')
    track.target, track.track_axis, track.up_axis = target, 'TRACK_NEGATIVE_Z', 'UP_Y'
    return camera


def _mimic_xy(entry):
    return world_position(entry[1])


def _at(keys, frame, where):
    """Linear position between the keys either side of `frame`."""
    before = max((k for k in keys if k[0] <= frame), key=lambda k: k[0], default=keys[0])
    after = min((k for k in keys if k[0] >= frame), key=lambda k: k[0], default=keys[-1])
    a, b = where(before), where(after)
    t = 0.0 if after[0] == before[0] else (frame - before[0]) / (after[0] - before[0])
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def _loot(mimic, death):
    """Coins and two gems burst from the mouth after it dies and scatter in front of it."""
    random.seed(5)
    gold = materials.flat("loot_gold", (1.0, 0.66, 0.16), roughness=0.3, metallic=1.0)
    gems = [materials.flat("loot_amber", (1.0, 0.35, 0.02), roughness=0.2),
            materials.flat("loot_ruby", (0.7, 0.01, 0.04), roughness=0.2)]
    for i in range(16):
        piece = (shapes.cylinder(f"coin_{i}", 0.05, 0.012, material=gold, vertices=10) if i < 14
                 else shapes.sphere(f"gem_{i}", 0.05, material=gems[i - 14], segments=6, rings=4))
        _toss(piece, mimic, death + 8 + i // 2, random.uniform(-1.1, 1.1), random.uniform(0.4, 1.5))


def _toss(piece, mimic, start, right, distance):
    fx, fy = mimic.facing()
    mouth = Vector((mimic.x + fx * 0.3, mimic.y + fy * 0.3, 0.7))
    land = Vector(mimic.ahead(0.45 + distance, right)) .to_3d()
    land.z = 0.02
    for frame, where, scale in ((1, mouth, 0.0), (start - 1, mouth, 0.0), (start, mouth, 1.0),
                                (start + 9, (mouth + land) / 2 + Vector((0, 0, 0.9)), 1.0), (start + 18, land, 1.0)):
        piece.location, piece.scale = where, (scale,) * 3
        piece.rotation_euler = (random.uniform(0, 6), random.uniform(0, 6), 0.0) if frame == start + 18 else (0, 0, 0)
        for path in ("location", "scale", "rotation_euler"):
            piece.keyframe_insert(path, frame=frame)


def _render(rig, player, camera, hud):
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 24
    s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = 1280, 720, 100
    s.render.image_settings.media_type, s.render.image_settings.file_format = 'IMAGE', 'PNG'
    s.render.filepath = os.path.join(OUT, "frames", "f_")
    bpy.ops.render.render(animation=True)
    _write_overlay_data(rig, player, camera, hud)


def _write_overlay_data(rig, player, camera, hud):
    """screen.json (where the two are on screen, every frame) and hud.json, for hud.py."""
    s = bpy.context.scene
    screen = {}
    for frame in range(s.frame_start, s.frame_end + 1):
        s.frame_set(frame)
        screen[frame] = _on_screen(s, rig, player, camera)
    with open(os.path.join(OUT, "screen.json"), "w", encoding="utf-8") as handle:
        json.dump(screen, handle)
    with open(os.path.join(OUT, "hud.json"), "w", encoding="utf-8") as handle:
        json.dump(hud.data(), handle)


def _on_screen(s, rig, player, camera):
    """Normalised screen points (0..1, y up) above the mimic's lid and the player's head, read from the evaluated
    scene: the original objects' poses and constraints are not updated by a frame change."""
    graph = bpy.context.evaluated_depsgraph_get()
    body = rig.evaluated_get(graph)
    top = body.matrix_world @ body.pose.bones["body"].head + Vector((0, 0, 1.0))
    head = player.evaluated_get(graph).matrix_world.translation + Vector((0, 0, 2.05))
    view = camera.evaluated_get(graph)
    return {k: list(world_to_camera_view(s, view, v))[:2] for k, v in (("mimic", top), ("player", head))}


def main():
    os.makedirs(os.path.join(OUT, "frames"), exist_ok=True)
    rig, player, camera, hud = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "combat.blend"))
    print("WORKSHOP saved", os.path.join(OUT, "combat.blend"), "frames", bpy.context.scene.frame_end, flush=True)
    if "--sheet" in sys.argv:
        _sheet(hud)
    if "--overlay-only" in sys.argv:
        s = bpy.context.scene
        s.render.resolution_x, s.render.resolution_y = 1280, 720
        _write_overlay_data(rig, player, camera, hud)
        print("WORKSHOP overlay data written", flush=True)
    if "--render" in sys.argv:
        _render(rig, player, camera, hud)
        print("WORKSHOP frames rendered", flush=True)


def _sheet(hud):
    """Sixteen small frames across the fight, one per beat where possible, for a quick check."""
    from workshop import preview
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 8
    s.render.resolution_x, s.render.resolution_y = 480, 270
    s.render.image_settings.media_type, s.render.image_settings.file_format = 'IMAGE', 'PNG'
    frames = sorted({f for f, _ in hud.captions} | {p["frame"] for p in hud.popups})
    frames = [frames[round(i * (len(frames) - 1) / 15)] + 3 for i in range(16)]
    paths = []
    for frame in frames:
        s.frame_set(frame)
        s.render.filepath = os.path.join(OUT, f"sheet_{frame:04d}.png")
        bpy.ops.render.render(write_still=True)
        paths.append(s.render.filepath)
    preview.grid(paths, 4, os.path.join(OUT, "combat_sheet.png"))
    print("WORKSHOP sheet frames", frames, flush=True)


main()
