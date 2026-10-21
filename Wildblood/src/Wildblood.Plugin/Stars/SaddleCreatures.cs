// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0, adapted from CreatureLevelManager.cs (IsSaddleableCreature)
// at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: Wildblood namespace.
// SPDX-License-Identifier: GPL-3.0-only
using UnityEngine;

namespace ExtraPotions.Wildblood.Runtime
{
    internal static class SaddleCreatures
    {
        /// <summary>A Tameable with a saddle item, or a component named Saddle.</summary>
        internal static bool IsSaddleable(Character character)
        {
            Tameable tameable = character.GetComponent<Tameable>();
            if (tameable != null && tameable.m_saddleItem != null)
            {
                return true;
            }

            foreach (Component component in character.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "Saddle")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
