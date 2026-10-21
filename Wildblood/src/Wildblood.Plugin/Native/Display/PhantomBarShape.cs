// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// The shape of one bar in a Phantom row (<see cref="PhantomBars"/>): shrunk to its slot, put back as the game drew it
    /// once the boss's copies are gone, and the boss's coloured stars hidden while it sits among copies that show none.
    /// Per-field baselines release only our exact writes, preserving later native or foreign HUD changes.
    /// </summary>
    internal static class PhantomBarShape
    {
        private const float BarHeight = 5f;
        private const float NameScale = 0.42f;

        private sealed class Parts
        {
            public RectTransform? Health, Name;
            public bool RootOwned, HealthOwned;
            public Vector2 RootBase, RootLast, NameSizeBase, NameSizeLast, NamePositionBase, NamePositionLast;
            public Vector3 HealthBase, HealthLast, NameScaleBase, NameScaleLast;
            public bool NameScaleOwned, NameSizeOwned, NamePositionOwned;
        }

        private static readonly ConditionalWeakTable<Transform, Parts> Kept = new ConditionalWeakTable<Transform, Parts>();

        /// <summary>The boss bar's health bar on the game's template, or null.</summary>
        public static RectTransform? HealthOf(RectTransform root) => PartsOf(root).Health;

        // The bar is scaled rather than resized, so the game's own bar filling works in its usual units. The name is
        // scaled evenly, given the slot's width to fit in, and set down just above its bar.
        public static void Shrink(RectTransform root, float width)
        {
            Parts parts = PartsOf(root);
            RectTransform? health = parts.Health;
            RectTransform? name = parts.Name;
            if (health == null || name == null || health.sizeDelta.x < 1f || health.sizeDelta.y < 1f)
            {
                return;
            }
            Vector3 scale = new Vector3(width / health.sizeDelta.x, BarHeight / health.sizeDelta.y, 1f);
            Vector3 nameScale = new Vector3(NameScale, NameScale, 1f);
            Vector2 nameSize = new Vector2(width / NameScale, name.sizeDelta.y);
            if (!health.localScale.Equals(scale)) health.localScale = scale;
            if (!name.localScale.Equals(nameScale)) name.localScale = nameScale;
            if (!name.sizeDelta.Equals(nameSize)) name.sizeDelta = nameSize;
            float barCentre = (health.anchorMin.y - name.anchorMin.y) * root.rect.height + health.anchoredPosition.y;
            float lift = BarHeight / 2f + name.sizeDelta.y * NameScale / 2f;
            Vector2 position = new Vector2(0f, barCentre + lift);
            if (!name.anchoredPosition.Equals(position)) name.anchoredPosition = position;
        }

        // Native HUD producers run before layout. Refresh only fields another writer changed since our last layout.
        public static void Begin(RectTransform root)
        {
            Parts p = PartsOf(root);
            if (!p.RootOwned || !root.anchoredPosition.Equals(p.RootLast)) p.RootBase = root.anchoredPosition;
            if (p.Health != null && (!p.HealthOwned || !p.Health.localScale.Equals(p.HealthLast))) p.HealthBase = p.Health.localScale;
            if (p.Name != null)
            {
                if (!p.NameScaleOwned || !p.Name.localScale.Equals(p.NameScaleLast)) p.NameScaleBase = p.Name.localScale;
                if (!p.NameSizeOwned || !p.Name.sizeDelta.Equals(p.NameSizeLast)) p.NameSizeBase = p.Name.sizeDelta;
                if (!p.NamePositionOwned || !p.Name.anchoredPosition.Equals(p.NamePositionLast)) p.NamePositionBase = p.Name.anchoredPosition;
            }
        }

        public static void Capture(RectTransform root, bool shape)
        {
            Parts p = PartsOf(root);
            p.RootOwned = true; p.RootLast = root.anchoredPosition;
            if (!shape) return;
            if (p.Health != null) { p.HealthOwned = true; p.HealthLast = p.Health.localScale; }
            if (p.Name != null)
            {
                p.NameScaleOwned = true; p.NameScaleLast = p.Name.localScale;
                p.NameSizeOwned = true; p.NameSizeLast = p.Name.sizeDelta;
                p.NamePositionOwned = true; p.NamePositionLast = p.Name.anchoredPosition;
            }
        }

        public static void Restore(GameObject gui, bool position = true)
        {
            if (gui == null || !(gui.transform is RectTransform root) || !Kept.TryGetValue(root, out Parts p)) return;
            p = PartsOf(root);
            if (position && p.RootOwned && root.anchoredPosition.Equals(p.RootLast)) root.anchoredPosition = p.RootBase;
            if (p.Health != null && p.HealthOwned && p.Health.localScale.Equals(p.HealthLast)) p.Health.localScale = p.HealthBase;
            if (p.Name != null)
            {
                if (p.NameScaleOwned && p.Name.localScale.Equals(p.NameScaleLast)) p.Name.localScale = p.NameScaleBase;
                if (p.NameSizeOwned && p.Name.sizeDelta.Equals(p.NameSizeLast)) p.Name.sizeDelta = p.NameSizeBase;
                if (p.NamePositionOwned && p.Name.anchoredPosition.Equals(p.NamePositionLast)) p.Name.anchoredPosition = p.NamePositionBase;
            }
            if (position) p.RootOwned = false;
            p.HealthOwned = p.NameScaleOwned = p.NameSizeOwned = p.NamePositionOwned = false;
        }

        public static void ShowStars(GameObject gui, bool shown)
        {
            if (gui != null) gui.GetComponent<StarRow>()?.SetShown(shown);
        }

        private static Parts PartsOf(Transform root)
        {
            if (!Kept.TryGetValue(root, out Parts parts))
            {
                parts = new Parts { Health = root.Find("Health") as RectTransform, Name = root.Find("Name") as RectTransform };
                Kept.Add(root, parts);
            }
            RectTransform? health = root.Find("Health") as RectTransform;
            RectTransform? name = root.Find("Name") as RectTransform;
            if (!ReferenceEquals(parts.Health, health))
            {
                parts.Health = health; parts.HealthOwned = false;
            }
            if (!ReferenceEquals(parts.Name, name))
            {
                parts.Name = name;
                parts.NameScaleOwned = parts.NameSizeOwned = parts.NamePositionOwned = false;
            }
            return parts;
        }
    }
}
