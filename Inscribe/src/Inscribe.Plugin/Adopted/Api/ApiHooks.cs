// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Loot;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// The hook endpoints (api.md section 5) over the callback lists that live where they are asked:
    /// <see cref="EquipmentProviders"/> (the effects rebuild), <see cref="MagicBaseFilters"/>
    /// (<see cref="ItemClasses.IsMagicBase"/>), <see cref="ItemChanges"/> (every item-state write) and
    /// <see cref="LootGenerated"/> (pre-rolled drops). A provider change rebuilds the local player's totals on the next
    /// frame; a filter change refreshes the rules at the end of the frame (the gear pool asks the filters).
    /// </summary>
    internal static class ApiHooks
    {
        public static bool AddProvider(string? id, Func<Player, List<ItemDrop.ItemData>>? provider) =>
            Rebuilt(EquipmentProviders.Providers.Set(id, provider));

        public static bool RemoveProvider(string? id) => Rebuilt(EquipmentProviders.Providers.Remove(id));

        /// <summary>The local player's totals are rebuilt on the next frame (at most once per frame); others have none here.</summary>
        public static bool InvalidatePlayer(Player? player)
        {
            if (player != null && ReferenceEquals(player, Player.m_localPlayer))
            {
                EffectRuntime.MarkDirty();   // the rebuild republishes loot find (FindPublisher)
            }
            return true;
        }

        public static bool AddFilter(string? id, Func<ItemDrop.ItemData, bool>? allow) =>
            Refreshed(MagicBaseFilters.Filters.Set(id, allow));

        public static bool RemoveFilter(string? id) => Refreshed(MagicBaseFilters.Filters.Remove(id));

        public static bool AddChangedListener(Action<ItemDrop.ItemData, string>? listener) => ItemChanges.Listeners.AddListener(listener);

        public static bool RemoveChangedListener(Action<ItemDrop.ItemData, string>? listener) => ItemChanges.Listeners.RemoveListener(listener);

        public static bool AddLootListener(Action<ItemDrop.ItemData>? listener) => LootGenerated.Listeners.AddListener(listener);

        public static bool RemoveLootListener(Action<ItemDrop.ItemData>? listener) => LootGenerated.Listeners.RemoveListener(listener);

        private static bool Rebuilt(bool changed)
        {
            if (changed)
            {
                EffectRuntime.MarkDirty();
            }
            return changed;
        }

        private static bool Refreshed(bool changed)
        {
            if (changed)
            {
                RuleRebuild.Request(inscriptions: false);
            }
            return changed;
        }
    }
}
