namespace Burrow.Reach
{
    /// <summary>How a requirement amount is shown when containers hold part of it.</summary>
    public enum RequirementDisplay
    {
        /// <summary>"3 + 12": the inventory part, then the container part in the storage colour.</summary>
        Split,
        /// <summary>"15": the required amount in the storage colour when containers pay part of it.</summary>
        Total,
        /// <summary>The game's text; only the colour changes (never red when the total covers it).</summary>
        Vanilla,
    }
}
