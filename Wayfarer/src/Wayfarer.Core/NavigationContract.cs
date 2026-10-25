using System;
using System.Collections.Generic;
using System.Linq;
namespace Wayfarer.Core {
    public static class WayfarerIdentity { public const string Version="0.5.13",Family="Mischief"; }
    public static class ConfigurationKeys {public const string Enabled="Enabled",Language="Language",CloseMap="CloseMap",Messages="ShowMessages",TombstoneHints="TombstoneHints";}
    /// <summary>Server-authoritative switches: the master switch and Bed travel, which is a feature, not a cheat.</summary>
    public static class ServerConfigurationKeys {public const string Section="Server",AllowTeleport="AllowTeleport",AllowBed="AllowBed";}
    /// <summary>Travel cheats (Crucible Mischief category Travel): the server switches each on; the Mischief policy decides who may use it.</summary>
    public static class CheatKeys {public const string Section="Cheats",Stones="TeleportToStones",Map="TeleportAnywhere",Boat="TeleportToBoat",Grave="TeleportToGrave",Player="TeleportToPlayer";
        /// <summary>The cheat key of a travel destination; null for Bed, which is not a cheat.</summary>
        public static string? For(Category category)=>category==Category.Stones?Stones:category==Category.Map?Map:category==Category.Boat?Boat:category==Category.Grave?Grave:category==Category.Player?Player:null;}
    public interface IWayfarerNavigation { NavigationSnapshot Snapshot(); }
    public sealed class NavigationSnapshot {
        public bool WorldReady {get;}public bool Advanced {get;}public IReadOnlyList<Category> Categories {get;}
        public NavigationSnapshot(bool worldReady,bool advanced,IEnumerable<Category> categories){WorldReady=worldReady;Advanced=advanced;Categories=Array.AsReadOnly(categories.ToArray());}
    }
    public sealed class NavigationProvider:IWayfarerNavigation {
        readonly Func<NavigationSnapshot> snapshot;
        public NavigationProvider(Func<NavigationSnapshot> snapshot){this.snapshot=snapshot??throw new ArgumentNullException(nameof(snapshot));}
        public NavigationSnapshot Snapshot()=>snapshot();
    }
    public sealed class ProductPresentation {public string Title {get;}public string Brand {get;}public ProductPresentation(string title,string brand){Title=title;Brand=brand;}}
}
