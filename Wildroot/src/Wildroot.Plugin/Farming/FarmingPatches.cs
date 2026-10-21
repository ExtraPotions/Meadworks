using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>
 /// Farming experience (#21, #65). The game raises Farming by 1 per cultivator placement and per crop picked, on the
 /// player's own client. While a scope is open (a crop being planted or picked) Crucible's experience hook multiplies
 /// that raise by Experience Multiplier and the crop's tier, and a pick of a kind the character never picked by the
 /// discovery multiplier. Discovery is kept in the character's own custom data and saves with it.
 /// </summary>
 internal static class FarmingExperience {
  internal const string PickedKey="wildroot.farm.picked";
  sealed class Scope {internal CropInfo? Crop;internal string? Discover;}
  static readonly Scope current=new Scope();static Scope? scope;
  internal static bool Begin(CropInfo? crop,string? discover){if(scope!=null)return false;current.Crop=crop;current.Discover=discover;scope=current;return true;}
  internal static void End(bool opened){if(opened)scope=null;}
  /// <summary>Crucible experience provider: the factor for one local Farming raise.</summary>
  internal static float Scale(Player player,float factor){
   var rules=FarmingState.Rules;var open=scope;if(!rules.Enabled||open==null)return factor;
   bool first=false;var kind=open.Discover;open.Discover=null;
   if(kind!=null){player.m_customData.TryGetValue(PickedKey,out var record);var (isFirst,next)=FarmingXp.Discover(record,kind);if(isFirst){player.m_customData[PickedKey]=next;first=true;}}
   return factor*FarmingXp.Scale(rules,CropIndex.Value(open.Crop),first);
  }
  /// <summary>
  /// Wildroot plants crops itself, so the game's placement never raises Farming for them: raise once per committed
  /// cell, as the game does per cultivator placement. Recovered placements were paid in an earlier session and raise nothing.
  /// </summary>
  internal static void Planted(Player actor,Piece piece){
   if(!actor||actor!=Player.m_localPlayer)return;
   bool opened=Begin(CropIndex.OfPlant(PlacementBackend.PrefabName(piece.gameObject)),null);
   try{actor.RaiseSkill(FarmingState.Skill,1f);}finally{End(opened);}
  }
  /// <summary>The game's own cultivator placements (pieces Wildroot does not plant): scale their raise by the crop's tier.</summary>
  [HarmonyPatch(typeof(Player),"UpdatePlacement")]
  static class PlacementScope {
   static readonly AccessTools.FieldRef<Player,GameObject> Ghost=AccessTools.FieldRefAccess<Player,GameObject>("m_placementGhost");
   static void Prefix(Player __instance,out bool __state){
    __state=false;if(__instance!=Player.m_localPlayer||!Ghost(__instance)||!FarmingState.Active)return;
    var piece=__instance.GetSelectedPiece();if(!piece||!piece.GetComponent<Plant>())return;
    __state=Begin(CropIndex.OfPlant(PlacementBackend.PrefabName(piece.gameObject)),null);
   }
   static Exception? Finalizer(Exception? __exception,bool __state){End(__state);return __exception;}
  }
 }

 /// <summary>Marks a plant the local player places with the planter and their Farming level (#12, #64): its perks follow that level on every peer.</summary>
 internal static class PlanterMark {
  static bool placing;
  [HarmonyPatch(typeof(Player),nameof(Player.PlacePiece))]
  static class Placing {
   static void Prefix(Player __instance){placing=__instance==Player.m_localPlayer;}
   static Exception? Finalizer(Exception? __exception){placing=false;return __exception;}
  }
  [HarmonyPatch(typeof(Piece),nameof(Piece.SetCreator))]
  static class Creator {
   static void Postfix(Piece __instance,long uid){
    if(!placing||uid==0L||!FarmingState.Active)return;
    var plant=__instance.GetComponent<Plant>();var zdo=plant?PlantKeys.Of(plant!):null;if(zdo==null||GrowsVine(plant!))return;
    PlantKeys.Write(zdo,uid,FarmingState.Level(Player.m_localPlayer));
   }
  }
  static bool GrowsVine(Plant plant){if(plant.m_grownPrefabs==null)return false;foreach(var grown in plant.m_grownPrefabs)if(grown&&grown.GetComponent<Vine>())return true;return false;}
 }

 /// <summary>Planted plants grow faster with the planter's level (#12): the grow time is divided by 1 + its share of Growth Speed.</summary>
 [HarmonyPatch(typeof(Plant),"GetGrowTime")]
 static class FarmingGrowTime {
  static void Postfix(Plant __instance,ref float __result){
   var rules=FarmingState.Rules;if(!rules.Enabled)return;var zdo=PlantKeys.Of(__instance);if(!PlantKeys.IsPlanted(zdo))return;
   __result=FarmingPerks.GrowTime(rules,__result,true,PlantKeys.Level(zdo));
  }
 }

 /// <summary>
 /// Crop traits from the planter's level (#12 grow space, #64 heat and cold tolerance), set on the instance before
 /// the game checks its health and always rebuilt from the prefab, so turning Farming off restores the game's values.
 /// Rain (#12): once per minute of game time a healthy planted plant gains part of that time while it rains.
 /// </summary>
 [HarmonyPatch(typeof(Plant),nameof(Plant.UpdateHealth))]
 static class FarmingPlantHealth {
  sealed class Mark {internal double Time;}
  static readonly ConditionalWeakTable<Plant,Mark> marks=new ConditionalWeakTable<Plant,Mark>();
  static void Prefix(Plant __instance){
   var crop=CropIndex.CropOf(__instance);if(crop==null||!crop.Prefab)return;
   var rules=FarmingState.Rules;var zdo=PlantKeys.Of(__instance);bool planted=rules.Enabled&&PlantKeys.IsPlanted(zdo);float level=PlantKeys.Level(zdo);
   FarmingTraits.Apply(__instance,crop.Prefab,rules,planted,level);
  }
  static void Postfix(Plant __instance,double timeSincePlanted){
   var rules=FarmingState.Rules;if(!rules.Enabled||timeSincePlanted<FarmingPerks.RainMinAge||!ZNet.instance)return;
   var view=FarmingState.PlantView(__instance);if(!view||!view.IsValid()||!view.IsOwner())return;
   double now=ZNet.instance.GetTimeSeconds();
   if(!marks.TryGetValue(__instance,out var mark)){marks.Add(__instance,new Mark{Time=now});return;}
   double elapsed=now-mark.Time;if(elapsed<FarmingPerks.RainInterval)return;mark.Time=now;
   var zdo=view.GetZDO();
   double credit=FarmingPerks.RainCredit(rules,elapsed,EnvMan.IsWet()&&__instance.GetStatus()==Plant.Status.Healthy&&PlantKeys.IsPlanted(zdo));
   if(credit<=0)return;
   long planted=zdo.GetLong(ZDOVars.s_plantTime,ZNet.instance.GetTime().Ticks);
   zdo.Set(ZDOVars.s_plantTime,planted-TimeSpan.FromSeconds(credit).Ticks);
  }
 }

 /// <summary>Effective crop traits for a planter level, for live plants and for the local player's placement checks.</summary>
 internal static class FarmingTraits {
  internal static void Apply(Plant plant,Plant prefab,FarmingRules rules,bool planted,float level){
   plant.m_growRadius=FarmingPerks.GrowRadius(rules,prefab.m_growRadius,planted,level);
   plant.m_tolerateHeat=FarmingPerks.HeatTolerant(rules,prefab.m_tolerateHeat,planted,level);
   plant.m_tolerateCold=FarmingPerks.ColdTolerant(rules,prefab.m_tolerateCold,(int)prefab.m_biome,planted,level);
   plant.m_biome=(Heightmap.Biome)FarmingPerks.Biomes(rules,(int)prefab.m_biome,planted,level);
  }
  /// <summary>
  /// The traits a crop will have once the local player plants it. A plant without a live network identity (a prefab
  /// or a placement ghost) is about to be planted; a live plant already carries its planter's traits.
  /// </summary>
  internal static (float radius,Heightmap.Biome biome,bool heat,bool cold) Placement(Plant plant){
   var view=FarmingState.PlantView(plant);var rules=FarmingState.Rules;
   if(view&&view.IsValid()||!rules.Enabled||CropIndex.OfPlant(PlacementBackend.PrefabName(plant.gameObject))==null)return (plant.m_growRadius,plant.m_biome,plant.m_tolerateHeat,plant.m_tolerateCold);
   float level=FarmingState.Level(Player.m_localPlayer);
   return (FarmingPerks.GrowRadius(rules,plant.m_growRadius,true,level),(Heightmap.Biome)FarmingPerks.Biomes(rules,(int)plant.m_biome,true,level),
    FarmingPerks.HeatTolerant(rules,plant.m_tolerateHeat,true,level),FarmingPerks.ColdTolerant(rules,plant.m_tolerateCold,(int)plant.m_biome,true,level));
  }
 }

 /// <summary>
 /// Picking a crop (#12, #21): the bonus crop rolls with Bonus Yield Chance in place of the game's 25%, the raise is
 /// scaled (tier, discovery), and afterwards seed return and auto replant (#108) roll at the picker's level. Bulk
 /// harvest calls the same Interact inside its dispatch scope; its seed return and replant run when the owner's receipt
 /// confirms the harvest, so nothing is given for a pick that did not happen.
 /// </summary>
 [HarmonyPatch(typeof(Pickable),nameof(Pickable.Interact))]
 static class FarmingPick {
  // Id is read before the pick: the owner's pick destroys a non-respawning crop's network view inside Interact.
  internal sealed class State {internal Pickable Pickable=null!;internal CropInfo Crop=null!;internal ZDOID Id;internal float Chance;internal bool Opened,Finished;}
  static void Prefix(Pickable __instance,Humanoid character,out State? __state){
   __state=null;
   if(!(character is Player player)||player!=Player.m_localPlayer||!FarmingState.Active||!CanPick(__instance))return;
   var crop=CropIndex.OfPickable(PlacementBackend.PrefabName(__instance.gameObject));if(crop==null)return;
   __state=new State{Pickable=__instance,Crop=crop,Id=FarmingState.PickableView(__instance).GetZDO().m_uid,Chance=__instance.m_maxLevelBonusChance};
   __instance.m_maxLevelBonusChance=FarmingPerks.BonusChance(FarmingState.Rules,__state.Chance);
   __state.Opened=FarmingExperience.Begin(crop,crop.Pickable);
  }
  static void Postfix(Pickable __instance,Humanoid character,State? __state){
   if(__state==null)return;Finish(__state);
   // A pick happened only if the game marked this client's pick; a bulk-harvest prefix may have skipped Interact.
   if(!FarmingState.PickablePickedLocal(__instance)||NativeHarvestService.Dispatching)return;
   try{AfterPick((Player)character,__instance,__state);}
   catch(Exception error){Debug.LogError("Wildroot farming pick failed: "+error.GetType().Name+": "+error.Message);}
  }
  static Exception? Finalizer(Exception? __exception,State? __state){if(__state!=null)Finish(__state);return __exception;}
  static void Finish(State state){if(state.Finished)return;state.Finished=true;if(state.Pickable)state.Pickable.m_maxLevelBonusChance=state.Chance;FarmingExperience.End(state.Opened);}
  static bool CanPick(Pickable pickable){
   var view=FarmingState.PickableView(pickable);
   if(!view||!view.IsValid()||FarmingState.PickableEnabled(pickable)==0||FarmingState.PickablePicked(pickable)||FarmingState.PickablePickedLocal(pickable)||!pickable.CanBePicked())return false;
   var floating=pickable.m_tarPreventsPicking?pickable.GetComponent<Floating>():null;return !floating||!floating!.IsInTar();
  }
  static void AfterPick(Player player,Pickable pickable,State state){
   var rules=FarmingState.Rules;float level=FarmingState.Level(player);
   FarmingSeeds.Roll(rules,level,state.Crop,pickable.transform.position);
   if(FarmingPerks.AutoReplants(rules,level))Plugin.Instance?.Harvesting?.SubmitAutoReplant(pickable,state.Id);
  }
 }

 /// <summary>Seed return (#12): a pick sometimes gives back the seed its plant grew from, made by the picker at the crop.</summary>
 internal static class FarmingSeeds {
  internal static ItemDrop? Roll(FarmingRules rules,float level,CropInfo crop,Vector3 position){
   float chance=FarmingPerks.SeedReturnChance(rules,level);
   if(!crop.Seed||chance<=0f||chance<1f&&UnityEngine.Random.value>=chance)return null;
   var spawned=UnityEngine.Object.Instantiate(crop.Seed!.gameObject,position+Vector3.up*0.5f,Quaternion.Euler(0f,UnityEngine.Random.Range(0f,360f),0f));
   var item=spawned.GetComponent<ItemDrop>();if(!item)return null;
   item.SetStack(1);ItemDrop.OnCreateNew(item);
   var view=item.GetComponent<ZNetView>();if(view&&view.IsValid())ItemDrop.SaveToZDO(item.m_itemData,view.GetZDO());
   var body=spawned.GetComponent<Rigidbody>();if(body)body.linearVelocity=Vector3.up*4f;
   return item;
  }
 }
}
