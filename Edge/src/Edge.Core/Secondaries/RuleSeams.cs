// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Edge boundaries for adopted portable normalization. Main thread only, reset when Edge unloads.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Edge")]
[assembly: InternalsVisibleTo("Edge.Core.Tests")]
namespace ExtraPotions.Edge.Secondaries.Adopted;

internal readonly struct RotationOffset
{
    public readonly float X, Y, Z;
    public RotationOffset(float x, float y, float z) { X = x; Y = y; Z = z; }
    public static RotationOffset Zero => default;
}

internal static class RuleDiagnostics
{
    internal static Action<string>? OnInfo;
    internal static Action<string>? OnWarning;
    internal static Action<string>? OnError;
    internal static void Info(string text) => OnInfo?.Invoke(text);
    internal static void Warning(string text) => OnWarning?.Invoke(text);
    internal static void Error(string text) => OnError?.Invoke(text);
}

internal enum SecondaryAttackYamlDomainId { Ranged, Melee, BloodMagic }
internal sealed class SecondaryAttackYamlDomain
{
    internal SecondaryAttackYamlDomain(SecondaryAttackYamlDomainId id, string name) { Id = id; FileName = name; }
    internal SecondaryAttackYamlDomainId Id { get; }
    internal string FileName { get; }
}
internal static class SecondaryAttackYamlDomainRegistry
{
    internal const string RangedYamlFileName = "Edge.Ranged.yml";
    internal const string MeleeYamlFileName = "Edge.Melee.yml";
    internal const string BloodMagicYamlFileName = "Edge.BloodMagic.yml";
    internal static SecondaryAttackYamlDomain Get(SecondaryAttackYamlDomainId id) => new(id, "Edge." + id + ".yml");
}
internal sealed class SecondaryAttackYamlTexts
{
    private readonly IReadOnlyDictionary<SecondaryAttackYamlDomainId, string> texts;
    internal SecondaryAttackYamlTexts(IReadOnlyDictionary<SecondaryAttackYamlDomainId, string> texts) => this.texts = texts;
    internal string Get(SecondaryAttackYamlDomainId id) => texts.TryGetValue(id, out var text) ? text : "";
}
