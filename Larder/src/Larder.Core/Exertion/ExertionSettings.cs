// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Exertion
{
    /// <summary>
    /// The Exertion settings the rules read (design 4.6; user decision Q1: the whole module stays in Larder). Defaults
    /// are the game's: every multiplier 1, no discount, base health 25 and stamina 75, no skill stamina, world rates 0
    /// (the world's own). L02 binds them; this model only carries values.
    /// </summary>
    public sealed class ExertionSettings
    {
        public float RunCost { get; set; } = 1f;
        public float JumpCost { get; set; } = 1f;
        public float DodgeCost { get; set; } = 1f;
        public float BlockCost { get; set; } = 1f;
        public float AttackCost { get; set; } = 1f;
        public float SneakCost { get; set; } = 1f;
        public float SwimCost { get; set; } = 1f;
        public float EncumberedCost { get; set; } = 1f;
        public float ToolCost { get; set; } = 1f;
        public float FishingPullCost { get; set; } = 1f;
        public float FishingHookedCost { get; set; } = 1f;
        public float HarpoonCost { get; set; } = 1f;
        public float DrowningDamage { get; set; } = 1f;

        public float OutOfCombatRunCost { get; set; } = 1f;
        public float OutOfCombatJumpCost { get; set; } = 1f;
        public float OutOfCombatDodgeCost { get; set; } = 1f;
        public float OutOfCombatSneakCost { get; set; } = 1f;
        public bool FreeSneakingWithoutEnemies { get; set; }

        /// <summary>Percent off block, dodge and jump at skill level 100.</summary>
        public float SkillDiscount { get; set; }

        public float BaseHealth { get; set; } = BaseValues.DefaultHealth;
        public float BaseStamina { get; set; } = BaseValues.DefaultStamina;

        public float RunSkillStamina { get; set; }
        public float JumpSkillStamina { get; set; }
        public float SneakSkillStamina { get; set; }
        public float SwimSkillStamina { get; set; }
        public float FishingSkillStamina { get; set; }

        public float RunSkillGain { get; set; } = 1f;
        public float JumpSkillGain { get; set; } = 1f;
        public float SneakSkillGain { get; set; } = 1f;
        public float SwimSkillGain { get; set; } = 1f;
        public float FishingSkillGain { get; set; } = 1f;

        public float FoodRate { get; set; }
        public float StaminaRate { get; set; }
        public float MoveStaminaRate { get; set; }
        public float StaminaRegenRate { get; set; }

        /// <summary>Whether any stamina cost rule differs from the game (a cost patch group is needed).</summary>
        public bool ChangesCosts =>
            RunCost != 1f || JumpCost != 1f || DodgeCost != 1f || BlockCost != 1f || AttackCost != 1f || SneakCost != 1f
            || SwimCost != 1f || EncumberedCost != 1f || ToolCost != 1f || FishingPullCost != 1f || FishingHookedCost != 1f
            || HarpoonCost != 1f || OutOfCombatRunCost != 1f || OutOfCombatJumpCost != 1f || OutOfCombatDodgeCost != 1f
            || OutOfCombatSneakCost != 1f || FreeSneakingWithoutEnemies || SkillDiscount != 0f;

        /// <summary>Whether any world rate is overridden (above 0).</summary>
        public bool OverridesWorldRates => FoodRate > 0f || StaminaRate > 0f || MoveStaminaRate > 0f || StaminaRegenRate > 0f;
    }
}
