// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// Section 7: the players' Bone Crossbow. Whether it can be made, what it costs at the workbench (a placeholder
    /// until the recipe is settled), and its own blow and reload. Its other numbers are fixed: a workbench weapon, three
    /// quality levels, 100 durability (+50 a level), 2 weight, bolts flying at 60 m/s (the Arbalest's: 200), and no
    /// recoil pushing the shooter back (the Arbalest's does). The bolt it looses adds its own damage and projectile, as
    /// with any crossbow. Synced; a change reaches the crossbows already made and the recipe at once.
    /// </summary>
    public static class XbowItemSettings
    {
        public const string Section = "7 - Bone Crossbow";

        private static GatedEntry<bool> craftable = null!;
        private static GatedEntry<string> recipe = null!;
        private static GatedEntry<int> workbenchLevel = null!;
        private static GatedEntry<float> damage = null!;
        private static GatedEntry<float> damagePerLevel = null!;
        private static GatedEntry<float> reloadTime = null!;
        private static GatedEntry<string> boltRecipe = null!;
        private static GatedEntry<int> boltsPerCraft = null!;
        private static GatedEntry<float> boltDamage = null!;

        public static bool Craftable => craftable.Value;
        public static string Recipe => recipe.Value;
        public static int WorkbenchLevel => workbenchLevel.Value;
        public static string BoltRecipe => boltRecipe.Value;
        public static int BoltsPerCraft => boltsPerCraft.Value;
        public static float BoltDamage => boltDamage.Value;

        public static void Initialize(NativeConfiguration config)
        {
            BindCrossbow(config);
            BindBolts(config);
        }

        private static void BindCrossbow(NativeConfiguration config)
        {
            craftable = config.Bind(Section, "Craftable", true,
                "The Bone Crossbow, the crossbowmen's crossbow of bones, can be made at the workbench.");
            recipe = config.Bind(Section, "Recipe", "ECP_Spine:1, Wood:10:5, BoneFragments:12:6, LeatherScraps:4:2",
                "What it costs, as item:amount:amount per upgrade, separated by commas (a placeholder for now). Item names are "
                + "the game's prefab names, like BoneFragments, Wood, DeerHide, TrophySkeleton; the spine is ECP_Spine.");
            Settings.Renew(recipe, "Wood:10:5, BoneFragments:12:6, LeatherScraps:4:2");
            workbenchLevel = config.Bind(Section, "Workbench Level", 2, "The workbench level it needs (its Blunted Bone Bolts too).",
                acceptableValues: new AcceptableValueRange<int>(1, 5));
            damage = config.Bind(Section, "Damage", 30f,
                "Blunt damage of its own blow, before the bolt's (a Blunted Bone Bolt adds 27 blunt, the game's bone bolt 32 pierce; the Arbalest: 200 pierce).",
                acceptableValues: Settings.Range(0f, 500f));
            damagePerLevel = config.Bind(Section, "Damage Per Level", 4f, "Blunt damage added by each upgrade.",
                acceptableValues: Settings.Range(0f, 100f));
            reloadTime = config.Bind(Section, "Reload Time", 2.3f,
                "Seconds to span and load it with no Crossbows skill; the skill halves it (the Arbalest: 3.5).",
                acceptableValues: Settings.Range(0.5f, 20f));
            Settings.Renew(reloadTime, 3f);
        }

        /// <summary>The Blunted Bone Bolts: its ammo, made at the same workbench level.</summary>
        private static void BindBolts(NativeConfiguration config)
        {
            boltRecipe = config.Bind(Section, "Bolt Recipe", "BoneFragments:8",
                "What a batch of Blunted Bone Bolts costs, as item:amount, separated by commas; made at the crossbow's workbench level.");
            boltsPerCraft = config.Bind(Section, "Bolts Per Craft", 20, "Blunted Bone Bolts made at once.",
                acceptableValues: new AcceptableValueRange<int>(1, 100));
            boltDamage = config.Bind(Section, "Bolt Damage", 27f,
                "Blunt damage a Blunted Bone Bolt adds to the crossbow's own: a flint arrow's, as blunt (flint arrows 27 pierce; the game's bone bolt 32 pierce).",
                acceptableValues: Settings.Range(0f, 500f));
        }

        /// <summary>The numbers onto the item's shared data, which every Bone Crossbow in the world shares.</summary>
        public static void Apply(ItemDrop? drop)
        {
            if (drop == null)
            {
                return;
            }
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            shared.m_damages = new HitData.DamageTypes { m_blunt = damage.Value };
            shared.m_damagesPerLevel = new HitData.DamageTypes { m_blunt = damagePerLevel.Value };
            shared.m_attack.m_reloadTime = reloadTime.Value;
            shared.m_attack.m_projectileVel = 60f;   // the best bows' full draw
            shared.m_attack.m_recoilPushback = 0f;   // the Arbalest shoves its shooter back; this one does not
            shared.m_toolTier = 0;
            shared.m_maxQuality = 3;
            shared.m_maxDurability = 100f;
            shared.m_durabilityPerLevel = 50f;
            shared.m_weight = 2f;
            shared.m_attackForce = 60f;
            drop.m_itemData.m_durability = shared.m_maxDurability;
        }
    }
}
