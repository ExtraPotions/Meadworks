// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Almanac.Runtime.UI
{
    // Product search over displayed settings facts. The window accepts a shared matcher when AS07 is available.
    internal sealed class SettingsQuery
    {
        readonly List<string> required=new List<string>(),excluded=new List<string>();
        internal bool Empty=>required.Count+excluded.Count==0;
        internal SettingsQuery(string text)
        {
            var token=new StringBuilder();bool quoted=false;
            void Add()
            {
                string word=token.ToString();token.Clear();bool exclude=word.StartsWith("-");
                if(exclude)word=word.Substring(1);word=Fold(word);
                if(word.Length>0)(exclude?excluded:required).Add(word);
            }
            foreach(char c in text??"")
                if(c=='"')quoted=!quoted;
                else if(char.IsWhiteSpace(c)&&!quoted)Add();
                else token.Append(c);
            Add();
        }
        internal bool Matches(string text)
        {
            text=Fold(text);
            foreach(var word in required)if(!text.Contains(word))return false;
            foreach(var word in excluded)if(text.Contains(word))return false;
            return true;
        }
        static string Fold(string text)
        {
            var result=new StringBuilder();
            foreach(char c in (text??"").Normalize(NormalizationForm.FormD))
                if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)result.Append(char.ToLowerInvariant(c));
            return result.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
