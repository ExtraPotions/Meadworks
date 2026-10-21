# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
import hashlib
import json
import re
from pathlib import Path


ROOT=Path(__file__).resolve().parent


def expanded_uv(part):
    count=len(part["vertices"])//3
    assert len(part["uv"])==count*2
    return [part["uv"][i*2:i*2+2] for i in part["triangles"]]


def main():
    builder_path=ROOT.parent/"BuildCustomItems.cs"
    builder=builder_path.read_text()
    compact=re.sub(r"\s+","",builder)
    assert "Require(part.uv==null||part.uv.Length==inputVertices.Length*2," in compact,"Supplied UVs must be validated against indexed input vertex count"
    assert "if(part.uv!=null)mesh.uv=part.triangles.Select" in compact
    assert "mesh.vertices=part.triangles.Select(i=>inputVertices[i]).ToArray();" in compact
    assert "mesh.uv=part.triangles.Select(i=>newVector2(part.uv[i*2],part.uv[i*2+1])).ToArray();" in compact,"UV expansion must follow the same triangle indices as vertex expansion"
    fixture={"vertices":[0,0,0,1,0,0,0,1,0,1,1,0],"triangles":[2,0,1,2,1,3],
             "uv":[.10,.20,.30,.40,.50,.60,.70,.80]}
    assert expanded_uv(fixture)==[[.50,.60],[.10,.20],[.30,.40],[.50,.60],[.30,.40],[.70,.80]]
    assert len(fixture["uv"])!=len(fixture["triangles"])*2,"Fixture must reject the old expanded-count UV guard"
    try:expanded_uv(dict(fixture,uv=fixture["uv"][:-1]))
    except AssertionError:pass
    else:raise AssertionError("Malformed supplied UVs accepted")
    source_path=ROOT/"models.json"
    source=json.loads(source_path.read_text())
    assets={asset["id"]:{part["name"]:part for part in asset["parts"]} for asset in source["assets"]}
    explicit=[(asset["id"],part) for asset in source["assets"] for part in asset["parts"] if "uv" in part]
    assert len(explicit)==10
    for identifier,part in explicit:
        uv=expanded_uv(part)
        assert len(uv)==len(part["triangles"])
        assert len(part["uv"])!=len(part["triangles"])*2,(identifier,part["name"],"Must exercise historical rejection")
    pairs=[]
    for name in ("executioner_axehead","blade_worn_edge","head_haft_socket"):
        a,b=assets["bestiary_executioner"][name],assets["bestiary_axehead"][name]
        assert expanded_uv(a)==expanded_uv(b),(name,"Expanded head UV mismatch")
        pairs.append({"part":name,"expandedUvCount":len(a["triangles"])})
    for name in ("hooked_beak","beak_horn_tip"):
        a,b=assets["bestiary_beak"][name],assets["bestiary_shield"][name]
        assert expanded_uv(a)==expanded_uv(b),(name,"Expanded beak UV mismatch")
        pairs.append({"part":name,"expandedUvCount":len(a["triangles"])})
    workspace=ROOT.parents[3]
    runtime_path=workspace/"Bestiary/src/Bestiary.Plugin/Art/OriginalItemVisuals.cs"
    runtime=runtime_path.read_text()
    drop=runtime[runtime.index("static void Replace("):runtime.index("static void Crossbow(")]
    projectile=runtime[runtime.index("static void ReplaceProjectile("):]
    drop_compact=re.sub(r"\s+","",drop)
    assert 'if(id=="ECP_ArrowBone"||id=="ECP_BoltBoneBlunt")visual.transform.localPosition=-ModelBounds.In(visual,holder).center;' in drop_compact
    assert drop.count("visual.transform.localPosition")==1,"Centering must stay restricted to the two ammo items"
    assert "ModelBounds.In" not in projectile,"Projectile nock/forward frame must remain unchanged"
    assert "!Native.Adapters.NativeRuntime.Owns(prefab)" in projectile
    offsets=re.findall(r'ReplaceProjectile\(scene, "([^"]+)", "([^"]+)", (-?[.0-9]+)f\)',runtime)
    assert len(offsets)==3
    by_prefab={prefab:asset for asset in source["assets"] for prefab in asset["prefabs"]}
    drop_centers=[]
    for prefab in ("ECP_ArrowBone","ECP_BoltBoneBlunt"):
        asset=by_prefab[prefab]
        low,high=asset["boundsMin"],asset["boundsMax"]
        offset=[-(a+b)/2 for a,b in zip(low,high)]
        centered=[(a+b)/2+move for a,b,move in zip(low,high,offset)]
        assert all(abs(v)<1e-8 for v in centered)
        drop_centers.append({"prefab":prefab,"localOffset":offset,"dropCenter":centered})
    arrow=(workspace/"Bestiary/src/Bestiary.Plugin/Native/Arsenal/ArsenalArrow.cs").read_text()
    bolt=(workspace/"Bestiary/src/Bestiary.Plugin/Native/Crossbow/XbowBolt.cs").read_text()
    native_points={"ECP_ArrowBone":float(re.search(r"const float Point = ([.0-9]+)f",arrow)[1]),
                   "ECP_BoltBoneBlunt":float(re.search(r"const float Head = ([.0-9]+)f",bolt)[1])}
    projectile_points=[]
    for prefab,item,offset in offsets:
        tip=by_prefab[item]["boundsMax"][2]+float(offset)
        assert abs(tip-native_points[item])<1e-8,(prefab,"Native projectile tip moved")
        projectile_points.append({"prefab":prefab,"tipZ":round(tip,6),"nativeTipZ":native_points[item]})
    receipt={"result":"PASS","explicitIndexedUvParts":len(explicit),"pairedExpandedUvChecks":pairs,
             "historicalExpandedCountGuardWouldRejectParts":len(explicit),
             "dropCenters":drop_centers,"projectileTips":projectile_points,
             "modelsSha256":hashlib.sha256(source_path.read_bytes()).hexdigest().upper(),
             "builderSha256":hashlib.sha256(builder_path.read_bytes()).hexdigest().upper(),
             "runtimeSha256":hashlib.sha256(runtime_path.read_bytes()).hexdigest().upper(),
             "checks":["Actual builder accepts UVs by indexed input count","Actual builder expands vertices and UVs through identical triangle indices",
                       "Nontrivial indexed quad has exact expected reordered UVs","All ten actual indexed UV arrays survive source expansion",
                       "Paired heads and beaks retain identical expanded wear coordinates","Malformed supplied UVs rejected",
                       "Only dropped arrow and bolt models centered on item origin","Three projectile tips retain native positions","Foreign projectile ownership protected"],
             "limits":["Source contract regression only; does not execute Unity Mesh/CombineMeshes or inspect bundle output."]}
    (ROOT/"EXPORT-CONTRACT-CHECKS.json").write_text(json.dumps(receipt,indent=2)+"\n")
    print(json.dumps(receipt))


if __name__=="__main__":main()
