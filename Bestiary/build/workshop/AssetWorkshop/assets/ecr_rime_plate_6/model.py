"""Rime Giant plate 6, on the left thigh (Elite Creatures Reborn); built by assets/ecr_rimegiant/rime_plate.py."""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecr_rimegiant"))

import rime_plate  # noqa: E402

TEXTURE_SIZE = rime_plate.TEXTURE_SIZE
AO_STRENGTH = rime_plate.AO_STRENGTH


def build():
    rime_plate.build(6)
