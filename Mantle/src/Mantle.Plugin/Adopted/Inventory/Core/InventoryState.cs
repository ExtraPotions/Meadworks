using System.Collections.Generic;
using Mantle.Layout;
using Mantle.Slots;

namespace Mantle.Core
{
    /// <summary>
    /// The local player and the layout its inventory is in. Every patch asks <see cref="Manages"/> first: only the local
    /// player's own inventory, only while the module is on. Set when the character loads (before the game equips its
    /// saved items, so every utility in the slots comes back worn) and whenever the layout is applied.
    /// </summary>
    public static class InventoryState
    {
        public static Player Player { get; private set; }

        public static InventoryLayout Layout { get; private set; }

        public static bool Active => Plugin.WorldReady && InventorySettings.Enabled.Value && Player != null && Layout != null;

        public static bool IsLocal(Humanoid humanoid) => Plugin.WorldReady && humanoid != null && humanoid == Player && humanoid == Player.m_localPlayer;

        public static bool Manages(Inventory inventory) => Active && inventory != null && ReferenceEquals(inventory, Player.GetInventory());

        internal static void Set(Player player, InventoryLayout layout)
        {
            Player = player;
            Layout = layout;
        }

        /// <summary>The cells a worn backpack's partly used last row blocks, in a managed inventory; else none.</summary>
        public static IEnumerable<Vector2i> BlockedCells(Inventory inventory)
        {
            if (!Manages(inventory))
                yield break;
            for (int x = Layout.Width - Layout.BlockedCells; x < Layout.Width; x++)
                yield return new Vector2i(x, Layout.MainRows - 1);
        }

        /// <summary>The slot at a cell of a managed inventory, else null.</summary>
        public static Slot SlotAt(Inventory inventory, Vector2i pos) => Manages(inventory) ? Layout.SlotAt(pos) : null;

        private static readonly List<Vector2i> none = new List<Vector2i>();

        /// <summary>The cells of every slot of a kind, in number order.</summary>
        public static IReadOnlyList<Vector2i> CellsOf(SlotKind kind) => Layout != null ? Layout.CellsOf(kind) : none;

        /// <summary>The item in slot <paramref name="number"/> of a kind, or null.</summary>
        public static ItemDrop.ItemData ItemIn(SlotKind kind, int number)
        {
            IReadOnlyList<Vector2i> cells = CellsOf(kind);
            if (!Active || number < 1 || number > cells.Count)
                return null;
            Vector2i cell = cells[number - 1];
            return Player.GetInventory().GetItemAt(cell.x, cell.y);
        }
    }
}
