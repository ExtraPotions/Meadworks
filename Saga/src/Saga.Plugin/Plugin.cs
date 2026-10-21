// Saga - ExtraPotions Meadworks progression: boss powers, Defense, Sneak, gathering skills and the skill book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Saga.Runtime
{
    /// <summary>
    /// Saga (Fang). Required on all peers (design section 6): woodcutting, mining, foraging and fishing perks run on
    /// whichever peer owns the tree, rock, pickable or fish spawner, kill credit and experience credit are Saga
    /// messages, and every peer must use the server's rules; major.minor must match. On a vanilla server or a Crucible
    /// server without Saga the product is off for the session; skill levels registered through CS1 are kept by
    /// Crucible. A headless process (dedicated server, C10) never builds UI.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = SagaCore.PluginGuid;
        public const string Name = SagaCore.ProductName;
        public const string Version = SagaCore.ProductVersion;
        public const string Family = SagaCore.Family;

        private ProductRegistration? registration;
        private MeadworksLog? log;
        private SagaPresetRuntime? presets;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Saga UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Saga's settings, read at use time; null until Awake succeeded.</summary>
        public static SagaConfig? Settings { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                Settings = SagaConfig.Bind(Config, log);
                presets = new SagaPresetRuntime(Config, Settings, log);
                Adopted.Grindstone.NativeSaga.Initialize(Logger);
                SagaConverter.Initialize(Config);
                PowerCommands.Initialize(registration);
                BackstabCredit.Initialize(registration);
                registration.AddCapability<System.Collections.Generic.IReadOnlyDictionary<string,Delegate>>("meadworks.saga.progression",new System.Version(1,0),NativeBook.Capability());
                registration.AddCapability<System.Collections.Generic.IReadOnlyDictionary<string,Delegate>>("meadworks.saga.powers",new System.Version(1,0),NativePowers.Capability());
                registration.AddCapability<Func<string,string,bool>>("meadworks.saga.register_boss",new System.Version(1,0),NativePowers.RegisterBoss);
                registration.AddCapability<Func<System.Collections.Generic.IReadOnlyList<string>>>("meadworks.diagnostics."+Guid,new System.Version(1,0),Adopted.Grindstone.NativeSaga.Describe);
                Ready = true;
                log.Info(AllowsUi ? "Saga registered." : "Saga registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                log.Error("Saga initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void OnDestroy() => Shutdown();
        private void Start(){if(Ready)try{Adopted.Grindstone.NativeSaga.Start();}catch(Exception error){log?.Error("Saga modules rejected: "+error);Shutdown();}}
        private void Update()
        {
            if (!Ready) return;
            presets?.Tick();
            Adopted.Grindstone.NativeSaga.Tick();
        }

        private void Shutdown()
        {
            Ready = false;
            presets?.Dispose();
            presets = null;
            Adopted.Grindstone.NativeSaga.Shutdown();
            SagaConverter.Shutdown();
            PowerCommands.Shutdown();
            BackstabCredit.Shutdown();
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
