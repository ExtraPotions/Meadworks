// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Bestiary.Catalog;

public sealed class CompendiumPage
{
    public string Id { get; }
    public string TitleKey { get; }
    public string BodyKey { get; }
    public IReadOnlyList<string> Prefabs { get; }
    public bool Items { get; }
    internal CompendiumPage(string id, string titleKey, string bodyKey, IEnumerable<string> prefabs, bool items)
    { Id = id; TitleKey = titleKey; BodyKey = bodyKey; Prefabs = prefabs.ToArray(); Items = items; }
}
public static class CompendiumCatalog
{
    public static IReadOnlyList<CompendiumPage> Pages { get; } = Build();
    public static IReadOnlyList<string> Keys { get; } = Pages.SelectMany(p => new[] { p.TitleKey, p.BodyKey })
        .Where(k => k.StartsWith("compendium_", StringComparison.Ordinal)).Concat(new[] { "compendium_items" }).Distinct().ToArray();
    private static IReadOnlyList<CompendiumPage> Build()
    {
        var pages = CreatureCatalog.All.Select(c => new CompendiumPage(c.Prefab, c.Word ?? "compendium_raised_title",
            "compendium_" + Body(c) + "_body", new[] { c.Prefab }, false)).ToList();
        pages.Add(new CompendiumPage("bone-arsenal", "compendium_arsenal_title", "compendium_arsenal_body",
            ItemCatalog.BoneWeapons.Select(i => i.Prefab).Concat(new[] { "ECP_Spine", "ECP_ArrowBone" }), true));
        pages.Add(new CompendiumPage("bone-crossbow", "item_ecp_bonecrossbow", "compendium_crossbow_body", new[] { "ECP_BoneCrossbow", "ECP_BoltBoneBlunt" }, true));
        pages.Add(new CompendiumPage("executioner-gear", "item_ecp_executionergreataxe", "compendium_greataxe_body", new[] { "ECP_ExecutionerGreataxe", "ECP_ExecutionerAxehead" }, true));
        pages.Add(new CompendiumPage("kraken-gear", "compendium_kraken_gear_title", "compendium_kraken_gear_body", new[] { "ECP_KrakenBeak", "ECP_KrakenMeat", "ECP_KrakenMeatCooked", "ECP_ShieldKraken" }, true));
        return pages;
    }

    /// <summary>ECP body keys drop the four-character prefix. MW keys drop three, so existing ECP keys stay put.</summary>
    private static string Body(CreatureDefinition c)
    {
        if (c.Spawn == SpawnKind.SkeletonDraw && c.Prefab != "ECP_SkeletonCrossbowman") return "arsenal_creature";
        var name = c.Prefab.StartsWith("MW_", StringComparison.Ordinal) ? c.Prefab.Substring(3) : c.Prefab.Substring(4);
        return name.ToLowerInvariant();
    }
}
