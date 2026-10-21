// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// Stacks a Tethered pair's boss bars, one under the other at full size. The game draws every boss bar in the same
    /// place at the top of the screen, so the two bars would lie on top of each other and only one would show - and the
    /// pair's whole fight is reading both healths. Each frame, after the game has updated its bars, the pair's bars are
    /// set in ZDO id order: the first where the game puts a boss bar, the next a bar's height lower, clear of the first
    /// one's star row. When one falls its bar goes and the other's returns to the top. Drawn locally on each client from
    /// the replicated partner link; nothing is sent.
    /// </summary>
    internal static class TetherBars
    {
        /// <summary>From one bar's place to the next: its name, its bar and its star row, in canvas units.</summary>
        private const float Step = 90f;

        /// <summary>True, with its id, when this character is one of a Tethered pair - read from its ZDO, ready or not.</summary>
        public static bool IsTethered(Character character, out ZDOID id)
        {
            id = ZDOID.None;
            if (!character.IsBoss())
            {
                return false;
            }
            ZNetView nview = character.m_nview;
            ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            id = zdo != null ? zdo.m_uid : ZDOID.None;
            return zdo != null && AspectStore.GetTether(zdo) != ZDOID.None;
        }

        public static void Layout(List<PhantomBars.Bar> bars)
        {
            GameObject? bossHud = EnemyHud.instance != null ? EnemyHud.instance.m_baseHudBoss : null;
            RectTransform? template = bossHud != null ? bossHud.transform as RectTransform : null;
            if (bars.Count == 0 || template == null)
            {
                return;
            }
            bars.Sort(PhantomBars.ById);
            for (int i = 0; i < bars.Count; i++)
            {
                if (bars[i].Gui != null && bars[i].Gui.transform is RectTransform root)
                {
                    PhantomBarShape.Restore(bars[i].Gui, false);
                    PhantomBarShape.Begin(root);
                    Vector2 position = template.anchoredPosition + new Vector2(0f, -Step * i);
                    if (!root.anchoredPosition.Equals(position)) root.anchoredPosition = position;
                    PhantomBarShape.Capture(root, false);
                }
            }
        }
    }
}
