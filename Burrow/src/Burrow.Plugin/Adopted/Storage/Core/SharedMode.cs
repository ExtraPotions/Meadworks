namespace Burrow.Core
{
    /// <summary>
    /// The <c>Shared Chests</c> setting of section 0: what happens with a chest another player has open.
    /// <c>Off</c> skips it, as the game refuses to open it; <c>View</c> opens it read-only; <c>Full</c> opens it
    /// read-only and sends every change as a request to the owning client (SPEC section 9).
    /// </summary>
    public enum SharedMode
    {
        Off,
        View,
        Full,
    }
}
