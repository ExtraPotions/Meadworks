// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Reused from Timber8b9c5ad NativeDeliveryInventory; player-only adaptation of Burrow14f92c83fdfced32a0439fac7164718306a09e6e Resources/NativeBundleStore.cs.
// Exact before/after reconciliation, native object preservation and saved character receipts retained; chest/image bundles excluded.
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Wildblood.Transfers;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Adopted.Transfers;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime {
    internal sealed class NativeTheftInventory:IInventoryImageStore,IInventoryCheckpointStore,IInventoryRecoveryDiagnostics {
        readonly Dictionary<string,InventoryDecisionRecord> bindings=new Dictionary<string,InventoryDecisionRecord>(StringComparer.Ordinal);
        readonly HashSet<string> confirmed=new HashSet<string>(StringComparer.Ordinal),unsaved=new HashSet<string>(StringComparer.Ordinal);
        readonly long world;readonly string actor;readonly Player player;
        double nextWarning;
        internal NativeTheftInventory(InventoryDecisionJournal journal,long world,string actor,Player player){this.world=world;this.actor=actor;this.player=player;Refresh(journal);}
        internal void Refresh(InventoryDecisionJournal journal){foreach(var record in journal.Snapshot())if(!bindings.ContainsKey(record.Source)){Read(record.Before);Read(record.After);bindings.Add(record.Source,record);if(record.State==InventoryDecision.Decided)unsaved.Add(record.Source);}}
        bool Current=>player&&!TheftInventoryGuards.Loading(player)&&ReferenceEquals(Player.m_localPlayer,player)&&player.GetPlayerID().ToString(System.Globalization.CultureInfo.InvariantCulture)==actor
            &&CruciblePlugin.IsReady&&CruciblePlugin.Session.AuthorityReady&&CruciblePlugin.Session.WorldId==world
            &&player.m_nview&&player.m_nview.IsValid()&&player.m_nview.IsOwner()&&ZNet.instance&&ZNet.instance.GetWorldUID()==world;
        public bool IsBound(string source)=>Current&&bindings.ContainsKey(source)&&SameShape(Read(bindings[source].Before),player.GetInventory());
        internal static byte[] Bytes(Inventory inventory){var packet=new ZPackage();packet.Write(1);packet.Write(inventory.GetWidth());packet.Write(inventory.GetHeight());var body=new ZPackage();inventory.Save(body);packet.Write(body.GetArray());return packet.GetArray();}
        internal static Inventory Read(byte[] bytes){
            if(bytes==null||bytes.Length>2096128)throw new InvalidDataException("Delivery inventory image bound.");
            var packet=new ZPackage(bytes);if(packet.ReadInt()!=1)throw new InvalidDataException("Delivery inventory version.");
            int width=packet.ReadInt(),height=packet.ReadInt();if(width<1||height<1||width>256||height>256)throw new InvalidDataException("Delivery inventory shape.");
            var body=ReadBlob(packet,2096128);if(packet.GetPos()!=packet.Size())throw new InvalidDataException("Delivery image trailing data.");
            var inventory=new Inventory("Wildblood theft snapshot",null,width,height);
            TheftInventoryGuards.LoadSnapshot(inventory,()=>inventory.Load(new ZPackage(body)));
            var roundTrip=new ZPackage();inventory.Save(roundTrip);
            if(!roundTrip.GetArray().SequenceEqual(body))throw new InvalidDataException("Delivery inventory cannot round-trip exactly; no live write permitted.");
            return inventory;
        }
        static byte[] ReadBlob(ZPackage packet,int maximum){
            int start=packet.GetPos(),length=packet.ReadInt();
            if(length<0||length>maximum||length>packet.Size()-packet.GetPos())throw new InvalidDataException("Theft image body bound.");
            packet.SetPos(start);return packet.ReadByteArray();
        }
        static bool SameShape(Inventory a,Inventory b)=>a.GetWidth()==b.GetWidth()&&a.GetHeight()==b.GetHeight();
        public byte[] Capture(string source){if(!IsBound(source))throw new InvalidOperationException("Delivery actor unavailable.");return ReceiptConfirmed(source)?bindings[source].After:Bytes(player.GetInventory());}
        public void Apply(string source,byte[] bytes){
            if(!IsBound(source)||!bytes.SequenceEqual(bindings[source].After))throw new InvalidOperationException("Delivery image unavailable.");
            if(ReceiptConfirmed(source))return;
            var live=player.GetInventory();var expected=Read(bytes);var original=live.GetAllItems().ToArray();var changed=new List<ItemDrop.ItemData>();
            if(TheftInventoryGuards.ForeignHeld(live,true))throw new InvalidOperationException("Theft inventory is held by another product.");
            foreach(var item in expected.GetAllItems()){
                var same=original.FirstOrDefault(old=>old.m_gridPos==item.m_gridPos&&SameExceptCount(old,item));
                if(same!=null){same.m_stack=item.m_stack;changed.Add(same);}else changed.Add(item);
            }
            TheftInventoryGuards.Applying=true;
            try{live.GetAllItems().Clear();live.GetAllItems().AddRange(changed);live.Changed();}
            finally{TheftInventoryGuards.Applying=false;}
        }
        internal static bool SameExceptCount(ItemDrop.ItemData a,ItemDrop.ItemData b){
            var first=a.Clone();var second=b.Clone();first.m_stack=second.m_stack=1;
            var p=new ZPackage();var q=new ZPackage();p.Write(first.m_dropPrefab?first.m_dropPrefab.name:"");q.Write(second.m_dropPrefab?second.m_dropPrefab.name:"");first.Save(p);second.Save(q);return p.GetArray().SequenceEqual(q.GetArray());
        }
        internal static string ReceiptKey(string source)=>"wildblood.theft.receipt."+source;
        string Key(string source)=>ReceiptKey(source);
        internal void InvalidateReceipts(){confirmed.Clear();}
        public bool ReceiptConfirmed(string source){
            if(!Current)return false;
            if(confirmed.Contains(source))return true;
            if(unsaved.Contains(source)||!player.m_customData.TryGetValue(Key(source),out var receipt)||receipt!="1")return false;
            confirmed.Add(source);return true;
        }
        public void Save(string source){
            if(!IsBound(source))throw new InvalidOperationException("Delivery actor changed before save.");
            if(ReceiptConfirmed(source))return;
            if(TheftInventoryGuards.ForeignHeld(player.GetInventory(),true))throw new InvalidOperationException("Theft inventory is held by another product.");
            unsaved.Add(source);player.m_customData[Key(source)]="1";
            var profile=Game.instance.GetPlayerProfile();profile.SavePlayerData(player);if(!profile.Save())throw new IOException("Native character save failed.");
            unsaved.Remove(source);if(!ReceiptConfirmed(source))throw new IOException("Delivery character receipt missing.");
        }
        public void RecoveryRequired(string code){if(UnityEngine.Time.realtimeSinceStartupAsDouble<nextWarning)return;nextWarning=UnityEngine.Time.realtimeSinceStartupAsDouble+10;TheftTransfers.Warn("Wildblood theft "+OperationState.RecoveryRequired+": "+code);}
    }
}
