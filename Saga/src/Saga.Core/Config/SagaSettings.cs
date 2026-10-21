// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names, defaults and ranges follow design section 4; the ProgressivePowers, SNEAKer,
// SecondaryAttacks and GrindstoneSkills options they replace are recorded in the design's Source column (names only).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Saga.Powers;
using static ExtraPotions.Saga.Config.SettingScope;

namespace ExtraPotions.Saga.Config
{
    /// <summary>
    /// Every Saga setting of design section 4 in file order. The plugin binds exactly this list; the portable tests compare
    /// it with the spec tables. Deviation from design 4.11, recorded in the task document: Mastery Override is a cheat
    /// toggle (safe off) and the forced level is the ServerAuthoritative number Mastery Override Level that acts only while
    /// the cheat is allowed and on, so every Cheat-scope entry is a toggle whose safe value is off. Section 27 holds the
    /// hidden carriers of the YAML families (C01 push) and section 28 the hidden import marker.
    /// </summary>
    public static class SagaSettings
    {
        private static SagaSetting<float> N(string section, string key, float value, float min, float max, string description) =>
            SagaSetting.Number(section, key, value, min, max, ServerAuthoritative, description);

        private static SagaSetting<int> I(string section, string key, int value, int min, int max, string description) =>
            SagaSetting.Integer(section, key, value, min, max, ServerAuthoritative, description);

        private static SagaSetting<bool> T(string section, string key, bool value, string description) =>
            SagaSetting.Toggle(section, key, value, ServerAuthoritative, description);

        private static SagaSetting<bool> LocalToggle(string section, string key, bool value, string description) =>
            SagaSetting.Toggle(section, key, value, Local, description);

        private static SagaSetting<SagaPreset> Preset(string section, string key, string description) =>
            SagaSetting.Choice(section, key, SagaPreset.MeadWorksDefault, ServerAuthoritative, description);

        private const string LevelNote = " A level above 100 turns it off.";

        public static class General
        {
            public const string Section = "1 - General";

            public static readonly SagaSetting<bool> Enabled = T(Section, "Enabled", true, "Whole product. Off: powers and skills behave as the game does; skill levels are kept.");
            public static readonly SagaSetting<bool> PowersEnabled = T(Section, "Powers Enabled", true, "Boss powers as permanent passives that deepen with boss kills.");
            public static readonly SagaSetting<bool> DefenseEnabled = T(Section, "Defense Enabled", true, "Defense skill perks. Off: fights play as in the game; levels are kept.");
            public static readonly SagaSetting<bool> SneakEnabled = T(Section, "Sneak Enabled", true, "Sneak speed, visibility, quieter crouched noise, stronger eligible backstabs, backstab experience and crouch keep.");
            public static readonly SagaSetting<bool> WoodcuttingEnabled = T(Section, "Woodcutting Enabled", true, "Woodcutting experience, felling, chopping and finds.");
            public static readonly SagaSetting<bool> MiningEnabled = T(Section, "Mining Enabled", true, "Mining experience, seams, veins, perks and finds.");
            public static readonly SagaSetting<bool> ForagingEnabled = T(Section, "Foraging Enabled", true, "Foraging skill experience, yield and sweep picking.");
            public static readonly SagaSetting<bool> DodgingEnabled = T(Section, "Dodging Enabled", true, "Dodge skill perks: longer dodge windows, Counter at Dodge 50 and Heavy Roll while over-encumbered at 75. Off: the game's dodge; levels are kept.");
            public static readonly SagaSetting<bool> WeaponMasteryEnabled = T(Section, "Weapon Mastery Enabled", true, "Weapon Switch experience and weapon milestones. The saved Weapon Switch option still controls its experience boost separately.");
            public static readonly SagaSetting<bool> SwimmingEnabled = T(Section, "Swimming Enabled", true, "Shorter Wet, delayed and softer drowning, and farther body pickup while swimming. Off keeps skill levels.");
            public static readonly SagaSetting<bool> RidingEnabled = T(Section, "Riding Enabled", true, "Riding skill perks: faster gallop and less damage to your mount. Off keeps skill levels.");
            public static readonly SagaSetting<bool> JumpingEnabled = T(Section, "Jumping Enabled", true, "Cheaper jumps, safer falls and a forward landing roll at Jump 50. Off keeps skill levels.");
            public static readonly SagaSetting<bool> RunningEnabled = T(Section, "Running Enabled", true, "Running carry capacity, encumbered stamina savings and Second Wind. Off keeps skill levels.");
            public static readonly SagaSetting<bool> FishingEnabled = T(Section, "Fishing Enabled", true, "Fishing fight, bites, big fish, catch rewards and the angler's log.");
            public static readonly SagaSetting<bool> CraftingEnabled = T(Section, "Crafting Enabled", true, "Maker's Mark, Sturdy Build, Thrifty and Journeyman. Off keeps skill levels.");
        }

        public static class Progression
        {
            public const string Section = "2 - Progression";

            public static readonly SagaSetting<float> SkillLossOnDeath = N(Section, "Skill Loss On Death", 5f, 0f, 100f, "Percent of every skill lost on death; the world's death penalty modifier still scales it.");
            public static readonly SagaSetting<bool> LoseProgressOnDeath = T(Section, "Lose Progress On Death", true, "Death also clears the progress toward each next level. Off: that progress is kept.");
            public static readonly SagaSetting<string> SkillLossOverrides = SagaSetting.Text(Section, "Skill Loss Overrides", "", ServerAuthoritative,
                "Per-skill exceptions to Skill Loss On Death, as Skill=Percent entries separated by commas, for example Swords=0, Run=10. A listed skill loses its own percent (0 to 100) on death; every other skill uses Skill Loss On Death. Empty: every skill uses it. Host or server; applies at the next death.",
                global::ExtraPotions.Saga.Progression.SkillLossOverrides.Normalize);
            public static readonly SagaSetting<bool> CatchUp = T(Section, "Catch Up", true, "Skills below their historical best base level train twice as fast. Character history survives death; explicit skill resets forget it.");
            public static readonly SagaSetting<bool> WeaponSwitch = T(Section, "Weapon Switch", true, "Weapon skills below the highest current base weapon level train 50% faster. With Catch Up, only the larger boost applies.");
            public static readonly SagaSetting<bool> SkillBook = LocalToggle(Section, "Skill Book", true, "The Skills window gets an info pane with each skill's page.");
            public static readonly SagaSetting<SkillGainNotices> SkillGainNotices = SagaSetting.Choice(Section, "Skill Gain Notices", Config.SkillGainNotices.Off, Local,
                "Show experience gains: Off, SagaSkills (Defense, Sneak, gathering) or All skills.");
            public static readonly SagaSetting<SagaPreset> ExperiencePreset = Preset(Section, "Experience Preset",
                "Host or server. Takes effect immediately. Sets Defense, Woodcutting, Mining and Fishing experience multipliers, Foraging experience per pick, the Woodcutting and Foraging discovery multipliers, and the Mining, Fishing and new-size discovery bonuses. MeadWorksDefault keeps Saga's rates. Vanilla uses multipliers 0.5, 1 experience per pick, discovery multipliers 1 and discovery bonuses 0. Relaxed uses multipliers 2, 6 experience per pick, discovery multipliers 5 and double the discovery bonuses. Custom keeps individual values; editing a member selects Custom. Loading never replaces saved values. Planted Pickable Foraging XP stays as configured.");
            public static readonly SagaSetting<SagaPreset> PerkThresholds = Preset(Section, "Perk Threshold Preset",
                "Host or server. Takes effect immediately. Sets the 14 perk unlock levels. MeadWorksDefault keeps Saga's levels. Vanilla sets each level to 101, which turns that perk off. Relaxed opens each perk earlier: Defense 10, 25, 50 and 75; Unbroken 75; Read The Rock 10 and Echo 25; Sweep 10; Grace 50; Legendary and Double Bonus 25; Species Sense, Size Sense and Water Sense 10, 25 and 50. Custom keeps individual levels; editing one selects Custom. Loading never replaces saved levels.");
            public static readonly SagaSetting<SagaPreset> PerkStrength = Preset(Section, "Perk Strength Preset",
                "Host or server. Takes effect immediately. Sets the At 100 perk strengths for Defense, Defense Guard, Felling, Chopping, Mining perks, Foraging, Fishing tackle and bite chance. MeadWorksDefault keeps Saga's strengths. Vanilla sets each to 0. Relaxed raises each by half, up to its own maximum. Custom keeps individual values; editing one selects Custom. Loading never replaces saved values.");
            public static readonly SagaSetting<SagaPreset> DeathPreset = Preset(Section, "Death Preset",
                "Host or server. Takes effect immediately. Sets Skill Loss On Death, Lose Progress On Death and Catch Up. MeadWorksDefault keeps 5 percent loss, lost progress and Catch Up. Vanilla keeps 5 percent loss and lost progress, and turns Catch Up off. Relaxed keeps skills and level progress, with Catch Up on. Custom keeps individual values; editing one selects Custom. Loading never replaces saved values. Skill Loss Overrides stays as configured.");
            public static readonly SagaSetting<SagaPreset> FindsPreset = Preset(Section, "Finds Preset",
                "Host or server. Takes effect immediately. Sets wood and mine find chances, snag chances, big-fish and legendary chances, and the three vein star shares. MeadWorksDefault keeps Saga's chances. Vanilla sets them to 0. Relaxed raises wood finds to 5 and 30, mine finds to 1 and 3, snags to 5 and 12, big-fish chance to 50, the night bonus to 100, legendary chance to 2, and vein shares to 40, 20 and 10, which total 70. Custom keeps individual values; editing one selects Custom. Loading never replaces saved values.");
        }

        public static class Powers
        {
            public const string Section = "3 - Powers";

            public static readonly SagaSetting<int> MaxAttunedPowers = I(Section, "Max Attuned Powers", 1, 1, 8, "Powers a character can hold at once. Lowering it sets the oldest aside without forgetting them.");
            public static readonly SagaSetting<float> KillCreditRange = N(Section, "Kill Credit Range", 200f, 0f, 1000f, "Players within this many meters of a boss when it dies get the kill. 0: unlimited.");
            public static readonly SagaSetting<bool> BlockPowerActivation = T(Section, "Block Power Activation", true, "The powers cannot be activated. Off: vanilla activation works on top of the passives.");
            public static readonly SagaSetting<bool> AdvancedPowerConfig = T(Section, "Advanced Power Config", false, "Saga.Powers.yml replaces the feature settings for every power it lists.");
            public static readonly SagaSetting<bool> ShowLockedFeatures = LocalToggle(Section, "Show Locked Features", true, "Hovers and tooltips list locked features, grayed, with the kills still needed.");
            public static readonly SagaSetting<bool> ShowPassiveInStatusBar = LocalToggle(Section, "Show Passive In Status Bar", true, "Each attuned power shows its passive as a status icon.");
            public static readonly SagaSetting<PowerDisplayMode> DisplayMode = SagaSetting.Choice(Section, "Power Display Mode", PowerDisplayMode.Compact, Local,
                "How several attunements share the power HUD: Stacked, Mixed, Compact, Icons or NewestOnly.");
            public static readonly SagaSetting<int> FullPowerDisplays = SagaSetting.Integer(Section, "Full Power Displays", 2, 1, 8, Local, "Attunements shown with the full power display.");
            public static readonly SagaSetting<int> DisplaySpacing = SagaSetting.Integer(Section, "Power Display Spacing", 4, 0, 32, Local, "Pixels between power display rows.");
            public static readonly SagaSetting<bool> ActivateAllAttuned = T(Section, "Activate All Attuned Powers", true, "On: one activation runs every attuned power, and the power wheel stays hidden. Off: one activation runs only the power chosen on the wheel. Host or server. Takes effect immediately.");
            public static readonly SagaSetting<string> PowerWheelKey = SagaSetting.Shortcut(Section, "Power Wheel Key", "Key held to show the wheel of your attuned powers. Unbound by default, so it does not take a game key or another MeadWorks key. Bind one the game and your other MeadWorks mods leave free. Almanac warns when that key is already used. Your own control. The wheel stays hidden while Activate All Attuned Powers is on.");
            public static readonly SagaSetting<PowerWheelPad> PowerWheelGamepad = SagaSetting.Choice(Section, "Power Wheel Gamepad", PowerWheelPad.Unbound, Local, "Gamepad button held to show the same wheel. Unbound by default. Aim with the right stick or the D-pad, release the button to choose, or press A. B or Escape cancels. Your own control. The wheel stays hidden while Activate All Attuned Powers is on.");
            public static readonly SagaSetting<SagaPreset> PowerStrength = Preset(Section, "Power Strength Preset",
                "Host or server. Takes effect immediately. Sets every boss power feature. MeadWorksDefault keeps Saga's feature values. Vanilla sets numbers to 0, switches off and resistances to Normal. Relaxed moves each number 50 percent further from 0, within its range, and raises each resistance one step. Switches stay as they are. Custom keeps individual values; editing one selects Custom. Loading never replaces saved values. While Advanced Power Config is on, Saga.Powers.yml replaces the features it lists.");
        }

        public static class Defense
        {
            public const string Section = "5 - Defense";
            public const string Experience = "6 - Defense Experience";
            public const string Milestones = "7 - Defense Milestones";
            public const string Guard = "8 - Defense Guard";

            public static readonly SagaSetting<bool> ShowCallouts = LocalToggle(Section, "Show Callouts", true, "Show floating words such as Riposte!, Reflex! and Bash! where those Defense events happen. Your own display choice.");
            public static readonly SagaSetting<float> MaxHealthAt100 = N(Section, "Max Health At 100", 25f, 0f, 200f, "Flat max health at level 100, linear from 0.");
            public static readonly SagaSetting<float> FoodHealthAt100 = N(Section, "Food Health At 100", 10f, 0f, 100f, "Percent more health from food at level 100.");
            public static readonly SagaSetting<float> DamageReductionAt100 = N(Section, "Damage Reduction At 100", 10f, 0f, 90f, "Percent less damage taken at level 100.");
            public static readonly SagaSetting<float> RegenerationAt100 = N(Section, "Regeneration At 100", 1f, 0f, 20f, "Percent of max health healed each interval out of combat at level 100.");
            public static readonly SagaSetting<float> RegenerationInterval = N(Section, "Regeneration Interval", 10f, 1f, 60f, "Seconds between out-of-combat heals.");
            public static readonly SagaSetting<float> OutOfCombatDelay = N(Section, "Out Of Combat Delay", 10f, 0f, 120f, "Seconds without damage before regeneration starts.");
            public static readonly SagaSetting<float> PoiseAt100 = N(Section, "Poise At 100", 25f, 0f, 200f, "Percent higher stagger threshold at level 100.");
            public static readonly SagaSetting<float> ParryWindowAt100 = N(Section, "Parry Window At 100", 0.1f, 0f, 0.5f, "Seconds added to the parry window at level 100.");
            public static readonly SagaSetting<float> BlockStaminaReductionAt100 = N(Section, "Block Stamina Reduction At 100", 10f, 0f, 100f, "Percent cheaper blocks at level 100.");
            public static readonly SagaSetting<float> DodgeStaminaReductionAt100 = N(Section, "Dodge Stamina Reduction At 100", 10f, 0f, 100f, "Percent cheaper dodges at level 100.");

            public static readonly SagaSetting<float> ExperienceMultiplier = N(Experience, "Experience Multiplier", 1f, 0f, 10f, "Multiplies all Defense experience.");
            public static readonly SagaSetting<float> BlockExperience = N(Experience, "Block Experience", 1f, 0f, 20f, "Experience for a block, before the hit size.");
            public static readonly SagaSetting<float> ParryMultiplier = N(Experience, "Parry Multiplier", 2f, 0f, 10f, "A parry earns this many times a block.");
            public static readonly SagaSetting<float> WeaponBlockShare = N(Experience, "Weapon Block Share", 50f, 0f, 100f, "Percent of block experience for a block without a shield.");
            public static readonly SagaSetting<float> HitTakenExperience = N(Experience, "Hit Taken Experience", 0.5f, 0f, 20f, "Experience for an unblocked hit that hurt, before the hit size.");
            public static readonly SagaSetting<float> HitSizeDamage = N(Experience, "Hit Size Damage", 10f, 1f, 1000f, "Damage of a hit of size 1; size grows with the square root of the damage.");
            public static readonly SagaSetting<float> MinHitSize = N(Experience, "Min Hit Size", 0.5f, 0f, 10f, "Smallest size a hit counts as for Defense experience, so light hits still earn a share. Block and hit experience is multiplied by the hit size.");
            public static readonly SagaSetting<float> MaxHitSize = N(Experience, "Max Hit Size", 4f, 0f, 20f, "Largest size a hit counts as for Defense experience, so very heavy hits do not give unlimited experience.");
            public static readonly SagaSetting<float> ExperienceCooldown = N(Experience, "Experience Cooldown", 0.5f, 0f, 10f, "Seconds after a hit that earned experience before the next can.");
            public static readonly SagaSetting<float> FirstBlockBonus = N(Experience, "First Block Bonus", 3f, 1f, 20f, "The first block against each kind of creature earns this many times as much.");
            public static readonly SagaSetting<bool> PlayerHitsTrain = T(Experience, "Player Hits Train", false, "Hits from other players (PvP) train Defense.");

            public static readonly SagaSetting<int> RiposteLevel = I(Milestones, "Riposte Level", 25, 0, 101, "Level that unlocks Riposte: a parry empowers the next attack." + LevelNote);
            public static readonly SagaSetting<float> RiposteWindow = N(Milestones, "Riposte Window", 2f, 0.5f, 10f, "Seconds after a parry the riposte stays ready.");
            public static readonly SagaSetting<float> RiposteDamage = N(Milestones, "Riposte Damage", 25f, 0f, 300f, "Percent more damage of the riposte.");
            public static readonly SagaSetting<int> ShieldWallLevel = I(Milestones, "Shield Wall Level", 50, 0, 101, "Level that unlocks Shield Wall: blocking shelters nearby allies." + LevelNote);
            public static readonly SagaSetting<float> ShieldWallRadius = N(Milestones, "Shield Wall Radius", 4f, 1f, 15f, "Meters around a blocking player that are sheltered.");
            public static readonly SagaSetting<float> ShieldWallReduction = N(Milestones, "Shield Wall Reduction", 10f, 0f, 90f, "Percent less damage for sheltered allies.");
            public static readonly SagaSetting<int> HardenedLevel = I(Milestones, "Hardened Level", 75, 0, 101, "Level that unlocks Hardened: each hit taken stacks damage reduction." + LevelNote);
            public static readonly SagaSetting<float> HardenedPerStack = N(Milestones, "Hardened Per Stack", 3f, 0f, 30f, "Percent less damage per stack.");
            public static readonly SagaSetting<int> HardenedMaxStacks = I(Milestones, "Hardened Max Stacks", 5, 1, 20, "Most Hardened stacks you can hold at once. Each stack lowers damage taken by Hardened Per Stack.");
            public static readonly SagaSetting<float> HardenedDuration = N(Milestones, "Hardened Duration", 8f, 1f, 60f, "Seconds the stacks last after the last hit.");
            public static readonly SagaSetting<int> LastStandLevel = I(Milestones, "Last Stand Level", 100, 0, 101, "Level that unlocks Last Stand: a lethal hit leaves 1 health instead." + LevelNote);
            public static readonly SagaSetting<float> LastStandCooldown = N(Milestones, "Last Stand Cooldown", 600f, 10f, 7200f, "Seconds before Last Stand can save again.");
            public static readonly SagaSetting<float> LastStandInvulnerability = N(Milestones, "Last Stand Invulnerability", 2f, 0f, 10f, "Seconds of invulnerability after Last Stand.");

            public static readonly SagaSetting<float> ReflexChanceAt100 = N(Guard, "Reflex Chance At 100", 10f, 0f, 100f, "Percent chance at level 100 to block a hit while not blocking.");
            public static readonly SagaSetting<float> ShieldBashChanceAt100 = N(Guard, "Shield Bash Chance At 100", 15f, 0f, 100f, "Percent chance at level 100 that a block staggers the attacker.");
            public static readonly SagaSetting<float> ThornsAt100 = N(Guard, "Thorns At 100", 10f, 0f, 200f, "Percent of blocked damage returned to the attacker at level 100.");
            public static readonly SagaSetting<float> AdrenalineBonusAt100 = N(Guard, "Adrenaline Bonus At 100", 25f, 0f, 100f, "Percent more adrenaline from blocks at level 100.");
            public static readonly SagaSetting<float> ShieldWearReductionAt100 = N(Guard, "Shield Wear Reduction At 100", 50f, 0f, 100f, "Percent less shield wear at level 100.");
            public static readonly SagaSetting<float> KnockbackReductionAt100 = N(Guard, "Knockback Reduction At 100", 50f, 0f, 100f, "Percent less knockback at level 100.");
            public static readonly SagaSetting<float> DesperationHealth = N(Guard, "Desperation Health", 25f, 0f, 100f, "Below this percent of max health, Desperation multiplies the damage reduction.");
            public static readonly SagaSetting<float> DesperationMultiplier = N(Guard, "Desperation Multiplier", 2f, 1f, 5f, "Damage reduction multiplier while desperate.");
        }

        public static class Sneak
        {
            public const string Section = "9 - Sneak";

            public static readonly SagaSetting<float> SpeedAt100 = N(Section, "Sneak Speed At 100", 1.5f, 1f, 2.5f, "Crouched speed at Sneak level 100, times the game's crouch speed (linear from level 0).");
            public static readonly SagaSetting<float> VisibilitySkillFactor = N(Section, "Visibility Skill Factor", 2f, 1f, 2f, "Multiplies the Sneak skill's visibility reduction while crouching; visibility never drops below 0.1.");
            public static readonly SagaSetting<float> BackstabExperience = N(Section, "Backstab Experience", 1f, 0f, 10f, "Sneak experience for each successful backstab.");
            public static readonly SagaSetting<bool> KeepCrouching = T(Section, "Keep Crouching Through Damage Over Time", true, "Fire, spirit and poison ticks do not stand you up; direct, lethal, stagger and knockback hits still do.");
        }

        public static class Woodcutting
        {
            public const string Section = "10 - Woodcutting";
            public const string Felling = "11 - Felling";
            public const string Chopping = "12 - Chopping";
            public const string Finds = "13 - Wood Finds";

            public static readonly SagaSetting<bool> ShowCallouts = LocalToggle(Section, "Show Callouts", true, "Show floating words such as Timber!, Clean split! and finds above trees and logs near you. Your own display choice.");
            public static readonly SagaSetting<float> ExperienceMultiplier = N(Section, "Experience Multiplier", 1f, 0f, 10f, "Multiplies all Woodcutting experience Saga gives.");
            public static readonly SagaSetting<bool> TierScaling = T(Section, "Tier Scaling", true, "Wood that needs a better axe gives more experience.");
            public static readonly SagaSetting<float> TierExperience = N(Section, "Tier Experience Per Tool Tier", 50f, 0f, 500f, "Percent more experience per tool tier the wood needs.");
            public static readonly SagaSetting<float> SmallWoodHealth = N(Section, "Small Wood Health", 30f, 0f, 200f, "Wood with less health than this is small wood.");
            public static readonly SagaSetting<float> SmallWoodExperience = N(Section, "Small Wood Experience", 25f, 0f, 100f, "Percent of swing experience small wood gives.");
            public static readonly SagaSetting<float> FellExperience = N(Section, "Fell Experience", 5f, 0f, 100f, "Experience for felling a tree.");
            public static readonly SagaSetting<float> SplitExperience = N(Section, "Split Experience", 2f, 0f, 100f, "Experience for splitting a log that drops wood.");
            public static readonly SagaSetting<float> DiscoveryMultiplier = N(Section, "Discovery Multiplier", 3f, 1f, 10f, "The first fell of each kind of tree earns this many times as much.");

            public static readonly SagaSetting<float> FallPushAt100 = N(Felling, "Fall Push At 100", 900f, 0f, 3000f, "Percent stronger push of a falling tree away from the chopper at level 100.");
            public static readonly SagaSetting<float> LogSafetyAt100 = N(Felling, "Log Safety At 100", 100f, 0f, 100f, "Percent less damage from your own falling logs at level 100.");
            public static readonly SagaSetting<float> DominoImpactAt100 = N(Felling, "Domino Impact At 100", 200f, 0f, 1000f, "Percent impact damage a falling tree deals to trees it hits at level 100.");
            public static readonly SagaSetting<int> DominoMaxChain = I(Felling, "Domino Max Chain", 5, 1, 50, "Most trees one fell can bring down.");
            public static readonly SagaSetting<float> CleanFellAt100 = N(Felling, "Clean Fell At 100", 100f, 0f, 100f, "Percent chance at level 100 that the stump goes with the tree.");
            public static readonly SagaSetting<float> ReplantingAt100 = N(Felling, "Replanting At 100", 50f, 0f, 100f, "Percent chance at level 100 that a sapling is planted for free.");

            public static readonly SagaSetting<float> StaminaRefundAt100 = N(Chopping, "Stamina Refund At 100", 30f, 0f, 100f, "Percent of swing stamina refunded on wood hits at level 100.");
            public static readonly SagaSetting<float> AxeWearReductionAt100 = N(Chopping, "Axe Wear Reduction At 100", 50f, 0f, 100f, "Percent less axe wear at level 100.");
            public static readonly SagaSetting<float> CleanSplitChanceAt100 = N(Chopping, "Clean Split Chance At 100", 20f, 0f, 100f, "Percent chance at level 100 of a clean split.");
            public static readonly SagaSetting<float> CleanSplitBonus = N(Chopping, "Clean Split Bonus", 50f, 0f, 500f, "Percent more wood from a clean split.");
            public static readonly SagaSetting<float> OldGrowthBonus = N(Chopping, "Old Growth Bonus", 100f, 0f, 500f, "Percent more wood from the largest trees.");
            public static readonly SagaSetting<float> OldGrowthStart = N(Chopping, "Old Growth Start", 50f, 0f, 100f, "Tree size percentile where the old-growth bonus starts.");
            public static readonly SagaSetting<float> OldGrowthFull = N(Chopping, "Old Growth Full", 100f, 0f, 100f, "Tree size percentile with the full old-growth bonus.");

            public static readonly SagaSetting<float> FindChanceAt0 = N(Finds, "Find Chance At 0", 2f, 0f, 100f, "Percent chance of a find in a felled tree at level 0.");
            public static readonly SagaSetting<float> FindChanceAt100 = N(Finds, "Find Chance At 100", 15f, 0f, 100f, "Percent chance of a find in a felled tree at level 100.");
        }

        public static class Mining
        {
            public const string Section = "14 - Mining";
            public const string Seams = "15 - Seams";
            public const string Veins = "16 - Veins";
            public const string Perks = "17 - Mining Perks";
            public const string Finds = "18 - Mine Finds";

            public static readonly SagaSetting<bool> ShowCallouts = LocalToggle(Section, "Show Callouts", true, "Show floating words such as Clean strike!, the Echo's distance and finds above rocks near you. Your own display choice.");
            public static readonly SagaSetting<string> PlainStoneItems = SagaSetting.Text(Section, "Plain Stone Items", "Stone, Grausten", ServerAuthoritative,
                "Comma list of item prefabs that are plain stone, never ore.", NameList.Normalize);
            public static readonly SagaSetting<float> ExperienceMultiplier = N(Section, "Experience Multiplier", 1f, 0f, 10f, "Multiplies all Mining experience Saga gives.");
            public static readonly SagaSetting<float> BiomeStep = N(Section, "Experience Per Biome Step", 25f, 0f, 200f, "Percent more swing experience per biome step (Meadows 0 to Ashlands and Deep North 6).");
            public static readonly SagaSetting<float> OreBonus = N(Section, "Ore Experience Bonus", 50f, 0f, 500f, "Percent more experience from ore deposits.");
            public static readonly SagaSetting<float> CleanStrikeExperience = N(Section, "Clean Strike Experience", 1f, 0f, 100f, "Experience for a clean strike.");
            public static readonly SagaSetting<float> DiscoveryExperience = N(Section, "Discovery Experience", 10f, 0f, 1000f, "Experience for the first hit on each kind of ore deposit.");

            public static readonly SagaSetting<float> SeamChanceAt0 = N(Seams, "Seam Chance At 0", 10f, 0f, 100f, "Percent chance at level 0 that a swing opens a seam.");
            public static readonly SagaSetting<float> SeamChanceAt100 = N(Seams, "Seam Chance At 100", 40f, 0f, 100f, "Percent chance at level 100 that a swing opens a seam.");
            public static readonly SagaSetting<float> SeamWindowAt0 = N(Seams, "Seam Window At 0", 2.6f, 0.5f, 30f, "Seconds a seam stays open at level 0.");
            public static readonly SagaSetting<float> SeamWindowAt100 = N(Seams, "Seam Window At 100", 5.6f, 0.5f, 30f, "Seconds a seam stays open at level 100.");
            public static readonly SagaSetting<float> CleanStrikeDamage = N(Seams, "Clean Strike Damage", 2f, 1f, 10f, "Damage multiplier of a clean strike.");
            public static readonly SagaSetting<int> UnbrokenLevel = I(Seams, "Unbroken Level", 100, 0, 101, "Level that unlocks Unbroken chains of clean strikes." + LevelNote);
            public static readonly SagaSetting<float> UnbrokenBonusPerLink = N(Seams, "Unbroken Bonus Per Link", 20f, 0f, 200f, "Percent more damage per link of an Unbroken chain.");
            public static readonly SagaSetting<int> UnbrokenMaxLinks = I(Seams, "Unbroken Max Links", 5, 1, 50, "Most links of an Unbroken chain.");

            public static readonly SagaSetting<float> VeinChance1 = N(Veins, "Vein Chance 1 Star", 25f, 0f, 100f, "Percent of ore deposits with one star.");
            public static readonly SagaSetting<float> VeinChance2 = N(Veins, "Vein Chance 2 Stars", 11f, 0f, 100f, "Percent of ore deposits with two stars.");
            public static readonly SagaSetting<float> VeinChance3 = N(Veins, "Vein Chance 3 Stars", 4f, 0f, 100f, "Percent of ore deposits with three stars.");
            public static readonly SagaSetting<float> VeinBonusPerStar = N(Veins, "Vein Bonus Per Star", 25f, 0f, 500f, "Percent more drop rolls per vein star.");
            public static readonly SagaSetting<int> ReadTheRockLevel = I(Veins, "Read The Rock Level", 25, 0, 101, "Level that shows vein stars on hover." + LevelNote);
            public static readonly SagaSetting<int> EchoLevel = I(Veins, "Echo Level", 50, 0, 101, "Level that unlocks Echo, a ping toward the nearest unfound deposit." + LevelNote);
            public static readonly SagaSetting<float> EchoRadius = N(Veins, "Echo Radius", 40f, 5f, 100f, "Meters Echo searches.");
            public static readonly SagaSetting<float> EchoCooldown = N(Veins, "Echo Cooldown", 10f, 0f, 600f, "Seconds between echoes.");

            public static readonly SagaSetting<float> ExtraOreChanceAt100 = N(Perks, "Extra Ore Chance At 100", 30f, 0f, 100f, "Percent chance at level 100 of an extra ore drop.");
            public static readonly SagaSetting<float> PickaxeWearReductionAt100 = N(Perks, "Pickaxe Wear Reduction At 100", 50f, 0f, 100f, "Percent less pickaxe wear at level 100.");
            public static readonly SagaSetting<float> SplashDamageAt100 = N(Perks, "Splash Damage At 100", 15f, 0f, 200f, "Damage to touching rock chunks at level 100.");

            public static readonly SagaSetting<float> FindChanceAt0 = N(Finds, "Find Chance At 0", 0.2f, 0f, 100f, "Percent chance of a mine find at level 0.");
            public static readonly SagaSetting<float> FindChanceAt100 = N(Finds, "Find Chance At 100", 1f, 0f, 100f, "Percent chance of a mine find at level 100.");
        }

        public static class Foraging
        {
            public const string Section = "19 - Foraging";
            public const string Perks = "20 - Forage Perks";

            public static readonly SagaSetting<bool> ShowCallouts = LocalToggle(Section, "Show Callouts", true, "Show floating words such as Discovered Thistle! above plants you pick. Your own display choice.");
            public static readonly SagaSetting<float> ExperiencePerPick = N(Section, "Experience Per Pick", 3f, 0f, 50f, "Foraging experience per wild pick, before the item and biome factors.");
            public static readonly SagaSetting<float> BiomeStep = N(Section, "Experience Per Biome Step", 25f, 0f, 200f, "Percent more experience per biome step.");
            public static readonly SagaSetting<float> DiscoveryMultiplier = N(Section, "Discovery Multiplier", 3f, 1f, 20f, "The first pick of each kind earns this many times as much.");
            public static readonly SagaSetting<float> PlantedExperience = N(Section, "Planted Pickable Foraging XP", 10f, 0f, 100f, "Percent of the normal Foraging experience for picking a pickable a player planted, such as cultivator stones, berries or mushrooms. Wild plants give full experience; yields and crops are unchanged.");

            public static readonly SagaSetting<float> ExtraYieldChanceAt100 = N(Perks, "Extra Yield Chance At 100", 50f, 0f, 100f, "Percent chance at level 100 of an extra item (replaces the game's 25 percent for these picks).");
            public static readonly SagaSetting<int> SweepLevel = I(Perks, "Sweep Level", 25, 0, 101, "Level that unlocks sweep picking around the plant." + LevelNote);
            public static readonly SagaSetting<float> SweepRadiusAt100 = N(Perks, "Sweep Radius At 100", 4f, 0f, 10f, "Sweep radius in meters at level 100.");
        }

        public static class Fishing
        {
            public const string Section = "21 - Fishing";
            public const string Fight = "22 - Fishing Fight";
            public const string BigFish = "24 - Big Fish";
            public const string Tackle = "25 - Catch And Tackle";

            public static readonly SagaSetting<bool> ShowCallouts = LocalToggle(Section, "Show Callouts", true, "Show floating words such as It's a big one! and finds above the water near you. Your own display choice.");
            public static readonly SagaSetting<float> ExperienceMultiplier = N(Section, "Experience Multiplier", 1f, 0f, 10f, "Multiplies all Fishing experience Saga gives.");
            public static readonly SagaSetting<float> EmptyReelExperience = N(Section, "Empty Reel Experience", 0f, 0f, 100f, "Percent of the game's reeling experience for an empty line.");
            public static readonly SagaSetting<float> FightExperience = N(Section, "Fight Experience", 100f, 0f, 500f, "Percent of the game's reeling experience with a fish on the line.");
            public static readonly SagaSetting<float> CatchExperience = N(Section, "Catch Experience", 10f, 0f, 1000f, "Experience per catch, times the species factor.");
            public static readonly SagaSetting<float> SizeBonus = N(Section, "Size Experience Bonus", 50f, 0f, 500f, "Percent more catch experience per fish level above 1.");
            public static readonly SagaSetting<float> DiscoveryExperience = N(Section, "Discovery Experience", 30f, 0f, 1000f, "Experience for the first catch of a species.");
            public static readonly SagaSetting<float> NewSizeExperience = N(Section, "New Size Experience", 10f, 0f, 1000f, "Experience for the first catch of a new size of a known species.");

            public static readonly SagaSetting<SagaPreset> FightPreset = Preset(Fight, "Preset",
                "Host or server. Takes effect immediately. Sets the line tension bar, tension build and ease, strike windows, the perfect strike, tiring, thrashes, spent reel speed and grace seconds. MeadWorksDefault keeps the bar and Saga's timing. Vanilla turns the bar off and keeps the timing numbers. Relaxed keeps the bar, builds tension at 40 and 20 percent, eases it at 80 percent, opens strike windows to 1 and 1.5 seconds with a 0.35 second perfect strike, tires 25 percent per thrash after 2 thrashes, reels a spent fish 80 percent faster and allows 8 seconds of grace. Custom keeps individual values; editing one selects Custom. Loading never replaces saved values. Grace Level stays on the perk threshold preset.");
            public static readonly SagaSetting<bool> LineTension = T(Fight, "Line Tension", true, "Fights have a line tension bar.");
            public static readonly SagaSetting<float> TensionBuildAt0 = N(Fight, "Tension Build At 0", 60f, 1f, 500f, "Percent tension build rate at level 0.");
            public static readonly SagaSetting<float> TensionBuildAt100 = N(Fight, "Tension Build At 100", 30f, 1f, 500f, "Percent tension build rate at level 100.");
            public static readonly SagaSetting<float> TensionEase = N(Fight, "Tension Ease", 50f, 1f, 500f, "Percent tension release rate while not reeling.");
            public static readonly SagaSetting<float> StrikeWindowAt0 = N(Fight, "Strike Window At 0", 0.5f, 0.1f, 5f, "Seconds to strike after a bite at level 0.");
            public static readonly SagaSetting<float> StrikeWindowAt100 = N(Fight, "Strike Window At 100", 1f, 0.1f, 5f, "Seconds to strike after a bite at level 100.");
            public static readonly SagaSetting<float> PerfectStrikeWindow = N(Fight, "Perfect Strike Window", 0.2f, 0f, 2f, "Seconds after a bite that count as a perfect strike.");
            public static readonly SagaSetting<float> TiringPerThrash = N(Fight, "Tiring Per Thrash", 15f, 0f, 50f, "Percent a fish tires per thrash it survives.");
            public static readonly SagaSetting<int> ThrashesToTire = I(Fight, "Thrashes To Tire", 4, 0, 50, "Thrashes before a fish is spent.");
            public static readonly SagaSetting<float> SpentReelSpeed = N(Fight, "Spent Reel Speed", 50f, 0f, 300f, "Percent faster reeling of a spent fish.");
            public static readonly SagaSetting<int> GraceLevel = I(Fight, "Grace Level", 75, 0, 101, "Level that unlocks stamina grace while reeling." + LevelNote);
            public static readonly SagaSetting<float> GraceSeconds = N(Fight, "Grace Seconds", 4f, 1f, 30f, "Seconds of reeling at zero stamina before the line drops.");

            public static readonly SagaSetting<float> BigOneChanceAt100 = N(BigFish, "Big One Chance At 100", 25f, 0f, 100f, "Percent chance at level 100 that a catch is a big one.");
            public static readonly SagaSetting<float> NightBigOneBonus = N(BigFish, "Night Big One Bonus", 50f, 0f, 500f, "Percent more big-one chance at night.");
            public static readonly SagaSetting<float> LegendaryChance = N(BigFish, "Legendary Chance", 0.5f, 0f, 100f, "Percent chance of a legendary fish.");
            public static readonly SagaSetting<int> LegendaryLevel = I(BigFish, "Legendary Level", 50, 0, 101, "Level that unlocks legendary fish." + LevelNote);
            public static readonly SagaSetting<int> LegendaryThrashes = I(BigFish, "Legendary Thrashes", 8, 0, 50, "Thrashes before a legendary fish is spent.");
            public static readonly SagaSetting<bool> AnnounceLegendaryCatches = T(BigFish, "Announce Legendary Catches", true, "Tell every player when someone lands a legendary fish.");

            public static readonly SagaSetting<float> BonusItemChanceAt100 = N(Tackle, "Bonus Item Chance At 100", 40f, 0f, 100f, "Percent chance at level 100 of a bonus item with a catch.");
            public static readonly SagaSetting<int> DoubleBonusLevel = I(Tackle, "Double Bonus Level", 50, 0, 101, "Level from which bonus items come in twos." + LevelNote);
            public static readonly SagaSetting<float> BaitSaverAt100 = N(Tackle, "Bait Saver At 100", 30f, 0f, 100f, "Percent chance at level 100 that a cast keeps its bait.");
            public static readonly SagaSetting<float> SnagChanceAt0 = N(Tackle, "Snag Chance At 0", 2f, 0f, 100f, "Percent chance of a snag at level 0.");
            public static readonly SagaSetting<float> SnagChanceAt100 = N(Tackle, "Snag Chance At 100", 6f, 0f, 100f, "Percent chance of a snag at level 100.");
            public static readonly SagaSetting<float> SnagWait = N(Tackle, "Snag Wait", 8f, 1f, 120f, "Seconds a line must rest before a snag can bite.");
            public static readonly SagaSetting<float> CastDistanceAt100 = N(Tackle, "Cast Distance At 100", 30f, 0f, 200f, "Percent longer casts at level 100.");
            public static readonly SagaSetting<float> LineLengthAt100 = N(Tackle, "Line Length At 100", 50f, 0f, 200f, "Percent longer line at level 100.");
        }

        public static class Bites
        {
            public const string Section = "23 - Bites";

            public static readonly SagaSetting<float> BiteChanceAt100 = N(Section, "Bite Chance At 100", 100f, 0f, 500f, "Percent more bite chance at level 100.");
            public static readonly SagaSetting<float> DawnAndDuskBonus = N(Section, "Dawn And Dusk Bite Bonus", 50f, 0f, 500f, "Percent more bite chance at dawn and dusk.");
            public static readonly SagaSetting<float> RainBonus = N(Section, "Rain Bite Bonus", 25f, 0f, 500f, "Percent more bite chance in rain.");
            public static readonly SagaSetting<string> ChumItems = SagaSetting.Text(Section, "Chum Items", "Entrails, Bloodbag", ServerAuthoritative,
                "Comma list of items that can be thrown as chum. Empty turns chum off.", NameList.Normalize);
            public static readonly SagaSetting<float> ChumBonus = N(Section, "Chum Bite Bonus", 100f, 0f, 500f, "Percent more bite chance near chum.");
            public static readonly SagaSetting<float> ChumRadius = N(Section, "Chum Radius", 12f, 1f, 50f, "Meters around chum that bite more.");
            public static readonly SagaSetting<float> ChumDuration = N(Section, "Chum Duration", 60f, 10f, 3600f, "Seconds chum lasts.");
            public static readonly SagaSetting<int> SpeciesSenseLevel = I(Section, "Species Sense Level", 25, 0, 101, "Level that names the species on the line." + LevelNote);
            public static readonly SagaSetting<int> SizeSenseLevel = I(Section, "Size Sense Level", 50, 0, 101, "Level that tells the size on the line." + LevelNote);
            public static readonly SagaSetting<int> WaterSenseLevel = I(Section, "Water Sense Level", 75, 0, 101, "Level that tells which fish the water holds." + LevelNote);
        }

        public static class Cheats
        {
            public const string Section = "26 - Cheats";

            public static readonly SagaSetting<bool> PowerCommands = SagaSetting.Toggle(Section, "Power Commands", false, Cheat,
                "Cheat. Enables saga givekills and saga resetkills; the server still checks the admin list for another player. Off whenever the server blocks cheats.", safe: false);
            public static readonly SagaSetting<bool> MasteryOverride = SagaSetting.Toggle(Section, "Mastery Override", false, Cheat,
                "Cheat. Every power acts at Mastery Override Level. Off whenever the server blocks cheats.", safe: false);
            public static readonly SagaSetting<int> MasteryOverrideLevel = I(Section, "Mastery Override Level", 7, 0, 100, "While Mastery Override is on: the mastery level of every power.");
        }

        public static class Data
        {
            public const string Section = "27 - Data";

            private static SagaSetting<string> Carrier(string key, string family) => SagaSetting.Text(Section, key, "", ServerAuthoritative,
                "Managed by Saga: the server's " + family + " files, pushed to clients.", t => t ?? "", hidden: true);

            public static readonly SagaSetting<string> PowerLevels = Carrier("Power Levels", "Saga.PowerLevels");
            public static readonly SagaSetting<string> Powers = Carrier("Powers", "Saga.Powers");
            public static readonly SagaSetting<string> Finds = Carrier("Finds", "Saga.Finds");
            public static readonly SagaSetting<string> MineFinds = Carrier("Mine Finds", "Saga.MineFinds");
            public static readonly SagaSetting<string> Forage = Carrier("Forage", "Saga.Forage");
            public static readonly SagaSetting<string> Snags = Carrier("Snags", "Saga.Snags");
        }

        public static class Import
        {
            public const string Section = "28 - Import";

            public static readonly SagaSetting<string> ImportedSources = SagaSetting.Text(Section, "Imported Sources", "", Local,
                "Managed by Saga: source files already imported.", t => (t ?? "").Trim(), hidden: true);
        }

        private static readonly Lazy<Dictionary<PowerFeature, SagaSetting>> featureSettings = new Lazy<Dictionary<PowerFeature, SagaSetting>>(() =>
            PowerCatalog.Features.ToDictionary(f => f, f => FeatureSetting(f)));

        private static SagaSetting FeatureSetting(PowerFeature f)
        {
            var section = PowerCatalog.Section(f.Power);
            var description = FeatureDescription(f);
            switch (f.Kind)
            {
                case FeatureKind.Resistance: return SagaSetting.Choice(section, f.Key, (Resistance)f.Default, ServerAuthoritative, description);
                case FeatureKind.Toggle: return SagaSetting.Toggle(section, f.Key, (bool)f.Default, ServerAuthoritative, description);
                default:
                    var range = PowerCatalog.Range(f.Kind)!.Value;
                    return SagaSetting.Number(section, f.Key, (float)f.Default, range.Min, range.Max, ServerAuthoritative, description);
            }
        }

        /// <summary>What a power feature's value does, from the effect it produces (PowerCatalog and NativePowers).</summary>
        private static string FeatureDescription(PowerFeature f)
        {
            var lead = "Unlocks at mastery level " + f.Index + " of " + PowerCatalog.DisplayName(f.Power) + "'s power. ";
            var name = f.Name.Substring(0, 1).ToLowerInvariant() + f.Name.Substring(1);
            switch (f.Kind)
            {
                case FeatureKind.Resistance:
                    return lead + "How you take " + f.Name.Replace(" resistance", "").ToLowerInvariant() + " damage, using the game's damage modifiers " +
                        "(for example Resistant takes less, Weak takes more, Immune takes none). Normal turns this feature off.";
                case FeatureKind.Toggle:
                    return lead + (f.Name == "Cold immunity" ? "You are not affected by the Cold status effect."
                        : f.Name == "Sailing tailwind" ? "The wind blows from behind the ship you steer." : f.Name + ".") + " Off turns this feature off.";
                case FeatureKind.CarryWeight:
                    return lead + "Carry weight added to your maximum. 0 turns this feature off.";
                case FeatureKind.Armor:
                    return lead + "Armor points added to your armor. 0 turns this feature off.";
                case FeatureKind.SkillLevels:
                    return lead + "Levels added to your " + f.Name + " on top of your trained level. 0 turns this feature off.";
                case FeatureKind.HealthTick:
                    return lead + "Health restored every 10 seconds; a negative value drains health instead. 0 turns this feature off.";
            }
            if (f.Name == "Wind at your back")
                return lead + "With the wind behind you, faster movement and cheaper running, as a fraction: 0.25 is up to 25% faster and 25% cheaper. 0 turns this feature off.";
            if (f.Name == "Stealth")
                return lead + "How much harder you are to see, as a fraction: 0.25 is 25% harder; a negative value makes you easier to see. 0 turns this feature off.";
            if (f.Name == "Timed block bonus")
                return lead + "Added to the parry (timed block) bonus of your shield or weapon: 0.5 raises a 2x parry bonus to 2.5x. 0 turns this feature off.";
            return lead + "Change to " + name + ", as a fraction: 0.1 is 10% more and -0.3 is 30% less. 0 turns this feature off.";
        }

        /// <summary>The setting of a power feature (design 4.3.2).</summary>
        public static SagaSetting PowerFeature(PowerFeature feature) =>
            featureSettings.Value.TryGetValue(feature ?? throw new ArgumentNullException(nameof(feature)), out var s) ? s : throw new ArgumentException("Not a catalog feature.", nameof(feature));

        // Deferred so that every nested holder is initialized first (Mono initializes the containing type eagerly).
        private static readonly Lazy<SagaSetting[]> all = new Lazy<SagaSetting[]>(() => new SagaSetting[]
        {
            General.Enabled, General.PowersEnabled, General.DefenseEnabled, General.SneakEnabled, General.WoodcuttingEnabled, General.MiningEnabled,
            General.ForagingEnabled, General.DodgingEnabled, General.WeaponMasteryEnabled, General.SwimmingEnabled, General.RidingEnabled, General.JumpingEnabled, General.RunningEnabled, General.FishingEnabled, General.CraftingEnabled,
            Progression.SkillLossOnDeath, Progression.LoseProgressOnDeath, Progression.SkillLossOverrides, Progression.CatchUp, Progression.WeaponSwitch, Progression.SkillBook, Progression.SkillGainNotices,
            Progression.ExperiencePreset, Progression.PerkThresholds, Progression.PerkStrength, Progression.DeathPreset, Progression.FindsPreset,
            Powers.MaxAttunedPowers, Powers.KillCreditRange, Powers.BlockPowerActivation, Powers.AdvancedPowerConfig, Powers.ShowLockedFeatures,
            Powers.ShowPassiveInStatusBar, Powers.DisplayMode, Powers.FullPowerDisplays, Powers.DisplaySpacing,
            Powers.ActivateAllAttuned, Powers.PowerWheelKey, Powers.PowerWheelGamepad, Powers.PowerStrength,
        }
        .Concat(PowerCatalog.Features.Select(PowerFeature))
        .Concat(new SagaSetting[]
        {
            Defense.ShowCallouts, Defense.MaxHealthAt100, Defense.FoodHealthAt100, Defense.DamageReductionAt100, Defense.RegenerationAt100,
            Defense.RegenerationInterval, Defense.OutOfCombatDelay, Defense.PoiseAt100, Defense.ParryWindowAt100, Defense.BlockStaminaReductionAt100,
            Defense.DodgeStaminaReductionAt100,
            Defense.ExperienceMultiplier, Defense.BlockExperience, Defense.ParryMultiplier, Defense.WeaponBlockShare, Defense.HitTakenExperience,
            Defense.HitSizeDamage, Defense.MinHitSize, Defense.MaxHitSize, Defense.ExperienceCooldown, Defense.FirstBlockBonus, Defense.PlayerHitsTrain,
            Defense.RiposteLevel, Defense.RiposteWindow, Defense.RiposteDamage, Defense.ShieldWallLevel, Defense.ShieldWallRadius, Defense.ShieldWallReduction,
            Defense.HardenedLevel, Defense.HardenedPerStack, Defense.HardenedMaxStacks, Defense.HardenedDuration, Defense.LastStandLevel,
            Defense.LastStandCooldown, Defense.LastStandInvulnerability,
            Defense.ReflexChanceAt100, Defense.ShieldBashChanceAt100, Defense.ThornsAt100, Defense.AdrenalineBonusAt100, Defense.ShieldWearReductionAt100,
            Defense.KnockbackReductionAt100, Defense.DesperationHealth, Defense.DesperationMultiplier,
            Sneak.SpeedAt100, Sneak.VisibilitySkillFactor, Sneak.BackstabExperience, Sneak.KeepCrouching,
            Woodcutting.ShowCallouts, Woodcutting.ExperienceMultiplier, Woodcutting.TierScaling, Woodcutting.TierExperience, Woodcutting.SmallWoodHealth,
            Woodcutting.SmallWoodExperience, Woodcutting.FellExperience, Woodcutting.SplitExperience, Woodcutting.DiscoveryMultiplier,
            Woodcutting.FallPushAt100, Woodcutting.LogSafetyAt100, Woodcutting.DominoImpactAt100, Woodcutting.DominoMaxChain, Woodcutting.CleanFellAt100,
            Woodcutting.ReplantingAt100,
            Woodcutting.StaminaRefundAt100, Woodcutting.AxeWearReductionAt100, Woodcutting.CleanSplitChanceAt100, Woodcutting.CleanSplitBonus,
            Woodcutting.OldGrowthBonus, Woodcutting.OldGrowthStart, Woodcutting.OldGrowthFull,
            Woodcutting.FindChanceAt0, Woodcutting.FindChanceAt100,
            Mining.ShowCallouts, Mining.PlainStoneItems, Mining.ExperienceMultiplier, Mining.BiomeStep, Mining.OreBonus, Mining.CleanStrikeExperience,
            Mining.DiscoveryExperience,
            Mining.SeamChanceAt0, Mining.SeamChanceAt100, Mining.SeamWindowAt0, Mining.SeamWindowAt100, Mining.CleanStrikeDamage, Mining.UnbrokenLevel,
            Mining.UnbrokenBonusPerLink, Mining.UnbrokenMaxLinks,
            Mining.VeinChance1, Mining.VeinChance2, Mining.VeinChance3, Mining.VeinBonusPerStar, Mining.ReadTheRockLevel, Mining.EchoLevel,
            Mining.EchoRadius, Mining.EchoCooldown,
            Mining.ExtraOreChanceAt100, Mining.PickaxeWearReductionAt100, Mining.SplashDamageAt100,
            Mining.FindChanceAt0, Mining.FindChanceAt100,
            Foraging.ShowCallouts, Foraging.ExperiencePerPick, Foraging.BiomeStep, Foraging.DiscoveryMultiplier, Foraging.PlantedExperience,
            Foraging.ExtraYieldChanceAt100, Foraging.SweepLevel, Foraging.SweepRadiusAt100,
            Fishing.ShowCallouts, Fishing.ExperienceMultiplier, Fishing.EmptyReelExperience, Fishing.FightExperience, Fishing.CatchExperience,
            Fishing.SizeBonus, Fishing.DiscoveryExperience, Fishing.NewSizeExperience,
            Fishing.FightPreset, Fishing.LineTension, Fishing.TensionBuildAt0, Fishing.TensionBuildAt100, Fishing.TensionEase, Fishing.StrikeWindowAt0,
            Fishing.StrikeWindowAt100, Fishing.PerfectStrikeWindow, Fishing.TiringPerThrash, Fishing.ThrashesToTire, Fishing.SpentReelSpeed,
            Fishing.GraceLevel, Fishing.GraceSeconds,
            Bites.BiteChanceAt100, Bites.DawnAndDuskBonus, Bites.RainBonus, Bites.ChumItems, Bites.ChumBonus,
            Bites.ChumRadius, Bites.ChumDuration, Bites.SpeciesSenseLevel, Bites.SizeSenseLevel, Bites.WaterSenseLevel,
            Fishing.BigOneChanceAt100, Fishing.NightBigOneBonus, Fishing.LegendaryChance, Fishing.LegendaryLevel,
            Fishing.LegendaryThrashes, Fishing.AnnounceLegendaryCatches,
            Fishing.BonusItemChanceAt100, Fishing.DoubleBonusLevel, Fishing.BaitSaverAt100, Fishing.SnagChanceAt0,
            Fishing.SnagChanceAt100, Fishing.SnagWait, Fishing.CastDistanceAt100, Fishing.LineLengthAt100,
            Cheats.PowerCommands, Cheats.MasteryOverride, Cheats.MasteryOverrideLevel,
            Data.PowerLevels, Data.Powers, Data.Finds, Data.MineFinds, Data.Forage, Data.Snags,
            Import.ImportedSources,
        }).ToArray());

        private static readonly Lazy<Dictionary<string, SagaSetting>> byId = new Lazy<Dictionary<string, SagaSetting>>(
            () => all.Value.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every setting in file order.</summary>
        public static IReadOnlyList<SagaSetting> All => all.Value;

        /// <summary>A setting by <c>Section.Key</c> in any case; null when unknown.</summary>
        public static SagaSetting? Find(string? id) => id != null && byId.Value.TryGetValue(id.Trim(), out var setting) ? setting : null;
    }
}
