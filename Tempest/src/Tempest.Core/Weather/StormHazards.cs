// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the lingering ground lightning of a strike shocks a player standing in it.
using System;
using ExtraPotions.Tempest.Events;

namespace ExtraPotions.Tempest.Weather
{
    /// <summary>The game status effects a strike's lingering area gives a player standing in it.</summary>
    [Flags]
    public enum HazardEffects
    {
        None = 0,
        Shock = 1,
        Burning = 2,
    }

    /// <summary>
    /// A strike's ground lightning stays on the ground until the game removes the strike object (its own timeout) or the
    /// storm's Active phase ends. While it is there, each client shocks its own player (and, with Strike Starts Fires and no
    /// rain, burns it) when the player stands inside the strike's area, at most once per <see cref="RefreshSeconds"/>.
    /// </summary>
    public static class StormHazards
    {
        /// <summary>Seconds between two shocks of the same player.</summary>
        public const double RefreshSeconds = 1;

        /// <summary>Network object keys the striking client writes on the strike: the storm serial and the hazard's end (world milliseconds).</summary>
        public const string SerialKey = "Tempest_HazardSerial";
        public const string UntilKey = "Tempest_HazardUntil";

        /// <summary>The strike's hazard still exists: same running storm, inside its Active phase and before the strike times out.</summary>
        public static bool Live(int strikeSerial, double until, TempestEvent? current, double now) =>
            current != null && EventKinds.Lightning(current.Kind) && strikeSerial == current.Serial
            && now >= current.ActiveStart && now < current.ClearingStart && now < until;

        /// <summary>The player is inside the area: horizontal distance within the radius and no more than the radius above or below it.</summary>
        public static bool Inside(float dx, float dy, float dz, float radius) =>
            radius > 0 && !float.IsNaN(dx + dy + dz) && dx * dx + dz * dz <= radius * radius && Math.Abs(dy) <= radius;

        /// <summary>A new shock is due: none yet, a second since the last one, or the world clock went back (new world).</summary>
        public static bool Due(double last, double now) => last < 0 || now < last || now - last >= RefreshSeconds;

        /// <summary>The player takes the hazard: setting on and the player neither dead, in god mode nor in ghost mode.</summary>
        public static bool Affects(bool enabled, bool dead, bool godMode, bool ghostMode) => enabled && !dead && !godMode && !ghostMode;

        /// <summary>Shock always; Burning too while Strike Starts Fires is on and it is not raining (rain cancels fire).</summary>
        public static HazardEffects Effects(bool startsFires, bool raining) =>
            HazardEffects.Shock | (startsFires && !raining ? HazardEffects.Burning : HazardEffects.None);

        /// <summary>Damage of one hazard tick: all lightning, or split evenly between lightning and fire while Burning applies. 0: status effects only.</summary>
        public static (float Lightning, float Fire) Damage(HazardEffects effects, float damage)
        {
            if (float.IsNaN(damage) || damage <= 0) return (0, 0);
            return (effects & HazardEffects.Burning) != 0 ? (damage / 2, damage / 2) : (damage, 0);
        }

        /// <summary>
        /// After the hazard's hit, Tempest adds the game's Burning itself when fire applies and the player has none: the game
        /// drops a fire share too small after armor (low Storm Hazard Damage) without adding Burning.
        /// </summary>
        public static bool AddBurning(HazardEffects effects, bool hasBurning, bool dead) => (effects & HazardEffects.Burning) != 0 && !hasBurning && !dead;

        /// <summary>
        /// Burning the Ash Storm added ends with the storm: the hazard is off, or the Ash Storm (serial
        /// <paramref name="burnedSerial"/>) is gone or outside its Active phase. 0: nothing added.
        /// </summary>
        public static bool AshBurnEnded(int burnedSerial, TempestEvent? current, double now, bool enabled) =>
            burnedSerial != 0 && (!enabled || current == null || current.Serial != burnedSerial || now < current.ActiveStart || now >= current.ClearingStart);

        /// <summary>The same for the Ash Storm burn, which runs only once <see cref="AshBurns"/> and the shelter check allowed it.</summary>
        public static bool AshAddBurning(bool hasBurning, bool dead) => AddBurning(HazardEffects.Burning, hasBurning, dead);

        /// <summary>Seconds between two Ash Storm burns of the same player.</summary>
        public const double AshSeconds = 5;

        /// <summary>
        /// An Ash Storm burns the local player: the storm's Active phase where the player stands, Storm Hazards Affect Players
        /// on, and the player not dead, in god or ghost mode, indoors (a dungeon or other interior) or under a roof (the game's
        /// own cover check, <c>Cover.IsUnderRoof</c>).
        /// </summary>
        public static bool AshBurns(TempestEvent? current, double now, WeatherBiome biome, bool forced, bool enabled, bool dead, bool godMode, bool ghostMode,
            bool interior, bool underRoof) =>
            current != null && current.Kind == EventKind.AshStorm && !forced && current.Affects(biome)
            && Timeline.At(current, now).Phase == EventPhase.Active
            && Affects(enabled, dead, godMode, ghostMode) && !interior && !underRoof;

        /// <summary>An Ash Storm burn is due: none yet, <see cref="AshSeconds"/> since the last one, or the world clock went back.</summary>
        public static bool AshDue(double last, double now) => last < 0 || now < last || now - last >= AshSeconds;

        /// <summary>Fire damage of one Ash Storm burn: Storm Hazard Damage, all fire (0: the Burning effect only).</summary>
        public static float AshDamage(float damage) => float.IsNaN(damage) || damage <= 0 ? 0 : damage;

        /// <summary>World milliseconds stored on the strike for the hazard's end.</summary>
        public static long UntilMilliseconds(double now, float lingerSeconds) =>
            (long)Math.Round((now + Math.Max(0, float.IsNaN(lingerSeconds) ? 0 : lingerSeconds)) * 1000);
    }
}
