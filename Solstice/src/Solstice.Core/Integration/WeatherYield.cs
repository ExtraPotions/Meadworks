// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: how Solstice yields the sky to Tempest (meadworks.weather.events 1.0) and to forced environments.
using System;

namespace ExtraPotions.Solstice.Integration
{
    public enum YieldReason { None, TempestEvent, ForcedEnvironment }

    public readonly struct WeatherYieldDecision
    {
        public WeatherYieldDecision(bool yield, YieldReason reason)
        {
            Yield = yield;
            Reason = reason;
        }

        /// <summary>Solstice must not change the sky now.</summary>
        public bool Yield { get; }

        public YieldReason Reason { get; }
    }

    /// <summary>
    /// Solstice shapes the weather only through the biome weather lists (the seasonal add, replace and remove rules). That never fights a
    /// Tempest event, whose environment the game asks for ahead of any list. The one direct touch Solstice has on running weather is the reset
    /// at a season start, which makes the game choose again. The game already prefers an active override on that path, so the deferral is defensive
    /// (no later direct-apply path can regress). Solstice yields that reset: while a Tempest event reaches the local
    /// player, or while the game has a forced or debug environment (an admin command, or a personal override), it waits.
    /// </summary>
    public static class WeatherYield
    {
        /// <summary>The longest Solstice waits for the sky (one hour, the contract's longest phase): after that the reset runs.</summary>
        public const float MaxDeferSeconds = 3600f;

        /// <param name="tempestRunning">The capability reports an event in any phase but idle.</param>
        /// <param name="tempestLocalAffected">The event reaches the biome under the local player.</param>
        /// <param name="gameForced">The game has a forced or debug environment (<c>EnvMan.m_forceEnv</c> or <c>m_debugEnv</c>).</param>
        public static WeatherYieldDecision Decide(bool tempestRunning, bool tempestLocalAffected, bool gameForced)
        {
            if (gameForced) return new WeatherYieldDecision(true, YieldReason.ForcedEnvironment);
            if (tempestRunning && tempestLocalAffected) return new WeatherYieldDecision(true, YieldReason.TempestEvent);
            return new WeatherYieldDecision(false, YieldReason.None);
        }
    }

    /// <summary>A season-start weather reset that waits for the sky to be free, at most <see cref="WeatherYield.MaxDeferSeconds"/>.</summary>
    public sealed class EnvironmentRefresh
    {
        double since;

        public bool Pending { get; private set; }

        /// <summary>Asks for a reset; a second request while one waits keeps the first start of the wait.</summary>
        public void Request(double now)
        {
            if (Pending) return;
            Pending = true;
            since = now;
        }

        /// <summary>True when the reset should run now (once).</summary>
        public bool Tick(double now, WeatherYieldDecision decision)
        {
            if (!Pending) return false;
            if (decision.Yield && now - since < WeatherYield.MaxDeferSeconds) return false;
            Pending = false;
            return true;
        }

        public void Cancel() => Pending = false;
    }
}
