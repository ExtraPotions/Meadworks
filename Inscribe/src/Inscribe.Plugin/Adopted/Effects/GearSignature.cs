// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// What the last rebuild counted (effects-runtime.md section 3): the local player's counted items in slot order
    /// (<see cref="ItemEffects.EquippedItems"/>, the API's equipment providers included) and each one's item state. An
    /// inventory change rebuilds the aggregate only when this differs - an item moved into or out of a counted slot
    /// (PackPanel's backpack slot is an inventory move, not an equip), or a counted item's state replaced. A pickup, a
    /// craft, an arrow shot or a meal costs one comparison of a handful of references. Local player only, main thread.
    /// </summary>
    internal static class GearSignature
    {
        private static readonly List<ItemDrop.ItemData> Gear = new List<ItemDrop.ItemData>();
        private static readonly List<ItemState> States = new List<ItemState>();
        private static readonly List<ItemDrop.ItemData> Scratch = new List<ItemDrop.ItemData>();

        /// <summary>After a rebuild: remember what it counted.</summary>
        public static void Remember(Player player)
        {
            ItemEffects.EquippedItems(player, Gear);
            States.Clear();
            for (int i = 0; i < Gear.Count; i++)
            {
                States.Add(ItemState.Read(Gear[i]));
            }
        }

        /// <summary>True when the counted items or their states are no longer the ones the last rebuild saw.</summary>
        public static bool Changed(Player player)
        {
            ItemEffects.EquippedItems(player, Scratch);
            if (Scratch.Count != Gear.Count)
            {
                return true;
            }
            for (int i = 0; i < Scratch.Count; i++)
            {
                if (!ReferenceEquals(Scratch[i], Gear[i]) || !ReferenceEquals(ItemState.Read(Scratch[i]), States[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
