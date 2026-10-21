using ExtraPotions.Mantle.Embedded.YamlConfig;

namespace Mantle.Crafting
{
    /// <summary>
    /// The keys every crafted item's YAML entry shares, each optional: <c>station</c> (a crafting station's prefab name),
    /// <c>level</c> (1 to 10) and <c>cost</c> (prefab:amount pairs); a key left out keeps the default. Plus the range
    /// checks the item files use for their own numbers: a value out of range is an error, which rejects the file set so
    /// the previous values stay.
    /// </summary>
    public static class RecipeYaml
    {
        public static string Station(YamlNode node, CraftStats defaults) =>
            node.Get("station").TryString(out string s) && s.Trim().Length > 0 ? s.Trim() : defaults.Station;

        public static int Level(YamlNode node, CraftStats defaults) => Whole(node.Get("level"), 1, 10, defaults.Level);

        public static string Cost(YamlNode node, CraftStats defaults) =>
            node.Get("cost").TryString(out string c) && c.Trim().Length > 0 ? c.Trim() : defaults.Cost;

        public static int Whole(YamlNode node, int min, int max, int fallback)
        {
            if (!node.TryInt(out int value))
                return fallback;
            if (value >= min && value <= max)
                return value;
            node.Error($"must be a whole number from {min} to {max}, found {value}");
            return fallback;
        }

        public static float Number(YamlNode node, float min, float max, float fallback)
        {
            if (!node.TryFloat(out float value))
                return fallback;
            if (value >= min && value <= max)
                return value;
            node.Error($"must be a number from {min} to {max}, found {value}");
            return fallback;
        }
    }
}
