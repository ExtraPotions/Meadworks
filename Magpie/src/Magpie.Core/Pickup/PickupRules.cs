// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// The rule order restates the game's auto pickup as documented by OpenKeep src/BuildCamera/CameraPickup.cs
// (Wanted, Pull, Fits; GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow, reference ad278ecf323901c3645eb1f9f6fac6833a0f8cb1).
namespace ExtraPotions.Magpie.Pickup
{
    /// <summary>What the game knows about one item in range, read by the plugin at scan time.</summary>
    public readonly struct PickupFacts
    {
        public PickupFacts(bool autoPickup, bool isPiece, bool valid, bool haveUnique, bool ignored, bool owned, bool inTar, bool fits)
        {
            AutoPickup = autoPickup;
            IsPiece = isPiece;
            Valid = valid;
            HaveUnique = haveUnique;
            Ignored = ignored;
            Owned = owned;
            InTar = inTar;
            Fits = fits;
        }

        /// <summary>The item's own <c>ItemDrop.m_autoPickup</c> flag.</summary>
        public bool AutoPickup { get; }
        public bool IsPiece { get; }

        /// <summary>Has a valid <c>ZNetView</c>.</summary>
        public bool Valid { get; }

        /// <summary>A unique item (for example a key) the player already has.</summary>
        public bool HaveUnique { get; }

        /// <summary>Named in Ignore Items (A7).</summary>
        public bool Ignored { get; }

        /// <summary><c>ItemDrop.CanPickup</c>: this client owns the item.</summary>
        public bool Owned { get; }
        public bool InTar { get; }

        /// <summary>Room in the inventory and carry weight left.</summary>
        public bool Fits { get; }
    }

    public enum PickupAction
    {
        /// <summary>Not an auto pickup item for this player.</summary>
        Skip,

        /// <summary>Ask the owner to hand the item over (<c>ItemDrop.RequestOwn</c>); nothing moves yet.</summary>
        RequestOwnership,

        /// <summary>Wanted, but stuck in tar or no room: the item stays where it is.</summary>
        Hold,

        /// <summary>Glide it in and take it on arrival.</summary>
        Pull,
    }

    /// <summary>Design A5 and A6: the game's auto pickup rules, kept unchanged, plus the take-time re-check.</summary>
    public static class PickupRules
    {
        public static PickupAction Decide(PickupFacts item)
        {
            if (!item.AutoPickup || item.IsPiece || !item.Valid || item.HaveUnique || item.Ignored)
                return PickupAction.Skip;
            if (!item.Owned)
                return PickupAction.RequestOwnership;
            if (item.InTar || !item.Fits)
                return PickupAction.Hold;
            return PickupAction.Pull;
        }

        /// <summary>SwiftPickup 1.3.3 anti-dupe rule (behavior): right before the take the item is still valid and owned here.</summary>
        public static bool CanTakeNow(bool valid, bool owned) => valid && owned;
    }
}
