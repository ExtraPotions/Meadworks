using System;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 internal static class GrowthPolicy {
  internal static bool Unrestricted=>Plugin.Instance&&Plugin.Instance.GameplayAvailable&&Plugin.Instance.Settings!=null&&Plugin.Instance.Settings.UnrestrictedGrowth;
  internal static PiecePolicy? For(Component plant)=>Plugin.Instance&&Plugin.Instance.GameplayAvailable&&Plugin.Instance.Settings?.Active==true?Plugin.Instance.Catalog?.Policy(PlacementBackend.PrefabName(plant.gameObject)):null;
 }
 [HarmonyPatch(typeof(Plant),"HaveGrowSpace")]
 internal static class GrowthSpacePatch {static bool Prefix(Plant __instance,ref bool __result){if(!GrowthPolicy.Unrestricted&&GrowthPolicy.For(__instance)?.RequiresGrowthSpace!=false)return true;__result=true;return false;}}
 [HarmonyPatch(typeof(Plant),"HaveRoof")]
 internal static class GrowthRoofPatch {static bool Prefix(Plant __instance,ref bool __result){if(!GrowthPolicy.Unrestricted&&GrowthPolicy.For(__instance)?.RequiresSunlight!=false)return true;__result=false;return false;}}
 [HarmonyPatch(typeof(Plant),"UpdateHealth")]
 internal static class GrowthHealthPatch {
  static bool Prefix(Plant __instance){
   if(!GrowthPolicy.Unrestricted)return true;
   NativeFields.PlantStatus.SetValue(__instance,Plant.Status.Healthy);
   var healthy=(GameObject)NativeFields.PlantHealthy.GetValue(__instance);
   var unhealthy=(GameObject)NativeFields.PlantUnhealthy.GetValue(__instance);
   if(healthy)healthy.SetActive(true);if(unhealthy)unhealthy.SetActive(false);
   return false;
  }
 }
 /// <summary>PlantEverything hover timers: remaining growth on plants and respawn on picked pickables.</summary>
 internal static class HoverTimers {
  internal static string PlantText(Plant plant){
   var settings=Plugin.Instance?.Settings;if(settings==null||!settings.Get("Hover.EnablePlantTimers",true)||!Plugin.Instance!.GameplayAvailable)return "";
   var view=plant.GetComponent<ZNetView>();if(!view||!view.IsValid())return "";
   double since=(double)NativeFields.TimeSincePlanted.Invoke(plant,Array.Empty<object>());double grow=(float)NativeFields.GetGrowTime.Invoke(plant,Array.Empty<object>());
   return grow<=0?"":HoverTimer.Format(grow-since,since/grow,settings.Get("Hover.GrowthAsPercentage",false));
  }
  internal static string PickableText(Pickable pickable){
   var settings=Plugin.Instance?.Settings;if(settings==null||!settings.Get("Hover.EnablePickableTimers",true)||!Plugin.Instance!.GameplayAvailable||pickable.m_respawnTimeMinutes<=0||!ZNet.instance)return "";
   var view=pickable.GetComponent<ZNetView>();if(!view||!view.IsValid()||!(bool)NativeFields.GetPicked.Invoke(pickable,Array.Empty<object>()))return "";
   long picked=view.GetZDO().GetLong(ZDOVars.s_pickedTime,0L);if(picked<=0)return "";double total=pickable.m_respawnTimeMinutes*60.0,elapsed=(ZNet.instance.GetTime()-new DateTime(picked)).TotalSeconds;
   return HoverTimer.Format(total-elapsed,elapsed/total,settings.Get("Hover.GrowthAsPercentage",false));
  }
  static string Append(string text,string timer)=>string.IsNullOrEmpty(timer)?text:(string.IsNullOrEmpty(text)?"":text+"\n")+"<color=#C0C0C0>"+timer+"</color>";
  [HarmonyPatch(typeof(Plant),"GetHoverText")]
  static class PlantHover {static void Postfix(Plant __instance,ref string __result){try{__result=Append(__result,PlantText(__instance));}catch(Exception){}}}
  [HarmonyPatch(typeof(Pickable),"GetHoverText")]
  static class PickableHover {static void Postfix(Pickable __instance,ref string __result){try{__result=Append(__result,PickableText(__instance));}catch(Exception){}}}
 }
}
