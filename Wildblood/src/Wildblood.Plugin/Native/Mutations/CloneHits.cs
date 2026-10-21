// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The two blows Cloning cares about, each decided where the game decides the hit. A decoy's blow does no harm: no
    /// damage of any kind, no push, no stagger, no status effect - stripped on the struck one's owner as it lands (players,
    /// tames and creatures alike), and at the swing itself on the decoy's owner, so it breaks no wall or tree either. It
    /// still lands, so a shield still blocks it and a parry still staggers the decoy: the fight looks real. The hidden
    /// creature's blow that reaches a player - blocked or not, but not dodged - gives it away: decided on that player's
    /// own machine, where the game resolves every hit on them, and sent to the creature's owner, which shows it. That blow
    /// does its full damage.
    /// </summary>
    internal static class CloneHits
    {
        /// <summary>A decoy's blow, emptied of everything that could hurt or move whatever it lands on.</summary>
        public static void Harmless(HitData hit)
        {
            hit.m_damage = new HitData.DamageTypes();
            hit.m_pushForce = 0f;
            hit.m_staggerMultiplier = 0f;
            hit.m_statusEffectHash = 0;
            hit.m_healthReturn = 0f;
            hit.m_eitrAdd = 0f;
        }

        /// <summary>
        /// The struck one's owner, before the hit resolves: a decoy's blow is made harmless here; true when it is instead a
        /// hidden Cloning creature's blow landing on a player of this machine, which gives the creature away.
        /// </summary>
        public static bool Struck(Character victim, HitData hit)
        {
            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker.IsPlayer())
            {
                return false;
            }
            if (CloneStore.IsDecoy(attacker))
            {
                Harmless(hit);
                return false;
            }
            ZNetView view = attacker.m_nview;
            return victim.IsPlayer() && Lands(victim, hit) && view != null && view.IsValid() && CloneStore.Hiding(view.GetZDO());
        }

        // The same tests the game makes before it applies a hit: the victim is here, alive and not mid-teleport, and the
        // blow did not meet a dodge roll's invulnerable frames.
        private static bool Lands(Character victim, HitData hit)
        {
            ZNetView view = victim.m_nview;
            return view != null && view.IsValid() && view.IsOwner() && !victim.IsDead() && !victim.IsTeleporting()
                && !(hit.m_dodgeable && victim.IsDodgeInvincible());
        }

        /// <summary>To the hidden creature's owner, wherever that is: its blow landed, so it shows itself there.</summary>
        public static void Report(Character? creature)
        {
            ZNetView? view = creature != null ? creature.m_nview : null;
            if (view != null && NativeRpcOwnership.OwnsView(view, NativeRpcOwner.CreatureClone))
            {
                view.InvokeRPC(CloneBehaviour.HitRpc); // no target: the creature's owner, delivered here when that is this machine
            }
        }
    }
}
