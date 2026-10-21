// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// <c>penetration</c> (Sundering): this weapon's hits ignore X% of the target's resistance to each damage type: of
    /// the reduction a Slightly Resistant (25%), Resistant (50%) or Very Resistant (75%) step would apply, X% is
    /// bypassed. Weaknesses are unchanged, and so are Immune and Ignore (judgement call: an immunity is a rule of the
    /// creature, not a resistance a weapon wears down).
    /// <para>
    /// Multiplayer: the resistances are the target's, known exactly only on its owner (its status effects, a player's
    /// armor), so the owner applies the bypass, right after the game's own resistance step (HitData.ApplyResistance,
    /// inside RPC_Damage). The attacker's client publishes the share of the weapon each swing starts with on its own
    /// player ZDO (<c>ecf_pen</c>, written only when it changes), and the owner reads it from the hit's attacker
    /// (<see cref="HitOwnerContext"/>), clamped to the running rules.
    /// </para>
    /// </summary>
    internal static class Penetration
    {
        private static readonly int Key = "ecf_pen".GetStableHashCode();

        private static bool _saved;
        private static HitData.DamageTypes _before;

        /// <summary>The local player's swing starts with this weapon (its own client owns the player ZDO).</summary>
        public static void Publish(Player player, ItemDrop.ItemData weapon)
        {
            ZDO? zdo = player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner() ? player.m_nview.GetZDO() : null;
            if (zdo == null)
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(weapon);
            float share = sums != null ? sums.Get(EffectKind.Penetration) : 0f;
            if (zdo.GetFloat(Key, 0f) != share)
            {
                zdo.Set(Key, share);
            }
        }

        /// <summary>The share published in the attacker's ZDO, clamped to the running rules (0 for null or a creature's).</summary>
        public static float OfZdo(ZDO? zdo)
        {
            float share = zdo != null ? zdo.GetFloat(Key, 0f) : 0f;
            return float.IsNaN(share) ? 0f : Mathf.Clamp(share, 0f, CombatCaps.Penetration);
        }

        public static void BeforeResistance(HitData hit)
        {
            _saved = HitOwnerContext.ResistBypass > 0f;
            if (_saved)
            {
                _before = hit.m_damage;
            }
        }

        public static void AfterResistance(HitData hit, HitData.DamageModifiers mods)
        {
            if (!_saved)
            {
                return;
            }
            _saved = false;
            float share = HitOwnerContext.ResistBypass;
            ref HitData.DamageTypes d = ref hit.m_damage;
            d.m_blunt = Bypass(_before.m_blunt, d.m_blunt, mods.m_blunt, share);
            d.m_slash = Bypass(_before.m_slash, d.m_slash, mods.m_slash, share);
            d.m_pierce = Bypass(_before.m_pierce, d.m_pierce, mods.m_pierce, share);
            d.m_fire = Bypass(_before.m_fire, d.m_fire, mods.m_fire, share);
            d.m_frost = Bypass(_before.m_frost, d.m_frost, mods.m_frost, share);
            d.m_lightning = Bypass(_before.m_lightning, d.m_lightning, mods.m_lightning, share);
            d.m_poison = Bypass(_before.m_poison, d.m_poison, mods.m_poison, share);
            d.m_spirit = Bypass(_before.m_spirit, d.m_spirit, mods.m_spirit, share);
        }

        // A resisted type keeps kept + (1 - kept) Ã— share of its damage instead of kept; any other keeps the game's result.
        private static float Bypass(float before, float after, HitData.DamageModifier mod, float share)
        {
            float kept = Kept(mod);
            return kept > 0f ? before * (kept + (1f - kept) * share) : after;
        }

        private static float Kept(HitData.DamageModifier mod)
        {
            switch (mod)
            {
                case HitData.DamageModifier.SlightlyResistant: return 0.75f;
                case HitData.DamageModifier.Resistant: return 0.5f;
                case HitData.DamageModifier.VeryResistant: return 0.25f;
                default: return 0f;
            }
        }
    }

    /// <summary>Every resistance step the game applies; acts only inside a penetrating hit's RPC_Damage on the owner.</summary>
    [HarmonyPatch(typeof(HitData), nameof(HitData.ApplyResistance))]
    internal static class ResistanceStepPatch
    {
        private static void Prefix(HitData __instance) => Penetration.BeforeResistance(__instance);

        private static void Postfix(HitData __instance, HitData.DamageModifiers modifiers) =>
            Penetration.AfterResistance(__instance, modifiers);
    }
}
