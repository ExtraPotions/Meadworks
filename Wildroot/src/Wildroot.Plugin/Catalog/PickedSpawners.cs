using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>
 /// PlantEverything's ShowPickableSpawners: picked mushrooms, smoke puffs, thistles, dandelions and fiddleheads stay
 /// visible (and hoverable) instead of vanishing. Wildroot's picked look is its own: the plant's native meshes and
 /// colliders without lights or particles, cut down to 40 percent height. No reference art is used.
 /// </summary>
 internal static class PickedSpawners {
  internal const string ChildName="wildroot_picked";
  const float CutHeight=0.4f;
  static readonly Func<Pickable,bool> Picked=AccessTools.MethodDelegate<Func<Pickable,bool>>(NativeFields.GetPicked);
  /// <summary>Adds the inactive picked spawner to a pickable prefab once; instances spawned afterwards carry it.</summary>
  internal static void AddTo(GameObject prefab){
   if(!prefab||prefab.transform.Find(ChildName))return;var pickable=prefab.GetComponent<Pickable>();var visual=pickable?pickable.m_hideWhenPicked:null;if(!visual)return;
   var root=new GameObject(ChildName);root.SetActive(false);root.layer=visual!.layer;root.transform.SetParent(prefab.transform,false);
   root.transform.localPosition=prefab.transform.InverseTransformPoint(visual.transform.position);root.transform.localRotation=Quaternion.Inverse(prefab.transform.rotation)*visual.transform.rotation;
   root.transform.localScale=Vector3.Scale(visual.transform.localScale,new Vector3(1,CutHeight,1));CopyColliders(visual,root);
   foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true)){
    var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!filter.sharedMesh)continue;var part=filter.gameObject==visual?root:new GameObject(filter.name);
    if(part!=root){part.layer=filter.gameObject.layer;part.transform.SetParent(root.transform,false);part.transform.localPosition=visual.transform.InverseTransformPoint(filter.transform.position);part.transform.localRotation=Quaternion.Inverse(visual.transform.rotation)*filter.transform.rotation;
     var parent=visual.transform.lossyScale;var own=filter.transform.lossyScale;part.transform.localScale=new Vector3(own.x/parent.x,own.y/parent.y,own.z/parent.z);CopyColliders(filter.gameObject,part);}
    part.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;part.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
   }
  }
  static void CopyColliders(GameObject source,GameObject target){
   foreach(var collider in source.GetComponents<Collider>()){
    if(!collider||collider.isTrigger)continue;
    switch(collider){
     case SphereCollider sphere:{var c=target.AddComponent<SphereCollider>();c.center=sphere.center;c.radius=sphere.radius;break;}
     case CapsuleCollider capsule:{var c=target.AddComponent<CapsuleCollider>();c.center=capsule.center;c.radius=capsule.radius;c.height=capsule.height;c.direction=capsule.direction;break;}
     case BoxCollider box:{var c=target.AddComponent<BoxCollider>();c.center=box.center;c.size=box.size;break;}
    }
   }
  }
  /// <summary>Shows the picked spawner only while picked, with Wildroot active and ShowPickableSpawners on.</summary>
  internal static void Sync(Pickable pickable){
   if(!pickable)return;var child=pickable.transform.Find(ChildName);if(!child)return;
   var plugin=Plugin.Instance;var view=plugin?plugin!.Catalog?.View:null;
   bool show=plugin&&plugin!.GameplayAvailable&&plugin.Settings?.Active==true&&view!=null&&CatalogPolicy.ShowsPickedSpawner(PlacementBackend.PrefabName(pickable.gameObject),view)&&Picked(pickable);
   if(child.gameObject.activeSelf!=show)child.gameObject.SetActive(show);
  }
  /// <summary>Applies a live setting change to loaded pickables.</summary>
  internal static void SyncAll(){foreach(var pickable in UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None))Sync(pickable);}
  [HarmonyPatch(typeof(Pickable),"SetPicked")]
  static class SetPickedPatch {static void Postfix(Pickable __instance){try{Sync(__instance);}catch(Exception){}}}
  [HarmonyPatch(typeof(Pickable),"Awake")]
  static class AwakePatch {static void Postfix(Pickable __instance){try{Sync(__instance);}catch(Exception){}}}
 }
}
