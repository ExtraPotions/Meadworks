// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// On every player, on every peer that draws (<see cref="PlayerHolds"/>): while the Executioner's Greataxe is in the right hand (as the player's equipment
    /// shows it, which the game syncs), the player's animator plays the greataxe's combo (<see cref="GreataxeAnimations"/>)
    /// and, after each pose, the left fist is put on the haft (<see cref="HaftGrip"/>). The game's controller comes
    /// back when the greataxe leaves the hand, unless another mod has put its own in meanwhile.
    /// </summary>
    public sealed class GreataxeHold : MonoBehaviour
    {
        private const string ModelName = "ecp_greataxe";

        /// <summary>From against the right fist (a fist is about 0.08 wide at this scale) to the head's start, up the model.</summary>
        private static readonly Haft Haft = new Haft(HeadsmanAxe.Spine, 0.16f, 0.8f);

        private VisEquipment? equipment;
        private Animator? animator;
        private HaftGrip? grip;
        private RuntimeAnimatorController? game;
        private AnimatorOverrideController? ours;
        private GameObject? held;
        private Transform? axe;

        private void Awake()
        {
            equipment = GetComponent<VisEquipment>();
            animator = GetComponentInChildren<Animator>(true);
            Transform visual = transform.Find("Visual") ?? transform;
            grip = HaftGrip.Of(visual);
        }

        private void OnDisable() => Animate(false);
        private void OnDestroy() => Animate(false);

        private void LateUpdate()
        {
            bool holding = Adapters.NativeConfiguration.Active && equipment != null && equipment.m_currentRightItemHash == GreataxeItems.Hash;
            Animate(holding);
            if (holding)
            {
                Hold();
            }
        }

        /// <summary>The greataxe's controller in while it is held, the game's back after.</summary>
        private void Animate(bool holding)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }
            if (holding && game == null)
            {
                RuntimeAnimatorController current = AnimatorSwap.Game(animator.runtimeAnimatorController);
                ours = GreataxeAnimations.For(current);
                if (ours != null)
                {
                    game = current;
                    GreataxeAnimations.Swap(animator, ours);
                }
            }
            else if (!holding && game != null)
            {
                if (animator.runtimeAnimatorController == ours)
                {
                    GreataxeAnimations.Swap(animator, game);
                }
                (game, ours) = (null, null);
            }
        }

        private void Hold()
        {
            GameObject? instance = equipment!.m_rightItemInstance;
            if (instance != held)
            {
                held = instance;
                axe = instance != null ? instance.transform.Find(ModelName) : null;
            }
            if (axe != null && grip != null && axe.gameObject.activeInHierarchy)
            {
                grip.Apply(axe, Haft);
            }
        }
    }
}
