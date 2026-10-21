# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
import hashlib
import json
import math
import re
from pathlib import Path
from generate import COLORS, CROSSBOW_POINTS, polygon
from geometry import extrude, transform, validate


ROOT=Path(__file__).resolve().parent


def volume(part):
    vertices=part["vertices"]
    if vertices and isinstance(vertices[0],(int,float)):
        vertices=[vertices[i:i+3] for i in range(0,len(vertices),3)]
    result=0
    for i in range(0,len(part["triangles"]),3):
        a,b,c=[vertices[j] for j in part["triangles"][i:i+3]]
        result+=sum(a[k]*(b[(k+1)%3]*c[(k+2)%3]-b[(k+2)%3]*c[(k+1)%3]) for k in range(3))/6
    return result


def vertices(part):
    flat=part["vertices"]
    return [flat[i:i+3] for i in range(0,len(flat),3)]


def close(a,b):
    return len(a)==len(b) and all(abs(x-y)<1.5e-6 for x,y in zip(a,b))


def bounds(points):
    return ([min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)])


def main():
    source=json.loads((ROOT/"models.json").read_text())
    catalog=(ROOT.parents[3]/"Bestiary/src/Bestiary.Core/Catalog/ItemCatalog.cs").read_text()
    expected={"ECP_Bone"+key for key in re.findall(r'\("(Dagger|Sword|Axe|Mace|Spear|Atgeir|Bow)",',catalog)}
    expected.update(re.findall(r'new ItemDefinition\("(ECP_[A-Za-z]+)"',catalog))
    expected.discard("ECP_Bone")
    assert len(expected)==17
    assets={asset["id"]:asset for asset in source["assets"]}
    assert len(assets)==17 and {prefab for a in assets.values() for prefab in a["prefabs"]}==expected
    assert source["schema"]==1 and source["units"]=="meters" and source["product"]=="Bestiary"
    materials={mat["id"]:mat for mat in source["materials"]}
    assert len(materials)==len(source["materials"])
    assert all(mat["nativePrefab"]=="SerpentScale" and mat["emission"]==[0,0,0] for mat in materials.values())
    triangles=0
    for asset in assets.values():
        assert len({part["name"] for part in asset["parts"]})==len(asset["parts"])
        points=[]
        for part in asset["parts"]:
            verts=vertices(part)
            assert len(part["vertices"])%3==0 and len(part["triangles"])%3==0
            assert all(math.isfinite(v) for v in part["vertices"])
            if "uv" in part:
                assert len(part["uv"])==len(verts)*2 and all(math.isfinite(v) for v in part["uv"])
            assert part["material"] in materials and part["state"]=="always" and not part["collidable"]
            validate([dict(part,vertices=verts)])
            assert volume(part)>1e-12,(asset["id"],part["name"],"outward signed volume")
            triangles+=len(part["triangles"])//3
            if not part.get("dynamic"):points.extend(verts)
        low,high=bounds(points)
        assert close(asset["boundsMin"],low) and close(asset["boundsMax"],high)
        assert close(asset["collider"]["center"],[(a+b)/2 for a,b in zip(low,high)])
        assert close(asset["collider"]["size"],[b-a for a,b in zip(low,high)])
        assert all(v>0 for v in asset["collider"]["size"])
        assert asset["icon"]["orthographicSize"]>max(b-a for a,b in zip(low,high))*.5
    head_names={"executioner_axehead","blade_worn_edge","head_haft_socket"}
    equipped={p["name"]:p for p in assets["bestiary_executioner"]["parts"] if p["name"] in head_names}
    loose={p["name"]:p for p in assets["bestiary_axehead"]["parts"]}
    assert equipped.keys()==loose.keys()==head_names
    for name in head_names:
        a,b=equipped[name],loose[name]
        assert a["material"]==b["material"] and a["triangles"]==b["triangles"]
        assert a["uv"]==b["uv"],(name,"assembled/loose axehead wear pattern")
        normalized=[v-(.78 if i%3==2 else 0) for i,v in enumerate(a["vertices"])]
        assert close(normalized,b["vertices"]),(name,"equipped/loose exact same head")
    beak={p["name"]:p for p in assets["bestiary_beak"]["parts"]}
    shield={p["name"]:p for p in assets["bestiary_shield"]["parts"]}
    for name,part in beak.items():
        worn=shield[name]
        assert part["triangles"]==worn["triangles"] and part["material"]==worn["material"]
        assert part["uv"]==worn["uv"],(name,"loot/shield beak keratin pattern")
        normalized=[(v-[0,-.025,.10][i%3])/.55 for i,v in enumerate(worn["vertices"])]
        assert close(normalized,part["vertices"]),(name,"shield boss must be the same beak")
    crown=vertices(beak['hooked_beak']);tip=vertices(beak['beak_horn_tip'])
    assert len({round(p[2],3) for p in crown})>12,'Beak crown must have volumetric section variation, not a planar extrusion'
    tip_end=tip[-12:];tip_start=tip[:12]
    assert max(math.dist(a,b) for a in tip_end for b in tip_end)<.001,'Ivory beak must end in one narrow point'
    assert max(math.dist(a,b) for a in tip_start for b in tip_start)>.09,'Ivory tip must taper from a thick crown join'
    assert sum(p[1] for p in tip_end)/12<min(p[1] for p in crown)-.10,'Single hook point must curl below the blunt crown root'
    low,high=bounds(crown+tip)
    assert .3<high[0]-low[0]<.5 and .4<high[1]-low[1]<.55 and .10<high[2]-low[2]<.17,'Beak must retain native-sized bounds'
    for ident,length,head in [("bestiary_arrow",1.16,"sharp_head"),("bestiary_bolt",.57,"blunt_head")]:
        a=assets[ident]
        assert abs(a["boundsMin"][2])<1e-6 and abs(a["boundsMax"][2]-length)<1e-6
        assert sum(p["name"]=="shaft" for p in a["parts"])==1
        assert sum(p["name"]==head for p in a["parts"])==1
        assert a["collider"]["size"][2]>a["collider"]["size"][0]*7
    for ident in ("dagger","sword","axe","mace","executioner"):
        a=assets["bestiary_"+ident]
        assert a["collider"]["size"][2]>a["collider"]["size"][1]*3,(ident,"native +Z held axis")
    spear=assets["bestiary_spear"]
    assert close(spear["boundsMin"][2:3],[-1.08]) and spear["boundsMax"][2]>1.3
    bow=assets["bestiary_bow"]
    grip=next(p for p in bow["parts"] if p["name"]=="handgrip")
    low,high=bounds(vertices(grip));assert close([(a+b)/2 for a,b in zip(low,high)],[0,0,0])
    crossbow=assets["bestiary_crossbow"]
    anchors={a["name"]:a["position"] for a in crossbow["anchors"]}
    assert all(close(anchors[name],point) for name,point in CROSSBOW_POINTS.items())
    cords=[p for p in crossbow["parts"] if p.get("dynamic")]
    assert {p["name"] for p in cords}=={"ecp_xbow_string_a","ecp_xbow_string_b"}
    assert not any(p["name"]=="crossbow_string" for p in crossbow["parts"])
    for cord in cords:
        low,high=bounds(vertices(cord));assert abs(low[2])<1e-6 and abs(high[2]-1)<1e-6
        posed=transform(dict(cord,vertices=[(0,0,0),(0,0,1)]),cord["position"],cord["rotation"],cord["scale"])
        assert close(posed["vertices"][0],CROSSBOW_POINTS["ecp_xbow_tip_"+cord["name"][-1]])
        assert close(posed["vertices"][1],CROSSBOW_POINTS["ecp_xbow_nut"])
    notch=[(0,0),(3,0),(3,3),(2,3),(2,1),(1,1),(1,3),(0,3)]
    for outline in (notch,list(reversed(notch))):
        for part in (polygon("notch","bone",outline,.2),extrude("notch",COLORS["bone"],outline,.2)):
            assert abs(volume(part)-1.4)<1e-8,"Concave cap or winding regression"
    for bad in ([[0,0,0],[0,0,0],[0,0,0]],[[float("nan"),0,0],[1,0,0],[0,1,0]]):
        try:validate([{"name":"invalid","vertices":bad,"triangles":[0,1,2]}])
        except AssertionError:pass
        else:raise AssertionError("Invalid source accepted")
    receipt={"result":"PASS","models":17,"triangles":triangles,
             "modelsSha256":hashlib.sha256((ROOT/"models.json").read_bytes()).hexdigest().upper(),
             "checks":["actual ItemCatalog coverage","finite nondegenerate outward meshes","exact assembled/loose axehead including socket and edge",
                       "concave caps both outline windings","one arrow/bolt with native nock/forward axis/length","native held axes and bow origin",
                       "matching beak loot and shield boss","volumetric beak crown, single tapered ivory hook and native envelope","paired head and beak local UV wear patterns","crossbow native anchors and animated unitcord contract","matching source-render camera and collider bounds","native opaque nonemissive material intentions","invalid geometry rejected"],
             "limits":["Source checks and software geometry preview only; Unity build/materials, equip poses, animation and native play remain root verification."]}
    (ROOT/"SOURCE-CHECKS.json").write_text(json.dumps(receipt,indent=2)+"\n")
    print(json.dumps(receipt))


if __name__=="__main__":main()
