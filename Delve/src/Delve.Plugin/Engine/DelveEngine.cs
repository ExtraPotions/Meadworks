// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the regeneration engine runtime (DV03b, design B1 to B12): one check every General / Check Interval
// Seconds on the authority, sweeps at dawn for day clocks and at every check for real hours, a shared action budget per
// check (Max Actions Per Tick), dry runs, regenerate-now requests, the sidecar and the status model.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Delve.Commands;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Integration;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    /// <summary>One regeneration service (chests, dungeon resources, world objects, spawners, full resets).</summary>
    public interface IDelveService
    {
        IReadOnlyList<DelveKind> Kinds { get; }

        /// <summary>The world is loaded on the authority: index prefabs and what is already there.</summary>
        void Start(DelveEngine engine);

        /// <summary>Every check: visits, notices and pending writes; <paramref name="sweeping"/> holds the kinds that sweep now.</summary>
        void Check(DelveEngine engine, ISet<DelveKind> sweeping);

        /// <summary>The world is going away or Delve stops: write what is pending, forget the rest.</summary>
        void Stop(DelveEngine engine);
    }

    public sealed partial class DelveEngine
    {
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly List<IDelveService> services = new List<IDelveService>();
        private readonly HashSet<DelveKind> carrying = new HashSet<DelveKind>();
        private readonly HashSet<DelveKind> evaluateNow = new HashSet<DelveKind>();
        private readonly ClaimClearWatch claimClear = new ClaimClearWatch();
        private bool claimClearReady;
        private object? world;
        private float nextCheck;
        private double lastSeconds = double.NaN;
        private string? sidecarPath;
        private bool sidecarDirty;

        public DelveEngine(MeadworksLog log)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
            Status = new EngineStatus(20);
        }

        public MeadworksLog Log { get; }
        public EngineStatus Status { get; }
        public Sidecar Sidecar { get; private set; } = new Sidecar();
        public bool Running { get; private set; }

        /// <summary>Both clocks at the current check.</summary>
        public Stamp Now { get; private set; }

        public double DayLengthSeconds { get; private set; } = 1800;

        /// <summary>Players at the current check.</summary>
        public IReadOnlyList<DelvePlayer> Players { get; private set; } = Array.Empty<DelvePlayer>();

        /// <summary>Site actions left in this check (General / Max Actions Per Tick, design B12).</summary>
        public int Budget { get; set; }

        public DelveConfig Settings => Plugin.Settings ?? throw new InvalidOperationException("Delve settings are not bound.");

        public void Add(IDelveService service) => services.Add(service ?? throw new ArgumentNullException(nameof(service)));

        public T? Service<T>() where T : class, IDelveService => services.OfType<T>().FirstOrDefault();

        /// <summary>A kind may act now (module active, not stepped aside, its own switch on).</summary>
        public bool Active(DelveKind kind) => DelveModules.Active(kind);

        public bool DryRun(DelveKind kind) => Settings.Get(DelveSettings.General.DryRunAll) || Settings.Get(DelveSettings.Kind(kind).DryRun);

        public (ClockKind Clock, double Delay) ClockOf(DelveKind kind) => (Settings.Get(DelveSettings.Kind(kind).Clock), Settings.Delay(kind));

        public bool RegenerateNow(DelveKind kind) => Sidecar.RegenerateNow.Contains(kind);

        /// <summary>Called by a service after its sweep of a kind: the one-shot flag is spent; deferred work carries to the next check.</summary>
        public void Swept(DelveKind kind, int deferred)
        {
            if (Sidecar.RegenerateNow.Remove(kind)) sidecarDirty = true;
            if (deferred > 0) carrying.Add(kind); else carrying.Remove(kind);
            if (claimClear.NoteSwept(kind, deferred)) claimClearReady = true;
        }

        /// <summary>
        /// A full regenerate was asked for. Arms a Bestiary claim clear only when <paramref name="asked"/> names every
        /// kind, <paramref name="skipDelay"/> is the Regenerate cheat, and <paramref name="accepted"/> will run. The
        /// clear waits until those regenerates finish. A usual-delay request leaves an armed job alone.
        /// </summary>
        public void ConsiderClaimClear(IEnumerable<DelveKind> asked, IReadOnlyCollection<DelveKind> accepted, bool skipDelay)
        {
            bool dryRun = Plugin.Settings != null && Plugin.Settings.Get(DelveSettings.General.DryRunAll);
            claimClear.Request(BestiaryClaimReset.CoversEveryKind(asked), dryRun, skipDelay, accepted);
        }

        /// <summary>A scheduled sweep did not finish this kind (generation failed). The claim clear stays armed.</summary>
        public void NoteClaimFailure(DelveKind kind) => claimClear.NoteFailure(kind);

        public void Report(DelveKind kind, KindReport report) => Status.Set(kind, report);

        public void Record(DelveKind kind, string what, bool dryRun)
        {
            Status.Record(kind, what, dryRun, Now);
            if (dryRun) Log.Info("Delve dry run: would have " + what);
            else Log.Verbose("Delve " + kind + ": " + what);
        }

        public void MarkSidecarDirty() => sidecarDirty = true;

        /// <summary>
        /// Admin request (design B9). With <paramref name="skipDelay"/> (the Regenerate cheat allowed) the kinds come back
        /// at their next sweep whatever their delay; without it they are only evaluated at the next check. The guards
        /// always apply. Returns the kinds that will act.
        /// </summary>
        public IReadOnlyList<DelveKind> Request(IEnumerable<DelveKind> kinds, bool skipDelay)
        {
            var acted = new List<DelveKind>();
            foreach (var kind in kinds)
            {
                if (!services.Any(s => s.Kinds.Contains(kind))) continue;
                if (skipDelay)
                {
                    if (Sidecar.RegenerateNow.Add(kind)) sidecarDirty = true;
                }
                else evaluateNow.Add(kind);
                acted.Add(kind);
            }
            nextCheck = 0f;
            return acted;
        }

        private void FinishClaimClear()
        {
            if (!claimClearReady) return;
            claimClearReady = false;
            BestiaryClaims.ClearFinished();
        }

        /// <summary>From Plugin.Update: starts with the world on the authority, checks on the interval, stops with the world.</summary>
        public void Update()
        {
            var current = CurrentWorld();
            if (current == null || !DelveModules.ProductActive(out _))
            {
                if (Running) Stop();
                return;
            }
            if (!Running || !ReferenceEquals(current, world))
            {
                if (Running) Stop();
                Start(current);
            }
            // A shortened interval applies at once rather than after the previously scheduled check.
            nextCheck = Mathf.Min(nextCheck, Time.unscaledTime + Settings.Get(DelveSettings.General.CheckIntervalSeconds));
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + Settings.Get(DelveSettings.General.CheckIntervalSeconds);
            Check();
        }

        public void Stop()
        {
            claimClear.Cancel();
            claimClearReady = false;
            foreach (var service in services)
                try { service.Stop(this); }
                catch (Exception error) { Log.Warning("Delve stop " + service.GetType().Name + ": " + error.GetType().Name + ": " + error.Message); }
            SaveSidecar();
            DelveBases.Clear();
            DelveDungeonIndex.Clear();
            Running = false;
            world = null;
            lastSeconds = double.NaN;
            carrying.Clear();
            evaluateNow.Clear();
        }

        private static object? CurrentWorld()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZNetScene.instance == null || EnvMan.instance == null) return null;
            return ZNet.World != null ? (object)ZNet.World : null;
        }

        private void Start(object current)
        {
            world = current;
            Running = true;
            DayLengthSeconds = Math.Max(1, EnvMan.instance.m_dayLengthSec);
            Now = WorldClock.Now(ZNet.instance.GetTimeSeconds(), DayLengthSeconds, DateTime.UtcNow);
            Status.Resize(Settings.Get(DelveSettings.General.StatusReportLines));
            LoadSidecar();
            DelveBases.Index();
            DelveDungeonIndex.Index();
            DelveDungeonIndex.ListAll();
            foreach (var service in services)
                try { service.Start(this); }
                catch (Exception error) { Log.Error("Delve start " + service.GetType().Name + ": " + error.GetType().Name + ": " + error.Message); }
            Log.Info("Delve regeneration engine started on the authority" + (ZNet.instance.IsDedicated() ? " (dedicated server)." : "."));
        }

        private void Check()
        {
            double seconds = ZNet.instance.GetTimeSeconds();
            DayLengthSeconds = Math.Max(1, EnvMan.instance.m_dayLengthSec);
            Now = WorldClock.Now(seconds, DayLengthSeconds, DateTime.UtcNow);
            bool dawn = !double.IsNaN(lastSeconds) && WorldClock.DawnBetween(lastSeconds, seconds, DayLengthSeconds);
            lastSeconds = seconds;
            Players = DelveWorld.Players();
            Budget = Settings.Get(DelveSettings.General.MaxActionsPerTick);
            Status.Resize(Settings.Get(DelveSettings.General.StatusReportLines));
            claimClear.BeginCheck();
            claimClearReady = false;
            var sweeping = new HashSet<DelveKind>();
            foreach (DelveKind kind in Enum.GetValues(typeof(DelveKind)))
            {
                if (!Active(kind)) continue;
                if (carrying.Contains(kind) || evaluateNow.Contains(kind) || WorldClock.ShouldSweep(ClockOf(kind).Clock, dawn, RegenerateNow(kind)))
                    sweeping.Add(kind);
            }
            evaluateNow.Clear();
            foreach (var service in services)
                try { service.Check(this, sweeping); }
                catch (Exception error)
                {
                    Log.Error("Delve check " + service.GetType().Name + ": " + error.GetType().Name + ": " + error.Message);
                }
            FinishClaimClear();
            foreach (DelveKind kind in Enum.GetValues(typeof(DelveKind)))
                if (!Active(kind)) Status.Set(kind, new KindReport(0, 0, 0, 0, null, false, false, InactiveReason(kind)));
            if (sidecarDirty) SaveSidecar();
        }

        private static string InactiveReason(DelveKind kind)
        {
            var module = ExtraPotions.Delve.Integration.ModuleStepAside.ModuleOf(kind);
            if (DelveModules.StepAside.SteppedAside(module)) return "stepped aside";
            return "switched off";
        }

        public IEnumerable<string> StatusLines()
        {
            bool dryRunAll = Plugin.Settings != null && Settings.Get(DelveSettings.General.DryRunAll);
            return Running ? Status.Lines(Now, dryRunAll, g => DelveText.Text(ExtraPotions.Delve.Localization.DelveTextKeys.Guard(g))) : new[] { "Delve status: the engine is not running (no world on this authority)." };
        }

        private void LoadSidecar()
        {
            sidecarPath = null;
            Sidecar = new Sidecar();
            try
            {
                var w = ZNet.World;
                var source = w.m_fileSource == FileHelpers.FileSource.Cloud ? FileHelpers.FileSource.Local : w.m_fileSource;
                var root = SaveSystem.GetWorldsSaveRootPath(source);
                sidecarPath = Path.Combine(root, w.m_name + ".delve.json");
                var text = File.Exists(sidecarPath) ? File.ReadAllText(sidecarPath) : null;
                Sidecar = Sidecar.Parse(text, out var errors);
                foreach (var e in errors) Log.Warning("Delve " + Path.GetFileName(sidecarPath) + ": " + e + ".");
            }
            catch (Exception error)
            {
                Log.Warning("Delve sidecar unavailable: " + error.GetType().Name + ": " + error.Message + "; world object spots are kept in memory only.");
            }
            sidecarDirty = false;
        }

        public void SaveSidecar()
        {
            if (sidecarPath == null || !sidecarDirty || Sidecar.ReadOnly) return;
            try
            {
                var tmp = sidecarPath + ".tmp";
                File.WriteAllText(tmp, Sidecar.Write());
                if (File.Exists(sidecarPath)) File.Delete(sidecarPath);
                File.Move(tmp, sidecarPath);
                sidecarDirty = false;
            }
            catch (Exception error)
            {
                Log.Warning("Delve sidecar write failed: " + error.GetType().Name + ": " + error.Message);
            }
        }

        /// <summary>The shared guard input for a site: players, held, player-built objects and base areas near it.</summary>
        public GuardInput Guard(Vector3 center, float playerRadius, float clearance, bool held = false, Box? interior = null)
        {
            return new GuardInput(DelveWorld.ToVec(center), playerRadius)
            {
                Interior = interior,
                Players = Players.Select(p => DelveWorld.ToVec(p.Position)).ToList(),
                HeldByPlayer = held,
                Clearance = clearance,
                Built = clearance > 0f ? DelveWorld.PlayerBuiltNear(center, clearance) : (IReadOnlyList<Vec3>)Array.Empty<Vec3>(),
                BaseGuard = Settings.Get(DelveSettings.General.PlayerBaseGuard),
                Bases = Settings.Get(DelveSettings.General.PlayerBaseGuard) ? DelveBases.Near(center) : new List<Area>(),
            };
        }

        public SchedulerOptions SchedulerOptions() =>
            new SchedulerOptions(Math.Max(1, Budget), Settings.Get(DelveSettings.General.DryRunAll), k => ClockOf(k), k => Settings.Get(DelveSettings.Kind(k).DryRun));
    }
}
