namespace Burrow.Stow
{
    /// <summary>The orders of the Sort actions: item type then name, name, heaviest first, most valuable first, the
    /// item held most of first (all its stacks together).</summary>
    public enum SortOrder
    {
        Category,
        Name,
        Weight,
        Value,
        Amount,
    }
}
