using System;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Wayfarer.Core;
using Jotunn.Utils;
using ExtraPotions.Meadworks.Crucible;
using Wayfarer.CrucibleIntegration;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace Wayfarer {
 [BepInPlugin(Id,"Wayfarer",WayfarerIdentity.Version)]
 [BepInDependency(CruciblePlugin.Guid,CruciblePlugin.Version)]
 // Hard: the Crucible.UI kit is the standard Meadworks UI; it ships in the Crucible package and stays idle on a headless server.
 [BepInDependency(ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Guid,ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Version)]
 [BepInDependency(Jotunn.Main.ModGuid,"2.30.2")]
 [NetworkCompatibility(CompatibilityLevel.ClientMustHaveMod,VersionStrictness.Minor)]
 public sealed class Plugin:BaseUnityPlugin {
  public const string Id="com.meadworks.wayfarer";
  internal static Plugin? Instance;
  internal Settings Options=null!;
  internal readonly SelectionState State=new SelectionState();
  internal DestinationCatalog Catalog=null!;
  internal TeleportService Teleports=null!;
  internal MapUi? Ui;
  MeadworksLog? log;
  Harmony? harmony; float nextScan; float nextError;
  ProductRegistration? registration;
  CrucibleServices? integration;
  internal CrucibleServices Integration=>integration!;
  internal bool CanTeleport=>Options!=null&&Options.Enabled.Value&&integration!=null&&integration.Session.AuthorityReady&&integration.EffectivePolicy.AllowTeleport;
  internal bool CanUse(Category category)=>CanTeleport&&integration!.EffectivePolicy.AllowsCategory(category,integration.TravelCheats);
  /// <summary>The refusal code (and message key) for <paramref name="category"/>: teleport off, destination off, or no travel cheat access.</summary>
  internal string Refusal(Category category)=>integration==null||!integration.Session.AuthorityReady?"authorityunavailable":integration.EffectivePolicy.Refusal(category,integration.TravelCheats)??"unavailable";
  /// <summary>The refusal code when teleport as a whole is refused.</summary>
  internal string TeleportRefusal()=>integration==null||!integration.Session.AuthorityReady?"authorityunavailable":TeleportRefusals.TeleportDisabled;
  /// <summary>Tells the player why a destination is refused: the Mischief gate's own C03 notice for a server-enabled travel cheat, otherwise the cause's own message.</summary>
  internal void Refuse(Category category) {
   if(integration!=null&&TeleportPolicy.IsCheat(category)&&integration.EffectivePolicy.Enables(category)&&!MeadworksMischief.TryUse(Id,CheatKeys.Section+"."+CheatKeys.For(category),MischiefCategory.Travel))return;
   Report(Refusal(category));
  }
  internal bool CanAdvanced=>CanUse(Category.Boat)||CanUse(Category.Grave)||CanUse(Category.Player);
  internal ProductPresentation Presentation=new ProductPresentation("Wayfarer","Meadworks");
  internal long World=>Player.m_localPlayer&&ZNet.instance&&ZNet.instance.GetWorld()!=null?ZNet.instance.GetWorldUID():0;
  internal string Language=>Options.Language.Value!="Auto"?WayfarerLanguages.Normalize(Options.Language.Value):MeadworksTranslations.GameLanguage();
  internal string T(string key)=>integration?.Text(key,Language)??key;
  void Awake() {
   log=MeadworksLog.For(Logger,"Wayfarer");
   try{
    if(!CruciblePlugin.IsReady)throw new InvalidOperationException("Required Crucible is not ready");
    MeadworksConfig.EnsureSchema(Config,1);
    registration=CruciblePlugin.Services.Register(MeadworksIntegration.Metadata());
    integration=new CrucibleServices(registration,Info.Metadata,Config,Logger,Navigation);
   }catch(Exception error){integration?.Dispose();integration=null;registration?.Dispose();registration=null;enabled=false;log.Error("Wayfarer initialization rejected: "+error.GetType().Name+": "+error.Message);return;}
   Options=new Settings(Config,integration!);
   if(!MeadworksHeadless.AllowsUi){InfoMessage("Wayfarer server policy and authority handlers ready");return;}
   Instance=this;Catalog=new DestinationCatalog(this);Teleports=new TeleportService(this);
   Ui=new MapUi(this);
   harmony=new Harmony(Id);harmony.PatchAll(typeof(Plugin).Assembly);
   InfoMessage("Wayfarer "+WayfarerIdentity.Version+" | ExtraPotions | expDARE | Crucible API 0.1 ready");
  }
  NavigationSnapshot Navigation()=>new NavigationSnapshot(integration!=null&&integration.Session.AuthorityReady,CanAdvanced,Enum.GetValues(typeof(Category)).Cast<Category>().Where(CanUse));
  void InfoMessage(string message)=>log?.Info(message);
  void LogError(object error)=>log?.Error(error);
  void Update() {
   if(Options==null||!MeadworksHeadless.AllowsUi)return;
   try {
    State.ChangeWorld(World);State.SetAdvanced(CanAdvanced);State.SetEnabled(CanTeleport);
    if(State.Armed&&!CanUse(Category.Map))State.Close();
    if(Player.m_localPlayer!=null){
     Teleports?.UpdateLanding();
     integration?.Update();
     if(Time.unscaledTime>=nextScan){Catalog.Observe();nextScan=Time.unscaledTime+2;}
     Ui?.Refresh();
    }
   }catch(Exception error){if(Time.unscaledTime>=nextError){LogError(error);nextError=Time.unscaledTime+10;}}
  }
  /// <summary>A player notice through the Crucible channel (C10): the HUD on a graphical client, otherwise the log.</summary>
  internal void Report(string key) {if(!Options.ShowMessages.Value)return;var text=T(key);if(MeadworksHeadless.Channel==NoticeChannel.Hud&&MessageHud.instance!=null)MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft,text);else InfoMessage(text);}
  internal void Error(Exception error) {LogError(error);Report("error");}
  void OnDestroy(){Teleports?.Dispose();Ui?.Destroy();harmony?.UnpatchSelf();integration?.Dispose();registration?.Dispose();if(Instance==this)Instance=null;}
 }
 [HarmonyPatch(typeof(Minimap),"OnMapLeftClick")]
 internal static class MapPatches {
  static bool Prefix(Minimap __instance) {
   var p=Plugin.Instance;if(p==null || !p.Options.Enabled.Value || !p.State.Armed || MapUi.PortalPickerOpen(__instance) || p.Teleports == null)return true;
   try {
     var method=AccessTools.Method(typeof(Minimap),"ScreenToWorldPoint");if(method==null){p.State.Close();p.Report("unavailable");return true;}
    var input=AccessTools.TypeByName("ZInput");var pointer=input==null?null:AccessTools.Property(input,"pointerPosition")?.GetValue(null);
    var point=(Vector3)method.Invoke(__instance,new object[]{pointer is Vector3 v?v:Input.mousePosition});
    p.Teleports.ToMap(point);
     return false;
    }catch(Exception error){p.State.Close();p.Error(error);return true;}
  }
 }
}
