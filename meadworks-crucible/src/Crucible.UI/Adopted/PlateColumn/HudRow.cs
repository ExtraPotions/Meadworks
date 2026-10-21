// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/HudRow.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// Small boxes on the HUD, in a column right of the minimap, for readouts a player wants in view without opening the
    /// inventory (the user asked for the world tier and the weight, then for them beside the map, top to bottom). A HUD box
    /// is made like a column box - a copy of the column's restyled armour box with the mod's icon and an empty number line
    /// - but lives in the HUD column (<see cref="HudContainer"/>), takes no pointer and has no tooltip. The column hides
    /// with the small map (the big map open, a world without a map, the HUD hidden). Boxes sort top to bottom by rank,
    /// named as column boxes are, so every copy of this library reads the same order. The game writes nothing here: the mod
    /// writes its box's text itself, every frame or when the value changes, and hides the box with
    /// <c>SetActive(false)</c>; the column closes the gap.
    /// </summary>
    public static class HudRow
    {
        /// <summary>The column's size against the inventory's: a 64 unit box shows at about 45, a quarter of the minimap's width.</summary>
        public const float Scale = 0.7f;

        /// <summary>Units between the minimap's right edge and the column.</summary>
        public const float Gap = 6f;

        /// <summary>
        /// The mod's HUD box for this spec (its tooltip words are unused), found when the row already has it, made
        /// otherwise, with the row put in rank order. Null without PackPanel laying out the inventory (the row is PackPanel's
        /// look, as the column is: <see cref="Column.Active"/>), while there is no minimap, or when the inventory's plates
        /// are missing.
        /// </summary>
        public static Plate? Add(InventoryGui gui, PlateSpec spec)
        {
            if (!Column.Active)
            {
                return null;
            }
            RectTransform? row = HudContainer.Get();
            BoxStack? stack = row != null ? BoxStack.Ready(gui) : null;
            if (row == null || stack == null)
            {
                return null;
            }
            string name = ColumnLayout.NameOf(spec);
            Plate? plate = row.Find(name) is RectTransform existing ? PlateCopy.Wrap(existing) : Make(stack, spec, name, row);
            if (plate != null)
            {
                Order(row);
            }
            return plate;
        }

        /// <summary>
        /// The HUD column itself, made when missing, for the mod laying out the HUD to size it and make room for it. Null
        /// without PackPanel laying out the inventory (<see cref="Column.Active"/>) or while there is no minimap.
        /// </summary>
        public static RectTransform? Container() => Column.Active ? HudContainer.Get() : null;

        /// <summary>The icon of the game's weight box, for a mod that shows the weight on the HUD; null when it is missing.</summary>
        public static Sprite? WeightIcon(InventoryGui gui)
        {
            GamePlates? game = GamePlates.Find(gui);
            Plate? weight = game != null ? PlateCopy.Wrap(game.Weight) : null;
            return weight != null ? weight.Icon.sprite : null;
        }

        private static Plate? Make(BoxStack stack, PlateSpec spec, string name, RectTransform row)
        {
            Plate? plate = PlateCopy.Make(stack.Game.Armor, spec, name, row);
            if (plate != null)
            {
                foreach (Graphic graphic in plate.Rect.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }
            return plate;
        }

        private static void Order(RectTransform row)
        {
            List<RectTransform> members = ColumnLayout.Sorted(row);
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].GetSiblingIndex() != i)
                {
                    members[i].SetSiblingIndex(i);
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        }
    }
}
