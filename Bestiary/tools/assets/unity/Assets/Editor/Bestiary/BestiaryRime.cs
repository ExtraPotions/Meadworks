// Bestiary numerical fit adapter. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Placement and flattening follow AssetWorkshop/RimeGiant/RimeKit at a8ea29008f23dcebfd3afe93c157c6bf6e8d7be1.
// Portions Copyright (C) 2026 Gerald Glasgow. Changed by expDARE, 2026: consume runtime-measured local TRS;
// never instantiate a game Troll in the editor or depend on exported game assets.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bestiary.Build
{
    [Serializable] public sealed class RimeMount {
        public string name,mount,pose;
        public Vector3 localPosition,localScale;
        public Quaternion localRotation;
    }
    [Serializable] public sealed class RimeMeasuredFit { public RimeMount[] bestiary_mounts; }

    public static class BestiaryRime
    {
        public static string[] Build(string bundle)
        {
            string folder=BestiaryBundles.BundlesFolder+"/"+bundle;
            foreach(string part in BestiaryBundles.PartFolders(bundle))Workshop.PrefabBuilder.Build(part);
            var fit=JsonUtility.FromJson<RimeMeasuredFit>(File.ReadAllText(folder+"/_fit.json"));
            if(fit.bestiary_mounts==null||fit.bestiary_mounts.Length!=15||fit.bestiary_mounts.Select(m=>m.name).Distinct().Count()!=15)
                throw new InvalidOperationException("Rime Giant requires 8 plate and 7 sleeping crust numeric mounts.");
            var kit=new GameObject("ecr_rime_kit");
            try{
                foreach(var measured in fit.bestiary_mounts){
                    if(string.IsNullOrWhiteSpace(measured.mount)||!measured.name.StartsWith("ecr_rime_",StringComparison.Ordinal))
                        throw new InvalidOperationException("Unexpected Rime mount name.");
                    var mount=kit.transform.Find(measured.mount);
                    if(mount==null){mount=new GameObject(measured.mount).transform;mount.SetParent(kit.transform,false);}
                    string source=folder+"/"+measured.name+"/"+measured.name+".prefab";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
                    if(prefab==null)throw new InvalidOperationException("Missing rebuilt part "+source);
                    var part=UnityEngine.Object.Instantiate(prefab,mount,false);
                    part.name=measured.name;
                    Workshop.RimeGiant.RimeKit.Flatten(part);
                    part.transform.localPosition=measured.localPosition;
                    part.transform.localRotation=measured.localRotation;
                    part.transform.localScale=measured.localScale;
                }
                if(kit.GetComponentsInChildren<Component>(true).Any(c=>c is Collider||c is Rigidbody||c is Animator))
                    throw new InvalidOperationException("The Rime kit must contain only authored renderers and numeric transforms.");
                string output=folder+"/_built";
                Directory.CreateDirectory(output);
                string kitPath=output+"/ecr_rime_kit.prefab";
                PrefabUtility.SaveAsPrefabAsset(kit,kitPath);
                string boulderPath=folder+"/ecr_rime_boulder/ecr_rime_boulder.prefab";
                var boulder=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(boulderPath));
                boulder.name="ecr_rime_boulder";
                try{Workshop.RimeGiant.RimeKit.Flatten(boulder);PrefabUtility.SaveAsPrefabAsset(boulder,boulderPath);}
                finally{UnityEngine.Object.DestroyImmediate(boulder);}
                BestiaryBundles.Line("rime numerical fit: 8 plate mounts, 7 sleeping crust mounts, no exported Troll");
                return new[]{kitPath,boulderPath};
            }finally{UnityEngine.Object.DestroyImmediate(kit);}
        }
    }
}
