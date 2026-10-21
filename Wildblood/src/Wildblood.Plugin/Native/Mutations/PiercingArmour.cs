// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: EliteCreaturesReborn 3.22's Piercing mutation, built from its described behavior only.
using ExtraPotions.Wildblood.Adopted.Patches;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Piercing: a hit from it ignores `ignore armour` percent of the struck player's armor. Decided on the player's own
    /// machine, where the game resolves the hit: <see cref="HitPatch"/> opens the cut before the game reads the armor
    /// (<see cref="Begin"/>), the game's own armor read takes it (<see cref="PiercingArmourPatch"/>), and the cut is closed
    /// once the hit is done (<see cref="End"/>), so no other armor read ever sees it. A tamed one pierces no one.
    /// </summary>
    internal static class PiercingArmour
    {
        private static Player? _victim;
        private static float _ignore;

        /// <summary>The share of armor the hit now being resolved ignores, 0 to 1; 0 outside a Piercing hit.</summary>
        public static float Ignoring => _victim != null ? _ignore : 0f;

        public static void Begin(Struck struck)
        {
            _victim = null;
            EliteController? elite = struck.AttackerElite;
            if (!(struck.Victim is Player player) || !struck.Owned || elite == null || !elite.Ready
                || !elite.Traits.Has(Mutation.Piercing) || struck.Attacker == null || struck.Attacker.IsTamed())
            {
                return;
            }
            float ignore = Mathf.Clamp(Enhance.Magnitude(elite.Rules, elite.Traits, Mutation.Piercing, Fields.IgnoreArmour), 0f, 100f);
            if (ignore > 0f)
            {
                _victim = player;
                _ignore = ignore / 100f;
            }
        }

        /// <summary>The game's armor read for <paramref name="player"/>, cut while a Piercing hit on them resolves.</summary>
        public static void Apply(Player player, ref float armour)
        {
            if (_victim != null && ReferenceEquals(player, _victim))
            {
                armour *= 1f - _ignore;
            }
        }

        public static void End() => _victim = null;
    }
}
