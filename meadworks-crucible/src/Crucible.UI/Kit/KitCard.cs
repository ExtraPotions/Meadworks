// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A kit panel for Unity layout groups: the card is an ordinary laid-out rect; its first child <c>Face</c> is a
    /// <see cref="KitPanel"/> stretched over it and inset by <see cref="KitSkin.PanelOverhang"/>, so the background art ends
    /// exactly on the card's edge. The face ignores layout and is re-inset when the look changes (10 or 12 units).
    /// Content goes under the card itself (a layout group on the card should pad by at least
    /// <see cref="KitCardLayout.Padding"/>).
    /// </summary>
    public static class KitCard
    {
        static readonly List<RectTransform> faces = new List<RectTransform>();
        static bool hooked;

        /// <summary>A card under <paramref name="parent"/>; null headless or before the graphical kit is active.</summary>
        public static RectTransform Create(Transform parent, string name)
        {
            if (!CrucibleUiPlugin.Active || !MeadworksHeadless.AllowsUi || parent == null) return null;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var card = (RectTransform)go.transform;
            card.SetParent(parent, false);
            var face = KitPanel.Create(card, "Face", Vector2.zero);
            if (face == null) { Object.Destroy(go); return null; }
            face.SetAsFirstSibling();
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.pivot = new Vector2(0.5f, 0.5f);
            face.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            KitPanel.Background(face).raycastTarget = false;
            Inset(face);
            if (!hooked) { hooked = true; KitLook.Changed += InsetAll; }
            faces.Add(face);
            return card;
        }

        /// <summary>The card's face (its kit panel), or null.</summary>
        public static RectTransform Face(RectTransform card) => card != null ? card.Find("Face") as RectTransform : null;

        /// <summary>Re-reads the overhang and re-skins the face (after the product changed the look or rebuilt art).</summary>
        public static void Refresh(RectTransform card)
        {
            var face = Face(card);
            if (face == null) return;
            KitPanel.Refresh(face);
            Inset(face);
        }

        static void Inset(RectTransform face)
        {
            var inset = KitCardLayout.FaceInset(KitSkin.PanelOverhang);
            face.offsetMin = KitUnity.Vector(inset.Min);
            face.offsetMax = KitUnity.Vector(inset.Max);
        }

        static void InsetAll()
        {
            for (int i = faces.Count - 1; i >= 0; i--)
            {
                if (faces[i] == null) { faces.RemoveAt(i); continue; }
                Inset(faces[i]);
            }
        }
    }

    /// <summary>A divider for Unity layout groups in the PackPanel bronze, two units thick, stretching along its group.</summary>
    public static class KitDivider
    {
        /// <summary>A horizontal divider (or vertical in a horizontal group); null headless or before the kit is active.</summary>
        public static Image Create(Transform parent, string name, bool vertical = false)
        {
            if (!CrucibleUiPlugin.Active || !MeadworksHeadless.AllowsUi || parent == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = KitUnity.Colour(KitCardLayout.DividerColour);
            image.raycastTarget = false;
            var element = go.GetComponent<LayoutElement>();
            float thickness = KitCardLayout.DividerThickness;
            if (vertical) { element.minWidth = element.preferredWidth = thickness; element.flexibleWidth = 0; element.flexibleHeight = 1; }
            else { element.minHeight = element.preferredHeight = thickness; element.flexibleHeight = 0; element.flexibleWidth = 1; }
            ((RectTransform)go.transform).sizeDelta = vertical ? new Vector2(thickness, 0) : new Vector2(0, thickness);
            return image;
        }
    }
}
