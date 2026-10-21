"""Verify the loop seam, IK reach and fixed feet on the finished preview rig."""
import bpy
import os
import json
from mathutils import Vector
HERE = os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, 'out', 'skeleton_holding_axe.blend'))
s = bpy.context.scene
rig = bpy.data.objects['Original_Valheim_Skeleton']
axe = bpy.data.objects['ecp_bone_greataxe']
snapshots = {}
max_error = 0
feet = []
for frame in (1, 31, 61, 91, 121):
    s.frame_set(frame)
    bpy.context.view_layer.update()
    snapshots[frame] = [rig.matrix_world @ p.head for p in rig.pose.bones] + [axe.matrix_world.translation.copy()]
    feet.append([rig.matrix_world @ rig.pose.bones[side + 'Foot'].head for side in ('Left', 'Right')])
    for side in ('Left', 'Right'):
        wrist = rig.matrix_world @ rig.pose.bones[side + 'ForeArm'].tail
        target = bpy.data.objects['Original_Valheim_Skeleton_' + side + '_hand'].matrix_world.translation
        max_error = max(max_error, (wrist-target).length)
seam = max((a-b).length for a,b in zip(snapshots[1], snapshots[121]))
foot_drift = max((a-b).length for f in feet for a,b in zip(feet[0], f))
motion = max((a-b).length for a,b in zip(snapshots[1], snapshots[61]))
result = dict(loop_seam_metres=seam, hand_ik_error_metres=max_error,
              foot_drift_metres=foot_drift, motion_metres=motion, seconds=5)
print(json.dumps(result, indent=2))
assert seam < .001
assert max_error < .005
assert foot_drift < .005
assert motion > .005
with open(os.path.join(HERE, 'out', 'idle_validation.json'), 'w') as f:
    json.dump(result, f, indent=2)
s.frame_set(61)
s.render.filepath = os.path.join(HERE, 'out', 'idle_midpoint.png')
bpy.ops.render.render(write_still=True)
