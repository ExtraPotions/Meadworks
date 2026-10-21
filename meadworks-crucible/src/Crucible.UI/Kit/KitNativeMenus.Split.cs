// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static readonly Dictionary<string, System.Reflection.FieldInfo> splitFields = Fields(typeof(SplitDialog),
            "m_panel", "m_splitOkButton", "m_splitCancelButton", "m_splitAmount", "m_splitIconName", "m_splitIcon");
        static SplitSurface splitSurface;
        static int splitGeneration;
        static void MarkSplitChanged() { splitGeneration++; }
        static T SplitField<T>(SplitDialog host, string name) where T : class => splitFields[name]?.GetValue(host) as T;
        internal static object SplitStatus => splitSurface?.Status;
        static bool ReservesSplit(Image image)
        {
            var gui = InventoryGui.instance;
            var host = gui != null ? gui.m_splitDialog : null;
            return host != null && image != null && Native(image.transform, host.gameObject);
        }
        static void TickSplit()
        {
            if (!MeadworksHeadless.AllowsUi) { ShutdownSplit(); return; }
            var gui = InventoryGui.instance;
            var host = gui != null ? gui.m_splitDialog : null;
            if (splitSurface != null && !splitSurface.Matches(host)) { splitSurface.Dispose(); splitSurface = null; }
            if (splitSurface == null && host != null && host.IsActive) splitSurface = new SplitSurface(host);
            splitSurface?.Apply(On);
        }
        static void ShutdownSplit() { splitSurface?.Dispose(); splitSurface = null; }
        static Image VisibleSplitBacking(RectTransform panel)
        {
            if (panel == null) return null;
            Image found = null;
            bool seen = false;
            for (int index = 0; index < panel.childCount; index++)
            {
                var child = panel.GetChild(index);
                if (child.name != "border (1)") continue;
                if (seen) return null;
                seen = true; found = child.GetComponent<Image>();
            }
            return found;
        }
        sealed class SplitSurface : System.IDisposable
        {
            readonly SplitDialog host;
            internal readonly GameObject Root;
            readonly RectTransform panel;
            readonly Button ok, cancel;
            readonly Slider slider;
            readonly Image icon;
            readonly TMP_Text amount, iconName;
            readonly Graphic sliderGraphic;
            readonly List<ImageLook> images = new List<ImageLook>();
            readonly List<BookButton> buttons = new List<BookButton>();
            readonly Dictionary<TMP_Text, FontLook> fonts = new Dictionary<TMP_Text, FontLook>();
            Image background;
            string reason;
            int generation = -1;
            bool paintedOn, yielded;
            internal object Status => new { present = host != null, visible = host != null && host.IsActive,
                captured = background != null, images = images.Count, buttons = buttons.Count, labels = fonts.Count,
                panel = panel != null ? panel.name : "", backing = background != null ? background.name : "", reason, yielded, on = On };
            internal SplitSurface(SplitDialog host)
            {
                this.host = host; Root = host.gameObject;
                panel = SplitField<RectTransform>(host, "m_panel");
                ok = SplitField<Button>(host, "m_splitOkButton"); cancel = SplitField<Button>(host, "m_splitCancelButton");
                slider = host.m_splitSlider; icon = SplitField<Image>(host, "m_splitIcon");
                amount = SplitField<TMP_Text>(host, "m_splitAmount"); iconName = SplitField<TMP_Text>(host, "m_splitIconName");
                sliderGraphic = slider != null ? slider.targetGraphic : null;
                if (panel == null || ok == null || cancel == null || slider == null || icon == null || amount == null || iconName == null
                    || !Native(panel, Root) || !Native(ok.transform, Root) || !Native(cancel.transform, Root) || !Native(slider.transform, Root)
                    || !ok.transform.IsChildOf(panel) || !cancel.transform.IsChildOf(panel) || !slider.transform.IsChildOf(panel))
                { reason = "native-control-bindings-unverified"; return; }
                var image = VisibleSplitBacking(panel);
                var canvas = panel.GetComponentInParent<Canvas>(); var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
                if (image == null || image == icon || image.sprite == null || !image.enabled || !image.gameObject.activeInHierarchy || image.color.a <= 0 || image.canvasRenderer.GetAlpha() <= 0 || canvasRect == null
                    || panel.rect.width <= 0 || panel.rect.height <= 0 || panel.rect.width >= canvasRect.rect.width || panel.rect.height >= canvasRect.rect.height)
                { reason = "bounded-native-panel-image-unverified"; return; }
                if (owned.Contains(image) || KitSkin.Owns(image) || KitNativeDialogs.Owns(image) || !GamePanelTheme.TryRelease(image))
                { reason = "background-owned-or-handoff-refused"; return; }
                if (!image.fillCenter) { reason = "native-panel-fill-unverified"; return; }
                background = image; images.Add(new ImageLook(image, KitSurfaceRole.Window, protectOffState: true));
                buttons.Add(new BookButton(ok, Root)); buttons.Add(new BookButton(cancel, Root));
                AddFont(amount); AddFont(iconName);
                foreach (var button in new[] { ok, cancel })
                {
                    var labels = button.GetComponentsInChildren<TMP_Text>(true);
                    if (labels.Length == 1) AddFont(labels[0]);
                }
                AddChrome(slider.GetComponent<Image>(), KitSurfaceRole.Field);
                AddChrome(slider.targetGraphic as Image, KitSurfaceRole.Card);
                reason = "verified-native-split-dialog";
            }
            void AddFont(TMP_Text label)
            { if (label != null && Native(label.transform, Root) && !fonts.ContainsKey(label)) fonts.Add(label, new FontLook(label)); }
            void AddChrome(Image image, KitSurfaceRole role)
            {
                if (image != null && image != icon && image != background && Native(image.transform, Root)
                    && !owned.Contains(image) && !KitSkin.Owns(image) && !KitNativeDialogs.Owns(image)) images.Add(new ImageLook(image, role));
            }
            internal bool Matches(SplitDialog current) => host != null && current == host && SplitField<RectTransform>(host, "m_panel") == panel
                && SplitField<Button>(host, "m_splitOkButton") == ok && SplitField<Button>(host, "m_splitCancelButton") == cancel
                && host.m_splitSlider == slider && SplitField<Image>(host, "m_splitIcon") == icon
                && SplitField<TMP_Text>(host, "m_splitAmount") == amount && SplitField<TMP_Text>(host, "m_splitIconName") == iconName;
            internal void Apply(bool on)
            {
                if (background == null || yielded) return;
                if (panel == null || VisibleSplitBacking(panel) != background || slider == null || slider.targetGraphic != sliderGraphic)
                { Dispose(); yielded = true; reason = "panel-or-slider-image-binding-changed"; return; }
                if (generation != splitGeneration || paintedOn != on)
                {
                    foreach (var image in images) image.Apply(on); foreach (var button in buttons) button.Apply(on);
                    foreach (var font in fonts.Values) font.Apply(on);
                    generation = splitGeneration; paintedOn = on;
                }
                else foreach (var button in buttons) button.FollowText(on);
            }
            public void Dispose()
            { foreach (var image in images) image.Dispose(); foreach (var button in buttons) button.Dispose(); foreach (var font in fonts.Values) font.Apply(false); }
        }
    }
}
