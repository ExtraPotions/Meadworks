// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// A Cloning creature's body while it hides behind its decoy, on every machine with a screen: every renderer under it
    /// - its mesh, its gear, its shadow, any effect it wears - is off, and its lights light nothing, until it shows again.
    /// Renderers go off through <c>forceRenderingOff</c>, a switch of their own (as Blinking's veil uses), so this never
    /// fights Cloaked's per-frame toggling, and only what it switched is switched back. A light is culled from every layer
    /// rather than disabled, because the game's own light scripts switch lights back on. While hidden it looks again every
    /// frame, into reused lists, for parts added or handed back since (a weapon drawn, an effect attached, Blinking's veil
    /// lifting), so nothing new gives it away.
    /// </summary>
    internal sealed class CloneVeil
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<int> _masks = new List<int>();
        private readonly List<Renderer> _foundRenderers = new List<Renderer>();
        private readonly List<Light> _foundLights = new List<Light>();

        /// <summary>True while the body is hidden on this machine.</summary>
        public bool Active { get; private set; }

        /// <summary>One frame: hide or show as <paramref name="hide"/> says; true on the frame the body shows again.</summary>
        public bool Tick(GameObject body, bool hide)
        {
            if (hide)
            {
                Active = true;
                HideRenderers(body);
                HideLights(body);
                return false;
            }
            if (!Active)
            {
                return false;
            }
            Lift();
            return true;
        }

        private void HideRenderers(GameObject body)
        {
            body.GetComponentsInChildren(true, _foundRenderers);
            foreach (Renderer renderer in _foundRenderers)
            {
                if (renderer != null && !renderer.forceRenderingOff)
                {
                    renderer.forceRenderingOff = true;
                    _renderers.Add(renderer);
                }
            }
        }

        private void HideLights(GameObject body)
        {
            body.GetComponentsInChildren(true, _foundLights);
            foreach (Light light in _foundLights)
            {
                if (light != null && light.cullingMask != 0 && !_lights.Contains(light))
                {
                    _lights.Add(light);
                    _masks.Add(light.cullingMask);
                    light.cullingMask = 0;
                }
            }
        }

        /// <summary>Everything it switched, switched back.</summary>
        public void Lift()
        {
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = false;
                }
            }
            for (int i = 0; i < _lights.Count; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].cullingMask = _masks[i];
                }
            }
            _renderers.Clear();
            _lights.Clear();
            _masks.Clear();
            Active = false;
        }
    }
}
