// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: bounded admission and completion ledger; durable inventory escrow owns unresolved delivery decisions.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Thing.Moderation;

namespace ExtraPotions.Thing.Admin
{
    public enum TransferPhase { Armed, Removed, Delivered, Restore, Restored, DeleteArmed, DeleteReady, Deleted, Failed }

    public enum TransferWhere { Inventory, Feet }

    /// <summary>The selected native prefab and slot. Runtime escrow also checks the complete owner-serialized item image before removal.</summary>
    public readonly struct ItemSlot
    {
        public ItemSlot(string prefab, int stack, int quality, int x, int y, long crafter, int variant)
        {
            Prefab = prefab ?? ""; Stack = stack; Quality = quality; X = x; Y = y; Crafter = crafter; Variant = variant;
        }

        public string Prefab { get; }
        public int Stack { get; }
        public int Quality { get; }
        public int X { get; }
        public int Y { get; }
        public long Crafter { get; }
        public int Variant { get; }

        public string Text => string.Join("\t", Prefab, Stack.ToString(CultureInfo.InvariantCulture), Quality.ToString(CultureInfo.InvariantCulture),
            X.ToString(CultureInfo.InvariantCulture), Y.ToString(CultureInfo.InvariantCulture), Crafter.ToString(CultureInfo.InvariantCulture), Variant.ToString(CultureInfo.InvariantCulture));

        public static bool TryParse(string text, out ItemSlot slot)
        {
            slot = default;
            var parts = (text ?? "").Split('\t');
            if (parts.Length != 7 || !ValidPrefab(parts[0])) return false;
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var stack) || stack < 1 || stack > 9999) return false;
            if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var quality) || quality < 1 || quality > 100) return false;
            if (!int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var x) || x < 0 || x > 32) return false;
            if (!int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var y) || y < 0 || y > 32) return false;
            if (!long.TryParse(parts[5], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var crafter)) return false;
            if (!int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out var variant) || variant < 0 || variant > 100) return false;
            slot = new ItemSlot(parts[0], stack, quality, x, y, crafter, variant);
            return true;
        }

        public static bool ValidPrefab(string prefab) => !string.IsNullOrWhiteSpace(prefab) && prefab.Length <= 128 && prefab.IndexOfAny(new[] { '\t', '\r', '\n', '|', ' ' }) < 0;

        public bool Same(ItemSlot other) => Prefab == other.Prefab && Stack == other.Stack && Quality == other.Quality && X == other.X && Y == other.Y && Crafter == other.Crafter && Variant == other.Variant;
    }

    public sealed class ItemMove
    {
        internal ItemMove(string token, string admin, string target, ItemSlot slot, bool delete)
        {
            Token = token; AdminId = admin; TargetId = target; Slot = slot; Delete = delete;
            Phase = delete ? TransferPhase.DeleteArmed : TransferPhase.Armed;
        }

        public string Token { get; }
        public string AdminId { get; }
        public string TargetId { get; }
        public ItemSlot Slot { get; }
        public bool Delete { get; }
        public TransferPhase Phase { get; internal set; }
        public TransferWhere Where { get; internal set; }
    }

    /// <summary>In-session ledger. A token is single use. Deliver and restore are mutually exclusive. Delete never delivers.</summary>
    public sealed class TransferLedger
    {
        public const int MaxActive = 32;
        readonly Dictionary<string, ItemMove> moves = new Dictionary<string, ItemMove>(StringComparer.Ordinal);

        public ItemMove? Find(string token) => token != null && moves.TryGetValue(token, out var move) ? move : null;

        public void Clear() => moves.Clear();

        /// <summary>Drops a delete that is waiting for the second confirm. A take or a confirmed delete may already be at the owner, so those stay put.</summary>
        public void ReleaseIdle(string admin, string? keep = null)
        {
            if (string.IsNullOrEmpty(admin)) return;
            foreach (var move in moves.Values.Where(m => m.AdminId == admin && m.Token != keep && m.Phase == TransferPhase.DeleteArmed).ToArray())
                Fail(move.Token);
        }

        public int Delivered => moves.Values.Count(m => m.Phase == TransferPhase.Delivered);
        public int Restored => moves.Values.Count(m => m.Phase == TransferPhase.Restored);
        public int Deleted => moves.Values.Count(m => m.Phase == TransferPhase.Deleted);
        public int InFlight => moves.Values.Count(m => m.Phase == TransferPhase.Removed || m.Phase == TransferPhase.Restore);

        public ItemMove? BeginTake(string admin, string target, ItemSlot slot, string token) => Begin(admin, target, slot, token, false);
        public ItemMove? BeginDelete(string admin, string target, ItemSlot slot, string token) => Begin(admin, target, slot, token, true);

        public bool Removed(string token)
        {
            var move = Find(token);
            if (move == null || move.Delete || (move.Phase != TransferPhase.Armed && move.Phase != TransferPhase.Failed)) return false;
            move.Phase = TransferPhase.Removed;
            return true;
        }

        public bool DeliveredTo(string token, TransferWhere where)
        {
            var move = Find(token);
            if (move == null || move.Delete || move.Phase != TransferPhase.Removed) return false;
            if (where != TransferWhere.Inventory && where != TransferWhere.Feet) return false;
            move.Where = where;
            move.Phase = TransferPhase.Delivered;
            return true;
        }

        public bool NeedsRestore(string token)
        {
            var move = Find(token);
            if (move == null || move.Delete || move.Phase != TransferPhase.Removed) return false;
            move.Phase = TransferPhase.Restore;
            return true;
        }

        public bool RestoredToOwner(string token)
        {
            var move = Find(token);
            if (move == null || move.Phase != TransferPhase.Restore) return false;
            move.Phase = TransferPhase.Restored;
            return true;
        }

        public bool ConfirmDelete(string token)
        {
            var move = Find(token);
            if (move == null || !move.Delete || move.Phase != TransferPhase.DeleteArmed) return false;
            move.Phase = TransferPhase.DeleteReady;
            return true;
        }

        public bool DeletedFrom(string token)
        {
            var move = Find(token);
            if (move == null || !move.Delete || move.Phase != TransferPhase.DeleteReady) return false;
            move.Phase = TransferPhase.Deleted;
            return true;
        }

        /// <summary>Stops a transfer that has not taken the item yet. After removal, only deliver or restore is legal.</summary>
        public bool Fail(string token)
        {
            var move = Find(token);
            if (move == null) return false;
            if (move.Phase != TransferPhase.Armed && move.Phase != TransferPhase.DeleteArmed && move.Phase != TransferPhase.DeleteReady) return false;
            move.Phase = TransferPhase.Failed;
            return true;
        }

        ItemMove? Begin(string admin, string target, ItemSlot slot, string token, bool delete)
        {
            if (!Actor(admin) || !GameListIdentity.Valid(target) || admin == target || !Token(token) || string.IsNullOrEmpty(slot.Prefab)) return null;
            if (moves.ContainsKey(token)) return null;
            if (moves.Values.Count(m => Active(m) && (m.TargetId == target || m.AdminId == admin)) > 0) return null;
            if (moves.Values.Count(Active) >= MaxActive) return null;
            var move = new ItemMove(token, admin, target, slot, delete);
            moves[token] = move;
            return move;
        }

        static bool Active(ItemMove move) => move.Phase == TransferPhase.Armed || move.Phase == TransferPhase.Removed || move.Phase == TransferPhase.Restore
            || move.Phase == TransferPhase.DeleteArmed || move.Phase == TransferPhase.DeleteReady;

        static bool Actor(string id) => id == "host" || id == "server" || GameListIdentity.Valid(id);
        static bool Token(string token) => token != null && token.Length == 32 && token.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    }
}
