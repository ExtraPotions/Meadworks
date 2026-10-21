// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// The raw, parsed content of an item's <c>ecf_</c> keys, before any definition is looked up. Shared by every
    /// <see cref="ItemState"/> resolved from it (a rules reload re-resolves without re-parsing). Immutable.
    /// </summary>
    internal sealed class StateData
    {
        public static readonly StateData Empty = new StateData();

        public int Format { get; set; } = ItemKeys.CurrentFormat;

        /// <summary>Written by a newer version of the mod: read what parses, never write back.</summary>
        public bool Newer { get; set; }

        public string? RarityId { get; set; }
        public ItemSegment[] Segments { get; set; } = Array.Empty<ItemSegment>();
        public string? SealedReason { get; set; }

        /// <summary>The socket count (<c>ecf_sockets</c>), 0 = none.</summary>
        public int Sockets { get; set; }

        /// <summary>The filled sockets in order (<c>ecf_gems</c>), unreadable entries kept verbatim in place.</summary>
        public GemSegment[] Gems { get; set; } = Array.Empty<GemSegment>();

        /// <summary>The reserved <c>ecf_tier</c> value, preserved verbatim.</summary>
        public string? ReservedTier { get; set; }

        /// <summary>
        /// The segments' grades are format 1's (1-7 over seven tiers) and still need each inscription's ladder to convert
        /// (<see cref="ItemMigrations.ResolveGrades"/>, run when the state is resolved against rules).
        /// </summary>
        public bool LegacyGrades { get; set; }

        public bool IsEmpty => RarityId == null && Segments.Length == 0 && SealedReason == null && ReservedTier == null && !Newer
            && Sockets == 0 && Gems.Length == 0;

        public StateData Copy() => (StateData)MemberwiseClone();
    }
}
