using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Conversions between the Core kit's Unity-free values and Unity's.</summary>
    public static class KitUnity
    {
        public static Color Colour(KitColor colour) => new Color(colour.R, colour.G, colour.B, colour.A);

        public static Vector4 Vector4(KitColor value) => new Vector4(value.R, value.G, value.B, value.A);

        public static KitRect Rect(Rect rect) => new KitRect(rect.x, rect.y, rect.width, rect.height);

        public static Rect Rect(KitRect rect) => new Rect(rect.X, rect.Y, rect.Width, rect.Height);

        public static Vector2 Vector(KitVector vector) => new Vector2(vector.X, vector.Y);

        public static KitVector Vector(Vector2 vector) => new KitVector(vector.x, vector.y);

        /// <summary>
        /// The kit's safe destroy, for kit parts and for products destroying children a kit part may look up again by name.
        /// Unity destroys at the end of the frame, so a plain <c>Object.Destroy</c> leaves the child findable for the rest
        /// of the frame (Threshold's picker NRE: <c>KitButton.Create</c> reused a dying button). This hides the child,
        /// renames it (<see cref="KitChildNames.Retired"/>) so no kit lookup returns it again, then destroys it.
        /// </summary>
        public static void Retire(GameObject child)
        {
            if (child == null)
                return;
            child.SetActive(false);
            child.name = KitChildNames.Retired(child.name);
            Object.Destroy(child);
        }

        /// <summary>The first child of <paramref name="parent"/> named <paramref name="name"/> that is not retired, or null.</summary>
        public static Transform FindLive(Transform parent, string name)
        {
            if (parent == null)
                return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && KitChildNames.Reusable(child.name, name))
                    return child;
            }
            return null;
        }
    }
}
