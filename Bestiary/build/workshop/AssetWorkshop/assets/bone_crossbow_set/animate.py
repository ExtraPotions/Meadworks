"""Render the actual Blender firing demonstration for the review GIF."""
from pathlib import Path
import bpy
from mathutils import Vector
OUT=Path(__file__).resolve().parent/'out'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Gravebranch_editable.blend'))
s=bpy.context.scene; s.render.resolution_x=800; s.render.resolution_y=600
s.eevee.taa_render_samples=32
target=Vector((0,-.16,0)); s.camera.location=target+Vector((1,-1.4,2.3))*2
s.camera.rotation_euler=(target-s.camera.location).to_track_quat('-Z','Y').to_euler(); s.camera.data.ortho_scale=1.5
folder=OUT/'frames'; folder.mkdir(exist_ok=True)
for f in range(1,91,2):
    s.frame_set(f); s.render.filepath=str(folder/f'{f:03}.png'); bpy.ops.render.render(write_still=True)
