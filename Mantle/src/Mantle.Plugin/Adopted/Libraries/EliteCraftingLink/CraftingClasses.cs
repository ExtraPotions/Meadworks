using System;

namespace ExtraPotions.Mantle.Embedded.EliteCraftingLink
{
    /// <summary>
    /// Item classes and item levels (api.md section 2). A class definition is a JSON object with the fields of the
    /// economy YAML's <c>classes</c> entries: <c>id</c>, <c>group</c>, <c>name</c>, <c>rolls</c>, <c>damage_scale</c>,
    /// <c>drop_weight</c>, <c>match</c>, <c>items</c>. Without EliteCrafting every call answers false, null or 0.
    /// </summary>
    public static class CraftingClasses
    {
        private static readonly Endpoint<Func<string, bool>> registerItemClass = new Endpoint<Func<string, bool>>("RegisterItemClass");
        private static readonly Endpoint<Func<string, string[], bool>> claimItems = new Endpoint<Func<string, string[], bool>>("ClaimItems");
        private static readonly Endpoint<Func<string, Func<ItemDrop.ItemData, string?>, bool>> registerClassifier =
            new Endpoint<Func<string, Func<ItemDrop.ItemData, string?>, bool>>("RegisterClassifier");
        private static readonly Endpoint<Func<string, bool>> unregisterClassifier = new Endpoint<Func<string, bool>>("UnregisterClassifier");
        private static readonly Endpoint<Func<string, int, bool>> setItemLevel = new Endpoint<Func<string, int, bool>>("SetItemLevel");
        private static readonly Endpoint<Func<ItemDrop.ItemData, string?>> getItemClass = new Endpoint<Func<ItemDrop.ItemData, string?>>("GetItemClass");
        private static readonly Endpoint<Func<ItemDrop.ItemData, int>> getItemLevel = new Endpoint<Func<ItemDrop.ItemData, int>>("GetItemLevel");

        /// <summary>Adds or replaces an item class (under the YAML: a YAML class with the id changes only the fields it names).</summary>
        public static bool RegisterItemClass(string json) => Safe.Call(registerItemClass.Call, json, false);

        /// <summary>Puts these prefabs in the class (step 1 of classification; the class may be registered later).</summary>
        public static bool ClaimItems(string classId, params string[] prefabs) => Safe.Call(claimItems.Call, classId, prefabs, false);

        /// <summary>A callback naming a class id for the item types it knows, null for the rest; asked once per item type.</summary>
        public static bool RegisterClassifier(string id, Func<ItemDrop.ItemData, string?> classify) =>
            Safe.Call(registerClassifier.Call, id, classify, false);

        public static bool UnregisterClassifier(string id) => Safe.Call(unregisterClassifier.Call, id, false);

        /// <summary>A prefab's item level, 1 (Meadows) to 8 (Deep North), under the YAML's <c>item_tiers.items</c>.</summary>
        public static bool SetItemLevel(string prefab, int level) => Safe.Call(setItemLevel.Call, prefab, level, false);

        /// <summary>The item's class id, or null.</summary>
        public static string? GetItemClass(ItemDrop.ItemData item) => Safe.Call(getItemClass.Call, item, null);

        /// <summary>The item's level 1-8, or 0.</summary>
        public static int GetItemLevel(ItemDrop.ItemData item) => Safe.Call(getItemLevel.Call, item, 0);
    }
}
