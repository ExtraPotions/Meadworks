// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behaviour only: the world tools of design C1, C4, C5 and C6 follow what Valheim_Admin_Tool 0.5.0's
// README describes (terrain reset 1 to 50 m with ownership protection and cancel on missing data, remove drops of loose item
// stacks only, a global time freeze released when its admin leaves or loses rights, a session-only ward override for
// confirmed admins). No code of that mod was read.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Thing.Admin;

namespace ExtraPotions.Thing.WorldAdmin
{
    /// <summary>A building piece near a terrain reset: ground position, horizontal extent and the creator's player ID (0: unknown).</summary>
    public readonly struct PieceFootprint
    {
        public PieceFootprint(float x, float z, float extent, long creator)
        {
            X = x;
            Z = z;
            Extent = Math.Max(0, extent);
            Creator = creator;
        }

        public float X { get; }
        public float Z { get; }
        public float Extent { get; }
        public long Creator { get; }

        /// <summary>Wholly or partly inside the circle.</summary>
        public bool Overlaps(float centerX, float centerZ, float radius)
        {
            var dx = X - centerX;
            var dz = Z - centerZ;
            var reach = radius + Extent;
            return dx * dx + dz * dz <= reach * reach;
        }
    }

    /// <summary>What the server could see of a terrain reset area: whether every zone was loaded and the pieces in or near it.</summary>
    public sealed class TerrainArea
    {
        public TerrainArea(bool complete, IReadOnlyList<PieceFootprint> pieces)
        {
            Complete = complete;
            Pieces = pieces ?? Array.Empty<PieceFootprint>();
        }

        public bool Complete { get; }
        public IReadOnlyList<PieceFootprint> Pieces { get; }
    }

    public enum TerrainOutcome { Proceed, RefusedNotAdmin, RefusedIncomplete, RefusedOtherPieces }

    public static class TerrainReset
    {
        /// <summary>Whole meters from 1 to the server's Terrain Reset Max Radius.</summary>
        public static int Radius(float requested, int serverMaximum)
        {
            var max = Math.Max(1, serverMaximum);
            if (float.IsNaN(requested) || float.IsInfinity(requested)) return 1;
            return (int)Math.Min(max, Math.Max(1, Math.Round(requested, MidpointRounding.AwayFromZero)));
        }

        /// <summary>
        /// The server's ownership protection: missing area data cancels for everyone; an admin resets any checked area; a
        /// non-admin (verdict <see cref="Verdict.AllowedOwnPiecesOnly"/>) only an area with no pieces or only their own;
        /// a piece with an unknown creator counts as another player's.
        /// </summary>
        public static TerrainOutcome Decide(Verdict verdict, long requester, float centerX, float centerZ, int radius, TerrainArea area)
        {
            if (!AdminCheck.Permits(verdict)) return TerrainOutcome.RefusedNotAdmin;
            if (area == null || !area.Complete) return TerrainOutcome.RefusedIncomplete;
            if (verdict == Verdict.Allowed) return TerrainOutcome.Proceed;
            var others = area.Pieces.Any(p => p.Overlaps(centerX, centerZ, radius) && (p.Creator == 0 || p.Creator != requester));
            return others ? TerrainOutcome.RefusedOtherPieces : TerrainOutcome.Proceed;
        }
    }

    public enum DropKind { LooseItem, PlacedItem, BuildingPiece, Container, Tombstone, SwimmingFish }

    public static class DropRules
    {
        /// <summary>Remove drops takes loose item stacks within the radius; placed items, buildings, containers, tombstones and swimming fish stay.</summary>
        public static bool Removes(DropKind kind, float distance, float radius) =>
            kind == DropKind.LooseItem && !float.IsNaN(distance) && distance >= 0 && distance <= radius;
    }

    public static class TimeOfDay
    {
        public static double? AtCurrentDay(double seconds, double dayLength, double fraction)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0
                || double.IsNaN(dayLength) || double.IsInfinity(dayLength) || dayLength <= 0
                || double.IsNaN(fraction) || double.IsInfinity(fraction) || fraction < 0 || fraction >= 1) return null;
            var result = Math.Floor(seconds / dayLength) * dayLength + fraction * dayLength;
            return double.IsInfinity(result) || result > 315537897599 ? (double?)null : result;
        }
        /// <summary>
        /// A day fraction (0 midnight, 0.5 noon) from <c>HH:mm</c> (00:00 to 23:59) or a preset: Midnight, Sunrise (06:00),
        /// Noon, Sunset (18:00); null when unreadable.
        /// </summary>
        public static double? Parse(string? text)
        {
            var t = (text ?? "").Trim();
            switch (t.ToLowerInvariant())
            {
                case "midnight": return 0.0;
                case "sunrise": return 0.25;
                case "noon": return 0.5;
                case "sunset": return 0.75;
            }
            var parts = t.Split(':');
            if (parts.Length != 2 || parts[0].Length == 0 || parts[0].Length > 2 || parts[1].Length != 2) return null;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var h) || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m)) return null;
            if (h > 23 || m > 59) return null;
            return (h * 60 + m) / 1440.0;
        }

        public static string Format(double fraction)
        {
            var f = double.IsNaN(fraction) ? 0 : fraction - Math.Floor(fraction);
            var minutes = (int)Math.Round(f * 1440) % 1440;
            return (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The server's global time override (design C1): who set it, the time and whether it is frozen.</summary>
    public sealed class GlobalTimeState
    {
        public string SetBy { get; private set; } = "";
        public double Fraction { get; private set; }
        public bool Frozen { get; private set; }

        /// <summary>Sets the time; <paramref name="adminId"/> empty for the server console or the host.</summary>
        public void Apply(string adminId, double fraction, bool freeze)
        {
            SetBy = (adminId ?? "").Trim();
            Fraction = fraction;
            Frozen = freeze;
        }

        /// <summary>A freeze set by a remote admin is released when that admin is offline or no longer admin (Global Time Release On Leave).</summary>
        public bool ShouldRelease(Func<string, bool> online, Func<string, bool> isAdmin, bool releaseOnLeave)
        {
            if (!Frozen || !releaseOnLeave || SetBy.Length == 0) return false;
            return !online(SetBy) || !isAdmin(SetBy);
        }

        /// <summary>Releases a freeze without undoing the chosen time.</summary>
        public bool Release()
        {
            if (!Frozen) return false;
            Frozen = false;
            return true;
        }
    }

    /// <summary>Session-only ward override grants by platform ID (design C6); granted only after the server confirmed admin rights.</summary>
    public sealed class WardOverrideGrants
    {
        readonly HashSet<string> granted = new HashSet<string>(StringComparer.Ordinal);

        public bool Grant(string id) => granted.Add((id ?? "").Trim());

        public bool Revoke(string id) => granted.Remove((id ?? "").Trim());

        public bool Has(string id) => granted.Contains((id ?? "").Trim());

        /// <summary>Revokes every grant whose holder is no longer an online admin; returns the revoked IDs in order.</summary>
        public IReadOnlyList<string> Prune(Func<string, bool> stillOnlineAdmin)
        {
            var gone = granted.Where(id => !stillOnlineAdmin(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
            foreach (var id in gone) granted.Remove(id);
            return gone;
        }

        public void Clear() => granted.Clear();
    }
}
