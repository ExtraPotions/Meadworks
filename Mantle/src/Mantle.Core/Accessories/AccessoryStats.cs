// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace Mantle.Accessories
{
    /// <summary>
    /// What a ring or a belt gives while it lies in its slot. Flat numbers add as they are (max health, max stamina, max
    /// eitr, carry weight); the three percentages are of the base value, as the game's status effects apply them (stamina
    /// regeneration, walking and running speed, and the stamina every attack takes, where a negative value is a saving).
    /// Two items add up, so two identical rings give exactly double.
    /// </summary>
    public sealed class AccessoryStats
    {
        public static readonly AccessoryStats None = new AccessoryStats();

        public AccessoryStats(float maxHealth = 0f, float maxStamina = 0f, float maxEitr = 0f, float staminaRegenPercent = 0f,
            float moveSpeedPercent = 0f, float attackStaminaPercent = 0f, float carry = 0f)
        {
            MaxHealth = maxHealth;
            MaxStamina = maxStamina;
            MaxEitr = maxEitr;
            StaminaRegenPercent = staminaRegenPercent;
            MoveSpeedPercent = moveSpeedPercent;
            AttackStaminaPercent = attackStaminaPercent;
            Carry = carry;
        }

        public float MaxHealth { get; }
        public float MaxStamina { get; }
        public float MaxEitr { get; }
        public float StaminaRegenPercent { get; }
        public float MoveSpeedPercent { get; }
        public float AttackStaminaPercent { get; }
        public float Carry { get; }

        public bool IsNone => MaxHealth == 0f && MaxStamina == 0f && MaxEitr == 0f && StaminaRegenPercent == 0f
            && MoveSpeedPercent == 0f && AttackStaminaPercent == 0f && Carry == 0f;

        /// <summary>What to add to the stamina regeneration multiplier: the game's own effects above 1 add their excess (1.10 adds 0.10).</summary>
        public float StaminaRegenAdd => StaminaRegenPercent / 100f;

        /// <summary>What to add to a speed: a share of the base speed, as <c>SE_Stats.ModifySpeed</c> does.</summary>
        public float SpeedAdd(float baseSpeed) => baseSpeed * MoveSpeedPercent / 100f;

        /// <summary>What to add to an attack's stamina use: a share of its base use, as <c>SE_Stats.ModifyAttackStaminaUsage</c> does.</summary>
        public float AttackStaminaAdd(float baseUse) => baseUse * AttackStaminaPercent / 100f;

        /// <summary>The attack's stamina use after the share, clamped at zero where the game clamps it (<paramref name="minZero"/>).</summary>
        public float AttackStaminaApplied(float baseUse, float current, bool minZero)
        {
            float use = current + AttackStaminaAdd(baseUse);
            return minZero && use < 0f ? 0f : use;
        }

        public AccessoryStats Plus(AccessoryStats other) => new AccessoryStats(MaxHealth + other.MaxHealth, MaxStamina + other.MaxStamina,
            MaxEitr + other.MaxEitr, StaminaRegenPercent + other.StaminaRegenPercent, MoveSpeedPercent + other.MoveSpeedPercent,
            AttackStaminaPercent + other.AttackStaminaPercent, Carry + other.Carry);

        public override bool Equals(object obj) => obj is AccessoryStats other && MaxHealth == other.MaxHealth && MaxStamina == other.MaxStamina
            && MaxEitr == other.MaxEitr && StaminaRegenPercent == other.StaminaRegenPercent && MoveSpeedPercent == other.MoveSpeedPercent
            && AttackStaminaPercent == other.AttackStaminaPercent && Carry == other.Carry;

        public override int GetHashCode() => MaxHealth.GetHashCode() ^ (MaxStamina.GetHashCode() * 31) ^ (Carry.GetHashCode() * 17) ^ (MaxEitr.GetHashCode() * 13);

        public override string ToString() => $"hp {MaxHealth}, stamina {MaxStamina}, eitr {MaxEitr}, regen {StaminaRegenPercent}%, speed {MoveSpeedPercent}%, attack {AttackStaminaPercent}%, carry {Carry}";
    }
}
