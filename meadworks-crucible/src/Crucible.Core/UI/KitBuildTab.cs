// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// KX06 portable rules. Behaviour follows OpenKeep 3.0.0's Blueprints tab (BlueprintTab, HammerTable, BlueprintSelection,
// reference ad278ecf323901c3645eb1f9f6fac6833a0f8cb1), rebuilt generic for any product; no OpenKeep code is copied here.
using System;
using System.Collections.Generic;
using System.Text;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>What a product's build-menu tab is now (KX06).</summary>
    public enum KitBuildTabState { Hidden, Shown, Blocked }

    /// <summary>The product's answer for its tab: hidden, shown, or shown but blocked (C03, with the option for its reason).</summary>
    public readonly struct KitBuildTabView {
        public KitBuildTabState State { get; }
        public BlockedOption? Blocked { get; }
        KitBuildTabView(KitBuildTabState state,BlockedOption? blocked){State=state;Blocked=blocked;}
        public static KitBuildTabView Hidden=>new KitBuildTabView(KitBuildTabState.Hidden,null);
        public static KitBuildTabView Shown=>new KitBuildTabView(KitBuildTabState.Shown,null);
        /// <summary>Shown while the option is absent or allowed; blocked (visible, unusable, the reason as tooltip) otherwise.</summary>
        public static KitBuildTabView Block(BlockedOption? option)=>option==null||option.Allowed?Shown:new KitBuildTabView(KitBuildTabState.Blocked,option);
    }

    /// <summary>How a menu leaves a tab that just went hidden or blocked.</summary>
    public enum KitBuildTabLeave {
        /// <summary>The menu is not on the tab.</summary>
        Stay,
        /// <summary>The open menu shows the tab: select the first tab (the game redraws).</summary>
        SelectFirst,
        /// <summary>The menu is closed or has no build tool: only reset the remembered tab to the first.</summary>
        ResetIndex,
    }

    /// <summary>The tab button's state for a view (<see cref="KitBuildTabRules.Plan"/>).</summary>
    public readonly struct KitBuildTabPlan {
        /// <summary>The button is in the row.</summary>
        public bool ButtonActive { get; }
        /// <summary>The menu's tab handler holds the button, so the tab keys and pad bumpers step to it.</summary>
        public bool KeysReach { get; }
        public bool Interactable { get; }
        public KitBuildTabLeave Leave { get; }
        public KitBuildTabPlan(bool active,bool keys,bool interactable,KitBuildTabLeave leave){ButtonActive=active;KeysReach=keys;Interactable=interactable;Leave=leave;}
    }

    /// <summary>How a tab leaves a menu at product teardown.</summary>
    public enum KitBuildTabTeardown {
        /// <summary>The last tab: taken out of the piece lists, the tab buttons and the handler.</summary>
        Remove,
        /// <summary>Tabs follow it: it stays as an empty, hidden place so their indices never move.</summary>
        Neutralize,
    }

    /// <summary>What a click on a piece in the build menu does (<see cref="KitBuildTabRules.Click"/>).</summary>
    public enum KitBuildTabClick {
        /// <summary>Not a tab entry: the game's own click.</summary>
        Game,
        /// <summary>An ordinary entry: the game selects it, as before.</summary>
        Select,
        /// <summary>An action entry: the product acts on the click; the entry is never selected and the menu stays open.</summary>
        Act,
    }

    public static class KitBuildTabRules {
        public const int MaxIdLength=128;
        public const string PrefabPrefix="Meadworks_Tab_";

        /// <summary>
        /// How far a build menu's piece view (and its window) shrinks to fit its rows, keeping its top edge: 0 when the
        /// rows fill or overflow the view (the hammer), more for a short list (the cultivator). At least one row stays.
        /// </summary>
        public static float FitShrink(int pieces,int columns,float cellHeight,float spacing,float padding,float viewHeight){
            if(columns<1||viewHeight<=0f)return 0f;
            int rows=Math.Max(1,(pieces+columns-1)/columns);
            float needed=padding+rows*cellHeight+(rows-1)*spacing;
            return Math.Max(0f,viewHeight-needed);
        }

        /// <summary>Columns a grid fits across a width (cell plus spacing each, the last without spacing).</summary>
        public static int FitColumns(float width,float cellWidth,float spacing)=>cellWidth<=0f?1:Math.Max(1,(int)Math.Floor((width+spacing)/(cellWidth+spacing)+0.001f));
        /// <summary>Piece.PieceCategory.Misc, the entries' category when no spare one is left.</summary>
        public const int Misc=0;
        /// <summary>Piece.PieceCategory DeepNorth, Feasts, Food, Meads: build categories a hammer may leave unused.</summary>
        public static readonly int[] SpareCategories={5,6,7,8};
        const int NamePart=24;

        /// <summary>
        /// Why a build menu is not shaped as the tab service expects (null when it is): one piece list, one tab button and
        /// one handler tab per tab, and a tab row to copy a button from. A refused menu gets nothing added.
        /// </summary>
        public static string? MenuRefusal(int pieceLists,int tabButtons,bool hasHandler,int handlerTabs,int containerChildren){
            if(pieceLists<1)return "no_piece_lists";
            if(tabButtons!=pieceLists)return "tab_buttons_mismatch";
            if(!hasHandler)return "no_tab_handler";
            if(handlerTabs!=pieceLists)return "tab_handler_mismatch";
            if(containerChildren<1)return "no_tab_row";
            return null;
        }

        /// <summary>The tab button to copy: the last one of the game's own lists (never another product's tab), or -1.</summary>
        public static int TemplateIndex(IReadOnlyList<bool>? gameOwned){
            if(gameOwned==null)return -1;
            for(int i=gameOwned.Count-1;i>=0;i--)if(gameOwned[i])return i;
            return -1;
        }

        /// <summary>
        /// The button for <paramref name="view"/> at <paramref name="tabIndex"/> (-1: not installed) while the menu shows list
        /// <paramref name="currentList"/>. Hidden: no button and no handler entry. Blocked: the button stays with its reason
        /// but is not clickable and the keys skip it. A menu on a tab that went hidden or blocked leaves it.
        /// </summary>
        public static KitBuildTabPlan Plan(KitBuildTabView view,int currentList,int tabIndex,bool menuOpen,bool hasTool){
            if(tabIndex<0)return new KitBuildTabPlan(false,false,false,KitBuildTabLeave.Stay);
            bool current=currentList==tabIndex;
            if(view.State==KitBuildTabState.Shown)return new KitBuildTabPlan(true,true,!current,KitBuildTabLeave.Stay);
            var leave=!current?KitBuildTabLeave.Stay:menuOpen&&hasTool?KitBuildTabLeave.SelectFirst:KitBuildTabLeave.ResetIndex;
            return new KitBuildTabPlan(view.State==KitBuildTabState.Blocked,false,false,leave);
        }

        /// <summary>A click on entry <paramref name="id"/> (null: a game piece); <paramref name="actions"/> are the tab's action entry ids.</summary>
        public static KitBuildTabClick Click(string? id,ICollection<string>? actions){
            if(id==null)return KitBuildTabClick.Game;
            return actions!=null&&actions.Contains(id)?KitBuildTabClick.Act:KitBuildTabClick.Select;
        }

        public static KitBuildTabTeardown Teardown(int index,int count)=>index>=0&&index==count-1?KitBuildTabTeardown.Remove:KitBuildTabTeardown.Neutralize;

        /// <summary>Why an entry id is refused (null when accepted): empty, too long, control characters, or padded.</summary>
        public static string? IdRefusal(string? id){
            if(string.IsNullOrEmpty(id))return "empty";
            if(id!.Length>MaxIdLength)return "too_long";
            foreach(char c in id)if(char.IsControl(c))return "control";
            if(id.Trim().Length!=id.Length)return "padded";
            return null;
        }

        /// <summary>Validates a refill: every id accepted and none twice.</summary>
        public static void CheckEntries(IReadOnlyList<string> ids){
            if(ids==null)throw new ArgumentNullException(nameof(ids));
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in ids){
                var refusal=IdRefusal(id);
                if(refusal!=null)throw new ArgumentException($"Tab entry id refused ({refusal}).",nameof(ids));
                if(!seen.Add(id))throw new ArgumentException("Tab entry id listed twice: "+id,nameof(ids));
            }
        }

        /// <summary>The entries the piece table holds: the shown ones in order, then kept ones (a chosen entry of another folder).</summary>
        public static IReadOnlyList<string> TableIds(IReadOnlyList<string> shown,IReadOnlyList<string>? keep){
            var ids=new List<string>();
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in shown)if(seen.Add(id))ids.Add(id);
            if(keep!=null)foreach(var id in keep)if(seen.Add(id))ids.Add(id);
            return ids;
        }

        /// <summary>
        /// The entry object's name: the kit prefix, readable owner and id parts, and a hash of both. Letters, digits and
        /// underscores only, since the game cuts a prefab name at its first space or bracket and matches pieces by it.
        /// </summary>
        public static string PrefabName(string owner,string id){
            owner??="";id??="";
            return PrefabPrefix+Plain(owner)+"_"+Plain(id)+"_"+Hash(owner+"\n"+id).ToString("x8");
        }

        static string Plain(string text){
            var b=new StringBuilder();
            int start=Math.Max(0,text.Length-NamePart);
            for(int i=start;i<text.Length;i++){char c=text[i];b.Append(c<128&&char.IsLetterOrDigit(c)?c:'_');}
            return b.ToString();
        }

        /// <summary>FNV-1a 32 over UTF-16 code units: the same on every platform and runtime.</summary>
        public static uint Hash(string text){
            uint h=2166136261u;
            foreach(char c in text??""){
                if(c<256){h^=c;h*=16777619u;}
                else{h^=(uint)(c&0xff);h*=16777619u;h^=(uint)(c>>8);h*=16777619u;}
            }
            return h;
        }

        /// <summary>The first build category of <see cref="SpareCategories"/> the table does not use, or -1 (use Misc).</summary>
        public static int SpareCategory(ICollection<int> used){
            foreach(int candidate in SpareCategories)if(used==null||!used.Contains(candidate))return candidate;
            return -1;
        }
    }

    /// <summary>
    /// The entry the player chose in a tab (KX06). The game remembers a selection as a place in a category's list, and a
    /// refill moves the places, so after a refill the chosen entry is selected again at the next look with the build tool
    /// out; otherwise the selection is the player's own. A product may choose an entry itself (<see cref="Choose"/>).
    /// </summary>
    public sealed class KitBuildTabSelection {
        bool verify;
        public string? Chosen { get; private set; }

        public void Reset(){Chosen=null;verify=false;}

        /// <summary>The tab was refilled: the next look checks the selection.</summary>
        public void Filled()=>verify=true;

        /// <summary>The product chooses <paramref name="id"/>; it is selected at the next look while it is in the table.</summary>
        public void Choose(string id){
            var refusal=KitBuildTabRules.IdRefusal(id);
            if(refusal!=null)throw new ArgumentException($"Tab entry id refused ({refusal}).",nameof(id));
            Chosen=id;verify=true;
        }

        /// <summary>
        /// A look with the build tool out. <paramref name="selected"/> is the selected entry of this tab (null for a game piece
        /// or nothing). Returns the id to select again, or null when the selection stands.
        /// </summary>
        public string? Track(string? selected,Func<string,bool> present){
            string? restore=null;
            if(verify&&Chosen!=null&&!string.Equals(selected,Chosen,StringComparison.Ordinal)&&present!=null&&present(Chosen))restore=Chosen;
            else Chosen=selected;
            verify=false;
            return restore;
        }
    }

    /// <summary>
    /// The entry names the kit made known to the player (the game lists only known pieces). Names the player knew before
    /// are never recorded, so withdrawing them before a save and restoring them after keeps entry names out of the
    /// character file without touching anything the game taught.
    /// </summary>
    public sealed class KitKnownNames {
        readonly HashSet<string> added=new HashSet<string>(StringComparer.Ordinal);
        public int Count=>added.Count;

        public bool Learn(ISet<string> known,string name){
            if(known==null||string.IsNullOrEmpty(name)||added.Contains(name))return false;
            if(!known.Add(name))return false;
            added.Add(name);
            return true;
        }

        /// <summary>Takes the kit's names out of <paramref name="known"/>; returns those removed, for <see cref="Restore"/>.</summary>
        public IReadOnlyList<string> Withdraw(ISet<string> known){
            var removed=new List<string>();
            if(known==null)return removed;
            foreach(var name in added)if(known.Remove(name))removed.Add(name);
            return removed;
        }

        public void Restore(ISet<string> known,IReadOnlyList<string> withdrawn){
            if(known==null||withdrawn==null)return;
            foreach(var name in withdrawn)if(added.Contains(name))known.Add(name);
        }

        /// <summary>Teardown: the kit's names leave the set for good.</summary>
        public void Unlearn(ISet<string> known){Withdraw(known);added.Clear();}

        /// <summary>Another player: forget the record, leaving that player's set alone.</summary>
        public void Reset()=>added.Clear();
    }
}
