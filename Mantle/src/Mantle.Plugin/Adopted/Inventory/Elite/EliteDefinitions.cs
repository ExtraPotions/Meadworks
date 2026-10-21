namespace Mantle.Elite
{
    /// <summary>
    /// What Mantle hands EliteCrafting through its public API (EliteCrafting's <c>features/api.md</c>), as the JSON
    /// definitions the API takes (the field names of EliteCrafting's YAML format 2, <c>classes-and-tiers.md</c>). Kept
    /// as constants so a check outside the game can read exactly what the mod registers.
    /// </summary>
    internal static class EliteDefinitions
    {
        /// <summary>The item class of the eight backpacks: rune-made magic only, never a pre-rolled drop.</summary>
        public const string ClassId = "backpack";

        /// <summary>The extra slots a pack's inscriptions give: an item-scope effect EliteCrafting rolls and sums, Mantle applies.</summary>
        public const string SlotsEffect = "pack_slots";

        public const string DeepPocketsId = "deep_pockets";

        /// <summary>EliteCrafting's item-data keys all start with this (its <c>item-data.md</c>); nothing else of it is read here.</summary>
        public const string ItemKeyPrefix = "ecf_";

        /// <summary>The id the worn pack's equipment provider is registered under.</summary>
        public const string ProviderId = "mantle";

        public const string ClassJson =
            "{ \"id\": \"backpack\", \"group\": \"jewel\", \"name\": \"$mantle_class_backpack\", \"rolls\": true, \"drop_weight\": 0 }";

        public const string EffectJson =
            "{ \"id\": \"pack_slots\", \"scope\": \"item\", \"value\": \"flat\", \"polarity\": \"raise\", \"cap\": 8, "
            + "\"description\": \"Mantle: inventory slots a worn backpack adds\" }";

        public const string DeepPocketsJson =
            "{ \"id\": \"deep_pockets\", \"name\": \"$mantle_affix_deep_pockets\", \"effect\": \"pack_slots\", \"value\": \"flat\", "
            + "\"affix\": \"prefix\", \"family\": \"charms_fortune\", \"category\": \"utility\", "
            + "\"classes\": { \"best\": [\"backpack\"], \"allowed\": [] }, \"tiers\": { \"count\": 4, \"from\": 1, \"min\": 1, \"max\": 4 } }";

        /// <summary>EliteCrafting's own inscriptions that may also roll on a backpack: best fit (every tier open) or allowed.</summary>
        public static readonly (string Id, bool Best)[] Pool =
        {
            ("broad_back", true),
            ("pack_mule", true),
            ("lightened", false),
            ("gossamer", false),
            ("magpie", false),
            ("harvester", false),
        };
    }
}
