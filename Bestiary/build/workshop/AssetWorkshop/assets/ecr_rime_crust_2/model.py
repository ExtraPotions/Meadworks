"""Rime Giant crust piece 2, the snow on the head (Head), shown only while it sleeps (Elite Creatures Reborn);
built by assets/ecr_rimegiant/rime_crust.py."""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecr_rimegiant"))

import rime_crust  # noqa: E402

TEXTURE_SIZE = rime_crust.TEXTURE_SIZE
AO_STRENGTH = rime_crust.AO_STRENGTH


def build():
    rime_crust.build(2)
