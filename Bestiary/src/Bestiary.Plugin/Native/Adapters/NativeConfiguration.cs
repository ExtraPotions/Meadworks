// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Bestiary.Runtime.Integration;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Bestiary.Runtime.Native.Adapters
{
    /// <summary>Source-shaped reads of already-bound C01 entries, never a second configuration manager.</summary>
    public sealed class GatedEntry<T>
    {
        private readonly SettingSpec spec;
        internal GatedEntry(SettingSpec spec) { this.spec = spec; }
        public T Value
        {
            get
            {
                // The portable catalog stores BiomeMask; C01 binds its native Heightmap.Biome equivalent.
                T value = typeof(T) == typeof(Heightmap.Biome)
                    ? (T)(object)(Heightmap.Biome)(int)Plugin.Settings!.Biomes(spec.Id)
                    : Plugin.Settings!.Get<T>(spec);
                if (value is bool flag && (spec.Key == "Enabled" || spec.Key == "Recipe" || spec.Key.StartsWith("Skeleton ", StringComparison.Ordinal)))
                    return (T)(object)(flag && NativeConfiguration.Active);
                return value;
            }
        }
        public object DefaultValue => spec.Default;
    }
    public sealed class NativeConfiguration
    {
        internal static bool Active => Plugin.Ready && !BestiaryIntegration.ProductYields &&
            MeadworksAuthority.ProductActive(Plugin.Guid, out _) && Plugin.Settings?.Flag(BestiarySettings.General + ".Enabled") == true;
        public GatedEntry<T> Bind<T>(string section, string key, T defaultValue, string description, bool synced = true, AcceptableValueBase? acceptableValues = null, params object[] tags)
        {
            var spec = BestiarySettings.All.FirstOrDefault(row => row.Section == section && row.Key == key)
                ?? throw new InvalidOperationException("Unmapped native Bestiary setting: " + section + "/" + key);
            if (Plugin.Settings!.Raw(spec) is not ConfigEntry<T>) throw new InvalidOperationException("Native setting type mismatch: " + spec.Id);
            return new GatedEntry<T>(spec);
        }
    }
    internal static class Guard
    {
        internal static int Failures { get; private set; }
        public static void Report(Exception error, string context)
        {
            Failures++;
            if (Admit(context + "|" + error.GetType().FullName, out int suppressed))
                Core.Log.Error(context + ": " + error + (suppressed > 0 ? " (" + suppressed + " more in the last minute)" : ""));
        }

        // Many callers run every frame (Update guards, hot patches): a fault that persists is logged in full the first time,
        // then at most once a minute per context and exception type, with the count of the ones held back.
        private const double RepeatSeconds = 60;
        private static readonly System.Collections.Generic.Dictionary<string, (double At, int Held)> logged =
            new System.Collections.Generic.Dictionary<string, (double At, int Held)>(StringComparer.Ordinal);
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        internal static bool Admit(string key, out int suppressed)
        {
            lock (logged)
            {
                double now = clock.Elapsed.TotalSeconds;
                suppressed = 0;
                if (logged.TryGetValue(key, out var last) && now - last.At < RepeatSeconds)
                {
                    logged[key] = (last.At, last.Held + 1);
                    return false;
                }
                if (last.At > 0 || last.Held > 0) suppressed = last.Held;
                logged[key] = (now, 0);
                return true;
            }
        }
        public static void Run(string context, Action action) { try { action(); } catch (Exception error) { Report(error, context); } }
    }
}
