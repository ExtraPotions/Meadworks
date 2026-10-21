// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Cairn's Comfort square in the minimap column (user decisions 2026-10-12 14:54 and 15:27).
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Cairn.Localization;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>
    /// The comfort level over the game's Rested icon, with the Rested time left while the player is rested. Shown with
    /// Cairn's HUD and hidden while the inventory is open, as Mantle's squares above it are. Local and display-only. The square
    /// is Crucible.UI's KitColumnSquare: the column's dresser (Mantle) makes it through meadworks.hud.column_square.
    /// </summary>
    public static class CairnSquares
    {
        static readonly KitColumnSquare comfort = new KitColumnSquare(ColumnSquares.ComfortRank, ColumnSquares.ComfortId);
        static float nextWrite;

        public static void Tick()
        {
            var settings = Plugin.Settings;
            var player = Player.m_localPlayer;
            var show = Plugin.Ready && Plugin.AllowsUi && settings != null && player != null && !player.IsDead() && ObjectDB.instance != null
                && settings.Get(CairnSettings.Hud.Enabled) && !CairnModules.StepAside.SteppedAside(CairnModule.Hud) && !CairnHud.ToggledOff
                && !InventoryGui.IsVisible();
            if (!show) { comfort.Hide(); return; }
            // The values change by the second at most.
            if (Time.unscaledTime < nextWrite && comfort.Rect != null && comfort.Rect.gameObject.activeSelf) return;
            nextWrite = Time.unscaledTime + .25f;
            var rested = player!.GetSEMan().GetStatusEffect(SEMan.s_statusEffectRested);
            var left = rested != null ? rested.GetRemaningTime() : 0f;
            var restedText = ColumnSquares.Rested(left);
            var icon = ObjectDB.instance!.GetStatusEffect(SEMan.s_statusEffectRested);
            comfort.Draw(true, icon != null ? icon.m_icon : null, ColumnSquares.Comfort(player.GetComfortLevel(), left),
                CairnText.Format(CairnTextKeys.SquareComfort, player.GetComfortLevel()),
                restedText.Length > 0 ? CairnText.Format(CairnTextKeys.SquareRested, restedText) : CairnText.Text(CairnTextKeys.SquareNotRested));
        }

        public static void Reset()
        {
            comfort.Dispose(); nextWrite = 0f;
        }
    }
}
