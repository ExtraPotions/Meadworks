// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Syntax coloring, translated JSON/YAML checks and the table editor shared by Almanac's text and list editors.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Runtime.Localization;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    internal static class AlmanacSyntax
    {
        internal static AlmanacPreferences? Preferences;
        static bool On=>Preferences?.SyntaxHighlighting.Value!=false;
        static string T(string english)=>AlmanacText.T(english);

        /// <summary>Translated "Line N: reason" for the first JSON/YAML structure problem; empty when valid or plain text.</summary>
        internal static string Error(string text,SyntaxKind kind)
        {
            var issue=TextSyntax.Find(text,kind);
            return issue==null?"":T("Line")+" "+issue.Line+": "+T(issue.Message);
        }

        /// <summary>Coloring for the kind; with <paramref name="table"/>, a Table button when the text has rows of columns.</summary>
        internal static KitTextEditorOptions Options(SyntaxKind kind,KitWindow parent,string title,bool table)
        {
            var options=new KitTextEditorOptions{Highlight=kind==SyntaxKind.Plain?null!:text=>On?TextSyntax.Highlight(text,kind):null!};
            if(table)
            {
                options.ExtraLabel=T("Table");
                options.Extra=(current,set)=>
                {
                    var lines=TableText.Lines(current,out string lineBreak);
                    var shape=TableText.Detect(lines);
                    if(shape!=null)OpenTable(parent,title,shape,null,null,rows=>set(TableText.JoinLines(rows,lineBreak)),null);
                };
            }
            return options;
        }

        /// <summary>True when multi-line text forms at least two rows of the same columns.</summary>
        internal static bool HasTable(string text)
        {
            var lines=TableText.Lines(text??"",out _);
            return lines.Count(line=>line.Length>0)>=2&&TableText.Detect(lines)!=null;
        }

        /// <summary>
        /// Opens rows as a table. Each row is rejoined with the detected delimiter and checked by <paramref name="validate"/>
        /// (the setting's own item check) before <paramref name="apply"/> receives the entries in order.
        /// </summary>
        internal static void OpenTable(KitWindow parent,string title,TableShape shape,string? listSeparator,Func<string,string>? validate,
            Action<IReadOnlyList<string>> apply,Action<IReadOnlyList<string>>? listView)
        {
            char delimiter=shape.Delimiter;
            var headers=Enumerable.Range(1,shape.Columns).Select(i=>T("Column")+" "+i).ToArray();
            string Cell(string value){var e=TableText.CellError(value,delimiter,listSeparator);return e.Length==0?"":T(e);}
            string Row(string[] cells)=>validate?.Invoke(TableText.Join(cells,delimiter))??"";
            IReadOnlyList<string> Join(IReadOnlyList<string[]> rows)=>rows.Select(cells=>TableText.Join(cells,delimiter)).ToArray();
            KitAlmanacTable.Open(parent,title,headers,shape.Rows,Cell,Row,rows=>apply(Join(rows)),T("Add"),T("Remove"),
                listView!=null?T("List view"):null!,listView!=null?rows=>listView(Join(rows)):null!);
        }
    }
}
