// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Cairn registers its compass markers window and its HUD toggle with Crucible's Meadworks menu (MeadworksMenu:
// the single "Meadworks" pause-menu entry and the F8 menu key).
using System;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnMenu
    {
        public const string Id = "cairn";
        public const string HudId = "cairn.hud";
        static IDisposable? registration, hud;

        /// <summary>Offers the compass markers window and the HUD toggle while Cairn runs on a graphical client. Cairn's settings are in Almanac.</summary>
        public static void Register()
        {
            if (registration != null) return;
            registration = MeadworksMenu.Register(Id, () => CairnText.Text("ui_menu_markers"), CairnSettingsWindow.Open,
                () => Plugin.Ready && Plugin.AllowsUi && !CairnModules.StepAside.SteppedAside(CairnModule.Hud));
            hud = MeadworksMenu.Register(HudId, () => CairnText.Text(CairnHud.ToggledOff ? "ui_menu_hud_show" : "ui_menu_hud_hide"), CairnHud.ToggleFromMenu,
                () => Plugin.Ready && Plugin.AllowsUi && Player.m_localPlayer != null && !CairnModules.StepAside.SteppedAside(CairnModule.Hud));
        }

        public static void Reset()
        {
            registration?.Dispose();
            registration = null;
            hud?.Dispose();
            hud = null;
        }
    }
}
