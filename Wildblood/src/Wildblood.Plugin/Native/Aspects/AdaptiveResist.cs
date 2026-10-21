// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Adaptive's hit hook, called from <see cref="Scaling.AspectDamage.Incoming"/> on the boss's owner for every hit,
    /// after the stars and Plated and before the game's own resistances. In this order, so a player can read it:
    /// <list type="number">
    /// <item>The hit's share of the type the boss resists right now - the colour it is glowing as the hit lands - is
    /// cut by `resist` percent. Fire, poison and spirit are cut before the game turns them into their ticking effects,
    /// so the burn is cut too.</item>
    /// <item>A hit from a player or a tame is then recorded, type by type, as the boss's own resistances will leave it
    /// and before this cut: what the fight is really throwing at the boss, so a type it already shrugs off can never
    /// become the one it adapts to, and adapting never weakens its own hold.</item>
    /// <item>Only then is the dominant type re-read, so a hit is never cut by a type it tipped the balance to itself:
    /// a change takes effect from the next hit.</item>
    /// </list>
    /// </summary>
    internal static class AdaptiveResist
    {
        public static void Apply(EliteController boss, HitData hit)
        {
            AdaptiveBehaviour adaptive = boss.GetComponent<AdaptiveBehaviour>();
            if (adaptive == null)
            {
                return;
            }
            HitData.DamageTypes landing = AfterResistances(boss.Creature, hit);
            AdaptiveType resisted = adaptive.Current;
            if (resisted != AdaptiveType.None)
            {
                AdaptiveTypes.Scale(hit, resisted, AspectMath.Cut(AspectMath.Power(Aspect.Adaptive, Fields.Resist)));
            }
            if (FromPlayerSide(hit.GetAttacker()))
            {
                adaptive.Record(landing);
            }
        }

        /// <summary>The hit's damage after the boss's own resistances: the game's own sum, on a copy.</summary>
        private static HitData.DamageTypes AfterResistances(Character boss, HitData hit)
        {
            HitData probe = hit.Clone();
            probe.ApplyResistance(boss.GetDamageModifiers(boss.GetWeakSpot(hit.m_weakSpot)), out _);
            return probe.m_damage;
        }

        /// <summary>Players and their allies, the tamed creatures fighting beside them. A wild creature or a hit with
        /// no attacker teaches the boss nothing.</summary>
        private static bool FromPlayerSide(Character? attacker) =>
            attacker != null && (attacker.IsPlayer() || attacker.IsTamed());
    }
}
