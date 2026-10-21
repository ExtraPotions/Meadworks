import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'bone_crossbow_set'))
from design import build_bolt
TEXTURE_SIZE=256
NORMAL_MAP=False
AO_STRENGTH=.35
def build(): build_bolt()
