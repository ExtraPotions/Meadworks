using System;
using System.Collections.Generic;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// The memory and profile of every chest Auto Tidy looks at, read and scored once and kept while nothing they
    /// depend on changed: the ZDO's data revision (contents or memory written), the revision the inventory was loaded
    /// at, the stow rules, the learned themes and, because settling and fading go on with the clock, at most
    /// <see cref="MaxAge"/> seconds (settling takes a day, fading days). A profile is scored only when a look asks for
    /// it, and the memory is read again only when the ZDO changed. So a base of quiet chests costs a dictionary lookup
    /// per neighbour, not a walk.
    /// </summary>
    internal static class TidyProfiles
    {
        private const float MaxAge = 300f;

        private sealed class Kept
        {
            public uint DataRevision;
            public uint LoadedRevision;
            public ItemGroups Groups;
            public int Themes;
            public float Built;
            public TidyMemory Memory;
            public TidyProfile Profile;
            public Container Chest;

            /// <summary>Scored on first use: learning reads only the memory.</summary>
            public TidyProfile Scored => Profile ?? (Profile = Build(Chest, Memory));
        }

        private static readonly Dictionary<Container, Kept> kept = new Dictionary<Container, Kept>();
        private static readonly PruneMark pruneMark = new PruneMark(256);

        /// <summary>World time now, in seconds; the clock every chest memory is written in.</summary>
        public static double Now => ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;

        /// <summary>In-game days a chest stays the home of an item after it ran out, the memory fading over that time.</summary>
        private const double MemoryDays = 3.0;

        /// <summary>Seconds of world time a memory takes to fade away.</summary>
        public static double FadeSeconds => MemoryDays * DaySeconds;

        /// <summary>Seconds of world time a newly arrived item takes to count fully: one in-game day.</summary>
        public static double SettleSeconds => DaySeconds;

        private static double DaySeconds => EnvMan.instance != null && EnvMan.instance.m_dayLengthSec > 0L ? EnvMan.instance.m_dayLengthSec : 1800.0;

        public static TidyProfile Of(Container container) => Get(container).Scored;

        public static TidyMemory MemoryOf(Container container) => Get(container).Memory;

        /// <summary>A profile from a memory in hand (the owner's, just updated), not kept.</summary>
        public static TidyProfile Build(Container container, TidyMemory memory) =>
            new TidyProfile(Held(container.GetInventory()), memory, Now, FadeSeconds, SettleSeconds);

        /// <summary>Stacks per prefab: each stack counts its part of a full stack, an item that does not stack counts one.</summary>
        public static Dictionary<string, float> Held(Inventory inventory)
        {
            Dictionary<string, float> held = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item == null || item.m_shared == null)
                    continue;
                string prefab = ItemNames.PrefabName(item);
                held.TryGetValue(prefab, out float stacks);
                held[prefab] = stacks + (float)item.m_stack / Mathf.Max(1, item.m_shared.m_maxStackSize);
            }
            return held;
        }

        /// <summary>
        /// The kept entry: the memory is read again only when the ZDO changed; a profile that only went stale (age, the
        /// inventory loaded again, rules or themes) is scored again from the memory in hand.
        /// </summary>
        private static Kept Get(Container container)
        {
            ZDO zdo = container.m_nview.GetZDO();
            if (kept.TryGetValue(container, out Kept entry) && entry.DataRevision == zdo.DataRevision)
            {
                if (!Fresh(entry, container))
                    Rescore(entry, container);
                return entry;
            }
            TidyMemory memory = TidyMemory.Read(zdo);
            entry = new Kept
            {
                DataRevision = zdo.DataRevision,
                LoadedRevision = container.m_lastRevision,
                Groups = StowRules.Groups,
                Themes = TidyThemes.Version,
                Built = Time.time,
                Memory = memory,
                Chest = container,
            };
            if (pruneMark.Due(kept.Count))
                Prune();
            kept[container] = entry;
            return entry;
        }

        private static bool Fresh(Kept entry, Container container) =>
            entry.LoadedRevision == container.m_lastRevision && entry.Themes == TidyThemes.Version
            && ReferenceEquals(entry.Groups, StowRules.Groups) && Time.time - entry.Built < MaxAge;

        private static void Rescore(Kept entry, Container container)
        {
            entry.LoadedRevision = container.m_lastRevision;
            entry.Groups = StowRules.Groups;
            entry.Themes = TidyThemes.Version;
            entry.Built = Time.time;
            entry.Profile = null;
        }

        private static void Prune()
        {
            List<Container> gone = new List<Container>();
            foreach (KeyValuePair<Container, Kept> pair in kept)
            {
                if (pair.Key == null || Time.time - pair.Value.Built > MaxAge)
                    gone.Add(pair.Key);
            }
            gone.ForEach(container => kept.Remove(container));
            pruneMark.Pruned(kept.Count);
        }
    }
}
