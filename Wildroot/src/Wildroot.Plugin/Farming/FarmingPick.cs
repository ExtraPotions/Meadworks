using System;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>Scopes native Farming experience and bonus chance; harvest perks follow owner acceptance.</summary>
 [HarmonyPatch(typeof(Pickable),nameof(Pickable.Interact))]
 static class FarmingPick {
  [ThreadStatic]static State? ordinary;
  internal sealed class State {internal Pickable Pickable=null!;internal CropInfo Crop=null!;internal Player Player=null!;internal State? Previous;internal ZDOID Id;internal float Chance;internal bool Opened,Finished,Captured;}
  static void Prefix(Pickable __instance,Humanoid character,out State? __state){
   __state=null;
   if(!(character is Player player)||player!=Player.m_localPlayer||!FarmingState.Active||!CanPick(__instance))return;
   var crop=CropIndex.OfPickable(PlacementBackend.PrefabName(__instance.gameObject));if(crop==null)return;
   __state=new State{Pickable=__instance,Crop=crop,Id=FarmingState.PickableView(__instance).GetZDO().m_uid,Chance=__instance.m_maxLevelBonusChance};
   __instance.m_maxLevelBonusChance=FarmingPerks.BonusChance(FarmingState.Rules,__state.Chance);
   __state.Opened=FarmingExperience.Begin(crop,crop.Pickable);
   if(!NativeHarvestService.Dispatching){__state.Player=player;__state.Previous=ordinary;ordinary=__state;}
  }
  static void Postfix(Pickable __instance,Humanoid character,State? __state){
   if(__state!=null)Finish(__state);
  }
  static Exception? Finalizer(Exception? __exception,State? __state){if(__state!=null)Finish(__state);return __exception;}
  static void Finish(State state){if(state.Finished)return;state.Finished=true;if(ordinary==state)ordinary=state.Previous;if(state.Pickable)state.Pickable.m_maxLevelBonusChance=state.Chance;FarmingExperience.End(state.Opened);}
  internal static bool CaptureDispatch(ZNetView view,string method,object[] arguments){
   var state=ordinary;
   if(state==null||state.Finished||method!="RPC_Pick"||FarmingState.PickableView(state.Pickable)!=view||arguments.Length!=1||!(arguments[0] is int bonus))return false;
   if(state.Captured)return true;
   state.Captured=Plugin.Instance?.Harvesting?.SubmitSingle(state.Pickable,state.Player,state.Id,bonus)==true;
   return state.Captured;
  }
  static bool CanPick(Pickable pickable){
   var view=FarmingState.PickableView(pickable);
   if(!view||!view.IsValid()||FarmingState.PickableEnabled(pickable)==0||FarmingState.PickablePicked(pickable)||FarmingState.PickablePickedLocal(pickable)||!pickable.CanBePicked())return false;
   var floating=pickable.m_tarPreventsPicking?pickable.GetComponent<Floating>():null;return !floating||!floating!.IsInTar();
  }
 }
}
