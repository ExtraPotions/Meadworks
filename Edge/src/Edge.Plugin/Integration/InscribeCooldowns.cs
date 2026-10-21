// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: resolves Inscribe's secondary cooldown factor through Crucible (batch 3, plan step 7).
using System;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Edge.Runtime
{
    /// <summary>
    /// Asks Inscribe, when present, how much the weapon's inscriptions shorten a secondary cooldown
    /// (<see cref="InscribedCooldown"/>). Resolved on each use, so Inscribe loading after Edge or a hot reload is picked up;
    /// a throwing provider is logged once and then treated as absent for the session. Host setting
    /// <c>1 - General / Inscription Cooldown Bonus</c> off (read live) skips it.
    /// </summary>
    internal static class InscribeCooldowns
    {
        private static bool _failed;

        internal static float Apply(Character attacker, ItemDrop.ItemData? weapon, float cooldown)
        {
            if (_failed || cooldown <= 0f || attacker == null || weapon == null) return cooldown;
            if (Plugin.Settings == null || !Plugin.Settings.Get(EdgeSettings.General.InscriptionCooldownBonus)) return cooldown;
            try
            {
                if (!CruciblePlugin.Services.Capabilities.TryResolve<Func<Character, ItemDrop.ItemData, float>>(
                        InscribedCooldown.Capability, InscribedCooldown.Version, out var factor) || factor == null)
                    return cooldown;
                return InscribedCooldown.Apply(cooldown, factor(attacker, weapon));
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log?.Warning("Inscribe secondary cooldowns skipped: " + e.Message);
                return cooldown;
            }
        }
    }
}
