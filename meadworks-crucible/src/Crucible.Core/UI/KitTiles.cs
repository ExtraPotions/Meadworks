using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>The tile list's layouts (KX05): the game's list, a compact list, and grids of 5, 4 or 3 tiles a row.</summary>
    public enum KitTileView { List, CompactList, SmallGrid, MediumGrid, LargeGrid }

    /// <summary>
    /// Layout rules of the tile list (KX05; behavior as OpenKeep 3.0.0's recipe views, rebuilt generic): List keeps the
    /// game's row step; CompactList packs rows <see cref="CompactStep"/> apart; a grid turns every row into a square tile,
    /// as many to a row as the view says, sized to the list's width. Tiles carry a kit cell frame, the icon inset by
    /// <see cref="Border"/>, a level badge in the top left, a corner mark in the top right and a durability bar along the
    /// bottom. Positions are top-left anchored with y down.
    /// </summary>
    public static class KitTileLayout {
        public const float CompactStep=22f;
        /// <summary>The list's width when it has none yet (before its first layout).</summary>
        public const float FallbackWidth=187f;
        public const float Border=3f;
        /// <summary>An unavailable tile's icon.</summary>
        public static readonly KitColor Greyed=new KitColor(0.45f,0.45f,0.45f,0.8f);

        public static int Columns(KitTileView view){
            switch(view){
                case KitTileView.SmallGrid:return 5;
                case KitTileView.MediumGrid:return 4;
                case KitTileView.LargeGrid:return 3;
                default:return 1;
            }
        }

        public static bool IsGrid(KitTileView view)=>Columns(view)>1;

        /// <summary>From one row (or tile) to the next: the game's <paramref name="listSpace"/>, 22 compact, width over columns in a grid.</summary>
        public static float Step(KitTileView view,float width,float listSpace){
            int columns=Columns(view);
            if(columns>1)return KitMath.Floor((width>1f?width:FallbackWidth)/columns);
            return view==KitTileView.CompactList?CompactStep:listSpace;
        }

        public static KitVector Position(int index,int columns,float step){
            if(index<0)throw new ArgumentOutOfRangeException(nameof(index));
            if(columns<1)throw new ArgumentOutOfRangeException(nameof(columns));
            return new KitVector(index%columns*step,-(index/columns)*step);
        }

        public static int Rows(int count,int columns)=>count<=0?0:(count+columns-1)/columns;

        /// <summary>The list content's height, never below the game's own <paramref name="baseHeight"/>.</summary>
        public static float ContentHeight(int count,int columns,float step,float baseHeight)=>KitMath.Max(baseHeight,Rows(count,columns)*step);

        /// <summary>The icon inside a tile of <paramref name="size"/> (origin lower left).</summary>
        public static KitRect Icon(float size)=>new KitRect(Border,Border,size-Border*2f,size-Border*2f);

        /// <summary>The level badge's text size, <c>round(size x 0.3)</c> (Unity's <c>Mathf.Round</c>: halves to even).</summary>
        public static float LevelFont(float size)=>(float)Math.Round(size*0.3f);

        public static KitVector LevelBox(float size)=>new KitVector(size*0.5f,size*0.4f);

        /// <summary>The corner mark (a favorite star) square, as large as the level text.</summary>
        public static float MarkSize(float size)=>LevelFont(size);

        /// <summary>The corner mark in the tile's top right, inside the border (origin lower left).</summary>
        public static KitRect CornerMark(float size){
            float mark=MarkSize(size);
            return new KitRect(size-Border-mark,size-Border-mark,mark,mark);
        }

        /// <summary>The game's durability bar keeps the width it woke with, so it is scaled into the tile.</summary>
        public static float DurabilityScale(float size,float barWidth)=>KitMath.Min(1f,(size-2f*Border)/KitMath.Max(1f,barWidth));

        /// <summary>A compact row is the step plus the game's 2 unit overlap high...</summary>
        public static float CompactHeight(float step)=>step+2f;

        /// <summary>...and everything in it scales by this from the game's row height.</summary>
        public static float CompactScale(float step,float rowHeight)=>CompactHeight(step)/KitMath.Max(1f,rowHeight);

        public static GridCell Cell(int index,int columns)=>new GridCell(index%columns,index/columns);

        public static int Index(GridCell cell,int columns)=>cell.Y*columns+cell.X;

        /// <summary>
        /// Gamepad stepping over the tiles (C07 <see cref="GridNavigator"/>): cells past the last tile and tiles the
        /// product hides (filtered, blocked) are skipped; null for an empty list.
        /// </summary>
        public static GridNavigator? Navigator(int count,int columns,Func<int,bool> shows){
            if(shows==null)throw new ArgumentNullException(nameof(shows));
            if(count<=0)return null;
            return new GridNavigator(columns,Rows(count,columns),c=>{int i=Index(c,columns);return i<count&&shows(i);});
        }
    }

    /// <summary>The rows (or tiles) a virtualised list builds: <see cref="First"/> up to <see cref="End"/> (exclusive).</summary>
    public readonly struct KitVirtualRange {
        public int First { get; }
        public int End { get; }
        public int Count=>End-First;
        public KitVirtualRange(int first,int end){First=first;End=end<first?first:end;}
        public bool Contains(int index)=>index>=First&&index<End;
    }

    /// <summary>
    /// Virtualisation windows over a list of equal steps (KX05 grids and lists; Almanac AX02): only the rows a viewport
    /// shows, plus <c>overscan</c> rows each side, are built; whole grid rows at a time.
    /// </summary>
    public static class KitVirtual {
        public static KitVirtualRange Window(float scrollTop,float viewport,int count,int columns,float step,int overscan){
            if(count<=0||step<=0f||columns<1)return new KitVirtualRange(0,0);
            int rows=KitTileLayout.Rows(count,columns);
            float top=KitMath.Max(0f,scrollTop);
            int firstRow=(int)Math.Floor(top/step)-Math.Max(0,overscan);
            int endRow=(int)Math.Ceiling((top+KitMath.Max(0f,viewport))/step)+Math.Max(0,overscan);
            firstRow=Math.Min(Math.Max(firstRow,0),rows);
            endRow=Math.Min(Math.Max(endRow,firstRow),rows);
            return new KitVirtualRange(Math.Min(firstRow*columns,count),Math.Min(endRow*columns,count));
        }
    }

    /// <summary>
    /// Rows of different heights for a virtualised list (Almanac AX02): offsets from the heights, the row under a
    /// point, the rows a viewport shows, and the scroll that keeps a focused row in view after a rebuild.
    /// </summary>
    public sealed class KitVirtualRows {
        readonly float[] offsets;

        public KitVirtualRows(IEnumerable<float> heights){
            if(heights==null)throw new ArgumentNullException(nameof(heights));
            var list=new List<float>{0f};
            float total=0f;
            foreach(var height in heights){
                if(height<0f||float.IsNaN(height))throw new ArgumentOutOfRangeException(nameof(heights));
                total+=height;
                list.Add(total);
            }
            offsets=list.ToArray();
        }

        public int Count=>offsets.Length-1;
        public float Total=>offsets[offsets.Length-1];
        /// <summary>The top of row <paramref name="index"/> (the total for <see cref="Count"/>).</summary>
        public float Offset(int index)=>offsets[Math.Min(Math.Max(index,0),Count)];
        public float Height(int index)=>Offset(index+1)-Offset(index);

        /// <summary>The row under <paramref name="y"/> (from the top), clamped to the first and last row; -1 with no rows.</summary>
        public int IndexAt(float y){
            if(Count==0)return -1;
            if(y<=0f)return 0;
            int low=0,high=Count-1;
            while(low<high){
                int middle=(low+high+1)/2;
                if(offsets[middle]<=y)low=middle;else high=middle-1;
            }
            return low;
        }

        public KitVirtualRange Range(float scrollTop,float viewport,int overscan){
            if(Count==0)return new KitVirtualRange(0,0);
            float top=KitMath.Max(0f,scrollTop),bottom=top+KitMath.Max(0f,viewport);
            int first=IndexAt(top);
            int last=IndexAt(bottom);
            if(last>first&&offsets[last]>=bottom)last--;
            first=Math.Max(0,first-Math.Max(0,overscan));
            int end=Math.Min(Count,last+1+Math.Max(0,overscan));
            return new KitVirtualRange(first,end);
        }

        /// <summary>The scroll position that shows row <paramref name="index"/> whole, moving as little as possible.</summary>
        public float Keep(int index,float scrollTop,float viewport){
            if(Count==0)return 0f;
            float top=Offset(index),bottom=Offset(index+1);
            if(top<scrollTop)return top;
            if(bottom>scrollTop+viewport)return KitMath.Max(0f,KitMath.Min(top,bottom-viewport));
            return scrollTop;
        }
    }
}
