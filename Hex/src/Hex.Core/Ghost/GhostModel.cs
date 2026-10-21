// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: server-confirmed Ghost Mode model and decision (docs/ghost-authority.md). Unity-free.
using System;
namespace ExtraPotions.Hex.Ghost
{
    /// <summary>A character's native ZDO identity (user and id), the same on every peer.</summary>
    public readonly struct GhostKey : IEquatable<GhostKey>, IComparable<GhostKey>
    {
        public GhostKey(long user, uint id) { User = user; Id = id; }
        public long User { get; }
        public uint Id { get; }
        /// <summary>The native "no object" identity; never confirmed.</summary>
        public bool IsNone => User == 0 && Id == 0;
        public bool Equals(GhostKey other) => User == other.User && Id == other.Id;
        public override bool Equals(object? obj) => obj is GhostKey other && Equals(other);
        public override int GetHashCode() => unchecked(User.GetHashCode() * 397 ^ (int)Id);
        public int CompareTo(GhostKey other) { var user = User.CompareTo(other.User); return user != 0 ? user : Id.CompareTo(other.Id); }
        public static bool operator ==(GhostKey a, GhostKey b) => a.Equals(b);
        public static bool operator !=(GhostKey a, GhostKey b) => !a.Equals(b);
        public override string ToString() => User + ":" + Id;
    }

    public enum GhostVerb : byte { Off = 0, On = 1 }
    public enum GhostState : byte { Off = 0, Confirmed = 1, Refused = 2 }
    public enum GhostReason : byte { None = 0, Inactive = 1, Character = 2, Denied = 3, Capacity = 4, Vanilla = 5 }

    /// <summary>The server's answer to one request. Only <see cref="GhostState.Confirmed"/> keeps a registry entry.</summary>
    public readonly struct GhostAnswer : IEquatable<GhostAnswer>
    {
        public GhostAnswer(GhostState state, GhostReason reason) { State = state; Reason = reason; }
        public GhostState State { get; }
        public GhostReason Reason { get; }
        public bool Keeps => State == GhostState.Confirmed;
        public bool Equals(GhostAnswer other) => State == other.State && Reason == other.Reason;
        public override bool Equals(object? obj) => obj is GhostAnswer other && Equals(other);
        public override int GetHashCode() => (int)State * 16 + (int)Reason;
        public override string ToString() => State + (Reason == GhostReason.None ? "" : ":" + Reason);
    }

    /// <summary>The server's decision for one peer's request, from facts the server resolved itself.</summary>
    public static class GhostDecision
    {
        /// <param name="active">Hex is ready and active on the current authority.</param>
        /// <param name="characterMatches">The requested character is the sender's current native character.</param>
        /// <param name="allowed">C02/C13 allows the Personal category for this peer now.</param>
        /// <param name="capacityAvailable">The registry can hold this peer (it has an entry already, or there is room).</param>
        /// <param name="vanillaBlocked">The server's vanilla-client setting refuses Ghost now (<see cref="GhostVanillaRule.Blocks"/>).</param>
        public static GhostAnswer Decide(GhostVerb verb, bool active, bool characterMatches, bool allowed, bool capacityAvailable, bool vanillaBlocked = false)
        {
            if (!active) return new GhostAnswer(GhostState.Refused, GhostReason.Inactive);
            if (!characterMatches) return new GhostAnswer(GhostState.Refused, GhostReason.Character);
            if (verb == GhostVerb.Off) return new GhostAnswer(GhostState.Off, GhostReason.None);
            if (!allowed) return new GhostAnswer(GhostState.Refused, GhostReason.Denied);
            if (vanillaBlocked) return new GhostAnswer(GhostState.Refused, GhostReason.Vanilla);
            if (!capacityAvailable) return new GhostAnswer(GhostState.Refused, GhostReason.Capacity);
            return new GhostAnswer(GhostState.Confirmed, GhostReason.None);
        }
    }

    /// <summary>What a remote client knows when it considers asking: its session, character, wish, confirmation and policy stamp.</summary>
    public readonly struct GhostRequestFacts : IEquatable<GhostRequestFacts>
    {
        public GhostRequestFacts(long generation, GhostKey character, bool wish, bool confirmed, string stamp)
        { Generation = generation; Character = character; Wish = wish; Confirmed = confirmed; Stamp = stamp ?? ""; }
        public long Generation { get; }
        public GhostKey Character { get; }
        public bool Wish { get; }
        public bool Confirmed { get; }
        /// <summary>Mirrored policy and admin facts; a change lets an answered request be asked again.</summary>
        public string Stamp { get; }
        /// <summary>The facts that make a request new (confirmation itself is not part of the key).</summary>
        internal bool SameKey(GhostRequestFacts other) => Generation == other.Generation && Character == other.Character && Wish == other.Wish
            && string.Equals(Stamp, other.Stamp, StringComparison.Ordinal);
        public bool Equals(GhostRequestFacts other) => SameKey(other) && Confirmed == other.Confirmed;
        public override bool Equals(object? obj) => obj is GhostRequestFacts other && Equals(other);
        public override int GetHashCode() => unchecked(Generation.GetHashCode() * 397 ^ Character.GetHashCode() ^ (Wish ? 1 : 0) ^ Stamp.GetHashCode());
    }
}
