// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using BepInEx.Configuration;
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Config
{
    /// <summary>
    /// The .cfg holds only what the rule file does not: per-player display preferences, none of which the server ever
    /// locks, because they change only what one player sees. Everything that changes what the game does - star chances,
    /// star power, mutation chances and power, and the lock switch itself - lives in the YAML rule file instead.
    /// </summary>
    public static class Configuration
    {
        public const string Display = "8 - Display (per player)";
        public const string Diagnostics = "9 - Diagnostics";

        public static NativeEntry<bool> ShowTraitNames = null!;
        public static NativeEntry<bool> TraitNamesAboveName = null!;
        public static NativeEntry<bool> Diagnose = null!;
        public static NativeEntry<bool> ColouredStars = null!;
        public static NativeEntry<float> NameplateDistance = null!;
        public static NativeEntry<float> EffectDensity = null!;
        public static NativeEntry<float> SmallStarSize = null!;
        public static NativeEntry<float> LargeStarSize = null!;
        public static NativeEntry<bool> ShowStolenItems = null!;
        public static NativeEntry<float> StolenIconSize = null!;
        public static NativeEntry<bool> ShowDevouredCreatures = null!;
        public static NativeEntry<float> DevouredIconSize = null!;
        public static NativeEntry<bool> ShowBossBoard = null!;
        public static NativeEntry<float> BossBoardSeconds = null!;
        public static NativeEntry<bool> ShowWorldTierOnHud = null!;
        public static NativeEntry<bool> BossHints = null!;

        public static void BindAll(NativeConfiguration config)
        {
            ShowTraitNames = config.Bind(Display, "Show trait names", true,
                "Prepend each mutation's word to a creature's name. Client side; never locked.");
            TraitNamesAboveName = config.Bind(Display, "Trait Names Above Creature Name", true,
                "Show the trait words on their own line above a creature's health bar name instead. Client side; never locked.");
            ColouredStars = config.Bind(Display, "Colored Stars", true,
                "Draw a creature's stars colored one-for-one by its mutations. Client side; never locked.");
            NameplateDistance = config.Bind(Display, "Nameplate distance", 0f,
                "Meters within which a creature's mutation name and colors are drawn. 0 uses the game's own nameplate distance.");
            EffectDensity = config.Bind(Display, "Effect density", 1f,
                new ConfigDescription(
                    "Brightness and density of mutation effects (poison clouds, blast tells, cloaking). 1 is full, "
                    + "0 turns them off for a quieter screen or weaker hardware. Lower values hide the effects only; "
                    + "the poison and blasts still hurt you. Client side; never locked.",
                    new AcceptableValueRange<float>(0f, 1f)));
            BindNameplate(config);
            BindBoardAndTier(config);
            BindHints(config);
            PaletteSettings.Bind(config);
            Recap.RecapSettings.Bind(config);
            BindDiagnostics(config);
        }

        // What a creature's nameplate draws beside its name: the star glyphs, and the icons of what a Thieving creature
        // carries and what a Devouring creature has eaten.
        private static void BindNameplate(NativeConfiguration config)
        {
            SmallStarSize = config.Bind(Display, "Small star size", 1.3f,
                "Size of a small star glyph as a multiple of the size vanilla draws a star at. 1.3 = 30% larger, so a "
                + "single star is not a speck. Client side; never locked.");
            LargeStarSize = config.Bind(Display, "Large star size", 2.2f,
                "Size of a large star glyph (worth five) as a multiple of the size vanilla draws a star at. Client side; "
                + "never locked.");
            ShowStolenItems = config.Bind(Display, "Show stolen items", true,
                "Draw the icons of items a Thieving creature is carrying on its nameplate. Client side; never locked.");
            StolenIconSize = config.Bind(Display, "Stolen item icon size", 1.6f,
                "Size of a stolen-item icon as a multiple of the size vanilla draws a star at. Client side; never locked.");
            ShowDevouredCreatures = config.Bind(Display, "Show devoured creatures", true,
                "Draw an icon on a Devouring creature's nameplate for each creature it has eaten: that creature's trophy, "
                + "or a horned monster head for one that drops none. Client side; never locked.");
            DevouredIconSize = config.Bind(Display, "Devoured creature icon size", 1.6f,
                "Size of a devoured-creature icon as a multiple of the size vanilla draws a star at. Client side; never "
                + "locked.");
        }

        private static void BindBoardAndTier(NativeConfiguration config)
        {
            ShowBossBoard = config.Bind(Display, "Boss damage board", true,
                "When a boss dies, show every player who hurt it and how much health they took off it (its Phantom "
                + "copies included), at the far left of the screen. /damage shows the latest board again. Client "
                + "side; never locked.");
            BossBoardSeconds = config.Bind(Display, "Boss damage board seconds", 60f,
                new ConfigDescription("How long the boss damage board stays on screen. Client side; never locked.",
                    new AcceptableValueRange<float>(5f, 600f)));
            ShowWorldTierOnHud = config.Bind(Display, "Show world tier under minimap", true,
                "With PackPanel installed: show the world tier in a small box under the minimap as well, so it is in view "
                + "without opening the inventory. Hidden anyway while world tiers are off. Client side; never locked.");
        }

        private static void BindHints(NativeConfiguration config)
        {
            BossHints = config.Bind(Display, "Boss hints", true,
                "When Bonemass, Moder, Yagluth or the Elder dies, every player within 100 m feels a pull: the compass "
                + "direction from the boss's altar to the Bog Witch (Bonemass), the ancient forge (Moder), Haldor "
                + "(Yagluth) or Hildir (the Elder). Client side; never locked.");
        }

        // Not a display preference and never locked: a switch that makes the death/split/absorb chain log every step, so
        // a mutation that quietly does nothing can be diagnosed on a live server without a rebuild. Off by default.
        private static void BindDiagnostics(NativeConfiguration config)
        {
            Diagnose = config.Bind(Diagnostics, "Log diagnostics", false,
                "Print step-by-step diagnostics for mutation death events (splintering, bloated, devouring) to the log. "
                + "Off by default; turn it on to see why a mutation is not firing. Client side; never locked.");
            Log.Diagnostics = Diagnose.Value;
            Diagnose.SettingChanged += (_, __) => Log.Diagnostics = Diagnose.Value;
        }
    }
}
