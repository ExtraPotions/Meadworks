// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackConfigLoader.cs stable-file length/time checks, first-run defaults and complete snapshot replacement.
// Changed by expDARE, 2026: Edge file families and layers, C01 hidden carriers, main-thread polling and lifecycle;
// retained normalizer/parser handles all schemas. No ServerSync or upstream config ownership.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Edge.Runtime;

internal sealed class EdgeRuleFiles
{
    private readonly EdgeConfig config;
    private readonly MeadworksLog log;
    private readonly string directory;
    private readonly Dictionary<string, string> embeddedDefaults;
    private string localStamp = "";
    private string remoteStamp = "";
    private SessionRole? previousRole;
    private float nextPoll;
    private int revision;
    internal static SecondaryAttackCompiledSnapshot Current { get; private set; } = SecondaryAttackCompiledSnapshot.Empty;

    internal EdgeRuleFiles(EdgeConfig config, MeadworksLog log)
    {
        this.config = config; this.log = log;
        directory = Path.Combine(Paths.ConfigPath, "Edge");
        RuleDiagnostics.OnInfo = log.Info;
        RuleDiagnostics.OnWarning = log.Warning;
        RuleDiagnostics.OnError = log.Error;
        embeddedDefaults = ReadEmbeddedDefaults();
        if (!Apply(embeddedDefaults, "Embedded Edge defaults rejected."))
            throw new InvalidOperationException("Embedded Edge defaults are invalid");
        Tick(true);
    }

    private Dictionary<string, string> ReadEmbeddedDefaults()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (RuleFamily family in Enum.GetValues(typeof(RuleFamily)))
        {
            var path = Path.Combine(directory, EdgeDataFiles.BaseName(family));
            using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Edge.defaults." + EdgeDataFiles.BaseName(family))
                ?? throw new InvalidOperationException("Missing Edge default " + family);
            using var reader = new StreamReader(stream);
            result[path] = reader.ReadToEnd();
        }
        return result;
    }

    private void MaterializeDefaults()
    {
        try { Directory.CreateDirectory(directory); }
        catch (Exception error)
        {
            log.Warning("Rule directory unavailable; embedded defaults retained: " + directory + ": " + error.Message);
            return;
        }
        foreach (var pair in embeddedDefaults)
        {
            try
            {
                if (!File.Exists(pair.Key)) File.WriteAllText(pair.Key, pair.Value);
            }
            catch (Exception error)
            {
                log.Warning("Rule default could not be written; embedded copy retained: " + pair.Key + ": " + error.Message);
            }
        }
    }

    internal void Tick(bool force = false)
    {
        if (!CruciblePlugin.IsReady) return;
        var role = CruciblePlugin.Session.Role;
        if (previousRole != role) { force = true; localStamp = remoteStamp = ""; previousRole = role; }
        if (!force && Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 1f;
        try
        {
            if (role == SessionRole.RemoteClient) ReadServer();
            else ReadLocal(force);
        }
        catch (Exception error) { log.Warning("Rule files retained: " + error.Message); }
    }

    private void ReadLocal(bool force)
    {
        MaterializeDefaults();
        string[] paths;
        try
        {
            paths = Directory.GetFiles(directory, "*.yml").Where(p => EdgeDataFiles.Classify(p) != null)
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }
        catch (Exception error)
        {
            log.Warning("Rule directory could not be read; embedded defaults retained: " + directory + ": " + error.Message);
            return;
        }
        var stamps = paths.ToDictionary(p => p, p => Stamp(p));
        var files = paths.ToDictionary(p => p, File.ReadAllText);
        foreach (var pair in embeddedDefaults)
        {
            RuleFamily? family = EdgeDataFiles.Classify(pair.Key);
            if (family != null && !paths.Any(path => EdgeDataFiles.Classify(path) == family)) files[pair.Key] = pair.Value;
        }
        var signature = string.Join("\n", files.Keys.OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => p + ":" + (stamps.TryGetValue(p, out var stamp) ? stamp : "embedded")));
        if (!force && signature == localStamp) return;
        if (paths.Any(p => stamps[p] != Stamp(p)) || !paths.SequenceEqual(Directory.GetFiles(directory, "*.yml")
            .Where(p => EdgeDataFiles.Classify(p) != null).OrderBy(p => p, StringComparer.Ordinal))) return;
        localStamp = signature;
        Apply(files, "Edge YAML rejected; previous rules retained.");
    }

    private bool Apply(IReadOnlyDictionary<string, string> files, string rejection)
    {
        if (!RuleDocuments.TryMerge(files, out var merged) || !SecondaryAttackConfigLoader.TryCompileSnapshot(revision + 1,
            new SecondaryAttackYamlTexts(merged), out var candidate))
        {
            log.Warning(rejection);
            return false;
        }
        var carriers = RuleDocuments.Stamp(merged);
        foreach (var pair in carriers)
        {
            var entry = (ConfigEntry<string>)config.Entry(EdgeDataFiles.Carrier((RuleFamily)pair.Key));
            if (entry.Value != pair.Value) MeadworksConfig.Amend(entry, Plugin.Guid, ConfigScope.ServerAuthoritative, pair.Value);
        }
        Current = candidate!; revision++;
        log.Verbose("Edge rules applied: revision " + revision + ", " + Current.Weapons.Count + " prefab entries.");
        return true;
    }

    private void ReadServer()
    {
        if (!MeadworksAuthority.ProductActive(Plugin.Guid, out _)) return;
        var carriers = Enum.GetValues(typeof(RuleFamily)).Cast<RuleFamily>().ToDictionary(f => (SecondaryAttackYamlDomainId)f,
            f => config.Get(EdgeDataFiles.Carrier(f)));
        var signature = string.Join("\n", carriers.Values);
        if (signature == remoteStamp || !RuleDocuments.TryReadCarriers(carriers, out var texts)) return;
        remoteStamp = signature;
        if (!SecondaryAttackConfigLoader.TryCompileSnapshot(revision + 1, texts!, out var candidate))
        {
            log.Warning("Server Edge YAML rejected; previous rules retained.");
            return;
        }
        Current = candidate!; revision++;
    }

    private static string Stamp(string path)
    {
        var file = new FileInfo(path); file.Refresh();
        return file.Length + ":" + file.LastWriteTimeUtc.Ticks;
    }

    internal bool ReloadForImport()
    {
        int before = Current.SnapshotId;
        Tick(true);
        return Current.SnapshotId != before;
    }

    internal void Shutdown()
    {
        Current = SecondaryAttackCompiledSnapshot.Empty;
        RuleDiagnostics.OnInfo = RuleDiagnostics.OnWarning = RuleDiagnostics.OnError = null;
        SecondaryAttackWarningLog.Reset();
    }
}
