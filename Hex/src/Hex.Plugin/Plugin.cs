// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Jotunn.Utils;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;
namespace ExtraPotions.Hex.Runtime;

[BepInPlugin(Guid, "Hex", HexCore.ProductVersion)]
[BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
[BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
[BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
// VersionCheckOnly: Jotunn compares versions only when both sides run Hex, so players without Hex are not refused.
[NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Minor)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = HexCore.PluginGuid;
    public static bool Ready { get; private set; }
    public static bool Active { get; private set; }
    public static HexConfig? Settings { get; private set; }
    private ProductRegistration? registration;
    internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
    private Harmony? harmony;
    private HexImport? importer;
    private void Awake()
    {
        try
        {
            if (!CruciblePlugin.IsReady) throw new InvalidOperationException("Required Crucible is not ready");
            registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, HexCore.ProductName, new System.Version(HexCore.ProductVersion),
                new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.Optional, HexCore.Family));
            // Client-optional: the menu, Ghost and spawning run only for the Hex user and the server checks every request, so players
            // without Hex (Meadworks or vanilla under AllowVanilla) join; Ghost is partial against creatures they run, remote kill/drops
            // skip their targets, they see normal size. On a server without Hex every Hex cheat stays locked.
            HexText.Initialize(Info.Metadata, Logger);
            Settings = new HexConfig(Config);
            Ready = true;
            Native.HexPresets.Initialize(Logger);
            importer = new HexImport(Config);
        }
        catch (Exception error) { Logger.LogError("Hex initialization refused: " + error); Shutdown(); }
    }
    private void Start()
    {
        if (!Ready) return;
        try
        {
            Active = !Chainloader.PluginInfos.ContainsKey("com.icecub.valheimadmintool");
            if (!Active) { Logger.LogWarning("Hex steps aside while Valheim_Admin_Tool is installed."); return; }
            harmony = new Harmony(Guid); harmony.PatchAll(typeof(Plugin).Assembly);
            EdgeWear.Register(registration!);
            Native.HexGhost.Initialize(registration!, Logger);
            Native.HexSpawnAuthority.Initialize(registration!, Logger);
            Native.HexActions.Initialize();
            Native.HexInformation.Initialize();
            Native.HexDebug.Initialize();
            Native.HexRemoteDebug.Initialize(registration!, Logger);
            Native.HexMap.Initialize();
            Native.HexPoi.Initialize();
            Native.HexSkills.Initialize();
            Native.HexAchievements.Initialize();
            Native.HexParty.Initialize(Logger);
            Native.HexSize.Initialize();
            Logger.LogInfo("Hex source candidate loaded; menu and native spawning implemented; runtime qualification remains pending.");
        }
        catch (Exception error) { Logger.LogError("Hex native hooks refused: " + error); Shutdown(); }
    }
    private void Update() { if (Ready) { HexText.Tick(); Settings?.Tick(); HexPauseMenu.Tick(); Native.HexDebug.Tick(); Native.HexGrantHint.Tick(); Native.HexRemoteDebug.Tick(); Native.HexEsp.Tick(); if (Active) { HexWindow.Tick(); Native.HexPresets.Tick(); Native.HexPoi.Tick(); Native.HexSpawn.Tick(); Native.HexModes.Tick(); Native.HexGhost.Tick(); Native.HexDeath.Tick(); Native.HexMarks.Tick(); Native.HexBounce.Tick(); Native.HexParty.Tick(); Native.HexSize.Tick(); } } }
    private void LateUpdate() { if (Ready) Native.HexSpawnProtection.Tick(); }
    private void FixedUpdate() { if (Ready && Active) Native.HexMovement.ApplyGravity(); }
    private void OnDestroy() => Shutdown();
    private void Shutdown()
    {
        Ready = Active = false;
        importer?.Dispose(); importer = null;
        HexPauseMenu.Reset();
        HexWindow.Shutdown();
        Native.HexDebug.Reset();
        Native.HexRemoteDebug.Shutdown();
        Native.HexEsp.Reset();
        Native.HexSpawnProtection.Reset();
        Native.HexModes.Restore();
        Native.HexDeath.Reset();
        Native.HexActions.Shutdown();
        Native.HexPresets.Shutdown();
        Native.HexPoi.Reset();
        Native.HexSpawn.Reset();
        Native.HexSpawnAuthority.Shutdown();
        Native.HexAchievements.Shutdown();
        Native.HexMarks.Reset();
        Native.HexLocalTime.Reset();
        Native.HexBounce.Reset();
        Native.HexParty.Shutdown();
        Native.HexSize.Shutdown();
        Native.HexGhost.Shutdown();
        harmony?.UnpatchSelf(); harmony = null;
        Settings = null;
        HexText.Shutdown();
        registration?.Dispose(); registration = null;
    }
}
