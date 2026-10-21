// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using ExtraPotions.Bestiary.Conversion;
using ExtraPotions.Bestiary.Runtime.Workshop.Adopted;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Bestiary.Runtime.Workshop;

internal sealed class WorkshopConversion : IDisposable
{
    private sealed class Preview
    {
        internal long Generation;
        internal string Target = "";
        internal string[] Listing = Array.Empty<string>();
        internal readonly Dictionary<string, byte[]> Copies = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Hashes = new(StringComparer.Ordinal);
        internal readonly List<string> Notes = new();
        public override string ToString() => string.Join("\n", Notes.Concat(Copies.Keys.Select(p => "Copy " + p)));
    }
    private readonly IDisposable registration;
    private Preview? current;
    private bool disposed;
    private static string Source => Path.Combine(Paths.ConfigPath, "CreatureManager");
    private bool Allowed => !disposed && Plugin.Ready && ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAuthority;
    internal WorkshopConversion()
    {
        registration = MeadworksConversion.Register(new ConversionRegistration("bestiary.creaturemanager.workshop", Plugin.Guid,
            StepAsideRules.CreatureManagerGuid, "CreatureManager workshop", () => Allowed && Directory.Exists(Source),
            MakePreview, (document, resolutions, backup) => Apply(document, backup)));
    }
    private static string Hash(string path)
    {
        if (!File.Exists(path)) return "missing";
        using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    private static string[] List() => Directory.Exists(Source) ? Directory.GetFiles(Source, "*", SearchOption.AllDirectories)
        .Select(p => p.Substring(Source.Length + 1).Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
    private object MakePreview()
    {
        if (!Allowed) throw new InvalidOperationException("Workshop import requires the authority.");
        var preview = new Preview { Generation = ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation, Target = WorkshopHost.DataDirectory, Listing = List() };
        var plan = WorkshopImport.Plan(preview.Listing);
        foreach (var file in plan.Files)
        {
            if (file.Action != WorkshopAction.Copy) { if (file.Action == WorkshopAction.Wildblood) preview.Notes.Add("Left to Wildblood: " + file.Path); continue; }
            string source = Path.Combine(Source, file.Path), target = Path.Combine(preview.Target, file.Path);
            string before = Hash(source); byte[] bytes = File.ReadAllBytes(source);
            if (before != Hash(source)) throw new IOException("Source changed while previewing " + source);
            if (bytes.Length > (file.Path.StartsWith("textures/", StringComparison.Ordinal) ? CreatureTextureSync.MaxFileBytes : 2 * 1024 * 1024))
                throw new InvalidDataException("Workshop file exceeds the source size limit: " + file.Path);
            if (file.Path.StartsWith("textures/", StringComparison.Ordinal) && !CreatureTextureSync.TryValidatePng(bytes, out _, out _, out string pngError))
                throw new InvalidDataException(file.Path + ": " + pngError);
            if (!file.Path.StartsWith("textures/", StringComparison.Ordinal) && !ValidateText(target, Encoding.UTF8.GetString(bytes)))
                throw new InvalidDataException("Invalid workshop YAML: " + file.Path);
            preview.Copies[target] = bytes; preview.Hashes[source] = before; preview.Hashes[target] = Hash(target);
        }
        if (preview.Copies.Count == 0) throw new InvalidOperationException("No supported workshop files found.");
        foreach (string sample in plan.SampleTextures) preview.Notes.Add("User-owned sample texture retained, never shipped by Bestiary: " + sample);
        preview.Notes.Add("Source files stay untouched. Apply requires this fresh preview and backups.");
        current = preview; return preview;
    }
    private static bool ValidateText(string path, string text)
    {
        if (Path.GetDirectoryName(path) == Path.Combine(WorkshopHost.DataDirectory, "localization")) return CreatureYaml.TryReadLocalizationMap(text, path, out _);
        return WorkshopDataFiles.Domain(path) switch
        {
            "creatures" => CreatureYaml.TryReadDefinitions<CreatureDefinition>(text, path, out _),
            "attacks" => CreatureYaml.TryReadDefinitions<AttackDefinition>(text, path, out _),
            "ai" => CreatureYaml.TryReadDefinitions<AiDefinition>(text, path, out _),
            "factions" => CreatureYaml.TryReadDefinitions<FactionDefinition>(text, path, out _),
            "projectile" => CreatureYaml.TryReadDefinitions<ProjectileDefinition>(text, path, out _), _ => false
        };
    }
    private object Apply(object document, bool backup)
    {
        if (!Allowed || !backup || current == null || !ReferenceEquals(document, current)
            || current.Generation != ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation || current.Target != WorkshopHost.DataDirectory
            || !current.Listing.SequenceEqual(List()) || current.Hashes.Any(p => Hash(p.Key) != p.Value))
            throw new InvalidOperationException("Import requires the authority, fresh unchanged preview and backups.");
        var preview = current; current = null;
        var before = preview.Copies.Keys.ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        string stamp = DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var pair in before) if (pair.Value != null) File.Copy(pair.Key, pair.Key + ".import-" + stamp + ".bak", false);
        try
        {
            foreach (var pair in preview.Copies) Write(pair.Key, pair.Value);
            if (WorkshopRuntime.Initialized)
            {
                if (!CreatureDomainManager.LoadDefinitionsFromDisk(true) || (WorkshopHost.Active && !CreatureServerLocalization.ReloadFromDiskAndSync()))
                    throw new InvalidOperationException("The complete workshop transaction was rejected; files and active data are restored.");
            }
            return "Copied " + preview.Copies.Count + " workshop files with backups; sources retained. Remove CreatureManager and restart to activate Bestiary if its workshop currently steps aside.";
        }
        catch
        {
            foreach (var pair in before) { if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); } else Write(pair.Key, pair.Value); }
            if (WorkshopRuntime.Initialized) { CreatureDomainManager.LoadDefinitionsFromDisk(true); if (WorkshopHost.Active) CreatureServerLocalization.ReloadFromDiskAndSync(); }
            throw;
        }
    }
    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".import-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Dispose() { disposed = true; current = null; registration.Dispose(); }
}
