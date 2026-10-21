using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// The inventory's column of brown boxes is Mantle's look (the user's call, 2026-09-29: Mantle is the inventory
    /// UI mod; OpenKeep and Elite Creatures Reborn leave the inventory as the game draws it). So the column is built only
    /// while Mantle runs with its inventory section on (<c>1. Inventory / Enabled</c>): Mantle is found by its plugin
    /// GUID in BepInEx's chainloader on first use, after every plugin has loaded, and nothing of it is referenced. Without
    /// it <see cref="Column"/> changes nothing and the game's armour and weight plates stay exactly as the game draws them.
    /// </summary>
    internal static class MantleOwner
    {
        private const string Guid = "com.meadworks.mantle";

        private static bool looked;
        private static ConfigEntry<bool>? enabled;

        public static bool LaysOutInventory
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    enabled = Find();
                }
                return enabled != null && enabled.Value;
            }
        }

        private static ConfigEntry<bool>? Find()
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) || info.Instance == null)
            {
                return null;
            }
            return info.Instance.Config.TryGetEntry("1. Inventory", "Enabled", out ConfigEntry<bool> entry) ? entry : null;
        }
    }
}
