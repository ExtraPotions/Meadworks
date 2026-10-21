import os
import sys
sys.path.insert(0,os.path.abspath(os.path.join(os.path.dirname(__file__),'..','kraken_beak_set')))
import design
TEXTURE_SIZE=512
NORMAL_MAP=False
AO_STRENGTH=.4
def build():
    design.build(True)
