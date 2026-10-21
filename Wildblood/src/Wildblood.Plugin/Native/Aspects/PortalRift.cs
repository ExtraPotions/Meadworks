// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One Portalbound portal, drawn locally on each client: the wooden portal's swirl inside a glowing rim
    /// (<see cref="PortalEffects"/>), facing where the throw goes. It opens by growing from nothing and closes by
    /// shrinking back to nothing, its swirl dimming as it goes, then removes itself; it lives apart from the boss, so a
    /// boss that dies or unloads mid-throw leaves its portals to close on their own rather than vanish. The far portal
    /// hangs where it was put and turns to face the target's latest place; the one at the hand rides the boss's hand,
    /// a little out from the palm, facing the way the boss faces, for as long as the hand is there.
    /// </summary>
    internal sealed class PortalRift : MonoBehaviour
    {
        /// <summary>The rim's radius before sizing: the wooden portal's ring of flames.</summary>
        private const float RimRadius = 1.3f;

        private const float GrowTime = 0.35f;
        private const float ShrinkTime = 0.5f;

        /// <summary>Degrees a second the far portal turns toward a new facing.</summary>
        private const float TurnSpeed = 240f;

        /// <summary>How far out from the palm the portal at the hand opens.</summary>
        private const float HandReach = 0.6f;

        /// <summary>The scale a portal starts and ends at: small enough to be unseen, never zero.</summary>
        private const float Pinpoint = 0.01f;

        private static readonly Color Glow = new Color(1.00f, 0.55f, 0.15f, 1f);

        private EffectFade? _fade;
        private Transform? _hand;
        private Transform? _body;
        private Quaternion _facing;
        private float _size;
        private float _born;
        private float _closedAt = -1f;

        /// <summary>A portal opening at <paramref name="center"/>, facing along <paramref name="facing"/>.</summary>
        public static PortalRift Open(Vector3 center, Vector3 facing, float size)
        {
            GameObject holder = new GameObject("ecr_portal");
            holder.transform.SetPositionAndRotation(center, Look(facing, Quaternion.identity));
            PortalRift rift = holder.AddComponent<PortalRift>();
            rift.Build(size);
            return rift;
        }

        /// <summary>A portal opening at a boss's hand and riding it, facing the way the boss faces.</summary>
        public static PortalRift OnHand(Transform hand, Transform body, float size)
        {
            PortalRift rift = Open(hand.position + body.forward * HandReach, body.forward, size);
            rift._hand = hand;
            rift._body = body;
            return rift;
        }

        private void Build(float size)
        {
            _size = Mathf.Max(Pinpoint, size);
            _born = Time.time;
            _facing = transform.rotation;
            PortalEffects.Rim(gameObject, RimRadius, Glow);
            _fade = PortalEffects.Swirl(transform);
            transform.localScale = Vector3.one * Pinpoint; // built at full size, then grown open from a point
        }

        /// <summary>The far portal turns, smoothly, to face along <paramref name="facing"/>.</summary>
        public void Face(Vector3 facing) => _facing = Look(facing, _facing);

        private void LateUpdate() => Guard.Run("PortalRift.LateUpdate", static self => self.Step(), this);

        // After the animation has posed the hand: follow it, turn, and grow or shrink.
        private void Step()
        {
            Place();
            float open = _closedAt < 0f
                ? Mathf.Clamp01((Time.time - _born) / GrowTime)
                : 1f - Mathf.Clamp01((Time.time - _closedAt) / ShrinkTime);
            transform.localScale = Vector3.one * Mathf.Max(Pinpoint, open * _size);
            if (_closedAt >= 0f && Time.time - _closedAt >= ShrinkTime)
            {
                Destroy(gameObject);
            }
        }

        private void Place()
        {
            if (_hand != null && _body != null)
            {
                _facing = Look(_body.forward, _facing);
                transform.SetPositionAndRotation(_hand.position + _body.forward * HandReach, _facing);
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _facing, TurnSpeed * Time.deltaTime);
        }

        /// <summary>The portal closes: its swirl dims and stops, and it shrinks away. Closing twice changes nothing.</summary>
        public void Close()
        {
            if (_closedAt >= 0f)
            {
                return;
            }
            _closedAt = Time.time;
            if (_fade != null)
            {
                _fade.SetActive(false);
            }
        }

        private static Quaternion Look(Vector3 facing, Quaternion fallback) =>
            facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing.normalized) : fallback;
    }
}
