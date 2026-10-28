using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// One product item operation under the achievement guard. Dispose when the operation ends; for QoL actions the
    /// local inventory's marked count is audited (a rise beyond reported incoming marked items is logged as a defect).
    /// </summary>
    public sealed class GuardedOperation:IDisposable {
        readonly int before;int incoming;
        public GuardScope Scope { get; }
        internal GuardedOperation(GuardScope scope){Scope=scope;before=MeadworksAchievementGuard.LocalMarkedCount();}
        /// <summary>Storage crafting: report each consumed item so the result is marked from what was really used.</summary>
        public void ReportConsumed(ItemDrop.ItemData? item){if(item!=null)Scope.ReportConsumed(item.m_cheated);}
        public void ReportConsumed(bool marked)=>Scope.ReportConsumed(marked);
        /// <summary>Items that arrive already marked (pickup of a marked drop) and so may raise the count.</summary>
        public void ReportIncomingMarked(int count=1){if(count>0)incoming+=count;}
        public void Dispose(){
            if(Scope.Disposed)return;
            Scope.Dispose();
            var after=MeadworksAchievementGuard.LocalMarkedCount();
            if(CheatMarks.Audit(Scope.Action,before,after,incoming)==GuardAudit.Violation)
                MeadworksAchievementGuard.Log?.Warning("Achievement guard: "+Scope+" raised the marked item count from "+before+" to "+after+"; Meadworks QoL must not mark items as cheated.");
        }
    }

    /// <summary>
    /// C14 achievement guard adapter. The game's mark is the field <c>ItemDrop.ItemData.m_cheated</c> (no setter), read
    /// 2026-10-06 from assembly_valheim: set by <c>ItemDrop.OnCreateNew</c>, <c>CharacterDrop.DropItems</c>,
    /// <c>Piece.DropResources</c>, <c>Container.AddDefaultItems</c> (from the source ZDO's <c>s_cheated</c>), by the
    /// <c>Inventory.AddItem</c> overloads taking a <c>cheated</c> flag, and by <c>Inventory.AddItem(ItemData)</c>, which
    /// marks the whole target stack when a marked item joins it (unless <c>PlayerProfile.s_bypassCheatChecks</c>).
    /// <c>Inventory.ItemCheated(Requirement[], int, bool)</c> checks only the player's inventory by resource name and is
    /// read by <c>InventoryGui.DoCrafting</c>, <c>Player.TryPlacePiece</c> and <c>StoreGui</c>. Hooks (active only inside a
    /// product's guard scope):
    /// <list type="bullet">
    /// <item><c>Inventory.FindFreeStackItem</c> postfix (private, found by name): inside a QoL scope an added item only
    /// joins a stack with the same mark, so QoL never taints a clean stack or folds a clean item into a marked one.</item>
    /// <item><c>Inventory.AddItem(ItemData, int, int, int, bool)</c> prefix: inside a QoL scope a placement onto a stack
    /// with a different mark is refused.</item>
    /// <item><c>Inventory.ItemCheated(Requirement[], int, bool)</c> postfix: inside a StorageCraft scope that reported
    /// consumed resources, the answer is whether a consumed resource was marked.</item>
    /// <item><c>Player.OnSpawned</c> postfix (local player) and a resync key: past-stat resync (<see cref="StatResync"/>).</item>
    /// </list>
    /// Steps aside from AchievementSync (<c>liveriadevs.valheim.achievementsync</c>) for the resync only.
    /// </summary>
    public static class MeadworksAchievementGuard {
        public const string AchievementSyncGuid="liveriadevs.valheim.achievementsync";
        public static GuardScopes Scopes { get; }=new GuardScopes();
        /// <summary>Set by Hex: true while "achievements despite cheats" is on and granted by the Mischief gate (Progress).</summary>
        public static Func<bool>? CheatsForgiven { get; set; }
        internal static MeadworksLog? Log;
        static Harmony? harmony;
        static ConfigEntry<bool>? syncPastStats;
        static ConfigEntry<KeyboardShortcut>? resyncKey;
        static readonly Stack<bool> adding=new Stack<bool>();

        public static GuardedOperation Begin(string productId,GuardAction action)=>new GuardedOperation(Scopes.Begin(productId,action));
        public static void Run(string productId,GuardAction action,Action body){
            if(body==null)throw new ArgumentNullException(nameof(body));
            using(Begin(productId,action))body();
        }

        public static bool IsMarked(ItemDrop.ItemData? item)=>item!=null&&item.m_cheated;
        public static bool CanStack(GuardAction action,ItemDrop.ItemData target,ItemDrop.ItemData incoming)=>CheatMarks.CanStack(action,IsMarked(target),IsMarked(incoming));
        /// <summary>Splits and copies: the new item carries the source's mark.</summary>
        public static void CopyMark(ItemDrop.ItemData from,ItemDrop.ItemData to){if(from==null||to==null)throw new ArgumentNullException(from==null?nameof(from):nameof(to));to.m_cheated=CheatMarks.Split(from.m_cheated);}
        /// <summary>A cheat's own output (Magpie multiplied extras, Magpie Q3): marked like other cheat-made items.</summary>
        public static void MarkCheatOutput(ItemDrop.ItemData item){if(item==null)throw new ArgumentNullException(nameof(item));item.m_cheated=CheatMarks.Result(GuardAction.Cheat,item.m_cheated);}

        internal static int LocalMarkedCount(){
            var inventory=Player.m_localPlayer?.GetInventory();
            return inventory==null?0:inventory.GetAllItems().Count(i=>i!=null&&i.m_cheated);
        }

        internal static void Install(ConfigFile config,ManualLogSource logger){
            Log=MeadworksLog.For(logger,"Crucible");
            syncPastStats=MeadworksConfig.Bind(config,"Achievements","SyncPastStats",true,"When a character that has not used cheats loads, copy the stats the game recorded while achievements were not tracked into the achievement table. Characters that used cheats are never copied unless Hex allows it. Disabled while AchievementSync is installed. Crucible owns this local installation setting.",ConfigScope.Local);
            resyncKey=MeadworksConfig.Bind(config,"Achievements","ResyncKey",KeyboardShortcut.Empty,"Key that rechecks your saved game statistics and grants any MeadWorks achievements already earned. Unassigned by default. Crucible owns this local installation setting.",ConfigScope.Local);
            harmony=new Harmony(Plugin.Guid+".achievements");
            Patch(AccessTools.Method(typeof(Inventory),"FindFreeStackItem",new[]{typeof(string),typeof(int),typeof(float)}),postfix:nameof(FindFreeStackPostfix));
            foreach(var overload in new[]{new[]{typeof(ItemDrop.ItemData)},new[]{typeof(ItemDrop.ItemData),typeof(Vector2i)}})
                Patch(AccessTools.Method(typeof(Inventory),"AddItem",overload),prefix:nameof(AddPrefix),finalizer:nameof(AddFinalizer));
            Patch(AccessTools.Method(typeof(Inventory),"AddItem",new[]{typeof(ItemDrop.ItemData),typeof(int),typeof(int),typeof(int),typeof(bool)}),prefix:nameof(PlacePrefix));
            Patch(AccessTools.Method(typeof(Inventory),"ItemCheated",new[]{typeof(Piece.Requirement[]),typeof(int),typeof(bool)}),postfix:nameof(ItemCheatedPostfix));
            Patch(AccessTools.Method(typeof(Player),"OnSpawned"),postfix:nameof(SpawnedPostfix));
        }

        static void Patch(System.Reflection.MethodBase? target,string? prefix=null,string? postfix=null,string? finalizer=null){
            if(target==null){Log?.Warning("Achievement guard: a game member was not found; that hook is off.");return;}
            HarmonyMethod? M(string? name)=>name==null?null:new HarmonyMethod(typeof(MeadworksAchievementGuard),name);
            harmony!.Patch(target,prefix:M(prefix),postfix:M(postfix),finalizer:M(finalizer));
        }

        internal static void Update(){
            if(resyncKey!=null&&MeadworksInput.Pressed(resyncKey)){
                var done=ResyncStats(out var message);
                var text=done?"Achievement stats resynced: "+message:message;
                Log?.Info(text);
                if(MeadworksHeadless.Channel==NoticeChannel.Hud)MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center,text);
            }
        }

        internal static void Uninstall(){harmony?.UnpatchSelf();harmony=null;Scopes.Reset();adding.Clear();}

        static void AddPrefix(ItemDrop.ItemData item){adding.Push(item!=null&&item.m_cheated);}
        static Exception? AddFinalizer(Exception? __exception){if(adding.Count>0)adding.Pop();return __exception;}

        static void FindFreeStackPostfix(Inventory __instance,string name,int quality,float worldLevel,ref ItemDrop.ItemData? __result){
            var scope=Scopes.Current;
            if(scope==null||!CheatMarks.IsQol(scope.Action)||adding.Count==0||__result==null)return;
            var mark=adding.Peek();
            if(CheatMarks.CanStack(scope.Action,__result.m_cheated,mark))return;
            __result=__instance.GetAllItems().FirstOrDefault(i=>i!=null&&i.m_shared.m_name==name&&i.m_quality==quality&&i.m_stack<i.m_shared.m_maxStackSize&&i.m_worldLevel==worldLevel&&i.m_cheated==mark);
        }

        static bool PlacePrefix(Inventory __instance,ItemDrop.ItemData item,int x,int y,ref bool __result){
            var scope=Scopes.Current;
            if(scope==null||!CheatMarks.IsQol(scope.Action)||item==null)return true;
            var target=__instance.GetItemAt(x,y);
            if(target==null||ReferenceEquals(target,item)||!target.IsSameType(item)||CheatMarks.CanStack(scope.Action,target.m_cheated,item.m_cheated))return true;
            __result=false;return false;
        }

        static void ItemCheatedPostfix(ref bool __result){
            var scope=Scopes.Current;
            if(scope!=null&&scope.Action==GuardAction.StorageCraft&&scope.ConsumedMarked.HasValue)__result=scope.ConsumedMarked.Value;
        }

        static void SpawnedPostfix(Player __instance){
            if(__instance!=Player.m_localPlayer)return;
            try{
                if(ResyncStats(out var message))Log?.Info("Achievement stats resynced: "+message);
                else Log?.Verbose("Achievement stat resync skipped: "+message);
            }catch(Exception error){Log?.Warning("Achievement stat resync failed: "+error.GetType().Name);}
        }

        /// <summary>Facts for the local character now; null when no profile is loaded.</summary>
        public static ResyncFacts? Facts(){
            var profile=Game.instance?.GetPlayerProfile();if(profile==null)return null;
            bool forgiven=false;try{forgiven=CheatsForgiven?.Invoke()??false;}catch(Exception){forgiven=false;}
            return new ResyncFacts(syncPastStats?.Value??false,Chainloader.PluginInfos.ContainsKey(AchievementSyncGuid),profile.m_usedCheats,Achievements.IsWorldCheated(),LocalMarkedCount()>0,forgiven);
        }

        /// <summary>
        /// Copies lagging raw stats into the achievement table (Any) and the platform stat. Returns false with the reason
        /// when refused or when there is no character; the message then says why.
        /// </summary>
        public static bool ResyncStats(out string message){
            var profile=Game.instance?.GetPlayerProfile();var facts=Facts();
            if(profile==null||facts==null){message="No character is loaded.";return false;}
            var refusal=StatResync.Check(facts);
            if(refusal!=ResyncRefusal.None){message=StatResync.Describe(refusal);return false;}
            var tables=profile.m_playerStats;
            if(tables==null||tables.Length<=StatResync.AchievementTable||tables[StatResync.RawTable]==null||tables[StatResync.AchievementTable]==null){message="The profile has no stat tables.";return false;}
            var raw=tables[StatResync.RawTable].m_stats;var achieved=tables[StatResync.AchievementTable].m_stats;
            var plan=StatResync.Plan(raw.ToDictionary(p=>(int)p.Key,p=>p.Value),achieved.ToDictionary(p=>(int)p.Key,p=>p.Value));
            foreach(var change in plan){
                var stat=(PlayerStatType)change.Stat;
                achieved[stat]=change.To;
                Achievements.AchievementStatSetEvent(stat,StatResync.PlatformValue(change.To));
            }
            message=plan.Count+" stat(s) updated.";return true;
        }
    }
}
