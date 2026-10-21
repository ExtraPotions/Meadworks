// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-only
// Changed by expDARE, 2026: FrozenKing_p2 identity guard adapted from CreatureManager 1.2.5 CreatureLevelManager at
// Portions Copyright (C) sighsorry1029; 47e45a6e8c3b389a51e42fc7387d1a4fb90af473 (GPL-3.0-only); original notice in THIRD_PARTY_NOTICES.
using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Bestiary.Runtime.Integration;
using UnityEngine;
namespace ExtraPotions.Bestiary.Runtime.Workshop;
internal static class WorkshopHost
{
    internal const string ModGUID = Plugin.Guid + ".workshop", ModName = "Bestiary workshop";
    internal static WorkshopLog Log = null!;
    internal static bool Active => WorkshopRuntime.Initialized && Requested;
    internal static bool CanLoadIdentities => Plugin.Ready && !BestiaryIntegration.ProductYields && !BestiaryIntegration.WorkshopYields;
    internal static bool Requested => Native.Adapters.NativeConfiguration.Active && !BestiaryIntegration.WorkshopYields && Plugin.Settings?.Flag(BestiarySettings.General + ".Workshop Enabled") == true;
    internal static bool HotReload => Requested && Plugin.Settings?.Flag(BestiarySettings.Workshop + ".Hot Reload") == true;
    internal static bool TextureSync => Requested && Plugin.Settings?.Flag(BestiarySettings.Workshop + ".Texture Sync") == true;
    internal static long CacheLimit => (long)(Plugin.Settings?.Get<int>(BestiarySettings.Workshop + ".Texture Cache Limit") ?? 256) * 1024L * 1024L;
    internal static string DataDirectory
    {
        get
        {
            string relative = Plugin.Settings?.Get<string>(BestiarySettings.Workshop + ".Data Folder") ?? "Meadworks/bestiary/workshop";
            // WorkshopRuntime.Tick reads this every frame; the path work allocates, so reuse it while the setting is unchanged.
            if (dataFolderTarget != null && relative == dataFolderSetting) return dataFolderTarget;
            string setting = relative;
            relative = relative.Replace('\\', '/');
            if (relative.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase)) relative = relative.Substring(15);
            string root = Path.GetFullPath(Paths.ConfigPath), target = Path.GetFullPath(Path.Combine(root, relative));
            var comparison = Application.platform == RuntimePlatform.LinuxPlayer ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (!target.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison))
                throw new InvalidOperationException("Workshop data folder must stay under BepInEx/config.");
            dataFolderSetting = setting; dataFolderTarget = target;
            return target;
        }
    }
    private static string? dataFolderSetting, dataFolderTarget;
    internal static bool IsFrozenKingPhaseTwo(Character? character)
    {
        if (character == null) return false;
        var data = character.GetComponent<ZNetView>()?.GetZDO();
        return data != null && data.GetPrefab() != 0 ? data.GetPrefab() == "FrozenKing_p2".GetStableHashCode()
            : string.Equals(Utils.GetPrefabName(character.gameObject), "FrozenKing_p2", StringComparison.Ordinal);
    }
    internal static void RegisterCommands() { if (Active) WorkshopCommands.Register(); }
    internal static string? CandidatePath, CandidateText;
    internal static string ReadText(string path) => CandidatePath == path ? CandidateText! : File.ReadAllText(path);
}

internal sealed class WorkshopLog
{
    private readonly ManualLogSource source;
    private readonly ExtraPotions.Meadworks.Crucible.MeadworksLog quiet;
    internal WorkshopLog(ManualLogSource source) { this.source = source; quiet = ExtraPotions.Meadworks.Crucible.MeadworksLog.For(source, Plugin.Name); }
    internal void LogDebug(object data) => quiet.Verbose(Convert.ToString(data) ?? "");
    internal void LogInfo(object data) => quiet.Verbose(Convert.ToString(data) ?? "");
    internal void LogWarning(object data) => source.LogWarning(data);
    internal void LogError(object data) => source.LogError(data);
}
