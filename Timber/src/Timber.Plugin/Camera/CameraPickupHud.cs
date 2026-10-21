// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original product adapter around Crucible.UI KX07 and the adopted CameraNeeds/pickup producer.
// Retains OpenKeep's one-second blocked-pickup notice behavior without copying its UI construction.
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Timber.Runtime;
using TMPro;
using UnityEngine;

namespace Timber.Camera
{
    public static class CameraPickupHud
    {
        static KitHudPanel? panel;
        static Transform? owner;
        static TMP_Text? label;
        static float expires;
        static int shownNeeds = -1;
        static string? shownLanguage;
        static float measuredCanvasWidth = -1f;

        // Called only after the actual camera pickup producer found native candidate items and unmet needs.
        public static void Blocked(Player player)
        {
            if (CameraState.IsOut(player) && CameraPrefs.PickupPanel.Value)
                expires = Time.unscaledTime + 1f;
        }

        public static void Tick()
        {
            var player = Player.m_localPlayer;
            var root = Hud.instance != null && Hud.instance.m_rootObject != null ? Hud.instance.m_rootObject.transform : null;
            if (panel != null && (panel.Root == null || owner != root)) Reset();
            bool visible = CrucibleUiPlugin.Active && CameraState.Active && CameraSettings.CameraPickup.Value && Player.m_enableAutoPickup &&
                CameraPrefs.PickupPanel.Value && player != null && Time.unscaledTime < expires &&
                !CameraNeeds.Met(player, CameraSettings.PickupNeedsResting, CameraSettings.PickupMinComfort);
            if (!visible || root == null || KitCanvas.Font == null)
            {
                panel?.Update(false, false, 1f, 1f);
                return;
            }
            if (panel == null)
            {
                panel = KitHudPanel.Create(root, "Timber_CameraPickup", new Vector2(320f, 64f), Vector2.zero, global::ExtraPotions.Timber.Runtime.Plugin.Guid, "Timber_CameraPickup");
                if (panel == null) return;
                owner = root;
                label = KitCanvas.Text(panel.Content, "Timber_CameraPickupText", "", 20f,
                    KitAppearance.Color(KitColorRole.OnPrimary), TextAlignmentOptions.Center, false);
                var rect = label.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(16f, 12f); rect.offsetMax = new Vector2(-16f, -12f);
                panel.SetInteractive(label, false);
            }
            var text = label;
            var hudPanel = panel;
            if (text == null || hudPanel == null) { Reset(); return; }
            var panelRect = hudPanel.Root;
            if (panelRect == null) { Reset(); return; }
            var language = MeadworksTranslations.GameLanguage();
            int needs = CameraNeeds.Key(player!, CameraSettings.PickupNeedsResting, CameraSettings.PickupMinComfort);
            float canvasWidth = root is RectTransform canvas ? canvas.rect.width : Screen.width;
            if (shownNeeds != needs || shownLanguage != language || !Mathf.Approximately(measuredCanvasWidth, canvasWidth))
            {
                shownNeeds = needs; shownLanguage = language;
                measuredCanvasWidth = canvasWidth;
                text.text = TimberText.Text("camera_pickup_needs") + ": " +
                    CameraNeeds.Describe(player!, CameraSettings.PickupNeedsResting, CameraSettings.PickupMinComfort);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                var wanted = text.GetPreferredValues(text.text);
                float width = Mathf.Min(Mathf.Max(160f, wanted.x + 32f), Mathf.Max(160f, canvasWidth - 32f));
                text.textWrappingMode = TextWrappingModes.Normal;
                var wrapped = text.GetPreferredValues(text.text, width - 32f, 0f);
                panelRect.sizeDelta = new Vector2(width, Mathf.Max(48f, wrapped.y + 24f));
            }
            // Product config is an offset from top-center (x right, y up); KX07 uses top-left (x right, y down).
            var offset = CameraPrefs.PickupPanelPosition.Value;
            hudPanel.SetPosition(new Vector2(canvasWidth * .5f - panelRect.sizeDelta.x * .5f + offset.x, -offset.y));
            hudPanel.Update(true, false, 1f, 1f);
        }

        public static void Reset()
        {
            panel?.Dispose(); panel = null; owner = null; label = null;
            expires = 0f; shownNeeds = -1; shownLanguage = null; measuredCanvasWidth = -1f;
        }
    }
}
