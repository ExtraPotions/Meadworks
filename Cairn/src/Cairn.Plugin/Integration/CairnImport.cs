// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: existing CA10a plans on the C09 converter registry and atomic config target.
// Source configuration and marker files are read only; no TimeWeatherHud implementation is used.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Conversion;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ModuleStepAside = ExtraPotions.Cairn.Integration.ModuleStepAside;

namespace ExtraPotions.Cairn.Runtime
{
    public sealed class CairnImport : IDisposable
    {
        const string MarkerSource = "TimeWeatherHudMarkers";
        readonly ConfigFile config;
        readonly CairnConfig settings;
        readonly MeadworksLog log;
        readonly List<IDisposable> registrations = new List<IDisposable>();
        bool disposed;
        float nextMarkers;

        sealed class Snapshot
        {
            internal string Path = "", Id = "", Text = "", Hash = "";
        }

        sealed class Preview
        {
            internal CairnImport Owner = null!;
            internal Snapshot[] Sources = Array.Empty<Snapshot>();
            internal bool ServerFile;
            internal ImportPlan? Plan;
            internal MarkerImport? Markers;
            public override string ToString() => Plan != null ? Plan.Report() :
                "Source: TimeWeatherHud markers\n" + string.Join("\n", Markers!.Sets.Select(s =>
                    s.World + "/" + s.Character + ": " + s.Choices.Entries.Count + " choices (applied on matching character/world load)"))
                + "\n" + string.Join("\n", Markers.Notes.Select(n => n.ToString()));
        }

        public CairnImport(ConfigFile config, CairnConfig settings, MeadworksLog log)
        {
            this.config = config; this.settings = settings; this.log = log;
            foreach (ImportSource source in Enum.GetValues(typeof(ImportSource)))
            {
                var s = source;
                var from = Source(s);
                registrations.Add(MeadworksConversion.Register(new ConversionRegistration("cairn." + s.ToString().ToLowerInvariant(),
                    Plugin.Guid, from.Guid, from.Name, () => Available(s.ToString(), PathFor(s)), () => Prepare(s),
                    (document, resolutions, backup) => Apply(document, backup))));
            }
            registrations.Add(MeadworksConversion.Register(new ConversionRegistration("cairn.timeweatherhudmarkers", Plugin.Guid,
                ModuleStepAside.TimeWeatherHud.Guid, "TimeWeatherHud markers", () => Available(MarkerSource, MarkersPath),
                PrepareMarkers, (document, resolutions, backup) => Apply(document, backup))));
        }

        static ModuleSource Source(ImportSource source) => ModuleStepAside.Sources[(int)source];
        static string PathFor(ImportSource source) => Path.Combine(Paths.ConfigPath, SourceImport.FileName(source));
        static string MarkersPath => Path.Combine(Paths.ConfigPath, SourceImport.MarkersFileName);
        static bool ServerFile() => CruciblePlugin.Session.Role == SessionRole.Host || CruciblePlugin.Session.Role == SessionRole.DedicatedServer;
        string Imported => settings.Get(CairnSettings.Import.ImportedSources);
        bool Available(string id, string path) => !disposed && File.Exists(path) && !Imported.Split(',').Contains(id);

        static Snapshot Read(string id, string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 1024 * 1024) throw new InvalidDataException("Import source exceeds 1 MiB.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            using var sha = SHA256.Create();
            using var reader = new StreamReader(new MemoryStream(bytes));
            return new Snapshot { Id = id, Path = path, Text = reader.ReadToEnd(), Hash = BitConverter.ToString(sha.ComputeHash(bytes)) };
        }

        object Prepare(ImportSource source)
        {
            if (!Available(source.ToString(), PathFor(source))) throw new InvalidOperationException("Source is unavailable or already imported.");
            var sources = new List<ImportSource> { source };
            if (source == ImportSource.NoFogBruhConfig || source == ImportSource.BetterFogConfig)
            {
                var other = source == ImportSource.NoFogBruhConfig ? ImportSource.BetterFogConfig : ImportSource.NoFogBruhConfig;
                if (Available(other.ToString(), PathFor(other))) sources.Add(other);
            }
            sources.Sort();
            var snapshots = sources.Select(s => Read(s.ToString(), PathFor(s))).ToArray();
            var server = ServerFile();
            return new Preview { Owner = this, Sources = snapshots, ServerFile = server,
                Plan = SourceImport.Combine(snapshots.Select(s => SourceImport.Plan((ImportSource)Enum.Parse(typeof(ImportSource), s.Id), s.Text, server))) };
        }

        object PrepareMarkers()
        {
            if (!Available(MarkerSource, MarkersPath)) throw new InvalidOperationException("Marker source is unavailable or already imported.");
            var source = Read(MarkerSource, MarkersPath);
            return new Preview { Owner = this, Sources = new[] { source }, ServerFile = ServerFile(), Markers = MarkerImport.Parse(source.Text) };
        }

        object Apply(object document, bool backup)
        {
            if (!(document is Preview preview) || !ReferenceEquals(preview.Owner, this) || disposed)
                throw new ArgumentException("Wrong or expired Cairn preview.");
            if (!backup) throw new InvalidOperationException("Cairn import requires a backup.");
            if (CruciblePlugin.Session.Role == SessionRole.RemoteClient)
                throw new InvalidOperationException("Disconnect before importing: remote config overlays must not replace this machine's preferences.");
            if (preview.ServerFile != ServerFile()) throw new InvalidOperationException("Session changed; preview again.");
            foreach (var source in preview.Sources)
                if (!Available(source.Id, source.Path) || Read(source.Id, source.Path).Hash != source.Hash)
                    throw new InvalidOperationException("Source changed; preview again.");
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var entries = new List<ConfigEntryBase>();
            if (preview.Plan != null)
                foreach (var value in preview.Plan.Values)
                {
                    var setting = CairnSettings.All.Single(s => s.Id == value.SettingId);
                    var entry = settings.Entry(setting);
                    if (!MeadworksAuthority.Decide(Plugin.Guid, CairnConfig.Map(setting.Scope)).Allowed)
                        throw new InvalidOperationException("Import blocked: " + setting.Key);
                    values.Add(setting.Id, value.Value); entries.Add(entry);
                }
            if (preview.Markers != null)
            {
                values.Add(CairnSettings.Import.PendingMarkers.Id, MarkerImport.Parse(settings.Get(CairnSettings.Import.PendingMarkers)).Merge(preview.Markers).Serialize());
                entries.Add(settings.Entry(CairnSettings.Import.PendingMarkers));
            }
            values.Add(CairnSettings.Import.ImportedSources.Id, string.Join(",", Imported.Split(',').Where(s => s.Length > 0)
                .Concat(preview.Sources.Select(s => s.Id)).Distinct()));
            entries.Add(settings.Entry(CairnSettings.Import.ImportedSources));
            var target = MeadworksConversion.Target(config);
            ((IValidatedConversionTarget)target).Validate(values);
            var result = MeadworksConfig.AmendBatch(Plugin.Guid, entries, () => { target.Backup(); target.WriteAtomic(values); }, "Cairn import");
            if (!result.Applied) throw new InvalidOperationException("Cairn import was refused before writing.");
            nextMarkers = 0;
            var text = "Cairn imported " + string.Join(" + ", preview.Sources.Select(s => s.Id)) + "; backup retained. Source files unchanged.";
            log.Info(text); return text;
        }

        /// <summary>Accepted choices follow the existing character/world store; other characters remain queued.</summary>
        public void Tick()
        {
            if (disposed || UnityEngine.Time.unscaledTime < nextMarkers) return;
            nextMarkers = UnityEngine.Time.unscaledTime + 2;
            var pending = settings.Get(CairnSettings.Import.PendingMarkers);
            var player = Player.m_localPlayer;
            var net = ZNet.instance;
            var world = net != null ? net.GetWorld() : null;
            if (pending.Length == 0 || player == null || net == null || world == null) return;
            var import = MarkerImport.Parse(pending);
            var name = player.GetPlayerName();
            var set = import.For(world.m_name, name);
            if (set == null) return;
            var key = MarkerChoices.DataKey(net.GetWorldUID());
            var had = player.m_customData.TryGetValue(key, out var previous);
            try
            {
                var directory = Path.Combine(Paths.ConfigPath, "Meadworks", "cairn");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "markers-import-" + System.Guid.NewGuid().ToString("N") + ".bak");
                using (var file = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)))
                    file.Write(key + "\n" + (had ? previous : "<absent>"));
                CairnMarkers.Save(set.Choices);
                var result = MeadworksConfig.Amend((ConfigEntry<string>)settings.Entry(CairnSettings.Import.PendingMarkers), Plugin.Guid,
                    ConfigScope.Local, import.Without(world.m_name, name));
                if (!result.Applied) throw new InvalidOperationException("Marker queue update refused.");
                log.Info("Imported Cairn marker choices for the current character/world; previous choices backed up at " + path + ".");
            }
            catch (Exception error)
            {
                if (had) player.m_customData[key] = previous!; else player.m_customData.Remove(key);
                var save = config.SaveOnConfigSet;
                config.SaveOnConfigSet = false;
                try { settings.Entry(CairnSettings.Import.PendingMarkers).BoxedValue = pending; }
                finally { config.SaveOnConfigSet = save; }
                nextMarkers = UnityEngine.Time.unscaledTime + 30;
                log.Warning("Marker import retained for retry: " + error.GetType().Name + ": " + error.Message);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var registration in registrations) registration.Dispose();
            registrations.Clear();
        }
    }
}
