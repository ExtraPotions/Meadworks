// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using ExtraPotions.Inscribe.Adopted.Items;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// Target resolution for <c>inspect</c>, <c>reroll</c> and <c>affix</c> (console-commands.md section 3). Words:
    /// <c>cursor</c> (the dragged item), <c>hover</c> (the inventory or open container item under the mouse),
    /// <c>ground</c> (the dropped item under the crosshair, <c>inspect</c> only) and the equipment slots <c>right</c>,
    /// <c>left</c>, <c>head</c>, <c>chest</c>, <c>legs</c>, <c>cape</c>, <c>utility</c>. With no word the first match of
    /// cursor, hover, ground, right wins. Local player only; reads nothing that needs ownership.
    /// </summary>
    internal static class ItemTargets
    {
        public static readonly string[] EquipmentWords = { "right", "left", "head", "chest", "legs", "cape", "utility" };

        public const string InventoryWords = "cursor|hover|<slot>";
        public const string AnyWords = "cursor|hover|ground|<slot>";

        public static bool IsWord(string word, bool allowGround) =>
            word == "cursor" || word == "hover" || (allowGround && word == "ground") || Array.IndexOf(EquipmentWords, word) >= 0;

        /// <summary>The item a word names ("" = the first match); <paramref name="where"/> is the word that matched.</summary>
        public static ItemDrop.ItemData? Resolve(string word, bool allowGround, out string where)
        {
            where = word;
            if (word.Length > 0)
            {
                return ByWord(word, allowGround);
            }
            string[] order = allowGround ? new[] { "cursor", "hover", "ground", "right" } : new[] { "cursor", "hover", "right" };
            foreach (string candidate in order)
            {
                ItemDrop.ItemData? item = ByWord(candidate, allowGround);
                if (item != null)
                {
                    where = candidate;
                    return item;
                }
            }
            return null;
        }

        private static ItemDrop.ItemData? ByWord(string word, bool allowGround)
        {
            switch (word)
            {
                case "cursor": return Cursor();
                case "hover": return Hover();
                case "ground": return allowGround ? Ground() : null;
                default: return Player.m_localPlayer == null ? null : Equipped(Player.m_localPlayer, word);
            }
        }

        private static ItemDrop.ItemData? Cursor()
        {
            InventoryGui? gui = InventoryGui.instance;
            return gui != null && gui.m_dragGo != null ? gui.m_dragItem : null;
        }

        private static ItemDrop.ItemData? Hover()
        {
            InventoryGui? gui = InventoryGui.instance;
            if (gui == null || !InventoryGui.IsVisible())
            {
                return null;
            }
            ItemDrop.ItemData? item = HoveredIn(gui.m_playerGrid);
            return item ?? (gui.IsContainerOpen() ? HoveredIn(gui.m_containerGrid) : null);
        }

        private static ItemDrop.ItemData? HoveredIn(InventoryGrid? grid)
        {
            Inventory? inventory = grid?.GetInventory();
            if (grid == null || inventory == null)
            {
                return null;
            }
            Vector2i pos = grid.GetElementPos(grid.GetHoveredElement());
            return pos.x < 0 ? null : inventory.GetItemAt(pos.x, pos.y);
        }

        // The crosshair's hover object; the replicated item data is re-read from its ZDO (no ownership needed).
        private static ItemDrop.ItemData? Ground()
        {
            ItemDrop? drop = Player.m_localPlayer?.GetHoverObject()?.GetComponentInParent<ItemDrop>();
            if (drop == null)
            {
                return null;
            }
            drop.Load();
            return drop.m_itemData;
        }

        private static ItemDrop.ItemData? Equipped(Humanoid player, string word)
        {
            switch (word)
            {
                case "right": return player.m_rightItem;
                case "left": return player.m_leftItem;
                case "head": return player.m_helmetItem;
                case "chest": return player.m_chestItem;
                case "legs": return player.m_legItem;
                case "cape": return player.m_shoulderItem;
                case "utility": return player.m_utilityItem;
                default: return null;
            }
        }

        /// <summary>
        /// The target of a changing command (<c>reroll</c>, <c>affix</c>): in the caller's own inventory, a magic base.
        /// Replies with the refusal and returns null otherwise.
        /// </summary>
        public static ItemDrop.ItemData? OwnMagicBase(CommandCall call, string word, string grammar)
        {
            ItemDrop.ItemData? item = Resolve(word, allowGround: false, out _);
            Player? player = Player.m_localPlayer;
            if (item == null || player == null)
            {
                call.Fail(word.Length == 0 ? "no item on the cursor, under the mouse or in the right hand." : $"no item at '{word}'.", grammar);
                return null;
            }
            if (!player.GetInventory().ContainsItem(item))
            {
                call.Reply("that item is not in your own inventory.");
                return null;
            }
            if (!ItemClasses.IsMagicBase(item))
            {
                call.Reply($"{ItemText.Name(item)} is not a magic base (stackable, no slot, or a rune).");
                return null;
            }
            return item;
        }

        /// <summary>Test commands never write an item of a newer format (item-data.md section 8); replies when it is one.</summary>
        public static bool IsNewer(CommandCall call, Affixes.ItemState state)
        {
            if (state.IsNewerFormat)
            {
                call.Reply("the item was changed by a newer ExtraPotions.Inscribe.Adopted; it is left alone.");
            }
            return state.IsNewerFormat;
        }
    }
}
