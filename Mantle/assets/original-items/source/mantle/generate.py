# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
"""Generate the 21 approved Mantle model sources without Unity or game assets."""
import hashlib
import json
import math
from pathlib import Path
from mantle_geometry import Geometry

ROOT = Path(__file__).resolve().parent


def material(id, color, native, metallic=0, emission=None):
    return dict(id=id,color=[int(color[i:i+2],16)/255 for i in (0,2,4)]+[1],roughness=.9 if not metallic else .65,
                metallic=metallic,emission=emission or [0,0,0],nativePrefab=native)


MATERIALS = [
    material('deer_leather','6B4730','DeerHide'), material('strap','463026','LeatherScraps'),
    material('troll_hide','384D67','TrollHide'),material('bog_leather','414832','Root'),
    material('root','64543C','Root'),material('guck','839544','Guck',emission=[.025,.045,.005]),
    material('dark_leather','403830','LeatherScraps'),material('wolf_fur','B8B8AB','WolfPelt'),
    material('wolf_fur_shadow','969A90','WolfPelt'),
    material('lox_fur','9C8162','LoxPelt'),material('lox_fur_shadow','80674E','LoxPelt'),
    material('winter_fur','C5B9A0','WolfPelt'),material('winter_fur_shadow','A79D86','WolfPelt'),
    material('carapace','4B5667','Carapace',.25),material('carapace_ridge','657182','Carapace',.25),material('blue_jute','426C8C','JuteBlue'),
    material('ask_hide','483A32','AskHide'),material('flametal','B66A37','FlametalNew',.7),
    material('ember_seam','D18E40','FlametalNew',.4,[.1,.025,.002]),
    material('wood','7A5735','Wood'),material('finewood','94734B','FineWood'),
    material('yggdrasil','57523F','YggdrasilWood'),material('bone','C5BA94','BoneFragments'),
    material('bronze','9D773C','Bronze',.7),material('copper','B96D46','Copper',.7),
    material('iron','585B60','Iron',.7),material('blood','542C27','Bloodbag'),
    material('silver','AAAFAB','Silver',.8),material('black_metal','34383B','BlackMetal',.7),
    material('gold','B18A3B','Coins',.8),material('amber','C68126','Amber',.05),
    material('eitr','6B4F94','Eitr',.05,[.03,.01,.06]),material('eitr_core','69A492','Eitr',.05,[.02,.06,.035]),
    material('thread','BBA67C','LinenThread')]


def tube(g,name,mat,points,r=.005,sides=5): return g.tube(name,mat,points,r,sides)


def buckle(g,name,mat,c,size=.055):
    x,y,z=c
    a=size/2
    for i,(p,q) in enumerate((((x-a,y-a,z),(x-a,y+a,z)),((x+a,y-a,z),(x+a,y+a,z)),((x-a,y-a,z),(x+a,y-a,z)),((x-a,y+a,z),(x+a,y+a,z)))):
        tube(g,name+str(i),mat,[p,q],size*.065,4)
    tube(g,name+'_tongue',mat,[(x-a*.7,y,z+.003),(x+a*.5,y,z+.003)],size*.045,4)


def diamond(g,name,mat,x,y,z,w=.055):
    g.profile(name,mat,[(x,y+w*.6),(x+w*.5,y),(x,y-w*.6),(x-w*.5,y)],z,.012)


def stitches(g,w,h,z,y=-.12,mat='thread'):
    for side in (-1,1):
        x=side*w*.38
        for j in range(7):
            py=y-h*.32+j*h*.085
            tube(g,f'side_seam_{side}_{j}',mat,[(x-.009,py-.007,z),(x+.009,py+.007,z)],.0018,4)


def shoulder_straps(g,w,h,z):
    for side in (-1,1):
        x=side*w*.28
        points=[(x,-.12+h*.35,z-.05),(x,.16+h*.2,.08),(x,.20+h*.2,.015),(x,-.16,.015),(x,-.12-h*.33,z-.05)]
        g.strap(f'shoulder_strap_{side}','strap',points,.033)
        buckle(g,f'strap_buckle_{side}','bronze',(x,-.18,.025),.035)


def flap(g,w,h,z,mat,ragged=False):
    y=.12
    outline=[(-w*.46,y+h*.13),(w*.46,y+h*.13),(w*.48,y-.10),(w*.25,y-.15),(0,y-.17),(-w*.25,y-.15),(-w*.48,y-.10)]
    g.profile('folded_flap',mat,outline,z,.016)
    if ragged:
        for i in range(5):
            x=(i-2)*w*.17
            g.profile('ragged_flap_'+str(i),mat,[(x-.025,y-.115),(x+.02,y-.115),(x+.013,y-.19-.008*(i%2))],z+.008,.008)


def cushion(g,name,mat,outline,z,bulge,depth=.008):
    if sum(outline[i][0]*outline[(i+1)%len(outline)][1]-outline[(i+1)%len(outline)][0]*outline[i][1] for i in range(len(outline)))<0:
        outline=list(reversed(outline))
    center=[sum(p[k] for p in outline)/len(outline) for k in range(2)]
    p=g.part(name,mat)
    front=[[x,y,z] for x,y in outline];back=[[x,y,z-depth] for x,y in outline]
    for i in range(len(outline)):
        n=(i+1)%len(outline)
        g.face(p,[[center[0],center[1],z+bulge],front[i],front[n]])
        g.face(p,[[center[0],center[1],z-depth],back[n],back[i]])
        g.face(p,[back[i],back[n],front[n],front[i]])
    return p


def fur(g,name,mat,w,y,z,height=.18,rows=2):
    shadow=mat+'_shadow' if mat in ('wolf_fur','lox_fur','winter_fur') else mat
    tuft_width=w/10.0
    length=min(.075,height*.36)
    for row in range(rows):
        for j in range(11):
            x=(j-5)*w/11+.009*math.sin(j*1.7+row*2.3)
            py=y-row*length*.65+.012*math.sin(j*2.1+row)
            span=tuft_width*(.65+.14*((j+row)%4));drop=length*(.54+.16*((j+row)%4))
            sway=.014*math.sin(j*2.1+row)
            outline=[(x-span*.46,py+.012),(x+span*.41,py+.008),(x+span*.43,py-drop*.31),
                     (x+sway,py-drop),(x-span*.35,py-drop*.38)]
            surface=z+.005*(rows-row)+.008*math.cos((j-5)*.55)
            cushion(g,f'{name}_{row}_{j}',shadow if (j+row)%4==0 else mat,outline,surface,.010,.006)


def plates(g,w,h,z,mat='carapace',rows=4):
    for row in range(rows):
        for col in range(3):
            x=(col-1)*w*.29+(.012 if row%2 else -.012)
            y=.14-row*h*.215
            outline=[(x-w*.17,y+.07),(x+w*.12,y+.075),(x+w*.175,y+.03),
                     (x+w*.16,y-.035),(x+w*.035,y-.12),(x-w*.08,y-.095),(x-w*.175,y-.025)]
            surface=z+.017*(rows-row)-.020*abs(col-1)
            cushion(g,f'domed_shell_{row}_{col}',mat,outline,surface,.026,.010)
            if mat=='carapace':
                tube(g,f'shell_keel_{row}_{col}','carapace_ridge',[(x-.008,y+.03,surface+.023),(x+.003,y-.045,surface+.026),(x+w*.035,y-.10,surface+.008)], [.004,.004,.002],4)


PACKS=[
 ('Mantle_DeerhideSatchel','Deerhide Satchel','deer_leather',.37,.49,.19),
 ('Mantle_TrollhideBackpack','Trollhide Backpack','troll_hide',.44,.61,.23),
 ('Mantle_RootboundPack','Rootbound Pack','bog_leather',.42,.66,.21),
 ('Mantle_WolfpeltPack','Wolfpelt Pack','dark_leather',.47,.65,.23),
 ('Mantle_LoxHauler','Lox Hauler','lox_fur',.57,.64,.28),
 ('Mantle_CarapacePack','Carapace Pack','dark_leather',.46,.65,.24),
 ('Mantle_AsksvinPack','Asksvin Pack','ask_hide',.46,.65,.24),
 ('Mantle_MoosehidePack','Moosehide Pack','deer_leather',.46,.73,.24)]


def pack(index,spec):
    prefab,name,mat,w,h,d=spec
    g=Geometry();z=.11+d/2;front=z+d*.48
    g.sack('sewn_body',mat,w,h,d,z=z)
    shoulder_straps(g,w,h,z)
    flap(g,w,h,front,mat,index in (1,6))
    stitches(g,w,h,front+.012)
    if index==0:
        tube(g,'bone_toggle','bone',[(-.048,-.06,front+.032),(.045,-.052,front+.032)],.009,6)
        tube(g,'toggle_tie','strap',[(0,.05,front+.023),(0,-.12,front+.04)],.007,5)
    elif index==1:
        for x in (-w*.27,w*.27):
            g.strap('long_front_strap','strap',[(x,.12,front+.025),(x,-.27,front+.035),(x,-.35,front+.015)],.028)
            g.annulus('bronze_flap_disc','bronze',.026,.013,.011,10,(x,.01,front+.04))
            buckle(g,'strap_clasp','bronze',(x,-.17,front+.045),.045)
    elif index==2:
        for s in (-1,1):
            tube(g,'root_side_'+str(s),'root',[(s*w*.36,-.43,front-.03),(s*w*.47,-.22,front+.01),(s*w*.40,.01,front+.02),(s*w*.31,.23,front-.005)], [.023,.021,.024,.016],6)
        for j in range(3):
            y=-.35+j*.23
            tube(g,'root_cross_'+str(j),'root',[(-w*.40,y,front+.025),(0,y+.025*(-1)**j,front+.055),(w*.40,y+.06,front+.025)],.018,6)
            for k in (-1,1): tube(g,'guck_seam','guck',[(k*w*.36,y,front+.035),(k*w*.31,y+.055,front+.04)],.004,5)
        buckle(g,'bog_clasp','bronze',(0,-.02,front+.078),.045)
    elif index==3:
        g.sack('bulky_wolf_ruff','wolf_fur',w*1.04,.17,.09,y=.15,z=front+.015)
        fur(g,'wolf_ruff','wolf_fur',w*1.04,.22,front+.055,.21,4)
        for x in (-.12,.12): g.strap('ruff_binding','strap',[(x,.24,front-.05),(x,.12,front+.07),(x,-.03,front+.065)],.032)
        diamond(g,'silver_clasp','silver',0,-.08,front+.09,.082)
        diamond(g,'clasp_dark_inset','dark_leather',0,-.08,front+.101,.044)
        tube(g,'silver_drop','silver',[(0,-.13,front+.09),(0,-.21,front+.09)],.008,4)
    elif index==4:
        for x in (-w*.42,w*.42):
            tube(g,'black_metal_frame','black_metal',[(x,-.46,front+.08),(x,.22,front+.08)],.019,4)
        for y in (-.38,-.10,.18): tube(g,'frame_cross','black_metal',[(-w*.46,y,front+.085),(w*.46,y,front+.085)],.018,4)
        fur(g,'lox_wrap','lox_fur',w,.19,front+.005,.27,8)
        for x in (-w*.27,w*.27):
            tube(g,'rope_lashing','thread',[(x,-.44,front+.09),(x+.014,-.12,front+.10),(x,.22,front+.09)],.009,6)
        for x in (-w*.42,w*.42):
            for y in (-.37,.17): diamond(g,'frame_rivet','iron',x,y,front+.103,.028)
    elif index==5:
        plates(g,w,h,front)
        for s in (-1,1): tube(g,'blue_jute_binding','blue_jute',[(s*w*.3,.22,front+.085),(s*w*.40,-.05,front+.09),(s*w*.25,-.43,front+.025)],.011,6)
        tube(g,'jute_cross','blue_jute',[(-w*.43,-.12,front+.08),(0,-.10,front+.11),(w*.43,-.11,front+.08)],.010,6)
    elif index==6:
        plates(g,w,h,front,'ask_hide',3)
        for s in (-1,1):
            g.strap('ash_binding','strap',[(s*w*.30,.18,front+.06),(s*w*.31,-.40,front+.03)],.027)
            for y in (-.36,.18):
                g.profile('flametal_corner','flametal',[(s*w*.44,y+.04),(s*w*.28,y+.04),(s*w*.28,y-.045),(s*w*.40,y-.065)],front+.065,.018)
                tube(g,'hot_corner_seam','ember_seam',[(s*w*.39,y+.025,front+.078),(s*w*.39,y-.035,front+.078)],.0025,4)
        diamond(g,'flametal_clasp','flametal',0,-.02,front+.105,.055)
    else:
        g.sack('bulky_winter_ruff','winter_fur',w*1.06,.18,.09,y=.16,z=front+.012)
        fur(g,'winter_ruff','winter_fur',w*1.08,.235,front+.055,.20,4)
        for s in (-1,1):
            x=s*w*.28
            g.strap('expedition_strap','strap',[(x,.24,front+.055),(x,-.45,front+.025)],.035)
            buckle(g,'gold_fastener','gold',(x,.11,front+.07),.048)
            for y in (-.12,-.32):
                tube(g,'antler_toggle','bone',[(x-s*.06,y+.015,front+.075),(x-s*.02,y-.015,front+.10),(x+s*.045,y+.03,front+.08),(x+s*.060,y+.072,front+.065)],[.004,.010,.006,.0015],6)
        diamond(g,'gold_catch','gold',0,-.015,front+.075,.07)
    g.rotate_pack()
    asset=finish(g,prefab,name,'backpack')
    asset['attachment']={'bone':'Spine2','position':[0,0,0],'rotationCorrectionQuaternion':[.0494,0,0,.9988],'scale':[1,1,1],
                         'frame':'Player upright frame, outward -Z; existing BackpackMount handles inverse rest quaternion',
                         'spineHeightMeters':1.4524,'bodyBackClearanceMeters':.11,'fitVerifiedInGame':False}
    asset['anchors']=[{'name':'spine2','position':[0,0,0]},{'name':'outward_face','position':[0,-.12,-front]}]
    asset['icon']['position']=[-.9,.5,-1.2]
    return asset


TACKLE=[('Mantle_DriftwoodTacklebox','Driftwood Tacklebox'),('Mantle_FinewoodTacklebox','Finewood Tacklebox'),
        ('Mantle_CarapaceTacklebox','Carapace Tacklebox'),('Mantle_FlametalTacklebox','Flametal Tacklebox')]


def tackle(index,spec):
    prefab,name=spec;g=Geometry();w,h,d=.51,.23,.23
    wood=('wood','finewood','yggdrasil','flametal')[index]
    for row in range(3):
        for z in (-d/2,d/2): g.box('side_plank',wood,(0,.04+row*.065,z),(w,.059,.022))
    for x in (-w/2,w/2): g.box('end_board',wood,(x,.115,0),(.025,h,d))
    g.box('floor',wood,(0,.008,0),(w,.022,d));g.box('lid',wood,(0,h+.006,0),(w+.018,.035,d+.018))
    if index==0:
        fur(g,'fur_lid','lox_fur',w*.85,h+.03,d/2+.02,.13,1)
        for x in (-.18,.18):
            g.strap('leather_band','strap',[(x,.02,d/2+.018),(x,h+.045,d/2+.02),(x,h+.055,-d/2-.02),(x,.03,-d/2-.02)],.026)
        buckle(g,'bone_clasp','bone',(0,.16,d/2+.027),.043)
    elif index==1:
        g.box('troll_leather_lid','troll_hide',(0,h+.029,0),(w-.06,.018,d+.015))
        g.box('front_leather','troll_hide',(0,.13,d/2+.015),(w-.08,.13,.015))
        for x in (-.24,.24):
            for z in (-d/2-.014,d/2+.014):
                g.box('bronze_corner','bronze',(x,.13,z),(.055,h+.035,.018))
                for y in (.05,.21): diamond(g,'corner_rivet','iron',x,y,z+.012,.014)
        buckle(g,'bronze_clasp','bronze',(0,.17,d/2+.032),.052)
    elif index==2:
        for j in range(7):
            x=(j-3)*.073
            p=cushion(g,'carapace_lid_plate','carapace',[(x-.055,-.11),(x+.04,-.11),(x+.057,-.035),(x+.033,.055),(x,.125),(x-.042,.075),(x-.056,-.025)],.006*(j%2),.024,.012)
            # Rotate lid plates from their XY authoring plane into the horizontal XZ plane.
            for key in ('vertices','normals'):
                for k in range(0,len(p[key]),3):
                    y,z=p[key][k+1],p[key][k+2]
                    p[key][k+1]=h+.04+z if key=='vertices' else z
                    p[key][k+2]=-y
        for x in (-.18,.18): tube(g,'blue_jute_lashing','blue_jute',[(x,.02,d/2+.025),(x,h+.07,d/2+.025),(x,h+.07,-d/2-.025),(x,.02,-d/2-.025)],.008,6)
        diamond(g,'bronze_lid_seal','bronze',0,.16,d/2+.04,.052)
    else:
        g.box('asksvin_lid','ask_hide',(0,h+.03,0),(w-.02,.016,d+.015))
        g.box('asksvin_front','ask_hide',(0,.13,d/2+.017),(w-.04,.14,.016))
        for x in (-.23,.23):
            for z in (-d/2-.016,d/2+.016):
                g.box('flametal_corner','flametal',(x,.13,z),(.055,h+.04,.022))
                tube(g,'glowing_corner','ember_seam',[(x,.045,z+.014),(x,.215,z+.014)],.0025,4)
        diamond(g,'flametal_lock','flametal',0,.16,d/2+.045,.06)
    return finish(g,prefab,name,'tacklebox')


RINGS=[('Mantle_BoneBand','Bone Band','bone'),('Mantle_CopperRing','Copper Ring','copper'),('Mantle_IronRing','Iron Ring','iron'),
       ('Mantle_SilverRing','Silver Ring','silver'),('Mantle_BlackMetalRing','Black Metal Ring','black_metal'),('Mantle_EitrRing','Eitr Ring','silver')]


def ring(index,spec):
    prefab,name,mat=spec;g=Geometry();outer=.017 if index==2 else .016
    g.annulus('forged_band',mat,outer,.0105,.0085,18)
    if index==0:
        for x in (-.003,0,.003):
            tube(g,'leather_binding','strap',[(x,.0125,-.005),(x,.0175,-.004),(x,.0175,.005),(x,.0125,.006)],.0010,4)
    elif index==1:
        diamond(g,'amber_setting','amber',0,.016,.005,.008)
        for x in (-.004,.004): tube(g,'iron_prong','iron',[(x,.013,.005),(x*.8,.018,.008)],.0007,4)
    elif index==2:
        g.box('heavy_square_face','iron',(0,.016,.001),(.017,.011,.012))
        for x in (-.009,.009): tube(g,'inside_blood_stain','blood',[(x,-.004,.004),(x,-.008,.004)],.0008,4)
    elif index==3:
        for j in range(5):
            a=(j-2)*.30+math.pi/2
            x,y=.0132*math.cos(a),.0132*math.sin(a)
            tube(g,'cut_frost_rune','dark_leather',[(x-.001,y-.001,.0044),(x,y+.0014,.0044),(x+.0013,y-.001,.0044)],.00030,3)
    elif index==4:
        for j in range(5):
            a=math.pi/2+(j-2)*.40;x,y=.015*math.cos(a),.015*math.sin(a)
            tube(g,'needle_wire','bronze',[(x*.83,y*.83,-.005),(x*1.09,y*1.09,-.004),(x*1.09,y*1.09,.005),(x*.83,y*.83,.005)],.00075,5)
    else:
        for s in (-1,1):
            g.profile('low_silver_cradle','silver',[(s*.011,.010),(s*.009,.018),(s*.005,.022),(s*.005,.014)],.002,.006)
        purple=g.part('squat_eitr_cut','eitr');green=g.part('eitr_inclusion_facets','eitr_core')
        girdle=[];table=[]
        for j in range(8):
            a=j*math.tau/8
            girdle.append([.0065*math.cos(a),.0165+.0055*math.sin(a),.006])
            table.append([.0033*math.cos(a),.0165+.0028*math.sin(a),.009])
        g.cap(purple,table,False)
        for j in range(8):
            n=(j+1)%8
            g.face(green if j in (1,2) else purple,[girdle[j],girdle[n],table[n],table[j]])
            g.face(purple,[[0,.0165,.001],girdle[n],girdle[j]])
    asset=finish(g,prefab,name,'ring');asset['attachment']={'display':'dropped and inventory only; existing accessory slot has no finger attachment','actualOuterDiameterMeters':outer*2}
    asset['icon']['position']=[.055,.043,.08]
    return asset


BELTS=[('Mantle_LeatherBelt','Leather Belt','deer_leather','bone'),('Mantle_TrollHideBelt','Troll Hide Belt','troll_hide','bronze'),('Mantle_LoxBelt','Lox Belt','dark_leather','black_metal')]


def belt(index,spec):
    prefab,name,mat,metal=spec;g=Geometry();height=(.065,.078,.09)[index]
    p=g.part('open_coiled_belt',mat)
    rings=[]
    for j in range(49):
        a=-.12+j/48*math.tau*1.20;r=.13-j/48*.026
        rings.append([[math.sin(a)*(r+dr),y,math.cos(a)*(r+dr)] for dr,y in ((-.003,-height/2),(.003,-height/2),(.003,height/2),(-.003,height/2))])
    for j in range(48):
        for k in range(4):
            n=(k+1)%4;g.face(p,[rings[j][k],rings[j+1][k],rings[j+1][n],rings[j][n]])
    g.face(p,list(reversed(rings[0])));g.face(p,rings[-1])
    buckle(g,'square_buckle',metal,(-.014,0,.141),height*.82)
    for x in (-.048,-.064): g.box('keeper_loop','strap',(x,0,.132),(.008,height+.009,.015))
    for j in range(5):
        a=.6+j*.25;x,z=.130*math.sin(a),.130*math.cos(a)
        tube(g,'punched_hole','dark_leather',[(x,-.002,z),(x,.002,z)],.0018,5)
    if index==1:
        for j in range(4):
            tube(g,'troll_cross_stitch','thread',[(-.075+j*.008,-.025,.105),(-.069+j*.008,-.012,.108)],.0012,4)
    if index==2:
        for j in range(30):
            a=j*math.tau/30;x,z=.132*math.sin(a),.132*math.cos(a)
            for s in (-1,1):
                tube(g,'lox_fur_trim','lox_fur',[(x,s*height*.44,z),(x*1.02,s*(height*.54+.0015*(j%3)),z*1.02)],[.009,.0008],5)
        for x in (-.07,.05): diamond(g,'reinforcement_stud','black_metal',x,0,.127,.018)
    asset=finish(g,prefab,name,'belt');asset['attachment']={'display':'dropped and inventory only; existing accessory slot has no waist attachment','bandWidthMeters':height}
    return asset


def finish(g,prefab,name,kind):
    points=[p['vertices'][i:i+3] for p in g.parts for i in range(0,len(p['vertices']),3)]
    low=[min(p[i] for p in points) for i in range(3)];high=[max(p[i] for p in points) for i in range(3)]
    center=[(low[i]+high[i])/2 for i in range(3)];size=[high[i]-low[i] for i in range(3)]
    parts=g.merged()
    for p in parts: p.update(state='always',collidable=False)
    return dict(id=prefab.lower(),displayName=name,prefabs=[prefab],kind=kind,parts=parts,
                icon=dict(position=[center[0]+size[0]*1.8,center[1]+max(size)*1.25,center[2]+max(size)*2],target=center,orthographicSize=max(size)*.68),
                anchors=[dict(name='model_origin',position=[0,0,0])],collider=dict(center=center,size=size),boundsMin=low,boundsMax=high,
                dropPositionOffset=[-center[0],-low[1],-center[2]],sourceConcept='01-mantle-packs-tackleboxes.png' if kind in ('backpack','tacklebox') else '02-mantle-rings-belts.png',
                iconModelParity='Icon must be rendered from these exact parts with the same material assignments; no independent icon geometry')


def build():
    assets=[pack(i,spec) for i,spec in enumerate(PACKS)]+[tackle(i,spec) for i,spec in enumerate(TACKLE)]+[ring(i,spec) for i,spec in enumerate(RINGS)]+[belt(i,spec) for i,spec in enumerate(BELTS)]
    return dict(schema=1,product='Mantle',units='meters',materials=MATERIALS,assets=assets,
                author='expDARE',axes='Unity X right Y up Z forward. Backpack outward face -Z; other display faces +Z.',
                nativeStyle='Faceted matte hides and broad forged fittings, silhouette prioritized at 32 px; glow confined to guck, flametal seams and eitr core.',
                status='Original editable source geometry; Unity import, native material matching and in-game fit pending')


if __name__=='__main__':
    data=build()
    from validate_source import validate
    report=validate(data)
    out=ROOT/'models.json';out.write_text(json.dumps(data,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8')
    report['modelsSHA256']=hashlib.sha256(out.read_bytes()).hexdigest().upper()
    (ROOT/'validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2))
