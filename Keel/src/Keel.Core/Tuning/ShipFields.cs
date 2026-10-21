// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the per-ship entry table of design 4.2 (field names follow ShipConfig's, ranges are Keel's).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Keel.Tuning
{
    public enum ShipField
    {
        Health, SailForce, SailForceOffset, PaddleForce, RudderSpeed, TurnForceSailing, TurnForcePaddling,
        ForwardDrag, SidewaysDrag, AngularDamping, WaterImpactDamage, UpsideDownDamage, WeatherWear, DamageTaken, BuildCost,
    }

    /// <summary>
    /// One per-ship entry: its key, whether it is a switch, its default for a given ship and its range. Toggles carry
    /// 0 and 1 as their numeric default and range.
    /// </summary>
    public sealed class ShipFieldSpec
    {
        private readonly Func<ShipDefaults, float> defaultFor;
        private readonly Func<ShipDefaults, float> minFor;
        private readonly Func<ShipDefaults, float> maxFor;

        internal ShipFieldSpec(ShipField field, bool toggle, string description, Func<ShipDefaults, float> defaultFor, Func<ShipDefaults, float> minFor, Func<ShipDefaults, float> maxFor)
        {
            Field = field;
            IsToggle = toggle;
            Description = description;
            this.defaultFor = defaultFor;
            this.minFor = minFor;
            this.maxFor = maxFor;
        }

        public ShipField Field { get; }

        /// <summary>The key part after the prefab name, the field name itself (ShipConfig's spelling).</summary>
        public string Key => Field.ToString();

        public bool IsToggle { get; }

        public string Description { get; }

        public float DefaultFor(ShipDefaults defaults) => defaultFor(defaults ?? throw new ArgumentNullException(nameof(defaults)));

        public float MinimumFor(ShipDefaults defaults) => minFor(defaults ?? throw new ArgumentNullException(nameof(defaults)));

        public float MaximumFor(ShipDefaults defaults) => maxFor(defaults ?? throw new ArgumentNullException(nameof(defaults)));

        /// <summary>The value clamped into this ship's range; non-finite numbers give the default.</summary>
        public float Normalize(ShipDefaults defaults, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return DefaultFor(defaults);
            return Math.Min(MaximumFor(defaults), Math.Max(MinimumFor(defaults), value));
        }
    }

    public static class ShipFields
    {
        public const string Section = "Ships";

        /// <summary>Range factor of the relative fields: 0 to 10 times the prefab default (design 4.2).</summary>
        public const float RelativeRange = 10f;

        private static readonly ShipFieldSpec[] all =
        {
            Absolute(ShipField.Health, d => d.Health, 1f, 100000f, "Hull points of this ship."),
            Relative(ShipField.SailForce, d => d.SailForce, "Thrust from the sail: top speed under sail, scaled by wind and sail size."),
            Absolute(ShipField.SailForceOffset, d => d.SailForceOffset, -5f, 10f,
                "Height in meters above the center of mass where the sail pushes. Lower heels less and capsizes less at high sail force."),
            Relative(ShipField.PaddleForce, d => d.PaddleForce, "Force at the oars, forward and backward."),
            Relative(ShipField.RudderSpeed, d => d.RudderSpeed, "How fast the rudder swings to full lock while steering."),
            Relative(ShipField.TurnForceSailing, d => d.TurnForceSailing, "Turning force under sail; grows with forward speed."),
            Relative(ShipField.TurnForcePaddling, d => d.TurnForcePaddling, "Turning force at the oars."),
            Relative(ShipField.ForwardDrag, d => d.ForwardDrag, "Forward water drag. Lower is faster and coasts longer; it caps the top speed."),
            Relative(ShipField.SidewaysDrag, d => d.SidewaysDrag, "Sideways water drag. Lower lets the ship slide in a crosswind. Tune with care."),
            Relative(ShipField.AngularDamping, d => d.AngularDamping, "Resistance to rolling and pitching. Higher is a steadier deck; too high and the ship stops turning."),
            Absolute(ShipField.WaterImpactDamage, d => d.WaterImpactDamage, 0f, 1000f, "Damage from slamming into rough seas with players aboard. 0 turns it off."),
            Absolute(ShipField.UpsideDownDamage, d => d.UpsideDownDamage, 0f, 1000f, "Damage per second while capsized. 0 turns it off."),
            new ShipFieldSpec(ShipField.WeatherWear, true,
                "On: the ship loses health in rain and with its hull under water, down to half health. Off by default (user decision 2026-10-07).",
                d => 0f, d => 0f, d => 1f),
            Absolute(ShipField.DamageTaken, d => 1f, 0f, 10f, "Factor on every hit this ship takes. 1 is vanilla, 0 means no damage."),
            Absolute(ShipField.BuildCost, d => 1f, 0.1f, 10f, "Factor on the materials this ship costs. Each material is rounded and at least 1."),
        };

        /// <summary>Every field in design order.</summary>
        public static IReadOnlyList<ShipFieldSpec> All { get; } = Array.AsReadOnly(all);

        public static ShipFieldSpec Get(ShipField field) => all[(int)field];

        /// <summary>The fields a ship gets: all of them, without Build Cost when the ship has no Piece.</summary>
        public static IEnumerable<ShipFieldSpec> For(ShipDefaults defaults)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            return all.Where(f => f.Field != ShipField.BuildCost || defaults.BuildCosts != null);
        }

        /// <summary>The entry key in section <see cref="Section"/>: <c>Prefab.Field</c>.</summary>
        public static string Key(string prefab, ShipField field)
        {
            if (string.IsNullOrWhiteSpace(prefab) || prefab.Trim() != prefab) throw new ArgumentException("Expected a trimmed prefab name.", nameof(prefab));
            return prefab + "." + field;
        }

        private static ShipFieldSpec Absolute(ShipField field, Func<ShipDefaults, float> value, float min, float max, string description) =>
            new ShipFieldSpec(field, false, description, value, d => min, d => max);

        /// <summary>0 to 10 times the prefab default; a default of 0 still allows 0 to 1.</summary>
        private static ShipFieldSpec Relative(ShipField field, Func<ShipDefaults, float> value, string description) =>
            new ShipFieldSpec(field, false, description + " Range 0 to 10 times this ship's vanilla value.", value, d => 0f,
                d => value(d) > 0f ? value(d) * RelativeRange : 1f);
    }
}