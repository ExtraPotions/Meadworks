namespace Mantle.Crafting
{
    /// <summary>
    /// How one of Mantle's crafted items is made: the crafting station (prefab name), its level and the cost as
    /// prefab:amount pairs. Each kind of item adds what it gives (<see cref="Backpacks.BackpackStats"/>,
    /// <see cref="Tackle.TackleboxStats"/>).
    /// </summary>
    public class CraftStats
    {
        public CraftStats(string station, int level, string cost)
        {
            Station = station;
            Level = level;
            Cost = cost;
        }

        public string Station { get; }

        public int Level { get; }

        public string Cost { get; }
    }
}
