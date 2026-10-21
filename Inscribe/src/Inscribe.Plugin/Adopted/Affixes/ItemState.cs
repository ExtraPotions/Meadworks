// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
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
    /// An item's Inscribe state (item-data.md): rarity, affixes, sockets and gems, sealed reason and format version, resolved
    /// against the running rules. Immutable: to change an item, take <see cref="ToBuilder"/>, change the builder, and
    /// hand the result to <see cref="Write"/> - the only write path.
    /// <para>
    /// Read an item with <see cref="Read"/>: a cache hit is one table lookup and one reference compare, and a plain
    /// item (no custom data) returns <see cref="Empty"/> without any lookup, so hot paths may call it on every item.
    /// </para>
    /// </summary>
    public sealed class ItemState
    {
        public static readonly ItemState Empty = new ItemState(StateData.Empty, RuleSet.Empty);

        private readonly StateData _source;
        private readonly AffixRoll[] _rolls;
        private readonly AffixDef?[] _defs;
        private readonly string[] _unreadable;
        private readonly GemRoll[] _gems;
        private readonly int[] _gemSockets;
        private readonly AffixDef?[] _gemDefs;
        private readonly EffectRoll[] _effective;

        internal ItemState(StateData data, RuleSet rules)
        {
            // Format-1 grades and retired ids need the inscriptions' ladders: converted here, against these rules, from
            // the source.
            _source = data;
            data = ItemMigrations.MergeRetired(ItemMigrations.ResolveGrades(data, rules.Affixes), rules.Affixes);
            Data = data;
            Generation = rules.Generation;
            _rolls = Rolls(data.Segments, out _unreadable);
            _defs = Resolve(_rolls, rules.Affixes);
            _gems = GemCodec.Readable(data.Gems, out _gemSockets);
            _gemDefs = Resolve(_gems, rules.Affixes);
            RarityId = data.RarityId;
            Rarity = rules.Rarity(data.RarityId);
            _effective = EffectRollBuilder.Build(this);
        }

        internal StateData Data { get; }

        /// <summary>The rules generation this state was resolved against.</summary>
        public int Generation { get; }

        // ---- the stored data

        /// <summary>Format version (current after in-memory migration).</summary>
        public int Format => Data.Format;

        /// <summary>Written by a newer mod version: effects of parseable affixes apply, every rune refuses.</summary>
        public bool IsNewerFormat => Data.Newer;

        /// <summary>Rarity id; null = Normal (the base rarity is never stored).</summary>
        public string? RarityId { get; }

        /// <summary>The parsed affixes in stored order (unreadable segments left out); <see cref="AffixRoll.Tier"/> is the grade.</summary>
        public IReadOnlyList<AffixRoll> Affixes => _rolls;

        /// <summary>Segments of <c>ecf_inscriptions</c> that did not parse, kept verbatim and written back in place.</summary>
        public IReadOnlyList<string> Unreadable => _unreadable;

        public string? SealedReason => Data.SealedReason;

        // ---- resolved against the rules

        /// <summary>The rarity definition; null for Normal and for an unknown rarity id.</summary>
        public RarityDef? Rarity { get; }

        /// <summary>Magic or better: the item carries a rarity id (known or not).</summary>
        public bool IsMagic => RarityId != null;

        public bool IsUnknownRarity => RarityId != null && Rarity == null;
        public bool IsSealed => Data.SealedReason != null;
        public bool HasAffixes => _rolls.Length > 0;
        public int AffixCount => _rolls.Length;

        /// <summary>No <c>ecf_</c> state at all: a plain vanilla item.</summary>
        public bool IsEmpty => Data.IsEmpty;

        /// <summary>The live definition of affix <paramref name="index"/>; null when orphaned (id unknown).</summary>
        public AffixDef? DefinitionAt(int index) => _defs[index];

        /// <summary>Active = defined and enabled; everything else is dormant (kept, shown greyed, inert).</summary>
        public bool IsActiveAt(int index) => _defs[index] != null && _defs[index]!.Enabled;

        public int IndexOf(string id) => Array.FindIndex(_rolls, r => r.Id == id);

        public bool HasAffix(string id) => IndexOf(id) >= 0;

        // ---- sockets (sockets.md section 2)

        /// <summary>How many sockets the item has (0-3).</summary>
        public int Sockets => Data.Sockets;

        /// <summary>The filled sockets in order (unreadable entries left out); the empty sockets follow them.</summary>
        public IReadOnlyList<GemRoll> Gems => _gems;

        /// <summary>Sockets holding something, unreadable entries included; the next empty socket has this index.</summary>
        public int FilledSockets => Data.Gems.Length;

        /// <summary>Sockets without a gem (an unreadable entry fills its socket).</summary>
        public int FreeSockets => Math.Max(0, Data.Sockets - Data.Gems.Length);

        /// <summary>The socket (0-based) gem <paramref name="index"/> sits in.</summary>
        public int GemSocketAt(int index) => _gemSockets[index];

        /// <summary>The live definition of the inscription gem <paramref name="index"/> gives; null when orphaned.</summary>
        public AffixDef? GemDefinitionAt(int index) => _gemDefs[index];

        /// <summary>A socketed gem counts while its inscription is defined and enabled; otherwise it is dormant.</summary>
        public bool IsGemActiveAt(int index) => _gemDefs[index] != null && _gemDefs[index]!.Enabled;

        /// <summary>What effects read: the active affixes, then the active gems (<see cref="EffectRollBuilder"/>).</summary>
        public IReadOnlyList<EffectRoll> EffectRolls => _effective;

        /// <summary>
        /// The effects area's item-local numbers for this state (<c>Effects.ItemLocalCache</c>), made on first use and kept
        /// with it, so a hot getter costs the one cache lookup of <see cref="Read"/>. Valid as long as the state is: a
        /// state never changes, and a write, a reload by the game or a rules change hands out a new one. Main thread only.
        /// </summary>
        internal object? LocalSums;

        public ItemStateBuilder ToBuilder() => new ItemStateBuilder(Data);

        // ---- entry points

        /// <summary>The item's state, from the cache (see <see cref="ItemStateCache"/>).</summary>
        public static ItemState Read(ItemDrop.ItemData? item) => ItemStateCache.Read(item);

        /// <summary>Writes the state into the item's custom data; the only write path. False (and an error log) when refused.</summary>
        public static bool Write(ItemDrop.ItemData item, ItemState state) => ItemStateCache.Write(item, state);

        /// <summary>Parses a custom-data dictionary without the cache (commands, tests).</summary>
        public static ItemState Parse(Dictionary<string, string> data) => new ItemState(ItemCodec.Parse(data), ActiveRules.Current);

        /// <summary>The same data resolved against another rules snapshot (no string work).</summary>
        internal ItemState Resolve(RuleSet rules) => _source.IsEmpty ? Empty : new ItemState(_source, rules);

        private static AffixRoll[] Rolls(ItemSegment[] segments, out string[] unreadable)
        {
            List<AffixRoll> rolls = new List<AffixRoll>(segments.Length);
            List<string> raw = new List<string>();
            foreach (ItemSegment segment in segments)
            {
                if (segment.IsRoll)
                {
                    rolls.Add(segment.Roll);
                }
                else
                {
                    raw.Add(segment.Raw!);
                }
            }
            unreadable = raw.Count == 0 ? Array.Empty<string>() : raw.ToArray();
            return rolls.Count == 0 ? Array.Empty<AffixRoll>() : rolls.ToArray();
        }

        private static AffixDef?[] Resolve(GemRoll[] gems, AffixRules rules)
        {
            AffixDef?[] defs = gems.Length == 0 ? Array.Empty<AffixDef?>() : new AffixDef?[gems.Length];
            for (int i = 0; i < gems.Length; i++)
            {
                defs[i] = rules.Get(gems[i].Roll.Id);
            }
            return defs;
        }

        private static AffixDef?[] Resolve(AffixRoll[] rolls, AffixRules rules)
        {
            AffixDef?[] defs = rolls.Length == 0 ? Array.Empty<AffixDef?>() : new AffixDef?[rolls.Length];
            for (int i = 0; i < rolls.Length; i++)
            {
                defs[i] = rules.Get(rolls[i].Id);
            }
            return defs;
        }
    }
}
