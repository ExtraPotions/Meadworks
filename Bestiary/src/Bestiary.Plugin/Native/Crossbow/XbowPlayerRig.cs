// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// The Bone Crossbow's string and bolts in a player's hands, on every peer: the string's middle lies at its rest once
    /// the crossbow is shot, comes onto the right fingers as they hook it in the reload and rides them back into the nut;
    /// a bolt is in the right fist from the reach to the hip until it is laid, then in the groove until the next shot.
    /// Timed by the reload clip's own time in the player's animator (<see cref="XbowHold"/> puts the Bone Crossbow's reload,
    /// AssetWorkshop Crossbow/XbowClips PlayerReloadKeys, in the game's reload state) and by the game's own loaded flag,
    /// which it syncs and clears as the shot leaves (the game's bolt projectile flies from there), so it needs no network
    /// of its own. At the shot the string snaps to its rest and shivers there, as the crossbowmen's does (XbowRig). Each
    /// half of the string runs from its prod tip to the middle. The right hand is kept on the crossbow first
    /// (<see cref="XbowPlayerHand"/>), so the string and the bolt follow the hand that works them.
    /// </summary>
    public sealed class XbowPlayerRig : MonoBehaviour
    {
        public const string Slot = "ecp_xbow_rig", GrooveBolt = "ecp_xbow_groove_bolt";
        private const string ReloadClip = "ecp_xbow_player_reload";
        // The player reload clip's own seconds (XbowClips PlayerTime: the crossbowman's reload moved 0.72 s earlier and
        // paced by XbowClips.PlayerPace for the 2.3 s reload).
        private const float StringGrab = 0.7077f, Spanned = 1.2385f, BoltGrab = 1.8046f, Lay = 2.5477f, Hook = 0.0885f;

        private static GameObject? boltLook;
        private static Material? skin;
        private static int hash;

        private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>();
        private Player? player;
        private VisEquipment? equipment;
        private Animator? animator;
        private Transform? rightHand, tipA, tipB, rest, nut, halfA, halfB;
        private GameObject? held, groove, handBolt;
        private XbowPlayerHand? fist;
        private bool wasLoaded;
        private float shotAt = -10f;

        /// <summary>The bolt model for the right fist, once the bundle is loaded.</summary>
        public static void Use(GameObject look, Material material) =>
            (boltLook, skin, hash) = (look, material, XbowItem.PrefabName.GetStableHashCode());

        private void Awake()
        {
            (player, equipment, animator) = (GetComponent<Player>(), GetComponent<VisEquipment>(), GetComponentInChildren<Animator>(true));
            rightHand = GameMaterials.Find(transform, "RightHand_Attach");
            fist = XbowPlayerHand.Of(transform.Find("Visual") ?? transform);
        }

        private void LateUpdate()
        {
            GameObject? instance = Adapters.NativeConfiguration.Active && equipment != null && hash != 0 && equipment.m_currentLeftItemHash == hash ? equipment.m_leftItemInstance : null;
            if (instance != held)
            {
                Bind(instance);
            }
            if (held == null || tipA == null || player == null)
            {
                Fist(false);
                return;
            }
            float time = ReloadTime();
            bool loaded = Loaded(player, time);
            fist?.Apply(tipA.parent, rest!.position, nut!.position, time);
            Vector3 middle = Middle(time, loaded);
            Stretch(halfA!, tipA, middle);
            Stretch(halfB!, tipB!, middle);
            groove?.SetActive(time >= 0f ? time >= Lay : loaded);
            Fist(time >= BoltGrab && time < Lay);
            Drop(time);
        }

        private void OnDisable() { Fist(false); Bind(null); }
        private void OnDestroy() { if (handBolt != null) Destroy(handBolt); }

        /// <summary>
        /// The bolt in the fist swinging from the fingers into the groove as the hand comes down over it, so at the lay it
        /// lies exactly where the groove bolt shows (the hand keeps its own turn).
        /// </summary>
        private void Drop(float time)
        {
            if (handBolt == null || groove == null || rightHand == null || !handBolt.activeSelf)
            {
                return;
            }
            float laid = XbowPlayerHand.Laid(time);
            Vector3 held = rightHand.TransformPoint(new Vector3(0f, 0f, -0.12f));
            handBolt.transform.SetPositionAndRotation(Vector3.Lerp(held, groove.transform.position, laid),
                Quaternion.Slerp(rightHand.rotation, groove.transform.rotation, laid));
        }

        /// <summary>The game's loaded flag; its fall outside a reload is the shot, which the string's shiver is timed from.</summary>
        private bool Loaded(Player owner, float time)
        {
            bool loaded = owner.IsWeaponLoaded();
            if (wasLoaded && !loaded && time < 0f)
            {
                shotAt = Time.time;
            }
            wasLoaded = loaded;
            return loaded;
        }

        private void Bind(GameObject? instance)
        {
            held = instance;
            Transform? rig = instance != null ? GameMaterials.Find(instance.transform, Slot) : null;
            Transform? Part(string name) => rig != null ? GameMaterials.Find(rig, name) : null;
            (tipA, tipB, rest, nut) = (Part("ecp_xbow_tip_a"), Part("ecp_xbow_tip_b"), Part("ecp_xbow_rest"), Part("ecp_xbow_nut"));
            (halfA, halfB, groove) = (Part("ecp_xbow_string_a"), Part("ecp_xbow_string_b"), Part(GrooveBolt)?.gameObject);
            if (tipA == null || tipB == null || rest == null || nut == null || halfA == null || halfB == null)
            {
                (held, tipA) = (null, null);
            }
        }

        /// <summary>Seconds into the Bone Crossbow's reload clip while it plays (on any layer), else -1.</summary>
        private float ReloadTime()
        {
            for (int layer = 0; animator != null && layer < animator.layerCount; layer++)
            {
                animator.GetCurrentAnimatorClipInfo(layer, clips);
                if (clips.Count > 0 && clips[0].clip != null && clips[0].clip.name == ReloadClip)
                {
                    return Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(layer).normalizedTime) * clips[0].clip.length;
                }
            }
            return -1f;
        }

        /// <summary>Where the string's middle is: at rest, coming onto the fingers, riding them, or in the nut.</summary>
        private Vector3 Middle(float time, bool loaded)
        {
            if (time < 0f)
            {
                return loaded ? nut!.position : Shiver(Time.time - shotAt);
            }
            if (time < StringGrab - Hook || rightHand == null)
            {
                return time >= Spanned ? nut!.position : rest!.position;
            }
            if (time < StringGrab)
            {
                return Vector3.Lerp(rest!.position, rightHand.position, (time - (StringGrab - Hook)) / Hook);
            }
            return time < Spanned ? rightHand.position : nut!.position;
        }

        /// <summary>The string's middle just after the shot: snapped to its rest, shivering along the stock and settling.</summary>
        private Vector3 Shiver(float since)
        {
            Transform bow = rest!.parent;
            return bow.TransformPoint(rest.localPosition + Vector3.forward * (0.03f * Mathf.Exp(-14f * since) * Mathf.Cos(70f * since)));
        }

        /// <summary>A half of the string, the one-metre cord in the crossbow's own frame, from its tip to the middle.</summary>
        private static void Stretch(Transform half, Transform tip, Vector3 middle)
        {
            Transform bow = half.parent;
            Vector3 from = tip.localPosition, to = bow.InverseTransformPoint(middle);
            half.localPosition = from;
            half.localRotation = Quaternion.LookRotation(to - from, Vector3.up);
            half.localScale = new Vector3(1f, 1f, (to - from).magnitude);
        }

        /// <summary>The bolt in the right fist, held a fifth of the way up from its nock, head forward.</summary>
        private void Fist(bool shown)
        {
            if (handBolt == null && shown && rightHand != null && boltLook != null && skin != null)
            {
                handBolt = Instantiate(boltLook, rightHand, false);
                handBolt.name = "ecp_xbow_hand_bolt";
                handBolt.transform.localPosition = new Vector3(0f, 0f, -0.12f);
                XbowKit.Dress(handBolt, skin);
            }
            handBolt?.SetActive(shown);
        }
    }
}
