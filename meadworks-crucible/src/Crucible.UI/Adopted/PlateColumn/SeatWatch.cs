// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/SeatWatch.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// Keeps the column right every frame it is shown, whoever changed it (<see cref="Seats"/>): seats a known box of
    /// another mod the frame it appears (<see cref="Guests"/>), moves a game box an older copy of this library took into
    /// the container back out, gives each seat its box's size and shown state, lays the column out again the frame any box
    /// is shown, hidden or resized (the layout group hears of a child going inactive only through a Graphic on that child,
    /// and a box's root draws nothing), pins each box over its seat after every mod's Update has placed it, and lifts a
    /// seated box's own canvases above the inventory's when its parts change (<see cref="Guests.Lift"/>). On the
    /// column's container, in place of the older <see cref="ColumnWatch"/>. The seats are listed again only when the
    /// container's children change, and the boxes are kept once found (<see cref="PanelChild"/>): they and the guests are
    /// looked up by name again only on a frame the panel's children changed, or every half second (a mod may rename its
    /// box after adding it), so a steady frame finds nothing by name and allocates nothing. Nothing runs while the
    /// inventory is closed: the game never deactivates its root (it only animates it away), so this would otherwise run
    /// all session; the first frame it is open again catches up on whatever changed.
    /// </summary>
    internal sealed class SeatWatch : MonoBehaviour
    {
        private const float RecheckSeconds = 0.5f;

        private readonly List<SeatSlot> seats = new List<SeatSlot>();
        private readonly PanelChild[] guests = Array.ConvertAll(Guests.Known, guest => new PanelChild(guest.Name));
        private readonly int[] guestParts = new int[Guests.Known.Length];
        private GamePlates? game;
        private Canvas? canvas;
        private int listed = -1;
        private int seen = int.MinValue;
        private int panelChildren = -1;
        private float nextRecheck;

        private void LateUpdate()
        {
            if (!InventoryGui.IsVisible() || !(transform.parent is RectTransform panel))
            {
                return;
            }
            RectTransform boxes = (RectTransform)transform;
            bool recheck = Recheck(panel);
            if (Rescue(panel, boxes) | (recheck && Seats.SeatGuests(panel, boxes)))
            {
                Seats.Arrange(panel, boxes);
                listed = -1;
            }
            List();
            bool resized = Seats.Follow(panel, seats, recheck);
            int shape = ColumnWatch.Shape(boxes);
            if (resized || shape != seen)
            {
                seen = shape;
                LayoutRebuilder.ForceRebuildLayoutImmediate(boxes);
            }
            Seats.Pin(panel, boxes, seats);
            LiftGuests(panel, recheck);
        }

        /// <summary>Whether to look the boxes up by name again: the panel's children changed, or half a second passed.</summary>
        private bool Recheck(RectTransform panel)
        {
            float now = Time.unscaledTime;
            if (panel.childCount == panelChildren && now < nextRecheck)
            {
                return false;
            }
            panelChildren = panel.childCount;
            nextRecheck = now + RecheckSeconds;
            return true;
        }

        /// <summary>
        /// Lifts a known box's own canvases (<see cref="Guests.Lift"/>) whenever its children change: a mod may add them
        /// after making the box, as TrashItems does the next frame.
        /// </summary>
        private void LiftGuests(RectTransform panel, bool recheck)
        {
            if (canvas == null)
            {
                canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas;
            }
            for (int i = 0; i < Guests.Known.Length; i++)
            {
                RectTransform? box = guests[i].In(panel, recheck);
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
