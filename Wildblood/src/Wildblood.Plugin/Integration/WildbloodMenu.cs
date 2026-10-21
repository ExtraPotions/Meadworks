// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original: the death recap and the lore (the Wildblood page of the game's Compendium) as entries of Crucible's MeadWorks
// menu (F8, Esc > MeadWorks). Their own keys stay settings, unbound by default.
using System;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Wildblood.Runtime;

internal static class WildbloodMenu
{
    internal const string RecapId = "wildblood.recap";
    internal const string LoreId = "wildblood.lore";
    static IDisposable? recap, lore;

    internal static void Tick()
    {
        if (!Plugin.AllowsUi) return;
        recap ??= MeadworksMenu.Register(RecapId, () => WildbloodText.Text("recap_title"), WildbloodRecap.OpenFromMenu, () => WildbloodRecap.MenuAvailable);
        lore ??= MeadworksMenu.Register(LoreId, () => WildbloodText.Text("lore_title"), () => { if (!WildbloodLore.IsOpen) WildbloodLore.Open(); }, () => WildbloodLore.MenuAvailable);
    }

    internal static void Shutdown()
    {
        recap?.Dispose(); recap = null;
        lore?.Dispose(); lore = null;
    }
}
