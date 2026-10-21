using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// Keeps the column right every frame it is shown, whoever changed it (<see cref="Seats"/>): seats a known box of
    /// another mod the frame it appears (<see cref="Guests"/>), moves a game box an older copy of this library took into
    /// the container back out, gives each seat its box's size and shown state, lays the column out again the frame any box
    /// is shown, hidden or resized (the layout group hears of a child going inactive only through a Graphic on that child,
    /// and a box's root draws nothing), pins each box over its seat after every mod's Update has placed it, and lifts a
    /// seated box's own canvases above the inventory's when its parts change (<see cref="Guests.Lift"/>). On the
    /// column's container, in place of the older <see cref="ColumnWatch"/>. The seats are listed again only when the
    /// container's children change, so a steady frame is a few finds by name and allocates nothing.
    /// </summary>
    internal sealed class SeatWatch : MonoBehaviour
    {
        private readonly List<KeyValuePair<RectTransform, string>> seats = new List<KeyValuePair<RectTransform, string>>();
        private readonly int[] guestParts = new int[Guests.Known.Length];
        private GamePlates? game;
        private Canvas? canvas;
        private int listed = -1;
        private int seen = int.MinValue;

        private void LateUpdate()
        {
            RectTransform boxes = (RectTransform)transform;
            if (!(boxes.parent is RectTransform panel))
            {
                return;
            }
            if (Rescue(panel, boxes) | Seats.SeatGuests(panel, boxes))
            {
                Seats.Arrange(panel, boxes);
                listed = -1;
            }
            List();
            bool resized = Seats.Follow(panel, seats);
            int shape = ColumnWatch.Shape(boxes);
            if (resized || shape != seen)
            {
                seen = shape;
                LayoutRebuilder.ForceRebuildLayoutImmediate(boxes);
            }
            Seats.Pin(panel, boxes, seats);
            LiftGuests(panel);
        }

        /// <summary>
        /// Lifts a known box's own canvases (<see cref="Guests.Lift"/>) whenever its children change: a mod may add them
        /// after making the box, as TrashItems does the next frame.
        /// </summary>
        private void LiftGuests(RectTransform panel)
        {
            if (canvas == null)
            {
                canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas;
            }
            for (int i = 0; i < Guests.Known.Length; i++)
            {
                RectTransform? box = panel.Find(Guests.Known[i].Name) as RectTransform;
                int parts = box != null ? box.childCount : -1;
                if (parts != guestParts[i] && canvas != null)
                {
                    guestParts[i] = parts;
                    if (box != null && Guests.LooksLikeBox(box))
                    {
                        Guests.Lift(box, canvas);
                    }
                }
            }
        }

        private void List()
        {
            if (listed != transform.childCount)
            {
                Seats.Collect((RectTransform)transform, seats);
                listed = transform.childCount;
            }
        }

        /// <summary>The game's boxes back on the panel, seated, when an older copy of this library moved them into the container.</summary>
        private bool Rescue(RectTransform panel, RectTransform boxes)
        {
            if (game == null || game.Armor == null || game.Weight == null)
            {
                game = GamePlates.Find(InventoryGui.instance);
            }
            if (game == null || (game.Armor.parent == panel && game.Weight.parent == panel))
            {
                return false;
            }
            return Seats.Seat(panel, boxes, game.Armor, Column.ArmorRank) | Seats.Seat(panel, boxes, game.Weight, Column.WeightRank);
        }
    }
}
