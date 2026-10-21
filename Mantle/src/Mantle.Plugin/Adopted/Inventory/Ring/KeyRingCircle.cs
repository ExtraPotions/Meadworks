using UnityEngine;

namespace Mantle.Ring
{
    /// <summary>
    /// Where the key ring pop-up's cells sit (the user's idea: the keys hang round a ring): evenly round a circle, the
    /// first at the top and the rest clockwise, neighbours a little more than one grid step apart and the circle never
    /// smaller than <see cref="MinRadius"/> steps, so even two keys read as a ring. The pop-up is a disc just big enough
    /// for the cells' corners and its frame. Positions are from the circle's centre, y up. The cells are drawn at
    /// <see cref="CellScale"/> of the grid's (the user asked for a smaller ring), so the step and cell size passed in are
    /// the scaled ones.
    /// </summary>
    public static class KeyRingCircle
    {
        public const float CellScale = 0.65f;
        private const float Spread = 1.12f;
        private const float MinRadius = 0.95f;
        private const float Frame = 10f;

        /// <summary>The bronze wire's picture is this many times the circle's radius wide (the wire runs near its edge).</summary>
        public const float WireScale = 2.077f;

        public static float Radius(int count, float step)
        {
            float least = MinRadius * step;
            return count < 2 ? least : Mathf.Max(least, Spread * step / (2f * Mathf.Sin(Mathf.PI / count)));
        }

        public static Vector2 Position(int index, int count, float radius)
        {
            float angle = Mathf.PI / 2f - 2f * Mathf.PI * index / Mathf.Max(1, count);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <summary>The disc's diameter: the circle, a cell's corner beyond it, and the frame.</summary>
        public static float Diameter(float radius, float cell) => 2f * (radius + cell * 0.7072f + Frame);
    }
}
