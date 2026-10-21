// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473
// Changed by expDARE, 2026: persist the existing regional state without a second pressure engine.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.AdoptedCm;

// Source timestamps are relative to Time.time. Resume that clock at the last save so offline time
// neither clears regional Karma nor grants a fresh cooldown/grace period on each reload.
internal static partial class CreatureKarmaManager
{
    static readonly string[] PressureKeys = { CountedDeathKey, EnforcerKey, EnforcerSummonedKey, EnforcerNameKey, EnforcerLevelBonusKey, EnforcerIsBossKey, EnforcerBossHudKey, EnforcerLootKey, EnforcerLootDroppedKey, EnforcerPresenceAnchorStoredKey, EnforcerPresenceInteriorKey, EnforcerPresenceZoneXKey, EnforcerPresenceZoneYKey };
    internal static bool MigrateOwnedPressureState(ZDO zdo) => CreatureModifierManager.MigrateOwnedKeys(zdo, PressureKeys, "wildblood.cm.karma_imported");
    static string LegacyPressureKey(string key) => "CreatureManager_" + key.Substring("wildblood.cm.".Length);
    static bool ReadPressureBool(ZDO? zdo, string key, bool fallback) => zdo == null ? fallback : zdo.GetBool(key, zdo.GetBool(LegacyPressureKey(key), fallback));
    static int ReadPressureInt(ZDO? zdo, string key, int fallback) => zdo == null ? fallback : zdo.GetInt(key, zdo.GetInt(LegacyPressureKey(key), fallback));
    static string ReadPressureString(ZDO? zdo, string key, string fallback) => zdo == null ? fallback : zdo.GetString(key, zdo.GetString(LegacyPressureKey(key), fallback));
    static float pressureClockOffset;
    internal static float PressureTime => Time.time + pressureClockOffset;
    internal static void ResetPressureClock() => pressureClockOffset = 0;
    internal static string ExportPressure(long world)
    {
        lock (Sync)
        {
            float now = PressureTime;
            var text = new StringBuilder("wildblood-karma:1:").Append(world.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(now.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            foreach (var row in Sectors.OrderBy(row => row.Key, StringComparer.Ordinal))
            {
                ApplyDecayUnsafe(row.Value, now);
                var state = row.Value;
                if (!Finite(state.Karma) || !Finite(state.LastKarmaTime) || !Finite(state.LastEnforcerTime))
                    throw new InvalidDataException("Non-finite regional pressure state.");
                text.Append(row.Key).Append('|').Append(state.Karma.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|').Append(state.LastKarmaTime.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|').Append(state.LastEnforcerTime.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            return text.ToString();
        }
    }
    internal static void ImportPressure(string text, long world)
    {
        using var reader = new StringReader(text);
        var header = (reader.ReadLine() ?? "").Split(':');
        if (header.Length != 4 || header[0] != "wildblood-karma" || header[1] != "1" ||
            !long.TryParse(header[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long savedWorld) || savedWorld != world ||
            !Number(header[3], out float clock) || clock < 0)
            throw new InvalidDataException("Unsupported or mismatched pressure save; original retained.");
        var prepared = new Dictionary<string, SectorState>(StringComparer.Ordinal);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            var parts = line.Split('|');
            if (prepared.Count >= MaximumSectorStates || parts.Length != 4 || !SectorKey(parts[0]) ||
                !Number(parts[1], out float karma) || karma < 0 ||
                !Number(parts[2], out float lastKarma) || lastKarma < 0 || lastKarma > clock ||
                !Number(parts[3], out float lastEnforcer) || lastEnforcer > clock || prepared.ContainsKey(parts[0]))
                throw new InvalidDataException("Invalid regional pressure row; original retained.");
            prepared.Add(parts[0], new SectorState { Karma = karma, LastKarmaTime = lastKarma, LastEnforcerTime = lastEnforcer });
        }
        lock (Sync)
        {
            Sectors.Clear(); SectorPruneQueue.Clear();
            foreach (var row in prepared) { Sectors.Add(row.Key, row.Value); SectorPruneQueue.Enqueue(row.Key); }
            pressureClockOffset = clock - Time.time;
        }
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Number(string text, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Finite(value);
    static bool SectorKey(string text)
    {
        if (text.Length < 5 || (text[0] != 'O' && text[0] != 'D') || text[1] != ':') return false;
        var xy = text.Substring(2).Split(',');
        return xy.Length == 2 && int.TryParse(xy[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
            int.TryParse(xy[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) &&
            text == text.Substring(0, 2) + x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture);
    }
}

// Installed explicitly with the pressure module, never by assembly-wide PatchAll.
internal static class NativePressurePersistence
{
    const int MaximumBytes = 4 * 1024 * 1024;
    static long world, generation = -1;
    static bool loaded, writable;
    static bool StorageAuthority => CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId != 0 &&
        ZNet.instance != null && ZNet.instance.IsServer();
    static bool Authority => StorageAuthority && Plugin.NativeRulesReady && WildbloodModules.Active(WildbloodModule.Pressure);
    static string PathFor(long id) => Path.Combine(Paths.ConfigPath, "Meadworks", "wildblood", "worlds", id.ToString(CultureInfo.InvariantCulture) + ".karma");
    internal static void Tick()
    {
        if (!Authority) return;
        var session = CruciblePlugin.Session;
        if (loaded && world == session.WorldId && generation == session.Generation) return;
        // The activation owner stops previous-world producers before entering this generation.
        CreatureKarmaManager.ResetRuntimeState(); CreatureKarmaManager.ResetPressureClock();
        world = session.WorldId; generation = session.Generation; loaded = true; writable = false;
        try
        {
            string path = PathFor(world);
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Pressure save exceeds 4 MiB.");
                CreatureKarmaManager.ImportPressure(File.ReadAllText(path), world);
            }
            writable = true;
        }
        catch (Exception error) { Plugin.NativeLogger?.LogWarning("Pressure state retained on disk; save refused: " + error.Message); }
    }
    internal static void Save()
    {
        // Disabling pressure stops new producers, but must still flush the loaded world's last state.
        if (!StorageAuthority || !loaded || !writable || world != CruciblePlugin.Session.WorldId || generation != CruciblePlugin.Session.Generation) return;
        string temporary = "";
        try
        {
            string text = CreatureKarmaManager.ExportPressure(world);
            if (Encoding.UTF8.GetByteCount(text) > MaximumBytes) throw new InvalidDataException("Pressure save exceeds 4 MiB.");
            string path = PathFor(world); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporary = path + ".write-" + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            if (!StorageAuthority || world != CruciblePlugin.Session.WorldId || generation != CruciblePlugin.Session.Generation) return;
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
        }
        catch (Exception error) { Plugin.NativeLogger?.LogWarning("Pressure save refused; previous file retained: " + error.Message); }
        finally
        {
            try { if (temporary.Length != 0 && File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) { Plugin.NativeLogger?.LogWarning("Pressure temporary cleanup refused: " + error.GetType().Name); }
        }
    }
    internal static void Reset() { loaded = writable = false; world = 0; generation = -1; CreatureKarmaManager.ResetPressureClock(); }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Save), new[] { typeof(bool), typeof(bool), typeof(bool) })]
internal static class NativePressureSavePatch
{
    static void Prefix() => NativePressurePersistence.Save();
}
[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Shutdown))]
internal static class NativePressureShutdownPatch
{
    static void Prefix() { NativePressurePersistence.Save(); NativePressurePersistence.Reset(); }
}
