// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System;
using System.Linq;
using ExtraPotions.Magpie.Loot;

namespace ExtraPotions.Magpie.Config
{
    /// <summary>
    /// Every Magpie setting of design section 4, in file order. The plugin binds this table through Crucible
    /// (<c>MeadworksConfig.Bind</c> / <c>BindCheat</c>) and reads values at use time. "SP" = SwiftPickup 1.3.3,
    /// "FI" = FloatingItems 1.0.1 (behavior sources). The optional server lock list forces selected Local values without rewriting personal settings.
    /// </summary>
    public static class MagpieSettings
    {
        private const string General = "1 - General", Pickup = "2 - Pickup", Messages = "3 - Messages", Merge = "4 - Merge",
            Float = "5 - Float", Cheats = "6 - Cheats", Interface = "7 - Interface";

        private const SettingScope L = SettingScope.Local, SA = SettingScope.ServerAuthoritative, C = SettingScope.Cheat;

        public static readonly SettingSpec Enabled = new SettingSpec(General, "Enabled", SA, true,
            "Turns all of Magpie on or off. Off: the game's own auto pickup runs and no item is merged, floated or marked.");
        public static readonly SettingSpec PickupEnabled = new SettingSpec(General, "Pickup Enabled", SA, true,
            "Magpie pickup (fast, smooth magnet). Off: the game's own auto pickup runs.");
        public static readonly SettingSpec MergeEnabled = new SettingSpec(General, "Merge Enabled", SA, true,
            "Identical items lying near each other merge into one stack.");
        public static readonly SettingSpec FloatEnabled = new SettingSpec(General, "Float Enabled", SA, true,
            "Dropped items float on water and tar like wood. A change also applies to items already lying in the world.");

        public static readonly SettingSpec AutoPickupAtStart = new SettingSpec(Pickup, "Auto Pickup At Start", L, true,
            "Auto pickup state when the game starts; the game's auto pickup key (and the optional toggle key) changes it in play.");
        public static readonly SettingSpec PickupRange = new SettingSpec(Pickup, "Pickup Range", L, 3.5f,
            "Radius in meters around the player in which items glide in. The server's Max Pickup Range caps it; range bonuses from other mods still scale it.", 0.5, 20);
        public static readonly SettingSpec MagnetSpeed = new SettingSpec(Pickup, "Magnet Speed", L, 15f,
            "Speed in meters per second at which items glide to the player (the game uses 15).", 1, 50);
        public static readonly SettingSpec ScanInterval = new SettingSpec(Pickup, "Scan Interval", L, 0.15f,
            "Seconds between searches for items in range.", 0.05, 1);
        public static readonly SettingSpec MaxPickupsPerScan = new SettingSpec(Pickup, "Max Pickups Per Scan", L, 2,
            "Items taken per search at most; 0 means no limit. Never more than one item per frame.", 0, 20);
        public static readonly SettingSpec IgnoreItems = new SettingSpec(Pickup, "Ignore Items", L, "",
            "Comma-separated prefab names that are never picked up automatically (picking them up by hand still works).");
        public static readonly SettingSpec MaxPickupRange = new SettingSpec(Pickup, "Max Pickup Range", SA, 10f,
            "Largest Pickup Range any player may use, in meters.", 2, 20);

        public static readonly SettingSpec ShowPickupMessages = new SettingSpec(Messages, "Show Pickup Messages", L, true,
            "Shows the top-left message for each item taken automatically.");
        public static readonly SettingSpec GroupPickupMessages = new SettingSpec(Messages, "Group Pickup Messages", L, true,
            "Items of the same kind taken within one second show as one line with the summed amount.");
        public static readonly SettingSpec SkipPickupEffects = new SettingSpec(Messages, "Skip Pickup Effects", L, true,
            "No particles or sound for each item taken automatically.");

        public static readonly SettingSpec MergeRadius = new SettingSpec(Merge, "Merge Radius", SA, 3f,
            "Distance in meters within which identical items merge.", 0.5, 10);
        public static readonly SettingSpec MergeMinCount = new SettingSpec(Merge, "Merge Min Count", SA, 2,
            "Identical items needed within the radius before they merge.", 2, 20);
        public static readonly SettingSpec MaxMergesPerScan = new SettingSpec(Merge, "Max Merges Per Scan", SA, 2,
            "Merges started per scan at most.", 1, 20);
        public static readonly SettingSpec MergeScanInterval = new SettingSpec(Merge, "Merge Scan Interval", SA, 0.5f,
            "Seconds between merge scans.", 0.1, 5);
        public static readonly SettingSpec MergeSettleTime = new SettingSpec(Merge, "Merge Settle Time", SA, 2f,
            "Seconds an item must lie still before it may merge.", 0, 30);
        public static readonly SettingSpec MergeIgnoreItems = new SettingSpec(Merge, "Merge Ignore Items", SA, "",
            "Comma-separated prefab names that never merge.");

        public static readonly SettingSpec ExcludedItems = new SettingSpec(Float, "Excluded Items", SA, "",
            "Comma-separated prefab names that keep sinking, for example \"Coins, IronScrap\". A change also applies to items already lying in the world.");

        public static readonly SettingSpec LootMultiplier = new SettingSpec(Cheats, "Loot Multiplier", C, 1,
            "Cheat: world loot you pick up is multiplied by this number (1 = off). Only drops from creatures, destructibles, rocks, trees, pickables and loot spawners; the extras are not marked as cheated. In multiplayer the server decides.", 1, 20);
        public static readonly SettingSpec LootSources = new SettingSpec(Cheats, "Loot Sources", C, Loot.LootSources.Default,
            "Cheat: which kinds of world loot the multiplier applies to. Player drops, chest and tombstone spills, refunds and station output never count.");
        public static readonly SettingSpec StackableOnly = new SettingSpec(Cheats, "Stackable Only", C, true,
            "Cheat: only stackable loot is multiplied; weapons, armor and other single items stay one item.");

        public static readonly SettingSpec PickupToggleKey = new SettingSpec(Interface, "Pickup Toggle Key", L, "",
            "Optional key that turns auto pickup on or off, like the game's own auto pickup key. Merging and the loot multiplier are not affected.", shortcut: true);

        public static readonly SettingSpec PickupTogglePadChord = new SettingSpec(Interface, "Pickup Toggle Pad Chord", L, "",
            "Optional gamepad chord that toggles auto pickup, for example LB+X. Unbound by default; typing and game windows block it. Merging and loot are unaffected.");

        public static readonly SettingSpec LockedPlayerSettings = new SettingSpec(Interface, "Locked Player Settings", SA, "",
            "Comma-separated Magpie Local setting IDs (Section.Key) that the server forces to its own values. Empty: personal settings remain independent.");

        public static SettingSpec? Find(string? id) => id == null ? null : All.FirstOrDefault(setting => string.Equals(setting.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

        public static IReadOnlyList<SettingSpec> All { get; } = new[]
        {
            Enabled, PickupEnabled, MergeEnabled, FloatEnabled,
            AutoPickupAtStart, PickupRange, MagnetSpeed, ScanInterval, MaxPickupsPerScan, IgnoreItems, MaxPickupRange,
            ShowPickupMessages, GroupPickupMessages, SkipPickupEffects,
            MergeRadius, MergeMinCount, MaxMergesPerScan, MergeScanInterval, MergeSettleTime, MergeIgnoreItems,
            ExcludedItems,
            LootMultiplier, LootSources, StackableOnly,
            PickupToggleKey, PickupTogglePadChord, LockedPlayerSettings,
        };
    }
}
