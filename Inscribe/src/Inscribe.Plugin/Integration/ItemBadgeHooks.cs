// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the icon badge and ground glint hooks left in place when enhancement was removed (Inscribe 0.2.0).
using System;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Data sources for the icon badge and the ground glint. The rendering (ivory icon badge, bronze and ivory glint) stayed
    /// when enhancement levels were removed. <see cref="InstallDefaults"/> points the glint at Legendary rarity (user decision
    /// 2026-10-19) and leaves the badge empty: the game already draws the quality level in each slot's top-right corner, so
    /// a second quality number would duplicate it. Rarity ticks, backdrops, seals and sockets never read these.
    /// </summary>
    internal static class ItemBadgeHooks
    {
        /// <summary>Short text for the icon's ivory badge (for example "3"); null or empty hides it.</summary>
        internal static Func<ItemDrop.ItemData, string?>? BadgeText;

        /// <summary>True when gear on the ground shows the small glint.</summary>
        internal static Func<ItemDrop.ItemData, bool>? Glint;

        internal static string Badge(ItemDrop.ItemData? item)
        {
            if (item == null || BadgeText == null) return "";
            try { return BadgeText(item) ?? ""; }
            catch (Exception) { return ""; }
        }

        internal static bool GlintFor(ItemDrop.ItemData? item)
        {
            if (item == null || Glint == null) return false;
            try { return Glint(item); }
            catch (Exception) { return false; }
        }

        /// <summary>Legendary glint (7 - Ground Glow / Legendary Glint); no badge provider.</summary>
        internal static void InstallDefaults()
        {
            BadgeText = null;
            Glint = item => Items.IconBadges.Glint(
                Plugin.Settings?.Get(Config.InscribeSettings.Glow.LegendaryGlint) == true, Adopted.Affixes.ItemState.Read(item).RarityId);
        }

        internal static void Reset()
        {
            BadgeText = null;
            Glint = null;
        }
    }
}
