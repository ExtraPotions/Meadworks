// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the detail
// icon backdrop is left to Kindle while its Salvage tab is shown.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Display.Backdrops;
using ExtraPotions.Inscribe.Adopted.Rules;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ExtraPotions.Inscribe.Runtime;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// The crafting panel's upgrade tab (display.md section 2). The game describes the selected recipe from the recipe
    /// prefab's data, not the player's item, so an upgrade target's rarity and affixes would never show there.
    /// <list type="bullet">
    /// <item>The selected recipe's description: while <c>InventoryGui.UpdateRecipe</c> runs, the upgrade target is
    /// remembered, and <see cref="TooltipPatch"/> appends the target's cached affix block to the prefab tooltip the
    /// game asks for. The description text then comes out identical frame to frame, so the label is not rebuilt.</item>
    /// <item>The selected recipe's name label, and each upgrade entry in the recipe list: colored through the label's
    /// <c>color</c> property, not a tag. The game rewrites the name text every frame; a tag would change the text twice
    /// per frame and rebuild the label, while an unchanged color is a compare and nothing else.</item>
    /// <item>The rarity backdrop behind the selected upgrade's large icon and an upgrade entry's icon
    /// (<see cref="IconBackdrop"/>), from the same two patches.</item>
    /// </list>
    /// Runs on the local player's client only, every frame while the panel is open: two field reads, one cached state
    /// lookup, no allocation.
    /// </summary>
    internal static class CraftingPanel
    {
        private static ItemDrop.ItemData? _recipeItem;
        private static ItemDrop.ItemData? _target;
        private static TMP_Text? _nameLabel;
        private static Color _nameBase;

        /// <summary>
        /// The item whose affix block belongs under a crafting tooltip: the upgrade target when the game is describing
        /// the selected recipe's prefab data, otherwise the item itself.
        /// </summary>
        public static ItemDrop.ItemData Shown(ItemDrop.ItemData item)
        {
            return _target != null && ReferenceEquals(item, _recipeItem) ? _target : item;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
        internal static class UpdateRecipePatch
        {
            [HarmonyPrefix]
            private static void Prefix(InventoryGui __instance)
            {
                Recipe? recipe = __instance.m_selectedRecipe.Recipe;
                ItemDrop? prefab = recipe == null ? null : recipe.m_item;
                ItemDrop.ItemData? target = __instance.m_selectedRecipe.ItemData;
                bool upgrading = prefab != null && target != null;
                _recipeItem = upgrading ? prefab!.m_itemData : null;
                _target = upgrading ? target : null;
            }

            // Kindle's Salvage tab draws the detail icon and its backdrop (capability meadworks.crafting.salvage_tab v1.0,
            // a Func<bool> that is true while the tab is shown). Clearing the backdrop there would hide what Kindle shows
            // and switch one Image off and on every frame.
            private static readonly CapabilityLink<System.Func<bool>> SalvageTab =
                new CapabilityLink<System.Func<bool>>("meadworks.crafting.salvage_tab", new System.Version(1, 0));

            [HarmonyPostfix]
            private static void Postfix(InventoryGui __instance)
            {
                ColorName(__instance.m_recipeName, _target);
                Recipe? recipe = __instance.m_selectedRecipe.Recipe;
                if (recipe != null || !SalvageTabShown())
                {
                    IconBackdrop.Set(__instance.m_recipeIcon, recipe != null ? __instance.m_selectedRecipe.ItemData : null);
                }
                _recipeItem = null;
                _target = null;
            }

            private static bool SalvageTabShown()
            {
                try
                {
                    return CruciblePlugin.IsReady
                        && SalvageTab.TryGet(CruciblePlugin.Services.Capabilities, out var active)
                        && active != null && active();
                }
                catch (System.Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// An upgrade entry in the recipe list, built on list rebuilds only (tab, station, inventory change): its name in
        /// the rarity color and its icon's backdrop. The game makes every entry anew, so a plain item's needs nothing.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.AddRecipeToList))]
        internal static class RecipeListPatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryGui __instance, ItemDrop.ItemData item, bool canCraft)
            {
                int count = __instance.m_availableRecipes.Count;
                RarityDef? rarity = item == null || count == 0 ? null : ItemState.Read(item).Rarity;
                GameObject? element = item == null || count == 0 ? null : __instance.m_availableRecipes[count - 1].InterfaceElement;
                if (element == null)
                {
                    return;
                }
                Transform icon = element.transform.Find("icon");
                if (icon != null)
                {
                    IconBackdrop.Set(icon.GetComponent<Image>(), item);
                }
                ColorEntry(element.transform.Find("name"), rarity, canCraft);
            }
        }

        // The game greys an entry the player cannot afford; keep that cue by dimming the rarity color alike.
        private static void ColorEntry(Transform? name, RarityDef? rarity, bool canCraft)
        {
            if (!InscribeModules.Active(InscribeModule.Display) || !InscribeModules.StepAside.RarityDisplay) return;
            TMP_Text? label = name == null ? null : name.GetComponent<TMP_Text>();
            if (label != null && RarityPalette.TryNameColor(rarity, out Color color))
            {
                label.color = canCraft ? color : new Color(color.r * 0.66f, color.g * 0.66f, color.b * 0.66f, 1f);
            }
        }

        // The label's own color is remembered the first time it is seen, and restored for plain and new-craft recipes.
        private static void ColorName(TMP_Text? label, ItemDrop.ItemData? target)
        {
            if (label == null)
            {
                return;
            }
            if (!ReferenceEquals(label, _nameLabel))
            {
                _nameLabel = label;
                _nameBase = label.color;
            }
            Color wanted = _nameBase;
            if (target != null && InscribeModules.Active(InscribeModule.Display) && InscribeModules.StepAside.RarityDisplay
                && RarityPalette.TryNameColor(ItemState.Read(target).Rarity, out Color rarity))
            {
                wanted = rarity;
            }
            if (label.color != wanted)
            {
                label.color = wanted;
            }
        }
    }
}
