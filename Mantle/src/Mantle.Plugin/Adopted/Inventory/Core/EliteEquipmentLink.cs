using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace Mantle.Core
{
    /// <summary>
    /// EliteEquipment's boots and whether there is a Feet slot for them. EliteEquipment runs on this peer when its plugin
    /// GUID is in BepInEx's chainloader (checked on first use, after every plugin has loaded). Its boots are Legs items it
    /// wears itself, told apart by their prefab names (<see cref="BootsPrefix"/>); they get a Feet slot while its
    /// <c>1. Boots / Separate Boots</c> is on (<see cref="SeparateBoots"/>, the server's value while connected). The two
    /// mods reference nothing of each other: the prefix, the section and the key are the contract.
    /// </summary>
    public static class EliteEquipmentLink
    {
        public const string Guid = "milkyteam.eliteequipment";
        public const string BootsPrefix = "EE_Boots_";
        private const string BootsSection = "1. Boots";
        private const string BootsKey = "Separate Boots";

        private static bool detected;
        private static ConfigFile config;
        private static ConfigEntry<bool> separateBoots;
        private static bool bootsBroken;

        /// <summary>EliteEquipment's Separate Boots changed (on this peer or from the server): the layout gains or loses the Feet slot.</summary>
        public static event Action BootsChanged;

        public static bool Present
        {
            get
            {
                if (!detected)
                {
                    detected = true;
                    config = Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) && info.Instance != null
                        ? info.Instance.Config : null;
                }
                return config != null;
            }
        }

        /// <summary>EliteEquipment wears its boots on their own, so the layout has a Feet slot.</summary>
        public static bool SeparateBoots
        {
            get
            {
                ConfigEntry<bool> entry = BootsEntry();
                return entry != null && entry.Value;
            }
        }

        /// <summary>One of EliteEquipment's boots, by its prefab name (real leggings are Legs items too).</summary>
        public static bool IsBoots(ItemDrop.ItemData item) =>
            item != null && ItemNames.PrefabName(item).StartsWith(BootsPrefix, StringComparison.Ordinal);

        /// <summary>
        /// EliteEquipment's Separate Boots entry, kept once found and watched from then on; looked for again while missing
        /// (only at layout time), so without it there is simply no Feet slot.
        /// </summary>
        private static ConfigEntry<bool> BootsEntry()
        {
            if (separateBoots != null || bootsBroken || !Present)
                return separateBoots;
            try
            {
                if (!config.TryGetEntry(BootsSection, BootsKey, out ConfigEntry<bool> entry))
                    return null;
                separateBoots = entry;
            }
            catch (InvalidCastException)
            {
                bootsBroken = true;
                Plugin.Log.LogWarning($"EliteEquipment's {BootsSection} / {BootsKey} is not a true/false setting; no Feet slot");
                return null;
            }
            separateBoots.SettingChanged += (sender, args) => BootsChanged?.Invoke();
            Plugin.Log.LogInfo($"Feet slot follows EliteEquipment's {BootsKey} ({separateBoots.Value})");
            return separateBoots;
        }
    }
}
