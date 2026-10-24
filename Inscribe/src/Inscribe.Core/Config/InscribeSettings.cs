// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names, defaults and ranges follow design section 4; the EliteCrafting and
// ItemEnhancements options they replaced are recorded in the design's Source column (names only).
using System;
using System.Collections.Generic;
using System.Linq;
using static ExtraPotions.Inscribe.Config.SettingScope;

namespace ExtraPotions.Inscribe.Config
{
    /// <summary>Rune confirm gate for confirm-gated runes (design B4, per player).</summary>
    public enum ConfirmMode { HoldShift, Dialog, Off }

    /// <summary>How much of the tooltip block is shown (design E2).</summary>
    public enum TooltipDetail { Compact, Standard, Full }

    /// <summary>
    /// Every Inscribe setting of design section 4 in file order (4.7, the YAML families, are rule files, not settings).
    /// The plugin binds exactly this list; the portable tests compare it with the spec tables. The enhancement settings
    /// (3 - Enhancement and the enhancement entries of 1, 4, 6, 8 and 9) were removed in 0.2.0: their old lines stay in
    /// the cfg file as orphaned entries, which BepInEx keeps and ignores.
    /// </summary>
    public static class InscribeSettings
    {
        private static string Trimmed(string? text) => (text ?? "").Trim();

        public static class General
        {
            public const string Section = "1 - General";

            public static readonly InscribeSetting<bool> Enabled = InscribeSetting.Toggle(Section, "Enabled", true, ServerAuthoritative, "Whole product. Off: items keep and show their state; nothing rolls, drops or applies.");
            public static readonly InscribeSetting<bool> RarityEnabled = InscribeSetting.Toggle(Section, "Rarity Enabled", true, ServerAuthoritative, "Item rarity, inscriptions and runes. Off: nothing new rolls or drops and inscriptions stop applying; existing item data is kept and shown.");
            public static readonly InscribeSetting<bool> InscriptionEffects = InscribeSetting.Toggle(Section, "Inscription Effects", true, ServerAuthoritative, "Off: inscriptions are kept and shown, but none applies.");
            public static readonly InscribeSetting<bool> AccessoryRarity = InscribeSetting.Toggle(Section, "Accessory Rarity", true, ServerAuthoritative,
                "Rings and belts (Mantle) and trinkets can drop with a rarity and take runes. Off: new ones stay Common and runes refuse them; inscriptions they already have are kept and still apply. Host or server; default on. For one kind only, set rolls: false on the ring, belt or trinket class in Inscribe.economy.yml.");
        }

        public static class Runes
        {
            public const string Section = "2 - Runes";

            public static readonly InscribeSetting<bool> ModifyEquippedItems = InscribeSetting.Toggle(Section, "Modify Equipped Items", true, ServerAuthoritative, "Runes may be applied to equipped items.");
            public static readonly InscribeSetting<ConfirmMode> ConfirmDestructiveRunes = InscribeSetting.Choice(Section, "Confirm Destructive Runes", ConfirmMode.HoldShift, Local,
                "How confirm-gated runes (Cleansing, Sealed) ask first: HoldShift, a yes/no Dialog, or Off.");
            public static readonly InscribeSetting<bool> RunesFromSalvage = InscribeSetting.Toggle(Section, "Runes From Salvage", true, ServerAuthoritative,
                "Salvaging an Uncommon, Rare or Epic item (Kindle's Salvage) may give back the Ascension Rune that raised it, one time in four. Legendary items give none.");
            public static readonly InscribeSetting<bool> RuneTable = InscribeSetting.Toggle(Section, "Rune Table", true, ServerAuthoritative,
                "The Rune Table (hammer, Crafting tab): holds runes, turns trophies into essence and applies runes; an essence chooses what the Ascension Rune adds. Off: it cannot be built or used; what it holds is kept.");
            public static readonly InscribeSetting<bool> BiomeEssences = InscribeSetting.Toggle(Section, "Biome Essences", true, ServerAuthoritative,
                "The Rune Table also offers the Mist (Mistlands), Ash (Ashlands) and Frost (Deep North) essences, which steer the Ascension Rune toward movement and stealth; fury, ammunition and thrown weapons; or warmth, food, meads and endurance. Off: only the first five essences are offered; essence in the pool and inscriptions already rolled are kept. Host or server; default on.");
            public static readonly InscribeSetting<bool> GemsAndSockets = InscribeSetting.Toggle(Section, "Gems And Sockets", true, ServerAuthoritative,
                "Weapons, staves, armor, shields and backpacks hold up to three sockets: the Dvergr Chisel cuts them and the eleven gems fill them. Off: the chisel and gems neither drop nor work; socketed gems keep their stats.");
            public static readonly InscribeSetting<float> GemRecoveryCommon = InscribeSetting.Number(Section, "Gem Recovery Common", 100f, 0f, 100f, ServerAuthoritative,
                "Percent chance a gem comes back when it is replaced in a Common item or the item is salvaged; otherwise the gem is lost. Host or server; default 100.");
            public static readonly InscribeSetting<float> GemRecoveryUncommon = InscribeSetting.Number(Section, "Gem Recovery Uncommon", 90f, 0f, 100f, ServerAuthoritative,
                "Percent chance a gem comes back when it is replaced in a Uncommon item or the item is salvaged; otherwise the gem is lost. Host or server; default 90.");
            public static readonly InscribeSetting<float> GemRecoveryRare = InscribeSetting.Number(Section, "Gem Recovery Rare", 75f, 0f, 100f, ServerAuthoritative,
                "Percent chance a gem comes back when it is replaced in a Rare item or the item is salvaged; otherwise the gem is lost. Host or server; default 75.");
            public static readonly InscribeSetting<float> GemRecoveryEpic = InscribeSetting.Number(Section, "Gem Recovery Epic", 50f, 0f, 100f, ServerAuthoritative,
                "Percent chance a gem comes back when it is replaced in a Epic item or the item is salvaged; otherwise the gem is lost. Host or server; default 50.");
            public static readonly InscribeSetting<float> GemRecoveryLegendary = InscribeSetting.Number(Section, "Gem Recovery Legendary", 25f, 0f, 100f, ServerAuthoritative,
                "Percent chance a gem comes back when it is replaced in a Legendary item or the item is salvaged; otherwise the gem is lost. Host or server; default 25.");

            /// <summary>The five Gem Recovery settings, Common first (Rarity/GemRecovery tiers).</summary>
            public static InscribeSetting<float>[] GemRecovery => new[] { GemRecoveryCommon, GemRecoveryUncommon, GemRecoveryRare, GemRecoveryEpic, GemRecoveryLegendary };
        }

        public static class Drops
        {
            public const string Section = "4 - Drops";

            public static readonly InscribeSetting<bool> RuneDrops = InscribeSetting.Toggle(Section, "Rune Drops", true, ServerAuthoritative, "Creatures and bosses drop runes.");
            public static readonly InscribeSetting<bool> MagicItemDrops = InscribeSetting.Toggle(Section, "Magic Item Drops", true, ServerAuthoritative, "Creatures and bosses drop gear of Uncommon rarity or better.");
            public static readonly InscribeSetting<bool> ChestDrops = InscribeSetting.Toggle(Section, "Chest Drops", true, ServerAuthoritative, "World chests roll runes and gear once.");
            public static readonly InscribeSetting<bool> WildbloodStarSynergy = InscribeSetting.Toggle(Section, "Wildblood Star Synergy", false, ServerAuthoritative, "Wildblood's stars raise Inscribe's drop chances.");
            public static readonly InscribeSetting<float> UncommonWeight = InscribeSetting.Number(Section, "Uncommon Drop Weight", 1f, 0f, 5f, ServerAuthoritative,
                "Multiplies how often dropped gear is Uncommon (green), against the drop tables. 1: as the tables say; 0: never. Host or server; the next drop uses it.");
            public static readonly InscribeSetting<float> RareWeight = InscribeSetting.Number(Section, "Rare Drop Weight", 1f, 0f, 5f, ServerAuthoritative,
                "Multiplies how often dropped gear is Rare (blue), against the drop tables. 1: as the tables say; 0: never. Host or server; the next drop uses it.");
            public static readonly InscribeSetting<float> EpicWeight = InscribeSetting.Number(Section, "Epic Drop Weight", 1f, 0f, 5f, ServerAuthoritative,
                "Multiplies how often dropped gear is Epic (purple), against the drop tables. 1: as the tables say; 0: never. Host or server; the next drop uses it.");
            public static readonly InscribeSetting<bool> LegendaryFromBosses = InscribeSetting.Toggle(Section, "Legendary From Wildblood Bosses", true, ServerAuthoritative,
                "A boss kill may drop one Legendary (gold) item, when Wildblood is installed. Legendary never drops from other creatures, chests, crafting or runes. Host or server.");
            public static readonly InscribeSetting<bool> LegendaryFromRaids = InscribeSetting.Toggle(Section, "Legendary From Wildblood Raids", true, ServerAuthoritative,
                "A won Wildblood raid may drop one Legendary (gold) item at the raid site. Host or server.");
            public static readonly InscribeSetting<float> LegendaryBossChance = InscribeSetting.Number(Section, "Legendary Boss Chance", 20f, 0f, 100f, ServerAuthoritative,
                "Percent chance a boss kill drops a Legendary item, while Legendary From Wildblood Bosses is on. Host or server.");
            public static readonly InscribeSetting<float> LegendaryRaidChance = InscribeSetting.Number(Section, "Legendary Raid Chance", 10f, 0f, 100f, ServerAuthoritative,
                "Percent chance a won raid drops a Legendary item, while Legendary From Wildblood Raids is on. Host or server.");
        }

        public static class Commands
        {
            public const string Section = "5 - Commands";

            public static readonly InscribeSetting<bool> ReadOnlyForEveryone = InscribeSetting.Toggle(Section, "Read-Only Commands For Everyone", true, ServerAuthoritative, "Everyone may use inscribe inspect, stats, list and help.");
        }

        public static class Display
        {
            public const string Section = "6 - Display";

            public static readonly InscribeSetting<bool> ColoredItemNames = InscribeSetting.Toggle(Section, "Colored Item Names", true, Local, "Item names show in their rarity color.");
            public static readonly InscribeSetting<TooltipDetail> TooltipDetail = InscribeSetting.Choice(Section, "Tooltip Detail", Config.TooltipDetail.Standard, Local, "Tooltip block: Compact, Standard or Full.");
            public static readonly InscribeSetting<bool> ShowDormant = InscribeSetting.Toggle(Section, "Show Dormant Inscriptions", true, Local, "Inscriptions whose definition is gone show grayed.");
        }

        public static class Glow
        {
            public const string Section = "7 - Ground Glow";

            public static readonly InscribeSetting<bool> GroundGlow = InscribeSetting.Toggle(Section, "Ground Glow", true, Local, "Dropped Uncommon or better gear glows in its rarity color.");
            public static readonly InscribeSetting<float> Intensity = InscribeSetting.Number(Section, "Glow Intensity", 1f, 0f, 3f, Local, "Brightness of the ground glow.");
            public static readonly InscribeSetting<float> Range = InscribeSetting.Number(Section, "Glow Range", 2f, 0.5f, 6f, Local, "Light range in meters.");
            public static readonly InscribeSetting<int> MaxLights = InscribeSetting.Integer(Section, "Glow Max Lights", 25, 0, 100, Local, "Only the nearest this many items hold a live light.");
            public static readonly InscribeSetting<float> RefreshSeconds = InscribeSetting.Number(Section, "Glow Refresh Seconds", 1f, 0.25f, 5f, Local, "How often the nearest items are chosen again.");
            public static readonly InscribeSetting<bool> GlowRunes = InscribeSetting.Toggle(Section, "Glow Runes", false, Local, "Dropped runes glow as well.");
            public static readonly InscribeSetting<bool> LootBeam = InscribeSetting.Toggle(Section, "Loot Beam", true, Local, "Glowing dropped gear also shows a beam of light in its rarity color.");
            public static readonly InscribeSetting<bool> LegendaryGlint = InscribeSetting.Toggle(Section, "Legendary Glint", true, Local,
                "Dropped Legendary gear also shows a small bronze and ivory glint on the ground. Other rarities never glint. This client only.");
        }

        public static class Interface
        {
            public const string Section = "8 - Interface";

            public static readonly InscribeSetting<bool> StationButton = InscribeSetting.Toggle(Section, "Station Button", true, Local, "Retained legacy preference for the removed station button; no effect.");
            public static readonly InscribeSetting<ExtraPotions.Inscribe.Runes.RecastFocus> RecastingFocus = InscribeSetting.Choice(Section, "Recasting Focus", ExtraPotions.Inscribe.Runes.RecastFocus.Unrestricted, Local,
                "Your saved Rune Table category for one new inscription: Unrestricted, Offense, Defense or Utility. Recasting still replaces all inscriptions at the same rune cost and uses the host's rules. Your choice is saved on this computer.");
            public static readonly InscribeSetting<ExtraPotions.Inscribe.Runes.RecastSide> RecastingSide = InscribeSetting.Choice(Section, "Recasting Side", ExtraPotions.Inscribe.Runes.RecastSide.Prefix, Local,
                "Your saved prefix or suffix choice for the focused inscription. Applies only when a Recasting category is selected; the other inscriptions still reroll normally. Your choice is saved on this computer.");
        }

        public static class Cheats
        {
            public const string Section = "9 - Cheats";

            public static readonly InscribeSetting<bool> ItemCommands = InscribeSetting.Toggle(Section, "Item Commands", false, Cheat,
                "Cheat. Enables inscribe give, roll, reroll and cleanse console commands.", safe: false);
        }

        // Deferred so that every nested holder is initialized first (Mono initializes the containing type eagerly).
        private static readonly Lazy<InscribeSetting[]> all = new Lazy<InscribeSetting[]>(() => new InscribeSetting[]
        {
            General.Enabled, General.RarityEnabled, General.InscriptionEffects, General.AccessoryRarity,
            Runes.ModifyEquippedItems, Runes.ConfirmDestructiveRunes, Runes.RunesFromSalvage, Runes.RuneTable, Runes.BiomeEssences, Runes.GemsAndSockets,
            Runes.GemRecoveryCommon, Runes.GemRecoveryUncommon, Runes.GemRecoveryRare, Runes.GemRecoveryEpic, Runes.GemRecoveryLegendary,
            Drops.RuneDrops, Drops.MagicItemDrops, Drops.ChestDrops, Drops.WildbloodStarSynergy,
            Drops.UncommonWeight, Drops.RareWeight, Drops.EpicWeight, Drops.LegendaryFromBosses, Drops.LegendaryFromRaids,
            Drops.LegendaryBossChance, Drops.LegendaryRaidChance,
            Commands.ReadOnlyForEveryone,
            Display.ColoredItemNames, Display.TooltipDetail, Display.ShowDormant,
            Glow.GroundGlow, Glow.Intensity, Glow.Range, Glow.MaxLights, Glow.RefreshSeconds, Glow.GlowRunes, Glow.LootBeam, Glow.LegendaryGlint,
            Interface.RecastingFocus, Interface.RecastingSide,
            Cheats.ItemCommands,
        });

        private static readonly Lazy<Dictionary<string, InscribeSetting>> byId = new Lazy<Dictionary<string, InscribeSetting>>(
            () => all.Value.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every setting in file order.</summary>
        public static IReadOnlyList<InscribeSetting> All => all.Value;

        /// <summary>A setting by <c>Section.Key</c> in any case; null when unknown.</summary>
        public static InscribeSetting? Find(string? id) => id != null && byId.Value.TryGetValue(id.Trim(), out var setting) ? setting : null;
    }
}
