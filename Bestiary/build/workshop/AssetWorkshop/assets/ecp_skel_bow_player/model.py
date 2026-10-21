"""The skeleton arsenal's bow as players hold it (Elite Creatures Pack's ECP_BoneBow): the same bow as ecp_skel_bow, turned
into the player's bow hold instead of the skeleton archer's. The game holds a player's bow differently from a skeleton's:
in the attach frame of its Bow (the crude bow), the stave runs along LIMBS and the string lies along STRING from the
grip (measured from Bow.prefab's mesh, Blender axes), where skeleton_bow lies slanted. Everything else is ecp_skel_bow's
build; the tips go to out/ecp_skel_bow_player_points.json for the mod's string.
"""
import importlib.util
import os

from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("ecp_skel_bow_model", os.path.join(HERE, "..", "ecp_skel_bow", "model.py"))
bow = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bow)

TEXTURE_SIZE = bow.TEXTURE_SIZE
AO_STRENGTH = bow.AO_STRENGTH
CATEGORY = bow.CATEGORY
NORMAL_MAP = bow.NORMAL_MAP

bow.STRING = Vector((0.995, 0.041, -0.092))       # Bow.prefab: from the grip towards the string
bow.LIMBS = Vector((-0.026, 0.987, 0.158))        # Bow.prefab: along the stave
bow.TOP = 1
bow.POINTS_FILE = os.path.join(HERE, "out", "ecp_skel_bow_player_points.json")


def build():
    bow.build()
