// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// Builds the Rime Giant's prefabs once and registers them whenever ZNetScene wakes, identically on the server and
    /// every client: the creature (a copy of the game's forest troll wearing the rime kit from the embedded bundle), its
    /// ice boulder (a copy of the troll's thrown rock), its corpse (a copy of the troll's ragdoll) and its three attacks
    /// (copies of the troll's own). The bundle comes from AssetWorkshop (assets/ecr_rimegiant); every material is the
    /// troll's own wearing the bundle's textures. Its spawn joins the game's own spawn lists (<see cref="RimeGiantSpawns"/>).
    /// </summary>
    public static class RimeGiantPrefabs
    {
        public const string Creature = "ECP_RimeGiant";
        public const string Boulder = "ECP_RimeGiant_boulder";
        public const string Corpse = "ECP_RimeGiant_ragdoll";
        private const string Troll = "Troll";
        private const string Bundle = "ecr_rimegiant";
        private const string Kit = "ecr_rime_kit";
        private const string BoulderLook = "ecr_rime_boulder";

        /// <summary>The creature prefab, once built; null until the first ZNetScene wakes.</summary>
        public static GameObject? Prefab { get; private set; }

        private static GameObject? boulder, corpse;

        public static void Install(Harmony harmony)
        {
            NetPrefabs.OnSceneAwake(harmony, scene => Guard.Run("RimeGiantPrefabs.Build", () => Build(scene, harmony)));
            Settings.Changed += () => SafeCall.Run("rime giant settings", Reapply);
        }

        private static void Build(ZNetScene scene, Harmony harmony)
        {
            if (scene.GetPrefab(Creature) != null)
            {
                return;
            }
            if (Prefab == null && !Create(scene, harmony))
            {
                return;
            }
            NetPrefabs.Register(scene, boulder!);
            NetPrefabs.Register(scene, corpse!);
            NetPrefabs.Register(scene, Prefab!);
            RimeGiantSpawns.Refresh();
            RimeGiantWords.Add(Localization.instance);
            Log.Info($"Rime giant ready: {Creature}, {Boulder}, {Corpse}.");
        }

        /// <summary>
        /// Builds every prefab, or logs what is missing and builds none (no giant then, but nothing else is held up). The
        /// troll's attacks are not network prefabs: they are found among its own weapon sets.
        /// </summary>
        private static bool Create(ZNetScene scene, Harmony harmony)
        {
            Humanoid? troll = scene.GetPrefab(Troll)?.GetComponent<Humanoid>();
            GameObject?[] parts = troll == null ? new GameObject?[0]
                : new[] { RimeAttacks.TrollItem(troll, RimeAttacks.Punch), RimeAttacks.TrollItem(troll, RimeAttacks.GroundSlam), RimeAttacks.TrollItem(troll, RimeAttacks.RockThrow) };
            GameObject? rock = parts.Length == 3 ? parts[2]?.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack.m_attackProjectile : null;
            corpse = troll == null ? null : RimeCorpse.Build(troll);
            if (troll == null || parts.Any(part => part == null) || rock == null || corpse == null)
            {
                Log.Error($"Rime giant not built: the game's {Troll} is missing {(troll == null ? "entirely" : "an attack, its thrown rock or its ragdoll")}.");
                return false;
            }
            AssetBundle bundle = EmbeddedBundle.Load(typeof(RimeGiantPrefabs).Assembly, Bundle);
            Material skin = RimeKit.Skin(troll.transform);
            boulder = RimeBoulder.Build(rock, bundle.LoadAsset<GameObject>(BoulderLook), skin, scene);
            GameObject[] attacks = RimeAttacks.Build(parts[0]!, parts[1]!, parts[2]!, boulder);
            foreach (GameObject attack in attacks)
            {
                ItemPrefabs.Register(harmony, attack);
            }
            Prefab = RimeGiantCreature.Build(troll.gameObject, attacks, corpse, EmbeddedBundle.Prefab(bundle, Kit), scene);
            return true;
        }

        /// <summary>After a settings change: attack damage on the prefab and loaded giants, health for new ones, the spawn.</summary>
        private static void Reapply()
        {
            RimeAttacks.ApplyToAll();
            RimeGiantSpawns.Refresh();
            if (Prefab != null)
            {
                Prefab.GetComponent<Character>().m_health = RimeGiantSettings.Health;
            }
        }
    }
}
