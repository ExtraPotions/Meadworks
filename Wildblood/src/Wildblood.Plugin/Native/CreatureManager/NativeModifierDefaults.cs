// From CreatureManager 1.2.5 (sighsorry1029), pinned at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// GPL-3.0-only; original LICENSE retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: source defaults for the unified Wildblood modifier rules; the reference example distanceScaling band is active (it acts only while the Distance Scaling setting is on).
// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
namespace ExtraPotions.Wildblood.AdoptedCm;
internal static class NativeModifierDefaults
{
    internal static string BuildDefaultLevelOverrideYaml()
    {
        StringBuilder builder = new();
        AppendTemplateComment(builder, "Targets: Global, Boss, biome, group, or prefab; omitted fields inherit independently.");
        AppendTemplateComment(builder, "Priority: non-boss/Enforcer prefab > group > biome > Global; regular boss prefab > group > Boss.");
        AppendTemplateComment(builder, "Nested biome blocks override their parent target in that biome.");
        AppendTemplateComment(builder, "modifiers: omit/{} = inherit, [] = clear; tuples start with chance% and may omit only trailing values.");
        AppendTemplateBlankLine(builder);

        builder.AppendLine("Global:");
        AppendIndented(builder, 1, "level: [80, 20]                       # Weights by level: level 1 = 80, level 2 = 20.");
        AppendTemplateLine(builder, 1, "scalePerLevel: 0.1                    # Optional CM star growth; 0 = none. Omit for prefab sizes in ExceptLevel mode; CM growth is skipped in dungeons.");
        AppendIndented(builder, 1, "damage: 1                            # Level-1 damage multiplier.");
        AppendIndented(builder, 1, "damagePerLevel: 0.25                 # Vanilla: 0.5. Replaces vanilla growth: damage * (1 + (level - 1) * value).");
        AppendIndented(builder, 1, "health: 1                            # Level-1 max-health multiplier.");
        AppendIndented(builder, 1, "healthPerLevel: 1                    # Health = base * health * (1 + (level - 1) * value).");
        AppendIndented(builder, 1, "distanceScaling: " + ExtraPotions.Wildblood.Stars.DistanceScaling.DefaultBandText + " # damageStep, healthStep, stepMeters, maxSteps (0 = unlimited). Acts only while the Distance Scaling setting is on.");
        AppendTemplateLine(builder, 1, "modifierDistanceScaling: [0.03, 1000, 8] # chanceStep, stepMeters, maxSteps; 20% -> 20.6% at 1000m, 24.8% at 8000m+.");
        AppendDefaultLevelModifiers(builder, bossDefaults: false);
        builder.AppendLine();

        builder.AppendLine("Boss:                                  # Regular bosses only; Enforcers use non-boss rules.");
        AppendIndented(builder, 1, "# level: [100]                       # Boss fallback: level 1; omit to allow the enabled built-in biome preset ([] is invalid).");
        AppendIndented(builder, 1, "scalePerLevel: 0.1");
        AppendIndented(builder, 1, "damage: 1");
        AppendIndented(builder, 1, "damagePerLevel: 0.1");
        AppendIndented(builder, 1, "health: 1");
        AppendIndented(builder, 1, "healthPerLevel: 0.5");
        AppendDefaultLevelModifiers(builder, bossDefaults: true);
        builder.AppendLine();

        builder.AppendLine("TentaRoot:");
        AppendIndented(builder, 1, "modifiers: []                       # Clears inherited modifiers for this prefab.");
        builder.AppendLine();

        AppendTemplateComment(builder, "Optional examples; uncomment only what you need.");
        AppendTemplateLine(builder, 0, "groups:");
        AppendTemplateLine(builder, 1, "ForestBrutes:");
        AppendTemplateLine(builder, 2, "- Troll");
        AppendTemplateLine(builder, 2, "- Greydwarf_Elite");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "Meadows:");
        AppendTemplateLine(builder, 1, "level: [100]");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "ForestBrutes:");
        AppendTemplateLine(builder, 1, "health: 1.1");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "Troll:");
        AppendTemplateLine(builder, 1, "BlackForest:");
        AppendTemplateLine(builder, 2, "damage: 1.2");
        return builder.ToString();
    }

    private static void AppendDefaultLevelModifiers(StringBuilder builder, bool bossDefaults)
    {
        string chance = bossDefaults ? "10" : "5";
        string regenerationPerSecond = bossDefaults ? "0.002" : "0.01";
        string blamerChance = bossDefaults ? "0" : chance;
        string blamerKarmaPerSecond = bossDefaults ? "1" : "0.5";
        string blamerMaxKarmaGain = bossDefaults ? "60" : "45";
        AppendIndented(builder, 1, "modifiers:                           # One per group; totals below 100 allow none, above 100 are weights.");
        AppendIndented(builder, 2, "# Offense: Enraged to Undodgeable");
        AppendIndented(builder, 2, $"enraged: {chance}, 0.15                 # chance%, outgoingDamageBonus");
        AppendIndented(builder, 2, $"fire: {chance}, 0.2                     # chance%, addedFireDamage");
        AppendIndented(builder, 2, $"frost: {chance}, 0.1                    # chance%, addedFrostDamage");
        AppendIndented(builder, 2, $"lightning: {chance}, 0.1                # chance%, addedLightningDamage");
        AppendIndented(builder, 2, $"spirit: {chance}, 0.05                  # chance%, addedSpiritDoT");
        AppendIndented(builder, 2, $"armorPiercing: {chance}, 0.3            # chance%, ignoredPlayerArmor");
        AppendIndented(builder, 2, $"staggering: {chance}, 0.6               # chance%, staggerBonus");
        AppendIndented(builder, 2, $"undodgeable: {chance}, 0.25             # chance%, damageReduction");
        AppendIndented(builder, 2, "# Defense: Armored to Chameleon");
        AppendIndented(builder, 2, $"armored: {chance}, 0.3                 # chance%, damageReduction");
        AppendIndented(builder, 2, $"deathward: {chance}, 0.2, 10, 3         # chance%, restoredHealth, cooldownSeconds, maxActivations");
        AppendIndented(builder, 2, $"regenerating: {chance}, {regenerationPerSecond}, 20      # chance%, maxHealthRatioPerSecond, healthPerSecondCap (0 = unlimited)");
        AppendIndented(builder, 2, $"reflection: {chance}, 0.1, 0.5         # chance%, actualMeleeDamageReflected, procChance");
        AppendIndented(builder, 2, $"vortex: {chance}, 0.5                  # chance%, projectileIgnoreProc");
        AppendIndented(builder, 2, $"adaptive: {chance}, 0.5                # chance%, rememberedTypeDamageReduction");
        AppendIndented(builder, 2, $"unflinching: {chance}                    # chance%");
        AppendIndented(builder, 2, $"chameleon: {chance}, 10                  # chance%, immunitySwitchSeconds");
        AppendIndented(builder, 2, "# Affliction: Exposed to ToxicDeath");
        AppendIndented(builder, 2, $"exposed: {chance}, 0.2, 0.5, 5          # chance%, damageTaken, proc, duration");
        AppendIndented(builder, 2, $"weakened: {chance}, 0.2, 0.5, 5         # chance%, outgoingDamageReduction, proc, duration");
        AppendIndented(builder, 2, $"withered: {chance}, 0.5, 0.5, 5         # chance%, healingReduction, proc, duration");
        AppendIndented(builder, 2, $"crippling: {chance}, 0.5, 0.5, 0.5, 5  # chance%, moveReduction, jumpReduction, proc, duration");
        AppendIndented(builder, 2, $"disruptive: {chance}, 0.5, 0.5, 0.5, 5  # chance%, staminaRegenReduction, eitrRegenReduction, proc, duration");
        AppendIndented(builder, 2, $"adrenalineDrain: {chance}, 0.5, 0.5, 0.5, 5 # chance%, currentAdrenalineRemoved, adrenalineGainReduction, procChance, duration");
        AppendIndented(builder, 2, $"corrosive: {chance}, 0.5, 0.5, 5          # chance%, durabilityLossBonus, procChance, duration");
        AppendIndented(builder, 2, "toxicDeath: 10, 0.3, 4, blob_aoe   # chance%, maxHealthDamage, radius, triggerEffect");
        AppendIndented(builder, 2, "# Special: Swift to Blamer");
        AppendIndented(builder, 2, $"swift: {chance}, 0.4                  # chance%, movementSpeedBonus");
        AppendIndented(builder, 2, $"attackSpeed: {chance}, 0.3            # chance%, attackSpeedBonus");
        AppendIndented(builder, 2, $"vampiric: {chance}, 0.3               # chance%, actualDirectDamageHealing");
        AppendIndented(builder, 2, $"reaping: {chance}, 0.05, 20, 0.1, 2, 0.01, 0.2, 0.05, 0.5 # chance%, heal/base, healMaxActivations, maxHealth/base, maxHealthCap, damagePerKill, damageCap, scalePerKill, scaleCap");
        AppendIndented(builder, 2, $"blink: {chance}, 6, 16, fx_Adrenaline1  # chance%, cooldown, maxRange, startEffect");
        AppendIndented(builder, 2, $"omen: {chance}, 0.5                  # chance%, forcedEnforcerChance");
        AppendIndented(builder, 2, $"juggernaut: {chance}, 150, 5           # chance%, minimumPushForce, cooldownSeconds");
        AppendIndented(builder, 2, $"blamer: {blamerChance}, {blamerKarmaPerSecond}, {blamerMaxKarmaGain}, 0.75           # chance%, karmaPerSecond, maxKarmaGain (0 = unlimited), fleeHealthRatio");
    }

    private static void AppendIndented(StringBuilder builder, int indent, string text)
    {
        builder.Append(' ', indent * 2);
        builder.AppendLine(text);
    }

    private static void AppendTemplateComment(StringBuilder builder, string text)
    {
        builder.Append("# ");
        builder.AppendLine(text);
    }

    private static void AppendTemplateLine(StringBuilder builder, int indent, string text)
    {
        builder.Append("# ");
        builder.Append(' ', indent * 2);
        builder.AppendLine(text);
    }

    private static void AppendTemplateBlankLine(StringBuilder builder)
    {
        builder.AppendLine("#");
    }
}
