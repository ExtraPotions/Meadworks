using System;
using BepInEx.Configuration;

namespace Mantle.Consume
{
    /// <summary>
    /// The Food Key's and each Mead Slot key's short name for the Food and Mead bar ("Z", "Alt+1"), made once and again only
    /// after a key setting changed (its <c>SettingChanged</c>), not on each of the bar's checks ten times a second.
    /// <see cref="Version"/> grows with every change, so the bar knows to plan again.
    /// </summary>
    public static class ConsumeKeyNames
    {
        private static string food;
        private static string[] meads;

        public static int Version { get; private set; }

        public static string Food
        {
            get
            {
                Watch();
                return food ?? (food = ExtraPotions.Mantle.Embedded.Hotkeys.KeyNames.Short(ConsumeSettings.FoodKey.Value));
            }
        }

        public static string Mead(int slot)
        {
            Watch();
            return meads[slot] ?? (meads[slot] = ExtraPotions.Mantle.Embedded.Hotkeys.KeyNames.Short(ConsumeSettings.MeadSlotKeys[slot].Value));
        }

        private static void Watch()
        {
            if (meads != null)
                return;
            meads = new string[ConsumeSettings.MeadSlotKeys.Length];
            ConsumeSettings.FoodKey.SettingChanged += Forget;
            foreach (ConfigEntry<KeyboardShortcut> key in ConsumeSettings.MeadSlotKeys)
                key.SettingChanged += Forget;
        }

        private static void Forget(object sender, EventArgs args)
        {
            food = null;
            Array.Clear(meads, 0, meads.Length);
            Version++;
        }
    }
}
