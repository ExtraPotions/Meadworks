// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names, defaults and ranges follow design section 4. Names, defaults and ranges of the
// Wayfare 0.3.2 jump and sea gate options (GPL-3.0-or-later) and of PotalMap's documented options (behaviour reference)
// are used as names and values only. Defaults follow the user decisions of 2026-10-08 (U1 to U10 in odd/tasks/threshold.md).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Access;
using static ExtraPotions.Threshold.Config.SettingScope;

namespace ExtraPotions.Threshold.Config
{
    /// <summary>Every Threshold setting in file order, then the product-managed hidden entry. The plugin binds exactly this list.</summary>
    public static class ThresholdSettings
    {
        public static class General
        {
            public const string Section = "1 - General";

            public static readonly ThresholdSetting<bool> Enabled = ThresholdSetting.Toggle(Section, "Enabled", true, ServerAuthoritative, "Whole product. Off: portals pair by tag as in the game.");
            public static readonly ThresholdSetting<TeleportMode> TeleportMode = ThresholdSetting.Choice(Section, "Teleport Mode", Config.TeleportMode.Map, ServerAuthoritative, "Map: integrated map and list. TargetTeleport: named destinations in a dropdown, then Teleport. Default: portals pair by matching tag, as in the game; portal access modes and icons are off. Quick jumps and sea gates work in every mode.");
            public static readonly ThresholdSetting<OreTransportMode> OreTransport = ThresholdSetting.Choice(Section, "Ore Transport", OreTransportMode.FromStoneToWood, ServerAuthoritative, "Routed trips: Blocked keeps ordinary item restrictions; StoneToStone allows restricted items only between portals that allow all items; FromStoneToWood preserves the game's source-portal rule; All allows restricted items from every portal. Hard item blocks and world restrictions remain.");
            public static readonly ThresholdSetting<string> PortalPrefabs = ThresholdSetting.Text(Section, "Portal Prefabs", "", ServerAuthoritative, "Comma list of portal prefab names in the list. Empty: every prefab with a portal component, modded ones included.", SettingText.NameList);
            public static readonly ThresholdSetting<bool> ListUntaggedPortals = ThresholdSetting.Toggle(Section, "List Untagged Portals", true, ServerAuthoritative, "Untagged portals are destinations too, as in Wayfare. Off: only tagged portals and hubs.");
            public static readonly ThresholdSetting<bool> OnlyExploredPortals = ThresholdSetting.Toggle(Section, "Only Explored Portals", false, ServerAuthoritative, "Portals in map areas the player has not explored are hidden from the list and the map.");
            public static readonly ThresholdSetting<string> LockedPlayerSettings = ThresholdSetting.Text(Section, "Locked Player Settings", "", ServerAuthoritative, "Comma list of player settings (Section.Key) the server forces on every player.", SettingText.NameList);
        }

        public static class Hubs
        {
            public const string Section = "2 - Hubs";

            public static readonly ThresholdSetting<ReturnToHubMode> ReturnToHub = ThresholdSetting.Choice(Section, "Return To Hub", ReturnToHubMode.Always, Local, "The Return To Hub button on the travel map (every portal opens the map when walked into). Always: at every portal that is not a hub, to your home hub (set from a hub on the map; no button until one is usable). UnpairedOnly: only at portals without a tag pair. Off: no button.");
            public static readonly ThresholdSetting<HubEditing> HubEditing = ThresholdSetting.Choice(Section, "Hub Editing", Config.HubEditing.Anyone, ServerAuthoritative, "Who may make a portal a hub or clear it.");
            public static readonly ThresholdSetting<int> MaxHubs = ThresholdSetting.Integer(Section, "Max Hubs", 1, 1, 20, ServerAuthoritative, "Named hubs per world. Each tagged portal returns to its nearest hub.");
        }

        public static class Access
        {
            public const string Section = "3 - Access";

            public static readonly ThresholdSetting<bool> AccessModesEnabled = ThresholdSetting.Toggle(Section, "Access Modes Enabled", true, ServerAuthoritative, "Owners may make their portals Private; admins may also make them Admin. Off: every portal is Public.");
            public static readonly ThresholdSetting<AccessMode> DefaultAccessMode = ThresholdSetting.Choice(Section, "Default Access Mode", AccessMode.Public, ServerAuthoritative, "Access mode of a newly built portal.");
        }

        public static class Picker
        {
            public const string Section = "4 - Picker";

            public static readonly ThresholdSetting<SortOrder> Sort = ThresholdSetting.Choice(Section, "Sort", SortOrder.Name, Local, "How portals are ordered inside each group of the picker: Name sorts them alphabetically, Distance puts the nearest first.");
            public static readonly ThresholdSetting<string> TagEditKey = ThresholdSetting.Hotkey(Section, "Tag Edit Key", "E + LeftShift", "On a hub: open the game's tag input instead of the picker.");
            public static readonly ThresholdSetting<string> ModeCycleKey = ThresholdSetting.Hotkey(Section, "Mode Cycle Key", "E + LeftAlt", "On your own portal: cycle its access mode (Shift + E is the tag edit key).");
            public static readonly ThresholdSetting<bool> HighlightStonePortals = ThresholdSetting.Toggle(Section, "Highlight Stone Portals", true, Local, "Portals that let every item through (the game's stone portal) get their own Stone Portals group in the list and gray pins on the maps, so destinations for ore are easy to find. Off: they sort into their access group with the usual colors.");
        }

        public static class Map
        {
            public const string Section = "5 - Map";

            public static readonly ThresholdSetting<bool> UseGameMap = ThresholdSetting.Toggle(Section, "Use Game Map", true, Local, "Show the game's own map image in the picker. Off: a plain fallback map.");
            public static readonly ThresholdSetting<bool> HideUnexploredMap = ThresholdSetting.Toggle(Section, "Hide Unexplored Map", true, Local, "Fallback map only: cover unexplored areas with fog.");
            public static readonly ThresholdSetting<bool> ShowMapLabels = ThresholdSetting.Toggle(Section, "Show Map Labels", true, Local, "Show portal tags beside their pins.");
            public static readonly ThresholdSetting<float> StartZoom = ThresholdSetting.Number(Section, "Start Zoom", 1f, 0.25f, 8f, Local, "Picker map zoom when it opens.");
            public static readonly ThresholdSetting<bool> ShowPortalPins = ThresholdSetting.Toggle(Section, "Show Portal Pins", true, Local, "Show the portals you may use as pins on the ordinary map, apart from your own pins.");
            public static readonly ThresholdSetting<bool> SpreadClusteredPins = ThresholdSetting.Toggle(Section, "Spread Clustered Pins", true, Local, "Spread pins of portals standing close together so they do not overlap.");
            public static readonly ThresholdSetting<string> PortalPinsKey = ThresholdSetting.Hotkey(Section, "Portal Pins Key", "", "On the open large map: show or hide the portal pins (turns Show Portal Pins on or off). Unbound by default; Show Portal Pins is also a setting.");
        }

        public static class Fuel
        {
            public const string Section = "6 - Fuel";

            public static readonly ThresholdSetting<bool> FuelEnabled = ThresholdSetting.Toggle(Section, "Fuel Enabled", false, ServerAuthoritative, "Each trip Threshold routes consumes Fuel Amount of Fuel Item from the traveler's inventory. Ignore Teleport Restrictions waives it.");
            public static readonly ThresholdSetting<string> FuelItem = ThresholdSetting.Text(Section, "Fuel Item", "GreydwarfEye", ServerAuthoritative, "Item prefab name consumed per trip.", v => SettingText.ItemName(v, "GreydwarfEye"));
            public static readonly ThresholdSetting<int> FuelAmount = ThresholdSetting.Integer(Section, "Fuel Amount", 1, 1, 50, ServerAuthoritative, "Items consumed per trip.");
        }

        public static class Jumps
        {
            public const string Section = "7 - Jumps";

            public static readonly ThresholdSetting<bool> QuickPortals = ThresholdSetting.Toggle(Section, "Quick Portals", true, ServerAuthoritative, "Near portals jump quicker than the game's 8 s, down to Quick Portal Seconds.");
            public static readonly ThresholdSetting<float> QuickPortalRange = ThresholdSetting.Number(Section, "Quick Portal Range", 10000f, 10f, 20000f, ServerAuthoritative, "Map meters between two portals at which a jump takes the game's full 8 s.");
            public static readonly ThresholdSetting<float> QuickPortalSeconds = ThresholdSetting.Number(Section, "Quick Portal Seconds", 0.5f, 0f, 8f, ServerAuthoritative, "Seconds a jump takes between portals side by side.");
            public static readonly ThresholdSetting<bool> ScreenOnlyWhenLoading = ThresholdSetting.Toggle(Section, "Screen Only When Loading", true, Local, "A jump into an area already loaded keeps the screen clear.");
            public static readonly ThresholdSetting<bool> QuickAreaLoading = ThresholdSetting.Toggle(Section, "Quick Area Loading", true, Local, "After a long jump the land around you loads as fast as your PC allows.");
            public static readonly ThresholdSetting<float> ArrivalProtectionSeconds = ThresholdSetting.Number(Section, "Arrival Protection Seconds", 3f, 0f, 30f, ServerAuthoritative, "After a portal trip you take no damage and creatures do not target you for this long. 0: off.");
        }

        /// <summary>Bring Tames (T18) and Bring Carts (ideas seen in OdinPlus TeleportEverything; clean-room): in the jumps section.</summary>
        public static class Tames
        {
            public const string Section = Jumps.Section;

            public static readonly ThresholdSetting<bool> BringTames = ThresholdSetting.Toggle(Section, "Bring Tames", false, ServerAuthoritative, "Tames following you (follow command) within Tame Radius come through the portal with you. Off: they stay, as in the game.");
            public static readonly ThresholdSetting<float> TameRadius = ThresholdSetting.Number(Section, "Tame Radius", 10f, 1f, 50f, ServerAuthoritative, "Meters around you in which following tames come along.");
            public static readonly ThresholdSetting<int> MaxTames = ThresholdSetting.Integer(Section, "Max Tames", 5, 1, 20, ServerAuthoritative, "Most tames that come along per trip, nearest first.");
            public static readonly ThresholdSetting<bool> BringCarts = ThresholdSetting.Toggle(Section, "Bring Carts", true, ServerAuthoritative, "The cart you are pulling comes through the portal with you, its load intact, and stands in front of you on arrival. A cart carrying ores or other restricted items follows Ore Transport: by default it passes only from a stone portal. Off: the cart stays, as in the game.");
        }

        public static class SeaGates
        {
            public const string Section = "8 - Sea Gates";

            public static readonly ThresholdSetting<bool> Enabled = ThresholdSetting.Toggle(Section, "Enabled", false, ServerAuthoritative, "Sea gates: two pillars open a portal for ships. Experimental, off by default.");
            public static readonly ThresholdSetting<string> PillarRecipe = ThresholdSetting.Text(Section, "Pillar Recipe", "Stone:10,FineWood:5,GreydwarfEye:5,SurtlingCore:1", ServerAuthoritative, "What one pillar costs, as Item:Amount pairs.", v => SettingText.Recipe(v, "Stone:10,FineWood:5,GreydwarfEye:5,SurtlingCore:1"));
            public static readonly ThresholdSetting<float> MinGateWidth = ThresholdSetting.Number(Section, "Min Gate Width", 10f, 2f, 50f, ServerAuthoritative, "Closest two pillars may stand to pair, in meters.");
            public static readonly ThresholdSetting<float> MaxGateWidth = ThresholdSetting.Number(Section, "Max Gate Width", 15f, 2f, 50f, ServerAuthoritative, "Farthest two pillars may stand to pair, in meters.");
            public static readonly ThresholdSetting<float> MinWaterDepth = ThresholdSetting.Number(Section, "Min Water Depth", 1f, 0f, 20f, ServerAuthoritative, "Water depth a gate needs, in meters.");
            public static readonly ThresholdSetting<float> ProtectionSeconds = ThresholdSetting.Number(Section, "Protection Seconds", 5f, 0f, 60f, ServerAuthoritative, "After a jump, the ship and its crew take no damage for this long.");
            public static readonly ThresholdSetting<float> CrewWaitSeconds = ThresholdSetting.Number(Section, "Crew Wait Seconds", 20f, 0f, 120f, ServerAuthoritative, "How long the crew waits for the ship before being set ashore.");
        }

        public static class Cheats
        {
            public const string Section = "9 - Cheats";

            public static readonly ThresholdSetting<bool> IgnoreTeleportRestrictions = ThresholdSetting.Toggle(Section, "Ignore Teleport Restrictions", false, Cheat, "Carry anything through portals and sea gates, ores and metals included, and travel without fuel. Off by default; the server decides in multiplayer.", safe: false);
            public static readonly ThresholdSetting<bool> TeleportListedItems = ThresholdSetting.Toggle(Section, "Teleport Listed Items", false, Cheat, "Carry the items named in Listed Items through portals Threshold routes, even where the game forbids them. Fuel still applies. Off by default; the server decides in multiplayer.", safe: false);
            public static readonly ThresholdSetting<string> ListedItems = ThresholdSetting.Text(Section, "Listed Items", "CopperOre,Copper,TinOre,Tin,Bronze,CopperScrap,IronScrap,IronOre,Iron,SilverOre,Silver,BlackMetalScrap,BlackMetal,FlametalOre,Flametal,FlametalOreNew,FlametalNew", ServerAuthoritative, "Comma list of item prefab names Teleport Listed Items lets through (default: ores and metals).", SettingText.NameList);
        }

        public static class Internal
        {
            public const string Section = "Internal";

            public static readonly ThresholdSetting<string> ImportedFrom = ThresholdSetting.Text(Section, "Imported From", "", Local, "Converter marker: source configs already imported. Managed by Threshold.", v => (v ?? "").Trim(), hidden: true);
        }

        public static IReadOnlyList<ThresholdSetting> All { get; } = Array.AsReadOnly(new ThresholdSetting[]
        {
            General.Enabled, General.TeleportMode, General.OreTransport, General.PortalPrefabs, General.ListUntaggedPortals, General.OnlyExploredPortals, General.LockedPlayerSettings,
            Hubs.ReturnToHub, Hubs.HubEditing, Hubs.MaxHubs,
            Access.AccessModesEnabled, Access.DefaultAccessMode,
            Picker.Sort, Picker.TagEditKey, Picker.ModeCycleKey, Picker.HighlightStonePortals,
            Map.UseGameMap, Map.HideUnexploredMap, Map.ShowMapLabels, Map.StartZoom, Map.ShowPortalPins, Map.SpreadClusteredPins, Map.PortalPinsKey,
            Fuel.FuelEnabled, Fuel.FuelItem, Fuel.FuelAmount,
            Jumps.QuickPortals, Jumps.QuickPortalRange, Jumps.QuickPortalSeconds, Jumps.ScreenOnlyWhenLoading, Jumps.QuickAreaLoading, Jumps.ArrivalProtectionSeconds,
            Tames.BringTames, Tames.TameRadius, Tames.MaxTames, Tames.BringCarts,
            SeaGates.Enabled, SeaGates.PillarRecipe, SeaGates.MinGateWidth, SeaGates.MaxGateWidth, SeaGates.MinWaterDepth, SeaGates.ProtectionSeconds, SeaGates.CrewWaitSeconds,
            Cheats.IgnoreTeleportRestrictions, Cheats.TeleportListedItems, Cheats.ListedItems,
            Internal.ImportedFrom,
        });

        /// <summary>Ids of the visible Local settings a server may force through Locked Player Settings.</summary>
        public static IReadOnlyCollection<string> LockableLocalIds { get; } =
            new HashSet<string>(All.Where(s => s.Scope == Local && !s.Hidden).Select(s => s.Id), StringComparer.Ordinal);

        /// <summary>The lockable ids named in a Locked Player Settings value, in table order; unknown and non-Local names are ignored.</summary>
        public static IReadOnlyList<string> ParseLocked(string? value)
        {
            var named = new HashSet<string>(NameList(value ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            return All.Where(s => LockableLocalIds.Contains(s.Id) && named.Contains(s.Id)).Select(s => s.Id).ToList();
        }

        /// <summary>Trimmed, non-empty, first occurrence kept, comma-joined.</summary>
        public static string NameList(string value) => SettingText.NameList(value);
    }

    /// <summary>
    /// Pure text rules used by setting initializers. Kept apart from <see cref="ThresholdSettings"/> so that initializing one
    /// settings group never starts the outer class's initializer, which reads every group (on Mono that re-entry saw a half-built group).
    /// </summary>
    internal static class SettingText
    {
        /// <summary>Trimmed, non-empty, first occurrence kept, comma-joined.</summary>
        public static string NameList(string value)
        {
            if (value == null) return "";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var names = new List<string>();
            foreach (var part in value.Split(','))
            {
                var name = part.Trim();
                if (name.Length > 0 && seen.Add(name)) names.Add(name);
            }
            return string.Join(",", names);
        }

        internal static string ItemName(string value, string fallback)
        {
            var name = (value ?? "").Trim();
            return IsPrefabName(name) ? name : fallback;
        }

        /// <summary><c>Item:Amount</c> pairs with prefab names and positive amounts; anything else gives the fallback.</summary>
        internal static string Recipe(string value, string fallback)
        {
            var parts = new List<string>();
            foreach (var pair in (value ?? "").Split(','))
            {
                var bits = pair.Split(':');
                if (bits.Length != 2) return fallback;
                var item = bits[0].Trim();
                if (!IsPrefabName(item) || !int.TryParse(bits[1].Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 1)
                    return fallback;
                parts.Add(item + ":" + amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return parts.Count == 0 ? fallback : string.Join(",", parts);
        }

        private static bool IsPrefabName(string name) =>
            name.Length > 0 && name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '(' || c == ')');
    }
}
