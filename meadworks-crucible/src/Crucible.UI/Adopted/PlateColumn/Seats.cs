// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/Seats.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// The game's armour and weight boxes stay direct children of the player panel under the game's names, because other
    /// mods find them there by name to copy them or to place their own boxes against them (<see cref="Guests"/>); moving
    /// them into the container broke those mods (Jewelcrafting threw on every item drag, and the inventory stopped
    /// working). In the column each such box has a seat instead: an empty member of the container at the box's rank, as
    /// big as the box and shown while it is, which the layout group places like any other box; the box itself is pinned
    /// over its seat and drawn after the container. A box an older copy of this library moved into the container is moved
    /// back out onto the panel.
    /// </summary>
    internal static class Seats
    {
        /// <summary>Seats <paramref name="box"/> at <paramref name="rank"/>; true when a seat was made or the box moved back out.</summary>
        public static bool Seat(RectTransform panel, RectTransform boxes, RectTransform box, int rank)
        {
            bool moved = MoveOut(panel, boxes, box);
            if (box.parent != panel)
            {
                return moved;
            }
            string name = ColumnLayout.SeatName(rank, box.name);
            if (boxes.Find(name) != null)
            {
                return moved;
            }
            Make(boxes, name, box);
            return true;
        }

        /// <summary>Seats every known box of another mod that is on the panel and not seated yet; true when any seat was made.</summary>
        public static bool SeatGuests(RectTransform panel, RectTransform boxes)
        {
            bool seated = false;
            foreach (Guests guest in Guests.Known)
            {
                if (boxes.Find(guest.Seat) == null && panel.Find(guest.Name) is RectTransform box && Guests.LooksLikeBox(box))
                {
                    Guests.Dress(box);
                    seated |= Seat(panel, boxes, box, guest.Rank);
                }
            }
            return seated;
        }

        /// <summary>
        /// Styles every mod's box (a seated box is left as it is: the game's two are styled when they are seated, another
        /// mod's never), puts the members in rank order, gives each seat its box's size and shown state, lays the column
        /// out now and pins each box over its seat, so positions read right away are true.
        /// </summary>
        public static void Arrange(RectTransform panel, RectTransform boxes)
        {
            List<RectTransform> members = ColumnLayout.Sorted(boxes);
            for (int i = 0; i < members.Count; i++)
            {
                if (ColumnLayout.OccupantOf(members[i].name) == null)
                {
                    BoxStyle.Apply(members[i]);
                }
                if (members[i].GetSiblingIndex() != i)
                {
                    members[i].SetSiblingIndex(i);
                }
            }
            List<SeatSlot> seats = new List<SeatSlot>();
            Collect(boxes, seats);
            Follow(panel, seats, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(boxes);
            Pin(panel, boxes, seats);
        }

        /// <summary>Every seat in the container, with the name of the box it holds the place of.</summary>
        public static void Collect(RectTransform boxes, List<SeatSlot> into)
        {
            into.Clear();
            foreach (Transform child in boxes)
            {
                if (child is RectTransform seat && ColumnLayout.OccupantOf(child.name) is string name)
                {
                    into.Add(new SeatSlot(seat, name));
                }
            }
        }

        /// <summary>The box a member stands for: a seat's box on the panel (null when it is gone), any other member itself.</summary>
        public static RectTransform? BoxOf(RectTransform panel, RectTransform member)
        {
            string? name = ColumnLayout.OccupantOf(member.name);
            return name == null ? member : panel.Find(name) as RectTransform;
        }

        /// <summary>
        /// Each seat takes its box's size and shown state; true when any seat changed. The boxes are the ones kept from
        /// before (<see cref="PanelChild"/>), looked up by name again only with <paramref name="recheck"/>.
        /// </summary>
        public static bool Follow(RectTransform panel, List<SeatSlot> seats, bool recheck)
        {
            bool changed = false;
            foreach (SeatSlot slot in seats)
            {
                if (slot.Seat != null)
                {
                    changed |= Follow(slot.Seat, slot.Box.In(panel, recheck));
                }
            }
            return changed;
        }

        /// <summary>Each shown box over its seat and drawn after the container; nothing is written for a box already there.</summary>
        public static void Pin(RectTransform panel, RectTransform boxes, List<SeatSlot> seats)
        {
            foreach (SeatSlot slot in seats)
            {
                if (slot.Seat != null && slot.Seat.gameObject.activeSelf && slot.Box.In(panel, false) is RectTransform box)
                {
                    Pin(panel, boxes, slot.Seat, box);
                }
            }
        }

        private static bool Follow(RectTransform seat, RectTransform? box)
        {
            bool shown = box != null && box.gameObject.activeSelf;
            bool changed = seat.gameObject.activeSelf != shown;
            if (changed)
            {
                seat.gameObject.SetActive(shown);
            }
            if (box == null)
            {
                return changed;
            }
            Vector2 size = Vector2.Scale(box.rect.size, box.localScale);
            if ((seat.sizeDelta - size).sqrMagnitude > 0.01f)
            {
                seat.sizeDelta = size;
                changed = true;
            }
            return changed;
        }

        /// <summary>Centre on centre, in the panel's space (the box is the panel's child), whatever the box's anchors and pivot.</summary>
        private static void Pin(RectTransform panel, RectTransform boxes, RectTransform seat, RectTransform box)
        {
            Vector2 target = panel.InverseTransformPoint(seat.TransformPoint(seat.rect.center));
            Vector2 now = panel.InverseTransformPoint(box.TransformPoint(box.rect.center));
            Vector2 shift = target - now;
            if (shift.sqrMagnitude > 0.01f)
            {
                box.localPosition += (Vector3)shift;
            }
            int after = boxes.GetSiblingIndex() + 1;
            if (box.GetSiblingIndex() < after)
            {
                box.SetSiblingIndex(after);
            }
        }

        private static bool MoveOut(RectTransform panel, RectTransform boxes, RectTransform box)
        {
            if (box.parent != boxes)
            {
                return false;
            }
            box.SetParent(panel, true);
            box.SetSiblingIndex(boxes.GetSiblingIndex() + 1);
            return true;
        }

        /// <summary>An empty stand-in anchored as the layout group anchors its children, as big as the box and shown as it is.</summary>
        private static void Make(RectTransform boxes, string name, RectTransform box)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = boxes.gameObject.layer;
            RectTransform seat = (RectTransform)go.transform;
            seat.SetParent(boxes, false);
            seat.anchorMin = new Vector2(0f, 1f);
            seat.anchorMax = new Vector2(0f, 1f);
            seat.pivot = new Vector2(0.5f, 0.5f);
            seat.sizeDelta = Vector2.Scale(box.rect.size, box.localScale);
            go.SetActive(box.gameObject.activeSelf);
        }
    }
}
