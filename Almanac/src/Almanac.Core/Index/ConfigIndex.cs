// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Almanac.Index
{
    /// <summary>One config or data file in the index. Values are never copied here; the plugin reads them live.</summary>
    public sealed class IndexedFile
    {
        private readonly List<ConfigEntryInfo> entries = new List<ConfigEntryInfo>();
        private readonly HashSet<EntryKey> entryKeys = new HashSet<EntryKey>();
        private readonly List<string> sections = new List<string>();
        private readonly List<EntryKey> orphans = new List<EntryKey>();
        private readonly HashSet<DiscoveryRoute> routes = new HashSet<DiscoveryRoute>();
        private int ownerRank;

        internal IndexedFile(string path) { Path = path; }

        /// <summary>Display path: the plugin instance's path when known, else the first path seen.</summary>
        public string Path { get; private set; }
        public PluginIdentity? Owner { get; private set; }
        public DataFileRegistration? Registration { get; private set; }
        public IReadOnlyCollection<DiscoveryRoute> Routes => routes;
        public IReadOnlyList<ConfigEntryInfo> Entries => entries;
        public IReadOnlyList<string> Sections => sections;
        public IReadOnlyList<EntryKey> OrphanedKeys => orphans;

        public FileKind Kind
        {
            get
            {
                if (routes.Contains(DiscoveryRoute.CoreConfig)) return FileKind.CoreConfig;
                if (IsLive) return FileKind.BoundConfig;
                if (Registration != null) return FileKind.DataFile;
                return FileKind.UnboundConfig;
            }
        }

        /// <summary>Entries edit through their live <c>ConfigEntry</c> (subject to lock state).</summary>
        public bool EntriesEditable => Kind == FileKind.BoundConfig || Kind == FileKind.CoreConfig;

        /// <summary>Only registered Meadworks YAML files are edited as text (Q4).</summary>
        public bool TextEditable => Kind == FileKind.DataFile;

        public bool AllEntriesAdvanced => Kind == FileKind.CoreConfig;
        public bool AllEntriesRequireRestart => Kind == FileKind.CoreConfig;

        public string GroupKey
        {
            get
            {
                switch (Kind)
                {
                    case FileKind.CoreConfig: return ConfigIndex.CoreGroupKey;
                    case FileKind.UnboundConfig: return ConfigIndex.DiskGroupKey;
                    case FileKind.DataFile: return Registration!.Product.Guid;
                    default: return Owner?.Guid ?? "unknown:" + ConfigPaths.FileName(Path);
                }
            }
        }

        public string DisplayName => Kind == FileKind.DataFile ? Registration!.DisplayName : ConfigPaths.FileName(Path);

        internal bool IsLive => routes.Contains(DiscoveryRoute.PluginInstance) || routes.Contains(DiscoveryRoute.ConstructorHook) || routes.Contains(DiscoveryRoute.FieldScan);

        internal void AddRoute(DiscoveryRoute route, string path)
        {
            routes.Add(route);
            if (route == DiscoveryRoute.PluginInstance) Path = path;
        }

        internal void OfferOwner(PluginIdentity? owner, int rank)
        {
            if (owner == null || rank <= ownerRank) return;
            Owner = owner;
            ownerRank = rank;
        }

        internal void SetRegistration(DataFileRegistration registration) => Registration = registration;

        internal bool AddEntries(IEnumerable<ConfigEntryInfo>? incoming)
        {
            var added = false;
            if (incoming == null) return false;
            foreach (var entry in incoming)
            {
                if (entry == null || !entryKeys.Add(entry.Key)) continue;
                entries.Add(entry);
                if (!sections.Contains(entry.Section)) sections.Add(entry.Section);
                added = true;
            }
            return added;
        }

        internal void SetOrphans(IEnumerable<EntryKey> keys)
        {
            orphans.Clear();
            foreach (var key in keys)
                if (!orphans.Contains(key) && !entryKeys.Contains(key)) orphans.Add(key);
        }

        public override string ToString() => Kind + " " + Path;
    }

    /// <summary>A left-pane group: one plugin (with its extra files), an unknown owner, BepInEx core, or files on disk.</summary>
    public sealed class IndexGroup
    {
        internal IndexGroup(string key, GroupKind kind, string displayName, IReadOnlyList<IndexedFile> files)
        {
            Key = key;
            Kind = kind;
            DisplayName = displayName;
            Files = files;
        }

        public string Key { get; }
        public GroupKind Kind { get; }
        public string DisplayName { get; }
        public IReadOnlyList<IndexedFile> Files { get; }
        public override string ToString() => Kind + " " + DisplayName;
    }

    /// <summary>
    /// The discovery index (design section 3): files from plugin instances, the <c>ConfigFile</c> constructor hook,
    /// the plugin field scan, BepInEx core config, the disk scan and the Meadworks YAML registry, deduplicated by
    /// portable path identity. Owner precedence: plugin instance, declared owner metadata, field scan holder, calling assembly.
    /// </summary>
    public sealed class ConfigIndex
    {
        public const string CoreGroupKey = "bepinex:core";
        public const string DiskGroupKey = "disk:unbound";
        public const string DiskGroupName = "Files on disk";

        private const int RankCalling = 1, RankFieldScan = 2, RankDeclared = 3, RankInstance = 4;

        private readonly bool caseInsensitive;
        private readonly Dictionary<string, IndexedFile> byKey = new Dictionary<string, IndexedFile>();
        private readonly List<IndexedFile> order = new List<IndexedFile>();

        /// <param name="caseInsensitivePaths">True on Windows; false on Linux dedicated servers.</param>
        public ConfigIndex(bool caseInsensitivePaths = true) { caseInsensitive = caseInsensitivePaths; }

        /// <summary>Every file; unbound disk files that a live file now covers are merged, not listed twice.</summary>
        public IReadOnlyList<IndexedFile> Files => order;

        public IndexedFile? Find(string path) => byKey.TryGetValue(ConfigPaths.Key(path, caseInsensitive), out var file) ? file : null;

        public void AddPluginConfig(PluginIdentity plugin, string path, IEnumerable<ConfigEntryInfo> entries)
        {
            var file = Get(path);
            file.AddRoute(DiscoveryRoute.PluginInstance, path);
            file.OfferOwner(plugin ?? throw new ArgumentNullException(nameof(plugin)), RankInstance);
            file.AddEntries(entries);
        }

        /// <summary>A file seen by the <c>ConfigFile</c> constructor postfix.</summary>
        /// <param name="declaredOwner">The <c>BepInPlugin ownerMetadata</c> argument, when given.</param>
        /// <param name="callingPlugin">The plugin whose assembly constructed the file, when resolved.</param>
        public void AddHookedFile(string path, PluginIdentity? declaredOwner, PluginIdentity? callingPlugin, IEnumerable<ConfigEntryInfo> entries)
        {
            var file = Get(path);
            file.AddRoute(DiscoveryRoute.ConstructorHook, path);
            file.OfferOwner(declaredOwner, RankDeclared);
            file.OfferOwner(callingPlugin, RankCalling);
            file.AddEntries(entries);
        }

        /// <summary>A file found in a plugin instance's (or static) <c>ConfigFile</c> field at chainloader completion.</summary>
        public void AddFieldScanFile(PluginIdentity holder, string path, IEnumerable<ConfigEntryInfo> entries)
        {
            var file = Get(path);
            file.AddRoute(DiscoveryRoute.FieldScan, path);
            file.OfferOwner(holder, RankFieldScan);
            file.AddEntries(entries);
        }

        public void AddCoreConfig(string path, IEnumerable<ConfigEntryInfo> entries)
        {
            var file = Get(path);
            file.AddRoute(DiscoveryRoute.CoreConfig, path);
            file.AddEntries(entries);
        }

        /// <summary>A file seen on disk under <c>BepInEx/config</c>. Only <c>.cfg</c> files are considered.</summary>
        public void AddDiskFile(string path)
        {
            if (!ConfigPaths.HasExtension(path, ".cfg")) return;
            Get(path).AddRoute(DiscoveryRoute.DiskScan, path);
        }

        /// <summary>Records keys present in the file that no plugin bound (read-only "Unused keys").</summary>
        public void AddOrphanedKeys(string path, IEnumerable<EntryKey> keys)
        {
            var file = Find(path) ?? throw new InvalidOperationException("Unknown file: " + path);
            file.SetOrphans(keys);
        }

        /// <summary>Registers a Meadworks YAML data file. YAML only; another product's path is refused; re-registration is idempotent.</summary>
        public void AddDataFile(DataFileRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            if (!ConfigPaths.HasExtension(registration.Path, ".yml", ".yaml"))
                throw new ArgumentException("Only YAML data files can be registered: " + registration.Path, nameof(registration));
            var existing = Find(registration.Path);
            if (existing?.Registration != null)
            {
                if (existing.Registration.Product.Guid != registration.Product.Guid)
                    throw new InvalidOperationException("Data file already registered by " + existing.Registration.Product.Guid + ": " + registration.Path);
                return;
            }
            var file = existing ?? Get(registration.Path);
            file.AddRoute(DiscoveryRoute.DataFileRegistry, registration.Path);
            file.SetRegistration(registration);
        }

        /// <summary>Merges a re-read entry list (late <c>Bind</c>). Returns true when new entries appeared.</summary>
        public bool UpdateEntries(string path, IEnumerable<ConfigEntryInfo> entries)
        {
            var file = Find(path) ?? throw new InvalidOperationException("Unknown file: " + path);
            return file.AddEntries(entries);
        }

        /// <summary>Groups in display order: plugins by name, unknown owners by file name, BepInEx core, files on disk.</summary>
        public IReadOnlyList<IndexGroup> Groups
        {
            get
            {
                var groups = new List<IndexGroup>();
                foreach (var bucket in order.GroupBy(f => f.GroupKey))
                {
                    var files = bucket.OrderBy(f => f.Routes.Contains(DiscoveryRoute.PluginInstance) ? 0 : 1)
                        .ThenBy(f => f.Kind == FileKind.DataFile ? 1 : 0)
                        .ThenBy(f => ConfigPaths.Normalize(f.Path), StringComparer.Ordinal).ToList();
                    var first = files[0];
                    GroupKind kind;
                    string name;
                    switch (first.Kind)
                    {
                        case FileKind.CoreConfig: kind = GroupKind.Core; name = "BepInEx"; break;
                        case FileKind.UnboundConfig: kind = GroupKind.DiskFiles; name = DiskGroupName; break;
                        case FileKind.DataFile when files.All(f => f.Kind == FileKind.DataFile):
                            kind = GroupKind.Plugin; name = first.Registration!.Product.Name; break;
                        default:
                            var owner = files.Select(f => f.Owner).FirstOrDefault(o => o != null);
                            kind = owner == null ? GroupKind.UnknownOwner : GroupKind.Plugin;
                            name = owner?.Name ?? ConfigPaths.FileName(first.Path);
                            break;
                    }
                    groups.Add(new IndexGroup(bucket.Key, kind, name, files));
                }
                return groups.OrderBy(g => (int)g.Kind).ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            }
        }

        private IndexedFile Get(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is required", nameof(path));
            var key = ConfigPaths.Key(path, caseInsensitive);
            if (byKey.TryGetValue(key, out var file)) return file;
            file = new IndexedFile(path);
            byKey.Add(key, file);
            order.Add(file);
            return file;
        }
    }
}
