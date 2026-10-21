using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
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
