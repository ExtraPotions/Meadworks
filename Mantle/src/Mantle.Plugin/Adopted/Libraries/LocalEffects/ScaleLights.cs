using System.Reflection;
using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.LocalEffects
{
    /// <summary>
    /// The lights of a copy being resized (see <see cref="ScaleParts"/>): each reaches as far times the factor. The game
    /// drives many effect lights from components that read the light as it wakes - before a copy can be resized - and
    /// put those numbers back later: <see cref="LightLod"/> zeroes the range and fades it back up to the range it read,
    /// and <see cref="LightFlicker"/> sets the light's position from the one it read plus a wobble. Those remembered
    /// numbers are resized too, or the light would grow back to full size a moment later. Read by reflection, as this
    /// library does not publicize the game's assembly.
    /// </summary>
    internal static class ScaleLights
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo? LodRange = typeof(LightLod).GetField("m_baseRange", Private);
        private static readonly FieldInfo? FlickerHome = typeof(LightFlicker).GetField("m_basePosition", Private);

        public static void Apply(GameObject clone, float factor)
        {
            foreach (Light light in clone.GetComponentsInChildren<Light>(true))
            {
                light.range *= factor;
            }
            foreach (LightLod lod in clone.GetComponentsInChildren<LightLod>(true))
            {
                if (LodRange?.GetValue(lod) is float range)
                {
                    LodRange.SetValue(lod, range * factor);
                }
            }
            foreach (LightFlicker flicker in clone.GetComponentsInChildren<LightFlicker>(true))
            {
                flicker.m_movement *= factor;
                if (FlickerHome?.GetValue(flicker) is Vector3 home)
                {
                    FlickerHome.SetValue(flicker, home * factor);
                }
            }
        }
    }
}
