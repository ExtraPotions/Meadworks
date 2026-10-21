// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Tempest yields to the game's own random events (raids such as jotuninvasion or elakingarmy).
using System;

namespace ExtraPotions.Tempest.Events
{
    /// <summary>The area of the game's current random event (<c>RandomEvent.m_pos</c> and <c>m_eventRange</c>).</summary>
    public readonly struct VanillaEventArea
    {
        public VanillaEventArea(float x, float z, float range)
        {
            X = x;
            Z = z;
            Range = range;
        }

        public float X { get; }
        public float Z { get; }
        public float Range { get; }
    }

    /// <summary>
    /// While a vanilla random event runs, its own sky and music win; Tempest does not start an event that would cover a
    /// player inside it, and sends no strikes, hazards or center notices to players inside it.
    /// </summary>
    public static class VanillaEvents
    {
        /// <summary>Above this height <c>RandEventSystem.IsInsideRandomEventArea</c> (1.0.20) counts no one as inside.</summary>
        public const float CeilingY = 3000f;

        /// <summary>The game's own test (1.0.20): y at most 3000 and the XZ distance below the event range.</summary>
        public static bool Inside(VanillaEventArea? area, float x, float y, float z)
        {
            if (area == null || y > CeilingY) return false;
            var a = area.Value;
            var dx = x - a.X;
            var dz = z - a.Z;
            return Math.Sqrt(dx * dx + dz * dz) < a.Range;
        }

        /// <summary>
        /// Whether a player is in a vanilla event: inside the current random event's area, or the game reports an active
        /// event for the local player (<c>RandEventSystem.GetActiveEvent</c>, which also covers the game's forced events).
        /// </summary>
        public static bool Covers(bool activeForPlayer, VanillaEventArea? area, float x, float y, float z) => activeForPlayer || Inside(area, x, y, z);
    }
}
