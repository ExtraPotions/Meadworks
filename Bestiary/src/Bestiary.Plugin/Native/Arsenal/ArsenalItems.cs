// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// The skeleton arsenal is every bone weapon of the mod (the user, 2026-09-30): the bone dagger, sword, axe, mace,
    /// spear, atgeir and bow (the players' and the Skeleton Bowman's), the bone arrow, the spine, the Bone Crossbow with
    /// its Blunted Bone Bolts, and the Executioner's Greataxe. It ships in three bundles: <c>ecp_skel_arsenal</c> (built
    /// in this folder), <c>ecp_crossbowman</c> (the Bone Crossbow and its bolts, built beside the crossbowman:
    /// <see cref="Crossbow.XbowItem"/>, <see cref="Crossbow.XbowBolts"/>) and <c>ecp_headsman</c> (the greataxe, built
    /// beside the Executioner: <see cref="Headsman.GreataxeItems"/>), which those creatures share.
    ///
    /// Builds the players' bone weapons and the spine. Each weapon is a copy of the bronze-age weapon of its class
    /// (<see cref="ArsenalWeapon.GameItem"/>), so it keeps that weapon's handling, animations, skill, sounds, trail,
    /// durability, weight and upgrades; the game's model comes off its "attach" and the bone one goes in, with a box
    /// collider round it for the ground and the upgrade glow reshaped to it. Its damage, and what each upgrade adds, is
    /// that weapon's times <see cref="DamageFactor"/>: like bronze, a little weaker. The
    /// spine is a copy of the game's bone fragments (a material: its weight, stack and sounds) wearing the bundle's
    /// spine. Every item shows the bundle's icon.
    /// </summary>
    public static class ArsenalItems
    {
        public const string SpineName = "ECP_Spine";
        public const string SpineWord = "ecp_spine";
        private const string GameSpine = "BoneFragments";
        private const float DamageFactor = 0.85f;

        /// <summary>The weapons built, by weapon; one the game lacks the model for is missing.</summary>
        public static readonly Dictionary<ArsenalWeapon, GameObject> Weapons = new Dictionary<ArsenalWeapon, GameObject>();

        public static GameObject? Spine { get; private set; }

        /// <summary>
        /// A recipe's item by prefab name; the spine even before `db` lists it (the item registration may run after the
        /// recipe), null while it is not built.
        /// </summary>
        public static ItemDrop? Find(ObjectDB db, string name)
        {
            GameObject? prefab = name == SpineName ? Spine : db.GetItemPrefab(name);
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        }

        public static void Build(ZNetScene scene, AssetBundle bundle, Material skin)
        {
            Spine = BuildSpine(scene.GetPrefab(GameSpine), bundle, skin);
            foreach (ArsenalWeapon weapon in ArsenalWeapon.All)
            {
                GameObject? item = BuildWeapon(scene.GetPrefab(weapon.GameItem), weapon, bundle, skin);
                if (item != null)
                {
                    Weapons[weapon] = item;
                }
            }
        }

        private static GameObject? BuildWeapon(GameObject? game, ArsenalWeapon weapon, AssetBundle bundle, Material skin)
        {
            if (game == null || game.transform.Find("attach") == null)
            {
                Log.Warn($"Bone {weapon.Key.ToLowerInvariant()} not built: the game has no {weapon.GameItem} with an attach.");
                return null;
            }
            GameObject item = PrefabBench.Copy(game, weapon.Item);
            Transform slot = item.transform.Find("attach");
            ArsenalLook.Clear(slot, "UpgraderGlow", "equiped");
            GameObject model = ArsenalLook.Wear(slot, EmbeddedBundle.Prefab(bundle, weapon.PlayerModel), skin);
            if (weapon.IsBow)
            {
                ArsenalLook.String(slot, ArsenalLook.PlayerBow, skin);
            }
            ArsenalLook.Glow(slot, ArsenalLook.Collide(slot, model));
            ItemDrop drop = item.GetComponent<ItemDrop>();
            drop.m_itemData.m_shared.m_damages.Modify(DamageFactor);
            drop.m_itemData.m_shared.m_damagesPerLevel.Modify(DamageFactor);
            Describe(drop, weapon.Word, bundle.LoadAsset<Sprite>(weapon.Icon));
            return item;
        }

        private static GameObject? BuildSpine(GameObject? game, AssetBundle bundle, Material skin)
        {
            if (game == null || game.transform.Find("attach") == null)
            {
                Log.Error($"Spine not built: the game has no {GameSpine} with an attach; the recipes go without it.");
                return null;
            }
            GameObject item = PrefabBench.Copy(game, SpineName);
            Transform slot = item.transform.Find("attach");
            ArsenalLook.Strip(slot);
            ArsenalLook.Collide(slot, ArsenalLook.Wear(slot, EmbeddedBundle.Prefab(bundle, "ecp_spine"), skin));
            Describe(item.GetComponent<ItemDrop>(), SpineWord, bundle.LoadAsset<Sprite>("ecp_spine_icon"));
            return item;
        }

        /// <summary>The item's own name, description and icon (the game item's icon when the bundle lacks one).</summary>
        public static void Describe(ItemDrop drop, string word, Sprite? icon)
        {
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            shared.m_name = "$item_" + word;
            shared.m_description = "$item_" + word + "_description";
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            else
            {
                Log.Warn($"Skeleton arsenal: the bundle has no icon for {drop.name}; it shows the game item's.");
            }
        }
    }
}
