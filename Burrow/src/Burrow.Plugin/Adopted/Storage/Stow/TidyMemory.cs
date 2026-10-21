using System;
using System.Collections.Generic;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// What a chest remembers, kept in its ZDO under <c>Burrow.tidy</c> so it survives restarts and changes of owner.
    /// Per prefab: when the chest first held it, the most stacks it held lately (fading linearly over three
    /// in-game days from the moment the item left, never below what it holds now), the stacks it held at the
    /// last look, whether a player ever put it in by hand, whether it is kept (a player put it back after Auto Tidy
    /// sent it away) and when Auto Tidy last sent it away. Only the owner updates it, at a look, and a look happens
    /// only when the contents changed, so the item that is gone now was there until this change.
    /// A chest Auto Tidy sees for the first time counts what it holds as placed by hand and settled: it was there
    /// before Auto Tidy, so a player put it there.
    /// </summary>
    internal sealed class TidyMemory
    {
        public const string Key = "Burrow.tidy";

        private static readonly int KeyHash = Key.GetStableHashCode();

        /// <summary>World seconds after a send-away in which a player putting the item back by hand keeps it there.</summary>
        public const double CorrectionSeconds = 900.0;

        private const int Version = 2;
        private const int MaxEntries = 32;
        private const float Refresh = 1.15f;
        private const byte HandFlag = 1;
        private const byte KeptFlag = 2;

        public sealed class Entry
        {
            public double FirstSeen;
            public float Peak;
            public double PeakTime;
            public float Last;
            public bool Hand;
            public bool Kept;
            public double SentAway;

            /// <summary>The peak faded linearly over <paramref name="fade"/> seconds from its time.</summary>
            public float Faded(double now, double fade)
            {
                double age = Math.Max(0.0, now - PeakTime);
                return fade <= 0.0 ? 0f : Peak * (float)Math.Max(0.0, 1.0 - age / fade);
            }
        }

        public Dictionary<string, Entry> Entries { get; } = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>No memory was written yet: Auto Tidy never looked at this chest.</summary>
        public bool IsNew { get; private set; } = true;

        public static TidyMemory Read(ZDO zdo)
        {
            TidyMemory memory = new TidyMemory();
            byte[] bytes = zdo != null ? zdo.GetByteArray(KeyHash) : null;
            if (bytes == null || bytes.Length == 0)
                return memory;
            memory.IsNew = false;
            try
            {
                memory.Load(new ZPackage(bytes));
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"Burrow: tidy memory unreadable, starting afresh: {e.GetType().Name}");
                memory.Entries.Clear();
            }
            return memory;
        }

        public void Write(ZDO zdo)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(Version);
            pkg.Write(Entries.Count);
            foreach (KeyValuePair<string, Entry> pair in Entries)
                WriteEntry(pkg, pair.Key, pair.Value);
            zdo.Set(KeyHash, pkg.GetArray());
            IsNew = false;
        }

        /// <summary>
        /// Takes in what the chest holds now (stacks per prefab) and what players put in by hand since the last look.
        /// True when the memory changed and should be written.
        /// </summary>
        public bool Update(Dictionary<string, float> held, HashSet<string> byHand, double now, double fade, double settle)
        {
            bool changed = IsNew;
            foreach (KeyValuePair<string, float> pair in held)
            {
                bool hand = byHand != null && byHand.Contains(pair.Key);
                changed |= Entries.TryGetValue(pair.Key, out Entry entry)
                    ? Refreshed(entry, pair.Value, hand, now, fade)
                    : Created(pair.Key, pair.Value, hand, now, settle);
            }
            changed |= Left(held, now, fade);
            changed |= Forget(held, now, fade);
            return changed;
        }

        /// <summary>Auto Tidy sent the prefab away from this chest just now.</summary>
        public void Sent(string prefab, double now)
        {
            if (Entries.TryGetValue(prefab, out Entry entry))
                entry.SentAway = now;
        }

        private bool Created(string prefab, float stacks, bool hand, double now, double settle)
        {
            double first = IsNew ? now - settle : now;
            Entries[prefab] = new Entry { FirstSeen = first, Peak = stacks, PeakTime = now, Last = stacks, Hand = hand || IsNew };
            return true;
        }

        private static bool Refreshed(Entry entry, float stacks, bool hand, double now, double fade)
        {
            bool changed = false;
            if (stacks > entry.Faded(now, fade) * Refresh + 0.01f)
            {
                entry.Peak = stacks;
                entry.PeakTime = now;
                changed = true;
            }
            if (Mathf.Abs(stacks - entry.Last) > 0.01f)
            {
                entry.Last = stacks;
                changed = true;
            }
            return Learned(entry, hand, now) || changed;
        }

        /// <summary>By hand: the item counts as chosen; put back soon after Auto Tidy sent it away, it is kept here.</summary>
        private static bool Learned(Entry entry, bool hand, double now)
        {
            if (!hand)
                return false;
            bool correction = !entry.Kept && entry.SentAway > 0.0 && now - entry.SentAway <= CorrectionSeconds;
            bool changed = !entry.Hand || correction;
            entry.Hand = true;
            entry.Kept |= correction;
            return changed;
        }

        /// <summary>An item held at the last look and gone now left with this change: its memory starts fading now.</summary>
        private bool Left(Dictionary<string, float> held, double now, double fade)
        {
            bool changed = false;
            foreach (KeyValuePair<string, Entry> pair in Entries)
            {
                Entry entry = pair.Value;
                if (entry.Last <= 0f || held.ContainsKey(pair.Key))
                    continue;
                entry.Peak = Mathf.Max(entry.Faded(now, fade), entry.Last);
                entry.PeakTime = now;
                entry.Last = 0f;
                changed = true;
            }
            return changed;
        }

        /// <summary>Drops what faded away and is not held, then the weakest memories beyond the limit.</summary>
        private bool Forget(Dictionary<string, float> held, double now, double fade)
        {
            List<KeyValuePair<string, float>> weights = new List<KeyValuePair<string, float>>();
            List<string> gone = new List<string>();
            foreach (KeyValuePair<string, Entry> pair in Entries)
            {
                held.TryGetValue(pair.Key, out float stacks);
                float weight = Mathf.Max(stacks, pair.Value.Faded(now, fade));
                if (weight <= 0f)
                    gone.Add(pair.Key);
                else
                    weights.Add(new KeyValuePair<string, float>(pair.Key, weight));
            }
            weights.Sort((a, b) => a.Value.CompareTo(b.Value));
            for (int i = 0; i < weights.Count - MaxEntries; i++)
                gone.Add(weights[i].Key);
            gone.ForEach(prefab => Entries.Remove(prefab));
            return gone.Count > 0;
        }

        private static void WriteEntry(ZPackage pkg, string prefab, Entry entry)
        {
            pkg.Write(prefab);
            pkg.Write(entry.FirstSeen);
            pkg.Write(entry.Peak);
            pkg.Write(entry.PeakTime);
            pkg.Write(entry.Last);
            pkg.Write((byte)((entry.Hand ? HandFlag : 0) | (entry.Kept ? KeptFlag : 0)));
            pkg.Write(entry.SentAway);
        }

        private void Load(ZPackage pkg)
        {
            if (pkg.ReadInt() != Version)
                return;
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                string prefab = pkg.ReadString();
                Entry entry = new Entry { FirstSeen = pkg.ReadDouble(), Peak = pkg.ReadSingle(), PeakTime = pkg.ReadDouble(), Last = pkg.ReadSingle() };
                byte flags = pkg.ReadByte();
                entry.Hand = (flags & HandFlag) != 0;
                entry.Kept = (flags & KeptFlag) != 0;
                entry.SentAway = pkg.ReadDouble();
                Entries[prefab] = entry;
            }
        }
    }
}
