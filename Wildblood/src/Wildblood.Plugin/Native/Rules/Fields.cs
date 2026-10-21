// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The exact YAML key strings, in one place, so the parser, the defaults and the behaviours never disagree on a
    /// name. Star-power line names and every mutation-power field name the specification fixes live here.
    /// </summary>
    internal static class Fields
    {
        // star power lines
        public const string Growth = "growth";
        public const string Hp = "hp";
        public const string Attack = "attack";
        public const string SwingSpeed = "swing speed";
        public const string Speed = "speed";
        public const string Drops = "drops";

        // Mad
        public const string Move = "move";
        public const string AttackSpeed = "attack speed";
        public const string Health = "health";

        // Bloated
        public const string Delay = "delay";
        public const string Damage = "damage";
        public const string Radius = "radius";
        public const string BlastEffect = "blast effect";
        public const string BlastSound = "blast sound";
        public const string WarningEffect = "warning effect";

        // Cloaked
        public const string RevealDistance = "reveal distance";
        public const string FadeTime = "fade time";
        public const string FadeMargin = "fade margin";

        // Splintering
        public const string MaxGenerations = "max generations";
        public const string MaxDescendants = "max descendants";

        // Leeching
        public const string Regen = "regen";
        public const string Lifesteal = "lifesteal";
        public const string RegenCap = "regen cap";
        public const string CombatCooldown = "combat cooldown";

        // Warding
        public const string Reflect = "reflect";
        public const string Knockback = "knockback";
        public const string MaxReflect = "max reflect";

        // Plated
        public const string Armour = "armour";
        public const string MaxReduction = "max reduction";

        // Miasmic
        public const string CloudLife = "cloud life";
        public const string CloudDamage = "cloud damage";
        public const string CloudsPerSecond = "clouds per second";
        public const string CloudRadius = "cloud radius";
        public const string CloudEffect = "cloud effect";
        public const string BodyEffect = "body effect";

        /// <summary>The mutation-power fields that name a vanilla prefab (a string) rather than carrying a number.</summary>
        public static bool IsPrefabField(string field) =>
            field == CloudEffect || field == BodyEffect || field == BlastEffect || field == BlastSound
            || field == WarningEffect || field == BonusItem || field == GlitterEffect || field == BlinkEffect
            || field == TellEffect || field == TellSound || field == ShriekSound
            || field == AuraEffect || field == TrailEffect
            || field == RevealEffect || field == RevealSound || field == VanishEffect || field == HowlSound;

        // Devouring
        public const string AbsorbHealth = "absorb health";
        public const string AbsorbDamage = "absorb damage";
        public const string SlowPer100Health = "slow per 100 health";
        public const string PlayerThreshold = "player threshold";
        public const string DevourCooldown = "devour cooldown";
        public const string MaxPreyHealth = "max prey health";
        public const string MinMeals = "min meals";

        // Thieving
        public const string MaxItems = "max items";

        // Gilded (`loot` is shared with the aspects block below)
        public const string BonusItem = "bonus item";
        public const string BonusAmount = "bonus amount";
        public const string FleeDistance = "flee distance";
        public const string GlitterEffect = "glitter effect";

        // Blinking (`every` and `health` are shared)
        public const string Distance = "distance";
        public const string TellTime = "tell time";
        public const string BlinkEffect = "blink effect";
        public const string TellEffect = "tell effect";
        public const string TellSound = "tell sound";

        // Relentless
        public const string ChaseDistance = "chase distance";

        // Screecher (`radius` is shared)
        public const string Cooldown = "cooldown";
        public const string Threshold = "threshold";
        public const string MuteTime = "mute time";
        public const string ShriekSound = "shriek sound";

        // Frostbound (`trail life`, `patch radius`, `patch spacing`, `slow`, `trail effect` shared with Mudbound)
        public const string AuraRadius = "aura radius";
        public const string StaminaRegen = "stamina regen";
        public const string FrostHeal = "frost heal";
        public const string AuraEffect = "aura effect";
        public const string TrailLife = "trail life";
        public const string PatchRadius = "patch radius";
        public const string PatchSpacing = "patch spacing";
        public const string Grip = "grip";
        public const string TrailEffect = "trail effect";

        // Mudbound
        public const string Slow = "slow";

        // Corrodent
        public const string Durability = "durability";

        // Cloning (`range` and `cooldown` are shared)
        public const string Times = "times";
        public const string DecoyLife = "decoy life";
        public const string RevealEffect = "reveal effect";
        public const string RevealSound = "reveal sound";
        public const string VanishEffect = "vanish effect";

        // Flamebound (the trail fields above and `damage` are shared): fire damage a second to a player in its patches

        // Piercing (EliteCreaturesReborn 3.22): the share of a struck player's armour its hits ignore
        public const string IgnoreArmour = "ignore armour";

        // Howling (`count`, `radius` and `cooldown` are shared)
        public const string HowlSound = "howl sound";

        // Binding (`cooldown` is shared)
        public const string RootTime = "root time";

        // boss aspects block (under `bosses:`)
        public const string Aspects = "aspects";
        public const string Enabled = "enabled";
        public const string ShiftHours = "shift hours";
        public const string ShiftSeconds = "shift seconds";
        public const string Chances = "chances";
        public const string Loot = "loot";
        public const string Power = "power";
        public const string PerBoss = "per boss";
        public const string Match = "match";
        public const string Summons = "summons";

        // aspect power fields (Reflect and Health above are shared)
        public const string ArrowReduction = "arrow reduction";
        public const string Every = "every";
        public const string Count = "count";
        public const string Stars = "stars";
        public const string ElementalBonus = "elemental bonus";
        public const string PhysicalBonus = "physical bonus";
        public const string LessHealth = "less health";
        public const string LessDamage = "less damage";
        public const string SplitAt = "split at";
        public const string PerPlayer = "per player";
        public const string HealthPerTier = "health per tier";

        // Adaptive
        public const string Resist = "resist";
        public const string Window = "window";

        // Fixated (`every` is shared)
        public const string MarkedBonus = "marked bonus";
        public const string OthersLess = "others less";

        // Stormbound (`every`, `tell time`, `radius` and `damage` are shared) and Gravitic
        public const string Range = "range";
        public const string PullTime = "pull time";
        public const string PullSpeed = "pull speed";
        public const string SlamRadius = "slam radius";
        public const string SlamDamage = "slam damage";

        // Colossal
        public const string Bigger = "bigger";
        public const string MoreHealth = "more health";
        public const string Slower = "slower";
        public const string ShockwaveRadius = "shockwave radius";

        // Tethered (`less health`, `less damage`, `attack speed` and `armour` are shared)
        public const string FullGap = "full gap";

        // Bountiful
        public const string ExtraAspects = "extra aspects";

        // Portalbound (`range` is shared)
        public const string MinHeight = "min height";
        public const string Clearance = "clearance";

        // Nightfall (`range`, `every`, `damage` shared)
        public const string EveryMax = "every max";
        public const string Life = "life";
        public const string FormTime = "form time";
        public const string TornadoSpeed = "tornado speed";
        public const string TopWidth = "top width";
        public const string BaseWidth = "base width";
        public const string Height = "height";
        public const string TossDistance = "toss distance";

        // Brutal
        public const string Launch = "launch";
        public const string Lift = "lift";

        /// <summary>Phantom's fixed copy count before copies came per player; read only to tell a rule file it is unused.</summary>
        public const string Copies = "copies";

        // world tiers and breeding blocks (Enabled above is shared)
        public const string WorldTiers = "world tiers";
        public const string BossKeys = "bosses";
        public const string StarBoost = "star boost";
        public const string MutationBoost = "mutation boost";
        public const string Breeding = "breeding";
        public const string MutationChance = "mutation chance";

        // loot block
        public const string Mode = "mode";
        public const string ExtraRollChance = "extra roll chance";
        public const string MaxExtraRolls = "max extra rolls";
        public const string GlobalMultiplier = "global multiplier";
        public const string BossMultiplier = "boss multiplier";
        public const string MultiplyTrophies = "multiply trophies";

        // creature loot rows
        public const string Item = "item";
        public const string Amount = "amount";
        public const string Chance = "chance";
        public const string Remove = "remove";
        public const string PerStar = "per star";
        public const string DropOverrides = "drop overrides";
        public const string ExtraDrops = "extra drops";
    }
}
