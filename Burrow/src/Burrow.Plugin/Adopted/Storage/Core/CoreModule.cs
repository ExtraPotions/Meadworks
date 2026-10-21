using ExtraPotions.Burrow.Embedded.SyncedConfig;

namespace Burrow.Core
{
    /// <summary>
    /// Entry point of the Core module: binds the section 0 settings. The console command registers itself through
    /// a patch on <c>Terminal.InitTerminal</c> (<see cref="Command"/>), container tracking through
    /// <see cref="ContainerScan"/>'s patch on <c>Container.Awake</c>.
    /// </summary>
    public static class CoreModule
    {
        public static void Initialize(SyncedConfiguration synced)
        {
            CoreSettings.Initialize(synced);
        }
    }
}
