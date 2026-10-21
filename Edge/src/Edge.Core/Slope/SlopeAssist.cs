// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room (design B1 to B3). Written from public behavior only: the Slope Combat Assistance
// README ("Effects the Player only"), its config names and defaults (Height 1 with "0.6 default", Offset 0, range 0 to 5)
// and the game's own Attack fields (m_attackHeight, m_attackOffset, m_maxYAngle). No Slope Combat Assistance source
// file (AGPL-3.0) was read for this unit; only its LICENSE and docs/README.md.
using System;
using ExtraPotions.Edge.Config;

namespace ExtraPotions.Edge.Slope
{
    /// <summary>The three Attack fields slope assist changes for one melee call.</summary>
    public readonly struct AttackShape : IEquatable<AttackShape>
    {
        public AttackShape(float height, float offset, float maxYAngle)
        {
            Height = height;
            Offset = offset;
            MaxYAngle = maxYAngle;
        }

        /// <summary><c>Attack.m_attackHeight</c>: height the sweep starts from.</summary>
        public float Height { get; }

        /// <summary><c>Attack.m_attackOffset</c>: forward offset of the sweep.</summary>
        public float Offset { get; }

        /// <summary><c>Attack.m_maxYAngle</c>: most degrees the attack direction may tilt up or down.</summary>
        public float MaxYAngle { get; }

        public bool Equals(AttackShape other) => Height.Equals(other.Height) && Offset.Equals(other.Offset) && MaxYAngle.Equals(other.MaxYAngle);
        public override bool Equals(object? obj) => obj is AttackShape other && Equals(other);
        public override int GetHashCode() => (Height, Offset, MaxYAngle).GetHashCode();
        public override string ToString() => "height " + Height + ", offset " + Offset + ", max pitch " + MaxYAngle;
    }

    /// <summary>Who attacks and where (read by the plugin from the attacker, its ground normal and its target).</summary>
    public readonly struct SlopeFacts
    {
        public SlopeFacts(bool localPlayer, bool melee, float groundSlopeDegrees, float? targetVerticalOffset)
        {
            LocalPlayer = localPlayer;
            Melee = melee;
            GroundSlopeDegrees = groundSlopeDegrees;
            TargetVerticalOffset = targetVerticalOffset;
        }

        public bool LocalPlayer { get; }
        public bool Melee { get; }

        /// <summary>Angle in degrees between the ground the player stands on and the horizontal.</summary>
        public float GroundSlopeDegrees { get; }

        /// <summary>Height of the aimed target above (positive) or below the player's chest, in meters; null without a target.</summary>
        public float? TargetVerticalOffset { get; }
    }

    /// <summary>The design 4.3 settings in effect (normalized on construction).</summary>
    public readonly struct SlopeSettings
    {
        public SlopeSettings(SlopeMode mode, float height, float offset, float slopeThreshold, float maxPitch)
        {
            Mode = EdgeSettings.Slope.Mode.Normalize(mode);
            Height = EdgeSettings.Slope.AttackHeight.Normalize(height);
            Offset = EdgeSettings.Slope.AttackOffset.Normalize(offset);
            SlopeThreshold = EdgeSettings.Slope.AdaptiveSlopeThreshold.Normalize(slopeThreshold);
            MaxPitch = EdgeSettings.Slope.AdaptiveMaxPitch.Normalize(maxPitch);
        }

        public static SlopeSettings Defaults => new SlopeSettings(EdgeSettings.Slope.Mode.Default, EdgeSettings.Slope.AttackHeight.Default,
            EdgeSettings.Slope.AttackOffset.Default, EdgeSettings.Slope.AdaptiveSlopeThreshold.Default, EdgeSettings.Slope.AdaptiveMaxPitch.Default);

        public SlopeMode Mode { get; }
        public float Height { get; }
        public float Offset { get; }
        public float SlopeThreshold { get; }
        public float MaxPitch { get; }
    }

    public static class SlopeAssist
    {
        /// <summary>Adaptive: a target at least this many meters above or below counts as "above or below".</summary>
        public const float TargetTriggerHeight = 0.5f;

        /// <summary>
        /// The shape one melee call uses, or null to leave the attack untouched. Only the local player's melee attacks
        /// change (creatures and other players keep the game's values). Fixed: the configured height and offset. Adaptive:
        /// the same, and on steep ground or toward a target above or below the pitch limit rises to the configured maximum
        /// (never below the attack's own limit), so the swing follows the camera pitch.
        /// </summary>
        public static AttackShape? Decide(SlopeSettings settings, SlopeFacts facts, AttackShape vanilla)
        {
            if (settings.Mode == SlopeMode.Off || !facts.LocalPlayer || !facts.Melee) return null;
            var pitch = vanilla.MaxYAngle;
            if (settings.Mode == SlopeMode.Adaptive && Triggered(settings, facts)) pitch = Math.Max(vanilla.MaxYAngle, settings.MaxPitch);
            return new AttackShape(settings.Height, settings.Offset, pitch);
        }

        private static bool Triggered(SlopeSettings settings, SlopeFacts facts)
        {
            var slope = facts.GroundSlopeDegrees;
            if (!float.IsNaN(slope) && !float.IsInfinity(slope) && slope >= settings.SlopeThreshold) return true;
            var target = facts.TargetVerticalOffset;
            return target.HasValue && !float.IsNaN(target.Value) && !float.IsInfinity(target.Value) && Math.Abs(target.Value) >= TargetTriggerHeight;
        }
    }

    /// <summary>Holds an attack's own values between the prefix that changes them and the finalizer that restores them.</summary>
    public sealed class SlopeRestore
    {
        private AttackShape saved;

        public bool Pending { get; private set; }

        public void Save(AttackShape original)
        {
            saved = original;
            Pending = true;
        }

        public AttackShape Take()
        {
            if (!Pending) throw new InvalidOperationException("No saved attack values.");
            Pending = false;
            return saved;
        }
    }
}
