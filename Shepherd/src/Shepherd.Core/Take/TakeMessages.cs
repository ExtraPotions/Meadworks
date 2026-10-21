// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;

namespace ExtraPotions.Shepherd.Take
{
    /// <summary>Constants of the owner-routed take (design section 3.3).</summary>
    public static class TakeProtocol
    {
        /// <summary>Animal owner to trough (or container) owner: take one item for this animal.</summary>
        public const string TakeRpc = "shepherd.take";

        /// <summary>Trough owner to the animal's current owner: the item was removed, apply the meal.</summary>
        public const string MealRpc = "shepherd.meal";

        /// <summary>The animal owner drops a pending request after this long and searches again; it never resends it.</summary>
        public const double ReplyTimeoutSeconds = 10.0;

        /// <summary>A trough owner refuses a request id it saw within this window.</summary>
        public const double DedupeWindowSeconds = 60.0;

        /// <summary>Request ids a trough owner remembers at most.</summary>
        public const int DedupeCapacity = 256;

        /// <summary>Longest item name (the game's <c>m_shared.m_name</c> token) a message carries.</summary>
        public const int MaxItemNameLength = 128;

        internal const string Version = "v1";
        internal const char Separator = '|';
    }

    /// <summary>A network object id, the portable shape of the game's <c>ZDOID</c> (user id and object id).</summary>
    public readonly struct NetObjectId : IEquatable<NetObjectId>
    {
        public NetObjectId(long user, uint id) { User = user; Id = id; }

        public long User { get; }
        public uint Id { get; }
        public bool IsNone => User == 0 && Id == 0;

        public bool Equals(NetObjectId other) => User == other.User && Id == other.Id;
        public override bool Equals(object? obj) => obj is NetObjectId other && Equals(other);
        public override int GetHashCode() => unchecked(User.GetHashCode() * 397 ^ (int)Id);
        public override string ToString() => User.ToString(CultureInfo.InvariantCulture) + ":" + Id.ToString(CultureInfo.InvariantCulture);

        public static bool TryParse(string? text, out NetObjectId value)
        {
            value = default;
            if (!IdText.TryPair(text, out long user, out uint id)) return false;
            value = new NetObjectId(user, id);
            return true;
        }
    }

    /// <summary>
    /// A take request id: the issuing peer (its session id) and a per-issuer sequence that only grows, so an id is never
    /// reused by the same machine and two machines never share one.
    /// </summary>
    public readonly struct TakeRequestId : IEquatable<TakeRequestId>
    {
        public TakeRequestId(long issuer, uint sequence) { Issuer = issuer; Sequence = sequence; }

        public long Issuer { get; }
        public uint Sequence { get; }

        public bool Equals(TakeRequestId other) => Issuer == other.Issuer && Sequence == other.Sequence;
        public override bool Equals(object? obj) => obj is TakeRequestId other && Equals(other);
        public override int GetHashCode() => unchecked(Issuer.GetHashCode() * 397 ^ (int)Sequence);
        public override string ToString() => Issuer.ToString(CultureInfo.InvariantCulture) + ":" + Sequence.ToString(CultureInfo.InvariantCulture);

        public static bool TryParse(string? text, out TakeRequestId value)
        {
            value = default;
            if (!IdText.TryPair(text, out long issuer, out uint sequence)) return false;
            value = new TakeRequestId(issuer, sequence);
            return true;
        }
    }

    /// <summary><c>shepherd.take(requestId, itemName, animalZdoid)</c>.</summary>
    public sealed class TakeRequest
    {
        public TakeRequest(TakeRequestId id, string itemName, NetObjectId animal)
        {
            Id = id;
            ItemName = itemName ?? throw new ArgumentNullException(nameof(itemName));
            Animal = animal;
        }

        public TakeRequestId Id { get; }
        public string ItemName { get; }
        public NetObjectId Animal { get; }
    }

    /// <summary><c>shepherd.meal(requestId, itemName)</c>, invoked on the animal's view.</summary>
    public sealed class MealMessage
    {
        public MealMessage(TakeRequestId id, string itemName)
        {
            Id = id;
            ItemName = itemName ?? throw new ArgumentNullException(nameof(itemName));
        }

        public TakeRequestId Id { get; }
        public string ItemName { get; }
    }

    /// <summary>
    /// Single-string payloads for the two RPCs: <c>v1|take|issuer:seq|user:id|item</c> and
    /// <c>v1|meal|issuer:seq|item</c>. A payload of another version, kind or shape decodes to nothing, and the
    /// receiver then changes nothing.
    /// </summary>
    public static class TakeCodec
    {
        private const string TakeKind = "take";
        private const string MealKind = "meal";

        public static string Encode(TakeRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireItem(request.ItemName);
            return string.Join(TakeProtocol.Separator.ToString(), TakeProtocol.Version, TakeKind, request.Id.ToString(), request.Animal.ToString(), request.ItemName);
        }

        public static string Encode(MealMessage meal)
        {
            if (meal == null) throw new ArgumentNullException(nameof(meal));
            RequireItem(meal.ItemName);
            return string.Join(TakeProtocol.Separator.ToString(), TakeProtocol.Version, MealKind, meal.Id.ToString(), meal.ItemName);
        }

        public static bool TryDecodeTake(string? text, out TakeRequest? request)
        {
            request = null;
            var parts = Split(text, TakeKind, 5);
            if (parts == null || !TakeRequestId.TryParse(parts[2], out var id) || !NetObjectId.TryParse(parts[3], out var animal)
                || !IsItem(parts[4]))
                return false;
            request = new TakeRequest(id, parts[4], animal);
            return true;
        }

        public static bool TryDecodeMeal(string? text, out MealMessage? meal)
        {
            meal = null;
            var parts = Split(text, MealKind, 4);
            if (parts == null || !TakeRequestId.TryParse(parts[2], out var id) || !IsItem(parts[3]))
                return false;
            meal = new MealMessage(id, parts[3]);
            return true;
        }

        /// <summary>A non-empty item token without the separator, at most <see cref="TakeProtocol.MaxItemNameLength"/> chars.</summary>
        public static bool IsItem(string? item) =>
            !string.IsNullOrEmpty(item) && item!.Length <= TakeProtocol.MaxItemNameLength && item.IndexOf(TakeProtocol.Separator) < 0;

        private static void RequireItem(string item)
        {
            if (!IsItem(item))
                throw new ArgumentException("An item name is 1 to " + TakeProtocol.MaxItemNameLength + " characters without '" + TakeProtocol.Separator + "'.", nameof(item));
        }

        private static string[]? Split(string? text, string kind, int count)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var parts = text!.Split(TakeProtocol.Separator);
            return parts.Length == count && parts[0] == TakeProtocol.Version && parts[1] == kind ? parts : null;
        }
    }

    internal static class IdText
    {
        /// <summary>Parses <c>long:uint</c> in invariant culture, nothing else.</summary>
        public static bool TryPair(string? text, out long first, out uint second)
        {
            first = 0; second = 0;
            if (string.IsNullOrEmpty(text)) return false;
            int colon = text!.IndexOf(':');
            if (colon <= 0 || colon != text.LastIndexOf(':')) return false;
            return long.TryParse(text.Substring(0, colon), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out first)
                && uint.TryParse(text.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out second);
        }
    }
}
