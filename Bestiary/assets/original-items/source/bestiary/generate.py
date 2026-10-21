# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
"""Original bone arsenal and sea-loot geometry, in Unity meters with the hand grip at zero."""
import json
import math
from pathlib import Path
from geometry import box, lathe, sphere, transform, tube, validate

ROOT = Path(__file__).resolve().parent
COLORS = {"bone": [.72,.64,.48,1], "bone_edge": [.87,.80,.64,1], "leather": [.20,.12,.075,1],
          "wood": [.30,.19,.10,1], "iron": [.18,.19,.20,1], "iron_edge": [.35,.34,.30,1],
          "beak": [.13,.10,.12,1], "raw": [.36,.07,.14,1], "raw_cut": [.67,.40,.40,1],
          "cooked": [.30,.13,.055,1], "cooked_cut": [.68,.44,.28,1], "silver": [.55,.57,.57,1], "sea_wood": [.16,.24,.23,1]}


def polygon(name, material, outline, depth, center=(0,0,0)):
    area = sum(outline[i][0]*outline[(i+1)%len(outline)][1]-outline[(i+1)%len(outline)][0]*outline[i][1] for i in range(len(outline)))
    if area < 0: outline = outline[::-1]
    def turn(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    remaining=list(range(len(outline))); faces=[]
    while len(remaining)>3:
        for offset,b in enumerate(remaining):
            a,c=remaining[offset-1],remaining[(offset+1)%len(remaining)]
            if turn(outline[a],outline[b],outline[c])<=1e-10: continue
            if any(all(turn(outline[u],outline[v],outline[p])>=-1e-10 for u,v in ((a,b),(b,c),(c,a))) for p in remaining if p not in (a,b,c)): continue
            faces.append((a,b,c));remaining.pop(offset);break
        else: raise AssertionError("Non-simple outline: "+name)
    faces.append(tuple(remaining));n=len(outline)
    vertices=[(x+center[0],y+center[1],z+center[2]) for z in (-depth/2,depth/2) for x,y in outline]
    triangles=[]
    for a,b,c in faces: triangles.extend((c,b,a,n+a,n+b,n+c))
    for i in range(n):
        j=(i+1)%n;triangles.extend((i,j,n+j,i,n+j,n+i))
    return {"name":name,"material":material,"color":COLORS[material],"vertices":vertices,"triangles":triangles}


def round_part(name, material, center, size, segments=8):
    return sphere(name,COLORS[material],center,size,segments,4,material)


def shaft(parts, length, radius=.026, material="wood", start=-.12, grip=(-.055,.065)):
    parts.append(lathe("haft",COLORS[material],[(radius*.85,start),(radius,start+.025),(radius,length),(radius*.7,length+.025)],8,material))
    count=max(1,round((grip[1]-grip[0])/.025))
    for i in range(count):
        y=grip[0]+i*(grip[1]-grip[0])/count
        parts.append(lathe("grip_%02d"%i,COLORS["leather"],[(radius*1.08,y),(radius*1.08,y+.018)],8,"leather"))
    parts.append(round_part("pommel","bone",(0,start-.025,0),(radius*2.7,radius*2.6,radius*2.4)))


def bindings(parts, y, width=.055, height=.15):
    for i in range(4):
        parts.append(box("binding_%d_%d"%(int(y*100),i),COLORS["leather"],(0,y+i*.032,.005),(width,.018,.072),"leather",rotation=(0,0,(-1 if i%2 else 1)*12)))


def blade(parts,name,length,width,tip=.02,material="bone"):
    outline=[(-width*.4,0),(-width*.5,length*.65),(-width*.31,length*.86),(tip,length),(width*.38,length*.82),(width*.5,.12)]
    parts.append(polygon(name,material,outline,.035))
    parts.append(polygon(name+"_ridge","bone_edge" if material=="bone" else "iron_edge",[(-.009,.1),(0,length*.93),(.011,.1)],.044))


def bow(parts):
    path=[(-.22,-.64,0),(-.08,-.47,0),(.015,-.29,0),(0,-.10,0),(0,.10,0),(.015,.29,0),(-.08,.47,0),(-.22,.64,0)]
    parts.append(tube("bone_limbs",COLORS["bone"],path,.037,7,"bone"))
    parts.append(tube("bow_string",COLORS["leather"],[(-.22,-.64,0),(-.22,.64,0)],.004,5,"leather"))
    for y in [-.40,.38]:
        parts.append(box("limb_binding_"+str(y),COLORS["leather"],(-.045,y,0),(.095,.065,.09),"leather"))
    parts.append(box("handgrip",COLORS["leather"],(0,0,0),(.075,.19,.075),"leather"))


def axehead():
    return [(-.055,.035),(-.055,.30),(.10,.35),(.20,.46),(.32,.43),(.29,.23),(.23,.025),(.17,-.11),(.11,-.05),(.055,.055)]


def executioner_head_parts(center=(0,0,0)):
    parts=[polygon("executioner_axehead","iron",axehead(),.075)]
    parts.append(polygon("blade_worn_edge","iron_edge",[(.28,.42),(.26,.22),(.20,.02),(.17,-.10),(.19,-.08),(.25,.13),(.30,.42)],.078))
    parts.append(lathe("head_haft_socket",COLORS["iron"],[(.055,.04),(.055,.32),(.037,.32),(.037,.04),(.055,.04)],10,"iron",caps=False))
    for part in parts:
        part["uv"]=[value for x,y,z in part["vertices"] for value in (x*3,(y+z)*3)]
    return [transform(part,offset=center) for part in parts]


def make_weapon(kind):
    p=[]
    if kind=="Dagger":
        shaft(p,.04,.021,start=-.055,grip=(-.05,.025))
        cutting=[];blade(cutting,"knife_blade",.30,.075)
        p.extend(transform(part,offset=(0,.04,0)) for part in cutting)
        p.append(polygon("guard","bone",[(-.075,.025),(-.04,.065),(.045,.065),(.075,.025)],.065))
    elif kind=="Sword":
        shaft(p,.08,.025,start=-.085);blade(p,"sword_blade",.96,.09)
        p.append(round_part("vertebra_guard","bone",(0,.045,0),(.23,.10,.11)))
        for x in [-.11,.11]:p.append(polygon("guard_tip_"+str(x),"bone",[(x-.015,.02),(x,.15),(x+.025,.03)],.04))
    elif kind=="Axe":
        shaft(p,.66,.028)
        p.append(polygon("axe_blade","bone",[(-.035,.46),(.11,.59),(.22,.71),(.24,.52),(.20,.30),(.08,.36),(-.035,.37)],.05));bindings(p,.42,.085)
    elif kind=="Mace":
        shaft(p,.64,.028)
        for i in range(7):
            angle=i*math.tau/7;p.append(round_part("bone_knuckle_%d"%i,"bone",(math.cos(angle)*.095,.66+(.025 if i%2 else 0),math.sin(angle)*.095),(.12,.17,.11)))
        bindings(p,.53,.12)
    elif kind=="Spear":
        shaft(p,.73,.025,start=-1.30)
        p.append(polygon("spearhead","bone",[(-.035,.705),(-.065,.85),(0,1.08),(.065,.85),(.035,.705)],.04));bindings(p,.67,.075)
    elif kind=="Atgeir":
        shaft(p,1.40,.031,start=-.68,grip=(-.08,.12))
        p.append(polygon("hooked_blade","bone",[(-.03,1.27),(-.03,1.65),(.03,1.88),(.19,2.03),(.36,2.06),(.43,1.92),(.39,1.77),(.31,1.71),(.33,1.88),(.22,1.92),(.13,1.84),(.08,1.62),(.08,1.36)],.05))
        p.append(polygon("side_prong","bone",[(-.02,1.52),(-.19,1.67),(-.14,1.48),(-.02,1.44)],.045));bindings(p,1.27,.09)
    elif kind=="Bow":bow(p)
    elif kind=="Crossbow":
        p.append(box("stock",COLORS["wood"],(0,.215,0),(.075,.73,.075),"wood"))
        p.append(box("grip",COLORS["wood"],(0,-.025,.045),(.07,.13,.09),"wood",rotation=(20,0,0)))
        p.append(tube("bone_crosslimbs",COLORS["bone"],[(-.352,.398,-.024),(-.25,.437,-.017),(0,.472,-.011),(.25,.437,-.017),(.352,.398,-.024)],.025,7,"bone"))
        p.append(box("rail",COLORS["iron"],(0,.315,-.035),(.025,.53,.018),"iron"))
        p.append(box("trigger_nut",COLORS["bone"],(0,.100,-.036),(.05,.035,.022),"bone"))
        bindings(p,.45,.085)
    elif kind=="Executioner":
        shaft(p,1.105,.033,start=-.36)
        for i in range(7):
            y=.25+i*.029
            p.append(lathe("upper_hand_wrap_"+str(i),COLORS["leather"],[(.036,y),(.036,y+.018)],8,"leather"))
        p.extend(executioner_head_parts((0,.78,0)));bindings(p,.79,.10)
    return p


def ammunition(blunt=False):
    length=.57 if blunt else 1.16
    shaft_end=length-.05 if blunt else length-.09
    p=[lathe("shaft",COLORS["wood"],[(.006,0),(.006,shaft_end)],6,"wood")]
    if blunt:
        p.append(round_part("blunt_head","bone",(0,length-.0325,0),(.055,.065,.047),6))
    else:
        p.append(polygon("sharp_head","bone",[(-.020,length-.13),(0,length),(.020,length-.13)],.015))
    fletch_length=.12 if blunt else .19
    for sign in [-1,1]:
        p.append(polygon("fletch_%d"%sign,"bone_edge",[(0,.025),(sign*.026,.055),
                (sign*.026,fletch_length),(0,fletch_length-.035)],.003))
    return [transform(part,rotation=(90,0,0)) for part in p]


def beak_parts(center=(0,0,0),scale=1):
    # One broad keratin crown curls into one ivory point; sections vary in both width and depth.
    path=[(-.11,.025,.0),(-.115,.115,.0),(-.070,.205,.008),(.020,.235,.012),
          (.105,.205,.015),(.165,.125,.017),(.184,.025,.018),(.175,-.065,.021),(.145,-.145,.023)]
    widths=[.067,.083,.087,.083,.072,.053,.032,.014,.0004]
    depths=[.041,.058,.065,.065,.056,.043,.029,.014,.0004]
    sections=[];frames=[];segments=12
    for i,point in enumerate(path):
        before,after=path[max(0,i-1)],path[min(len(path)-1,i+1)]
        dx,dy=after[0]-before[0],after[1]-before[1]
        length=math.hypot(dx,dy);side=(-dy/length,dx/length)
        frames.append(side)
        sections.append([(point[0]+side[0]*widths[i]*math.cos(j*math.tau/segments),
                          point[1]+side[1]*widths[i]*math.cos(j*math.tau/segments),
                          point[2]+depths[i]*math.sin(j*math.tau/segments)) for j in range(segments)])
    def sweep(name,material,start,end):
        vertices=[point for section in sections[start:end+1] for point in section]
        triangles=[]
        for i in range(end-start):
            for j in range(segments):
                n=(j+1)%segments;a=i*segments+j;b=i*segments+n;c=(i+1)*segments+n;d=(i+1)*segments+j
                triangles.extend((a,b,c,a,c,d))
        for offset,reverse in ((0,True),((end-start)*segments,False)):
            for j in range(1,segments-1):
                face=(offset,offset+j,offset+j+1)
                triangles.extend(reversed(face) if reverse else face)
        return dict(name=name,material=material,color=COLORS[material],vertices=vertices,triangles=triangles)
    crown=sweep('hooked_beak','beak',0,5)
    for i in (1,2,3,4):
        point,side=path[i],frames[i]
        ridge=[]
        for j in range(9):
            angle=.12+j*(math.pi-.24)/8
            ridge.append((point[0]+side[0]*(widths[i]+.0015)*math.cos(angle),
                          point[1]+side[1]*(widths[i]+.0015)*math.cos(angle),
                          point[2]+(depths[i]+.0015)*math.sin(angle)))
        raised=tube('keratin_growth_ridge',COLORS['beak'],ridge,.0024,5,'beak')
        offset=len(crown['vertices']);crown['vertices'].extend(raised['vertices'])
        crown['triangles'].extend(index+offset for index in raised['triangles'])
    p=[crown,sweep('beak_horn_tip','bone_edge',5,8)]
    for part in p:
        part["uv"]=[value for x,y,z in part["vertices"] for value in (x*3,(y+z)*3)]
    return [transform(part,offset=center,scale=(scale,scale,scale)) for part in p]


def meat(cooked=False):
    body="cooked" if cooked else "raw";cut="cooked_cut" if cooked else "raw_cut"
    p=[round_part("mantle_meat",body,(0,0,0),(.30,.16,.20))]
    p.append(round_part("cut_face",cut,(.11,0,.015),(.085,.13,.165),8))
    for i in range(4):
        material="iron" if cooked else "raw_cut"
        radius=.004 if cooked else .002
        path=[]
        for dx,z in [(0,-.045),(.005,.015),(.009,.055)]:
            x=-.08+i*.05+dx
            path.append((x,top_surface_at(p[0],x,z)-radius*.25,z))
        p.append(tube("sear_or_fiber_%d"%i,COLORS[material],path,radius,4,material))
    return p


def top_surface_at(part,x,z):
    intersections=[]
    for i in range(0,len(part["triangles"]),3):
        a,b,c=[part["vertices"][j] for j in part["triangles"][i:i+3]]
        denominator=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
        if abs(denominator)<1e-12:continue
        u=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/denominator
        v=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/denominator
        w=1-u-v
        if min(u,v,w)>=-1e-8:intersections.append(u*a[1]+v*b[1]+w*c[1])
    assert intersections,"Surface detail outside meat geometry"
    return max(intersections)


def shield():
    p=[]
    p.append(lathe("shield_board",COLORS["sea_wood"],[(.32,-.025),(.33,0),(.32,.025)],16,"sea_wood",rotation=(90,0,0)))
    p.append(lathe("silver_rim",COLORS["silver"],[(.32,-.028),(.345,-.028),(.345,.028),(.32,.028),(.32,-.028)],16,"silver",rotation=(90,0,0),caps=False))
    for x in [-.20,-.10,0,.10,.20]:p.append(box("plank_seam_"+str(x),COLORS["wood"],(x,0,.027),(.005,math.sqrt(.32**2-x**2)*2,.004),"wood"))
    p+=beak_parts(center=(0,-.025,.10),scale=.55)
    for i in range(12):
        a=i*math.tau/12;p.append(round_part("rim_rivet_%d"%i,"silver",(math.cos(a)*.325,math.sin(a)*.325,.037),(.021,.021,.015),5))
    p.append(box("rear_handle",COLORS["leather"],(0,0,-.09),(.045,.24,.035),"leather"))
    return p


def weapon_frame(parts, kind):
    if kind != "Atgeir":
        return [transform(part,rotation=(-90 if kind=="Spear" else 90,0,0)) for part in parts]
    up=[.339,-.143,.930]
    mag=math.sqrt(sum(v*v for v in up));up=[v/mag for v in up]
    side=[.925,.198,-.325]
    along=sum(a*b for a,b in zip(side,up));side=[side[i]-up[i]*along for i in range(3)]
    mag=math.sqrt(sum(v*v for v in side));side=[v/mag for v in side]
    normal=[side[1]*up[2]-side[2]*up[1],side[2]*up[0]-side[0]*up[2],side[0]*up[1]-side[1]*up[0]]
    result=[]
    for part in parts:
        posed=dict(part)
        posed["vertices"]=[[round(side[i]*x+up[i]*y+normal[i]*z,6) for i in range(3)] for x,y,z in part["vertices"]]
        result.append(posed)
    return result


CROSSBOW_POINTS={"ecp_xbow_tip_a":[-.352,.024,.398],"ecp_xbow_tip_b":[.352,.024,.398],
                 "ecp_xbow_rest":[0,.024,.398],"ecp_xbow_nut":[0,.036,.100],
                 "ecp_xbow_muzzle":[0,.032,.560],"ecp_xbow_support":[0,-.010,.220]}


def crossbow_cords():
    parts=[]
    for side in ("a","b"):
        part=lathe("ecp_xbow_string_"+side,COLORS["leather"],[(.003,0),(.003,1)],5,"leather",rotation=(90,0,0))
        start=CROSSBOW_POINTS["ecp_xbow_tip_"+side];end=CROSSBOW_POINTS["ecp_xbow_nut"]
        along=[end[i]-start[i] for i in range(3)];length=math.sqrt(sum(v*v for v in along))
        part.update({"dynamic":True,"position":start,
                     "rotation":[-math.degrees(math.asin(along[1]/length)),math.degrees(math.atan2(along[0],along[2])),0],
                     "scale":[1,1,length]})
        parts.append(part)
    return parts


def asset(identifier,name,prefab,parts,kind,anchors=None,contract=None):
    validate(parts)
    vertices=[v for part in parts if not part.get("dynamic") for v in part["vertices"]]
    low=[min(p[i] for p in vertices) for i in range(3)]; high=[max(p[i] for p in vertices) for i in range(3)]
    center=[(low[i]+high[i])/2 for i in range(3)];extent=max(high[i]-low[i] for i in range(3))
    exported=[]
    for part in parts:
        entry={"name":part["name"].replace("-","n").replace(".","_"),"material":part["material"],"vertices":[round(c,6) for v in part["vertices"] for c in v],"triangles":part["triangles"],"collidable":False,"state":"always"}
        for field in ("uv","dynamic","position","rotation","scale"):
            if field in part:entry[field]=part[field]
        exported.append(entry)
    axis=[.8,2,-.65] if kind in ("weapon","ammo") else [.8,.35,2]
    if prefab=="ECP_BoneSpear":axis=[-.8,2,.65]
    if prefab=="ECP_BoneAtgeir":axis=[.25,2,-.65]
    result={"id":identifier,"displayName":name,"prefabs":[prefab],"kind":kind,"parts":exported,
            "icon":{"position":[center[i]+extent*axis[i] for i in range(3)],"target":center,"orthographicSize":extent*.64},
            "collider":{"center":center,"size":[high[i]-low[i] for i in range(3)]},"anchors":anchors or [],"boundsMin":low,"boundsMax":high}
    if contract:result["attachment"]=contract
    return result


def build():
    assets=[]
    for kind in ["Dagger","Sword","Axe","Mace","Spear","Atgeir","Bow","Crossbow","Executioner"]:
        prefab="ECP_ExecutionerGreataxe" if kind=="Executioner" else "ECP_Bone"+kind
        name="Executioner Greataxe" if kind=="Executioner" else "Bone "+kind
        parts=weapon_frame(make_weapon(kind),kind)
        anchors=[{"name":"hand_grip","position":[0,0,0]}]
        contract={"frame":"native attach identity; hand grip at origin"}
        if kind=="Crossbow":
            parts+=crossbow_cords()
            anchors += [{"name":name,"position":point} for name,point in CROSSBOW_POINTS.items()]
            contract.update({"existingRig":"ecp_xbow_rig","existingModelScale":1.25,"replaceOnlySolidBody":True,
                             "cordNodes":["ecp_xbow_string_a","ecp_xbow_string_b"],"cordAxis":"local +Z, length1m, nock at origin",
                             "grooveBoltNode":"ecp_xbow_groove_bolt","grooveBoltAsset":"bestiary_bolt","defaultIconPose":"loaded",
                             "runtime":"Preserve existing rig anchors and animated halfcord nodes; copy canonical cord mesh/material, hide new default-pose cord nodes."})
        if kind=="Bow":anchors += [{"name":"bow_tip_a","position":[-.22,0,-.64]},{"name":"bow_tip_b","position":[-.22,0,.64]}]
        assets.append(asset("bestiary_"+kind.lower(),name,prefab,parts,"weapon",anchors,contract))
    spine=[]
    centers=[]
    for i in range(5):
        a=i*.22
        center=(math.sin(a)*.12,-.18+i*.085,math.cos(a)*.05);centers.append(center)
        spine.append(round_part("vertebra_%d"%i,"bone",center,(.12,.068,.11)))
        spine.append(polygon("spine_ridge_%d"%i,"bone_edge",[(-.02,-.01),(0,.08),(.02,-.01)],.12,center=center))
        for sign in (-1,1):
            spine.append(round_part("vertebra_wing_%d_%d"%(i,sign),"bone_edge",(center[0]+sign*.068,center[1],center[2]),(.065,.035,.035),6))
    spine.append(tube("spinal_cord",COLORS["leather"],centers,.020,6,"leather"))
    assets.append(asset("bestiary_spine","Spine","ECP_Spine",spine,"material"))
    assets.append(asset("bestiary_arrow","Bone Arrows","ECP_ArrowBone",ammunition(),"ammo"))
    assets.append(asset("bestiary_bolt","Blunt Bone Bolts","ECP_BoltBoneBlunt",ammunition(True),"ammo"))
    loose_head=asset("bestiary_axehead","Executioner Axehead","ECP_ExecutionerAxehead",weapon_frame(executioner_head_parts(),"Executioner"),"material")
    middle=loose_head["icon"]["target"];extent=max(loose_head["collider"]["size"])
    loose_head["icon"]["position"]=[middle[i]+extent*[.8,2,-.65][i] for i in range(3)]
    assets.append(loose_head)
    assets.append(asset("bestiary_beak","Kraken Beak","ECP_KrakenBeak",beak_parts(),"material"))
    assets.append(asset("bestiary_meat_raw","Raw Kraken Meat","ECP_KrakenMeat",meat(),"material"))
    assets.append(asset("bestiary_meat_cooked","Cooked Kraken Meat","ECP_KrakenMeatCooked",meat(True),"food"))
    assets.append(asset("bestiary_shield","Kraken Shield","ECP_ShieldKraken",shield(),"shield"))
    assert len(assets)==17
    assert len({a["prefabs"][0] for a in assets})==17
    materials=[{"id":key,"color":value,"roughness":.76 if "iron" in key or key=="silver" else .94,"metallic":.65 if key in ("iron","iron_edge","silver") else 0,"emission":[0,0,0],"nativePrefab":"SerpentScale"} for key,value in COLORS.items()]
    return {"schema":1,"product":"Bestiary","units":"meters","materials":materials,"assets":assets}


if __name__=="__main__":
    data=build();path=ROOT/"models.json";path.write_text(json.dumps(data,separators=(',',':'),allow_nan=False)+"\n",encoding="utf-8")
    print(str(path)+": "+str(len(data["assets"]))+" assets, "+str(sum(len(p["triangles"])//3 for a in data["assets"] for p in a["parts"]))+" triangles")
