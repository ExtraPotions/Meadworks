using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;

namespace Burrow.Core
{
    /// <summary>
    /// Who lays out the player inventory, resolved afresh; character data never establishes plugin presence. The
    /// third-party PackPanel is read through its own config and custom data key. A MeadWorks inventory owner (Mantle)
    /// is read only through Crucible capabilities (user decision 2026-10-12 22:41): <c>meadworks.owns.inventory</c>,
    /// <c>meadworks.inventory.layout</c> and <c>meadworks.inventory.keyring</c>.
    /// </summary>
    public static class PackPanelLink
    {
        public const string Guid = "milkyteam.packpanel";
        public const string KeySection = "3. Key Ring";

        /// <summary>The capability registry; set by the plugin once Crucible is ready (tests set their own).</summary>
        public static Func<CapabilityRegistry> Registry = () => null;

        private static readonly FeatureLink inventoryOwner = new FeatureLink(FeatureOwnerContract.Id(FeatureOwnerContract.Inventory));
        private static readonly CapabilityLink<Func<Tuple<bool, int, int, int>>> layout =
            new CapabilityLink<Func<Tuple<bool, int, int, int>>>(InventoryLayoutContract.Id, InventoryLayoutContract.ContractVersion);
        private static readonly CapabilityLink<Func<Tuple<int, IReadOnlyCollection<string>>>> keyRing =
            new CapabilityLink<Func<Tuple<int, IReadOnlyCollection<string>>>>(KeyRingContract.Id, KeyRingContract.ContractVersion);

        private static CapabilityRegistry Current() { try { return Registry(); } catch { return null; } }

        private static ConfigFile PackPanel => Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info)
            && info.Instance != null && info.Instance.isActiveAndEnabled ? info.Instance.Config : null;
        private static bool Enabled(ConfigFile config) => config != null
            && (!config.TryGetEntry("1. Inventory", "Enabled", out ConfigEntry<bool> entry) || entry.Value);

        /// <summary>A MeadWorks product owns the inventory layout (installed, whether or not it is switched on).</summary>
        public static bool OwnerPresent => inventoryOwner.TryOwned(Current(), out _);
        /// <summary>A MeadWorks product owns the inventory layout and lays it out now.</summary>
        public static bool OwnerLaysOut => inventoryOwner.TryOwned(Current(), out bool on) && on;
        public static bool PackPanelLaysOut => Enabled(PackPanel);

        public static bool Present => PackPanel != null || OwnerPresent;
        public static bool Ambiguous => PackPanelLaysOut && OwnerLaysOut;
        public static bool LaysOutInventory => PackPanelLaysOut || OwnerLaysOut;

        /// <summary>The owner's published main grid; false while it publishes none, an invalid one, or is absent.</summary>
        public static bool TryOwnerGrid(out InventoryLayout grid)
        {
            layout.TryGet(Current(), out var provider);
            return InventoryLayoutContract.TryRead(provider, out grid) && grid.LaysOut;
        }

        /// <summary>The key ring: the owner's while one is installed, else PackPanel's config; false without one or when ambiguous.</summary>
        public static bool TryKeyRing(out int stack, out IReadOnlyCollection<string> items)
        {
            stack = 0;
            items = Array.Empty<string>();
            if (Ambiguous)
                return false;
            if (OwnerPresent)
            {
                keyRing.TryGet(Current(), out var provider);
                return KeyRingContract.TryRead(provider, out stack, out items);
            }
            ConfigFile config = PackPanel;
            if (config == null || !config.TryGetEntry(KeySection, "Key Stack", out ConfigEntry<int> size))
                return false;
            stack = size.Value;
            items = config.TryGetEntry(KeySection, "Key Items", out ConfigEntry<string> names) ? PackPanelKeyNames(names.Value) : Array.Empty<string>();
            return true;
        }

        /// <summary>PackPanel's own config entry (third-party), for change notifications.</summary>
        public static ConfigEntry<T> PackPanelEntry<T>(string section, string key)
        {
            ConfigFile config = PackPanel;
            return config != null && config.TryGetEntry(section, key, out ConfigEntry<T> entry) ? entry : null;
        }

        private static string cachedRaw;
        private static IReadOnlyCollection<string> cachedNames = Array.Empty<string>();
        private static IReadOnlyCollection<string> PackPanelKeyNames(string raw)
        {
            if (!ReferenceEquals(raw, cachedRaw))
            {
                var names = new List<string>();
                foreach (string name in (raw ?? "").Split(','))
                    if (name.Trim().Length > 0)
                        names.Add(name.Trim());
                cachedRaw = raw;
                cachedNames = names;
            }
            return cachedNames;
        }
    }
}
