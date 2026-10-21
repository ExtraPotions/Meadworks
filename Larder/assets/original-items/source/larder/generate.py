"""Original Larder feature-icon scenes. Copyright (C) 2026 expDARE.

SPDX-License-Identifier: GPL-3.0-or-later
"""
import math
import sys
from pathlib import Path

sys.path.insert(0,str(Path(__file__).resolve().parent.parent/"shepherd"))
from geometry import box,lathe,sphere,extrude,tube,transform,validate
from generate import export

WOOD=(0.42,0.255,0.125,1)
DARK_WOOD=(0.25,0.135,0.065,1)
IRON=(0.145,0.16,0.16,1)
SOUP=(0.34,0.14,0.045,1)
BREAD=(0.64,0.42,0.19,1)
GREEN=(0.23,0.34,0.095,1)
LEAF=(0.30,0.39,0.13,1)
GOLD=(0.63,0.43,0.13,1)
RED=(0.49,0.11,0.08,1)
PURPLE=(0.35,0.16,0.52,1)


def scene(name,title,parts):
    return {"id":"larder_"+name,"displayName":title,"prefabs":[],"kind":"feature-icon",
            "parts":parts,"icon":{"position":[1.7,1.35,3.0],"target":[0,0.35,0],"orthographicSize":0.66},
            "anchors":[],"collider":{"center":[0,0,0],"size":[0,0,0]},
            "attachment":{"scope":"existing Larder features only; no new items, recipes, status effects or skill identities"}}


def scaled(parts,scale,offset):
    return [transform(p,offset,scale=(scale,scale,scale)) for p in parts]


def bowl(prefix="bowl"):
    return [lathe(prefix,WOOD,[(0.12,0.035),(0.24,0.10),(0.34,0.25),(0.34,0.29),
                             (0.30,0.29),(0.29,0.245),(0.195,0.12),(0.10,0.095)],14),
            lathe(prefix+"_stew",SOUP,[(0.28,0.248),(0.28,0.254)],14,"food")]+foodbits(prefix)


def foodbits(prefix):
    parts=[]
    for i,(x,z) in enumerate([(-0.15,-0.08),(0.13,0.04),(-0.05,0.14),(0.06,-0.16)]):
        parts.append(box(prefix+"_root_chunk",(0.61,0.26,0.07,1),(x,0.278,z),(0.095,0.067,0.08),"food",rotation=(8,i*25,12)))
    for i,(x,z) in enumerate([(-0.13,0.03),(0.07,0.13),(0.13,-0.10)]):
        parts.append(sphere(prefix+"_meat",(0.34,0.17,0.095,1),(x,0.282,z),(0.11,0.07,0.09),8,3))
    for i,(x,z) in enumerate([(-0.2,0.01),(0.01,0.19),(0.21,0.10),(0.03,-0.08)]):
        parts.append(box(prefix+"_herb",GREEN,(x,0.289,z),(0.055,0.02,0.035),"leaf",rotation=(0,i*43,0)))
    return parts


def pot(food=True):
    parts=[lathe("blackened_pot",IRON,[(0.15,0.05),(0.25,0.10),(0.30,0.31),(0.285,0.405),
                 (0.25,0.405),(0.265,0.315),(0.21,0.15),(0.13,0.115)],14,"iron")]
    path=[(0.29*math.cos(a),0.41+0.31*math.sin(a),0) for a in [i*math.pi/12 for i in range(13)]]
    parts.append(tube("forged_swing_handle",IRON,path,0.019,6,"iron"))
    for x,z in [(-0.16,-0.14),(0.16,-0.14),(0,0.19)]:
        parts.append(box("short_iron_leg",IRON,(x,0.04,z),(0.055,0.09,0.055),"iron"))
    if food:
        parts.append(lathe("pot_stew",SOUP,[(0.25,0.35),(0.25,0.355)],14,"food"))
        parts+=scaled(foodbits("pot"),0.85,(0,0.13,0))
    else:
        parts.append(lathe("pot_lid",IRON,[(0.10,0.40),(0.30,0.405),(0.27,0.435),(0.06,0.47)],14,"iron"))
        parts.append(box("lid_grip",DARK_WOOD,(0,0.486,0),(0.12,0.04,0.035)))
    return parts


def ember(center,scale=1):
    return [sphere("banked_ember",(0.55,0.105,0.025,1),center,(0.16*scale,0.07*scale,0.12*scale),7,3,"ember")]


def cooking():
    parts=pot()
    parts.append(box("ladle_handle",WOOD,(0.39,0.35,0.10),(0.035,0.63,0.04),rotation=(0,0,-12)))
    parts.append(lathe("ladle_bowl",WOOD,[(0.035,0.025),(0.09,0.07),(0.075,0.10),(0.06,0.085),(0.025,0.045)],10,
                       center=(0.32,0.02,0.10)))
    parts+=ember((0,0.018,0.08))
    return scene("cooking","Cooking",parts)


def food():
    parts=bowl()
    outline=[(-0.13,0.05),(0.10,0.035),(0.14,0.29),(0.06,0.39),(-0.08,0.36)]
    parts.append(extrude("bread_crust",BREAD,outline,0.07,"food",center=(0.11,0.20,-0.10),rotation=(-10,0,-7)))
    parts.append(extrude("bread_crumb",(0.78,0.62,0.37,1),[(x*.78,y*.83) for x,y in outline],0.008,"food",center=(0.11,0.23,-0.056),rotation=(-10,0,-7)))
    return scene("food","Food",parts)


def mead():
    parts=[lathe("amber_mead_bottle",(0.48,0.255,0.035,1),[(0.15,0.025),(0.24,0.065),(0.25,0.34),
                  (0.17,0.47),(0.065,0.54),(0.065,0.65),(0.085,0.66)],10,"amber"),
           lathe("cork",BREAD,[(0.065,0.635),(0.07,0.735)],7,"cork"),
           lathe("leather_neck_tie",DARK_WOOD,[(0.079,0.585),(0.08,0.61)],10,"leather"),
           tube("tie_tail",DARK_WOOD,[(0.055,0.60,0.04),(0.10,0.49,0.12),(0.13,0.39,0.15)],0.013,5,"leather"),
           extrude("leather_neck_tag",WOOD,[(-.065,-.07),(.065,-.07),(.065,.055),(0,.095),(-.065,.055)],.018,"leather",center=(0.10,0.43,0.185),rotation=(0,0,-15))]
    return scene("mead","Mead",parts)


def fermentation():
    parts=[lathe("oak_fermenter",WOOD,[(.22,-.34),(.27,-.28),(.30,-.08),(.30,.08),(.27,.28),(.22,.34)],12,center=(-.10,.39,0),rotation=(0,0,90))]
    for x in (-.33,.13):
        parts.append(lathe("dark_barrel_hoop",IRON,[(.282,-.025),(.30,-.025),(.30,.025),(.282,.025)],12,"iron",center=(x,.39,0),rotation=(0,0,90)))
    for x in (-.31,.11):
        parts.append(box("fermenter_trestle",DARK_WOOD,(x,.09,0),(.11,.18,.48)))
    parts.append(lathe("bung",BREAD,[(.035,.66),(.035,.72)],7,center=(-.10,0,0)))
    parts.append(lathe("ceramic_foam_cup",(0.62,.52,.35,1),[(.075,0),(.09,.23),(.075,.23),(.06,.04)],10,"ceramic",center=(.37,.02,.20)))
    for i in range(4):
        parts.append(sphere("mead_foam",(.80,.74,.58,1),(.37+(i%2)*.05-.025,.26+(i%2)*.01,.2+(i//2)*.05-.025),(.10,.06,.10),7,3,"foam"))
    return scene("fermentation","Fermentation",parts)


def cook_time():
    parts=scaled(pot(False),.78,(-.22,0,0))
    for y in (.035,.60): parts.append(lathe("hourglass_wood_end",WOOD,[(.145,y),(.145,y+.045)],8,center=(.26,0,0)))
    for x,z in [(-.10,-.08),(.10,-.08),(.10,.08)]:
        parts.append(box("hourglass_spindle",DARK_WOOD,(.26+x,.325,z),(.025,.52,.025)))
    parts.append(lathe("sand_glass",(.54,.48,.32,1),[(.10,.09),(.10,.17),(.016,.32),(.10,.48),(.10,.59)],10,"sandglass",center=(.26,0,0)))
    parts.append(lathe("visible_sand",(.75,.59,.32,1),[(.065,.10),(.065,.16),(.005,.22)],9,"sand",center=(.26,0,.02)))
    return scene("cook_time","Cook Time",parts)


def burn_warning():
    parts=[sphere("charred_meat",(.115,.09,.065,1),(-.12,.30,.03),(.45,.22,.27),9,4)]
    for x in (-.13,.13):
        parts.append(box("iron_tong_jaw",IRON,(x-.12,.32,.07),(.035,.035,.38),"iron",rotation=(0,0,0)))
        parts.append(box("iron_tong_handle",IRON,(x+.09,.35,-.26),(.035,.035,.49),"iron",rotation=(0,22 if x<0 else -22,0)))
    for i in range(4):
        parts.append(box("meat_sear_fissure",(.35,.055,.018,1),(-.28+i*.10,.407,.03),(.018,.008,.16),"ember",rotation=(0,12,0)))
    parts+=ember((-.31,.39,.11),.35)
    return scene("burn_warning","Burn Warning",parts)


def leaf(name,center,size,rotation=(0,0,0),color=GREEN):
    shape=[(0,-.5),(.26,-.15),(.30,.15),(0,.5),(-.28,.12),(-.21,-.17)]
    return transform(extrude(name,color,shape,.025,"leaf"),center,rotation,scale=size)


def sprig(x=-.20,y=.40,z=-.04,scale=1):
    parts=[tube("herb_stem",DARK_WOOD,[(x,y-.25*scale,z),(x+.08*scale,y+.22*scale,z)],.011*scale,5)]
    for i in range(5):
        parts.append(leaf("herb_leaf",(x+(i%2)*.10*scale-.03*scale,y-.15*scale+i*.075*scale,z+.015),(.16*scale,.24*scale,1),rotation=(0,0,-40 if i%2 else 35),color=LEAF))
    return parts


def health():
    heart=[(0,-.30),(.29,-.03),(.35,.15),(.28,.29),(.13,.31),(0,.19),(-.13,.31),(-.28,.29),(-.35,.15),(-.29,-.03)]
    parts=sprig(.19,.34,-.065,.9)
    parts.append(extrude("carved_wood_heart",WOOD,heart,.11,center=(0,.35,0)))
    parts.append(extrude("red_heart_inlay",RED,[(x*.66,y*.66) for x,y in heart],.018,"paint",center=(0,.35,.065)))
    return scene("health_recovery","Health Recovery",parts)


def stamina():
    foot=[(-.27,-.25),(-.10,-.26),(.06,-.19),(.16,-.04),(.18,.08),(.30,.34),(.17,.43),(.03,.23),(-.015,.03),(-.15,-.10),(-.29,-.12)]
    parts=[extrude("carved_running_foot",WOOD,foot,.10,center=(.03,.33,.01)),
           tube("gold_foot_carving",GOLD,[(-.19,.15,.073),(-.04,.20,.073),(.10,.34,.073),(.19,.61,.073)],.025,5,"paint"),
           tube("barley_stem",GOLD,[(-.25,.07,-.02),(-.21,.40,-.02),(-.34,.66,-.02)],.012,5,"barley")]
    for i in range(5):
        for side in (-1,1):
            parts.append(leaf("barley_seed",(-.24+side*.043-i*.011,.33+i*.062,-.02),(.10,.14,1),(0,0,side*38),GOLD))
    return scene("stamina_recovery","Stamina Recovery",parts)


def eitr():
    outline=[(.30*math.cos(i*math.tau/9),.30*math.sin(i*math.tau/9)) for i in range(9)]
    parts=[extrude("carved_stone_spiral_token",(.33,.32,.30,1),outline,.12,"stone",center=(0,.35,0))]
    path=[]
    for i in range(25):
        a=i*math.pi*3.3/24
        r=.225*(1-i/29)
        path.append((r*math.cos(a),.35+r*math.sin(a),.071))
    parts.append(tube("restrained_violet_spiral",PURPLE,path,.023,5,"eitr"))
    return scene("eitr_recovery","Eitr Recovery",parts)


def rested():
    wool=(.235,.265,.285,1)
    parts=[lathe("rolled_wool_blanket",wool,[(.20,-.34),(.23,-.28),(.23,.28),(.20,.34)],12,"wool",center=(0,.31,0),rotation=(0,0,90))]
    path=[]
    for i in range(31):
        a=i*math.pi*5/30; r=.185*(1-i/35)
        path.append((.349,.31+r*math.sin(a),r*math.cos(a)))
    parts.append(tube("visible_wool_roll",(.34,.365,.37,1),path,.022,5,"wool"))
    for x in (-.21,.21):
        parts.append(lathe("blanket_leather_strap",DARK_WOOD,[(.233,-.025),(.246,-.025),(.246,.025),(.233,.025)],12,"leather",center=(x,.31,0),rotation=(0,0,90)))
    parts.append(lathe("ember_bowl",(.30,.29,.26,1),[(.10,.015),(.14,.08),(.12,.105),(.09,.055)],9,"stone",center=(.26,0,.30)))
    parts+=ember((.26,.085,.30),.8)
    return scene("rested","Rested",parts)


def extra_food():
    return scene("extra_food","Extra Food",scaled(bowl("rear_bowl"),.85,(.13,.17,-.12))+scaled(bowl("front_bowl"),.9,(-.13,0,.18)))


def saved():
    hand=[(-.25,-.25),(-.12,-.25),(-.02,-.13),(.20,-.02),(.31,.10),(.30,.19),(.24,.19),(.14,.10),(.06,.09),(.19,.24),(.17,.30),(.10,.27),(-.02,.13),(-.11,.06),(-.15,.17),(-.22,.17),(-.20,.02),(-.29,-.12)]
    parts=[extrude("protective_carved_hand",WOOD,hand,.08,center=(0,.28,.10),rotation=(0,-12,-10)),
           sphere("saved_turnip",(.47,.31,.26,1),(.05,.40,.055),(.25,.25,.24),9,4),
           lathe("turnip_purple_crown",(.34,.14,.28,1),[(.11,.45),(.10,.48),(.04,.52)],9,"food",center=(.05,0,.055))]
    parts+=sprig(-.22,.40,-.05,.85)
    for i in range(3): parts.append(leaf("turnip_leaf",(.02+i*.05,.61+i*.015,.045),(.16,.25,1),(0,0,-35+i*30),LEAF))
    parts.append(tube("herb_bundle_tie",BREAD,[(-.30,.28,-.02),(-.17,.29,-.02),(-.18,.32,.04),(-.30,.31,.04)],.017,5,"rope",closed=True))
    return scene("saved_ingredients","Saved Ingredients",parts)


if __name__=="__main__":
    assets=[cooking(),food(),mead(),fermentation(),cook_time(),burn_warning(),health(),stamina(),eitr(),rested(),extra_food(),saved()]
    count=sum(validate(a["parts"]) for a in assets)
    export("Larder",assets,Path(__file__).with_name("models.json"))
    print(f"Larder: {len(assets)} feature-icon scenes; {count} nondegenerate triangles; no runtime item identities")
