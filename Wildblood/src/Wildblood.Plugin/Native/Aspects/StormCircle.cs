// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One Stormbound circle, drawn locally on each client: a glowing line of the circle's exact radius laid over the
    /// ground (each point dropped onto the floor near the centre's height, so it follows a slope and a step but never
    /// falls off a ledge), and a blue light at its heart that brightens as the strike nears and pulses faster, so a
    /// player sees it round their own feet and on the ground around them. It stays where it was put. The line is the
    /// warning itself, so it is drawn whatever the effect density; the light follows it. When the lightning falls the
    /// line and light go out and the strike effect plays in their place; the object removes itself once that is done.
    /// </summary>
    internal sealed class StormCircle : MonoBehaviour
    {
        private const int Segments = 48;
        private const float LineWidth = 0.22f;
        private const float LightIntensity = 3f;

        /// <summary>How long the object outlives the strike, for the strike effect and its crack to play out.</summary>
        private const float StrikeLife = 4f;

        private static readonly Color Glow = new Color(0.40f, 0.80f, 1.00f, 1f);

        private LineRenderer? _line;
        private Light _light = null!;
        private Vector3 _center;
        private float _radius;
        private float _born;
        private float _tell;
        private float _brightness;

        /// <summary>A new circle at <paramref name="center"/>, striking in <paramref name="tell"/> seconds.</summary>
        public static StormCircle Draw(Vector3 center, float radius, float tell)
        {
            GameObject holder = new GameObject("ecr_storm_circle");
            holder.transform.position = center + Vector3.up; // the light sits a metre up, over the ring
            StormCircle circle = holder.AddComponent<StormCircle>();
            circle.Build(center, radius, tell);
            return circle;
        }

        private void Build(Vector3 center, float radius, float tell)
        {
            _center = center;
            _radius = radius;
            _born = Time.time;
            _tell = Mathf.Max(0.1f, tell);
            _brightness = LightIntensity * StormEffects.Density();
            _line = Ring(center, radius);
            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Glow;
            _light.range = radius * 2f + 2f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
        }

        private LineRenderer? Ring(Vector3 center, float radius)
        {
            Material? material = StormEffects.RingMaterial();
            if (material == null)
            {
                return null; // no material to draw with: the light alone marks the circle
            }
            LineRenderer line = gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.SetPositions(Points(center, radius));
            line.widthMultiplier = LineWidth;
            line.startColor = Glow;
            line.endColor = Glow;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        // Each point dropped onto the floor within a couple of metres of the centre's height, lifted a hair off it.
        private static Vector3[] Points(Vector3 center, float radius)
        {
            Vector3[] points = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float angle = Mathf.PI * 2f * i / Segments;
                Vector3 point = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                point.y = StormTargets.FloorY(point, center.y, 2f, 2f) + 0.08f;
                points[i] = point;
            }
            return points;
        }

        private void Update() => Guard.Run("StormCircle.Update", static self => self.Pulse(), this);

        // Brighter and quicker as the strike nears: from a steady glow to a fast throb in the last moments.
        private void Pulse()
        {
            float t = Mathf.Clamp01((Time.time - _born) / _tell);
            float throb = 0.75f + 0.25f * Mathf.Sin((Time.time - _born) * Mathf.Lerp(6f, 30f, t));
            _light.intensity = _brightness * Mathf.Lerp(0.4f, 1f, t) * throb;
            if (_line != null)
            {
                Color color = Glow;
                color.a = Mathf.Lerp(0.6f, 1f, t) * throb;
                _line.startColor = color;
                _line.endColor = color;
            }
        }

        /// <summary>The lightning falls here: the circle goes out and the strike plays in its place.</summary>
        public void Strike()
        {
            enabled = false;
            if (_line != null)
            {
                _line.enabled = false;
            }
            _light.enabled = false;
            StormEffects.Strike(transform, _center, _radius);
            Destroy(gameObject, StrikeLife);
        }

        /// <summary>The storm broke before it fell (the boss died, or went out of reach): gone at once.</summary>
        public void Dismiss() => Destroy(gameObject);
    }
}
