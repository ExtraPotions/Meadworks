// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; runes on
// claimed cell accessories (rings, belts).
using System;
using System.Collections.Generic;
using ItemType = ItemDrop.ItemData.ItemType;
using SkillType = Skills.SkillType;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// The classification itself (classes-and-tiers.md section 1), run once per item type: (1) the first class whose
    /// <c>items</c> (or a claim) names the prefab, (2) the first classifier callback that names a class, (3) the first
    /// class in order with a <c>match</c> rule the item meets, (4) none. A rune is never classified; PackPanel's
    /// backpacks never meet a match rule (steps 1 and 2 only). Hands, traits and governing skills are computed beside
    /// the class, as before.
    /// </summary>
    internal static class ClassClassifier
    {
        /// <summary>
        /// The shared name prefix of PackPanel's backpacks. They are Utility items, but worn in PackPanel's own backpack
        /// slot rather than the game's utility field, so the <c>utility</c> class's type rule must not take them; a
        /// class that lists them, or a mod's claim or classifier, can.
        /// </summary>
        private const string PackPanelBackpack = "$packpanel_backpack_";

        public static ClassInfo Classify(ItemDrop.ItemData item, string? prefab, ClassIndex index)
        {
            ItemDrop.ItemData.SharedData shared = item.m_shared;
            if (ItemClasses.IsStoneName(prefab, shared.m_name))
            {
                return ClassInfo.None;
            }
            ItemClass? found = Find(item, prefab, index);
            return found == null ? ClassInfo.None : new ClassInfo(found, HandsOf(shared.m_itemType), TraitsOf(shared), SkillsOf(shared));
        }

        /// <summary>
        /// Steps 1-3: the item's class, without the rune check and the class info around it. Step 3 takes equippable
        /// items only: a mead base or the dragon egg keeps the game's default skill (Swords) and would match sword_1h.
        /// </summary>
        public static ItemClass? Find(ItemDrop.ItemData item, string? prefab, ClassIndex index)
        {
            if (prefab != null && index.ByPrefab.TryGetValue(prefab, out ItemClass listed))
            {
                return listed;
            }
            ItemClass? asked = ClassRegistry.Ask(item, index);
            if (asked != null || IsBackpack(item.m_shared) || !item.IsEquipable())
            {
                return asked;
            }
            return FirstMatch(item.m_shared, prefab, index.Classes);
        }

        internal static ClassInfo ForRune(ItemDrop.ItemData item, string? prefab, ClassIndex index)
        {
            var shared = item.m_shared;
            if ((!item.IsEquipable() && !CellAccessory(item, prefab, index)) || shared.m_itemType == ItemType.Ammo || ItemClasses.IsStoneName(prefab, shared.m_name)) return ClassInfo.None;
            var found = Find(item, prefab, index);
            if (found == null || !found.Rolls && RuneFallback(found.Id))
            {
                found = null;
                foreach (var candidate in index.Classes)
                {
                    if (RuneFallback(candidate.Id)) continue;
                    foreach (var rule in candidate.Match)
                        if (rule.Meets(shared, prefab)) { found = candidate; break; }
                    if (found != null) break;
                }
            }
            return found?.Rolls == true ? new ClassInfo(found, HandsOf(shared.m_itemType), TraitsOf(shared), SkillsOf(shared)) : ClassInfo.None;
        }

        /// <summary>
        /// A single misc item a mod claimed by prefab into a jewel class (Mantle's rings and belts, which lie in their cells
        /// and are never worn): runes may target it like equipment (user decision 2026-10-19). Consumables stay refused.
        /// </summary>
        internal static bool CellAccessory(ItemDrop.ItemData item, string? prefab, ClassIndex index) =>
            item.m_shared.m_itemType == ItemType.Misc && item.m_shared.m_maxStackSize == 1 && prefab != null
            && index.ByPrefab.TryGetValue(prefab, out ItemClass claimed) && claimed.Rolls && claimed.Group == "jewel";

        // These built-in classes exclude pre-rolled drops; a carried rune uses the item's normal equipment class.
        static bool RuneFallback(string id) => id == "unused" || id == "throwable" || id == "tankard" || id == "practice" || id == "cosmetic";

        /// <summary>Step 3 alone: the first class in order with a rule the item type meets.</summary>
        public static ItemClass? FirstMatch(ItemDrop.ItemData.SharedData shared, string? prefab, IReadOnlyList<ItemClass> classes)
        {
            for (int i = 0; i < classes.Count; i++)
            {
                IReadOnlyList<ClassMatch> rules = classes[i].Match;
                for (int r = 0; r < rules.Count; r++)
                {
                    if (rules[r].Meets(shared, prefab))
                    {
                        return classes[i];
                    }
                }
            }
            return null;
        }

        private static bool IsBackpack(ItemDrop.ItemData.SharedData shared) =>
            shared.m_name != null && shared.m_name.StartsWith(PackPanelBackpack, StringComparison.Ordinal);

        private static Hands HandsOf(ItemType type)
        {
            switch (type)
            {
                case ItemType.OneHandedWeapon: return Hands.One;
                case ItemType.TwoHandedWeapon:
                case ItemType.TwoHandedWeaponLeft:
                case ItemType.Attach_Atgeir:
                case ItemType.Bow: return Hands.Two;
                default: return Hands.None;
            }
        }

        private static ItemTraits TraitsOf(ItemDrop.ItemData.SharedData shared)
        {
            ItemTraits traits = ItemTraits.None;
            if (shared.m_useDurability) traits |= ItemTraits.WearsOut;
            if (shared.m_movementModifier < 0f) traits |= ItemTraits.MovementPenalty;
            if (shared.m_buildPieces != null) traits |= ItemTraits.Builds;
            if (FiresProjectiles(shared.m_attack)) traits |= ItemTraits.Projectile;
            if (!string.IsNullOrEmpty(shared.m_ammoType)) traits |= ItemTraits.Ammo;
            if (shared.m_itemType == ItemType.Shield && shared.m_timedBlockBonus > 1f) traits |= ItemTraits.CanParry;
            return traits;
        }

        private static bool FiresProjectiles(Attack? attack) =>
            attack != null && (attack.m_attackType == Attack.AttackType.Projectile
                || attack.m_attackType == Attack.AttackType.TriggerProjectile);

        private static IReadOnlyList<SkillType> SkillsOf(ItemDrop.ItemData.SharedData shared)
        {
            List<SkillType> skills = new List<SkillType> { shared.m_skillType };
            if (shared.m_damages.m_chop > 0f && shared.m_skillType != SkillType.WoodCutting)
            {
                skills.Add(SkillType.WoodCutting);
            }
            if (shared.m_damages.m_pickaxe > 0f && shared.m_skillType != SkillType.Pickaxes)
            {
                skills.Add(SkillType.Pickaxes);
            }
            return skills;
        }
    }
}
