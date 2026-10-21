// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: publishes the shared meadworks.ember.sleep 1.0 read capability (Cairn interop, design E05).
using System;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Ember.Sleep;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>
    /// Provides <see cref="EmberSleepContract"/> (<c>meadworks.ember.sleep</c> 1.0) to Cairn and any other HUD. The
    /// reading is refreshed on the plugin tick: the authority's own state on the host and in single player, the last
    /// authority reply on a client; required count and skip progress come from Ember.Core's <see cref="SleepReading"/>.
    /// The delegate only packs the stored reading, so it is cheap and never touches the game.
    /// </summary>
    public static class SleepCapability
    {
        static readonly SkipProgressTracker skip = new SkipProgressTracker();

        /// <summary>The current reading; closed while the sleep module is off.</summary>
        public static SleepReading Reading { get; private set; }

        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability<Func<Tuple<bool, int, int, int, double, bool, float>>>(EmberSleepContract.Id, EmberSleepContract.ContractVersion, Provide);
        }

        static Tuple<bool, int, int, int, double, bool, float> Provide()
        {
            var r = Reading;
            return EmberSleepContract.Pack(new EmberSleepState(r.WindowOpen, r.Players, r.Sleeping, r.Required, r.ExtraRate, r.SkipRunning, r.SkipProgress));
        }

        /// <summary>Called every frame after <see cref="SleepRuntime.Tick"/>.</summary>
        public static void Tick()
        {
            var env = EnvMan.instance;
            var znet = ZNet.instance;
            if (!EmberModules.Active(EmberModule.Sleep) || env == null || znet == null)
            {
                Reset();
                return;
            }
            var server = znet.IsServer();
            var state = server ? SleepRuntime.Current : SleepRuntime.Received ?? default;
            var skipping = server && env.IsTimeSkipping();
            var now = znet.GetTimeSeconds();
            // The game skips to the next morning start; read the target the same way the game computes it.
            var target = skipping ? env.m_skipToTime : EmberSeasons.NightActive ? EmberSeasons.NextMorning(env)
                : env.GetMorningStartSec(env.GetDay(now - env.m_dayLengthSec * 0.15) + 1);
            var running = state.FullSkip || skipping;
            Reading = SleepReading.From(state, skipping, skip.Update(running, now, target));
        }

        public static void Reset()
        {
            skip.Reset();
            Reading = SleepReading.Closed;
        }
    }
}
