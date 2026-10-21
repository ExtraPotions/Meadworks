// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; hone (temper left 2026-10-19).
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// One rune verb: its own precondition (pipeline step 11) and its dry run on a copy (step 12). Returns a refusal
    /// or the new state with its feedback; never writes.
    /// </summary>
    internal interface IStoneVerb
    {
        StoneResult Run(StoneJob job);
    }

    /// <summary>
    /// The fixed verb set of the runes, dispatched by the rune's <c>verb</c>: <c>promote</c> (Ascension),
    /// <c>reroll</c> (Recasting), <c>strip</c> (Cleansing), <c>corrupt</c> (Serpent),
    /// <c>socket</c> (the Dvergr Chisel), <c>gem</c> (the gems), <c>hone</c> (Runes/TargetedVerbs).
    /// A new verb is one class and one line in <see cref="Implemented"/>. A stone whose verb this build cannot perform
    /// refuses as disabled (<c>stone_disabled</c>), with one log warning for the server owner, rather than failing (IMP-60).
    /// </summary>
    internal static class StoneVerbs
    {
        private static readonly Dictionary<StoneVerb, IStoneVerb> Implemented = new Dictionary<StoneVerb, IStoneVerb>
        {
            { StoneVerb.Promote, new PromoteVerb() },
            { StoneVerb.Strip, new StripVerb() },
            { StoneVerb.Corrupt, new CorruptVerb() },
            { StoneVerb.Reroll, new RerollVerb() },
            { StoneVerb.Socket, new SocketVerb() },
            { StoneVerb.Gem, new GemVerb() },
            { StoneVerb.Hone, new HoneVerb() },
        };

        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>No rune raises the item's rarity: it is the top, or the next is source-only (Legendary: bosses and raids).</summary>
        public static bool AtTop(StoneJob job)
        {
            if (job.Rarity == null || job.Rarity.Index == 0)
            {
                return false;
            }
            RarityDef? next = job.Rules.Economy.Next(job.Rarity);
            return next == null || next.SourceOnly;
        }

        /// <summary>Whether this build performs the verb; the economy loader disables stones whose verb it does not.</summary>
        public static bool IsImplemented(StoneVerb verb) => Implemented.ContainsKey(verb);

        /// <summary>A live, enabled definition whose verb this build performs (pipeline step 4).</summary>
        public static bool IsUsable(StoneDef? def)
        {
            if (def == null || !def.Enabled)
            {
                return false;
            }
            if (Implemented.ContainsKey(def.Verb))
            {
                return true;
            }
            if (Warned.Add(def.Id))
            {
                Log.Warn($"rune '{def.Id}' is enabled, but its verb '{def.Verb}' is not in this version; it refuses as disabled");
            }
            return false;
        }

        /// <summary>The verb object of a definition, or null (the inscription chooser asks it for its candidates).</summary>
        public static IStoneVerb? For(StoneDef? def) => def != null && Implemented.TryGetValue(def.Verb, out IStoneVerb verb) ? verb : null;

        public static StoneResult Run(StoneJob job)
        {
            StoneVerb verb = job.Def!.Verb;
            return Implemented[verb].Run(job);
        }
    }
}
