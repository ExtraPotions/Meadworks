using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// The children a tile list row is made of, by name. The defaults are the game's recipe list element (icon, name,
    /// QualityLevel, Durability, selected), so rows the product makes from a game list element keep the game's
    /// selection marker and gamepad stepping.
    /// </summary>
    public sealed class KitTileParts
    {
        public string Icon = "icon";
        public string Name = "name";
        public string Level = "QualityLevel";
        public string Durability = "Durability";

        public static KitTileParts Game { get; } = new KitTileParts();
    }

    /// <summary>
    /// KX05: List, CompactList, SmallGrid, MediumGrid and LargeGrid layouts over rows the product makes (copies of a game
    /// list element), the steps and sizes from Core <see cref="KitTileLayout"/>. In a grid every row becomes a square
    /// tile framed as a kit cell (<see cref="KitSkin.Cell"/>, re-skinned when the look changes), its icon filling the
    /// tile less the border, its name hidden (the tooltip says it), the level badge small in the top left, a corner mark
    /// slot in the top right and the durability bar along the bottom; an unavailable tile's icon is grayed. A compact row
    /// is the game's row made lower with everything in it scaled. Tiles are reachable with the gamepad through
    /// <see cref="Navigator"/> (C07 <see cref="GridNavigator"/>). Null on a headless process (C10).
    /// </summary>
    public sealed partial class KitTileList
    {
        private const string FrameName = "Crucible_tile_frame";
        private const string MarkName = "Crucible_tile_mark";

        private static readonly List<KitTileList> lists = new List<KitTileList>();
        private static bool hooked;

        private readonly List<RectTransform> rows = new List<RectTransform>();
        private readonly Dictionary<RectTransform, Vector2> gameSizes = new Dictionary<RectTransform, Vector2>();

        public RectTransform Content { get; }
        public KitTileParts Parts { get; }
        public KitTileView View { get; set; }
        /// <summary>The game list's own row step (List layout).</summary>
        public float ListSpace { get; set; }
        /// <summary>The game list's own content height; the content is never made lower.</summary>
        public float BaseHeight { get; set; }

        public int Columns => KitTileLayout.Columns(View);
        public float Step { get; private set; }
        public IReadOnlyList<RectTransform> Rows => rows;

        private KitTileList(RectTransform content, KitTileParts parts, float listSpace, float baseHeight)
        {
            Content = content;
            Parts = parts ?? KitTileParts.Game;
            ListSpace = listSpace;
            BaseHeight = baseHeight;
        }

        /// <summary>A tile list over <paramref name="content"/> (the list's scrolling content rect); null headless.</summary>
        public static KitTileList For(RectTransform content, float listSpace, float baseHeight, KitTileParts parts = null)
        {
            if (!CrucibleUiPlugin.Active || content == null)
                return null;
            Hook();
            KitTileList list = new KitTileList(content, parts, listSpace, baseHeight);
            lists.Add(list);
            return list;
        }

        /// <summary>Places <paramref name="items"/> (the rows the product built this time) for the current view.</summary>
        public void Arrange(IList<RectTransform> items)
        {
            rows.Clear();
            if (items != null)
                foreach (RectTransform row in items)
                    if (row != null)
                        rows.Add(row);
            if (gameSizes.Count > 0)
                foreach (RectTransform old in new List<RectTransform>(gameSizes.Keys))
                    if (old == null || !rows.Contains(old))
                        gameSizes.Remove(old);
            int columns = Columns;
            float width = Content.rect.width;
            Step = KitTileLayout.Step(View, width, ListSpace);
            for (int i = 0; i < rows.Count; i++)
                Place(rows[i], i, columns);
            Content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, KitTileLayout.ContentHeight(rows.Count, columns, Step, BaseHeight));
        }

        /// <summary>The tile's corner mark (a favorite): a game sprite, or the kit star when <paramref name="sprite"/> is null.</summary>
        public void Mark(RectTransform row, bool shown, Sprite sprite = null, Color? colour = null)
        {
            if (row == null)
                return;
            Image mark = Child(row, MarkName, true);
            mark.gameObject.SetActive(shown);
            if (!shown)
                return;
            mark.sprite = sprite != null ? sprite : KitGlyphSprites.Of(KitGlyph.Star);
            mark.color = colour ?? KitUnity.Colour(KitPalette.SheetValue);
            mark.preserveAspect = true;
            mark.transform.SetAsLastSibling();
            float size = KitTileLayout.IsGrid(View) ? Step : row.rect.height;
            KitRect place = KitTileLayout.CornerMark(size);
            RectTransform rect = mark.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-KitTileLayout.Border, -KitTileLayout.Border);
            rect.sizeDelta = new Vector2(place.Width, place.Height);
        }

        /// <summary>The level badge's text (the game element's QualityLevel); empty hides it.</summary>
        public void Badge(RectTransform row, string text)
        {
            TMP_Text level = row != null ? row.Find(Parts.Level)?.GetComponent<TMP_Text>() : null;
            if (level == null)
                return;
            level.text = text ?? "";
            level.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>An unavailable entry: its icon grayed (tiles show the icon where the game's list hides it).</summary>
        public void Greyed(RectTransform row, bool greyed)
        {
            Image icon = row != null ? row.Find(Parts.Icon)?.GetComponent<Image>() : null;
            if (icon == null)
                return;
            icon.color = greyed ? KitUnity.Colour(KitTileLayout.Greyed) : Color.white;
            if (greyed && KitTileLayout.IsGrid(View))
                icon.gameObject.SetActive(true);
        }

        public void Tooltip(RectTransform row, string topic, string text) => KitTooltip.Set(row != null ? row.gameObject : null, topic, text);

        /// <summary>Gamepad stepping over the shown tiles (whole list in List layouts), skipping those the product hides.</summary>
        public GridNavigator Navigator(Func<int, bool> shows = null)
        {
            Func<int, bool> visible = shows ?? (i => i < rows.Count && rows[i] != null && rows[i].gameObject.activeInHierarchy);
            return KitTileLayout.Navigator(rows.Count, Columns, visible);
        }

        /// <summary>The rows a viewport of <paramref name="viewport"/> scrolled to <paramref name="scrollTop"/> shows (virtualisation).</summary>
        public KitVirtualRange Visible(float scrollTop, float viewport, int count, int overscan = 1) =>
            KitVirtual.Window(scrollTop, viewport, count, Columns, Step > 0f ? Step : KitTileLayout.Step(View, Content.rect.width, ListSpace), overscan);

        /// <summary>Applies the current kit look to every tile frame again.</summary>
        public void Refresh()
        {
            foreach (RectTransform row in rows)
            {
                Transform frame = row != null ? row.Find(FrameName) : null;
                if (frame != null)
                    KitSkin.Cell(frame.GetComponent<Image>());
            }
        }

        public void Forget() => lists.Remove(this);

        private static void Hook()
        {
            if (hooked)
                return;
            hooked = true;
            KitLook.Changed += () =>
            {
                for (int i = lists.Count - 1; i >= 0; i--)
                {
                    if (lists[i].Content == null)
                    {
                        lists.RemoveAt(i);
                        continue;
                    }
                    lists[i].Refresh();
                }
            };
        }
    }
}
