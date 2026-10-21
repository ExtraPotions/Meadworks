// From CreatureManager 1.2.5 (sighsorry1029), pinned at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// GPL-3.0-only; original LICENSE retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: source modifier rules, passive spawn policy and stored-state reads over Wildblood authority;
// upstream GetGlobalModifierDefinitions restored as a read-only view for the lore window and Compendium.
// The Tamed Keep Traits setting withholds a tamed creature's modifier effects when off.
// The Protected Prefabs setting replaces the hard-coded FrozenKing_p2 check; the Distance Scaling setting gates the modifier
// chance band and selects the pinned distanceScaling stat band once per creature (TrySelectStatDistanceScaling, CM
// TrySelectDistanceScalingMultiplier). The multipliers are applied by the ECR stat path, never by a CM level roll.
// No CM level roll, SetLevel, or additional birth owner.
// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Adopted.Traits;
using CoreModifiers = ExtraPotions.Wildblood.Traits.Modifiers;
namespace ExtraPotions.Wildblood.AdoptedCm;
internal static class CreatureLevelManager
{
    private const string HealthAppliedKey = "wildblood.cm.LevelHealthApplied";
    private const string HealthMultiplierKey = "wildblood.cm.LevelHealthMultiplier";
    private const string DamageAppliedKey = "wildblood.cm.LevelDamageApplied";
    private const string DamageMultiplierKey = "wildblood.cm.LevelDamageMultiplier";
    private const string DamageReplacesVanillaKey = "wildblood.cm.LevelDamageReplacesVanilla";
    private static readonly object Sync = new();
    private static LevelDefinition[] ActiveDefinitions = Array.Empty<LevelDefinition>();
    private static Character? CachedRuleSearchCharacter;
    private static LevelRuleScope CachedRuleSearchScope;
    private static int CachedRuleSearchFrame = -1;
    private static LevelRuleSearch? CachedRuleSearch;
    private enum LevelRuleScope { Full, BaselineOnly }
    private enum ModifierApplicationMode { Roll, Keep, Block }
    private readonly struct SpawnPolicy
    {
        internal SpawnPolicy(ModifierApplicationMode mode, LevelRuleScope scope, bool distance)
        { ModifierMode = mode; ModifierScope = scope; AllowModifierDistanceScaling = distance; }
        internal ModifierApplicationMode ModifierMode { get; }
        internal LevelRuleScope ModifierScope { get; }
        internal bool AllowModifierDistanceScaling { get; }
    }
    internal static void Load(List<LevelDefinition> definitions)
    {
        lock (Sync) { ActiveDefinitions = definitions.ToArray(); CachedRuleSearch = null; CachedRuleSearchFrame = -1; CachedRuleSearchCharacter = null; }
    }
    internal static bool IsLevelSystemEnabled() => Plugin.NativeRulesReady && NativeModifierRules.Ready
        && WildbloodModules.Active(WildbloodModule.Traits);
    // Restored from the pin for the read-only lore/Compendium: cloned global (no biome) modifier rules, disabled ones dropped.
    internal static Dictionary<string, ModifierDefinition> GetGlobalModifierDefinitions()
    {
        Dictionary<string, ModifierDefinition> result = new(StringComparer.OrdinalIgnoreCase);
        LevelDefinition[] definitions;
        lock (Sync)
        {
            definitions = ActiveDefinitions;
        }

        foreach (LevelDefinition definition in definitions)
        {
            string target = (definition.Target ?? "").Trim();
            string biome = (definition.Biome ?? "").Trim();
            if (!string.Equals(target, "global", StringComparison.OrdinalIgnoreCase) || biome.Length > 0)
            {
                continue;
            }

            if (definition.ModifiersCleared)
            {
                result.Clear();
                continue;
            }

            if (definition.Modifiers == null)
            {
                continue;
            }

            foreach (KeyValuePair<string, ModifierDefinition> entry in definition.Modifiers)
            {
                if (!result.TryGetValue(entry.Key, out ModifierDefinition current))
                {
                    result[entry.Key] = entry.Value.Clone();
                    continue;
                }

                current.OverlayFrom(entry.Value);
            }
        }

        foreach (string disabled in result
                     .Where(entry => !entry.Value.Chance.HasValue || entry.Value.Chance.Value <= 0f)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            result.Remove(disabled);
        }

        return result;
    }
    internal static bool IsReadyForModifierApplication(Character character)
    {
        if (character == null || character.IsPlayer() || !CreatureDomainManager.IsSynchronizedConfigurationReady()) return false;
        if (GetSpawnPolicy(character).ModifierMode == ModifierApplicationMode.Keep) return true;
        ZNetView view = character.m_nview;
        return view != null && view.IsValid() && view.GetZDO() is ZDO zdo && TraitStore.IsResolved(zdo);
    }
    private static SpawnPolicy GetSpawnPolicy(Character character) => IsProtectedPrefab(character)
        ? new SpawnPolicy(ModifierApplicationMode.Block, LevelRuleScope.Full, false)
        : GetSpawnPolicy(CreatureManagerSpawnLifecycle.GetSpawnSource(character));
    private static SpawnPolicy GetSpawnPolicy(CreatureSpawnSourceKind source) => source switch
    {
        CreatureSpawnSourceKind.Command or CreatureSpawnSourceKind.PlayerSummon => new SpawnPolicy(ModifierApplicationMode.Block, LevelRuleScope.Full, false),
        CreatureSpawnSourceKind.Breeding or CreatureSpawnSourceKind.Egg => new SpawnPolicy(ModifierApplicationMode.Roll, LevelRuleScope.BaselineOnly, false),
        CreatureSpawnSourceKind.Growup or CreatureSpawnSourceKind.TamedRestore => new SpawnPolicy(ModifierApplicationMode.Keep, LevelRuleScope.Full, false),
        _ => new SpawnPolicy(ModifierApplicationMode.Roll, LevelRuleScope.Full, true)
    };
    private static int GetMaximumRolledModifiers(bool isBoss, bool isEnforcer)
    {
        if (!IsLevelSystemEnabled() || (isBoss && !isEnforcer && !WildbloodModules.Active(WildbloodModule.Bosses))) return 0;
        var setting = isBoss && !isEnforcer ? WildbloodSettings.Bosses.BossModifiers : WildbloodSettings.Traits.CreatureModifiers;
        return CoreModifiers.Count(Plugin.Settings == null ? setting.Default : Plugin.Settings.Get(setting));
    }
    /// <summary>A prefab on the Protected Prefabs list (CreatureManager hard-codes FrozenKing_p2; Wildblood reads the setting): no level,
    /// stat or modifier scaling, no inherited or restored modifiers.</summary>
    internal static bool IsProtectedPrefab(Character? character)
    {
        if (character == null)
        {
            return false;
        }

        ZNetView? view = character.m_nview;
        ZDO? zdo = view != null ? view.GetZDO() : null;
        if (zdo != null && zdo.GetPrefab() != 0)
        {
            return IsProtectedPrefab(zdo);
        }

        // Prefab editing and early initialization can precede Character.Awake/ZDO setup.
        return ExtraPotions.Wildblood.Runtime.ProtectedCreatures.IsProtected(character.gameObject);
    }

    internal static bool IsProtectedPrefab(ZDO? zdo)
    {
        return ExtraPotions.Wildblood.Runtime.ProtectedCreatures.IsProtected(zdo);
    }

    internal static bool ShouldRollModifiers(Character character)
    {
        return AreModifiersEnabled(character) && GetModifierMode(character) == ModifierApplicationMode.Roll;
    }

    internal static bool BlocksModifierRoll(Character character)
    {
        return GetModifierMode(character) == ModifierApplicationMode.Block;
    }

    internal static bool AllowsModifierEffects(Character character)
    {
        return IsLevelSystemEnabled() &&
               AreModifiersEnabled(character) &&
               GetModifierMode(character) != ModifierApplicationMode.Block &&
               !TamedTraitsWithheld(character.IsTamed());
    }

    // Tamed Keep Traits off: a tamed creature's modifiers stay stored but do not act (the same withholding the ECR controller applies to its mutations).
    private static bool TamedTraitsWithheld(bool tamed) => tamed && ExtraPotions.Wildblood.Stars.TamedTraits.Stripped(
        Plugin.Settings == null || Plugin.Settings.Get(WildbloodSettings.Traits.TamedKeepTraits), true);

    internal static bool AllowsModifierEffects(ZDO zdo, bool isBoss, bool isEnforcer)
    {
        return zdo != null &&
               !IsProtectedPrefab(zdo) &&
               IsLevelSystemEnabled() &&
               AreModifiersEnabled(isBoss, isEnforcer) &&
               GetSpawnPolicy(CreatureManagerSpawnLifecycle.GetSpawnSource(zdo)).ModifierMode !=
               ModifierApplicationMode.Block &&
               !TamedTraitsWithheld(zdo.GetBool(ZDOVars.s_tamed, false));
    }

    private static bool AreModifiersEnabled(Character character)
    {
        if (character == null || character.IsPlayer())
        {
            return false;
        }

        return AreModifiersEnabled(character.IsBoss(), CreatureKarmaManager.IsEnforcer(character));
    }

    private static bool AreModifiersEnabled(bool isBoss, bool isEnforcer)
    {
        return GetMaximumRolledModifiers(isBoss, isEnforcer) > 0;
    }

    internal static int GetMaximumRolledModifiers(Character character)
    {
        if (character == null || character.IsPlayer())
        {
            return 0;
        }

        return GetMaximumRolledModifiers(character.IsBoss(), CreatureKarmaManager.IsEnforcer(character));
    }

    private static ModifierApplicationMode GetModifierMode(Character character)
    {
        return GetSpawnPolicy(character).ModifierMode;
    }

    private static bool TryGetModifierRuleScope(Character character, out LevelRuleScope scope, out bool allowDistanceScaling)
    {
        SpawnPolicy policy = GetSpawnPolicy(character);
        scope = policy.ModifierScope;
        allowDistanceScaling = policy.AllowModifierDistanceScaling;
        return policy.ModifierMode == ModifierApplicationMode.Roll;
    }

    /// <summary>Exact source reload policy: current native level wins over DesiredLevel and no new level roll occurs.</summary>
    internal static void SynchronizePreservedNativeState(Character character, ZDO zdo)
    {
        if (!TraitStore.KeepsNativeLevel(zdo) || character.m_nview == null || !character.m_nview.IsOwner()) return;
        bool held = CreatureModifierManager.SuspendReapingRuntime(character, health: true, scale: false);
        try
        {
            int level = Math.Max(1, zdo.GetInt(ZDOVars.s_level, 1));
            if (character.GetLevel() != level) character.m_level = level;
            if (TryGetStoredHealthMultiplier(zdo, out float multiplier)) ApplyHealthMultiplier(character, multiplier);
        }
        finally
        {
            if (held) CreatureModifierManager.ResumeReapingRuntime(character, health: true, scale: false);
        }
    }

    // Read-only proof for the imported CM baseline; this is not an ECR-owned restore lease.
    internal static bool MatchesPreservedNativeHealthBaseline(Character character)
    {
        ZDO? zdo = character.m_nview != null && character.m_nview.IsValid() ? character.m_nview.GetZDO() : null;
        if (zdo == null || !TraitStore.KeepsNativeLevel(zdo) || !TryGetStoredHealthMultiplier(zdo, out float multiplier)) return false;
        float expected = character.GetMaxHealthBase() * Math.Max(1, character.GetLevel()) * multiplier;
        return expected > 0f && !float.IsNaN(expected) && !float.IsInfinity(expected)
            && character.GetMaxHealth() == expected;
    }

    private static void ApplyHealthMultiplier(Character character, float multiplier)
    {
        if (multiplier <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
        {
            return;
        }

        float vanillaMaxHealth = character.GetMaxHealthBase() * Math.Max(1, character.GetLevel());
        float targetMaxHealth = vanillaMaxHealth * multiplier;
        if (targetMaxHealth <= 0f || float.IsNaN(targetMaxHealth) || float.IsInfinity(targetMaxHealth))
        {
            return;
        }

        float currentMaxHealth = character.GetMaxHealth();
        if (Mathf.Approximately(currentMaxHealth, targetMaxHealth))
        {
            return;
        }

        float currentHealth = character.GetHealth();
        float missingHealth = Mathf.Max(0f, currentMaxHealth - currentHealth);
        character.SetMaxHealth(targetMaxHealth);
        if (currentMaxHealth > 0f && currentHealth > 0f)
        {
            float minimumHealth = Mathf.Min(1f, targetMaxHealth);
            character.SetHealth(Mathf.Clamp(targetMaxHealth - missingHealth, minimumHealth, targetMaxHealth));
        }
    }

    internal static bool TryGetDamageMultiplier(Character character, out float multiplier)
    {
        multiplier = 1f;
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return false;
        }

        // The adopted ECR controller already owns native/star damage. Legacy CM saved level factors must not stack.
        var controller = character.GetComponent<ExtraPotions.Wildblood.Adopted.Runtime.EliteController>();
        if (controller != null && controller.Ready && !controller.KeepsNativeLevel) return false;

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null || !ReadLegacyLevelBool(zdo, DamageAppliedKey))
        {
            return false;
        }

        multiplier = Mathf.Max(0f, ReadLegacyLevelFloat(zdo, DamageMultiplierKey, 1f));
        return !Mathf.Approximately(multiplier, 1f);
    }

    /// <summary>Read-only source saved policy, restricted to imported native levels; no new stat selection/writer.</summary>
    internal static bool ReplacesVanillaLevelDamage(Character character)
    {
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer()) return false;
        var view = character.m_nview;
        var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
        return zdo != null && TraitStore.KeepsNativeLevel(zdo) && ReadLegacyLevelBool(zdo, DamageAppliedKey)
            && ReadLegacyLevelBool(zdo, DamageReplacesVanillaKey);
    }

    internal static bool TryGetStoredHealthMultiplier(ZDO zdo, out float multiplier)
    {
        multiplier = float.NaN;
        if (zdo == null || !ReadLegacyLevelBool(zdo, HealthAppliedKey))
        {
            return false;
        }

        multiplier = ReadLegacyLevelFloat(zdo, HealthMultiplierKey, float.NaN);
        return true;
    }

    private static string LegacyLevelKey(string key) => "CreatureManager_" + key.Substring("wildblood.cm.".Length);
    private static bool ReadLegacyLevelBool(ZDO zdo, string key) => zdo.GetBool(key, zdo.GetBool(LegacyLevelKey(key), false));
    private static float ReadLegacyLevelFloat(ZDO zdo, string key, float fallback) => zdo.GetFloat(key, zdo.GetFloat(LegacyLevelKey(key), fallback));

    internal static bool IsDungeonCreature(Character character)
    {
        return Character.InInterior(character.transform);
    }

    internal static bool TrySelectModifierChances(Character character, out ModifierChanceDefinition chances)
    {
        chances = new ModifierChanceDefinition();
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer() || !CreatureDomainManager.IsSynchronizedConfigurationReady())
        {
            return false;
        }

        if (!TryGetModifierRuleScope(character, out LevelRuleScope scope, out bool allowDistanceScaling))
        {
            return false;
        }

        LevelRuleSearch search = GetRuleSearch(character, scope);
        Dictionary<string, ModifierDefinition> modifiers = GetEffectiveModifierDefinitions(search);
        if (!CreatureModifierManager.ApplyModifierChances(chances, modifiers))
        {
            return false;
        }

        float distanceMultiplier = 1f;
        if (allowDistanceScaling && DistanceScalingOn())
        {
            TrySelectModifierDistanceScalingMultiplier(search, out distanceMultiplier);
        }

        foreach (string modifier in CreatureModifierManager.GetKnownModifierKeys())
        {
            float? chance = chances.Get(modifier);
            if (chance.HasValue)
            {
                chances.Set(modifier, Mathf.Clamp(chance.Value * distanceMultiplier, 0f, 100f));
            }
        }

        // Wildblood merges these two source effects into its signature mutations.
        // Explicit zeros prevent inherited source defaults from rolling a second minor effect.
        chances.Set("blink", 0f);
        chances.Set("corrosive", 0f);
        return true;
    }

    internal static bool TrySelectModifierPowers(Character character, out ModifierPowerDefinition powers)
    {
        powers = new ModifierPowerDefinition();
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer() || !CreatureDomainManager.IsSynchronizedConfigurationReady())
        {
            return false;
        }

        // Kept/inherited canonical data still needs actual source powers; this lookup never rolls a modifier.
        SpawnPolicy policy = GetSpawnPolicy(character);
        if (policy.ModifierMode == ModifierApplicationMode.Block) return false;
        LevelRuleScope scope = policy.ModifierScope;

        Dictionary<string, ModifierDefinition> modifiers =
            GetEffectiveModifierDefinitions(GetRuleSearch(character, scope));
        if (!CreatureModifierManager.ApplyModifierPowers(powers, modifiers))
        {
            return false;
        }

        foreach (string modifier in CreatureModifierManager.GetKnownModifierKeys())
        {
            float? power = powers.Get(modifier);
            if (power.HasValue)
            {
                powers.Set(modifier, CreatureModifierManager.ResolveModifierPower(modifier, power));
            }
        }

        return true;
    }

    private static Dictionary<string, ModifierDefinition> GetEffectiveModifierDefinitions(LevelRuleSearch search)
    {
        if (search.EffectiveModifiers != null)
        {
            return search.EffectiveModifiers;
        }

        List<Dictionary<string, ModifierDefinition>> layers = new();
        foreach (LevelRuleCandidate candidate in search.Candidates)
        {
            LevelDefinition rule = candidate.Definition;
            if (rule.ModifiersCleared)
            {
                break;
            }

            if (rule.Modifiers is { Count: > 0 })
            {
                layers.Add(rule.Modifiers);
            }
        }

        Dictionary<string, ModifierDefinition> effective = new(StringComparer.OrdinalIgnoreCase);
        for (int index = layers.Count - 1; index >= 0; index--)
        {
            foreach (KeyValuePair<string, ModifierDefinition> entry in layers[index])
            {
                if (entry.Value == null)
                {
                    continue;
                }

                if (!effective.TryGetValue(entry.Key, out ModifierDefinition current))
                {
                    effective[entry.Key] = entry.Value.Clone();
                    continue;
                }

                current.OverlayFrom(entry.Value);
            }
        }

        search.EffectiveModifiers = effective;
        return effective;
    }

    private static LevelRuleSearch GetRuleSearch(Character character, LevelRuleScope scope)
    {
        int frame = Time.frameCount;
        if (CachedRuleSearch != null &&
            CachedRuleSearchFrame == frame &&
            CachedRuleSearchScope == scope &&
            CachedRuleSearchCharacter == character)
        {
            return CachedRuleSearch;
        }

        LevelDefinition[] definitions;
        lock (Sync)
        {
            definitions = ActiveDefinitions;
        }

        LevelRuleContext context = LevelRuleContext.From(character);
        List<LevelRuleCandidate> candidates = new(definitions.Length);
        for (int index = 0; index < definitions.Length; index++)
        {
            if (TryMatchRule(definitions[index], context, index, scope, out LevelRuleCandidate candidate))
            {
                candidates.Add(candidate);
            }
        }

        candidates.Sort((left, right) => right.CompareTo(left));
        CachedRuleSearchCharacter = character;
        CachedRuleSearchScope = scope;
        CachedRuleSearchFrame = frame;
        CachedRuleSearch = new LevelRuleSearch(context, candidates);
        return CachedRuleSearch;
    }

    private static bool DistanceScalingOn() => Plugin.Settings != null && Plugin.Settings.Get(WildbloodSettings.Stars.DistanceScaling);

    private static ExtraPotions.Wildblood.Stars.SpawnSource ToCore(CreatureSpawnSourceKind source) => source switch
    {
        CreatureSpawnSourceKind.Command => ExtraPotions.Wildblood.Stars.SpawnSource.Command,
        CreatureSpawnSourceKind.PlayerSummon => ExtraPotions.Wildblood.Stars.SpawnSource.PlayerSummon,
        CreatureSpawnSourceKind.Breeding => ExtraPotions.Wildblood.Stars.SpawnSource.Breeding,
        CreatureSpawnSourceKind.Egg => ExtraPotions.Wildblood.Stars.SpawnSource.Egg,
        CreatureSpawnSourceKind.Growup => ExtraPotions.Wildblood.Stars.SpawnSource.Growup,
        CreatureSpawnSourceKind.TamedRestore => ExtraPotions.Wildblood.Stars.SpawnSource.TamedRestore,
        _ => ExtraPotions.Wildblood.Stars.SpawnSource.Natural
    };

    /// <summary>
    /// The damage and health multipliers the matching rule's <c>distanceScaling</c> band gives this creature at its position now
    /// (CM TrySelectDistanceScalingMultiplier): false while Distance Scaling is off, for protected prefabs, for sources that keep
    /// their stats, or when no rule carries a band. Meant to be read once at the owner's roll and stored with the creature.
    /// </summary>
    internal static bool TrySelectStatDistanceScaling(Character character, out float damage, out float health)
    {
        damage = health = 1f;
        if (character == null || character.IsPlayer() || !Plugin.NativeRulesReady || !NativeModifierRules.Ready || IsProtectedPrefab(character))
        {
            return false;
        }

        CreatureSpawnSourceKind source = CreatureManagerSpawnLifecycle.GetSpawnSource(character);
        if (!ExtraPotions.Wildblood.Stars.DistanceScaling.Active(DistanceScalingOn(), ToCore(source)))
        {
            return false;
        }

        LevelRuleSearch search = GetRuleSearch(character, source == CreatureSpawnSourceKind.Command ? LevelRuleScope.BaselineOnly : LevelRuleScope.Full);
        bool found = false;
        foreach (int index in new[] { ExtraPotions.Wildblood.Stars.DistanceScaling.Damage, ExtraPotions.Wildblood.Stars.DistanceScaling.Health })
        {
            foreach (LevelRuleCandidate candidate in search.Candidates)
            {
                List<float>? band = candidate.Definition.DistanceScaling;
                if (band == null || band.Count <= index || band[index] <= 0f)
                {
                    continue;
                }

                float multiplier = ExtraPotions.Wildblood.Stars.DistanceScaling.Multiplier(search.Context.Distance, band, index);
                if (index == ExtraPotions.Wildblood.Stars.DistanceScaling.Damage) damage = multiplier; else health = multiplier;
                found = true;
                break;
            }
        }

        return found;
    }

    private static bool TrySelectModifierDistanceScalingMultiplier(LevelRuleSearch search, out float multiplier)
    {
        multiplier = 1f;
        foreach (LevelRuleCandidate candidate in search.Candidates)
        {
            LevelDefinition rule = candidate.Definition;
            if (!HasModifierDistanceScalingValue(rule.ModifierDistanceScaling))
            {
                continue;
            }

            multiplier = GetModifierDistanceScalingMultiplier(search.Context.Distance, rule.ModifierDistanceScaling);
            return true;
        }

        return false;
    }

    private static bool HasModifierDistanceScalingValue(List<float>? scaling)
    {
        return scaling is { Count: 3 };
    }

    private static bool TryMatchRule(LevelDefinition definition, LevelRuleContext context, int index, LevelRuleScope scope, out LevelRuleCandidate candidate)
    {
        candidate = new LevelRuleCandidate(definition, 0, index);
        string target = (definition.Target ?? "").Trim();
        if (target.Length == 0)
        {
            return false;
        }

        bool regularBoss = context.IsBoss && !context.IsEnforcer;
        if (scope == LevelRuleScope.BaselineOnly &&
            (regularBoss ? !IsBossTarget(target) : !IsGlobalTarget(target)))
        {
            return false;
        }

        string biome = (definition.Biome ?? "").Trim();
        bool hasBiomeCondition = biome.Length > 0;
        if (hasBiomeCondition && !MatchesBiome(biome, context.Biome))
        {
            return false;
        }

        int biomeSpecificity = hasBiomeCondition ? 50 : 0;
        if (IsGlobalTarget(target))
        {
            if (regularBoss)
            {
                return false;
            }

            candidate = new LevelRuleCandidate(definition, biomeSpecificity, index);
            return true;
        }

        if (IsBossTarget(target))
        {
            if (!regularBoss)
            {
                return false;
            }

            candidate = new LevelRuleCandidate(definition, 150 + biomeSpecificity, index);
            return true;
        }

        if (string.Equals(target, context.PrefabName, StringComparison.OrdinalIgnoreCase))
        {
            candidate = new LevelRuleCandidate(definition, 300 + biomeSpecificity, index);
            return true;
        }

        List<string>? prefabs = definition.Prefabs;
        if (prefabs is { Count: > 0 })
        {
            bool matchedPrefab = false;
            foreach (string prefab in prefabs)
            {
                if (!string.Equals(prefab, context.PrefabName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchedPrefab = true;
                break;
            }

            if (!matchedPrefab)
            {
                return false;
            }

            candidate = new LevelRuleCandidate(definition, 200 + biomeSpecificity, index);
            return true;
        }

        if ((context.IsEnforcer || !context.IsBoss) && MatchesBiome(target, context.Biome))
        {
            candidate = new LevelRuleCandidate(definition, 100, index);
            return true;
        }

        return false;
    }

    private static bool IsGlobalTarget(string target)
    {
        return string.Equals(target, "global", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBossTarget(string target)
    {
        return string.Equals(target, "boss", StringComparison.OrdinalIgnoreCase);
    }

    private static float GetModifierDistanceScalingMultiplier(float distance, List<float>? scaling)
    {
        if (scaling is not { Count: 3 })
        {
            return 1f;
        }

        float perStep = Mathf.Max(0f, scaling[0]);
        float interval = scaling[1] > 0f ? scaling[1] : 1000f;
        int steps = Mathf.FloorToInt(distance / interval);
        if (scaling[2] > 0f)
        {
            steps = Math.Min(steps, Mathf.FloorToInt(scaling[2]));
        }

        return Mathf.Max(0f, 1f + steps * perStep);
    }

    private static Heightmap.Biome GetBiome(Vector3 position)
    {
        if (WorldGenerator.instance == null)
        {
            return Heightmap.Biome.None;
        }

        try
        {
            return WorldGenerator.instance.GetBiome(position);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogDebug($"Failed to resolve biome for level rule at {position}: {ex.Message}");
            return Heightmap.Biome.None;
        }
    }

    private static float GetHorizontalDistance(Vector3 position)
    {
        return Mathf.Sqrt(position.x * position.x + position.z * position.z);
    }

    private static string NormalizeBiomeName(string biomeName)
    {
        return new string((biomeName ?? "")
            .Where(character => !char.IsWhiteSpace(character) && character != '_' && character != '-')
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static bool MatchesBiome(string biomeName, Heightmap.Biome currentBiome)
    {
        string normalized = NormalizeBiomeName(biomeName);
        if (normalized.Length == 0)
        {
            return false;
        }

        if (CreatureKarmaManager.TryResolveBiomeName(biomeName, out Heightmap.Biome parsedBiome))
        {
            return (currentBiome & parsedBiome) != 0;
        }

        if (NormalizeBiomeName(currentBiome.ToString()) == normalized)
        {
            return true;
        }

        return CreatureKarmaManager.TryGetBiomeDisplayName(currentBiome, out string displayName) &&
               NormalizeBiomeName(displayName) == normalized;
    }

    private readonly struct LevelRuleContext
    {
        internal string PrefabName { get; }
        internal Heightmap.Biome Biome { get; }
        internal float Distance { get; }
        internal bool IsBoss { get; }
        internal bool IsEnforcer { get; }

        private LevelRuleContext(string prefabName, Heightmap.Biome biome, float distance, bool isBoss, bool isEnforcer)
        {
            PrefabName = prefabName;
            Biome = biome;
            Distance = distance;
            IsBoss = isBoss;
            IsEnforcer = isEnforcer;
        }

        internal static LevelRuleContext From(Character character)
        {
            return new LevelRuleContext(
                GetPrefabName(character.gameObject),
                GetBiome(character.transform.position),
                GetHorizontalDistance(character.transform.position),
                character.IsBoss(),
                CreatureKarmaManager.IsEnforcer(character));
        }
    }

    private readonly struct LevelRuleCandidate
    {
        internal LevelRuleCandidate(LevelDefinition definition, int specificity, int index)
        {
            Definition = definition;
            Specificity = specificity;
            Index = index;
        }

        internal LevelDefinition Definition { get; }
        private int Specificity { get; }
        private int Index { get; }

        internal int CompareTo(LevelRuleCandidate other)
        {
            int specificity = Specificity.CompareTo(other.Specificity);
            return specificity != 0 ? specificity : Index.CompareTo(other.Index);
        }
    }

    private sealed class LevelRuleSearch
    {
        internal LevelRuleSearch(LevelRuleContext context, List<LevelRuleCandidate> candidates)
        {
            Context = context;
            Candidates = candidates;
        }

        internal LevelRuleContext Context { get; }
        internal List<LevelRuleCandidate> Candidates { get; }
        internal Dictionary<string, ModifierDefinition>? EffectiveModifiers { get; set; }
    }

    private static string GetPrefabName(GameObject gameObject)
    {
        string name = gameObject.name;
        int cloneIndex = name.IndexOf("(Clone)", StringComparison.Ordinal);
        if (cloneIndex >= 0)
        {
            name = name.Substring(0, cloneIndex);
        }

        return name.Trim();
    }
}
