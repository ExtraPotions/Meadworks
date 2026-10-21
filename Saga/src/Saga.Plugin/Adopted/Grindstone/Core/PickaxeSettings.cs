// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/PickaxeSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 14: the Pickaxes module's master switch, each player's callout preference and the list of plain stone
    /// items, and the order the Pickaxes feature settings are bound in. The features share five sections: Pickaxes
    /// (this one and experience), Seams (clean strikes, Unbroken), Veins (Rich veins, Read the rock, Echo), Pickaxe Perks
    /// (extra ore, wear, splash) and Mine Finds. Gameplay values are synced and lockable; callouts are each player's own.
    /// </summary>
    public static class PickaxeSettings
    {
        public const string Section = "14 - Mining";
        public const string SeamsSection = "15 - Seams";
        public const string VeinsSection = "16 - Veins";
        public const string PerksSection = "17 - Mining Perks";
        public const string FindsSection = "18 - Mine Finds";

        public static NativeSetting<bool> Enabled { get; private set; }
        public static NativeSetting<bool> ShowCallouts { get; private set; }
        public static NativeSetting<string> PlainStoneItems { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            Enabled = config.Bind(Section, "Pickaxes Enabled", true,
                "Turns every Pickaxes feature on or off: seams, veins, splash, perks, finds, experience changes. Off, rocks and ore deposits behave exactly as in the game. Levels are kept either way.");
            ShowCallouts = config.Bind(Section, "Show Callouts", true,
                "Shows words like Clean strike!, the Echo's distance and finds floating above rocks near you.", synced: false);
            PlainStoneItems = config.Bind(Section, "Plain Stone Items", "Stone, Grausten",
                "Item prefab names, comma-separated, that count as plain stone. A rock whose drops hold anything else is an ore deposit: it gets vein stars, extra ore, clean strike drops, more experience and Echo pings.");
            PickaxeExperienceSettings.Initialize(config);
            SeamSettings.Initialize(config);
            VeinSettings.Initialize(config);
            PickaxePerkSettings.Initialize(config);
            MineFindSettings.Initialize(config);
        }

        /// <summary>The Plain Stone Items setting as a set of prefab names, case-insensitive, blanks dropped.</summary>
        public static HashSet<string> ParsePlainStone(string value)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in (value ?? "").Split(','))
            {
                string name = part.Trim();
                if (name.Length > 0)
                    names.Add(name);
            }
            return names;
        }
    }
}
