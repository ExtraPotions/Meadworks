using System.Collections.Generic;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// Which items Auto Tidy treats as alike: the same prefab, a shared label (<see cref="TidyLabels"/>), or a pair the
    /// base taught it. A pair is learned when players keep the two items together in at least <see cref="MinChests"/>
    /// chests: both put in by hand, both held there for at least a day, in a chest of at most
    /// <see cref="MaxKinds"/> such items (a junk chest teaches nothing). Learning reads the memories of every loaded
    /// chest, whoever owns it, at most once every <see cref="RelearnSeconds"/>; <see cref="Version"/> changes when
    /// the learned pairs do, so kept profiles are scored again.
    /// </summary>
    internal static class TidyThemes
    {
        private const int MinChests = 2;
        private const int MaxKinds = 8;
        private const float RelearnSeconds = 30f;

        /// <summary>The learned pairs, each as its two prefab name hashes (<see cref="Pair"/>), so a test builds no string.</summary>
        private static HashSet<long> learned = new HashSet<long>();
        private static float learnedAt = float.MinValue;

        public static int Version { get; private set; }

        public static int Learned => learned.Count;

        public static bool Alike(string a, string b) =>
            a == b || TidyLabels.Shared(a, b) || (learned.Count > 0 && learned.Contains(Pair(a, b)));

        /// <summary>Learns again from the loaded chests when the last time is long enough ago.</summary>
        public static void Refresh()
        {
            if (Time.time - learnedAt < RelearnSeconds)
                return;
            learnedAt = Time.time;
            HashSet<long> fresh = Learn();
            if (fresh.SetEquals(learned))
                return;
            learned = fresh;
            Version++;
        }

        private static HashSet<long> Learn()
        {
            Dictionary<long, int> together = new Dictionary<long, int>();
            foreach (Container chest in ContainerScan.All())
            {
                if (!TidyChests.TakesPart(chest) || !TidyChests.IsLoaded(chest))
                    continue;
                List<string> kept = KeptByHand(TidyProfiles.MemoryOf(chest));
                if (kept.Count >= 2 && kept.Count <= MaxKinds)
                    Tally(kept, together);
            }
            HashSet<long> pairs = new HashSet<long>();
            foreach (KeyValuePair<long, int> pair in together)
            {
                if (pair.Value >= MinChests)
                    pairs.Add(pair.Key);
            }
            return pairs;
        }

        /// <summary>The prefabs a chest held at its last look that a player put there and that have been there a day.</summary>
        private static List<string> KeptByHand(TidyMemory memory)
        {
            double now = TidyProfiles.Now;
            double settle = TidyProfiles.SettleSeconds;
            List<string> kept = new List<string>();
            foreach (KeyValuePair<string, TidyMemory.Entry> pair in memory.Entries)
            {
                TidyMemory.Entry entry = pair.Value;
                if (entry.Hand && entry.Last > 0f && now - entry.FirstSeen >= settle)
                    kept.Add(pair.Key);
            }
            return kept;
        }

        private static void Tally(List<string> kept, Dictionary<long, int> together)
        {
            for (int i = 0; i < kept.Count; i++)
            {
                for (int j = i + 1; j < kept.Count; j++)
                {
                    long key = Pair(kept[i], kept[j]);
                    together.TryGetValue(key, out int count);
                    together[key] = count + 1;
                }
            }
        }

        /// <summary>The pair as one number, the same either way round: the two names' stable hashes, smaller first.</summary>
        private static long Pair(string a, string b)
        {
            int x = a.GetStableHashCode(), y = b.GetStableHashCode();
            return x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
        }
    }
}
