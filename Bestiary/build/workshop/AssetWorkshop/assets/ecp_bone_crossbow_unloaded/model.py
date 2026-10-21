import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'bone_crossbow_set'))
from design import build_crossbow
TEXTURE_SIZE=512
NORMAL_MAP=False
AO_STRENGTH=.45
def build(): build_crossbow(False)
