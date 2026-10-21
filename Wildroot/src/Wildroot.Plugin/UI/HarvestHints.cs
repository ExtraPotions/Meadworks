using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>Read-only harvest/replant help on ripe objects; never reserves materials or changes the selection.</summary>
 [HarmonyPatch]
 internal static class HarvestHints {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){yield return AccessTools.Method(typeof(Pickable),"GetHoverText");yield return AccessTools.Method(typeof(Beehive),"GetHoverText");}
  static void Postfix(Component __instance,ref string __result){
   var plugin=Plugin.Instance;var player=Player.m_localPlayer;var settings=plugin?.Settings;var text=plugin?.Text;
   if(plugin?.GameplayAvailable!=true||settings==null||text==null||!settings.Active||!settings.Get("Harvest.Enabled",true)||!player||player.IsDead()||player.IsTeleporting())return;
   bool ready=__instance is Pickable pickable?(bool)NativeFields.CanBePicked.Invoke(pickable,Array.Empty<object>()):__instance is Beehive hive&&(int)NativeFields.GetHoneyLevel.Invoke(hive,Array.Empty<object>())>0;
   if(!ready)return;
   // One hint line under one switch (Preview.ShowHoverKeyHints): the harvest keys, then the replacement crop while
   // replanting is on. Preview.ShowHoverReplantHint stays as a file setting that drops only the crop part.
   if(!settings.Get("Preview.ShowHoverKeyHints",true))return;
   string use=Localization.instance.Localize("$KEY_Use");
   string hint=text.Format("harvest_modifier_hint",GridHud.KeyLabel(settings.Find(ZInput.IsGamepadActive()?"Input.GamepadModifierKey":"Input.KeyboardHarvestModifierKey")?.BoxedValue),use);
   string? replant=settings.Get("Preview.ShowHoverReplantHint",true)&&settings.Get("Harvest.Replant",false)&&__instance is Pickable?ReplantName(plugin,settings,__instance):null;
   if(replant!=null)hint+=" · "+text.Format("replant_hint",replant);
   __result=string.IsNullOrEmpty(__result)?hint:__result+"\n"+hint;
  }
  static string? ReplantName(Plugin plugin,WildrootConfig settings,Component pickable){
   if(plugin.Catalog==null)return null;
   string source=NativePlanting.BasePrefabName(pickable.gameObject);var original=plugin.Catalog.Definitions.Values.FirstOrDefault(d=>d.Kind==PlantKind.Crop&&d.GrownPrefab==source);
   if(original==null)return null;
   // Show the configured replacement even when seeds are short; the actual transaction still checks and charges them.
   var desired=ReplantPlanner.Choose(new ReplantContext(original.Id,plugin.Controls?.LastSelectedPlant??"",settings.Get("Harvest.ReplantTarget","Original")=="Selected",_=>int.MaxValue),new CatalogSnapshot(plugin.Catalog.Definitions.Values.ToArray())).Plant;
   if(desired==null)return null;
   var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(desired.Prefab):null;var piece=prefab?prefab.GetComponent<Piece>():null;
   return Localization.instance.Localize(piece?piece.m_name:desired.Prefab);
  }
 }
}
