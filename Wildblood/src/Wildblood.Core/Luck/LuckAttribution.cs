// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: killer attribution and the luck delta (design 3.4, F3), and the luck policy (F4, Q6). SaveYourLuckBruh's
// README promises attribution to the killer; its code does not do it (design F3), so this is new.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Wildblood.Luck
{
    public enum AttackerKind { Environment, Player, Tame }

    /// <summary>Who struck the last hit, as the dying creature's owner sees it.</summary>
    public readonly struct KillFacts
    {
        private KillFacts(AttackerKind kind, long player, long? follows)
        {
            Kind = kind;
            Player = player;
            Follows = follows;
        }

        public static KillFacts Environment => new KillFacts(AttackerKind.Environment, 0, null);

        public static KillFacts ByPlayer(long playerId) => new KillFacts(AttackerKind.Player, playerId, null);

        /// <summary>A tame's kill; <paramref name="follows"/> is the player the tame follows, if any.</summary>
        public static KillFacts ByTame(long? follows) => new KillFacts(AttackerKind.Tame, 0, follows);

        public AttackerKind Kind { get; }
        public long Player { get; }
        public long? Follows { get; }
    }

    public readonly struct NearbyPlayer
    {
        public NearbyPlayer(long id, float distance)
        {
            Id = id;
            Distance = distance;
        }

        public long Id { get; }
        public float Distance { get; }
    }

    public static class KillerAttribution
    {
        /// <summary>
        /// The player whose counters a kill uses: the last-hit player; for a tame, the followed player within range, else the
        /// nearest player within range; null means the game's process-local counter (unsaved, the game's own behavior).
        /// </summary>
        public static long? Resolve(KillFacts kill, IEnumerable<NearbyPlayer> players, float tameCreditRange)
        {
            switch (kill.Kind)
            {
                case AttackerKind.Player:
                    return kill.Player;
                case AttackerKind.Tame:
                    var inRange = (players ?? Enumerable.Empty<NearbyPlayer>()).Where(p => p.Distance <= tameCreditRange).OrderBy(p => p.Distance).ToList();
                    if (kill.Follows.HasValue && inRange.Any(p => p.Id == kill.Follows.Value)) return kill.Follows.Value;
                    return inRange.Count > 0 ? inRange[0].Id : (long?)null;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// The owner's verdict for one pseudo row, sent to the killer's client (design 3.4 step 3): decrement by
    /// <see cref="Consumed"/>, or set <see cref="NewCounter"/> (0 or more) after a guaranteed drop. -1 means "no set".
    /// </summary>
    public readonly struct LuckDelta : IEquatable<LuckDelta>
    {
        public LuckDelta(string prefab, float chance, int consumed, int newCounter)
            : this(prefab, chance, consumed, newCounter, false)
        {
        }

        public LuckDelta(string prefab, float chance, int consumed, int newCounter, bool weighted)
        {
            Prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            Chance = chance;
            Consumed = consumed;
            NewCounter = newCounter;
            Weighted = weighted;
        }

        public string Prefab { get; }
        public float Chance { get; }
        public int Consumed { get; }
        public int NewCounter { get; }
        public bool Weighted { get; }

        /// <summary><c>prefab:chance:consumed:newCounter</c>, invariant culture.</summary>
        public string ToWire() => Prefab + ":" + Chance.ToString("R", CultureInfo.InvariantCulture) + ":"
            + Consumed.ToString(CultureInfo.InvariantCulture) + ":" + NewCounter.ToString(CultureInfo.InvariantCulture) + (Weighted ? ":w1" : "");

        public static bool TryParse(string? wire, out LuckDelta delta)
        {
            delta = default;
            var f = (wire ?? "").Split(':');
            if ((f.Length != 4 && (f.Length != 5 || f[4] != "w1")) || f[0].Length == 0) return false;
            if (!float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var chance) || float.IsNaN(chance)) return false;
            if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var consumed) || consumed < 0) return false;
            if (!int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var counter) || counter < -1) return false;
            delta = new LuckDelta(f[0], chance, consumed, counter, f.Length == 5);
            return true;
        }

        public bool Equals(LuckDelta other) => Prefab == other.Prefab && Chance.Equals(other.Chance) && Consumed == other.Consumed && NewCounter == other.NewCounter && Weighted == other.Weighted;
        public override bool Equals(object? obj) => obj is LuckDelta other && Equals(other);
        public override int GetHashCode() => (Prefab, Chance, Consumed, NewCounter, Weighted).GetHashCode();
    }

    public enum LuckMode
    {
        /// <summary>No counter work (module off, or the world's NoPseudoDrops modifier).</summary>
        Off,

        /// <summary>The game's own per-machine counters, not saved.</summary>
        Vanilla,

        /// <summary>SaveYourLuckBruh mode: this machine's counters saved per world, no attribution.</summary>
        LocalPersisted,

        /// <summary>Per-player counters, attributed to the killer whichever machine owns the creature.</summary>
        Attributed,
    }

    public static class LuckPolicy
    {
        /// <summary>
        /// The luck mode now. Without Wildblood in the session (vanilla server or a server without it) luck keeps running in
        /// SaveYourLuckBruh mode (user decision Q6, a declared exception to the peer-class rule).
        /// </summary>
        public static LuckMode Mode(bool enabled, bool persist, bool attribute, bool noPseudoDrops, bool productActive)
        {
            if (!enabled || noPseudoDrops) return LuckMode.Off;
            if (!persist) return LuckMode.Vanilla;
            return attribute && productActive ? LuckMode.Attributed : LuckMode.LocalPersisted;
        }
    }
}
