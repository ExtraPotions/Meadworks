// From CreatureManager 1.2.5 (sighsorry1029), pinned at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// GPL-3.0-only; original LICENSE retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: CreatureCompendiumManager's owned TextsDialog page insertion and selected-page refresh,
// adapted to one combined Wildblood page (mutations, then modifiers, boss aspects and difficulty) from the shared read-only lore content.
// The page is tracked by identity; foreign pages are never touched, no known-text record is written and no raw TMP link/icon
// rendering or sort of the native list is copied. The Wildblood lore entry points open the game's own Compendium on this page.
// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using HarmonyLib;

namespace ExtraPotions.Wildblood.AdoptedCm;

internal static class CreatureCompendium
{
    static readonly OwnedEntries<TextsDialog.TextInfo> Owned = new OwnedEntries<TextsDialog.TextInfo>();
    static TextsDialog? dialog;

    /// <summary>After the game rebuilt its Compendium list: append this session's single Wildblood page.</summary>
    internal static void AddPages(TextsDialog target)
    {
        if (target == null || target.m_texts == null) return;
        // The native rebuild cleared its list, so every earlier page left with it.
        Owned.Forget(); dialog = target;
        if (!WildbloodLore.ContentAvailable || !Tabs.Any(t => WildbloodLoreData.Entries(t).Count > 0)) return;
        var pages = new List<TextsDialog.TextInfo> { new TextsDialog.TextInfo(WildbloodLoreData.CompendiumTopic(), Text()) };
        Owned.Append(target.m_texts, pages);
    }

    /// <summary>Before the game shows a page: refresh an owned page from the live rules.</summary>
    internal static void Refresh(TextsDialog.TextInfo info)
    {
        if (info == null || !Owned.Owns(info)) return;
        info.m_text = WildbloodLore.ContentAvailable ? Text() : WildbloodText.Text("lore_none");
    }

    static readonly LoreTab[] Tabs = { LoreTab.Mutations, LoreTab.Modifiers, LoreTab.Aspects, LoreTab.Difficulty };

    /// <summary>The game's Compendium is open in the visible inventory.</summary>
    internal static bool Showing => InventoryGui.instance != null && InventoryGui.IsVisible()
        && InventoryGui.instance.m_textsDialog != null && InventoryGui.instance.m_textsDialog.gameObject.activeInHierarchy;

    /// <summary>
    /// Opens the game's own Compendium in the already visible inventory and shows the owned Wildblood page (the game's
    /// first page when it is absent). False while the inventory is not up yet.
    /// </summary>
    internal static bool TryShow()
    {
        var gui = InventoryGui.instance;
        if (gui == null || !InventoryGui.IsVisible() || gui.m_textsDialog == null || Player.m_localPlayer == null) return false;
        gui.OnOpenTexts();
        var page = gui.m_textsDialog.m_texts?.FirstOrDefault(Owned.Owns);
        if (page != null) gui.m_textsDialog.ShowText(page);
        return true;
    }

    /// <summary>Closes the inventory the Compendium sits in, as Esc does.</summary>
    internal static void Close() { if (InventoryGui.instance != null) InventoryGui.instance.Hide(); }

    // One section per non-empty tab under its bold heading, each entry under its bold title as Bestiary's single page does;
    // titles are already rich-text-safe literals. Every entry comes from the live rules, so no discovery state can be lost.
    static string Text()
    {
        var sections = Tabs.Where(t => WildbloodLoreData.Entries(t).Count > 0).Select(t => "<size=120%><b>" + LoreBook.Literal(WildbloodText.Text(LoreBook.TabKey(t)), LoreBook.MaxTitle) + "</b></size>\n\n"
            + string.Join("\n\n", WildbloodLoreData.Entries(t).Select(e => "<b>" + e.Title + "</b>\n" + e.Body)) + Note(WildbloodLoreData.Note(t))).ToList();
        return sections.Count > 0 ? string.Join("\n\n\n", sections) : WildbloodText.Text("lore_none");
    }

    static string Note(string note) => note.Length > 0 ? "\n\n<i>" + note + "</i>" : "";

    /// <summary>Product teardown: remove exactly the owned pages and their native list rows from a live dialog.</summary>
    internal static void Release()
    {
        if (dialog != null && dialog.m_texts != null)
        {
            foreach (var info in dialog.m_texts)
                if (Owned.Owns(info) && info.m_listElement != null) UnityEngine.Object.Destroy(info.m_listElement);
            Owned.RemoveFrom(dialog.m_texts);
        }
        Owned.Forget(); dialog = null;
    }
}

[HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.UpdateTextsList))]
internal static class CompendiumListPatch
{
    static void Postfix(TextsDialog __instance) => Guard.Run("Wildblood Compendium pages", () => CreatureCompendium.AddPages(__instance));
}

[HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), new Type[] { typeof(TextsDialog.TextInfo) })]
internal static class CompendiumShowPatch
{
    static void Prefix(TextsDialog.TextInfo text) => Guard.Run("Wildblood Compendium page", () => CreatureCompendium.Refresh(text));
}
