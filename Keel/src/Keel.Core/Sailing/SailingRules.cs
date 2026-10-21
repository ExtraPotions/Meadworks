// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.15.0, GPL-3.0-or-later.
// Adapted from ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1:
// GrindstoneSkills/GrindstoneSkills/Sailing/SailingSkill.cs (Share), ShipwrightHealth.cs (Factor), HelmSpeed.cs (SpeedFactor,
// ForceFactor), SeaExploration.cs (RadiusFactor).
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Keel.Sailing; Unity-free (System.Math); the switch,
// the setting value and the levels are passed in instead of read from BepInEx entries and the local player, so the rules
// are testable; the owner check of HelmSpeed.ForceFactor is a parameter.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Keel.Sailing
{
    /// <summary>
    /// The Sailing perks. Each grows linearly from nothing at level 0 to its value at level 100. The plugin reads the
    /// settings and levels and applies the factors in its patches (shipwright on placement and load, helm speed in
    /// <c>Ship.CustomFixedUpdate</c> on the ship's owner, the radius in <c>Minimap.UpdateExplore</c> or through Cairn).
    /// </summary>
    public static class SailingRules
    {
        public const float MaxLevel = 100f;

        /// <summary>A perk's share at a level: <paramref name="percentAt100"/> percent at level 100, linear from 0, clamped.</summary>
        public static float Share(float percentAt100, float level)
        {
            if (float.IsNaN(level) || float.IsNaN(percentAt100)) return 0f;
            return Math.Max(0f, percentAt100) / 100f * Math.Min(1f, Math.Max(0f, level / MaxLevel));
        }

        /// <summary>The max health multiplier of a ship built at this Sailing level; 1 while Sailing is off.</summary>
        public static float ShipwrightFactor(bool active, float shipHealthAt100, float builderLevel) =>
            active ? 1f + Share(shipHealthAt100, builderLevel) : 1f;

        /// <summary>The top speed factor the helmsman's level gives the ship: 1.2 is 20% faster; 1 without a helmsman or with Sailing off.</summary>
        public static float HelmSpeedFactor(bool active, bool hasHelmsman, float shipSpeedAt100, float helmsmanLevel) =>
            active && hasHelmsman ? 1f + Share(shipSpeedAt100, helmsmanLevel) : 1f;

        /// <summary>
        /// The force multiplier on the machine that moves the ship (its ZDO owner); 1 anywhere else. The game's forward
        /// drag grows with the square of the speed, so top speed grows with the square root of the force: the force is
        /// raised by the square of the speed factor.
        /// </summary>
        public static float HelmForceFactor(bool isOwner, float speedFactor) =>
            isOwner && speedFactor > 1f ? speedFactor * speedFactor : 1f;

        /// <summary>The factor on the local player's exploration radius while aboard: 2 doubles it; 1 ashore or with Sailing off.</summary>
        public static float RadiusFactor(bool active, bool aboard, float explorationRadiusAt100, float level) =>
            active && aboard ? 1f + Share(explorationRadiusAt100, level) : 1f;
    }
}