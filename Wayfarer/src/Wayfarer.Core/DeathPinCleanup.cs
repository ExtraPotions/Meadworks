// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Wayfarer.Core
{
    public readonly struct DeathPinCandidate
    {
        public readonly float X, Z;
        public readonly string? Name;
        public readonly bool Saved, Death, ForeignAuthor;
        public readonly long OwnerId;

        public DeathPinCandidate(float x, float z, string? name, bool saved, bool death, long ownerId, bool foreignAuthor)
        { X = x; Z = z; Name = name; Saved = saved; Death = death; OwnerId = ownerId; ForeignAuthor = foreignAuthor; }
    }

    public static class DeathPinCleanup
    {
        // Conservative fixed XZ match bound; native tombstone-to-pin identity has no built-in radius.
        public const float MatchRadius = 1f;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static int FindUnique(IReadOnlyList<DeathPinCandidate> pins, float originalX, float originalZ, int nativeDay, long localPlayerId)
        {
            if (pins == null || localPlayerId == 0 || nativeDay < 0 || !Finite(originalX) || !Finite(originalZ)) return -1;
            string name = "$hud_mapday " + nativeDay.ToString(CultureInfo.InvariantCulture);
            int found = -1;
            for (int i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                if (!pin.Saved || !pin.Death || pin.ForeignAuthor || (pin.OwnerId != 0 && pin.OwnerId != localPlayerId)
                    || !string.Equals(pin.Name, name, StringComparison.Ordinal) || !Finite(pin.X) || !Finite(pin.Z)) continue;
                double dx = (double)pin.X - originalX, dz = (double)pin.Z - originalZ;
                if (dx * dx + dz * dz >= (double)MatchRadius * MatchRadius) continue;
                if (found >= 0) return -1;
                found = i;
            }
            return found;
        }
    }
}
