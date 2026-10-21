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
        Directory.CreateDirectory(directory);
        foreach (RuleFamily family in Enum.GetValues(typeof(RuleFamily)))
        {
            var path = Path.Combine(directory, EdgeDataFiles.BaseName(family));
            if (File.Exists(path)) continue;
            using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Edge.defaults." + EdgeDataFiles.BaseName(family))
                ?? throw new InvalidOperationException("Missing Edge default " + family);
            using var reader = new StreamReader(stream);
            File.WriteAllText(path, reader.ReadToEnd());
        }
        Tick(true);
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
        var paths = Directory.GetFiles(directory, "*.yml").Where(p => EdgeDataFiles.Classify(p) != null).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var stamps = paths.ToDictionary(p => p, p => Stamp(p));
        var signature = string.Join("\n", paths.Select(p => p + ":" + stamps[p]));
        if (!force && signature == localStamp) return;
        var files = paths.ToDictionary(p => p, File.ReadAllText);
        if (paths.Any(p => stamps[p] != Stamp(p)) || !paths.SequenceEqual(Directory.GetFiles(directory, "*.yml")
            .Where(p => EdgeDataFiles.Classify(p) != null).OrderBy(p => p, StringComparer.Ordinal))) return;
        localStamp = signature;
        if (!RuleDocuments.TryMerge(files, out var merged) || !SecondaryAttackConfigLoader.TryCompileSnapshot(revision + 1,
            new SecondaryAttackYamlTexts(merged), out var candidate))
        {
            log.Warning("Edge YAML rejected; previous rules retained.");
            return;
        }
        var carriers = RuleDocuments.Stamp(merged);
        foreach (var pair in carriers)
        {
            var entry = (ConfigEntry<string>)config.Entry(EdgeDataFiles.Carrier((RuleFamily)pair.Key));
            if (entry.Value != pair.Value) MeadworksConfig.Amend(entry, Plugin.Guid, ConfigScope.ServerAuthoritative, pair.Value);
        }
        Current = candidate!; revision++;
        log.Verbose("Edge rules applied: revision " + revision + ", " + Current.Weapons.Count + " prefab entries.");
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
