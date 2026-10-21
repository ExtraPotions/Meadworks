// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        sealed class SwitchLook : IDisposable
        {
            readonly Toggle toggle;
            readonly Graphic check, target;
            readonly bool checkEnabled;
            readonly Selectable.Transition transition;
            readonly ColorBlock colors;
            readonly Color targetColor;
            readonly RectTransform root, thumb;
            readonly Image track, knob;
            bool applied, yielded;
            ColorBlock assignedColors;
            Color assignedColor;
            internal SwitchLook(Toggle toggle)
            {
                this.toggle = toggle; check = toggle.graphic; target = toggle.targetGraphic;
                checkEnabled = check.enabled; targetColor = target.color;
                transition = toggle.transition; colors = toggle.colors;
                if (check is Image checkImage) owned.Add(checkImage);
                if (target is Image targetImage) owned.Add(targetImage);
                var go = new GameObject("Crucible_native_switch", typeof(RectTransform), typeof(Image));
                go.layer = target.gameObject.layer;
                root = (RectTransform)go.transform; root.SetParent(target.transform, false);
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.sizeDelta = new Vector2(36, 18);
                track = go.GetComponent<Image>(); track.raycastTarget = false; track.type = Image.Type.Sliced; owned.Add(track);
                var child = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                child.layer = go.layer;
                thumb = (RectTransform)child.transform; thumb.SetParent(root, false);
                thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(.5f, .5f); thumb.sizeDelta = new Vector2(12, 12);
                knob = child.GetComponent<Image>(); knob.raycastTarget = false; knob.type = Image.Type.Sliced; owned.Add(knob);
                root.gameObject.SetActive(false);
            }
            internal void Apply(bool on)
            {
                if (toggle == null || root == null || yielded) return;
                if (applied && (toggle.targetGraphic != target || toggle.graphic != check
                    || toggle.transition != Selectable.Transition.ColorTint || !toggle.colors.Equals(assignedColors)
                    || target.color != assignedColor))
                { Restore(); yielded = true; return; }
                if (!on) { Restore(); return; }
                if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
                if (!applied)
                {
                    // Transparent native states prevent pointer/gamepad tint from briefly exposing the check box.
                    assignedColors = colors;
                    assignedColors.normalColor = Clear(colors.normalColor); assignedColors.highlightedColor = Clear(colors.highlightedColor);
                    assignedColors.pressedColor = Clear(colors.pressedColor); assignedColors.selectedColor = Clear(colors.selectedColor);
                    assignedColors.disabledColor = Clear(colors.disabledColor); assignedColors.fadeDuration = 0;
                    toggle.colors = assignedColors; toggle.transition = Selectable.Transition.ColorTint;
                    assignedColor = Clear(targetColor); target.color = assignedColor;
                    if (check != target) check.enabled = false;
                    applied = true;
                }
                // Runs five times a second per switch: only real changes are written, so an idle menu dirties no canvas.
                var trackSprite = KitAppearance.Sprite(KitSurfaceRole.Field);
                if (track.sprite != trackSprite) track.sprite = trackSprite;
                Fit(track, KitAppearance.PixelsPerUnit(KitSurfaceRole.Field));
                var knobSprite = KitAppearance.Sprite(KitSurfaceRole.Card);
                if (knob.sprite != knobSprite) knob.sprite = knobSprite;
                Fit(knob, KitAppearance.PixelsPerUnit(KitSurfaceRole.Card));
                float alpha = toggle.IsInteractable() ? 1f : .4f;
                var tc = KitAppearance.Color(toggle.isOn ? KitColorRole.Accent : KitColorRole.Muted); tc.a = alpha;
                if (track.color != tc) track.color = tc;
                var kc = KitAppearance.Color(KitColorRole.Text); kc.a = alpha;
                if (knob.color != kc) knob.color = kc;
                var at = new Vector2(toggle.isOn ? 9 : -9, 0);
                if (thumb.anchoredPosition != at) thumb.anchoredPosition = at;
                var centered = new Vector2(BoxCenter() - target.rectTransform.rect.width * .5f, 0);
                if (root.anchoredPosition != centered) root.anchoredPosition = centered;
            }
            // Centered on the check box the game draws, not on the target's rect: most settings toggles (Fullscreen,
            // VSync...) have a 109.5-unit-wide Background that draws a 20-unit box at its pivot side (preserve aspect),
            // and the rect's middle lies under the label.
            float BoxCenter()
            {
                var rect = target.rectTransform.rect;
                if (target is Image image && image.preserveAspect && image.sprite != null && image.type == Image.Type.Simple
                    && rect.height > 0 && image.sprite.rect.height > 0)
                {
                    float drawn = Mathf.Min(rect.width, rect.height * image.sprite.rect.width / image.sprite.rect.height);
                    return (rect.width - drawn) * target.rectTransform.pivot.x + drawn * .5f;
                }
                return rect.width * .5f;
            }
            static Color Clear(Color color) { color.a = 0; return color; }
            void Restore()
            {
                if (root != null) root.gameObject.SetActive(false);
                if (!applied) return;
                if (toggle != null)
                {
                    if (toggle.colors.Equals(assignedColors)) toggle.colors = colors;
                    if (toggle.transition == Selectable.Transition.ColorTint) toggle.transition = transition;
                }
                if (check != null && check != target && !check.enabled) check.enabled = checkEnabled;
                if (target != null && target.color == assignedColor) target.color = targetColor;
                applied = false;
            }
            public void Dispose()
            {
                Restore(); owned.Remove(track); owned.Remove(knob);
                if (check is Image checkImage) owned.Remove(checkImage);
                if (target is Image targetImage) owned.Remove(targetImage);
                if (root != null) Object.Destroy(root.gameObject);
            }
        }
    }
}
