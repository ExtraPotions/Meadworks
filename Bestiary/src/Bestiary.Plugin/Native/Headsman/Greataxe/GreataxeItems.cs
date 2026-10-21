// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The players' part of the Executioner: its axehead, which it drops by the settings' chance, and the Executioner's
    /// Greataxe made from it at the workbench (<see cref="GreataxeRecipe"/>), one of the skeleton arsenal
    /// (<see cref="Arsenal.ArsenalItems"/>). The greataxe is a copy of the game's
    /// Battleaxe, so it keeps the Battleaxe's handling (two hands, its stance, its three-swing combo and secondary, the
    /// Axes skill, its place on the back, its swing and hit sounds) with the bundle's bone greataxe in its hands
    /// (AssetWorkshop Greataxe/GreataxeModel: held near the butt, its haft aimed through the left fist) and on the ground;
    /// its swings are the Executioner's own (<see cref="GreataxeHold"/>, <see cref="GreataxeSwing"/>). It hits and chops like a fully
    /// upgraded Bronze Axe and is not upgraded. The axehead is a copy of the game's bone fragments wearing the bundle's
    /// axehead. Both wear the Skeleton's bone material with the axe's texture, as the Executioner's axe does.
    /// </summary>
    public static class GreataxeItems
    {
        public const string AxeName = "ECP_ExecutionerGreataxe", HeadName = "ECP_ExecutionerAxehead";
        public const string AxeWord = "ecp_executionergreataxe", HeadWord = "ecp_executioneraxehead";
        private const string GameAxe = "Battleaxe", GameMaterial = "BoneFragments", ModelName = "ecp_greataxe";
        private const string Held = "ecp_greataxe_held", Head = "ecp_greataxe_axehead", AxeIcon = "ecp_greataxe_icon", HeadIcon = "ecp_greataxe_axehead_icon";

        public static GameObject? Axe { get; private set; }
        public static GameObject? Axehead { get; private set; }

        /// <summary>The greataxe's name hash, as the players' equipment shows what is in a hand.</summary>
        public static int Hash { get; } = AxeName.GetStableHashCode();

        /// <summary>The greataxe and the axehead, for ZNetScene.</summary>
        public static IEnumerable<GameObject> NetPrefabs => new[] { Axe, Axehead }.Where(p => p != null).Select(p => p!);

        public static void Build(ZNetScene scene, Harmony harmony, AssetBundle bundle, Humanoid skeleton)
        {
            Material skin = HeadsmanKit.Skin(skeleton.transform.Find("Visual"));
            GameObject? axe = scene.GetPrefab(GameAxe), bones = scene.GetPrefab(GameMaterial);
            if (axe == null || bones == null || axe.transform.Find("attach") == null || bones.transform.Find("attach") == null)
            {
                Log.Error($"Executioner's Greataxe not built: the game's {GameAxe} or {GameMaterial} is missing or has no attach.");
                return;
            }
            Axehead = Make(bones, HeadName, HeadWord, EmbeddedBundle.Prefab(bundle, Head).transform, bundle.LoadAsset<Sprite>(HeadIcon), skin);
            Axe = Make(axe, AxeName, AxeWord, EmbeddedBundle.Prefab(bundle, Held).transform.Find(ModelName), bundle.LoadAsset<Sprite>(AxeIcon), skin);
            Arm(Axe.GetComponent<ItemDrop>().m_itemData.m_shared, Axe.transform.Find("attach"));
            ItemPrefabs.Register(harmony, Axehead);
            ItemPrefabs.Register(harmony, Axe);
            GreataxeRecipe.Install(ObjectDB.instance);
        }

        /// <summary>A copy of the game's item wearing `model` in its attach, named, described and with its icon.</summary>
        private static GameObject Make(GameObject game, string name, string word, Transform model, Sprite? icon, Material skin)
        {
            GameObject item = PrefabBench.Copy(game, name);
            Transform attach = item.transform.Find("attach");
            GreataxeLook.Wear(attach, model, skin);
            ItemDrop drop = item.GetComponent<ItemDrop>();
            drop.m_itemData.m_dropPrefab = item;
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            (shared.m_name, shared.m_description) = ("$item_" + word, "$item_" + word + "_description");
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            else
            {
                Log.Warn($"Crypt Executioner: the bundle has no icon for {name}; it shows the game's.");
            }
            return item;
        }

        /// <summary>The greataxe's own numbers (<see cref="Reapply"/>) and its swing trail along our blade.</summary>
        private static void Arm(ItemDrop.ItemData.SharedData shared, Transform attach)
        {
            shared.m_maxQuality = 1;
            shared.m_toolTier = 2;
            GreataxeLook.Trail(attach);
            Apply(shared);
        }

        private static void Apply(ItemDrop.ItemData.SharedData shared)
        {
            shared.m_damages = new HitData.DamageTypes { m_slash = GreataxeSettings.Slash, m_chop = GreataxeSettings.Chop };
            shared.m_damagesPerLevel = new HitData.DamageTypes();
        }

        /// <summary>After a settings change: the damage on the prefab. Existing greataxes copy it from there. The recipe follows.</summary>
        public static void Reapply()
        {
            if (Axe != null)
            {
                Apply(Axe.GetComponent<ItemDrop>().m_itemData.m_shared);
            }
            GreataxeRecipe.Install(ObjectDB.instance);
        }
    }
}
