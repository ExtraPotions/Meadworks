namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The search row (KX03) along the top of a list rect: a kit text field and square icon buttons at its right end,
    /// <see cref="Height"/> high, the list given up <see cref="Lost"/> below it (kindle-design.md 3.2).
    /// </summary>
    public static class KitSearchLayout {
        public const float Height=KitButtonRow.StripHeight;
        /// <summary>Between the row and the list under it.</summary>
        public const float Gap=4f;
        public const float Button=KitButtonRow.ButtonHeight;
        public const float Between=KitTabLayout.Between;
        public const float Lost=Height+Gap;

        /// <summary>The field's width: the row less one button and gap per button.</summary>
        public static float FieldWidth(float rowWidth,int buttons)=>KitMath.Max(0f,rowWidth-buttons*(Button+Between));

        /// <summary>A button's left edge in the row, counting from the right end (0 is the rightmost).</summary>
        public static float ButtonX(float rowWidth,int fromRight)=>rowWidth-Button-fromRight*(Button+Between);

        /// <summary>Buttons centered vertically in the row (top-left anchored, y down).</summary>
        public const float ButtonY=-(Height-Button)/2f;
    }

    /// <summary>Where the row and the list go after <see cref="KitListShrink.Apply"/> (top-left anchored, y down).</summary>
    public readonly struct KitShrinkResult {
        public KitVector RowPosition { get; }
        public KitVector RowSize { get; }
        /// <summary>The list must be set to <see cref="ListPosition"/> and <see cref="ListSize"/>.</summary>
        public bool Write { get; }
        public KitVector ListPosition { get; }
        public KitVector ListSize { get; }
        public KitShrinkResult(KitVector rowPosition,KitVector rowSize,bool write,KitVector listPosition,KitVector listSize){RowPosition=rowPosition;RowSize=rowSize;Write=write;ListPosition=listPosition;ListSize=listSize;}
    }

    /// <summary>
    /// The list gives up the row's height while the row shows, from its full size: what this left it at plus the row,
    /// or, when another mod set it since (a larger panel), what it is now. Called every update, so the row follows the
    /// list's size; when the row turns off a list still as this left it is restored, one another mod set is left alone.
    /// </summary>
    public sealed class KitListShrink {
        KitVector setPosition,setSize;
        bool shrunk;

        public bool Shrunk=>shrunk;

        public KitShrinkResult Apply(KitVector position,KitVector size,bool on,float lost=KitSearchLayout.Lost,float rowHeight=KitSearchLayout.Height){
            var drop=new KitVector(0f,lost);
            bool ours=shrunk&&position==setPosition&&size==setSize;
            if(ours){position+=drop;size+=drop;}
            shrunk=on;
            var rowSize=new KitVector(size.X,rowHeight);
            if(on){
                setPosition=position-drop;
                setSize=size-drop;
                return new KitShrinkResult(position,rowSize,true,setPosition,setSize);
            }
            return new KitShrinkResult(position,rowSize,ours,position,size);
        }

        public void Reset(){shrunk=false;setPosition=setSize=KitVector.Zero;}
    }

    /// <summary>A one-time failure: the first <see cref="Fail"/> asks for the warning, later ones stay quiet for the session.</summary>
    public sealed class KitLatch {
        public bool Failed { get; private set; }
        public bool Fail(){if(Failed)return false;Failed=true;return true;}
        public void Reset(){Failed=false;}
    }
}
