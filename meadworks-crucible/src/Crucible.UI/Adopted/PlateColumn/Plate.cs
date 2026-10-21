// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/Plate.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// A box a mod added to the column: <see cref="Rect"/> is the box itself (a child of the column's container; hide it
    /// with <c>SetActive(false)</c> and the column closes the gap), <see cref="Icon"/> the image at its top showing the
    /// mod's sprite, and <see cref="Text"/> the number line along its bottom when the box keeps one.
    /// </summary>
    public sealed class Plate
    {
        public Plate(RectTransform rect, Image icon, TMP_Text? text)
        {
            Rect = rect;
            Icon = icon;
            Text = text;
        }

        public RectTransform Rect { get; }

        public Image Icon { get; }

        public TMP_Text? Text { get; }
    }
}
