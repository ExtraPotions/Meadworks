// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// (Ballista/BallistaLook.cs).
// Changed by expDARE, 2026: Bestiary namespace; draws the game turret's own model (its body and neck turned the turret's
// way, its armed and unarmed bodies, its bone missile visual) with the turret's own effects; no arms, string or hands.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Prefabs;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Bestiary.Runtime.Native.Ballista
{
    /// <summary>
    /// Draws a Bone Ballista on every peer that draws (never a dedicated server), from its ZDO alone, sending nothing:
    /// the turn (the holder's own aim at once, everyone else's eased toward the last one sent), the string drawn or slack
    /// (the turret's armed and unarmed bodies), the missile laid in the groove, the kick of a shot and the game turret's
    /// sounds: its shot, its reload as the latch catches, its added ammo as a missile is laid.
    /// </summary>
    public sealed class BallistaLook : MonoBehaviour
    {
        /// <summary>The game ballista's own effects, taken from its Turret when the piece is built (<see cref="BallistaPiece"/>).</summary>
        public static EffectList? Fire, Latch, Laid;

        private const float Kick = 3f, KickTime = 0.08f, Follow = 12f;

        private BallistaControl control = null!;
        private float yaw, pitch;
        private int shots = -1;
        private bool drawn, laid;

        private void Awake()
        {
            control = GetComponent<BallistaControl>();
            enabled = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        }

        /// <summary>A placement ghost (no ZDO) rests armed and empty, and is left still.</summary>
        private void Start()
        {
            ZNetView view = GetComponent<ZNetView>();
            if (view != null && view.GetZDO() != null)
            {
                return;
            }
            if (control.Parts != null)
            {
                Show(control.Parts, true, false);
            }
            enabled = false;
        }

        private void LateUpdate()
        {
            BallistaParts? parts = control.Parts;
            if (parts == null || control.Net == null || !control.Net.IsValid())
            {
                return;
            }
            BallistaState s = control.State;
            Turn(parts, s);
            bool nowDrawn = BallistaTimeline.Drawn(s), nowLaid = BallistaTimeline.MissileLaid(s);
            if (shots >= 0)
            {
                if (s.Shots > shots)
                {
                    Fire?.Create(parts.Eye.position, parts.Eye.rotation);
                }
                if (nowDrawn && !drawn)
                {
                    Latch?.Create(transform.position, transform.rotation);
                }
                if (nowLaid && !laid)
                {
                    Laid?.Create(transform.position, transform.rotation);
                }
            }
            Show(parts, nowDrawn, nowLaid);
            (shots, drawn, laid) = (s.Shots, nowDrawn, nowLaid);
        }

        /// <summary>The holder's own aim, worked out here each frame; anyone else's eased toward the ZDO's; a shot kicks the nose up.</summary>
        private void Turn(BallistaParts parts, in BallistaState s)
        {
            if (Player.m_localPlayer != null && control.HeldBy(Player.m_localPlayer))
            {
                BallistaOperator.Aim(control, Player.m_localPlayer, Time.deltaTime);
                (yaw, pitch) = (control.Yaw, control.Pitch);
            }
            else
            {
                float ease = 1f - Mathf.Exp(-Follow * Time.deltaTime);
                (yaw, pitch) = (Mathf.LerpAngle(yaw, s.Yaw, ease), Mathf.LerpAngle(pitch, s.Pitch, ease));
            }
            float kick = s.ShotAge < 1f ? Kick * Mathf.Exp(-s.ShotAge / KickTime) : 0f;
            parts.Point(transform, yaw, pitch + kick);
        }

        private static void Show(BallistaParts parts, bool drawn, bool laid)
        {
            if (parts.Armed != null && parts.Armed.activeSelf != drawn)
            {
                parts.Armed.SetActive(drawn);
            }
            if (parts.Unarmed != null && parts.Unarmed.activeSelf == drawn)
            {
                parts.Unarmed.SetActive(!drawn);
            }
            if (parts.Laid != null && parts.Laid.activeSelf != laid)
            {
                parts.Laid.SetActive(laid);
            }
        }
    }
}
