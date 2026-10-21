// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// One creature's decorated name (<see cref="Naming"/>), kept. The game asks for a creature's name every frame its
    /// nameplate or hover text shows, so the words are put together again only when what they decorate changes: the
    /// name the game hands in (a tamed creature renamed, the language switched) or the traits it carries, which a
    /// creature resolves once and keeps.
    /// </summary>
    public sealed class NameMemo
    {
        private string? _from;
        private CreatureTraits? _traits;
        private string _made = "";
        private int _language = -1, _mask, _extras;
        private Aspect _aspect;

        public string Decorate(CreatureTraits traits, string baseName)
        {
            if (_mask != traits.Mask || _aspect != traits.Aspect || _extras != traits.ExtraAspects || _language != ExtraPotions.Wildblood.Runtime.WildbloodText.LanguageVersion || !ReferenceEquals(traits, _traits) || !string.Equals(baseName, _from, StringComparison.Ordinal))
            {
                _made = Naming.Decorate(traits, baseName);
                _from = baseName;
                _traits = traits;
                _mask = traits.Mask; _aspect = traits.Aspect; _extras = traits.ExtraAspects;
                _language = ExtraPotions.Wildblood.Runtime.WildbloodText.LanguageVersion;
            }
            return _made;
        }
    }
}
