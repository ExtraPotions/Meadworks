// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Wildblood.Runtime {
    internal static class NativeRaidChestPrefab {
        internal const string PrefabName="mw_raid_chest";
        static GameObject prefab;
        static bool initialized;
        internal static bool Registered=>prefab;
        internal static void Initialize(){if(initialized)return;initialized=true;PrefabManager.OnVanillaPrefabsAvailable+=Register;}
        internal static void Shutdown(){if(!initialized)return;PrefabManager.OnVanillaPrefabsAvailable-=Register;initialized=false;}
        internal static void Tick(){
            if(prefab)prefab.GetComponent<Piece>().m_enabled=Plugin.Running&&WildbloodModules.ProductActive(out _)
                &&Plugin.Settings?.Get(ExtraPotions.Wildblood.Config.WildbloodSettings.General.Enabled)==true&&!NativeRaidAdmission.SourcePresent;
        }
        static void Register(){
            if(prefab)return;
            AssetBundle bundle=null;GameObject clone=null;Material material=null;
            try {
                bool linux=Application.platform==RuntimePlatform.LinuxPlayer||Application.platform==RuntimePlatform.LinuxServer;
                if(!linux&&Application.platform!=RuntimePlatform.WindowsPlayer&&Application.platform!=RuntimePlatform.WindowsServer)throw new PlatformNotSupportedException("Original raid chest requires the retained Windows or Linux bundle.");
                string resource="Wildblood.raids."+(linux?"linux":"windows")+".bundle";
                byte[] bytes;using(var stream=typeof(Plugin).Assembly.GetManifestResourceStream(resource)??throw new InvalidDataException("Missing original chest resource: "+resource))
                using(var buffer=new MemoryStream()){stream.CopyTo(buffer);bytes=buffer.ToArray();}
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
                if(hash!=(linux?"ADFBF0A6125237B0A4CA576AD74A4F26D5E3E48764F48730090EB5BDE0E96475":"B5B33712B3E4C7B04CAE798B9A898A13E9035C8AB7180F6F85B1587464A76F8A"))throw new InvalidDataException("Original raid chest bundle identity mismatch.");
                bundle=AssetBundle.LoadFromMemory(bytes);if(!bundle)throw new InvalidDataException("Original chest bundle did not load.");
                var names=bundle.GetAllAssetNames();var model=bundle.LoadAsset<GameObject>(names.Single(n=>n.EndsWith("/mw_raid_chest_model.prefab")));
                var icon=bundle.LoadAssetWithSubAssets<Sprite>(names.Single(n=>n.EndsWith("/mw_raid_chest_icon.png"))).Single();
                if(!model||!icon||model.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||model.GetComponents<BoxCollider>().Length!=3)throw new InvalidDataException("Original chest asset contract mismatch.");
                var donor=PrefabManager.Instance.GetPrefab("piece_chest");if(!donor)throw new InvalidOperationException("Native chest donor unavailable.");
                var nativeMaterial=donor.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="ironchest").sharedMaterials.Single(m=>m.shader.name=="Custom/Piece");
                clone=PrefabManager.Instance.CreateClonedPrefab(PrefabName,donor);
                if(!clone)throw new InvalidOperationException("Original raid chest prefab clone unavailable.");
                foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                foreach(var collider in clone.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                var visual=Object.Instantiate(model,clone.transform);visual.name="wildblood_original_chest";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
                foreach(var node in visual.GetComponentsInChildren<Transform>(true))node.gameObject.layer=LayerMask.NameToLayer("piece");
                material=new Material(nativeMaterial){name="Wildblood original chest"};material.mainTexture=model.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture;material.mainTextureScale=Vector2.one;material.mainTextureOffset=Vector2.zero;
                foreach(var property in new[]{"_BumpMap","_NormalMap","_MetallicGlossMap","_OcclusionMap","_ParallaxMap","_DetailMask","_DetailAlbedoMap","_DetailNormalMap","_SpecGlossMap","_EmissionMap","_StyleTex"})if(material.HasProperty(property))material.SetTexture(property,null);
                foreach(var property in new[]{"_UseGlossmap","_Metallic","_Style"})if(material.HasProperty(property))material.SetFloat(property,0);
                if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.1f);
                if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",Color.black);
                material.DisableKeyword("_NORMALMAP");material.DisableKeyword("_METALLICGLOSSMAP");material.DisableKeyword("_EMISSION");
                foreach(var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterial=material;
                var container=clone.GetComponent<Container>();container.m_name="Raid chest";container.m_open=null;container.m_closed=null;
                var wear=clone.GetComponent<WearNTear>();wear.m_new=visual;wear.m_worn=visual;wear.m_broken=visual;wear.m_wet=null;wear.m_fragmentRoots=null;
                var piece=clone.GetComponent<Piece>();piece.m_name="Raid chest";piece.m_description="A chest for a host-started paid raid. Store Coins here, then use wildblood_raid start.";piece.m_icon=icon;piece.m_enabled=false;
                clone.AddComponent<NativeRaidChestLid>();
                if(!PieceManager.Instance.AddPiece(new CustomPiece(clone,false,new PieceConfig{PieceTable="Hammer",Enabled=false,AllowedInDungeons=piece.m_allowedInDungeons})))throw new InvalidOperationException("Original raid chest piece registration rejected.");
                prefab=clone;clone=null;material=null;
                Plugin.NativeLogger?.LogInfo("Registered original raid chest; native recipe, access, inventory, wear and save behavior retained.");
            }catch(Exception error){if(clone)Object.Destroy(clone);if(material)Object.Destroy(material);Plugin.NativeLogger?.LogError("Original raid chest registration failed: "+error);}
            finally{if(bundle)bundle.Unload(false);}
        }
    }
    internal sealed class NativeRaidChestLid:MonoBehaviour {
        Container chest;ZNetView view;Transform lid;
        void Awake(){chest=GetComponent<Container>();view=GetComponent<ZNetView>();lid=transform.Find("wildblood_original_chest/model/lid");}
        void Update(){
            if(!chest||!lid||!view||!view.IsValid())return;
            bool open=view.IsOwner()?chest.IsInUse():view.GetZDO().GetInt(ZDOVars.s_inUse)==1;
            lid.localRotation=Quaternion.RotateTowards(lid.localRotation,Quaternion.Euler(open?-100:0,0,0),300f*Time.deltaTime);
        }
    }
}
