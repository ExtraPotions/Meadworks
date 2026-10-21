// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/Column.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// The stat readouts on the inventory's player panel as one column of small brown boxes any of our mods can add to:
    /// the game's armour and weight plates, restyled, and every box a mod adds, top to bottom by rank, stacked just
    /// outside the panel's top-right corner. Each box is <see cref="BoxSize"/> square with its icon at the top and its
    /// number underneath, drawn with <see cref="Skin.Box"/>; each carries a tooltip pinned right of it.
    /// <para>
    /// Each mod merges its own copy of this library, so nothing is shared in memory: the column lives in the scene. It is
    /// one container object on the panel, <c>PlateColumn_boxes</c>, whose Unity layout group stacks its active children
    /// in sibling order; a mod's box is a child named with its rank, so every copy of this code sorts the same column from
    /// what it finds, whichever mod arranges last, and a hidden box leaves no gap. Plates an older copy of the library
    /// left on the panel are adopted into the container as boxes (the same objects, so references to them keep working).
    /// The game's armour and weight plates are never moved into it: other mods find them on the panel by name, so they
    /// stay there and are pinned over seats in the container (<see cref="Seats"/>), as are the known boxes other mods copy
    /// from the armour plate (<see cref="Guests"/>). The column is PackPanel's look and exists only while PackPanel lays
    /// out the inventory (<see cref="Active"/>); without it no copy of this library touches the game's plates.
    /// </para>
    /// </summary>
    public static class Column
    {
        /// <summary>
        /// Whether the column exists at all: only while PackPanel lays out the inventory (<see cref="PackPanelOwner"/>).
        /// Without it <see cref="Arrange"/>, <see cref="Boxes"/> and <see cref="Add"/> change nothing and return false or
        /// null, and the game's plates stay as the game draws them; check this first rather than treat that as a failure.
        /// </summary>
        public static bool Active => PackPanelOwner.LaysOutInventory;

        public const int ArmorRank = 100;
        public const int WeightRank = 300;

        /// <summary>The gap between the player panel's right edge and the column.</summary>
        public const float Left = 8f;

        /// <summary>The edge of every box.</summary>
        public const float BoxSize = 64f;

        /// <summary>The gap between two boxes.</summary>
        public const float Spacing = 6f;

        /// <summary>How far under the player panel's top edge the first box's top sits.</summary>
        public const float Top = 6f;

        /// <summary>
        /// Builds the column if needed - the container, the game's plates and any older plates adopted and styled as
        /// boxes, tooltips on the game's two - puts every box in rank order and lays it out now. Call it again after
        /// showing or hiding a box (the column also notices on its own the same frame). False, with nothing changed,
        /// when either game plate is missing.
        /// </summary>
        public static bool Arrange(InventoryGui gui) => BoxStack.Ready(gui) != null;

        /// <summary>
        /// The container holding the boxes (<c>PlateColumn_boxes</c>, a child of <c>gui.m_player</c>, anchored to the
        /// panel's top-right corner with its pivot at its own top-left, as wide as a box and as tall as the visible
        /// boxes), after building and arranging the column as <see cref="Arrange"/> does; for a mod that places something
        /// against the column. Null when the game's plates are missing.
        /// </summary>
        public static RectTransform? Boxes(InventoryGui gui) => BoxStack.Ready(gui)?.Boxes;

        /// <summary>
        /// The mod's box for this spec - found when the column already has it, made from a copy of the armour box
        /// otherwise - with its tooltip, and the column arranged around it. <see cref="Plate.Rect"/> is the box. Null
        /// when the game's plates are missing.
        /// </summary>
        public static Plate? Add(InventoryGui gui, PlateSpec spec)
        {
            BoxStack? stack = BoxStack.Ready(gui);
            if (stack == null)
            {
                return null;
            }
            string name = ColumnLayout.NameOf(spec);
            Plate? plate = stack.Boxes.Find(name) is RectTransform existing
                ? PlateCopy.Wrap(existing)
                : PlateCopy.Make(stack.Game.Armor, spec, name, stack.Boxes);
            if (plate != null)
            {
                PlateTips.Set(gui, plate.Rect, spec.Topic, spec.Tip);
                stack.Order();
            }
            return plate;
        }

        /// <summary>
        /// Every box in the column whose container is <paramref name="boxes"/>, in the container's sibling order: its
        /// children, each seat replaced by the box it holds the place of (the game's armour and weight plates, which stay
        /// on the player panel, and seated boxes of other mods); a seat whose box is gone is left out. For a mod that
        /// dresses the boxes itself. <paramref name="into"/> is cleared first.
        /// </summary>
        public static void BoxesIn(RectTransform boxes, List<RectTransform> into)
        {
            into.Clear();
            RectTransform? panel = boxes.parent as RectTransform;
            foreach (Transform child in boxes)
            {
                RectTransform? box = panel != null && child is RectTransform member ? Seats.BoxOf(panel, member) : child as RectTransform;
                if (box != null)
                {
                    into.Add(box);
                }
            }
        }

        /// <summary>
        /// Gives one of the game's own tooltips (a <c>UITooltip</c>, which follows the pointer) the box the column's
        /// tips show: the game's inventory item tooltip with the gold border. For a mod whose tips live elsewhere, such
        /// as the skills panel, so every tip reads alike. The game still decides when and where it shows. False, with
        /// nothing changed, when the item tooltip cannot be found.
        /// </summary>
        public static bool DressTooltip(InventoryGui gui, UITooltip tooltip)
        {
            GameObject? template = TipTemplate.For(gui);
            if (template == null || tooltip == null)
            {
                return false;
            }
            tooltip.m_tooltipPrefab = template;
            return true;
        }
    }
}
