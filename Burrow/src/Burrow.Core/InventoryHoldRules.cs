// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: what an inventory write does while Burrow holds the inventory (playtest 2026-10-20, item safety).
using System;

namespace ExtraPotions.Burrow
{
    /// <summary>
    /// While a reservation or shared transfer holds an inventory, Burrow's guard decides per <c>Inventory</c> write what the
    /// caller sees. No write is swallowed while reporting success:
    /// <list type="bullet">
    /// <item><see cref="HeldWrite.Refuse"/>: methods that answer (bool or the added item) are skipped and answer false or
    /// null; nothing is added or removed, so a caller that checks the answer never loses items.</item>
    /// <item><see cref="HeldWrite.Defer"/>: void removals (<c>RemoveItem(name, ...)</c>, <c>RemoveAll</c>) cannot answer,
    /// so they are kept and applied when the hold ends: the caller's payment still happens.</item>
    /// <item><see cref="HeldWrite.Neutral"/>: void moves (<c>MoveAll</c>, <c>MoveItemToThis</c> without an amount) are skipped
    /// on both sides together: nothing leaves one inventory without arriving in the other.</item>
    /// <item><see cref="HeldWrite.Allow"/>: <c>Load</c> (character load, a chest's data arriving from its owner) always runs;
    /// it is the authoritative state, never a mod's add or remove. That includes the <c>AddItem</c> calls the game makes inside
    /// <c>Inventory.Load</c> after it clears the list: refusing them emptied a held chest in memory (playtest 2026-10-20,
    /// Wildroot grid from a chest).</item>
    /// </list>
    /// </summary>
    public enum HeldWrite { Allow, Refuse, Defer, Neutral }

    public static class InventoryHoldRules
    {
        /// <summary>The Inventory methods the guard patches. <c>StackAll</c> (the game's Place stacks) adds to the chest and then
        /// removes from the player: with the player held the removal was refused after the add, so the stack was in both (2026-10-29).</summary>
        public static readonly string[] Guarded = { "AddItem", "RemoveItem", "RemoveAll", "RemoveOneItem", "RemoveItemByName", "MoveItemToThis", "MoveAll", "StackAll" };

        /// <summary>Most removals kept at once. Beyond it a void removal runs at once (logged): the hold's commit then sees a
        /// changed inventory and rolls back or waits for recovery, which never deducts twice or overwrites the change.</summary>
        public const int MaxDeferred = 256;

        public static HeldWrite Classify(string method, bool returnsVoid) => Classify(method, returnsVoid, false);

        /// <summary><paramref name="insideLoad"/>: the write is made by <c>Inventory.Load</c> of the same inventory.</summary>
        public static HeldWrite Classify(string method, bool returnsVoid, bool insideLoad)
        {
            if (insideLoad || method == "Load")
                return HeldWrite.Allow;
            if (Array.IndexOf(Guarded, method) < 0)
                return HeldWrite.Allow;
            if (!returnsVoid)
                return HeldWrite.Refuse;
            return method == "MoveAll" || method == "MoveItemToThis" ? HeldWrite.Neutral : HeldWrite.Defer;
        }
    }
}
