using Mantle.Crafting;

namespace Mantle.Backpacks
{
    /// <summary>
    /// What one backpack gives and costs: its recipe (<see cref="CraftStats"/>), the slots it adds to the grid, the carry
    /// weight it adds, and whether its slots pass portals (only while Backpack Portal Pass is on). A kind's defaults are
    /// built in (<see cref="BackpackCatalog"/>); <c>Mantle.Backpacks.yml</c> overrides any of them (<see cref="BackpacksModel"/>).
    /// </summary>
    public sealed class BackpackStats : CraftStats
    {
        public BackpackStats(string station, int level, string cost, int slots, float carry, bool portal)
            : base(station, level, cost)
        {
            Slots = slots;
            Carry = carry;
            Portal = portal;
        }

        public int Slots { get; }

        public float Carry { get; }

        public bool Portal { get; }
    }
}
