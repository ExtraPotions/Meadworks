// Threshold - ExtraPotions Meadworks portals.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/Portals/PortalAccess.cs (MayTarget, NextMode, MaySet,
// MayCycle).
// Changed by expDARE, 2026: Unity-free (mode and owner values instead of a ZDO), an access-modes switch, and the
// offered-mode list. Wayfare's three modes only (user decision U2, 2026-10-08: no Guild mode). Copyright (C) 2026 expDARE
// (ExtraPotions Meadworks) for the changes.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Access
{
    /// <summary>The player asking: persistent player id and whether the server counts them as an admin (the host counts as one).</summary>
    public readonly struct AccessViewer
    {
        public AccessViewer(long playerId, bool isAdmin)
        {
            PlayerId = playerId;
            IsAdmin = isAdmin;
        }

        public long PlayerId { get; }
        public bool IsAdmin { get; }
    }

    /// <summary>The one access rule. Threshold teleports on the player's client (U3), so this filters the picker list and
    /// the client's own travel check; the server enforces only what it writes (mode changes and hubs).</summary>
    public static class PortalAccessRules
    {
        public static AccessMode EffectiveMode(AccessMode mode, bool modesEnabled) => modesEnabled ? mode : AccessMode.Public;

        public static bool MayTarget(PortalEntry portal, AccessViewer viewer, bool modesEnabled)
        {
            if (portal == null) throw new ArgumentNullException(nameof(portal));
            return MayTarget(portal.Mode, portal.Owner, viewer, modesEnabled);
        }

        public static bool MayTarget(AccessMode mode, long owner, AccessViewer viewer, bool modesEnabled)
        {
            if (viewer.IsAdmin)
                return true;
            switch (EffectiveMode(mode, modesEnabled))
            {
                case AccessMode.Public:
                    return true;
                case AccessMode.Private:
                    return owner != 0L && owner == viewer.PlayerId;
                default: // Admin (and any unknown value): only the IsAdmin check above may pass
                    return false;
            }
        }

        /// <summary>The modes this player's mode cycle offers, in cycle order: Admin only for an admin.</summary>
        public static IReadOnlyList<AccessMode> Offered(AccessViewer viewer)
        {
            var modes = new List<AccessMode> { AccessMode.Public, AccessMode.Private };
            if (viewer.IsAdmin) modes.Add(AccessMode.Admin);
            return modes;
        }

        /// <summary>The mode the cycle key moves to. A mode the player cannot set (an Admin portal for a non-admin, an
        /// unknown value) moves to Public, as Wayfare does for Admin.</summary>
        public static AccessMode NextMode(AccessMode mode, AccessViewer viewer)
        {
            var offered = Offered(viewer);
            int at = -1;
            for (int i = 0; i < offered.Count; i++)
                if (offered[i] == mode) at = i;
            return at < 0 ? AccessMode.Public : offered[(at + 1) % offered.Count];
        }

        /// <summary>Whether the acting player may set this mode.</summary>
        public static bool MaySet(AccessMode mode, AccessViewer viewer) =>
            Enum.IsDefined(typeof(AccessMode), mode) && ((IList<AccessMode>)Offered(viewer)).Contains(mode);

        /// <summary>Whether the acting player may change this portal's mode. An unowned portal may be claimed by anyone; an
        /// owned one by its owner, or by an admin unless it is Private: a Private portal only by its owner, not even an
        /// admin (Wayfare rule).</summary>
        public static bool MayCycle(AccessMode mode, long owner, AccessViewer viewer)
        {
            if (owner == 0L || owner == viewer.PlayerId)
                return true;
            return viewer.IsAdmin && mode != AccessMode.Private;
        }
    }
}
