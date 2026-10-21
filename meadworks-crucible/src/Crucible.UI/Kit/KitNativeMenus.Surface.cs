// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        /// <summary>
        /// Native text that keeps its own font when panels are styled: startup labels sized from their own text, the Credits
        /// list paragraphs and everything inside the EULA's scroll content (its link included). Both scroll views size their content from
        /// these font metrics, so a font swap would change native geometry and scroll position.
        /// </summary>
        static bool SizedFromContent(Transform t)
        {
            var fitter = t.GetComponent<ContentSizeFitter>();
            return fitter != null && fitter.enabled && (fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained || fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained);
        }

        public static bool KeepsReadingFont(TMP_Text label)
        {
            var startup = FejdStartup.instance;
            if (startup == null || label == null) return false;
            // A label whose own rect is sized from its text would change native geometry with another font.
            if (SizedFromContent(label.transform) || label.transform.parent != null && SizedFromContent(label.transform.parent)) return true;
            if (startup.m_creditsList != null && label.enableWordWrapping && label.transform.IsChildOf(startup.m_creditsList)) return true;
            if (startup.m_eulaWindow == null || startup.m_eulaWindow.m_window == null || !label.transform.IsChildOf(startup.m_eulaWindow.m_window.transform)) return false;
            var scroll = label.GetComponentInParent<ScrollRect>();
            return scroll != null && scroll.content != null && label.transform.IsChildOf(scroll.content);
        }
        sealed class Surface : IDisposable
        {
            internal readonly GameObject Root;
            readonly List<ImageLook> images = new List<ImageLook>();
            readonly List<ButtonLook> buttons = new List<ButtonLook>();
            readonly List<TextButtonLook> textButtons = new List<TextButtonLook>();
            readonly List<FontLook> fonts = new List<FontLook>();
            readonly List<SwitchLook> switches = new List<SwitchLook>();
            static readonly FieldInfo startupMenuButtons = typeof(FejdStartup).GetField("m_menuButtons", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            readonly HashSet<Button> capturedButtons = new HashSet<Button>();
            readonly Dictionary<Button, int> textLabelCounts = new Dictionary<Button, int>();
            bool applied, visible, pendingMainButtons;
            internal int Images => images.Count;
            internal int Buttons => buttons.Count + textButtons.Count;
            internal int Labels => fonts.Count;
            internal int Switches => switches.Count;
            internal Surface(GameObject root)
            {
                Root = root;
                RegisterSettingsDropdowns(root);
                CaptureButtons();
                foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
                    if (SurfaceNative(toggle.transform, root) && toggle.graphic != null && toggle.targetGraphic != null)
                        switches.Add(new SwitchLook(toggle));
                foreach (var control in root.GetComponentsInChildren<Selectable>(true))
                {
                    if (control is Button || control is Toggle || !SurfaceNative(control.transform, root)) continue;
                    var field = control.targetGraphic as Image;
                    if (field != null && !owned.Contains(field)) images.Add(new ImageLook(field, KitSurfaceRole.Field));
                    if (control is Slider slider)
                    {
                        var handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
                        if (handle != null && !owned.Contains(handle)) images.Add(new ImageLook(handle, KitSurfaceRole.Card));
                    }
                }
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (owned.Contains(image) || !SurfaceNative(image.transform, root)) continue;
                    if ((image.type == Image.Type.Sliced || image.type == Image.Type.Tiled) && image.fillCenter
                        && KitThemeRules.IsWoodSprite(image.sprite != null ? image.sprite.name : null)
                        && image.GetComponentInParent<Selectable>() == null)
                        images.Add(new ImageLook(image, KitSurfaceRole.Window, protectOffState: true));
                    else if (image.GetComponent<InputField>() != null || image.GetComponent<TMP_InputField>() != null)
                        images.Add(new ImageLook(image, KitSurfaceRole.Field));
                }
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                    if (SurfaceNative(label.transform, root) && !NativeCreditsParagraph(label, root)) fonts.Add(new FontLook(label));
            }
            static bool NativeCreditsParagraph(TMP_Text label, GameObject root)
            {
                var startup = FejdStartup.instance;
                return startup != null && KeepsReadingFont(label);
            }
            void CaptureButtons()
            {
                var root = Root;
                var startup = FejdStartup.instance;
                bool main = startup != null && root == startup.m_menuList;
                bool cinematics = startup != null && root == startup.m_cinematicsMenuList;
                var menuButtons = main ? startupMenuButtons?.GetValue(startup) as Button[] : null;
                pendingMainButtons = main && startupMenuButtons != null && (menuButtons == null || menuButtons.Length == 0);
                var nativeMain = menuButtons != null ? new HashSet<Button>(menuButtons) : null;
                var nativeTabs = new HashSet<Button>();
                foreach (var handler in root.GetComponentsInChildren<TabHandler>(true))
                {
                    if (!SurfaceNative(handler.transform, root)) continue;
                    foreach (var tab in handler.m_tabs)
                        if (tab.m_button != null) nativeTabs.Add(tab.m_button);
                }
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (capturedButtons.Contains(button) || !SurfaceNative(button.transform, root)) continue;
                    bool nativeAction = false;
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        var target = button.onClick.GetPersistentTarget(i);
                        if (target != null && target.GetType().Assembly == typeof(Menu).Assembly) nativeAction = true;
                    }
                    bool nativeTab = nativeTabs.Contains(button);
                    if (!nativeAction && !nativeTab && nativeMain?.Contains(button) != true && !cinematics) continue;
                    if (nativeTab)
                    {
                        var selected = button.transform.Find("Selected");
                        var selection = selected != null && SurfaceNative(selected, root) ? selected.GetComponent<Image>() : null;
                        if (selection != null && !owned.Contains(selection)) images.Add(new ImageLook(selection, KitSurfaceRole.ButtonHover));
                    }
                    capturedButtons.Add(button);
                    var image = button.targetGraphic as Image;
                    if (image != null && !owned.Contains(image)) buttons.Add(new ButtonLook(button, image));
                    else if (TextButtonLook.Supported)
                    {
                        var label = button.targetGraphic as TMP_Text;
                        if (button.targetGraphic == null)
                        {
                            int count = 0;
                            foreach (var child in button.GetComponentsInChildren<TMP_Text>(true))
                            {
                                if (child.GetComponentInParent<Button>() != button || !SurfaceNative(child.transform, root)) continue;
                                label = child; count++;
                            }
                            textLabelCounts[button] = count;
                            if (count != 1) label = null;
                        }
                        if (label != null) textButtons.Add(new TextButtonLook(button, label));
                    }
                }
            }
            internal void Refresh(bool on, bool dirty)
            {
                if (pendingMainButtons && Root != null)
                {
                    var startup = FejdStartup.instance;
                    var ready = startup != null && Root == startup.m_menuList ? startupMenuButtons.GetValue(startup) as Button[] : null;
                    if (ready != null && ready.Length > 0) { CaptureButtons(); dirty = true; }
                }
                bool now = Root != null && Root.activeInHierarchy;
                if (dirty || on != applied || now && !visible)
                {
                    foreach (var image in images) image.Apply(on);
                    foreach (var button in buttons) button.Apply(on);
                    foreach (var font in fonts) font.Apply(on);
                    applied = on;
                }
                foreach (var toggle in switches) toggle.Apply(on);
                foreach (var button in textButtons) button.Apply(on);
                visible = now;
            }
            internal bool NativeFont(TMP_Text label, out TMP_FontAsset font, out Material material)
            {
                foreach (var look in fonts) if (look.Describes(label, out font, out material)) return true;
                font = null; material = null; return false;
            }
            public void Dispose()
            {
                foreach (var toggle in switches) toggle.Dispose();
                foreach (var button in buttons) button.Dispose();
                foreach (var button in textButtons) button.Dispose();
                foreach (var image in images) image.Dispose();
                foreach (var font in fonts) font.Apply(false);
            }
        }
        sealed class ImageLook : IDisposable
        {
            internal readonly Image Image;
            readonly Sprite original;
            readonly Material material;
            readonly Image.Type type;
            readonly float ppu;
            readonly KitSurfaceRole role;
            readonly bool protectOffState;
            Sprite assigned;
            Material assignedMaterial;
            float assignedPpu;
            Image frame;
            bool applied, yielded;
            internal ImageLook(Image image, KitSurfaceRole role, bool protectOffState = false)
            {
                Image = image; this.role = role; this.protectOffState = protectOffState; original = image.sprite; material = image.material;
                type = image.type; ppu = image.pixelsPerUnitMultiplier; owned.Add(image);
            }
            bool Current => Image != null && Image.sprite == assigned && Image.material == assignedMaterial
                && Image.type == Image.Type.Sliced && Mathf.Approximately(Image.pixelsPerUnitMultiplier, assignedPpu);
            bool OriginalCurrent => Image != null && Image.sprite == original && Image.material == material
                && Image.type == type && Mathf.Approximately(Image.pixelsPerUnitMultiplier, ppu);
            internal bool Apply(bool on)
            {
                if (Image == null || yielded) return false;
                if (applied && !Current) { Restore(); yielded = true; return false; }
                if (!on) { Restore(); return false; }
                if (protectOffState && !applied && !OriginalCurrent) { yielded = true; return false; }
                var sprite = KitAppearance.Sprite(role);
                if (sprite == null) return false;
                assigned = sprite; Image.sprite = sprite; Image.material = null; assignedMaterial = Image.material; Image.type = Image.Type.Sliced;
                Fit(Image, KitAppearance.PixelsPerUnit(role)); assignedPpu = Image.pixelsPerUnitMultiplier; applied = true;
                var border = role == KitSurfaceRole.Window ? KitAppearance.Sprite(KitSurfaceRole.Frame) : null;
                if (frame != null) frame.gameObject.SetActive(border != null);
                if (border != null)
                {
                    if (frame == null)
                    {
                        var node = new GameObject("Crucible_native_frame", typeof(RectTransform), typeof(Image));
                        node.layer = Image.gameObject.layer;
                        var rect = (RectTransform)node.transform; rect.SetParent(Image.transform, false);
                        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                        rect.SetAsFirstSibling(); frame = node.GetComponent<Image>(); frame.raycastTarget = false; owned.Add(frame);
                    }
                    frame.sprite = border; frame.type = Image.Type.Sliced;
                    Fit(frame, KitAppearance.PixelsPerUnit(KitSurfaceRole.Frame)); frame.gameObject.SetActive(true);
                }
                return true;
            }
            void Restore()
            {
                if (frame != null) frame.gameObject.SetActive(false);
                if (Image == null || !applied) return;
                if (Image.sprite == assigned) Image.sprite = original;
                if (Image.material == assignedMaterial) Image.material = material;
                if (Image.type == Image.Type.Sliced) Image.type = type;
                if (Mathf.Approximately(Image.pixelsPerUnitMultiplier, assignedPpu)) Image.pixelsPerUnitMultiplier = ppu;
                applied = false;
            }
            public void Dispose()
            {
                Restore(); owned.Remove(Image);
                if (frame != null) { owned.Remove(frame); Object.Destroy(frame.gameObject); }
            }
        }
        sealed class ButtonLook : IDisposable
        {
            readonly Button button;
            readonly ImageLook image;
            readonly Selectable.Transition transition;
            readonly SpriteState sprites;
            bool applied, yielded;
            SpriteState assigned;
            internal ButtonLook(Button button, Image image)
            { this.button = button; this.image = new ImageLook(image, KitSurfaceRole.Button); transition = button.transition; sprites = button.spriteState; }
            internal void Apply(bool on)
            {
                if (button == null || yielded) return;
                if (applied && (button.transition != Selectable.Transition.SpriteSwap || !button.spriteState.Equals(assigned)))
                { image.Apply(false); Restore(); yielded = true; return; }
                if (!on) { image.Apply(false); Restore(); return; }
                if (!image.Apply(true)) { Restore(); yielded = true; return; }
                assigned = new SpriteState { highlightedSprite = KitAppearance.Sprite(KitSurfaceRole.ButtonHover),
                    selectedSprite = KitAppearance.Sprite(KitSurfaceRole.ButtonHover), pressedSprite = KitAppearance.Sprite(KitSurfaceRole.ButtonPressed),
                    disabledSprite = KitAppearance.Sprite(KitSurfaceRole.ButtonDisabled) };
                button.transition = Selectable.Transition.SpriteSwap; button.spriteState = assigned; applied = true;
            }
            void Restore()
            {
                if (button == null || !applied) return;
                if (button.spriteState.Equals(assigned)) button.spriteState = sprites;
                if (button.transition == Selectable.Transition.SpriteSwap) button.transition = transition;
                applied = false;
            }
            public void Dispose() { Restore(); image.Dispose(); }
        }
        sealed class FontLook
        {
            readonly TMP_Text label;
            readonly TMP_FontAsset font;
            readonly Material material;
            readonly KitFontRole role;
            TMP_FontAsset assigned;
            Material assignedMaterial;
            bool applied, yielded;
            internal FontLook(TMP_Text label)
            {
                this.label = label; font = label.font; material = label.fontSharedMaterial;
                role = label.fontSize >= 26 ? KitFontRole.Title : KitFontRole.Body;
            }
            internal bool Describes(TMP_Text other, out TMP_FontAsset nativeFont, out Material nativeMaterial)
            {
                nativeFont = font; nativeMaterial = material;
                return other != null && other == label;
            }
            internal void Apply(bool on)
            {
                if (label == null || yielded) return;
                if (applied && (label.font != assigned || label.fontSharedMaterial != assignedMaterial))
                { Restore(); yielded = true; return; }
                if (!on) { Restore(); return; }
                var next = KitAppearance.Font(role);
                if (next == null) return;
                assigned = next; assignedMaterial = next.material; label.font = next; label.fontSharedMaterial = assignedMaterial; applied = true;
            }
            void Restore()
            {
                if (label == null || !applied) return;
                var liveMaterial = label.fontSharedMaterial;
                if (label.font == assigned) label.font = font;
                label.fontSharedMaterial = liveMaterial == assignedMaterial ? material : liveMaterial;
                applied = false;
            }
        }
        static void Fit(Image image, float pixelsPerUnit) => KitAppearance.Fit(image, pixelsPerUnit);
    }
}
