using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>One band of a skin's border, measured inward from the outer edge.</summary>
    internal readonly struct SkinRing
    {
        public SkinRing(float width, Color32 colour)
        {
            Width = width;
            Colour = colour;
        }

        public float Width { get; }

        public Color32 Colour { get; }
    }
}
