using Mantle.Crafting;

namespace Mantle.Tackle
{
    /// <summary>
    /// What one tacklebox gives and costs: its recipe (<see cref="CraftStats"/>) and the cells it adds for bait while it
    /// lies in the Tacklebox slot. A kind's defaults are built in (<see cref="TackleboxCatalog"/>);
    /// <c>Mantle.Tackleboxes.yml</c> overrides any of them (<see cref="TackleboxesModel"/>).
    /// </summary>
    public sealed class TackleboxStats : CraftStats
    {
        public TackleboxStats(string station, int level, string cost, int cells)
            : base(station, level, cost)
        {
            Cells = cells;
        }

        public int Cells { get; }
    }
}
