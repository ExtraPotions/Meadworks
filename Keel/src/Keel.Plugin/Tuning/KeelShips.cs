// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, ShipConfig 1.4.2, GPL-3.0-or-later.
// Adapted from ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1:
// ShipConfig/ShipConfig/ShipConfiguration.cs, ShipDiscovery.cs, ShipDefaults.cs (From, ReadSailing), ShipBuildCost.cs
// (ReadOriginals, Apply), ShipValues.cs, ShipHealthUpdater.cs, ShipValueQueue.cs, ShipDamagePatches.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: one Keel class instead of eight ShipConfig classes; entries bound through Crucible C01
// (MeadworksConfig.Bind, ServerAuthoritative, section Ships, Keel's ranges) instead of SyncedConfig; the effective values
// come from Keel.Core ShipTuning (cheat lists instead of the per-ship AshlandsOceanDamage and Invulnerable entries); the
// vanilla values are restored while the tuning module is off (peer class gate, step-aside); the queue flushes from the
// plugin's Update instead of a coroutine; imported values for ships not present yet are applied at discovery; the
// damage lookup caches the effective ship per prefab hash; a loaded ship's max health includes the Sailing shipwright
// factor (GrindstoneSkills ShipwrightHealth), so tuning changes keep it; PatchGuard replaced by try/catch with a log line;
// the catalog prepares Keel's hull clones before discovery and a changed catalog recipe replaces that ship's material amounts;
// pending imported values use the existing C09 atomic target under one C01 batch, and remote overlays never consume them.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Integration;
using ExtraPotions.Keel.Tuning;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Ship tuning (design A1 to A7, D1, D2): finds every ship prefab, binds its per-ship entries, writes the effective
    /// values into the prefab and every loaded ship of that kind, scales or drops damage on known ships. Main thread only.
    /// </summary>
    public static class KeelShips
    {
        private sealed class ShipEntries
        {
            public ShipEntries(string name, ShipDefaults defaults)
            {
                Name = name;
                Defaults = defaults;
            }

            public string Name { get; }
            public ShipDefaults Defaults { get; }
            public Dictionary<ShipField, ConfigEntryBase> Entries { get; } = new Dictionary<ShipField, ConfigEntryBase>();
            public EffectiveShip Effective { get; set; }
            public bool Applied { get; set; }
        }

        private static readonly ShipRegistry registry = new ShipRegistry();
        private static readonly Dictionary<string, ShipEntries> ships = new Dictionary<string, ShipEntries>(StringComparer.Ordinal);
        private static readonly Dictionary<int, ShipEntries> byPrefab = new Dictionary<int, ShipEntries>();
        private static readonly HashSet<string> pendingApply = new HashSet<string>(StringComparer.Ordinal);
        private static bool pendingAll;
        private static bool? lastActive;
        private static long lastGeneration = -1;
        private static MeadworksLog? log;
        private static KeelImport? import;
        private static readonly Dictionary<ConfigEntryBase, Action> watched = new Dictionary<ConfigEntryBase, Action>();
        private static ConfigFile? file;

        public static int Count => ships.Count;

        public static void Initialize(MeadworksLog logger, KeelImport? importer)
        {
            log = logger;
            import = importer;
            var s = Plugin.Settings;
            if (s == null) return;
            file = s.File;
            file.SettingChanged += OnSettingChanged;
            file.ConfigReloaded += OnReloaded;
            Jotunn.Managers.SynchronizationManager.OnConfigurationSynchronized += OnSynchronized;
            foreach (var setting in new KeelSetting[]
            {
                KeelSettings.Multipliers.Health, KeelSettings.Multipliers.SailForce, KeelSettings.Multipliers.PaddleForce,
                KeelSettings.Multipliers.Turning, KeelSettings.Multipliers.DamageTaken, KeelSettings.Multipliers.BuildCost,
                KeelSettings.Cheats.AshlandsImmuneShips, KeelSettings.Cheats.AshlandsImmuneList,
                KeelSettings.Cheats.InvulnerableShips, KeelSettings.Cheats.InvulnerableList,
            })
                watched[s.Entry(setting)] = RequestAll;
        }

        /// <summary>
        /// While ShipConfig is installed the whole tuning module stays out of the way: no entries, no writes, no damage
        /// changes, so ShipConfig's own values stand (design 9.1).
        /// </summary>
        private static bool SteppedAside => KeelModules.StepAside.SteppedAside(KeelModule.Tuning);

        /// <summary>Every frame: re-applies when the module turns on or off (session, authority) and flushes queued changes.</summary>
        public static void Tick()
        {
            if (SteppedAside) return;
            var active = KeelModules.Active(KeelModule.Tuning);
            var generation = CruciblePlugin.Session.Generation;
            if (lastActive != active || generation != lastGeneration)
            {
                lastActive = active;
                lastGeneration = generation;
                RequestAll();
            }
            if (!pendingAll && pendingApply.Count == 0) return;
            try
            {
                if (pendingAll) ApplyAll();
                else foreach (var name in pendingApply) Apply(name);
            }
            catch (Exception error)
            {
                log?.Warning("Keel ship values: " + error.GetType().Name + ": " + error.Message);
            }
            pendingAll = false;
            pendingApply.Clear();
        }

        public static void Shutdown()
        {
            if (file != null)
            {
                file.SettingChanged -= OnSettingChanged;
                file.ConfigReloaded -= OnReloaded;
                Jotunn.Managers.SynchronizationManager.OnConfigurationSynchronized -= OnSynchronized;
                file = null;
            }
            watched.Clear();
            foreach (var ship in ships.Values)
                foreach (var entry in ship.Entries.Values) MeadworksBlockedOptions.Untrack(entry);
            ships.Clear();
            byPrefab.Clear();
            pendingApply.Clear();
            pendingAll = false;
            lastActive = null;
            lastGeneration = -1;
            import = null;
            log = null;
        }

        public static void RequestAll() => pendingAll = true;

        private static void Request(string name) => pendingApply.Add(name);

        // Discovery (ShipDiscovery).

        /// <summary>Binds entries for every ship prefab of the scene (once) and applies the values everywhere.</summary>
        internal static void LoadShips()
        {
            var scene = ZNetScene.instance;
            if (scene == null || Plugin.Settings == null || SteppedAside) return;
            var before = ships.Count;
            Batch(() =>
            {
                foreach (var prefab in scene.m_prefabs) RegisterPrefab(prefab);
            });
            LogCount(before);
            ApplyAll();
        }

        private static void RegisterPrefab(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponent<Ship>() == null || prefab.GetComponent<WearNTear>() == null) return;
            var defaults = ReadDefaults(prefab, isInstance: false);
            if (defaults != null) Register(prefab.name, defaults);
        }

        /// <summary>A ship's WearNTear awoke: register its prefab late when unknown, then apply to this instance.</summary>
        internal static void OnShipAwake(WearNTear wearNTear, Ship ship)
        {
            if (Plugin.Settings == null || SteppedAside) return;
            var name = Utils.GetPrefabName(wearNTear.gameObject);
            if (!ships.TryGetValue(name, out var entries))
            {
                var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                var defaults = prefab != null ? ReadDefaults(prefab, isInstance: false) : ReadDefaults(wearNTear.gameObject, isInstance: true);
                if (defaults == null) return;
                var before = ships.Count;
                Batch(() => Register(name, defaults));
                LogCount(before);
                if (!ships.TryGetValue(name, out entries)) return;
                Apply(name);
            }
            ApplyToInstance(ship, wearNTear, entries);
        }

        private static void Register(string name, ShipDefaults defaults)
        {
            if (!registry.Register(name, defaults)) return;
            var s = Plugin.Settings!;
            var entries = new ShipEntries(name, defaults);
            foreach (var spec in ShipFields.For(defaults))
            {
                ConfigEntryBase entry;
                var key = ShipFields.Key(name, spec.Field);
                var description = spec.Description;
                if (spec.IsToggle)
                    entry = MeadworksConfig.Bind(s.File, ShipFields.Section, key, spec.DefaultFor(defaults) != 0f, description, ConfigScope.ServerAuthoritative);
                else
                    entry = MeadworksConfig.Bind(s.File, ShipFields.Section, key, spec.DefaultFor(defaults), description, ConfigScope.ServerAuthoritative,
                        new AcceptableValueRange<float>(spec.MinimumFor(defaults), Math.Max(spec.MinimumFor(defaults), spec.MaximumFor(defaults))));
                MeadworksBlockedOptions.Track(Plugin.Guid, entry, ConfigScope.ServerAuthoritative);
                watched[entry] = () => Request(name);
                entries.Entries.Add(spec.Field, entry);
            }
            ships[name] = entries;
            byPrefab[name.GetStableHashCode()] = entries;
            ApplyImported(entries);
        }

        /// <summary>Imported values for this ship (converter, design 9.2) are written into its entries once, when it appears.</summary>
        private static void ApplyImported(ShipEntries entries)
        {
            if (import == null || !MeadworksConfig.CanEdit(ConfigScope.ServerAuthoritative)) return;
            import.ApplyPending(entries.Name, entries.Entries);
        }

        private static void Batch(Action register)
        {
            var file = Plugin.Settings!.File;
            var save = file.SaveOnConfigSet;
            var before = ships.Count;
            file.SaveOnConfigSet = false;
            try { register(); }
            finally { file.SaveOnConfigSet = save; }
            if (save && ships.Count != before) file.Save();
        }

        private static void LogCount(int before)
        {
            if (ships.Count != before) log?.Info("Ship settings loaded for " + ships.Count + " ships.");
        }

        /// <summary>
        /// A Keel ship's catalog recipe changed (K07b): its new material amounts become the amounts the build cost entry and
        /// multiplier scale, and the prefab is written again on the next frame. Unknown ships wait for discovery.
        /// </summary>
        internal static void SetBuildCosts(string name, int[] amounts)
        {
            if (amounts == null || !ships.TryGetValue(name ?? "", out var entries)) return;
            entries.Defaults.BuildCosts = (int[])amounts.Clone();
            Request(entries.Name);
        }

        // Defaults (ShipDefaults, ShipBuildCost.ReadOriginals, ShipHealthUpdater.WorldLevelFactor).

        /// <summary>
        /// The vanilla values of a prefab or a loaded instance; null without Ship and WearNTear. An instance's max health
        /// already carries the world level bonus, which is taken off again.
        /// </summary>
        private static ShipDefaults? ReadDefaults(GameObject source, bool isInstance)
        {
            var ship = source != null ? source.GetComponent<Ship>() : null;
            var wearNTear = source != null ? source.GetComponent<WearNTear>() : null;
            if (ship == null || wearNTear == null) return null;
            return new ShipDefaults
            {
                Health = isInstance ? wearNTear.m_health / WorldLevelFactor() : wearNTear.m_health,
                SailForce = ship.m_sailForceFactor,
                SailForceOffset = ship.m_sailForceOffset,
                PaddleForce = ship.m_backwardForce,
                RudderSpeed = ship.m_rudderSpeed,
                TurnForceSailing = ship.m_stearVelForceFactor,
                TurnForcePaddling = ship.m_stearForce,
                ForwardDrag = ship.m_dampingForward,
                SidewaysDrag = ship.m_dampingSideway,
                AngularDamping = ship.m_angularDamping,
                WaterImpactDamage = ship.m_waterImpactDamage,
                UpsideDownDamage = ship.m_upsideDownDmg,
                WeatherWear = wearNTear.m_noRoofWear,
                AshlandsReady = ship.m_ashlandsReady,
                BuildCosts = ReadOriginals(source!.GetComponent<Piece>()),
            };
        }

        private static int[]? ReadOriginals(Piece piece)
        {
            if (piece == null || piece.m_resources == null || piece.m_resources.Length == 0) return null;
            var amounts = new int[piece.m_resources.Length];
            for (var i = 0; i < amounts.Length; i++) amounts[i] = piece.m_resources[i] != null ? piece.m_resources[i].m_amount : 0;
            return amounts;
        }

        /// <summary>1 plus the bonus WearNTear.Awake adds to every piece's max health at world level above 0.</summary>
        private static float WorldLevelFactor()
        {
            if (Game.m_worldLevel <= 0 || Game.instance == null) return 1f;
            return 1f + Game.m_worldLevel * Game.instance.m_worldLevelPieceHPMultiplier;
        }

        // Values (ShipValues, ShipHealthUpdater).

        private static void ApplyAll()
        {
            foreach (var entries in ships.Values) Apply(entries);
        }

        private static void Apply(string name)
        {
            if (ships.TryGetValue(name, out var entries)) Apply(entries);
        }

        private static void Apply(ShipEntries entries)
        {
            entries.Effective = Compute(entries);
            ApplyToPrefab(entries);
            foreach (IMonoUpdater updater in Ship.Instances)
            {
                var ship = updater as Ship;
                if (ship != null && Utils.GetPrefabName(ship.gameObject) == entries.Name)
                    ApplyToInstance(ship, ship.GetComponent<WearNTear>(), entries);
            }
            entries.Applied = true;
        }

        /// <summary>Configured values while the tuning module acts; the game's own values otherwise.</summary>
        private static EffectiveShip Compute(ShipEntries entries)
        {
            var s = Plugin.Settings;
            if (s == null || !KeelModules.Active(KeelModule.Tuning))
                return ShipTuning.Compute(ShipSettings.Vanilla(entries.Defaults), GlobalMultipliers.Vanilla, false, false);
            var settings = ShipSettings.FromDefaults(entries.Defaults);
            float Number(ShipField f)
            {
                var entry = (ConfigEntry<float>)entries.Entries[f];
                var spec = ShipFields.Get(f);
                return spec.Normalize(entries.Defaults, MeadworksConfig.Effective(entry, Plugin.Guid, ConfigScope.ServerAuthoritative, spec.DefaultFor(entries.Defaults)));
            }
            settings.Health = Number(ShipField.Health);
            settings.SailForce = Number(ShipField.SailForce);
            settings.SailForceOffset = Number(ShipField.SailForceOffset);
            settings.PaddleForce = Number(ShipField.PaddleForce);
            settings.RudderSpeed = Number(ShipField.RudderSpeed);
            settings.TurnForceSailing = Number(ShipField.TurnForceSailing);
            settings.TurnForcePaddling = Number(ShipField.TurnForcePaddling);
            settings.ForwardDrag = Number(ShipField.ForwardDrag);
            settings.SidewaysDrag = Number(ShipField.SidewaysDrag);
            settings.AngularDamping = Number(ShipField.AngularDamping);
            settings.WaterImpactDamage = Number(ShipField.WaterImpactDamage);
            settings.UpsideDownDamage = Number(ShipField.UpsideDownDamage);
            settings.DamageTaken = Number(ShipField.DamageTaken);
            if (entries.Entries.ContainsKey(ShipField.BuildCost)) settings.BuildCost = Number(ShipField.BuildCost);
            settings.WeatherWear = MeadworksConfig.Effective((ConfigEntry<bool>)entries.Entries[ShipField.WeatherWear], Plugin.Guid,
                ConfigScope.ServerAuthoritative, false);
            var multipliers = GlobalMultipliers.From(s.Get(KeelSettings.Multipliers.Health), s.Get(KeelSettings.Multipliers.SailForce),
                s.Get(KeelSettings.Multipliers.PaddleForce), s.Get(KeelSettings.Multipliers.Turning), s.Get(KeelSettings.Multipliers.DamageTaken),
                s.Get(KeelSettings.Multipliers.BuildCost));
            return ShipTuning.Compute(settings, multipliers, s.Invulnerable.Matches(entries.Name), s.AshlandsImmune.Matches(entries.Name));
        }

        /// <summary>The scene prefab of the ship, build cost included (placement reads the prefab's Piece).</summary>
        private static void ApplyToPrefab(ShipEntries entries)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(entries.Name) : null;
            var ship = prefab != null ? prefab.GetComponent<Ship>() : null;
            var wearNTear = prefab != null ? prefab.GetComponent<WearNTear>() : null;
            if (ship == null || wearNTear == null) return;
            WriteFields(ship, wearNTear, entries.Effective);
            wearNTear.m_health = entries.Effective.Health;
            var piece = prefab!.GetComponent<Piece>();
            var amounts = ShipTuning.BuildAmounts(entries.Defaults.BuildCosts, entries.Effective.BuildCost);
            if (piece == null || amounts == null || piece.m_resources == null) return;
            for (var i = 0; i < Math.Min(amounts.Length, piece.m_resources.Length); i++)
                if (piece.m_resources[i] != null) piece.m_resources[i].m_amount = amounts[i];
        }

        /// <summary>
        /// One loaded ship: fields, then the max health with the world level bonus; the stored current health stays and
        /// the cached percentage and the visuals are refreshed from it, the way RPC_HealthChanged does.
        /// </summary>
        private static void ApplyToInstance(Ship ship, WearNTear wearNTear, ShipEntries entries)
        {
            if (ship == null || wearNTear == null) return;
            if (!entries.Applied) entries.Effective = Compute(entries);
            WriteFields(ship, wearNTear, entries.Effective);
            wearNTear.m_health = entries.Effective.Health * WorldLevelFactor() * SailingPerks.ShipwrightFactor(wearNTear);
            var view = wearNTear.m_nview;
            if (view == null || !view.IsValid()) return;
            var current = view.GetZDO().GetFloat(ZDOVars.s_health, wearNTear.m_health);
            wearNTear.m_healthPercentage = wearNTear.m_health > 0f ? Mathf.Clamp01(current / wearNTear.m_health) : 0f;
            wearNTear.UpdateVisual(false);
        }

        private static void WriteFields(Ship ship, WearNTear wearNTear, EffectiveShip e)
        {
            ship.m_sailForceFactor = e.SailForce;
            ship.m_sailForceOffset = e.SailForceOffset;
            ship.m_backwardForce = e.PaddleForce;
            ship.m_rudderSpeed = e.RudderSpeed;
            ship.m_stearVelForceFactor = e.TurnForceSailing;
            ship.m_stearForce = e.TurnForcePaddling;
            ship.m_dampingForward = e.ForwardDrag;
            ship.m_dampingSideway = e.SidewaysDrag;
            ship.m_angularDamping = e.AngularDamping;
            ship.m_waterImpactDamage = e.WaterImpactDamage;
            ship.m_upsideDownDmg = e.UpsideDownDamage;
            ship.m_ashlandsReady = e.AshlandsReady;
            wearNTear.m_noRoofWear = e.WeatherWear;
        }

        /// <summary>Re-applies one loaded ship's max health (a builder level was just stored on it).</summary>
        internal static void RefreshHealth(WearNTear wearNTear)
        {
            var ship = wearNTear != null ? wearNTear.GetComponent<Ship>() : null;
            if (ship == null || SteppedAside || !ships.TryGetValue(Utils.GetPrefabName(wearNTear!.gameObject), out var entries)) return;
            ApplyToInstance(ship, wearNTear, entries);
        }

        // Damage (ShipDamagePatches).

        /// <summary>The effective values of the known ship this WearNTear belongs to: one lookup by the ZDO's prefab hash.</summary>
        internal static bool TryShip(WearNTear wearNTear, out EffectiveShip effective)
        {
            effective = default;
            var zdo = wearNTear != null && wearNTear.m_nview != null ? wearNTear.m_nview.GetZDO() : null;
            if (zdo == null || !byPrefab.TryGetValue(zdo.GetPrefab(), out var entries) || !entries.Applied) return false;
            effective = entries.Effective;
            return true;
        }

        /// <summary>A change re-applies its ship, or every ship for a multiplier or cheat, once on the next frame.</summary>
        private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (e?.ChangedSetting != null && watched.TryGetValue(e.ChangedSetting, out var request)) request();
        }

        private static void OnReloaded(object sender, EventArgs e) => RequestAll();

        /// <summary>Server values arrived (Jotunn configuration sync, which C01 uses): re-apply every ship once.</summary>
        private static void OnSynchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs e) => RequestAll();

        internal static void Warn(string where, Exception error) => log?.Warning("Keel " + where + ": " + error.GetType().Name + ": " + error.Message);
    }

    /// <summary>Scene start, after other mods' postfixes so prefabs they register at normal priority are seen.</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    internal static class KeelZNetSceneAwakePatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            try { KeelCatalog.OnSceneReady(); }
            catch (Exception error) { KeelShips.Warn("catalog scene start", error); }
            try { KeelShips.LoadShips(); }
            catch (Exception error) { KeelShips.Warn("ship discovery", error); }
        }
    }

    /// <summary>
    /// A ship instance awoke. WearNTear.Awake is the hook: it reads the stored health and adds the world level bonus,
    /// which must happen first. Every building piece wakes here, so anything not a ship leaves at once.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Awake))]
    internal static class KeelWearNTearAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(WearNTear __instance)
        {
            if (!__instance.TryGetComponent(out Ship ship)) return;
            try { KeelShips.OnShipAwake(__instance, ship); }
            catch (Exception error) { KeelShips.Warn("ship awake", error); }
        }
    }

    /// <summary>Scales every damage component of a hit on a known ship, or drops the hit for a ship that takes no damage.</summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.RPC_Damage))]
    internal static class KeelRpcDamagePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WearNTear __instance, HitData hit)
        {
            if (!KeelShips.TryShip(__instance, out var ship)) return true;
            if (ship.TakesNoDamage) return false;
            hit.m_damage.Modify(ship.DamageTaken);
            return true;
        }
    }

    /// <summary>Blocks weather wear, and any other direct damage, on a ship that takes no damage. Hits were scaled already.</summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
    internal static class KeelApplyDamagePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WearNTear __instance, ref bool __result)
        {
            if (!KeelShips.TryShip(__instance, out var ship) || !ship.TakesNoDamage) return true;
            __result = false;
            return false;
        }
    }
}
