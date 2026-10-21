"""One body on a game skeleton from model.py to the files Unity reads (route b; gamerig.py has the idea):

    model.build()  ->  the rig (gamerig.armature) and weighted parts  ->  joined into one skinned body, triangulated,
    four weights a vertex  ->  unwrapped and baked into one atlas (albedo with ambient occlusion, normal map, paint
    regions)  ->  out/<name>.json (the contract, gamerig_export), the PNGs, <name>.blend, preview.png

model.py sets SOURCE (the game prefab, e.g. "Characters/Skeleton/Skeleton.prefab"), and may set TEXTURE_SIZE (256),
NORMAL_MAP, AO_STRENGTH, CATEGORY, SMOOTHNESS (0.2) and BONE_ORDER ("game": the game body's own, so the game's
attach_skin gear fits; "own": only the bones it uses). build() makes the rig with gamerig.armature(SOURCE) and returns
it; every mesh it leaves in the scene (reference objects and col_* colliders aside) is a part of the body and must be
weighted (gamerig_weights).
"""
import importlib.util
import os
import time

import bmesh
import bpy

from . import bake, export, gamerig_export, gamerig_weights, materials, preview, reference, regions, scene

DEFAULTS = {"TEXTURE_SIZE": 256, "NORMAL_MAP": True, "AO_STRENGTH": 0.6, "CATEGORY": None, "SMOOTHNESS": 0.2,
            "BONE_ORDER": "game"}


def run(asset_dir, render_preview=True):
    """Builds the asset in `asset_dir` (its model.py) into out/; returns (rig, body)."""
    started = time.time()
    name = os.path.basename(os.path.normpath(asset_dir))
    out = _fresh_out(asset_dir)
    scene.clear()
    model = _load(asset_dir, name)
    rig = model.build()
    if rig is None or "gamerig_source" not in rig:
        raise SystemExit("model.py: build() must return the rig from gamerig.armature()")
    settings = {key: getattr(model, key, value) for key, value in DEFAULTS.items()}
    body = join(rig, name)
    images = _textures(body, name, settings)
    files = _write_images(images, name, out)
    _final_material(body, name, images)
    data = gamerig_export.contract(name, rig, body, files, settings)
    data.update(_codex_keys(images, files, body))
    gamerig_export.write(os.path.join(out, name + ".json"), data)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, name + ".blend"))
    if render_preview:
        _log("preview", preview.render_sheet(body, out))
    _log("seconds", round(time.time() - started, 1))
    return rig, body


def join(rig, name):
    """Every weighted part joined into one triangulated body, four weights a vertex, skinned to the rig."""
    parts = [o for o in scene.meshes() if not reference.is_reference(o) and not scene.is_collider(o)]
    if not parts:
        raise SystemExit("model.py built no body")
    for part in parts:
        for modifier in [m for m in part.modifiers if m.type == 'ARMATURE']:
            part.modifiers.remove(modifier)
        if not part.material_slots:
            part.data.materials.append(materials.flat(name + "_default", (0.5, 0.5, 0.5)))
        world = part.matrix_world.copy()
        part.parent = None
        part.matrix_world = world
    body = scene.join(parts, name)
    _triangulate(body)
    missing = gamerig_weights.finish(body, rig)
    if missing:
        raise SystemExit(f"gamerig: {missing} vertices of the body have no weight on a deform bone")
    body.parent = rig
    body.modifiers.new("armature", 'ARMATURE').object = rig
    _log("body", f"{scene.triangles(body)} triangles, {len(body.data.vertices)} vertices, "
                 f"{len(body.vertex_groups)} bones weighted")
    return body


def _fresh_out(asset_dir):
    out = os.path.join(asset_dir, "out")
    os.makedirs(out, exist_ok=True)
    for entry in os.listdir(out):
        if os.path.isfile(os.path.join(out, entry)):
            os.remove(os.path.join(out, entry))
    return out


def _load(asset_dir, name):
    spec = importlib.util.spec_from_file_location(f"asset_{name}", os.path.join(asset_dir, "model.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _triangulate(obj):
    """Triangulated before the unwrap, so what Unity gets is exactly what was baked and previewed."""
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.triangulate(mesh, faces=mesh.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    mesh.to_mesh(obj.data)
    mesh.free()


def _textures(body, name, settings):
    """The atlas: albedo with ambient occlusion multiplied in, a tangent-space normal map, the paint regions."""
    size = settings["TEXTURE_SIZE"]
    _log("bake device", bake.setup())
    bake.unwrap(body)
    _pack(body)
    albedo = bake.new_image(name + "_albedo", size)
    bake.albedo(body, albedo)
    if settings["AO_STRENGTH"] > 0:
        ao = bake.new_image(name + "_ao", size, data=True)
        bake.occlusion(body, ao)
        bake.multiply(albedo, ao, settings["AO_STRENGTH"])
        bpy.data.images.remove(ao)
    images = {"albedo": albedo, "normal": None, "regions": None}
    _log("texel density", f"{_density(body, size):.0f} px/m on a {size} px atlas")
    if settings["NORMAL_MAP"]:
        images["normal"] = bake.new_image(name + "_normal", size, data=True)
        bake.normal(body, images["normal"])
    if regions.present(body):
        images["regions"] = regions.bake_mask(body, name + "_regions", size)
        _log("regions", ", ".join(f"{r['name']} {r['share']:.0%} {r['albedo']}"
                                  for r in regions.table(images["regions"], albedo)))
    return images


def _pack(body):
    """Packs the unwrap's islands tighter than smart project leaves them, turning them to fit."""
    scene.select_only([body])
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.008)
    bpy.ops.object.mode_set(mode='OBJECT')


def _density(body, size):
    """Texels per metre of surface: the atlas size times the root of UV area over surface area."""
    mesh, uv = body.data, body.data.uv_layers.active.data
    uv_area = sum(abs(_uv_area([uv[i].uv for i in p.loop_indices])) for p in mesh.polygons)
    return size * (uv_area / max(sum(p.area for p in mesh.polygons), 1e-9)) ** 0.5


def _uv_area(points):
    return sum(a.x * b.y - b.x * a.y for a, b in zip(points, points[1:] + points[:1])) / 2


def _write_images(images, name, out):
    """PNGs beside the contract; returns the contract's texture entries."""
    files = {"albedo": "", "normal": "", "regions": "", "textureSize": images["albedo"].size[0]}
    for kind, image in images.items():
        if image is not None:
            files[kind] = f"{name}_{kind}.png"
            export.png(image, os.path.join(out, files[kind]))
    return files


def _codex_keys(images, files, body):
    """The keys the codex's tools read from an asset's manifest (codex/tools/recolour.py, the style check): the atlas
    files, the paint regions with their mask colours, shares and median colours, the texture size and the size."""
    keys = {"albedo": files["albedo"], "normal": files["normal"], "textureSize": files["textureSize"],
            "size": export.unity_size(*scene.bounds([body]))}
    if images["regions"] is not None:
        keys["regionMask"] = files["regions"]
        keys["regions"] = regions.table(images["regions"], images["albedo"])
    return keys


def _final_material(body, name, images):
    """The baked atlas in place of the recipe materials, for the .blend and the preview."""
    mat = bpy.data.materials.new(name)
    tree, bsdf = mat.node_tree, materials.principled(mat)
    albedo = tree.nodes.new('ShaderNodeTexImage')
    albedo.image, albedo.interpolation = images["albedo"], 'Closest'
    tree.links.new(albedo.outputs['Color'], bsdf.inputs['Base Color'])
    if images["normal"] is not None:
        texture, normal_map = tree.nodes.new('ShaderNodeTexImage'), tree.nodes.new('ShaderNodeNormalMap')
        texture.image = images["normal"]
        tree.links.new(texture.outputs['Color'], normal_map.inputs['Color'])
        tree.links.new(normal_map.outputs['Normal'], bsdf.inputs['Normal'])
    body.data.materials.clear()
    body.data.materials.append(mat)
    body.data.polygons.foreach_set("material_index", [0] * len(body.data.polygons))


def _log(key, value):
    print(f"WORKSHOP {key}: {value}", flush=True)

