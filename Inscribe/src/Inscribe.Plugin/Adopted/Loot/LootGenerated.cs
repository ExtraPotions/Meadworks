// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The API's loot-generated listeners (api.md section 5): raised by <see cref="GearFactory.Build"/> after a
    /// pre-rolled magic item was made for a drop (a creature's loot or a world container), on the peer that rolls it,
    /// before it reaches the world. Each listener runs guarded on its own. Main thread only.
    /// </summary>
    internal static class LootGenerated
    {
        public static readonly Callbacks<Action<ItemDrop.ItemData>> Listeners =
            new Callbacks<Action<ItemDrop.ItemData>>("loot-generated listener");

        public static void Raise(ItemDrop.ItemData item)
        {
            foreach (Callbacks<Action<ItemDrop.ItemData>>.Entry listener in Listeners.Snapshot)
            {
                try
                {
                    listener.Callback(item);
                }
                catch (Exception e)
                {
                    Listeners.Failed(listener, e);
                }
            }
        }
    }
}
