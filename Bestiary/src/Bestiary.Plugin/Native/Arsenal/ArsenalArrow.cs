// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// The bone arrow: the players' Bone Arrows (a copy of the game's wood arrows, their projectile a copy of the wood
    /// arrow's) and the arrow the bowmen loose (a copy of the skeleton archer's). Each wears the bundle's bone arrow
    /// (a bone shaft, a vertebra head, feathers) in place of the game's; flight, hits, sounds and trail are the game's.
    /// The players' arrow adds <see cref="Damage"/> pierce (wood arrows 22, flint 27); the
    /// bowman's damage comes from its bow (<see cref="ArsenalAttack"/>).
    /// </summary>
    public static class ArsenalArrow
    {
        public const string ItemName = "ECP_ArrowBone";
        public const string Word = "ecp_arrowbone";
        private const string Look = "ecp_skel_arrow";
        private const string GameLook = "polySurface20";   // the game projectiles' own arrow model
        private const float Length = 1.16f;                // AssetWorkshop ecp_skel_arrow: nock at its origin, point along +Z
        private const float Point = 0.032f;                // the game arrow's point, ahead of its projectile's origin
        private const float Damage = 24f;

        public static GameObject? Item { get; private set; }
        public static GameObject? Shot { get; private set; }
        public static GameObject? SkeletonShot { get; private set; }

        public static void Build(ZNetScene scene, AssetBundle bundle, Material skin)
        {
            GameObject look = EmbeddedBundle.Prefab(bundle, Look);
            SkeletonShot = Projectile(scene.GetPrefab("draugr_bow_projectile"), "ECP_SkeletonArrow_projectile", look, skin);
            Shot = Projectile(scene.GetPrefab("bow_projectile"), ItemName + "_projectile", look, skin);
            GameObject? game = scene.GetPrefab("ArrowWood");
            if (game == null || game.transform.Find("model") == null || Shot == null)
            {
                Log.Warn("Bone arrows not built: the game has no ArrowWood with a model, or no bow_projectile.");
                return;
            }
            Item = Arrows(game, look, skin, bundle);
        }

        private static GameObject? Projectile(GameObject? game, string name, GameObject look, Material skin)
        {
            if (game == null)
            {
                Log.Warn($"Skeleton arsenal: the game has no arrow projectile for {name}; it is not built.");
                return null;
            }
            GameObject shot = PrefabBench.Copy(game, name);
            LODGroup? lod = shot.GetComponent<LODGroup>();
            if (lod != null)
            {
                Object.DestroyImmediate(lod);
            }
            Transform? old = shot.transform.Find(GameLook);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
            ArsenalLook.Wear(shot.transform, look, skin).transform.localPosition = new Vector3(0f, 0f, Point - Length);
            return shot;
        }

        private static GameObject Arrows(GameObject game, GameObject look, Material skin, AssetBundle bundle)
        {
            GameObject item = PrefabBench.Copy(game, ItemName);
            Transform slot = item.transform.Find("model");
            ArsenalLook.Strip(slot);
            GameObject arrow = ArsenalLook.Wear(slot, look, skin);
            arrow.transform.localPosition = new Vector3(0f, 0f, -Length / 2f);
            ArsenalLook.Collide(slot, arrow);
            ItemDrop drop = item.GetComponent<ItemDrop>();
            drop.m_itemData.m_shared.m_attack.m_attackProjectile = Shot;
            drop.m_itemData.m_shared.m_damages = new HitData.DamageTypes { m_pierce = Damage };
            ArsenalItems.Describe(drop, Word, bundle.LoadAsset<Sprite>(Look + "_icon"));
            return item;
        }
    }
}
