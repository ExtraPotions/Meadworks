// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ExtraPotions.Wildblood.AdoptedLibs.PlayerGrid
{
    /// <summary>
    /// PackPanel, the inventory mod (a bigger grid, labelled slots, a key ring, backpacks, a tacklebox): whether it runs
    /// on this peer, by its plugin GUID in BepInEx's chainloader, checked on first use (after every plugin has loaded)
    /// and cached for the process. Nothing of PackPanel is referenced; a consumer reads what it publishes: its config
    /// entries (<see cref="Entry{T}"/>) and the main grid in the character's custom data (<see cref="PackPanelGrid"/>).
    /// </summary>
    public static class PackPanelLink
    {
        public const string Guid = "milkyteam.packpanel";

        private static bool detected;
        private static ConfigFile? config;
        private static ConfigEntry<bool>? enabled;   // kept once found: a lookup by name allocates and takes the file's lock

        public static bool Present
        {
            get
            {
                if (!detected)
                {
                    detected = true;
                    config = Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) && info.Instance != null ? info.Instance.Config : null;
                }
                return config != null;
            }
        }

        /// <summary>PackPanel is loaded and its master switch (<c>1. Inventory / Enabled</c>) is on. Read per item and
        /// per slot, so the entry is looked up by name only until it is found.</summary>
        public static bool LaysOutInventory
        {
            get
            {
                enabled ??= Entry<bool>("1. Inventory", "Enabled");
                return enabled != null && enabled.Value;
            }
        }

        /// <summary>One of PackPanel's config entries (the server's value while connected), or null without PackPanel.</summary>
        public static ConfigEntry<T>? Entry<T>(string section, string key) =>
            Present && config!.TryGetEntry(section, key, out ConfigEntry<T> entry) ? entry : null;
    }
}
