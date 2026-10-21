// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// One read-only content model for the lore window and the Compendium pages. Every number comes from the live rules
// through the existing describers (mutation report, aspect text, modifier compendium text); nothing here rolls traits,
// creates creatures, reads or writes ZDO data or applies an effect.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Wildblood.Adopted.Commands;
using ExtraPotions.Wildblood.Adopted.Display;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Localization;

namespace ExtraPotions.Wildblood.Runtime;

internal sealed class LoreEntry
{
    public LoreEntry(LoreTab tab, string id, string title, string body)
    {
        Tab = tab; Id = id;
        Title = LoreBook.Literal(title, LoreBook.MaxTitle);
        Body = LoreBook.Literal(body, LoreBook.MaxBody);
    }
    public LoreTab Tab { get; }
    public string Id { get; }
    public string Title { get; }
    public string Body { get; }
}

internal static class WildbloodLoreData
{
    static readonly HashSet<string> Keys = new HashSet<string>(WildbloodTextKeys.All.Select(k => k.Key), StringComparer.Ordinal);

    internal static bool TraitsOn => WildbloodModules.Active(WildbloodModule.Traits);
    internal static bool BossesOn => WildbloodModules.Active(WildbloodModule.Bosses);

    static string T(string key) => WildbloodText.Text(key);
    static string Named(string key, string fallback) => Keys.Contains(key) ? T(key) : fallback;

    /// <summary>The biome the examples are read in: where the local player stands, else Meadows.</summary>
    internal static Heightmap.Biome Biome()
    {
        var player = Player.m_localPlayer;
        var biome = player != null ? player.GetCurrentBiome() : Heightmap.Biome.None;
        return biome == Heightmap.Biome.None ? Heightmap.Biome.Meadows : biome;
    }

    /// <summary>A value that changes whenever anything the pages show may have changed.</summary>
    internal static int Version() => HashCode.Combine(RuleState.Active, WildbloodText.LanguageVersion, Biome(), TraitsOn, BossesOn, WorldTier.Current());

    internal static string Note(LoreTab tab) => tab switch
    {
        LoreTab.Mutations => TraitsOn ? WildbloodText.Format("lore_example", global::Localization.instance != null ? global::Localization.instance.Localize("$biome_" + Biome().ToString().ToLowerInvariant()) : Biome().ToString()) + " " + T("lore_prose_note") : T("lore_none"),
        LoreTab.Modifiers => TraitsOn ? T("lore_global") + " " + T("lore_prose_note") : T("lore_none"),
        LoreTab.Aspects => BossesOn ? T("lore_boss_rules") + " " + T("lore_prose_note") : T("lore_none"),
        _ => "",
    };

    internal static IReadOnlyList<LoreEntry> Entries(LoreTab tab) => CreatureLocalization.ReadLore(() => tab switch
    {
        LoreTab.Mutations => TraitsOn ? Mutations() : Array.Empty<LoreEntry>(),
        LoreTab.Modifiers => TraitsOn ? Modifiers() : Array.Empty<LoreEntry>(),
        LoreTab.Aspects => BossesOn ? Aspects() : Array.Empty<LoreEntry>(),
        _ => DifficultyEntries(),
    });

    /// <summary>The title of the single Wildblood page in the game's Compendium ("Wildblood mutations").</summary>
    internal static string CompendiumTopic() => LoreBook.Literal(T("lore_compendium"), LoreBook.MaxTitle);

    static string Safe(Func<string> describe, string fallback)
    {
        try { return describe(); }
        catch (Exception error) { Plugin.NativeLogger?.LogWarning("Wildblood lore entry " + fallback + ": " + error.GetType().Name); return fallback; }
    }

    static IReadOnlyList<LoreEntry> Mutations()
    {
        RuleSet set = RuleState.Active;
        BiomeRules rules = set.For(Biome());
        var list = new List<LoreEntry>(MutationCatalog.InOrder.Length);
        foreach (Mutation m in MutationCatalog.InOrder)
        {
            // A plain 1-star example: no creature exists, nothing is rolled or stored.
            var example = new CreatureTraits(1, 1 << (int)m);
            string body = Safe(() => MutationReport.Line(rules, example, m), MutationCatalog.Word(m));
            if (!set.IsEnabled(m)) body = T("lore_disabled") + " " + body;
            list.Add(new LoreEntry(LoreTab.Mutations, m.ToString(), Named("mutation_" + m.ToString().ToLowerInvariant(), MutationCatalog.Word(m)), body));
        }
        return list;
    }

    static IReadOnlyList<LoreEntry> Modifiers()
    {
        Dictionary<string, ModifierDefinition> global = CreatureLevelManager.GetGlobalModifierDefinitions();
        var list = new List<LoreEntry>();
        foreach (string key in CreatureModifierManager.GetKnownModifierKeys())
        {
            bool on = global.TryGetValue(key, out ModifierDefinition definition);
            ModifierDefinition shown = on ? definition : new ModifierDefinition();
            string body = Safe(() => CreatureModifierManager.GetModifierCompendiumText(key, shown), key);
            if (!on) body = T("lore_disabled") + " " + body;
            string id = string.Equals(key, "juggernaut", StringComparison.OrdinalIgnoreCase) ? "crushing" : key.ToLowerInvariant();
            list.Add(new LoreEntry(LoreTab.Modifiers, key, Named("modifier_" + id, CreatureModifierManager.GetModifierDisplayName(key)), body));
        }
        return list;
    }

    static IReadOnlyList<LoreEntry> Aspects()
    {
        AspectRules rules = RuleState.Active.Boss.Aspects;
        return AspectCatalog.Outcomes.Where(a => a != Aspect.None).Select(a => new LoreEntry(LoreTab.Aspects, a.ToString(),
            Named("aspect_" + a.ToString().ToLowerInvariant(), a.ToString()), Safe(() => AspectText.Describe(a, rules), a.ToString()))).ToArray();
    }

    static IReadOnlyList<LoreEntry> DifficultyEntries()
    {
        RuleSet set = RuleState.Active;
        Heightmap.Biome biome = Biome();
        string preset = Named("difficulty_" + set.Difficulty.ToString().ToLowerInvariant(), set.Difficulty.ToString());
        var list = new List<LoreEntry>
        {
            new LoreEntry(LoreTab.Difficulty, "preset", T("lore_tab_difficulty"), WildbloodText.Format("lore_difficulty", preset)),
            new LoreEntry(LoreTab.Difficulty, "tier", T("tier_plate_title"), WildbloodText.Format("world_tier_label", WorldTier.Current())),
            new LoreEntry(LoreTab.Difficulty, "stars", T("lore_tab_difficulty"), set.CreatureStars ? WildbloodText.Format("lore_stars", set.MaxStars) : T("lore_stars_off")),
            new LoreEntry(LoreTab.Difficulty, "mutations", T("lore_tab_mutations"), WildbloodText.Format("lore_mutations", set.MaxMutations)),
        };
        // Custom reads the biome rows directly; a preset derives its odds from the world tier instead.
        if (set.Difficulty == Adopted.Rules.Difficulty.Custom)
        {
            float[] chances = set.For(biome).StarChances;
            list.Add(new LoreEntry(LoreTab.Difficulty, "chances", biome.ToString(), WildbloodText.Format("lore_star_chances", biome.ToString(),
                string.Join(" / ", chances.Take(16).Select(c => c.ToString("0.#", CultureInfo.CurrentCulture) + "%")))));
        }
        return list;
    }
}
