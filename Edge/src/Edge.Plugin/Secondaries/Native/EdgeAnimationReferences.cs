// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8.
// SecondaryAttackManager.Diagnostics.cs: animator trigger and compatible route names only; source algorithms retained.
// Changed by expDARE, 2026: Edge-owned reference path, authority/session/module guards, retry after failed metadata write.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using BepInEx;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Integration;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

internal static class EdgeAnimationReferences
{
    private static bool _animatorDumpWritten, _customAnimationDumpWritten;
    private static readonly List<string> PlayerAnimatorTriggers = new();
    private static Player? currentPlayer;
    private static long generation = -1;
    internal static void Tick()
    {
        var player = Player.m_localPlayer;
        if (!Plugin.Ready || !CruciblePlugin.Session.IsAuthority || !EdgeModules.Active(EdgeModule.Secondaries) || player == null)
        { Reset(); return; }
        if (currentPlayer != player || generation != CruciblePlugin.Session.Generation)
        { Reset(); currentPlayer = player; generation = CruciblePlugin.Session.Generation; }
        DumpPlayerAnimatorReferences(player);
        if (_animatorDumpWritten) DumpCustomAnimationReferences(player);
    }
    internal static void Reset()
    { currentPlayer = null; generation = -1; _animatorDumpWritten = _customAnimationDumpWritten = false; PlayerAnimatorTriggers.Clear(); }

    public static void DumpPlayerAnimatorReferences(Player player)
    {
        if (_animatorDumpWritten)
        {
            return;
        }

        try
        {
            Animator? animator = player.GetComponentsInChildren<Animator>(true).FirstOrDefault();
            if (animator == null)
            {
                return;
            }

            SortedSet<string> triggerLines = new(StringComparer.Ordinal);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger &&
                    !string.IsNullOrWhiteSpace(parameter.name))
                {
                    triggerLines.Add(parameter.name);
                }
            }

            PlayerAnimatorTriggers.Clear();
            foreach (string triggerLine in triggerLines)
            {
                PlayerAnimatorTriggers.Add(triggerLine);
            }

            WriteAnimationReferenceFile();
            _animatorDumpWritten = true;
        }
        catch (Exception exception)
        {
            SecondaryAttackWarningLog.WarnOnce("animation-reference", $"Failed to write {EdgeDataFiles.AnimationReferences}: {exception.Message}");
        }
    }

    public static void DumpCustomAnimationReferences(Player player)
    {
        if (_customAnimationDumpWritten)
        {
            return;
        }

        try
        {
            WriteAnimationReferenceFile();
            _customAnimationDumpWritten = true;
        }
        catch (Exception exception)
        {
            SecondaryAttackWarningLog.WarnOnce("animation-reference", $"Failed to write {EdgeDataFiles.AnimationReferences}: {exception.Message}");
        }
    }

    private static void WriteAnimationReferenceFile()
    {
        Directory.CreateDirectory(Path.Combine(Paths.ConfigPath, "Edge"));

        StringBuilder builder = new();
        builder.AppendLine("Edge animation reference metadata");
        builder.AppendLine("This file is informational only; Edge does not read it as config. No animation assets are exported.");
        builder.AppendLine();
        builder.AppendLine("[Vanilla Animations]");
        builder.AppendLine();
        builder.AppendLine("[Player Animator Triggers]");
        if (PlayerAnimatorTriggers.Count == 0)
        {
            builder.AppendLine("<empty>");
        }
        else
        {
            foreach (string trigger in PlayerAnimatorTriggers)
            {
                builder.AppendLine($"- {trigger}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("[Mod Animations]");
        builder.AppendLine();
        AppendAnimationReplaceManagerDump(builder);
        AppendPixMovementSlideDump(builder);

        File.WriteAllText(Path.Combine(Paths.ConfigPath, "Edge", EdgeDataFiles.AnimationReferences), builder.ToString());
    }

    private static void AppendAnimationReplaceManagerDump(StringBuilder builder)
    {
        const string typeName = "KG_Managers.AnimationReplaceManager";

        builder.AppendLine("[KG_Managers.AnimationReplaceManager]");
        Type? managerType = FindLoadedType(typeName);
        if (managerType == null)
        {
            builder.AppendLine("Loaded: false");
            builder.AppendLine();
            return;
        }

        AssemblyName assemblyName = managerType.Assembly.GetName();
        builder.AppendLine("Loaded: true");
        builder.AppendLine($"Assembly: {assemblyName.Name} {assemblyName.Version}");
        builder.AppendLine();

        AppendAnimationSetsDump(builder, managerType);
        builder.AppendLine();
    }

    private static void AppendPixMovementSlideDump(StringBuilder builder)
    {
        const string typeName = "Pix.Movement.Slide";

        builder.AppendLine("[Pix.Movement.Slide]");
        Type? slideType = FindLoadedType(typeName);
        if (slideType == null)
        {
            builder.AppendLine("Loaded: false");
            builder.AppendLine();
            return;
        }

        AssemblyName assemblyName = slideType.Assembly.GetName();
        builder.AppendLine("Loaded: true");
        builder.AppendLine($"Assembly: {assemblyName.Name} {assemblyName.Version}");

        object? triggerEntry = GetStaticFieldValue(slideType, "CfgAnimTriggerName");
        object? triggerValue = GetInstancePropertyValue(triggerEntry, "Value");
        if (triggerValue is string trigger && !string.IsNullOrWhiteSpace(trigger))
        {
            builder.AppendLine($"Trigger: {trigger}");
        }

        RuntimeAnimatorController? controller = GetStaticFieldValue(slideType, "_slideController") as RuntimeAnimatorController;
        builder.AppendLine();
        builder.AppendLine("[Animation Sets]");
        if (controller == null)
        {
            builder.AppendLine("<missing>");
            builder.AppendLine();
            return;
        }

        AppendStringList(builder, GetRuntimeAnimatorControllerClipNames(controller), "");
        builder.AppendLine();
    }

    private static void AppendAnimationSetsDump(StringBuilder builder, Type managerType)
    {
        builder.AppendLine("[Animation Sets]");
        object? value = GetStaticFieldValue(managerType, "AllAnimationSets");
        if (value is not IEnumerable sets)
        {
            builder.AppendLine("<missing>");
            return;
        }

        int index = 0;
        foreach (object? set in sets)
        {
            if (set is string)
            {
                continue;
            }

            IEnumerable<object> names = set is IEnumerable enumerable
                ? enumerable.Cast<object>()
                : Enumerable.Empty<object>();
            builder.AppendLine($"Set {index}:");
            AppendStringList(builder, names.Select(static name => name?.ToString() ?? "<null>"), "  ");
            index++;
        }

        if (index == 0)
        {
            builder.AppendLine("<empty>");
        }
    }

    private static void AppendStringList(StringBuilder builder, IEnumerable<string> values, string indent)
    {
        List<string> normalizedValues = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();
        if (normalizedValues.Count == 0)
        {
            builder.AppendLine($"{indent}<empty>");
            return;
        }

        foreach (string value in normalizedValues)
        {
            builder.AppendLine($"{indent}- {value}");
        }
    }

    private static object? GetStaticFieldValue(Type type, string fieldName)
    {
        FieldInfo? field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetValue(null);
    }

    private static object? GetInstancePropertyValue(object? instance, string propertyName)
    {
        if (instance == null)
        {
            return null;
        }

        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return property?.GetValue(instance);
    }

    private static IEnumerable<string> GetRuntimeAnimatorControllerClipNames(RuntimeAnimatorController controller)
    {
        return controller.animationClips
            .Where(static clip => clip != null)
            .Select(static clip => clip.name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(static name => name, StringComparer.Ordinal);
    }

    private static Type? FindLoadedType(string fullTypeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = assembly.GetType(fullTypeName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

}
