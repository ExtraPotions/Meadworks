// Portions Copyright (C) 2026 Gerald Glasgow.
// EliteCreaturesPack 0.7.2 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: EliteCreaturesPack/EliteCreaturesPack/Core/AnimatorSwap.cs
// Changed by expDARE, 2026: namespace isolation for Bestiary; reference algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Adopted.Core
{
    /// <summary>
    /// Puts a player's animator onto another controller (an override of the game's, while a weapon of ours is in hand)
    /// without a jump: each layer stays in the state and at the time it was, every parameter as it was.
    /// </summary>
    public static class AnimatorSwap
    {
        /// <summary>The game's controller under one of this mod's overrides (named ecp_*), else the controller itself.</summary>
        public static RuntimeAnimatorController Game(RuntimeAnimatorController controller) =>
            controller is AnimatorOverrideController ours && ours.name.StartsWith("ecp_") && ours.runtimeAnimatorController != null
                ? ours.runtimeAnimatorController : controller;

        /// <summary>The animator onto `controller`, each layer in the state and at the time it was, every parameter as it was.</summary>
        public static void Swap(Animator animator, RuntimeAnimatorController controller)
        {
            AnimatorStateInfo[] states = Enumerable.Range(0, animator.layerCount).Select(animator.GetCurrentAnimatorStateInfo).ToArray();
            (AnimatorControllerParameter, float)[] values = animator.parameters.Select(p => (p, Read(animator, p))).ToArray();
            animator.runtimeAnimatorController = controller;
            foreach (var (parameter, value) in values)
            {
                Write(animator, parameter, value);
            }
            for (int layer = 0; layer < states.Length && layer < animator.layerCount; layer++)
            {
                animator.Play(states[layer].fullPathHash, layer, states[layer].normalizedTime);
            }
        }

        private static float Read(Animator animator, AnimatorControllerParameter p) => p.type switch
        {
            AnimatorControllerParameterType.Float => animator.GetFloat(p.nameHash),
            AnimatorControllerParameterType.Int => animator.GetInteger(p.nameHash),
            AnimatorControllerParameterType.Bool => animator.GetBool(p.nameHash) ? 1f : 0f,
            _ => 0f,
        };

        private static void Write(Animator animator, AnimatorControllerParameter p, float value)
        {
            switch (p.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(p.nameHash, value);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(p.nameHash, (int)value);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(p.nameHash, value > 0.5f);
                    break;
            }
        }
    }
}
