// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/ToolWear.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The durability a melee swing takes off its weapon, and giving part of it back, on the swinging player's own client
    /// (the inventory lives there and is saved with the character, so nothing needs the server). Shared by the
    /// Woodcutting axe wear perk (<see cref="SwingPerks"/>) and the Pickaxes wear perk (<see cref="PickaxePerks"/>).
    /// Attack.DoMeleeAttack takes m_useDurabilityDrain times Game.m_durabilityRate off the weapon once per swing that hits
    /// anything, for players and items that use durability, before it raises the swing's skills; nothing else in the swing
    /// wears it. The value is read again here rather than measured, as the game computes it.
    /// </summary>
    public static class ToolWear
    {
        /// <summary>The durability the game drains from this weapon per swing that hits anything; 0 for items without wear.</summary>
        public static float PerSwing(ItemDrop.ItemData weapon) =>
            weapon != null && weapon.m_shared.m_useDurability ? weapon.m_shared.m_useDurabilityDrain * Game.m_durabilityRate : 0f;

        /// <summary>
        /// Puts <paramref name="share"/> (clamped to 0..1) of one swing's wear back on the weapon, never above its max
        /// durability. Call it after the game drained the swing.
        /// </summary>
        public static void GiveBack(ItemDrop.ItemData weapon, float share)
        {
            float refund = PerSwing(weapon) * Mathf.Clamp01(share);
            if (refund <= 0f)
                return;
            float restored = Mathf.Min(weapon.GetMaxDurability(), weapon.m_durability + refund);
            if (restored > weapon.m_durability)
                weapon.m_durability = restored;
        }
    }
}
