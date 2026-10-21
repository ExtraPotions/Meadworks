using Mantle.Look;
using Mantle.Slots;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// Flat ochre item silhouettes for empty slots (<c>assets/icon_&lt;slot&gt;.png</c>, 64 px, the approved set in
    /// artwork/slot-icons-approved, drawn by artwork/slot-icons-silhouette-concept/draw.py), colour baked in.
    /// Slight transparency distinguishes the hint from an item occupying the slot.
    /// </summary>
    public static class SlotIcons
    {
        public static readonly Color Hint = new Color(1f, 1f, 1f, 0.85f);

        public static Color TintFor(SlotKind kind) => Hint;

        public static Sprite For(SlotKind kind)
        {
            return kind == SlotKind.Retired ? null : SkinArt.Icon(IconName(kind));
        }

        /// <summary>
        /// The ring and belt slots have no icon of their own yet: they borrow the trinket's pendant and the utility's belt
        /// until the user approves ring and belt art. The Feet slot borrows the legs icon (no boot art of Mantle's own).
        /// </summary>
        public static string IconName(SlotKind kind)
        {
            switch (kind)
            {
                case SlotKind.Ring: return "trinket";
                case SlotKind.Belt: return "utility";
                case SlotKind.Feet: return "legs";
                default: return kind.ToString().ToLowerInvariant();
            }
        }

    }
}
