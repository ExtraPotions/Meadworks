// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: economy files written before the Awakening and Tempering Runes were folded into Ascension and Honing
// (user decision 2026-10-19) read as the new rune set, in memory; the file on disk is never rewritten.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Inscribe.Runes;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Conversion
{
    /// <summary>
    /// Rewrites one loaded economy document (a main file or an override, local or from the server) so it names only the
    /// five runes. A legacy rune row is dropped; when an Awakening row was present and Ascension still accepts only Magic
    /// (the old default), Ascension takes Common, Uncommon and Rare. A legacy drop weight list is added tier by tier into
    /// its successor's (one missing is taken over), so every column keeps its total; a boss or creature bonus row pays the
    /// successor (drop weights after the merge, see <see cref="ApplyDropWeights"/>). A document that names no legacy rune is left as it is, so a second pass changes nothing.
    /// </summary>
    internal static class EconomyRuneMigration
    {
        /// <summary>The rarities Ascension accepted before 2026-10-19 and accepts now.</summary>
        private static readonly string[] OldAscension = { "magic" };
        private static readonly string[] NewAscension = { "normal", "magic", "rare" };

        /// <summary>
        /// Converts <paramref name="root"/> in place; each change adds a note. True when anything changed.
        /// <paramref name="dropWeights"/> false leaves <c>drops.runes</c> alone: the loader converts each file without it
        /// and calls <see cref="ApplyDropWeights"/> once on the merged document, because a later layer's list replaces an
        /// earlier one. Folding per file would let an override that names only <c>tempering</c> replace the Honing row
        /// below it instead of adding to it.
        /// </summary>
        public static bool Apply(YamlMappingNode root, ICollection<string> notes, bool dropWeights = true)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (notes == null) throw new ArgumentNullException(nameof(notes));
            var changed = Runes(root, notes);
            if (Child(root, "drops") is YamlMappingNode drops)
            {
                if (dropWeights) changed |= DropWeights(drops, notes);
                changed |= Bonuses(Child(drops, "bosses") as YamlMappingNode, "drops.bosses", notes);
                changed |= Bonuses(Child(drops, "creatures") as YamlMappingNode, "drops.creatures", notes);
            }
            return changed;
        }

        /// <summary>Folds legacy drop weight lists into their successors on a merged document; true when anything changed.</summary>
        public static bool ApplyDropWeights(YamlMappingNode merged, ICollection<string> notes)
        {
            if (merged == null) throw new ArgumentNullException(nameof(merged));
            if (notes == null) throw new ArgumentNullException(nameof(notes));
            return Child(merged, "drops") is YamlMappingNode drops && DropWeights(drops, notes);
        }

        private static bool Runes(YamlMappingNode root, ICollection<string> notes)
        {
            if (!(Child(root, "runes") is YamlSequenceNode runes)) return false;
            var hadAwakening = false;
            var changed = false;
            for (var i = runes.Children.Count - 1; i >= 0; i--)
            {
                var id = runes.Children[i] is YamlMappingNode row ? Scalar(row, "id") : null;
                if (!LegacyRunes.IsLegacy(id)) continue;
                hadAwakening |= id == "awakening";
                runes.Children.RemoveAt(i);
                notes.Add("rune '" + id + "' was folded into '" + LegacyRunes.SuccessorOf(id) + "': its row is ignored");
                changed = true;
            }
            if (!hadAwakening) return changed;
            foreach (var node in runes.Children)
            {
                if (!(node is YamlMappingNode row) || Scalar(row, "id") != "ascension") continue;
                if (!(Child(row, "applies_to") is YamlSequenceNode applies)) continue;
                var ids = applies.Children.OfType<YamlScalarNode>().Select(s => s.Value).ToArray();
                if (!ids.SequenceEqual(OldAscension)) continue;
                row.Children[new YamlScalarNode("applies_to")] = new YamlSequenceNode(NewAscension.Select(r => new YamlScalarNode(r)));
                notes.Add("rune 'ascension' accepted only magic (the old default): it now raises normal, magic and rare");
                changed = true;
            }
            return changed;
        }

        private static bool DropWeights(YamlMappingNode drops, ICollection<string> notes)
        {
            if (!(Child(drops, "runes") is YamlMappingNode weights)) return false;
            var changed = false;
            foreach (var pair in LegacyRunes.Successors)
            {
                var key = new YamlScalarNode(pair.Key);
                if (!weights.Children.TryGetValue(key, out var legacy)) continue;
                weights.Children.Remove(key);
                changed = true;
                var target = new YamlScalarNode(pair.Value);
                var own = weights.Children.TryGetValue(target, out var node) ? node : null;
                var sum = Sum(legacy as YamlSequenceNode, own as YamlSequenceNode, own == null || IsNull(own));
                if (sum != null && !IsNull(legacy))
                {
                    weights.Children[target] = sum;
                    notes.Add("drops.runes." + pair.Key + " was added into drops.runes." + pair.Value);
                }
                else
                {
                    notes.Add("drops.runes." + pair.Key + " was folded into '" + pair.Value + "' and is ignored");
                }
            }
            return changed;
        }

        // Tier by tier; a shorter list's last value counts for the tiers it lacks (as the loader reads seven of eight).
        private static YamlSequenceNode? Sum(YamlSequenceNode? legacy, YamlSequenceNode? own, bool ownMissing)
        {
            var a = Numbers(legacy);
            var b = ownMissing ? new double[0] : Numbers(own);
            if (a == null || b == null) return null;
            var length = Math.Max(a.Length, b.Length);
            var result = new YamlSequenceNode { Style = YamlDotNet.Core.Events.SequenceStyle.Flow };
            for (var i = 0; i < length; i++)
                result.Add(new YamlScalarNode((At(a, i) + At(b, i)).ToString(CultureInfo.InvariantCulture)));
            return result;
        }

        private static double At(double[] values, int i) => values.Length == 0 ? 0 : values[Math.Min(i, values.Length - 1)];

        private static double[]? Numbers(YamlSequenceNode? list)
        {
            if (list == null) return null;
            var values = new double[list.Children.Count];
            for (var i = 0; i < values.Length; i++)
                if (!(list.Children[i] is YamlScalarNode s) || !double.TryParse(s.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return null;
            return values;
        }

        private static bool Bonuses(YamlMappingNode? owners, string path, ICollection<string> notes)
        {
            if (owners == null) return false;
            var changed = false;
            foreach (var owner in owners.Children)
            {
                if (!(owner.Value is YamlMappingNode map) || !(Child(map, "bonus") is YamlSequenceNode rows)) continue;
                foreach (var row in rows.Children.OfType<YamlMappingNode>())
                {
                    var rune = Scalar(row, "rune");
                    var successor = LegacyRunes.SuccessorOf(rune);
                    if (successor == null) continue;
                    row.Children[new YamlScalarNode("rune")] = new YamlScalarNode(successor);
                    notes.Add(path + "." + ((owner.Key as YamlScalarNode)?.Value ?? "?") + " bonus '" + rune + "' pays '" + successor + "'");
                    changed = true;
                }
            }
            return changed;
        }

        private static YamlNode? Child(YamlMappingNode map, string key) =>
            map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;

        private static string? Scalar(YamlMappingNode map, string key) => (Child(map, key) as YamlScalarNode)?.Value;

        private static bool IsNull(YamlNode node) =>
            node is YamlScalarNode s && (s.Value == null || s.Value == "" || s.Value == "~" || s.Value == "null");
    }
}
