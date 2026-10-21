// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd: BetterSleepBruh/Patches/EnvMan.cs (CalculateCanSleepPatch),
// Patches/ZNet.cs (UpdateNetTime postfix, SyncNetTimeToPeers, boost total, SaveWorldPatch), Patches/Game.cs
// (UpdateSleepingPatch with its null-instance guard), Patches/PlayerPatches.cs (AttachStart and AttachStop hints)
// and Components/SleepTracker.cs (GetSleepOccupancyCounts, GetNextMorningCapSeconds, recount, heartbeat, SaveData,
// LoadData).
// The MIT notice is retained in THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: math moved to Ember.Core (SleepWindow, SleepRate, SleepBoost, SleepSkip, SleepRecount);
// routed RPCs replaced by Crucible operations to the authority (bed changed hint, state poll); no MonoBehaviour,
// ticks from the plugin; module gate (peer class, step-aside, Sleep.Enabled, Use Vanilla Sleep Window); cheats
// through the authority gate; wake at window end reimplemented from behavior (the HUD view is not adopted);
// at or past the morning cap the time is left unchanged; the boost total travels in the state reply (no extra
// routed RPC, so no duplicate registration) and is known on a client only after its first reply; the total is saved
// per world id; quiet logging; namespace Ember.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Ember.Sleep;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>Request marker of the two sleep operations; the payload is the version byte only.</summary>
    public sealed class SleepPing
    {
        public static readonly SleepPing Instance = new SleepPing();
    }

    /// <summary>Wire codec of meadworks.ember.sleep.bed_changed and meadworks.ember.sleep.state (Ember.Core SleepCommands).</summary>
    public sealed class SleepCodec : IOperationCodec<SleepPing, SleepState>
    {
        readonly bool bedChanged;

        public SleepCodec(bool bedChanged) { this.bedChanged = bedChanged; }

        public byte[] EncodeRequest(SleepPing request) => bedChanged ? SleepCommands.EncodeBedChanged() : SleepCommands.EncodeStateRequest();

        public SleepPing DecodeRequest(byte[] bytes)
        {
            string? error;
            var ok = bedChanged ? SleepCommands.TryDecodeBedChanged(bytes, out error) : SleepCommands.TryDecodeStateRequest(bytes, out error);
            if (!ok) throw new FormatException(error);
            return SleepPing.Instance;
        }

        public byte[] EncodeReply(SleepState reply) => SleepCommands.EncodeState(reply);

        public SleepState DecodeReply(byte[] bytes) =>
            SleepCommands.TryDecodeState(bytes, out var state, out var error) ? state : throw new FormatException(error);
    }

    /// <summary>
    /// E04 sleep runtime. The authority (single player, host, dedicated server) counts beds from ZDOs, adds the partial
    /// boost to the network time, runs the all-in-bed skip and answers the state poll. Every graphical client polls the
    /// state once per second inside the window and once when joining, and wakes its own player when the window ends
    /// without a full skip. The reply carries the world's boost total for the station timers. Main thread only.
    /// </summary>
    public static class SleepRuntime
    {
        // Crucible admits a remote request at its own peer protocol version (1.0): an operation registered under another
        // version is unavailable to every client of a server, and the state poll and bed hint were answered with
        // OperationUnavailable. The authority itself submits locally, which is why single player and the host never saw it.
        static readonly System.Version Protocol = new System.Version(1, 0);
        static readonly RecountThrottle throttle = new RecountThrottle();
        static readonly Heartbeat heartbeat = new Heartbeat();
        static readonly WindowEdges edges = new WindowEdges();
        static readonly PublishGate published = new PublishGate();
        static readonly TimeSyncTimer timeSync = new TimeSyncTimer();
        static readonly List<IDisposable> operations = new List<IDisposable>();

        static MeadworksLog? log;
        static double tickTimer;
        static double pollTimer;
        static Task<OperationResult<SleepState>>? poll;
        static Task<OperationResult<SleepState>>? hint;
        static long polledSession = -1;
        static bool hintWanted;
        static ClientOwner? clientOwner;
        static ZNet? authorityNetwork;
        static EnvMan? authorityEnvironment;
        static long authorityGeneration = -1, authorityWorld;
        static string? authoritySession;
        static double boostTotal;
        static bool boostDirty;

        static void ResetAuthority()
        {
            SaveBoostTotal();
            boostTotal = 0.0;
            Current = default;
            tickTimer = 0;
            throttle.Reset();
            heartbeat.Reset();
            edges.Reset();
            published.Reset();
            timeSync.Reset();
            authorityNetwork = null;
            authorityEnvironment = null;
            authorityGeneration = -1;
            authorityWorld = 0;
            authoritySession = null;
        }

        static bool PrepareAuthority()
        {
            var network = ZNet.instance;
            var environment = EnvMan.instance;
            var session = CruciblePlugin.Session;
            if (!Plugin.Ready || Plugin.Settings == null || network == null || environment == null ||
                !network.IsServer() || !session.IsAuthority || !session.AuthorityReady || !Active ||
                session.WorldId == 0 || ZNet.m_world == null || network.GetWorldUID() != session.WorldId ||
                string.IsNullOrEmpty(session.SessionId))
            {
                ResetAuthority();
                return false;
            }
            if (ReferenceEquals(authorityNetwork, network) && ReferenceEquals(authorityEnvironment, environment) &&
                authorityGeneration == session.Generation && authorityWorld == session.WorldId && authoritySession == session.SessionId)
                return true;
            ResetAuthority();
            authorityNetwork = network;
            authorityEnvironment = environment;
            authorityGeneration = session.Generation;
            authorityWorld = session.WorldId;
            authoritySession = session.SessionId;
            boostTotal = LoadBoostTotal(session.WorldId);
            try { Recount(force: true); }
            catch { ResetAuthority(); throw; }
            return true;
        }

        sealed class ClientOwner : IDisposable
        {
            readonly ZNet network;
            readonly Player actor;
            readonly long generation, world;
            readonly string session;
            internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

            internal ClientOwner(ZNet network, Player actor)
            {
                this.network = network;
                this.actor = actor;
                var current = CruciblePlugin.Session;
                generation = current.Generation;
                world = current.WorldId;
                session = current.SessionId;
            }

            internal bool IsCurrent()
            {
                var current = CruciblePlugin.Session;
                return !Cancellation.IsCancellationRequested && network != null && actor != null &&
                    ReferenceEquals(network, ZNet.instance) && ZNet.m_world != null && network.GetWorldUID() == world &&
                    ReferenceEquals(actor, Player.m_localPlayer) &&
                    actor.m_nview != null && actor.m_nview.IsValid() && actor.m_nview.IsOwner() &&
                    current.AuthorityReady && current.Generation == generation && current.WorldId == world &&
                    current.SessionId == session;
            }

            public void Dispose()
            {
                Cancellation.Cancel();
                Cancellation.Dispose();
            }
        }

        static void ResetClient()
        {
            clientOwner?.Dispose();
            clientOwner = null;
            poll = null;
            hint = null;
            hintWanted = false;
            pollTimer = 0;
            polledSession = -1;
            Received = null;
        }

        static bool PrepareClient()
        {
            var actor = Player.m_localPlayer;
            var network = ZNet.instance;
            var session = CruciblePlugin.Session;
            if (!MeadworksHeadless.AllowsUi || !Active || actor == null || network == null ||
                actor.m_nview == null || !actor.m_nview.IsValid() || !actor.m_nview.IsOwner() ||
                !session.AuthorityReady || session.WorldId == 0 || ZNet.m_world == null ||
                network.GetWorldUID() != session.WorldId || string.IsNullOrEmpty(session.SessionId))
            {
                ResetClient();
                return false;
            }
            if (clientOwner != null && clientOwner.IsCurrent()) return true;
            ResetClient();
            clientOwner = new ClientOwner(network, actor);
            return true;
        }

        /// <summary>The authority's current state (counts, rate, window, skip). Zero outside the authority.</summary>
        public static SleepState Current { get; private set; }

        /// <summary>The last state this client received from the authority; null before the first reply.</summary>
        public static SleepState? Received { get; private set; }

        public static void Initialize(ProductRegistration registration, MeadworksLog logger)
        {
            log = logger;
            var services = CruciblePlugin.Services;
            operations.Add(services.Operations.Register(registration, new OperationDescriptor(SleepCommands.BedChangedId, Protocol), new SleepCodec(true), OnBedChanged));
            operations.Add(services.Operations.Register(registration, new OperationDescriptor(SleepCommands.StateId, Protocol), new SleepCodec(false), OnStateRequest));
        }

        public static void Shutdown()
        {
            foreach (var op in operations) op.Dispose();
            operations.Clear();
            ResetClient();
            ResetAuthority();
        }

        static bool Active => EmberModules.Active(EmberModule.Sleep);

        static bool IsServer => ZNet.instance != null && ZNet.instance.IsServer();

        /// <summary>S1, Sleep Any Time: Ember's window at the current day fraction.</summary>
        public static bool WindowOpen(EnvMan env)
        {
            var s = Plugin.Settings;
            if (env == null || s == null) return false;
            return SleepWindow.IsOpen(env.GetDayFraction(), s.Get(EmberSettings.Sleep.SleepStart), env.IsTimeSkipping(), s.SleepAnyTime);
        }

        static BoostSettings Boost()
        {
            var s = Plugin.Settings!;
            return new BoostSettings(s.Get(EmberSettings.Sleep.PartialSleepBoost), s.Get(EmberSettings.Sleep.BonusMultiplier), s.Get(EmberSettings.Sleep.BonusIncrementScale));
        }

        // Authority.

        /// <summary>S8: players = max(character ZDOs, connected players) plus testing offsets; sleepers = ZDOs flagged in bed.</summary>
        static SleepCounts CountBeds()
        {
            var znet = ZNet.instance;
            var zdos = znet.GetAllCharacterZDOS();
            var inBed = 0;
            if (zdos != null)
                foreach (var zdo in zdos)
                    if (zdo != null && zdo.IsValid() && zdo.GetBool(ZDOVars.s_inBed)) inBed++;
            var counts = SleepRate.Count(zdos?.Count ?? 0, znet.GetNrOfPlayers(), inBed, Plugin.Settings!.Testing);
            return new SleepCounts(Math.Min(counts.Players, SleepCommands.MaxPlayers), Math.Min(counts.Sleeping, Math.Min(counts.Players, SleepCommands.MaxPlayers)));
        }

        static void Recount(bool force)
        {
            if (!IsServer || EnvMan.instance == null || !Active)
            {
                Current = default;
                return;
            }
            var open = WindowOpen(EnvMan.instance);
            var counts = CountBeds();
            var rate = Math.Min(SleepCommands.MaxExtraRate, SleepRate.ExtraRate(counts, Boost(), open));
            var skipping = Game.instance != null && Game.instance.m_sleeping;
            Current = new SleepState(open, counts.Players, counts.Sleeping, rate, rate > 0.0, skipping);
            if (published.ShouldPublish(Current, force))
                log?.Verbose("Sleep state " + Current);
        }

        /// <summary>The 1 s server tick (S9): recount, heartbeat publish every 3 s inside the window, window edges.</summary>
        static void TickAuthority(double dt)
        {
            if (!PrepareAuthority()) return;
            tickTimer += dt;
            if (tickTimer < Heartbeat.TickSeconds) return;
            tickTimer = 0.0;
            if (!Active || EnvMan.instance == null)
            {
                Current = default;
                edges.Update(false, false);
                return;
            }
            var open = WindowOpen(EnvMan.instance);
            Recount(heartbeat.Tick(open));
            switch (edges.Update(open, EnvMan.instance.IsTimeSkipping()))
            {
                case WindowEdge.Opened: Recount(force: true); break;
                case WindowEdge.ClosedWake:
                case WindowEdge.ClosedDuringSkip: published.Reset(); break;
            }
        }

        static OperationExecution<SleepState> OnBedChanged(AuthorityContext context, SleepPing _)
        {
            if (!PrepareAuthority()) return OperationExecution<SleepState>.Rejected("SleepOff");
            if (throttle.Admit(context.IsLocal ? "local" : context.SenderId, verified: true, UnityEngine.Time.realtimeSinceStartup)) Recount(force: false);
            return OperationExecution<SleepState>.Success(Reply());
        }

        static OperationExecution<SleepState> OnStateRequest(AuthorityContext context, SleepPing _) =>
            PrepareAuthority() ? OperationExecution<SleepState>.Success(Reply()) : OperationExecution<SleepState>.Rejected("SleepOff");

        static SleepState Reply() => Current.WithBoostTotal(StationTimers.AddBoost(boostTotal, 0.0));

        // Boost total (station timers).

        static string? BoostFile(long world) =>
            world == 0 ? null : Path.Combine(Paths.ConfigPath, "Meadworks", "ember", StationTimers.FileName(world));

        static double LoadBoostTotal(long world)
        {
            boostDirty = false;
            var path = BoostFile(world);
            try
            {
                if (path != null && File.Exists(path) && StationTimers.TryParseTotal(File.ReadAllText(path), out var total)) return total;
            }
            catch (Exception error) { log?.Warning("Could not read the sleep boost total: " + error.GetType().Name + ": " + error.Message); }
            return 0.0;
        }

        /// <summary>Writes the authority's boost total when it changed; also the postfix of ZNet.SaveWorld.</summary>
        internal static void SaveBoostTotal()
        {
            if (!boostDirty || authorityNetwork == null) return;
            boostDirty = false;
            var path = BoostFile(authorityWorld);
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, StationTimers.FormatTotal(boostTotal));
            }
            catch (Exception error) { log?.Warning("Could not save the sleep boost total: " + error.GetType().Name + ": " + error.Message); }
        }

        /// <summary>True while this peer protects station timers: Ember's sleep is active and the setting is on.</summary>
        internal static bool StationsOn(EmberSetting<bool> setting) =>
            Plugin.Ready && Plugin.Settings != null && Active && Plugin.Settings.Get(setting);

        /// <summary>The extra rate the ocean safeguards react to: zero unless Ember's sleep is running here.</summary>
        internal static double OceanRate => Plugin.Ready && Plugin.Settings != null && Active ? StationRate : 0.0;

        static bool AuthorityHere => authorityNetwork != null && ReferenceEquals(authorityNetwork, ZNet.instance);

        /// <summary>The extra rate in effect here: the authority's own, else the last reply's; zero when unknown.</summary>
        internal static double StationRate =>
            AuthorityHere ? Current.ExtraRate : clientOwner != null && Received.HasValue ? Received.Value.ExtraRate : 0.0;

        /// <summary>The world's boost total; false until it is known (a client knows it after its first reply).</summary>
        internal static bool TryBoostTotal(out double total)
        {
            if (AuthorityHere)
            {
                total = boostTotal;
                return true;
            }
            if (clientOwner != null && Received.HasValue)
            {
                total = Received.Value.BoostTotal;
                return true;
            }
            total = 0.0;
            return false;
        }

        /// <summary>S6 cap: the start of the next morning (BetterSleepBruh's day lookup with a 0.15 day back-off).</summary>
        static double MorningCap()
        {
            var env = EnvMan.instance;
            if (!IsServer || env == null) return double.PositiveInfinity;
            var time = ZNet.instance.GetTimeSeconds();
            var day = env.GetDay(time - env.m_dayLengthSec * 0.150000005960464);
            return env.GetMorningStartSec(day + 1);
        }

        /// <summary>S4, S6, S7: postfix of ZNet.UpdateNetTime on the server.</summary>
        internal static void AfterNetTime(ZNet znet, float dt)
        {
            if (!ReferenceEquals(znet, ZNet.instance) || !PrepareAuthority() || znet.GetNrOfPlayers() <= 0) return;
            var rate = Current.ExtraRate;
            if (rate <= 0.0)
            {
                timeSync.Reset();
                return;
            }
            var step = SleepBoost.Step(znet.GetTimeSeconds(), dt, rate, MorningCap(), Plugin.Settings!.Get(EmberSettings.Sleep.BoostFadeSeconds));
            if (!step.Advanced)
            {
                timeSync.Reset();
                return;
            }
            znet.SetNetTime(step.NewTime);
            boostTotal = StationTimers.AddBoost(boostTotal, step.Applied);
            boostDirty = true;
            if (znet.m_peers == null || znet.m_peers.Count == 0 || !timeSync.Tick(dt)) return;
            var netTime = znet.GetTimeSeconds();
            foreach (var peer in znet.m_peers)
                if (peer != null && peer.IsReady() && peer.m_rpc != null) peer.m_rpc.Invoke("NetTime", netTime);
        }

        /// <summary>S5: prefix of Game.UpdateSleeping; false skips the game's own update.</summary>
        internal static bool BeforeUpdateSleeping(Game game)
        {
            if (game == null) return false;
            var authorityReady = PrepareAuthority();
            var env = EnvMan.instance;
            var action = SleepSkip.Decide(Active, authorityReady, game.m_sleeping, env != null && env.IsTimeSkipping(),
                env != null && WindowOpen(env), authorityReady && new SleepCounts(Current.Players, Current.Sleeping).AllInBed);
            switch (action)
            {
                case SkipAction.Vanilla:
                    return true;
                case SkipAction.StartSkip:
                    game.m_sleeping = true;
                    ZRoutedRpc.instance?.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStart");
                    try { env!.SkipToMorning(); }
                    catch (Exception error) { log?.Warning("Skip to morning failed: " + error.GetType().Name + ": " + error.Message); }
                    return false;
                case SkipAction.StopSkip:
                    game.m_sleeping = false;
                    ZRoutedRpc.instance?.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStop");
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>S1, S2: prefix of EnvMan.CalculateCanSleep; false replaces the game's result.</summary>
        internal static bool BeforeCanSleep(EnvMan env, ref bool result)
        {
            if (!Active) return true;
            var open = WindowOpen(env);
            var player = Player.m_localPlayer;
            double? wakeup = player != null && ZNet.instance != null ? player.m_wakeupTime : (double?)null;
            var now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
            result = SleepWindow.CanSleep(open, now, wakeup, env.m_sleepCooldownSeconds);
            return false;
        }

        // Clients (the host and single player use the same path through the local authority).

        /// <summary>S9: postfixes of Player.AttachStart(isBed) and Player.AttachStop when the local player's bed state changed.</summary>
        internal static void BedChanged(Player player)
        {
            if (player == null || player != Player.m_localPlayer || !Active) return;
            if (IsServer)
            {
                if (PrepareAuthority()) Recount(force: false);
                return;
            }
            hintWanted = true;
        }

        public static void Tick(double dt)
        {
            if (!Plugin.Ready || Plugin.Settings == null) { ResetClient(); ResetAuthority(); return; }
            TickAuthority(dt);
            if (!PrepareClient()) return;
            SendHint();
            Poll(dt);
        }

        static void SendHint()
        {
            if (hint != null)
            {
                if (!hint.IsCompleted) return;
                var completed = hint;
                hint = null;
                if (completed.Status == TaskStatus.RanToCompletion) Accept(completed.Result);
            }
            if (!hintWanted) return;
            hintWanted = false;
            hint = CruciblePlugin.RequestClient.InvokeAsync<SleepPing, SleepState>(SleepCommands.BedChangedId, SleepPing.Instance, clientOwner!.Cancellation.Token);
        }

        static void Poll(double dt)
        {
            if (poll != null)
            {
                if (!poll.IsCompleted) return;
                var completed = poll;
                poll = null;
                if (completed.Status == TaskStatus.RanToCompletion) Accept(completed.Result);
            }
            var session = CruciblePlugin.Session.Generation;
            var joined = polledSession != session;
            pollTimer += dt;
            var windowLikely = (Received?.WindowOpen ?? false) || (EnvMan.instance != null && WindowOpen(EnvMan.instance));
            if (!joined && (pollTimer < 1.0 || !windowLikely)) return;
            pollTimer = 0.0;
            polledSession = session;
            poll = CruciblePlugin.RequestClient.InvokeAsync<SleepPing, SleepState>(SleepCommands.StateId, SleepPing.Instance, clientOwner!.Cancellation.Token);
        }

        static void Accept(OperationResult<SleepState> result)
        {
            if (result.State != OperationState.Succeeded || clientOwner == null || !clientOwner.IsCurrent() ||
                !Active || Plugin.Settings == null) return;
            var previous = Received;
            Received = result.Reply;
            if (previous.HasValue && previous.Value.WindowOpen && !result.Reply.WindowOpen && !result.Reply.FullSkip && !previous.Value.FullSkip
                && Plugin.Settings!.Get(EmberSettings.Sleep.WakeAtWindowEnd))
                WakeLocalPlayer();
        }

        /// <summary>S10 (behavior): stop sleeping, leave the bed, and start the wake-up cooldown.</summary>
        static void WakeLocalPlayer()
        {
            var player = Player.m_localPlayer;
            if (player == null || !player.InBed()) return;
            player.SetSleeping(false);
            player.AttachStop();
            if (ZNet.instance != null) player.m_wakeupTime = ZNet.instance.GetTimeSeconds();
            log?.Verbose("Woke the local player at the end of the sleep window.");
        }
    }

    [HarmonyPatch]
    internal static class SleepPatches
    {
        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.CalculateCanSleep)), HarmonyPrefix]
        static bool CanSleep(EnvMan __instance, ref bool __result) => SleepRuntime.BeforeCanSleep(__instance, ref __result);

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.UpdateNetTime)), HarmonyPostfix]
        static void NetTime(ZNet __instance, float dt) => SleepRuntime.AfterNetTime(__instance, dt);

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SaveWorld)), HarmonyPostfix]
        static void SaveWorld() => SleepRuntime.SaveBoostTotal();

        [HarmonyPatch(typeof(Game), nameof(Game.UpdateSleeping)), HarmonyPrefix]
        static bool UpdateSleeping(Game __instance) => SleepRuntime.BeforeUpdateSleeping(__instance);

        [HarmonyPatch(typeof(Player), nameof(Player.AttachStart)), HarmonyPostfix]
        static void AttachStart(Player __instance, bool isBed)
        {
            if (isBed) SleepRuntime.BedChanged(__instance);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.AttachStop)), HarmonyPrefix]
        static void BeforeAttachStop(Player __instance, out bool __state) => __state = __instance.InBed();

        [HarmonyPatch(typeof(Player), nameof(Player.AttachStop)), HarmonyPostfix]
        static void AttachStop(Player __instance, bool __state)
        {
            if (__state) SleepRuntime.BedChanged(__instance);
        }
    }
}
