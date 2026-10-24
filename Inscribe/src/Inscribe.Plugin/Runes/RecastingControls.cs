// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Inscribe.Adopted.Stones;
using ExtraPotions.Inscribe.Config;
using ExtraPotions.Inscribe.Runes;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InscribePreferences = ExtraPotions.Inscribe.Config.InscribeSettings;
using FocusText = ExtraPotions.Inscribe.Runtime.InscribeText;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    internal sealed class RecastingControls
    {
        private readonly Button? category;
        private readonly Button? side;

        internal RecastingControls(RectTransform parent, Rect area)
        {
            float width = Mathf.Max(1f, (area.width - 6f) / 2f);
            category = KitButton.Create(parent, "Inscribe_RecastingCategory", "", CycleCategory, Vector2.zero, new Vector2(width, area.height));
            side = KitButton.Create(parent, "Inscribe_RecastingSide", "", CycleSide, Vector2.zero, new Vector2(width, area.height));
            if (category != null) Rects.Place((RectTransform)category.transform, area.x, area.y, width, area.height);
            if (side != null) Rects.Place((RectTransform)side.transform, area.x + width + 6f, area.y, width, area.height);
            Show(false);
        }

        internal static RecastFocus Focus => Runtime.Plugin.Settings?.Get(InscribePreferences.Interface.RecastingFocus) ?? RecastFocus.Unrestricted;
        internal static RecastSide Side => Runtime.Plugin.Settings?.Get(InscribePreferences.Interface.RecastingSide) ?? RecastSide.Prefix;

        internal static StoneJob Apply(StoneJob job, Player player, string rune) =>
            rune == "recasting" ? job.WithFocus(player, Focus, Side) : job;

        internal void Show(bool visible)
        {
            if (category != null) category.gameObject.SetActive(visible);
            if (side != null) side.gameObject.SetActive(visible);
            if (!visible) return;
            Fill(category, FocusText.Text("recast_focus_" + (Focus == RecastFocus.Unrestricted ? "any" : Focus.ToString().ToLowerInvariant())),
                FocusText.Text("recast_focus_help"), true);
            Fill(side, FocusText.Text(Side == RecastSide.Prefix ? "recast_side_prefix" : "recast_side_suffix"),
                FocusText.Text("recast_side_help"), Focus != RecastFocus.Unrestricted);
        }

        private static void Fill(Button? button, string caption, string help, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = caption;
                label.enableAutoSizing = true;
                label.fontSizeMin = 12f;
                label.fontSizeMax = 18f;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
            KitTooltip.Set(button.gameObject, caption, help);
        }

        private static void CycleCategory() => TableWindow.Run(view =>
        {
            var settings = Runtime.Plugin.Settings;
            if (settings == null) return;
            var next = (RecastFocus)(((int)Focus + 1) % 4);
            MeadworksConfig.Amend(settings.Entry(InscribePreferences.Interface.RecastingFocus), next);
            TableWindow.MarkDirty();
        });

        private static void CycleSide() => TableWindow.Run(view =>
        {
            var settings = Runtime.Plugin.Settings;
            if (settings == null || Focus == RecastFocus.Unrestricted) return;
            MeadworksConfig.Amend(settings.Entry(InscribePreferences.Interface.RecastingSide), Side == RecastSide.Prefix ? RecastSide.Suffix : RecastSide.Prefix);
            TableWindow.MarkDirty();
        });
    }
}
