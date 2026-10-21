// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/PlateTips.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
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
