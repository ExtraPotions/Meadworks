// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Sets <see cref="LootKeys.AllyHit"/> on a creature when a tamed or summoned ally hits it (drops.md section 2), so
    /// players who fight with wolves or skeletons still qualify for our drops. Runs where the game applies the damage:
    /// the damage RPC's prefix (<c>Effects.IncomingDamageDispatch</c>), on the creature's ZDO owner only, as vanilla
    /// acts, so the flag is in the ZDO before a killing blow reaches the death hook. A player's hit ends at the attacker
    /// ZDO's prefab; per hit it costs one ZDO bool read once the flag is set.
    /// </summary>
    internal static class AllyHits
    {
        private static readonly int PlayerPrefab = "Player".GetStableHashCode();

        /// <summary>The owner's hit on a creature; <paramref name="attacker"/> is the attacker's ZDO, already looked up.</summary>
        public static void Mark(ZNetView nview, HitData hit, ZDO? attacker)
        {
            if (attacker == null || attacker.GetPrefab() == PlayerPrefab)
            {
                return;
            }
            ZDO zdo = nview.GetZDO();
            if (LootKeys.AllyHitSet(zdo))
            {
                return;
            }
            Character found = hit.GetAttacker();
            if (found != null && !found.IsPlayer() && LootKeys.IsPlayerAlly(found))
            {
                zdo.Set(LootKeys.AllyHitHash, true);
            }
        }
    }
}
