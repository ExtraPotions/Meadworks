// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// What other mods add to the inscription family through the API (api.md section 3), kept as data the loader merges:
    /// registered inscription entries (the YAML format 2 fields, parsed from the mod's JSON) form the bottom layer of
    /// every build of the family, under the built-in defaults and the files (<see cref="FamilyBuilder"/>), so a YAML entry
    /// with the same id changes only the fields it names; and pool additions (an inscription allowed on one more class)
    /// are applied to the parsed entries after the merge (<see cref="ApplyPools"/>), so they hold whatever the files say
    /// about the inscription's classes. Code, not data: every peer runs the same mods and registers the same things, and
    /// a bound player builds the server's texts over its own copy. Main thread only.
    /// </summary>
    internal static class CodeLayer
    {
        public const string Origin = "(registered by mods)";

        private static readonly List<KeyValuePair<string, YamlMappingNode>> Inscriptions = new List<KeyValuePair<string, YamlMappingNode>>();
        private static readonly List<PoolAddition> Pools = new List<PoolAddition>();

        public static bool HasInscription(string id) => Inscriptions.Exists(e => e.Key == id);

        /// <summary>Adds an inscription entry, or replaces the one registered with its id (it keeps its place).</summary>
        public static void SetInscription(string id, YamlMappingNode entry)
        {
            int index = Inscriptions.FindIndex(e => e.Key == id);
            KeyValuePair<string, YamlMappingNode> pair = new KeyValuePair<string, YamlMappingNode>(id, entry);
            if (index >= 0)
            {
                Inscriptions[index] = pair;
            }
            else
            {
                Inscriptions.Add(pair);
            }
        }

        /// <summary>Lets an inscription roll on a class; a later call for the same pair replaces the fit. False when unchanged.</summary>
        public static bool AddPool(string inscriptionId, string classId, bool best)
        {
            int index = Pools.FindIndex(p => p.Inscription == inscriptionId && p.Class == classId);
            if (index >= 0 && Pools[index].Best == best)
            {
                return false;
            }
            if (index >= 0)
            {
                Pools.RemoveAt(index);
            }
            Pools.Add(new PoolAddition(inscriptionId, classId, best));
            return true;
        }

        /// <summary>The bottom layer of a build of <paramref name="spec"/>, or null when nothing is registered for it.</summary>
        public static SourceLayer? For(FamilySpec spec, RuleIssues issues)
        {
            if (spec != FamilySpec.Affixes || Inscriptions.Count == 0)
            {
                return null;
            }
            YamlSequenceNode list = new YamlSequenceNode();
            Inscriptions.ForEach(e => list.Add(e.Value));
            YamlMappingNode root = new YamlMappingNode { { "inscriptions", list } };
            issues.Register(root, Origin);
            return new SourceLayer(Origin, root, builtIn: true);
        }

        /// <summary>
        /// After the entries are parsed, before the pools are built: every pool addition joins its inscription's
        /// <c>best</c> or <c>allowed</c> list (best wins over allowed, as in the YAML). An unknown inscription id is a
        /// warning and ignored.
        /// </summary>
        public static void ApplyPools(IReadOnlyList<AffixDef> affixes, RuleIssues issues)
        {
            foreach (PoolAddition pool in Pools)
            {
                AffixDef? def = FindAffix(affixes, pool.Inscription);
                if (def == null)
                {
                    issues.Warn("inscriptions", null, $"a mod lets '{pool.Inscription}' roll on '{pool.Class}', but no inscription has that id; ignored");
                    continue;
                }
                Join(def, pool);
            }
        }

        private static void Join(AffixDef def, PoolAddition pool)
        {
            List<string> best = new List<string>(def.BestClasses);
            List<string> allowed = new List<string>(def.AllowedClasses);
            if (best.Contains(pool.Class) || (!pool.Best && allowed.Contains(pool.Class)))
            {
                return;
            }
            allowed.Remove(pool.Class);
            (pool.Best ? best : allowed).Add(pool.Class);
            def.BestClasses = best;
            def.AllowedClasses = allowed;
        }

        private static AffixDef? FindAffix(IReadOnlyList<AffixDef> affixes, string id)
        {
            for (int i = 0; i < affixes.Count; i++)
            {
                if (affixes[i].Id == id)
                {
                    return affixes[i];
                }
            }
            return null;
        }

        private sealed class PoolAddition
        {
            public PoolAddition(string inscription, string classId, bool best)
            {
                Inscription = inscription;
                Class = classId;
                Best = best;
            }

            public string Inscription { get; }
            public string Class { get; }
            public bool Best { get; }
        }
    }
}
