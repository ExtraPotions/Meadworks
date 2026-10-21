// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Burrow;
using ExtraPotions.Burrow.Embedded.SyncedConfig;

namespace Burrow.Capacity
{
    /// <summary>
    /// Section "5a. Chest Preview" (parity choices #19 and #59): the panel that lists a chest's contents while you look at
    /// it. Everything here is per player. Settings named after ChestPreview's behavior; no code of that mod is used.
    /// </summary>
    public static class PreviewSettings
    {
        public const string Section = "5a. Chest Preview";

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<KeyboardShortcut> ToggleKey { get; private set; }
        public static ConfigEntry<bool> ShowItemIcons { get; private set; }
        public static ConfigEntry<bool> ShowQuality { get; private set; }
        public static ConfigEntry<PreviewSort> SortBy { get; private set; }
        public static ConfigEntry<bool> SortDescending { get; private set; }
        public static ConfigEntry<bool> CombineStacks { get; private set; }
        public static ConfigEntry<int> Columns { get; private set; }
        public static ConfigEntry<int> MaxEntries { get; private set; }
        public static ConfigEntry<float> ColumnWidth { get; private set; }
        public static ConfigEntry<float> PositionX { get; private set; }
        public static ConfigEntry<float> PositionY { get; private set; }
        public static ConfigEntry<float> Alpha { get; private set; }
        public static ConfigEntry<float> BackgroundAlpha { get; private set; }
        public static ConfigEntry<string> CapacityFormat { get; private set; }
        public static ConfigEntry<bool> HideEmpty { get; private set; }

        public static void Bind(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(Section, "Enabled", true,
                "Looking at a container you could open shows a panel with its contents: item icons, names, quality and counts, and the used slots beside its name. Dungeon and world chests are shown too. While the panel shows a chest, the hover text leaves its content lines out.", synced: false);
            ToggleKey = synced.Bind(Section, "Toggle Key", KeyboardShortcut.Empty,
                "Turns the chest preview on and off. Unbound by default; the F8 Meadworks menu has the same switch.", synced: false);
            ShowItemIcons = synced.Bind(Section, "Show Item Icons", true, "Each row starts with the item's icon.", synced: false, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            ShowQuality = synced.Bind(Section, "Show Quality", true, "Items above quality 1 show a star and their quality.", synced: false, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            SortBy = synced.Bind(Section, "Sort By", PreviewSort.Name,
                "Value (the stack's worth), Weight (the stack's weight), Name or Amount.", synced: false, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            SortDescending = synced.Bind(Section, "Sort Descending", false, "Largest (or Z) first. Off: smallest (or A) first, as Solo lists a chest.", synced: false, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            CombineStacks = synced.Bind(Section, "Combine Stacks", true,
                "Stacks of the same item, quality and variant are shown as one row with their counts added up.", synced: false, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            Columns = synced.Bind(Section, "Columns", 1, "Rows are split over this many columns.", synced: false,
                acceptableValues: new AcceptableValueRange<int>(1, ChestPreviewRules.MaxColumns), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            MaxEntries = synced.Bind(Section, "Max Entries", 24, "At most this many rows, then '+ n more'. 0 shows every row.", synced: false,
                acceptableValues: new AcceptableValueRange<int>(0, 200), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            ColumnWidth = synced.Bind(Section, "Column Width", 284f, "Width of one column of the panel, in interface units.", synced: false,
                acceptableValues: new AcceptableValueRange<float>(120f, 600f), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            PositionX = synced.Bind(Section, "Position X", 0.641f, "The panel's left edge, as a fraction of the screen width.", synced: false,
                acceptableValues: new AcceptableValueRange<float>(0f, 1f), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            PositionY = synced.Bind(Section, "Position Y", 0.551f, "The panel's middle, as a fraction of the screen height from the bottom.", synced: false,
                acceptableValues: new AcceptableValueRange<float>(0f, 1f), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            Alpha = synced.Bind(Section, "Alpha", 1f, "Opacity of the whole panel.", synced: false,
                acceptableValues: new AcceptableValueRange<float>(0.1f, 1f), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            BackgroundAlpha = synced.Bind(Section, "Background Alpha", 0.4f, "Opacity of the panel's background only.", synced: false,
                acceptableValues: new AcceptableValueRange<float>(0f, 1f), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            CapacityFormat = synced.Bind(Section, "Capacity Format", "{0}/{1}",
                "Beside the chest's name: {0} used slots, {1} all slots, {2} percent used. Empty hides it.", synced: false, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
            HideEmpty = synced.Bind(Section, "Hide Preview For Empty Chests", false,
                "On: looking at an empty container shows no panel, only the hover prompt. Off (default): the panel shows the empty chest with its name and capacity. Per player (local); the host's settings do not change it.", synced: false, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Chest Preview: Enabled.") });
        }
    }
}
