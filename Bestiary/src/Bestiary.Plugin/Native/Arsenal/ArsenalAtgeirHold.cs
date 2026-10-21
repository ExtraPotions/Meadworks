// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// On every player, on every peer that draws (<see cref="PlayerHolds"/>): while the Bone Atgeir is in the right hand (as the player's equipment shows it, which
    /// the game syncs), the player's animator plays the Bone Atgeir's own three attacks in place of the game's atgeir
    /// attacks (AssetWorkshop SkelArsenal/AtgeirAuthor: the game's moves with the left fist on this haft throughout, each
    /// ending in the stance, the game's lengths and markers), and the left fist is put on the haft after each pose
    /// (<see cref="HaftGrip"/>). The bone atgeir lies in
    /// the fist exactly as the game's bronze atgeir does, but the game's atgeir clips leave the left fist off that haft:
    /// about 0.4 m in the stance and 0.07 to 0.19 m through the combo (AssetWorkshop SkelArsenal/ArsenalAtgeirProbe).
    /// The haft is a straight line from the right fist (the model's origin, its attach frame) up towards the blade.
    /// </summary>
    public sealed class ArsenalAtgeirHold : MonoBehaviour
    {
        /// <summary>Up the haft in the attach frame (the model's longest spread, measured in the probe), and the stretch held.</summary>
        private static readonly Vector3 Up = new Vector3(0.339f, -0.143f, 0.930f).normalized;
        private static readonly Haft Haft = new Haft(h => Up * h, 0.2f, 1.3f);

        private const string Item = "ECP_BoneAtgeir";   // ArsenalWeapon "Atgeir"'s item

        private static int hash;

        private static readonly (string game, string ours)[] Attacks =
        {
            ("2Hand-Spear-Attack1", "2Hand-Spear-Attack1"), ("2Hand-Spear-Attack9", "2Hand-Spear-Attack9"),
            ("2Hand-Spear-Attack3", "2Hand-Spear-Attack3"),
        };
        private static readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController?> made =
            new Dictionary<RuntimeAnimatorController, AnimatorOverrideController?>();
        private static AnimationClip?[] clips = new AnimationClip?[0];

        private VisEquipment? equipment;
        private HaftGrip? grip;
        private Animator? animator;
        private RuntimeAnimatorController? game;
        private AnimatorOverrideController? ours;

        /// <summary>The bundle's attack clips, once loaded.</summary>
        public static void Use(AssetBundle bundle)
        {
            var player = ZNetScene.instance?.GetPrefab("Player")?.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController;
            clips = Attacks.Select(a => player?.animationClips.FirstOrDefault(c => c.name == a.game)).ToArray();
            if (clips.Any(c => c == null))
            {
                Log.Warn("Bone Atgeir: live player attack clips are missing; players retain the game's controller.");
            }
        }

        private void Awake()
        {
            equipment = GetComponent<VisEquipment>();
            grip = HaftGrip.Of(transform.Find("Visual") ?? transform);
            animator = GetComponentInChildren<Animator>(true);
            hash = hash != 0 ? hash : Item.GetStableHashCode();
        }

        private void OnDisable() => Animate(false);
        private void OnDestroy() => Animate(false);

        private void LateUpdate()
        {
            GameObject? held = Adapters.NativeConfiguration.Active && equipment != null && equipment.m_currentRightItemHash == hash ? equipment.m_rightItemInstance : null;
            Animate(held != null);
            if (held != null && grip != null && held.activeInHierarchy)
            {
                grip.Apply(held.transform, Haft);
            }
        }
        /// <summary>Our attacks' controller in while the atgeir is held, the game's back after.</summary>
        private void Animate(bool holding)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }
            if (holding && game == null)
            {
                RuntimeAnimatorController current = AnimatorSwap.Game(animator.runtimeAnimatorController);
                ours = For(current);
                if (ours != null)
                {
                    game = current;
                    AnimatorSwap.Swap(animator, ours);
                }
            }
            else if (!holding && game != null)
            {
                if (animator.runtimeAnimatorController == ours)
                {
                    AnimatorSwap.Swap(animator, game);
                }
                (game, ours) = (null, null);
            }
        }

        private static AnimatorOverrideController? For(RuntimeAnimatorController game)
        {
            if (!made.TryGetValue(game, out AnimatorOverrideController? ours))
            {
                ours = made[game] = Make(game);
            }
            return ours;
        }

        private static AnimatorOverrideController? Make(RuntimeAnimatorController game)
        {
            AnimationClip?[] slots = Attacks.Select(a => game.animationClips.FirstOrDefault(c => c != null && c.name == a.game)).ToArray();
            if (clips.Length != Attacks.Length || clips.Any(c => c == null) || slots.Any(c => c == null))
            {
                return null;
            }
            var ours = new AnimatorOverrideController(game) { name = "ecp_atgeir_player" };
            for (int i = 0; i < slots.Length; i++)
            {
                ours[slots[i]!] = clips[i]!;
            }
            return ours;
        }
    }
}
