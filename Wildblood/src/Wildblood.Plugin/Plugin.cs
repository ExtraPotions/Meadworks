// Wildblood - ExtraPotions Meadworks creature difficulty, stars, traits, boss aspects, loot rules and bad-luck protection.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>
    /// Wildblood (Fang). Required on all peers (design section 6): traits change how creatures behave on whichever machine
    /// owns them, loot runs on the dying creature's owner and every peer draws the same names and stars; major.minor must
    /// match. On a vanilla server or a Crucible server without Wildblood the product is off for the session (Luck keeps
    /// running client-side, user decision Q6). Each module steps aside for its own source mod (section 11). A headless
    /// process (dedicated server, C10) never builds UI.
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
        public const string Guid = WildbloodCore.PluginGuid;
        public const string Name = WildbloodCore.ProductName;
        public const string Version = WildbloodCore.ProductVersion;
        public const string Family = WildbloodCore.Family;

        private ProductRegistration? registration;
        private MeadworksLog? log;
        private Harmony? harmony;
        private Harmony? theftHarmony;
        private Harmony? luckHarmony;
        private IDisposable? luckCapability;
        private Adopted.Rules.NativeRuleAuthority? nativeRules;
        private NativeCreatureRules? creatureRules;
        private Adopted.Config.NativeConfiguration? nativeConfiguration;
        private IDisposable? modifierRules;
        private IDisposable? karmaRules;
        private bool started;
        private static Plugin? instance;
        internal static ProductRegistration? Source => instance?.registration;
        // Built once: lambdas that capture this would allocate a new delegate every frame in Update.
        private Action? recapTick, scalingTick;

        /// <summary>
        /// Registered and its modules started (not shut down): the live answer of <c>meadworks.owns.elite_creatures</c>,
        /// registered in Awake (coordinator ruling 2026-10-13). Readers that only asked whether Wildblood is installed
        /// (Inscribe, Bestiary) use the capability's presence; Burrow refuses new transfers while it reads false.
        /// </summary>
        internal static bool Running => Ready && instance != null && instance.started;
        internal static bool NativeRulesReady => instance?.nativeRules?.IsReady == true;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Wildblood UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Wildblood's settings, read at use time; null until Awake succeeded.</summary>
        public static WildbloodConfig? Settings { get; private set; }
        internal static BepInEx.Logging.ManualLogSource? NativeLogger { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            NativeLogger = Logger;
            instance = this;
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, Family));
                luckCapability = LuckDeathAdmission.RegisterCapability(registration);
                registration.AddCapability<Func<bool>>(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.Id(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.EliteCreatures),
                    ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.ContractVersion, () => Running);
                AllowsUi = MeadworksHeadless.AllowsUi;
                Settings = WildbloodConfig.Bind(Config, log);
                creatureRules = new NativeCreatureRules(Config, registration, () => nativeRules?.Invalidate());
                WildbloodSeasons.Initialize(Config);
                WildbloodText.Initialize(Info.Metadata, Logger);
                NativeRaidChestPrefab.Initialize();
                Ready = true;
                log.Info(AllowsUi ? "Wildblood registered; modules start after plugin discovery."
                    : "Wildblood registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                log.Error("Wildblood initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || log == null) return;
            try
            {
                WildbloodModules.Decide(log);
                Adopted.Util.Log.Bind(Logger);
                nativeConfiguration = new Adopted.Config.NativeConfiguration(Settings!);
                Adopted.Config.Configuration.BindAll(nativeConfiguration);
                nativeRules = new Adopted.Rules.NativeRuleAuthority(Config, log);
                modifierRules = AdoptedCm.NativeModifierRuleAuthority.Register(nativeRules);
                karmaRules = AdoptedCm.NativeKarmaRuleAuthority.Register(nativeRules);
                Adopted.Adapters.NativeTraitsCapability.Register(registration);
                Adopted.Adapters.OffspringStarsCapability.Register(registration);
                Adopted.Adapters.NativeLootCapability.Register(registration);
                Adopted.Transfers.TheftInventoryGuards.Initialize();
                registration.AddCapability<Func<object, bool>>("meadworks.wildblood.inventory_hold", new System.Version(1, 0), Adopted.Transfers.TheftInventoryGuards.Export);
                WildbloodStatus.Register(registration);
                Adopted.Runtime.BossProgress.Initialize(registration);
                harmony = new Harmony(Guid);
                theftHarmony = new Harmony(Guid + ".inventory");
                luckHarmony = new Harmony(Guid + ".luck-decision");
                Adopted.Runtime.NativeRpcOwnership.Resume();
                // Exact reviewed native groups use the existing authority/config adapters.
                // HookActivation also keeps the Delve-owned hooks (spawner respawn, dungeon loot) out for good.
                foreach (var type in typeof(Plugin).Assembly.GetTypes().Where(type => Integration.HookActivation.MayActivate(
                    type.Namespace, type.Name, type.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0)))
                {
                    if (type.Namespace == "ExtraPotions.Wildblood.Adopted.Raids" && NativeRaidAdmission.SourcePresent) continue;
                    bool inventoryOwner = Integration.HookActivation.NativeTheft.Contains(type.Namespace + "." + type.Name)
                        || type.DeclaringType == typeof(NativeRaidChest)
                        || type.DeclaringType == typeof(NativeRaidAdmission) && (type.Name == "RunningOwnership" || type.Name == "RaidSceneOwnership");
                    bool luckOwner = type == typeof(LuckDeathGate) || type == typeof(LuckDeathScope);
                    (luckOwner ? luckHarmony : inventoryOwner ? theftHarmony : harmony).CreateClassProcessor(type).Patch();
                }
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Wildblood modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            // Removals kept while Wildblood held an inventory; also runs while a stopped Wildblood still retains its guard.
            if (Adopted.Transfers.TheftInventoryGuards.DeferredRemovals != 0)
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood kept inventory removals", Adopted.Transfers.TheftInventoryGuards.DrainRemovals);
            if (!Ready) return;
            AdoptedLibs.PatchGuard.Guard.Run("Wildblood season facts", WildbloodSeasons.Tick);
            WildbloodText.Tick();
            creatureRules?.Tick();
            nativeRules?.Tick();
            if (started && log != null)
            {
                WildbloodModules.Tick(log);
                NativeEngineLifecycle.Tick();
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood luck decision settlement", LuckDeathAdmission.Tick);
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood raid chest availability", NativeRaidChestPrefab.Tick);
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood raid owner", NativeRaidAdmission.Tick);
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood theft recovery owner", () => TheftTransfers.Tick());
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood display owner", Adopted.Patches.EnemyHudPatch.Tick);
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood boss board owner", Adopted.Tally.BossBoardView.Tick);
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood status owner", WildbloodStatus.Tick);
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood recap owner", recapTick ??= () => WildbloodRecap.Tick(gameObject));
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood lore owner", WildbloodLore.Tick);
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood MeadWorks menu", WildbloodMenu.Tick);
                AdoptedLibs.PatchGuard.Guard.Run("Wildblood player scaling", scalingTick ??= () => PlayerScalingRuntime.Tick(log!));
            }
            else PlayerScalingRuntime.Restore();
        }

        private void LateUpdate()
        {
            if (!Ready) return;
            AdoptedLibs.PatchGuard.Guard.Run("Wildblood star restore", Adopted.Runtime.StarRestoreBatch.EndFrame);
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            try { Adopted.Runtime.StarRestoreBatch.FlushNow(); }
            catch (Exception error) { log?.Warning("Wildblood star restore: " + error.GetType().Name); }
            NativeRaidChestPrefab.Shutdown();
            try { AdoptedCm.NativePressurePersistence.Save(); }
            catch (Exception error) { log?.Warning("Wildblood pressure settlement: " + error.GetType().Name); }
            NativeEngineLifecycle.SavePersonal();
            bool releaseRaidGuards;
            try { releaseRaidGuards = NativeRaidAdmission.Shutdown(); }
            catch (Exception error) { releaseRaidGuards = false; log?.Warning("Wildblood raid settlement: " + error.GetType().Name); }
            bool releaseTheftGuards = TheftTransfers.Stop() & releaseRaidGuards;
            luckCapability?.Dispose();
            bool releaseLuckGuards = LuckDeathAdmission.Stop();
            PouchDropOwner.Reset();
            Ready = false;
            started = false;
            WildbloodSeasons.Shutdown();
            WildbloodMenu.Shutdown();
            WildbloodRecap.Reset();
            Adopted.Patches.EnemyHudPatch.Reset();
            Adopted.Tally.BossBoardView.Shutdown();
            foreach (Action stop in new Action[] { WildbloodLore.Shutdown, AdoptedCm.CreatureCompendium.Release })
                try { stop(); } catch (Exception error) { log?.Warning("Wildblood lore teardown: " + error.GetType().Name); }
            WildbloodStatus.Reset();
            PlayerScalingRuntime.Reset();
            Adopted.Runtime.BossProgress.Shutdown();
            NativeEngineLifecycle.Shutdown();
            try { harmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Wildblood unpatch: " + error.GetType().Name); }
            harmony = null;
            if (releaseTheftGuards)
            {
                // Without the guard a kept removal could never be applied: apply it now rather than lose the caller's payment.
                try { Adopted.Transfers.TheftInventoryGuards.FlushRemovals(); } catch (Exception error) { log?.Warning("Wildblood kept removals: " + error.GetType().Name); }
                try { theftHarmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Wildblood inventory unpatch: " + error.GetType().Name); }
                theftHarmony = null;
            }
            if (releaseLuckGuards)
            {
                try { luckHarmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Wildblood luck unpatch: " + error.GetType().Name); }
                luckHarmony = null;
            }
            karmaRules?.Dispose(); karmaRules = null;
            modifierRules?.Dispose(); modifierRules = null;
            AdoptedCm.CreatureKarmaManager.ResetRuntimeState();
            AdoptedCm.CreatureManagerSpawnLifecycle.ResetRuntimeState();
            creatureRules?.Dispose(); creatureRules = null;
            nativeRules?.Dispose();
            nativeRules = null;
            nativeConfiguration?.Dispose();
            nativeConfiguration = null;
            foreach (Action stop in new Action[] { () => {
                if (releaseLuckGuards) Adopted.Runtime.NativeRpcOwnership.Shutdown();
                else foreach (Adopted.Runtime.NativeRpcOwner owner in Enum.GetValues(typeof(Adopted.Runtime.NativeRpcOwner)))
                    if (owner != Adopted.Runtime.NativeRpcOwner.LuckPlayer) Adopted.Runtime.NativeRpcOwnership.Release(owner);
            }, WildbloodModules.Reset, WildbloodText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Wildblood teardown: " + error.GetType().Name); }
            Settings?.Shutdown();
            Settings = null;
            // A stopped Wildblood still holding a native inventory guard stays registered, so meadworks.owns.elite_creatures
            // reads "installed but stopped" and Burrow keeps refusing new transfers; its providers read Wildblood's
            // stopped state. Otherwise the registration goes, as before.
            if (releaseTheftGuards && releaseLuckGuards)
            {
                registration?.Dispose();
                registration = null;
            }
            NativeLogger = null;
            if (ReferenceEquals(instance, this)) instance = null;
        }
    }
}
