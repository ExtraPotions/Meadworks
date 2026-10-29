// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.Runtime.Merge;
using ExtraPotions.Magpie.Runtime.Loot;
using ExtraPotions.Magpie.Runtime.Pickup;
using ExtraPotions.Magpie.Runtime.Settings;
using ExtraPotions.Magpie.StepAside;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Magpie.Runtime
{
    /// <summary>
    /// Magpie (Hoard): fast smooth pickup, ground-stack merging, floating drops and a server-controlled loot multiplier.
    /// Required on all peers (roster peer classes): merging writes shared item ZDOs, loot marks are written by the
    /// spawning peer and floating physics runs on the item's owner; major.minor must match.
    /// Modules: Pickup (<see cref="PickupEngine"/>), Merge (<see cref="MergeEngine"/>), Float (adopted FloatingItems) and
    /// Loot (marks and the pickup multiplier). A headless process (dedicated server, C10) never builds pickup messages or
    /// effects, and still merges, floats and marks the items it owns or spawns.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    // The Crucible.UI kit is the standard Meadworks UI: a mandatory dependency (shipped in the Crucible package, idle on a
    // headless server).
    [BepInDependency(ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Guid, ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = MagpieCore.ProductId;
        public const string Name = "Magpie";
        public const string Version = "0.1.18";
        public const string Family = "Hoard";

        private ProductRegistration? registration;
        private MeadworksLog? log;
        private Harmony? harmony;
        private MagpieConfig? settings;
        private MagpieImport? imports;
        private string? padText;
        private PadButton[] padChord = Array.Empty<PadButton>();
        private Player? startPlayer;
        private ZNet? startNetwork;
        private long startGeneration = -1, startWorld;
        private string? startSession;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may show pickup messages and effects (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

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
                MagpieText.Initialize(Info.Metadata, Logger);
                settings = new MagpieConfig(Config, log);
                imports = new MagpieImport(Config, settings);
                MagpieRuntime.Initialize(settings, log);
                MagpiePickup.Register(registration);
                registration.AddCapability<Func<object,int,int>>("meadworks.loot.multiplier",new System.Version(1,0),LootPickup.MultiplyExternalCreatureLoot);
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Ready = true;
                log.Info(AllowsUi ? "Magpie registered." : "Magpie registered on a headless process; no messages or effects are created.");
            }
            catch (Exception error)
            {
                Shutdown();
                log.Error("Magpie initialization rejected: " + error.GetType().Name + ": " + error.Message);
            }
        }

        private void Start()
        {
            if (!Ready) return;
            try
            {
                MagpieRuntime.DecideStepAside();
            }
            catch (Exception error)
            {
                log?.Error("Magpie step-aside decision failed: " + error);
            }
        }

        private void Update()
        {
            if (!Ready || settings == null) return;
            MagpieText.Tick();
            settings.Tick(UnityEngine.Time.unscaledTime);
            if (AllowsUi)
            {
                InitializePickupState();
                Toggle();
                PickupEngine.FlushMessages();
            }
            MergeEngine.Tick();
            Runtime.Adopted.FloatingItems.FloatLive.Tick();
        }

        /// <summary>A8: optional keyboard or pad input flips the game's own auto pickup flag once.</summary>
        private void Toggle()
        {
            var player = global::Player.m_localPlayer;
            var network = ZNet.instance;
            long generation = CruciblePlugin.Session.Generation, world = CruciblePlugin.Session.WorldId;
            string session = CruciblePlugin.Session.SessionId;
            if (!MagpieRuntime.ModuleOn(MagpieModule.Pickup) || !ToggleOwner(player, network, generation, world, session) ||
                !MeadworksInput.Allows(InputContext.Gameplay) || !ToggleOwner(player, network, generation, world, session)) return;
            string text = settings!.Get<string>(MagpieSettings.PickupTogglePadChord);
            if (text != padText) { padText = text; padChord = MagpiePadChord.Parse(text); }
            // Evaluate both routes so a simultaneous pad chord is consumed, then change the native flag once.
            bool key = MeadworksInput.Pressed(settings.ToggleKey);
            if (!ToggleOwner(player, network, generation, world, session)) return;
            bool padAllowed = MeadworksInput.Allows(InputContext.Gameplay);
            if (!padAllowed || !ToggleOwner(player, network, generation, world, session)) return;
            bool pad = MagpiePadChord.Pressed(padChord, MeadworksInput.GamepadActive, padAllowed,
                MeadworksInput.PadHeld, MeadworksInput.PadDown, MeadworksInput.PadConsume);
            if ((!key && !pad) || !ToggleOwner(player, network, generation, world, session)) return;
            global::Player.m_enableAutoPickup = !global::Player.m_enableAutoPickup;
            player.Message(MessageHud.MessageType.TopLeft, "$hud_autopickup:" + (global::Player.m_enableAutoPickup ? "$hud_on" : "$hud_off"));
        }

        private void InitializePickupState()
        {
            var player = Player.m_localPlayer; var network = ZNet.instance;
            long generation = CruciblePlugin.Session.Generation, world = CruciblePlugin.Session.WorldId;
            string session = CruciblePlugin.Session.SessionId;
            if (!ToggleOwner(player, network, generation, world, session, readyForInput: false)) { ClearStartOwner(); return; }
            if (ReferenceEquals(startPlayer, player) && ReferenceEquals(startNetwork, network) && startGeneration == generation &&
                startWorld == world && startSession == session) return;
            if (!MagpieRuntime.ModuleOn(MagpieModule.Pickup) || settings == null || !settings.StartReceiptReady ||
                !ToggleOwner(player, network, generation, world, session)) return;
            bool enabled = settings.Get<bool>(MagpieSettings.AutoPickupAtStart);
            if (!ToggleOwner(player, network, generation, world, session)) return;
            Player.m_enableAutoPickup = enabled;
            startPlayer = player; startNetwork = network; startGeneration = generation; startWorld = world; startSession = session;
        }
        private void ClearStartOwner()
        {
            startPlayer = null; startNetwork = null; startGeneration = -1; startWorld = 0; startSession = null;
        }
        internal static bool ToggleOwner(Player player, ZNet network, long generation, long world, string session, bool readyForInput = true) =>
            player != null && ReferenceEquals(player, Player.m_localPlayer) && network != null && ReferenceEquals(network, ZNet.instance) &&
            CruciblePlugin.IsReady && CruciblePlugin.Session.AuthorityReady && ZNet.m_world != null &&
            world != 0 && network.GetWorldUID() == world && CruciblePlugin.Session.WorldId == world &&
            CruciblePlugin.Session.Generation == generation && CruciblePlugin.Session.SessionId == session &&
            player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner() && (!readyForInput || !player.IsDead() && !player.IsTeleporting());

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            imports?.Dispose(); imports = null;
            padText = null; padChord = Array.Empty<PadButton>();
            ClearStartOwner();
            settings?.Shutdown(); settings = null;
            harmony?.UnpatchSelf();
            harmony = null;
            MagpiePickup.Reset();
            PickupEngine.Reset();
            MergeEngine.Reset();
            MagpieRuntime.Shutdown();
            MagpieText.Shutdown();
            registration?.Dispose();
            registration = null;
        }
    }
}
