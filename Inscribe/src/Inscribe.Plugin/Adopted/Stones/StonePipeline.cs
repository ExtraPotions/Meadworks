// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; a promote rune at
// Epic or Legendary refuses with its own message (2026-10-19: Legendary is boss and raid only).
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Items;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// The check pipeline of applying-stones.md section 2, steps 1-12, in order, for one rune click; the first failure refuses. Every step
    /// only reads: the verb's dry run (steps 11-12) works on a copy, and its result is what <see cref="StoneCommit"/>
    /// writes. The confirm gate (13) and the commit (14) follow in <see cref="ConfirmGate"/>.
    /// <para>
    /// Runs on the local client that owns the inventory (Valheim trusts a client with its own inventory). Every check
    /// reads the item or the synced rules only, so a host and a host-less client on a dedicated server refuse
    /// identically, and a client cannot change the odds by editing its files while the server binds them.
    /// </para>
    /// </summary>
    internal static class StonePipeline
    {
        public static StoneResult Evaluate(StoneJob job)
        {
            if (!Runtime.InscribeModules.Active(global::ExtraPotions.Inscribe.Integration.InscribeModule.Rarity)) return StoneResult.Refuse("stone_disabled", job.StoneName);
            return ItemChecks(job) ?? StoneChecks(job) ?? RarityChecks(job) ?? StoneVerbs.Run(job);
        }

        // 1-3: target in the own inventory and the rune within reach (own inventory or the open chest), magic base, not a newer format.
        private static StoneResult? ItemChecks(StoneJob job)
        {
            if (!job.Inventory.ContainsItem(job.Target))
            {
                return StoneResult.Refuse("not_own_inventory");
            }
            if (job.StoneSource == null && job.Supply == null)
            {
                return StoneResult.Refuse("stone_out_of_reach");
            }
            if (!(job.UsesSocketRules ? ItemClasses.IsMagicBase(job.Target) : ItemClasses.IsRuneBase(job.Target)))
            {
                return StoneResult.Refuse("not_magic_base");
            }
            return job.State.IsNewerFormat ? StoneResult.Refuse("newer_format") : null;
        }

        // 4-7: live and enabled rune this build can perform, not sealed, equipped rule.
        private static StoneResult? StoneChecks(StoneJob job)
        {
            if (!StoneVerbs.IsUsable(job.Def))
            {
                return StoneResult.Refuse("stone_disabled", job.StoneName);
            }
            // STN-1: a sealed item refuses every rune.
            if (job.State.IsSealed)
            {
                return StoneResult.Refuse("sealed");
            }
            return job.IsEquipped && !ModSettings.ModifyEquippedItems.Value ? StoneResult.Refuse("equipped") : null;
        }

        // 8-10: known rarity, in applies_to, enough runes in the carried stack (or the table's supply, and its essence).
        private static StoneResult? RarityChecks(StoneJob job)
        {
            if (job.Rarity == null)
            {
                return StoneResult.Refuse("unknown_rarity");
            }
            if (!job.Def!.AppliesToRarity(job.Rarity.Id))
            {
                return job.Def.Verb == Rules.StoneVerb.Promote && StoneVerbs.AtTop(job)
                    ? StoneResult.Refuse("ascension_top", StoneNames.Rarity(job.Rarity))
                    : StoneResult.Refuse("wrong_rarity", job.StoneName, StoneNames.Rarity(job.Rarity));
            }
            if (job.StonesHeld < job.Cost)
            {
                return StoneResult.Refuse("not_enough_stones", job.Cost.ToString(), job.StoneName);
            }
            StoneMessage? shortfall = job.Supply?.Shortfall(job);
            return shortfall != null ? StoneResult.Refuse(shortfall.Id, shortfall.Words) : null;
        }
    }
}
