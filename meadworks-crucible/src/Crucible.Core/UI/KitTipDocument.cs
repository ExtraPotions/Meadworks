// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace ExtraPotions.Meadworks.Crucible
{
    public enum KitTipTone { Normal, Muted, Accent, Good, Bad, Warning }
    public enum KitTipKind { Value, Comparison, Cost, Status }
    /// <summary>Display facts supplied by the product; no numeric or gameplay interpretation is performed.</summary>
    public sealed class KitTipRow
    {
        public KitTipKind Kind { get; }
        public string Label { get; }
        public string Value { get; }
        public string Before { get; }
        public string Delta { get; }
        public string Note { get; }
        public KitTipTone Tone { get; }
        public KitTipRow(string label,string value="",KitTipTone tone=KitTipTone.Normal,string note="")
            :this(KitTipKind.Value,label,value,"","",tone,note){}
        KitTipRow(KitTipKind kind,string label,string value,string before,string delta,KitTipTone tone,string note)
        {
            if(!Enum.IsDefined(typeof(KitTipTone),tone))throw new ArgumentOutOfRangeException(nameof(tone));
            Kind=kind;Label=label??"";Value=value??"";Before=before??"";Delta=delta??"";Tone=tone;Note=note??"";
        }
        public static KitTipRow Compare(string label,string before,string after,string delta,KitTipTone tone=KitTipTone.Normal)
            =>new KitTipRow(KitTipKind.Comparison,label,after,before,delta,tone,"");
        public static KitTipRow Cost(string label,string required,bool enough)
            =>new KitTipRow(KitTipKind.Cost,label,required,"","",enough?KitTipTone.Normal:KitTipTone.Bad,"");
        public static KitTipRow Status(string text,KitTipTone tone=KitTipTone.Muted)
            =>new KitTipRow(KitTipKind.Status,text,"","","",tone,"");
    }
    public sealed class KitTipSection
    {
        public string Title { get; }
        public bool Card { get; }
        public IReadOnlyList<KitTipRow> Rows { get; }
        public KitTipSection(string title,IEnumerable<KitTipRow> rows,bool card=false)
        {
            if(rows==null)throw new ArgumentNullException(nameof(rows));
            var copy=rows.ToArray();if(copy.Any(row=>row==null))throw new ArgumentException("A tooltip row cannot be null.",nameof(rows));
            Title=title??"";Card=card;Rows=new ReadOnlyCollection<KitTipRow>(copy);
        }
    }
    /// <summary>Immutable, localization-ready content for a shared tooltip or comparison card.</summary>
    public sealed class KitTipDocument
    {
        public string Title { get; }
        public string Body { get; }
        public IReadOnlyList<KitTipSection> Sections { get; }
        public float PreferredWidth { get; }
        public int SplitAfter { get; }
        public string Key { get; }
        public bool HasContent => Title.Length>0 || Body.Length>0 || Sections.Any(section=>section.Title.Length>0||section.Rows.Count>0);
        public KitTipDocument(string title,IEnumerable<KitTipSection> sections,string body="",float preferredWidth=420,int splitAfter=0)
        {
            if(sections==null)throw new ArgumentNullException(nameof(sections));
            if((float.IsNaN(preferredWidth)||float.IsInfinity(preferredWidth))||preferredWidth<=0)throw new ArgumentOutOfRangeException(nameof(preferredWidth));
            var copy=sections.ToArray();if(copy.Any(section=>section==null))throw new ArgumentException("A tooltip section cannot be null.",nameof(sections));
            if(splitAfter<0||splitAfter>=copy.Length && splitAfter!=0)throw new ArgumentOutOfRangeException(nameof(splitAfter));
            Title=title??"";Body=body??"";Sections=new ReadOnlyCollection<KitTipSection>(copy);PreferredWidth=preferredWidth;SplitAfter=splitAfter;
            var key=new StringBuilder();void Part(string value){key.Append(value.Length).Append(':').Append(value);}
            Part(Title);Part(Body);Part(preferredWidth.ToString("R",System.Globalization.CultureInfo.InvariantCulture));Part(splitAfter.ToString());
            foreach(var section in copy){Part(section.Title);Part(section.Card?"card":"plain");Part(section.Rows.Count.ToString());
                foreach(var row in section.Rows){Part(row.Kind.ToString());Part(row.Label);Part(row.Value);Part(row.Before);Part(row.Delta);Part(row.Note);Part(row.Tone.ToString());}}
            Key=key.ToString();
        }
        public string PlainText()
        {
            var text=new StringBuilder();void Line(string value){if(value.Length==0)return;if(text.Length>0)text.Append('\n');text.Append(value);}
            Line(Body);
            foreach(var section in Sections){Line(section.Title);foreach(var row in section.Rows){
                string value=row.Kind==KitTipKind.Comparison?row.Before+" → "+row.Value+(row.Delta.Length>0?" ("+row.Delta+")":""):row.Value;
                Line(row.Label+(value.Length>0?": "+value:""));Line(row.Note);}}
            return text.ToString();
        }
        public static KitTipDocument FromSheet(string title,KitSheet sheet)
        {
            if(sheet==null)throw new ArgumentNullException(nameof(sheet));
            var sections=new List<KitTipSection>();var rows=new List<KitTipRow>();string heading="";
            foreach(var line in sheet.Lines){if(line.Heading){if(rows.Count>0){sections.Add(new KitTipSection(heading,rows));rows.Clear();}heading=line.Label;}
                else rows.Add(new KitTipRow(line.Label,line.Value,note:line.Tip??""));}
            if(rows.Count>0)sections.Add(new KitTipSection(heading,rows));return new KitTipDocument(title,sections);
        }
    }
    public static class KitStructuredTipLayout
    {
        public const float Margin=24,Gap=12;
        public static float ColumnWidth(float preferred,float available)
        {
            if(float.IsNaN(preferred)||float.IsInfinity(preferred)||float.IsNaN(available)||float.IsInfinity(available)||preferred<=0||available<=0)throw new ArgumentOutOfRangeException();
            return Math.Min(preferred,available);
        }
        public static bool TwoColumns(float columnWidth,float availableWidth,float contentHeight,float availableHeight,int sections)
            =>sections>1 && contentHeight>availableHeight && columnWidth*2+Gap<=availableWidth;
        public static float ViewHeight(float contentHeight,float availableHeight)=>Math.Min(Math.Max(0,contentHeight),Math.Max(0,availableHeight));
    }
}
