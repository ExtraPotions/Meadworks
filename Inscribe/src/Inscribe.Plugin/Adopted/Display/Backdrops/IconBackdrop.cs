// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Runtime;
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// The rarity backdrop behind one item icon (display.md section 2, "Icon backdrop"): an Image of
    /// <see cref="BackdropArt"/> in <see cref="BackdropTone"/>, made once per icon as a sibling of it, just above its
    /// cell's own background (<c>bkg</c>) or first, so every marker and the icon draw over it. It covers the icon's rect
    /// less 3 of every 64 units on each side (58 of the game's 64-unit icon), follows the icon's rect (fitted when made and
    /// whenever the icon's size changes), and shows only while the icon does. Display only, on the viewing client.
    /// Per-frame callers pay the custom-data test first; <see cref="Hide"/> is one int test while no backdrop shows.
    /// </summary>
    internal static class IconBackdrop
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(SlotBackdrops.GridPatch)).Patch();
            harmony.CreateClassProcessor(typeof(SlotBackdrops.HotbarPatch)).Patch();
            harmony.CreateClassProcessor(typeof(PanelBackdrops.DragPatch)).Patch();
            harmony.CreateClassProcessor(typeof(PanelBackdrops.RadialPatch)).Patch();
            harmony.CreateClassProcessor(typeof(CraftingPanel.UpdateRecipePatch)).Patch();
            harmony.CreateClassProcessor(typeof(CraftingPanel.RecipeListPatch)).Patch();
            harmony.CreateClassProcessor(typeof(MessageBackdrops.ItemMessagePatch)).Patch();
            harmony.CreateClassProcessor(typeof(MessageBackdrops.QueuePatch)).Patch();
            harmony.CreateClassProcessor(typeof(MessageBackdrops.ShowPatch)).Patch();
        }

        private const string Name = "ecf_backdrop";
        private const float Inset = (64f - BackdropArt.Span) / 2f / 64f;

        private static readonly Dictionary<Image, Made> Backdrops = new Dictionary<Image, Made>();
        private static readonly Dictionary<Image, Tracked> Items = new Dictionary<Image, Tracked>();
        private static readonly List<Image> StaleItems = new List<Image>();
        private static readonly List<Image> Gone = new List<Image>();
        private static int _sweepAt = 64;
        private static int _itemSweepAt = 64;
        private static int _shown;

        private sealed class Tracked
        {
            internal ItemDrop.ItemData Item = null!;
            internal float FadeUntil;
            internal float FadeSeconds;
        }

        /// <summary>A made backdrop and the icon size it was last fitted to.</summary>
        private sealed class Made
        {
            public Image Backdrop = null!;
            public Vector2 FittedSize = new Vector2(-1f, -1f);
        }

        /// <summary>
        /// Shows the backdrop for the item while the icon shows, else hides it, and the seal mark and the socket marks with
        /// it (<see cref="IconSeal"/>, <see cref="IconSockets"/>). Called per frame by most surfaces.
        /// </summary>
        public static void Set(Image? icon, ItemDrop.ItemData? item)
        {
            if (icon == null)
            {
                return;
            }
            if (item == null || !Visible(icon))
            {
                Hide(icon);
                return;
            }
            Remember(icon, item);
            if (!Plugin.AllowsUi || !InscribeModules.Active(InscribeModule.Display))
            {
                HideVisuals(icon);
                return;
            }
            bool ownRarity = !InscribeModules.StepAside.SteppedAside(InscribeModule.Rarity);
            if (ownRarity && BackdropTone.TryGet(item, out Color tone))
            {
                Show(icon, tone);
            }
            else
            {
                HideBackdrop(icon);
            }
            if (ownRarity && IconSeal.IsSealed(item))
            {
                IconSeal.Show(icon, IconIdentification.HasBadge(item) && item!.m_shared.m_maxStackSize <= 1);
            }
            else
            {
                IconSeal.Hide(icon);
            }
            IconSockets.Set(icon, ownRarity ? item : null);
            IconIdentification.Set(icon, item, ownRarity);
        }

        public static void Hide(Image? icon)
        {
            if (icon != null) Items.Remove(icon);
            HideVisuals(icon);
        }

        internal static void HideVisuals(Image? icon)
        {
            IconEquipped.Hide(icon);
            IconSeal.Hide(icon);
            IconSockets.Hide(icon);
            IconIdentification.Hide(icon);
            HideBackdrop(icon);
        }

        internal static void RefreshAll()
        {
            // Set can forget hidden or destroyed icons while this settings-change snapshot is replayed.
            var snapshot = new KeyValuePair<Image, Tracked>[Items.Count];
            ((ICollection<KeyValuePair<Image, Tracked>>)Items).CopyTo(snapshot, 0);
            foreach (var pair in snapshot)
            {
                Image icon = pair.Key;
                if (icon == null) { Items.Remove(pair.Key); continue; }
                Set(icon, pair.Value.Item);
                if (!Items.ContainsKey(icon) || pair.Value.FadeSeconds <= 0f) continue;
                float remaining = Mathf.Max(0f, pair.Value.FadeUntil - Time.unscaledTime);
                FadeGraphics(icon, remaining, Mathf.Clamp01(remaining / pair.Value.FadeSeconds));
            }
        }

        private static void Remember(Image icon, ItemDrop.ItemData item)
        {
            if (Items.TryGetValue(icon, out Tracked tracked))
            {
                if (!ReferenceEquals(tracked.Item, item))
                {
                    tracked.Item = item;
                    tracked.FadeUntil = tracked.FadeSeconds = 0f;
                }
                return;
            }
            if (Items.Count >= _itemSweepAt)
            {
                StaleItems.Clear();
                foreach (Image key in Items.Keys)
                    if (key == null) StaleItems.Add(key);
                foreach (Image key in StaleItems) Items.Remove(key);
                _itemSweepAt = Math.Max(64, Items.Count * 2);
            }
            Items[icon] = new Tracked { Item = item };
        }

        private static void HideBackdrop(Image? icon)
        {
            if (_shown == 0 || icon == null || !Backdrops.TryGetValue(icon, out Made made))
            {
                return;
            }
            Image backdrop = made.Backdrop;
            if (backdrop != null && backdrop.enabled)
            {
                backdrop.enabled = false;
                _shown--;
            }
        }

        internal static void HideAll()
        {
            foreach (Made made in Backdrops.Values)
                if (made.Backdrop != null) made.Backdrop.enabled = false;
            _shown = 0;
            IconEquipped.HideAll();
            IconSeal.HideAll();
            IconSockets.HideAll();
            IconIdentification.HideAll();
        }

        internal static void Shutdown()
        {
            foreach (Made made in Backdrops.Values)
                if (made.Backdrop != null) KitUnity.Retire(made.Backdrop.gameObject);
            Backdrops.Clear();
            Items.Clear();
            StaleItems.Clear();
            Gone.Clear();
            _shown = 0;
            _sweepAt = 64;
            _itemSweepAt = 64;
            IconEquipped.Shutdown();
            IconSeal.Shutdown();
            IconSockets.Shutdown();
            IconIdentification.Shutdown();
            MessageBackdrops.Shutdown();
        }

        internal static void Fade(Image icon, float seconds)
        {
            if (Items.TryGetValue(icon, out Tracked tracked))
            {
                tracked.FadeUntil = Time.unscaledTime + seconds;
                tracked.FadeSeconds = seconds;
            }
            FadeGraphics(icon, seconds, 1f);
        }

        private static void FadeGraphics(Image icon, float seconds, float alpha)
        {
            if (Backdrops.TryGetValue(icon, out Made made) && made.Backdrop != null) FadeGraphic(made.Backdrop, seconds, alpha);
            IconSeal.Fade(icon, seconds, alpha);
            IconSockets.Fade(icon, seconds, alpha);
            IconIdentification.Fade(icon, seconds, alpha);
        }

        internal static void FadeGraphic(Graphic graphic, float seconds, float alpha)
        {
            graphic.canvasRenderer.SetAlpha(alpha);
            graphic.CrossFadeAlpha(0f, seconds, true);
        }

        /// <summary>The icon's backdrop, made on first use, fitted to the icon and shown in the tone.</summary>
        public static Image Show(Image icon, Color tone)
        {
            Made made = Get(icon);
            Image backdrop = made.Backdrop;
            RectTransform iconRect = icon.rectTransform;
            Vector2 size = iconRect.rect.size;
            if (size != made.FittedSize)
            {
                made.FittedSize = size;
                Fit((RectTransform)backdrop.transform, iconRect, size);
            }
            if (backdrop.color != tone)
            {
                backdrop.color = tone;
            }
            if (!backdrop.enabled)
            {
                backdrop.enabled = true;
                _shown++;
            }
            return backdrop;
        }

        /// <summary>
        /// Whether Inscribe's cell look is drawn on this icon now: the icon shows, the UI and the display module run, and
        /// Inscribe's rarity has not stepped aside. The equipped ring replaces the game's blue fill only then.
        /// </summary>
        internal static bool DrawsLook(Image icon) => Visible(icon) && Plugin.AllowsUi && InscribeModules.Active(InscribeModule.Display)
            && !InscribeModules.StepAside.SteppedAside(InscribeModule.Rarity);

        private static bool Visible(Image icon) =>
            icon.enabled && icon.gameObject.activeSelf && icon.sprite != null && icon.color.a > 0.01f;

        private static Made Get(Image icon)
        {
            if (Backdrops.TryGetValue(icon, out Made made) && made.Backdrop != null)
            {
                return made;
            }
            Sweep();
            made = new Made { Backdrop = Make(icon) };
            Backdrops[icon] = made;
            return made;
        }

        /// <summary>Forgets icons the game destroyed (recipe lists are rebuilt, drag ghosts come and go), now and then.</summary>
        private static void Sweep()
        {
            if (Backdrops.Count < _sweepAt)
            {
                return;
            }
            CollectGone();
            foreach (Image icon in Gone)
            {
                Backdrops.Remove(icon);
            }
            _sweepAt = Math.Max(64, Backdrops.Count * 2);
        }

        /// <summary>Lists the destroyed pairs in <see cref="Gone"/> and recounts the backdrops showing.</summary>
        private static void CollectGone()
        {
            Gone.Clear();
            _shown = 0;
            foreach (KeyValuePair<Image, Made> pair in Backdrops)
            {
                Image backdrop = pair.Value.Backdrop;
                if (pair.Key == null || backdrop == null)
                {
                    Gone.Add(pair.Key!);
                }
                else if (backdrop.enabled)
                {
                    _shown++;
                }
            }
        }

        private static Image Make(Image icon)
        {
            Transform parent = icon.transform.parent;
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.layer = icon.gameObject.layer;
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Transform bkg = parent.Find("bkg");
            rect.SetSiblingIndex(bkg != null ? bkg.GetSiblingIndex() + 1 : 0);
            Image image = go.GetComponent<Image>();
            image.sprite = BackdropArt.Sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        /// <summary>The icon's anchors, pivot and scale, its corners moved in by the inset; written only when they differ.</summary>
        private static void Fit(RectTransform backdrop, RectTransform icon, Vector2 size)
        {
            Vector2 inset = size * Inset;
            Vector2 min = icon.offsetMin + inset;
            Vector2 max = icon.offsetMax - inset;
            if (backdrop.anchorMin != icon.anchorMin || backdrop.anchorMax != icon.anchorMax || backdrop.pivot != icon.pivot)
            {
                backdrop.anchorMin = icon.anchorMin;
                backdrop.anchorMax = icon.anchorMax;
                backdrop.pivot = icon.pivot;
            }
            if (backdrop.offsetMin != min || backdrop.offsetMax != max)
            {
                backdrop.offsetMin = min;
                backdrop.offsetMax = max;
            }
            if (backdrop.localScale != icon.localScale)
            {
                backdrop.localScale = icon.localScale;
            }
        }
    }
}
