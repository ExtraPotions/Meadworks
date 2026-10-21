using BepInEx;
using BepInEx.Bootstrap;

namespace Mantle.Core
{
    /// <summary>
    /// Whether OpenKeep 1.8.0 or later runs on this peer: by its plugin GUID in BepInEx's chainloader, checked on first
    /// use (after every plugin has loaded) and cached for the process. The two mods reference nothing of each other and
    /// work alone; together, OpenKeep reads what Mantle publishes: the main grid (<see cref="Layout.GridContract"/>),
    /// the button strip (<see cref="Panels.ButtonStrip"/>) and the key ring's Key Items and Key Stack from this mod's
    /// config. Here it decides only whether the player panel keeps a strip for OpenKeep's buttons and who writes the
    /// keys' stack size (<see cref="Ring.KeyStacks"/>). Older OpenKeep versions know nothing of Mantle, so they count
    /// as absent.
    /// </summary>
    public static class OpenKeepLink
    {
        public const string Guid = "milkyteam.openkeep";
        private static readonly System.Version First = new System.Version(1, 8, 0);

        private static bool detected;
        private static bool present;

        public static bool Present
        {
            get
            {
                if (!detected)
                {
                    detected = true;
                    present = Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) && info.Metadata.Version >= First;
                }
                return present;
            }
        }
    }
}
