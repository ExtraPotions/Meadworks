// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Thing.Admin
{
    public enum InventoryDeleteResult { Pending, Deleted, Unchanged }

    public interface IInventoryDeleteOwner
    {
        string Receipt { get; set; }
        bool MatchesReservation();
        void Prepare();
        // Unlink the reserved object before any notification or callback can save the character.
        void Remove();
        bool ContainsReservedItem();
        bool Save();
    }

    public static class DurableInventoryDelete
    {
        public static InventoryDeleteResult Apply(IInventoryDeleteOwner owner, string receipt)
        {
            if (owner == null || string.IsNullOrEmpty(receipt)) throw new ArgumentException("A delete owner and receipt are required.");
            var rejected = "unchanged:" + receipt;
            if (owner.Receipt.Length != 0)
            {
                if (owner.Receipt != receipt && owner.Receipt != rejected || !owner.Save()) return InventoryDeleteResult.Pending;
                return owner.Receipt == receipt ? InventoryDeleteResult.Deleted : InventoryDeleteResult.Unchanged;
            }
            if (!owner.MatchesReservation())
            {
                owner.Receipt = rejected;
                return owner.Save() ? InventoryDeleteResult.Unchanged : InventoryDeleteResult.Pending;
            }
            try { owner.Prepare(); }
            catch { return InventoryDeleteResult.Pending; }
            if (!owner.MatchesReservation())
            {
                owner.Receipt = rejected;
                return owner.Save() ? InventoryDeleteResult.Unchanged : InventoryDeleteResult.Pending;
            }
            // Character persistence includes both the receipt and inventory; callbacks may throw after removal.
            owner.Receipt = receipt;
            try { owner.Remove(); }
            catch { }
            if (owner.ContainsReservedItem()) owner.Receipt = rejected;
            if (!owner.Save()) return InventoryDeleteResult.Pending;
            return owner.Receipt == receipt ? InventoryDeleteResult.Deleted : InventoryDeleteResult.Unchanged;
        }
    }
}
