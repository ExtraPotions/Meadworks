namespace Mantle.Layout
{
    /// <summary>
    /// The layout a character's items were last arranged in, kept in the character's own data
    /// (<c>Player.m_customData["Mantle.inventoryLayout"]</c>, saved with the character). Read from the player being
    /// loaded rather than the local player, because the record is needed while the game loads the character.
    /// Without a record the items are where the game put them: its 8 wide grid of <c>invrows</c> rows, anything
    /// below belonging to no slot of ours.
    /// </summary>
    public static class LayoutRecord
    {
        public const string Key = "Mantle.inventoryLayout";

        public static InventoryLayout Read(Player player)
        {
            InventoryLayout recorded = player.m_customData.TryGetValue(Key, out string value) ? InventoryLayout.Parse(value) : null;
            return recorded ?? LayoutBuilder.GameLayout(player);
        }

        public static void Write(Player player, InventoryLayout layout) => player.m_customData[Key] = layout.Record();

        public static void Clear(Player player) => player.m_customData.Remove(Key);

        public static bool Has(Player player) => player.m_customData.ContainsKey(Key);
    }
}
