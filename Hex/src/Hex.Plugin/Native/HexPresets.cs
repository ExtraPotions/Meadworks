// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original preset storage. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexPresets
{
    static string path = "", disk = "";
    static HexPresetDocument document = new();
    static IDisposable? lease;
    static ManualLogSource? log;
    static bool readable;
    static long generation = -1, restoredCharacter;
    internal static IReadOnlyList<string> Names => document.Presets.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    internal static string Selected => document.Selected;
    internal static void Initialize(ManualLogSource logger)
    {
        log = logger;
        path = Path.Combine(Paths.ConfigPath, "Meadworks", "hex", "presets.json");
        Reload();
        lease = MeadworksDataFiles.Register(new DataFileRegistration(Plugin.Guid, "Hex presets", path, ReadDisk, Validate, (expected, proposed) => Write(expected, proposed)));
    }
    internal static void Shutdown()
    {
        lease?.Dispose(); lease = null; log = null; path = disk = "";
        document = new(); readable = false; generation = -1; restoredCharacter = 0;
    }
    static string ReadDisk()
    {
        if (!File.Exists(path)) return "";
        if (new FileInfo(path).Length > HexPresetJson.MaximumCharacters * 4L) throw new FormatException("Preset file exceeds its size limit.");
        return File.ReadAllText(path, Encoding.UTF8);
    }
    static HexPresetDocument Read(string text) => HexPresetJson.Read(text, (key, value) => Plugin.Settings?.ValidPreference(key, value) == true);
    static IReadOnlyList<string> Validate(string text)
    {
        try { Read(text); return Array.Empty<string>(); }
        catch { return new[] { HexText.Text("preset_invalid") }; }
    }
    static bool Reload()
    {
        try { var text = ReadDisk(); var next = Read(text); disk = text; document = next; readable = true; return true; }
        catch { readable = false; log?.LogWarning("Hex preset file could not be read; existing file preserved."); return false; }
    }
    static bool Write(string expected, string proposed, bool importOnly = false)
    {
        if (!Plugin.Ready || !Plugin.Active && !importOnly || Plugin.Settings == null || path.Length == 0) return false;
        string? temporary = null;
        try
        {
            var next = Read(proposed);
            if (ReadDisk() != expected) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporary = path + ".write-" + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, proposed, new UTF8Encoding(false));
            if (ReadDisk() != expected) return false;
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
            else File.Move(temporary, path);
            disk = proposed; document = next; readable = true;
            return true;
        }
        catch { return false; }
        finally { if (temporary != null && File.Exists(temporary)) try { File.Delete(temporary); } catch { } }
    }
    internal static string ImportDisk()
    {
        if (!Plugin.Ready || path.Length == 0 || !Reload()) throw new InvalidOperationException("Hex presets unavailable.");
        return disk;
    }
    internal static string ImportProposal(string expected, IReadOnlyDictionary<string, object> values)
    {
        var next = Read(expected);
        const string name = "Imported VAT";
        if (next.Presets.ContainsKey(name)) throw new InvalidOperationException("Rename or remove the existing Imported VAT preset before importing again.");
        if (next.Presets.Count >= HexPresetJson.MaximumPresets) throw new InvalidOperationException("Hex preset limit reached.");
        foreach (var value in values) if (Plugin.Settings?.ValidPreference(value.Key, value.Value) != true) throw new FormatException("Invalid imported preset preference.");
        next.Presets.Add(name, values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        return HexPresetJson.Write(next); // Keep selection unchanged; import never activates the preferences.
    }
    internal static void ImportBackup(string expected)
    {
        if (ImportDisk() != expected) throw new InvalidOperationException("Hex presets changed; preview again.");
        if (File.Exists(path)) File.Copy(path, path + ".import-" + Guid.NewGuid().ToString("N") + ".bak", false);
    }
    internal static void ImportWrite(string expected, string proposed)
    {
        if (!Write(expected, proposed, importOnly: true)) throw new IOException("Hex preset import failed; existing file preserved.");
    }
    internal static void CancelJoinRestore()
    {
        generation = CruciblePlugin.Session.Generation;
        restoredCharacter = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0;
    }
    internal static void Tick()
    {
        var player = Player.m_localPlayer;
        var settings = Plugin.Settings;
        var session = CruciblePlugin.Session;
        if (generation != session.Generation) { generation = session.Generation; restoredCharacter = 0; }
        if (!readable || settings == null || settings.RestorePresetOnJoin.Value != true || !Plugin.Active
            || !settings.Enabled.Value || !session.AuthorityReady || !HexMovement.Owned(player)
            || !MeadworksAuthority.ProductActive(Plugin.Guid, out _) || player.GetPlayerID() == 0 || restoredCharacter == player.GetPlayerID()) return;
        // One restoration per character/session, not each respawn or permission update.
        restoredCharacter = player.GetPlayerID();
        if (document.Selected == "" || !document.Presets.TryGetValue(document.Selected, out var snapshot)) return;
        try { settings.ApplyPreferences(snapshot); }
        catch { log?.LogWarning("Hex preset preferences may be partially applied; configuration save failed."); }
    }
    internal static string Execute(string[] args)
    {
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings == null) return HexText.Text("preset_unavailable");
        if (args.Length < 2) return HexText.Text("preset_help");
        var verb = args[1].ToLowerInvariant();
        if (verb == "reload" && args.Length == 2) return Reload() ? HexText.Text("preset_reloaded") : HexText.Text("preset_invalid");
        if (!readable) return HexText.Text("preset_invalid");
        if (verb == "list" && args.Length == 2)
            return document.Presets.Count == 0 ? HexText.Text("preset_empty") : string.Join("\n", document.Presets.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => HexText.Format("preset_row", name, name.Equals(document.Selected, StringComparison.OrdinalIgnoreCase) ? "*" : "")));
        if (args.Length < 3 || verb != "save" && verb != "load" && verb != "delete") return HexText.Text("preset_help");
        var name = string.Join(" ", args.Skip(2));
        if (!HexPresetJson.NameValid(name)) return HexText.Text("preset_name_invalid");
        var next = document.Copy();
        if (verb == "save")
        {
            if (Plugin.Settings == null || !next.Presets.ContainsKey(name) && next.Presets.Count >= HexPresetJson.MaximumPresets)
                return HexText.Text("preset_limit");
            next.Presets[name] = Plugin.Settings.Snapshot(); next.Selected = name;
        }
        else if (!next.Presets.ContainsKey(name)) return HexText.Text("preset_missing");
        else if (verb == "load") next.Selected = name;
        else
        {
            next.Presets.Remove(name);
            if (next.Selected.Equals(name, StringComparison.OrdinalIgnoreCase)) next.Selected = "";
        }
        if (!Write(disk, HexPresetJson.Write(next))) return HexText.Text("preset_write_failed");
        if (verb == "load")
        {
            CancelJoinRestore();
            try { Plugin.Settings!.ApplyPreferences(document.Presets[name]); }
            catch { return HexText.Text("preset_apply_failed"); }
            return HexText.Text("preset_loaded");
        }
        return HexText.Text(verb == "save" ? "preset_saved" : "preset_deleted");
    }
}
