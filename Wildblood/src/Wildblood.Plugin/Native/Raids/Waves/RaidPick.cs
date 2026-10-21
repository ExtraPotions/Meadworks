// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Which raid comes (features/raids.md section 4.2): one of the game's own events (<c>RandEventSystem.instance.m_events</c>),
    /// so another mod's raids added there come too, that this world has unlocked by the game's own rules
    /// (<see cref="RaidUnlock"/>) and that brings at least one creature (<see cref="RaidCreatures"/>), drawn at random
    /// among all of them, wherever the base stands (decision 6: the raid is random among the unlocked ones). Called once,
    /// on the host's owner, as the raid is sounded (<see cref="RaidStart"/>), before anything is written; never throws.
    /// <para>
    /// The name players read is made from the raid's creatures rather than from a table, so another mod's raid is named
    /// too: the creature that comes most, in this machine's language, and "raid" ("Fuling raid"). The game's own start
    /// messages are mood lines ("The forest is moving..."), not names.
    /// </para>
    /// </summary>
    public static class RaidPick
    {
        private static readonly List<RandomEvent> Unlocked = new List<RandomEvent>();
        private static readonly List<int> Entries = new List<int>();

        /// <summary>The raid drawn for a host at <paramref name="at"/>; null when the world has unlocked none.</summary>
        public static RaidChoice? Draw(Vector3 at)
        {
            try
            {
                return DrawAt(at);
            }
            catch (Exception e)
            {
                Guard.Report(e, "raid draw");
                return null;
            }
        }

        private static RaidChoice? DrawAt(Vector3 at)
        {
            RandEventSystem? system = RandEventSystem.instance;
            if (system == null)
            {
                return null;
            }
            Collect(system, at);
            RandomEvent? ev = Unlocked.Count > 0 ? Unlocked[UnityEngine.Random.Range(0, Unlocked.Count)] : null;
            if (Log.Diagnostics)
            {
                Log.Diag($"raid draw at {at:F0}: {Unlocked.Count} unlocked; drew {ev?.m_name ?? "none"}");
            }
            Unlocked.Clear();
            return ev != null ? new RaidChoice(ev.m_name, NameOf(ev)) : null;
        }

        // Each event once, by name: another mod may list a second event under a name already taken, which the waves would
        // never reach (they look the event up by its name), so only the first of a name counts.
        private static void Collect(RandEventSystem system, Vector3 at)
        {
            Unlocked.Clear();
            foreach (RandomEvent ev in system.m_events)
            {
                if (ev == null || RaidCreatures.Event(ev.m_name) != ev || !RaidUnlock.Unlocked(ev, at) || !RaidCreatures.AnyComes(ev))
                {
                    continue;
                }
                Unlocked.Add(ev);
            }
        }

        // "Fuling raid": the creature that comes most, localized; its prefab's name where it has no name of its own.
        private static string NameOf(RandomEvent ev)
        {
            RaidCreatures.Entries(ev, Entries);
            SpawnSystem.SpawnData? face = Entries.Count > 0 ? ev.m_spawn[Entries[RaidCreatures.Commonest(ev, Entries)]] : null;
            Character? creature = face != null ? RaidCreatures.Creature(face) : null;
            string name = creature != null && global::Localization.instance != null ? global::Localization.instance.Localize(creature.m_name) : "";
            if (string.IsNullOrEmpty(name) || name.StartsWith("$", StringComparison.Ordinal))
            {
                name = face != null && face.m_prefab != null ? face.m_prefab.name : "";
            }
            return string.IsNullOrEmpty(name) ? "Raid" : name + " raid";
        }
    }
}
