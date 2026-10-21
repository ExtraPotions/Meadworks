using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using Newtonsoft.Json.Linq;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>
 /// Wildroot's settings on the Crucible config core (C01): every setting carries the scope from
 /// <see cref="WildrootSettings.ScopeOf"/>, server-owned settings are read-only for non-administrators in configuration
 /// managers (C03), and the one cheat is bound through <c>BindCheat</c> with its safe value and Mischief category.
 /// </summary>
 public sealed class WildrootConfig {
  readonly Dictionary<string,ConfigEntryBase> entries=new Dictionary<string,ConfigEntryBase>(StringComparer.Ordinal);
  readonly Dictionary<string,ConfigScope> scopes=new Dictionary<string,ConfigScope>(StringComparer.Ordinal);
  readonly Dictionary<string,string> aliases=new Dictionary<string,string>(StringComparer.Ordinal);
  readonly ConfigFile file;
  public ConfigFile File=>file;
  public ConfigEntry<bool> Enabled{get;}
  /// <summary>The cheat as configured; act on <see cref="UnrestrictedPlacement"/> or <see cref="UnrestrictedGrowth"/>.</summary>
  public ConfigEntry<bool> Unrestricted{get;}
  public ConfigEntry<string> Language{get;}
  internal ConfigEntry<string> ExtraResourcesShared{get;}
  internal ConfigEntry<string> ServerFingerprint{get;}
  internal ConfigEntry<bool> ImportReferenceSettings{get;}
  internal ConfigEntry<string> ImportConflictChoices{get;}
  internal ConfigEntry<string> ImportBindingOverrides{get;}
  /// <summary>Set when a reference mod is installed (C09 step-aside); Wildroot then stays off.</summary>
  internal bool SteppedAside{get;set;}
  /// <summary>Raised with the canonical "Section.Key" id whenever a bound setting changes.</summary>
  public event Action<string>? Changed;
  /// <summary>Counts configuration changes so per-frame readers can cache what they derive from it.</summary>
  internal int Revision{get;private set;}
  public WildrootConfig(ConfigFile file,string assetDirectory){
   this.file=file;bool upgradeShortcuts=false;
   MeadworksConfig.EnsureSchema(file,WildrootSettings.SchemaVersion,new[]{new MigrationStep(0,1,()=>{}),new MigrationStep(1,2,()=>upgradeShortcuts=true)});
   file.SettingChanged+=(_,e)=>{
    // A same-frame master-switch or cheat amendment must invalidate the cached authority decision immediately.
    decidedFrame=-1;placementFrame=-2;Revision++;
    Changed?.Invoke(e.ChangedSetting.Definition.Section+"."+e.ChangedSetting.Definition.Key);
   };
   var mapping=JObject.Parse(System.IO.File.ReadAllText(Path.Combine(assetDirectory,"config","import-mapping.json")));
   if(mapping.Value<int>("schema")!=1)throw new NotSupportedException("Unsupported import mapping schema");
   foreach(var token in (JArray)mapping["rules"]!){var obj=(JObject)token;if(obj.Value<bool>("metadataOnly"))continue;string id=obj.Value<string>("target")!;if(WildrootSettings.Superseded.ContainsKey(id))continue;aliases.Add(obj.Value<string>("source")!,id);if(entries.ContainsKey(id))continue;
    string type=obj.Value<string>("type")!,value=obj.Value<string>("default")??"",constraints=obj.Value<string>("constraints")??"";
    var last=id.LastIndexOf('.');string section=id.Substring(0,last),key=id.Substring(last+1);
    switch(type){
     case "Boolean":Add(id,Bind(section,key,bool.Parse(value)));break;
     case "Int32":Add(id,Bind(section,key,int.Parse(value,CultureInfo.InvariantCulture),id is "Grid.Rows" or "Grid.Columns"?new AcceptableValueRange<int>(1,GridLayout.MaxDimension):null));break;
     case "Single":Add(id,Bind(section,key,float.Parse(value,CultureInfo.InvariantCulture)));break;
     case "KeyboardShortcut":Add(id,Bind(section,key,WildrootSettings.UnboundByDefault.Contains(id)||WildrootSettings.EarlierShortcutDefaults.ContainsKey(id)?KeyboardShortcut.Empty:KeyboardShortcut.Deserialize(value)));break;
     case "KeyCode":Add(id,Bind(section,key,(KeyCode)Enum.Parse(typeof(KeyCode),value)));break;
     case "Color":if(!ColorUtility.TryParseHtmlString("#"+value,out var color))throw new ArgumentException("Invalid color default");Add(id,Bind(section,key,color));break;
     default:Add(id,Bind(section,key,value,type=="String"?null:new AcceptableValueList<string>(constraints.Split(new[]{", "},StringSplitOptions.None))));break;
    }
   }
   Enabled=(ConfigEntry<bool>)entries[WildrootSettings.Enabled];
   Language=Bind("Presentation","Language","Auto",new AcceptableValueList<string>("Auto","English","Korean","Spanish","Chinese","Russian","French","German"),"Language of Wildroot's own text. Auto follows the game's language; a named language overrides it for Wildroot only. Your own display choice.");Add("Presentation.Language",Language);
   Unrestricted=MeadworksConfig.BindCheat(file,"Gardening","Unrestricted",WildrootSettings.UnrestrictedSafe,"Cheat: plant anywhere and let plants grow without space, sunlight, biome or temperature limits. Off by default; in multiplayer the server's value applies and its cheat policy decides who may place this way.",WildrootSettings.UnrestrictedCategory);
   Add(WildrootSettings.Unrestricted,Unrestricted);
   Add("Presentation.ExportEnglishReference",Bind("Presentation","ExportEnglishReference",false,null,"Set to true to write Wildroot's English text to a reference file for translators. Wildroot writes it once and sets this back to false."));
   Add("Grid.Enabled",Bind("Grid","Enabled",true,null,"Plant in a grid of Rows by Columns cells with each cultivator use. Off: plant one at a time. Grid: ToggleKey switches this in game."));
   Add("Grid.ToggleKey",Bind("Grid","ToggleKey",new KeyboardShortcut(KeyCode.None),null,"Key that turns grid planting (Grid: Enabled) on or off while you play. Unbound by default."));
   Add("Farm.ToggleKey",Bind("Farm","ToggleKey",KeyboardShortcut.Empty,null,"No longer used: farming mode was removed and this key does nothing. Grid planting and bulk harvesting still in progress stop when you open the pause menu or another window."));
   Add("Harvest.ReplantTarget",Bind("Harvest","ReplantTarget","Original",new AcceptableValueList<string>("Original","Selected"),"What replanting after a bulk harvest plants. Original: the same crop that was picked. Selected: the crop currently selected on your cultivator; any other selection keeps the original crop."));
   ExtraResourcesShared=Bind("Catalog","ExtraResourcesShared","",null,"Managed by Wildroot: the server's extra-resource definitions sent to clients. Edit extra-resources.cfg instead.");Add(WildrootSettings.ExtraResourcesShared,ExtraResourcesShared);
   ServerFingerprint=Bind("Catalog","ServerFingerprint","",null,"Managed by Wildroot: the server's catalog fingerprint; clients compare their own catalog against it.");Add(WildrootSettings.ServerFingerprint,ServerFingerprint);
   Add(CatalogPolicy.AshlandsCropsId,Bind("Restrictions","AshlandsCrops",true,null,"On: carrots, onions, turnips, oats, poteitr and kale, and their seed crops, grow in the Ashlands as they do in the game and PlantEverything 1.21.4. There they still need a shield unless the planter's Farming level reaches HeatToleranceLevel. Off: these crops do not grow in the Ashlands. Barley, flax, magecap, Jotun puffs and the Ashlands' own plants are not affected. Applies while EnforceBiomesVanilla is on."));
   ImportReferenceSettings=Bind("Import","ImportReferenceSettings",false,null,"Set to true to import PlantEasily and PlantEverything settings once; Wildroot backs up this file first, writes a report to Wildroot/import-report.txt and sets this back to false.");Add("Import.ImportReferenceSettings",ImportReferenceSettings);
   ImportConflictChoices=Bind("Import","ConflictChoices","",null,"Conflict choices for the import, as Target=SourceSetting pairs separated by semicolons.");Add("Import.ConflictChoices",ImportConflictChoices);
   ImportBindingOverrides=Bind("Import","BindingOverrides","",null,"Replacement shortcuts for import collisions, as Target=Key pairs separated by semicolons.");Add("Import.BindingOverrides",ImportBindingOverrides);
   BindFarming();
   if(upgradeShortcuts)UpgradeShortcuts();
   foreach(var pair in entries)if(scopes[pair.Key]!=ConfigScope.Local)MeadworksBlockedOptions.Track(WildrootSettings.ProductId,pair.Value,scopes[pair.Key]);
  }
  /// <summary>Farming skill settings (#12, #64, #65, #108) and seed recipes (#31). Server-owned except the two personal switches.</summary>
  void BindFarming(){
   var percent=new AcceptableValueRange<float>(0f,100f);var levels=new AcceptableValueRange<int>(0,101);
   Add(FarmingRules.EnabledId,Bind("Farming","Enabled",true,null,"Farming skill perks and experience scaling. Off leaves the game's own Farming skill unchanged."));
   Add("Farming.GrowthSpeedAt100",Bind("Farming","GrowthSpeedAt100",40f,new AcceptableValueRange<float>(0f,500f),"Percent faster growth at level 100 for everything a player plants, from the planter's level."));
   Add("Farming.GrowSpaceReductionAt100",Bind("Farming","GrowSpaceReductionAt100",40f,new AcceptableValueRange<float>(0f,80f),"Percent less room a planted crop needs around it at level 100, from the planter's level."));
   Add("Farming.BonusYieldChanceAt100",Bind("Farming","BonusYieldChanceAt100",50f,percent,"Bonus crop chance in percent at level 100 when picking, in place of the game's 25%."));
   Add("Farming.SeedReturnChanceAt100",Bind("Farming","SeedReturnChanceAt100",30f,percent,"Chance in percent at level 100 that picking a crop also gives back its seed."));
   Add("Farming.RainGrowthBonus",Bind("Farming","RainGrowthBonus",50f,new AcceptableValueRange<float>(0f,500f),"Percent faster growth while it rains, for everything a player planted."));
   Add("Farming.RowOfThreeLevel",Bind("Farming","RowOfThreeLevel",25,levels,"From this level, planting one crop at a time (a 1 x 1 grid) plants a row of three. 101 turns it off."));
   Add("Farming.RowOfFiveLevel",Bind("Farming","RowOfFiveLevel",50,levels,"From this level, a row of five. 101 turns it off."));
   Add("Farming.AutoReplantLevel",Bind("Farming","AutoReplantLevel",50,levels,"From this level, picking a crop plants it again in the same spot, paid from your inventory. 101 turns it off."));
   Add("Farming.HeatToleranceLevel",Bind("Farming","HeatToleranceLevel",75,levels,"From this planter level, crops grow in the Ashlands without a shield. 101 turns it off."));
   Add("Farming.ColdToleranceLevel",Bind("Farming","ColdToleranceLevel",100,levels,"From this planter level, crops that grow in the Meadows also grow in the Mountain and Deep North. 101 turns it off."));
   Add("Farming.ExperienceMultiplier",Bind("Farming","ExperienceMultiplier",1f,new AcceptableValueRange<float>(0f,10f),"Multiplies Farming experience from planting and picking."));
   Add("Farming.TierScaling",Bind("Farming","TierScaling",true,null,"Richer crops teach more: experience is multiplied by the crop's value over TierReferenceValue, never below 1."));
   Add("Farming.TierReferenceValue",Bind("Farming","TierReferenceValue",30f,new AcceptableValueRange<float>(1f,500f),"The crop value (health + stamina + eitr, or what it is milled or cooked into) that earns the game's own experience."));
   Add("Farming.TierMaximumMultiplier",Bind("Farming","TierMaximumMultiplier",3f,new AcceptableValueRange<float>(1f,10f),"The most TierScaling multiplies experience by."));
   Add("Farming.DiscoveryMultiplier",Bind("Farming","DiscoveryMultiplier",3f,new AcceptableValueRange<float>(1f,10f),"Experience multiplier the first time a character picks each kind of crop."));
   Add(FarmingRules.RowPlantingId,Bind("Farming","RowPlanting",true,null,"Your own switch for the row-of-three and row-of-five perks."));
   Add(FarmingRules.AutoReplantId,Bind("Farming","AutoReplant",true,null,"Your own switch for the auto-replant perk."));
   Add(SeedRecipes.EnabledId,Bind("SeedCrafting","Enabled",true,null,"Workbench recipes that turn one carrot, turnip, onion, kale or poteitr into two of its seeds."));
  }
  FarmingRules? farming;int farmingRevision=-1;
  /// <summary>The Farming settings as one snapshot, rebuilt only when a setting changed.</summary>
  internal FarmingRules Farming{get{
   if(farming!=null&&farmingRevision==Revision)return farming;
   farmingRevision=Revision;
   return farming=new FarmingRules{
    Enabled=Get(FarmingRules.EnabledId,true),GrowthSpeedAt100=Get("Farming.GrowthSpeedAt100",40f),GrowSpaceReductionAt100=Get("Farming.GrowSpaceReductionAt100",40f),
    BonusYieldChanceAt100=Get("Farming.BonusYieldChanceAt100",50f),SeedReturnChanceAt100=Get("Farming.SeedReturnChanceAt100",30f),RainGrowthBonus=Get("Farming.RainGrowthBonus",50f),
    RowOfThreeLevel=Get("Farming.RowOfThreeLevel",25),RowOfFiveLevel=Get("Farming.RowOfFiveLevel",50),AutoReplantLevel=Get("Farming.AutoReplantLevel",50),
    HeatToleranceLevel=Get("Farming.HeatToleranceLevel",75),ColdToleranceLevel=Get("Farming.ColdToleranceLevel",100),
    ExperienceMultiplier=Get("Farming.ExperienceMultiplier",1f),TierScaling=Get("Farming.TierScaling",true),TierReferenceValue=Get("Farming.TierReferenceValue",30f),
    TierMaximumMultiplier=Get("Farming.TierMaximumMultiplier",3f),DiscoveryMultiplier=Get("Farming.DiscoveryMultiplier",3f),
    RowPlanting=Get(FarmingRules.RowPlantingId,true),AutoReplant=Get(FarmingRules.AutoReplantId,true),
   };
  }}
  /// <summary>Schema 1 to 2, once per file: a saved shortcut still at its earlier default becomes unbound; a changed one is kept.</summary>
  void UpgradeShortcuts(){
   var upgraded=new List<string>();
   foreach(var id in WildrootSettings.EarlierShortcutDefaults.Keys)
    if(entries.TryGetValue(id,out var entry)&&entry is ConfigEntry<KeyboardShortcut> shortcut&&WildrootSettings.UpgradeShortcut(id,shortcut.Value.Serialize())){shortcut.Value=KeyboardShortcut.Empty;upgraded.Add(id);}
   if(upgraded.Count==0)return;
   file.Save();
   ShortcutUpgradeNote="Wildroot shortcuts left at their earlier defaults are now unbound (their switches are settings rows): "+string.Join(", ",upgraded)+". Shortcuts you changed are kept.";
  }
  /// <summary>The one log line from the schema 2 shortcut upgrade, or null when nothing changed; the plugin logs it.</summary>
  internal string? ShortcutUpgradeNote{get;private set;}
  void Add(string id,ConfigEntryBase entry){entries.Add(id,entry);scopes.Add(id,WildrootSettings.ScopeOf(id));}
  ConfigEntry<T> Bind<T>(string section,string key,T value,AcceptableValueBase? acceptable=null,string? description=null){
   string id=section+"."+key,help=description??WildrootImportedHelp.Description(id)??"Wildroot "+key+" setting.";
   var scope=WildrootSettings.ScopeOf(id);
   if(WildrootImportedHelp.IsInternal(id))return MeadworksConfig.BindHidden(file,section,key,value,help,scope,acceptable);
   return MeadworksConfig.Bind(file,section,key,value,new ConfigDescription(help,acceptable,Dependencies(id)),scope);
  }
  object[] Dependencies(string id){
   string? extra=null;
   switch(id){
    case "Grid.Rows":case "Grid.Columns":
     return new object[]{new ConfigDependency(()=>Get(WildrootSettings.Enabled,true)&&Get("Grid.Enabled",true),()=>"Requires General: Enabled and Grid: Enabled.")};
    case "Farming.RowOfThreeLevel":case "Farming.RowOfFiveLevel":extra=FarmingRules.RowPlantingId;break;
    case "Farming.AutoReplantLevel":extra=FarmingRules.AutoReplantId;break;
    case "Farming.TierReferenceValue":case "Farming.TierMaximumMultiplier":extra="Farming.TierScaling";break;
    case "Farming.GrowthSpeedAt100":case "Farming.GrowSpaceReductionAt100":case "Farming.BonusYieldChanceAt100":
    case "Farming.SeedReturnChanceAt100":case "Farming.RainGrowthBonus":case "Farming.HeatToleranceLevel":
    case "Farming.ColdToleranceLevel":case "Farming.ExperienceMultiplier":case "Farming.TierScaling":
    case "Farming.DiscoveryMultiplier":case FarmingRules.RowPlantingId:case FarmingRules.AutoReplantId:break;
    default:return Array.Empty<object>();
   }
   return new object[]{new ConfigDependency(()=>Get(WildrootSettings.Enabled,true)&&Get(FarmingRules.EnabledId,true)&&(extra==null||Get(extra,true)),
    ()=>"Requires General: Enabled and Farming: Enabled"+(extra==null?"":" and "+extra)+".")};
  }
  public ConfigEntryBase? Find(string id){if(aliases.TryGetValue(id,out var canonical))id=canonical;return entries.TryGetValue(id,out var entry)?entry:null;}
  public T Get<T>(string id,T fallback)=>Find(id)?.BoxedValue is T value?value:fallback;
  public ConfigScope ScopeOf(string id){if(aliases.TryGetValue(id,out var canonical))id=canonical;return scopes.TryGetValue(id,out var scope)?scope:ConfigScope.Local;}
  public IReadOnlyDictionary<string,ConfigEntryBase> Entries=>entries;
  internal static AuthorityFacts Facts=>CruciblePlugin.IsReady?MeadworksAuthority.Facts(WildrootSettings.ProductId):new AuthorityFacts(SessionRole.Menu,false,false,ServerCrucible.Unknown,PeerClass.RequiredOnAllPeers);
  internal static SessionRole Role=>CruciblePlugin.IsReady?CruciblePlugin.Session.Role:SessionRole.Menu;
  /// <summary>Wildroot runs: master switch on (the server's in multiplayer), no reference mod installed, and the session gate allows it.</summary>
  public bool Active{get{Refresh();return active;}}
  /// <summary>Unrestricted placement for the local player (Mischief gate, category World).</summary>
  public bool UnrestrictedPlacement{get{
   Refresh();if(placementFrame==decidedFrame)return placement;placementFrame=decidedFrame;
   // The platform id only matters for a remote client under a Listed policy; reading it early in the menu would
   // touch the game's user and localization services before they exist.
   // On a server, players may use it when the admin lets them (Players May Use).
   placement=active&&(facts!.Role==SessionRole.RemoteClient&&MeadworksMischief.PlayersMay(Unrestricted)
    ?Unrestricted.Value:WildrootCheats.Placement(facts!,MeadworksMischief.Policy,facts!.Role==SessionRole.RemoteClient?MeadworksMischief.LocalPlatformId():null,Unrestricted.Value));
   return placement;}}
  /// <summary>Unrestricted growth: one world rule, the server's value once authority is established.</summary>
  public bool UnrestrictedGrowth{get{Refresh();return growth;}}
  // Growth patches ask per plant; the decisions change at most once per frame.
  int decidedFrame=-1,placementFrame=-2;bool active,placement,growth;AuthorityFacts? facts;
  void Refresh(){
   int frame=Time.frameCount;if(frame==decidedFrame)return;decidedFrame=frame;
   facts=Facts;active=WildrootCheats.Active(facts,Enabled.Value,SteppedAside);
   growth=active&&WildrootCheats.Growth(facts,Unrestricted.Value);
  }
  /// <summary>Tells the player why unrestricted placement is refused (C03 notice, once per session).</summary>
  internal void NotifyUnrestrictedBlocked(){if(Unrestricted.Value&&!UnrestrictedPlacement&&Active)MeadworksMischief.TryUse(WildrootSettings.ProductId,WildrootSettings.Unrestricted,WildrootSettings.UnrestrictedCategory,"Wildroot unrestricted planting");}
 }
}
