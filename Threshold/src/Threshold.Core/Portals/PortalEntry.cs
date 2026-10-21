// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the portable shape of one portal of the server list (design section 3). The plugin fills it from
// the portal ZDO; Wayfare's PortalInfo (GPL-3.0-or-later) has the same role with fewer fields.
using System;
using ExtraPotions.Threshold.Access;

namespace ExtraPotions.Threshold.Portals
{
    /// <summary>A world position in game meters; Y is height.</summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public MapPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public bool IsFinite => !(float.IsNaN(X) || float.IsInfinity(X) || float.IsNaN(Y) || float.IsInfinity(Y) || float.IsNaN(Z) || float.IsInfinity(Z));

        /// <summary>Distance on the map plane (X and Z), as the map and the picker show it.</summary>
        public double FlatDistance(MapPoint other)
        {
            double dx = X - other.X, dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public bool Equals(MapPoint other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object? obj) => obj is MapPoint other && Equals(other);
        public override int GetHashCode() => (X, Y, Z).GetHashCode();
        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }

    /// <summary>A portal's session identity (the game's ZDOID: user id and object id). Changes on every world load.</summary>
    public readonly struct PortalId : IEquatable<PortalId>
    {
        public PortalId(long user, uint id)
        {
            User = user;
            Id = id;
        }

        public long User { get; }
        public uint Id { get; }

        public bool Equals(PortalId other) => User == other.User && Id == other.Id;
        public override bool Equals(object? obj) => obj is PortalId other && Equals(other);
        public override int GetHashCode() => (User, Id).GetHashCode();
        public override string ToString() => User + ":" + Id;
    }

    /// <summary>One portal as the server lists it: identity, prefab, tag, place, access, owner, hub name, whether
    /// its prefab lets every item through (the game's stone portal) and its facing (yaw in degrees, for the exit point in
    /// front of it, T11 and T12).</summary>
    public sealed class PortalEntry
    {
        public PortalEntry(PortalId id, string prefab, string tag, MapPoint position, AccessMode mode, long owner, string hubName, bool allowsAllItems, float yaw = 0f)
        {
            if (string.IsNullOrWhiteSpace(prefab)) throw new ArgumentException("Expected a prefab name.", nameof(prefab));
            if (!position.IsFinite) throw new ArgumentException("Expected a finite position.", nameof(position));
            Id = id;
            Prefab = prefab;
            Tag = tag ?? "";
            Position = position;
            Mode = Enum.IsDefined(typeof(AccessMode), mode) ? mode : AccessMode.Public;
            Owner = owner;
            HubName = (hubName ?? "").Trim();
            AllowsAllItems = allowsAllItems;
            Yaw = float.IsNaN(yaw) || float.IsInfinity(yaw) ? 0f : yaw;
        }

        public PortalId Id { get; }
        public string Prefab { get; }

        /// <summary>The portal's tag exactly as typed (player text).</summary>
        public string Tag { get; }

        public MapPoint Position { get; }
        public AccessMode Mode { get; }

        /// <summary>Owner player id; 0 for a portal nobody has claimed.</summary>
        public long Owner { get; }

        /// <summary>Hub name; empty when the portal is not a hub.</summary>
        public string HubName { get; }

        public bool AllowsAllItems { get; }

        /// <summary>Facing around the vertical axis, degrees.</summary>
        public float Yaw { get; }

        public bool IsHub => HubName.Length > 0;

        public bool IsTagged => Tag.Trim().Length > 0;

        public PortalEntry WithTag(string tag) => new PortalEntry(Id, Prefab, tag, Position, Mode, Owner, HubName, AllowsAllItems, Yaw);

        public PortalEntry WithMode(AccessMode mode) => new PortalEntry(Id, Prefab, Tag, Position, mode, Owner, HubName, AllowsAllItems, Yaw);

        public PortalEntry WithHub(string hubName) => new PortalEntry(Id, Prefab, Tag, Position, Mode, Owner, hubName, AllowsAllItems, Yaw);

        public override string ToString() => Prefab + " '" + Tag + "' " + Position + (IsHub ? " hub '" + HubName + "'" : "");
    }
}
