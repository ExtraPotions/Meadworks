// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Spawning;
using ExtraPotions.Bestiary.Text;
using BestiaryConfig = ExtraPotions.Bestiary.Runtime.Settings.BestiaryConfig;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Adapters.Kit
{
    /// <summary>Registers the four copies when ZNetScene wakes, before any prop is attached.</summary>
    internal static class KitPrefabs
    {
        private static ZRoutedRpc? bound;

        public static GameObject? BogHook { get; private set; }
        public static GameObject? Javelineer { get; private set; }
        public static GameObject? Slag { get; private set; }
        public static GameObject? Marrow { get; private set; }

        public static void Install(Harmony harmony) => NetPrefabs.OnSceneAwake(harmony, Build);

        private static void Build(ZNetScene scene)
        {
            EnsureRpc();
            // Every world load builds fresh copies from that load's settings. The last load's copies sit inactive on the
            // bench (never instantiated, so no scene instance or ZDO refers to them) and would otherwise pile up.
            Release(BogHook); Release(Javelineer); Release(Slag); Release(Marrow);
            BogHook = BuildOne(scene, KitCreatures.BogHook);
            Javelineer = BuildOne(scene, KitCreatures.Javelineer);
            Slag = BuildOne(scene, KitCreatures.SlagCharred);
            Marrow = BuildOne(scene, KitCreatures.MarrowLantern);
            if (BogHook != null) StandIns.Add(BogHook, KitCreatures.Draugr);
            if (Javelineer != null) StandIns.Add(Javelineer, KitCreatures.Fuling);
            if (Marrow != null) StandIns.Add(Marrow, KitCreatures.DeepNorthSkeleton);
            KitSpawns.Refresh();
        }

        private static void Release(GameObject? copy)
        {
            if (copy != null) UnityEngine.Object.Destroy(copy);
        }

        private static void EnsureRpc()
        {
            var routed = ZRoutedRpc.instance;
            if (routed == null || routed == bound) return;
            routed.Register<ZDOID, Vector3, float>(BestiaryKeys.BogHookPull, KitPull.Receive);
            routed.Register<int, Vector3>(BestiaryKeys.SlagClaimAsk, KitSpawns.Asked);
            routed.Register<bool, int, Vector3>(BestiaryKeys.SlagClaimAnswer, KitSpawns.Answer);
            bound = routed;
        }

        private static GameObject? BuildOne(ZNetScene scene, string name)
        {
            try
            {
                var creature = CreatureCatalog.Find(name);
                var settings = Plugin.Settings;
                if (creature == null || settings == null) return null;
                if (scene.GetPrefab(name) != null)
                {
                    Log.Warn("Bestiary " + name + " not built: that name is already registered.");
                    return null;
                }
                var body = scene.GetPrefab(creature.BasePrefab);
                if (body == null)
                {
                    Log.Warn("Bestiary " + name + " not built: the game has no " + creature.BasePrefab + ".");
                    return null;
                }
                var copy = PrefabBench.Copy(body, name);
                Configure(scene, copy, creature, settings);
                if (!NetPrefabs.Register(scene, copy)) return null;
                try { KitLook.Dress(copy, name); }
                catch (Exception error) { Log.Warn("Bestiary " + name + " prop skipped: " + error.Message); }
                return copy;
            }
            catch (Exception error)
            {
                Log.Warn("Bestiary " + name + " not built: " + error.GetType().Name + ": " + error.Message);
                return null;
            }
        }

        private static void Configure(ZNetScene scene, GameObject copy, CreatureDefinition creature, BestiaryConfig settings)
        {
            var character = copy.GetComponent<Character>();
            if (character != null)
            {
                character.m_boss = false;
                character.m_bossEvent = "";
                if (creature.Word != null) character.m_name = BestiaryTextKeys.Token(creature.Word);
                if (creature.HealthSetting != null) character.m_health = settings.Get<float>(creature.HealthSetting);
            }
            var tame = copy.GetComponent<Tameable>();
            if (tame != null) UnityEngine.Object.DestroyImmediate(tame);
            var bred = copy.GetComponent<Procreation>();
            if (bred != null) UnityEngine.Object.DestroyImmediate(bred);
            var drops = copy.GetComponent<CharacterDrop>();
            if (drops != null)
            {
                if (!creature.KeepBaseDrops) drops.m_drops = new List<CharacterDrop.Drop>();
                foreach (var spec in creature.Drops)
                {
                    var prefab = scene.GetPrefab(spec.Item);
                    if (prefab == null)
                    {
                        Log.Warn("Bestiary " + creature.Prefab + ": drop " + spec.Item + " is unknown; left out.");
                        continue;
                    }
                    drops.m_drops.Add(new CharacterDrop.Drop
                    {
                        m_prefab = prefab,
                        m_amountMin = spec.Min,
                        m_amountMax = spec.Max,
                        m_chance = spec.Chance,
                        m_levelMultiplier = spec.LevelMultiplier,
                        m_dontScale = spec.DontScale,
                    });
                }
            }
            if (creature.Prefab == KitCreatures.BogHook) copy.AddComponent<BogHook>();
            else if (creature.Prefab == KitCreatures.Javelineer)
            {
                copy.AddComponent<JavelinOnce>();
                SpearOnly(scene, copy);
            }
            else if (creature.Prefab == KitCreatures.SlagCharred) copy.AddComponent<KitPlates>();
        }

        // New array on this copy. The Goblin prefab keeps its own weapon list. Shield, armor, and default items stay as copied.
        private static void SpearOnly(ZNetScene scene, GameObject copy)
        {
            var humanoid = copy.GetComponent<Humanoid>();
            if (humanoid == null)
            {
                Log.Warn("Bestiary " + KitCreatures.Javelineer + " has no humanoid; no spear was assigned.");
                return;
            }
            var names = SpearPool.For(name => Item(scene, name) != null);
            GameObject? spear = names.Length == 0 ? null : Item(scene, names[0]);
            if (spear == null)
            {
                humanoid.m_randomWeapon = Array.Empty<GameObject>();
                Log.Warn("Bestiary " + KitCreatures.Javelineer + " spear prefab is missing; it keeps no random weapon. The fight still runs.");
                return;
            }
            humanoid.m_randomWeapon = new[] { spear };
        }

        private static GameObject? Item(ZNetScene scene, string name)
        {
            var found = scene.GetPrefab(name);
            if (found != null) return found;
            var db = ObjectDB.instance;
            return db != null ? db.GetItemPrefab(name) : null;
        }
    }
}
