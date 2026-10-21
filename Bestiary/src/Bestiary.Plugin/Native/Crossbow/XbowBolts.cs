// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// The players' Blunted Bone Bolts, one of the skeleton arsenal (<see cref="Arsenal.ArsenalItems"/>): crossbow ammo
    /// made from bone fragments at the workbench (<see cref="XbowRecipe"/>),
    /// for the Bone Crossbow above all, though any crossbow shoots them. A copy of the game's bone bolts (the same ammo
    /// type, stack and weight) wearing the bundle's blunted bone bolt, in the hand and in flight; they add
    /// <see cref="XbowItemSettings.BoltDamage"/> blunt to the crossbow's own blow, as ammo does. The crossbowmen drop them.
    /// Its own inventory icon shows the blunt knuckle head and featherless bone shaft.
    /// </summary>
    public static class XbowBolts
    {
        public const string ItemName = "ECP_BoltBoneBlunt";
        public const string Word = "ecp_boltboneblunt";
        private const string GameItem = "BoltBone", GameLook = "default", Icon = "ecp_xbow_bolt_icon";

        public static GameObject? Item { get; private set; }
        public static GameObject? Shot { get; private set; }

        public static void Build(ZNetScene scene, GameObject gameBolt, GameObject look, Material skin, AssetBundle bundle)
        {
            GameObject? game = scene.GetPrefab(GameItem);
            if (game == null || game.transform.Find(GameLook) == null)
            {
                Log.Warn($"Blunted Bone Bolts not built: the game has no {GameItem} with a model.");
                return;
            }
            Shot = XbowBolt.Build(gameBolt, ItemName + "_projectile", false, look, skin);
            Item = PrefabBench.Copy(game, ItemName);
            Wear(Item.transform.Find(GameLook), look, skin);
            ItemDrop drop = Item.GetComponent<ItemDrop>();
            drop.m_itemData.m_dropPrefab = Item;
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            (shared.m_name, shared.m_description) = ("$item_" + Word, "$item_" + Word + "_description");
            shared.m_attack.m_attackProjectile = Shot;
            Sprite? icon = bundle.LoadAsset<Sprite>(Icon);
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            else
            {
                Log.Warn($"Blunted Bone Bolts: the bundle has no {Icon}; using the game's bone bolt icon.");
            }
            Apply();
        }

        /// <summary>
        /// The game's bolt model off (its collider stays, for lying on the ground), ours on along the same line: the game's
        /// mesh runs along its own +Y, turned into the item by the slot, and ours along +Z from its nock.
        /// </summary>
        private static void Wear(Transform slot, GameObject look, Material skin)
        {
            Mesh? mesh = slot.GetComponent<MeshFilter>()?.sharedMesh;
            Vector3 along = slot.localRotation * Vector3.up;
            Vector3 middle = slot.localPosition + slot.localRotation * (mesh != null ? mesh.bounds.center : Vector3.zero);
            Object.DestroyImmediate(slot.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(slot.GetComponent<MeshFilter>());
            GameObject ours = XbowBolt.Wear(slot.parent, look, skin, slot.gameObject.layer);
            ours.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, along);
            ours.transform.localPosition = middle - along * (XbowBolt.Size / 2f);
        }

        /// <summary>The damage from the settings onto the shared data every stack of these bolts shares.</summary>
        public static void Apply()
        {
            if (Item != null)
            {
                Item.GetComponent<ItemDrop>().m_itemData.m_shared.m_damages = new HitData.DamageTypes { m_blunt = XbowItemSettings.BoltDamage };
            }
        }
    }
}
