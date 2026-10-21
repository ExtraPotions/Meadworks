// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The moment a Blinking creature is gone. On every machine its body - every renderer under it, shadows and worn
    /// effects included - is hidden from the instant it vanishes until its synced position has landed at the
    /// destination. The owner moves it at once, but a client learns of the jump from the next position update, a moment
    /// after the blink message, and ZSyncTransform eases a jump under 5 m instead of snapping it; unveiled, a client
    /// would see it stand still after the puff and then slide across. It uses <c>forceRenderingOff</c>, a switch of its
    /// own, so it never fights Cloaked's per-frame renderer toggling, restores only what it switched, and lifts after
    /// <see cref="MaxHidden"/> whatever happens, so a lost update can never leave a creature invisible.
    /// </summary>
    internal sealed class BlinkVeil
    {
        /// <summary>Gone for at least this long, so the blink reads as a vanishing rather than a cut.</summary>
        private const float MinHidden = 0.15f;

        private const float MaxHidden = 1.5f;

        /// <summary>Caught up with its synced position once this close (a moving creature leads it a little).</summary>
        private const float CatchUp = 1f;

        private readonly List<Renderer> _hidden = new List<Renderer>();
        private Vector3 _origin;
        private Vector3 _dest;
        private float _since;

        public bool Active { get; private set; }

        /// <summary>Hides the body where it stands now, until it lands at <paramref name="dest"/>.</summary>
        public void Drop(GameObject body, Vector3 dest)
        {
            Lift();
            _origin = body.transform.position;
            _dest = dest;
            _since = 0f;
            Active = true;
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                if (renderer != null && !renderer.forceRenderingOff)
                {
                    renderer.forceRenderingOff = true;
                    _hidden.Add(renderer);
                }
            }
        }

        /// <summary>Every frame, every machine: lift the veil once the jump lands here, or after the cap.</summary>
        public void Tick(ZNetView view, Transform body, float dt)
        {
            if (!Active)
            {
                return;
            }
            _since += dt;
            if (_since >= MaxHidden || (_since >= MinHidden && Landed(view, body)))
            {
                Lift();
            }
        }

        // Landed: the synced position has made the jump (it is nearer the destination than the start), and the body has
        // caught up with it - at once where the jump snapped, after a few frames of easing where it was short.
        private bool Landed(ZNetView view, Transform body)
        {
            if (view == null || !view.IsValid())
            {
                return true;
            }
            Vector3 synced = view.GetZDO().GetPosition();
            bool jumped = Vector3.Distance(synced, _origin) >= Vector3.Distance(_dest, _origin) * 0.5f;
            return jumped && Vector3.Distance(body.position, synced) <= CatchUp;
        }

        public void Lift()
        {
            foreach (Renderer renderer in _hidden)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = false;
                }
            }
            _hidden.Clear();
            Active = false;
        }
    }
}
