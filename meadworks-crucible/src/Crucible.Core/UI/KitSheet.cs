// Adapted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/StatSheet.cs, TipText.cs, StatRows.cs, GearStats.cs, TabButtons.cs, ButtonStrip.cs,
// PopupPlace.cs, BlockedCell.cs and ValheimModLibs/PlateColumn/TipBox.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: the stat sheet model and breakdown text without game types
// (localisation is a delegate, numbers use the invariant culture), tab widths for any number of tabs, and the pop-up and
// tooltip placement rules over plain rectangles.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// A stat list as lines, each a label, a value (empty for sentences that carry their number) and the breakdown its
    /// tooltip shows, or a section's heading. A section starts with a little space and its heading when it has one; a
    /// section that gets no line shows neither. <see cref="Key"/> changes whenever any line does, so the list is drawn
    /// again only then.
    /// </summary>
    public sealed class KitSheet {
        public struct Line {
            public string Label;
            public string Value;
            public bool Heading;
            public bool SpaceBefore;
            public string? Tip;
        }

        readonly List<Line> lines=new List<Line>();
        readonly StringBuilder key=new StringBuilder();
        bool gap;
        string? heading;

        public IReadOnlyList<Line> Lines=>lines;
        public string Key=>key.ToString();

        public void Clear(){lines.Clear();key.Clear();gap=false;heading=null;}

        /// <summary>The next line starts a section: space before it unless nothing came before, then the heading.</summary>
        public void Section(string? title=null){gap=lines.Count>0;heading=title;}

        public void Add(string label,string value,string? tip=null){
            if(heading!=null){Put(heading,"",true,null);heading=null;}
            Put(label??"",value??"",false,tip);
        }

        void Put(string label,string value,bool isHeading,string? tip){
            lines.Add(new Line{Label=label,Value=value,Heading=isHeading,SpaceBefore=gap,Tip=tip});
            gap=false;
            key.Append(label).Append('\t').Append(value).Append('\t').Append(tip).Append('\n');
        }
    }

    /// <summary>
    /// One stat's breakdown as tooltip text: lines of a source and its part ("Iron helmet: 20"), now and then a heading,
    /// and the rest of the total nobody named as one "Other effects" line, so the parts always add up. Sources are
    /// localised through the caller's delegate; amounts are orange, as the game's own tooltips show numbers.
    /// </summary>
    public sealed class KitTipText {
        readonly StringBuilder text=new StringBuilder();
        readonly Func<string,string> localize;
        readonly string other;
        readonly string nothing;

        public KitTipText(Func<string,string>? localize,string other,string nothing){this.localize=localize??(s=>s);this.other=other??"";this.nothing=nothing??"";}

        public bool Empty=>text.Length==0;

        public KitTipText Part(string source,string amount){Line().Append(localize(source)).Append(": <color=orange>").Append(amount).Append("</color>");return this;}

        public KitTipText Heading(string title){
            if(!Empty)text.Append('\n');
            Line().Append("<color=#C9A46B>").Append(localize(title)).Append("</color>");
            return this;
        }

        /// <summary>What the named parts leave of the total, as "Other effects", when it is at least half a unit.</summary>
        public KitTipText Rest(float rest,Func<float,string> format)=>KitMath.Abs(rest)>=0.5f?Part(other,format(rest)):this;

        /// <summary>The breakdown, or the "nothing gives this" line when no part was named.</summary>
        public override string ToString()=>Empty?localize(nothing):text.ToString();

        StringBuilder Line()=>Empty?text:text.Append('\n');

        public static string Whole(float value)=>value.ToString("0",CultureInfo.InvariantCulture);
        public static string Signed(float value)=>value.ToString("+0;-0;0",CultureInfo.InvariantCulture);
        public static string Percent(float fraction)=>(fraction*100f).ToString("+0;-0;0",CultureInfo.InvariantCulture)+"%";
    }

    /// <summary>Stat sheet rows: a label left, its value right, the label wrapping under itself when they do not fit.</summary>
    public static class KitRowLayout {
        public const float TextSize=12f;
        public const float HeadingSize=10.5f;
        public const float SectionSpace=5f;
        public const float ValueGap=6f;
        public const float MinLabelWidth=24f;
        /// <summary>The scroll bar's width in the sheet's right margin.</summary>
        public const float ScrollBarWidth=4f;
        /// <summary>Mouse wheel speed when the game's recipe list has none to copy.</summary>
        public const float FallbackScrollSensitivity=30f;
        public const float DividerThickness=2f;
        /// <summary>Margin inside the sheet's rounded inset (PackPanel <c>GearStats</c>).</summary>
        public const float SheetPadding=6f;
        /// <summary>A shown sheet is worked out again at most this often...</summary>
        public const float RefreshSeconds=0.25f;
        /// <summary>...and redrawn even with unchanged inputs after this long.</summary>
        public const float RecheckSeconds=2f;

        public static float LabelWidth(float width,float valueWidth)=>KitMath.Max(MinLabelWidth,width-(valueWidth>0f?valueWidth+ValueGap:0f));
    }

    public readonly struct KitTabPlace {
        public float X { get; }
        public float Width { get; }
        public KitTabPlace(float x,float width){X=x;Width=width;}
    }

    /// <summary>A strip of tab buttons across a panel's top: equal widths, <see cref="Between"/> apart.</summary>
    public static class KitTabLayout {
        public const float Height=26f;
        /// <summary>From the tabs' bottom to the content under them.</summary>
        public const float Gap=8f;
        public const float Between=6f;

        public static IReadOnlyList<KitTabPlace> Place(float width,int count,float left){
            if(count<0)throw new ArgumentOutOfRangeException(nameof(count));
            var places=new List<KitTabPlace>(count);
            if(count==0)return places;
            float each=(width-Between*(count-1))/count;
            for(int i=0;i<count;i++)places.Add(new KitTabPlace(left+i*(each+Between),each));
            return places;
        }
    }

    /// <summary>Captions on cloned game buttons: auto-sized, one line, cut at the edge (no ellipsis: the game fonts lack the glyph).</summary>
    public static class KitButtonText {
        public const float MinSize=9f;
        public const float MaxSize=16f;
        /// <summary>TextMeshPro margin (left, top, right, bottom) as R, G, B, A.</summary>
        public static readonly KitColor Margin=new KitColor(4f,1f,4f,1f);
    }

    /// <summary>A row of buttons along a panel's bottom edge (PackPanel's strip kept for OpenKeep's row).</summary>
    public static class KitButtonRow {
        public const float StripHeight=30f;
        public const float ButtonHeight=26f;
    }

    /// <summary>A cell the grid shows but does not open: dimmed, a grey cross a little inset, deaf to the pointer.</summary>
    public static class KitCells {
        public const float BlockedAlpha=0.55f;
        public const float CrossInset=8f;
    }

    public readonly struct KitTipPlace {
        /// <summary>True when the tip sits right of its box (pivot at its left middle), false left of it (right middle).</summary>
        public bool RightSide { get; }
        /// <summary>The pivot point before the vertical shift.</summary>
        public KitVector Position { get; }
        /// <summary>How far to move the tip up (negative: down) to keep it on screen.</summary>
        public float Shift { get; }
        public KitTipPlace(bool rightSide,KitVector position,float shift){RightSide=rightSide;Position=position;Shift=shift;}
    }

    public static class KitPlacement {
        /// <summary>
        /// A pop-up's left edge under its button: centred under it, never left of <paramref name="slotLeft"/>, and right of
        /// an open container panel that reaches under it.
        /// </summary>
        public static float PopupX(float slotLeft,float centre,float popupWidth,bool containerOpen,float containerRight,float gap){
            float x=KitMath.Max(slotLeft,centre-popupWidth/2f);
            if(containerOpen)x=KitMath.Max(x,containerRight+gap);
            return x;
        }

        /// <summary>
        /// A tooltip beside a box (screen units, y up): right of it, centred on its height, unless that leaves the screen;
        /// then left of it. Shifted up or down to stay on screen.
        /// </summary>
        public static KitTipPlace Tip(KitRect box,KitVector size,float gap,float screenWidth,float screenHeight){
            float middle=box.Y+box.Height/2f;
            var position=new KitVector(box.XMax+gap,middle);
            bool right=position.X+size.X<=screenWidth;
            if(!right)position=new KitVector(box.XMin-gap,middle);
            float bottom=middle-size.Y/2f,top=middle+size.Y/2f;
            float shift=KitMath.Max(0f,-bottom)-KitMath.Max(0f,top-screenHeight);
            return new KitTipPlace(right,position,shift);
        }
    }
}
