// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// What code adds to the item classes beside the YAML (classes-and-tiers.md section 1, api.md section 2): classes
    /// registered by id, prefabs claimed for a class, and classifier callbacks. Every change bumps <see cref="Version"/>,
    /// which drops every cached classification and rebuilds <see cref="Index"/> on its next read. Main thread only;
    /// every peer runs the same mods, so every peer registers the same things.
    /// </summary>
    internal static class ClassRegistry
    {
        private static readonly List<ItemClass> Registered = new List<ItemClass>();
        private static readonly List<KeyValuePair<string, string>> Claims = new List<KeyValuePair<string, string>>();
        private static readonly List<Classifier> Classifiers = new List<Classifier>();
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);
        private static ClassIndex? _index;
        private static int _indexGeneration = -1;
        private static int _indexVersion = -1;

        /// <summary>Bumped by every registration; classification caches compare it with the one they were built under.</summary>
        public static int Version { get; private set; }

        /// <summary>The effective classes under the running rules and the registrations so far.</summary>
        public static ClassIndex Index
        {
            get
            {
                RuleSet rules = ActiveRules.Current;
                if (_index == null || _indexGeneration != rules.Generation || _indexVersion != Version)
                {
                    _index = ClassIndex.Build(rules.Economy.Classes, Registered, Claims);
                    _indexGeneration = rules.Generation;
                    _indexVersion = Version;
                }
                return _index;
            }
        }

        /// <summary>Adds a class, or replaces the registered one with its id. A YAML class of that id lies over it.</summary>
        public static void Register(ItemClass def)
        {
            int index = Registered.FindIndex(c => c.Id == def.Id);
            if (index >= 0)
            {
                Registered[index] = def;
            }
            else
            {
                Registered.Add(def);
            }
            Version++;
        }

        /// <summary>Puts prefabs in a class (step 1, after the YAML <c>items</c> lists). A class defined later still gets them.</summary>
        public static void Claim(string classId, IEnumerable<string> prefabs)
        {
            foreach (string prefab in prefabs)
            {
                Claims.Add(new KeyValuePair<string, string>(prefab, classId));
            }
            Version++;
        }

        /// <summary>Adds a classifier (step 2, in registration order), or replaces the one with this id.</summary>
        public static void AddClassifier(string id, Func<ItemDrop.ItemData, string?> classify)
        {
            Classifiers.RemoveAll(c => c.Id == id);
            Classifiers.Add(new Classifier(id, classify));
            Version++;
        }

        public static bool RemoveClassifier(string id)
        {
            bool removed = Classifiers.RemoveAll(c => c.Id == id) > 0;
            Version += removed ? 1 : 0;
            return removed;
        }

        /// <summary>
        /// Step 2: the first classifier that names a class this index knows. A classifier that throws or names an
        /// unknown class is logged once and counts as no answer.
        /// </summary>
        public static ItemClass? Ask(ItemDrop.ItemData item, ClassIndex index)
        {
            for (int i = 0; i < Classifiers.Count; i++)
            {
                string? id = Guarded(Classifiers[i], item);
                if (id == null)
                {
                    continue;
                }
                if (index.ById.TryGetValue(id, out ItemClass found))
                {
                    return found;
                }
                ReportOnce(Classifiers[i].Id + "|" + id, $"classifier '{Classifiers[i].Id}' named the class '{id}', which is not defined");
            }
            return null;
        }

        private static string? Guarded(Classifier classifier, ItemDrop.ItemData item)
        {
            try
            {
                return classifier.Classify(item);
            }
            catch (Exception e)
            {
                ReportOnce(classifier.Id, $"classifier '{classifier.Id}' threw; counted as no answer (logged once): {e}");
                return null;
            }
        }

        private static void ReportOnce(string key, string message)
        {
            if (Reported.Add(key))
            {
                Log.Warn(message);
            }
        }

        private sealed class Classifier
        {
            public Classifier(string id, Func<ItemDrop.ItemData, string?> classify)
            {
                Id = id;
                Classify = classify;
            }

            public string Id { get; }
            public Func<ItemDrop.ItemData, string?> Classify { get; }
        }
    }

    /// <summary>
    /// The effective item classes at one moment, immutable: the YAML classes in file order (each laid over a registered
    /// class of the same id), then the registered classes the YAML does not name, in registration order; and the
    /// prefab table of step 1 (the classes' <c>items</c> in that order, then the claims; the first entry wins).
    /// </summary>
    internal sealed class ClassIndex
    {
        private ClassIndex(List<ItemClass> classes, Dictionary<string, ItemClass> byId, Dictionary<string, ItemClass> byPrefab)
        {
            Classes = classes;
            ById = byId;
            ByPrefab = byPrefab;
        }

        public IReadOnlyList<ItemClass> Classes { get; }
        public IReadOnlyDictionary<string, ItemClass> ById { get; }
        public IReadOnlyDictionary<string, ItemClass> ByPrefab { get; }

        public static ClassIndex Build(IReadOnlyList<ItemClass> rules, IReadOnlyList<ItemClass> registered,
            IReadOnlyList<KeyValuePair<string, string>> claims)
        {
            List<ItemClass> classes = Effective(rules, registered);
            Dictionary<string, ItemClass> byId = new Dictionary<string, ItemClass>(StringComparer.Ordinal);
            Dictionary<string, ItemClass> byPrefab = new Dictionary<string, ItemClass>(StringComparer.Ordinal);
            foreach (ItemClass c in classes)
            {
                byId[c.Id] = c;
                foreach (string prefab in c.Items)
                {
                    TryAdd(byPrefab, prefab, c);
                }
            }
            foreach (KeyValuePair<string, string> claim in claims)
            {
                if (byId.TryGetValue(claim.Value, out ItemClass c))
                {
                    TryAdd(byPrefab, claim.Key, c);
                }
            }
            return new ClassIndex(classes, byId, byPrefab);
        }

        private static List<ItemClass> Effective(IReadOnlyList<ItemClass> rules, IReadOnlyList<ItemClass> registered)
        {
            Dictionary<string, ItemClass> byId = new Dictionary<string, ItemClass>(StringComparer.Ordinal);
            foreach (ItemClass c in registered)
            {
                byId[c.Id] = c;
            }
            List<ItemClass> classes = new List<ItemClass>(rules.Count + registered.Count);
            HashSet<string> named = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemClass c in rules)
            {
                classes.Add(byId.TryGetValue(c.Id, out ItemClass under) ? c.Over(under) : c);
                named.Add(c.Id);
            }
            foreach (ItemClass c in registered)
            {
                if (!named.Contains(c.Id))
                {
                    classes.Add(c);
                }
            }
            return classes;
        }

        private static void TryAdd(Dictionary<string, ItemClass> map, string prefab, ItemClass c)
        {
            if (!map.ContainsKey(prefab))
            {
                map[prefab] = c;
            }
        }
    }
}
