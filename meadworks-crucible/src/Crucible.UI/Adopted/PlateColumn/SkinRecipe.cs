// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/SkinRecipe.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// How to paint one skin sprite: a square texture of <see cref="Size"/> pixels whose corners are rounded by
    /// <see cref="Radius"/>, bordered by <see cref="Rings"/> from the outside in and filled with <see cref="Fill"/>;
    /// <see cref="Border"/> is the 9-slice border on every side, wide enough to hold the rounded corners and the rings so
    /// they keep their size however large the Image is drawn.
    /// </summary>
    internal sealed class SkinRecipe
    {
        public SkinRecipe(string name, int size, float radius, int border, Color32 fill, params SkinRing[] rings)
        {
            Name = name;
            Size = size;
            Radius = radius;
            Border = border;
            Fill = fill;
            Rings = rings;
        }

        public string Name { get; }

        public int Size { get; }

        public float Radius { get; }

        public int Border { get; }

        public Color32 Fill { get; }

        public SkinRing[] Rings { get; }
    }
}
