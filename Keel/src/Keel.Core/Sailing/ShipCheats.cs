// Keel - ExtraPotions Meadworks ships and sailing. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Meadworks-original. Valheim_Admin_Tool's ship cheats are a behavior-only reference (no license; nothing copied).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Keel.Sailing
{
    /// <summary>The personal ship cheats Tailwind and Fast Sailing, for the ship the local player steers.</summary>
    public static class ShipCheats
    {
        /// <summary>Fast Sailing's top speed factor.</summary>
        public const float SpeedFactor = 2f;

        /// <summary>The sail force factor: the game's forward drag grows with the square of the speed, so the force is raised by the square.</summary>
        public static float SailForceFactor(bool on) => on ? SpeedFactor * SpeedFactor : 1f;

        /// <summary>Whether a heading has a flat direction a wind can follow.</summary>
        public static bool HasTailwind(float forwardX, float forwardZ) => forwardX * forwardX + forwardZ * forwardZ > 1e-6f;

        /// <summary>Tailwind: the wind blows the flat way the ship points, so it always runs before the wind.</summary>
        public static (float X, float Z) Tailwind(float forwardX, float forwardZ)
        {
            var length = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            return length > 1e-3f ? (forwardX / length, forwardZ / length) : (0f, 0f);
        }
    }
}
