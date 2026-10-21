using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// The preview's light, handed to the preview shaders as globals in linear colour: a low evening sun, ambient, and
    /// up to four of the effects' point lights. Lights play the game's LightFlicker the way its code does (the flicker
    /// wave, the fade in, the fade out before the time to live), from the spec for our lights and from the reference
    /// export's settings for the game's (VfxBuild passes them in).
    /// </summary>
    public static class VfxLighting
    {
        public static readonly Vector3 SunDirection = Quaternion.Euler(28f, -40f, 0f) * Vector3.back;
        public static readonly Color SunColour = new Color(1.0f, 0.86f, 0.7f) * 0.8f;
        public static readonly Color Ambient = new Color(0.13f, 0.15f, 0.2f);

        /// <summary>A light and its flicker settings (null: steady), with the intensity it started at.</summary>
        public class Tracked
        {
            public Light light;
            public FlickerSpec flicker;
            public float baseIntensity, offset;
        }

        public static List<Tracked> Track(GameObject instance, Func<Light, FlickerSpec> flicker) =>
            instance.GetComponentsInChildren<Light>(true).Select(l => new Tracked
            {
                light = l, flicker = flicker(l), baseIntensity = l.intensity, offset = UnityEngine.Random.Range(0f, 10f)
            }).ToList();

        public static void Apply(IEnumerable<Tracked> lights, float time)
        {
            Shader.SetGlobalVector("_VfxSunDir", SunDirection.normalized);
            Shader.SetGlobalVector("_VfxSunColor", SunColour.linear);
            Shader.SetGlobalVector("_VfxAmbient", Ambient.linear);
            var active = lights.Where(t => t.light != null && t.light.enabled && t.light.gameObject.activeInHierarchy)
                .Select(t => (t, Intensity(t, time))).Where(p => p.Item2 > 0.001f).OrderByDescending(p => p.Item2).Take(4).ToList();
            var positions = new Vector4[4];
            var colours = new Vector4[4];
            for (int i = 0; i < active.Count; i++)
            {
                Light light = active[i].t.light;
                positions[i] = new Vector4(light.transform.position.x, light.transform.position.y, light.transform.position.z, light.range);
                colours[i] = light.color.linear * active[i].Item2;
            }
            Shader.SetGlobalVectorArray("_VfxLightPos", positions);
            Shader.SetGlobalVectorArray("_VfxLightColor", colours);
            Shader.SetGlobalFloat("_VfxLightCount", active.Count);
        }

        /// <summary>The game's LightFlicker.CustomUpdate for intensity: a three-wave flicker, a linear fade in, a fade out
        /// over the last fade seconds before the time to live, nothing after it.</summary>
        public static float Intensity(Tracked t, float time)
        {
            FlickerSpec f = t.flicker;
            if (f == null || !f.enabled)
                return t.baseIntensity;
            float n = t.offset + time * f.speed;
            float target = 1f + Mathf.Sin(n) * Mathf.Sin(n * 0.56436f) * Mathf.Cos(n * 0.758348f) * f.intensity;
            if (f.fade_in > 0f)
                target *= Mathf.Clamp01(time / f.fade_in);
            if (f.ttl > 0f)
                target = time > f.ttl ? 0f : target * (1f - Mathf.Clamp01((time - (f.ttl - f.fade)) / Mathf.Max(f.fade, 0.0001f)));
            return t.baseIntensity * target;
        }
    }
}
