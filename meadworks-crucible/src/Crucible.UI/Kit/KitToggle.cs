// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// AX05: original rectangular switch presentation using existing KitButton/KitArt factories.
// Factory/art baseline: PackPanel 0.12.0 at 01821b8557e4a77e37f5433c885b1f122cae8249.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>A rectangular on/off switch. Setting an effective value never writes a setting.</summary>
    public sealed class KitToggle : IDisposable
    {
        public Button Button { get; }
        public bool Value { get; private set; }
        readonly Image thumb;
        readonly TMP_Text caption;
        readonly Action<bool> changed;
        string onText, offText, topic = "", tooltip = "", reason = "", blockedTopic = "";
        bool usable = true, disposed;

        KitToggle(Button button, bool value, Action<bool> changed, string onText, string offText)
        {
            Button = button; Value = value; this.changed = changed;
            this.onText = onText ?? KitText.Text(KitText.On, CrucibleUiPlugin.Language); this.offText = offText ?? KitText.Text(KitText.Off, CrucibleUiPlugin.Language);
            caption = button.GetComponentInChildren<TMP_Text>(true);
            var node = new GameObject("Crucible_switchThumb", typeof(RectTransform), typeof(Image));
            node.layer = button.gameObject.layer; node.transform.SetParent(button.transform, false);
            thumb = node.GetComponent<Image>(); thumb.raycastTarget = false;
            thumb.type = Image.Type.Sliced;
            KitLook.Changed += Refresh;
            Refresh();
        }

        /// <summary>Captions default to the kit's localized On and Off. Null before the kit UI is available.</summary>
        public static KitToggle Create(RectTransform parent, string name, bool value, Action<bool> changed,
            Vector2 at, Vector2 size, string onText = null, string offText = null)
        {
            if (size.x < size.y * 2 || size.y < 12 || !Finite(size.x) || !Finite(size.y))
                throw new ArgumentOutOfRangeException(nameof(size), "A switch needs a finite rectangular track at least twice its height.");
            if (parent != null && parent.Find(name) != null)
                throw new ArgumentException("The switch must own a new named control.", nameof(name));
            KitToggle result = null;
            var button = KitButton.Create(parent, name, "", () => result?.Click(), at, size);
            if (button == null) return null;
            result = new KitToggle(button, value, changed, onText, offText);
            return result;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void SetValue(bool value) { if (disposed) return; Value = value; Refresh(); }
        public void SetLabels(string onText, string offText)
        { this.onText = onText ?? ""; this.offText = offText ?? ""; Refresh(); }
        public void SetUsable(bool value, string reason = "")
        { usable = value; this.reason = reason ?? ""; Refresh(); }
        public void Tooltip(string topic, string text)
        { this.topic = topic ?? ""; tooltip = text ?? ""; Refresh(); }
        public KitControlDecision Block(BlockedOption option, bool usable = true)
        {
            var decision = KitControls.Decide(usable, option, CrucibleUiPlugin.Language);
            this.usable = decision.Interactable;
            blockedTopic = decision.TooltipTopic; reason = decision.Tooltip;
            Refresh(); return decision;
        }
        void Click()
        {
            if (disposed || !usable || Button == null || !Button.IsInteractable()) return;
            bool old = Value; SetValue(!old);
            // A rejected native write may call SetValue(effective) from this callback.
            try { changed?.Invoke(Value); }
            catch { SetValue(old); throw; }
        }
        /// <summary>Call after resizing the control; look changes also refresh it automatically.</summary>
        public void Refresh()
        {
            if (disposed || Button == null) return;
            Button.interactable = usable;
            GameButtons.Tint(Button, Value && usable);
            RectTransform track = (RectTransform)Button.transform;
            float side = Mathf.Max(6, track.rect.height - 6);
            thumb.sprite = KitArt.Button;
            thumb.color = usable ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1);
            var rect = thumb.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(side, side);
            rect.anchoredPosition = new Vector2((Value ? 1 : -1) * (track.rect.width - side - 6) * 0.5f, 0);
            if (caption != null)
            {
                caption.text = Value ? onText : offText;
                caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one;
                caption.rectTransform.offsetMin = new Vector2(Value ? 3 : side + 6, 0);
                caption.rectTransform.offsetMax = new Vector2(Value ? -side - 6 : -3, 0);
            }
            string detail = tooltip;
            if (!usable && reason.Length > 0 && reason != tooltip && !tooltip.EndsWith("\n" + reason, StringComparison.Ordinal))
                detail = tooltip.Length > 0 ? tooltip + "\n\n" + reason : reason;
            KitTooltip.Set(Button.gameObject, !usable && blockedTopic.Length > 0 ? blockedTopic : topic, detail);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; KitLook.Changed -= Refresh;
            if (Button != null) { Button.gameObject.SetActive(false); UnityEngine.Object.Destroy(Button.gameObject); }
        }
    }
}
