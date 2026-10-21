using BepInEx.Bootstrap;

namespace Mantle.Core
{
    /// <summary>
    /// Whether Azumatt's AAA Crafting (AzuAntiArthriticCrafting) runs on this peer: by its plugin GUID in BepInEx's
    /// chainloader, checked on first use (after every plugin has loaded) and cached for the process. It lays the
    /// crafting panel out itself (a paged recipe grid with its own rows of buttons), so with it Mantle leaves the
    /// panel's size to it (<see cref="Panels.CraftingPanel"/>; asked on GitHub 2026-10-07: "make it compatible with
    /// it"). Nothing of it is referenced.
    /// </summary>
    public static class AaaCraftingLink
    {
        public const string Guid = "Azumatt.AzuAntiArthriticCrafting";

        private static bool detected;
        private static bool present;

        public static bool Present
        {
            get
            {
                if (!detected)
                {
                    detected = true;
                    present = Chainloader.PluginInfos.ContainsKey(Guid);
                    if (present)
                        Plugin.Log.LogInfo("AAA Crafting found: the crafting panel keeps its size (Crafting Panel Width and Height do nothing)");
                }
                return present;
            }
        }
    }
}
