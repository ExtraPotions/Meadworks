// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/RectMemo.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: namespace moved; otherwise unchanged.
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A game element's place and size as the game made it, so a layout can be put on it again and again from the same
    /// start (and taken off by setting it with no change): never added onto what an earlier pass left.
    /// </summary>
    public sealed class RectMemo
    {
        private readonly RectTransform rect;
        private readonly Vector2 position;
        private readonly Vector2 size;

        private RectMemo(RectTransform rect)
        {
            this.rect = rect;
            position = rect.anchoredPosition;
            size = rect.sizeDelta;
        }

        public static RectMemo Of(Transform transform) => transform is RectTransform rect ? new RectMemo(rect) : null;

        public Vector2 Size => size;

        public bool Alive => rect != null;

        /// <summary>The game's place moved by <paramref name="move"/> and its size grown by <paramref name="grow"/>.</summary>
        public void Set(Vector2 move, Vector2 grow)
        {
            if (rect == null)
                return;
            Vector2 at = position + move;
            Vector2 sized = size + grow;
            if (rect.anchoredPosition != at)
                rect.anchoredPosition = at;
            if (rect.sizeDelta != sized)
                rect.sizeDelta = sized;
        }
    }
}
