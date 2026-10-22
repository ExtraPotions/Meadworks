// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// The synchronized carrier, authority gating and hot reload follow the pattern of Kindle's SalvageYaml (original code by the same author).
// The files read and the way they are named follow TradersExtended 2.0.5 (Unlicense, pin 2d5dd879).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Barter.Lists;
using ExtraPotions.Barter.Runtime.Lists;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Extensions;
using Jotunn.Managers;
using Newtonsoft.Json;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>
    /// The trade lists in force. Where the world's rules come from decides what is read:
    /// single player and the machine that hosts (or the dedicated server) read the list files of their own config folder, hot reload
    /// them, and publish them in a hidden server-authoritative config value; a client of a server that runs Barter uses the files the
    /// server published (its own are ignored) once they arrive for the current session; a client of a server without Barter has no lists.
    /// A file with an error is rejected as a whole set: the previous lists stay in force.
    /// </summary>
    internal static class TradeLists
    {
        internal const string Folder = "Barter";
        private const float PollSeconds = 2f;
        private const int TotalByteLimit = 4 * 1024 * 1024;

        private static ConfigEntry<string> carrier;
        private static MeadworksLog log;
        private static readonly SyncReceipt receipt = new SyncReceipt();
        private static long observedGeneration = -1;
        private static string applied;
        private static string signature;
        private static float nextCheck;
        private static bool initialized;
        private static TradeBook automaticFor;
        private static ObjectDB automaticDb;
        private static TradeBook effective = TradeBook.Empty;

        /// <summary>The lists are loaded and valid for the current session.</summary>
        internal static bool Ready { get; private set; }

        /// <summary>The files as written (relative name to text) that the lists were built from.</summary>
        internal static IReadOnlyDictionary<string, string> Files { get; private set; } = new Dictionary<string, string>();

        private static TradeBook Book { get; set; } = TradeBook.Empty;

        /// <summary>Raised after a new set of lists was accepted.</summary>
        internal static event Action Changed;

        internal static void Initialize(ConfigFile config, MeadworksLog logger)
        {
            log = logger;
            bool save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                carrier = config.BindConfig("Trade Lists", "Managed List Files", "",
                    "Managed by Barter: the server's trade lists for clients. Edit the Barter.*.yml files instead.", synced: true,
                    configAttributes: new ConfigurationManagerAttributes { Browsable = false, ReadOnly = true });
            }
            finally
            {
                config.SaveOnConfigSet = save;
            }
            config.Save();
            SynchronizationManager.OnConfigurationSynchronized += Synchronized;
            initialized = true;
            Reload();
        }

        private static void Synchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (CruciblePlugin.IsReady && args.UpdatedPluginGUIDs != null && args.UpdatedPluginGUIDs.Contains(Plugin.Guid))
            {
                receipt.Observe();
                nextCheck = 0;
            }
        }

        internal static void Tick()
        {
            if (!initialized || !CruciblePlugin.IsReady) return;
            long generation = CruciblePlugin.Session.Generation;
            if (observedGeneration != generation)
            {
                observedGeneration = generation;
                applied = null;
                signature = null;
                Retire();
                nextCheck = 0;
            }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + PollSeconds;
            Reload();
        }

        /// <summary>Reads the lists again if their source changed. Safe to call every poll: nothing happens when nothing changed.</summary>
        internal static void Reload()
        {
            if (!initialized || !CruciblePlugin.IsReady) return;
            try
            {
                bool remote = CruciblePlugin.Session.Role == SessionRole.RemoteClient;
                var decision = MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative);
                if (remote && decision.Source == ConfigValueSource.Server)
                {
                    if (receipt.CurrentGeneration != CruciblePlugin.Session.Generation)
                    {
                        Retire();
                        return;
                    }
                    Apply(MeadworksConfig.Effective(carrier, Plugin.Guid, ConfigScope.ServerAuthoritative, ""));
                    return;
                }
                if (decision.Source == ConfigValueSource.Locked)
                {
                    Retire();
                    return;
                }

                string currentSignature = Signature();
                if (currentSignature == signature && Ready) return;
                var files = ReadFiles();
                string text = JsonConvert.SerializeObject(files);
                if (!Apply(text)) return;
                signature = currentSignature;
                if (!remote && carrier.Value != text)
                    MeadworksConfig.Amend(carrier, Plugin.Guid, ConfigScope.ServerAuthoritative, text);
            }
            catch (Exception error)
            {
                log?.Warning("Trade list reload refused: " + error);
            }
        }

        /// <summary>The lists plus, when enabled, an automatic sell entry for every valued item; cached per list set and item database.</summary>
        internal static TradeBook Effective
        {
            get
            {
                if (!Ready) return TradeBook.Empty;
                ObjectDB db = ObjectDB.instance;
                if (db == null || db.m_items == null || db.m_items.Count == 0) return Book;
                if (!ReferenceEquals(automaticFor, Book) || !ReferenceEquals(automaticDb, db))
                {
                    var values = new List<(string, int)>();
                    foreach (GameObject prefab in db.m_items)
                        if (prefab != null && prefab.TryGetComponent(out ItemDrop drop))
                            values.Add((prefab.name, drop.m_itemData.m_shared.m_value));
                    var listed = Book.Sell(BarterCore.CommonTrader).Select(entry => entry.Prefab);
                    effective = Book.WithAutomatic(CommonValuables.Build(values, listed, BarterCore.VanillaCurrency));
                    automaticFor = Book;
                    automaticDb = db;
                }
                return effective;
            }
        }

        internal static TradeBook Written => Book;

        internal static IReadOnlyDictionary<string, string> ReadForEdit() => ReadFiles();

        internal static void Shutdown()
        {
            SynchronizationManager.OnConfigurationSynchronized -= Synchronized;
            initialized = false;
            Retire();
            applied = null;
            signature = null;
            carrier = null;
            observedGeneration = -1;
            Changed = null;
            log = null;
        }

        private static void Retire()
        {
            bool had = Ready || Book != TradeBook.Empty;
            Ready = false;
            Book = TradeBook.Empty;
            effective = TradeBook.Empty;
            automaticFor = null;
            Files = new Dictionary<string, string>();
            applied = null;
            if (had) Changed?.Invoke();
        }

        private static bool Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) { Retire(); return false; }
            if (text == applied && Ready) return true;

            var files = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
            if (files == null) return false;

            long total = 0;
            var contributions = new List<ListContribution>();
            var errors = new List<string>();
            foreach (var file in files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                total += file.Value?.Length ?? 0;
                if (!IsSafeName(file.Key) || !ListFileName.TryParse(file.Key, out ListFileName name, AllowsBareName(file.Key)))
                {
                    log?.Warning("Ignored list file with an unusable name: " + file.Key);
                    continue;
                }
                ListReadResult result = ListRows.Read(name.Format, file.Value, file.Key);
                foreach (string warning in result.Warnings) log?.Warning(warning);
                if (!result.Ok)
                {
                    errors.AddRange(result.Errors.Select(error => file.Key + ": " + error));
                    continue;
                }
                contributions.Add(new ListContribution(name, result.Entries, file.Key));
            }

            if (total > TotalByteLimit)
                errors.Add("The list files together are larger than 4 MiB.");
            if (errors.Count > 0)
            {
                foreach (string error in errors)
                    log?.Warning(BarterText.Format("lists_rejected", error));
                return false;
            }

            Book = TradeBook.Build(contributions);
            effective = TradeBook.Empty;
            automaticFor = null;
            Files = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(files);
            applied = text;
            Ready = true;
            log?.Info(BarterText.Format("lists_loaded", contributions.Count, contributions.Sum(c => c.Entries.Count)));
            Changed?.Invoke();
            return true;
        }

        private static bool AllowsBareName(string relative) =>
            relative.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase);

        private static bool IsSafeName(string relative) =>
            !string.IsNullOrWhiteSpace(relative) && !relative.Contains("..") && !Path.IsPathRooted(relative) && relative.IndexOf('\\') < 0;

        /// <summary>The list files on disk as relative name (forward slashes) to path.</summary>
        private static List<KeyValuePair<string, string>> Find()
        {
            var found = new List<KeyValuePair<string, string>>();
            string root = Paths.ConfigPath;
            foreach (string path in Directory.GetFiles(root, BarterCore.FilePrefix + "*"))
            {
                string name = Path.GetFileName(path);
                if (ListFileName.TryParse(name, out _))
                    found.Add(new KeyValuePair<string, string>(name, path));
            }
            string folder = Path.Combine(root, Folder);
            if (Directory.Exists(folder))
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string relative = Folder + "/" + path.Substring(folder.Length).TrimStart('\\', '/').Replace('\\', '/');
                    if (ListFileName.TryParse(relative, out _, allowUnprefixed: true))
                        found.Add(new KeyValuePair<string, string>(relative, path));
                }
            return found;
        }

        private static string Signature()
        {
            var parts = new List<string>();
            foreach (var file in Find().OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file.Value);
                parts.Add(file.Key + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks);
            }
            return string.Join("\n", parts);
        }

        private static Dictionary<string, string> ReadFiles()
        {
            var found = Find();
            if (found.Count == 0 && CruciblePlugin.Session.Role != SessionRole.RemoteClient)
            {
                WriteDefaults();
                found = Find();
            }

            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in found)
            {
                using (var stream = new FileStream(file.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true))
                    files.Add(file.Key, reader.ReadToEnd());
            }
            return files;
        }

        /// <summary>
        /// The shipped lists (the explained example and the default lists of Haldor, Hildir and the Bog Witch), written to the config
        /// folder when no list file exists yet. A file that appeared in the meantime is never overwritten.
        /// </summary>
        private static void WriteDefaults()
        {
            const string prefix = "Barter.config.";
            var assembly = typeof(TradeLists).Assembly;
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(prefix, StringComparison.Ordinal) || !resource.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                    continue;
                string target = Path.Combine(Paths.ConfigPath, resource.Substring(prefix.Length));
                if (File.Exists(target))
                    continue;
                using (var stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) continue;
                    using (var reader = new StreamReader(stream))
                    using (var output = new StreamWriter(new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
                        output.Write(reader.ReadToEnd());
                }
            }
        }
    }
}
