// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The tether as one client sees it: a thin line from one boss's centre to the other's, drawn locally and never
    /// networked. Slack, pale blue and barely there while the two are level, it pulls taut, widens, brightens and reddens
    /// as their health draws apart, and it hums - a slow ripple running along it and a pulse in its glow, both quicker the
    /// tauter it is - so a player reads from across the fight how hard the pair is pushing. A hit's jump in the gap eases
    /// in over a moment rather than snapping. Its points live in one array reused every frame, so drawing allocates
    /// nothing. A dedicated server builds nothing.
    /// </summary>
    internal sealed class TetherLine
    {
        private const int Points = 24;
        private const float SlackWidth = 0.08f;
        private const float TautWidth = 0.2f;
        private const float SlackAlpha = 0.3f;
        private const float TautAlpha = 0.9f;

        /// <summary>The most a slack tether droops at its middle, in metres, reached once the two stand this far apart.</summary>
        private const float MaxSag = 0.6f;
        private const float SagReach = 20f;

        /// <summary>How fast the drawn tautness closes on the measured one, per second.</summary>
        private const float Settle = 1.5f;

        private static readonly Color Slack = new Color(0.55f, 0.75f, 1.00f, 1f);
        private static readonly Color Taut = new Color(1.00f, 0.30f, 0.20f, 1f);

        private readonly LineRenderer _line;
        private readonly Vector3[] _points = new Vector3[Points];
        private float _shown;
        private float _wave;
        private float _pulse;

        private TetherLine(LineRenderer line) => _line = line;

        /// <summary>A hidden line ready to draw; null on a dedicated server or when no shader could be found.</summary>
        public static TetherLine? Create()
        {
            Material? material = Headless() ? null : TetherGlow.Material();
            if (material == null)
            {
                return null;
            }
            LineRenderer line = new GameObject("ecr_tether").AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = Points;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return new TetherLine(line);
        }

        /// <summary>This frame's tether from <paramref name="from"/> to <paramref name="to"/> at a tautness of 0 to 1.</summary>
        public void Draw(Vector3 from, Vector3 to, float strength)
        {
            if (_line == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            _shown = Mathf.MoveTowards(_shown, strength, dt * Settle);
            _wave = Mathf.Repeat(_wave + dt * Mathf.Lerp(0.4f, 1.6f, _shown), 1f);
            _pulse = Mathf.Repeat(_pulse + dt * Mathf.Lerp(2f, 7f, _shown), Mathf.PI * 2f);
            Shape(from, to);
            _line.SetPositions(_points);
            Color color = Color.Lerp(Slack, Taut, _shown);
            color.a = Mathf.Lerp(SlackAlpha, TautAlpha, _shown) * (0.85f + 0.15f * Mathf.Sin(_pulse));
            _line.startColor = color;
            _line.endColor = color;
            _line.widthMultiplier = Mathf.Lerp(SlackWidth, TautWidth, _shown);
            _line.enabled = true;
        }

        // Straight between the two, drooping at the middle while slack and rippling with a wave that runs from end to
        // end; both ends stay pinned to the bodies.
        private void Shape(Vector3 from, Vector3 to)
        {
            float sag = MaxSag * (1f - _shown) * Mathf.Clamp01(Vector3.Distance(from, to) / SagReach);
            float ripple = Mathf.Lerp(0.03f, 0.08f, _shown);
            for (int i = 0; i < Points; i++)
            {
                float t = i / (Points - 1f);
                Vector3 point = Vector3.Lerp(from, to, t);
                point.y += Mathf.Sin(Mathf.PI * t) * (ripple * Mathf.Sin(2f * Mathf.PI * (2f * t - _wave)) - sag);
                _points[i] = point;
            }
        }

        public void Hide()
        {
            if (_line != null && _line.enabled)
            {
                _line.enabled = false;
            }
        }

        public void Dispose()
        {
            if (_line != null)
            {
                Object.Destroy(_line.gameObject);
            }
        }

        private static bool Headless() => ZNet.instance != null && ZNet.instance.IsDedicated();
    }
}
