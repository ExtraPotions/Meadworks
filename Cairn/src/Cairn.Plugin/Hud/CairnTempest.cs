// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: CA09, Tempest's weather event read through the Crucible contract meadworks.weather.events.detail 1.0
// when Tempest provides it, else meadworks.weather.events 1.0 (Crucible main cda9696). The HUD row (CA06b) reads Line and Text. The center notice (once per warning) is held until
// that row draws (TempestNotice.CentreEnabled; Group 2 follow-up of 2026-10-08).
using System;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Cairn.Localization;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using WeatherProvider = System.Func<System.Tuple<int, int, float, int, bool, int>>;
using DetailProvider = System.Func<System.Tuple<int, int, float, int, bool, int, string>>;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnTempest
    {
        /// <summary>Seconds between looks for a provider while none is registered.</summary>
        const float LookInterval = 5f;

        static WeatherProvider? provider;
        static DetailProvider? detail;
        static float nextLook;
        static long session = -1;
        static readonly TempestNotice notice = new TempestNotice();

        /// <summary>The HUD's Tempest row for this frame; <see cref="TempestLine.None"/> without Tempest or where it does not reach.</summary>
        public static TempestLine Line { get; private set; } = TempestLine.None;

        /// <summary>The row as text in the player's language; empty when there is no row.</summary>
        public static string Text(TempestLine line)
        {
            switch (line.Kind)
            {
                case TempestLineKind.Warning: return CairnText.Format(line.TextKey, CairnText.Text(line.EventKey), line.Countdown);
                case TempestLineKind.Active: return CairnText.Text(line.TextKey);
                case TempestLineKind.Clearing: return CairnText.Format(line.TextKey, CairnText.Text(line.EventKey));
                default: return "";
            }
        }

        /// <summary>Once per frame on a graphical client while the HUD module runs: reads the event and updates the row;
        /// once the HUD row draws (CA06b) it also shows the center notice when a warning starts where the player is.</summary>
        public static void Tick(MeadworksLog log)
        {
            Line = TempestLine.None;
            var settings = Plugin.Settings;
            var player = Player.m_localPlayer;
            if (!Plugin.Ready || settings == null || player == null || CairnModules.StepAside.SteppedAside(CairnModule.Hud)) return;
            var generation = CruciblePlugin.Session.Generation;
            if (generation != session)
            {
                session = generation;
                notice.Reset();
            }
            if (!settings.Get(CairnSettings.Hud.Enabled) || !settings.Get(CairnSettings.Hud.TempestWarnings)) return;
            if (!Read(out var state) || !state.Running) return;
            int local = (int)player.GetCurrentBiome();
            bool reaches = TempestWarning.Reaches(state.Biomes, local, state.LocalAffected);
            Line = TempestWarning.Line(true, (TempestKind)state.Kind, state.Phase, state.SecondsLeft, state.Biomes, local, state.LocalAffected,
                state.Token.Length > 0 ? state.Token : null);
            pendingRevision = state.Revision;
            pendingPhase = (TempestPhase)(int)state.Phase;
            pendingReaches = reaches;
        }

        /// <summary>The detail contract first (every Tempest kind with its name); a Tempest without it is read through 1.0.</summary>
        static bool Read(out TempestReading reading)
        {
            Look();
            if (detail != null) return TempestDetail.TryRead(detail, out reading);
            reading = default;
            if (!WeatherEventsContract.TryRead(provider, out var state)) return false;
            reading = new TempestReading((int)state.Kind, (TempestPhase)(int)state.Phase, state.SecondsLeft, state.Biomes, state.LocalAffected, state.Revision, "");
            return true;
        }

        public static void Reset()
        {
            provider = null;
            detail = null;
            nextLook = 0f;
            session = -1;
            notice.Reset();
            Line = TempestLine.None;
            pendingReaches = false;
        }

        static int pendingRevision;
        static TempestPhase pendingPhase;
        static bool pendingReaches;

        /// <summary>Runs after the HUD has decided this frame's actual visibility.</summary>
        public static void Present(MeadworksLog log)
        {
            if (Line.Kind != TempestLineKind.None && TempestNotice.CentreEnabled && notice.Update(pendingRevision, pendingPhase, pendingReaches))
                CairnText.Centre(CairnText.Format(CairnTextKeys.TempestNotice, CairnText.Text(Line.EventKey)), log);
        }

        static void Look()
        {
            if (detail != null || Time.unscaledTime < nextLook) return;
            nextLook = Time.unscaledTime + LookInterval;
            try { CruciblePlugin.Services.Capabilities.TryResolve(TempestDetail.Id, TempestDetail.ContractVersion, out detail); }
            catch (Exception) { detail = null; }
            if (detail != null || provider != null) return;
            try { CruciblePlugin.Services.Capabilities.TryResolve(WeatherEventsContract.Id, WeatherEventsContract.ContractVersion, out provider); }
            catch (Exception) { provider = null; }
        }
    }
}
