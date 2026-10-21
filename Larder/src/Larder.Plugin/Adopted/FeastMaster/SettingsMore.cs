// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/SettingsMore.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>How the remaining time under each food icon is shown.</summary>
    public enum FoodTimers
    {
        Vanilla,
        Always,
        Never,
    }

    /// <summary>Sections 4 to 8: stamina costs, base values, skills, world rates and display.</summary>
    public static partial class Settings
    {
        // 4. Stamina Costs
        public static ConfigEntry<float> RunCost { get; private set; }
        public static ConfigEntry<float> JumpCost { get; private set; }
        public static ConfigEntry<float> DodgeCost { get; private set; }
        public static ConfigEntry<float> BlockCost { get; private set; }
        public static ConfigEntry<float> AttackCost { get; private set; }
        public static ConfigEntry<float> SneakCost { get; private set; }
        public static ConfigEntry<float> SwimCost { get; private set; }
        public static ConfigEntry<float> EncumberedCost { get; private set; }
        public static ConfigEntry<float> ToolCost { get; private set; }
        public static ConfigEntry<float> FishingPullCost { get; private set; }
        public static ConfigEntry<float> FishingHookedCost { get; private set; }
        public static ConfigEntry<float> HarpoonCost { get; private set; }
        public static ConfigEntry<float> OutOfCombatRunCost { get; private set; }
        public static ConfigEntry<float> OutOfCombatJumpCost { get; private set; }
        public static ConfigEntry<float> OutOfCombatDodgeCost { get; private set; }
        public static ConfigEntry<float> OutOfCombatSneakCost { get; private set; }
        public static ConfigEntry<bool> FreeSneakingWithoutEnemies { get; private set; }
        public static ConfigEntry<float> SkillDiscount { get; private set; }
        public static ConfigEntry<float> DrowningDamage { get; private set; }

        // 5. Base Values
        public static ConfigEntry<float> BaseHealth { get; private set; }
        public static ConfigEntry<float> BaseStamina { get; private set; }
        public static ConfigEntry<float> RunSkillStamina { get; private set; }
        public static ConfigEntry<float> JumpSkillStamina { get; private set; }
        public static ConfigEntry<float> SneakSkillStamina { get; private set; }
        public static ConfigEntry<float> SwimSkillStamina { get; private set; }
        public static ConfigEntry<float> FishingSkillStamina { get; private set; }

        // 6. Skills
        public static ConfigEntry<float> RunSkillGain { get; private set; }
        public static ConfigEntry<float> JumpSkillGain { get; private set; }
        public static ConfigEntry<float> SneakSkillGain { get; private set; }
        public static ConfigEntry<float> SwimSkillGain { get; private set; }
        public static ConfigEntry<float> FishingSkillGain { get; private set; }

        // 7. World Rates
        public static ConfigEntry<float> FoodRate { get; private set; }
        public static ConfigEntry<float> StaminaRate { get; private set; }
        public static ConfigEntry<float> MoveStaminaRate { get; private set; }
        public static ConfigEntry<float> StaminaRegenRate { get; private set; }

        // 8. Display (per client)
        public static ConfigEntry<bool> HideHealthNumber { get; private set; }
        public static ConfigEntry<bool> HideStaminaNumber { get; private set; }
        public static ConfigEntry<bool> HideEitrNumber { get; private set; }
        public static ConfigEntry<FoodTimers> FoodTimersMode { get; private set; }

        private static void BindStaminaCosts(NativeBindings config)
        {
            RunCost = BindCost(config, "Run Cost", "running", "Stamina Rate and Move Stamina Rate in this section, Out Of Combat Run Cost and a mead's RunStaminaModifier");
            JumpCost = BindCost(config, "Jump Cost", "jumping", "Stamina Rate and Move Stamina Rate in this section, Out Of Combat Jump Cost, Skill Discount and a mead's JumpStaminaModifier");
            DodgeCost = BindCost(config, "Dodge Cost", "dodging", "Stamina Rate in this section, Out Of Combat Dodge Cost, Skill Discount and the game's own dodge skill discount");
            BlockCost = BindCost(config, "Block Cost", "blocking (normal and perfect blocks)", "Stamina Rate in this section and Skill Discount");
            AttackCost = BindCost(config, "Attack Cost", "attacking", "Stamina Rate in this section");
            SneakCost = BindCost(config, "Sneak Cost", "sneaking", "Stamina Rate in this section and Out Of Combat Sneak Cost; Free Sneaking Without Enemies overrides both");
            SwimCost = BindCost(config, "Swim Cost", "swimming", "Stamina Rate and Move Stamina Rate in this section");
            EncumberedCost = BindCost(config, "Encumbered Cost", "walking while encumbered", "Stamina Rate in this section");
            ToolCost = BindCost(config, "Tool Cost", "placing and repairing with the hammer, hoe and cultivator", "Stamina Rate in this section");
            FishingPullCost = BindCost(config, "Fishing Pull Cost", "reeling in the fishing line", "Stamina Rate in this section");
            FishingHookedCost = BindCost(config, "Fishing Hooked Cost", "holding a hooked fish on the line", "Stamina Rate in this section");
            HarpoonCost = BindCost(config, "Harpoon Cost", "pulling a harpooned creature", "Stamina Rate in this section");
            DrowningDamage = config.Bind(StaminaCostsSection, "Drowning Damage", 1f,
                "Multiplier on the damage taken each second while swimming without stamina. 1 = untouched, 0 = harmless.");
        }

        private static ConfigEntry<float> BindCost(NativeBindings config, string key, string action, string stacks)
        {
            return config.Bind(StaminaCostsSection, key, 1f,
                $"Multiplier on the stamina drained by {action}. 1 = untouched, 0 = free, 2 = double. Multiplies with {stacks}.");
        }

        private static void BindOutOfCombat(NativeBindings config)
        {
            const string outOfCombat = "Extra multiplier on the {0} cost while out of combat: no enemy targets or has noticed the player and the player is not attacking. 1 = no change.";
            OutOfCombatRunCost = config.Bind(StaminaCostsSection, "Out Of Combat Run Cost", 1f, string.Format(outOfCombat, "run"));
            OutOfCombatJumpCost = config.Bind(StaminaCostsSection, "Out Of Combat Jump Cost", 1f, string.Format(outOfCombat, "jump"));
            OutOfCombatDodgeCost = config.Bind(StaminaCostsSection, "Out Of Combat Dodge Cost", 1f, string.Format(outOfCombat, "dodge"));
            OutOfCombatSneakCost = config.Bind(StaminaCostsSection, "Out Of Combat Sneak Cost", 1f, string.Format(outOfCombat, "sneak"));
            FreeSneakingWithoutEnemies = config.Bind(StaminaCostsSection, "Free Sneaking Without Enemies", false,
                "Sneaking costs no stamina when no enemy is within stealth range and none has noticed the player.");
            SkillDiscount = config.Bind(StaminaCostsSection, "Skill Discount", 0f,
                "Percent taken off the block, dodge and jump costs at skill 100 of the matching skill (Blocking, Dodge, Jump). Scales with the skill and applies on top of the game's own discounts, so dodge at skill 100 gets both. Multiplies with the matching cost multipliers.");
        }

        private static void BindBaseValues(NativeBindings config)
        {
            BaseHealth = config.Bind(BaseValuesSection, "Base Health", 25f,
                "Health a player has with no food. The game uses 25; at 25 Larder does not change it, so another mod's value is kept.");
            BaseStamina = config.Bind(BaseValuesSection, "Base Stamina", 75f,
                "Stamina a player has with no food. The game uses 75; at 75 Larder does not change it, so another mod's value is kept. Regen Per Extra Stamina Point counts stamina above this value, so raising it lowers that bonus.");
            RunSkillStamina = BindSkillStamina(config, "Run Skill Stamina", "Run");
            JumpSkillStamina = BindSkillStamina(config, "Jump Skill Stamina", "Jump");
            SneakSkillStamina = BindSkillStamina(config, "Sneak Skill Stamina", "Sneak");
            SwimSkillStamina = BindSkillStamina(config, "Swim Skill Stamina", "Swim");
            FishingSkillStamina = BindSkillStamina(config, "Fishing Skill Stamina", "Fishing");
        }

        private static ConfigEntry<float> BindSkillStamina(NativeBindings config, string key, string skill)
        {
            return config.Bind(BaseValuesSection, key, 0f,
                $"Stamina added to Base Stamina at {skill} skill 100, scaling linearly with the skill level.");
        }

        private static void BindSkills(NativeBindings config)
        {
            RunSkillGain = BindSkillGain(config, "Run Skill Gain", "Run");
            JumpSkillGain = BindSkillGain(config, "Jump Skill Gain", "Jump");
            SneakSkillGain = BindSkillGain(config, "Sneak Skill Gain", "Sneak");
            SwimSkillGain = BindSkillGain(config, "Swim Skill Gain", "Swim");
            FishingSkillGain = BindSkillGain(config, "Fishing Skill Gain", "Fishing");
        }

        private static ConfigEntry<float> BindSkillGain(NativeBindings config, string key, string skill)
        {
            return config.Bind(SkillsSection, key, 1f,
                $"Multiplier on the experience the {skill} skill earns. 1 = untouched. The world's skill gain rate still applies on top.");
        }

        private static void BindWorldRates(NativeBindings config)
        {
            const string rate = "Overrides the world modifier '{0}' while above 0. 0 leaves the world's value.";
            FoodRate = config.Bind(WorldRatesSection, "Food Rate", 0f, string.Format(rate, "food rate: how fast food runs out") + " Applies together with Duration Modifier; Eat Again At is a fraction of the resulting duration.");
            StaminaRate = config.Bind(WorldRatesSection, "Stamina Rate", 0f, string.Format(rate, "stamina rate: all stamina use") + " Multiplies with every cost multiplier in this section.");
            MoveStaminaRate = config.Bind(WorldRatesSection, "Move Stamina Rate", 0f, string.Format(rate, "move stamina rate: running, jumping and swimming") + " Multiplies with Run, Jump and Swim Cost in this section and with Stamina Rate.");
            StaminaRegenRate = config.Bind(WorldRatesSection, "Stamina Regen Rate", 0f, string.Format(rate, "stamina regen rate") + " Multiplies with Stamina Regen Multiplier in 4 - Recovery.");
        }

        private static void BindDisplay(NativeBindings config)
        {
            const string perPlayer = " Per player, not synced from the server.";
            HideHealthNumber = config.Bind(DisplaySection, "Hide Health Number", false, "Hides the number on the health bar." + perPlayer, synced: false);
            HideStaminaNumber = config.Bind(DisplaySection, "Hide Stamina Number", false, "Hides the number on the stamina bar." + perPlayer, synced: false);
            HideEitrNumber = config.Bind(DisplaySection, "Hide Eitr Number", false, "Hides the number on the eitr bar." + perPlayer, synced: false);
            FoodTimersMode = config.Bind(DisplaySection, "Food Timers", FoodTimers.Vanilla,
                "The remaining time under each food icon: Vanilla shows it as the game does (always), Always forces it on, Never hides it." + perPlayer, synced: false);
        }
    }
}
