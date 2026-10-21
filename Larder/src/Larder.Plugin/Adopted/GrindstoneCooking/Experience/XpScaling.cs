// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Adapted from ValheimMods at 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Experience/XpScaling.cs.
// ValheimMods - Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE, 2026: Larder namespace/keys, effective module gates and shared Skills/UI seams;
// native reference interaction, item, food, experience-scope and presentation algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Scales Cooking experience raised inside an <see cref="XpScope"/>: the game's own amount times the Experience
    /// Multiplier, the dish's tier and, the first time the character makes the dish, the Discovery Multiplier. Every
    /// kitchen raise goes Player.RaiseSkill (status-effect modifiers) → Skills.RaiseSkill, so the prefix sits on the
    /// latter and only touches the local player's own skills.
    /// </summary>
    public static class XpScaling
    {
        // The single Crucible Skills hook invokes this callback for the actual owning local player.
        public static float Apply(Player player, float factor) =>
            NativeCookingFeatures.Active && XpScope.Active ? factor * Scale(player) : factor;

        public static float Multiplier => Mathf.Max(0f, ExperienceSettings.Multiplier.Value);

        /// <summary>
        /// The dish's tier: its food value (health + stamina + eitr, or what an intermediate becomes) over the reference
        /// value, clamped to 1..maximum. Always 1 while Tier Scaling is off, and for an unknown dish.
        /// </summary>
        public static float Tier(ItemDrop.ItemData dish)
        {
            if (!ExperienceSettings.TierScaling.Value)
                return 1f;
            float reference = Mathf.Max(1f, ExperienceSettings.TierReferenceValue.Value);
            float maximum = Mathf.Max(1f, ExperienceSettings.TierMaximum.Value);
            return Mathf.Clamp(Kitchen.FoodValue(dish) / reference, 1f, maximum);
        }

        /// <summary>Raises the local player's Cooking by an amount that is already scaled, bypassing any open scope.</summary>
        public static void RaiseScaled(Player player, float factor)
        {
            if (player == null || factor <= 0f)
                return;
            bool paused = XpScope.Pause();
            try
            {
                player.RaiseSkill(CookLevel.Skill, factor);
            }
            finally
            {
                XpScope.Resume(paused);
            }
        }

        /// <summary>
        /// The factor for the open scope. The discovery bonus covers one dish: a multi-craft of n earns n + (D - 1)
        /// dishes' worth, so a station take-off or a single craft is simply multiplied by D.
        /// </summary>
        private static float Scale(Player player)
        {
            float scale = Multiplier * Tier(XpScope.Dish);
            string dish = XpScope.TakeDiscoverable();
            if (dish != null && DishDiscovery.TryRecord(player, dish))
            {
                float discovery = Mathf.Max(1f, ExperienceSettings.DiscoveryMultiplier.Value);
                scale *= 1f + (discovery - 1f) / XpScope.Units;
            }
            return scale;
        }
    }
}
