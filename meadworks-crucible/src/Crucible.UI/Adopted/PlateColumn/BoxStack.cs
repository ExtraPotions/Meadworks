// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/BoxStack.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// The column as it stands in the scene, readied on every <see cref="Column.Arrange"/> and <see cref="Column.Add"/>;
    /// each step is safe to repeat and to run from any copy of this library. Readying finds the game's plates, finds or
    /// makes the container, adopts every mod's box still sitting on the panel itself (plates an older copy made,
    /// OpenKeep's pre-library trash plate) by moving the very same objects into the container, seats the game's two plates
    /// and any known box of another mod, which stay on the panel (<see cref="Seats"/>), gives the game's plates their
    /// tooltips, styles every box, puts the members in rank order and lays the column out at once.
    /// </summary>
    internal sealed class BoxStack
    {
        private readonly RectTransform panel;

        private BoxStack(RectTransform panel, RectTransform boxes, GamePlates game)
        {
            this.panel = panel;
            Boxes = boxes;
            Game = game;
        }

        /// <summary>The container, <c>PlateColumn_boxes</c>.</summary>
        public RectTransform Boxes { get; }

        public GamePlates Game { get; }

        /// <summary>
        /// The readied column, or null (with nothing changed) without PackPanel laying out the inventory
        /// (<see cref="PackPanelOwner"/>) or when the game's plates are missing.
        /// </summary>
        public static BoxStack? Ready(InventoryGui gui)
        {
            GamePlates? game = PackPanelOwner.LaysOutInventory ? GamePlates.Find(gui) : null;
            if (game == null)
            {
                return null;
            }
            BoxStack stack = new BoxStack(gui.m_player, BoxContainer.Get(gui.m_player), game);
            stack.Adopt();
            BoxStyle.Apply(game.Armor);
            BoxStyle.Apply(game.Weight);
            Seats.Seat(stack.panel, stack.Boxes, game.Armor, Column.ArmorRank);
            Seats.Seat(stack.panel, stack.Boxes, game.Weight, Column.WeightRank);
            Seats.SeatGuests(stack.panel, stack.Boxes);
            game.Tip(gui);
            stack.Order();
            return stack;
        }

        /// <summary>
        /// Styles every box, moves the members to the top of the container in rank order (anything else a mod put in the
        /// container goes after them), and lays the column out and pins the seated boxes now rather than at the end of the
        /// frame, so positions read right away are true.
        /// </summary>
        public void Order() => Seats.Arrange(panel, Boxes);

        /// <summary>Moves mods' boxes that are direct children of the panel into the container, active or not.</summary>
        private void Adopt()
        {
            List<Transform> strays = new List<Transform>();
            foreach (Transform child in panel)
            {
                if (ColumnLayout.IsMember(child))
                {
                    strays.Add(child);
                }
            }
            foreach (Transform stray in strays)
            {
                stray.SetParent(Boxes, false);
            }
        }
    }
}
