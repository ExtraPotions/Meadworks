// TextsDialog page insertion/sorting pattern adapted from CreatureManager 1.2.5 CreatureCompendiumManager,
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473. Portions Copyright (C) sighsorry1029.
// Changed by expDARE, 2026: actual Bestiary catalog pages, C05 content and exact owned-entry removal; 2026-10-11 all pages in one compact Texts entry. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Bestiary.Text;
using HarmonyLib;
namespace ExtraPotions.Bestiary.Runtime.Compendium;

[HarmonyPatch(typeof(TextsDialog), "UpdateTextsList")]
internal static class BestiaryCompendium
{
    private static readonly Dictionary<TextsDialog, HashSet<TextsDialog.TextInfo>> owned = new();
    private static bool Enabled => Plugin.AllowsUi && Native.Adapters.NativeConfiguration.Active
        && Plugin.Settings?.Flag(BestiarySettings.General + ".Compendium Pages") == true;
    private static void Postfix(TextsDialog __instance)
    {
        if (__instance == null || __instance.m_texts == null) return;
        Remove(__instance);
        if (!Enabled) return;
        var entries = new HashSet<TextsDialog.TextInfo>();
        var sections = new List<string>();
        foreach (var page in CompendiumCatalog.Pages)
        {
            string body = BestiaryText.Text(page.BodyKey);
            if (page.Items)
            {
                var names = page.Prefabs.Select(p => ItemCatalog.All.Single(i => i.Prefab == p)).Select(i => BestiaryText.Text(i.Word));
                body += "\n\n" + string.Format(BestiaryText.Text("compendium_items"), string.Join(", ", names));
            }
            sections.Add("<b>" + BestiaryText.Text(page.TitleKey) + "</b>\n" + body);
        }
        var info = new TextsDialog.TextInfo("Bestiary", string.Join("\n\n", sections));
        __instance.m_texts.Add(info); entries.Add(info);
        owned[__instance] = entries;
        __instance.m_texts.Sort((a, b) => string.Compare(Localization.instance.Localize(a.m_topic), Localization.instance.Localize(b.m_topic), StringComparison.CurrentCultureIgnoreCase));
    }
    private static void Remove(TextsDialog dialog)
    {
        if (!owned.TryGetValue(dialog, out var entries)) return;
        dialog.m_texts?.RemoveAll(info => entries.Contains(info)); owned.Remove(dialog);
    }
    internal static void Clear()
    {
        foreach (var dialog in owned.Keys.ToArray()) if (dialog != null) Remove(dialog);
        owned.Clear();
    }
    internal static void Tick()
    {
        if (owned.Count == 0) return; // nothing to prune or clear; skips the per-frame ToArray
        foreach (var dialog in owned.Keys.Where(d => d == null).ToArray()) owned.Remove(dialog);
        if (!Enabled) Clear();
    }
}
