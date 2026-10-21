// Delve - ExtraPotions Meadworks dungeons and world regeneration: room counts, dungeon resets, chest refills,
// dungeon resources, world objects, boss return and spawner respawn.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Delve.Runtime
{
    /// <summary>
    /// Delve (Runestone). Server only (design sections 1 and 6): it acts on the authority (single player, host,
    /// dedicated server) and idles on a remote client; clients need nothing, so Jotunn does not enforce it. Crucible and
    /// the Crucible.UI kit are hard dependencies; Delve itself builds no UI (design 11).
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = DelveCore.PluginGuid;
        public const string Name = DelveCore.ProductName;
        public const string Version = DelveCore.ProductVersion;
        public const string Family = DelveCore.Family;

        private ProductRegistration? registration;
        private DelveImport? import;
        private Harmony? harmony;
        private bool started;

        /// <summary>The regeneration engine; null until Start succeeded.</summary>
        public static DelveEngine? Engine { get; private set; }

        /// <summary>Registered with Crucible.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Delve's settings, read at use time; null until Awake succeeded.</summary>
        public static DelveConfig? Settings { get; private set; }

        /// <summary>Delve's log (C06 quiet by default).</summary>
        public static MeadworksLog? Log { get; private set; }

        private void Awake()
        {
            Log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.ServerOnly, Family));
                Settings = DelveConfig.Bind(Config, Log);
                DelveAdmin.Publish(registration); // meadworks.delve.admin; unhandled until the engine runs
                DelveText.Initialize(Info.Metadata, Logger);
                if (!DelveSites.KeyMatchesCrucible) Log.Warning("Delve site identity key differs from Crucible's C12 rule; site state may not persist.");
                Ready = true;
                Log.Info(MeadworksHeadless.IsHeadless ? "Delve registered on a headless server." : "Delve registered (server only; acts on the authority).");
            }
            catch (Exception error)
            {
                Log.Error("Delve initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || Log == null) return;
            try
            {
                DelveModules.Decide(Log);
                import = new DelveImport(Config, Settings!, Log);
                Engine = DelveServices.Create(Log);
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                started = true;
            }
            catch (Exception error)
            {
                Log.Error("Delve modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            DelveText.Tick();
            if (!started || Log == null) return;
            DelveModules.Tick(Log);
            DelveRooms.ReportUnknown();
            Engine?.Update();
            DelveSurface.Tick();
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            try { if (Engine?.Running == true) Engine.Stop(); } catch (Exception error) { Log?.Warning("Delve engine stop: " + error.GetType().Name); }
            Engine = null;
            try { harmony?.UnpatchSelf(); } catch (Exception error) { Log?.Warning("Delve unpatch: " + error.GetType().Name); }
            harmony = null;
            foreach (Action stop in new Action[] { () => import?.Dispose(), DelveModules.Reset, DelveRooms.Reset, DelveSurface.Reset, DelveText.Shutdown })
                try { stop(); } catch (Exception error) { Log?.Warning("Delve teardown: " + error.GetType().Name); }
            import = null;
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
