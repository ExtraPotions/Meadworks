// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the world key mw.solstice.clock, written by the server and read by clients. Same channel as Tempest's weather record.
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Integration;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// The saved season. The server reconciles it with the world day and writes <see cref="SeasonClockKey.Value"/>.
    /// A client shows that text and does not invent a season while it is missing. The Season Override cheat is not stored.
    /// Stepping aside or shutting down does not remove the key. Main thread only.
    /// </summary>
    public static class SeasonClockSync
    {
        static MeadworksLog? log;
        static ConfigEntry<string>? fallback;
        static WorldSettingBinding? binding;
        static long world;
        static SeasonClock? current;
        static string? lastWritten;
        static bool available;
        static WorldSettingWriteStatus lastWrite = WorldSettingWriteStatus.Applied;
        static bool warnedNewer;
        static bool warnedCorrupt;

        /// <summary>The clock in force, or null while this peer has none.</summary>
        public static SeasonClock? Current => available ? current : null;

        public static void Initialize(MeadworksLog logger, ConfigFile config)
        {
            log = logger;
            fallback = MeadworksConfig.BindHidden(config, "World", "Clock", "",
                "Managed by Solstice: required fallback of the guarded world key " + SeasonClockKey.Value + ". The season clock lives in the world, not in this file.",
                ConfigScope.ServerAuthoritative);
            binding = MeadworksWorldSettings.Register(Plugin.Guid, SeasonClockKey.Value, fallback, SeasonClockText.Canonical);
        }

        /// <summary>Reads the world key and, on the server, advances it. True when the clock this peer shows changed.</summary>
        public static bool Advance(int worldDay, SeasonCalendar lengths)
        {
            var before = Current;
            if (lengths == null || ZNet.instance == null || ZNet.World == null || ZoneSystem.instance == null || !SolsticeModules.Active(SolsticeModule.Seasons))
            {
                ClearSession();
                return before.HasValue;
            }
            long uid = ZNet.instance.GetWorldUID();
            if (uid != world)
            {
                ClearSession();
                world = uid;
                warnedNewer = false;
                warnedCorrupt = false;
            }
            bool server = ZNet.instance.IsServer();
            var text = binding?.WorldText;
            // The guarded binding canonicalizes unknown versions to null; inspect saved bytes before treating null as absent.
            if (string.IsNullOrEmpty(text) && ZoneSystem.instance.GetGlobalKey(SeasonClockKey.Value, out string stored))
            {
                if (!WorldSettingStorage.TryDecode(uid, stored, out text) || string.IsNullOrEmpty(text))
                {
                    if (!warnedCorrupt)
                    {
                        warnedCorrupt = true;
                        log?.Warning("Season clock " + SeasonClockKey.Value + " has an unreadable world record. Solstice preserves it and leaves seasonal effects off.");
                    }
                    available = false;
                    current = null;
                    return !Same(before, Current);
                }
            }
            if (string.IsNullOrEmpty(text))
            {
                if (!server)
                {
                    available = false;
                    current = null;
                    return !Same(before, Current);
                }
                if (current.HasValue && lastWritten != null)
                {
                    var kept = SeasonClocks.Reconcile(current.Value, worldDay, lengths);
                    if (!kept.Equals(current.Value)) Write(kept);
                    else available = true;
                    return !Same(before, Current);
                }
                Write(SeasonClocks.Seed(worldDay, lengths));
                return !Same(before, Current);
            }
            if (!SeasonClockText.TryParse(text, out var parsed, out var parse))
            {
                if (parse == ClockParse.Newer)
                {
                    if (!warnedNewer)
                    {
                        warnedNewer = true;
                        log?.Warning("Season clock " + SeasonClockKey.Value + " is a newer version. Solstice leaves it unread and does not overwrite it.");
                    }
                    available = false;
                    current = null;
                    return !Same(before, Current);
                }
                if (!warnedCorrupt)
                {
                    warnedCorrupt = true;
                    log?.Warning("Season clock " + SeasonClockKey.Value + " could not be read. Solstice preserves it and leaves seasonal effects off.");
                }
                available = false;
                current = null;
                return !Same(before, Current);
            }
            warnedCorrupt = false;
            warnedNewer = false;
            if (!server)
            {
                current = parsed;
                available = true;
                lastWritten = text;
                return !Same(before, Current);
            }
            var next = SeasonClocks.Reconcile(parsed, worldDay, lengths);
            if (!next.Equals(parsed)) Write(next);
            else
            {
                current = next;
                available = true;
                lastWritten = SeasonClockText.Format(next);
            }
            return !Same(before, Current);
        }

        public static void Shutdown()
        {
            ClearSession();
            binding?.Dispose();
            binding = null;
            fallback = null;
            log = null;
        }

        /// <summary>Drops the in-memory clock. The world key stays.</summary>
        public static void ClearSession()
        {
            current = null;
            available = false;
            lastWritten = null;
            world = 0;
        }

        static void Write(SeasonClock clock)
        {
            if (binding == null) return;
            var text = SeasonClockText.Format(clock);
            var result = binding.Set(text);
            if (result.Status != lastWrite)
            {
                lastWrite = result.Status;
                if (result.Status != WorldSettingWriteStatus.Applied && result.Status != WorldSettingWriteStatus.Queued)
                    log?.Warning("Season clock not written: " + result.Status + ".");
            }
            if (result.Status != WorldSettingWriteStatus.Applied && result.Status != WorldSettingWriteStatus.Queued) return;
            current = clock;
            lastWritten = text;
            available = true;
        }

        static bool Same(SeasonClock? left, SeasonClock? right) =>
            left.HasValue == right.HasValue && (!left.HasValue || left.Value.Equals(right!.Value));
    }
}
