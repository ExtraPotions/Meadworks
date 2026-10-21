// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Magpie.Items;

namespace ExtraPotions.Magpie.Merging
{
    /// <summary>World position in meters.</summary>
    public readonly struct Point3
    {
        public Point3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public float DistanceSquared(Point3 other)
        {
            float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }

    /// <summary>
    /// Snapshot of one ground item as the merge planner sees it, taken by the plugin on the scanning peer.
    /// The planner never reads the game; every fact it needs is here.
    /// </summary>
    public sealed class GroundItem
    {
        public GroundItem(long id, ItemIdentity identity, int stack, int maxStack, Point3 position, long created, double spawnTime,
            bool ownedLocally, bool valid, bool moving, double ageSeconds, bool pulled, bool pendingIntent, string? token)
        {
            Id = id;
            Identity = identity;
            Stack = stack;
            MaxStack = maxStack;
            Position = position;
            Created = created;
            SpawnTime = spawnTime;
            OwnedLocally = ownedLocally;
            Valid = valid;
            Moving = moving;
            AgeSeconds = ageSeconds;
            Pulled = pulled;
            PendingIntent = pendingIntent;
            Token = token;
        }

        /// <summary>Opaque handle the plugin maps back to its <c>ItemDrop</c> (unique within one scan).</summary>
        public long Id { get; }
        public ItemIdentity Identity { get; }
        public int Stack { get; }

        /// <summary>The item's maximum stack size; 1 means not stackable.</summary>
        public int MaxStack { get; }
        public Point3 Position { get; }

        /// <summary>Creation order of the item's ZDO; lower is older (target tie-break, design 3.3 step 1).</summary>
        public long Created { get; }

        /// <summary>The ZDO spawn time the game's despawn rule reads (design B8).</summary>
        public double SpawnTime { get; }

        /// <summary>This peer owns the item's ZDO right now (B6); Magpie never claims ownership in order to merge.</summary>
        public bool OwnedLocally { get; }

        /// <summary>The item's ZNetView and ZDO are valid.</summary>
        public bool Valid { get; }

        /// <summary>The rigidbody is still moving (not settled, B7).</summary>
        public bool Moving { get; }

        /// <summary>Seconds since the item appeared on this peer (B7 settle time).</summary>
        public double AgeSeconds { get; }

        /// <summary>The pickup magnet is pulling the item (B7).</summary>
        public bool Pulled { get; }

        /// <summary>The item carries an open <c>magpie.merge_in</c> or <c>magpie.merge_out</c> record.</summary>
        public bool PendingIntent { get; }

        /// <summary>Persisted instance token (<c>magpie.id</c>, Crucible C12), or null when none was written yet.</summary>
        public string? Token { get; }
    }
}
