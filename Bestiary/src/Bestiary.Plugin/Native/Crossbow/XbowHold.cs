// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// On every player, on every peer that draws (<see cref="PlayerHolds"/>): while the Bone Crossbow is in the left hand (as the player's equipment shows it,
    /// which the game syncs), the player's animator plays the Bone Crossbow's own reload in place of the game's Arbalest
    /// reload (AssetWorkshop Crossbow/XbowClips: the crossbowman's reload, lowering the crossbow, drawing the string into
    /// the nut, laying a bolt; timed so the game's reload state, at 1.4x, paced so the 2.3 s default ends it as the hand
    /// goes back to the carry, which the game's blend finishes; the Crossbows skill shortens the reload down to half,
    /// cutting more of the end), and a held carry in place of its "Reload done", 0.1 s instead of the game's 0.9 s
    /// (that state blocks shooting). The game's reload sounds come along: the game clips' events are copied onto ours.
    /// The game's controller comes back when the crossbow leaves the hand, unless another mod has put its own in.
    /// </summary>
    public sealed class XbowHold : MonoBehaviour
    {
        private const string GameReload = "Reload Crossbow", GameDone = "Reload done";
        private const string OurReload = "ecp_xbow_player_reload", OurDone = "ecp_xbow_player_reload_done";

        private static readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController?> made =
            new Dictionary<RuntimeAnimatorController, AnimatorOverrideController?>();
        private static AnimationClip? reload, done;
        private static int hash;

        private VisEquipment? equipment;
        private Animator? animator;
        private RuntimeAnimatorController? game;
        private AnimatorOverrideController? ours;

        /// <summary>The bundle's player clips, once loaded.</summary>
        public static void Use(AssetBundle bundle)
        {
            (reload, done) = (bundle.LoadAsset<AnimationClip>(OurReload), bundle.LoadAsset<AnimationClip>(OurDone));
            hash = XbowItem.PrefabName.GetStableHashCode();
            if (reload == null || done == null)
            {
                Log.Warn($"Bone Crossbow: the bundle has no {OurReload} or {OurDone}; players reload it as an Arbalest.");
            }
        }

        private void Awake()
        {
            equipment = GetComponent<VisEquipment>();
            animator = GetComponentInChildren<Animator>(true);
        }

        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();

        private void Restore()
        {
            if (game != null && animator != null && animator.runtimeAnimatorController == ours) AnimatorSwap.Swap(animator, game);
            (game, ours) = (null, null);
        }
        private void LateUpdate()
        {
            if (animator == null || animator.runtimeAnimatorController == null || hash == 0)
            {
                return;
            }
            bool holding = Adapters.NativeConfiguration.Active && equipment != null && equipment.m_currentLeftItemHash == hash;
            if (holding && game == null)
            {
                Take(animator);
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

        /// <summary>Our reload's controller in, over the game's (or over the game's under another of ours).</summary>
        private void Take(Animator animator)
        {
            RuntimeAnimatorController current = AnimatorSwap.Game(animator.runtimeAnimatorController);
            ours = For(current);
            if (ours != null)
            {
                game = current;
                AnimatorSwap.Swap(animator, ours);
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
            AnimationClip? gameReload = game.animationClips.FirstOrDefault(c => c != null && c.name == GameReload);
            AnimationClip? gameDone = game.animationClips.FirstOrDefault(c => c != null && c.name == GameDone);
            if (reload == null || done == null || gameReload == null || gameDone == null)
            {
                return null;
            }
            (reload.events, done.events) = (gameReload.events, gameDone.events);   // the game's reload sounds
            var ours = new AnimatorOverrideController(game) { name = "ecp_xbow_player" };
            (ours[gameReload], ours[gameDone]) = (reload, done);
            return ours;
        }
    }
}
