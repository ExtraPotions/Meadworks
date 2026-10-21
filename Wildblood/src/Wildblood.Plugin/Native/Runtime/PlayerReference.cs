// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// A reference player maximum health, for the one owner-side decision that needs one - whether a Devouring
    /// creature's per-hit damage has grown past a share of a player's health and it should start hunting players. It is
    /// the highest maximum health among the living players near the creature, read the same way on every machine (a
    /// player's maximum health travels in its ZDO), so the answer does not depend on which machine owns the creature:
    /// a dedicated server, which has no local player, decides as a client owner would. With no player near, the last
    /// value seen stands, and before any, a documented fallback - see DECISIONS.md on why it is what it is.
    /// </summary>
    internal static class PlayerReference
    {
        /// <summary>A mid-progression player's health; the stand-in until a real player is seen on this machine.</summary>
        public const float FallbackHealth = 100f;

        /// <summary>How near a player must be to count: well inside the area any machine that owns the creature holds.</summary>
        private const float Range = 64f;

        private static float _health = FallbackHealth;

        public static float MaxHealthNear(Vector3 position)
        {
            float best = 0f;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (!player.IsDead() && (player.transform.position - position).sqrMagnitude <= Range * Range)
                {
                    best = Mathf.Max(best, player.GetMaxHealth());
                }
            }
            if (best > 0f)
            {
                _health = best;
            }
            return _health;
        }
    }
}
