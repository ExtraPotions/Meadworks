// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/GamePanelTheme.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: dresses the game's panels only while KitLook asks for it
// (a product request or the Theme Game Panels setting); sprite and panel rules and the per-frame slice come from Core
// KitThemeRules/KitScan; no image scan at all until theming was first on; the kit's own panels are skipped.
// Modified by expDARE, 2026-10-13: dressed panels wear the forged set (calm window wood, straight edge, iron frame).
// Modified by expDARE, 2026-10-18: skip native-dialog images owned by the reversible shared dialog presenter.
// Modified by expDARE, 2026-10-19: dress only the game's own and MeadWorks panels, never other mods' (KitUiOwnership).
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Replaces native wood panel art with the kit's frame and wallpaper, retaining layout and controls. A scene load or
    /// a theme change looks at every image again, <see cref="KitScan.PerFrame"/> a frame rather than all in one; the
    /// sprites already judged are forgotten on a scene load, so the list never keeps a scene's runtime sprites alive.
    /// </summary>
    public static class GamePanelTheme
    {
        private static bool pending = true;
        private static bool everOn;
        private static Image[] scan;
        private static int scanned;
        private static readonly HashSet<Image> newlyEnabled = new HashSet<Image>();
        private static readonly Dictionary<Sprite, bool> woodSprites = new Dictionary<Sprite, bool>();
        private static readonly Dictionary<Image, FallbackState> dressed = new Dictionary<Image, FallbackState>();
        private static readonly List<Image> prune = new List<Image>();

        /// <summary>Theming is wanted now: Timber showing and the game's panels requested.</summary>
        public static bool On => KitLook.Timber && KitLook.ThemeGamePanels && KitArt.Panel != null;

        public static void Initialize()
        {
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                woodSprites.Clear();
                dressed.Clear();
                RequestRefresh();
            };
            KitLook.Changed += RequestRefresh;
        }

        public static void RequestRefresh() => pending = true;

        // Run after prefab setup, coalescing all panels enabled by one menu into one scan.
        // Inactive panels are included so switching back to Brown/vanilla restores them too.
        public static void Update()
        {
            if (!pending && scan == null && newlyEnabled.Count == 0)
                return;
            bool on = On;
            everOn |= on;
            if (pending)
            {
                pending = false;
                // Nothing was ever dressed: there is nothing to restore either, so the game's images are left unread.
                if (everOn)
                {
                    // Panels destroyed since the last scan leave dead keys; drop them before rescanning.
                    if (dressed.Count > 0)
                    {
                        prune.Clear();
                        foreach (Image image in dressed.Keys) if (image == null) prune.Add(image);
                        foreach (Image image in prune) dressed.Remove(image);
                        prune.Clear();
                    }
                    scan = Resources.FindObjectsOfTypeAll<Image>();
                    scanned = 0;
                }
            }
            if (scan != null)
                ScanSome(on);
            foreach (Image image in newlyEnabled)
                if (image != null && Themed(image))
                    Dress(image, on);
            newlyEnabled.Clear();
        }

        /// <summary>The next <see cref="KitScan.PerFrame"/> images of the scan; the scan ends with the last.</summary>
        private static void ScanSome(bool on)
        {
            int end = KitScan.End(scanned, scan.Length);
            for (; scanned < end; scanned++)
            {
                Image image = scan[scanned];
                if (image != null && Themed(image))
                    Dress(image, on);
            }
            if (scanned >= scan.Length)
                scan = null;
        }

        // The forged set covers the game's themed panels too (2026-10-13 11:09): calm wood and the iron frame, or the game's own art when off.
        private static void Dress(Image image, bool on)
        {
            if (on) TimberBackground.RegisterForged(image);
            TimberBackground.Apply(image, on);
            KitForged.GamePanel(image, on);
            if (on) dressed[image] = new FallbackState(image);
            else dressed.Remove(image);
        }

        /// <summary>Retire unchanged fallback decoration before a product captures this image's native appearance.</summary>
        public static bool TryRelease(Image image)
        {
            if (image == null || KitSkin.Owns(image) || KitNativeMenus.Owns(image) || KitNativeDialogs.Owns(image)) return false;
            if (dressed.TryGetValue(image, out var state))
            {
                if (!state.Matches(image) || !KitForged.TryReleaseGamePanel(image)) return false;
                TimberBackground.Apply(image, false);
                dressed.Remove(image);
                newlyEnabled.Remove(image);
                return true;
            }
            // A marker without our record belongs to another presenter, even if its name is familiar.
            if (image.transform.Find(KitThemeRules.WoodMarker) != null) return false;
            return KitForged.TryReleaseGamePanel(image);
        }

        sealed class FallbackState
        {
            readonly Sprite sprite;
            readonly Material material;
            readonly Image.Type type;
            readonly Color color;
            readonly bool fill;
            readonly float ppu;
            readonly TimberBackground wood;
            readonly Texture texture;
            readonly Material woodMaterial;
            readonly Color woodColor;
            readonly bool woodActive, woodEnabled, woodRaycast;
            readonly Vector2 size, position, min, max, pivot;
            readonly Vector3 scale;
            internal FallbackState(Image image)
            {
                sprite=image.sprite; material=image.material; type=image.type; color=image.color; fill=image.fillCenter; ppu=image.pixelsPerUnitMultiplier;
                var child=image.transform.Find(KitThemeRules.WoodMarker);
                wood=child!=null?child.GetComponent<TimberBackground>():null;
                if (wood==null) return;
                texture=wood.mainTexture; woodMaterial=wood.material; woodColor=wood.color;
                woodActive=wood.gameObject.activeSelf; woodEnabled=wood.enabled; woodRaycast=wood.raycastTarget;
                var rect=wood.rectTransform;
                size=rect.sizeDelta; position=rect.anchoredPosition; min=rect.anchorMin; max=rect.anchorMax; pivot=rect.pivot; scale=rect.localScale;
            }
            internal bool Matches(Image image)
            {
                if (image.sprite!=sprite || image.material!=material || image.type!=type || image.color!=color || image.fillCenter!=fill || image.pixelsPerUnitMultiplier!=ppu) return false;
                var child=image.transform.Find(KitThemeRules.WoodMarker);
                var current=child!=null?child.GetComponent<TimberBackground>():null;
                if (current!=wood) return false;
                if (wood==null) return true;
                var rect=wood.rectTransform;
                return wood.mainTexture==texture && wood.material==woodMaterial && wood.color==woodColor
                    && wood.gameObject.activeSelf==woodActive && wood.enabled==woodEnabled && wood.raycastTarget==woodRaycast
                    && rect.sizeDelta==size && rect.anchoredPosition==position && rect.anchorMin==min && rect.anchorMax==max && rect.pivot==pivot && rect.localScale==scale;
            }
        }

        // The kit's own panels follow the kit look alone (KitSkin), never this switch. Other mods' panels are never dressed.
        private static bool Themed(Image image) => image.gameObject.scene.IsValid() && image.canvas != null && !KitSkin.Owns(image) && !KitNativeDialogs.Owns(image) && !KitNativeMenus.Owns(image) && !KitNativeMenus.Reserves(image) && IsPanel(image) && KitUiOwnership.Styleable(image.transform);

        private static bool IsPanel(Image image)
        {
            bool marked = image.transform.Find(KitThemeRules.WoodMarker) != null;
            bool slicedOrTiled = image.type == Image.Type.Sliced || image.type == Image.Type.Tiled;
            // Cheap reads first; the sprite's and image's names are read last (each read makes a new string).
            if (!marked && !(slicedOrTiled && image.fillCenter && WoodSprite(image.sprite)))
                return false;
            return KitThemeRules.IsGamePanel(marked, slicedOrTiled, image.fillCenter, marked ? null : image.sprite.name, image.name);
        }

        /// <summary>
        /// One of the game's wood panel sprites, decided once per sprite (reading a sprite's name makes a new string).
        /// Selection masks, little projecting tabs and repair buttons are not panel backgrounds.
        /// </summary>
        private static bool WoodSprite(Sprite sprite)
        {
            if (sprite == null)
                return false;
            if (!woodSprites.TryGetValue(sprite, out bool wood))
            {
                wood = KitThemeRules.IsWoodSprite(sprite.name);
                woodSprites[sprite] = wood;
            }
            return wood;
        }

        [HarmonyPatch(typeof(Image), "OnEnable")]
        private static class PanelEnabled
        {
            private static void Postfix(Image __instance)
            {
                // Runs for every image shown anywhere (other mods' UI, map pins): the cheap reads come first, so almost
                // every one stops before a name is read or a child looked up. With theming off nothing new is dressed; a
                // theme change rescans every image anyway. Existing panels retain their theme and geometry while
                // hidden: opening one must not rescan every Image in the game or dirty unrelated menus. The cached sprite
                // check comes before the ownership checks, which walk the hierarchy and cost about 0.5 ms an image with
                // the inventory open (playtest 2026-10-20, item 25).
                if (!__instance.fillCenter || !On || !WoodSprite(__instance.sprite) || KitNativeDialogs.Owns(__instance) || KitNativeMenus.Owns(__instance) || KitNativeMenus.Reserves(__instance))
                    return;
                if (__instance.transform.Find(KitThemeRules.WoodMarker) == null && !KitSkin.Owns(__instance) && IsPanel(__instance) && KitUiOwnership.Styleable(__instance.transform))
                    newlyEnabled.Add(__instance);
            }
        }
    }
}
