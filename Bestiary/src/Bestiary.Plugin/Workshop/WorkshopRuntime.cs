// Portions Copyright (C) sighsorry1029; Localizer 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: C01 lifecycle and server-language hooks.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtraPotions.Bestiary.Runtime.Workshop.Adopted;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Workshop;

internal static class WorkshopRuntime
{
    private static Harmony? harmony;
    private static WorkshopSyncContext? context;
    private static ConfigFile? file;
    private static ManualLogSource? logger;
    private static bool initializationFailed;
    private static string invalidDirectory = "";
    private static bool initialized, settingsPending, baselineSuspended, mechanicsActive;
    private static string directory = "";
    internal static int Failures { get; private set; }
    internal static int PatchClasses { get; private set; }
    internal static bool Initialized => initialized;
    internal static string LastError { get; private set; } = "";

    internal static void Start(ConfigFile config, ManualLogSource logger)
    {
        if (Integration.BestiaryIntegration.ProductYields) return;
        WorkshopRuntime.logger = logger;
        WorkshopHost.Log = new WorkshopLog(logger);
        logger.LogEvent += Log;
        file = config; context = new WorkshopSyncContext(config);
        file.SettingChanged += Changed;
        harmony = new Harmony(WorkshopHost.ModGUID);
        foreach (var type in typeof(WorkshopRuntime).Assembly.GetTypes().Where(t => t.Namespace?.StartsWith("ExtraPotions.Bestiary.Runtime.Workshop", StringComparison.Ordinal) == true))
            if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                try { harmony.CreateClassProcessor(type).Patch(); PatchClasses++; }
                catch (Exception error) { Failed("patch " + type.Name, error); }
        Tick();
    }

    private static void Log(object sender, LogEventArgs args)
    {
        if ((args.Level & (LogLevel.Error | LogLevel.Warning)) != 0) LastError = args.Data?.ToString() ?? "";
    }
    private static void Changed(object sender, SettingChangedEventArgs args)
    {
        if (args.ChangedSetting.Definition.Section != "40 - Server Data") { settingsPending = true; initializationFailed = false; }
    }
    // Native settings update their original prefabs between these two calls. A previous workshop baseline must not
    // restore stale settings afterward; the pinned apply pass captures the refreshed native values on reapplication.
    internal static void BeforeNativeTick()
    {
        if (!settingsPending || !initialized) return;
        Run("restore before settings refresh", CreatureDomainManager.SuspendForNativeSettings);
        baselineSuspended = true;
    }
    internal static void Tick()
    {
        if (context == null) return;
        bool active = WorkshopHost.CanLoadIdentities;
        string nextDirectory;
        try { nextDirectory = active ? WorkshopHost.DataDirectory : ""; invalidDirectory = ""; }
        catch (Exception error)
        {
            if (invalidDirectory != error.Message) { invalidDirectory = error.Message; Failed("data folder", error); }
            if (initialized) StopModules(); return;
        }
        if (initialized && (!active || nextDirectory != directory)) StopModules();
        if (!initialized && active && !initializationFailed)
        {
            initialized = true; directory = nextDirectory;
            bool installed = Run("initialize", () =>
            {
                CreatureServerLocalization.Initialize(context);
                CreatureDomainManager.Initialize(context);
                CreatureTextureSync.RegisterCurrentPeers();
                WorkshopDataFiles.Start(context);
                CreatureDomainManager.NotifyGameDataAvailable();
            });
            if (!installed) { StopModules(); initializationFailed = true; }
        }
        context.Tick();
        if (initialized && mechanicsActive != WorkshopHost.Active)
        {
            mechanicsActive = WorkshopHost.Active;
            settingsPending = true;
            if (!mechanicsActive)
            {
                Run("pause mechanics", CreatureDomainManager.SuspendForNativeSettings);
                Run("pause appearance", CreatureAppearanceRuntime.Reset);
                Run("pause factions", CreatureFactionManager.Reset);
                WorkshopCommands.Stop();
            }
            else WorkshopCommands.Register();
        }
        if (!initialized) { settingsPending = false; return; }
        if (settingsPending)
        {
            settingsPending = false;
            Run("refresh settings", () =>
            {
                CreatureDomainManager.RefreshWatchers(); CreatureServerLocalization.RefreshWatchers();
                if (context.IsSourceOfTruth)
                {
                    CreatureDomainManager.RequestConfigurationReload(); if (WorkshopHost.Active) CreatureServerLocalization.ReloadFromDiskAndSync();
                }
                if (baselineSuspended) CreatureDomainManager.NotifyGameDataAvailable();
                baselineSuspended = false;
                if (WorkshopHost.TextureSync) CreatureTextureSync.RegisterCurrentPeers();
                else CreatureTextureSync.UnregisterOwnedPeerRpcs();
            });
        }
        Run("update", () => { CreatureServerLocalization.Update(); CreatureDomainManager.Update(); WorkshopDataFiles.Tick(); });
    }
    private static void StopModules()
    {
        initialized = false;
        Run("data-file teardown", WorkshopDataFiles.Stop);
        Run("command teardown", WorkshopCommands.Stop);
        Run("texture RPC teardown", CreatureTextureSync.UnregisterOwnedPeerRpcs);
        Run("localization teardown", CreatureServerLocalization.Dispose);
        Run("domain teardown", CreatureDomainManager.Dispose);
        Run("appearance teardown", CreatureAppearanceRuntime.Reset);
        Run("faction teardown", CreatureFactionManager.Reset);
        Run("carrier teardown", () => context?.ClearWrappers());
        directory = ""; baselineSuspended = false; mechanicsActive = false;
    }
    internal static bool Run(string operation, Action action)
    {
        try { action(); return true; } catch (Exception error) { Failed(operation, error); return false; }
    }
    private static void Failed(string operation, Exception error)
    {
        Failures++; LastError = operation + ": " + error.Message;
        if (Native.Adapters.Guard.Admit("workshop|" + operation + "|" + error.GetType().FullName, out int suppressed))
            WorkshopHost.Log.LogError("Bestiary workshop " + LastError + (suppressed > 0 ? " (" + suppressed + " more in the last minute)" : ""));
    }
    internal static void Shutdown()
    {
        if (context == null) return;
        StopModules(); harmony?.UnpatchSelf(); harmony = null;
        if (file != null) file.SettingChanged -= Changed;
        if (logger != null) logger.LogEvent -= Log; logger = null;
        file = null; context.Dispose(); context = null; settingsPending = false;
    }
}

public static class BestiaryWorkshopStatus
{
    public static bool Active => WorkshopRuntime.Initialized && WorkshopHost.Active;
    public static int NativeFailures => WorkshopRuntime.Failures;
    public static int PatchClasses => WorkshopRuntime.PatchClasses;
    public static string LastError => WorkshopRuntime.LastError;
}

// Workshop hooks adapted from CreatureManager Localizer at 47e45a6e. Portions Copyright (C) sighsorry1029.
// The embedded/product C05 catalog is owned separately. SPDX-License-Identifier: GPL-3.0-only
[HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage), new[] { typeof(string) })]
internal static class WorkshopLocalizationPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(Localization __instance)
    {
        if (WorkshopHost.Active) WorkshopRuntime.Run("prepare language", () => CreatureServerLocalization.BeforeLanguageSetup(__instance));
    }
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(Localization __instance, string language)
    {
        if (WorkshopHost.Active) WorkshopRuntime.Run("apply language", () => CreatureServerLocalization.ApplyCurrentLocalization(__instance, language));
    }
}
