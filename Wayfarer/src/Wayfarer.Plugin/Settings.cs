using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using Wayfarer.Core;
using Wayfarer.CrucibleIntegration;
namespace Wayfarer {
 internal sealed class Settings {
  public readonly ConfigEntry<bool> Enabled,CloseMap,ShowMessages,TombstoneHints;
  public readonly ConfigEntry<string> Language;
  readonly CrucibleServices integration;
  ConfigEntry<T> Bind<T>(ConfigFile config,string section,string key,T value,string description)=>Bind(config,section,key,value,new ConfigDescription(description));
  ConfigEntry<T> Bind<T>(ConfigFile config,string section,string key,T value,ConfigDescription description)=>integration.Bind(config,section,key,value,description);
  public Settings(ConfigFile config,CrucibleServices integration) {
   this.integration=integration;
   Enabled=Bind(config,"General",ConfigurationKeys.Enabled,true,"Master switch for Wayfarer travel and tombstone hints on this client.");
   Language=Bind(config,"General",ConfigurationKeys.Language,"Auto",new ConfigDescription("Auto follows the game. Other supported languages fall back to English.",new AcceptableValueList<string>(WayfarerLanguages.Settings)));
   CloseMap=Bind(config,"General",ConfigurationKeys.CloseMap,true,new ConfigDescription("Close the map after a teleport is accepted.",null,new ConfigDependency(()=>Enabled.Value,()=>"Requires General: Enabled.")));
   ShowMessages=Bind(config,"General",ConfigurationKeys.Messages,true,new ConfigDescription("Show translated screen messages.",null,new ConfigDependency(()=>Enabled.Value,()=>"Requires General: Enabled.")));
   TombstoneHints=Bind(config,"General",ConfigurationKeys.TombstoneHints,Wayfarer.Core.TombstoneHints.DefaultEnabled,new ConfigDescription("Client-side: show the cause of death and the day on tombstones in your game language (adapted from BetterTombstoneHints, MIT). Steps aside while BetterTombstoneHints is installed.",null,new ConfigDependency(()=>Enabled.Value,()=>"Requires General: Enabled.")));
  }
 }
}
