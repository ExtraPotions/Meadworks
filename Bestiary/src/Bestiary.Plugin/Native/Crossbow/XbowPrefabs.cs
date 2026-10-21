// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// Stand-in registration from EliteCreaturesPack 0.9.0 (ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6).
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// Builds the Skeleton Crossbowmen once and registers them whenever ZNetScene wakes, identically on the server and
    /// every client: for each of the game's archer skeletons (<see cref="XbowKind"/>) a crossbowman (a copy of that
    /// skeleton wearing the crossbow and quiver from the embedded bundle and playing the bundle's clips in place of the
    /// archer's) and its shot (a copy of that skeleton's bow); and the one bolt they all fire (a copy of the game's bone
    /// bolt projectile wearing the bundle's bone bolt); and the players' Bone Crossbow (<see cref="XbowItem"/>). The bundle
    /// comes from AssetWorkshop (assets/ecp_crossbowman);
    /// every material is the game's own Skeleton material wearing the bundle's textures.
    /// </summary>
    public static class XbowPrefabs
    {
        public const string Bolt = "ECP_SkeletonCrossbowman_bolt";
        public const string Bundle = "ecp_crossbowman";
        private const string Kit = "ecp_xbow_kit";
        private const string BoltLook = "ecp_xbow_bolt";
        private const string GameBolt = "arbalest_projectile_bone";

        private static GameObject? bolt;

        public static void Install(Harmony harmony)
        {
            NetPrefabs.OnSceneAwake(harmony, scene => Guard.Run("XbowPrefabs.Build", () => Build(scene, harmony)));
            Settings.Changed += () => SafeCall.Run("crossbowman settings", XbowShot.ApplyToAll);
            Settings.Changed += () => SafeCall.Run("bone crossbow settings", Reprice);
        }

        private static void Build(ZNetScene scene, Harmony harmony)
        {
            if (bolt == null && !Create(scene, harmony))
            {
                return;
            }
            NetPrefabs.Register(scene, bolt!);
            foreach (XbowKind kind in XbowKind.All.Where(k => k.Prefab != null))
            {
                NetPrefabs.Register(scene, XbowShot.Of(kind)!);
                NetPrefabs.Register(scene, kind.Prefab!);
            }
            foreach (GameObject item in new[] { XbowItem.Prefab, XbowBolts.Item, XbowBolts.Shot }.OfType<GameObject>())
            {
                NetPrefabs.Register(scene, item);
            }
            XbowWords.Add(Localization.instance);
            Log.Info($"Skeleton crossbowmen ready: {string.Join(", ", XbowKind.All.Where(k => k.Prefab != null).Select(k => k.Creature))}.");
        }

        /// <summary>
        /// The bolt, then a crossbowman for every archer skeleton the game has; a missing one is logged and left out,
        /// and nothing is built without the bolt (nothing else is held up either way).
        /// </summary>
        private static bool Create(ZNetScene scene, Harmony harmony)
        {
            GameObject? gameBolt = scene.GetPrefab(GameBolt), skeleton = scene.GetPrefab(XbowKind.All[0].Skeleton);
            if (gameBolt == null || skeleton == null)
            {
                Log.Error($"Skeleton crossbowmen not built: the game has no {(gameBolt == null ? GameBolt : XbowKind.All[0].Skeleton)}.");
                return false;
            }
            AssetBundle bundle = EmbeddedBundle.Load(typeof(XbowPrefabs).Assembly, Bundle);
            Material skin = XbowKit.Skin(skeleton.transform.Find("Visual"));
            bolt = XbowBolt.Build(gameBolt, Bolt, true, EmbeddedBundle.Prefab(bundle, BoltLook), skin);
            XbowBolts.Build(scene, gameBolt, EmbeddedBundle.Prefab(bundle, BoltLook), skin, bundle);
            XbowRig.Click = scene.GetPrefab("sfx_reload_done");
            foreach (XbowKind kind in XbowKind.All)
            {
                Create(kind, scene, harmony, bundle);
            }
            Players(scene, harmony, bundle, skin);
            return true;
        }

        /// <summary>The players' side: the Bone Crossbow and its bolts, their reload clips and rig, the recipes.</summary>
        private static void Players(ZNetScene scene, Harmony harmony, AssetBundle bundle, Material skin)
        {
            XbowItem.Build(scene, bundle);
            XbowHold.Use(bundle);
            XbowPlayerRig.Use(EmbeddedBundle.Prefab(bundle, BoltLook), skin);
            foreach (GameObject item in new[] { XbowItem.Prefab, XbowBolts.Item }.OfType<GameObject>())
            {
                ItemPrefabs.Register(harmony, item);
            }
            XbowRecipe.Install(ObjectDB.instance);
        }

        /// <summary>After a settings change: the Bone Crossbow's and its bolts' numbers and their recipes.</summary>
        private static void Reprice()
        {
            XbowItemSettings.Apply(XbowItem.Prefab != null ? XbowItem.Prefab.GetComponent<ItemDrop>() : null);
            XbowBolts.Apply();
            XbowRecipe.Install(ObjectDB.instance);
        }

        private static void Create(XbowKind kind, ZNetScene scene, Harmony harmony, AssetBundle bundle)
        {
            GameObject? skeleton = scene.GetPrefab(kind.Skeleton);
            GameObject? bow = skeleton?.GetComponent<Humanoid>()?.m_randomWeapon.FirstOrDefault(item => item != null && item.name == kind.Bow);
            if (skeleton == null || bow == null)
            {
                Log.Warn($"Skeleton crossbowman {kind.Creature} not built: the game has no {(skeleton == null ? kind.Skeleton : kind.Bow)}.");
                return;
            }
            GameObject shot = XbowShot.Build(kind, bow, bolt!, scene.GetPrefab("sfx_arbalest_fire"));
            ItemPrefabs.Register(harmony, shot);
            kind.Prefab = XbowCreature.Build(kind, skeleton, shot, bundle, EmbeddedBundle.Prefab(bundle, Kit), XbowBolts.Item ?? scene.GetPrefab("BoltBone"));
            StandIns.Add(kind.Prefab, kind.Skeleton);
        }
    }
}
