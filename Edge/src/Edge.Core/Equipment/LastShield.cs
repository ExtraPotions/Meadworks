// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// LastEquippedShieldSystem.cs (item identity and player selection keys, the v1: selection format, 32-digit GUID
// identities, keeping a unique identity and replacing a copied or invalid one, finding exactly one remembered shield in
// the inventory while skipping off-hand crossbows, refusing on a duplicate identity, equipping only after a one-handed
// weapon lands in the right hand with an empty left hand).
// Changed by expDARE, 2026: Unity-free decisions over plain candidates; the keys are kept byte for byte so remembered
// shields carry over from SecondaryAttacks (design 10.2); adds the C5 rule that a swim restore never equips a shield.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Edge.Equipment
{
    /// <summary>What an equip call put on: a shield, a one-handed weapon or anything else.</summary>
    public enum EquippedKind { Shield, OneHandedWeapon, Other }

    /// <summary>One inventory item as the finder sees it.</summary>
    public readonly struct ShieldCandidate
    {
        public ShieldCandidate(bool isShield, bool isOffhandCrossbow, string? id)
        {
            IsShield = isShield;
            IsOffhandCrossbow = isOffhandCrossbow;
            Id = id;
        }

        public bool IsShield { get; }
        public bool IsOffhandCrossbow { get; }

        /// <summary>The item's <see cref="LastShield.ItemIdentityKey"/> custom data value, if any.</summary>
        public string? Id { get; }
    }

    public readonly struct ShieldMatch
    {
        public ShieldMatch(int index, bool duplicate)
        {
            Index = index;
            Duplicate = duplicate;
        }

        /// <summary>Candidate index of the remembered shield; -1 when none or ambiguous.</summary>
        public int Index { get; }

        /// <summary>More than one shield carries the remembered identity (warn once, equip nothing).</summary>
        public bool Duplicate { get; }
    }

    public static class LastShield
    {
        public const string ItemIdentityKey = "SecondaryAttacks.LastShieldId";
        public const string PlayerSelectionKey = "SecondaryAttacks.LastShieldSelection";
        public const string SelectionVersionPrefix = "v1:";

        public static bool TryNormalizeId(string? value, out string id)
        {
            if (Guid.TryParseExact(value ?? "", "N", out var parsed))
            {
                id = parsed.ToString("N");
                return true;
            }
            id = string.Empty;
            return false;
        }

        public static string Selection(string id) => SelectionVersionPrefix + id;

        public static bool TryParseSelection(string? stored, out string id)
        {
            id = string.Empty;
            return stored != null && stored.StartsWith(SelectionVersionPrefix, StringComparison.Ordinal)
                && TryNormalizeId(stored.Substring(SelectionVersionPrefix.Length), out id);
        }

        /// <summary>
        /// The identity to store on a shield being equipped: its own when valid and unique in the inventory, otherwise a fresh
        /// one from <paramref name="newId"/> that no other item carries.
        /// </summary>
        public static string IdToRemember(string? storedId, IEnumerable<string?> otherItemIds, Func<string> newId)
        {
            if (newId == null) throw new ArgumentNullException(nameof(newId));
            var taken = new HashSet<string>((otherItemIds ?? Enumerable.Empty<string?>())
                .Select(o => TryNormalizeId(o, out var n) ? n : null).Where(n => n != null)!, StringComparer.Ordinal);
            if (TryNormalizeId(storedId, out var current) && !taken.Contains(current)) return current;
            for (var attempt = 0; attempt < 64; attempt++)
            {
                if (TryNormalizeId(newId(), out var fresh) && !taken.Contains(fresh)) return fresh;
            }
            throw new InvalidOperationException("Could not create a unique shield identity.");
        }

        public static ShieldMatch Find(string selectedId, IReadOnlyList<ShieldCandidate> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (!TryNormalizeId(selectedId, out var wanted)) return new ShieldMatch(-1, false);
            var match = -1;
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!c.IsShield || c.IsOffhandCrossbow || !TryNormalizeId(c.Id, out var id) || !string.Equals(id, wanted, StringComparison.Ordinal)) continue;
                if (match >= 0) return new ShieldMatch(-1, true);
                match = i;
            }
            return new ShieldMatch(match, false);
        }

        public static bool ShouldRemember(EquippedKind kind, bool offhandCrossbow) => kind == EquippedKind.Shield && !offhandCrossbow;

        /// <summary>
        /// After a successful equip: bring back the remembered shield only for a one-handed weapon now in the right hand
        /// with an empty left hand (torches and other left-hand items stay), and never while a swim restore shows items.
        /// </summary>
        public static bool ShouldEquip(EquippedKind kind, bool equippedIsRightItem, bool leftHandEmpty, bool restoringHiddenItems) =>
            kind == EquippedKind.OneHandedWeapon && equippedIsRightItem && leftHandEmpty && !restoringHiddenItems;
    }
}
