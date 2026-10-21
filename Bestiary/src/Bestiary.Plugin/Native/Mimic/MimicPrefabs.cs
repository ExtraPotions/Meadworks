// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// Builds the crypt mimic's three prefabs once and registers them whenever ZNetScene wakes, identically on the server
    /// and every client: the creature (a copy of the Black Forest crypt skeleton, so it keeps that skeleton's health,
    /// resistances, faction and effects, wearing our rig and the game's own crypt chest), its bite (a copy of the
    /// skeleton's sword, stripped of its model), and the corpse it leaves when it dies. The rig, parts and animations come
    /// from the embedded asset bundle built by AssetWorkshop; the chest meshes and every material are borrowed from the game.
    /// </summary>
    public static class MimicPrefabs
    {
        public const string Creature = "ECP_CryptMimic";
        public const string Corpse = "ECP_CryptMimic_ragdoll";
        public const string Bite = "ECP_CryptMimic_bite";
        public const string Chest = "TreasureChest_forestcrypt";
        private const string Bundle = "ecr_cryptmimic";
        private const string Model = "crypt_mimic";

        /// <summary>The prefabs, built on the first ZNetScene wake and kept (on the prefab bench) for every later one.</summary>
        private static GameObject? creature, bite, corpse;

        /// <summary>Registers on every ZNetScene wake; a rule reload puts the new bite numbers on every mimic at once.</summary>
        public static void Install(Harmony harmony)
        {
            NetPrefabs.OnSceneAwake(harmony, scene => Guard.Run("MimicPrefabs.Build", () => Build(scene, harmony)));
            Settings.Changed += () => SafeCall.Run("mimic settings", MimicBite.ApplySettings);
        }

        private static void Build(ZNetScene scene, Harmony harmony)
        {
            if (scene.GetPrefab(Creature) != null)
            {
                return;
            }
            if (creature == null)
            {
                Create(scene, harmony);
            }
            NetPrefabs.Register(scene, bite!);
            NetPrefabs.Register(scene, corpse!);
            NetPrefabs.Register(scene, creature!);
            MimicWords.Add(Localization.instance);
            Log.Info($"Crypt mimic ready: {Creature}, {Bite}, {Corpse}.");
        }

        private static void Create(ZNetScene scene, Harmony harmony)
        {
            GameObject model = EmbeddedBundle.Prefab(EmbeddedBundle.Load(typeof(MimicPrefabs).Assembly, Bundle), Model);
            GameObject chest = scene.GetPrefab(Chest);
            bite = MimicBite.Build(scene.GetPrefab("skeleton_sword"));
            ItemPrefabs.Register(harmony, bite);
            corpse = MimicCorpse.Build(scene.GetPrefab("Draugr_ragdoll"), MimicBody.Build(scene, model, chest));
            creature = MimicCreature.Build(scene.GetPrefab("Skeleton"), MimicBody.Build(scene, model, chest), bite, corpse);
        }
    }
}
