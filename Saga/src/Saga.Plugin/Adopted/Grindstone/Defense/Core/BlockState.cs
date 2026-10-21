// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Core/BlockState.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// What <see cref="BlockHooks"/> knows about one block before the game resolves it: whether it parries (the game's
    /// test, with the parry window widened), whether the blocker is a shield, the blocker's durability (for the wear
    /// refund) and the blockable damage (for Thorns), and the block timer the window changed, to put back afterwards.
    /// </summary>
    public sealed class BlockState
    {
        /// <summary>The game's parry window: a constant inside Humanoid.BlockAttack.</summary>
        public const float GameParryWindow = 0.25f;

        private float timerBefore;
        private bool timerChanged;
        private float durabilityBefore;
        private ItemDrop.ItemData blocker;

        public bool Parry { get; private set; }
        public bool Shield { get; private set; }
        public float BlockableBefore { get; private set; }

        /// <summary>Reads the block before BlockAttack runs; null when the player has nothing to block with.</summary>
        public static BlockState Before(Humanoid player, HitData hit)
        {
            ItemDrop.ItemData blocker = player.GetCurrentBlocker();
            if (blocker == null)
                return null;
            BlockState state = new BlockState
            {
                blocker = blocker,
                durabilityBefore = blocker.m_durability,
                Shield = blocker.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield,
                BlockableBefore = hit.GetTotalBlockableDamage(),
            };
            state.ReadParry(player, blocker);
            return state;
        }

        /// <summary>
        /// The game parries when the blocker has a timed block bonus and m_blockTimer (seconds since the block was
        /// raised, -1 while not blocking) is under 0.25. A timer inside the widened window is scaled into the game's
        /// window for the call, and put back by <see cref="Restore"/>.
        /// </summary>
        private void ReadParry(Humanoid player, ItemDrop.ItemData blocker)
        {
            float timer = player.m_blockTimer;
            float window = ParryWindow.Local();
            Parry = blocker.m_shared.m_timedBlockBonus > 1f && timer >= 0f && timer < window;
            if (!Parry || timer < GameParryWindow)
                return;
            timerBefore = timer;
            timerChanged = true;
            player.m_blockTimer = timer * GameParryWindow / window;
        }

        public void Restore(Humanoid player)
        {
            if (timerChanged)
                player.m_blockTimer = timerBefore;
            timerChanged = false;
        }

        /// <summary>Gives back the Shield Wear share of the durability this block took off the blocker.</summary>
        public void RefundWear()
        {
            float lost = durabilityBefore - blocker.m_durability;
            float share = DefenseSkill.LocalShare(DefenseGuardSettings.ShieldWear.Value);
            if (lost > 0f && share > 0f)
                blocker.m_durability = Mathf.Min(durabilityBefore, blocker.m_durability + lost * Mathf.Clamp01(share));
        }
    }
}
