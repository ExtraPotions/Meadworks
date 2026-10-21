"""One asset from model.py to Unity-ready files: build, join, unwrap, bake, export, preview.

A model.py may set TEXTURE_SIZE (atlas pixels, default 512), NORMAL_MAP (default True) and AO_STRENGTH
(0 to 1, how much baked ambient occlusion darkens the albedo, default 0.6), and CATEGORY (a codex category key such
as "weapon.axe_2h", written into the manifest for the style check). When a material names a paint region
(regions.mark), the build also bakes <name>_regions.png and lists the regions in the manifest. NORMAL_FROM_ALBEDO (a
strength, 2 to 4; 5 to 7 for bone, bark and stone) adds the albedo's own relief to the baked normal map, as the game's
are made; the paint recipes (paint.py) want it, with AO_STRENGTH 0.2 since they paint their own hollows.
"""
import importlib.util
import os
import time

import bpy

from . import bake, export, materials, paint_normal, preview, regions, scene

DEFAULTS = {"TEXTURE_SIZE": 512, "NORMAL_MAP": True, "AO_STRENGTH": 0.6, "CATEGORY": None, "NORMAL_FROM_ALBEDO": None}


def run(asset_dir, render_preview=True):
    started = time.time()
    name = os.path.basename(os.path.normpath(asset_dir))
    out = _fresh_out(asset_dir)
    scene.clear()
    model = _load(asset_dir, name)
    model.build()
    settings = {key: getattr(model, key, value) for key, value in DEFAULTS.items()}
    visual, colliders = _collect(name)
    images = _textures(visual, name, settings)
    _final_material(visual, name, images)
    _write(name, out, visual, colliders, images, settings)
    if render_preview:
        _log("preview", preview.render_sheet(visual, out))
    _log("seconds", round(time.time() - started, 1))


def _fresh_out(asset_dir):
    out = os.path.join(asset_dir, "out")
    os.makedirs(out, exist_ok=True)
    for entry in os.listdir(out):
        path = os.path.join(out, entry)
        if os.path.isfile(path):
            os.remove(path)
    return out


def _load(asset_dir, name):
    spec = importlib.util.spec_from_file_location(f"asset_{name}", os.path.join(asset_dir, "model.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _collect(name):
    parts = [o for o in scene.meshes() if not scene.is_collider(o)]
    colliders = [o for o in scene.meshes() if scene.is_collider(o)]
    if not parts:
        raise SystemExit("model.py built no visual mesh")
    fallback = materials.flat(name + "_default", (0.5, 0.5, 0.5))
    for part in parts:
        if not part.material_slots:
            part.data.materials.append(fallback)
    for collider in colliders:
        collider.hide_render = True
    return scene.join(parts, name), colliders


def _textures(visual, name, settings):
    size = settings["TEXTURE_SIZE"]
    _log("bake device", bake.setup())
    bake.unwrap(visual)
    albedo = bake.new_image(name + "_albedo", size)
    bake.albedo(visual, albedo)
    if settings["AO_STRENGTH"] > 0:
        ao = bake.new_image(name + "_ao", size, data=True)
        bake.occlusion(visual, ao)
        bake.multiply(albedo, ao, settings["AO_STRENGTH"])
        bpy.data.images.remove(ao)
    images = {"albedo": albedo, "normal": _normal(visual, name, size, albedo, settings)}
    if regions.present(visual):
        images["regions"] = regions.bake_mask(visual, name + "_regions", size)
    return images


def _normal(visual, name, size, albedo, settings):
    """The baked normal map, or None; with NORMAL_FROM_ALBEDO also the albedo's own relief on top, at that strength
    (the game's normal maps are the albedo's relief: codex/paint.md, paint_normal)."""
    if not settings["NORMAL_MAP"]:
        return None
    normal = bake.new_image(name + "_normal", size, data=True)
    bake.normal(visual, normal)
    if settings["NORMAL_FROM_ALBEDO"]:
        paint_normal.image_from_albedo(albedo, normal, settings["NORMAL_FROM_ALBEDO"])
    return normal


def _final_material(visual, name, images):
    """Replaces the recipe materials with the baked atlas, which is all Unity and the preview see."""
    mat = bpy.data.materials.new(name)
    tree, bsdf = mat.node_tree, materials.principled(mat)
    albedo = tree.nodes.new('ShaderNodeTexImage')
    albedo.image = images["albedo"]
    tree.links.new(albedo.outputs['Color'], bsdf.inputs['Base Color'])
    if images["normal"] is not None:
        texture, normal_map = tree.nodes.new('ShaderNodeTexImage'), tree.nodes.new('ShaderNodeNormalMap')
        texture.image = images["normal"]
        tree.links.new(texture.outputs['Color'], normal_map.inputs['Color'])
        tree.links.new(normal_map.outputs['Normal'], bsdf.inputs['Normal'])
    visual.data.materials.clear()
    visual.data.materials.append(mat)
    visual.data.polygons.foreach_set("material_index", [0] * len(visual.data.polygons))


def _write(name, out, visual, colliders, images, settings):
    files = {kind: f"{name}_{kind}.png" for kind, image in images.items() if image is not None}
    for kind, file in files.items():
        export.png(images[kind], os.path.join(out, file))
    export.fbx(os.path.join(out, name + ".fbx"), [visual] + colliders)
    low, high = scene.bounds([visual])
    data = {"asset": name, "fbx": name + ".fbx", "visual": name, "albedo": files["albedo"],
            "normal": files.get("normal", ""), "textureSize": settings["TEXTURE_SIZE"],
            "colliders": [c.name for c in colliders], "size": export.unity_size(low, high),
            "triangles": scene.triangles(visual)}
    _codex(data, images, files, settings)
    export.manifest(os.path.join(out, name + ".json"), data)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, name + ".blend"))
    for key in ("size", "triangles", "colliders", "category"):
        if key in data:
            _log(key, data[key])
    if "regions" in data:
        _log("regions", ", ".join(f"{r['name']} {r['share']:.0%} {r['albedo']}" for r in data["regions"]))


def _codex(data, images, files, settings):
    """The manifest's optional entries: the codex category and the paint regions, only when the model has them."""
    if settings["CATEGORY"]:
        data["category"] = settings["CATEGORY"]
    if images.get("regions") is not None:
        data["regionMask"] = files["regions"]
        data["regions"] = regions.table(images["regions"], images["albedo"])


def _log(key, value):
    print(f"WORKSHOP {key}: {value}", flush=True)
