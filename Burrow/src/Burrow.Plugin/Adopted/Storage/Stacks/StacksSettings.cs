using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Burrow.Embedded.SyncedConfig;

namespace Burrow.Stacks
{
    /// <summary>Section "4. Stacks", all synced. Values are read at use time.</summary>
    public static class StacksSettings
    {
        public const string Section = "4. Stacks";
        public const string StackSection = "4a. Item Stacks";
        public const string WeightSection = "4b. Item Weights";

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<float> StackMultiplier { get; private set; }
        public static ConfigEntry<float> WeightMultiplier { get; private set; }
        public static ConfigEntry<bool> IgnoreTeleportRestriction { get; private set; }
        public static ConfigEntry<bool> MergeIntoChests { get; private set; }
        public static ConfigEntry<bool> PerItemConfigEntries { get; private set; }

        public static void Bind(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(Section, "Enabled", true,
                "Master switch of the Stacks module. Off: every item keeps its vanilla stack and weight, portals and chest merging are vanilla.");
            StackMultiplier = synced.Bind(Section, "Stack Multiplier", 4f,
                "Every stackable item's maximum stack is multiplied by this (rounded, at least 1). Items with a vanilla stack of 1 stay 1. Default 4 with Weight Multiplier 0.25: four times the stack, and a full stack weighs what a vanilla full stack weighs (Wood 200 at 0.5 instead of 50 at 2). Burrow.Stacks.yml may replace it.",
                acceptableValues: new AcceptableValueRange<float>(0.01f, 1000f), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Stacks: Enabled.") });
            WeightMultiplier = synced.Bind(Section, "Weight Multiplier", 0.25f,
                "Every stackable item's weight (vanilla stack above 1) is multiplied by this; items with a vanilla stack of 1 (tools, weapons, armor) keep their weight. Default 0.25 goes with Stack Multiplier 4, so a full stack weighs the same as in vanilla. Burrow.Stacks.yml may replace it.",
                acceptableValues: new AcceptableValueRange<float>(0f, 100f), tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Stacks: Enabled.") });
            IgnoreTeleportRestriction = synced.Bind(Section, "Ignore Teleport Restriction", false,
                "The portal check accepts every item.");
            MergeIntoChests = synced.Bind(Section, "Merge Into Chests", true,
                "A stack dragged into an open container fills that container's partial stacks of the same item first; the remainder goes to the slot it was dropped on.", tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Stacks: Enabled.") });
            PerItemConfigEntries = synced.Bind(Section, "Per Item Config Entries", false,
                "Generates one <prefab>.Stack and <prefab>.Weight entry per item into sections 4a and 4b when the item database loads, for Configuration Manager users. 0 or the vanilla value means not set. Burrow.Stacks.yml wins where it names the same item.");
        }
    }
}
