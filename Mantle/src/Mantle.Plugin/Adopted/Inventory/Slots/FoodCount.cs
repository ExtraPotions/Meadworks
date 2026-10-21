using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx;
using Mantle.Core;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace Mantle.Slots
{
    /// <summary>
    /// How many foods a player can eat at once: FeastMaster's Food Slots when that mod is loaded (read from its own
    /// config entry through BepInEx, the value its server synced), the game's 3 otherwise. Looked up on first use,
    /// when every plugin has loaded; a change of FeastMaster's value raises <see cref="Changed"/> so the food slots
    /// follow at once.
    /// </summary>
    public static class FoodCount
    {
        public const int GameFoods = 3;
        private const string FeastMasterGuid = "com.FeastMaster";
        private const string FeastMasterSection = "0. Global Settings";
        private const string FeastMasterKey = "Food Slots";

        private static ConfigEntry<int> feastMaster;
        private static bool looked;
        private static int lastCount = -1;

        public static event Action Changed;

        /// <summary>The food slots the layout gets: the eating count or the fixed setting, 0 to 5.</summary>
        public static int Slots()
        {
            int count = InventorySettings.FoodSlotsFollowEating.Value ? Eatable() : InventorySettings.FoodSlots.Value;
            return Math.Max(0, Math.Min(InventorySettings.MaxGroup, count));
        }

        public static int Eatable()
        {
            // The authoritative eating count is independent of HUD visibility or the number of foods already eaten.
            if (CruciblePlugin.IsReady)
            {
                try
                {
                    var registry = CruciblePlugin.Services.Capabilities;
                    var providers = registry.Providers("meadworks.larder.foodslots.count");
                    if (providers.Count == 1 && providers[0].Owner == "com.meadworks.larder"
                        && registry.TryResolve<Func<int>>("meadworks.larder.foodslots.count", new System.Version(1, 0), out var larder))
                    {
                        int count = larder();
                        // Negative means the owner stepped aside; malformed counts also retain the native fallback.
                        if (count >= 0 && count <= InventorySettings.MaxGroup)
                            return count;
                    }
                }
                catch { /* A failed optional provider retains the pinned native/default fallback. */ }
            }
            ConfigEntry<int> entry = FeastMasterEntry();
            return entry != null ? entry.Value : GameFoods;
        }

        /// <summary>Existing layout-change event also follows an optional provider's effective value.</summary>
        public static void Tick()
        {
            if (!InventorySettings.Enabled.Value || !InventorySettings.FoodSlotsFollowEating.Value)
                return;
            int count = Eatable();
            if (count == lastCount)
                return;
            lastCount = count;
            Changed?.Invoke();
        }

        private static void FeastMasterChanged(object sender, EventArgs args) => Changed?.Invoke();

        public static void Shutdown()
        {
            if (feastMaster != null)
                feastMaster.SettingChanged -= FeastMasterChanged;
            feastMaster = null;
            looked = false;
            lastCount = -1;
        }

        private static ConfigEntry<int> FeastMasterEntry()
        {
            if (looked)
                return feastMaster;
            looked = true;
            if (!Chainloader.PluginInfos.TryGetValue(FeastMasterGuid, out PluginInfo info) || info.Instance == null)
                return null;
            if (!info.Instance.Config.TryGetEntry(FeastMasterSection, FeastMasterKey, out feastMaster))
            {
                Plugin.Log.LogWarning($"FeastMaster is loaded but has no {FeastMasterSection} / {FeastMasterKey}; food slots follow the game's {GameFoods} foods");
                return null;
            }
            feastMaster.SettingChanged += FeastMasterChanged;
            Plugin.Log.LogInfo($"food slots follow FeastMaster's Food Slots ({feastMaster.Value})");
            return feastMaster;
        }
    }
}
