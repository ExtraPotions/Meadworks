// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Milestones/ShieldWall.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Shield Wall, the level 50 milestone, worked out on the sheltered player's own client, where their damage is
    /// taken (<see cref="DamageIntake"/>): the local player takes Shield Wall Reduction percent less damage while
    /// another player
    /// <list type="bullet">
    /// <item>is blocking (Character.IsBlocking reads the blocker's ZDO on other machines);</item>
    /// <item>with a shield in the left hand (the item their VisEquipment shows, read from their ZDO);</item>
    /// <item>has reached Shield Wall Level in Defense (their published level, <see cref="CustomSkillLevels"/>);</item>
    /// <item>and stands within Shield Wall Radius in front of the local player: the local player is behind them,
    /// measured flat against the way the blocker faces.</item>
    /// </list>
    /// Several blockers do not add up. The sheltered player sees a Shield Wall icon (<see cref="DefenseEffects"/>).
    /// </summary>
    public static class ShieldWall
    {
        /// <summary>The reduction the local player gets from a shelter now, 0 without one.</summary>
        public static float Reduction(Player player) =>
            Sheltered(player) ? Mathf.Clamp01(DefenseMilestoneSettings.ShieldWallReduction.Value / 100f) : 0f;

        public static bool Sheltered(Player player)
        {
            if (!DefenseSkill.Active || player == null)
                return false;
            foreach (Player other in Player.GetAllPlayers())
            {
                if (other != player && Shelters(other, player))
                    return true;
            }
            return false;
        }

        private static bool Shelters(Player blocker, Player sheltered)
        {
            if (blocker == null || blocker.IsDead() || !blocker.IsBlocking() || !HoldsShield(blocker))
                return false;
            if (!DefenseSkill.Reached(DefenseSkill.Of(blocker), DefenseMilestoneSettings.ShieldWallLevel.Value))
                return false;
            Vector3 offset = sheltered.transform.position - blocker.transform.position;
            offset.y = 0f;
            float radius = DefenseMilestoneSettings.ShieldWallRadius.Value;
            return offset.sqrMagnitude <= radius * radius && Vector3.Dot(blocker.transform.forward, offset) < 0f;
        }

        /// <summary>The left-hand item the player's visible equipment shows is a shield.</summary>
        private static bool HoldsShield(Player player)
        {
            VisEquipment equipment = player.m_visEquipment;
            if (equipment == null || equipment.m_currentLeftItemHash == 0 || ObjectDB.instance == null)
                return false;
            GameObject prefab = ObjectDB.instance.GetItemPrefab(equipment.m_currentLeftItemHash);
            ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return item != null && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;
        }
    }
}
