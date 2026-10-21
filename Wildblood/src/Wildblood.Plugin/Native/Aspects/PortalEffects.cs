// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The vanilla pieces a Portalbound portal is drawn and heard with. Its heart is the wooden portal's own swirl - the
    /// one a linked portal shows when a player comes near: a ring of blue flames 2.6 m across around a dark centre that
    /// draws sparks in, with its orange light and its hum. That swirl is a child of the portal piece rather than an effect
    /// of its own, so it is taken from the piece's prefab and cloned locally through <see cref="CosmeticClone"/> (the
    /// player's effect density applies), centred on the portal, and set to follow the portal's scale so it can open and
    /// close by growing and shrinking. Around it a thin glowing rim, drawn whatever the effect density because it is
    /// the warning itself: it borrows the Stormbound circle's soft band (<see cref="StormEffects.RingMaterial"/>),
    /// tinted the portal's orange. A portal opens with the game's portal-opening sound. The aspects block of the rule
    /// file holds numbers only, so these names are code constants rather than rule fields.
    /// </summary>
    internal static class PortalEffects
    {
        public const string PortalPiece = "portal_wood";
        public const string OpenSound = "sfx_OpenPortal";

        private const int RimSegments = 40;
        private const float RimWidth = 0.12f;

        private static readonly string[] OpenKeywords = { "portal", "teleport" };

        private static GameObject? _swirl;
        private static Vector3 _swirlCentre;
        private static bool _warned;

        /// <summary>The wooden portal's swirl under <paramref name="holder"/>, centred on it and facing with it; its
        /// fade, to close it with, or null when the density is 0 or the swirl is missing.</summary>
        public static EffectFade? Swirl(Transform holder)
        {
            GameObject? clone = CosmeticClone.Spawn(Source(), holder, holder.position, endless: true);
            if (clone == null)
            {
                return null;
            }
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            clone.transform.localPosition = -_swirlCentre; // the swirl's heart on the holder, not the piece's foot
            foreach (ParticleSystem system in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            EffectFade fade = clone.GetComponent<EffectFade>();
            if (fade != null)
            {
                fade.SetActive(true); // the swirl wakes switched off, as on a portal no one stands at
            }
            return fade;
        }

        // The swirl on the wooden portal piece, and where its heart sits within it (its first particle system).
        private static GameObject? Source()
        {
            if (_swirl != null)
            {
                return _swirl;
            }
            GameObject? piece = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(PortalPiece) : null;
            TeleportWorld? portal = piece != null ? piece.GetComponent<TeleportWorld>() : null;
            if (portal == null || portal.m_target_found == null)
            {
                Warn();
                return null;
            }
            _swirl = portal.m_target_found.gameObject;
            ParticleSystem heart = _swirl.GetComponentInChildren<ParticleSystem>(true);
            _swirlCentre = heart != null ? _swirl.transform.InverseTransformPoint(heart.transform.position) : Vector3.zero;
            return _swirl;
        }

        private static void Warn()
        {
            if (!_warned)
            {
                _warned = true;
                Log.Warn($"the {PortalPiece} swirl was not found; Portalbound portals are drawn as rims only");
            }
        }

        /// <summary>The rim, a circle of <paramref name="radius"/> in the holder's own plane; none without its material.</summary>
        public static void Rim(GameObject holder, float radius, Color color)
        {
            Material? material = StormEffects.RingMaterial();
            if (material == null)
            {
                return;
            }
            LineRenderer line = holder.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false; // the rim turns and grows with the portal
            line.loop = true;
            line.positionCount = RimSegments;
            line.SetPositions(Circle(radius));
            line.widthMultiplier = RimWidth;
            line.startColor = color;
            line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        // A circle across the holder's face: its local X and Y, the plane the wooden portal's swirl turns in.
        private static Vector3[] Circle(float radius)
        {
            Vector3[] points = new Vector3[RimSegments];
            for (int i = 0; i < RimSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / RimSegments;
                points[i] = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * radius;
            }
            return points;
        }

        /// <summary>A portal opening here; heard whatever the effect density, which governs what is seen.</summary>
        public static void Opened(Vector3 at) =>
            CosmeticClone.Sound(EffectResolver.ResolveSound(OpenSound, OpenKeywords, "Portalbound portal"), at);
    }
}
