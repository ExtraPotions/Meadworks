using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Wildroot {
 /// <summary>
 /// Wildroot's setting identities and Crucible scopes (C01). Catalog, growth, costs, yields, restrictions, difficulty
 /// and the master switch are world rules: the server owns them in multiplayer and the player in single player. Input,
 /// layout, previews, hover text, language and performance stay with each player. The one cheat is
 /// <see cref="Unrestricted"/>, bound with its safe value and a Mischief category.
 /// </summary>
 public static class WildrootSettings {
  public const string ProductId="com.meadworks.wildroot";
  public const string ProductName="Wildroot";
  /// <summary>Schema 2 (October 20, 2026) moved the snap, replant and old farming-mode keys to unbound; see <see cref="UpgradeShortcut"/>.</summary>
  public const int SchemaVersion=2;
  public const string Enabled="General.Enabled";
  public const string Unrestricted="Gardening.Unrestricted";
  public const bool UnrestrictedSafe=false;
  public const MischiefCategory UnrestrictedCategory=MischiefCategory.World;
  /// <summary>The server's extra-resource definitions as sent to clients (hidden; written by the authority only).</summary>
  public const string ExtraResourcesShared="Catalog.ExtraResourcesShared";
  /// <summary>The server's effective catalog fingerprint; clients compare their own against it.</summary>
  public const string ServerFingerprint="Catalog.ServerFingerprint";
  /// <summary>Keys bound with no key by default although the reference mapping names one: the MeadWorks menu (F8) carries them.
  /// The master switch's reference default F8 is the MeadWorks menu key.</summary>
  public static IReadOnlyCollection<string> UnboundByDefault{get;}=new[]{"General.ToggleKey"};
  /// <summary>
  /// Keys unbound by default since schema 2: snapping and replanting are settings rows, and farming mode is gone. The value
  /// is the earlier default. The reference import still writes the snap and replant keys a player chose in PlantEasily.
  /// </summary>
  public static IReadOnlyDictionary<string,string> EarlierShortcutDefaults{get;}=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Grid.SnapKey","F10"},{"Harvest.ReplantToggleKey","F6"},{"Farm.ToggleKey","F7"},
  };
  /// <summary>
  /// The schema 1 to 2 step for one saved shortcut: true when <paramref name="saved"/> (BepInEx serialized form) is
  /// exactly the earlier default, which then becomes unbound. A key the player changed, or added modifiers to, is kept.
  /// </summary>
  public static bool UpgradeShortcut(string id,string? saved)=>saved!=null&&EarlierShortcutDefaults.TryGetValue(id,out var earlier)&&string.Equals(saved.Trim(),earlier,StringComparison.Ordinal);
  /// <summary>Mapped reference settings that Crucible now owns; import reports them and writes nothing.</summary>
  public static IReadOnlyDictionary<string,string> Superseded{get;}=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Authority.LockConfiguration","Wildroot is required on all peers, so Crucible always locks its server settings in multiplayer; the reference lock switch has no Wildroot equivalent."},
  };
  static readonly string[] ServerPrefixes={"Catalog.Vanilla.","Growth.","Drops.","Restrictions.","Farming.","SeedCrafting."};
  /// <summary>Personal switches inside a server-owned section.</summary>
  static readonly HashSet<string> LocalKeys=new HashSet<string>(StringComparer.Ordinal){FarmingRules.RowPlantingId,FarmingRules.AutoReplantId};
  static readonly HashSet<string> ServerKeys=new HashSet<string>(StringComparer.Ordinal){
   Enabled,"Planting.PreventPartialPlanting","Planting.PreventInvalidPlanting","Planting.UseStamina","Planting.UseDurability",
   "Catalog.ShowPickableSpawners","Catalog.EnableMiscFlora","Catalog.EnableDebris","Catalog.EnableExtraResources","Catalog.SnappableVines","Catalog.DisabledResourceNames",
   ExtraResourcesShared,ServerFingerprint,
  };
  /// <summary>The scope of a canonical "Section.Key" id.</summary>
  public static ConfigScope ScopeOf(string id){
   if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("Expected a setting id.",nameof(id));
   if(string.Equals(id,Unrestricted,StringComparison.Ordinal))return ConfigScope.Cheat;
   if(LocalKeys.Contains(id))return ConfigScope.Local;
   if(ServerKeys.Contains(id))return ConfigScope.ServerAuthoritative;
   foreach(var prefix in ServerPrefixes)if(id.StartsWith(prefix,StringComparison.Ordinal))return ConfigScope.ServerAuthoritative;
   return ConfigScope.Local;
  }
 }

 /// <summary>
 /// How <see cref="WildrootSettings.Unrestricted"/> resolves. Placement is a player's action, so the Mischief gate
 /// (C13, category World) decides per player. Growth runs on whichever peer owns a plant, so it must be the same world
 /// rule everywhere: the server's value once authority is established (C02), the safe value while pending, on a vanilla
 /// server or while Wildroot is off for the session.
 /// </summary>
 public static class WildrootCheats {
  public static bool Placement(AuthorityFacts facts,MischiefPolicy policy,string? platformId,bool value){
   if(facts==null)throw new ArgumentNullException(nameof(facts));if(policy==null)throw new ArgumentNullException(nameof(policy));
   return MischiefGate.Decide(WildrootSettings.UnrestrictedCategory,facts,policy,platformId).Allowed?value:WildrootSettings.UnrestrictedSafe;
  }
  public static bool Growth(AuthorityFacts facts,bool value){
   if(facts==null)throw new ArgumentNullException(nameof(facts));
   return ConfigValues.Resolve(AuthorityGate.Decide(ConfigScope.Cheat,facts),value,WildrootSettings.UnrestrictedSafe);
  }
  /// <summary>Whether Wildroot runs in a world: its master switch and session gate; the menu restores native values while configuration stays editable.</summary>
  public static bool Active(AuthorityFacts facts,bool enabled,bool steppedAside)=>enabled&&!steppedAside&&(facts??throw new ArgumentNullException(nameof(facts))).Role!=SessionRole.Menu&&AuthorityGate.ProductActive(facts,out _);
 }
}
