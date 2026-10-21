// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/Guests.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// Other mods' boxes beside the game's. Several mods copy the game's armour box, found on the player panel by name
    /// (<c>m_player.Find("Armor")</c>), keep the copy on the panel under a name of their own and place it against the
    /// game's armour and weight boxes: Jewelcrafting's synergy box, the coin pocket of CurrencyPocket and OttoPay, the
    /// trash can of TrashItems. That is why the column leaves the game's boxes on the panel (<see cref="Seats"/>). Once
    /// one of these shows up it gets a seat at its rank, so it sits in the column rather than on one of its boxes: the
    /// synergy box right under the armour, where Jewelcrafting puts it; a trash can (TrashItems', Quick Stack Store Sort
    /// Trash Restock's, or OpenKeep's own since 1.11.0, all named <c>Trash</c>) under it; the coin pocket
    /// between the armour and the weight, where those mods put it. Only a copy of a box is seated (it has the armour box's
    /// <c>armor_icon</c>). It is moved, never restyled: its mod may add parts of its own later (TrashItems puts a clickable
    /// canvas bigger than the box on its can a frame after making it, which a restyle would take for the background). It
    /// only loses the armour tooltip it copied along, and its own canvases are lifted above the inventory's
    /// (<see cref="Lift"/>).
    /// </summary>
    internal sealed class Guests
    {
        private const string BoxPart = "armor_icon";
        private static readonly List<Canvas> Canvases = new List<Canvas>();

        public static readonly Guests[] Known =
        {
            new Guests("Jewelcrafting Synergy", 110),
            new Guests("Trash", 125),
            new Guests("CoinPocketUI", 200),
        };

        private Guests(string name, int rank)
        {
            Name = name;
            Rank = rank;
            Seat = ColumnLayout.SeatName(rank, name);
        }

        /// <summary>The copy's name on the panel.</summary>
        public string Name { get; }

        public int Rank { get; }

        /// <summary>Its seat's name, made once so the per-frame check allocates nothing.</summary>
        public string Seat { get; }

        /// <summary>Whether <paramref name="box"/> is a copy of a box rather than something else under the same name.</summary>
        public static bool LooksLikeBox(Transform box) => box.Find(BoxPart) != null;

        /// <summary>
        /// Lifts the box's own canvases that sort themselves just above <paramref name="root"/>, the inventory's canvas
        /// (order 600). TrashItems takes clicks on its can through such a canvas at order 1, which loses the pointer to
        /// anything of the inventory's under it: the box's own background, which takes the pointer for the column's
        /// tooltips, and PackPanel's stats panel. Only raised, never lowered.
        /// </summary>
        public static void Lift(RectTransform box, Canvas root)
        {
            box.GetComponentsInChildren(true, Canvases);
            foreach (Canvas canvas in Canvases)
            {
                if (canvas.overrideSorting && canvas.sortingOrder <= root.sortingOrder)
                {
                    canvas.sortingOrder = root.sortingOrder + 1;
                }
            }
            Canvases.Clear();
        }

        /// <summary>Any copy of this library's tooltip, copied from the armour box along with the rest, removed.</summary>
        public static void Dress(RectTransform box)
        {
            foreach (MonoBehaviour behaviour in box.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().FullName == typeof(PlateTip).FullName)
                {
                    Object.Destroy(behaviour);
                }
            }
        }
    }
}
