// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/NightShade.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: the light curve, threshold and fade moved to Core
// NightShadeCurve; the setting and the look switch come from KitLook.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Kit panels darken with the light around the player, as the game's own wooden panels do (their <c>litpanel</c>
    /// material reads the sun and ambient colours <c>EnvMan</c> writes every frame: <c>_SunColor</c>,
    /// <c>_AmbientColor</c>). Applied as each graphic's <c>CanvasRenderer</c> colour, which multiplies its own colour
    /// without changing it, and only to graphics on the default material, so anything still drawn with the game's lit
    /// material is not shaded twice. Cells are never shaded: a Button's hover tint writes the same colour.
    /// </summary>
    public static class NightShade
    {
        private static readonly int SunId = Shader.PropertyToID("_SunColor");
        private static readonly int AmbientId = Shader.PropertyToID("_AmbientColor");

        private static readonly List<Graphic> shaded = new List<Graphic>();
        private static readonly HashSet<Graphic> known = new HashSet<Graphic>();
        private static float current = 1f;

        /// <summary>Shade this graphic from now on, at the current shade straight away (again after a re-skin).</summary>
        public static void Track(Graphic graphic)
        {
            if (graphic == null)
                return;
            if (known.Add(graphic))
                shaded.Add(graphic);
            Paint(graphic, current);
        }

        /// <summary>Plugin Update: follows the light, and repaints only when the shade moved.</summary>
        public static void Update()
        {
            float target = Target();
            if (!NightShadeCurve.Moves(current, target))
                return;
            current = NightShadeCurve.Step(current, target, Time.unscaledDeltaTime);
            for (int i = shaded.Count - 1; i >= 0; i--)
            {
                Graphic graphic = shaded[i];
                if (graphic == null)
                {
                    known.Remove(graphic);
                    shaded.RemoveAt(i);
                    continue;
                }
                Paint(graphic, current);
            }
        }

        private static float Target()
        {
            float night = KitLook.Night;
            if (night >= 1f || !KitLook.On || EnvMan.instance == null)
                return 1f;
            Color ambient = Shader.GetGlobalColor(AmbientId);
            Color sun = Shader.GetGlobalColor(SunId);
            return NightShadeCurve.Target(night, true, true, ambient.r, ambient.g, ambient.b, sun.r, sun.g, sun.b);
        }

        private static void Paint(Graphic graphic, float shade)
        {
            float value = graphic.material == Graphic.defaultGraphicMaterial ? shade : 1f;
            Color tint = new Color(value, value, value, 1f);
            if (graphic.canvasRenderer.GetColor() != tint)
                graphic.canvasRenderer.SetColor(tint);
        }
    }
}
