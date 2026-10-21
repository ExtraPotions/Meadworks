using ExtraPotions.Burrow.Embedded.SyncedConfig;

namespace Burrow.Shared
{
    /// <summary>
    /// Entry point of the Shared module (SPEC section 9): binds the section 9 settings and registers the module's
    /// words. The mode itself is Core's <c>Shared Chests</c> setting. Patches: the user name in the ZDO
    /// (<see cref="UserNamePatch"/>), the read-only open (<see cref="ViewOpenPatch"/>), the viewer's panel
    /// (<see cref="ViewerPanel"/>), the routed and blocked panel actions (<see cref="PanelRouting"/>), the request
    /// RPCs (<see cref="ChestRequests"/>), the request bookkeeping (<see cref="ChestRequester"/>) and the touch
    /// feedback (<see cref="Touches"/>).
    /// </summary>
    public static class SharedModule
    {
        public static void Initialize(SyncedConfiguration synced)
        {
            SharedSettings.Bind(synced);
            SharedWords.Register();
        }
    }
}
