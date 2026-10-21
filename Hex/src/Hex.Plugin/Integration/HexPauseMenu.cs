// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original: Hex registers its window with Crucible's MeadWorks menu
// (MeadworksMenu): the single "MeadWorks" pause-menu entry and the MeadWorks menu key (F8) list it.
using System;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Hex.Runtime;

internal static class HexPauseMenu
{
    internal const string Id = "hex";
    static IDisposable? registration;

    internal static void Tick()
    {
        var wanted = Plugin.Ready && Plugin.Active && MeadworksHeadless.AllowsUi;
        if (!wanted) { Reset(); return; }
        registration ??= MeadworksMenu.Register(Id, "Hex", Open, () => Plugin.Ready && Plugin.Active && Player.m_localPlayer != null);
    }

    static void Open()
    {
        if (!Plugin.Ready || !Plugin.Active || Player.m_localPlayer == null) return;
        if (Menu.IsVisible()) Menu.instance?.OnClose();
        HexWindow.Open();
    }

    internal static void Reset()
    {
        registration?.Dispose();
        registration = null;
    }
}
