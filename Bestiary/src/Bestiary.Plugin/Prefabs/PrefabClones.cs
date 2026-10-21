// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Settings;
using ExtraPotions.Bestiary.Text;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Prefabs
{
    /// <summary>
    /// Plain-clone fallback (design section 8 "Prefab build"). It runs after the native builders on every peer, when
    /// <c>ZNetScene</c> wakes. A catalog item or creature the native runtime registered is left as it is. One it did not
    /// register (its rebuilt bundle is unavailable on this platform, or its builder threw) becomes a copy of its game base,
    /// renamed to its ECP prefab name (Q2) and registered in <c>ZNetScene</c> and, for items, in every <c>ObjectDB</c>, so a
    /// ZDO of that name still resolves on every peer. Items get their names, icons, the bone damage factor and food values;
    /// creatures get their names, scale, health and drop tables; no rig, attacks, recipes or spawn hooks. A name another
    /// mod registers is left alone.
    /// </summary>
    internal static class PrefabClones
    {
        private static readonly Dictionary<string, GameObject> built = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private static readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
        private static Harmony? harmony;
        private static MeadworksLog? log;
        private static Func<bool>? active;

        public static int ItemCount { get; private set; }
        public static int CreatureCount { get; private set; }
        public static int ModelCount { get; private set; }

        public static void Install(Harmony patcher, MeadworksLog logger, Func<bool> isActive)
        {
            harmony = patcher;
            log = logger;
            active = isActive;
            NetPrefabs.OnSceneAwake(patcher, Build);
        }

        public static void Reset()
        {
            harmony = null;
            active = null;
        }

        private static void Build(ZNetScene scene)
        {
            if (harmony == null || active == null || !active()) return;
            var settings = Plugin.Settings;
            if (settings == null) return;
            ItemCount = 0;
            CreatureCount = 0;
            ModelCount = 0;
            foreach (var item in ItemCatalog.All)
                Guard("item " + item.Prefab, () => BuildItem(scene, item));
            foreach (var creature in CreatureCatalog.All)
                Guard("creature " + creature.Prefab, () => BuildCreature(scene, creature, settings));
            if (ItemCount + CreatureCount > 0)
                log?.Verbose($"Bestiary plain-clone fallback registered: {ItemCount} items, {CreatureCount} creatures.");
        }

        private static void Guard(string what, Action build)
        {
            try
            {
                build();
            }
            catch (Exception error)
            {
                log?.Warning($"Bestiary {what} not built; it is left out and the game carries on: {error.GetType().Name}: {error.Message}");
            }
        }

        /// <summary>The copy to register in this scene: built once per game process, reused by later scenes (<paramref name="fresh"/> false).</summary>
        private static GameObject? Obtain(ZNetScene scene, string baseName, string name, out bool fresh)
        {
            fresh = false;
            built.TryGetValue(name, out var mine);
            var present = scene.GetPrefab(name);
            if (present != null && present != mine)
            {
                if (!Native.Adapters.NativeRuntime.Owns(present))
                    Once(name, $"Bestiary {name} not built: another mod already registers that name.");
                return null;
            }
            if (mine != null) return mine;
            var game = scene.GetPrefab(baseName);
            if (game == null)
            {
                Once(name, $"Bestiary {name} not built: the game has no {baseName}.");
                return null;
            }
            fresh = true;
            Once(name, $"Bestiary {name}: native build unavailable; registered a plain {baseName} clone as fallback.");
            return built[name] = PrefabBench.Copy(game, name);
        }

        /// <summary>One warning per name per process; scene wakes repeat every session.</summary>
        private static void Once(string name, string message)
        {
            if (reported.Add(name)) log?.Warning(message);
        }

        private static void BuildItem(ZNetScene scene, ItemDefinition item)
        {
            var copy = Obtain(scene, item.BaseItem, item.Prefab, out var fresh);
            if (copy == null) return;
            if (fresh) {
                Configure(copy, item);
                try { if(ArsenalItemModels.Apply(scene,copy,item,log))ModelCount++; }
                catch(Exception error){log?.Warning("Bestiary model dressing for "+item.Prefab+" failed: "+error.GetType().Name);}
            }
            NetPrefabs.Register(scene, copy);
            ItemPrefabs.Register(harmony!, copy);
            ItemCount++;
        }

        private static void Configure(GameObject copy, ItemDefinition item)
        {
            var drop = copy.GetComponent<ItemDrop>();
            if (drop == null) throw new InvalidOperationException(item.BaseItem + " has no ItemDrop");
            var shared = drop.m_itemData.m_shared;
            shared.m_name = BestiaryTextKeys.Token(item.Word);
            shared.m_description = BestiaryTextKeys.Token(BestiaryTextKeys.Description(item.Word));
            var icon=RebuiltBundles.ItemIcon(item.Prefab,log);
            if(icon!=null)shared.m_icons=new[]{icon};
            if (item.DamageFactor != 1f)
            {
                shared.m_damages.Modify(item.DamageFactor);
                shared.m_damagesPerLevel.Modify(item.DamageFactor);
            }
            if (item.Kind == ItemKind.Food)
            {
                shared.m_food = item.Food.Health;
                shared.m_foodStamina = item.Food.Stamina;
                shared.m_foodRegen = item.Food.Regen;
                shared.m_foodBurnTime = item.Food.BurnTime;
            }
        }

        private static void BuildCreature(ZNetScene scene, CreatureDefinition creature, BestiaryConfig settings)
        {
            var copy = Obtain(scene, creature.BasePrefab, creature.Prefab, out var fresh);
            if (copy == null) return;
            var character = copy.GetComponent<Character>();
            if (character != null && creature.HealthSetting != null) character.m_health = settings.Get<float>(creature.HealthSetting);
            if (fresh) Configure(scene, copy, character, creature, settings);
            NetPrefabs.Register(scene, copy);
            CreatureCount++;
        }

        private static void Configure(ZNetScene scene, GameObject copy, Character? character, CreatureDefinition creature, BestiaryConfig settings)
        {
            if (creature.Scale != 1f) copy.transform.localScale = Vector3.one * creature.Scale;
            if (character != null && creature.Word != null) character.m_name = BestiaryTextKeys.Token(creature.Word);
            var drops = copy.GetComponent<CharacterDrop>();
            if (drops != null)
            {
                if (!creature.KeepBaseDrops) drops.m_drops = new List<CharacterDrop.Drop>();
                foreach (var spec in creature.Drops)
                {
                    var prefab = scene.GetPrefab(spec.Item);
                    if (prefab == null)
                    {
                        log?.Warning($"Bestiary {creature.Prefab}: drop {spec.Item} is unknown; left out.");
                        continue;
                    }
                    var chance = spec.ChanceSetting != null ? settings.Get<float>(spec.ChanceSetting) / 100f : spec.Chance;
                    drops.m_drops.Add(new CharacterDrop.Drop
                    {
                        m_prefab = prefab, m_amountMin = spec.Min, m_amountMax = spec.Max, m_chance = chance,
                        m_levelMultiplier = spec.LevelMultiplier, m_dontScale = spec.DontScale,
                    });
                }
            }
        }
    }
}
