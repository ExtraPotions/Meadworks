// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// The arrangement follows PackPanel 0.15.0's inventory screen (docs/research/packpanel-ui-baseline.md): the grid at the top
// left, a status column and the equipment panel beside it, the character header and the crafting panel in a column at the
// right. The rules are PackPanel-free: rectangles only, written for this kit.
using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>The named places of the inventory screen. Products register panels into a region instead of positioning themselves.</summary>
    public enum KitRegion {
        /// <summary>The player's grid panel, top left.</summary>
        Inventory,
        /// <summary>The small column of stat boxes (armor, weight, tier) right of the grid.</summary>
        Status,
        /// <summary>The equipment panel: slot tabs, the stat sheet, coins, keys and tackle.</summary>
        Equipment,
        /// <summary>The crafting panel, the full height of the right column under the header.</summary>
        Crafting,
        /// <summary>The character header (name and tabs) over the crafting panel.</summary>
        Header,
        /// <summary>Free room under the equipment panel for product panels (loadouts, secondary attacks, buttons).</summary>
        Extras,
    }

    /// <summary>What the layout needs to know: the screen and the natural sizes of the panels. Sizes are in canvas units.</summary>
    public sealed class KitScreenInput {
        public float CanvasWidth { get; set; }=1920f;
        public float CanvasHeight { get; set; }=1080f;
        /// <summary>The grid panel's own size.</summary>
        public KitVector Inventory { get; set; }=new KitVector(566f,330f);
        /// <summary>Width of the status column; 0 for none.</summary>
        public float StatusWidth { get; set; }=57f;
        public float StatusHeight { get; set; }=190f;
        /// <summary>The equipment panel's natural size; zero for none.</summary>
        public KitVector Equipment { get; set; }=new KitVector(363f,561f);
        /// <summary>The crafting panel's preferred size; the width is what the layout works with.</summary>
        public KitVector Crafting { get; set; }=new KitVector(670f,740f);
        public float HeaderHeight { get; set; }=146f;
        public float MinCraftingWidth { get; set; }=KitScreenRules.DefaultMinCraftingWidth;
        public float MinCraftingHeight { get; set; }=KitScreenRules.DefaultMinCraftingHeight;
        public float Margin { get; set; }=KitScreenRules.DefaultMargin;
        /// <summary>Where the grid panel actually starts (its left and top distance from the screen edge); 0 or less uses <see cref="Margin"/>.</summary>
        public float LeftMargin { get; set; }
        public float TopMargin { get; set; }
        public float SideGap { get; set; }=KitScreenRules.DefaultSideGap;
        /// <summary>The gap between the grid panel and the panel right of it, when the product's frames reach further than the others'; negative: <see cref="SideGap"/>.</summary>
        public float InventoryGap { get; set; }=-1f;
        public float BottomClear { get; set; }=KitScreenRules.DefaultBottomClear;
    }

    /// <summary>The rectangles of every region, canvas coordinates with y upward and the origin at the bottom left.</summary>
    public sealed class KitScreenPlan {
        readonly Dictionary<KitRegion,KitRect> rects;
        internal KitScreenPlan(Dictionary<KitRegion,KitRect> rects,float scale,bool fits,float width,float height){this.rects=rects;Scale=scale;Fits=fits;CanvasWidth=width;CanvasHeight=height;}
        public KitRect Rect(KitRegion region)=>rects.TryGetValue(region,out var r)?r:new KitRect(0f,0f,0f,0f);
        /// <summary>The factor the left group and the right column are scaled by so everything fits; 1 when the screen is roomy.</summary>
        public float Scale { get; }
        /// <summary>False when even the smallest scale leaves the arrangement too big for the screen (the rectangles still stay inside it).</summary>
        public bool Fits { get; }
        public float CanvasWidth { get; }
        public float CanvasHeight { get; }
    }

    public static class KitScreenRules {
        public const float DefaultMargin=16f;
        /// <summary>Between the grid, the status column and the equipment panel (PackPanel's SideGap).</summary>
        public const float DefaultSideGap=4f;
        /// <summary>Between the header and the crafting panel under it.</summary>
        public const float DefaultGap=4f;
        /// <summary>Between the left group and the crafting column.</summary>
        public const float DefaultCraftGap=16f;
        /// <summary>Kept clear at the bottom for the game's key hints (PackPanel's crafting BottomMargin).</summary>
        public const float DefaultBottomClear=70f;
        public const float DefaultMinScale=0.5f;
        public const float DefaultMinCraftingWidth=360f;
        public const float DefaultMinCraftingHeight=300f;
        public const float MinDockWidth=120f;
        public const float MinDockHeight=40f;
        /// <summary>The widest the dock beside a grid with no equipment panel grows.</summary>
        public const float BesideDockWidth=420f;

        static float Pos(float value,float fallback)=>KitMapRules.Finite(value)&&value>0f?value:fallback;
        static float NonNegative(float value)=>KitMapRules.Finite(value)&&value>0f?value:0f;
        static KitVector Size(KitVector value)=>new KitVector(NonNegative(value.X),NonNegative(value.Y));

        /// <summary>
        /// The regions for a screen. Left group at the top left (Inventory, Status, Equipment), the Header and Crafting column
        /// at the right margin, the Extras dock under the equipment panel (beside the grid when there is none). When the natural
        /// sizes do not fit, the crafting column narrows down to its minimum, then the whole arrangement scales down to
        /// <see cref="DefaultMinScale"/>. Every rectangle stays inside the screen and no two overlap.
        /// </summary>
        public static KitScreenPlan Plan(KitScreenInput input){
            float W=Pos(input.CanvasWidth,1920f),H=Pos(input.CanvasHeight,1080f);
            float M=KitMath.Min(NonNegative(input.Margin),KitMath.Min(W,H)/4f),G=NonNegative(input.SideGap),CG=DefaultCraftGap,GAP=DefaultGap;
            float BC=KitMath.Min(NonNegative(input.BottomClear),H/3f);
            float LM=input.LeftMargin>0f&&KitMapRules.Finite(input.LeftMargin)?KitMath.Min(input.LeftMargin,W/4f):M,TM=input.TopMargin>0f&&KitMapRules.Finite(input.TopMargin)?KitMath.Min(input.TopMargin,H/4f):M;
            var inv=Size(input.Inventory);
            float sw=NonNegative(input.StatusWidth),sh=sw>0f?NonNegative(input.StatusHeight):0f;
            var eq=Size(input.Equipment);
            if(eq.X<=0f||eq.Y<=0f)eq=KitVector.Zero;
            float hh=NonNegative(input.HeaderHeight);
            float minCW=Pos(input.MinCraftingWidth,DefaultMinCraftingWidth),minCH=Pos(input.MinCraftingHeight,DefaultMinCraftingHeight);
            float prefCW=KitMath.Max(minCW,Pos(input.Crafting.X,minCW));

            float IG=KitMapRules.Finite(input.InventoryGap)&&input.InventoryGap>=0f?input.InventoryGap:G;
            float gaps=(sw>0f||eq.X>0f?IG:0f)+(sw>0f&&eq.X>0f?G:0f);
            float gw=inv.X+sw+eq.X;
            float gh=KitMath.Max(inv.Y,KitMath.Max(sh,eq.Y));
            float scale=1f;
            if(gw+minCW>0f)scale=KitMath.Min(scale,(W-LM-M-CG-gaps)/(gw+minCW));
            if(gh>0f)scale=KitMath.Min(scale,(H-TM-M)/gh);
            if(hh+GAP+minCH>0f)scale=KitMath.Min(scale,(H-TM-BC)/(hh+GAP+minCH));
            bool fits=scale>=DefaultMinScale-0.0001f;
            scale=KitMath.Clamp(KitMapRules.Finite(scale)?scale:DefaultMinScale,DefaultMinScale,1f);

            var rects=new Dictionary<KitRegion,KitRect>();
            float top=H-TM,x=LM;
            rects[KitRegion.Inventory]=Clip(new KitRect(x,top-inv.Y*scale,inv.X*scale,inv.Y*scale),W,H);
            x+=inv.X*scale;
            bool first=true;
            if(sw>0f){x+=IG;first=false;rects[KitRegion.Status]=Clip(new KitRect(x,top-sh*scale,sw*scale,sh*scale),W,H);x+=sw*scale;}
            else rects[KitRegion.Status]=new KitRect(x,top,0f,0f);
            if(eq.X>0f){x+=first?IG:G;rects[KitRegion.Equipment]=Clip(new KitRect(x,top-eq.Y*scale,eq.X*scale,eq.Y*scale),W,H);x+=eq.X*scale;}
            else rects[KitRegion.Equipment]=new KitRect(x,top,0f,0f);
            float groupRight=x;

            float avail=KitMath.Max(0f,W-M-(groupRight+CG));
            float craftW=KitMath.Min(avail,KitMath.Max(minCW*scale,prefCW*scale));
            float colX=W-M-craftW;
            float headerH=KitMath.Min(hh*scale,H-TM-BC);
            rects[KitRegion.Header]=Clip(new KitRect(colX,top-headerH,craftW,headerH),W,H);
            float craftTop=top-headerH-(headerH>0f?GAP:0f);
            rects[KitRegion.Crafting]=Clip(new KitRect(colX,BC,craftW,KitMath.Max(0f,craftTop-BC)),W,H);
            rects[KitRegion.Extras]=Dock(rects,groupRight,colX-CG,top,BC,W,H,sw>0f,eq.X>0f);
            return new KitScreenPlan(rects,scale,fits,W,H);
        }

        static KitRect Dock(Dictionary<KitRegion,KitRect> rects,float groupRight,float limitRight,float top,float bottom,float W,float H,bool status,bool equipment){
            var inv=rects[KitRegion.Inventory];
            float left,right,dockTop;
            if(status||equipment){
                var st=rects[KitRegion.Status];var eq=rects[KitRegion.Equipment];
                left=status?st.XMin:eq.XMin;
                right=equipment?eq.XMax:st.XMax;
                float lowest=top;
                if(status)lowest=KitMath.Min(lowest,st.YMin);
                if(equipment)lowest=KitMath.Min(lowest,eq.YMin);
                dockTop=lowest-DefaultSideGap;
            }else{
                left=inv.XMax+DefaultSideGap;
                right=KitMath.Min(left+BesideDockWidth,limitRight);
                dockTop=top;
            }
            float width=right-left,height=dockTop-bottom;
            if(width<MinDockWidth||height<MinDockHeight)return new KitRect(KitMath.Clamp(left,0f,W),KitMath.Clamp(dockTop,0f,H),0f,0f);
            return Clip(new KitRect(left,bottom,width,height),W,H);
        }

        static KitRect Clip(KitRect r,float W,float H){
            float x0=KitMath.Clamp(r.XMin,0f,W),y0=KitMath.Clamp(r.YMin,0f,H),x1=KitMath.Clamp(r.XMax,0f,W),y1=KitMath.Clamp(r.YMax,0f,H);
            return KitRect.MinMax(x0,y0,KitMath.Max(x0,x1),KitMath.Max(y0,y1));
        }
    }

    /// <summary>
    /// Keeps the last plan while the input is the same: the inventory screen plans every frame while it is open, and a plan
    /// builds a dictionary of regions. Any changed input value plans afresh.
    /// </summary>
    public sealed class KitScreenPlanner {
        readonly KitScreenInput last=new KitScreenInput();
        KitScreenPlan? plan;
        public KitScreenPlan Plan(KitScreenInput input){
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(plan!=null&&Same(input,last))return plan;
            plan=KitScreenRules.Plan(input);
            Copy(input,last);
            return plan;
        }
        /// <summary>The next <see cref="Plan"/> plans afresh.</summary>
        public void Reset()=>plan=null;
        static bool Same(KitScreenInput a,KitScreenInput b)=>a.CanvasWidth==b.CanvasWidth&&a.CanvasHeight==b.CanvasHeight&&a.Inventory==b.Inventory&&a.StatusWidth==b.StatusWidth&&a.StatusHeight==b.StatusHeight
            &&a.Equipment==b.Equipment&&a.Crafting==b.Crafting&&a.HeaderHeight==b.HeaderHeight&&a.MinCraftingWidth==b.MinCraftingWidth&&a.MinCraftingHeight==b.MinCraftingHeight
            &&a.Margin==b.Margin&&a.LeftMargin==b.LeftMargin&&a.TopMargin==b.TopMargin&&a.SideGap==b.SideGap&&a.InventoryGap==b.InventoryGap&&a.BottomClear==b.BottomClear;
        static void Copy(KitScreenInput from,KitScreenInput to){
            to.CanvasWidth=from.CanvasWidth;to.CanvasHeight=from.CanvasHeight;to.Inventory=from.Inventory;to.StatusWidth=from.StatusWidth;to.StatusHeight=from.StatusHeight;
            to.Equipment=from.Equipment;to.Crafting=from.Crafting;to.HeaderHeight=from.HeaderHeight;to.MinCraftingWidth=from.MinCraftingWidth;to.MinCraftingHeight=from.MinCraftingHeight;
            to.Margin=from.Margin;to.LeftMargin=from.LeftMargin;to.TopMargin=from.TopMargin;to.SideGap=from.SideGap;to.InventoryGap=from.InventoryGap;to.BottomClear=from.BottomClear;
        }
    }

    /// <summary>One panel for the dock's flow.</summary>
    public readonly struct KitDockItem {
        public string Id { get; }
        public KitVector Size { get; }
        /// <summary>How far the panel's drawn frame reaches past <see cref="Size"/> on every side (the under-inventory dock keeps it clear).</summary>
        public float Frame { get; }
        public KitDockItem(string id,KitVector size){Id=id;Size=size;Frame=0f;}
        public KitDockItem(string id,KitVector size,float frame){Id=id;Size=size;Frame=frame;}
    }

    /// <summary>Where a dock panel goes, or that it did not fit.</summary>
    public readonly struct KitDockSlot {
        public string Id { get; }
        public KitRect Rect { get; }
        public bool Shown { get; }
        public KitDockSlot(string id,KitRect rect,bool shown){Id=id;Rect=rect;Shown=shown;}
    }

    public static class KitDock {
        /// <summary>
        /// Panels left to right in rows from the dock's top left, a new row when the next one would pass the right edge; a
        /// panel wider than the dock is narrowed to it. A panel that no longer fits in height, or has no usable size, is not
        /// shown; later smaller panels still get their turn. Panels never overlap and never leave the dock.
        /// </summary>
        public static IReadOnlyList<KitDockSlot> Flow(KitRect dock,IReadOnlyList<KitDockItem> items,float gap){
            var slots=new List<KitDockSlot>(items.Count);Flow(dock,items,gap,slots);return slots;
        }
        /// <summary><see cref="Flow(KitRect,IReadOnlyList{KitDockItem},float)"/> into a caller's list (cleared first), for per-frame callers.</summary>
        public static void Flow(KitRect dock,IReadOnlyList<KitDockItem> items,float gap,List<KitDockSlot> slots){
            slots.Clear();
            float g=KitMapRules.Finite(gap)&&gap>0f?gap:0f;
            float x=dock.XMin,y=dock.YMax,rowHeight=0f;
            const float eps=0.001f;
            for(int i=0;i<items.Count;i++){
                var item=items[i];
                float w=item.Size.X,h=item.Size.Y;
                if(!KitMapRules.Finite(w)||!KitMapRules.Finite(h)||w<=0f||h<=0f||dock.Width<=0f||dock.Height<=0f){slots.Add(new KitDockSlot(item.Id,new KitRect(dock.XMin,dock.YMax,0f,0f),false));continue;}
                w=KitMath.Min(w,dock.Width);
                if(x>dock.XMin+eps&&x+w>dock.XMax+eps){y-=rowHeight+g;x=dock.XMin;rowHeight=0f;}
                if(y-h<dock.YMin-eps){slots.Add(new KitDockSlot(item.Id,new KitRect(dock.XMin,dock.YMax,0f,0f),false));continue;}
                slots.Add(new KitDockSlot(item.Id,new KitRect(x,y-h,w,h),true));
                x+=w+g;rowHeight=KitMath.Max(rowHeight,h);
            }
        }
    }

    /// <summary>A panel a product registered into a region.</summary>
    public sealed class KitPanelRegistration {
        internal KitPanelRegistration(string id,KitRegion region,int order,Func<KitVector> size,Action<KitRect> place,Func<bool>? visible,Action? hide){Id=id;Region=region;Order=order;Size=size;Place=place;Visible=visible;Hide=hide;}
        public string Id { get; }
        public KitRegion Region { get; }
        public int Order { get; }
        /// <summary>The panel's natural size now (canvas units, before any scale).</summary>
        public Func<KitVector> Size { get; }
        /// <summary>Called with the rectangle the panel gets, canvas coordinates; only when it changed.</summary>
        public Action<KitRect> Place { get; }
        public Func<bool>? Visible { get; }
        /// <summary>Called when the panel stops being shown (not visible, or no room).</summary>
        public Action? Hide { get; }
        internal KitRect LastRect;
        internal bool LastShown;
    }

    /// <summary>
    /// The panels products registered into the inventory screen's regions, the sizes products report for the regions they own,
    /// and the application of a plan: each region's panels flow inside the region's rectangle in order (then id).
    /// </summary>
    public sealed class KitScreenRegistry {
        readonly object gate=new object();
        readonly Dictionary<string,KitPanelRegistration> panels=new Dictionary<string,KitPanelRegistration>(StringComparer.Ordinal);
        readonly Dictionary<KitRegion,KitVector> reported=new Dictionary<KitRegion,KitVector>();
        // Apply runs every frame while the inventory is open: the regions, each region's sorted panels and the working lists
        // are kept. A sorted list is rebuilt, as a new list, only after a register or a removal.
        static readonly KitRegion[] Regions=(KitRegion[])Enum.GetValues(typeof(KitRegion));
        readonly List<KitPanelRegistration>?[] sorted=new List<KitPanelRegistration>?[Regions.Length];
        int changes,sortedAt=-1;
        sealed class Work {
            public readonly List<KitDockSlot> All=new List<KitDockSlot>(),Slots=new List<KitDockSlot>();
            public readonly List<KitDockItem> Items=new List<KitDockItem>();
            public readonly List<KitPanelRegistration> Shown=new List<KitPanelRegistration>(),Hidden=new List<KitPanelRegistration>();
        }
        readonly Work work=new Work();bool applying;
        /// <summary>The gap a product wants between the grid panel and the panel beside it (see <see cref="KitScreenInput.InventoryGap"/>); negative for the default.</summary>
        public float InventoryGap { get; set; }=-1f;
        public int Failures { get; private set; }
        public string LastError { get; private set; }="";

        /// <summary>Registers a panel; the same id again replaces the earlier registration.</summary>
        public IDisposable Register(string id,KitRegion region,int order,Func<KitVector> size,Action<KitRect> place,Func<bool>? visible,Action? hide){
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("A panel needs an id.",nameof(id));
            if(!Enum.IsDefined(typeof(KitRegion),region))throw new ArgumentOutOfRangeException(nameof(region));
            if(size==null)throw new ArgumentNullException(nameof(size));
            if(place==null)throw new ArgumentNullException(nameof(place));
            var registration=new KitPanelRegistration(id,region,order,size,place,visible,hide);
            lock(gate){panels[id]=registration;changes++;}
            return new Handle(this,registration);
        }

        public IReadOnlyList<KitPanelRegistration> Panels(KitRegion region){
            var list=new List<KitPanelRegistration>();
            lock(gate)foreach(var panel in panels.Values)if(panel.Region==region)list.Add(panel);
            list.Sort(ByOrder);
            return list;
        }
        static readonly Comparison<KitPanelRegistration> ByOrder=(a,b)=>{int c=a.Order.CompareTo(b.Order);return c!=0?c:string.CompareOrdinal(a.Id,b.Id);};

        // The sorted panels of a region, kept until the next register or removal; never changed in place, so a list being
        // walked stays valid when a panel registers during Apply.
        List<KitPanelRegistration> Sorted(int index){
            lock(gate){
                if(sortedAt!=changes){Array.Clear(sorted,0,sorted.Length);sortedAt=changes;}
                return sorted[index]??(sorted[index]=(List<KitPanelRegistration>)Panels(Regions[index]));
            }
        }

        /// <summary>A product says how big a region's panel is now (Status, Equipment, Crafting, Header). A non-finite size is ignored; zero removes it.</summary>
        public void Report(KitRegion region,KitVector size){
            if(!KitMapRules.Finite(size)||size.X<0f||size.Y<0f)return;
            lock(gate)reported[region]=size;
        }

        /// <summary>The size a product reported for a region, if it did and it is not zero.</summary>
        public bool TryReported(KitRegion region,out KitVector size){
            lock(gate)return reported.TryGetValue(region,out size)&&size.X>0f&&size.Y>0f;
        }

        /// <summary>The input for a plan: the screen, the grid panel's size, and the sizes products reported.</summary>
        public KitScreenInput Input(float canvasWidth,float canvasHeight,KitVector inventory){
            var input=new KitScreenInput{CanvasWidth=canvasWidth,CanvasHeight=canvasHeight,Inventory=inventory,StatusWidth=0f,StatusHeight=0f,Equipment=KitVector.Zero,InventoryGap=InventoryGap};
            lock(gate){
                if(reported.TryGetValue(KitRegion.Status,out var status)){input.StatusWidth=status.X;input.StatusHeight=status.Y;}
                if(reported.TryGetValue(KitRegion.Equipment,out var equipment))input.Equipment=equipment;
                if(reported.TryGetValue(KitRegion.Crafting,out var crafting)&&crafting.X>0f)input.Crafting=crafting;
                if(reported.TryGetValue(KitRegion.Header,out var header)&&header.Y>0f)input.HeaderHeight=header.Y;
            }
            return input;
        }

        /// <summary>Places every registered panel for the plan; a panel that throws is counted and the others carry on.</summary>
        /// <remarks>The returned list is reused: it is valid until the next Apply.</remarks>
        public IReadOnlyList<KitDockSlot> Apply(KitScreenPlan plan){
            // A nested Apply (from a panel's callback) gets its own lists.
            if(applying)return Apply(plan,new Work());
            applying=true;
            try{return Apply(plan,work);}finally{applying=false;}
        }
        IReadOnlyList<KitDockSlot> Apply(KitScreenPlan plan,Work w){
            var all=w.All;all.Clear();
            for(int r=0;r<Regions.Length;r++){
                var region=Regions[r];
                var list=Sorted(r);
                if(list.Count==0)continue;
                var items=w.Items;var shownList=w.Shown;var hiddenList=w.Hidden;items.Clear();shownList.Clear();hiddenList.Clear();
                for(int p=0;p<list.Count;p++){
                    var panel=list[p];
                    KitVector size=KitVector.Zero;bool visible;
                    try{visible=panel.Visible==null||panel.Visible();if(visible)size=panel.Size();}
                    catch(Exception error){Fail(error);visible=false;}
                    if(!visible){hiddenList.Add(panel);all.Add(new KitDockSlot(panel.Id,new KitRect(0f,0f,0f,0f),false));continue;}
                    items.Add(new KitDockItem(panel.Id,size));shownList.Add(panel);
                }
                var slots=w.Slots;KitDock.Flow(plan.Rect(region),items,KitScreenRules.DefaultSideGap,slots);
                for(int i=0;i<slots.Count;i++){
                    all.Add(slots[i]);
                    var panel=shownList[i];
                    try{
                        if(slots[i].Shown){
                            if(!panel.LastShown||panel.LastRect!=slots[i].Rect){panel.LastRect=slots[i].Rect;panel.LastShown=true;panel.Place(slots[i].Rect);}
                        }else Hidden(panel);
                    }catch(Exception error){Fail(error);}
                }
                for(int h=0;h<hiddenList.Count;h++){try{Hidden(hiddenList[h]);}catch(Exception error){Fail(error);}}
            }
            return all;
        }

        /// <summary>The inventory closed: every panel that was shown is told to hide, and is placed afresh the next time.</summary>
        public void HideAll(){
            KitPanelRegistration[] all;
            lock(gate){all=new KitPanelRegistration[panels.Count];panels.Values.CopyTo(all,0);}
            foreach(var panel in all){try{Hidden(panel);}catch(Exception error){Fail(error);}}
        }

        static void Hidden(KitPanelRegistration panel){
            if(!panel.LastShown)return;
            panel.LastShown=false;
            panel.Hide?.Invoke();
        }

        void Fail(Exception error){Failures++;LastError=error.GetType().Name+": "+error.Message;}

        sealed class Handle:IDisposable {
            readonly KitScreenRegistry owner;readonly KitPanelRegistration registration;
            public Handle(KitScreenRegistry owner,KitPanelRegistration registration){this.owner=owner;this.registration=registration;}
            public void Dispose(){
                lock(owner.gate)if(owner.panels.TryGetValue(registration.Id,out var current)&&ReferenceEquals(current,registration)){owner.panels.Remove(registration.Id);owner.changes++;}
            }
        }
    }

    /// <summary>The shared registry products use. Crucible.Core.dll loads once, so every product sees the same panels.</summary>
    public static class KitScreen {
        public static KitScreenRegistry Registry { get; }=new KitScreenRegistry();
        /// <summary>Registers a panel into a region at an order (lower first). Dispose the result to remove it.</summary>
        public static IDisposable Register(string id,KitRegion region,int order,Func<KitVector> size,Action<KitRect> place,Func<bool>? visible=null,Action? hide=null)=>Registry.Register(id,region,order,size,place,visible,hide);
        public static void Report(KitRegion region,KitVector size)=>Registry.Report(region,size);
    }
}
