// Portions Copyright (C) sighsorry1029. CreatureManager 1.2.5 at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: Bestiary workshop namespace, C01 carriers and owned lifecycle; level/karma/modifier domains excluded.
// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using ExtraPotions.Bestiary.Runtime.Workshop;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraPotions.Bestiary.Runtime.Workshop.Adopted;

internal static class CreatureServerLocalization
{
    private const string SyncedPayloadKey = "LocalizationBundle";
    private const long ReloadDebounceTicks = TimeSpan.TicksPerSecond / 4;
    private const long SyncedApplyDebounceTicks = TimeSpan.TicksPerSecond / 10;
    private const long WatcherRetryTicks = TimeSpan.TicksPerSecond * 5;
    private const int MaxSyncedPayloadBytes = 2 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly object Sync = new();
    private static readonly HashSet<string> KnownValheimLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Chinese", "Chinese_Trad", "Czech", "Danish", "Dutch", "English", "Finnish", "French", "German",
        "Greek", "Hungarian", "Italian", "Japanese", "Korean", "Norwegian", "Polish", "Portuguese_European",
        "Portuguese_Brazilian", "Russian", "Slovak", "Spanish", "Swedish", "Turkish"
    };
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .DisableAliases()
        .Build();
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .Build();

    private static WorkshopSyncContext? WorkshopSyncContext;
    private static WorkshopTextCarrier? SyncedPayload;
    private static FileSystemWatcher? Watcher;
    private static DateTime PendingDiskReloadTime = DateTime.MaxValue;
    private static DateTime PendingSyncedApplyTime = DateTime.MaxValue;
    private static bool DiskReloadPending;
    private static bool SyncedApplyPending;
    private static bool SuppressSyncedApply;
    private static bool AwaitingRemotePayload;
    private static bool WatcherResetPending;
    private static bool TemplatesEnsuredThisSession;
    private static ServerLocalizationPayload ActivePayload = CreateEmptyPayload();

    private static Localization? AppliedLocalization;
    private static string AppliedLanguage = "";
    private static readonly Dictionary<string, OriginalTranslation> OriginalTranslations =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> LastAppliedTranslations =
        new(StringComparer.Ordinal);

    internal static string LocalizationDirectoryPath =>
        Path.Combine(CreatureDomainManager.ConfigDirectoryPath, "localization");

    private readonly struct OriginalTranslation
    {
        internal OriginalTranslation(bool existed, string? value)
        {
            Existed = existed;
            Value = value;
        }

        internal bool Existed { get; }
        internal string? Value { get; }
    }

    internal static void Initialize(WorkshopSyncContext configSync)
    {
        WorkshopSyncContext = configSync;
        SyncedPayload = new WorkshopTextCarrier(configSync, SyncedPayloadKey, "", 100);
        SyncedPayload.ValueChanged += OnSyncedPayloadChanged;
        configSync.SourceOfTruthChanged += OnSourceOfTruthChanged;

        if (configSync.IsSourceOfTruth && WorkshopHost.Active)
        {
            ReloadFromDiskAndSync();
            SetupWatcher();
        }
        else if (WorkshopHost.Active && !string.IsNullOrWhiteSpace(SyncedPayload.Value))
        {
            RequestSyncedPayloadApply();
        }
    }

    internal static void Dispose()
    {
        RestoreAppliedTranslations();
        Watcher?.Dispose();
        Watcher = null;

        if (SyncedPayload != null)
        {
            SyncedPayload.ValueChanged -= OnSyncedPayloadChanged;
            SyncedPayload = null;
        }

        if (WorkshopSyncContext != null)
        {
            WorkshopSyncContext.SourceOfTruthChanged -= OnSourceOfTruthChanged;
            WorkshopSyncContext = null;
        }

        DiskReloadPending = false;
        SyncedApplyPending = false;
        SuppressSyncedApply = false;
        AwaitingRemotePayload = false;
        WatcherResetPending = false;
        TemplatesEnsuredThisSession = false;
        PendingDiskReloadTime = DateTime.MaxValue;
        PendingSyncedApplyTime = DateTime.MaxValue;
        lock (Sync)
        {
            ActivePayload = CreateEmptyPayload();
        }
    }

    internal static void Update()
    {
        if (!WorkshopHost.Active) return;
        DateTime now = DateTime.UtcNow;
        if (DiskReloadPending && now >= PendingDiskReloadTime)
        {
            DiskReloadPending = false;
            PendingDiskReloadTime = DateTime.MaxValue;
            if (WorkshopSyncContext?.IsSourceOfTruth == true)
            {
                ReloadFromDiskAndSync();
                if (WatcherResetPending)
                {
                    WatcherResetPending = false;
                    SetupWatcher();
                }
            }
        }

        if (SyncedApplyPending && now >= PendingSyncedApplyTime)
        {
            SyncedApplyPending = false;
            PendingSyncedApplyTime = DateTime.MaxValue;
            if (WorkshopSyncContext?.IsSourceOfTruth == false && !AwaitingRemotePayload)
            {
                ApplySyncedPayload();
            }
        }
    }

    internal static void BeforeLanguageSetup(Localization localization)
    {
        if (!WorkshopHost.Active || !IsLiveLocalization(localization))
        {
            return;
        }

        RestoreAppliedTranslations(localization);
    }

    internal static void ApplyCurrentLocalization()
    {
        Localization? localization = Localization.m_instance;
        if (localization != null)
        {
            ApplyCurrentLocalization(localization, localization.GetSelectedLanguage());
        }
    }

    internal static void ApplyCurrentLocalization(Localization localization, string? language)
    {
        if (!WorkshopHost.Active || !IsLiveLocalization(localization))
        {
            return;
        }

        string selectedLanguage = language?.Trim() ?? "";
        if (selectedLanguage.Length == 0)
        {
            selectedLanguage = "English";
        }
        if (!ReferenceEquals(AppliedLocalization, localization) ||
            !AppliedLanguage.Equals(selectedLanguage, StringComparison.OrdinalIgnoreCase))
        {
            RestoreAppliedTranslations();
            AppliedLocalization = localization;
            AppliedLanguage = selectedLanguage;
        }

        Dictionary<string, string> translations;
        lock (Sync)
        {
            translations = CreatureYaml.BuildLocalizationForLanguage(ActivePayload, selectedLanguage);
        }

        bool changed = RestoreRemovedTranslations(localization, translations.Keys);
        foreach (KeyValuePair<string, string> translation in translations)
        {
            changed |= ApplyTranslation(localization, translation.Key, translation.Value);
        }

        if (changed || translations.Count > 0)
        {
            localization.m_cache.EvictAll();
        }
    }

    internal static void OnWorldShutdown()
    {
        if (WorkshopSyncContext?.IsSourceOfTruth != false)
        {
            return;
        }

        DiskReloadPending = false;
        SyncedApplyPending = false;
        AwaitingRemotePayload = true;
        PendingDiskReloadTime = DateTime.MaxValue;
        PendingSyncedApplyTime = DateTime.MaxValue;
        ClearActivePayloadAndRestore();
    }

    private static bool IsLiveLocalization(Localization localization)
    {
        Localization? live = Localization.m_instance;
        return live != null && ReferenceEquals(localization, live);
    }

    internal static void RefreshWatchers() => SetupWatcher();
    internal static bool ReloadFromDiskAndSync()
    {
        if (!WorkshopHost.Active || WorkshopSyncContext?.IsSourceOfTruth != true)
        {
            return false;
        }

        if (!TryLoadPayloadFromDisk(out ServerLocalizationPayload payload) ||
            !TrySerializeAndVerifyPayload(payload, out string serialized, out ServerLocalizationPayload verified))
        {
            WorkshopHost.Log.LogWarning(
                "Keeping the last-known-good server localization because at least one localization file is invalid.");
            return false;
        }

        if (SyncedPayload == null)
        {
            WorkshopHost.Log.LogError(
                "Failed to publish server localization because ServerSync is not initialized.");
            return false;
        }

        if (!string.Equals(SyncedPayload.Value, serialized, StringComparison.Ordinal))
        {
            SuppressSyncedApply = true;
            try
            {
                SyncedPayload.AssignLocalValue(serialized);
            }
            catch (Exception ex)
            {
                WorkshopHost.Log.LogError(
                    $"Failed to publish server localization; keeping the last-known-good synchronized value: {ex.Message}");
                return false;
            }
            finally
            {
                SuppressSyncedApply = false;
            }
        }

        lock (Sync)
        {
            ActivePayload = verified;
        }

        ApplyCurrentLocalization();
        NotifyLocalizationChanged();
        int languageCount = verified.Languages?.Count ?? 0;
        int tokenCount = verified.Languages?.Values.Sum(map => map.Count) ?? 0;
        WorkshopHost.Log.LogInfo(
            $"Loaded and synchronized {tokenCount} localization token(s) across {languageCount} language file(s).");
        return true;
    }

    private static bool TryLoadPayloadFromDisk(out ServerLocalizationPayload payload)
    {
        payload = CreateEmptyPayload();
        try
        {
            EnsureLocalizationDirectoryAndTemplates();
            Dictionary<string, Dictionary<string, string>> languages =
                new(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.EnumerateFiles(LocalizationDirectoryPath, "*.*", SearchOption.TopDirectoryOnly)
                         .Where(IsLocalizationFile)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (new FileInfo(file).Length > MaxSyncedPayloadBytes)
                {
                    throw new InvalidDataException(
                        $"Localization file '{file}' exceeds the {MaxSyncedPayloadBytes}-byte payload safety limit.");
                }

                string language = Path.GetFileNameWithoutExtension(file).Trim();
                if (language.Length == 0 || language.Length > 64)
                {
                    throw new InvalidDataException(
                        $"Localization file '{file}' must use a Valheim language name from 1 to 64 characters.");
                }

                if (!KnownValheimLanguages.Contains(language))
                {
                    WorkshopHost.Log.LogWarning(
                        $"Localization file '{file}' uses unrecognized language name '{language}'. It will synchronize, but only clients selecting that exact language name can use it.");
                }

                if (languages.ContainsKey(language))
                {
                    throw new InvalidDataException(
                        $"More than one localization file resolves to language '{language}'. Keep only one .yml or .yaml file per language.");
                }

                string yaml = File.ReadAllText(file, Encoding.UTF8);
                if (!CreatureYaml.TryReadLocalizationMap(yaml, file, out Dictionary<string, string> translations))
                {
                    return false;
                }

                languages[language] = translations;
            }

            payload = new ServerLocalizationPayload
            {
                Version = CreatureYaml.ServerLocalizationPayloadVersion,
                Languages = languages
            };
            WarnAboutMissingEnglishFallback(payload);
            return CreatureYaml.TryNormalizeLocalizationPayload(payload, "local server localization", out payload);
        }
        catch (Exception ex)
        {
            WorkshopHost.Log.LogError($"Failed to load server localization files: {ex.Message}");
            payload = CreateEmptyPayload();
            return false;
        }
    }

    private static void WarnAboutMissingEnglishFallback(ServerLocalizationPayload payload)
    {
        Dictionary<string, Dictionary<string, string>> languages = payload.Languages ??
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> englishTokens = languages.TryGetValue("English", out Dictionary<string, string>? english)
            ? new HashSet<string>(english.Keys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        int missingCount = languages
            .Where(entry => !entry.Key.Equals("English", StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => entry.Value.Keys)
            .Distinct(StringComparer.Ordinal)
            .Count(token => !englishTokens.Contains(token));
        if (missingCount > 0)
        {
            WorkshopHost.Log.LogWarning(
                $"Server localization has {missingCount} token(s) without an English fallback. Clients using an unconfigured language may see raw [token] text.");
        }
    }

    private static bool TrySerializeAndVerifyPayload(
        ServerLocalizationPayload payload,
        out string serialized,
        out ServerLocalizationPayload verified)
    {
        serialized = "";
        verified = CreateEmptyPayload();
        try
        {
            serialized = Serializer.Serialize(payload);
            if (Encoding.UTF8.GetByteCount(serialized) > MaxSyncedPayloadBytes)
            {
                WorkshopHost.Log.LogError(
                    $"Server localization payload exceeds the {MaxSyncedPayloadBytes}-byte safety limit.");
                return false;
            }
        }
        catch (Exception ex)
        {
            WorkshopHost.Log.LogError($"Failed to serialize server localization: {ex.Message}");
            return false;
        }

        return TryDeserializePayload(serialized, "local server localization round-trip", out verified);
    }

    private static bool TryDeserializePayload(
        string serialized,
        string source,
        out ServerLocalizationPayload payload)
    {
        payload = CreateEmptyPayload();
        if (string.IsNullOrWhiteSpace(serialized))
        {
            WorkshopHost.Log.LogError($"Failed to read {source}: localization payload is empty.");
            return false;
        }

        if (Encoding.UTF8.GetByteCount(serialized) > MaxSyncedPayloadBytes)
        {
            WorkshopHost.Log.LogError(
                $"Failed to read {source}: localization payload exceeds the {MaxSyncedPayloadBytes}-byte safety limit.");
            return false;
        }

        try
        {
            ServerLocalizationPayload? deserialized = Deserializer.Deserialize<ServerLocalizationPayload>(serialized);
            return CreatureYaml.TryNormalizeLocalizationPayload(deserialized, source, out payload);
        }
        catch (Exception ex)
        {
            WorkshopHost.Log.LogError($"Failed to read {source}: {ex.Message}");
            return false;
        }
    }

    private static void ApplySyncedPayload()
    {
        if (SyncedPayload == null || WorkshopSyncContext?.IsSourceOfTruth != false)
        {
            return;
        }

        string serialized = SyncedPayload.Value;
        if (string.IsNullOrWhiteSpace(serialized))
        {
            AwaitingRemotePayload = true;
            return;
        }

        if (!TryDeserializePayload(
                serialized,
                "server synchronized localization",
                out ServerLocalizationPayload payload))
        {
            WorkshopHost.Log.LogWarning(
                "Keeping the last-known-good server localization because the synchronized payload is invalid.");
            return;
        }

        lock (Sync)
        {
            ActivePayload = payload;
        }

        ApplyCurrentLocalization();
        NotifyLocalizationChanged();
    }

    private static void OnSyncedPayloadChanged()
    {
        if (SuppressSyncedApply || WorkshopSyncContext?.IsSourceOfTruth != false)
        {
            return;
        }

        // The carrier resets to its empty default between sessions; this is not a localization document.
        if (string.IsNullOrWhiteSpace(SyncedPayload?.Value))
        {
            AwaitingRemotePayload = true;
            SyncedApplyPending = false;
            PendingSyncedApplyTime = DateTime.MaxValue;
            return;
        }

        AwaitingRemotePayload = false;
        RequestSyncedPayloadApply();
    }

    private static void RequestSyncedPayloadApply()
    {
        SyncedApplyPending = true;
        PendingSyncedApplyTime = DateTime.UtcNow.AddTicks(SyncedApplyDebounceTicks);
    }

    private static void OnSourceOfTruthChanged(bool isSourceOfTruth)
    {
        SetupWatcher();
        if (isSourceOfTruth)
        {
            AwaitingRemotePayload = false;
            SyncedApplyPending = false;
            PendingSyncedApplyTime = DateTime.MaxValue;
            ClearActivePayloadAndRestore();
            NotifyLocalizationChanged();
            ReloadFromDiskAndSync();
            SetupWatcher();
            return;
        }

        DiskReloadPending = false;
        SyncedApplyPending = false;
        AwaitingRemotePayload = true;
        PendingDiskReloadTime = DateTime.MaxValue;
        PendingSyncedApplyTime = DateTime.MaxValue;
        Watcher?.Dispose();
        Watcher = null;
        ClearActivePayloadAndRestore();
        NotifyLocalizationChanged();
    }

    private static void SetupWatcher()
    {
        if (!WorkshopHost.HotReload || WorkshopSyncContext?.IsSourceOfTruth != true) { Watcher?.Dispose(); Watcher = null; return; }
        Watcher?.Dispose();
        Watcher = null;
        if (!WorkshopHost.Active || WorkshopSyncContext?.IsSourceOfTruth != true)
        {
            return;
        }

        WatcherResetPending = false;
        try
        {
            EnsureLocalizationDirectoryAndTemplates();
            Watcher = new FileSystemWatcher(LocalizationDirectoryPath)
            {
                IncludeSubdirectories = false,
                Filter = "*.*",
                SynchronizingObject = ThreadingHelper.SynchronizingObject
            };
            Watcher.Changed += OnLocalizationFileChanged;
            Watcher.Created += OnLocalizationFileChanged;
            Watcher.Deleted += OnLocalizationFileChanged;
            Watcher.Renamed += OnLocalizationFileChanged;
            Watcher.Error += OnLocalizationWatcherError;
            Watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Watcher?.Dispose();
            Watcher = null;
            WatcherResetPending = true;
            DiskReloadPending = true;
            PendingDiskReloadTime = DateTime.UtcNow.AddTicks(WatcherRetryTicks);
            WorkshopHost.Log.LogError(
                $"Failed to watch server localization files; retrying in 5 seconds: {ex.Message}");
        }
    }

    private static void OnLocalizationFileChanged(object sender, FileSystemEventArgs args)
    {
        if (!WorkshopHost.HotReload) return;
        bool relevant = IsLocalizationFile(args.FullPath) ||
                        args is RenamedEventArgs renamed && IsLocalizationFile(renamed.OldFullPath);
        if (!relevant || WorkshopSyncContext?.IsSourceOfTruth != true)
        {
            return;
        }

        DiskReloadPending = true;
        PendingDiskReloadTime = DateTime.UtcNow.AddTicks(ReloadDebounceTicks);
    }

    private static bool IsLocalizationFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase);
    }

    private static void OnLocalizationWatcherError(object sender, ErrorEventArgs args)
    {
        if (!WorkshopHost.Active || WorkshopSyncContext?.IsSourceOfTruth != true)
        {
            return;
        }

        WorkshopHost.Log.LogWarning(
            $"Server localization watcher lost file events and will be rebuilt: {args.GetException().Message}");
        WatcherResetPending = true;
        DiskReloadPending = true;
        PendingDiskReloadTime = DateTime.UtcNow.AddTicks(ReloadDebounceTicks);
    }

    private static void EnsureLocalizationDirectoryAndTemplates()
    {
        Directory.CreateDirectory(LocalizationDirectoryPath);
        if (TemplatesEnsuredThisSession)
        {
            return;
        }

        string englishPath = Path.Combine(LocalizationDirectoryPath, "English.yml");
        if (!File.Exists(englishPath))
        {
            File.WriteAllText(
                englishPath,
                BuildTemplate("$bestiary_workshop_boar: Workshop boar"),
                Utf8WithoutBom);
        }

        string koreanPath = Path.Combine(LocalizationDirectoryPath, "Korean.yml");
        if (!File.Exists(koreanPath))
        {
            File.WriteAllText(
                koreanPath,
                BuildTemplate("$bestiary_workshop_boar: \uC791\uC5C5\uC7A5 \uBA67\uB3FC\uC9C0"),
                Utf8WithoutBom);
        }

        TemplatesEnsuredThisSession = true;
    }

    private static string BuildTemplate(string translations)
    {
        return
            "# Bestiary workshop server-authoritative localization. The file name must be a Valheim language name.\n" +
            "# English is the fallback; the selected client language overrides matching English tokens.\n" +
            "# Keys may have one leading '$'; reference them as $token in creatures.yml or other localized fields.\n" +
            "# Remote clients ignore their local files while connected. Redefining an existing token changes every use of it.\n" +
            "# Localization is a global live rule; synchronized edits refresh the current language and UI immediately.\n" +
            "# This token is used by the inactive creatures.sample.yml example.\n" +
            translations + "\n";
    }

    private static bool ApplyTranslation(Localization localization, string token, string text)
    {
        bool currentExists = localization.m_translations.TryGetValue(token, out string? current);
        if (!OriginalTranslations.ContainsKey(token) ||
            LastAppliedTranslations.TryGetValue(token, out string? lastApplied) &&
            (!currentExists || !string.Equals(current, lastApplied, StringComparison.Ordinal)))
        {
            OriginalTranslations[token] = new OriginalTranslation(currentExists, current);
        }

        bool changed = !currentExists || !string.Equals(current, text, StringComparison.Ordinal);
        localization.m_translations[token] = text;
        LastAppliedTranslations[token] = text;
        return changed;
    }

    private static bool RestoreRemovedTranslations(
        Localization localization,
        IEnumerable<string> currentTokens)
    {
        HashSet<string> current = new(currentTokens, StringComparer.Ordinal);
        bool changed = false;
        foreach (string token in LastAppliedTranslations.Keys.Where(token => !current.Contains(token)).ToArray())
        {
            changed |= RestoreTranslationIfOwned(localization, token);
            LastAppliedTranslations.Remove(token);
            OriginalTranslations.Remove(token);
        }

        return changed;
    }

    private static void RestoreAppliedTranslations()
    {
        RestoreAppliedTranslations(AppliedLocalization);
    }

    private static void RestoreAppliedTranslations(Localization? localization)
    {
        if (localization == null)
        {
            ClearAppliedTranslationState();
            return;
        }

        bool changed = false;
        foreach (string token in LastAppliedTranslations.Keys.ToArray())
        {
            changed |= RestoreTranslationIfOwned(localization, token);
        }

        if (changed)
        {
            localization.m_cache.EvictAll();
        }

        ClearAppliedTranslationState();
    }

    private static bool RestoreTranslationIfOwned(Localization localization, string token)
    {
        if (!LastAppliedTranslations.TryGetValue(token, out string? lastApplied) ||
            !localization.m_translations.TryGetValue(token, out string? current) ||
            !string.Equals(current, lastApplied, StringComparison.Ordinal))
        {
            return false;
        }

        if (OriginalTranslations.TryGetValue(token, out OriginalTranslation original) && original.Existed)
        {
            localization.m_translations[token] = original.Value ?? "";
        }
        else
        {
            localization.m_translations.Remove(token);
        }

        return true;
    }

    private static void ClearAppliedTranslationState()
    {
        AppliedLocalization = null;
        AppliedLanguage = "";
        OriginalTranslations.Clear();
        LastAppliedTranslations.Clear();
    }

    private static void ClearActivePayloadAndRestore()
    {
        lock (Sync)
        {
            ActivePayload = CreateEmptyPayload();
        }

        RestoreAppliedTranslations();
    }

    private static void NotifyLocalizationChanged()
    {
        if (Localization.m_instance == null || Localization.OnLanguageChange == null)
        {
            return;
        }

        foreach (Delegate subscriber in Localization.OnLanguageChange.GetInvocationList())
        {
            try
            {
                ((Action)subscriber).Invoke();
            }
            catch (Exception ex)
            {
                WorkshopHost.Log.LogWarning(
                    $"A localized UI subscriber failed after a server localization update: {ex.Message}");
            }
        }
    }

    private static ServerLocalizationPayload CreateEmptyPayload()
    {
        return new ServerLocalizationPayload
        {
            Version = CreatureYaml.ServerLocalizationPayloadVersion,
            Languages = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        };
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
internal static class CreatureManagerServerLocalizationShutdownPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix()
    {
        try
        {
            CreatureServerLocalization.OnWorldShutdown();
        }
        catch (Exception ex)
        {
            WorkshopHost.Log.LogWarning($"Failed to clear server localization during world shutdown: {ex.Message}");
        }
    }
}
