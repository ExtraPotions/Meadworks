using Mantle.Backpacks;

namespace Mantle.Slots
{
    /// <summary>
    /// What a slot holds. Head to Utility and <see cref="Trinket"/> are worn (<see cref="SlotRules.IsWorn"/>): an item in such
    /// a slot is the one the player wears. The others are carried: each takes only its kind of item. <see cref="Retired"/> stands for a slot an
    /// older build had (the quick slots) when a layout record is read, so the cells after it keep their places; it takes
    /// nothing, so what sat there moves into the grid. <see cref="Key"/> is a ring cell (<see cref="KeyRing"/>): each takes
    /// one key. <see cref="Tacklebox"/> holds a tacklebox and <see cref="Tackle"/> is one of that box's cells
    /// (<see cref="Mantle.Tackle.Tacklebox"/>): each takes bait. New kinds go at the end: the layout record stores slot ids
    /// ("key1", "tacklebox1", "tackle1"), not numbers, so an older record still reads, and an older build reads a newer
    /// kind's id as retired. <see cref="Ring"/> and <see cref="Belt"/> (Mantle's own, not PackPanel's) are carried slots, not worn ones:
    /// each takes only the items the accessory registry tags for it, and what lies there gives its bonus (<see cref="Mantle.Accessories.AccessoryBonus"/>).
    /// <see cref="Feet"/> (PackPanel 0.15, appended after Mantle's own kinds) is worn and takes EliteEquipment's boots; it is
    /// laid out only while EliteEquipment wears them on their own (<see cref="Core.EliteEquipmentLink.SeparateBoots"/>).
    /// </summary>
    public enum SlotKind
    {
        Head,
        Chest,
        Legs,
        Back,
        Backpack,
        Utility,
        Food,
        Mead,
        Ammo,
        Purse,
        Retired,
        Key,
        Tacklebox,
        Tackle,
        Trinket,
        Ring,
        Belt,
        Feet,
    }
}
