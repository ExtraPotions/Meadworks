// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour follows OpenKeep 4.1.0 (Recipe List: a row of category buttons under the search filters the recipes);
// Meadworks-original code on the Crucible kit (KitIconButton, KitListShrink); the icons are the game's own item icons.
using System.Collections.Generic;
using UnityEngine;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Kindle.Recipes;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ItemType = ItemDrop.ItemData.ItemType;

namespace ExtraPotions.Kindle.Runtime
{
    public static partial class KindleRecipes
    {
        /// <summary>A game item per category whose icon the button shows, in <see cref="RecipeCategories.All"/>'s order (Kindle ships no image).</summary>
        static readonly string[] categoryIcons = { "ArrowWood", "SwordIron", "Bow", "HelmetBronze", "ShieldWood", "Hammer", "CookedMeat", "Wood" };
        static readonly Color categoryDim = new Color(0.5f, 0.5f, 0.5f, 0.75f);
        static readonly KitListShrink categoryShrink = new KitListShrink();
        static readonly List<KitIconButton> categoryButtons = new List<KitIconButton>();
        static RectTransform categoryRow;
        static RecipeCategory picked = RecipeCategory.None;

        /// <summary>The picked category buttons while the row shows; none otherwise (then every recipe is listed).</summary>
        public static RecipeCategory PickedCategories => categoryRow && categoryRow.gameObject.activeSelf ? picked : RecipeCategory.None;

        /// <summary>A recipe's category is its item's type: food is a consumable that feeds, armour takes helmets, chests, legs, capes and utilities, weapons whatever the game calls a weapon that is not a bow.</summary>
        static RecipeCategory CategoryOf(ItemDrop.ItemData item)
        {
            var shared = item?.m_shared;
            if (shared == null) return RecipeCategory.None;
            switch (shared.m_itemType)
            {
                case ItemType.Consumable: return shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f || shared.m_foodRegen > 0f ? RecipeCategory.Food : RecipeCategory.None;
                case ItemType.Material: return RecipeCategory.Materials;
                case ItemType.Bow: return RecipeCategory.Bows;
                case ItemType.Helmet: case ItemType.Chest: case ItemType.Legs: case ItemType.Shoulder: case ItemType.Utility: return RecipeCategory.Armor;
                case ItemType.Ammo: case ItemType.AmmoNonEquipable: return RecipeCategory.Ammo;
                case ItemType.Shield: return RecipeCategory.Shields;
                case ItemType.Tool: return RecipeCategory.Tools;
                default: return item.IsWeapon() ? RecipeCategory.Weapons : RecipeCategory.None;
            }
        }

        /// <summary>The row beside the search row, built hidden; one square kit button per category with the game's item icon and a tooltip.</summary>
        static void BuildCategories()
        {
            if (categoryRow || search == null || !search.List) return;
            var list = search.List;
            var go = new GameObject("Kindle_recipe_categories", typeof(RectTransform));
            go.layer = list.gameObject.layer;
            categoryRow = (RectTransform)go.transform;
            categoryRow.SetParent(list.parent, false);
            categoryRow.SetSiblingIndex(search.Row.GetSiblingIndex() + 1);
            categoryRow.anchorMin = categoryRow.anchorMax = categoryRow.pivot = new Vector2(0f, 1f);
            for (int i = 0; i < RecipeCategories.All.Count; i++)
            {
                var category = RecipeCategories.All[i];
                var word = category.ToString().ToLowerInvariant();
                var button = KitIconButton.Create(categoryRow, "Kindle_recipe_category_" + word, KitGlyph.Bars, Vector2.zero, KitSearchLayout.Button,
                    _ => Guard(() => PickCategory(category)));
                if (button == null) continue;
                var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(categoryIcons[i]) : null;
                var drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
                if (drop) button.SetSprite(drop.m_itemData.GetIcon(), false);
                button.Tooltip(KindleText.Text("recipe_cat_" + word), KindleText.Text("recipe_cat_tip"));
                categoryButtons.Add(button);
            }
            go.SetActive(false);
        }

        static void PickCategory(RecipeCategory category)
        {
            if (!Active) return;
            picked = RecipeCategories.Toggle(picked, category);
            var gui = InventoryGui.instance;
            var scroll = gui && gui.m_recipeListRoot ? gui.m_recipeListRoot.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (scroll) scroll.verticalNormalizedPosition = 1f;
            RefreshCategories();
            Rebuild();
        }

        /// <summary>
        /// Every update: the search row and the category row each take their height off the top of the list. The category
        /// row gives its height back first, the search row then sizes the list as it left it, and the category row takes
        /// its height again under it, so neither ever shrinks the list twice.
        /// </summary>
        static void TickRows(InventoryGui gui, bool showSearch)
        {
            var list = search.List;
            bool showCategories = config.Get(KindleSettings.RecipeList.Categories) && !CustomTabSelected(gui);
            if (showCategories) BuildCategories();
            // The round below moves the list three times and ends where it started when nothing changed. Moving it each frame
            // rebuilt the recipe list's layout and the inventory's canvas every frame (playtest 2026-10-20, item 25), so with the
            // rows shown as last time and the list as this left it, the result is already in place.
            bool rowMatches = categoryRow ? categoryRow.gameObject.activeSelf == showCategories : !showCategories;
            if (steady && list && list.anchoredPosition == steadyPosition && list.sizeDelta == steadySize && showSearch == steadySearch
                && showCategories == steadyCategories && search.Shown == showSearch && rowMatches)
            {
                if (showCategories && categoryRow) { LayoutCategories(); RefreshCategories(); }
                return;
            }
            ShrinkForCategories(gui, list, false);
            search.Tick(showSearch);
            if (categoryRow)
            {
                if (categoryRow.gameObject.activeSelf != showCategories) categoryRow.gameObject.SetActive(showCategories);
                ShrinkForCategories(gui, list, showCategories);
                if (showCategories) { LayoutCategories(); RefreshCategories(); }
            }
            steady = list;
            if (!steady) return;
            steadyPosition = list.anchoredPosition;
            steadySize = list.sizeDelta;
            steadySearch = showSearch;
            steadyCategories = showCategories;
        }

        static bool steady, steadySearch, steadyCategories;
        static Vector2 steadyPosition, steadySize;

        static void ShrinkForCategories(InventoryGui gui, RectTransform list, bool on)
        {
            if (!list || (!on && !categoryShrink.Shrunk)) return;
            var result = categoryShrink.Apply(KitUnity.Vector(list.anchoredPosition), KitUnity.Vector(list.sizeDelta), on);
            if (categoryRow)
            {
                categoryRow.anchoredPosition = KitUnity.Vector(result.RowPosition);
                categoryRow.sizeDelta = KitUnity.Vector(result.RowSize);
            }
            if (!result.Write) return;
            list.anchoredPosition = KitUnity.Vector(result.ListPosition);
            list.sizeDelta = KitUnity.Vector(result.ListSize);
            if (gui) gui.m_recipeListBaseSize = list.sizeDelta.y;
        }

        /// <summary>Square buttons as large as the search row's, spread over the row's width (smaller when the row is narrow).</summary>
        static void LayoutCategories()
        {
            int count = categoryButtons.Count;
            if (count == 0) return;
            float width = categoryRow.sizeDelta.x;
            float size = Mathf.Floor(Mathf.Min(KitSearchLayout.Button, (width - 2f * (count - 1)) / count));
            float gap = count > 1 ? (width - size * count) / (count - 1) : 0f;
            for (int i = 0; i < count; i++)
            {
                var rect = (RectTransform)categoryButtons[i].Button.transform;
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = new Vector2(Mathf.Round(i * (size + gap)), -(KitSearchLayout.Height - size) / 2f);
            }
        }

        /// <summary>Lit while picked; with none picked every icon is bright, else the others are dimmed.</summary>
        static void RefreshCategories()
        {
            for (int i = 0; i < categoryButtons.Count && i < RecipeCategories.All.Count; i++)
            {
                var button = categoryButtons[i];
                bool on = RecipeCategories.IsPicked(picked, RecipeCategories.All[i]);
                if (button.On != on) button.SetOn(on);
                if (button.Glyph) button.Glyph.color = picked == RecipeCategory.None || on ? Color.white : categoryDim;
            }
        }

        /// <summary>Kindle inactive: both rows hide and give the list its height back, the category row first.</summary>
        static void HideRows()
        {
            if (search == null) return;
            steady = false;
            ShrinkForCategories(owner, search.List, false);
            if (categoryRow && categoryRow.gameObject.activeSelf) categoryRow.gameObject.SetActive(false);
            search.Tick(false);
        }

        /// <summary>Gives the list its height back and removes the row (before the search row is detached).</summary>
        static void DetachCategories()
        {
            steady = false;
            if (search != null) ShrinkForCategories(owner, search.List, false);
            categoryShrink.Reset();
            categoryButtons.Clear();
            if (categoryRow) KitUnity.Retire(categoryRow.gameObject);
            categoryRow = null;
        }
    }
}
