// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: provider of meadworks.weather.events 1.0 (Crucible WeatherEventsContract, cda9696) and the C11 lines.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Integration;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// Provides <c>meadworks.weather.events</c> 1.0 to Cairn and any HUD. The shape is the contract's BCL delegate
    /// <c>Func&lt;Tuple&lt;int,int,float,int,bool,int&gt;&gt;</c> (kind, phase, seconds left, biomes, local affected, revision),
    /// so Tempest needs no newer Crucible.Contracts assembly than its 0.3.7 floor. Idle while the Events module is off.
    /// </summary>
    public static class WeatherCapability
    {
        static readonly RevisionCounter Revisions = new RevisionCounter();
        static readonly RevisionCounter DetailRevisions = new RevisionCounter();

        public static void Register(ProductRegistration registration)
        {
            registration.AddCapability<Func<Tuple<int, int, float, int, bool, int>>>(WeatherEventsView.Id, WeatherEventsView.ContractVersion, Provide);
            registration.AddCapability<Func<Tuple<int, int, float, int, bool, int, string>>>(WeatherEventsDetail.Id, WeatherEventsDetail.ContractVersion, ProvideDetail);
            registration.AddCapability<Func<IReadOnlyList<string>>>("meadworks.diagnostics." + Plugin.Guid, new System.Version(1, 0), Describe);
        }

        public static WeatherEventsView Read()
        {
            if (!TempestModules.Active(TempestModule.Events)) return default;
            return WeatherEventsView.Read(TempestWorld.Current, TempestWorld.Now, TempestWorld.LocalBiome, Forced());
        }

        static bool Forced()
        {
            var envMan = EnvMan.instance;
            return envMan != null && (!string.IsNullOrEmpty(envMan.m_forceEnv) || !string.IsNullOrEmpty(envMan.m_debugEnv));
        }

        /// <summary>meadworks.weather.events.detail 1.0: the 1.0 items with the open kind number, plus the kind's name token.</summary>
        static Tuple<int, int, float, int, bool, int, string> ProvideDetail()
        {
            var (view, token) = TempestModules.Active(TempestModule.Events)
                ? WeatherEventsView.ReadDetail(TempestWorld.Current, TempestWorld.Now, TempestWorld.LocalBiome, Forced())
                : (default(WeatherEventsView), "");
            var revision = DetailRevisions.Observe(view);
            return Tuple.Create(view.Kind, view.Phase, view.SecondsLeft, view.Biomes, view.LocalAffected, revision, token);
        }

        static Tuple<int, int, float, int, bool, int> Provide()
        {
            var view = Read();
            var revision = Revisions.Observe(view);
            return Tuple.Create(view.Kind, view.Phase, view.SecondsLeft, view.Biomes, view.LocalAffected, revision);
        }

        static IReadOnlyList<string> Describe()
        {
            var lines = new List<string>();
            var aside = TempestModules.StepAside;
            lines.Add("modules: events " + (TempestModules.Active(TempestModule.Events) ? "on" : "off") + ", filter " + (TempestModules.Active(TempestModule.Filter) ? "on" : "off")
                + ", override " + (TempestModules.Active(TempestModule.Override) ? "on" : "off") + (aside.Any ? " (steps aside: " + string.Join(", ", aside.Notices.Select(n => n.SourceName)) + ")" : ""));
            var state = TempestWorld.State;
            var e = state.Current;
            lines.Add(e == null ? "event: none" : "event: " + e + ", " + Timeline.At(e, TempestWorld.Now).Phase);
            lines.Add("last rolled day " + state.LastRolledDay + ", next serial " + state.NextSerial + ", applying " + (TempestSky.Applying ?? "-"));
            return lines;
        }
    }
}
