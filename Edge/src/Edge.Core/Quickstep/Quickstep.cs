// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// QuickstepSystem.cs (dash acceleration, dash duration, shield invincibility, cooldown and stamina share constants;
// eligible weapon rule; start and dodge handoff conditions; begin, tick, handoff and stop flow with the cooldown
// started on a finished dash or a handoff and not on an interruption).
// Changed by expDARE, 2026: Unity-free state machine driven by explicit time and body facts so it runs in the portable
// tests; physics, animation and the Harmony hooks stay in the plugin (D07b).
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using ExtraPotions.Edge.Secondaries;

namespace ExtraPotions.Edge.Quickstep
{
    /// <summary>The player's state a quickstep or its roll handoff needs.</summary>
    public struct QuickstepBody
    {
        public bool OnGround { get; set; }
        public bool Dead { get; set; }
        public bool Teleporting { get; set; }
        public bool InAttack { get; set; }
        public bool Encumbered { get; set; }
        public bool InDodge { get; set; }
        public bool Staggering { get; set; }

        public override string ToString() =>
            "ground=" + OnGround + " dead=" + Dead + " teleporting=" + Teleporting + " attack=" + InAttack + " encumbered=" + Encumbered + " dodge=" + InDodge + " stagger=" + Staggering;
    }

    public static class QuickstepRules
    {
        public const float DashAcceleration = 200f;
        public const float DashDuration = 0.25f;
        public const float ShieldInvincibilityDuration = 0.15f;
        public const float CooldownDuration = 0.5f;
        public const float StaminaUsageFactor = 0.6f;

        /// <summary>
        /// The equipped item (right hand, else left hand) is a knife or an Unarmed-skill weapon; nothing equipped (bare
        /// fists) and shields never count.
        /// </summary>
        public static bool Eligible(WeaponSkill? skill, WeaponItemType? itemType) =>
            skill.HasValue && itemType.HasValue && itemType.Value != WeaponItemType.Shield
            && (skill.Value == WeaponSkill.Knives || skill.Value == WeaponSkill.Unarmed);

        public static bool CanStart(QuickstepBody b) =>
            b.OnGround && !b.Dead && !b.Teleporting && !b.InAttack && !b.Encumbered && !b.InDodge && !b.Staggering;

        public static bool CanHandoff(QuickstepBody b) =>
            b.OnGround && !b.Dead && !b.Teleporting && !b.InAttack && !b.Encumbered && !b.Staggering;

        /// <summary>The quickstep's stamina: 60 % of the game's dodge cost (after any other mod's multiplier).</summary>
        public static float StaminaUse(float dodgeStaminaUse) => Math.Max(0f, dodgeStaminaUse * StaminaUsageFactor);
    }

    public enum QuickstepPress { Ignored, Started, NoStamina, HandedOffToRoll }

    /// <summary>One local player's quickstep. Times are game seconds; main thread only.</summary>
    public sealed class QuickstepMachine
    {
        private double readyAt = double.NegativeInfinity;
        private float elapsed;
        private bool shield;

        public bool Active { get; private set; }

        /// <summary>The dash grants dodge invincibility now.</summary>
        public bool Invincible { get; private set; }

        /// <summary>Stamina of the running quickstep, or of the roll it handed off to.</summary>
        public float StaminaUse { get; private set; }

        public bool CoolingDown(double now) => !Active && now < readyAt;

        /// <summary>A dodge press. While dashing it hands off to the game's roll; otherwise it may start a quickstep.</summary>
        public QuickstepPress Press(double now, bool eligible, QuickstepBody body, float dodgeStaminaUse, float stamina, bool shield)
        {
            if (Active)
            {
                if (!QuickstepRules.CanHandoff(body)) return QuickstepPress.Ignored;
                if (stamina < StaminaUse) return QuickstepPress.NoStamina;
                Stop(now, startCooldown: true);
                return QuickstepPress.HandedOffToRoll;
            }
            if (!eligible || CoolingDown(now) || !QuickstepRules.CanStart(body)) return QuickstepPress.Ignored;
            var use = QuickstepRules.StaminaUse(dodgeStaminaUse);
            if (stamina < use) return QuickstepPress.NoStamina;
            Active = true;
            Invincible = true;
            elapsed = 0f;
            this.shield = shield;
            StaminaUse = use;
            return QuickstepPress.Started;
        }

        /// <summary>Advances the dash by <paramref name="dt"/> seconds; a finished dash starts the cooldown at <paramref name="now"/>.</summary>
        public void Tick(double now, float dt)
        {
            if (!Active) return;
            elapsed += Math.Max(0f, dt);
            if (shield && elapsed >= QuickstepRules.ShieldInvincibilityDuration) Invincible = false;
            if (elapsed >= QuickstepRules.DashDuration) Stop(now, startCooldown: true);
        }

        /// <summary>Death, teleport or a lost eligibility mid-dash: stop without a cooldown.</summary>
        public void Interrupt()
        {
            Active = false;
            Invincible = false;
        }

        private void Stop(double now, bool startCooldown)
        {
            Active = false;
            Invincible = false;
            if (startCooldown) readyAt = now + QuickstepRules.CooldownDuration;
        }
    }
}
