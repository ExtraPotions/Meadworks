// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateSurfaceView.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>One gate's surface on this client: a plain local GameObject (no ZNetView, no collider, nothing
    /// networked) at the gate, holding the sheet and the game's portal effect fitted to the span. Built once; after
    /// that only its intensity moves: up over <see cref="FadeSeconds"/> when the gate forms, to the dim level while
    /// the gate has no destination, down to nothing when the gate is gone, and then it is destroyed once the last
    /// particles have had time to die.</summary>
    internal sealed class SeaGateSurfaceView
    {
        internal const float FadeSeconds = 1f;
        private const float LingerSeconds = 3f;

        internal readonly long GateId;
        private readonly SeaGatePillar anchor;
        private readonly SeaGatePillar partner;
        private GameObject root;
        private SeaGateSurfaceSheet sheet;
        private float intensity;
        private float target;
        private float goneSince = -1f;

        private SeaGateSurfaceView(LoadedGate gate)
        {
            GateId = gate.Id;
            anchor = gate.Anchor;
            partner = gate.Partner;
        }

        /// <summary>True once it has faded out and lingered; the owner then calls <see cref="Destroy"/>.</summary>
        internal bool Finished { get; private set; }

        /// <summary>The surface for a gate, built under an inactive root and switched on at intensity 0. On failure
        /// nothing is left behind and the exception goes on to the caller.</summary>
        internal static SeaGateSurfaceView Create(LoadedGate gate)
        {
            SeaGateSurfaceView view = new SeaGateSurfaceView(gate);
            try
            {
                view.Build(new SeaGateSurfaceFrame(gate));
                return view;
            }
            catch
            {
                view.Destroy();
                throw;
            }
        }

        /// <summary>Whether this surface was built for the same two pillar objects (a pillar that unloaded and loaded
        /// again is a new object, and gets a new surface).</summary>
        internal bool Matches(LoadedGate gate) => gate.Anchor == anchor && gate.Partner == partner;

        internal void SetTarget(float value) => target = value;

        internal void Animate(float deltaTime, float time)
        {
            if (root == null)
            {
                Finished = true;
                return;
            }
            intensity = Mathf.MoveTowards(intensity, target, deltaTime / FadeSeconds);
            sheet?.Animate(time, intensity);
            if (intensity > 0f || target > 0f)
                goneSince = -1f;
            else if (goneSince < 0f)
                goneSince = time;
            else if (time - goneSince >= LingerSeconds)
                Finished = true;
        }

        internal void Destroy()
        {
            sheet?.Destroy();
            sheet = null;
            if (root != null)
                Object.Destroy(root);
            root = null;
        }

        private void Build(SeaGateSurfaceFrame frame)
        {
            root = new GameObject("Threshold_SeaGateSurface");
            root.SetActive(false);
            root.transform.SetPositionAndRotation(frame.Center, frame.Rotation);
            sheet = SeaGateSurfaceSheet.Build(root.transform, frame, underEffect: false);
            sheet?.Animate(0f, 0f);
            root.SetActive(true);
        }

    }
}
