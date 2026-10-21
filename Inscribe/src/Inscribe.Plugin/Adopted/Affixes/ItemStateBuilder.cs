// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// A mutable working copy of an item's state, for runes, drops and commands. Nothing reaches the item until
    /// <see cref="Build"/> is handed to <see cref="ItemState.Write"/>, so a rune can try an operation and walk away on
    /// failure. Unreadable segments stay in their positions unless explicitly cleared.
    /// </summary>
    public sealed class ItemStateBuilder
    {
        private readonly List<ItemSegment> _segments;
        private readonly List<GemSegment> _gems;
        private readonly StateData _data;

        internal ItemStateBuilder(StateData from)
        {
            _data = from.Copy();
            _segments = new List<ItemSegment>(from.Segments);
            _gems = new List<GemSegment>(from.Gems);
        }

        /// <summary>A builder for an item with no state yet.</summary>
        public static ItemStateBuilder New() => new ItemStateBuilder(StateData.Empty);

        public string? RarityId => _data.RarityId;
        public string? SealedReason => _data.SealedReason;

        /// <summary>The parsed affixes, in order (unreadable segments left out).</summary>
        public List<AffixRoll> Affixes => _segments.FindAll(s => s.IsRoll).ConvertAll(s => s.Roll);

        public int AffixCount => _segments.FindAll(s => s.IsRoll).Count;

        public bool HasAffix(string id) => IndexOf(id) >= 0;

        /// <summary>Sets the rarity; null or the base rarity's id makes the item Normal (the key is removed).</summary>
        public ItemStateBuilder SetRarity(string? rarityId)
        {
            RarityDef? rarity = ActiveRules.Current.Rarity(rarityId);
            _data.RarityId = rarity != null && rarity.IsBase ? null : rarityId;
            return this;
        }

        /// <summary>Appends an affix at the end. Throws when the id is already on the item (never two copies).</summary>
        public ItemStateBuilder AddAffix(AffixRoll roll)
        {
            if (HasAffix(roll.Id))
            {
                throw new InvalidOperationException($"inscription '{roll.Id}' is already on the item");
            }
            _segments.Add(new ItemSegment(roll));
            return this;
        }

        /// <summary>Removes an affix. False when absent.</summary>
        public bool RemoveAffix(string id)
        {
            int index = IndexOf(id);
            if (index < 0)
            {
                return false;
            }
            _segments.RemoveAt(index);
            return true;
        }

        /// <summary>Replaces an affix in place (a value reroll keeps its position). False when absent.</summary>
        public bool ReplaceAffix(string id, AffixRoll roll)
        {
            int index = IndexOf(id);
            if (index < 0 || (roll.Id != id && HasAffix(roll.Id)))
            {
                return false;
            }
            _segments[index] = new ItemSegment(roll);
            return true;
        }

        /// <summary>Removes every parsed affix; unreadable segments stay unless <paramref name="unreadableToo"/>.</summary>
        public ItemStateBuilder ClearAffixes(bool unreadableToo = false)
        {
            _segments.RemoveAll(s => s.IsRoll || unreadableToo);
            return this;
        }

        /// <summary>Seals with a reason id (<see cref="ItemKeys.SealedSerpent"/>); null unseals (no rune does).</summary>
        public ItemStateBuilder Seal(string? reason)
        {
            _data.SealedReason = string.IsNullOrEmpty(reason) ? null : reason;
            return this;
        }

        public int Sockets => _data.Sockets;

        /// <summary>Filled sockets, unreadable entries included (they fill theirs).</summary>
        public int GemCount => _gems.Count;

        /// <summary>Sets the socket count, 0-3; never below the gems the item holds.</summary>
        public ItemStateBuilder SetSockets(int count)
        {
            _data.Sockets = Math.Max(_gems.Count, Math.Min(count, GemCodec.MaxSockets));
            return this;
        }

        /// <summary>
        /// Puts a gem into socket <paramref name="index"/>: the next empty socket (index = the gem count) or a filled one,
        /// whose gem is replaced (destroyed). False when the index is outside the item's sockets or past the next empty one.
        /// </summary>
        public bool SetGem(int index, GemRoll gem)
        {
            if (index < 0 || index > _gems.Count || index >= _data.Sockets)
            {
                return false;
            }
            if (index == _gems.Count)
            {
                _gems.Add(new GemSegment(gem));
            }
            else
            {
                _gems[index] = new GemSegment(gem);
            }
            return true;
        }

        /// <summary>The finished state, resolved against the running rules, in the current format.</summary>
        public ItemState Build()
        {
            StateData data = _data.Copy();
            data.Segments = _segments.ToArray();
            data.Gems = _gems.ToArray();
            if (!data.Newer)
            {
                data.Format = ItemKeys.CurrentFormat;
            }
            return data.IsEmpty ? ItemState.Empty : new ItemState(data, ActiveRules.Current);
        }

        private int IndexOf(string id) => _segments.FindIndex(s => s.IsRoll && s.Roll.Id == id);
    }
}
