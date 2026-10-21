// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The Devouring bite, resolved on the VICTIM's owner - where RPC_Damage runs and whose ZDO writes stick. A landed
    /// attack on a creature that can be prey - never a player, a boss or a large creature - is an instant kill when the
    /// devourer is off its cooldown, has not yet eaten its one creature per star, and the prey's current health is within
    /// `max prey health` percent of its own (100 by default: no more than its own; see <see cref="DevourLimits"/>; all of
    /// it is read from the two creatures' prefabs and replicated ZDOs, so no routing is needed). Any other landed attack
    /// is an ordinary hit:
    /// <list type="bullet">
    /// <item>The <b>prefix</b> commits the kill before vanilla resolves the hit - it marks the prey with the devourer's id
    /// (so the prey's death path feeds that one devourer, see <see cref="DeathPatch"/>) and fires the sound
    /// tell, once. Marking here, not in the postfix, means a well-fed devourer whose ordinary damage would have finished
    /// the prey outright still routes its death as a devour.</item>
    /// <item>The <b>postfix</b> forces the kill if the prey survived the hit's own damage, by draining it and running the
    /// death check - so it dies this bite with no health bar and no fight, whatever its health was.</item>
    /// </list>
    /// The postfix also handles the reverse role: a player striking the devourer <see cref="DevourBehaviour.Provoke"/>s
    /// it, turning it on that player. Players are never devoured; Devouring's zeroed knockback lives in DamageScalingPatch.
    /// Both halves are steps of <see cref="HitPatch"/>.
    /// </summary>
    public static class DevourHitPatch
    {
        // Before the hit (prey's owner): decide whether this bite is a devour and, if so, commit it - mark the prey and tell.
        internal static void Commit(Struck struck)
        {
            Character prey = struck.Victim;
            if (struck.Hit == null || !struck.Owned || prey.IsDead())
            {
                return;
            }
            if (!DevourLimits.IsPrey(prey))
            {
                return; // a player, a boss or a large creature is never devoured: this is an ordinary hit
            }
            Character? devourer = ReadyDevourer(prey, struck);
            if (devourer != null)
            {
                Devour(prey, prey.m_nview, devourer);
            }
        }

        // The attacker if it is a resolved Devouring creature that is off its cooldown, still hungry, and big enough for
        // this prey (its health read before this hit lands); null otherwise.
        private static Character? ReadyDevourer(Character prey, Struck struck)
        {
            Character? attacker = struck.Attacker;
            EliteController? dc = struck.AttackerElite;
            if (attacker == null || dc == null || !dc.Ready || !dc.Traits.Has(Mutation.Devouring))
            {
                return null;
            }
            return OffCooldown(attacker) && DevourLimits.MayEat(dc, prey) ? attacker : null;
        }

        private static bool OffCooldown(Character devourer)
        {
            ZDO zdo = devourer.m_nview.GetZDO();
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
            return zdo != null && (long)(now * 1000.0) >= TraitStore.GetDevourReadyAt(zdo);
        }

        private static void Devour(Character prey, ZNetView nview, Character devourer)
        {
            TraitStore.MarkDevouredBy(nview.GetZDO(), devourer.GetZDOID());
            // The tell rides the DEVOURER's own ZNetView, so every client that can see the fight hears it at the prey - a
            // distant player hears the creature eaten. Every player receives it; only clients holding the devourer play it.
            CreatureRpc.FireDevour(devourer.m_nview, prey.GetCenterPoint());
            if (Log.Diagnostics)
            {
                Log.Diag($"{devourer.name} devours {prey.name} in one bite");
            }
        }

        // After the hit (prey's owner): force the marked prey's death if its own damage did not, then handle player
        // provocation. The attacker is the one looked up after the reactions, as each half once looked it up itself.
        internal static void Finish(Struck struck)
        {
            HitData? hit = struck.Hit;
            if (hit == null || !struck.Owned)
            {
                return;
            }
            ForceKill(struck.Victim, struck.Victim.m_nview, struck.Attacker);
            Provoke(struck.Victim, hit, struck.Attacker);
        }

        private static void ForceKill(Character prey, ZNetView nview, Character? attacker)
        {
            if (prey.IsDead() || attacker == null || TraitStore.GetDevouredBy(nview.GetZDO()) != attacker.GetZDOID())
            {
                return; // not the bite we just marked as a devour (or the hit's own damage already killed it)
            }
            prey.SetHealth(0f);
            prey.CheckDeath(); // routes through OnDeath with this hit as the last one
        }

        // A player hitting the devourer turns it on them; runs on the devourer's owner, where its AI is simulated.
        private static void Provoke(Character devourer, HitData hit, Character? attacker)
        {
            if (attacker == null || !attacker.IsPlayer() || hit.GetTotalDamage() <= 0f)
            {
                return;
            }
            DevourBehaviour? behaviour = devourer.GetComponent<DevourBehaviour>();
            if (behaviour != null)
            {
                behaviour.Provoke(attacker);
            }
        }
    }
}
