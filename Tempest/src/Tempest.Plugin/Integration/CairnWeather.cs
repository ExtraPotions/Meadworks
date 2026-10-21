// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: whether Cairn's weather display is showing the weather event (capability meadworks.cairn.weather_display 1.0).
using System;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>Shared check for Tempest's own weather notices: false when Cairn is absent, off or fails. Main thread only.</summary>
    public static class CairnWeather
    {
        static bool warned;

        public static bool DisplaysWeather(MeadworksLog? log)
        {
            try
            {
                Func<bool>? display = null;
                return CruciblePlugin.Services.Capabilities.TryResolve("meadworks.cairn.weather_display", new System.Version(1, 0), out display)
                    && display != null && display();
            }
            catch (Exception error)
            {
                if (!warned) log?.Warning("Cairn weather display was unavailable; Tempest keeps its own messages: " + error.GetType().Name);
                warned = true;
                return false;
            }
        }

        public static void Shutdown() => warned = false;
    }
}
