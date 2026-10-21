// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// Builds the Crypt Executioner's prefabs once and registers them whenever ZNetScene wakes, identically on the server
    /// and every client: the creature (a copy of the game's Skeleton carrying the bundle's greataxe,
    /// <see cref="HeadsmanCreature"/>), its six attacks (copies of the Skeleton's sword), its two thrown axes (copies of the skeleton archer's arrow) and the shatter where they break,
    /// the skeleton it raises there, the spawner a burial chamber gets (<see cref="HeadsmanChambers"/>), and the players'
    /// Executioner's Greataxe and its axehead (<see cref="GreataxeItems"/>). The bundle comes from AssetWorkshop
    /// (assets/ecp_headsman: build.ps1 -Bundle -Install); every material is the Skeleton's own wearing the axe's texture.
    /// </summary>
    public static class HeadsmanPrefabs
    {
        public const string Creature = "ECP_Headsman";
        private const string Bundle = "ecp_headsman", Skeleton = "Skeleton", Rock = "rock4_forest";

        /// <summary>The creature prefab, once built; null until the first ZNetScene wakes.</summary>
        public static GameObject? Prefab { get; private set; }

        private static readonly List<GameObject> net = new List<GameObject>();

        public static void Install(Harmony harmony)
        {
            NetPrefabs.OnSceneAwake(harmony, scene => Guard.Run("HeadsmanPrefabs.Build", () => Build(scene, harmony)));
            Settings.Changed += () => SafeCall.Run("crypt executioner settings", Reapply);
        }

        private static void Build(ZNetScene scene, Harmony harmony)
        {
            if (Prefab == null && !Create(scene, harmony))
            {
                return;
            }
            foreach (GameObject prefab in net)
            {
                NetPrefabs.Register(scene, prefab);
            }
            HeadsmanSounds.Load(scene);
            HeadsmanWords.Add(Localization.instance);
            Log.Info($"Crypt Executioner ready: {string.Join(", ", net.Select(p => p.name))}.");
        }

        /// <summary>Builds every prefab, or logs what the game lacks and builds none (nothing else is held up).</summary>
        private static bool Create(ZNetScene scene, Harmony harmony)
        {
            Humanoid? skeleton = scene.GetPrefab(Skeleton)?.GetComponent<Humanoid>();
            GameObject? sword = Weapon(skeleton, "skeleton_sword"), bow = Weapon(skeleton, "skeleton_bow");
            GameObject? arrow = bow?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_attack.m_attackProjectile;
            GameObject? raised = scene.GetPrefab(HeadsmanSummon.Base);
            if (skeleton == null || sword == null || arrow == null || raised == null)
            {
                Log.Error($"Crypt Executioner not built: the game lacks the {Skeleton}, its sword, its archer's arrow or {HeadsmanSummon.Base}.");
                return false;
            }
            AssetBundle bundle = EmbeddedBundle.Load(typeof(HeadsmanPrefabs).Assembly, Bundle);
            HeadsmanKit.Load(bundle, HeadsmanKit.Skin(skeleton.transform.Find("Visual")));
            HeadsmanRocks.Stone = GameMaterials.Borrow(scene.GetPrefab(Rock));
            GreataxeItems.Build(scene, harmony, bundle, skeleton);
            GameObject shatter = HeadsmanShatter.Build();
            GameObject hurl = HeadsmanThrow.Build(arrow, HeadsmanThrow.Hurled, false, shatter), disc = HeadsmanThrow.Build(arrow, HeadsmanThrow.Disc, true, shatter);
            GameObject[] attacks = HeadsmanAttacks.Build(sword, hurl, disc);
            attacks.ToList().ForEach(attack => ItemPrefabs.Register(harmony, attack));
            Prefab = HeadsmanCreature.Build(skeleton.gameObject, attacks, GreataxeItems.Axehead, scene);
            net.AddRange(new[] { shatter, hurl, disc, HeadsmanSummon.Build(raised), Prefab, HeadsmanChambers.BuildSpawner(Prefab) });
            net.AddRange(GreataxeItems.NetPrefabs);
            return true;
        }

        private static GameObject? Weapon(Humanoid? skeleton, string name) =>
            skeleton?.m_randomWeapon.FirstOrDefault(item => item != null && item.name == name);

        /// <summary>After a settings change: attack damage, health and the axehead's chance for new ones, the spawner's return time.</summary>
        private static void Reapply()
        {
            HeadsmanAttacks.ApplyToAll();
            if (Prefab != null)
            {
                Prefab.GetComponent<Character>().m_health = HeadsmanSettings.Health;
                HeadsmanCreature.Reloot(Prefab, GreataxeItems.Axehead);
            }
            HeadsmanChambers.Reapply();
            GreataxeItems.Reapply();
        }
    }
}
