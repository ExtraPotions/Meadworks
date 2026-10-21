// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken is a boss (the big health bar), so the game may count it among the bosses in a fight: the count the
    /// world's "no portals while a boss is up" setting reads, which only a boss's death takes back off. Its AI never
    /// raises the count (it cannot be alerted the game's way), but should anything else have, a kraken that leaves
    /// without dying takes itself off it as a death would, so the count can never stick.
    /// </summary>
    internal static class KrakenBoss
    {
        public static void Forget(Character kraken)
        {
            ZDO? zdo = kraken.m_nview != null && kraken.m_nview.IsValid() ? kraken.m_nview.GetZDO() : null;
            if (zdo == null || !zdo.GetBool(ZDOVars.s_bossCount) || ZoneSystem.instance == null)
            {
                return;
            }
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.activeBosses, out float count);
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.activeBosses, Mathf.Max(0f, count - 1f));
            zdo.Set(ZDOVars.s_bossCount, false);
        }
    }
}
