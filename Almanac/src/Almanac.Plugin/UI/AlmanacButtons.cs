// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Almanac's buttons carry translated captions in seven languages; the game's button art leaves the caption roughly half the button's width,
// which cut "Reset", "Undo", "Convert" and "Keybinds" to an ellipsis in German, Russian, French, Spanish and Korean.
using System;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    internal static class AlmanacButtons
    {
        public static Button Create(RectTransform parent,string name,string caption,Action click,Vector2 at,Vector2 size)
        {
            var button=KitButton.Create(parent,name,caption,click,at,size);
            Roomy(button);return button;
        }
        /// <summary>The caption's natural width at its design size (TMP measures auto-sized text at its largest size); 0 without a caption.</summary>
        public static float CaptionWidth(Button? button)
        {
            var text=button!=null?button.GetComponentInChildren<TMP_Text>(true):null;
            return text==null?0f:text.GetPreferredValues(text.text).x;
        }
        /// <summary>The caption fills the whole button instead of the art's inset (idempotent; null-safe).</summary>
        public static void Roomy(Button? button)
        {
            var text=button?.GetComponentInChildren<TMP_Text>(true);if(text==null)return;
            var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
        }
    }
}
