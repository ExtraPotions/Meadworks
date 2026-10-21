// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Keel's side of the shared exploration contracts (design 8.2, K06). Keel provides the Sailing sea
// factor as meadworks.explore.radius 1.0; Cairn multiplies it in and provides its computed radius as
// meadworks.explore.current 1.0, which Keel's ship panel shows. Keel.Core has no Crucible reference, so the contract
// bounds are restated here and the plugin reads through Crucible.Contracts.
using System;

namespace ExtraPotions.Keel.Sailing
{
    public static class ExploreShare
    {
        /// <summary>The largest factor <c>meadworks.explore.radius</c> 1.0 accepts (Crucible ExploreRadiusContract.MaximumFactor).</summary>
        public const float MaximumFactor = 16f;

        /// <summary>The game's own exploration radius in meters, used when the game's value cannot be read.</summary>
        public const float GameRadius = 100f;

        /// <summary>
        /// The factor Keel provides: <see cref="SailingRules.RadiusFactor"/> while the local player is aboard with Sailing
        /// active, else exactly 1; always finite and in (0, <see cref="MaximumFactor"/>].
        /// </summary>
        public static float Factor(bool active, bool aboard, float explorationRadiusAt100, float level)
        {
            if (float.IsInfinity(explorationRadiusAt100)) return 1f;
            var factor = SailingRules.RadiusFactor(active, aboard, explorationRadiusAt100, level);
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f) return 1f;
            return Math.Min(MaximumFactor, factor);
        }

        /// <summary>
        /// Whether Minimap.UpdateExplore reveals the map on this frame: it adds the frame time to its timer and explores once
        /// the timer is past the interval. The game calls it every frame, so the radius patch needs to act only on that frame.
        /// </summary>
        public static bool ExploresThisFrame(float timer, float deltaTime, float interval) => timer + deltaTime > interval;

        /// <summary>
        /// The radius the ship panel shows: Cairn's current radius when <c>meadworks.explore.current</c> gave one, else the
        /// game's radius times Keel's own factor (what Keel's fallback patch applies without Cairn). Never negative.
        /// </summary>
        public static float PanelRadius(bool hasCurrent, float current, float vanillaRadius, float factor)
        {
            if (hasCurrent) return Math.Max(0f, current);
            var radius = float.IsNaN(vanillaRadius) || float.IsInfinity(vanillaRadius) ? GameRadius : vanillaRadius;
            var f = float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f ? 1f : factor;
            return Math.Max(0f, radius * f);
        }
    }
}
