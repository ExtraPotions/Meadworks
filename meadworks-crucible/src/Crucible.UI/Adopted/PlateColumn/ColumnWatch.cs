// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/ColumnWatch.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// Re-lays the HUD row out the frame any box is shown or hidden, whoever does it. The layout group hears of a child
    /// going inactive only through a Graphic on that child itself, and a box's root draws nothing (its background is a
    /// child), so without this a hidden box would leave a gap. On the HUD row (<see cref="HudContainer"/>); the inventory's
    /// column has a <see cref="SeatWatch"/> instead, which does this too, though a container an older copy of this library
    /// made may still carry one of these. Runs only while its row is shown; cheap: one pass over a handful of children, no
    /// allocation.
    /// </summary>
    internal sealed class ColumnWatch : MonoBehaviour
    {
        private int seen = int.MinValue;

        private void LateUpdate()
        {
            int shape = Shape(transform);
            if (shape != seen)
            {
                seen = shape;
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            }
        }

        /// <summary>Which children are active, folded into one number.</summary>
        public static int Shape(Transform boxes)
        {
            int shape = boxes.childCount;
            for (int i = 0; i < boxes.childCount; i++)
            {
                shape = unchecked(shape * 31 + (boxes.GetChild(i).gameObject.activeSelf ? 1 : 0));
            }
            return shape;
        }
    }
}
