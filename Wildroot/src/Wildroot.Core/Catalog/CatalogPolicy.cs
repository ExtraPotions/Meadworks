using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;
namespace ExtraPotions.Wildroot {
 /// <summary>Typed read access to imported settings by canonical Wildroot id.</summary>
 public interface ICatalogSettings {bool TryGet<T>(string id,out T value);}
 public sealed class CatalogSettings:ICatalogSettings {
  readonly Dictionary<string,object> values;
  public CatalogSettings(IDictionary<string,object>? values=null){this.values=new Dictionary<string,object>(values??new Dictionary<string,object>(),StringComparer.Ordinal);}
  public CatalogSettings With(string id,object value){var copy=new CatalogSettings(values);copy.values[id]=value;return copy;}
  public bool TryGet<T>(string id,out T value){if(values.TryGetValue(id,out var boxed)&&boxed is T typed){value=typed;return true;}value=default!;return false;}
 }
 /// <summary>Reference grouping of a catalog entry; it selects which settings and policies apply.</summary>
 public enum CatalogSection {None,Berries,Mushrooms,Flowers,Debris,Crops,Saplings,Vines,MiscFlora}
 public readonly struct GrowthOverride {
  public double GrowMin{get;} public double GrowMax{get;} public double Radius{get;} public double ScaleMin{get;} public double ScaleMax{get;}
  public GrowthOverride(double growMin,double growMax,double radius,double scaleMin,double scaleMax){GrowMin=growMin;GrowMax=growMax;Radius=radius;ScaleMin=scaleMin;ScaleMax=scaleMax;}
 }
 /// <summary>
 /// Applies PlantEverything's per-entry settings to the captured catalog. The baseline is never mutated: every call
 /// derives a complete new snapshot, so a reload either replaces the whole catalog or leaves it untouched.
 /// </summary>
 public static class CatalogPolicy {
  /// <summary>Minimum sapling growth time; the reference grows a tree at least ten seconds after planting.</summary>
  public const double MinimumSaplingGrowth=10;
  static readonly Dictionary<string,(CatalogSection section,string stem)> Stems=new Dictionary<string,(CatalogSection,string)>(StringComparer.Ordinal){
   {"RaspberryBush",(CatalogSection.Berries,"Raspberry")},{"BlueberryBush",(CatalogSection.Berries,"Blueberry")},{"CloudberryBush",(CatalogSection.Berries,"Cloudberry")},{"LingonberryBush",(CatalogSection.Berries,"Lingonberry")},
   {"Pickable_Mushroom",(CatalogSection.Mushrooms,"Mushroom")},{"Pickable_Mushroom_yellow",(CatalogSection.Mushrooms,"YellowMushroom")},{"Pickable_Mushroom_blue",(CatalogSection.Mushrooms,"BlueMushroom")},{"Pickable_SmokePuff",(CatalogSection.Mushrooms,"Smokepuff")},
   {"Pickable_Thistle",(CatalogSection.Flowers,"Thistle")},{"Pickable_Dandelion",(CatalogSection.Flowers,"Dandelion")},{"Pickable_Fiddlehead",(CatalogSection.Flowers,"Fiddlehead")},
   {"Pickable_Branch",(CatalogSection.Debris,"PickableBranch")},{"Pickable_Stone",(CatalogSection.Debris,"PickableStone")},{"Pickable_Flint",(CatalogSection.Debris,"PickableFlint")},
   {"sapling_barley",(CatalogSection.Crops,"Barley")},{"sapling_carrot",(CatalogSection.Crops,"Carrot")},{"sapling_flax",(CatalogSection.Crops,"Flax")},{"sapling_onion",(CatalogSection.Crops,"Onion")},
   {"sapling_seedcarrot",(CatalogSection.Crops,"SeedCarrot")},{"sapling_seedonion",(CatalogSection.Crops,"SeedOnion")},{"sapling_seedturnip",(CatalogSection.Crops,"SeedTurnip")},{"sapling_turnip",(CatalogSection.Crops,"Turnip")},
   {"sapling_magecap",(CatalogSection.Crops,"Magecap")},{"sapling_jotunpuffs",(CatalogSection.Crops,"JotunPuffs")},{"sapling_oat",(CatalogSection.Crops,"Oat")},{"sapling_poteitr",(CatalogSection.Crops,"Poteitr")},
   {"sapling_Kale",(CatalogSection.Crops,"Kale")},{"sapling_seedkale",(CatalogSection.Crops,"SeedKale")},
   {"Beech_Sapling",(CatalogSection.Saplings,"Beech")},{"Birch_Sapling",(CatalogSection.Saplings,"Birch")},{"Autumn_Birch_Sapling",(CatalogSection.Saplings,"AutumnBirch")},{"Ancient_Sapling",(CatalogSection.Saplings,"Ancient")},
   {"Ygga_Sapling",(CatalogSection.Saplings,"Ygga")},{"Ashwood_Sapling",(CatalogSection.Saplings,"Ashwood")},{"Oak_Sapling",(CatalogSection.Saplings,"Oak")},{"FirTree_Sapling",(CatalogSection.Saplings,"Fir")},{"FirTree_big_Sapling",(CatalogSection.Saplings,"Timberwood")},{"PineTree_Sapling",(CatalogSection.Saplings,"Pine")},
  };
  /// <summary>Section of an entry: the named stem table, otherwise its kind (vines, unnamed saplings, decorative flora).</summary>
  public static CatalogSection Section(PlantDefinition definition){
   if(definition==null)throw new ArgumentNullException(nameof(definition));
   if(Stems.TryGetValue(definition.Id,out var known))return known.section;
   return definition.Kind switch{PlantKind.Vine=>CatalogSection.Vines,PlantKind.Decorative=>CatalogSection.MiscFlora,PlantKind.Sapling=>CatalogSection.Saplings,PlantKind.Crop=>CatalogSection.Crops,_=>CatalogSection.None};
  }
  /// <summary>Reference setting stem of an entry (for example "Raspberry"), or empty when the entry has none.</summary>
  public static string Stem(PlantDefinition definition)=>definition!=null&&Stems.TryGetValue(definition.Id,out var known)?known.stem:"";
  static bool Flag(ICatalogSettings s,string id,bool fallback)=>s.TryGet<bool>(id,out var v)?v:fallback;
  static double Number(ICatalogSettings s,string id,double fallback,IList<string>? warnings,double min=0){
   double value;if(s.TryGet<float>(id,out var f))value=f;else if(s.TryGet<int>(id,out var i))value=i;else if(s.TryGet<double>(id,out var d))value=d;else return fallback;
   if(double.IsNaN(value)||double.IsInfinity(value)||value<min){warnings?.Add(id+" out of range; kept "+fallback.ToString(CultureInfo.InvariantCulture));return fallback;}
   return value;
  }
  /// <summary>Names listed in DisabledResourceNames: comma separated, trimmed, case-sensitive.</summary>
  public static ISet<string> DisabledNames(ICatalogSettings s){
   var result=new HashSet<string>(StringComparer.Ordinal);if(!s.TryGet<string>("Catalog.DisabledResourceNames",out var text)||string.IsNullOrWhiteSpace(text))return result;
   foreach(var part in text.Split(','))if(part.Trim().Length>0)result.Add(part.Trim());return result;
  }
  public static bool CropOverrides(ICatalogSettings s)=>Flag(s,"Growth.Crops.EnableCropOverrides",false);
  public static bool VineOverrides(ICatalogSettings s)=>Flag(s,"Growth.Vines.EnableVineOverrides",false);
  /// <summary>Vine wall-attachment distance with vine overrides enabled, otherwise null (keep the native value).</summary>
  public static double? VineAttachDistance(ICatalogSettings s)=>VineOverrides(s)?Number(s,"Growth.Vines.VineAttachDistance",1.8,null):(double?)null;
  /// <summary>Crop growth values for third-party crops: only with crop overrides and OverrideModdedCrops enabled.</summary>
  public static GrowthOverride? ModdedCrop(ICatalogSettings s){
   if(!CropOverrides(s)||!Flag(s,"Growth.Crops.OverrideModdedCrops",true))return null;
   return CropGrowth(s,new GrowthOverride(4000,5000,0.5,0.9,1.1),null);
  }
  /// <summary>Sapling growth values for third-party saplings: only with OverrideModdedSaplings enabled.</summary>
  public static GrowthOverride? ModdedSapling(ICatalogSettings s){
   if(!Flag(s,"Growth.Saplings.OverrideModdedSaplings",false))return null;
   double time=Math.Max(MinimumSaplingGrowth,Number(s,"Growth.Saplings.ModdedSaplingGrowthTime",3000,null));
   double min=Number(s,"Growth.Saplings.ModdedSaplingMinScale",0.5,null,1e-6),max=Math.Max(min,Number(s,"Growth.Saplings.ModdedSaplingMaxScale",2,null,1e-6));
   return new GrowthOverride(time,time,Number(s,"Growth.Saplings.ModdedSaplingGrowRadius",2,null),min,max);
  }
  static GrowthOverride CropGrowth(ICatalogSettings s,GrowthOverride current,IList<string>? warnings){
   double growMin=Number(s,"Growth.Crops.CropGrowTimeMin",current.GrowMin,warnings),growMax=Math.Max(growMin,Number(s,"Growth.Crops.CropGrowTimeMax",current.GrowMax,warnings));
   double scaleMin=Number(s,"Growth.Crops.CropMinScale",current.ScaleMin,warnings,1e-6),scaleMax=Math.Max(scaleMin,Number(s,"Growth.Crops.CropMaxScale",current.ScaleMax,warnings,1e-6));
   return new GrowthOverride(growMin,growMax,Number(s,"Growth.Crops.CropGrowRadius",current.Radius,warnings),scaleMin,scaleMax);
  }
  /// <summary>Effective catalog for the current settings. Entries are disabled, never removed, so their identities stay stable.</summary>
  public static CatalogSnapshot Apply(CatalogSnapshot baseline,ICatalogSettings settings,IList<string>? warnings=null){
   if(baseline==null||settings==null)throw new ArgumentNullException(nameof(baseline));
   var disabled=DisabledNames(settings);bool misc=Flag(settings,"Catalog.EnableMiscFlora",true),debris=Flag(settings,"Catalog.EnableDebris",true),crops=CropOverrides(settings),vines=VineOverrides(settings);
   return new CatalogSnapshot(baseline.Entries.Select(d=>Effective(d,settings,disabled,misc,debris,crops,vines,warnings)).ToList());
  }
  static PlantDefinition Effective(PlantDefinition d,ICatalogSettings s,ISet<string> disabled,bool misc,bool debris,bool crops,bool vines,IList<string>? warnings){
   var section=Section(d);string stem=Stem(d);var costs=d.Costs;int yield=d.Yield;double respawn=d.RespawnSeconds,growMin=d.GrowMin,growMax=d.GrowMax,radius=d.Radius,scaleMin=d.ScaleMin,scaleMax=d.ScaleMax;bool enabled=d.Enabled;
   switch(section){
    case CatalogSection.Berries:case CatalogSection.Mushrooms:case CatalogSection.Flowers:case CatalogSection.Debris:{
     string prefix=section==CatalogSection.Debris?"Catalog.Vanilla.Debris.":"Catalog.Vanilla."+section+".";
     costs=Cost(costs,Number(s,prefix+stem+"Cost",FirstAmount(costs),warnings),ref enabled);
     yield=(int)Number(s,prefix+stem+"Return",yield,warnings);respawn=Number(s,prefix+stem+"RespawnTime",respawn/60,warnings)*60;
     if(section==CatalogSection.Debris&&!debris)enabled=false;break;}
    case CatalogSection.Crops:if(crops&&stem.Length>0){
     costs=Cost(costs,Number(s,"Catalog.Vanilla.Crops."+stem+"Cost",FirstAmount(costs),warnings),ref enabled);yield=(int)Number(s,"Catalog.Vanilla.Crops."+stem+"Return",yield,warnings);
     var g=CropGrowth(s,new GrowthOverride(growMin,growMax,radius,scaleMin,scaleMax),warnings);growMin=g.GrowMin;growMax=g.GrowMax;radius=g.Radius;scaleMin=g.ScaleMin;scaleMax=g.ScaleMax;}break;
    case CatalogSection.Saplings:if(stem.Length>0){
     growMin=growMax=Math.Max(MinimumSaplingGrowth,Number(s,"Growth.Saplings."+stem+"GrowthTime",growMin,warnings));radius=Number(s,"Growth.Saplings."+stem+"GrowRadius",radius,warnings);
     scaleMin=Number(s,"Growth.Saplings."+stem+"MinScale",scaleMin,warnings,1e-6);scaleMax=Math.Max(scaleMin,Number(s,"Growth.Saplings."+stem+"MaxScale",scaleMax,warnings,1e-6));}break;
    case CatalogSection.Vines:if(vines){
     growMin=growMax=Number(s,"Growth.Vines.VineGrowthTime",growMin,warnings);radius=Number(s,"Growth.Vines.VineGrowRadius",radius,warnings);
     respawn=Number(s,"Growth.Vines.VineBerryRespawnTime",respawn/60,warnings)*60;yield=(int)Number(s,"Growth.Vines.VineBerryReturn",yield,warnings);}break;
    case CatalogSection.MiscFlora:if(!misc)enabled=false;break;
   }
   if(disabled.Contains(d.Prefab))enabled=false;
   return new PlantDefinition(d.Id,d.Prefab,d.GrownPrefab,d.Kind,costs,d.HarvestItem,yield,growMin,growMax,respawn,radius,scaleMin,scaleMax,enabled,d.Free);
  }
  /// <summary>
  /// Whether a growing entry keeps its native biome mask. Vanilla crops and saplings follow EnforceBiomesVanilla
  /// (default on); saplings and vines the game does not ship (cloned prefabs) follow EnforceBiomes (default off).
  /// Pickables, decorative flora and debris have no growth biome.
  /// </summary>
  public static bool EnforcesBiome(PlantDefinition definition,bool nativePrefab,ICatalogSettings s){
   if(definition==null||s==null)throw new ArgumentNullException(nameof(definition));
   if(definition.Kind!=PlantKind.Crop&&definition.Kind!=PlantKind.Sapling&&definition.Kind!=PlantKind.Vine)return false;
   return nativePrefab?Flag(s,"Restrictions.EnforceBiomesVanilla",true):Flag(s,"Restrictions.EnforceBiomes",false);
  }
  /// <summary>Server-owned switch (default on): the ten crops PlantEverything 1.21.4 lets grow in the Ashlands do so.</summary>
  public const string AshlandsCropsId="Restrictions.AshlandsCrops";
  public static bool AshlandsCrops(ICatalogSettings s)=>Flag(s??throw new ArgumentNullException(nameof(s)),AshlandsCropsId,true);
  /// <summary>The pinned reference biome mask of an entry under the current settings, or null where the game's own mask applies.</summary>
  public static int? ReferenceBiome(string prefab,ICatalogSettings s)=>ReferenceTraits.Biome(prefab,AshlandsCrops(s));
  /// <summary>
  /// Adds custom extra-resource definitions as decorative entries. An extra resource never replaces a catalog entry
  /// (reported as a warning); DisabledResourceNames applies to extra resources as well.
  /// </summary>
  public static CatalogSnapshot WithExtras(CatalogSnapshot effective,IReadOnlyList<ExtraResourceDefinition> extras,ICatalogSettings s,IList<string>? warnings=null){
   if(effective==null||extras==null||s==null)throw new ArgumentNullException(nameof(effective));
   var disabled=DisabledNames(s);var result=effective.Entries.ToList();var known=new HashSet<string>(result.Select(e=>e.Prefab),StringComparer.Ordinal);
   foreach(var e in extras){
    if(e==null)continue;if(!known.Add(e.Prefab)){warnings?.Add("Extra resource "+e.Prefab+" is already a catalog entry; skipped");continue;}
    result.Add(new PlantDefinition(e.Prefab,e.Prefab,"",PlantKind.Decorative,new[]{new ResourceCost(e.Material,e.Cost)},"",0,0,0,0,1,1,1,!disabled.Contains(e.Prefab),false));
   }
   return new CatalogSnapshot(result);
  }
  static readonly Point3[] VineSnaps={new Point3(1,-0.5,0),new Point3(-1,-0.5,0),new Point3(1,-2,0),new Point3(-1,-2,0)};
  /// <summary>
  /// Local snap points for the decorative "vines" piece with SnappableVines on (default). Positions observed on
  /// PlantEverything 1.21.3 in game: two columns one meter either side, at 0.5 and 2 meters below the pivot.
  /// </summary>
  public static IReadOnlyList<Point3> VineSnapPoints(PlantDefinition definition,ICatalogSettings s){
   if(definition==null||s==null)throw new ArgumentNullException(nameof(definition));
   return definition.Id=="vines"&&Flag(s,"Catalog.SnappableVines",true)?VineSnaps:Array.Empty<Point3>();
  }
  /// <summary>Pickables that keep a visible picked spawner (observed on PlantEverything 1.21.3: mushrooms, smoke puff, thistle, dandelion, fiddlehead).</summary>
  public static readonly IReadOnlyCollection<string> PickedSpawners=new HashSet<string>(StringComparer.Ordinal){"Pickable_Mushroom","Pickable_Mushroom_yellow","Pickable_Mushroom_blue","Pickable_SmokePuff","Pickable_Thistle","Pickable_Dandelion","Pickable_Fiddlehead"};
  public static bool ShowsPickedSpawner(string prefab,ICatalogSettings s)=>s!=null&&prefab!=null&&PickedSpawners.Contains(prefab)&&Flag(s,"Catalog.ShowPickableSpawners",true);
  static double FirstAmount(IReadOnlyList<ResourceCost> costs)=>costs.Count>0?costs[0].Amount:0;
  /// <summary>The first material takes the configured amount; zero disables the entry and is never free planting.</summary>
  static IReadOnlyList<ResourceCost> Cost(IReadOnlyList<ResourceCost> costs,double amount,ref bool enabled){
   if(costs.Count==0)return costs;int value=(int)Math.Min(int.MaxValue,Math.Round(amount));
   if(value<=0){enabled=false;return costs;}
   var result=costs.ToList();result[0]=new ResourceCost(costs[0].ItemId,value);return result;
  }
 }
 /// <summary>Per-piece reference policies: enemy targeting, removal, recovery, shielding, ground and cultivation rules.</summary>
 public readonly struct PiecePolicy {
  public bool Targetable{get;} public bool Removable{get;} public bool RecoverResources{get;} public bool ToleratesEnvironment{get;} public bool GroundOnly{get;} public bool RequiresCultivation{get;} public bool RequiresSunlight{get;} public bool RequiresGrowthSpace{get;}
  public PiecePolicy(bool targetable,bool removable,bool recover,bool tolerates,bool groundOnly,bool cultivation,bool sunlight,bool growthSpace){Targetable=targetable;Removable=removable;RecoverResources=recover;ToleratesEnvironment=tolerates;GroundOnly=groundOnly;RequiresCultivation=cultivation;RequiresSunlight=sunlight;RequiresGrowthSpace=growthSpace;}
 }
 public static class PiecePolicies {
  static bool Flag(ICatalogSettings s,string id,bool fallback)=>s.TryGet<bool>(id,out var v)?v:fallback;
  /// <summary>
  /// Crops follow the [Crops] switches only with crop overrides enabled; every other added piece follows
  /// EnemiesTargetPieces. Decorative flora is removable only with CanRemoveFlora. Recovery applies to berries,
  /// mushrooms and flowers. PlaceAnywhere frees saplings, bushes and small trees from the ground; mushrooms,
  /// flowers, debris and the Ashlands fern keep their fixed ground rule. RequireCultivation applies to pickables.
  /// </summary>
  public static PiecePolicy For(PlantDefinition definition,ICatalogSettings s){
   if(definition==null||s==null)throw new ArgumentNullException(nameof(definition));var section=CatalogPolicy.Section(definition);bool crops=CatalogPolicy.CropOverrides(s);
   bool crop=section==CatalogSection.Crops,pickable=definition.Kind==PlantKind.Pickable;
   bool targetable=crop?!crops||Flag(s,"Growth.Crops.EnemiesTargetCrops",true):Flag(s,"Restrictions.EnemiesTargetPieces",true);
   bool removable=section!=CatalogSection.MiscFlora||Flag(s,"Restrictions.CanRemoveFlora",false);
   bool recover=(section==CatalogSection.Berries||section==CatalogSection.Mushrooms||section==CatalogSection.Flowers)&&Flag(s,"Restrictions.RecoverResources",false);
   bool tolerates=!Flag(s,"Restrictions.PlantsRequireShielding",true);
   // PlantEverything's PlaceAnywhere also frees eligible plants from roof and growth-space checks.
   bool placeAnywhere=Flag(s,"Restrictions.PlaceAnywhere",false)&&section!=CatalogSection.Mushrooms&&section!=CatalogSection.Flowers&&!crop;
   // Frozen StaticContent fixes these four pieces to the ground even with PlaceAnywhere enabled.
   bool fixedGround=definition.Prefab=="FernAshlands"||definition.Prefab=="Pickable_Branch"||definition.Prefab=="Pickable_Stone"||definition.Prefab=="Pickable_Flint";
   bool groundOnly=!placeAnywhere||fixedGround;
   bool cultivation=crop?!crops||Flag(s,"Growth.Crops.CropsRequireCultivation",true):pickable&&Flag(s,"Restrictions.RequireCultivation",false);
   bool sunlight=!placeAnywhere&&(!crop||!crops||Flag(s,"Growth.Crops.CropsRequireSunlight",true)),space=!placeAnywhere&&(!crop||!crops||Flag(s,"Growth.Crops.CropsRequireGrowthSpace",true));
   return new PiecePolicy(targetable,removable,recover,tolerates,groundOnly,cultivation,sunlight,space);
  }
  /// <summary>Pickables spawn already picked when ResourcesSpawnEmpty is on (berries, mushrooms, flowers and debris).</summary>
  public static bool SpawnsEmpty(PlantDefinition definition,ICatalogSettings s){var section=CatalogPolicy.Section(definition);return (section==CatalogSection.Berries||section==CatalogSection.Mushrooms||section==CatalogSection.Flowers||section==CatalogSection.Debris)&&Flag(s,"Restrictions.ResourcesSpawnEmpty",false);}
 }
 public readonly struct DropEntry {
  public string Item{get;} public int StackMin{get;} public int StackMax{get;}
  public DropEntry(string item,int stackMin,int stackMax){Item=item;StackMin=stackMin;StackMax=stackMax;}
 }
 /// <summary>A tree's destruction drop table: rolls between Min and Max, Chance per roll, optional one of each item.</summary>
 public sealed class DropTableValues {
  public int Min{get;} public int Max{get;} public double Chance{get;} public bool OneOfEach{get;} public IReadOnlyList<DropEntry> Entries{get;}
  public DropTableValues(int min,int max,double chance,bool oneOfEach,IReadOnlyList<DropEntry> entries){Min=min;Max=max;Chance=chance;OneOfEach=oneOfEach;Entries=new List<DropEntry>(entries).AsReadOnly();}
 }
 public static class TreeDropPolicy {
  /// <summary>Planting materials dropped by trees (seeds, cones, acorns and sap) use the seed stack range.</summary>
  public static readonly IReadOnlyCollection<string> SeedItems=new HashSet<string>(StringComparer.Ordinal){"BeechSeeds","BirchSeeds","Acorn","PineCone","FirCone","FirConeFrost","AncientSeed","Sap"};
  /// <summary>With seed overrides off the native table is returned unchanged; otherwise every reference value applies.</summary>
  public static DropTableValues Apply(DropTableValues original,ICatalogSettings s){
   if(original==null||s==null)throw new ArgumentNullException(nameof(original));if(!(s.TryGet<bool>("Drops.EnableSeedOverrides",out var on)&&on))return original;
   int Int(string id,int fallback)=>s.TryGet<int>(id,out var v)?v:fallback;
   int treeMin=Math.Max(0,Int("Drops.treeDropMin",1)),treeMax=Math.Max(treeMin,Int("Drops.treeDropMax",3)),seedMin=Math.Max(0,Int("Drops.seedDropMin",1)),seedMax=Math.Max(seedMin,Int("Drops.seedDropMax",2));
   double chance=s.TryGet<float>("Drops.dropChance",out var c)?c:0.5;if(double.IsNaN(chance))chance=0.5;chance=Math.Min(1,Math.Max(0,chance));
   var entries=original.Entries.Select(e=>SeedItems.Contains(e.Item)?new DropEntry(e.Item,seedMin,seedMax):e).ToList();
   return new DropTableValues(treeMin,treeMax,chance,s.TryGet<bool>("Drops.oneOfEach",out var one)&&one,entries);
  }
 }
 public static class HoverTimer {
  /// <summary>Hover growth text: a whole percentage, or the remaining time as h/m/s; empty once ready.</summary>
  public static string Format(double remainingSeconds,double fraction,bool asPercentage){
   if(double.IsNaN(remainingSeconds)||double.IsNaN(fraction))throw new ArgumentException("Invalid growth timer");
   if(remainingSeconds<=0)return "";
   if(asPercentage)return Math.Floor(Math.Min(1,Math.Max(0,fraction))*100).ToString(CultureInfo.InvariantCulture)+"%";
   long total=(long)Math.Ceiling(remainingSeconds),h=total/3600,m=total%3600/60,sec=total%60;
   return h>0?h+"h "+m+"m "+sec+"s":m>0?m+"m "+sec+"s":sec+"s";
  }
 }
}
