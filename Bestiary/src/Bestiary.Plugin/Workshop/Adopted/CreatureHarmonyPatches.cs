// Portions Copyright (C) sighsorry1029. CreatureManager 1.2.5, 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: workshop-only patches, module guards and owned installation. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

using ExtraPotions.Bestiary.Runtime.Workshop;
namespace ExtraPotions.Bestiary.Runtime.Workshop.Adopted;

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
            WorkshopHost.Log.LogWarning(
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
                WorkshopHost.Log.LogWarning(
                    $"Could not find optional Harmony target {declaringType.FullName}.{methodName}; " +
                    $"{feature} may be incomplete for this game version.");
            }

            return null;
        }

        string signatures = string.Join(
            ", ",
            matches.Select(method => $"({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})"));
        WorkshopHost.Log.LogWarning(
            $"Found {matches.Length} optional Harmony targets named {declaringType.FullName}.{methodName} " +
            $"with signatures {signatures}; refusing to select an overload implicitly, so {feature} is disabled.");
        return null;
    }
}

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
internal static class CreatureManagerFejdStartupAwakePatch
{
    private static void Postfix(FejdStartup __instance)
    {
        if (!WorkshopRuntime.Initialized) return;
        CreaturePrefabRegistry.CacheFejdStartup(__instance);
    }
}

[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
internal static class CreatureManagerZNetSceneAwakePatch
{
    private static void Prefix(ZNetScene __instance)
    {
        if (!WorkshopRuntime.Initialized) return;
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ZNetScene __instance)
    {
        if (!WorkshopRuntime.Initialized) return;
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);
        CreatureDomainManager.NotifyGameDataAvailable();
    }
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
internal static class CreatureManagerObjectDbAwakePatch
{
    private static void Prefix(ObjectDB __instance)
    {
        if (!WorkshopRuntime.Initialized) return;
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ObjectDB __instance)
    {
        if (!WorkshopRuntime.Initialized) return;
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);

        bool isFinalSerializedDatabase =
            __instance.GetComponent<FejdStartup>() == null &&
            __instance.m_items.Count > 0;
        CreatureDomainManager.NotifyGameDataAvailable(objectDbFinalized: isFinalSerializedDatabase);
    }
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
internal static class CreatureManagerObjectDbCopyOtherDbPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ObjectDB __instance)
    {
        if (!WorkshopRuntime.Initialized) return;
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);

        CreatureDomainManager.NotifyGameDataAvailable(objectDbFinalized: true);
    }
}

[HarmonyPatch(typeof(VisEquipment), "AttachItem")]
internal static class CreatureManagerVisEquipmentAttachItemPatch
{
    private static void Postfix(VisEquipment __instance, GameObject __result)
    {
        if (!WorkshopHost.Active) return;
        CreatureDomainManager.ScaleAttachedEquipmentVisual(__instance, __result);
    }
}

[HarmonyPatch(typeof(VisEquipment), "AttachArmor")]
internal static class CreatureManagerVisEquipmentAttachArmorPatch
{
    private static void Postfix(VisEquipment __instance, List<GameObject> __result)
    {
        if (!WorkshopHost.Active) return;
        CreatureDomainManager.ScaleAttachedEquipmentVisuals(__instance, __result);
    }
}

[HarmonyPatch]
internal static class CreatureManagerVisEquipmentUpdateEquipmentVisualsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(VisEquipment),
            "UpdateEquipmentVisuals",
            "ragdoll appearance and random hair visual attachment",
            Type.EmptyTypes);
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(VisEquipment __instance)
    {
        if (!WorkshopHost.Active) return;
        CreatureAppearanceRuntime.UpdateConfiguredRagdollAppearance(__instance);
        CreatureDomainManager.ApplyConfiguredRagdollTextures(__instance);
        CreatureAppearanceRuntime.RestoreEligibility(__instance);
        CreatureAppearanceRuntime.UpdateVisual(__instance);
    }
}

[HarmonyPatch]
internal static class CreatureManagerVisEquipmentGetHairItemPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(VisEquipment),
            "GetHairItem",
            "random hair helmet-visibility override",
            new[]
            {
                typeof(ItemDrop.ItemData.HelmetHairType),
                typeof(int),
                typeof(ItemDrop.ItemData.AccessoryType)
            });
        if (method != null)
        {
            yield return method;
        }
    }

    private static bool Prefix(
        VisEquipment __instance,
        int itemHash,
        ItemDrop.ItemData.AccessoryType accessory,
        ref int __result)
    {
        if (!WorkshopHost.Active) return true;
        if (!CreatureAppearanceRuntime.TryBypassHelmetHairFilter(
                __instance,
                itemHash,
                accessory,
                out int result))
        {
            return true;
        }

        __result = result;
        return false;
    }
}

[HarmonyPatch(typeof(VisEquipment), "OnEnable")]
internal static class CreatureManagerVisEquipmentOnEnableRandomHairPatch
{
    private static void Postfix(VisEquipment __instance)
    {
        if (!WorkshopHost.Active) return;
        CreatureAppearanceRuntime.UpdateConfiguredRagdollAppearance(__instance);
        CreatureDomainManager.ApplyConfiguredRagdollTextures(__instance);
        CreatureAppearanceRuntime.RestoreEligibility(__instance);
    }
}

[HarmonyPatch(typeof(VisEquipment), "OnDisable")]
internal static class CreatureManagerVisEquipmentOnDisableRandomHairPatch
{
    private static void Prefix(VisEquipment __instance)
    {
        if (!WorkshopHost.Active) return;
        CreatureDomainManager.ForgetRagdollTextureVisual(__instance);
        CreatureAppearanceRuntime.Forget(__instance);
    }
}

[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.Awake))]
internal static class CreatureManagerBaseAiAwakePatch
{
    private static void Postfix(BaseAI __instance)
    {
        if (!WorkshopHost.Active) return;
        CreatureFactionManager.SetupBaseAi(__instance);
    }
}

[HarmonyPatch(typeof(ZNetScene), "OnDestroy")]
internal static class CreatureManagerZNetSceneOnDestroyPatch
{
    private static void Prefix()
    {
        if (!WorkshopRuntime.Initialized) return;
        CreatureDomainManager.NotifyGameDataUnavailable();
        CreatureAppearanceRuntime.Reset();






    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Start))]
internal static class CreatureManagerHumanoidStartPatch
{
    private static void Postfix(Humanoid __instance)
    {
        if (!WorkshopHost.Active) return;
        CreatureAppearanceRuntime.Initialize(__instance);

    }
}

[HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
internal static class CreatureManagerHumanoidOnRagdollCreatedRandomHairPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Humanoid __instance, Ragdoll ragdoll)
    {
        if (!WorkshopHost.Active) return;
        CreatureAppearanceRuntime.ApplyToRagdoll(__instance, ragdoll);
    }
}

[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Setup))]
internal static class CreatureManagerRagdollSetupVisualScalePatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Ragdoll __instance)
    {
        if (!WorkshopHost.Active) return;
        CreatureDomainManager.ApplyConfiguredRagdollScale(__instance);
    }
}

[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), new[] { typeof(Character), typeof(Character) })]
internal static class CreatureManagerBaseAiIsEnemyPatch
{
    private static bool Prefix(Character a, Character b, ref bool __result)
    {
        if (!WorkshopHost.Active) return true;
        if (!CreatureFactionManager.TryIsEnemy(a, b, out bool isEnemy))
        {
            return true;
        }

        __result = isEnemy;
        return false;
    }
}
