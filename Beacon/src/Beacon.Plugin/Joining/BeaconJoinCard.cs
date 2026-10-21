// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: BC05 center countdown and reconnect panel with an explicit Cancel control.
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Beacon.Joining;
using ExtraPotions.Beacon.Localization;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// One center kit panel on the start screen's canvas. Nothing is created on a headless process. It is destroyed
    /// when nothing is counting down or waiting, on cancel, on join and on teardown.
    /// </summary>
    internal static class BeaconJoinCard
    {
        const float Width = 420f;
        static KitWindow? card;
        static TMP_Text? text;
        static string shown = "";

        public static void Show(IReadOnlyList<CardLine> lines)
        {
            if (!MeadworksHeadless.AllowsUi) return;
            var words = string.Join("\n", lines.Select(l => BeaconText.Format(l.Key, l.Arguments.ToArray())));
            if (card == null || card.Panel == null || text == null)
            {
                Remove();
                var canvas = FejdStartup.instance?.m_mainMenu?.GetComponentInParent<Canvas>()?.rootCanvas;
                if (canvas == null) return;
                card = KitWindow.Create("BeaconJoinCard", new Vector2(Width, 220), canvas.transform);
                if (card == null) return;
                text = KitAlmanacDialog.Label(card.Panel, "Lines", words, new Vector2(20, -20), new Vector2(Width - 40, 120), 18);
                text.alignment = TextAlignmentOptions.Center;
                var cancel = KitButton.Create(card.Panel, "Cancel", BeaconPanel.Ui("cancel"), () => BeaconAutoJoin.Cancel(Plugin.Log), new Vector2(100, -160), new Vector2(Width - 200, 38));
                card.Focusable(cancel, 0, 0);
                BeaconPanel.Tip(cancel.gameObject,"cancel",BeaconText.Text(BeaconTextKeys.CardCancelHint));
                card.Closed += Cancel;
                card.Open();
                shown = "";
            }
            if (words == shown && card.IsOpen) return;
            text.text = words;
            shown = words;
            card.Open();
        }

        static void Cancel() => BeaconAutoJoin.Cancel(Plugin.Log);

        public static void Remove()
        {
            if (card != null) { card.Closed -= Cancel; card.Close(); card.Destroy(); }
            card = null;
            text = null;
            shown = "";
        }
    }
}
