// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// The one parse cache every reader goes through (item-data.md section 5). Keyed on the <c>ItemData</c> instance
    /// (weakly); an entry remembers the custom-data dictionary it was parsed from, so the game's <c>Load</c> (which
    /// replaces the dictionary) or a clone is a miss and re-parses. The game clones items on every move, so misses are
    /// normal; a clone's affix text is the same string instance, which <see cref="ItemCodec"/> caches on too.
    /// A rules reload re-resolves an entry on its next read without re-parsing strings.
    /// </summary>
    public static class ItemStateCache
    {
        private sealed class Entry
        {
            public Dictionary<string, string>? Source;
            public ItemState State = ItemState.Empty;
        }

        private static readonly ConditionalWeakTable<ItemDrop.ItemData, Entry> Table =
            new ConditionalWeakTable<ItemDrop.ItemData, Entry>();

        /// <summary>
        /// Raised after every successful <see cref="Write"/>, with the item written. The effects area listens to
        /// rebuild the aggregate when the item is equipped by the local player. The API's item-changed listeners
        /// (<see cref="ItemChanges"/>) run right after it, with the write's reason.
        /// </summary>
        public static event Action<ItemDrop.ItemData>? Written;
        internal static void Shutdown() => Written = null;

        public static ItemState Read(ItemDrop.ItemData? item)
        {
            Dictionary<string, string>? data = item?.m_customData;
            if (data == null || data.Count == 0)
            {
                return ItemState.Empty;
            }
            Entry entry = Table.GetOrCreateValue(item!);
            if (!ReferenceEquals(entry.Source, data))
            {
                entry.State = new ItemState(ItemCodec.Parse(data), ActiveRules.Current);
                entry.Source = data;
            }
            else if (entry.State.Generation != ActiveRules.Generation && !entry.State.IsEmpty)
            {
                entry.State = entry.State.Resolve(ActiveRules.Current);
            }
            return entry.State;
        }

        /// <summary>
        /// Serializes <paramref name="state"/> into the item's custom data and replaces the cache entry in the same
        /// call. Refused (error log, nothing written) for a stackable item, a stone, or a newer-format state.
        /// </summary>
        public static bool Write(ItemDrop.ItemData item, ItemState state)
        {
            if (!MayWrite(item, state))
            {
                return false;
            }
            item.m_customData ??= new Dictionary<string, string>();
            ItemWriter.Serialize(state.Data, item.m_customData);
            Entry entry = Table.GetOrCreateValue(item);
            entry.Source = item.m_customData;
            entry.State = state.Generation == ActiveRules.Generation ? state : state.Resolve(ActiveRules.Current);
            RaiseWritten(item);
            return true;
        }

        private static bool MayWrite(ItemDrop.ItemData? item, ItemState state)
        {
            string name = item?.m_shared?.m_name ?? "(null)";
            if (item?.m_shared == null || item.m_shared.m_maxStackSize > 1 || ItemClasses.IsStone(item))
            {
                Log.Error($"refused to write item state to {name}: stackable items and stones never carry it");
                return false;
            }
            if (state.IsNewerFormat)
            {
                Log.Error($"refused to write item state to {name}: it was made by a newer ExtraPotions.Inscribe.Adopted");
                return false;
            }
            return true;
        }

        private static void RaiseWritten(ItemDrop.ItemData item)
        {
            try
            {
                Written?.Invoke(item);
            }
            catch (Exception e)
            {
                Log.Error($"an ItemStateCache.Written handler threw: {e}");
            }
            ItemChanges.Raise(item);
        }
    }
}
