// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the shared event state (design 1.2, 3, W5, W6, W11; deviation: Crucible-guarded world key transport).
using System;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Localization;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// Holds the shared event. The authority (single player, host, dedicated server) runs the scheduler and writes the
    /// record to the Crucible-guarded world key <see cref="TempestCore.WorldKey"/> through a CS4 world setting binding
    /// (<c>MeadworksWorldSettings</c>): the game saves it with the world and sends it to every peer, so clients and late
    /// joiners read the same event and a restart resumes it, and Crucible refuses writes from anyone but the server. A
    /// session 1 record under <see cref="TempestCore.LegacyWorldKey"/> is read only and copied once by the authority.
    /// The dry flag travels in its own guarded key <see cref="TempestCore.DryWorldKey"/>, so clients without dry storms read
    /// the record unchanged and see a normal thunderstorm. Dense Fog, Ash Storm and Meteor Shower travel in the guarded key
    /// <see cref="TempestCore.ExtendedWorldKey"/> while the record holds no event, so older clients see no event at all.
    /// Main thread only.
    /// </summary>
    public static class TempestWorld
    {
        const float TickSeconds = 1f;
        const double DefaultDayLength = 1800;

        static ScheduleState state = ScheduleState.Empty;
        static string? lastText;
        static string? lastMark;
        static string? lastExtended;
        static float sinceTick = TickSeconds;
        static int seenSerial;
        static EventPhase seenPhase = EventPhase.Idle;
        static bool seenAffected;
        static long world;
        static MeadworksLog? log;
        static ConfigEntry<string>? fallback;
        static WorldSettingBinding? binding;
        static ConfigEntry<string>? dryFallback;
        static WorldSettingBinding? dryBinding;
        static ConfigEntry<string>? extendedFallback;
        static WorldSettingBinding? extendedBinding;

        /// <summary>The event record as last read or written (Empty while the Events module is off).</summary>
        public static ScheduleState State => state;

        public static TempestEvent? Current => state.Current;

        public static double Now => ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0;

        public static double DayLength => EnvMan.instance != null && EnvMan.instance.m_dayLengthSec > 0 ? EnvMan.instance.m_dayLengthSec : DefaultDayLength;

        public static bool IsAuthority => ZNet.instance != null && ZNet.instance.IsServer();

        /// <summary>The biome under the local player, or None without one.</summary>
        public static WeatherBiome LocalBiome => Player.m_localPlayer != null ? Mask((int)Player.m_localPlayer.GetCurrentBiome()) : WeatherBiome.None;

        /// <summary>The area of the game's current random event (raid), or null. Peers learn it from the server's RPC_SetEvent.</summary>
        public static VanillaEventArea? VanillaArea
        {
            get
            {
                var system = RandEventSystem.instance;
                var ev = system != null ? system.GetCurrentRandomEvent() : null;
                return ev == null ? (VanillaEventArea?)null : new VanillaEventArea(ev.m_pos.x, ev.m_pos.z, ev.m_eventRange);
            }
        }

        /// <summary>Whether a position lies in the current vanilla random event's area (the game's own test).</summary>
        public static bool InVanillaEvent(UnityEngine.Vector3 at) => VanillaEvents.Inside(VanillaArea, at.x, at.y, at.z);

        /// <summary>Whether the local player is in a vanilla event: its area, or any event the game marks active for them.</summary>
        public static bool LocalInVanillaEvent
        {
            get
            {
                var player = Player.m_localPlayer;
                if (player == null) return false;
                var system = RandEventSystem.instance;
                var at = player.transform.position;
                return VanillaEvents.Covers(system != null && system.GetActiveEvent() != null, VanillaArea, at.x, at.y, at.z);
            }
        }

        /// <summary>
        /// Binds the hidden ServerAuthoritative C01 entry CS4 requires as the binding's fallback (never used as a record
        /// source) and registers the guarded world key with the record's canonical form as its validator.
        /// </summary>
        public static void Initialize(MeadworksLog logger, ConfigFile config)
        {
            log = logger;
            fallback = MeadworksConfig.BindHidden(config, "World", "Event Record", "",
                "Managed by Tempest: required fallback of the guarded world key " + TempestCore.WorldKey + "; the shared event lives in the world.",
                ConfigScope.ServerAuthoritative);
            binding = MeadworksWorldSettings.Register(Plugin.Guid, TempestCore.WorldKey, fallback, EventRecord.Canonical);
            dryFallback = MeadworksConfig.BindHidden(config, "World", "Lightning Storm Mark", "",
                "Managed by Tempest: required fallback of the guarded world key " + TempestCore.DryWorldKey + "; the lightning storm flag lives in the world.",
                ConfigScope.ServerAuthoritative);
            dryBinding = MeadworksWorldSettings.Register(Plugin.Guid, TempestCore.DryWorldKey, dryFallback, DryMark.Canonical);
            extendedFallback = MeadworksConfig.BindHidden(config, "World", "Extended Event Record", "",
                "Managed by Tempest: required fallback of the guarded world key " + TempestCore.ExtendedWorldKey + "; Dense Fog, Ash Storm and Meteor Shower live in the world.",
                ConfigScope.ServerAuthoritative);
            extendedBinding = MeadworksWorldSettings.Register(Plugin.Guid, TempestCore.ExtendedWorldKey, extendedFallback, ExtendedRecord.Canonical);
        }

        /// <summary>Called from Update.</summary>
        public static void Tick(float deltaTime)
        {
            sinceTick += deltaTime;
            if (sinceTick < TickSeconds) return;
            sinceTick = 0;
            var settings = Plugin.Settings;
            // ZNet.World stays null while a client is still connecting; GetWorldUID would throw every tick.
            if (settings == null || ZNet.instance == null || ZNet.World == null || ZoneSystem.instance == null || !TempestModules.Active(TempestModule.Events))
            {
                Clear();
                return;
            }
            if (ZNet.instance.GetWorldUID() != world)
            {
                Clear();
                world = ZNet.instance.GetWorldUID();
            }
            Read();
            if (IsAuthority)
            {
                var result = Scheduler.Tick(state, settings.Rules(), ZNet.instance.GetWorldUID(), Now, DayLength, Occupied(), Raided());
                Write(result.State);
                if (result.Outcome == SchedulerOutcome.Started) log?.Info("Weather event started: " + result.State.Current);
                else if (result.Outcome == SchedulerOutcome.Finished || result.Outcome == SchedulerOutcome.Stopped) log?.Info("Weather event ended (" + result.Outcome + ").");
            }
            Notices(settings);
        }

        /// <summary>Weather command summon on the authority (design 5); the caller checked the cheat. <paramref name="dry"/> (or kind LightningStorm) forces a Lightning Storm.</summary>
        public static SchedulerOutcome Summon(EventKind kind, bool skipWarning, bool dry = false) => Summon(kind, skipWarning, dry, out _);

        /// <summary>As <see cref="Summon(EventKind, bool, bool)"/>; <paramref name="started"/> is the kind actually started (a storm may roll a Lightning Storm).</summary>
        public static SchedulerOutcome Summon(EventKind kind, bool skipWarning, bool dry, out EventKind started)
        {
            started = kind;
            var settings = Plugin.Settings;
            if (settings == null || !IsAuthority || ZoneSystem.instance == null) return SchedulerOutcome.Refused;
            Read();
            var seed = unchecked((int)DateTime.UtcNow.Ticks ^ (int)(DateTime.UtcNow.Ticks >> 32));
            var result = Scheduler.Summon(state, settings.Rules(), kind, Now, skipWarning, seed, DayLength, dry ? true : (bool?)null);
            started = Scheduler.SummonedKind(result, kind);
            if (result.Outcome == SchedulerOutcome.Started)
            {
                Write(result.State);
                log?.Info("Weather event summoned: " + result.State.Current);
            }
            return result.Outcome;
        }

        /// <summary>Weather command stop on the authority; the current event clears from now.</summary>
        public static SchedulerOutcome Stop(out EventKind kind, out bool dry)
        {
            kind = EventKind.Blizzard;
            dry = false;
            if (Plugin.Settings == null || !IsAuthority || ZoneSystem.instance == null) return SchedulerOutcome.Refused;
            Read();
            if (state.Current != null)
            {
                kind = state.Current.Kind;
                dry = state.Current.Dry;
            }
            var result = Scheduler.Stop(state, Now);
            if (result.Outcome == SchedulerOutcome.Stopped)
            {
                Write(result.State);
                log?.Info("Weather event stopped by command.");
            }
            return result.Outcome;
        }

        public static void Shutdown()
        {
            Clear();
            binding?.Dispose();
            binding = null;
            fallback = null;
            dryBinding?.Dispose();
            dryBinding = null;
            dryFallback = null;
            extendedBinding?.Dispose();
            extendedBinding = null;
            extendedFallback = null;
            CairnWeather.Shutdown();
            log = null;
        }

        static void Clear()
        {
            state = ScheduleState.Empty;
            lastText = null;
            lastMark = null;
            lastExtended = null;
            seenSerial = 0;
            seenPhase = EventPhase.Idle;
            seenAffected = false;
            world = 0;
        }

        static void Read()
        {
            var legacy = ZoneSystem.instance.GetGlobalKey(TempestCore.LegacyWorldKey, out string old) ? old : null;
            var choice = RecordMigration.Choose(binding?.WorldText, legacy);
            if (choice.Text == null)
            {
                // The authority keeps what it wrote until the key echoes back; a client without a record sees no event.
                if (!IsAuthority)
                {
                    state = ScheduleState.Empty;
                    lastText = null;
                    lastMark = null;
                    lastExtended = null;
                }
                return;
            }
            if (choice.Migrate && IsAuthority && binding != null && binding.Set(choice.Text).Status != WorldSettingWriteStatus.Refused)
                log?.Info("Weather event record moved from " + TempestCore.LegacyWorldKey + " to " + TempestCore.WorldKey + " (the old key is left as it was).");
            var mark = DryMark.Canonical(dryBinding?.WorldText);
            var extended = ExtendedRecord.Canonical(extendedBinding?.WorldText);
            if (choice.Text == lastText && mark == lastMark && extended == lastExtended) return;
            // The authority keeps a dry flag or extended event it wrote until the key echoes back.
            if (IsAuthority && choice.Text == lastText
                && (mark != DryMark.Encode(state.Current) || (extended ?? ExtendedRecord.None) != ExtendedRecord.Encode(state.Current))) return;
            lastText = choice.Text;
            lastMark = mark;
            lastExtended = extended;
            state = EventRecord.TryDecode(choice.Text, out var decoded) ? ExtendedRecord.Apply(DryMark.Apply(decoded, mark), extended) : ScheduleState.Empty;
        }

        static void Write(ScheduleState next)
        {
            if (next.Equals(state) && lastText != null) return;
            if (binding == null) return;
            var mark = DryMark.Encode(next.Current);
            if (dryBinding != null && DryMark.Canonical(dryBinding.WorldText) != mark && !(mark == DryMark.None && dryBinding.WorldText == null))
            {
                var marked = dryBinding.Set(mark);
                if (marked.Status != WorldSettingWriteStatus.Applied && marked.Status != WorldSettingWriteStatus.Queued)
                    log?.Warning("Lightning storm mark not written: " + marked.Status + ".");
            }
            var extended = ExtendedRecord.Encode(next.Current);
            if (extendedBinding != null && ExtendedRecord.Canonical(extendedBinding.WorldText) != extended
                && !(extended == ExtendedRecord.None && extendedBinding.WorldText == null))
            {
                var written = extendedBinding.Set(extended);
                if (written.Status != WorldSettingWriteStatus.Applied && written.Status != WorldSettingWriteStatus.Queued)
                    log?.Warning("Extended weather event not written: " + written.Status + ".");
            }
            var text = EventRecord.Encode(next);
            var result = binding.Set(text);
            if (result.Status != WorldSettingWriteStatus.Applied && result.Status != WorldSettingWriteStatus.Queued)
            {
                log?.Warning("Weather event record not written: " + result.Status + ".");
                return;
            }
            state = next;
            lastText = text;
            lastMark = mark;
            lastExtended = extended;
        }

        /// <summary>Biomes with at least one online player (the scheduler's eligibility rule).</summary>
        static WeatherBiome Occupied()
        {
            var occupied = LocalBiome;
            var generator = WorldGenerator.instance;
            if (generator != null)
                foreach (var peer in ZNet.instance.GetPeers())
                    if (peer != null && peer.IsReady()) occupied |= Mask((int)generator.GetBiome(peer.m_refPos));
            return occupied;
        }

        /// <summary>Biomes of online players inside a vanilla random event (the scheduler waits while the picked kind covers one).</summary>
        static WeatherBiome Raided()
        {
            var raided = LocalInVanillaEvent ? LocalBiome : WeatherBiome.None;
            var generator = WorldGenerator.instance;
            var area = VanillaArea;
            if (generator != null && area != null)
                foreach (var peer in ZNet.instance.GetPeers())
                    if (peer != null && peer.IsReady() && VanillaEvents.Inside(area, peer.m_refPos.x, peer.m_refPos.y, peer.m_refPos.z))
                        raided |= Mask((int)generator.GetBiome(peer.m_refPos));
            return raided;
        }

        static WeatherBiome Mask(int biome)
        {
            var b = (WeatherBiome)biome;
            var known = WeatherBiome.None;
            foreach (var flag in BiomeSets.All) known |= flag;
            return b & known;
        }

        /// <summary>Center messages when a phase starts or ends where the local player is (design W8).</summary>
        static void Notices(TempestConfig settings)
        {
            if (!Plugin.AllowsUi || Player.m_localPlayer == null) return;
            var e = state.Current;
            var now = Now;
            var phase = e == null ? EventPhase.Idle : Timeline.At(e, now).Phase;
            var serial = e?.Serial ?? 0;
            // Inside a vanilla random event its own sky and messages win: no Tempest notices there.
            var raid = LocalInVanillaEvent;
            var affected = e != null && phase != EventPhase.Idle && !raid && e.Affects(LocalBiome);
            if (serial == seenSerial && phase == seenPhase)
            {
                seenAffected |= affected;
                return;
            }
            var wasAffected = seenAffected;
            string? endedKind = null;
            if (seenSerial != 0 && seenPhase != EventPhase.Idle && (serial != seenSerial || phase == EventPhase.Idle) && wasAffected) endedKind = LastKind;
            seenSerial = serial;
            seenPhase = phase;
            seenAffected = affected;
            if (e != null) LastKind = TempestTextKeys.Kind(e);
            if (raid || !settings.Get(Config.TempestSettings.Notices.WarningMessage)) return;
            if (endedKind == null && (!affected || e == null || phase == EventPhase.Clearing)) return;
            if (!CenterNotice.Show(true, CairnWeather.DisplaysWeather(log))) return;
            if (endedKind != null && phase == EventPhase.Idle)
            {
                TempestText.Centre(TempestText.Format(TempestTextKeys.EventEnded, TempestText.Text(endedKind)), log!);
                return;
            }
            if (!affected || e == null) return;
            var kind = TempestText.Text(TempestTextKeys.Kind(e));
            if (phase == EventPhase.Warning)
                TempestText.Centre(TempestText.Format(TempestTextKeys.WarningStarted, kind, TempestTextKeys.Countdown(Timeline.At(e, now).SecondsUntilActive)), log!);
            else if (phase == EventPhase.Active)
                TempestText.Centre(TempestText.Format(TempestTextKeys.EventStarted, kind), log!);
        }

        /// <summary>The name key of the last event seen (for its end notice).</summary>
        static string LastKind { get; set; } = TempestTextKeys.Kind(EventKind.Blizzard);
    }
}
