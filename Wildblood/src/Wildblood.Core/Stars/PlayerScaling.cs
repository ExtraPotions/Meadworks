// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0, adapted from CreatureHarmonyPatches.cs (CreatureGameSettings.ApplyDifficulty)
// and CreatureLevelManager.cs (ShouldApplyLevelScale) at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: Wildblood namespace; the values come from the Pressure settings and the module switch decides whether they act.
// SPDX-License-Identifier: GPL-3.0-only
using System;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>The game's multiplayer difficulty values, as CreatureManager sets them on Game: per extra nearby player and the player cap.</summary>
    public readonly struct PlayerScalingValues
    {
        public PlayerScalingValues(float healthPerPlayer, float damagePerPlayer, int maxPlayers)
        {
            HealthPerPlayer = healthPerPlayer;
            DamagePerPlayer = damagePerPlayer;
            MaxPlayers = maxPlayers;
        }

        /// <summary>Fraction of extra creature health per nearby player after the first (0.3 is +30%).</summary>
        public float HealthPerPlayer { get; }

        public float DamagePerPlayer { get; }
        public int MaxPlayers { get; }
    }

    public static class PlayerScaling
    {
        /// <summary>Percent settings to the game's fractions: 0 to 200 percent (non-finite counts as 0) and 1 to 25 players.</summary>
        public static PlayerScalingValues Resolve(float healthPercent, float damagePercent, int maxPlayers) =>
            new PlayerScalingValues(Fraction(healthPercent), Fraction(damagePercent), Math.Min(25, Math.Max(1, maxPlayers)));

        static float Fraction(float percent) => float.IsNaN(percent) || float.IsInfinity(percent) ? 0f : Math.Min(200f, Math.Max(0f, percent)) / 100f;

        /// <summary>Whether the Wildblood values are written to the game: the Pressure module is on and the game object exists.</summary>
        public static bool Acts(bool pressureModuleOn, bool gameExists) => pressureModuleOn && gameExists;

        /// <summary>Exact equality: the fields hold the very values Wildblood wrote unless something else wrote them.</summary>
        public static bool Same(PlayerScalingValues a, PlayerScalingValues b) =>
            a.HealthPerPlayer.Equals(b.HealthPerPlayer) && a.DamagePerPlayer.Equals(b.DamagePerPlayer) && a.MaxPlayers == b.MaxPlayers;
    }

    public static class SaddleScaling
    {
        /// <summary>CreatureManager's rule: a saddle-able creature keeps its size at any star count while Scale Saddle-able Creatures is off.</summary>
        public static bool Applies(bool scaleSaddleable, bool isSaddleable) => scaleSaddleable || !isSaddleable;
    }
}
