"""Workshop helpers for building Valheim assets in Blender from a script.

A model.py imports what it needs (materials, shapes) and defines build(); pipeline.run does the rest.
Conventions: metres, Z up, the model's front faces -Y, its pivot (the world origin) sits on the ground under it.
Mesh objects named col_* are colliders; every other mesh is joined into one visual mesh.
"""
