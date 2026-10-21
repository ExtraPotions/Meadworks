// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The row is a forged-iron plate in the game's own iron button look, the material of Mantle's HUD column squares
// (Crucible.UI KitHudArt.DressAsGameIron), with MeadWorks' own rivets in its corners (user decision 2026-10-13 09:30, panel option 1).
// The row's height, gap under the map and bonus color come from SleepRowLayout; the parts' places from SleepRowCells.
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Cairn.Runtime
{
    internal static class CairnMinimapEffects
    {
        static readonly Color Cream = new Color(1f, .9f, .68f, 1f);
        static KitHudPanel? panel;
        static Transform? owner;
        static TMP_Text? boost, count;
        static Image? plate, moon, bed;
        static readonly Image?[] rivets = new Image?[4];
        static bool dressed;
        // The row is laid out and its texts written again only when its place or its reading changes.
        static (float Width, float X, float Y, string? Sleeping, string? Players, string? Skip, string? Boost)? drawn;

        /// <summary>Draws the row under the minimap; false when it cannot (no minimap on screen, the panel not made).</summary>
        internal static bool Draw(Transform root)
        {
            var map = Minimap.instance?.m_smallRoot?.transform as RectTransform;
            if (!CairnSleep.Reading || map == null || !map.gameObject.activeInHierarchy || !(root is RectTransform canvas))
            { Hide(); return false; }
            if (panel != null && (panel.Root == null || owner != root)) Dispose();
            // The plate spans the map picture itself (the small root is a little wider than the picture), measured in canvas units.
            var picture = Minimap.instance!.m_mapImageSmall != null ? Minimap.instance.m_mapImageSmall.rectTransform : map;
            var bottomLeft = canvas.InverseTransformPoint(picture.TransformPoint(new Vector3(picture.rect.xMin, picture.rect.yMin)));
            var bottomRight = canvas.InverseTransformPoint(picture.TransformPoint(new Vector3(picture.rect.xMax, picture.rect.yMin)));
            var width = bottomRight.x - bottomLeft.x;
            // With the MeadWorks map frame shown, its wood reaches below the picture; the row hangs under the frame instead.
            var frame = KitMapFrames.ShownSmallFrame();
            if (frame != null)
            {
                var frameBottom = canvas.InverseTransformPoint(frame.TransformPoint(new Vector3(frame.rect.xMin, frame.rect.yMin))).y;
                if (frameBottom < bottomLeft.y) bottomLeft.y = frameBottom;
            }
            const float height = SleepRowLayout.Height;
            if (panel == null)
            {
                panel = KitHudPanel.Create(root, "Cairn_MinimapEffects", new Vector2(width, height), Vector2.zero, global::ExtraPotions.Cairn.Runtime.Plugin.Guid, "Cairn_MinimapEffects");
                if (panel == null) return false;
                owner = root;
                moon = Glyph("Moon", KitGlyphSprites.Of(KitGlyph.Moon));
                bed = Glyph("Bed", KitGlyphSprites.Of(KitGlyph.Bed));
                count = Text("SleepCount", Cream, TextAlignmentOptions.Center);
                boost = Text("SleepBoost", new Color32(SleepRowLayout.BoostColour[0], SleepRowLayout.BoostColour[1], SleepRowLayout.BoostColour[2], 255), TextAlignmentOptions.MidlineLeft);
                plate = Glyph("SleepPlate", null);
                plate.color = Color.white;
                plate.preserveAspect = false;
                plate.transform.SetAsFirstSibling();
                plate.gameObject.SetActive(false);
                for (var i = 0; i < rivets.Length; i++) rivets[i] = Glyph("SleepRivet" + i, KitHudArt.IronRivet);
                foreach (var rivet in rivets) { rivet!.color = Color.white; rivet.gameObject.SetActive(false); }
            }
            // The game's iron look is readable once its inventory screen is built; until then the row shows without a plate.
            // No tooltip (user decision 2026-10-12 22:31): the count and percentage carry the meaning.
            if (!dressed && KitHudArt.DressAsGameIron(plate!))
            {
                dressed = true; drawn = null;
                plate!.gameObject.SetActive(true);
                foreach (var rivet in rivets) rivet!.gameObject.SetActive(true);
            }
            var position = new Vector2(bottomLeft.x - canvas.rect.xMin, canvas.rect.yMax - bottomLeft.y + SleepRowLayout.Gap);
            var line = CairnSleep.Line;
            var key = (width, position.x, position.y, line.Sleeping, line.Players, line.SkipPercent, line.BoostPercent);
            if (drawn == key) { panel.Update(true, false, 1f, 0f); return true; }
            drawn = key;
            Place(plate!.rectTransform, 0, 0, width, height);
            const float r = SleepRowCells.Rivet, inset = SleepRowCells.RivetInset;
            Place(rivets[0]!.rectTransform, inset, inset, r, r);
            Place(rivets[1]!.rectTransform, width - inset - r, inset, r, r);
            Place(rivets[2]!.rectTransform, inset, height - inset - r, r, r);
            Place(rivets[3]!.rectTransform, width - inset - r, height - inset - r, r, r);

            var cells = new SleepRowCells(width, height);
            Place(moon!.rectTransform, cells.MoonX, (height - SleepRowCells.Moon) / 2, SleepRowCells.Moon, SleepRowCells.Moon);
            Place(bed!.rectTransform, cells.BedX, (height - SleepRowCells.Bed) / 2, SleepRowCells.Bed, SleepRowCells.Bed);
            Place(count!.rectTransform, cells.CountX, 0, cells.CountWidth, height);
            Place(boost!.rectTransform, cells.BoostX, 0, SleepRowCells.BoostWidth, height);
            panel.Root.sizeDelta = new Vector2(width, height);
            panel.SetPosition(position);
            count.text = (line.Sleeping.Length > 0 ? line.Sleeping : "0") + "/" + (line.Players.Length > 0 ? line.Players : "0");
            var nobody = line.Sleeping.Length == 0 || line.Sleeping == "0";
            count.color = new Color(Cream.r, Cream.g, Cream.b, nobody ? .6f : 1f);
            bed.color = new Color(1f, .84f, .45f, nobody ? .6f : 1f);
            boost.text = line.SkipPercent.Length > 0 ? line.SkipPercent + "%" : "+" + (line.BoostPercent.Length > 0 ? line.BoostPercent : "0") + "%";
            panel.Update(true, false, 1f, 0f);
            return true;
        }

        static Image Glyph(string name, Sprite? sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(panel!.Content, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = new Color(1f, .84f, .45f);
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Text(string name, Color colour, TextAlignmentOptions alignment)
        {
            var text = KitCanvas.Text(panel!.Content, name, "", 14, colour, alignment, false);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // A count or percentage never needs cutting; Truncate would drop the whole line in a tight rect.
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static void Hide() => panel?.Update(false, false, 1f, 0f);
        internal static void Dispose()
        {
            drawn = null; dressed = false; panel?.Dispose(); panel = null; owner = null; boost = count = null; plate = moon = bed = null;
            for (var i = 0; i < rivets.Length; i++) rivets[i] = null;
        }
    }
}
