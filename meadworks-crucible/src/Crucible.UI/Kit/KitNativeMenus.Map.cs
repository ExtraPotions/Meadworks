// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static MapControls mapControls;
        static int mapGeneration;
        static void MarkMapChanged() { mapGeneration++; }
        internal static object MapStatus => mapControls?.Status;
        static bool ReservesMap(Image image)
        {
            var map = Minimap.instance;
            var toggle = map != null ? map.m_publicPosition : null;
            return image != null && toggle != null && MapNative(toggle.transform, map.m_largeRoot)
                && (image == toggle.targetGraphic || image == toggle.graphic)
                && Native(image.transform, toggle.gameObject);
        }
        static void TickMap()
        {
            if (!MeadworksHeadless.AllowsUi) { ShutdownMap(); return; }
            var map = Minimap.instance;
            if (mapControls != null && !mapControls.Matches(map)) { mapControls.Dispose(); mapControls = null; }
            if (mapControls == null && map != null && ((map.m_smallRoot != null && map.m_smallRoot.activeInHierarchy)
                || (map.m_largeRoot != null && map.m_largeRoot.activeInHierarchy))) mapControls = new MapControls(map);
            mapControls?.Apply(On);
        }
        static void ShutdownMap() { mapControls?.Dispose(); mapControls = null; }
        static bool MapNative(Transform at, GameObject root)
        {
            if (at == null || root == null || !at.IsChildOf(root.transform)) return false;
            for (var parent = at; parent != null; parent = parent.parent)
            {
                if (parent.name.StartsWith("Cairn_", StringComparison.Ordinal)) return false;
                if (parent == root.transform) return Native(at, root);
            }
            return false;
        }
        sealed class MapControls : IDisposable
        {
            readonly Minimap map;
            readonly GameObject small, large;
            readonly TMP_Text smallBiome, largeBiome;
            readonly Toggle privacy;
            readonly List<FontLook> fonts = new List<FontLook>();
            readonly HashSet<TMP_Text> capturedBiomes = new HashSet<TMP_Text>();
            SwitchLook privacyLook;
            Graphic target, check;
            bool privacyAttempted, paintedOn;
            int paintedGeneration = -1;
            string privacyReason = "awaiting-visible-native-privacy-control";
            internal object Status => new { smallVisible = small != null && small.activeInHierarchy,
                largeVisible = large != null && large.activeInHierarchy, labels = fonts.Count,
                privacyCaptured = privacyLook != null, privacyReason, on = On,
                scope = "exact-biome-and-privacy-label-fonts/native-privacy-switch-only" };
            internal MapControls(Minimap map)
            {
                this.map = map; small = map.m_smallRoot; large = map.m_largeRoot;
                smallBiome = map.m_biomeNameSmall; largeBiome = map.m_biomeNameLarge; privacy = map.m_publicPosition;

            }
            internal bool Matches(Minimap current) => current != null && current == map && current.m_smallRoot == small
                && current.m_largeRoot == large && current.m_biomeNameSmall == smallBiome && current.m_biomeNameLarge == largeBiome
                && current.m_publicPosition == privacy;
            void CaptureBiome(TMP_Text label, GameObject root)
            {
                if (label == null || !label.isActiveAndEnabled || capturedBiomes.Contains(label) || !MapNative(label.transform, root)) return;
                // Capture after native activation initializes the previously hidden label material.
                capturedBiomes.Add(label); fonts.Add(new FontLook(label)); paintedGeneration = -1;
            }
            void CapturePrivacy()
            {
                if (privacyAttempted || privacy == null || !privacy.gameObject.activeInHierarchy) return;
                privacyAttempted = true;
                target = privacy.targetGraphic; check = privacy.graphic;
                if (!MapNative(privacy.transform, large) || !(target is Image targetImage) || !(check is Image checkImage)
                    || !Native(target.transform, privacy.gameObject) || !Native(check.transform, privacy.gameObject))
                { privacyReason = "unknown-native-privacy-hierarchy-or-graphics"; return; }
                if (owned.Contains(targetImage) || owned.Contains(checkImage) || KitSkin.Owns(targetImage) || KitSkin.Owns(checkImage)
                    || KitNativeDialogs.Owns(targetImage) || KitNativeDialogs.Owns(checkImage)
                    || !GamePanelTheme.TryRelease(targetImage) || (checkImage != targetImage && !GamePanelTheme.TryRelease(checkImage)))
                { privacyReason = "privacy-appearance-already-owned-or-foreign"; return; }
                privacyLook = new SwitchLook(privacy);
                foreach (var label in privacy.GetComponentsInChildren<TMP_Text>(true))
                    if (MapNative(label.transform, large)) fonts.Add(new FontLook(label));
                privacyReason = "exact-native-public-position-toggle"; paintedGeneration = -1;
            }
            internal void Apply(bool on)
            {
                CaptureBiome(smallBiome, small); CaptureBiome(largeBiome, large);
                CapturePrivacy();
                if (privacyLook != null && (target == null || check == null || privacy.targetGraphic != target || privacy.graphic != check))
                {
                    privacyLook.Dispose(); privacyLook = null; privacyReason = "privacy-binding-changed-yielded";
                }
                privacyLook?.Apply(on);
                if (paintedGeneration != mapGeneration || paintedOn != on)
                {
                    foreach (var font in fonts) font.Apply(on);
                    paintedGeneration = mapGeneration; paintedOn = on;
                }
            }
            public void Dispose() { privacyLook?.Dispose(); foreach (var font in fonts) font.Apply(false); }
        }
    }
}
