// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions from aedenthorn/ValheimMods TerrainReset, public-domain dedication; pin 83690e8ef9a37c4694604416667d252b4ff5f645.
// Adapted from ResetTerrain's bounded height/paint cell traversal. Native ownership and saves belong to ThingTerrain.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Thing.WorldAdmin
{
    public static class TerrainCells
    {
        /// <summary>Indices in a square native buffer. Radius is meters; pitch and spacing describe the actual buffer.</summary>
        public static IEnumerable<int> InRadius(int pitch, float centerX, float centerY, float radius, float spacing)
        {
            if (pitch <= 0 || pitch > 1024 || !Finite(centerX) || !Finite(centerY) || !Finite(radius) || radius < 0 || !Finite(spacing) || spacing <= 0)
                throw new ArgumentOutOfRangeException(nameof(pitch), "Invalid terrain grid.");
            var squaredRadius = radius * radius;
            for (var h = 0; h < pitch; h++)
                for (var w = 0; w < pitch; w++)
                {
                    var dx = (w - centerX) * spacing;
                    var dy = (h - centerY) * spacing;
                    if (dx * dx + dy * dy <= squaredRadius) yield return h * pitch + w;
                }
        }

        public static bool Shape(int pitch, params int[] lengths)
        {
            if (pitch <= 0 || pitch > 1024) return false;
            foreach (var length in lengths) if (length != pitch * pitch) return false;
            return lengths.Length > 0;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
