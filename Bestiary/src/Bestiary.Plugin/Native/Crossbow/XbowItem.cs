// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// The players' Bone Crossbow, one of the skeleton arsenal (<see cref="Arsenal.ArsenalItems"/>): the crossbowmen's
    /// crossbow of bones, made at the workbench (<see cref="XbowRecipe"/>).
    /// A copy of the game's Arbalest, so it keeps the game's crossbow handling: held in the left hand, the crossbow
    /// animations, the reload (a minor action, its loaded state in the player's ZDO for everyone to see), the Crossbows
    /// skill, bolts as ammo (their damage and projectile added to its own), its place on the back. The Arbalest's
    /// "attach" holds an "Unloaded" and a "Loaded" model that the game's WeaponLoadState swaps; ours go in their place
    /// (AssetWorkshop Crossbow/XbowItemModels: string let go; spanned with a blunt bolt laid), the collider is fitted to
    /// our size and the Arbalest's upgrade glow, laid along its own 1.7 m, comes off, as does the ring of smoke it fires
    /// with. It is the crossbowmen's crossbow at 1.25 times their size (1.04 m, a 0.9 m prod), so the left hand holds
    /// the fore-stock and the right the grip where the game's crossbow animations put the hands on the Arbalest's. Its
    /// own blow is blunt, it is a workbench weapon (<see cref="XbowItemSettings"/>) and it chops no trees (the Arbalest
    /// does).
    /// </summary>
    public static class XbowItem
    {
        public const string PrefabName = "ECP_BoneCrossbow";
        public const string Word = "ecp_bonecrossbow";
        private const string GameCrossbow = "CrossbowArbalest";
        private const string Icon = "ecp_xbow_crossbow_icon", RigModel = "ecp_xbow_item_rig";
        private const string FireSmoke = "vfx_arbalest_fire";
        private const float Scale = 1.25f;   // the skeletons' crossbow is sized for them; in a player's hands a little bigger
        private static readonly Vector3 BoxCentre = new Vector3(0f, 0.005f, 0.03f) * Scale, BoxSize = new Vector3(0.73f, 0.11f, 0.81f) * Scale;

        /// <summary>The item prefab, once built; null until the first ZNetScene wakes, or when the game has no Arbalest.</summary>
        public static GameObject? Prefab { get; private set; }

        public static void Build(ZNetScene scene, AssetBundle bundle)
        {
            Transform? attach = scene.GetPrefab(GameCrossbow)?.transform.Find("attach");
            MeshRenderer? look = attach?.Find("Unloaded")?.GetComponent<MeshRenderer>();
            if (attach == null || look == null || attach.Find("Loaded") == null)
            {
                Log.Error($"Bone crossbow not built: the game's {GameCrossbow} is missing or no longer has attach/Unloaded/Loaded.");
                return;
            }
            GameObject item = PrefabBench.Copy(attach.parent.gameObject, PrefabName);
            Transform holder = item.transform.Find("attach");
            Rig(holder, EmbeddedBundle.Prefab(bundle, RigModel), look.sharedMaterial);
            Collide(holder.Find("Collider"));
            Object.DestroyImmediate(holder.Find("UpgraderGlow")?.gameObject);
            Describe(item.GetComponent<ItemDrop>(), bundle);
            DropFireSmoke(item.GetComponent<ItemDrop>());
            Prefab = item;
            XbowItemSettings.Apply(item.GetComponent<ItemDrop>());
        }

        /// <summary>
        /// The Arbalest's "Unloaded" and "Loaded" emptied (the game's WeaponLoadState still toggles them, to no effect) and
        /// our rigged crossbow in a slot of its own beside them, always shown: its string and bolts are moved by
        /// <see cref="XbowPlayerRig"/> as the player shoots and reloads. Its groove bolt is the game bone bolt's length
        /// (the crossbow is at 1.25, the bolt at 1) and hidden until the crossbow is loaded.
        /// </summary>
        private static void Rig(Transform holder, GameObject model, Material look)
        {
            Clear(holder.Find("Unloaded"));
            Clear(holder.Find("Loaded"));
            var slot = new GameObject(XbowPlayerRig.Slot).transform;
            slot.SetParent(holder, false);
            slot.gameObject.layer = holder.gameObject.layer;
            Wear(slot, model, look);
            Transform? bolt = GameMaterials.Find(slot, XbowPlayerRig.GrooveBolt);
            if (bolt != null)
            {
                bolt.localScale = Vector3.one / Scale;
                bolt.gameObject.SetActive(false);
            }
        }

        private static void Clear(Transform slot)
        {
            Object.DestroyImmediate(slot.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(slot.GetComponent<MeshFilter>());
            foreach (Transform child in slot.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            (slot.localPosition, slot.localRotation, slot.localScale) = (Vector3.zero, Quaternion.identity, Vector3.one);
        }

        /// <summary>Our model in at the slot's origin, dressed.</summary>
        private static void Wear(Transform slot, GameObject model, Material look)
        {
            GameObject copy = Object.Instantiate(model, slot, false);
            copy.name = "model";
            copy.transform.localScale = Vector3.one * Scale;
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = slot.gameObject.layer;
            }
            XbowKit.Dress(copy, look);
        }

        /// <summary>A box round our crossbow in place of the Arbalest's mesh collider, for the item lying on the ground.</summary>
        private static void Collide(Transform? holder)
        {
            if (holder == null)
            {
                return;
            }
            Object.DestroyImmediate(holder.GetComponent<MeshCollider>());
            (holder.localPosition, holder.localRotation, holder.localScale) = (Vector3.zero, Quaternion.identity, Vector3.one);
            var box = holder.gameObject.AddComponent<BoxCollider>();
            (box.center, box.size) = (BoxCentre, BoxSize);
        }

        /// <summary>The Arbalest's ring of smoke at the shot comes off; its camera shake and its sound stay.</summary>
        private static void DropFireSmoke(ItemDrop drop)
        {
            EffectList fire = drop.m_itemData.m_shared.m_triggerEffect;
            fire.m_effectPrefabs = fire.m_effectPrefabs.Where(e => e.m_prefab == null || e.m_prefab.name != FireSmoke).ToArray();
        }

        private static void Describe(ItemDrop drop, AssetBundle bundle)
        {
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            shared.m_name = "$item_" + Word;
            shared.m_description = "$item_" + Word + "_description";
            Sprite? icon = bundle.LoadAsset<Sprite>(Icon);
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            else
            {
                Log.Warn($"Bone crossbow: the bundle has no {Icon}; it shows the Arbalest's icon.");
            }
        }
    }
}
