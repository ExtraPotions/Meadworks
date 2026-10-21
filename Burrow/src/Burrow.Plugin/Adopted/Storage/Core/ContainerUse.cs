namespace Burrow.Core
{
    /// <summary>What a module wants a container for. Both uses share the section 0 rules today; the value is kept
    /// so per use rules can be added without touching the callers.</summary>
    public enum ContainerUse
    {
        Reach,
        Stow,
    }
}
