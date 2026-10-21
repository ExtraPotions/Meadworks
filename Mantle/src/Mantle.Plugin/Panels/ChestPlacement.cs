using System;

namespace Mantle.Panels
{
    internal static class ChestPlacement
    {
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool BesideFits(float left, float top, float width, float height, float screenRight, float screenBottom, float margin)
        {
            if (!Finite(left) || !Finite(top) || !Finite(width) || !Finite(height) || !Finite(screenRight) || !Finite(screenBottom)
                || !Finite(margin) || width <= 0 || height <= 0) return false;
            return left + width <= screenRight - margin && top - height >= screenBottom + margin;
        }
    }
}
