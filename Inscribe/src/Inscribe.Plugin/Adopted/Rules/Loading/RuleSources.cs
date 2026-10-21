// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// The texts a family's active model was built from (for <c>ecraft list</c> and <c>ecraft dump</c>): the files in
    /// layer order, whether the built-in defaults were layered under them, and whether they came from the server (a
    /// bound player) or from this machine's files. Recorded when a model is adopted, so it always matches the rules in
    /// force, also while the files on disk have errors.
    /// </summary>
    internal sealed class RuleSources
    {
        public static readonly RuleSources None = new RuleSources(Array.Empty<SourceText>(), true, false);

        public RuleSources(IReadOnlyList<SourceText> files, bool defaultsLayered, bool fromServer)
        {
            Files = files;
            DefaultsLayered = defaultsLayered;
            FromServer = fromServer;
        }

        /// <summary>The files, main file first then by name; the built-in defaults are not in this list.</summary>
        public IReadOnlyList<SourceText> Files { get; }

        public bool DefaultsLayered { get; }

        public bool FromServer { get; }
    }

    /// <summary>What <see cref="ActiveRules.ReloadLocal"/> did with one family.</summary>
    public enum FamilyReload
    {
        /// <summary>This machine's files built and are now in force.</summary>
        Applied,
        /// <summary>The files have errors; the previous rules stay (see the log).</summary>
        Rejected,
        /// <summary>The server binds this player; its rules stay in force, the local files were only re-read.</summary>
        Bound,
        /// <summary>Every file of the family is gone; the loaded rules stay.</summary>
        NoFiles,
    }
}
