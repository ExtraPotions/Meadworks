using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>KX05 row shaping: grid tiles, compact rows and the kit cell frame (sizes from <see cref="KitTileLayout"/>).</summary>
    public sealed partial class KitTileList
    {
        private void Place(RectTransform row, int index, int columns)
        {
            if (!gameSizes.ContainsKey(row))
                gameSizes[row] = row.sizeDelta;
            Vector2 at = KitUnity.Vector(KitTileLayout.Position(index, columns, Step));
            row.anchoredPosition = at;
            if (columns > 1)
            {
                Tile(row);
                return;
            }
            Frame(row, false);
            if (View == KitTileView.CompactList)
                Compact(row);
        }

        private void Tile(RectTransform row)
        {
            float size = Step;
            row.sizeDelta = new Vector2(size, size);
            Frame(row, true);
            Transform icon = row.Find(Parts.Icon);
            if (icon is RectTransform iconRect)
            {
                KitRect place = KitTileLayout.Icon(size);
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.offsetMin = new Vector2(place.X, place.Y);
                iconRect.offsetMax = new Vector2(-(size - place.XMax), -(size - place.YMax));
            }
            Transform name = row.Find(Parts.Name);
            if (name != null)
                name.gameObject.SetActive(false);
            if (row.Find(Parts.Level) is RectTransform level)
            {
                level.anchorMin = level.anchorMax = level.pivot = new Vector2(0f, 1f);
                level.anchoredPosition = new Vector2(KitTileLayout.Border, -1f);
                level.sizeDelta = KitUnity.Vector(KitTileLayout.LevelBox(size));
                TMP_Text text = level.GetComponent<TMP_Text>();
                if (text != null)
                {
                    text.enableAutoSizing = false;
                    text.fontSize = KitTileLayout.LevelFont(size);
                    text.alignment = TextAlignmentOptions.TopLeft;
                }
            }
            if (row.Find(Parts.Durability) is RectTransform bar)
            {
                bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
                bar.anchoredPosition = new Vector2(0f, KitTileLayout.Border + 1f);
                bar.localScale = new Vector3(KitTileLayout.DurabilityScale(size, bar.sizeDelta.x), 1f, 1f);
            }
        }

        /// <summary>The game's row made lower; icon and marks scaled about the icon's center, the name given the freed width.</summary>
        private void Compact(RectTransform row)
        {
            Vector2 game = gameSizes[row];
            float height = KitTileLayout.CompactHeight(Step);
            float scale = KitTileLayout.CompactScale(Step, game.y);
            if (Mathf.Approximately(row.sizeDelta.y, height))
                return;
            row.sizeDelta = new Vector2(row.sizeDelta.x, height);
            if (!(row.Find(Parts.Icon) is RectTransform icon))
                return;
            float half = row.sizeDelta.x / 2f;
            float oldCentre = icon.anchoredPosition.x + icon.sizeDelta.x / 2f - half;
            icon.sizeDelta *= scale;
            float newCentre = icon.anchoredPosition.x + icon.sizeDelta.x / 2f - half;
            if (row.Find(Parts.Name) is RectTransform name)
            {
                float x = name.anchoredPosition.x;
                name.anchoredPosition = new Vector2(x * scale, name.anchoredPosition.y);
                name.sizeDelta = new Vector2(name.sizeDelta.x + x - x * scale, name.sizeDelta.y * scale);
            }
            foreach (string part in new[] { Parts.Level, Parts.Durability })
            {
                if (!(row.Find(part) is RectTransform mark))
                    continue;
                Vector2 at = mark.anchoredPosition;
                mark.anchoredPosition = new Vector2(newCentre + (at.x - oldCentre) * scale, at.y * scale);
                mark.localScale *= scale;
            }
        }

        /// <summary>The kit cell frame behind a tile (first child, deaf to the pointer); hidden in list layouts.</summary>
        private static void Frame(RectTransform row, bool shown)
        {
            Transform existing = row.Find(FrameName);
            if (existing == null && !shown)
                return;
            Image frame = Child(row, FrameName, false);
            frame.gameObject.SetActive(shown);
            if (!shown)
                return;
            frame.transform.SetAsFirstSibling();
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            KitSkin.Cell(frame);
        }

        private static Image Child(RectTransform row, string name, bool last)
        {
            Transform found = row.Find(name);
            if (found != null && found.GetComponent<Image>() is Image kept)
                return kept;
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = row.gameObject.layer;
            go.transform.SetParent(row, false);
            if (last)
                go.transform.SetAsLastSibling();
            else
                go.transform.SetAsFirstSibling();
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
