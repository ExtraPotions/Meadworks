// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace ExtraPotions.Inscribe.Adopted.Rules
{
    internal static class RuleReload
    {
        private static Action? poll;
        private static readonly List<Action> extraPolls = new List<Action>();
        internal static void AddPoll(Action action) => extraPolls.Add(action);
        private static float next;
        internal static readonly List<Action> SessionChecks = new List<Action>();
        internal static readonly List<Action> Stops = new List<Action>();
        public static void Attach(Action action) { poll = action; next = 0; }
        public static void Tick()
        {
            foreach (var check in SessionChecks) check();
            if (UnityEngine.Time.unscaledTime < next) return;
            next = UnityEngine.Time.unscaledTime + 5;
            try { poll?.Invoke(); foreach (var extra in extraPolls) extra(); } catch (Exception e) { Core.Log.Error("Inscribe YAML reload: " + e.Message); }
        }
        public static void Reset() { poll = null; extraPolls.Clear(); foreach (var stop in Stops) stop(); Stops.Clear(); SessionChecks.Clear(); }
    }
}
