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

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// Builds the skeleton arsenal once and registers it whenever ZNetScene wakes, identically on the server and every
    /// client: the bone arrows (<see cref="ArsenalArrow"/>), the players' bone weapons and the spine
    /// (<see cref="ArsenalItems"/>), and for each of the game's skeletons (<see cref="ArsenalKind"/>) seven arsenal
    /// skeletons, one per weapon (<see cref="ArsenalCreature"/>), each with its own weapon (<see cref="ArsenalAttack"/>).
    /// The models and icons come from the embedded bundle, built by AssetWorkshop (assets/ecp_skel_arsenal); every
    /// material is the game's own Skeleton material wearing the bundle's textures.
    /// </summary>
    public static class ArsenalPrefabs
    {
        public const string Bundle = "ecp_skel_arsenal";

        private static bool built;

        public static void Install(Harmony harmony)
        {
            NetPrefabs.OnSceneAwake(harmony, scene => Guard.Run("ArsenalPrefabs.Build", () => Build(scene, harmony)));
        }

        private static void Build(ZNetScene scene, Harmony harmony)
        {
            if (!built && !Create(scene, harmony))
            {
                return;
            }
            foreach (GameObject prefab in Items().Concat(Shots()).Concat(ArsenalAttack.Items).Concat(Creatures()))
            {
                NetPrefabs.Register(scene, prefab);
            }
            ArsenalWords.Add(Localization.instance);
            Log.Info($"Skeleton arsenal ready: {Creatures().Count()} skeletons, {Items().Count()} items.");
        }

        /// <summary>Everything once, from the Black Forest's Skeleton's material; a piece the game lacks is logged and left out.</summary>
        private static bool Create(ZNetScene scene, Harmony harmony)
        {
            GameObject? skeleton = scene.GetPrefab(ArsenalKind.All[0].Skeleton);
            if (skeleton == null)
            {
                Log.Error($"Skeleton arsenal not built: the game has no {ArsenalKind.All[0].Skeleton}.");
                return false;
            }
            built = true;
            AssetBundle bundle = EmbeddedBundle.Load(typeof(ArsenalPrefabs).Assembly, Bundle);
            Material skin = ArsenalLook.Skin(skeleton.transform.Find("Visual"));
            ArsenalLook.StringMaterial = BowString(skeleton);
            ArsenalClips.Load(scene);
            ArsenalArrow.Build(scene, bundle, skin);
            ArsenalAtgeirHold.Use(bundle);
            ArsenalItems.Build(scene, bundle, skin);
            foreach (ArsenalKind kind in ArsenalKind.All)
            {
                Create(kind, scene, bundle, skin, harmony);
            }
            Items().ToList().ForEach(item => ItemPrefabs.Register(harmony, item));
            ArsenalRecipes.Install(ObjectDB.instance);
            return true;
        }

        private static void Create(ArsenalKind kind, ZNetScene scene, AssetBundle bundle, Material skin, Harmony harmony)
        {
            GameObject? skeleton = scene.GetPrefab(kind.Skeleton);
            Humanoid? humanoid = skeleton != null ? skeleton.GetComponent<Humanoid>() : null;
            if (humanoid == null)
            {
                Log.Warn($"Arsenal skeletons of {kind.Skeleton} not built: the game has no such skeleton.");
                return;
            }
            foreach (ArsenalWeapon weapon in ArsenalWeapon.All)
            {
                GameObject? attack = ArsenalAttack.Build(kind, weapon, humanoid, EmbeddedBundle.Prefab(bundle, weapon.Model), skin, ArsenalArrow.SkeletonShot);
                if (attack != null)
                {
                    ItemPrefabs.Register(harmony, attack);
                    kind.Add(weapon, ArsenalCreature.Build(kind, weapon, skeleton!, attack, ArsenalItems.Spine));
                }
            }
        }

        /// <summary>The skeleton archer's bowstring material (its bow's "Cube"), before any bow is stripped.</summary>
        private static Material? BowString(GameObject skeleton)
        {
            string bow = ArsenalKind.All[0].Bow;
            GameObject? game = skeleton.GetComponent<Humanoid>()?.m_randomWeapon.FirstOrDefault(i => i != null && i.name == bow);
            Transform? cube = game != null ? game.transform.Find("attach/bow/Cube") : null;
            return cube != null ? cube.GetComponent<MeshRenderer>()?.sharedMaterial : null;
        }

        private static IEnumerable<GameObject> Items() =>
            ArsenalItems.Weapons.Values.Concat(new[] { ArsenalItems.Spine, ArsenalArrow.Item }.OfType<GameObject>());

        private static IEnumerable<GameObject> Shots() => new[] { ArsenalArrow.Shot, ArsenalArrow.SkeletonShot }.OfType<GameObject>();

        private static IEnumerable<GameObject> Creatures() => ArsenalKind.All.SelectMany(k => k.Creatures.Values);
    }
}
