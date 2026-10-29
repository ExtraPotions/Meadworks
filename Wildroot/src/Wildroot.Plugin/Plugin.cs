using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Utils;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Wildroot.Runtime {
 [BepInPlugin(Guid,"Wildroot",Version)]
 [BepInDependency(CruciblePlugin.Guid,CruciblePlugin.Version)]
 [BepInDependency(ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Guid,ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Version)]
 [BepInDependency(Jotunn.Main.ModGuid,"2.30.2")]
 [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Minor)]
 public sealed class Plugin:BaseUnityPlugin {
  public const string Guid=WildrootSettings.ProductId;
  public const string Version="0.1.25";
  ProductRegistration? registration;
  Harmony? harmony;
  readonly int unityThread=Thread.CurrentThread.ManagedThreadId;
  internal static Plugin? Instance{get;private set;}
  PrefabRegistrar? catalog;
  internal PrefabRegistrar? Catalog=>catalog;
  LocalizationLoader? localization;
  internal LocalizationLoader? Text=>localization;
  public GridHud? GridInformation{get;private set;}
  public WildrootConfig? Settings{get;private set;}
  public PlacementController? Planting{get;private set;}
  public InputController? Controls{get;private set;}
  public HarvestController? Harvesting{get;private set;}
  public GridPreview? Preview{get;private set;}
  internal FarmingFeatures? Farming{get;private set;}
  float previewErrorTime=-60;
  internal void LogPreviewError(Exception error){if(UnityEngine.Time.unscaledTime-previewErrorTime<30)return;previewErrorTime=UnityEngine.Time.unscaledTime;Logger.LogError("Wildroot grid preview failed: "+error.GetType().Name+": "+error.Message);}
  public bool Ready{get;private set;}
  public bool GameplayAvailable=>Ready&&catalog?.Registered==true&&Settings?.SteppedAside==false;
  /// <summary>Last reference import outcome; null until an import ran this session.</summary>
  public ImportOutcome? LastImport{get;private set;}
  bool importRequested,wasActive;
  internal string AssetDirectory=>Path.Combine(Path.GetDirectoryName(Info.Location)!,"assets");
  internal static string ImportReportPath=>Path.Combine(BepInEx.Paths.ConfigPath,"Wildroot","import-report.txt");
  void Awake(){
   try{
    if(!CruciblePlugin.IsReady)throw new InvalidOperationException("Required Crucible is not ready");
    Settings=new WildrootConfig(Config,AssetDirectory);if(Settings.ShortcutUpgradeNote!=null)Logger.LogInfo(Settings.ShortcutUpgradeNote);
    Logger.LogInfo("Wildroot retains but ignores obsolete configuration keys: "+string.Join(", ",WildrootPresets.Retired)+".");
    localization=new LocalizationLoader(Settings,AssetDirectory,Info.Metadata,Logger);
    registration=CruciblePlugin.Services.Register(new ProductMetadata(Guid,WildrootSettings.ProductName,new System.Version(Version),new System.Version(0,1),new System.Version(0,1),MultiplayerPolicy.RequiredOnAllPeers,"Hearth"));
    if(!CruciblePlugin.Services.StepAside.Declared(Guid).Any())MeadworksStepAside.Declare(Guid,WildrootSettings.ProductName,ReferenceImport.Sources.Select(s=>s.Mod));
    catalog=new PrefabRegistrar(Settings,AssetDirectory,Logger);
    Planting=new PlacementController(Settings,catalog,Logger);
    Preview=new GridPreview(Settings,catalog,Planting);
    Controls=new InputController(Settings,Planting);
    Harvesting=new HarvestController(Settings,Logger,catalog,Planting,registration);
    GridInformation=new GridHud(Settings,Planting,localization);
    Settings.Changed+=OnSettingChanged;
    Instance=this;
    Ready=true;Logger.LogInfo("Wildroot development configuration and Hearth registration ready; gameplay parity not established.");
   }catch(Exception error){Shutdown();Logger.LogError("Wildroot initialization rejected: "+error.GetType().Name+": "+error.Message);}
  }
  /// <summary>
  /// Runs after every plugin has loaded: a PlantEasily or PlantEverything install makes Wildroot step aside (C09) with
  /// one notice instead of patching the same methods twice; otherwise the game patches go in.
  /// </summary>
  void Start(){
   if(!Ready||Settings==null)return;
   try{
    Settings.SteppedAside=MeadworksStepAside.ShouldYield(Guid);
    if(!Settings.SteppedAside){harmony=new Harmony(Guid);harmony.PatchAll(typeof(Plugin).Assembly);NativePlacementReceipts.ReleasePendingSafety();Farming=new FarmingFeatures(Settings,localization);}
    else Logger.LogInfo("Wildroot stays off while a reference mod is installed; its settings can still be imported.");
    importRequested=Settings.ImportReferenceSettings.Value;
   }catch(Exception error){harmony?.UnpatchSelf();harmony=null;Settings.SteppedAside=true;Logger.LogError("Wildroot start rejected: "+error.GetType().Name+": "+error.Message);}
  }
  void OnSettingChanged(string id){
   if(id=="Import.ImportReferenceSettings"&&Settings?.ImportReferenceSettings.Value==true)importRequested=true;
   if(id=="Harvest.Replant"&&Settings?.Get(id,false)==false)Harvesting?.CancelPendingReplants();
   if(id=="General.Enabled"&&Settings?.Get(id,true)==false)Harvesting?.CancelAll();
   if(id=="Harvest.Enabled"&&Settings?.Get(id,true)==false)Harvesting?.CancelBulk();
  }
  void Update(){
   if(!Ready)return;
   try{Settings!.ApplyPendingPresets();}catch(Exception error){Logger.LogWarning("Wildroot preset could not be saved: "+error.GetType().Name+": "+error.Message);}
   if(importRequested){importRequested=false;RunImport();}
   bool active=Settings!.Active;
   if(wasActive&&!active){Planting?.CancelAll();Harvesting?.CancelAll();}
   wasActive=active;
   localization?.Update();catalog?.Update();Farming?.Update();Controls?.Update();Planting?.Update();Harvesting?.Update();GridInformation?.Update();
  }
  /// <summary>
  /// Reference import: reads the reference config files without changing them, writes a report next to Wildroot's other
  /// files, and applies only when nothing is ambiguous (C09 converter: one backup, one atomic write).
  /// </summary>
  void RunImport(){
   var settings=Settings;if(settings==null)return;
   ReferenceImportTarget? target=null;LastImport=null;
   try{
    target=new ReferenceImportTarget(Config,ReferenceImport.Sources.Select(source=>Path.Combine(BepInEx.Paths.ConfigPath,source.ConfigFile))
     .Concat(new[]{Path.Combine(AssetDirectory,"config","import-mapping.json")}));
    var converter=ReferenceImport.Converter(File.ReadAllText(Path.Combine(AssetDirectory,"config","import-mapping.json")));
    var values=new List<KeyValuePair<string,string>>();
    foreach(var source in ReferenceImport.Sources)values.AddRange(MeadworksConversion.ReadSourceConfig(Path.Combine(BepInEx.Paths.ConfigPath,source.ConfigFile),source.Prefix));
    var outcome=ReferenceImport.Run(WildrootConfig.Role,converter,values,target,settings.ImportConflictChoices.Value,settings.ImportBindingOverrides.Value);
    LastImport=outcome;
    Directory.CreateDirectory(Path.GetDirectoryName(ImportReportPath)!);File.WriteAllLines(ImportReportPath,outcome.Report);
    var line="Wildroot import "+outcome.Status+": "+outcome.Report[outcome.Report.Count-1]+" Report: "+ImportReportPath;
    if(outcome.Status==ImportStatus.Applied)Logger.LogInfo(line);else Logger.LogWarning(line);
   }catch(Exception error)when(error is IOException||error is UnauthorizedAccessException||error is FormatException||error is NotSupportedException||error is InvalidDataException||error is ArgumentException){
    Logger.LogWarning((target?.SavedLocally==true?"Wildroot import saved locally; completion failed: ":"Wildroot import failed; nothing was written: ")+error.GetType().Name+": "+error.Message);
   }finally{if(settings.ImportReferenceSettings.Value)settings.ImportReferenceSettings.Value=false;}
  }
  void OnDestroy()=>Shutdown();
  void Shutdown(){
   var context=SynchronizationContext.Current;
   bool ownerContext=context!=null&&Thread.CurrentThread.ManagedThreadId==unityThread&&SynchronizationContext.Current==context;
   long world=ownerContext?NativeWorld.CurrentUid():0;
   string session=ownerContext&&CruciblePlugin.IsReady?CruciblePlugin.Session.SessionId:"";
   var planting=Planting;
   Ready=false;Farming?.Dispose();Farming=null;Preview?.Dispose();Preview=null;GridInformation?.Dispose();GridInformation=null;localization?.Dispose();localization=null;Controls?.CancelQueuedWork();Controls?.ReleaseMenu();Controls=null;var retirement=planting?.ShutdownAsync();Planting=null;Harvesting?.Dispose();Harvesting=null;
   // Keep only the pending-object safety prefixes before removing ordinary gameplay patches.
   // Unknown context/world cannot prove absence of pending objects; install safety without native reads.
   var safetyOwnership=NativePlacementReceipts.PreservePendingSafety(ownerContext&&world!=0&&session.Length>0);
   harmony?.UnpatchSelf();harmony=null;Instance=null;
   // Dispose restores every native prefab value Wildroot changed.
   catalog?.Dispose();catalog=null;registration?.Dispose();registration=null;if(Settings!=null){Settings.Changed-=OnSettingChanged;Settings.Dispose();}Settings=null;
   _ = ReleaseSafetyAfterRetirement(retirement??Task.CompletedTask,planting,context,world,session,safetyOwnership);
  }
  async Task ReleaseSafetyAfterRetirement(Task retirement,PlacementController? planting,SynchronizationContext? context,long world,string session,object? safetyOwnership){
   try{
    await retirement;
    // An empty new world does not prove old saved objects settled. Recovery/context uncertainty retains safety.
    if(context!=null&&Thread.CurrentThread.ManagedThreadId==unityThread&&SynchronizationContext.Current==context&&planting?.HasRecoveryRequired!=true&&world!=0&&session.Length>0&&NativeWorld.IsCurrent(world)&&CruciblePlugin.IsReady&&CruciblePlugin.Session.SessionId==session)NativePlacementReceipts.ReleasePendingSafetyIfClear(safetyOwnership);
   }catch(Exception error){Logger.LogError("Wildroot pending-object safety retained after shutdown: "+error.GetType().Name);}
  }
 }
}
