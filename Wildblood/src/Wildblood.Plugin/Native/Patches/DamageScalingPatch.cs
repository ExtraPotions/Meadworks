// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; the damage the Distance Scaling band gave at the roll multiplies the outgoing hit.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Scales a hit as it lands, on the victim's owner - the single choke point every damage source passes through.
    /// The attacker's traits raise or lower what it deals (star attack, Splintering, Plated's rising bite, Devouring's
    /// eaten damage), summed additively; the victim's Plated mutation cuts what comes in by a flat, capped percentage.
    /// A boss's aspect then multiplies the starred hit (<see cref="AspectDamage"/>): Enraged, Elementalist, Twin and a
    /// Phantom copy on the way out, Shielded on the way in.
    /// Two more attacker-mutation hit tweaks live here because this is the prefix that owns the outgoing hit:
    /// Miasmic adds vanilla Poison when it hits a player, and Devouring zeroes knockback so its prey never tumbles away.
    /// Hits with no attacker - poison clouds, explosions, reflected damage - carry no traits and pass through. A step of
    /// <see cref="HitPatch"/>, before the hit.
    /// </summary>
    public static class DamageScalingPatch
    {
        internal static void Scale(Struck struck)
        {
            HitData? hit = struck.Hit;
            if (hit == null || !struck.Owned)
            {
                return;
            }
            ApplyOutgoing(struck, hit);
            ApplyIncoming(struck.Elite, hit, struck.Victim);
            ApplyAttackerMutations(struck, hit);
        }

        // Attacker-mutation tweaks to the outgoing hit, applied after scaling so they are not multiplied by it.
        private static void ApplyAttackerMutations(Struck struck, HitData hit)
        {
            Character victim = struck.Victim;
            EliteController? controller = struck.AttackerElite;
            if (controller == null || !controller.Ready)
            {
                return;
            }
            if (controller.Traits.Has(Mutation.Devouring))
            {
                hit.m_pushForce = 0f; // NEVER knocks anything back, players included: prey must stay where it was hit
            }
            if (controller.Traits.Has(Mutation.Miasmic) && victim.IsPlayer())
            {
                // Its attacks poison players with the game's own Poison, strength = cloud damage. RPC_Damage converts a
                // hit's poison portion into the SE_Poison itself, exactly as a blob does; creatures/tames are never hit.
                hit.m_damage.m_poison +=
                    Enhance.Magnitude(controller.Rules, controller.Traits, Mutation.Miasmic, Fields.CloudDamage);
            }
        }

        private static void ApplyOutgoing(Struck struck, HitData hit)
        {
            Character? attacker = struck.Attacker;
            EliteController? controller = struck.AttackerElite; // none for a player
            if (attacker == null) return;
            float horde = ExtraPotions.Wildblood.Adopted.Horde.HordeFactors.Attack(attacker, attacker.m_nview != null ? attacker.m_nview.GetZDO() : null);
            if (controller == null || !controller.Ready)
            {
                hit.ApplyModifier(horde);
                return;
            }
            CreatureTraits traits = controller.Traits;
            hit.ApplyModifier(DamageMath.OutgoingMultiplier(controller.Rules, traits, attacker.GetHealthPercentage())
                * TraitStore.GetDistanceDamage(controller.View.GetZDO()) * horde);
            hit.m_damage.m_blunt += DamageMath.DevouredFlatDamage(traits, controller.View.GetZDO());
            AspectDamage.Outgoing(controller, struck.Victim, hit); // after the stars, so an aspect multiplies the starred hit
        }

        private static void ApplyIncoming(EliteController? controller, HitData hit, Character victim)
        {
            if (controller == null || !controller.Ready)
            {
                return;
            }
            float reduction = DamageMath.PlatedReduction(controller.Rules, controller.Traits, victim.GetHealthPercentage());
            if (reduction > 0f)
            {
                hit.ApplyModifier(1f - reduction);
            }
            AspectDamage.Incoming(controller, hit);
        }
    }

    /// <summary>
    /// The game applies its native level factor during Attack.ModifyDamage. Replace that factor at its
    /// producer, so the adopted outgoing table still receives base attack damage. Other damage sources
    /// (clouds, reflected hits, flat devour damage) never lose a factor they did not receive.
    /// </summary>
    [HarmonyPatch(typeof(Attack), "GetLevelDamageFactor")]
    public static class NativeLevelDamagePatch
    {
        private static void Postfix(Attack __instance, ref float __result)
        {
            Character? character = __instance?.m_character;
            if (character == null || character.IsPlayer()) return;
            var view = character.m_nview;
            if (view != null && view.IsValid() && TraitStore.KeepsNativeLevel(view.GetZDO()))
            {
                // Source saved replacement policy is honored once; additive source factors keep vanilla's factor.
                if (ExtraPotions.Wildblood.AdoptedCm.CreatureLevelManager.ReplacesVanillaLevelDamage(character)) __result = 1f;
                return;
            }
            bool active = character.IsBoss() ? WildbloodModules.Active(WildbloodModule.Bosses)
                : WildbloodModules.Active(WildbloodModule.Stars);
            if (!active) return;
            EliteController? controller = character.GetComponent<EliteController>();
            if (controller != null && controller.Ready
                && !RuleState.Active.KeepsLevel(controller.Traits.Stars, character.IsBoss()))
            {
                __result = 1f;
            }
        }
    }

}
