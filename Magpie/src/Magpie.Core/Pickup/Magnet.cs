// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Take distance 0.3 m and pull speed 15 m/s are the game's values as documented by OpenKeep
// src/BuildCamera/CameraPickup.cs (GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow, reference ad278ecf323901c3645eb1f9f6fac6833a0f8cb1).
using System;

namespace ExtraPotions.Magpie.Pickup
{
    public enum MagnetAction
    {
        /// <summary>Beyond the radius (or no usable distance): the item never moves.</summary>
        Outside,

        /// <summary>Move the item <see cref="MagnetStep.Move"/> meters toward the player this frame.</summary>
        Pull,

        /// <summary>Close enough: take it.</summary>
        Take,
    }

    public readonly struct MagnetStep
    {
        public MagnetStep(MagnetAction action, float move)
        {
            Action = action;
            Move = move;
        }

        public MagnetAction Action { get; }
        public float Move { get; }
    }

    /// <summary>Design A3 (SwiftPickup 1.3.1 magnet, behavior): items inside the radius glide every frame and are taken within 0.3 m.</summary>
    public static class Magnet
    {
        public const float TakeDistance = 0.3f;

        public static MagnetStep Step(float distance, float radius, float speed, float deltaTime)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance > radius)
                return new MagnetStep(MagnetAction.Outside, 0f);
            if (distance < TakeDistance)
                return new MagnetStep(MagnetAction.Take, 0f);
            var move = Math.Max(0f, speed) * Math.Max(0f, deltaTime);
            return new MagnetStep(MagnetAction.Pull, Math.Min(move, distance));
        }
    }
}
