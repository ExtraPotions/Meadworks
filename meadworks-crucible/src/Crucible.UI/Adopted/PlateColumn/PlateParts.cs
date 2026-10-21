// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/PlateParts.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// How a box is put together, found by shape rather than by name so it works on the game's plates, on boxes any
    /// copy of this library made, and on plates an older copy made: the background is the largest child that draws an
    /// Image (the game's wood, later the brown box), the icon is the other Image child that is not the text, and the
    /// text is the one TMP_Text inside.
    /// </summary>
    internal static class PlateParts
    {
        /// <summary>The direct child of <paramref name="parent"/> that is or contains <paramref name="part"/>.</summary>
        public static Transform? ChildHolding(Transform parent, Transform? part)
        {
            Transform? t = part;
            while (t != null && t.parent != parent)
            {
                t = t.parent;
            }
            return t;
        }

        /// <summary>The box's background: its largest child that draws an Image.</summary>
        public static Image? BackgroundOf(Transform box)
        {
            Image? largest = null;
            float largestArea = 0f;
            foreach (Transform child in box)
            {
                Image image = child.GetComponent<Image>();
                Rect rect = child is RectTransform r ? r.rect : default;
                if (image != null && rect.width * rect.height > largestArea)
                {
                    largest = image;
                    largestArea = rect.width * rect.height;
                }
            }
            return largest;
        }

        /// <summary>The box's icon: the first child that draws an Image and is neither the background nor the text.</summary>
        public static Image? IconOf(Transform box, Transform? textChild)
        {
            Image? background = BackgroundOf(box);
            foreach (Transform child in box)
            {
                Image image = child.GetComponent<Image>();
                if (image != null && image != background && child != textChild)
                {
                    return image;
                }
            }
            return null;
        }

        /// <summary>
        /// Whether <paramref name="go"/> carries a behaviour of <paramref name="type"/> added by any copy of this library.
        /// Every mod merges its own copy, so another copy's component is a different type with the same full name.
        /// </summary>
        public static bool Carries(GameObject go, Type type)
        {
            foreach (MonoBehaviour behaviour in go.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().FullName == type.FullName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
