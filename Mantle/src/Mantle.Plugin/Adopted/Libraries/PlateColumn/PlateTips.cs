using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// A box's tooltip is a <see cref="PlateTip"/> showing the game's inventory item tooltip box with its gold border
    /// (<see cref="TipTemplate"/>), pinned beside the box. Without a tooltip prefab to borrow no tooltip is added. Every mod merges its
    /// own copy of this library, so another copy's tip is recognised by its type's name, not its type.
    /// </summary>
    internal static class PlateTips
    {
        public static void Set(InventoryGui gui, RectTransform box, string topic, string text)
        {
            PlateTip? tip = box.GetComponent<PlateTip>();
            if (tip == null)
            {
                GameObject? prefab = TipTemplate.For(gui);
                if (prefab == null)
                {
                    return;
                }
                tip = box.gameObject.AddComponent<PlateTip>();
                tip.Prefab = prefab;
            }
            tip.Topic = topic;
            tip.Text = text;
        }

        /// <summary>Tips a box only when no copy of this library has: the game's plates, which every copy reaches.</summary>
        public static void SetIfMissing(InventoryGui gui, RectTransform box, string topic, string text)
        {
            if (!PlateParts.Carries(box.gameObject, typeof(PlateTip)))
            {
                Set(gui, box, topic, text);
            }
        }
    }
}
