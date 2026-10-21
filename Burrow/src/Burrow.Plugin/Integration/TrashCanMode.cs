// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the trash can shows whether it destroys or salvages.
using Burrow.Stow;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Burrow.Integration
{
    /// <summary>
    /// The trash can (its own plate under the armor, or the end of the button row) shows its mode: the bin while trashing
    /// destroys, the game's hammer while <c>Trash Uses Salvage</c> is on and Kindle offers salvage. Its tooltip names the
    /// mode. Checked every inventory frame against the setting, Kindle's capability and the game language, so a change
    /// shows at once; the icon and tooltip are written only when one of them differs.
    /// </summary>
    public static class TrashCanMode
    {
        private static GameObject control;
        private static Image icon;
        private static Sprite hammer;
        private static int shown = -1;
        private static string language;
        private static float retryHammer = float.MaxValue;

        /// <summary>True when trashing a whole inventory stack of salvageable gear salvages it.</summary>
        public static bool Salvages => StowSettings.TrashUsesSalvage != null && StowSettings.TrashUsesSalvage.Value && SalvageLink.Available;

        /// <summary>The can's icon for the current mode: the game's hammer in salvage mode (the bin while it is not loaded), else the bin.</summary>
        public static Sprite Icon => Salvages && Hammer() is Sprite sprite ? sprite : StowSprites.Bin;

        /// <summary>The can to keep up to date: <paramref name="go"/> carries the tooltip, <paramref name="image"/> the icon.</summary>
        public static void Track(GameObject go, Image image)
        {
            control = go;
            icon = image;
            shown = -1;
            Tick();
        }

        public static void Tick()
        {
            if (control == null || icon == null)
                return;
            string current = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "";
            int mode = Salvages ? 1 : 0;
            if (mode == shown && string.Equals(current, language, System.StringComparison.Ordinal) && Time.unscaledTime < retryHammer)
                return;
            Sprite sprite = mode == 1 ? Hammer() : null;
            // Without the game's hammer icon (no item database yet) the bin stays and the tooltip still names the mode;
            // the icon is looked for again once a second until found.
            shown = mode;
            retryHammer = mode == 1 && sprite == null ? Time.unscaledTime + 1f : float.MaxValue;
            language = current;
            icon.sprite = sprite != null ? sprite : StowSprites.Bin;
            icon.preserveAspect = true;
            icon.color = Color.white;
            KitTooltip.Set(control, StowRowYield.Text(mode == 1 ? "trash_mode_salvage_topic" : "trash_mode_destroy_topic"),
                StowRowYield.Text(mode == 1 ? "trash_mode_salvage_tip" : "trash_mode_destroy_tip"));
        }

        /// <summary>The game's Hammer item icon, looked up once it exists.</summary>
        private static Sprite Hammer()
        {
            if (hammer != null)
                return hammer;
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Hammer") : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            Sprite[] icons = drop != null ? drop.m_itemData.m_shared.m_icons : null;
            return hammer = icons != null && icons.Length > 0 ? icons[0] : null;
        }
    }
}
