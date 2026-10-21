using BepInEx.Configuration;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using UnityEngine;

namespace Burrow.Reach
{
    /// <summary>
    /// Section "1. Reach". Gameplay settings are synced and locked; keys, display and colours are per player.
    /// Values are read at use time.
    /// </summary>
    public static class ReachSettings
    {
        public const string Section = "1. Reach";
        public const string DefaultColour = "#6fc3ff";

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<float> Range { get; private set; }
        public static ConfigEntry<bool> Crafting { get; private set; }
        public static ConfigEntry<bool> Building { get; private set; }
        public static ConfigEntry<bool> Upgrading { get; private set; }
        public static ConfigEntry<bool> FeedStations { get; private set; }
        public static ConfigEntry<KeyboardShortcut> FillModifier { get; private set; }
        public static ConfigEntry<KeyboardShortcut> PullModifier { get; private set; }
        public static ConfigEntry<KeyboardShortcut> ToggleKey { get; private set; }
        public static ConfigEntry<bool> ShowLinks { get; private set; }
        public static ConfigEntry<KeyboardShortcut> LinkKey { get; private set; }
        public static ConfigEntry<float> LinkSeconds { get; private set; }
        public static ConfigEntry<RequirementDisplay> Display { get; private set; }
        public static ConfigEntry<string> StorageColour { get; private set; }
        public static ConfigEntry<bool> FlashOnPull { get; private set; }

        public static void Initialize(SyncedConfiguration synced)
        {
            BindGameplay(synced);
            BindPlayer(synced);
        }

        private static void BindGameplay(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(Section, "Enabled", true,
                "Master switch of the Reach module: crafting, building and station feeding from nearby containers.");
            Range = synced.Bind(Section, "Range", 30f,
                "Meters from the player within which containers are reachable for crafting, building and station feeding. Burrow.Reach.yml may override it per container prefab.");
            Crafting = synced.Bind(Section, "Crafting", true,
                "Recipes in the crafting panel, and Epic Loot's enchanting table when it is installed, may consume from containers.");
            Building = synced.Bind(Section, "Building", true,
                "Pieces placed with the hammer, hoe and cultivator may consume from containers.");
            Upgrading = synced.Bind(Section, "Upgrading", true,
                "Item upgrades in the crafting panel may consume from containers.");
            FeedStations = synced.Bind(Section, "Feed Stations", true,
                "Smelters, kilns, blast furnaces, spinning wheels, windmills, fires, torches, hot tubs, cooking stations, ovens and fermenters take ore, fuel, food or base mead from containers when the player interacts with them and carries none.");
        }

        private static void BindPlayer(SyncedConfiguration synced)
        {
            FillModifier = synced.Bind(Section, "Fill Modifier", new KeyboardShortcut(KeyCode.LeftShift),
                "Held while interacting: fills a station until full or resources run out; aimed at a chest, quick-stacks matching carried items into that chest without opening it.", false);
            PullModifier = synced.Bind(Section, "Pull Modifier", KeyboardShortcut.Empty,
                "Held while interacting with a station or clicking Craft: the matching materials are moved into the player's inventory instead of being consumed from containers.", false);
            ToggleKey = synced.Bind(Section, "Toggle Key", KeyboardShortcut.Empty,
                "Toggles the whole module for this player; the state is remembered per character and shown in the center message.", false);
            ShowLinks = synced.Bind(Section, "Show Links", true,
                "When a container is placed, lines are drawn from it to every station within range for Link Seconds; also on Link Key.", false);
            LinkKey = synced.Bind(Section, "Link Key", KeyboardShortcut.Empty,
                "Draws the lines from every container in range to their stations for Link Seconds.", false);
            LinkSeconds = synced.Bind(Section, "Link Seconds", 5f,
                "Seconds the link lines stay on screen: the lines from containers to the stations they supply, and the Find Key markers.", false);
            Display = synced.Bind(Section, "Requirement Display", RequirementDisplay.Split,
                "How a requirement amount is shown when containers hold some: Split \"3 + 12\", Total \"15\", Vanilla inventory only.", false);
            StorageColour = synced.Bind(Section, "Storage Color", DefaultColour,
                "Color (hex, #rrggbb) of the amount from containers in the crafting and building panels and of the link lines.", false);
            FlashOnPull = synced.Bind(Section, "Flash On Pull", true,
                "The requirement rows that were paid from containers flash once after crafting or placing.", false);
        }

        /// <summary>The storage colour as a hex string usable in a rich text colour tag; the default when the setting is not a colour.</summary>
        public static string ColourHex()
        {
            string value = (StorageColour.Value ?? "").Trim();
            return ColorUtility.TryParseHtmlString(value, out _) ? value : DefaultColour;
        }

        public static Color Colour()
        {
            return ColorUtility.TryParseHtmlString(ColourHex(), out Color colour) ? colour : Color.cyan;
        }
    }
}
