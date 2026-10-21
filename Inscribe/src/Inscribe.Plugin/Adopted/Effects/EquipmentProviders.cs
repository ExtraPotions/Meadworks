// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The API's equipment providers (api.md section 5): items a player wears outside the game's equipment slots
    /// (PackPanel's backpack slot). <see cref="ItemEffects.EquippedItems"/> adds their answer after the game's slots, so
    /// their inscriptions count like equipped items for the local player (the only player whose totals this peer
    /// computes). An item already counted is not added twice; a provider that throws counts as no answer.
    /// Main thread only.
    /// </summary>
    internal static class EquipmentProviders
    {
        public static readonly Callbacks<Func<Player, List<ItemDrop.ItemData>>> Providers =
            new Callbacks<Func<Player, List<ItemDrop.ItemData>>>("equipment provider");

        /// <summary>Adds every provider's items for <paramref name="player"/> that are not in <paramref name="into"/> yet.</summary>
        public static void AddTo(Player player, List<ItemDrop.ItemData> into)
        {
            foreach (Callbacks<Func<Player, List<ItemDrop.ItemData>>>.Entry provider in Providers.Snapshot)
            {
                List<ItemDrop.ItemData>? items = Ask(provider, player);
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    if (items[i] != null && !into.Contains(items[i]))
                    {
                        into.Add(items[i]);
                    }
                }
            }
        }

        /// <summary>Whether a provider names this item for the player (a state write to it changes the totals).</summary>
        public static bool Provides(Player player, ItemDrop.ItemData item)
        {
            foreach (Callbacks<Func<Player, List<ItemDrop.ItemData>>>.Entry provider in Providers.Snapshot)
            {
                List<ItemDrop.ItemData>? items = Ask(provider, player);
                if (items != null && items.Contains(item))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<ItemDrop.ItemData>? Ask(Callbacks<Func<Player, List<ItemDrop.ItemData>>>.Entry provider, Player player)
        {
            try
            {
                return provider.Callback(player);
            }
            catch (Exception e)
            {
                Providers.Failed(provider, e);
                return null;
            }
        }
    }
}
