namespace Burrow.Reach
{
    /// <summary>The call site a count or payment belongs to; each has its own switch in the cfg.</summary>
    public enum ReachMode
    {
        Crafting,
        Building,
        Upgrading,
        Stations,
    }
}
