using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>
 /// Farming's registrations: the experience provider and skill page on Crucible's skill extensions (#21, #65), and
 /// the SeedCrafting workbench recipes (#31). Seed recipes are enabled only while Wildroot runs and the server's
 /// SeedCrafting.Enabled is on. Dispose removes everything.
 /// </summary>
 internal sealed class FarmingFeatures:IDisposable {
  readonly WildrootConfig settings;readonly LocalizationLoader? text;
  IDisposable? experience,page;
  readonly List<CustomRecipe> recipes=new List<CustomRecipe>();
  bool recipesOn;
  internal FarmingFeatures(WildrootConfig settings,LocalizationLoader? text){
   this.settings=settings;this.text=text;
   experience=MeadworksSkills.RegisterExperience(Plugin.Guid,FarmingState.Skill,FarmingExperience.Scale);
   page=MeadworksSkills.RegisterPage(Plugin.Guid,FarmingState.Skill,Page);
   foreach(var seed in SeedRecipes.All){
    var recipe=new CustomRecipe(new RecipeConfig{Name=seed.Name,Item=seed.Seed,Amount=seed.Amount,CraftingStation=seed.Station,MinStationLevel=seed.StationLevel,Enabled=false,Requirements=new[]{new RequirementConfig(seed.Crop,seed.Cost)}});
    if(ItemManager.Instance.AddRecipe(recipe))recipes.Add(recipe);
   }
  }
  /// <summary>Recipes the game currently offers; for diagnostics and tests.</summary>
  internal bool RecipesEnabled=>recipesOn;
  internal IReadOnlyList<CustomRecipe> Recipes=>recipes;
  internal void Update(){
   var plugin=Plugin.Instance;
   recipesOn=plugin&&plugin!.GameplayAvailable&&settings.Active&&settings.Get(SeedRecipes.EnabledId,true);
   foreach(var recipe in recipes){var native=recipe.Recipe;if(native&&native.m_enabled!=recipesOn)native.m_enabled=recipesOn;}
  }
  /// <summary>
  /// Farming's page, laid out as the Solo profile's GrindstoneSkills book (Skills/Pages/FarmingPage.cs, Book/PageText.cs):
  /// the about line, the game's own planting stamina and scythe reach, the perk numbers, then "Perks" with each one
  /// "unlocked" or "at level N". Giant crops, tending and the compost bin are not part of Wildroot (user decision).
  /// </summary>
  IReadOnlyList<SkillPageLine> Page(Player player,float level){
   var lines=new List<SkillPageLine>();var rules=FarmingState.Rules;
   if(!rules.Enabled){Game(lines,player,level);lines.Add(new SkillPageLine(T("farm_bonus_any"),title:T("farm_bonus_title"),tooltip:T("farm_bonus_any_tip")));lines.Add(new SkillPageLine(T("farm_off")));return lines;}
   lines.Add(new SkillPageLine(T("farm_about")));
   Game(lines,player,level);
   lines.Add(new SkillPageLine(F("farm_bonus",FarmingPage.Percent(FarmingPerks.Factor(level)*FarmingPerks.BonusChance(rules,FarmingPerks.NativeBonusChance))),title:T("farm_bonus_title"),tooltip:T("farm_bonus_tip")));
   if(rules.SeedReturnChanceAt100>0)lines.Add(new SkillPageLine(F("farm_seed",FarmingPage.Percent(FarmingPerks.SeedReturnChance(rules,level))),title:T("farm_seed_title"),tooltip:T("farm_seed_tip")));
   if(rules.GrowthSpeedAt100>0)lines.Add(new SkillPageLine(F("farm_growth",FarmingPage.Percent(FarmingPerks.Share(rules.GrowthSpeedAt100,level))),title:T("farm_growth_title"),tooltip:rules.RainGrowthBonus>0?F("farm_growth_tip_rain",Number(rules.RainGrowthBonus)):T("farm_growth_tip")));
   if(rules.GrowSpaceReductionAt100>0)lines.Add(new SkillPageLine(F("farm_spacing",FarmingPage.Percent(Math.Min(FarmingPerks.MaxSpaceReduction,FarmingPerks.Share(rules.GrowSpaceReductionAt100,level)))),title:T("farm_spacing_title"),tooltip:T("farm_spacing_tip")));
   lines.Add(new SkillPageLine(T("farm_perks"),heading:true));
   string alt=Key("$KEY_AltPlace","Shift");
   Perk(lines,level,T("farm_row3"),rules.RowOfThreeLevel,F("farm_row_tip",3,alt)+Own(rules.RowPlanting,"RowPlanting"));
   Perk(lines,level,T("farm_row5"),rules.RowOfFiveLevel,F("farm_row_tip",5,alt)+Own(rules.RowPlanting,"RowPlanting"));
   Perk(lines,level,T("farm_replant"),rules.AutoReplantLevel,T("farm_replant_tip")+Own(rules.AutoReplant,"AutoReplant"));
   Perk(lines,level,T("farm_heat"),rules.HeatToleranceLevel,T("farm_heat_tip"));
   Perk(lines,level,T("farm_cold"),rules.ColdToleranceLevel,T("farm_cold_tip"));
   return lines;
  }
  /// <summary>What the game itself does with the level: cultivator stamina (Player.GetBuildStamina saves up to half) and a scythe's reach.</summary>
  void Game(List<SkillPageLine> lines,Player player,float level){
   float factor=FarmingPerks.Factor(level);
   lines.Add(new SkillPageLine(F("farm_stamina",FarmingPage.Percent(0.5f*factor)),title:T("farm_stamina_title"),tooltip:T("farm_stamina_tip")));
   var harvest=HarvestAttack(player,out var tool);
   if(harvest==null){lines.Add(new SkillPageLine(T("farm_scythe_any"),title:T("farm_scythe_title"),tooltip:T("farm_scythe_any_tip")));return;}
   float radius=UnityEngine.Mathf.Lerp(harvest.m_harvestRadius,harvest.m_harvestRadiusMaxLevel,factor);
   lines.Add(new SkillPageLine(F("farm_reach",tool,Number(radius)),title:tool,tooltip:T("farm_reach_tip")));
  }
  /// <summary>The harvesting attack of a tool the player carries (a scythe) and its name; null without one.</summary>
  static Attack? HarvestAttack(Player? player,out string name){
   name="";if(!player)return null;
   foreach(var item in player!.GetInventory().GetAllItems()){
    var attack=item.m_shared.m_attack!=null&&item.m_shared.m_attack.m_harvest?item.m_shared.m_attack:item.m_shared.m_secondaryAttack!=null&&item.m_shared.m_secondaryAttack.m_harvest?item.m_shared.m_secondaryAttack:null;
    if(attack==null)continue;name=Localization.instance!=null?Localization.instance.Localize(item.m_shared.m_name):item.m_shared.m_name;return attack;
   }
   return null;
  }
  void Perk(List<SkillPageLine> lines,float level,string name,int perkLevel,string tip){
   var line=FarmingPage.PerkLine(name,perkLevel,level,T("farm_unlocked"),T("farm_at_level"));
   if(line!=null)lines.Add(new SkillPageLine(line,title:name,tooltip:tip));
  }
  string Own(bool on,string setting)=>on?"":" "+F("farm_switch_off",setting);
  static string Key(string token,string fallback)=>Localization.instance!=null?Localization.instance.Localize(token):fallback;
  string T(string key)=>text?.Text(key)??key;
  string F(string key,params object[] arguments)=>text?.Format(key,arguments)??key;
  static string Number(float value)=>value.ToString("0.#",CultureInfo.InvariantCulture);
  public void Dispose(){
   experience?.Dispose();experience=null;page?.Dispose();page=null;
   foreach(var recipe in recipes){var native=recipe.Recipe;if(native)native.m_enabled=false;ItemManager.Instance.RemoveRecipe(recipe);}
   recipes.Clear();recipesOn=false;
  }
 }
}
