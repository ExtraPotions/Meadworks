// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Recolours a workshop particle effect authored on a fully saturated green (the loot beam, the Rune Table's vortex):
    /// every system's start colour moved to the colour's hue, its saturation times the system's own share, full value, the
    /// authored alpha. Exact for any colour, as the effects keep colour over lifetime white and change only alpha.
    /// </summary>
    internal static class ParticleRetint
    {
        public static void Tint(GameObject effect, Color colour)
        {
            Color.RGBToHSV(colour, out float hue, out float saturation, out _);
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = Retint(main.startColor, hue, saturation);
            }
        }

        private static ParticleSystem.MinMaxGradient Retint(ParticleSystem.MinMaxGradient start, float hue, float saturation)
        {
            switch (start.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Retint(start.color, hue, saturation));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Retint(start.colorMin, hue, saturation),
                        Retint(start.colorMax, hue, saturation));
                default:
                    return start;
            }
        }

        private static Color Retint(Color authored, float hue, float saturation)
        {
            Color.RGBToHSV(authored, out _, out float share, out _);
            Color colour = Color.HSVToRGB(hue, saturation * share, 1f);
            colour.a = authored.a;
            return colour;
        }
    }
}
