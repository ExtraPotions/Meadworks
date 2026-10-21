using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>
 /// Farming's runtime state. Farming is the game's own skill (Skills.SkillType.Farming): Wildroot adds perks and
 /// experience scaling through Crucible's skill extensions and never registers a skill of its own. Behavior follows
 /// GrindstoneSkills' Farming module (GPL-3.0, see THIRD_PARTY_NOTICES/GrindstoneSkills.txt).
 /// </summary>
 internal static class FarmingState {
  internal const Skills.SkillType Skill=Skills.SkillType.Farming;
  static readonly FarmingRules Off=new FarmingRules{Enabled=false};
  internal static FarmingRules Rules{get{var plugin=Plugin.Instance;var settings=plugin?plugin!.Settings:null;return settings==null||!plugin!.GameplayAvailable||!settings.Active?Off:settings.Farming;}}
  internal static bool Active=>Rules.Enabled;
  /// <summary>A player's Farming level; for the local player, including Crucible skill modifiers.</summary>
  internal static float Level(Player? player)=>player?MeadworksSkills.EffectiveLevel(player,Skill):0f;
  internal static readonly AccessTools.FieldRef<Plant,ZNetView> PlantView=AccessTools.FieldRefAccess<Plant,ZNetView>("m_nview");
  internal static readonly AccessTools.FieldRef<Pickable,ZNetView> PickableView=AccessTools.FieldRefAccess<Pickable,ZNetView>("m_nview");
  internal static readonly AccessTools.FieldRef<Pickable,bool> PickablePicked=AccessTools.FieldRefAccess<Pickable,bool>("m_picked");
  internal static readonly AccessTools.FieldRef<Pickable,bool> PickablePickedLocal=AccessTools.FieldRefAccess<Pickable,bool>("m_pickedLocal");
  internal static readonly AccessTools.FieldRef<Pickable,int> PickableEnabled=AccessTools.FieldRefAccess<Pickable,int>("m_enabled");
 }

 /// <summary>Who planted a plant and at what Farming level, on the plant's ZDO so every peer reads the same perks.</summary>
 internal static class PlantKeys {
  internal const string PlanterKey="wildroot.farm.planter",LevelKey="wildroot.farm.level";
  static readonly int PlanterHash=PlanterKey.GetStableHashCode(),LevelHash=LevelKey.GetStableHashCode();
  internal static ZDO? Of(Plant plant){var view=plant?FarmingState.PlantView(plant):null;return view&&view!.IsValid()?view.GetZDO():null;}
  internal static bool IsPlanted(ZDO? zdo)=>zdo!=null&&zdo.GetLong(PlanterHash,0L)!=0L;
  internal static float Level(ZDO? zdo)=>zdo?.GetFloat(LevelHash,0f)??0f;
  internal static void Write(ZDO zdo,long planter,float level){zdo.Set(PlanterHash,planter);zdo.Set(LevelHash,level);}
 }

 /// <summary>A plant that grows into a pickable crop, with the items that set its experience tier.</summary>
 internal sealed class CropInfo {
  internal string Plant="";internal Plant Prefab=null!;internal Piece? Piece;internal string Pickable="";
  internal ItemDrop? Seed;internal ItemDrop? Crop;internal readonly List<ItemDrop> Extras=new List<ItemDrop>();
  internal float? Value;
  internal IEnumerable<string> Items(){if(Seed)yield return Seed!.name;if(Crop)yield return Crop!.name;foreach(var extra in Extras)if(extra)yield return extra.name;}
 }

 /// <summary>Crops indexed from the scene's plant prefabs (rebuilt when the scene or its prefab list changes) and their value.</summary>
 internal static class CropIndex {
  static readonly Dictionary<string,CropInfo> byPlant=new Dictionary<string,CropInfo>(StringComparer.Ordinal);
  static readonly Dictionary<string,CropInfo> byPickable=new Dictionary<string,CropInfo>(StringComparer.Ordinal);
  sealed class Known {internal CropInfo? Crop;}
  static ConditionalWeakTable<Plant,Known> known=new ConditionalWeakTable<Plant,Known>();
  static ZNetScene? scene;static int count=-1;static CropValueGraph? values;static ObjectDB? valuesFor;
  static void Ensure(){
   var current=ZNetScene.instance;if(!current){Clear();return;}
   if(current==scene&&current.m_prefabs.Count==count)return;
   Clear();scene=current;count=current.m_prefabs.Count;
   foreach(var obj in current.m_prefabs){
    var plant=obj?obj.GetComponent<Plant>():null;if(!plant||plant!.m_grownPrefabs==null)continue;
    foreach(var grown in plant.m_grownPrefabs){
     var pickable=grown?grown.GetComponent<Pickable>():null;if(!pickable||grown!.GetComponent<TreeBase>()||grown.GetComponent<Vine>())continue;
     var piece=obj!.GetComponent<Piece>();
     var crop=new CropInfo{Plant=obj.name,Prefab=plant,Piece=piece,Pickable=grown.name,Seed=piece&&piece!.m_resources!=null&&piece.m_resources.Length>0?piece.m_resources[0].m_resItem:null,Crop=pickable!.m_itemPrefab?pickable.m_itemPrefab.GetComponent<ItemDrop>():null};
     if(pickable.m_extraDrops?.m_drops!=null)foreach(var drop in pickable.m_extraDrops.m_drops){var item=drop.m_item?drop.m_item.GetComponent<ItemDrop>():null;if(item&&item!=crop.Crop&&!crop.Extras.Contains(item!))crop.Extras.Add(item!);}
     byPlant[crop.Plant]=crop;if(!byPickable.ContainsKey(crop.Pickable))byPickable[crop.Pickable]=crop;break;
    }
   }
  }
  static void Clear(){byPlant.Clear();byPickable.Clear();known=new ConditionalWeakTable<Plant,Known>();scene=null;count=-1;values=null;valuesFor=null;}
  internal static CropInfo? OfPlant(string name){Ensure();return byPlant.TryGetValue(name,out var crop)?crop:null;}
  internal static CropInfo? OfPickable(string name){Ensure();return byPickable.TryGetValue(name,out var crop)?crop:null;}
  internal static bool IsCrop(Piece? piece)=>piece&&OfPlant(PlacementBackend.PrefabName(piece!.gameObject))!=null;
  /// <summary>The crop a live plant belongs to, cached per instance; null for saplings, vines and other plants.</summary>
  internal static CropInfo? CropOf(Plant plant){
   Ensure();
   return known.GetValue(plant,instance=>new Known{Crop=byPlant.TryGetValue(PlacementBackend.PrefabName(instance.gameObject),out var crop)?crop:null}).Crop;
  }
  /// <summary>The crop's value for experience tiers, computed once per crop from ObjectDB recipes and the scene's converters.</summary>
  internal static float Value(CropInfo? crop){
   if(crop==null)return 0f;if(crop.Value.HasValue&&valuesFor==ObjectDB.instance)return crop.Value.Value;
   var graph=Graph();var value=graph==null?0f:graph.Value(crop.Items());if(graph!=null)crop.Value=value;return value;
  }
  static CropValueGraph? Graph(){
   var db=ObjectDB.instance;if(!db){values=null;valuesFor=null;return null;}
   if(values!=null&&valuesFor==db)return values;
   var graph=new CropValueGraph();
   foreach(var obj in db.m_items){var drop=obj?obj.GetComponent<ItemDrop>():null;if(drop)graph.Food(drop!.name,Food(drop));}
   foreach(var recipe in db.m_recipes){
    if(!recipe||!recipe.m_item||recipe.m_resources==null||!recipe.m_craftingStation||recipe.m_craftingStation.m_craftingSkill!=Skills.SkillType.Cooking)continue;
    float dish=Food(recipe.m_item);foreach(var requirement in recipe.m_resources)if(requirement?.m_resItem)graph.Dish(requirement.m_resItem.name,dish);
   }
   var current=ZNetScene.instance;
   if(current)foreach(var obj in current.m_prefabs){
    if(!obj)continue;
    var station=obj.GetComponent<CookingStation>();if(station&&station.m_skill==Skills.SkillType.Cooking&&station.m_conversion!=null)foreach(var c in station.m_conversion)if(c?.m_from)graph.Dish(c.m_from.name,c.m_to?Food(c.m_to):0f);
    var fermenter=obj.GetComponent<Fermenter>();if(fermenter&&fermenter.m_conversion!=null)foreach(var c in fermenter.m_conversion)if(c?.m_from)graph.Dish(c.m_from.name,c.m_to?Food(c.m_to):0f);
    var smelter=obj.GetComponent<Smelter>();if(smelter&&smelter.m_conversion!=null)foreach(var c in smelter.m_conversion)if(c?.m_from&&c.m_to)graph.Convert(c.m_from.name,c.m_to.name);
   }
   values=graph;valuesFor=db;foreach(var crop in byPlant.Values)crop.Value=null;return graph;
  }
  static float Food(ItemDrop drop){var shared=drop.m_itemData?.m_shared;return shared==null?0f:shared.m_food+shared.m_foodStamina+shared.m_foodEitr;}
 }
}
