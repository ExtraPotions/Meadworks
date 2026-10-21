// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// <c>dot_duration</c> (Lingering Wounds): burning, poison and frost the player inflicts last X% longer. The target's
    /// owner adds them while it resolves the hit (RPC_Damage → AddFireDamage / AddPoisonDamage / AddFrostDamage), so the
    /// owner stretches them there, by the attacker's published total (<see cref="HitOwnerContext.DotScale"/>):
    /// <list type="bullet">
    /// <item>Burning and poison keep their damage per tick and tick for longer, so a lingering burn deals that much more
    /// in all (the game's ticks do not stop when the damage pool is spent, only when the time is up).</item>
    /// <item>Frost slows for longer, easing off over the longer time.</item>
    /// </list>
    /// Burning spreads newly added fire over its duration, so a stretched burn goes back to the game's duration before
    /// more fire is added and is stretched again only if that hit lingers too (no compounding). Poison and frost set a
    /// fresh duration whenever they take, so they are stretched only when the game applied the new damage.
    /// </summary>
    internal static class DotDuration
    {
        private sealed class Original
        {
            public float Ttl;
        }

        private static readonly ConditionalWeakTable<StatusEffect, Original> Stretched = new ConditionalWeakTable<StatusEffect, Original>();

        public static void BeforeBurn(SE_Burning burning)
        {
            if (Stretched.TryGetValue(burning, out Original original))
            {
                burning.m_ttl = original.Ttl;
                Stretched.Remove(burning);
            }
        }

        public static void AfterBurn(SE_Burning burning)
        {
            float scale = HitOwnerContext.DotScale;
            if (scale <= 1f || burning.NameHash() != SEMan.s_statusEffectBurning || Stretched.TryGetValue(burning, out _))
            {
                return;
            }
            Stretched.Add(burning, new Original { Ttl = burning.m_ttl });
            burning.m_ttl *= scale;
        }

        public static void Stretch(StatusEffect effect)
        {
            float scale = HitOwnerContext.DotScale;
            if (scale > 1f)
            {
                effect.m_ttl *= scale;
            }
        }
    }

    [HarmonyPatch(typeof(SE_Burning), nameof(SE_Burning.AddFireDamage))]
    internal static class BurningDurationPatch
    {
        private static void Prefix(SE_Burning __instance) => DotDuration.BeforeBurn(__instance);

        private static void Postfix(SE_Burning __instance, bool __result)
        {
            if (__result)
            {
                DotDuration.AfterBurn(__instance);
            }
        }
    }

    /// <summary>The game re-times poison only when the new damage is at least what is left (its own condition).</summary>
    [HarmonyPatch(typeof(SE_Poison), nameof(SE_Poison.AddDamage))]
    internal static class PoisonDurationPatch
    {
        private static void Prefix(SE_Poison __instance, float damage, out bool __state) => __state = damage >= __instance.m_damageLeft;

        private static void Postfix(SE_Poison __instance, bool __state)
        {
            if (__state)
            {
                DotDuration.Stretch(__instance);
            }
        }
    }

    /// <summary>Frost takes a new hit only when it lasts longer than what is left; it then restarts its clock.</summary>
    [HarmonyPatch(typeof(SE_Frost), nameof(SE_Frost.AddDamage))]
    internal static class FrostDurationPatch
    {
        private static void Prefix(SE_Frost __instance, out float __state) => __state = __instance.m_ttl - __instance.m_time;

        private static void Postfix(SE_Frost __instance, float __state)
        {
            if (__instance.m_time == 0f && __instance.m_ttl > __state)
            {
                DotDuration.Stretch(__instance);
            }
        }
    }
}
