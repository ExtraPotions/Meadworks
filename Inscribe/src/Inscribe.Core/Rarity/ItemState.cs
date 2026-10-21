// Inscribe - ExtraPotions Meadworks gear inscriptions. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Changed by expDARE, 2026: a portable, immutable item state for the rune pipeline (no game types), the prefix and
// suffix limits over it, and the magic base rule as a pure predicate.
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.7.0 (Affixes/ItemState.cs, Rolling/AffixLimits.cs,
// Items/ItemClasses.cs IsMagicBase, Items/StackableGearWarning.cs), geraldjglasgow/ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Inscribe.Rarity
{
    /// <summary>Prefixes are an item's core power, suffixes everything else.</summary>
    public enum InscriptionKind { Prefix, Suffix }

    /// <summary>One inscription on an item: id, kind, strength grade (1 = weakest tier), value; dormant when its definition is gone.</summary>
    public sealed class Inscription
    {
        public Inscription(string id, InscriptionKind kind, int grade, float value, bool dormant = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Expected an inscription id.", nameof(id));
            Id = id;
            Kind = kind;
            Grade = grade;
            Value = value;
            Dormant = dormant;
        }

        public string Id { get; }
        public InscriptionKind Kind { get; }
        public int Grade { get; }
        public float Value { get; }

        /// <summary>Kept, shown greyed, inert; counts toward the rarity's limits (design A10).</summary>
        public bool Dormant { get; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// What Inscribe knows about an item's rarity state (design A11): rarity id (null is Normal), inscriptions in display
    /// order, sealed reason (null when not sealed) and whether the stored format is newer than this build reads.
    /// Immutable: every rune works on a copy and the dry-run result is what commits.
    /// </summary>
    public sealed class ItemState
    {
        public ItemState(string? rarityId, IReadOnlyList<Inscription>? inscriptions, string? sealedReason, bool newerFormat = false)
        {
            RarityId = string.IsNullOrEmpty(rarityId) ? null : rarityId;
            Inscriptions = inscriptions?.ToArray() ?? Array.Empty<Inscription>();
            SealedReason = string.IsNullOrEmpty(sealedReason) ? null : sealedReason;
            IsNewerFormat = newerFormat;
        }

        public static ItemState Normal { get; } = new ItemState(null, null, null);

        public string? RarityId { get; }
        public IReadOnlyList<Inscription> Inscriptions { get; }
        public string? SealedReason { get; }
        public bool IsSealed => SealedReason != null;
        public bool IsNewerFormat { get; }
        public int Count => Inscriptions.Count;

        public ItemState WithRarity(string? rarityId) => new ItemState(rarityId, Inscriptions, SealedReason, IsNewerFormat);

        public ItemState WithInscriptions(IEnumerable<Inscription> inscriptions) => new ItemState(RarityId, inscriptions.ToArray(), SealedReason, IsNewerFormat);

        public ItemState Adding(IEnumerable<Inscription> added) => WithInscriptions(Inscriptions.Concat(added));

        public ItemState Sealed(string reason) => new ItemState(RarityId, Inscriptions, reason, IsNewerFormat);
    }

    /// <summary>
    /// A rarity's prefix and suffix limits during one roll (design A1, B5): how many of each kind the item holds (dormant
    /// inscriptions count) and how many it may hold, the Serpent's overflow added. An unknown rarity limits nothing.
    /// </summary>
    public sealed class InscriptionLimits
    {
        private readonly int maxPrefixes;
        private readonly int maxSuffixes;
        private int prefixes;
        private int suffixes;

        private InscriptionLimits(int maxPrefixes, int maxSuffixes)
        {
            this.maxPrefixes = maxPrefixes;
            this.maxSuffixes = maxSuffixes;
        }

        public static InscriptionLimits For(ItemState state, RarityDef? rarity, int overflow)
        {
            overflow = Math.Max(overflow, 0);
            var limits = rarity == null
                ? new InscriptionLimits(int.MaxValue / 2, int.MaxValue / 2)
                : new InscriptionLimits(rarity.MaxPrefixes + overflow, rarity.MaxSuffixes + overflow);
            foreach (var inscription in state.Inscriptions) limits.Count(inscription.Kind);
            return limits;
        }

        /// <summary>Whether one more inscription of this kind keeps the item within its limits.</summary>
        public bool Allows(InscriptionKind kind) => kind == InscriptionKind.Prefix ? prefixes < maxPrefixes : suffixes < maxSuffixes;

        /// <summary>Counts an inscription now on the item.</summary>
        public void Count(InscriptionKind kind)
        {
            if (kind == InscriptionKind.Prefix) prefixes++;
            else suffixes++;
        }
    }

    /// <summary>Design A2: which items can become magic.</summary>
    public static class MagicBase
    {
        /// <summary>A max stack of exactly 1, a class with <c>rolls: true</c>, not a rune, and no API filter veto.</summary>
        public static bool Is(int maxStack, bool classRolls, bool isRune, bool vetoed) => maxStack == 1 && classRolls && !isRune && !vetoed;

        /// <summary>
        /// The load warning (EliteCrafting <c>StackableGearWarning</c>): every prefab whose class rolls but that another mod
        /// made stackable, so it cannot become magic. Input: prefab, max stack, class rolls (runes excluded by the caller).
        /// </summary>
        public static IReadOnlyList<string> StackableWarnings(IEnumerable<(string Prefab, int MaxStack, bool ClassRolls)> items) =>
            items.Where(i => i.MaxStack > 1 && i.ClassRolls).Select(i => i.Prefab).ToList();
    }
}
