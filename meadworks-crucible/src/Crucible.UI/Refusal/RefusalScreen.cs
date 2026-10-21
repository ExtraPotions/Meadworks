// Adapted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/Charter/RefusalScreen.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: Charter's moment is kept (a stored refusal is shown once,
// after FejdStartup.ShowConnectError on the main menu) but the text is no longer written into the game's
// connection-failed label: a kit window lists each refusing product with what to install or update, built from the
// client's C04 verdict (Core RefusalScreenModel, seven languages), behind the Network.PeerClassHandshake flag.
using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// The C04 join refusal on the main menu: when the game reports the failed connection and the client had refused
    /// the server's product set, the game's connection-failed panel gives way to a kit window titled "Join refused",
    /// one row per product (what to install or update, both versions) and a pointer to the refusal code in the server's
    /// log. Shown once; Close, Esc or the gamepad's B closes it. Only with the <c>Network.PeerClassHandshake</c> flag on.
    /// </summary>
    [HarmonyPatch(typeof(FejdStartup), "ShowConnectError")]
    public static class RefusalScreen
    {
        public const string Id = "refusal";
        public const float Width = 560f;
        private const float Pad = 24f;
        private const float Gap = 10f;
        private const float TitleSize = 22f;
        private const float BodySize = 15f;
        private const float SmallSize = 12.5f;
        private const float ButtonWidth = 160f;

        private static KitWindow window;

        [HarmonyPostfix]
        public static void Postfix(FejdStartup __instance)
        {
            if (!CruciblePlugin.PeerClassHandshake || !CrucibleUiPlugin.Active)
                return;
            PeerClassVerdict verdict = MeadworksRefusals.Take();
            if (verdict == null)
                return;
            try
            {
                if (Show(__instance, RefusalScreenModel.From(verdict, CrucibleUiPlugin.Language)) != null && __instance.m_connectionFailedPanel != null)
                    __instance.m_connectionFailedPanel.SetActive(false);
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: showing the join refusal failed, the game's message stays: {e}");
            }
        }

        /// <summary>The refusal window for <paramref name="model"/> (also used by test scenarios); null when empty or headless.</summary>
        public static KitWindow Show(FejdStartup startup, RefusalScreenModel model)
        {
            if (model == null || model.Empty || !CrucibleUiPlugin.Active)
                return null;
            window?.Destroy();
            Transform parent = startup != null && startup.m_connectionFailedPanel != null ? startup.m_connectionFailedPanel.transform.parent : KitCanvas.Front;
            window = KitWindow.Create(Id, new Vector2(Width, 200f), parent);
            if (window == null)
                return null;
            RectTransform panel = window.Panel;
            // TextMeshPro measures nothing under an inactive parent; the window is built active and reopened below.
            panel.gameObject.SetActive(true);
            float inner = Width - Pad * 2f;
            float y = Pad;
            y += Line(panel, "title", model.Title, TitleSize, KitPalette.TabLitText, TextAlignmentOptions.Top, inner, y) + Gap;
            y += Line(panel, "intro", model.Intro, BodySize, KitPalette.SheetLabel, TextAlignmentOptions.TopLeft, inner, y) + Gap;
            for (int i = 0; i < model.Rows.Count; i++)
            {
                RefusalRow row = model.Rows[i];
                y += Line(panel, "row" + i, "<b>" + row.Product + "</b>: " + row.Text, BodySize, KitPalette.SheetValue, TextAlignmentOptions.TopLeft, inner, y);
                y += Line(panel, "versions" + i, row.Versions, SmallSize, KitPalette.SheetHeading, TextAlignmentOptions.TopLeft, inner, y) + Gap * 0.5f;
            }
            y += Gap;
            y += Line(panel, "footer", model.Footer, SmallSize, KitPalette.SheetLabel, TextAlignmentOptions.TopLeft, inner, y) + Gap * 1.5f;
            Button close = KitButton.Create(panel, "Crucible_close", model.Close, () => window?.Close(),
                new Vector2((Width - ButtonWidth) / 2f, -y), new Vector2(ButtonWidth, KitTabLayout.Height + 8f));
            y += KitTabLayout.Height + 8f + Pad;
            panel.sizeDelta = new Vector2(Width, y);
            KitPanel.Refresh(panel);
            window.Focusable(close, 0, 0);
            panel.gameObject.SetActive(false);
            window.Open();
            return window;
        }

        /// <summary>A wrapped text block across the window at <paramref name="y"/> down; its height.</summary>
        private static float Line(RectTransform panel, string name, string words, float size, KitColor colour, TextAlignmentOptions alignment, float width, float y)
        {
            TMP_Text text = KitCanvas.Text(panel, name, words, size, KitUnity.Colour(colour), alignment, true);
            float height = text.GetPreferredValues(words, width, 0f).y;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Pad, -y);
            rect.sizeDelta = new Vector2(width, height);
            return height;
        }
    }
}
