// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Affixes that change a status effect the local player already gets from the game, by editing the player's own
    /// clone of it (SEMan adds a MemberwiseClone per character, so the template and other characters are untouched):
    /// Sealegs keeps the Wet status's regeneration at 100%, Marshstrider shrinks the Tared slow, and the mead affixes
    /// act on a status effect added by drinking. Applied when the status effect is added and again on every rebuild
    /// (an affix put on or taken off while wet), each time from the ObjectDB template's values. Local player only.
    /// </summary>
    internal static class StatusEffectTweaks
    {
        private static bool _consuming;

        /// <summary>Rebuild: re-apply to the Wet and Tared clones the player already has.</summary>
        public static void Refresh(Player player)
        {
            SEMan seman = player.GetSEMan();
            Tweak(seman.GetStatusEffect(SEMan.s_statusEffectWet) as SE_Stats);
            Tweak(seman.GetStatusEffect(SEMan.s_statusEffectTared) as SE_Stats);
        }

        public static void OnAdded(StatusEffect effect)
        {
            if (!(effect is SE_Stats stats))
            {
                return;
            }
            int hash = stats.NameHash();
            if (hash == SEMan.s_statusEffectWet || hash == SEMan.s_statusEffectTared)
            {
                Tweak(stats);
            }
            else if (_consuming)
            {
                Meads.OnDrunk(stats);
            }
        }

        public static void BeginConsume(bool local) => _consuming = local;

        public static void EndConsume() => _consuming = false;

        private static void Tweak(SE_Stats? clone)
        {
            SE_Stats? template = clone != null && ObjectDB.instance != null
                ? ObjectDB.instance.GetStatusEffect(clone.NameHash()) as SE_Stats : null;
            if (template == null || ReferenceEquals(template, clone))
            {
                return;
            }
            AggregateValues v = AggregateHost.Current;
            if (clone!.NameHash() == SEMan.s_statusEffectWet)
            {
                bool dry = v[EffectKind.IgnoreWet] > 0f;
                clone.m_healthRegenMultiplier = dry ? System.Math.Max(1f, template.m_healthRegenMultiplier) : template.m_healthRegenMultiplier;
                clone.m_staminaRegenMultiplier = dry ? System.Math.Max(1f, template.m_staminaRegenMultiplier) : template.m_staminaRegenMultiplier;
                clone.m_eitrRegenMultiplier = dry ? System.Math.Max(1f, template.m_eitrRegenMultiplier) : template.m_eitrRegenMultiplier;
                return;
            }
            float slow = template.m_speedModifier;
            clone.m_speedModifier = slow < 0f ? slow * (1f - v[EffectKind.TerrainSlow]) : slow;
        }
    }

    /// <summary>Every status effect newly added to the local player passes through <see cref="StatusEffectTweaks.OnAdded"/>.</summary>
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect),
        new[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short) })]
    internal static class StatusEffectAddedPatch
    {
        private static void Postfix(SEMan __instance, StatusEffect __result)
        {
            if (__result != null && ReferenceEquals(__instance.m_character, Player.m_localPlayer))
            {
                StatusEffectTweaks.OnAdded(__result);
            }
        }
    }

    /// <summary>Marks the status effects added while the local player consumes an item (a mead) for the mead affixes.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
    internal static class ConsumePatch
    {
        private static void Prefix(Player __instance) => StatusEffectTweaks.BeginConsume(ReferenceEquals(__instance, Player.m_localPlayer));

        private static void Postfix() => StatusEffectTweaks.EndConsume();
    }

    /// <summary>
    /// Purity: burning, poison and frost on the local player run their clock X% faster, so they end sooner and deal
    /// what is left of their damage never; Quench (<see cref="BurningDecay"/>) shares the postfix. The base update runs
    /// for every status effect the owner ticks; the first check is two float reads.
    /// </summary>
    [HarmonyPatch(typeof(StatusEffect), nameof(StatusEffect.UpdateStatusEffect))]
    internal static class DebuffDecayPatch
    {
        private static void Postfix(StatusEffect __instance, float dt)
        {
            AggregateValues v = AggregateHost.Current;
            float decay = v[EffectKind.DebuffDecay];
            float sooner = v[EffectKind.BurningDecay];
            if ((decay <= 0f && sooner <= 0f) || !ReferenceEquals(__instance.m_character, Player.m_localPlayer))
            {
                return;
            }
            if (decay > 0f && (__instance is SE_Burning || __instance is SE_Poison || __instance is SE_Frost))
            {
                __instance.m_time += dt * decay;
            }
            if (sooner > 0f)
            {
                BurningDecay.Apply(__instance, dt, sooner);
            }
        }
    }
}
