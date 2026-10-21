// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using GUIFramework;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static readonly Dictionary<TMP_Dropdown, SettingsDropdown> settingsDropdowns = new Dictionary<TMP_Dropdown, SettingsDropdown>();
        static readonly List<TMP_Dropdown> obsoleteDropdowns = new List<TMP_Dropdown>();
        static readonly FieldInfo dropdownPopup = typeof(TMP_Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
        static bool dropdownHooked;
        static int dropdownGeneration;

        static void RegisterSettingsDropdowns(GameObject root)
        {
            var settings = root != null ? root.GetComponent<Settings>() : null;
            if (settings == null || dropdownPopup == null) return;
            if (!dropdownHooked) { dropdownHooked = true; KitLook.Changed += MarkDropdownsChanged; }
            foreach (var dropdown in root.GetComponentsInChildren<GuiDropdown>(true))
            {
                if (!Native(dropdown.transform, root) || dropdown.template == null || !Native(dropdown.template, root)
                    || dropdown.GetComponentInParent<Settings>(true) != settings || settingsDropdowns.ContainsKey(dropdown)) continue;
                settingsDropdowns.Add(dropdown, new SettingsDropdown(root, dropdown));
            }
        }
        static void MarkDropdownsChanged() { dropdownGeneration++; }
        static bool SurfaceNative(Transform node, GameObject root)
        {
            if (!Native(node, root)) return false;
            foreach (var look in settingsDropdowns.Values)
                if (look.Root == root && (look.Template != null && node.IsChildOf(look.Template)
                    || look.Popup != null && node.IsChildOf(look.Popup.transform))) return false;
            return true;
        }
        static void TickDropdowns()
        {
            obsoleteDropdowns.Clear();
            foreach (var pair in settingsDropdowns)
            {
                if (pair.Key == null || pair.Value.Root == null || !Native(pair.Key.transform, pair.Value.Root))
                { pair.Value.Dispose(); obsoleteDropdowns.Add(pair.Key); }
                else pair.Value.Tick();
            }
            foreach (var key in obsoleteDropdowns) settingsDropdowns.Remove(key);
            obsoleteDropdowns.Clear();
        }
        static void ShutdownDropdowns()
        {
            foreach (var look in settingsDropdowns.Values) look.Dispose();
            settingsDropdowns.Clear();
            obsoleteDropdowns.Clear();
            if (dropdownHooked) KitLook.Changed -= MarkDropdownsChanged;
            dropdownHooked = false;
        }
        static bool ReservesDropdowns(Image image)
        {
            if (image == null) return false;
            foreach (var look in settingsDropdowns.Values)
                if (look.Template != null && image.transform.IsChildOf(look.Template) && Native(image.transform, look.Root)
                    || look.Popup != null && image.transform.IsChildOf(look.Popup.transform) && Native(image.transform, look.Root)) return true;
            return false;
        }
        internal static object DropdownStatus
        {
            get
            {
                var rows = new List<object>();
                foreach (var look in settingsDropdowns.Values) rows.Add(look.Status);
                return new { registered = rows.Count, controls = rows.ToArray() };
            }
        }

        sealed class SettingsDropdown : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly RectTransform Template;
            internal readonly TMP_Dropdown Control;
            readonly List<TemplateImage> templateImages = new List<TemplateImage>();
            readonly List<TemplateFont> templateFonts = new List<TemplateFont>();
            readonly List<ImageLook> images = new List<ImageLook>();
            readonly HashSet<Image> capturedImages = new HashSet<Image>();
            readonly List<FontLook> fonts = new List<FontLook>();
            internal GameObject Popup;
            string refusal = "";
            bool showVerified, appliedOn;
            int appliedGeneration = -1;

            internal SettingsDropdown(GameObject root, TMP_Dropdown control)
            {
                Root = root; Control = control; Template = control.template;
                foreach (var image in Template.GetComponentsInChildren<Image>(true))
                {
                    if (!Native(image.transform, root) || owned.Contains(image)) { refusal = "Template appearance owner is not native"; return; }
                    templateImages.Add(new TemplateImage(image));
                }
                foreach (var font in Template.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!Native(font.transform, root)) { refusal = "Template font owner is not native"; return; }
                    templateFonts.Add(new TemplateFont(font));
                }
                if (templateImages.Count == 0 || templateFonts.Count == 0) refusal = "Native template appearance baseline unavailable";
                Popup = dropdownPopup.GetValue(control) as GameObject;
                if (Popup != null) refusal = "List already expanded before native template registration";
            }
            internal bool TemplateUntouched
            {
                get
                {
                    if (Template == null || Control == null || Control.template != Template) return false;
                    if (Template.GetComponentsInChildren<Image>(true).Length != templateImages.Count
                        || Template.GetComponentsInChildren<TMP_Text>(true).Length != templateFonts.Count) return false;
                    foreach (var image in templateImages) if (!image.Current) return false;
                    foreach (var font in templateFonts) if (!font.Current) return false;
                    return true;
                }
            }
            internal void BeforeShow()
            {
                showVerified = initialized && MeadworksHeadless.AllowsUi && Root != null && Root.activeInHierarchy
                    && Control != null && Native(Control.transform, Root) && refusal.Length == 0 && TemplateUntouched;
                if (!showVerified && refusal.Length == 0) refusal = "Template changed before native expansion";
            }
            internal void AfterShow()
            {
                var popup = Control != null ? dropdownPopup.GetValue(Control) as GameObject : null;
                if (popup == Popup) return;
                ReleasePopup();
                if (popup == null) return;
                Popup = popup;
                if (!showVerified || !TemplateUntouched || !Native(popup.transform, Root))
                { if (refusal.Length == 0) refusal = "Expanded list baseline or native ancestry unavailable"; return; }
                // Show has just cloned the untouched native template; never snapshot a themed clone as its original.
                var background = popup.GetComponent<Image>();
                if (background != null) CaptureImage(background, KitSurfaceRole.Window, protectOffState: true);
                foreach (var toggle in popup.GetComponentsInChildren<Toggle>(true))
                {
                    if (!Native(toggle.transform, Root)) continue;
                    if (toggle.targetGraphic is Image target && target != toggle.graphic) CaptureImage(target, KitSurfaceRole.Row);
                }
                foreach (var scrollbar in popup.GetComponentsInChildren<Scrollbar>(true))
                {
                    if (!Native(scrollbar.transform, Root)) continue;
                    var handle = scrollbar.handleRect != null ? scrollbar.handleRect.GetComponent<Image>() : null;
                    if (handle != null) CaptureImage(handle, KitSurfaceRole.Card);
                    if (scrollbar.targetGraphic is Image target) CaptureImage(target, KitSurfaceRole.Field);
                }
                foreach (var font in popup.GetComponentsInChildren<TMP_Text>(true))
                    if (Native(font.transform, Root))
                    {
                        if (!templateFonts.Exists(original => original.Matches(font))) refusal = "Expanded font differs from native template baseline";
                        else fonts.Add(new FontLook(font));
                    }
                if (refusal.Length != 0) { var rejected = Popup; ReleasePopup(); Popup = rejected; return; }
                Apply();
            }
            void CaptureImage(Image image, KitSurfaceRole role, bool protectOffState = false)
            {
                if (image == null || !Native(image.transform, Root)) return;
                if (capturedImages.Contains(image)) return;
                if (owned.Contains(image) || !templateImages.Exists(original => original.Matches(image)))
                { refusal = "Expanded image differs from native template baseline or is already owned"; return; }
                images.Add(new ImageLook(image, role, protectOffState)); capturedImages.Add(image);
            }
            void Apply()
            {
                foreach (var image in images) image.Apply(On);
                foreach (var font in fonts) font.Apply(On);
                appliedGeneration = dropdownGeneration; appliedOn = On;
            }
            internal void Tick()
            {
                var current = Control != null ? dropdownPopup.GetValue(Control) as GameObject : null;
                if (current != Popup || Popup == null && (images.Count != 0 || fonts.Count != 0))
                {
                    ReleasePopup();
                    // A list not observed by the native Show hook has no verified pre-fallback baseline.
                    if (current != null) { Popup = current; refusal = "Expanded list was not captured at native creation"; }
                    return;
                }
                if (Popup != null && images.Count + fonts.Count > 0 && (appliedOn != On || appliedGeneration != dropdownGeneration)) Apply();
            }
            void ReleasePopup()
            {
                foreach (var image in images) image.Dispose();
                foreach (var font in fonts) font.Apply(false);
                images.Clear(); capturedImages.Clear(); fonts.Clear(); Popup = null; appliedGeneration = -1;
            }
            internal object Status => new { root = Root != null ? Root.name : "", controlInstanceId = Control != null ? Control.GetInstanceID() : 0,
                templateInstanceId = Template != null ? Template.GetInstanceID() : 0, popupInstanceId = Popup != null ? Popup.GetInstanceID() : 0,
                expanded = Control != null && Control.IsExpanded, captured = images.Count + fonts.Count > 0,
                baselineVerified = showVerified && refusal.Length == 0, templateUntouched = TemplateUntouched, refusal, images = images.Count, labels = fonts.Count };
            public void Dispose() { ReleasePopup(); }
        }
        sealed class TemplateImage
        {
            readonly Image image;
            readonly Sprite sprite;
            readonly Material material;
            readonly Image.Type type;
            readonly float ppu;
            internal TemplateImage(Image image) { this.image = image; sprite = image.sprite; material = image.material; type = image.type; ppu = image.pixelsPerUnitMultiplier; }
            internal bool Current => image != null && image.sprite == sprite && image.material == material && image.type == type && image.pixelsPerUnitMultiplier == ppu;
            internal bool Matches(Image other) => other != null && other.sprite == sprite && other.material == material && other.type == type && other.pixelsPerUnitMultiplier == ppu;
        }
        sealed class TemplateFont
        {
            readonly TMP_Text font;
            readonly TMP_FontAsset asset;
            readonly Material material;
            internal TemplateFont(TMP_Text font) { this.font = font; asset = font.font; material = font.fontSharedMaterial; }
            internal bool Current => font != null && font.font == asset && font.fontSharedMaterial == material;
            internal bool Matches(TMP_Text other) => other != null && other.font == asset && other.fontSharedMaterial == material;
        }
        [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.Show))]
        static class NativeSettingsDropdownShow
        {
            static void Prefix(TMP_Dropdown __instance)
            { if (settingsDropdowns.TryGetValue(__instance, out var look)) look.BeforeShow(); }
            static void Postfix(TMP_Dropdown __instance)
            { if (settingsDropdowns.TryGetValue(__instance, out var look)) look.AfterShow(); }
        }
    }
}
