// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The pills under the Inscribe text (user request 2026-10-07: "the mod name in a pill like thing colored with respect
    /// to the essence theme/color. you can hover the pill to see info about it"): one rounded tag per inscription the
    /// chosen essence can give, in the essence's colour, laid left to right in up to <see cref="Rows"/> rows at the bottom
    /// of the text area; what does not fit is one "+N more" pill whose hover lists the rest. Hidden on other tabs and with
    /// no essence chosen (<see cref="Hide"/>).
    /// </summary>
    internal sealed class PillStrip
    {
        public const int Rows = 2;
        // Small tags (user 2026-10-07: "The pill abilities need to be way smaller").
        public const float Height = 15f;
        private const float Gap = 3f;
        private const float Pad = 5f;
        private const float FontSize = 10.5f;

        private readonly RectTransform _root;
        private readonly TMP_Text _font;
        private readonly List<PillView> _views = new List<PillView>();
        private float _width;

        public PillStrip(RectTransform parent, TMP_Text font)
        {
            var go = new GameObject("ECF_Pills", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            _root = (RectTransform)go.transform;
            _root.SetParent(parent, false);
            _font = font;
            go.SetActive(false);
        }

        /// <summary>The strip's full height: <see cref="Rows"/> rows and the gaps between them.</summary>
        public static float AreaHeight => Rows * Height + (Rows - 1) * Gap;

        public void Place(float x, float y, float width)
        {
            Rects.Place(_root, x, y, width, AreaHeight);
            _width = width;
        }

        public void Hide()
        {
            if (_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }
        }

        public void Show(IReadOnlyList<Pill> pills, Color tone)
        {
            _root.gameObject.SetActive(pills.Count > 0);
            int used = Lay(pills, tone);
            for (int i = used; i < _views.Count; i++)
            {
                _views[i].Root.SetActive(false);
            }
        }

        // Greedy rows; when a pill does not fit on the last row, a "+N more" pill takes the rest (one pill earlier when
        // it does not fit itself). Returns how many views are in use.
        private int Lay(IReadOnlyList<Pill> pills, Color tone)
        {
            var at = new Vector2(0f, 0f);
            for (int i = 0; i < pills.Count; i++)
            {
                float width = Width(pills[i].Name);
                if (!Fits(ref at, width))
                {
                    return More(pills, i, tone, at);
                }
                View(i).Show(pills[i].Name, pills[i].Text, tone, at, width);
                at.x += width + Gap;
            }
            return pills.Count;
        }

        private int More(IReadOnlyList<Pill> pills, int first, Color tone, Vector2 at)
        {
            string label = Words.Localize("$ecf_table_pill_more", (pills.Count - first).ToString());
            float width = Width(label);
            if (at.x + width > _width && first > 0)
            {
                first--;
                at = View(first).At;
                label = Words.Localize("$ecf_table_pill_more", (pills.Count - first).ToString());
                width = Width(label);
            }
            View(first).Show(label, Rest(pills, first), tone, at, width);
            return first + 1;
        }

        // Moves to the next row when the pill does not fit on this one; false past the last row.
        private bool Fits(ref Vector2 at, float width)
        {
            if (at.x + width <= _width || at.x == 0f)
            {
                return true;
            }
            if (at.y + 2f * Height + Gap > AreaHeight + 0.5f)
            {
                return false;
            }
            at = new Vector2(0f, at.y + Height + Gap);
            return true;
        }

        private static string Rest(IReadOnlyList<Pill> pills, int first)
        {
            var sb = new StringBuilder();
            for (int i = first; i < pills.Count; i++)
            {
                sb.Append(i > first ? "\n" : "").Append(pills[i].Name);
            }
            return sb.ToString();
        }

        private float Width(string label) => Mathf.Min(_width, _font.GetPreferredValues(label, 1000f, Height).x
            * FontSize / Mathf.Max(1f, _font.fontSize) + 2f * Pad);

        private PillView View(int index)
        {
            while (_views.Count <= index)
            {
                _views.Add(new PillView(_root, _font, FontSize));
            }
            return _views[index];
        }
    }
}
