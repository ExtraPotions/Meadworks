// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, ShipConfig 1.4.2, GPL-3.0-or-later.
// Adapted from ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1:
// ShipConfig/ShipConfig/ShipDefaults.cs, ShipEntries.cs, ShipMultipliers.cs, ShipBuildCost.cs, ShipDamagePatches.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Keel.Tuning; Unity-free (System.Math, Mathf.RoundToInt as Math.Round half to
// even); the values are plain data passed in instead of BepInEx config entries, so the effective-value rules are
// testable; the per-ship AshlandsOceanDamage and Invulnerable entries became the Keel cheat lists (design D1, D2), so
// Ashlands readiness is the prefab's own value or the cheat; the TakesNoDamage rule of ShipEntries and the damage
// patches is one property here.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Keel.Tuning
{
    /// <summary>
    /// The vanilla values of one ship, read from its prefab, or from a live instance when the prefab is not in the
    /// scene (the plugin reads them). They become the defaults of the ship's entries, so a fresh config leaves the game
    /// unchanged (weather wear excepted, user decision Q1).
    /// </summary>
    public sealed class ShipDefaults
    {
        public float Health;
        public float SailForce;
        public float SailForceOffset;
        public float PaddleForce;
        public float RudderSpeed;
        public float TurnForceSailing;
        public float TurnForcePaddling;
        public float ForwardDrag;
        public float SidewaysDrag;
        public float AngularDamping;
        public float WaterImpactDamage;
        public float UpsideDownDamage;

        /// <summary>The game's <c>WearNTear.m_noRoofWear</c> on the prefab (restored while Keel's tuning is off).</summary>
        public bool WeatherWear;

        /// <summary>The game's <c>Ship.m_ashlandsReady</c>: the ship sails the Ashlands ocean unharmed (vanilla: the Drakkar).</summary>
        public bool AshlandsReady;

        /// <summary>The vanilla material amounts of the Piece; null when the ship has no Piece (nothing to build).</summary>
        public int[]? BuildCosts;
    }

    /// <summary>One ship's per-ship values as configured (design 4.2), before the global multipliers.</summary>
    public sealed class ShipSettings
    {
        public float Health;
        public float SailForce;
        public float SailForceOffset;
        public float PaddleForce;
        public float RudderSpeed;
        public float TurnForceSailing;
        public float TurnForcePaddling;
        public float ForwardDrag;
        public float SidewaysDrag;
        public float AngularDamping;
        public float WaterImpactDamage;
        public float UpsideDownDamage;
        public bool WeatherWear;
        public float DamageTaken = 1f;
        public float BuildCost = 1f;

        /// <summary>The prefab's own Ashlands readiness (not a setting; the cheat list can add a ship).</summary>
        public bool AshlandsReady;

        /// <summary>The prefab's own values; weather wear off (user decision Q1), damage and cost factors 1.</summary>
        /// <summary>The game's own values, weather wear included: what a ship gets back while Keel's tuning is off.</summary>
        public static ShipSettings Vanilla(ShipDefaults d)
        {
            var s = FromDefaults(d);
            s.WeatherWear = d.WeatherWear;
            return s;
        }

        public static ShipSettings FromDefaults(ShipDefaults d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            return new ShipSettings
            {
                Health = d.Health, SailForce = d.SailForce, SailForceOffset = d.SailForceOffset, PaddleForce = d.PaddleForce,
                RudderSpeed = d.RudderSpeed, TurnForceSailing = d.TurnForceSailing, TurnForcePaddling = d.TurnForcePaddling,
                ForwardDrag = d.ForwardDrag, SidewaysDrag = d.SidewaysDrag, AngularDamping = d.AngularDamping,
                WaterImpactDamage = d.WaterImpactDamage, UpsideDownDamage = d.UpsideDownDamage, AshlandsReady = d.AshlandsReady,
            };
        }
    }

    /// <summary>Section Multipliers: global factors on top of the per-ship values; values below 0 count as 0.</summary>
    public readonly struct GlobalMultipliers
    {
        public GlobalMultipliers(float health, float sailForce, float paddleForce, float turning, float damageTaken, float buildCost)
        {
            Health = Positive(health);
            SailForce = Positive(sailForce);
            PaddleForce = Positive(paddleForce);
            Turning = Positive(turning);
            DamageTaken = Positive(damageTaken);
            BuildCost = Positive(buildCost);
        }

        public static GlobalMultipliers Vanilla => new GlobalMultipliers(1f, 1f, 1f, 1f, 1f, 1f);

        public static GlobalMultipliers From(float health, float sailForce, float paddleForce, float turning, float damageTaken, float buildCost) =>
            new GlobalMultipliers(health, sailForce, paddleForce, turning, damageTaken, buildCost);

        public float Health { get; }
        public float SailForce { get; }
        public float PaddleForce { get; }
        public float Turning { get; }
        public float DamageTaken { get; }
        public float BuildCost { get; }

        internal static float Positive(float value) => float.IsNaN(value) || value < 0f ? 0f : value;
    }

    /// <summary>The values Keel writes into a ship prefab and every loaded ship of that kind.</summary>
    public readonly struct EffectiveShip
    {
        public EffectiveShip(ShipSettings s, GlobalMultipliers m, bool invulnerable, bool ashlandsImmune)
        {
            Health = GlobalMultipliers.Positive(s.Health) * m.Health;
            SailForce = GlobalMultipliers.Positive(s.SailForce) * m.SailForce;
            // Not clamped: negative moves the point below the centre of mass, which is the point of lowering it.
            SailForceOffset = s.SailForceOffset;
            PaddleForce = GlobalMultipliers.Positive(s.PaddleForce) * m.PaddleForce;
            RudderSpeed = GlobalMultipliers.Positive(s.RudderSpeed);
            TurnForceSailing = GlobalMultipliers.Positive(s.TurnForceSailing) * m.Turning;
            TurnForcePaddling = GlobalMultipliers.Positive(s.TurnForcePaddling) * m.Turning;
            ForwardDrag = GlobalMultipliers.Positive(s.ForwardDrag);
            SidewaysDrag = GlobalMultipliers.Positive(s.SidewaysDrag);
            AngularDamping = GlobalMultipliers.Positive(s.AngularDamping);
            WaterImpactDamage = GlobalMultipliers.Positive(s.WaterImpactDamage);
            UpsideDownDamage = GlobalMultipliers.Positive(s.UpsideDownDamage);
            DamageTaken = GlobalMultipliers.Positive(s.DamageTaken) * m.DamageTaken;
            BuildCost = GlobalMultipliers.Positive(s.BuildCost) * m.BuildCost;
            WeatherWear = s.WeatherWear;
            AshlandsReady = s.AshlandsReady || ashlandsImmune;
            Invulnerable = invulnerable;
        }

        public float Health { get; }
        public float SailForce { get; }
        public float SailForceOffset { get; }
        public float PaddleForce { get; }
        public float RudderSpeed { get; }
        public float TurnForceSailing { get; }
        public float TurnForcePaddling { get; }
        public float ForwardDrag { get; }
        public float SidewaysDrag { get; }
        public float AngularDamping { get; }
        public float WaterImpactDamage { get; }
        public float UpsideDownDamage { get; }
        public float DamageTaken { get; }
        public float BuildCost { get; }

        /// <summary>The game's <c>WearNTear.m_noRoofWear</c>: rain and submerged wear down to half health.</summary>
        public bool WeatherWear { get; }

        /// <summary>The game's <c>Ship.m_ashlandsReady</c>.</summary>
        public bool AshlandsReady { get; }

        public bool Invulnerable { get; }

        /// <summary>Invulnerable, or an effective damage factor of 0: the ship takes no damage at all.</summary>
        public bool TakesNoDamage => Invulnerable || DamageTaken <= 0f;
    }

    public static class ShipTuning
    {
        public static EffectiveShip Compute(ShipSettings settings, GlobalMultipliers multipliers, bool invulnerable, bool ashlandsImmune)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new EffectiveShip(settings, multipliers, invulnerable, ashlandsImmune);
        }

        /// <summary>
        /// The material amounts placement uses: vanilla amount times the effective cost, rounded like
        /// <c>Mathf.RoundToInt</c> (half to even), never below 1. Always computed from the vanilla amounts recorded at
        /// discovery, so repeated changes never compound. Null without a Piece.
        /// </summary>
        public static int[]? BuildAmounts(int[]? originals, float effectiveCost)
        {
            if (originals == null) return null;
            var amounts = new int[originals.Length];
            for (var i = 0; i < amounts.Length; i++)
                amounts[i] = Math.Max(1, (int)Math.Round(originals[i] * effectiveCost));
            return amounts;
        }
    }
}