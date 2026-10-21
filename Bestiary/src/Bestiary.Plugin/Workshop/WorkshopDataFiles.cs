// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Workshop.Adopted;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Bestiary.Runtime.Workshop;

// Almanac already supplies the shared KitPanel/KitTooltip editor. These registrations own validation, stale checks,
// backups and permission; the source engines own parsing and complete-snapshot publication.
internal static class WorkshopDataFiles
{
    private static readonly Dictionary<string, IDisposable> leases = new(StringComparer.Ordinal);
    private static WorkshopSyncContext? context;
    private static float nextRefresh;
    internal static void Start(WorkshopSyncContext sync)
    {
        context = sync;
        Add("Workshop status", Path.Combine(WorkshopHost.DataDirectory, ".workshop-status.txt"), Status, _ => Array.Empty<string>(), (_, _) => false);
        Refresh();
    }
    internal static void Tick()
    {
        if (UnityEngine.Time.unscaledTime < nextRefresh) return;
        nextRefresh = UnityEngine.Time.unscaledTime + 2; Refresh();
    }
    private static void Refresh()
    {
        if (context == null) return;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string domain in new[] { "creatures", "attacks", "ai", "factions", "projectile" }) paths.Add(Path.Combine(WorkshopHost.DataDirectory, domain + ".yml"));
        if (context.IsSourceOfTruth && Directory.Exists(WorkshopHost.DataDirectory))
        {
            foreach (string path in Directory.EnumerateFiles(WorkshopHost.DataDirectory)) if (Domain(path) != null) paths.Add(path);
            string localization = CreatureServerLocalization.LocalizationDirectoryPath;
            if (Directory.Exists(localization)) foreach (string path in Directory.EnumerateFiles(localization, "*.yml")) paths.Add(path);
        }
        foreach (string path in paths)
        {
            if (leases.ContainsKey(path)) continue;
            bool localization = Path.GetDirectoryName(path) == CreatureServerLocalization.LocalizationDirectoryPath;
            string? domain = Domain(path);
            Add("Workshop / " + (localization ? "localization/" : "") + Path.GetFileName(path), path,
                () => context?.IsSourceOfTruth == true ? (File.Exists(path) ? File.ReadAllText(path) : "") : CreatureDomainManager.ExportDomain(domain ?? ""),
                text => Validate(path, domain, localization, text), (original, proposed) => Apply(path, domain, localization, original, proposed));
        }
        foreach (string obsolete in leases.Keys.Where(p => !paths.Contains(p) && !p.EndsWith(".workshop-status.txt", StringComparison.Ordinal)).ToArray())
        { leases[obsolete].Dispose(); leases.Remove(obsolete); }
    }
    private static void Add(string name, string path, Func<string> read, Func<string, IReadOnlyList<string>> validate, Func<string, string, bool> apply)
        => leases[path] = MeadworksDataFiles.Register(new DataFileRegistration(Plugin.Guid, name, path, read, validate, apply));
    internal static string? Domain(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        if (!new[] { ".yml", ".yaml" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return null;
        foreach (string domain in new[] { "creatures", "attacks", "ai", "projectile", "factions" })
            if (stem.Equals(domain, StringComparison.OrdinalIgnoreCase) || (domain != "factions" && stem.StartsWith(domain + "_", StringComparison.OrdinalIgnoreCase))) return domain;
        return null;
    }
    private static IReadOnlyList<string> Validate(string path, string? domain, bool localization, string text)
    {
        bool valid = localization ? CreatureYaml.TryReadLocalizationMap(text, path, out _)
            : domain switch
            {
                "creatures" => CreatureYaml.TryReadDefinitions<CreatureDefinition>(text, path, out _),
                "attacks" => CreatureYaml.TryReadDefinitions<AttackDefinition>(text, path, out _),
                "ai" => CreatureYaml.TryReadDefinitions<AiDefinition>(text, path, out _),
                "factions" => CreatureYaml.TryReadDefinitions<FactionDefinition>(text, path, out _),
                "projectile" => CreatureYaml.TryReadDefinitions<ProjectileDefinition>(text, path, out _),
                _ => false
            };
        if (valid && !localization && context?.IsSourceOfTruth == true) valid = CreatureDomainManager.ValidateCandidateFile(path, text);
        return valid ? Array.Empty<string>() : new[] { path + ": " + WorkshopRuntime.LastError };
    }
    private static bool Apply(string path, string? domain, bool localization, string original, string proposed)
    {
        if (!WorkshopHost.Active || context?.IsSourceOfTruth != true) return false;
        string current = File.Exists(path) ? File.ReadAllText(path) : "";
        if (!string.Equals(current, original, StringComparison.Ordinal) || Validate(path, domain, localization, proposed).Count != 0) return false;
        string temporary = path + ".bestiary-pending";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(temporary, proposed);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bestiary-backup", true);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        if (localization) CreatureServerLocalization.ReloadFromDiskAndSync(); else CreatureDomainManager.RequestConfigurationReload();
        return true;
    }
    private static string Status() => "Bestiary workshop\nState: " + (WorkshopHost.Active ? "active" : "off")
        + "\nAuthority: " + (context?.IsSourceOfTruth == true ? "this process" : "server")
        + "\nHot reload: " + WorkshopHost.HotReload + "\nTexture transfer: " + WorkshopHost.TextureSync
        + "\nCache limit: " + WorkshopHost.CacheLimit / (1024 * 1024) + " MB\n"
        + CreatureTextureSync.DescribeState() + "\nLast warning or error: " + WorkshopRuntime.LastError + "\n";
    internal static void Stop()
    {
        foreach (var lease in leases.Values) lease.Dispose(); leases.Clear(); context = null; nextRefresh = 0;
    }
}
