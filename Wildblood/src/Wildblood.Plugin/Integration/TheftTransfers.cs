// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapts Timber8b9c5ad SiteDeliveries' durable client phases; v2 root-bound ask/answer, explicit status recovery,
// never an automatic resend or a manufactured refund.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using ExtraPotions.Wildblood.Transfers;
using ExtraPotions.Wildblood.Adopted.Transfers;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.AdoptedLibs.PlayerGrid;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static partial class TheftTransfers
{
    sealed class Pending
    {
        internal TheftIntent Intent=null!;
        internal ZRoutedRpc Bus=null!;
        internal ZNet Network=null!;
        internal long Generation;
        internal string Session="";
        internal double Deadline;
        internal byte[] Ask=Array.Empty<byte>();
    }
    /// <summary>One retained intent as the status command shows it.</summary>
    internal sealed class StatusLine
    {
        internal string Root="";internal int Counter;internal string Phase="";internal bool Legacy;
    }
    static TheftClientLedger? ledger;
    static readonly Dictionary<InventoryDecisionJournal,NativeTheftInventory> stores=new();
    static readonly Dictionary<int,Pending> pending=new();
    static readonly HashSet<string> verified=new(StringComparer.Ordinal);
    // Roots an explicit recovery asked about, with the exact owner peer asked: only that peer's answer is authoritative.
    static readonly Dictionary<string,long> asked=new(StringComparer.Ordinal);
    /// <summary>Raised once per newly settled v2 intent in this session, so its owner can release the retained outcome.</summary>
    internal static Action<TheftRequest>? Settled;
    /// <summary>The requester's own evidence for one v2 intent: the world archive row, else its replicated copy of the thief's
    /// archive or exact pouch entry. Only this evidence is recorded, so an owner's answer whose write never reached the
    /// world (owner crash, disconnect, ownership handover, world rollback) is never settled. Null: no native evidence.</summary>
    internal static Func<TheftRequest,TheftStatusAnswer>? Proof;
    /// <summary>Sends one status query to the thief's current owner (which fences an unbanked root); returns the peer asked or 0.</summary>
    internal static Func<TheftRequest,long>? Probe;
    /// <summary>Synchronized world time in seconds (ZNet.GetTimeSeconds), stamped on each ask.</summary>
    internal static Func<double>? Clock;
    static readonly Dictionary<string,double> nextProbe=new(StringComparer.Ordinal);
    const double ProbeInterval=5;
    static partial void Bind();
    static Player? player;
    static long world,actor;
    static bool held,fault,opening,waiting;
    static double nextWarning,nextTick;
    static bool OwnerCurrent => Plugin.Ready && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId!=0
        && ZNet.instance!=null && ZNet.m_world!=null && ZNet.instance.GetWorldUID()==CruciblePlugin.Session.WorldId
        && Player.m_localPlayer!=null && !TheftInventoryGuards.Loading(Player.m_localPlayer) && Player.m_localPlayer.m_nview!=null
        && Player.m_localPlayer.m_nview.IsValid() && Player.m_localPlayer.m_nview.IsOwner();
    static bool NativeActorCurrent => player!=null && player==Player.m_localPlayer && player.GetPlayerID()==actor
        && ZNet.instance!=null && ZNet.m_world!=null && ZNet.instance.GetWorldUID()==world;
    internal static bool HasLocalHold => NativeActorCurrent && (opening||fault||held||waiting);
    internal static bool RawHeld(Inventory inventory) => (opening||fault||held||waiting) && player!=null && !TheftInventoryGuards.Loading(player) && player==Player.m_localPlayer
        && ReferenceEquals(inventory,player.GetInventory()) && NativeActorCurrent;
    static NativeTheftInventory Store(InventoryDecisionJournal journal)
    {
        if(!stores.TryGetValue(journal,out var store)) {
            store=new NativeTheftInventory(journal,world,actor.ToString(CultureInfo.InvariantCulture),player!);stores.Add(journal,store);
        }
        else store.Refresh(journal);
        return store;
    }
    static bool Open()
    {
        if(!OwnerCurrent||TheftInventoryGuards.Loading(Player.m_localPlayer)) return false;
        if(ledger!=null&&player==Player.m_localPlayer&&world==CruciblePlugin.Session.WorldId&&actor==player!.GetPlayerID()) return !fault;
        Reset();player=Player.m_localPlayer;world=CruciblePlugin.Session.WorldId;actor=player!.GetPlayerID();
        try {
            opening=true;
            string identity=TheftMetadata.Hash(System.Text.Encoding.UTF8.GetBytes(world.ToString(CultureInfo.InvariantCulture)+"\n"+actor.ToString(CultureInfo.InvariantCulture)));
            var opened=ledger=new TheftClientLedger(Path.Combine(Paths.BepInExRootPath,"Wildblood.transactions",identity));
            foreach(var intent in opened.All)
                if(intent.Request.World!=world||intent.Request.Actor!=actor)throw new InvalidDataException("Theft journal actor/world mismatch.");
            // Hold only for an intent that still needs reconciliation. Settled history (anyone who ever stole) holds nothing at spawn.
            waiting=TheftHoldRules.HoldOnOpen(opened.All.Select(i=>i.Record.State==InventoryDecision.Committed&&opened.FinalReceiptConfirmed(i,Store)));
            return true;
        }
        catch(Exception error){fault=true;Warn("Wildblood theft recovery unavailable: "+error.GetType().Name);return false;}
        finally{opening=false;}
    }
    internal static int Hold(Inventory inventory,ItemDrop.ItemData item,ZNetView source,long owner)
    {
        if(!ThievingRpc.Active||!WildbloodInventory.Current(Player.m_localPlayer,inventory)||Player.m_localPlayer.IsDead()
            ||Player.m_localPlayer.IsTeleporting()||!Open()||held||ledger==null
            ||!ledger.CanAdmit||TheftInventoryGuards.ForeignHeld(inventory,true)||source==null||!source.IsValid()
            ||source.GetZDO().GetOwner()!=owner||owner==0||!ThievingRpc.CanSend(source)||!inventory.ContainsItem(item)) return 0;
        Tick(true);
        if(fault||held||ledger.Incomplete.Count!=0||ledger.All.Any(i=>!verified.Contains(i.Root))) return 0;
        try {
            byte[] before=NativeTheftInventory.Bytes(inventory);
            var copy=NativeTheftInventory.Read(before);
            var stolen=copy.GetItemAt(item.m_gridPos.x,item.m_gridPos.y);
            if(stolen==null||!NativeTheftInventory.SameExceptCount(stolen,item)||stolen.m_stack!=item.m_stack)return 0;
            var packet=new ZPackage();stolen.Save(packet);byte[] exactItem=packet.GetArray();
            if(exactItem.Length==0||exactItem.Length>1024*1024)return 0;
            int counter=checked(ledger.All.Select(i=>i.Request.Counter).DefaultIfEmpty().Max()+1);
            double sentAt=Clock?.Invoke()??double.NaN;
            if(double.IsNaN(sentAt)||double.IsInfinity(sentAt)||sentAt<0)return 0; // no world clock: no theft, nothing debited
            ZDOID sourceId=source.GetZDO().m_uid;
            var request=new TheftRequest(Guid.NewGuid().ToString("N"),world,actor,sourceId.UserID,sourceId.ID,owner,counter,
                item.m_gridPos.x,item.m_gridPos.y,CruciblePlugin.Session.SessionId,TheftMetadata.Hash(exactItem));
            copy.RemoveItem(stolen);
            byte[] after=NativeTheftInventory.Bytes(copy);
            // Last admission check precedes the durable intent; a changed native source is never overwritten.
            if(!OwnerCurrent||!WildbloodInventory.Current(player!,inventory)||TheftInventoryGuards.ForeignHeld(inventory,true)
                ||!before.SequenceEqual(NativeTheftInventory.Bytes(inventory))||source.GetZDO().GetOwner()!=owner) return 0;
            var intent=ledger.Admit(request,before,after);
            held=waiting=true;
            var state=ledger.CommitDebit(intent,Store);
            held=waiting=state!=InventoryDecision.Committed;
            if(held){Warn("Wildblood theft debit requires recovery; no request sent.");return 0;}
            pending[counter]=new Pending{Intent=intent,Bus=ZRoutedRpc.instance,Network=ZNet.instance,
                Generation=CruciblePlugin.Session.Generation,Session=CruciblePlugin.Session.SessionId,
                Deadline=UnityEngine.Time.realtimeSinceStartupAsDouble+TheftWire.ReplyDeadlineSeconds,
                Ask=TheftWire.EncodeAsk(request.Root,counter,request.Fingerprint,exactItem,sentAt)};
            return counter; // Native profile receipt has been saved before the existing ask is allowed.
        }
        catch(Exception error){fault=true;Warn("Wildblood theft debit retained for recovery: "+error.GetType().Name);return 0;}
    }
    /// <summary>The v2 ask bytes for a held counter, or null when nothing may be sent.</summary>
    internal static byte[]? AskBytes(int counter)=>pending.TryGetValue(counter,out var request)?request.Ask:null;
    /// <summary>A v2 answer (live reply or explicit status). Only the captured owner of a live ask, or the exact peer an
    /// explicit recovery asked, is authoritative; Unknown changes nothing and keeps the debit retained.</summary>
    internal static void Answer(ZRoutedRpc bus,long sender,byte[] bytes)
    {
        if(ledger==null||!OwnerCurrent||player!=Player.m_localPlayer||actor!=player!.GetPlayerID()||world!=CruciblePlugin.Session.WorldId
            ||!ReferenceEquals(bus,ZRoutedRpc.instance)||!NativeRpcOwnership.OwnsRouted(bus,NativeRpcOwner.StealReply)) return;
        try {
            var answer=TheftWire.DecodeAnswer(bytes);
            var intent=ledger.All.FirstOrDefault(i=>i.Root==answer.Root);
            if(intent==null||intent.WireVersion!=2||answer.Fingerprint!=intent.Request.Fingerprint)return;
            var live=pending.Values.FirstOrDefault(p=>p.Intent.Root==answer.Root);
            bool authoritative=live!=null&&sender==intent.Request.Owner&&ReferenceEquals(bus,live.Bus)&&ReferenceEquals(live.Network,ZNet.instance)
                &&live.Generation==CruciblePlugin.Session.Generation&&live.Session==CruciblePlugin.Session.SessionId
                ||asked.TryGetValue(answer.Root,out long peer)&&peer==sender;
            if(!authoritative)return;
            asked.Remove(answer.Root);
            switch(TheftRecovery.Step(answer.Answer,ledger.HasOutcome(intent))) {
                case TheftRecoveryStep.Retain:Warn("Wildblood theft owner has no authoritative outcome; debit retained.");return;
                case TheftRecoveryStep.Settled:return;
            }
            if(Proof!=null&&Proof(intent.Request)!=answer.Answer){Warn("Wildblood theft answer awaits its replicated proof; debit retained.");return;}
            if(!ledger.RecordOutcome(intent,TheftRecovery.Receipt(intent.Request,answer.Answer)!)){Warn("Wildblood theft outcome conflict; recovery required.");return;}
            if(live!=null)pending.Remove(live.Intent.Request.Counter);
            Tick(true);
        }
        catch(Exception error){Warn("Wildblood theft answer refused: "+error.GetType().Name);}
    }
    /// <summary>Retained intents for the read-only status command.</summary>
    internal static IReadOnlyList<StatusLine> Status()
    {
        if(!Open()||ledger==null)return Array.Empty<StatusLine>();
        var lines=new List<StatusLine>();
        foreach(var intent in ledger.All) {
            if(intent.Record.State==InventoryDecision.Committed)continue;
            string phase=!ledger.HasOutcome(intent)?"unknown"
                :ledger.Outcome(intent).Disposition==TheftDisposition.Approved?"approved":"denied";
            lines.Add(new StatusLine{Root=intent.Root.Substring(0,8),Counter=intent.Request.Counter,Phase=phase,Legacy=intent.WireVersion!=2});
        }
        return lines;
    }
    /// <summary>Explicit recovery: asks the current authoritative owner about every v2 intent without an outcome.
    /// <paramref name="ask"/> sends one status query and returns the owner peer asked (0: not reachable now).
    /// Legacy intents are never asked; nothing is resent and nothing is refunded here.</summary>
    internal static (int Asked,int Unreachable,int Legacy) Recover(Func<TheftRequest,long> ask)
    {
        if(!Open()||ledger==null)return (0,0,0);
        int sent=0,unreachable=0,legacy=0;
        foreach(var intent in ledger.Incomplete) {
            if(ledger.HasOutcome(intent))continue;
            if(!TheftRecovery.Eligible(intent.Request,intent.WireVersion==2)){legacy++;continue;}
            long peer=ask(intent.Request);
            if(peer==0){unreachable++;continue;}
            asked[intent.Root]=peer;sent++;
        }
        return (sent,unreachable,legacy);
    }
    internal static void Reply(ZRoutedRpc bus,long sender,int counter,bool banked)
    {
        if(ledger==null||!pending.TryGetValue(counter,out var request)||!OwnerCurrent||player!=Player.m_localPlayer
            ||actor!=player!.GetPlayerID()||world!=CruciblePlugin.Session.WorldId||sender!=request.Intent.Request.Owner
            ||!ReferenceEquals(bus,request.Bus)||!ReferenceEquals(bus,ZRoutedRpc.instance)||!ReferenceEquals(request.Network,ZNet.instance)
            ||request.Generation!=CruciblePlugin.Session.Generation||request.Session!=CruciblePlugin.Session.SessionId
            ||!NativeRpcOwnership.OwnsRouted(bus,NativeRpcOwner.StealReply)) return;
        try {
            if(!ledger.RecordOutcome(request.Intent,TheftReceipt.For(request.Intent.Request,banked))){Warn("Wildblood theft outcome conflict; recovery required.");return;}
            pending.Remove(counter);
            Tick(true);
        }
        catch(Exception error){fault=true;Warn("Wildblood theft outcome requires recovery: "+error.GetType().Name);}
    }
    internal static void Tick(bool force=false)
    {
        if(opening)return;
        Bind();
        if(!OwnerCurrent){pending.Clear();if(!NativeActorCurrent)Reset();return;}
        if(!force&&UnityEngine.Time.realtimeSinceStartupAsDouble<nextTick)return;
        nextTick=UnityEngine.Time.realtimeSinceStartupAsDouble+0.2;
        if(!Open()||ledger==null||player!.IsDead()||player.IsTeleporting())return;
        try {
            foreach(var intent in ledger.All) {
                if(verified.Contains(intent.Root))continue;
                if(intent.Record.State==InventoryDecision.Committed&&ledger.FinalReceiptConfirmed(intent,Store)){verified.Add(intent.Root);continue;}
                if(TheftInventoryGuards.ForeignHeld(player!.GetInventory(),true))return;
                held=waiting=true;
                var state=ledger.Reconcile(intent,Store);
                if(state==InventoryDecision.RecoveryRequired){Warn("Wildblood theft native debit requires recovery.");return;}
                if(state==InventoryDecision.Committed&&ledger.FinalReceiptConfirmed(intent,Store)){held=waiting=false;verified.Add(intent.Root);Release(intent);continue;}
                if(state!=InventoryDecision.Prepared){Warn("Wildblood theft debit has no confirmed receipt; recovery required.");return;}
                // Prepared here means Reconcile confirmed the saved debit and has no final settlement yet.
                held=waiting=false;
                if(!ledger.HasOutcome(intent)&&!AdoptProof(intent)) {
                    if(!pending.TryGetValue(intent.Request.Counter,out var request)
                        ||UnityEngine.Time.realtimeSinceStartupAsDouble>=request.Deadline) {
                        Warn("Wildblood theft outcome unknown; debit retained for recovery.");
                        ProbeOwner(intent);
                    }
                    return;
                }
                if(ledger.Settlement(intent)==null&&!PrepareSettlement(intent))return;
                held=waiting=true;
                state=ledger.CommitSettlement(intent,Store);
                if(state!=InventoryDecision.Committed||!ledger.FinalReceiptConfirmed(intent,Store)){Warn("Wildblood theft settlement requires recovery.");return;}
                held=waiting=false;verified.Add(intent.Root);Release(intent);
            }
            held=waiting=false;
        }
        catch(Exception error){fault=true;Warn("Wildblood theft reconciliation retained: "+error.GetType().Name);}
        finally{TheftInventoryGuards.DrainDeaths();}
    }
    // Records the requester's own evidence as the outcome. Unknown evidence changes nothing.
    static bool AdoptProof(TheftIntent intent)
    {
        if(Proof==null||!TheftRecovery.Eligible(intent.Request,intent.WireVersion==2))return false;
        var answer=Proof(intent.Request);
        if(answer==TheftStatusAnswer.Unknown)return false;
        if(!ledger!.RecordOutcome(intent,TheftRecovery.Receipt(intent.Request,answer)!)){Warn("Wildblood theft outcome conflict; recovery required.");return false;}
        pending.Remove(intent.Request.Counter);asked.Remove(intent.Root);nextProbe.Remove(intent.Root);
        return true;
    }
    // Automatic status query after the live ask expired: a status never resends the item, and only proof settles it.
    static void ProbeOwner(TheftIntent intent)
    {
        if(Probe==null||!TheftRecovery.Eligible(intent.Request,intent.WireVersion==2))return;
        double now=UnityEngine.Time.realtimeSinceStartupAsDouble;
        if(nextProbe.TryGetValue(intent.Root,out double next)&&now<next)return;
        nextProbe[intent.Root]=now+ProbeInterval;
        long peer=Probe(intent.Request);
        if(peer!=0)asked[intent.Root]=peer;
    }
    static void Release(TheftIntent intent)
    {
        if(intent.WireVersion!=2)return;
        try{Settled?.Invoke(intent.Request);}catch(Exception error){Warn("Wildblood theft acknowledgement skipped: "+error.GetType().Name);}
    }
    static bool PrepareSettlement(TheftIntent intent)
    {
        var inventory=player!.GetInventory();
        byte[] before=NativeTheftInventory.Bytes(inventory),after=before;
        if(ledger!.Outcome(intent).Disposition==TheftDisposition.Denied) {
            var original=NativeTheftInventory.Read(intent.Record.Before);
            var item=original.GetItemAt(intent.Request.X,intent.Request.Y);
            if(item==null)throw new InvalidDataException("Original theft item missing.");
            var packet=new ZPackage();item.Save(packet);
            if(TheftMetadata.Hash(packet.GetArray())!=intent.Request.Fingerprint)throw new InvalidDataException("Original theft item changed.");
            if(!WildbloodInventory.TryProtected(player,inventory,out var protectedCells)
                ||!PackPanelGrid.TryMainRows(player,out int mainRows))return false;
            var copy=NativeTheftInventory.Read(before);
            Vector2i target=new(intent.Request.X,intent.Request.Y);
            bool Free(Vector2i cell)=>cell.x>=0&&cell.x<copy.GetWidth()&&cell.y>=0&&cell.y<copy.GetHeight()
                &&cell.y<mainRows&&!protectedCells.Contains(cell)&&copy.GetItemAt(cell.x,cell.y)==null;
            if(!Free(target)) {
                target=new Vector2i(-1,-1);
                for(int y=0;y<copy.GetHeight()&&target.x<0;y++)for(int x=0;x<copy.GetWidth();x++)
                    if(Free(new Vector2i(x,y))){target=new Vector2i(x,y);break;}
            }
            int amount=item.m_stack;
            if(target.x<0||!copy.AddItem(item,target)||!ReferenceEquals(copy.GetItemAt(target.x,target.y),item)||item.m_stack!=amount) {
                Warn("Wildblood theft refund awaits an unprotected native cell; no world copy created.");return false;
            }
            if(!WildbloodInventory.TryProtected(player,inventory,out var latestProtected)||latestProtected.Contains(target)
                ||!PackPanelGrid.TryMainRows(player,out int latestRows)||target.y>=latestRows)return false;
            after=NativeTheftInventory.Bytes(copy);
        }
        if(!OwnerCurrent||TheftInventoryGuards.ForeignHeld(inventory,true)||!before.SequenceEqual(NativeTheftInventory.Bytes(inventory)))return false;
        ledger.PrepareSettlement(intent,before,after);return true;
    }
    internal static void Warn(string message)
    {
        if(UnityEngine.Time.realtimeSinceStartupAsDouble<nextWarning)return;
        nextWarning=UnityEngine.Time.realtimeSinceStartupAsDouble+10;
        Plugin.NativeLogger?.LogWarning(message);
    }
    internal static void ProfileLoading(Player target)
    {
        if(target!=player&&target!=Player.m_localPlayer)return;
        foreach(var store in stores.Values)store.InvalidateReceipts();
        stores.Clear();verified.Clear();pending.Clear();asked.Clear();nextProbe.Clear();waiting=true;nextTick=0;
    }
    internal static bool Stop()
    {
        pending.Clear();
        if(HasLocalHold){Plugin.NativeLogger?.LogWarning("Wildblood theft RecoveryRequired: native inventory guard retained until this actor leaves.");return false;}
        Reset();return true;
    }
    internal static void Reset()
    {
        ledger?.Dispose();ledger=null;stores.Clear();pending.Clear();verified.Clear();asked.Clear();nextProbe.Clear();player=null;world=actor=0;held=fault=opening=waiting=false;nextTick=0;
    }
}
