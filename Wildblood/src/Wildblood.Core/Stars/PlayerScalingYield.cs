// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: who else owns the game's per-player difficulty fields, and when Wildblood's write yields to them.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>Why the per-player write stands down.</summary>
    public enum PlayerScalingYieldReason
    {
        None,

        /// <summary>A mod named by plugin GUID (CreatureManager, or one on the Yield Scaling To list) is installed.</summary>
        NamedMod,

        /// <summary>The fields changed since Wildblood's last write: some other mod writes them.</summary>
        ExternalWrite,
    }

    /// <summary>
    /// The mods that write Game.m_healthScalePerPlayer, m_damageScalePerPlayer and m_difficultyScaleMaxPlayers, by BepInEx plugin GUID:
    /// CreatureManager always (its Multiplayer Difficulty section is applied whatever its Karma mode), plus the server's own list.
    /// </summary>
    public static class PlayerScalingOwners
    {
        /// <summary>Plugin GUIDs that always own the fields, whatever the list says.</summary>
        public static IReadOnlyList<ModuleSource> Builtin { get; } = Array.AsReadOnly(new[] { ModuleStepAside.CreatureManager });

        /// <summary>The installed owners: the built-in ones, then the listed GUIDs. GUIDs match exactly; a listed GUID is its own display name.</summary>
        public static IReadOnlyList<ModuleSource> Installed(IEnumerable<string> installedGuids, string? configured)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            var installed = new HashSet<string>(installedGuids.Where(g => g != null), StringComparer.Ordinal);
            var found = new List<ModuleSource>();
            foreach (var source in Builtin)
                if (installed.Contains(source.Guid)) found.Add(source);
            foreach (var guid in NameList.Parse(configured))
                if (installed.Contains(guid) && found.All(f => f.Guid != guid)) found.Add(new ModuleSource(guid, guid));
            return found;
        }
    }

    /// <summary>
    /// <see cref="PlayerScalingOwners.Installed"/> for a per-frame caller: the answer is kept until the configured list or the
    /// number of installed plugins changes, so the GUID set is not rebuilt every frame.
    /// shortcut: a plugin swapped for another with the same count goes unseen; BepInEx loads plugins once at startup.
    /// </summary>
    public sealed class PlayerScalingOwnersCache
    {
        IReadOnlyList<ModuleSource>? owners;
        string? configured;
        int count = -1;

        public IReadOnlyList<ModuleSource> Installed(ICollection<string> installedGuids, string? configuredList)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            if (owners == null || count != installedGuids.Count || !string.Equals(configured, configuredList, StringComparison.Ordinal))
            {
                owners = PlayerScalingOwners.Installed(installedGuids, configuredList);
                configured = configuredList;
                count = installedGuids.Count;
            }
            return owners;
        }

        public void Reset() { owners = null; configured = null; count = -1; }
    }

    /// <summary>What one tick should do to the game's fields.</summary>
    public readonly struct PlayerScalingStep
    {
        public PlayerScalingStep(bool write, PlayerScalingValues values, PlayerScalingYieldReason startedYield)
        {
            Write = write;
            Values = values;
            StartedYield = startedYield;
        }

        /// <summary>True: set the game's fields to <see cref="Values"/>.</summary>
        public bool Write { get; }

        public PlayerScalingValues Values { get; }

        /// <summary>The reason Wildblood began yielding on this tick; <see cref="PlayerScalingYieldReason.None"/> otherwise.</summary>
        public PlayerScalingYieldReason StartedYield { get; }

        internal static PlayerScalingStep Nothing => new PlayerScalingStep(false, default, PlayerScalingYieldReason.None);
    }

    /// <summary>
    /// One Game object's per-player write. It captures the game's values before its first write, puts them back when the Pressure module
    /// stops writing, and stands down (leaving whatever is in the fields) when a named owner is installed or when the fields are no longer
    /// what it last wrote, meaning another mod wrote them. A yield after an external write lasts until the module is switched off or the Game goes.
    /// </summary>
    public sealed class PlayerScalingCoordinator
    {
        bool applied;
        PlayerScalingValues baseline;
        PlayerScalingValues last;

        /// <summary>Why the write stands down now.</summary>
        public PlayerScalingYieldReason Yielding { get; private set; }

        public bool Applied => applied;

        public PlayerScalingStep Step(bool moduleOn, bool namedOwnerInstalled, PlayerScalingValues current, PlayerScalingValues desired)
        {
            if (!moduleOn)
            {
                Yielding = PlayerScalingYieldReason.None;
                return TakeRestore(current);
            }
            if (namedOwnerInstalled)
            {
                var back = TakeRestore(current);
                return Yield(PlayerScalingYieldReason.NamedMod, back);
            }
            if (Yielding == PlayerScalingYieldReason.ExternalWrite) return PlayerScalingStep.Nothing;
            Yielding = PlayerScalingYieldReason.None;
            if (applied && !PlayerScaling.Same(current, last))
            {
                applied = false;
                return Yield(PlayerScalingYieldReason.ExternalWrite, PlayerScalingStep.Nothing);
            }
            if (!applied)
            {
                baseline = current;
                applied = true;
            }
            last = desired;
            return new PlayerScalingStep(true, desired, PlayerScalingYieldReason.None);
        }

        /// <summary>The captured values to put back, when Wildblood wrote them and nothing else has changed them since.</summary>
        public PlayerScalingStep Restore(PlayerScalingValues current) => TakeRestore(current);

        public void Reset()
        {
            applied = false;
            Yielding = PlayerScalingYieldReason.None;
        }

        PlayerScalingStep TakeRestore(PlayerScalingValues current)
        {
            bool restore = applied && PlayerScaling.Same(current, last);
            applied = false;
            return restore ? new PlayerScalingStep(true, baseline, PlayerScalingYieldReason.None) : PlayerScalingStep.Nothing;
        }

        PlayerScalingStep Yield(PlayerScalingYieldReason reason, PlayerScalingStep step)
        {
            bool began = Yielding != reason;
            Yielding = reason;
            return began ? new PlayerScalingStep(step.Write, step.Values, reason) : step;
        }
    }
}
