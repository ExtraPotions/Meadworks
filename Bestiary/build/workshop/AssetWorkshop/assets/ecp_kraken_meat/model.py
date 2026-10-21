import os
import sys
sys.path.insert(0,os.path.abspath(os.path.join(os.path.dirname(__file__),'..','kraken_food')))
import food
TEXTURE_SIZE=256
NORMAL_MAP=False
AO_STRENGTH=.4
def build():
    food.build(False)
