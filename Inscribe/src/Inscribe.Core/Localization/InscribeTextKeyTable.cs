// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the full key table of the C05 catalog (design 13). English texts follow EliteCrafting's and
// ItemEnhancements' player-facing words where they exist; seven languages, best-effort translations for native review.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExtraPotions.Inscribe.Config;
using ExtraPotions.Inscribe.Rarity;
using ExtraPotions.Inscribe.Runes;

namespace ExtraPotions.Inscribe.Localization
{
    public readonly struct TextKey
    {
        public TextKey(string key, int arguments)
        {
            Key = key;
            Arguments = arguments;
        }

        public string Key { get; }

        /// <summary>Number of <c>{0}</c>-style placeholders, numbered from 0.</summary>
        public int Arguments { get; }

        public override string ToString() => Key;
    }

    public static partial class InscribeTextKeys
    {
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoInscribe = "off_no_inscribe";
        public const string ImportDone = "import_done";
        public const string ImportSyncIncomplete = "import_sync_incomplete";
        public const string ImportRecoveryIncomplete = "import_recovery_incomplete";
        public const string CheatItemCommands = "cheat_item_commands";
        public const string StackableGear = "stackable_gear";

        public static string RarityName(string rarityId) => "rarity_" + rarityId;
        public static string RuneName(string runeId) => "rune_" + runeId;
        public static string RuneDescription(string runeId) => "rune_" + runeId + "_desc";

        /// <summary>A rune pipeline message (<see cref="RuneResult.Refusal"/> or <see cref="RuneResult.Feedback"/>).</summary>
        public static string RuneMessage(string id) => "rune_msg_" + id;

        /// <summary>The removed enhancement's scrolls keep a name and a description while a stack waits to convert.</summary>
        public static string ScrollName(int tier) => "scroll_tier" + tier;
        public static string ScrollDescription(int tier) => "scroll_tier" + tier + "_desc";

        /// <summary>The enhancement conversion's messages (Inscribe 0.2.0).</summary>
        public const string EnhancementConverted = "enhancement_converted";
        public const string ScrollsConverted = "scrolls_converted";
        public const string ConversionNoRoom = "enhancement_convert_no_room";
        public const string ConversionDropped = "enhancement_convert_dropped";

        /// <summary>Keys with fixed names and their placeholder counts.</summary>
        internal static readonly TextKey[] Fixed =
        {
            new TextKey(OffNoCrucible, 0),
            new TextKey(OffNoInscribe, 0),
            new TextKey(ImportDone, 2),
            new TextKey(ImportSyncIncomplete, 0),
            new TextKey(ImportRecoveryIncomplete, 0),
            new TextKey(CheatItemCommands, 0),
            new TextKey(StackableGear, 2),
            new TextKey("step_aside_rarity", 1),
            new TextKey("step_aside_enhancement", 1),
            new TextKey(EnhancementConverted, 4),
            new TextKey(ScrollsConverted, 2),
            new TextKey(ConversionNoRoom, 0),
            new TextKey(ConversionDropped, 0),
            new TextKey("step_aside_all", 0),
            new TextKey("confirm_title", 1),
            new TextKey("confirm_body", 2),
            new TextKey("tooltip_sealed", 0),
            new TextKey("tooltip_dormant", 0),
            new TextKey("tooltip_item_level", 1),
            new TextKey("tooltip_quality", 1),
            new TextKey("tooltip_inscriptions", 1),
            new TextKey("tooltip_sockets", 2),
            new TextKey("gem_salvage_lost", 1),
            new TextKey("gem_replace_chance", 1),
            new TextKey("pick_inscription_title", 1),
            new TextKey("pick_inscription_body", 2),
        };

        /// <summary>Rune messages with their placeholder counts: refusals, then feedback (design B3).</summary>
        internal static readonly (string Id, int Arguments)[] RuneMessages =
        {
            ("not_own_inventory", 0), ("stone_out_of_reach", 0), ("not_magic_base", 0), ("newer_format", 0), ("stone_disabled", 1), ("sealed", 0),
            ("equipped", 0), ("unknown_rarity", 0), ("wrong_rarity", 2), ("not_enough_stones", 2), ("no_eligible_affix", 0), ("affixes_full", 0),
            ("confirm_required", 1), ("affix_at_top", 0), ("affix_is_flag", 0),
            ("gem_swap_kept", 4), ("gem_swap_lost", 4), ("honed", 2), ("ascension_top", 1),
            ("promoted", 2), ("affix_added", 2), ("rerolled", 1), ("stripped", 1), ("corrupt_seal", 1), ("corrupt_add", 2), ("corrupt_chaos", 1),
        };

        /// <summary>Inscriptions MeadWorks adds to the built-in defaults (batch 3): name, tooltip line and short line, one value each.</summary>
        public static readonly string[] MeadworksAffixes =
        {
            "unshaken", "steady_breath", "hardy", "elusive", "warding", "hale", "fleet", "hearthglow", "purifying", "gleaner", "seafarer",
        };

        public static string AffixName(string id) => "affix_" + id;
        public static string AffixLine(string id) => "affix_" + id + "_line";
        public static string AffixShort(string id) => "affix_" + id + "_short";

        /// <summary>
        /// Setting names, help and section headings (Crucible's cfg_&lt;section&gt;__&lt;key&gt;_name/_help and
        /// cfg_&lt;section&gt;_heading) for every entry of <see cref="InscribeSettings.All"/>, in file order. English holds the
        /// key, the code description and the section name; the config keys never change, so "Magic Item Drops" keeps its key
        /// and shows as "Uncommon Or Better Gear Drops".
        /// </summary>
        public static readonly string[] SettingTexts = BuildSettingTexts();

        /// <summary>Crucible's SettingTextKeys rule: lowercase, every run of characters other than ASCII letters and digits becomes one underscore.</summary>
        public static string SettingSlug(string text) => Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

        private static string[] BuildSettingTexts()
        {
            var keys = new List<string>();
            foreach (var setting in InscribeSettings.All)
            {
                var section = "cfg_" + SettingSlug(setting.Section);
                if (!keys.Contains(section + "_heading")) keys.Add(section + "_heading");
                keys.Add(section + "__" + SettingSlug(setting.Key) + "_name");
                keys.Add(section + "__" + SettingSlug(setting.Key) + "_help");
            }
            return keys.ToArray();
        }

        /// <summary>Every key the catalog must hold, in file order.</summary>
        public static IReadOnlyList<TextKey> All { get; } = Fixed
            .Concat(RarityLadder.Default.Rarities.Select(r => new TextKey(RarityName(r.Id), 0)))
            .Concat(RuneCatalog.Default.Runes.SelectMany(r => new[] { new TextKey(RuneName(r.Id), 0), new TextKey(RuneDescription(r.Id), 0) }))
            // The folded runes keep a name and a description while an old stack waits to convert (LegacyRunes).
            .Concat(LegacyRunes.Ids.SelectMany(id => new[] { new TextKey(RuneName(id), 0), new TextKey(RuneDescription(id), 0) }))
            .Concat(RuneMessages.Select(m => new TextKey(RuneMessage(m.Id), m.Arguments)))
            .Concat(Enumerable.Range(1, 4).SelectMany(t => new[] { new TextKey(ScrollName(t), 0), new TextKey(ScrollDescription(t), 0) }))
            .Concat(SettingTexts.Select(k => new TextKey(k, 0)))
            .Concat(BiomeEssences.OriginalIds.Select(id => new TextKey("essence_" + id, 0)))
            .Concat(BiomeEssences.All.Select(e => new TextKey(e.TextKey, 0)))
            .Concat(MeadworksAffixes.SelectMany(a => new[] { new TextKey(AffixName(a), 0), new TextKey(AffixLine(a), 1), new TextKey(AffixShort(a), 1) }))
            .ToArray();
    }
}
