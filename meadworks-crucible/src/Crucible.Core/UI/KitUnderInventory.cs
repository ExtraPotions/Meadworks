// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// User decisions 2026-10-12 20:19, 22:35 and 22:41: the room directly under the player inventory panel is shared. Burrow's
// stow row stands right under the panel; Edge's cooldown panel docks below whatever is already there, or goes back to its
// HUD spot when there is no room. Rules only, no Unity: Crucible.UI measures the screen and calls Apply when something changed.
using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Where the under-inventory dock may put panels, in the panel root's coordinates (y up, origin bottom left).</summary>
    public readonly struct KitUnderInventoryRoom:IEquatable<KitUnderInventoryRoom> {
        /// <summary>The x the entries are centered on (the center of the panel they hang under).</summary>
        public float CenterX { get; }
        /// <summary>The bottom edge of the player panel's drawn frame, or of the open container panel's when that is lower; the first entry hangs a gap under it.</summary>
        public float Top { get; }
        /// <summary>The lowest y an entry may reach (the key hints).</summary>
        public float Floor { get; }
        public float Left { get; }
        public float Right { get; }
        public KitUnderInventoryRoom(float centerX,float top,float floor,float left,float right){CenterX=centerX;Top=top;Floor=floor;Left=left;Right=right;}
        /// <summary>
        /// The room under the lowest visible of the player panel and the open container panel (ruling 2026-10-13): with a
        /// chest open the whole stack moves under the container panel, centered on it, so it never covers the chest; otherwise it hangs under
        /// the player panel. <paramref name="floor"/>, <paramref name="left"/> and <paramref name="right"/> bound it as before.
        /// </summary>
        public static KitUnderInventoryRoom Under(KitRect player,KitRect? container,float floor,float left,float right){
            KitRect anchor=container is KitRect open&&open.Width>0f&&open.Height>0f&&open.YMin<player.YMin?open:player;
            return new KitUnderInventoryRoom(anchor.XMin+anchor.Width/2f,anchor.YMin,floor,left,right);
        }
        public bool Usable=>KitMapRules.Finite(CenterX)&&KitMapRules.Finite(Top)&&KitMapRules.Finite(Floor)&&KitMapRules.Finite(Left)&&KitMapRules.Finite(Right)&&Right>Left&&Top>Floor;
        public bool Equals(KitUnderInventoryRoom o)=>CenterX==o.CenterX&&Top==o.Top&&Floor==o.Floor&&Left==o.Left&&Right==o.Right;
        public override bool Equals(object? obj)=>obj is KitUnderInventoryRoom o&&Equals(o);
        public override int GetHashCode()=>CenterX.GetHashCode()^Top.GetHashCode()*397^Floor.GetHashCode()*7919^Left.GetHashCode()*31^Right.GetHashCode()*17;
    }

    /// <summary>A panel a product registered under the inventory.</summary>
    public sealed class KitUnderInventoryEntry {
        internal KitUnderInventoryEntry(string id,int order,Func<KitVector> size,Action<KitRect> place,Func<bool>? visible,Action? hide){Id=id;Order=order;Size=size;Place=place;Visible=visible;Hide=hide;}
        public string Id { get; }
        public int Order { get; }
        /// <summary>The panel's size now (root units).</summary>
        public Func<KitVector> Size { get; }
        /// <summary>Called with the panel's rectangle in root coordinates; only when it changed or the panel was hidden before.</summary>
        public Action<KitRect> Place { get; }
        public Func<bool>? Visible { get; }
        /// <summary>Called when the panel stops being shown (not visible, no room, the inventory closed or the entry was removed).</summary>
        public Action? Hide { get; }
        internal KitRect LastRect;
        internal bool LastShown;
        /// <summary>How far the panel's drawn frame reached past its rectangle when it was last placed (see <see cref="KitUnderInventoryRegistry.ReportFrame"/>).</summary>
        internal float Frame;
    }

    /// <summary>
    /// The entries under the inventory, laid out top to bottom by order (then id), each centered on the player panel, a gap
    /// apart. An entry that does not fit above the floor is hidden; later smaller ones still get their turn. Nothing here
    /// runs per frame: <see cref="Dirty"/> tells the runtime to lay out again, which it also does when the screen moved.
    /// </summary>
    public sealed class KitUnderInventoryRegistry {
        public const float Gap=KitScreenRules.DefaultSideGap;
        readonly object gate=new object();
        readonly Dictionary<string,KitUnderInventoryEntry> entries=new Dictionary<string,KitUnderInventoryEntry>(StringComparer.Ordinal);
        readonly List<KitUnderInventoryEntry> ordered=new List<KitUnderInventoryEntry>();
        readonly List<KitDockItem> items=new List<KitDockItem>();
        readonly List<KitUnderInventoryEntry> shownList=new List<KitUnderInventoryEntry>();
        volatile bool dirty=true;
        KitUnderInventoryEntry? placing;
        float reported;

        /// <summary>Raised after a layout changed which entries are shown (an entry gained or lost its place). Handlers must not throw.</summary>
        public event Action? Changed;
        public int Failures { get; private set; }
        public string LastError { get; private set; }="";
        /// <summary>Something asks for a new layout: an entry came or went, or a product said its panel changed (<see cref="MarkDirty"/>).</summary>
        public bool Dirty=>dirty;
        public bool HasEntries{get{lock(gate)return entries.Count>0;}}
        /// <summary>Height left under the last shown entry down to the floor at the last layout (a gap already taken off); 0 before one.</summary>
        public float Room { get; private set; }
        /// <summary>The room the last layout used.</summary>
        public KitUnderInventoryRoom LastRoom { get; private set; }

        /// <summary>A panel of this height would still fit under the shown entries.</summary>
        public bool Fits(float height)=>KitMapRules.Finite(height)&&height>0f&&height<=Room+0.001f;

        /// <summary>Registers an entry; the same id again replaces the earlier one. Dispose the result to remove it.</summary>
        public IDisposable Register(string id,int order,Func<KitVector> size,Action<KitRect> place,Func<bool>? visible,Action? hide){
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("An entry needs an id.",nameof(id));
            if(size==null)throw new ArgumentNullException(nameof(size));
            if(place==null)throw new ArgumentNullException(nameof(place));
            var entry=new KitUnderInventoryEntry(id,order,size,place,visible,hide);
            KitUnderInventoryEntry? old;
            lock(gate){entries.TryGetValue(id,out old);entries[id]=entry;Sort();}
            if(old!=null)Hidden(old);
            dirty=true;
            return new Handle(this,entry);
        }

        /// <summary>A product's panel changed size or visibility: lay out again at the next chance.</summary>
        public void MarkDirty()=>dirty=true;

        /// <summary>
        /// Called while an entry's <c>Place</c> runs (Crucible.UI's <c>KitUnderInventoryDock.PlaceTopLeft</c> does it): the panel
        /// draws a frame this far past its rectangle. The dock keeps that frame clear of the panel above and the floor; a new
        /// value lays out again at the next chance. Outside a Place call it does nothing.
        /// </summary>
        public void ReportFrame(float frame){if(placing!=null&&KitMapRules.Finite(frame)&&frame>reported)reported=frame;}

        /// <summary>The entry has a place under the inventory now.</summary>
        public bool Shown(string id){lock(gate)return id!=null&&entries.TryGetValue(id,out var e)&&e.LastShown;}

        public IReadOnlyList<KitUnderInventoryEntry> Entries(){lock(gate)return ordered.ToArray();}

        /// <summary>
        /// Stacks the panels: one per row from <paramref name="room"/>'s top, centered on its center x and kept between its
        /// left and right (narrowed to that width when wider). <paramref name="remaining"/> is the height left under the last
        /// placed panel, less one gap.
        /// </summary>
        public static IReadOnlyList<KitDockSlot> Stack(KitUnderInventoryRoom room,IReadOnlyList<KitDockItem> list,float gap,out float remaining){
            var slots=new List<KitDockSlot>(list.Count);
            float g=KitMapRules.Finite(gap)&&gap>0f?gap:0f;
            remaining=0f;
            if(!room.Usable){foreach(var item in list)slots.Add(new KitDockSlot(item.Id,new KitRect(0f,0f,0f,0f),false));return slots;}
            float y=room.Top-g;
            const float eps=0.001f;
            foreach(var item in list){
                float w=item.Size.X,h=item.Size.Y,f=KitMapRules.Finite(item.Frame)&&item.Frame>0f?item.Frame:0f;
                // The frame takes room on every side: the panel sits f under the space's top and needs f more under it.
                if(!KitMapRules.Finite(w)||!KitMapRules.Finite(h)||w<=0f||h<=0f||y-h-2f*f<room.Floor-eps){slots.Add(new KitDockSlot(item.Id,new KitRect(0f,0f,0f,0f),false));continue;}
                w=KitMath.Min(w,room.Right-room.Left-2f*f);
                if(w<=0f){slots.Add(new KitDockSlot(item.Id,new KitRect(0f,0f,0f,0f),false));continue;}
                float x=KitMath.Max(room.Left+f,KitMath.Min(room.CenterX-w/2f,room.Right-f-w));
                slots.Add(new KitDockSlot(item.Id,new KitRect(x,y-f-h,w,h),true));
                y-=h+2f*f+g;
            }
            remaining=KitMath.Max(0f,y-room.Floor);
            return slots;
        }

        /// <summary>Lays every entry out in the room; Place only for changed rectangles, Hide for lost places. Clears <see cref="Dirty"/>.</summary>
        public IReadOnlyList<KitDockSlot> Apply(KitUnderInventoryRoom room)=>Apply(room,true);

        // A frame learned while placing lays out once more at once, so a framed panel is never drawn over its neighbor for a frame.
        IReadOnlyList<KitDockSlot> Apply(KitUnderInventoryRoom room,bool settle){
            dirty=false;
            KitUnderInventoryEntry[] all;
            lock(gate)all=ordered.ToArray();
            items.Clear();shownList.Clear();
            bool changed=false,framed=false;
            foreach(var entry in all){
                bool visible;KitVector size=KitVector.Zero;
                try{visible=entry.Visible==null||entry.Visible();if(visible)size=entry.Size();}
                catch(Exception error){Fail(error);visible=false;}
                if(!visible){changed|=Hidden(entry);continue;}
                items.Add(new KitDockItem(entry.Id,size,entry.Frame));shownList.Add(entry);
            }
            var slots=Stack(room,items,Gap,out float remaining);
            Room=remaining;LastRoom=room;
            for(int i=0;i<slots.Count;i++){
                var entry=shownList[i];
                try{
                    if(slots[i].Shown){
                        if(!entry.LastShown||entry.LastRect!=slots[i].Rect){changed|=!entry.LastShown;entry.LastRect=slots[i].Rect;entry.LastShown=true;framed|=Placing(entry,slots[i].Rect);}
                    }else changed|=Hidden(entry);
                }catch(Exception error){Fail(error);}
            }
            if(changed)Raise();
            if(framed&&settle)return Apply(room,false);
            if(framed)dirty=true;
            return slots;
        }

        /// <summary>The inventory closed: every shown entry hides and is placed afresh the next time.</summary>
        public void HideAll(){
            KitUnderInventoryEntry[] all;
            lock(gate)all=ordered.ToArray();
            bool changed=false;
            foreach(var entry in all)changed|=Hidden(entry);
            Room=0f;dirty=true;
            if(changed)Raise();
        }

        // The entry's frame as its Place reports it; true when it changed (the layout must run again).
        bool Placing(KitUnderInventoryEntry entry,KitRect rect){
            placing=entry;reported=0f;
            try{entry.Place(rect);}
            finally{
                placing=null;
            }
            if(Math.Abs(reported-entry.Frame)<=0.01f)return false;
            entry.Frame=reported;return true;
        }

        bool Hidden(KitUnderInventoryEntry entry){
            if(!entry.LastShown)return false;
            entry.LastShown=false;
            try{entry.Hide?.Invoke();}catch(Exception error){Fail(error);}
            return true;
        }

        void Raise(){try{Changed?.Invoke();}catch(Exception error){Fail(error);}}
        void Sort(){
            ordered.Clear();ordered.AddRange(entries.Values);
            ordered.Sort((a,b)=>{int c=a.Order.CompareTo(b.Order);return c!=0?c:string.CompareOrdinal(a.Id,b.Id);});
        }
        void Fail(Exception error){Failures++;LastError=error.GetType().Name+": "+error.Message;}

        sealed class Handle:IDisposable {
            readonly KitUnderInventoryRegistry owner;readonly KitUnderInventoryEntry entry;
            public Handle(KitUnderInventoryRegistry owner,KitUnderInventoryEntry entry){this.owner=owner;this.entry=entry;}
            public void Dispose(){
                bool removed=false;
                lock(owner.gate)if(owner.entries.TryGetValue(entry.Id,out var current)&&ReferenceEquals(current,entry)){owner.entries.Remove(entry.Id);owner.Sort();removed=true;}
                if(!removed)return;
                if(owner.Hidden(entry))owner.Raise();
                owner.dirty=true;
            }
        }
    }

    /// <summary>The shared under-inventory dock. Crucible.Core.dll loads once, so every product sees the same entries.</summary>
    public static class KitUnderInventory {
        /// <summary>Burrow's stow row: directly under the panel.</summary>
        public const int StowRowOrder=100;
        /// <summary>Edge's docked cooldown panel: under the stow row.</summary>
        public const int CooldownsOrder=200;
        public static KitUnderInventoryRegistry Registry { get; }=new KitUnderInventoryRegistry();
        /// <summary>
        /// Registers a panel under the player inventory at an order (lower sits higher). <paramref name="place"/> gets the
        /// rectangle in the panel root's coordinates (Crucible.UI's <c>KitUnderInventoryDock.PlaceTopLeft</c> puts a
        /// RectTransform there). Call <see cref="MarkDirty"/> when the panel's size or visibility changes.
        /// </summary>
        public static IDisposable Register(string id,int order,Func<KitVector> size,Action<KitRect> place,Func<bool>? visible=null,Action? hide=null)=>Registry.Register(id,order,size,place,visible,hide);
        public static void MarkDirty()=>Registry.MarkDirty();
        public static bool Shown(string id)=>Registry.Shown(id);
        public static float Room=>Registry.Room;
        public static bool Fits(float height)=>Registry.Fits(height);
        public static event Action Changed{add=>Registry.Changed+=value;remove=>Registry.Changed-=value;}
    }
}
