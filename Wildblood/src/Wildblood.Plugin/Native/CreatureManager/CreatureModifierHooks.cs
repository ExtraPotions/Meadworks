// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473
// Changed by expDARE, 2026: selected combat/passive hooks, debuff status registration on ObjectDB load and Harmony runtime compatibility.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.AdoptedCm;

internal static class CreatureHarmonyTargetResolver
{
    internal static MethodInfo? FindDeclared(
        Type declaringType,
        string methodName,
        string feature,
        Type[]? argumentTypes = null,
        bool warnIfMissing = true)
    {
        if (argumentTypes == null)
        {
            return FindUniqueDeclared(declaringType, methodName, feature, warnIfMissing);
        }

        MethodInfo? method = AccessTools.DeclaredMethod(declaringType, methodName, argumentTypes);
        if (method == null && warnIfMissing)
        {
            string signature = $"({string.Join(", ", argumentTypes.Select(type => type.Name))})";
            CreatureManagerPlugin.Log.LogWarning(
                $"Could not find optional Harmony target {declaringType.FullName}.{methodName}{signature}; " +
                $"{feature} may be incomplete for this game version.");
        }

        return method;
    }

    internal static MethodInfo? FindUniqueDeclared(
        Type declaringType,
        string methodName,
        string feature,
        bool warnIfMissing = true)
    {
        MethodInfo[] matches = declaringType
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == methodName)
            .ToArray();
        if (matches.Length == 1)
        {
            return matches[0];
        }

        if (matches.Length == 0)
        {
            if (warnIfMissing)
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"Could not find optional Harmony target {declaringType.FullName}.{methodName}; " +
                    $"{feature} may be incomplete for this game version.");
            }

            return null;
        }

        string signatures = string.Join(
            ", ",
            matches.Select(method => $"({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})"));
        CreatureManagerPlugin.Log.LogWarning(
            $"Found {matches.Length} optional Harmony targets named {declaringType.FullName}.{methodName} " +
            $"with signatures {signatures}; refusing to select an overload implicitly, so {feature} is disabled.");
        return null;
    }
}

[HarmonyPatch]
internal static class CreatureManagerUndodgeableAttackScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string methodName in new[]
                 {
                     "DoMeleeAttack",
                     "DoAreaAttack",
                     "ProjectileAttackTriggered"
                 })
        {
            MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
                typeof(Attack),
                methodName,
                "Undodgeable attack handling",
                Type.EmptyTypes);
            if (method != null)
            {
                yield return method;
            }
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
        Attack __instance,
        MethodBase __originalMethod,
        out CreatureModifierManager.UndodgeableScopeState __state)
    {
        CreatureModifierManager.UndodgeableSourcePath sourcePath = __originalMethod.Name switch
        {
            "DoMeleeAttack" => CreatureModifierManager.UndodgeableSourcePath.Melee,
            "DoAreaAttack" => CreatureModifierManager.UndodgeableSourcePath.Area,
            _ => CreatureModifierManager.UndodgeableSourcePath.None
        };
        __state = CreatureModifierManager.BeginUndodgeableAttackScope(__instance, sourcePath);
    }

    [HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.UndodgeableScopeState __state)
    {
        CreatureModifierManager.EndUndodgeableScope(ref __state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerUndodgeableProjectileScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Projectile),
            "IsValidTarget",
            "Undodgeable projectile handling",
            new[] { typeof(IDestructible) });
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
        Projectile __instance,
        IDestructible destr,
        out CreatureModifierManager.UndodgeableScopeState __state)
    {
        __state = CreatureModifierManager.BeginUndodgeableProjectileScope(__instance, destr);
    }

    [HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.UndodgeableScopeState __state)
    {
        CreatureModifierManager.EndUndodgeableScope(ref __state);
        return __exception;
    }
}

[HarmonyPatch(
    typeof(Projectile),
    nameof(Projectile.OnHit),
    new[] { typeof(Collider), typeof(Vector3), typeof(bool), typeof(Vector3) })]
internal static class CreatureManagerVortexProjectileImpactScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(
        Projectile __instance,
        Collider collider,
        bool water,
        out CreatureModifierManager.VortexProjectileImpactScopeState __state)
    {
        __state = CreatureModifierManager.BeginVortexProjectileImpact(__instance, collider, water);
    }

    [HarmonyPriority(Priority.Last)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.VortexProjectileImpactScopeState __state)
    {
        CreatureModifierManager.EndVortexProjectileImpact(ref __state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerUndodgeableAoeScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Aoe),
            "ShouldHit",
            "Undodgeable area-attack handling",
            new[] { typeof(Collider) });
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
        Aoe __instance,
        Collider collider,
        out CreatureModifierManager.UndodgeableScopeState __state)
    {
        __state = CreatureModifierManager.BeginUndodgeableAoeScope(__instance, collider);
    }

    [HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.UndodgeableScopeState __state)
    {
        CreatureModifierManager.EndUndodgeableScope(ref __state);
        return __exception;
    }
}

// Upstream registered the player debuff status effects from these ObjectDB hooks; without them the debuffs had no
// status effect, so players never saw their icon, name or tooltip.
[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
internal static class CreatureManagerObjectDbAwakeStatusPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ObjectDB __instance) => CreatureModifierManager.RegisterStatusEffects(__instance);
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
internal static class CreatureManagerObjectDbCopyStatusPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ObjectDB __instance) => CreatureModifierManager.RegisterStatusEffects(__instance);
}

[HarmonyPatch(typeof(Player), nameof(Player.IsDodgeInvincible))]
internal static class CreatureManagerUndodgeableDodgeQueryPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Player __instance, ref bool __result)
    {
        CreatureModifierManager.ApplyUndodgeableDodgeOverride(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Damage), new[] { typeof(HitData) })]
internal static class CreatureManagerUndodgeableDamageScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(out CreatureModifierManager.UndodgeableDamageScopeState __state)
    {
        __state = CreatureModifierManager.BeginUndodgeableDamageScope();
    }

    [HarmonyPriority(Priority.Last)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.UndodgeableDamageScopeState __state)
    {
        CreatureModifierManager.EndUndodgeableDamageScope(ref __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Damage), new[] { typeof(HitData) })]
internal static class CreatureManagerVortexDirectProjectileDamagePatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
        Character __instance,
        HitData hit,
        out CreatureModifierManager.VortexDirectProjectileDamageState __state)
    {
        __state = CreatureModifierManager.PrepareVortexDirectProjectileDamage(__instance, hit);
    }

    [HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.VortexDirectProjectileDamageState __state)
    {
        CreatureModifierManager.RestoreVortexDirectProjectileDamage(ref __state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerUndodgeableDamageDispatchPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            nameof(Character.Damage),
            "Undodgeable damage dispatch",
            new[] { typeof(HitData) });
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(Character __instance, HitData hit)
    {
        CreatureModifierManager.ApplyUndodgeableBeforeDamage(__instance, hit);
    }
}

[HarmonyPatch(typeof(Character), "RPC_Damage")]
internal static class CreatureManagerVortexRpcDamageMarkerPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(HitData hit)
    {
        CreatureModifierManager.CaptureVortexProjectileDecision(hit);
    }
}

[HarmonyPatch(typeof(Character), "RPC_Damage")]
internal static class CreatureManagerCharacterRpcDamagePatch
{
    private static bool Prefix(
        Character __instance,
        ref HitData hit,
        out CreatureModifierManager.RpcDamageScopeState __state)
    {
        __state = CreatureModifierManager.BeginRpcDamageScope(__instance, hit);
        return CreatureModifierManager.ApplyDamageModifiers(__instance, hit);
    }

    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        FieldInfo? damageField = AccessTools.Field(typeof(HitData), nameof(HitData.m_damage));
        FieldInfo? poisonField = AccessTools.Field(typeof(HitData.DamageTypes), nameof(HitData.DamageTypes.m_poison));
        MethodInfo? capture = AccessTools.Method(
            typeof(CreatureModifierManager),
            nameof(CreatureModifierManager.CapturePostMitigationDelayedDamage));
        if (damageField == null || poisonField == null || capture == null)
        {
            CreatureManagerPlugin.Log.LogError(
                "Failed to resolve delayed-damage capture members; pure poison/fire/spirit modifier support is disabled.");
            return codes;
        }

        List<int> matches = new();
        for (int index = 0; index <= codes.Count - 4; index++)
        {
            if (codes[index].opcode == OpCodes.Ldarg_2 &&
                codes[index + 1].opcode == OpCodes.Ldflda &&
                Equals(codes[index + 1].operand, damageField) &&
                codes[index + 2].opcode == OpCodes.Ldc_R4 &&
                codes[index + 2].operand is float zero &&
                zero == 0f &&
                codes[index + 3].opcode == OpCodes.Stfld &&
                Equals(codes[index + 3].operand, poisonField))
            {
                matches.Add(index);
            }
        }

        if (matches.Count != 1)
        {
            CreatureManagerPlugin.Log.LogError(
                $"Expected one delayed-damage extraction point in Character.RPC_Damage, found {matches.Count}; pure poison/fire/spirit modifier support is disabled.");
            return codes;
        }

        int insertionIndex = matches[0];
        CodeInstruction loadTarget = new(OpCodes.Ldarg_0);
        codes[insertionIndex].MoveLabelsTo(loadTarget);
        loadTarget.blocks.AddRange(codes[insertionIndex].blocks);
        codes[insertionIndex].blocks.Clear();
        codes.InsertRange(
            insertionIndex,
            new[]
            {
                loadTarget,
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, capture)
            });
        return codes;
    }

    private static Exception? Finalizer(
        HitData hit,
        CreatureModifierManager.RpcDamageScopeState __state,
        Exception? __exception)
    {
        try
        {
            CreatureModifierManager.EndRpcDamageScope(__state);
        }
        finally
        {
            CreatureModifierManager.ClearTransientDamageContext(hit);
        }

        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerCharacterApplyPushbackPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            "ApplyPushback",
            "Juggernaut cooldown confirmation",
            new[] { typeof(HitData) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(Character __instance, HitData hit)
    {
        CreatureModifierManager.ConfirmKnockbackPush(__instance, hit);
    }
}

[HarmonyPatch]
internal static class CreatureManagerHumanoidBlockStaggerScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Humanoid),
            "BlockAttack",
            "Staggering block handling",
            new[] { typeof(HitData), typeof(Character) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(Character attacker, out CreatureModifierManager.BlockAttackModifierState __state)
    {
        __state = CreatureModifierManager.BeginBlockAttackModifierScope(attacker);
    }

    private static Exception? Finalizer(Exception? __exception, CreatureModifierManager.BlockAttackModifierState __state)
    {
        CreatureModifierManager.EndBlockAttackModifierScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerCharacterBlockStaggerDamagePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            "AddStaggerDamage",
            "Staggering block buildup",
            new[] { typeof(float), typeof(Vector3), typeof(HitData) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(ref float damage)
    {
        CreatureModifierManager.ApplyBlockStaggerBonus(ref damage);
    }
}

[HarmonyPatch(
    typeof(Character),
    nameof(Character.ApplyDamage),
    new[] { typeof(HitData), typeof(bool), typeof(bool), typeof(HitData.DamageModifier) })]
internal static class CreatureManagerCharacterApplyDamagePatch
{
    private static void Prefix(Character __instance, HitData hit, out CreatureModifierManager.ApplyDamageState __state)
    {
        __state = CreatureModifierManager.BeginApplyDamage(__instance, hit);
    }

    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        MethodInfo? getTotalDamage = AccessTools.Method(
            typeof(HitData),
            nameof(HitData.GetTotalDamage),
            Type.EmptyTypes);
        MethodInfo? resolveDeathward = AccessTools.Method(
            typeof(CreatureModifierManager),
            nameof(CreatureModifierManager.ResolveFinalDeathwardDamage),
            new[] { typeof(Character), typeof(HitData), typeof(float) });
        if (getTotalDamage == null || resolveDeathward == null)
        {
            CreatureManagerPlugin.Log.LogError(
                "Failed to resolve final-damage Deathward members; Deathward lethal prevention is disabled.");
            return codes;
        }

        List<int> matches = new();
        for (int index = 0; index <= codes.Count - 5; index++)
        {
            if (codes[index].opcode == OpCodes.Ldarg_1 &&
                codes[index + 1].Calls(getTotalDamage) &&
                codes[index + 2].opcode == OpCodes.Stloc_1 &&
                codes[index + 3].opcode == OpCodes.Ldloc_1 &&
                codes[index + 4].opcode == OpCodes.Ldc_R4 &&
                codes[index + 4].operand is float threshold &&
                threshold == 0.1f)
            {
                matches.Add(index + 3);
            }
        }

        if (matches.Count != 1)
        {
            CreatureManagerPlugin.Log.LogError(
                $"Expected one final-damage checkpoint in Character.ApplyDamage, found {matches.Count}; Deathward lethal prevention is disabled.");
            return codes;
        }

        int insertionIndex = matches[0];
        CodeInstruction loadTarget = new(OpCodes.Ldarg_0);
        codes[insertionIndex].MoveLabelsTo(loadTarget);
        loadTarget.blocks.AddRange(codes[insertionIndex].blocks);
        codes[insertionIndex].blocks.Clear();
        codes.InsertRange(
            insertionIndex,
            new[]
            {
                loadTarget,
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, resolveDeathward),
                new CodeInstruction(OpCodes.Stloc_1)
            });
        return codes;
    }

    private static void Postfix(Character __instance, HitData hit, CreatureModifierManager.ApplyDamageState __state)
    {
        CreatureModifierManager.CompleteApplyDamage(__instance, hit, __state);
    }

    private static Exception? Finalizer(HitData hit, Exception? __exception)
    {
        if (__exception != null)
        {
            CreatureModifierManager.ClearTransientDamageContext(hit);
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(SE_Poison), nameof(SE_Poison.AddDamage), new[] { typeof(float) })]
internal static class CreatureManagerPoisonSourcePatch
{
    private static void Prefix(SE_Poison __instance, float damage, out bool __state)
    {
        __state = damage >= __instance.m_damageLeft;
    }

    private static void Postfix(SE_Poison __instance, bool __state)
    {
        CreatureModifierManager.RecordPoisonDamageSource(__instance, __state);
    }
}

[HarmonyPatch(typeof(SE_Burning), nameof(SE_Burning.AddFireDamage), new[] { typeof(float) })]
internal static class CreatureManagerFireSourcePatch
{
    private static void Prefix(SE_Burning __instance, out bool __state)
    {
        __state = __instance.m_fireDamageLeft <= 0f;
    }

    private static void Postfix(SE_Burning __instance, bool __state, bool __result)
    {
        CreatureModifierManager.RecordFireDamageSource(__instance, __state, __result);
    }
}

[HarmonyPatch(typeof(SE_Burning), nameof(SE_Burning.AddSpiritDamage), new[] { typeof(float) })]
internal static class CreatureManagerSpiritSourcePatch
{
    private static void Prefix(SE_Burning __instance, out bool __state)
    {
        __state = __instance.m_spiritDamageLeft <= 0f;
    }

    private static void Postfix(SE_Burning __instance, bool __state, bool __result)
    {
        CreatureModifierManager.RecordSpiritDamageSource(__instance, __state, __result);
    }
}

[HarmonyPatch(typeof(SE_Poison), nameof(SE_Poison.UpdateStatusEffect), new[] { typeof(float) })]
internal static class CreatureManagerPoisonTickScopePatch
{
    private static void Prefix(
        SE_Poison __instance,
        out CreatureModifierManager.DelayedDamageTickScopeState __state)
    {
        __state = CreatureModifierManager.BeginPoisonDamageTick(__instance);
    }

    private static Exception? Finalizer(
        Exception? __exception,
        CreatureModifierManager.DelayedDamageTickScopeState __state)
    {
        CreatureModifierManager.EndDelayedDamageTick(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(SE_Burning), nameof(SE_Burning.UpdateStatusEffect), new[] { typeof(float) })]
internal static class CreatureManagerBurningTickScopePatch
{
    private static void Prefix(
        SE_Burning __instance,
        out CreatureModifierManager.DelayedDamageTickScopeState __state)
    {
        __state = CreatureModifierManager.BeginBurningDamageTick(__instance);
    }

    private static Exception? Finalizer(
        Exception? __exception,
        CreatureModifierManager.DelayedDamageTickScopeState __state)
    {
        CreatureModifierManager.EndDelayedDamageTick(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Heal))]
internal static class CreatureManagerCharacterHealPatch
{
    private static void Prefix(Character __instance, ref float __0)
    {
        CreatureModifierManager.ApplyPlayerHealingDebuff(__instance, ref __0);
    }

    private static void Postfix(Character __instance)
    {
        CreatureModifierManager.ClearRecoveredDelayedDamageDeathCredit(__instance);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.SetHealth), new[] { typeof(float) })]
internal static class CreatureManagerCharacterSetHealthPatch
{
    private static void Postfix(Character __instance)
    {
        CreatureModifierManager.ClearRecoveredDelayedDamageDeathCredit(__instance);
    }
}

[HarmonyPatch]
internal static class CreatureManagerPlayerControlDebuffUpdatePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? update = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Player),
            "Update",
            "player control debuffs",
            Type.EmptyTypes);
        if (update != null)
        {
            yield return update;
        }
    }

    private static void Postfix(Player __instance)
    {
        if (CreatureModifierManager.ShouldUpdatePlayerControlDebuffs(__instance))
        {
            CreatureModifierManager.UpdatePlayerControlDebuffs(__instance);
        }
    }
}

[HarmonyPatch(typeof(Character), "OnDeath")]
internal static class CreatureManagerCharacterOnDeathPatch
{
    private static void Prefix(Character __instance)
    {
        try
        {
            CreatureKarmaManager.DropStoredEnforcerLoot(__instance);
            CreatureModifierManager.FinalDeathAttribution attribution = CreatureModifierManager.CaptureFinalDeathAttribution(__instance);
            CreatureModifierManager.HandleDeath(__instance, attribution);
            if (!__instance.IsPlayer()) CreatureKarmaManager.RecordDeath(__instance, attribution);
        }
        catch (Exception error) { ExtraPotions.Wildblood.AdoptedLibs.PatchGuard.Guard.Report(error, "Wildblood death effects"); }
    }
}

[HarmonyPatch]
internal static class CreatureManagerCharacterModifierUpdatePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            "CustomFixedUpdate",
            "passive modifier updates",
            new[] { typeof(float) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(Character __instance)
    {
        if (!CreatureModifierManager.NeedsPassiveModifierUpdate(__instance))
        {
            return;
        }

        CreatureModifierManager.UpdatePassiveModifiers(__instance);
    }
}

[HarmonyPatch]
internal static class CreatureManagerPlayerGetBodyArmorPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Player),
            "GetBodyArmor",
            "Armor Piercing",
            Type.EmptyTypes);
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(ref float __result)
    {
        CreatureModifierManager.ApplyArmorPiercing(ref __result);
    }
}

[HarmonyPatch]
internal static class CreatureManagerCharacterAnimEventSpeedPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(CharacterAnimEvent),
            "CustomFixedUpdate",
            "Attack Speed animation updates",
            new[] { typeof(float) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(CharacterAnimEvent __instance)
    {
        CreatureModifierManager.ApplyAttackAnimationSpeed(__instance);
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetTimeSinceLastAttack))]
internal static class CreatureManagerHumanoidGetTimeSinceLastAttackPatch
{
    private static void Postfix(Humanoid __instance, ref float __result)
    {
        CreatureModifierManager.ApplyMinimumAttackIntervalSpeed(__instance, ref __result);
    }
}

[HarmonyPatch]
internal static class CreatureManagerMonsterAiBlamerFleePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(MonsterAI),
            nameof(MonsterAI.UpdateAI),
            "Blamer flee behavior",
            new[] { typeof(float) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(MonsterAI __instance, out CreatureModifierManager.BlamerFleeOverrideState __state)
    {
        __state = CreatureModifierManager.BeginBlamerFleeOverride(__instance);
    }

    private static Exception? Finalizer(Exception? __exception, CreatureModifierManager.BlamerFleeOverrideState __state)
    {
        CreatureModifierManager.EndBlamerFleeOverride(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerMonsterAiBlinkAlertPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(MonsterAI),
            nameof(MonsterAI.SetAlerted),
            "Blink alert grace-period tracking",
            new[] { typeof(bool) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(MonsterAI __instance, out bool __state)
    {
        __state = __instance.IsAlerted();
    }

    private static void Postfix(MonsterAI __instance, bool __state)
    {
        CreatureModifierManager.HandleBlinkAlertStateChanged(__instance, __state);
    }
}

[HarmonyPatch]
internal static class CreatureManagerMonsterAiBlinkRangePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(MonsterAI),
            nameof(MonsterAI.UpdateAI),
            "Blink attack range handling",
            new[] { typeof(float) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(MonsterAI __instance, out CreatureModifierManager.BlinkAttackAiOverrideState? __state)
    {
        __state = CreatureModifierManager.BeginBlinkAttackAiOverride(__instance);
    }

    private static Exception? Finalizer(Exception? __exception, CreatureModifierManager.BlinkAttackAiOverrideState? __state)
    {
        CreatureModifierManager.EndBlinkAttackAiOverride(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerAttackStartSpeedPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Attack),
            "Start",
            "Blink and Attack Speed attack-start handling",
            new[]
            {
                typeof(Humanoid),
                typeof(Rigidbody),
                typeof(ZSyncAnimation),
                typeof(CharacterAnimEvent),
                typeof(VisEquipment),
                typeof(ItemDrop.ItemData),
                typeof(Attack),
                typeof(float),
                typeof(float)
            });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(Humanoid character, ItemDrop.ItemData weapon, bool __result)
    {
        CreatureModifierManager.TryBlinkOnAttackStart(character, weapon, __result);
        CreatureModifierManager.ApplyAttackIntervalSpeed(character, weapon, __result);
    }
}

[HarmonyPatch(typeof(Player), "OnDeath")]
internal static class CreatureManagerPlayerOnDeathPatch
{
    private static void Prefix(Player __instance)
    {
        try { CreatureModifierManager.HandlePlayerDeath(__instance); }
        catch (Exception error) { ExtraPotions.Wildblood.AdoptedLibs.PatchGuard.Guard.Report(error, "Wildblood player death effects"); }
    }

    private static void Postfix(Player __instance)
    {
        try { CreatureKarmaManager.RecordDeath(__instance); }
        catch (Exception error) { ExtraPotions.Wildblood.AdoptedLibs.PatchGuard.Guard.Report(error, "Wildblood player death pressure"); }
    }
}

[HarmonyPatch]
internal static class CreatureManagerPlayerOnRespawnPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Player),
            nameof(Player.OnRespawn),
            "Reaping replay reset after player respawn",
            Type.EmptyTypes);
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(Player __instance)
    {
        CreatureModifierManager.NotifyPlayerRespawn(__instance);
    }
}

