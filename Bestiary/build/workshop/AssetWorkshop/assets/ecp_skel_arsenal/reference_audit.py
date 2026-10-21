"""Inventory all local Skeleton assets and render the original creature/weapon prefabs."""
import os
import sys
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent.parent / 'blender'))
import bpy
from mathutils import Vector
from workshop import reference, prefab, preview, scene

root = Path(reference.ROOT)
out = HERE / 'out' / 'reference_audit'
out.mkdir(parents=True, exist_ok=True)
assets = sorted(p for p in (root / 'Characters' / 'Skeleton').rglob('*') if p.is_file() and p.suffix != '.meta')
inventory = [{'path': p.relative_to(root).as_posix(), 'bytes': p.stat().st_size} for p in assets]
(out / 'inventory.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
results = []
for category, paths in (
    ('creatures', [p for p in assets if p.suffix == '.prefab' and p.parent.name == 'Skeleton' and p.name.startswith('Skeleton')]),
    ('weapons', [p for p in assets if p.suffix == '.prefab' and p.parent.name == 'weapons' and any(s in p.stem for s in ('sword', 'mace', 'bow'))]),
):
    images = []
    for path in paths:
        scene.clear()
        rel = path.relative_to(root).as_posix()
        try:
            obj = prefab.load(rel)
            bpy.context.view_layer.update()
            meshes = prefab.meshes(obj)
            if not meshes:
                raise ValueError('no visible meshes')
            s = bpy.context.scene
            s.render.engine = 'BLENDER_EEVEE'
            s.eevee.taa_render_samples = 16
            s.render.resolution_x = s.render.resolution_y = 480
            s.view_settings.view_transform = 'Standard'
            preview._world(s)
            preview._sun()
            target = str(out / (path.stem + '.png'))
            preview._render(preview._camera(), meshes, Vector((0.5, -1, 0.3)), True, target)
            images.append(target)
            results.append({'path': rel, 'meshes': len(meshes), 'image': target})
        except Exception as exc:
            results.append({'path': rel, 'error': str(exc)})
            print('WORKSHOP reference failed', rel, exc)
    if images:
        preview.grid(images, 4, str(out / (category + '.png')))
(out / 'review.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
print('WORKSHOP audited', len(inventory), 'assets;', len(results), 'prefabs')
