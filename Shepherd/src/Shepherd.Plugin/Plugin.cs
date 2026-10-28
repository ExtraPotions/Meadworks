// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Runtime.Integration;
using ExtraPotions.Shepherd.Runtime.Skills;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Feeding;
using ExtraPotions.Shepherd.Runtime.Breeding;
using ExtraPotions.Shepherd.Runtime.Companions;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime
{
    /// <summary>
    /// Shepherd (Hearth): feed troughs, the Husbandry skill and breeding with sires, inheritance, twins, herd limits
    /// and lineage. Required on all peers (design section 5): troughs are pieces and feeding, breeding and inheritance
    /// run on whichever machine owns an animal; major.minor must match.
    /// Registers with Crucible as
    /// <see cref="MultiplayerPolicy.RequiredOnAllPeers"/>, provides the Burrow container-role capability and registers
    /// the Husbandry skill through CS1. Adopted keeper/taming/calm/petting hooks run on native ownership paths; troughs and owner-routed feeding (S04, S05); breeding pace, sire, inheritance, claims and carries (S07). The herd book, trough header and animal lore use the shared kit; a headless
    /// process (dedicated server, C10) never builds UI.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Guid, ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = ShepherdCore.ProductId;
        public const string Name = ShepherdCore.Name;
        public const string Version = ShepherdCore.Version;
        public const string Family = ShepherdCore.Family;

        private ProductRegistration? registration;
        private MeadworksSkill? husbandry;
        private MeadworksLog? log;
        private ShepherdImport? sourceImport;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Shepherd UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>The Husbandry skill (CS1); null until Awake succeeded.</summary>
        public static MeadworksSkill? Husbandry { get; private set; }

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
                ContainerRoleCapability.Register(registration);
                BreedingActivityCapability.Register(registration);
                LivestockPens.Register(registration);
                ShepherdLocalization.Initialize(Info.Metadata, Logger);
                husbandry = ExtraPotions.Shepherd.Runtime.Skills.HusbandrySkill.Register();
                Husbandry = husbandry;
                NativeHusbandry.Initialize(Config, log);
                ShepherdSeasons.Initialize(Config);
                ShepherdSources.Initialize();
                sourceImport = new ShepherdImport(Config, log);
                NativeFeeding.Initialize(log);
                ContainerFeedMark.Initialize(registration);
                NativeBreeding.Initialize(log);
                NativeCheats.Initialize();
                BreedingCommands.Initialize(registration);
                FollowCommand.Initialize();
                Pens.LandPenPieces.Initialize(() => ShepherdSettings.LandPenCapacity.Value, () => ShepherdSettings.LandPenRecipe.Value);
                UI.TroughHeader.Initialize();
                Ready = true;
                NativeHusbandry.Tick();
                ShepherdSources.Tick();
                NativeFeeding.Tick();
                ContainerFeedMark.Tick();
                NativeBreeding.Tick();
                NativeCheats.Tick();
                BreedingCommands.Tick();
                FollowCommand.Tick();
                log.Info("Shepherd registered; Husbandry taming and petting, troughs, owner-routed feeding and breeding core available.");
            }
            catch (Exception error)
            {
                Shutdown();
                log.Error("Shepherd initialization rejected: " + error.GetType().Name + ": " + error.Message);
            }
        }

        private void OnDestroy() => Shutdown();
        private void Update()
        {
            if (!Ready) return;
            NativeHusbandry.Run("season facts", ShepherdSeasons.Tick);
            NativeHusbandry.Run("Husbandry lifecycle", NativeHusbandry.Tick);
            NativeHusbandry.Run("source compatibility", ShepherdSources.Tick);
            NativeFeeding.Run("Feeding lifecycle", NativeFeeding.Tick);
            NativeFeeding.Run("container feed marking", ContainerFeedMark.Tick);
            NativeBreeding.Run("Breeding lifecycle", NativeBreeding.Tick);
            NativeBreeding.Run("cheat lifecycle", NativeCheats.Tick);
            NativeBreeding.Run("breeding commands", BreedingCommands.Tick);
            NativeHusbandry.Run("Follow Command lifecycle", FollowCommand.Tick);
            NativeHusbandry.Run("herd watch", HerdWatch.Tick);
            NativeHusbandry.Run("localization", ShepherdLocalization.Tick);
            NativeHusbandry.Run("herd book", UI.HerdBook.Tick);
            NativeHusbandry.Run("trough header", UI.TroughHeader.Tick);
            NativeHusbandry.Run("land pen", Pens.LandPenPieces.Tick);
        }

        private void Shutdown()
        {
            Ready = false;
            ShepherdSeasons.Shutdown();
            UI.HerdBook.Shutdown();
            UI.TroughHeader.Shutdown();
            Pens.LandPenPieces.Shutdown();
            FollowCommand.Shutdown();
            BreedingCommands.Shutdown();
            NativeCheats.Shutdown();
            NativeBreeding.Shutdown();
            LivestockPens.Clear();
            ContainerFeedMark.Shutdown();
            NativeFeeding.Shutdown();
            sourceImport?.Dispose();
            sourceImport = null;
            ShepherdSources.Shutdown();
            NativeHusbandry.Shutdown();
            HerdWatch.Clear();
            ShepherdLocalization.Shutdown();
            Husbandry = null;
            husbandry?.Dispose();
            husbandry = null;
            ExtraPotions.Shepherd.Runtime.Skills.HusbandrySkill.ShutdownIcon();
            registration?.Dispose();
            registration = null;
        }
    }
}
