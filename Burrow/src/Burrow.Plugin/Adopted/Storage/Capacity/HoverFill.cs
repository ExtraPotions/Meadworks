namespace Burrow.Capacity
{
    /// <summary>How the first extra hover line of a container shows how full it is.</summary>
    public enum HoverFill
    {
        /// <summary>"12 / 24 slots"</summary>
        Fraction,
        /// <summary>"50% full"</summary>
        Percent,
        /// <summary>No fill line.</summary>
        Off,
    }
}
