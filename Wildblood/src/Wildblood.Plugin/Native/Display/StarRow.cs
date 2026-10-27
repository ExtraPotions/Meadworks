// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// Draws a creature's stars on its nameplate as a row of glyphs, counted in fives: a small glyph is worth one star,
    /// a large glyph (2.2x vanilla) is worth five, so the count reads as `stars/5` large then `stars%5` small and never
    /// as a numeral. The row sits directly below the health bar, left-justified to the bar's left edge, so it never
    /// covers the name. Every glyph is anchored top-left and shares a common TOP edge, so a large star simply hangs
    /// further DOWN than the small ones beside it - it does not tower above them and its middle does not line up. When a
    /// creature carries mutations the glyphs are divided between them evenly (remainder to the leftmost) and every glyph
    /// is coloured solid; with one mutation the whole row takes that colour. A creature with no mutation keeps vanilla's
    /// own star colour. The star count comes from the resolved traits, not the vanilla level, which the mod keeps at 1.
    /// </summary>
    public sealed class StarRow : MonoBehaviour
    {
        private const int MaxStars = 50;

        /// <summary>The size vanilla draws a star at; both glyph sizes are configured multiples of this.</summary>
        private const float VanillaSize = 10f;
        private const float Gap = 3f;

        /// <summary>The shared top edge, set just below the health bar's bottom so glyphs hang down beneath it.</summary>
        private const float TopEdge = -3f;

        /// <summary>Vanilla's own star colour: a white tint leaves the badge sprite showing its native colour.</summary>
        private static readonly Color PlainColor = Color.white;

        private readonly List<GameObject> _glyphs = new List<GameObject>();
        private bool _shown = true;
        private int _stars = -1, _mask, _palette;
        private float _small, _large;
        private Character _character = null!;
        private Sprite _starSprite = null!;
        private RectTransform _healthBar = null!;

        public void Init(Character character, Sprite sprite, RectTransform healthBar)
        {
            _character = character;
            _starSprite = sprite;
            _healthBar = healthBar;
        }

        internal void SetShown(bool shown)
        {
            _shown = shown;
            foreach (GameObject glyph in _glyphs) if (glyph != null && glyph.activeSelf != shown) glyph.SetActive(shown);
        }

        internal float End()
        {
            float end = 0f;
            foreach (GameObject glyph in _glyphs)
                if (glyph != null && glyph.activeSelf && glyph.transform is RectTransform rect)
                    end = Mathf.Max(end, rect.anchoredPosition.x + rect.sizeDelta.x + IconRow.Gap);
            return end;
        }

        private void Start() => Guard.Run("StarRow.Start", Build);
        private void Update() => Guard.Run("StarRow.Update", static self => self.Build(), this);
        private void Clear()
        {
            foreach (GameObject glyph in _glyphs) if (glyph != null) { glyph.SetActive(false); Destroy(glyph); }
            _glyphs.Clear(); _stars = -1;
        }
        internal void ClearOwned() => Clear();

        private void OnDestroy() => Clear();

        private void Build()
        {
            EliteController? controller = _character != null ? _character.GetComponent<EliteController>() : null;
            if (!Configuration.ColouredStars.Value || controller == null || !controller.Ready || _starSprite == null || _healthBar == null)
            {
                if (_glyphs.Count > 0) Clear();
                return;
            }
            int stars = Mathf.Min(controller.Traits.Stars, MaxStars);
            if (stars <= 0) { if (_glyphs.Count > 0) Clear(); return; }
            int mask = controller.Traits.Mask, palette = PaletteSettings.Version;
            float small = Configuration.SmallStarSize.Value, large = Configuration.LargeStarSize.Value;
            if (_stars == stars && _mask == mask && _palette == palette && _small == small && _large == large) return;
            Clear();
            Layout(new List<Mutation>(controller.Traits.Active()), stars);
            _stars = stars; _mask = mask; _palette = palette; _small = small; _large = large;
        }

        private void Layout(List<Mutation> active, int stars)
        {
            int large = stars / 5;
            int total = large + stars % 5;
            float small = VanillaSize * Configuration.SmallStarSize.Value;
            float big = VanillaSize * Configuration.LargeStarSize.Value;
            float cursor = 0f;
            for (int i = 0; i < total; i++)
            {
                float size = i < large ? big : small;
                cursor += CreateGlyph(i, cursor, size, ColorForGlyph(i, active, total)) + Gap;
            }
        }

        /// <summary>
        /// The colour of glyph <paramref name="index"/>: vanilla's own colour when the creature has no mutation, else
        /// the total glyphs split between the mutations in table order as evenly as they go, any remainder to the
        /// leftmost, so a single mutation paints the whole row and every glyph is coloured.
        /// </summary>
        private static Color ColorForGlyph(int index, List<Mutation> active, int total)
        {
            if (active.Count == 0)
            {
                return PlainColor;
            }
            int span = total / active.Count;
            int remainder = total % active.Count;
            int edge = 0;
            for (int m = 0; m < active.Count; m++)
            {
                edge += span + (m < remainder ? 1 : 0);
                if (index < edge)
                {
                    return PaletteSettings.Of(active[m]);
                }
            }
            return PlainColor;
        }

        private float CreateGlyph(int index, float x, float size, Color color)
        {
            GameObject star = new GameObject("ecr_star_" + index, typeof(RectTransform));
            RectTransform rect = star.GetComponent<RectTransform>();
            rect.SetParent(_healthBar, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 1f); // top-left pivot: the glyph hangs down from its shared top edge
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(x, TopEdge);
            Image image = star.AddComponent<Image>();
            image.sprite = _starSprite;
            image.color = color;
            image.raycastTarget = false;
            star.SetActive(_shown);
            _glyphs.Add(star);
            return size;
        }
    }
}
