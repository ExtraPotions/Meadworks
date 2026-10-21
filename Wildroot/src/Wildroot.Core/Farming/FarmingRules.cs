using System;
using System.Collections.Generic;
namespace ExtraPotions.Wildroot {
 /// <summary>
 /// The Farming settings as one snapshot (#12, #64, #65, #108). Defaults follow GrindstoneSkills' Farming module
 /// (see THIRD_PARTY_NOTICES/GrindstoneSkills.txt). Everything except the two personal switches is a world rule the
 /// server owns.
 /// </summary>
 public sealed class FarmingRules {
  public const string EnabledId="Farming.Enabled";
  public const string RowPlantingId="Farming.RowPlanting";
  public const string AutoReplantId="Farming.AutoReplant";
  public bool Enabled{get;set;}=true;
  public float GrowthSpeedAt100{get;set;}=40;
  public float GrowSpaceReductionAt100{get;set;}=40;
  public float BonusYieldChanceAt100{get;set;}=50;
  public float SeedReturnChanceAt100{get;set;}=30;
  public float RainGrowthBonus{get;set;}=50;
  public int RowOfThreeLevel{get;set;}=25;
  public int RowOfFiveLevel{get;set;}=50;
  public int AutoReplantLevel{get;set;}=50;
  public int HeatToleranceLevel{get;set;}=75;
  public int ColdToleranceLevel{get;set;}=100;
  public float ExperienceMultiplier{get;set;}=1;
  public bool TierScaling{get;set;}=true;
  public float TierReferenceValue{get;set;}=30;
  public float TierMaximumMultiplier{get;set;}=3;
  public float DiscoveryMultiplier{get;set;}=3;
  public bool RowPlanting{get;set;}=true;
  public bool AutoReplant{get;set;}=true;
 }

 /// <summary>Farming level perks, all pure: the plugin feeds in native values and the planter's or picker's level.</summary>
 public static class FarmingPerks {
  public const float MaxLevel=100f;
  /// <summary>Grow space never shrinks below a fifth of the native radius.</summary>
  public const float MaxSpaceReduction=0.8f;
  /// <summary>Game seconds between two rain credits for one plant, the most one credit covers, and the youngest plant that earns one.</summary>
  public const double RainInterval=60,RainMaxCredit=90,RainMinAge=10;
  /// <summary>Heightmap.Biome flags used by the tolerance perks.</summary>
  public const int Meadows=1,Mountain=4,AshLands=32,DeepNorth=64;
  /// <summary>The native bonus-crop chance the BonusYieldChanceAt100 setting is measured against (the game's 25%).</summary>
  public const float NativeBonusChance=0.25f;

  public static float Factor(float level)=>Clamp01(level/MaxLevel);
  public static float Share(float percentAt100,float level)=>Math.Max(0f,percentAt100)/100f*Factor(level);
  public static bool Reached(int perkLevel,float level)=>perkLevel<=MaxLevel&&level>=perkLevel;

  public static float GrowthSpeed(FarmingRules rules,bool planted,float level)=>rules.Enabled&&planted?1f+Share(rules.GrowthSpeedAt100,level):1f;
  public static float GrowTime(FarmingRules rules,float nativeSeconds,bool planted,float level)=>nativeSeconds/GrowthSpeed(rules,planted,level);

  public static float GrowRadius(FarmingRules rules,float nativeRadius,bool planted,float level)=>
   rules.Enabled&&planted?nativeRadius*(1f-Math.Min(MaxSpaceReduction,Share(rules.GrowSpaceReductionAt100,level))):nativeRadius;
  public static bool HeatTolerant(FarmingRules rules,bool nativeTolerant,bool planted,float level)=>
   nativeTolerant||(rules.Enabled&&planted&&Reached(rules.HeatToleranceLevel,level));
  static bool ColdHardy(FarmingRules rules,int nativeBiomes,bool planted,float level)=>
   rules.Enabled&&planted&&(nativeBiomes&Meadows)!=0&&Reached(rules.ColdToleranceLevel,level);
  public static bool ColdTolerant(FarmingRules rules,bool nativeTolerant,int nativeBiomes,bool planted,float level)=>
   nativeTolerant||ColdHardy(rules,nativeBiomes,planted,level);
  /// <summary>The biomes a plant grows in: a cold-hardy Meadows crop also grows in the Mountain and Deep North.</summary>
  public static int Biomes(FarmingRules rules,int nativeBiomes,bool planted,float level)=>
   ColdHardy(rules,nativeBiomes,planted,level)?nativeBiomes|Mountain|DeepNorth:nativeBiomes;

  public static int RowWidth(FarmingRules rules,float level)=>Reached(rules.RowOfFiveLevel,level)?5:Reached(rules.RowOfThreeLevel,level)?3:1;
  /// <summary>
  /// The planting grid for a placement. Rows of three or five apply only where the player plants one at a time (a 1 x 1
  /// grid, or grid planting off): a larger grid is the player's own choice and stays as it is. Alt-place plants one.
  /// </summary>
  public static (int rows,int columns) PlantingGrid(FarmingRules rules,(int rows,int columns) grid,bool crop,bool alt,float level){
   if(!rules.Enabled||!rules.RowPlanting||!crop||alt||grid.rows!=1||grid.columns!=1)return grid;
   return (1,RowWidth(rules,level));
  }
  public static bool AutoReplants(FarmingRules rules,float level)=>rules.Enabled&&rules.AutoReplant&&Reached(rules.AutoReplantLevel,level);

  /// <summary>The bonus-crop chance at level 100: the native chance scaled so the game's 25% becomes BonusYieldChanceAt100.</summary>
  public static float BonusChance(FarmingRules rules,float nativeChance)=>
   rules.Enabled?Clamp01(nativeChance*Math.Max(0f,rules.BonusYieldChanceAt100)/100f/NativeBonusChance):nativeChance;
  public static float SeedReturnChance(FarmingRules rules,float level)=>rules.Enabled?Share(rules.SeedReturnChanceAt100,level):0f;

  /// <summary>Seconds of growth a planted, healthy plant gains for the time since its last rain check while it rains.</summary>
  public static double RainCredit(FarmingRules rules,double elapsed,bool raining){
   if(!rules.Enabled||!raining||elapsed<RainInterval)return 0;
   return Math.Min(elapsed,RainMaxCredit)*Math.Max(0f,rules.RainGrowthBonus)/100.0;
  }
  static float Clamp01(float value)=>value<0f?0f:value>1f?1f:value;
 }

 /// <summary>Farming experience scaling (#65): multiplier, crop tier from its food value, and the first pick of each crop kind.</summary>
 public static class FarmingXp {
  public const char Separator=',';
  public static float Tier(FarmingRules rules,float value){
   if(!rules.TierScaling)return 1f;
   float reference=Math.Max(1f,rules.TierReferenceValue),maximum=Math.Max(1f,rules.TierMaximumMultiplier);
   return Math.Min(maximum,Math.Max(1f,value/reference));
  }
  public static float Scale(FarmingRules rules,float value,bool firstPick){
   if(!rules.Enabled)return 1f;
   float scale=Math.Max(0f,rules.ExperienceMultiplier)*Tier(rules,value);
   return firstPick?scale*Math.Max(1f,rules.DiscoveryMultiplier):scale;
  }
  public static bool Picked(string? record,string kind)=>!string.IsNullOrEmpty(record)&&!string.IsNullOrEmpty(kind)&&Array.IndexOf(record!.Split(Separator),kind)>=0;
  /// <summary>Records a crop kind as picked. First is true when the character had never picked it.</summary>
  public static (bool first,string record) Discover(string? record,string kind){
   record??="";
   if(string.IsNullOrEmpty(kind)||kind.IndexOf(Separator)>=0||Picked(record,kind))return (false,record);
   return (true,record.Length==0?kind:record+Separator+kind);
  }
 }

 /// <summary>
 /// A crop's value for experience tiers: the best of its items' food value (health + stamina + eitr), else the best
 /// cooked dish an item goes into, else what it is converted into (a windmill, for one), up to four conversions deep.
 /// </summary>
 public sealed class CropValueGraph {
  public const int MaxChain=4;
  readonly Dictionary<string,float> food=new Dictionary<string,float>(StringComparer.Ordinal);
  readonly Dictionary<string,float> dishes=new Dictionary<string,float>(StringComparer.Ordinal);
  readonly Dictionary<string,List<string>> conversions=new Dictionary<string,List<string>>(StringComparer.Ordinal);
  public void Food(string item,float value){if(!string.IsNullOrEmpty(item))food[item]=Math.Max(0f,value);}
  public void Dish(string ingredient,float dishValue){if(string.IsNullOrEmpty(ingredient))return;dishes.TryGetValue(ingredient,out var best);dishes[ingredient]=Math.Max(best,dishValue);}
  public void Convert(string from,string to){if(string.IsNullOrEmpty(from)||string.IsNullOrEmpty(to))return;if(!conversions.TryGetValue(from,out var list))conversions[from]=list=new List<string>();if(!list.Contains(to))list.Add(to);}
  public float Value(IEnumerable<string> items){float value=0f;foreach(var item in items)value=Math.Max(value,ItemValue(item,0));return value;}
  float ItemValue(string item,int depth){
   if(string.IsNullOrEmpty(item)||depth>MaxChain)return 0f;
   if(food.TryGetValue(item,out var own)&&own>0f)return own;
   dishes.TryGetValue(item,out var value);
   if(conversions.TryGetValue(item,out var targets))foreach(var to in targets)value=Math.Max(value,ItemValue(to,depth+1));
   return value;
  }
 }

 /// <summary>One workbench recipe that turns a crop into seeds (#31, SeedCrafting behavior).</summary>
 public sealed class SeedRecipe {
  public string Crop{get;}
  public string Seed{get;}
  public int Amount=>2;
  public int Cost=>1;
  public string Station=>"piece_workbench";
  public int StationLevel=>1;
  public string Name=>"Recipe_Wildroot_"+Seed;
  internal SeedRecipe(string crop,string seed){Crop=crop;Seed=seed;}
 }
 public static class SeedRecipes {
  public const string EnabledId="SeedCrafting.Enabled";
  public static IReadOnlyList<SeedRecipe> All{get;}=new[]{
   new SeedRecipe("Carrot","CarrotSeeds"),new SeedRecipe("Turnip","TurnipSeeds"),new SeedRecipe("Onion","OnionSeeds"),
   new SeedRecipe("Kale","KaleSeeds"),new SeedRecipe("Poteitr","PoteitrSeeds"),
  };
 }
}
