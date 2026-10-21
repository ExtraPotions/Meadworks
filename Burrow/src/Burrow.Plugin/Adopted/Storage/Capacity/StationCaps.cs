namespace Burrow.Capacity
{
    /// <summary>
    /// The two caps of a station built on the game's Smelter: Items is Smelter.m_maxOre (how many items it holds
    /// queued), Fuel is Smelter.m_maxFuel. In a file entry 0 means "not set"; in the game's values 0 means the
    /// station does not take that at all.
    /// </summary>
    public readonly struct StationCaps
    {
        public StationCaps(int items, int fuel)
        {
            Items = items;
            Fuel = fuel;
        }

        public int Items { get; }

        public int Fuel { get; }

        public bool IsEmpty => Items == 0 && Fuel == 0;

        public override string ToString() => $"items {Items}, fuel {Fuel}";
    }
}
