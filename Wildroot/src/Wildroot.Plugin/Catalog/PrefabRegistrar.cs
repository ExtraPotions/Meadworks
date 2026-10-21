using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>
 /// Registers the captured PlantEverything catalog and keeps native prefabs, the cultivator table, tree drops and
 /// third-party plants in line with the current settings. Every apply derives the whole effective catalog first and
 /// only then writes prefabs, so an invalid reload leaves the previous state in place. Every native value Wildroot
 /// writes is recorded once in a <see cref="MutationLedger"/>; when Wildroot turns off (master switch, vanilla or
 /// Wildroot-less server, step-aside, shutdown) the game's values come back and the cultivator lists only its own
 /// pieces. Remote clients build extra resources from the server's published text and compare their catalog
 /// fingerprint with the server's.
 /// </summary>
 internal sealed class PrefabRegistrar:IDisposable,IPrefabCatalog {
  readonly WildrootConfig settings;readonly string assets;readonly ManualLogSource log;readonly ICatalogSettings view;bool registered,dirty;CatalogSnapshot? baseline;
  readonly Dictionary<string,Sprite> plantIcons=new Dictionary<string,Sprite>(StringComparer.Ordinal);
  readonly Dictionary<string,PlantDefinition> definitions=new Dictionary<string,PlantDefinition>(StringComparer.Ordinal);
  /// <summary>Catalog prefabs the game does not ship; they follow EnforceBiomes instead of EnforceBiomesVanilla.</summary>
  readonly HashSet<string> cloned=new HashSet<string>(StringComparer.Ordinal);
  static readonly Heightmap.Biome AllBiomes=Enum.GetValues(typeof(Heightmap.Biome)).Cast<Heightmap.Biome>().Aggregate(Heightmap.Biome.None,(all,b)=>all|b);
  readonly Dictionary<string,PiecePolicy> policies=new Dictionary<string,PiecePolicy>(StringComparer.Ordinal);
  readonly Dictionary<string,PrefabOriginal> originals=new Dictionary<string,PrefabOriginal>(StringComparer.Ordinal);
  readonly Dictionary<string,DropTableValues> treeOriginals=new Dictionary<string,DropTableValues>(StringComparer.Ordinal);
  readonly Dictionary<string,GrowthOverride> moddedOriginals=new Dictionary<string,GrowthOverride>(StringComparer.Ordinal);
  IReadOnlyList<ExtraResourceDefinition> extras=Array.Empty<ExtraResourceDefinition>();FileSystemWatcher? watcher;
  readonly MutationLedger ledger=new MutationLedger();
  /// <summary>The cultivator's own pieces before Wildroot registered anything; restored when Wildroot turns off.</summary>
  readonly HashSet<string> nativeCultivator=new HashSet<string>(StringComparer.Ordinal);
  bool lastActive;ExtraResourceOrigin lastOrigin;bool compareNeeded;readonly CatalogMismatchGate mismatch=new CatalogMismatchGate();
  /// <summary>True while the game's own prefab values are in place (nothing applied, or restored).</summary>
  public bool Restored{get;private set;}=true;
  /// <summary>Fingerprint of the catalog this peer applied last; empty while restored.</summary>
  public string Fingerprint{get;private set;}="";
  public CatalogMatch ServerMatch{get;private set;}=CatalogMatch.Unknown;
  /// <summary>Recorded native values awaiting restore; lets tests observe the snapshot.</summary>
  public int RecordedValues=>ledger.Count;
  public IReadOnlyDictionary<string,PlantDefinition> Definitions=>definitions;
  public bool Registered=>registered;
  /// <summary>Number of completed applies; lets tests observe a reload.</summary>
  public int Revision{get;private set;}
  public IReadOnlyList<string> LastWarnings{get;private set;}=Array.Empty<string>();
  internal static string ExtraResourcePath=>Path.Combine(BepInEx.Paths.ConfigPath,"Wildroot","extra-resources.cfg");
  public PrefabRegistrar(WildrootConfig settings,string assets,ManualLogSource log){
   this.settings=settings;this.assets=assets;this.log=log;view=new ConfigView(settings);
   PrefabManager.OnVanillaPrefabsAvailable+=Register;PieceManager.OnPiecesRegistered+=MarkDirty;settings.Changed+=OnSettingChanged;
  }
  public bool HasPrefab(string name)=>!string.IsNullOrEmpty(name)&&PrefabManager.Instance.GetPrefab(name);
  public bool HasItem(string name)=>HasPrefab(name)&&PrefabManager.Instance.GetPrefab(name).GetComponent<ItemDrop>();
  internal PiecePolicy? Policy(string prefab)=>policies.TryGetValue(prefab,out var policy)?policy:(PiecePolicy?)null;
  internal ICatalogSettings View=>view;
  void OnSettingChanged(string id){
   if(id==WildrootSettings.ServerFingerprint){compareNeeded=true;return;}
   if(id==WildrootSettings.ExtraResourcesShared){if(Origin==ExtraResourceOrigin.Server)dirty=true;return;}
   if(id==WildrootSettings.Enabled)dirty=true;
   if(id.StartsWith("Catalog.",StringComparison.Ordinal)||id.StartsWith("Growth.",StringComparison.Ordinal)||id.StartsWith("Drops.",StringComparison.Ordinal)||id.StartsWith("Restrictions.",StringComparison.Ordinal))dirty=true;}
  void MarkDirty()=>dirty=true;
  void Register(){
   if(registered)return;
   if(settings.SteppedAside){log.LogInfo("Wildroot catalog not registered: a reference mod is installed.");return;}
   try{
    var cultivator=PrefabManager.Instance.GetPrefab("Cultivator")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces;
    if(cultivator)foreach(var piece in cultivator!.m_pieces)if(piece)nativeCultivator.Add(piece.name);
    NativePlacementReceipts.Register();
    var vanilla=(JArray)JObject.Parse(File.ReadAllText(Path.Combine(assets,"catalog","vanilla-reference.json")))["entries"]!;
    foreach(JObject entry in vanilla){string name=entry.Value<string>("prefab")!;if(!HasPrefab(name)){cloned.Add(name);var clone=PrefabManager.Instance.CreateClonedPrefab(name,"Beech_Sapling");if(!clone)throw new InvalidOperationException("Cannot clone missing sapling: "+name);var plant=clone.GetComponent<Plant>();var grown=entry.Value<string>("grownPrefab")!;Set(plant,"m_grownPrefabs",new[]{PrefabManager.Instance.GetPrefab(grown)});PrefabManager.Instance.AddPrefab(new CustomPrefab(clone,false));}}
    var vanillaResult=CatalogLoader.Load(new JObject{{"schema",1},{"entries",vanilla}}.ToString(),this);if(!vanillaResult.Valid)throw new InvalidOperationException(string.Join("; ",vanillaResult.Errors));
    baseline=vanillaResult.Snapshot!;
    foreach(var definition in baseline.Entries){Configure(definition,PiecePolicies.For(definition,view),false);if(cloned.Contains(definition.Prefab))PrefabManager.Instance.RemovePrefab(definition.Prefab);/* AddPiece registers the prefab itself; the earlier AddPrefab only made the clone resolvable for Configure */if(!PieceManager.Instance.AddPiece(new CustomPiece(PrefabManager.Instance.GetPrefab(definition.Prefab),false,new PieceConfig{PieceTable="Cultivator",Enabled=true})))throw new InvalidOperationException("Cultivator piece rejected: "+definition.Prefab);}
    foreach(var name in CatalogPolicy.PickedSpawners)PickedSpawners.AddTo(PrefabManager.Instance.GetPrefab(name));
    registered=true;lastActive=settings.Active;lastOrigin=Origin;Apply();log.LogInfo("Wildroot registered PlantEverything catalog; two-reference development baseline.");
   }catch(Exception error){log.LogError("Wildroot catalog registration failed: "+error);}
  }
  /// <summary>Applies pending setting or extra-resource changes on the main thread.</summary>
  internal void Update(){
   if(!registered)return;
   bool active=settings.Active;var origin=Origin;
   if(active!=lastActive||origin!=lastOrigin){lastActive=active;lastOrigin=origin;dirty=true;}
   if(dirty){dirty=false;Apply();}
   if(compareNeeded||mismatch.Pending&&UnityEngine.Time.realtimeSinceStartup-mismatch.Since>=CatalogMismatchGate.SettleSeconds){compareNeeded=false;CompareWithServer();}
  }
  ExtraResourceOrigin Origin=>CatalogDistribution.Origin(WildrootConfig.Role,settings.Get("Catalog.EnableExtraResources",false));
  internal void Apply(){
   if(baseline==null)return;
   if(!settings.Active){RestoreNative();return;}
   try{
    var warnings=new List<string>();var effective=CatalogPolicy.Apply(baseline,view,warnings);var extra=LoadExtras(warnings);
    var catalogPrefabs=new HashSet<string>(effective.Entries.Select(d=>d.Prefab),StringComparer.Ordinal);
    var next=new Dictionary<string,PlantDefinition>(StringComparer.Ordinal);foreach(var d in CatalogPolicy.WithExtras(effective,extra,view,warnings).Entries)next[d.Prefab]=d;
    var added=extra.Where(e=>!catalogPrefabs.Contains(e.Prefab)).GroupBy(e=>e.Prefab,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal);
    var nextPolicies=next.Values.ToDictionary(d=>d.Prefab,d=>PiecePolicies.For(d,view),StringComparer.Ordinal);
    foreach(var d in next.Values)Configure(d,nextPolicies[d.Prefab],true,added.TryGetValue(d.Prefab,out var own)?own:null);
    var removedExtras=definitions.Keys.Where(k=>!next.ContainsKey(k)).ToArray();
    definitions.Clear();foreach(var pair in next)definitions[pair.Key]=pair.Value;policies.Clear();foreach(var pair in nextPolicies)policies[pair.Key]=pair.Value;extras=extra;
    ApplyTrees();SyncCultivator(removedExtras);PickedSpawners.SyncAll();Restored=false;Revision++;LastWarnings=warnings;foreach(var warning in warnings)log.LogWarning("Wildroot catalog: "+warning);
    Fingerprint=CatalogDistribution.Fingerprint(definitions.Values);PublishFingerprint();CompareWithServer();
   }catch(Exception error){log.LogError("Wildroot catalog apply rejected; previous catalog kept: "+error.Message);}
  }
  IReadOnlyList<ExtraResourceDefinition> LoadExtras(List<string> warnings){
   var origin=Origin;
   if(origin!=ExtraResourceOrigin.LocalFile){watcher?.Dispose();watcher=null;}
   if(origin==ExtraResourceOrigin.None){Publish(settings.ExtraResourcesShared,"");return Array.Empty<ExtraResourceDefinition>();}
   if(origin==ExtraResourceOrigin.Server)return ParseExtras(settings.ExtraResourcesShared.Value,warnings);
   var path=ExtraResourcePath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);
   if(!File.Exists(path))File.WriteAllText(path,"{\"prefabName\":\"PE_FakePrefab1\",\"resourceName\":\"Wood\",\"resourceCost\":1,\"groundOnly\":true,\"pieceName\":\"Example\",\"pieceDescription\":\"Entries starting with PE_Fake are ignored.\"};\n{\n \"prefabName\": \"PE_FakePrefab2\",\n \"resourceName\": \"Stone\",\n \"resourceCost\": 2,\n \"groundOnly\": false,\n \"pieceName\": \"Example two\",\n \"pieceDescription\": \"Any formatting of valid objects is accepted.\"\n}\n");
   if(watcher==null){watcher=new FileSystemWatcher(Path.GetDirectoryName(path)!,Path.GetFileName(path)){NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.Size|NotifyFilters.FileName};watcher.Changed+=(_,__)=>dirty=true;watcher.Created+=(_,__)=>dirty=true;watcher.Renamed+=(_,__)=>dirty=true;watcher.EnableRaisingEvents=true;}
   var text=File.ReadAllText(path);var parsed=ParseExtras(text,warnings);
   if(CatalogDistribution.Publishes(WildrootConfig.Role)){var shared=CatalogDistribution.Shareable(text);if(shared==null)warnings.Add("Extra resources exceed "+CatalogDistribution.MaxSharedLength+" characters and are not sent to clients");else Publish(settings.ExtraResourcesShared,shared);}
   return parsed;
  }
  IReadOnlyList<ExtraResourceDefinition> ParseExtras(string text,List<string> warnings){
   var result=ExtraResourceImporter.Parse(text,this);warnings.AddRange(result.Warnings);
   if(!result.Valid){warnings.Add("Extra resources rejected; previous definitions kept");return extras;}
   return result.Entries;
  }
  /// <summary>The authority sends a value to every client through the C01 Jotunn bridge; other roles never publish.</summary>
  void Publish(BepInEx.Configuration.ConfigEntry<string> entry,string value){
   if(!CatalogDistribution.Publishes(WildrootConfig.Role)||string.Equals(entry.Value,value,StringComparison.Ordinal))return;
   MeadworksConfig.Amend(entry,WildrootSettings.ProductId,ConfigScope.ServerAuthoritative,value);
  }
  void PublishFingerprint()=>Publish(settings.ServerFingerprint,Fingerprint);
  void CompareWithServer(){
   if(WildrootConfig.Role!=SessionRole.RemoteClient||Restored){ServerMatch=CatalogMatch.Unknown;mismatch.Observe(ServerMatch,"","",UnityEngine.Time.realtimeSinceStartup);return;}
   ServerMatch=CatalogDistribution.Compare(settings.ServerFingerprint.Value,Fingerprint);
   // An administrator's change arrives one setting at a time; only a difference that lasts is reported (CatalogMismatchGate).
   if(mismatch.Observe(ServerMatch,settings.ServerFingerprint.Value,Fingerprint,UnityEngine.Time.realtimeSinceStartup)==null)return;
   log.LogWarning("Wildroot catalog differs from the server's (server "+settings.ServerFingerprint.Value+", here "+Fingerprint+"). Every peer needs the same Wildroot version and the same prefab mods for extra resources.");
  }
  void Configure(PlantDefinition d,PiecePolicy policy,bool applied,ExtraResourceDefinition? extra=null){
   var prefab=PrefabManager.Instance.GetPrefab(d.Prefab);if(!prefab)throw new InvalidOperationException("Missing catalog prefab "+d.Prefab);
   var o=Original(prefab,d);var section=CatalogPolicy.Section(d);bool crops=CatalogPolicy.CropOverrides(view),vines=CatalogPolicy.VineOverrides(view);
   var plant=prefab.GetComponent<Plant>();var pickable=prefab.GetComponent<Pickable>();
   if(plant&&extra==null){
    Write(plant,"m_growTime",(float)d.GrowMin);Write(plant,"m_growTimeMax",(float)d.GrowMax);Write(plant,"m_growRadius",(float)d.Radius);Write(plant,"m_minScale",(float)d.ScaleMin);Write(plant,"m_maxScale",(float)d.ScaleMax);
    if(section==CatalogSection.Saplings)ApplySaplingSurvival(plant);
    Write(plant,"m_tolerateHeat",(ReferenceTraits.Heat(d.Prefab)??o.TolerateHeat)||policy.ToleratesEnvironment);Write(plant,"m_tolerateCold",(ReferenceTraits.Cold(d.Prefab)??o.TolerateCold)||policy.ToleratesEnvironment);
    if(section==CatalogSection.Crops)Write(plant,"m_needCultivatedGround",crops?policy.RequiresCultivation:o.NeedCultivated);
    if(section==CatalogSection.Vines)Write(plant,"m_attachDistance",(float)(CatalogPolicy.VineAttachDistance(view)??o.AttachDistance));
    // PlantEverything's own masks win where they are not the game's (ReferenceTraits).
    var pinned=CatalogPolicy.ReferenceBiome(d.Prefab,view);
    Write(plant,"m_biome",CatalogPolicy.EnforcesBiome(d,!cloned.Contains(d.Prefab),view)?(pinned.HasValue?(Heightmap.Biome)pinned.Value:cloned.Contains(d.Prefab)?VegetationBiome(d.GrownPrefab,o.Biome):o.Biome):AllBiomes);
    var grown=plant.m_grownPrefabs?.FirstOrDefault(g=>g);
    if(grown){foreach(var berry in grown!.GetComponentsInChildren<Pickable>(true)){var key=grown.name+"/"+berry.name;if(!o.Grown.TryGetValue(key,out var g))o.Grown[key]=g=(berry.m_amount,berry.m_respawnTimeMinutes);
     bool overridden=section==CatalogSection.Crops&&crops||section==CatalogSection.Vines&&vines;Write(berry,"m_amount",overridden?d.Yield:g.amount);if(section==CatalogSection.Vines)Write(berry,"m_respawnTimeMinutes",overridden?(float)(d.RespawnSeconds/60):g.respawn);}}
   }
   if(pickable&&extra==null){Write(pickable,"m_amount",d.Yield);Write(pickable,"m_respawnTimeMinutes",(float)(d.RespawnSeconds/60));
    // The catalog yield is the whole harvest of the pickable's own item (as PlantEverything), so extra-drop rolls of that same
    // item go (the game's Pickable_Fiddlehead rolls 2 more Fiddleheadfern); extras of other items stay.
    var own=pickable.m_itemPrefab;var table=pickable.m_extraDrops;
    if(own&&table?.m_drops!=null&&table.m_drops.Any(e=>e.m_item==own))
     Write(pickable,"m_extraDrops",new DropTable{m_dropMin=table.m_dropMin,m_dropMax=table.m_dropMax,m_dropChance=table.m_dropChance,m_oneOfEach=table.m_oneOfEach,m_drops=table.m_drops.Where(e=>e.m_item!=own).ToList()});}
   // A Piece Wildroot adds stays (Jotunn's registered piece refers to it); its neutral values are the restore target.
   var piece=prefab.GetComponent<Piece>();if(!piece){piece=prefab.AddComponent<Piece>();piece.m_canBeRemoved=false;piece.m_randomTarget=false;piece.m_primaryTarget=false;}
   // A sapling is placeable only where it grows, as PlantEverything sets Piece.m_onlyInBiome to the growth mask.
   if(plant&&extra==null&&d.Kind==PlantKind.Sapling)Write(piece,"m_onlyInBiome",plant.m_biome);
   var resources=d.Costs.Select(c=>new Piece.Requirement{m_resItem=PrefabManager.Instance.GetPrefab(c.ItemId).GetComponent<ItemDrop>(),m_amount=c.Amount,m_recover=policy.RecoverResources}).ToArray();
   Write(piece,"m_resources",resources);
   if(!nativeCultivator.Contains(d.Prefab)&&extra==null&&Plugin.Instance!=null&&!Application.isBatchMode){
    if(!plantIcons.TryGetValue(d.Prefab,out var icon)){
     icon=RenderManager.Instance.Render(new RenderManager.RenderRequest(prefab){Width=IconPixels,Height=IconPixels,Rotation=RenderManager.IsometricRotation,ParticleSimulationTime=-1f});
     if(icon){icon=NormalizeIcon(icon);plantIcons[d.Prefab]=icon;}
    }
    if(icon)Write(piece,"m_icon",icon);
   }
   else if(!piece.m_icon&&resources.Length>0)Write(piece,"m_icon",resources[0].m_resItem.m_itemData.GetIcon());
   if(extra!=null){Write(piece,"m_name",extra.Name);Write(piece,"m_description",extra.Description);Write(piece,"m_groundOnly",extra.GroundOnly);Write(piece,"m_groundPiece",extra.GroundOnly);}
   // A sapling cloned from Beech_Sapling would keep the beech piece name, and the game unlocks pieces by name: knowing beech seeds
   // alone would show it. Its own name keeps it hidden until all its materials are known, as PlantEverything's own piece names do.
   // shortcut: the name is English ("Ashwood Sapling"), not localized; add translation keys when piece names are localized.
   // A pickable shows the name of the item it yields ("$item_stone" reads "Stone"), never its prefab id (Pickable_Stone).
   else{var yields=d.Kind==PlantKind.Pickable&&pickable&&pickable!.m_itemPrefab?pickable.m_itemPrefab.GetComponent<ItemDrop>():null;
    if(yields&&!string.IsNullOrEmpty(yields!.m_itemData?.m_shared?.m_name))Write(piece,"m_name",yields.m_itemData!.m_shared.m_name);
    else if(cloned.Contains(d.Prefab))Write(piece,"m_name",d.Prefab.Replace('_',' '));else if(string.IsNullOrEmpty(piece.m_name))Write(piece,"m_name",d.Prefab);Write(piece,"m_groundOnly",policy.GroundOnly&&o.GroundOnly);Write(piece,"m_groundPiece",policy.GroundOnly&&o.GroundPiece);}
   if(d.Kind==PlantKind.Pickable)Write(piece,"m_cultivatedGroundOnly",policy.RequiresCultivation);
   else if(section==CatalogSection.Crops&&extra==null)Write(piece,"m_cultivatedGroundOnly",crops?policy.RequiresCultivation:o.PieceCultivated);
   if(d.Id=="vines"&&extra==null){ledger.Capture(prefab,SnapMember,null);SnapPoints(prefab,CatalogPolicy.VineSnapPoints(d,view));}
   if(applied){Write(piece,"m_canBeRemoved",policy.Removable);Write(piece,"m_randomTarget",policy.Targetable);Write(piece,"m_primaryTarget",o.PrimaryTarget&&policy.Targetable);}
  }
  // Rendered well above the build menu's display size: after cropping to the plant's bounds it stays as sharp as native icons.
  const int IconPixels=256;
  static Sprite NormalizeIcon(Sprite icon){
   var texture=icon.texture;var pixels=texture.GetPixels32();var rect=icon.rect;
   int x0=(int)rect.x,y0=(int)rect.y,w=(int)rect.width,h=(int)rect.height;
   int left=w,top=h,right=-1,bottom=-1;
   for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[(y0+y)*texture.width+x0+x].a>8){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
   if(right<left)return icon;
   var bounds=PlantIconBounds.Square(w,h,left,top,right,bottom);
   var normalized=Sprite.Create(texture,new Rect(x0+bounds.x,y0+bounds.y,bounds.size,bounds.size),new Vector2(.5f,.5f),icon.pixelsPerUnit);
   UnityEngine.Object.Destroy(icon);return normalized;
  }
  const string SnapName="wildroot_snappoint";const string SnapMember="@snappoints";
  /// <summary>Replaces Wildroot's own snap points on a prefab; native snap points are never touched.</summary>
  static void SnapPoints(GameObject prefab,IReadOnlyList<Point3> points){
   var existing=prefab.transform.Cast<Transform>().Where(t=>t.name==SnapName).ToList();
   if(existing.Count==points.Count&&existing.Select(t=>t.localPosition).All(p=>points.Any(q=>Vector3.Distance(p,new Vector3((float)q.X,(float)q.Y,(float)q.Z))<1e-4f)))return;
   foreach(var t in existing)UnityEngine.Object.DestroyImmediate(t.gameObject);
   foreach(var q in points){var snap=new GameObject(SnapName){tag="snappoint"};snap.SetActive(false);snap.transform.SetParent(prefab.transform,false);snap.transform.localPosition=new Vector3((float)q.X,(float)q.Y,(float)q.Z);}
  }
  PrefabOriginal Original(GameObject prefab,PlantDefinition d){
   if(originals.TryGetValue(prefab.name,out var o))return o;var plant=prefab.GetComponent<Plant>();var piece=prefab.GetComponent<Piece>();
   o=new PrefabOriginal{TolerateHeat=plant&&plant.m_tolerateHeat,TolerateCold=plant&&plant.m_tolerateCold,NeedCultivated=plant&&plant.m_needCultivatedGround,AttachDistance=plant?plant.m_attachDistance:0,
    GroundOnly=piece?piece.m_groundOnly:d.Kind!=PlantKind.Vine&&d.Id!="vines",GroundPiece=piece?piece.m_groundPiece:d.Kind!=PlantKind.Vine&&d.Id!="vines",PrimaryTarget=piece&&piece.m_primaryTarget,PieceCultivated=piece&&piece.m_cultivatedGroundOnly,Biome=plant?plant.m_biome:Heightmap.Biome.None};
   originals[prefab.name]=o;return o;
  }
  /// <summary>Tree destruction drops follow the seed overrides; the native tables are restored when they are off.</summary>
  void ApplyTrees(){
   if(!ZNetScene.instance)return;
   foreach(var prefab in ZNetScene.instance.m_prefabs){
    if(!prefab)continue;var tree=prefab.GetComponent<TreeBase>();if(!tree||tree.m_dropWhenDestroyed==null)continue;var table=tree.m_dropWhenDestroyed;
    if(!treeOriginals.TryGetValue(prefab.name,out var original))treeOriginals[prefab.name]=original=new DropTableValues(table.m_dropMin,table.m_dropMax,table.m_dropChance,table.m_oneOfEach,table.m_drops.Select(e=>new DropEntry(e.m_item?e.m_item.name:"",e.m_stackMin,e.m_stackMax)).ToList());
    var values=TreeDropPolicy.Apply(original,view);Write(table,"m_dropMin",values.Min);Write(table,"m_dropMax",values.Max);Write(table,"m_dropChance",(float)values.Chance);Write(table,"m_oneOfEach",values.OneOfEach);
    ledger.Capture(table,"m_drops",new List<DropTable.DropData>(table.m_drops));
    for(int i=0;i<table.m_drops.Count&&i<values.Entries.Count;i++){var drop=table.m_drops[i];drop.m_stackMin=values.Entries[i].StackMin;drop.m_stackMax=values.Entries[i].StackMax;table.m_drops[i]=drop;}
   }
  }
  /// <summary>A cloned sapling inherits Beech's mask; its own biome is where the game spawns the grown tree.</summary>
  static Heightmap.Biome VegetationBiome(string grown,Heightmap.Biome fallback){
   if(!ZoneSystem.instance||string.IsNullOrEmpty(grown))return fallback;var mask=Heightmap.Biome.None;
   foreach(var v in ZoneSystem.instance.m_vegetation)if(v!=null&&v.m_enable&&v.m_prefab&&v.m_prefab.name==grown)mask|=v.m_biome;
   return mask==Heightmap.Biome.None?fallback:mask;
  }
  internal static PieceTable? CultivatorTable()=>ObjectDB.instance?ObjectDB.instance.GetItemPrefab("Cultivator")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces:null;
  /// <summary>Disabled entries leave the cultivator, enabled entries and extra resources join it; modded plants get overrides.</summary>
  void SyncCultivator(IReadOnlyCollection<string> removed){
   var table=CultivatorTable();if(!table)return;Write(table!,"m_canRemovePieces",true);var pieces=table!.m_pieces;
   foreach(var name in removed){var prefab=PrefabManager.Instance.GetPrefab(name);if(prefab)pieces.Remove(prefab);}
   foreach(var d in definitions.Values){var prefab=PrefabManager.Instance.GetPrefab(d.Prefab);if(!prefab)continue;bool listed=pieces.Contains(prefab);if(d.Enabled&&!listed)pieces.Add(prefab);else if(!d.Enabled&&listed)pieces.Remove(prefab);}
   var crop=CatalogPolicy.ModdedCrop(view);var sapling=CatalogPolicy.ModdedSapling(view);
   foreach(var prefab in pieces.ToArray()){
    if(!prefab||definitions.ContainsKey(prefab.name))continue;var plant=prefab.GetComponent<Plant>();if(!plant)continue;
    if(!moddedOriginals.TryGetValue(prefab.name,out var original))moddedOriginals[prefab.name]=original=new GrowthOverride(plant.m_growTime,plant.m_growTimeMax,plant.m_growRadius,plant.m_minScale,plant.m_maxScale);
    bool isSapling=plant.m_grownPrefabs!=null&&plant.m_grownPrefabs.Any(g=>g&&g.GetComponent<TreeBase>());var values=(isSapling?sapling:crop)??original;
    Write(plant,"m_growTime",(float)values.GrowMin);Write(plant,"m_growTimeMax",(float)values.GrowMax);Write(plant,"m_growRadius",(float)values.Radius);Write(plant,"m_minScale",(float)values.ScaleMin);Write(plant,"m_maxScale",(float)values.ScaleMax);
    if(isSapling)ApplySaplingSurvival(plant);
   }
  }
  // Frozen PlantEverything InitSaplings applies this to native/added saplings and to external plants
  // whose grown prefabs contain TreeBase, independently of OverrideModdedSaplings. Crops/vines keep their own rule.
  void ApplySaplingSurvival(Plant plant){
   const string field="m_destroyIfCantGrow";
   bool native=ledger.Original(plant,field)?.Original is bool captured?captured:plant.m_destroyIfCantGrow;
   Write(plant,field,settings.Get("Restrictions.PlaceAnywhere",false)?false:native);
  }
  bool PlaceAnywhereEligible(GameObject created){
   if(!created||!definitions.TryGetValue(PlacementBackend.PrefabName(created),out var d))return false;
   var piece=created.GetComponent<Piece>();
   return d.Enabled&&settings.Get("Restrictions.PlaceAnywhere",false)&&policies.TryGetValue(d.Prefab,out var policy)&&!policy.GroundOnly&&piece&&!piece.m_groundOnly;
  }
  /// <summary>Before a correlated created object yields: persist native PlaceAnywhere support.</summary>
  internal void AfterCreated(GameObject created){if(PlaceAnywhereEligible(created))PlaceAnywhere.MarkPlaced(created);}
  /// <summary>After a committed placement: apply spawn-empty pickables.</summary>
  internal void AfterCommitted(GameObject created){
   if(!created||!definitions.TryGetValue(PlacementBackend.PrefabName(created),out var d))return;
   if(!PiecePolicies.SpawnsEmpty(d,view))return;
   var pickable=created.GetComponent<Pickable>();if(pickable)AccessTools.Method(typeof(Pickable),"SetPicked").Invoke(pickable,new object[]{true});
  }
  /// <summary>Writes a native field after recording its first original value.</summary>
  void Write(object target,string field,object? value){
   if(target==null||target is UnityEngine.Object unity&&!unity)throw new InvalidOperationException("Missing prefab component");
   var info=AccessTools.Field(target.GetType(),field)??throw new MissingFieldException(field);
   ledger.Capture(target,field,info.GetValue(target));info.SetValue(target,value);
  }
  /// <summary>
  /// Puts the game's own values back (newest change first), removes Wildroot's snap points and leaves only the
  /// cultivator's native pieces. Objects already planted stay in the world; they simply follow the game's rules again.
  /// </summary>
  internal void RestoreNative(){
   var table=CultivatorTable();if(table)RestoreCultivator(table!);
   if(Restored&&ledger.Count==0)return;
   int restored=0;
   foreach(var record in ledger.Restore()){
    try{
     if(record.Handle is UnityEngine.Object unity&&!unity)continue;
     switch(record.Member){
      case SnapMember:var prefab=(GameObject)record.Handle;foreach(var t in prefab.transform.Cast<Transform>().Where(t=>t.name==SnapName).ToList())UnityEngine.Object.DestroyImmediate(t.gameObject);break;
      default:AccessTools.Field(record.Handle.GetType(),record.Member).SetValue(record.Handle,record.Original);break;
     }
     restored++;
    }catch(Exception error){log.LogWarning("Wildroot could not restore "+record.Member+": "+error.Message);}
   }
   Restored=true;Fingerprint="";ServerMatch=CatalogMatch.Unknown;PickedSpawners.SyncAll();Revision++;
   log.LogInfo("Wildroot restored "+restored+" native values; the game's own planting rules apply.");
  }
  void RestoreCultivator(PieceTable table){
   var managed=definitions.Keys.Concat(baseline?.Entries.Select(d=>d.Prefab)??Enumerable.Empty<string>()).Concat(extras.Select(e=>e.Prefab));
   var byName=new Dictionary<string,GameObject>(StringComparer.Ordinal);foreach(var piece in table.m_pieces)if(piece&&!byName.ContainsKey(piece.name))byName[piece.name]=piece;
   var names=CultivatorMembership.Restore(table.m_pieces.Where(p=>p).Select(p=>p.name),nativeCultivator,managed);
   table.m_pieces.Clear();foreach(var name in names){var piece=byName.TryGetValue(name,out var known)?known:PrefabManager.Instance.GetPrefab(name);if(piece)table.m_pieces.Add(piece);}
  }
  static void Set(Component target,string field,object value){if(!target)throw new InvalidOperationException("Missing prefab component");var info=AccessTools.Field(target.GetType(),field);if(info==null)throw new MissingFieldException(field);info.SetValue(target,value);}
  public void Dispose(){try{RestoreNative();}catch(Exception error){log.LogWarning("Wildroot restore on shutdown failed: "+error.Message);}PrefabManager.OnVanillaPrefabsAvailable-=Register;PieceManager.OnPiecesRegistered-=MarkDirty;settings.Changed-=OnSettingChanged;watcher?.Dispose();watcher=null;}
  sealed class PrefabOriginal {public bool TolerateHeat,TolerateCold,NeedCultivated,GroundOnly,GroundPiece,PrimaryTarget,PieceCultivated;public float AttachDistance;public Heightmap.Biome Biome;public readonly Dictionary<string,(int amount,float respawn)> Grown=new Dictionary<string,(int,float)>(StringComparer.Ordinal);}
  sealed class ConfigView:ICatalogSettings {readonly WildrootConfig settings;public ConfigView(WildrootConfig settings){this.settings=settings;}public bool TryGet<T>(string id,out T value){if(settings.Find(id)?.BoxedValue is T typed){value=typed;return true;}value=default!;return false;}}
 }
}
