// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Kindle.Recipes;
using static ExtraPotions.Kindle.Config.SettingScope;

namespace ExtraPotions.Kindle.Config
{
    /// <summary>
    /// Every Kindle setting from design section 4, in file order. The plugin binds exactly this list; the portable
    /// tests compare it with the spec table. Section names follow OpenKeep's wording without its numbers.
    /// </summary>
    public static class KindleSettings
    {
        public static class RecipeList
        {
            public const string Section = "Recipe List";

            public static readonly KindleSetting<bool> Search = KindleSetting.Toggle(Section, "Search", true, Local,
                "Show a search row above the recipe list on the Craft and Upgrade tabs.");
            public static readonly KindleSetting<string> SearchKey = KindleSetting.Text(Section, "Search Key", "F + LeftControl", Local,
                "Focuses the search field (or opens Steam's on-screen keyboard). Empty: no key.", Shortcut);
            public static readonly KindleSetting<bool> ClearSearchOnClose = KindleSetting.Toggle(Section, "Clear Search On Close", true, Local,
                "Clear the search text when the inventory closes.");
            public static readonly KindleSetting<bool> IgnoreAccents = KindleSetting.Toggle(Section, "Ignore Accents", true, Local,
                "Search ignores accent marks, so plain letters also find accented names.");
            public static readonly KindleSetting<bool> Categories = KindleSetting.Toggle(Section, "Categories", true, Local,
                "A row of category buttons under the search: Ammo, Weapons, Bows, Armor, Shields, Tools, Food and Materials. Click one to list only its recipes, click more to add them, click a lit one to take it away; with none lit every recipe shows.");
            public static readonly KindleSetting<bool> Favourites = KindleSetting.Toggle(Section, "Favorites", true, Local,
                "Allow marking recipes as favorites (middle click, the favorite key or the star button).");
            public static readonly KindleSetting<bool> FavouritesFirst = KindleSetting.Toggle(Section, "Favorites First", true, Local,
                "List favorite recipes before the others, keeping the chosen sort within each group.");
            public static readonly KindleSetting<string> FavouriteRecipeKey = KindleSetting.Text(Section, "Favorite Recipe Key", "", Local,
                "Marks or unmarks the recipe under the pointer. Acts only over a recipe row. Unbound by default; middle click and the star button also mark favorites.", Shortcut);
            public static readonly KindleSetting<RecipeView> RecipeView = KindleSetting.Choice(Section, "Recipe View", Recipes.RecipeView.List, Local,
                "How recipes are laid out: List, CompactList, SmallGrid, MediumGrid or LargeGrid.");
            public static readonly KindleSetting<bool> SortButton = KindleSetting.Toggle(Section, "Sort Button", true, Local,
                "Show the sort button. Off hides the button; the stored sort (the game's sortcraft) still applies.");
            public static readonly KindleSetting<bool> GamepadControls = KindleSetting.Toggle(Section, "Gamepad Controls", true, Local,
                "Lets a gamepad move through and use the search, sort and favorite controls of the recipe list.");
            public static readonly KindleSetting<string> GamepadSortButton = KindleSetting.Text(Section, "Gamepad Sort Button", "JoyBack", Local,
                "Gamepad button that cycles the sort while the crafting panel is selected (a Crucible pad button name). Default Back, which the game leaves free in the inventory; holding the left stick stays the game's multi-craft modifier. Empty: off.", Shortcut);
        }

        public static class Batch
        {
            public const string Section = "Batch Crafting";

            public static readonly KindleSetting<bool> Enabled = KindleSetting.Toggle(Section, "Enabled", true, ServerAuthoritative,
                "Show the amount stepper left of Craft; Craft makes that many through the game's own multi-craft, at the same cost per item.");
            public static readonly KindleSetting<int> MaxAmount = KindleSetting.Integer(Section, "Max Amount", 100, 1, 1000, ServerAuthoritative,
                "The most one press of Craft makes. Materials and free inventory space still limit the amount.");
            public static readonly KindleSetting<float> CraftSpeed = KindleSetting.Number(Section, "Craft Speed", 1f, 0.1f, 10f, Cheat,
                "Cheat. How fast crafting completes: 2 takes half the game's time. Reads 1 whenever the server blocks cheats.", safe: 1f);
            public static readonly KindleSetting<bool> InstantCrafting = KindleSetting.Toggle(Section, "Instant Crafting", false, Cheat,
                "Cheat. Every craft and batch finishes at once, ignoring Craft Speed. Off whenever the server blocks cheats.", safe: false);
        }

        public static class Repair
        {
            public const string Section = "Repair";
            public static readonly KindleSetting<bool> OnStationOpen = KindleSetting.Toggle(Section, "Repair On Station Open", false, Local,
                "Opening a usable crafting station repairs all carried worn items that station can repair, using the same free repair-all action. Off by default.");
        }

        public static class Tracker
        {
            public const string Section = "Recipe Tracker";

            public static readonly KindleSetting<bool> Enabled = KindleSetting.Toggle(Section, "Enabled", true, Local,
                "Show tracked recipes and their materials on the HUD.");
            public static readonly KindleSetting<int> MaxTracked = KindleSetting.Integer(Section, "Max Tracked", 6, 1, 12, Local,
                "The most recipes tracked at once.");
            public static readonly KindleSetting<bool> CountNearbyChests = KindleSetting.Toggle(Section, "Count Nearby Chests", true, Local,
                "Count materials in nearby containers. Needs Burrow; without it only carried items count.");
            public static readonly KindleSetting<bool> UntrackWhenCrafted = KindleSetting.Toggle(Section, "Untrack When Crafted", true, Local,
                "Stop tracking a recipe once the tracked amount has been crafted.");
            public static readonly KindleSetting<bool> HideInCombat = KindleSetting.Toggle(Section, "Hide In Combat", true, Local,
                "Hide the tracker while in combat.");
            public static readonly KindleSetting<bool> HideWithMap = KindleSetting.Toggle(Section, "Hide With Map", true, Local,
                "Hide the tracker while the large map is open.");
            public static readonly KindleSetting<TrackerFont> Font = KindleSetting.Choice(Section, "Font", TrackerFont.Serif, Local,
                "Tracker font: Sans, Serif or Norse.");
            public static readonly KindleSetting<int> FontSize = KindleSetting.Integer(Section, "Font Size", 16, 10, 28, Local,
                "Text size of the recipe tracker on the HUD.");
            public static readonly KindleSetting<string> HaveColour = KindleSetting.Text(Section, "Have Color", "#FFFFFF", Local,
                "Color of materials you have enough of (HTML color, #RRGGBB or #RRGGBBAA).", t => Colour(t, "#FFFFFF"));
            public static readonly KindleSetting<string> MissingColour = KindleSetting.Text(Section, "Missing Color", "#FF6A5A", Local,
                "Color of materials you are missing (HTML color).", t => Colour(t, "#FF6A5A"));
            public static readonly KindleSetting<string> ReadyColour = KindleSetting.Text(Section, "Ready Color", "#FFB65C", Local,
                "Color of a recipe title once everything is gathered (HTML color).", t => Colour(t, "#FFB65C"));
            public static readonly KindleSetting<float> BackgroundOpacity = KindleSetting.Number(Section, "Background Opacity", 1f, 0f, 1f, Local,
                "Opacity of the tracker panel background.");
        }

        public static class Salvage
        {
            public const string Section = "Salvage";

            public static readonly KindleSetting<bool> Enabled = KindleSetting.Toggle(Section, "Enabled", true, ServerAuthoritative,
                "Add a Salvage tab to crafting stations that turns unwanted gear back into materials.");
            public static readonly KindleSetting<float> ReturnFraction = KindleSetting.Number(Section, "Return Fraction", 0.75f, 0f, 1f, ServerAuthoritative,
                "Share of each material's total cost returned by salvage.");
            public static readonly KindleSetting<SalvageRounding> Rounding = KindleSetting.Choice(Section, "Rounding", SalvageRounding.Round, ServerAuthoritative,
                "How a share becomes a whole number: Floor, Round (halves up) or Ceil.");
            public static readonly KindleSetting<bool> AtLeastOne = KindleSetting.Toggle(Section, "At Least One", true, ServerAuthoritative,
                "Every material returns at least one.");
            public static readonly KindleSetting<bool> UpgradeMaterials = KindleSetting.Toggle(Section, "Upgrade Materials", true, ServerAuthoritative,
                "Include the materials spent on upgrades.");
            public static readonly KindleSetting<bool> RequireKnownRecipe = KindleSetting.Toggle(Section, "Require Known Recipe", true, ServerAuthoritative,
                "Only salvage items whose recipe the character knows. Found gear (loot, chest, trader or creature drops, which no one crafted) always counts as known, as does every recipe when the world unlocks all recipes.");
            public static readonly KindleSetting<bool> RequireStation = KindleSetting.Toggle(Section, "Require Station", false, ServerAuthoritative,
                "Only salvage at the item's own crafting station.");
            public static readonly KindleSetting<bool> SkipItemsWithModData = KindleSetting.Toggle(Section, "Skip Items With Mod Data", true, ServerAuthoritative,
                "Never salvage items carrying data from other mods (keys starting with Mod Data Prefixes).");
            public static readonly KindleSetting<string> ModDataPrefixes = KindleSetting.Text(Section, "Mod Data Prefixes", "ecf_", ServerAuthoritative,
                "Comma list of item data key prefixes (case-sensitive) that mark an item as carrying other mods' data.", Config.ModDataPrefixes.Normalize);
            public static readonly KindleSetting<string> SalvageKey = KindleSetting.Text(Section, "Salvage Key", "", Local,
                "Salvages the item under the pointer on the Salvage tab. Unbound by default; the Salvage button does the same.", Shortcut);
            public static readonly KindleSetting<bool> ShowRarityBackgrounds = KindleSetting.Toggle(Section, "Show Rarity Backgrounds", true, Local,
                "Show rarity backgrounds on the Salvage tab (also obeys Epic Loot's own setting).");
            public static readonly KindleSetting<bool> NearbyChests = KindleSetting.Toggle(Section, "Nearby Chests", true, ServerAuthoritative,
                "The Salvage tab also lists gear in the nearby chests Burrow lets you craft from (needs Burrow: its range, wards and chest rules apply, and chests other players have open are skipped). Salvaged materials go to your inventory; what does not fit drops at your feet. Your own value applies in single player; on a server the host's value applies.");
        }

        public static class Cart
        {
            public const string Section = "Cart Workbench";

            public static readonly KindleSetting<bool> CartWorkbench = KindleSetting.Toggle(Section, "Cart Workbench", false, Cheat,
                "Cheat. Every loaded cart carries a workbench; Shift + Use opens it. Off whenever the server blocks cheats.", safe: false);
            public static readonly KindleSetting<int> CartStationLevel = KindleSetting.Integer(Section, "Cart Station Level", 1, 1, 5, ServerAuthoritative,
                "Workbench level of a cart before extensions.");
            public static readonly KindleSetting<float> CartStationRange = KindleSetting.Number(Section, "Cart Station Range", 10f, 1f, 50f, ServerAuthoritative,
                "Range of a cart's workbench in meters.");
        }

        public static class VirtualWorkbench
        {
            public const string Section = "Virtual Workbench";

            public static readonly KindleSetting<bool> Enabled = KindleSetting.Toggle(Section, "Virtual Workbench", false, ServerAuthoritative,
                "Host setting, off by default. Lets players reopen the crafting station they used last from anywhere it is still loaded, with Virtual Workbench Key. That station's own level, extensions, roof and fire rules apply, and crafting uses only carried materials, as at the station.");
            public static readonly KindleSetting<string> Key = KindleSetting.Text(Section, "Virtual Workbench Key", "", Local,
                "Your own key that reopens the crafting station you used last, when the host allows Virtual Workbench. Empty: no key.", Shortcut);
        }

        public static class Stations
        {
            public const string Section = "Crafting Stations";

            public static readonly KindleSetting<float> UseDistance = KindleSetting.Number(Section, "Use Distance Multiplier", 1.5f, 1f, 3f, ServerAuthoritative,
                "How far you can stand from a crafting station and still use it, as a multiple of the game's distance for that station (1 is the game's). Your own value applies in single player; on a server the host's value applies.");
        }

        public static class Server
        {
            public const string Section = "Server";

            public static readonly KindleSetting<string> LockedPlayerSettings = KindleSetting.Text(Section, "Locked Player Settings", "", ServerAuthoritative,
                "Retained compatibility key. Server lock lists no longer override client-local settings.",
                t => (t ?? "").Trim());
        }

        // A holder normalizes its defaults through this containing type. Defer catalog
        // enumeration until all holders are initialized, including under Unity's Mono runtime.
        private static readonly Lazy<KindleSetting[]> all = new Lazy<KindleSetting[]>(() => new KindleSetting[]
        {
            RecipeList.Search, RecipeList.SearchKey, RecipeList.ClearSearchOnClose, RecipeList.IgnoreAccents, RecipeList.Categories, RecipeList.Favourites,
            RecipeList.FavouritesFirst, RecipeList.FavouriteRecipeKey, RecipeList.RecipeView, RecipeList.SortButton,
            RecipeList.GamepadControls, RecipeList.GamepadSortButton,
            Batch.Enabled, Batch.MaxAmount, Batch.CraftSpeed, Batch.InstantCrafting,
            Repair.OnStationOpen,
            Tracker.Enabled, Tracker.MaxTracked, Tracker.CountNearbyChests, Tracker.UntrackWhenCrafted, Tracker.HideInCombat,
            Tracker.HideWithMap, Tracker.Font, Tracker.FontSize, Tracker.HaveColour, Tracker.MissingColour,
            Tracker.ReadyColour, Tracker.BackgroundOpacity,
            Salvage.Enabled, Salvage.ReturnFraction, Salvage.Rounding, Salvage.AtLeastOne, Salvage.UpgradeMaterials,
            Salvage.RequireKnownRecipe, Salvage.RequireStation, Salvage.SkipItemsWithModData, Salvage.ModDataPrefixes,
            Salvage.SalvageKey, Salvage.ShowRarityBackgrounds, Salvage.NearbyChests,
            Cart.CartWorkbench, Cart.CartStationLevel, Cart.CartStationRange,
            VirtualWorkbench.Enabled, VirtualWorkbench.Key,
            Stations.UseDistance,
            Server.LockedPlayerSettings,
        });

        private static readonly Lazy<Dictionary<string, KindleSetting>> byId = new Lazy<Dictionary<string, KindleSetting>>(
            () => all.Value.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every setting in file order.</summary>
        public static IReadOnlyList<KindleSetting> All => all.Value;

        /// <summary>A setting by <c>Section.Key</c> in any case; null when unknown.</summary>
        public static KindleSetting? Find(string? id) => id != null && byId.Value.TryGetValue(id.Trim(), out var setting) ? setting : null;

        /// <summary>Shortcut and pad button texts are parsed by the plugin; the core only trims them (empty means off).</summary>
        private static string Shortcut(string text) => (text ?? "").Trim();

        private static string Colour(string text, string fallback) => HtmlColour.TryParse(text, out var colour) ? colour.ToString() : fallback;
    }
}
