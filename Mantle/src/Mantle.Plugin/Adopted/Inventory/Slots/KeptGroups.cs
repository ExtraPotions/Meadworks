using System;

namespace Mantle.Slots
{
    /// <summary>
    /// The slot groups Keep On Death can keep (asked on GitHub 2026-10-07: "keep only food and ammunition for example"),
    /// any number at once: the .cfg takes them comma separated ("Food, Ammo"), the configuration manager a checkbox each.
    /// Gear is the worn armour slots (head, chest, legs and back, and feet while OpenKeep's boots have that slot).
    /// </summary>
    [Flags]
    public enum KeptGroups
    {
        None = 0,
        Gear = 1,
        Backpack = 2,
        Utility = 4,
        Trinket = 8,
        Food = 16,
        Mead = 32,
        Ammo = 64,
        Tacklebox = 128,
        Ring = 256,
        Belt = 512,
    }
}
