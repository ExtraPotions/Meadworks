// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Core/HusbandryXp.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Raises the local player's Husbandry: an amount times the creature's tier (the caller's part) times the Experience
    /// Multiplier. Nothing happens while Husbandry is off. Runs on the player's own client, where skills live.
    /// </summary>
    public static class HusbandryXp
    {
        public static float Multiplier => Mathf.Max(0f, HusbandryExperienceSettings.Multiplier.Value);

        public static void Raise(Player player, float amount)
        {
            if (player == null || player != Player.m_localPlayer || !HusbandrySkill.Active)
                return;
            float scaled = amount * Multiplier;
            if (scaled > 0f)
                player.RaiseSkill(HusbandrySkill.Type, scaled);
        }

        /// <summary><paramref name="perTier"/> (a setting's value) times the tier of <paramref name="creaturePrefab"/>.</summary>
        public static void RaiseForCreature(Player player, float perTier, string creaturePrefab) =>
            Raise(player, Mathf.Max(0f, perTier) * Herd.Tier(creaturePrefab));
    }
}
