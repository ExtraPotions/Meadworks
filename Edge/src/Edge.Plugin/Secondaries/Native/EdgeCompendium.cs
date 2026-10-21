// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackCompendiumManager.cs (the TextsDialog.UpdateTextsList postfix that appends the page). Source notices
// retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: the page is owned by reference (a foreign page with the same title is never touched),
// refreshed from the live language before it is shown, only while Edge's secondaries run, and removed at shutdown;
// no in-text icons.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries;
using HarmonyLib;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;

internal static class EdgeCompendium
{
    private static TextsDialog? dialog;
    private static TextsDialog.TextInfo? page;

    internal static void AddPage(TextsDialog target)
    {
        // The game rebuilt its list, so the earlier page left with it.
        dialog = target; page = null;
        if (target?.m_texts == null || !EdgeModules.Active(EdgeModule.Secondaries)) return;
        page = new TextsDialog.TextInfo(EdgeText.Text(CompendiumPage.TitleKey), Text());
        target.m_texts.Add(page);
    }

    internal static void Refresh(TextsDialog.TextInfo info)
    {
        if (info == null || !ReferenceEquals(info, page)) return;
        info.m_topic = EdgeText.Text(CompendiumPage.TitleKey);
        info.m_text = Text();
    }

    internal static void Release()
    {
        if (dialog?.m_texts != null && page != null)
        {
            if (page.m_listElement != null) UnityEngine.Object.Destroy(page.m_listElement);
            dialog.m_texts.RemoveAll(info => ReferenceEquals(info, page));
        }
        dialog = null; page = null;
    }

    private static string Text() => CompendiumPage.Text(EdgeText.Text, EdgeModules.Active(EdgeModule.Quickstep));
}

[HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.UpdateTextsList))]
internal static class EdgeCompendiumListPatch
{
    private static void Postfix(TextsDialog __instance) => EdgeCompendium.AddPage(__instance);
}

[HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), new Type[] { typeof(TextsDialog.TextInfo) })]
internal static class EdgeCompendiumShowPatch
{
    private static void Prefix(TextsDialog.TextInfo text) => EdgeCompendium.Refresh(text);
}
