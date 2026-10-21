// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The knockdown, judged on this machine for its own player: the only character it may move, since a player's body is
    /// simulated by their own client, and the one place their dodge is known exactly. A player on their feet within the
    /// shockwave's radius of the impact - across the ground, and not far above or below it - is knocked down: the game's
    /// own stagger (a stumble, turned to face the blow) and a short shove outward. It deals no damage; the blow itself
    /// does that. There are two ways through it: a dodge roll timed across it, rewarded as the game rewards any dodged
    /// hit, or being in the air as it passes, since it runs through the ground.
    /// </summary>
    internal static class ColossalKnockdown
    {
        /// <summary>The shove, as the game's own pushback force: about 2.5 m on an unencumbered player.</summary>
        private const float Shove = 50f;

        public static void Judge(Vector3 point, float radius)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !OnFeet(player) || !Within(player, point, radius))
            {
                return;
            }
            if (player.IsDodgeInvincible())
            {
                player.HitWhileDodging(); // the game's reward for a roll timed through a blow
                return;
            }
            Vector3 away = Away(player, point);
            player.Stagger(away); // this player's own machine owns their body, so it takes effect at once
            player.ApplyPushback(away, Shove);
        }

        // Standing on the ground and free to fall: not in the air (a jump clears the wave), swimming, seated or riding,
        // aboard a ship, teleporting, dead, a ghost, debug-flying, or already stumbling.
        private static bool OnFeet(Player player) =>
            player.IsOnGround() && !player.IsSwimming() && !player.IsAttached() && player.GetStandingOnShip() == null
            && !player.IsTeleporting() && !player.IsDead() && !player.InGhostMode() && !player.IsDebugFlying()
            && !player.IsStaggering();

        // Across the ground within the radius, and within half of it above or below - a slope, not a cliff.
        private static bool Within(Player player, Vector3 point, float radius)
        {
            Vector3 offset = player.transform.position - point;
            return Mathf.Abs(offset.y) <= radius * 0.5f && new Vector2(offset.x, offset.z).magnitude <= radius;
        }

        private static Vector3 Away(Player player, Vector3 point)
        {
            Vector3 away = player.transform.position - point;
            away.y = 0f;
            return away.sqrMagnitude > 0.0001f ? away.normalized : -player.transform.forward;
        }
    }
}
