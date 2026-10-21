// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.15.0, GPL-3.0-or-later.
// Adapted from ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1:
// GrindstoneSkills/GrindstoneSkills/Sailing/SailingXp.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Keel.Sailing; Unity-free; the ship, position,
// helm role and settings are passed in each step and the experience is returned instead of raised on the player, so the
// meter is testable; the per-frame timer, MaxStep and flat distance rules are unchanged.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Keel.Sailing
{
    /// <summary>Where the local player stands relative to the ship's helm.</summary>
    public enum HelmRole { Nobody, Helmsman, Crew }

    /// <summary>
    /// Sailing experience, once a second on each player's own client. Aboard a ship that someone steers, the distance
    /// the ship moved over the water since the last second earns experience: the full rate for the helmsman, the crew
    /// share for everyone else. The distance is measured flat, so bobbing earns nothing, and a jump of more than
    /// <see cref="MaxStep"/> metres in a second (a teleport, a ship that just loaded) earns nothing either.
    /// </summary>
    public sealed class SailingXp
    {
        public const float Interval = 1f;
        public const float MaxStep = 60f;

        private float timer;
        private object? lastShip;
        private double lastX;
        private double lastZ;

        /// <summary>
        /// Advances the meter by <paramref name="dt"/> seconds; returns the experience to raise now (0 between seconds).
        /// <paramref name="ship"/> identifies the ship the player is aboard (null ashore); x and z are its position.
        /// </summary>
        public float Step(float dt, object? ship, double x, double z, HelmRole role, bool active, float helmExperiencePerKilometre, float crewSharePercent)
        {
            return Advance(dt) ? Measure(ship, x, z, role, active, helmExperiencePerKilometre, crewSharePercent) : 0f;
        }

        /// <summary>
        /// Advances only the timer; true once per second, when <see cref="Measure"/> is due. The game side calls this every
        /// frame and gathers the ship, helmsman and position only when it returns true.
        /// </summary>
        public bool Advance(float dt)
        {
            timer += float.IsNaN(dt) ? 0f : Math.Max(0f, dt);
            if (timer < Interval) return false;
            timer = 0f;
            return true;
        }

        /// <summary>The experience for the second that <see cref="Advance"/> just completed.</summary>
        public float Measure(object? ship, double x, double z, HelmRole role, bool active, float helmExperiencePerKilometre, float crewSharePercent)
        {
            var moved = ship != null && ReferenceEquals(ship, lastShip) ? Flat(x - lastX, z - lastZ) : 0.0;
            lastShip = ship;
            lastX = ship != null ? x : 0.0;
            lastZ = ship != null ? z : 0.0;
            if (!active || moved <= 0.0 || moved > MaxStep) return 0f;
            var perKilometre = Math.Max(0f, helmExperiencePerKilometre) * Share(role, crewSharePercent);
            return perKilometre > 0f ? (float)(moved / 1000.0 * perKilometre) : 0f;
        }

        /// <summary>1 at the helm, the crew share aboard a ship someone else steers, 0 when nobody steers.</summary>
        public static float Share(HelmRole role, float crewSharePercent)
        {
            switch (role)
            {
                case HelmRole.Helmsman: return 1f;
                case HelmRole.Crew: return float.IsNaN(crewSharePercent) ? 0f : Math.Min(1f, Math.Max(0f, crewSharePercent / 100f));
                default: return 0f;
            }
        }

        private static double Flat(double dx, double dz) => Math.Sqrt(dx * dx + dz * dz);
    }
}