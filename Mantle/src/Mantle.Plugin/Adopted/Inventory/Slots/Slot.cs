namespace Mantle.Slots
{
    /// <summary>
    /// One slot of the layout: its kind and its number within the kind (1 for the first food slot). The id
    /// ("food1") is what the layout record in the character's data stores, so an item keeps its slot when the grid or
    /// the other slot counts change.
    /// </summary>
    public sealed class Slot
    {
        public Slot(SlotKind kind, int number)
        {
            Kind = kind;
            Number = number;
            Id = kind.ToString().ToLowerInvariant() + number;
        }

        /// <summary>A slot of an old record whose kind this build no longer has; it keeps its id and its cell.</summary>
        public Slot(string id)
        {
            Kind = SlotKind.Retired;
            Number = 0;
            Id = id;
        }

        public SlotKind Kind { get; }

        public int Number { get; }

        public string Id { get; }

        public override string ToString() => Id;
    }
}
