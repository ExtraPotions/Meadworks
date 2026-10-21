// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: provides meadworks.inscribe.secondary_cooldown for Edge (batch 3, plan step 7).
using System;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Interop;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Edge asks, when a secondary attack starts its cooldown and when its tooltip shows one, how much the weapon's
    /// inscriptions shorten it (<see cref="SecondaryCooldown"/>). Only the local player's weapons answer: cooldowns are
    /// kept on the attacker's own client, as Inscribe's swing effects are. Inscription Effects off, an item without
    /// inscriptions or any failure answers 1 (no change).
    /// </summary>
    internal static class SecondaryCooldowns
    {
        public static void Publish(ProductRegistration product) =>
            product.AddCapability<Func<Character, ItemDrop.ItemData, float>>(SecondaryCooldown.Capability, SecondaryCooldown.Version, Multiplier);

        internal static float Multiplier(Character attacker, ItemDrop.ItemData weapon)
        {
            try
            {
                if (attacker == null || weapon == null || !ReferenceEquals(attacker, Player.m_localPlayer)) return 1f;
                ItemLocalSums? sums = ItemLocalCache.Get(weapon);
                return sums == null ? 1f : SecondaryCooldown.Multiplier(sums.Get(EffectKind.AttackSpeed), sums.Get(EffectKind.CastSpeed));
            }
            catch (Exception e)
            {
                Adopted.Core.Log.Warn("secondary cooldown factor failed: " + e.Message);
                return 1f;
            }
        }
    }
}
