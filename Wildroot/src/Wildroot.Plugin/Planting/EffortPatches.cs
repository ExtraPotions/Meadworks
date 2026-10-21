using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 internal static class CultivatorEffort {
  internal static bool Applies(Player player,ItemDrop.ItemData? tool){var plugin=Plugin.Instance;return plugin&&plugin.GameplayAvailable&&plugin.Settings?.Active==true&&player==Player.m_localPlayer&&tool?.m_dropPrefab&&tool.m_dropPrefab.name=="Cultivator";}
 }
 [HarmonyPatch(typeof(Player),"GetBuildStamina")]
 static class BuildStaminaPatch {
  static void Postfix(Player __instance,ref float __result){
   if(!CultivatorEffort.Applies(__instance,PlacementController.RightItem(__instance)))return;
   var plugin=Plugin.Instance!;var settings=plugin.Settings!;
   if(!settings.Get("Planting.UseStamina",true)){__result=0;return;}
  }
 }
 [HarmonyPatch(typeof(Player),"GetPlaceDurability")]
 static class PlaceDurabilityPatch {
  static void Postfix(Player __instance,ItemDrop.ItemData __0,ref float __result){if(CultivatorEffort.Applies(__instance,__0)&&Plugin.Instance!.Settings?.Get("Planting.UseDurability",true)==false)__result=0;}
 }
}
