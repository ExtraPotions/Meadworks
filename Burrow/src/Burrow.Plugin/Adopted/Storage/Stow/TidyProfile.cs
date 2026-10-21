using System;
using System.Collections.Generic;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// What a chest is for, as Auto Tidy scores it. Every prefab weighs the stacks the chest holds or remembers
    /// (whichever is more), times how settled it is (15 % for an item that just arrived, growing to the whole over
    /// one in-game day), halved when it only ever arrived on its own (ground pickup, Auto Tidy) rather than by a
    /// player's hand. An item's share is the part of the chest's weight that is alike to it (<see cref="TidyThemes"/>).
    /// The "random items" score is one over the weighted mean of those shares: 1 for a chest of one kind, n for n
    /// unrelated kinds in equal parts. From <see cref="JunkScore"/> on, the chest is a junk chest, home to nothing,
    /// and everything in it may leave. Elsewhere an item with less than <see cref="StrayShare"/> is a stray. An item
    /// kept by a player (put back after Auto Tidy sent it away) is never a stray and always at home.
    /// </summary>
    internal sealed class TidyProfile
    {
        public const float StrayShare = 0.25f;
        public const float JunkScore = 5f;

        private const float NewWeight = 0.15f;
        private const float OnItsOwn = 0.5f;

        private readonly Dictionary<string, float> weights = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> shares = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly HashSet<string> kept = new HashSet<string>(StringComparer.Ordinal);
        private readonly float total;
        private float randomScore = -1f;

        public TidyProfile(Dictionary<string, float> held, TidyMemory memory, double now, double fade, double settle)
        {
            HashSet<string> prefabs = new HashSet<string>(held.Keys, StringComparer.Ordinal);
            prefabs.UnionWith(memory.Entries.Keys);
            foreach (string prefab in prefabs)
            {
                memory.Entries.TryGetValue(prefab, out TidyMemory.Entry entry);
                held.TryGetValue(prefab, out float stacks);
                float weight = Weight(stacks, entry, now, fade, settle);
                if (weight <= 0f)
                    continue;
                weights[prefab] = weight;
                total += weight;
                if (entry != null && entry.Kept)
                    kept.Add(prefab);
            }
        }

        /// <summary>
        /// One over the weighted mean share: how many unrelated kinds the chest holds, in effect. Worked out on first use
        /// (it compares every pair of items), since most chests a look passes over never need it.
        /// </summary>
        public float RandomScore => randomScore >= 0f ? randomScore : randomScore = Score();

        public bool IsJunk => RandomScore >= JunkScore;

        public IEnumerable<string> Prefabs => weights.Keys;

        public bool IsKept(string prefab) => kept.Contains(prefab);

        /// <summary>The part of the chest's weight that is alike to the prefab (the prefab itself included).</summary>
        public float Share(string prefab)
        {
            if (total <= 0f)
                return 0f;
            if (shares.TryGetValue(prefab, out float share))
                return share;
            float alike = 0f;
            foreach (KeyValuePair<string, float> pair in weights)
            {
                if (TidyThemes.Alike(pair.Key, prefab))
                    alike += pair.Value;
            }
            shares[prefab] = alike / total;
            return alike / total;
        }

        /// <summary>The item has no place here: not kept, and the chest is a junk chest or the item a small part of it.</summary>
        public bool IsStray(string prefab) => !IsKept(prefab) && (IsJunk || Share(prefab) < StrayShare);

        /// <summary>The chest is a home for the item: kept here, or a big enough part of a chest that is no junk chest.</summary>
        public bool IsHomeFor(string prefab) => IsKept(prefab) || (Share(prefab) >= StrayShare && !IsJunk);

        private static float Weight(float stacks, TidyMemory.Entry entry, double now, double fade, double settle)
        {
            if (entry == null)
                return stacks * NewWeight;
            float amount = Mathf.Max(stacks, entry.Faded(now, fade));
            float settled = settle <= 0.0 ? 1f : Mathf.Clamp01((float)((now - entry.FirstSeen) / settle));
            float origin = entry.Hand ? 1f : OnItsOwn;
            return amount * (NewWeight + (1f - NewWeight) * settled) * origin;
        }

        private float Score()
        {
            if (total <= 0f)
                return 0f;
            float mean = 0f;
            foreach (KeyValuePair<string, float> pair in weights)
                mean += pair.Value / total * Share(pair.Key);
            return mean > 0f ? 1f / mean : 0f;
        }
    }
}
