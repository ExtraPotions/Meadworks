using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
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
