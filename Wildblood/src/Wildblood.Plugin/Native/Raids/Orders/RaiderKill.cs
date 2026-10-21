// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A stopped raid's raider dying where it stands (features/raids.md, "Stopping"): with its death effect and its body,
    /// through the game's own death on its owner - its health set to nothing and the death check run at once, as a
    /// devoured creature dies - and dropping nothing, its coins included. Its coin share is set to -1 first, the mark
    /// <see cref="RaiderPurse.Forfeit"/> reads as "dies with no loot and no coins". The mutations that would spawn or
    /// pay something at a death are struck from its traits, in memory on this machine, just before it dies - the death
    /// patch and the loot engine read the traits as it dies: Bloated's blast, Splintering's copies and Gilded's purse.
    /// Thieving stays: what it stole is the players' own, never raid loot, so its pouch drops as it does at any death.
    /// Owner only.
    /// </summary>
    internal static class RaiderKill
    {
        /// <summary>The coin share that tells <see cref="RaidCoins"/> and <see cref="RaidLoot"/> this raider dies with no
        /// loot and no coins.</summary>
        public const int NoDrops = -1;

        private static readonly int CoinsKey = RaidKeys.RaiderCoins.GetStableHashCode();

        private static readonly Mutation[] Withheld =
        {
            Mutation.Bloated, Mutation.Splintering, Mutation.Gilded,
        };

        public static void Kill(RaiderSteering raider)
        {
            Character body = raider.Body;
            ZDO? zdo = raider.Zdo;
            if (zdo == null || body.IsDead() || !raider.View.IsOwner())
            {
                return;
            }
            zdo.Set(CoinsKey, NoDrops);
            Withhold(body);
            raider.Leave.Stop(raider);
            body.SetHealth(0f);
            body.CheckDeath();
        }

        private static void Withhold(Character body)
        {
            EliteController? controller = body.GetComponent<EliteController>();
            if (controller == null || !controller.Ready)
            {
                return; // a creature the mod never resolved runs no death mutation anyway
            }
            foreach (Mutation mutation in Withheld)
            {
                controller.Traits.Remove(mutation);
            }
        }
    }
}
