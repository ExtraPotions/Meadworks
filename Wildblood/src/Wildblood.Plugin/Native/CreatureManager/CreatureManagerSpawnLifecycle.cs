// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473
// Changed by expDARE, 2026: Wildblood owner-only canonical source keys; level resolution remains in Wildblood Start.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CoreSpawn = ExtraPotions.Wildblood.Stars.SpawnSource;
namespace ExtraPotions.Wildblood.AdoptedCm;
internal enum CreatureSpawnSourceKind
{
    Unknown = 0,
    Managed = 1,
    Command = 2,
    PlayerSummon = 3,
    Breeding = 4,
    Egg = 5,
    Growup = 6,
    TamedRestore = 7
}

internal static class CreatureManagerSpawnLifecycle
{
    private const string SpawnSourceKey = "CreatureManager_SpawnSource";
    private static readonly List<Character> SpawnedCharacters = new();
    private static readonly HashSet<int> ManagedSpawnCharacterIds = new();
    private static readonly HashSet<int> CommandSpawnCharacterIds = new();
    private static int SpawnContextDepth;
    private static readonly Stack<CreatureSpawnSourceKind> SourceContexts = new();

    internal static bool ShouldTrackCommandSpawn(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string command = text.TrimStart().Split(new[] { ' ' }, StringSplitOptions.None)[0].ToLowerInvariant();
        return command.StartsWith("spawn", StringComparison.Ordinal);
    }

    internal static void BeginSpawnContext()
    {
        if (SpawnContextDepth == 0)
        {
            SpawnedCharacters.Clear();
        }

        SpawnContextDepth++;
    }

    internal static void BeginCommandSpawnContext()
    {
        BeginSourceContext(CreatureSpawnSourceKind.Command);
    }

    internal static void EndCommandSpawnContext()
    {
        EndSourceContext();
    }

    internal static void BeginSourceContext(CreatureSpawnSourceKind source)
    {
        SourceContexts.Push(source);
    }

    internal static void EndSourceContext()
    {
        if (SourceContexts.Count > 0)
        {
            SourceContexts.Pop();
        }
    }

    internal static void EndSpawnContext()
    {
        if (SpawnContextDepth <= 0)
        {
            return;
        }

        SpawnContextDepth--;
        if (SpawnContextDepth > 0)
        {
            return;
        }

        try
        {
            foreach (Character character in SpawnedCharacters.Where(character => character != null).Distinct())
            {
                CreatureKarmaManager.ObservePotentialBlocker(character);
                CreatureSpawnBlocker.ObserveCharacter(character);
            }
        }
        finally
        {
            SpawnedCharacters.Clear();
            CreatureSpawnBlocker.InvalidateQueries();
        }
    }

    internal static void RecordAwake(Character character)
    {
        if (character == null)
        {
            return;
        }

        if (SourceContexts.Count > 0)
        {
            MarkSpawnSource(character, SourceContexts.Peek());
            return;
        }

        if (SpawnContextDepth <= 0)
        {
            return;
        }

        SpawnedCharacters.Add(character);
        ManagedSpawnCharacterIds.Add(character.GetInstanceID());
    }

    internal static bool IsManagedSpawn(Character character)
    {
        return character != null && ManagedSpawnCharacterIds.Contains(character.GetInstanceID());
    }

    internal static bool IsCommandSpawn(Character character)
    {
        return GetSpawnSource(character) == CreatureSpawnSourceKind.Command;
    }

    internal static CreatureSpawnSourceKind GetSpawnSource(Character character)
    {
        if (character == null)
        {
            return CreatureSpawnSourceKind.Unknown;
        }

        if (CommandSpawnCharacterIds.Contains(character.GetInstanceID()))
        {
            return CreatureSpawnSourceKind.Command;
        }

        ZNetView? nview = character.m_nview;
        if (nview != null && nview.IsValid())
        {
            ZDO zdo = nview.GetZDO();
            if (zdo != null)
            {
                CreatureSpawnSourceKind stored = GetSpawnSource(zdo);
                if (stored != CreatureSpawnSourceKind.Unknown)
                {
                    return stored;
                }
            }
        }

        if (IsManagedSpawn(character))
        {
            return CreatureSpawnSourceKind.Managed;
        }

        return character.IsTamed()
            ? CreatureSpawnSourceKind.TamedRestore
            : CreatureSpawnSourceKind.Unknown;
    }

    internal static CreatureSpawnSourceKind GetSpawnSource(ZDO zdo)
    {
        if (zdo == null)
        {
            return CreatureSpawnSourceKind.Unknown;
        }

        int canonical = zdo.GetInt(ExtraPotions.Wildblood.Adopted.Traits.TraitKeys.Source, -1);
        if (canonical >= 0 && Enum.IsDefined(typeof(CoreSpawn), canonical)) return FromWildblood((CoreSpawn)canonical);
        int stored = zdo.GetInt(SpawnSourceKey, 0);
        return stored != 0 && Enum.IsDefined(typeof(CreatureSpawnSourceKind), stored)
            ? (CreatureSpawnSourceKind)stored
            : CreatureSpawnSourceKind.Unknown;
    }

    internal static IEnumerator WrapSourceContext(IEnumerator inner, CreatureSpawnSourceKind source)
    {
        while (true)
        {
            object current;
            BeginSourceContext(source);
            try
            {
                if (!inner.MoveNext())
                {
                    yield break;
                }

                current = inner.Current;
            }
            finally
            {
                EndSourceContext();
            }

            yield return current;
        }
    }

    internal static bool IsBloodMagicItem(ItemDrop.ItemData? item)
    {
        try
        {
            return item?.m_shared != null && item.m_shared.m_skillType == Skills.SkillType.BloodMagic;
        }
        catch
        {
            return false;
        }
    }

    internal static void ForgetCharacter(Character character)
    {
        if (character == null)
        {
            return;
        }

        int id = character.GetInstanceID();
        ManagedSpawnCharacterIds.Remove(id);
        CommandSpawnCharacterIds.Remove(id);
        SpawnedCharacters.RemoveAll(candidate => candidate == null || candidate == character);
    }

    internal static void ResetRuntimeState()
    {
        SpawnedCharacters.Clear();
        ManagedSpawnCharacterIds.Clear();
        CommandSpawnCharacterIds.Clear();
        SourceContexts.Clear();
        SpawnContextDepth = 0;
    }

    private static void MarkSpawnSource(Character character, CreatureSpawnSourceKind source)
    {
        if (source == CreatureSpawnSourceKind.Command)
        {
            CommandSpawnCharacterIds.Add(character.GetInstanceID());
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            return;
        }

        if (zdo.IsOwner()) zdo.Set(ExtraPotions.Wildblood.Adopted.Traits.TraitKeys.Source, (int)ToWildblood(source));
    }

    private static CreatureSpawnSourceKind FromWildblood(CoreSpawn source) => source switch
    {
        CoreSpawn.Command => CreatureSpawnSourceKind.Command,
        CoreSpawn.PlayerSummon => CreatureSpawnSourceKind.PlayerSummon,
        CoreSpawn.Breeding => CreatureSpawnSourceKind.Breeding,
        CoreSpawn.Egg => CreatureSpawnSourceKind.Egg,
        CoreSpawn.Growup => CreatureSpawnSourceKind.Growup,
        CoreSpawn.TamedRestore => CreatureSpawnSourceKind.TamedRestore,
        CoreSpawn.Unknown => CreatureSpawnSourceKind.Unknown,
        _ => CreatureSpawnSourceKind.Managed
    };
    private static CoreSpawn ToWildblood(CreatureSpawnSourceKind source) => source switch
    {
        CreatureSpawnSourceKind.Command => CoreSpawn.Command,
        CreatureSpawnSourceKind.PlayerSummon => CoreSpawn.PlayerSummon,
        CreatureSpawnSourceKind.Breeding => CoreSpawn.Breeding,
        CreatureSpawnSourceKind.Egg => CoreSpawn.Egg,
        CreatureSpawnSourceKind.Growup => CoreSpawn.Growup,
        CreatureSpawnSourceKind.TamedRestore => CoreSpawn.TamedRestore,
        CreatureSpawnSourceKind.Managed => CoreSpawn.Natural,
        _ => CoreSpawn.Unknown
    };
}
