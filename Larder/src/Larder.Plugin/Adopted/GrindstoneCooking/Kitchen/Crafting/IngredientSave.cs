// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Crafting/IngredientSave.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The ingredient-save perk. After a craft at a kitchen crafting station, each crafted batch (1, or all of a
    /// multi-craft, so crafting five at once saves as often as five single crafts) has
    /// <see cref="Perks.IngredientSaveChance"/> of giving back one unit of an ingredient it used up. The ingredient is
    /// one of the used-up requirements picked at random, the unit a copy of one of its used-up units, so it comes
    /// back as it went in (a fish with its quality). It goes into the inventory, or to the ground at the
    /// crafter's feet when there is no room, and a short message says what was saved. Nothing used up, nothing saved:
    /// a failed craft and the no-cost cheat never give anything.
    /// </summary>
    public static class IngredientSave
    {
        public static void Roll(Player player, int batches, List<CraftRecord.Lot> lots)
        {
            float chance = Perks.IngredientSaveChance(CookLevel.Local());
            if (chance <= 0f)
                return;
            for (int batch = 0; batch < batches && lots.Count > 0; batch++)
            {
                if (UnityEngine.Random.value < chance)
                    GiveBack(player, TakeUnit(lots));
            }
        }

        /// <summary>Takes one used-up unit out of the lots: a random ingredient, then a random unit of it.</summary>
        private static ItemDrop.ItemData TakeUnit(List<CraftRecord.Lot> lots)
        {
            List<string> names = lots.Select(lot => lot.Item.m_shared.m_name).Distinct().ToList();
            string name = names[UnityEngine.Random.Range(0, names.Count)];
            List<CraftRecord.Lot> ofName = lots.Where(lot => lot.Item.m_shared.m_name == name).ToList();
            int pick = UnityEngine.Random.Range(0, ofName.Sum(lot => lot.Count));
            CraftRecord.Lot chosen = ofName.Last();
            foreach (CraftRecord.Lot lot in ofName)
            {
                if (pick < lot.Count)
                {
                    chosen = lot;
                    break;
                }
                pick -= lot.Count;
            }
            return Unit(lots, chosen);
        }

        /// <summary>One unit copied from the lot, which then holds one unit fewer.</summary>
        private static ItemDrop.ItemData Unit(List<CraftRecord.Lot> lots, CraftRecord.Lot lot)
        {
            lot.Count--;
            if (lot.Count <= 0)
                lots.Remove(lot);
            ItemDrop.ItemData unit = lot.Item.Clone();
            unit.m_stack = 1;
            return unit;
        }

        private static void GiveBack(Player player, ItemDrop.ItemData unit)
        {
            string name = Localization.instance.Localize(unit.m_shared.m_name);
            if (!player.GetInventory().AddItem(unit))
                CraftDrop.AtFeet(player, unit, 1);
            player.Message(MessageHud.MessageType.TopLeft, LarderText.Format("ingredient_saved", name), 0, unit.GetIcon());
        }
    }
}
