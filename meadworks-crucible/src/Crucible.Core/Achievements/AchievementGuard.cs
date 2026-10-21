using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>A Meadworks item operation, as a product declares it to the achievement guard (C14).</summary>
    public enum GuardAction {
        /// <summary>Moving items between inventories or slots.</summary>
        Move,
        /// <summary>Combining stacks (ground or inventory).</summary>
        Merge,
        /// <summary>Automatic pickup.</summary>
        Pickup,
        /// <summary>Sorting or quick stacking.</summary>
        Sort,
        /// <summary>Crafting or building from container contents.</summary>
        StorageCraft,
        /// <summary>Splitting a stack.</summary>
        Split,
        /// <summary>Restocking or refuelling from storage.</summary>
        Restock,
        /// <summary>Physics help such as floating drops.</summary>
        Float,
        /// <summary>A cheat's own output (spawned items, multiplied loot): marked like the game marks cheat-made items.</summary>
        Cheat,
    }

    public enum GuardAudit { Clean, Violation, Expected }

    /// <summary>
    /// The game's "obtained through cheats" mark (<c>ItemDrop.ItemData.m_cheated</c>, read 2026-10-06) and the C14 rules
    /// for Meadworks operations. Quality of life never sets a mark and never clears one: an output is marked exactly
    /// when an input was, and QoL never stacks a marked item with a clean one (the game's <c>Inventory.AddItem</c>
    /// marks the whole target stack when a marked item joins it). Clearing marks is Hex's cheat (F2), not the guard's.
    /// </summary>
    public static class CheatMarks {
        public static bool IsQol(GuardAction action){Validate(action);return action!=GuardAction.Cheat;}
        /// <summary>Mark of an operation's output given whether any input was marked.</summary>
        public static bool Result(GuardAction action,bool anyInputMarked)=>!IsQol(action)||anyInputMarked;
        /// <summary>Whether an operation may put <paramref name="incoming"/> onto a stack marked <paramref name="target"/>.</summary>
        public static bool CanStack(GuardAction action,bool target,bool incoming)=>!IsQol(action)||target==incoming;
        /// <summary>A split part carries its source's mark.</summary>
        public static bool Split(bool sourceMarked)=>sourceMarked;
        /// <summary>
        /// Mark of an item crafted or built from storage: the game checks only the player's inventory
        /// (<c>Inventory.ItemCheated</c> by resource name), so Meadworks storage paths answer from the resources actually
        /// consumed, plus the game's other two causes (no-cost cheat, marked crafting station).
        /// </summary>
        public static bool StorageCraft(IEnumerable<bool> consumedMarked,bool noCostCheat,bool stationMarked){
            if(consumedMarked==null)throw new ArgumentNullException(nameof(consumedMarked));
            return noCostCheat||stationMarked||consumedMarked.Any(m=>m);
        }
        /// <summary>Checks an operation by counting marked items before and after; items arriving already marked are allowed.</summary>
        public static GuardAudit Audit(GuardAction action,int markedBefore,int markedAfter,int incomingMarked=0){
            if(markedBefore<0||markedAfter<0||incomingMarked<0)throw new ArgumentOutOfRangeException(nameof(markedBefore),"Counts must not be negative.");
            if(!IsQol(action))return GuardAudit.Expected;
            return markedAfter>markedBefore+incomingMarked?GuardAudit.Violation:GuardAudit.Clean;
        }
        internal static void Validate(GuardAction action){if(!Enum.IsDefined(typeof(GuardAction),action))throw new ArgumentException("Unknown guard action.",nameof(action));}
    }

    /// <summary>One product operation under the guard. Dispose when the operation ends.</summary>
    public sealed class GuardScope:IDisposable {
        readonly GuardScopes owner;
        public string Product { get; }
        public GuardAction Action { get; }
        /// <summary>Whether any resource the operation consumed was marked; null until the product reports one.</summary>
        public bool? ConsumedMarked { get; private set; }
        public bool Disposed { get; private set; }
        internal GuardScope(GuardScopes owner,string product,GuardAction action){this.owner=owner;Product=product;Action=action;}
        /// <summary>Reports one consumed resource (storage crafting): any marked one marks the result.</summary>
        public void ReportConsumed(bool marked)=>ConsumedMarked=(ConsumedMarked??false)||marked;
        internal void Close()=>Disposed=true;
        public void Dispose(){if(Disposed)return;owner.End(this);}
        public override string ToString()=>Product+" "+Action+(ConsumedMarked.HasValue?" consumed-marked="+ConsumedMarked.Value:"");
    }

    /// <summary>Stack of active guard scopes (main thread): the innermost decides. Bounded so a leaked scope is noticed.</summary>
    public sealed class GuardScopes {
        public const int MaxDepth=32;
        readonly List<GuardScope> active=new List<GuardScope>();
        public GuardScope? Current=>active.Count==0?null:active[active.Count-1];
        public int Depth=>active.Count;
        public bool ActiveQol=>Current!=null&&CheatMarks.IsQol(Current.Action);
        public GuardScope Begin(string product,GuardAction action){
            if(string.IsNullOrWhiteSpace(product))throw new ArgumentException("Expected a product id.",nameof(product));
            CheatMarks.Validate(action);
            if(active.Count>=MaxDepth)throw new InvalidOperationException("Too many nested achievement guard scopes; a scope was not disposed.");
            var scope=new GuardScope(this,product,action);active.Add(scope);return scope;
        }
        internal void End(GuardScope scope){active.Remove(scope);scope.Close();}
        public void Reset(){foreach(var s in active)s.Close();active.Clear();}
    }

    public enum ResyncRefusal { None, Disabled, SteppedAside, ProfileUsedCheats, WorldCheated, MarkedItemsHeld }

    /// <summary>What the stat resync needs to know about the character and session.</summary>
    public sealed class ResyncFacts {
        public bool Enabled { get; }
        /// <summary>AchievementSync is installed: it owns the achievement tables (one enabler only).</summary>
        public bool SteppedAside { get; }
        public bool ProfileUsedCheats { get; }
        public bool WorldCheated { get; }
        public bool MarkedItemsHeld { get; }
        /// <summary>Hex "achievements despite cheats", granted by the Mischief gate (Progress).</summary>
        public bool CheatsForgiven { get; }
        public ResyncFacts(bool enabled,bool steppedAside,bool profileUsedCheats,bool worldCheated,bool markedItemsHeld,bool cheatsForgiven){
            Enabled=enabled;SteppedAside=steppedAside;ProfileUsedCheats=profileUsedCheats;WorldCheated=worldCheated;MarkedItemsHeld=markedItemsHeld;CheatsForgiven=cheatsForgiven;
        }
        public ResyncFacts With(bool? enabled=null,bool? steppedAside=null,bool? profileUsedCheats=null,bool? worldCheated=null,bool? markedItemsHeld=null,bool? cheatsForgiven=null)=>
            new ResyncFacts(enabled??Enabled,steppedAside??SteppedAside,profileUsedCheats??ProfileUsedCheats,worldCheated??WorldCheated,markedItemsHeld??MarkedItemsHeld,cheatsForgiven??CheatsForgiven);
    }

    public readonly struct StatChange {
        public int Stat { get; }
        public float From { get; }
        public float To { get; }
        public StatChange(int stat,float from,float to){Stat=stat;From=from;To=to;}
        public override string ToString()=>Stat+": "+From+" -> "+To;
    }

    /// <summary>
    /// Past-stat resync (C14; AchievementSync <c>SyncPastStats</c> behavior). The game keeps per-profile stat tables
    /// (<c>PlayerProfile.m_playerStats</c>, indexed by <c>DifficultyRequirement</c>): table 0 (RawStats) always counts,
    /// table 1 (Any) and the current difficulty's table count only while <c>Achievements.CanGetAchievements</c> holds.
    /// Resync raises the Any table to the raw value where it lags and sets the platform stat; it never lowers, never
    /// touches difficulty tables (the raw table does not say which difficulty earned a value), and runs only on a
    /// character that is clean now, unless Hex's achievements-despite-cheats is granted.
    /// </summary>
    public static class StatResync {
        public const int RawTable=0;
        public const int AchievementTable=1;
        const float Epsilon=0.001f;

        public static ResyncRefusal Check(ResyncFacts facts){
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            if(!facts.Enabled)return ResyncRefusal.Disabled;
            if(facts.SteppedAside)return ResyncRefusal.SteppedAside;
            if(facts.CheatsForgiven)return ResyncRefusal.None;
            if(facts.ProfileUsedCheats)return ResyncRefusal.ProfileUsedCheats;
            if(facts.WorldCheated)return ResyncRefusal.WorldCheated;
            if(facts.MarkedItemsHeld)return ResyncRefusal.MarkedItemsHeld;
            return ResyncRefusal.None;
        }

        public static string Describe(ResyncRefusal refusal){
            switch(refusal){
                case ResyncRefusal.None:return "";
                case ResyncRefusal.Disabled:return "Stat resync is turned off.";
                case ResyncRefusal.SteppedAside:return "AchievementSync is installed and keeps the achievement stats.";
                case ResyncRefusal.ProfileUsedCheats:return "This character has used cheats, so past stats are not copied to achievements.";
                case ResyncRefusal.WorldCheated:return "This world uses cheated world modifiers, so past stats are not copied to achievements.";
                case ResyncRefusal.MarkedItemsHeld:return "The inventory holds items obtained through cheats, so past stats are not copied to achievements.";
                default:throw new ArgumentException("Unknown resync refusal.",nameof(refusal));
            }
        }

        /// <summary>Stats whose achievement value lags the raw value, ordered by stat number. Invalid raw values are skipped.</summary>
        public static IReadOnlyList<StatChange> Plan(IReadOnlyDictionary<int,float> raw,IReadOnlyDictionary<int,float> achievement){
            if(raw==null)throw new ArgumentNullException(nameof(raw));
            if(achievement==null)throw new ArgumentNullException(nameof(achievement));
            var changes=new List<StatChange>();
            foreach(var pair in raw.OrderBy(p=>p.Key)){
                var value=pair.Value;
                if(float.IsNaN(value)||float.IsInfinity(value)||value<=0f)continue;
                var current=achievement.TryGetValue(pair.Key,out var a)&&!float.IsNaN(a)&&!float.IsInfinity(a)?a:0f;
                if(value-current>Epsilon)changes.Add(new StatChange(pair.Key,current,value));
            }
            return changes;
        }

        /// <summary>The platform stat value (the game passes <c>(uint)amount</c>): floored and clamped.</summary>
        public static uint PlatformValue(float value){
            if(float.IsNaN(value)||value<=0f)return 0u;
            if(value>=uint.MaxValue)return uint.MaxValue;
            return (uint)Math.Floor(value);
        }
    }
}
