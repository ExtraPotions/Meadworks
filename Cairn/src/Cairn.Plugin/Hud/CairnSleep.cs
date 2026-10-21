// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: consumes the existing Ember sleep v1 capability using CairnTempest's bounded lookup pattern.
using System;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Meadworks.Contracts;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using SleepProvider = System.Func<System.Tuple<bool, int, int, int, double, bool, float>>;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnSleep
    {
        static SleepProvider? provider;
        static float nextLook;
        public static SleepDisplay Line { get; private set; }

        /// <summary>Ember's sleep capability answered this frame (whether or not anyone is in bed).</summary>
        public static bool Reading { get; private set; }

        public static string Text => !Line.Visible ? "" : HudText.Join(CairnText.Format("sleep_sleeping", Line.Sleeping, Line.Players),
            Line.SkipPercent.Length > 0 ? CairnText.Format("sleep_skip", Line.SkipPercent) : CairnText.Format("sleep_boost", Line.BoostPercent));

        public static void Tick()
        {
            Line = default;
            Reading = false;
            if (!Plugin.Ready || Plugin.Settings == null || Player.m_localPlayer == null ||
                !Plugin.Settings.Get(CairnSettings.Hud.Enabled) || CairnModules.StepAside.SteppedAside(CairnModule.Hud)) return;
            if (provider == null && Time.unscaledTime >= nextLook)
            {
                nextLook = Time.unscaledTime + 5;
                try { CruciblePlugin.Services.Capabilities.TryResolve(EmberSleepContract.Id, EmberSleepContract.ContractVersion, out provider); }
                catch (Exception) { provider = null; }
            }
            if (EmberSleepContract.TryRead(provider, out var state))
            {
                Reading = true;
                Line = new SleepDisplay(state.WindowOpen, state.Players, state.Sleeping, state.ExtraRate, state.SkipRunning, state.SkipProgress);
            }
        }

        public static void Reset() { provider = null; nextLook = 0; Line = default; Reading = false; }
    }
}
