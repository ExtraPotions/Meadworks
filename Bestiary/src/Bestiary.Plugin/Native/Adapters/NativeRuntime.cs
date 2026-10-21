// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Integration;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Bestiary.Runtime.Native.Adapters
{
    internal static class NativeRuntime
    {
        private static Harmony? harmony;
        private static bool started;
        private static bool? active;
        private static readonly List<Tuple<ZNetScene, GameObject>> network = new();
        private static readonly List<Tuple<ObjectDB, GameObject>> items = new();
        private static readonly List<Tuple<ObjectDB, Recipe>> recipes = new();
        private static readonly List<Component> components = new();
        private static readonly List<GameObject> objects = new();
        private static readonly List<Tuple<SpawnSystem, SpawnSystemList>> spawnLists = new();
        private static readonly List<Tuple<CookingStation, CookingStation.ItemConversion>> cooking = new();
        private static readonly List<Tuple<ObjectDB, StatusEffect>> statuses = new();
        internal static int PatchClasses { get; private set; }
        internal static int PatchFailures { get; private set; }
        internal static void Start(ConfigFile file, ManualLogSource logger, MeadworksLog log)
        {
            if (BestiaryIntegration.ProductYields) return;
            started = true;
            Core.Log.Bind(logger); Settings.Initialize(file);
            harmony = new Harmony(Plugin.Guid + ".native");
            foreach (var type in typeof(NativeRuntime).Assembly.GetTypes().Where(t => t.Namespace == "ExtraPotions.Bestiary.Runtime.Native" || t.Namespace?.StartsWith("ExtraPotions.Bestiary.Runtime.Native.", StringComparison.Ordinal) == true))
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                    try { harmony.CreateClassProcessor(type).Patch(); PatchClasses++; }
                    catch (Exception error) { PatchFailures++; log.Error("Bestiary native patch " + type.Name + " refused: " + error.Message); }
            // Pinned order: loot/arsenal dependencies precede crossbow and greataxe construction.
            Mimic.MimicPrefabs.Install(harmony); Slinger.SlingerPrefabs.Install(harmony);
            RimeGiant.RimeGiantPrefabs.Install(harmony); Kraken.KrakenPrefabs.Install(harmony);
            Arsenal.ArsenalPrefabs.Install(harmony); Crossbow.XbowPrefabs.Install(harmony); Headsman.HeadsmanPrefabs.Install(harmony);
            Ballista.BallistaPrefabs.Install(harmony);
            // Runs after every native builder: a catalog name a builder left unregistered (bundle unavailable on this
            // platform, a builder that threw) becomes a plain game-prefab clone, so peers still resolve its ZDOs.
            Prefabs.PrefabClones.Install(harmony, log, () => !BestiaryIntegration.ProductYields);
            Prefabs.OriginalItemVisuals.Install(harmony, log);
            Settings.Changed += RefreshOwned;
            NetPrefabs.OnSceneAwake(harmony, _ => RefreshOwned());
        }
        internal static void Tick()
        {
            if (!started) return;
            bool now = NativeConfiguration.Active;
            if (active != now) { active = now; Settings.Request(); }
            Settings.Tick(); NativeWords.Tick();
        }
        internal static void Network(ZNetScene scene, GameObject prefab) => network.Add(Tuple.Create(scene, prefab));
        /// <summary>The prefab was registered by Bestiary itself (a native builder or a fallback clone), not another mod.</summary>
        internal static bool Owns(GameObject prefab) => network.Any(pair => pair.Item2 == prefab);
        internal static void Item(ObjectDB db, GameObject prefab) => items.Add(Tuple.Create(db, prefab));
        internal static void Recipe(ObjectDB db, Recipe recipe) { if (!recipes.Any(p => p.Item1 == db && p.Item2 == recipe)) recipes.Add(Tuple.Create(db, recipe)); }
        internal static void Status(ObjectDB db, StatusEffect effect) => statuses.Add(Tuple.Create(db, effect));
        internal static void SpawnList(SpawnSystem system, SpawnSystemList list) => spawnLists.Add(Tuple.Create(system, list));
        internal static void Cooking(CookingStation station, CookingStation.ItemConversion conversion) => cooking.Add(Tuple.Create(station, conversion));
        internal static void Own(GameObject value) => objects.Add(value);
        internal static void Own(Component component) => components.Add(component);
        private static void RefreshOwned()
        {
            Arsenal.ArsenalRecipes.Install(ObjectDB.instance);
            Crossbow.XbowRecipe.Install(ObjectDB.instance); Headsman.GreataxeRecipe.Install(ObjectDB.instance); Kraken.ShieldRecipe.Install(ObjectDB.instance);
            Ballista.BallistaCrafting.Refresh(ObjectDB.instance);
            foreach (var pair in recipes) if (pair.Item1 != null && pair.Item2 != null && !NativeConfiguration.Active) pair.Item2.m_enabled = false;
            BestiaryIntegration.RegisterWithSiblings();
        }
        internal static void Shutdown()
        {
            if (!started) return;
            started = false;
            Settings.Shutdown(); harmony?.UnpatchSelf(); harmony = null;
            Kraken.InkScreen.Clear(); Kraken.KrakenDropSpot.Close(); Ballista.BallistaCrafting.Remove();
            foreach (var pair in network) if (pair.Item1 != null && pair.Item2 != null)
            {
                int hash = pair.Item2.name.GetStableHashCode();
                if (pair.Item1.m_namedPrefabs.TryGetValue(hash, out var present) && present == pair.Item2) pair.Item1.m_namedPrefabs.Remove(hash);
                pair.Item1.m_prefabs.Remove(pair.Item2);
            }
            foreach (var pair in items) if (pair.Item1 != null && pair.Item2 != null)
            {
                int hash = pair.Item2.name.GetStableHashCode();
                if (pair.Item1.m_itemByHash.TryGetValue(hash, out var present) && present == pair.Item2) pair.Item1.m_itemByHash.Remove(hash);
                pair.Item1.m_items.Remove(pair.Item2);
            }
            foreach (var pair in recipes) if (pair.Item1 != null) pair.Item1.m_recipes.Remove(pair.Item2);
            foreach (var pair in statuses) if (pair.Item1 != null) pair.Item1.m_StatusEffects.Remove(pair.Item2);
            foreach (var pair in spawnLists) if (pair.Item1 != null) pair.Item1.m_spawnLists.Remove(pair.Item2);
            foreach (var pair in cooking) if (pair.Item1 != null) pair.Item1.m_conversion.Remove(pair.Item2);
            foreach (var value in objects) if (value != null) UnityEngine.Object.Destroy(value);
            foreach (var component in components) if (component != null) UnityEngine.Object.Destroy(component);
            NetPrefabs.ResetCallbacks(); ItemPrefabs.ResetCallbacks(); NativeWords.Reset(); Core.StandIns.Table.Clear();
            ExtraPotions.Meadworks.OriginalArt.OriginalItemArt.Shutdown();
            network.Clear(); items.Clear(); recipes.Clear(); statuses.Clear(); components.Clear(); objects.Clear(); spawnLists.Clear(); cooking.Clear(); active = null;
        }
    }
    public static class BestiaryNativeStatus
    {
        public static int PatchClasses => NativeRuntime.PatchClasses;
        /// <summary>Catalog prefabs registered as plain game clones on the last scene wake because their native build was unavailable.</summary>
        public static int FallbackClones => Prefabs.PrefabClones.ItemCount + Prefabs.PrefabClones.CreatureCount;
        public static int NativeFailures => Guard.Failures + NativeRuntime.PatchFailures + Plugin.NativeFailures + Workshop.WorkshopRuntime.Failures;
        public static int ItemCount => ZNetScene.instance == null ? 0 : Catalog.ItemCatalog.All.Count(item => ZNetScene.instance.GetPrefab(item.Prefab) != null);
        public static int CreatureCount => ZNetScene.instance == null ? 0 : Catalog.CreatureCatalog.All.Count(creature => ZNetScene.instance.GetPrefab(creature.Prefab) != null);
    }
}
