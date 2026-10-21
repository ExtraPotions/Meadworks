using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Burrow;
using Burrow.Core;

namespace Burrow.Resources {
    internal sealed class NativeImage {
        internal string Id;internal int Width,Height;internal byte[] Data;
        internal NativeImage Copy()=>new NativeImage{Id=Id,Width=Width,Height=Height,Data=(byte[])Data.Clone()};
        // The game's load leaves some durabilities a hundredth short of what they save as; keep every image byte-exact.
        internal Inventory Load(){var inventory=new Inventory("Burrow payment snapshot",null,Width,Height);inventory.Load(new ZPackage(Data));foreach(var item in inventory.GetAllItems())item.m_durability=SavedDurability.Exact(item.m_durability);return inventory;}
    }
    internal sealed class NativeBundleStore:IInventoryImageStore,IInventoryCheckpointStore,IInventoryRecoveryDiagnostics {
        internal const string HoldKey="burrow.resource-hold",HoldUntil="burrow.resource-hold-until";
        readonly Dictionary<string,(NativeImage[] Before,NativeImage[] After)> bindings=new Dictionary<string,(NativeImage[],NativeImage[])>();
        readonly HashSet<string> confirmed=new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> unsavedPlayerReceipts=new HashSet<string>(StringComparer.Ordinal);
        readonly long world;readonly string actor;
        internal Action<string,Inventory,IReadOnlyList<ItemDrop.ItemData>> BeforeChanged;
        double nextDiagnostic;
        public void RecoveryRequired(string code){if(UnityEngine.Time.realtimeSinceStartupAsDouble<nextDiagnostic)return;nextDiagnostic=UnityEngine.Time.realtimeSinceStartupAsDouble+10;Plugin.Log.LogWarning("Burrow native inventory recovery: "+code);}
        internal NativeBundleStore(InventoryDecisionJournal journal,long world,string actor){this.world=world;this.actor=actor;foreach(var record in journal.Snapshot()){Bind(record.Source,record.Before,record.After);if(record.State==InventoryDecision.Decided)unsavedPlayerReceipts.Add(record.Source);}}
        internal static byte[] Bytes(Inventory inventory){var packet=new ZPackage();inventory.Save(packet);return packet.GetArray();}
        internal static byte[] Encode(NativeImage[] images){var packet=new ZPackage();packet.Write(1);packet.Write(images.Length);foreach(var image in images){packet.Write(image.Id);packet.Write(image.Width);packet.Write(image.Height);packet.Write(image.Data);}return packet.GetArray();}
        static NativeImage[] Decode(byte[] bytes){
            var packet=new ZPackage(bytes);if(packet.ReadInt()!=1)throw new InvalidDataException("Unknown native payment image.");int count=packet.ReadInt();if(count<1||count>32)throw new InvalidDataException("Payment inventory bound.");
            var images=new NativeImage[count];for(int i=0;i<count;i++){var id=packet.ReadString();int width=packet.ReadInt(),height=packet.ReadInt();var data=packet.ReadByteArray();if(id.Length>128||width<1||height<1||width>256||height>256||(long)width*height>65536||data.Length>2096128)throw new InvalidDataException("Invalid native payment image.");images[i]=new NativeImage{Id=id,Width=width,Height=height,Data=data};}
            if(images.Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count()!=count)throw new InvalidDataException("Aliased native payment source.");return images;
        }
        internal void Bind(string source,byte[] before,byte[] after){var a=Decode(before);var b=Decode(after);if(!a.Select(e=>e.Id+":"+e.Width+":"+e.Height).SequenceEqual(b.Select(e=>e.Id+":"+e.Width+":"+e.Height)))throw new InvalidDataException("Native source changed in payment image.");bindings[source]=(a,b);}
        Player Actor=>Player.m_localPlayer&&Player.m_localPlayer.GetPlayerID().ToString(System.Globalization.CultureInfo.InvariantCulture)==actor?Player.m_localPlayer:null;
        Container Chest(string id)=>id.StartsWith("chest:",StringComparison.Ordinal)?NativeChestIdentity.Container(id.Substring(6)):null;
        Inventory Inventory(NativeImage image){if(image.Id=="player:"+actor)return Actor?Actor.GetInventory():null;var chest=Chest(image.Id);return chest?chest.GetInventory():null;}
        public bool IsBound(string source){
            if(NativeChestIdentity.World!=world||!bindings.TryGetValue(source,out var pair)||pair.Before.Any(e=>e.Id.StartsWith("player:",StringComparison.Ordinal))&&!Actor)return false;
            foreach(var image in pair.Before){if(EntryConfirmed(source,image))continue;var inventory=Inventory(image);if(inventory==null||inventory.GetWidth()!=image.Width||inventory.GetHeight()!=image.Height)return false;if(image.Id.StartsWith("chest:",StringComparison.Ordinal)){var chest=Chest(image.Id);if(!chest.m_nview.IsOwner())return false;}}
            return true;
        }
        internal bool Contains(string source,Inventory inventory)=>bindings.TryGetValue(source,out var pair)&&pair.Before.Any(e=>ReferenceEquals(Inventory(e),inventory));
        internal IEnumerable<Inventory> Inventories(string source)=>bindings.TryGetValue(source,out var pair)?pair.Before.Where(e=>!EntryConfirmed(source,e)).Select(Inventory):Enumerable.Empty<Inventory>();
        public byte[] Capture(string source){
            if(!IsBound(source))throw new InvalidOperationException("Native payment owner unavailable.");var pair=bindings[source];var current=pair.Before.Select((e,i)=>EntryConfirmed(source,e)?pair.After[i].Copy():new NativeImage{Id=e.Id,Width=e.Width,Height=e.Height,Data=Bytes(Inventory(e))}).ToArray();
            // Recovery may encounter a save between two inventory applications. Each
            // source must independently match its original or intended exact image.
            bool recognized=current.Select((e,i)=>e.Data.SequenceEqual(pair.Before[i].Data)||e.Data.SequenceEqual(pair.After[i].Data)).All(ok=>ok);
            if(recognized)return current.Select((e,i)=>e.Data.SequenceEqual(pair.After[i].Data)).All(ok=>ok)?Encode(pair.After):Encode(pair.Before);
            return Encode(current);
        }
        void RequireForeignUnlocked(string source){
            foreach(var image in bindings[source].After){
                if(EntryConfirmed(source,image))continue;
                if(NativeInventoryGuards.ForeignInventoryHeld(Inventory(image),true))
                    throw new InvalidOperationException("Native payment inventory is held by another product; recovery deferred.");
            }
        }
        public void Apply(string source,byte[] bytes){
            if(!IsBound(source)||!bytes.SequenceEqual(Encode(bindings[source].After)))throw new InvalidOperationException("Native payment image unavailable.");
            RequireForeignUnlocked(source);
            NativeInventoryGuards.Applying=true;
            try{foreach(var image in bindings[source].After){
                if(EntryConfirmed(source,image))continue;
                RequireForeignUnlocked(source);
                var live=Inventory(image);if(Bytes(live).SequenceEqual(image.Data))continue;
                var expected=image.Load();var original=live.GetAllItems().ToArray();var changed=new List<ItemDrop.ItemData>();
                foreach(var item in expected.GetAllItems()){
                    var same=original.FirstOrDefault(old=>old.m_gridPos==item.m_gridPos&&SameExceptCount(old,item));
                    if(same!=null){same.m_stack=item.m_stack;changed.Add(same);}else changed.Add(item);
                }
                live.GetAllItems().Clear();live.GetAllItems().AddRange(changed);
                BeforeChanged?.Invoke(source,live,original);
                live.Changed();
            }}finally{NativeInventoryGuards.Applying=false;}
        }
        static bool SameExceptCount(ItemDrop.ItemData a,ItemDrop.ItemData b){var first=a.Clone();var second=b.Clone();first.m_stack=1;second.m_stack=1;var p=new ZPackage();var q=new ZPackage();Burrow.Shared.ItemPacket.Write(p,first);Burrow.Shared.ItemPacket.Write(q,second);return p.GetArray().SequenceEqual(q.GetArray());}
        public bool ReceiptConfirmed(string source){
            if(confirmed.Contains(source))return true;
            if(!bindings.TryGetValue(source,out var pair)||NativeChestIdentity.World!=world||pair.Before.Any(e=>e.Id.StartsWith("player:",StringComparison.Ordinal))&&!Actor)return false;
            if(!pair.Before.All(e=>EntryConfirmed(source,e)))return false;
            confirmed.Add(source);return true;
        }
        bool EntryConfirmed(string source,NativeImage image){if(confirmed.Contains(source))return true;string key="burrow.receipt."+source;return image.Id=="player:"+actor?!unsavedPlayerReceipts.Contains(source)&&Actor&&Actor.m_customData.TryGetValue(key,out var receipt)&&receipt=="1":Chest(image.Id)&&Chest(image.Id).m_nview.GetZDO().GetString(key)=="1";}
        public void Save(string source){
            if(!IsBound(source))throw new InvalidOperationException("Native source changed before save.");RequireForeignUnlocked(source);var pair=bindings[source];string key="burrow.receipt."+source;
            foreach(var image in pair.After.Where(e=>e.Id.StartsWith("chest:",StringComparison.Ordinal)&&!EntryConfirmed(source,e))){var chest=Chest(image.Id);chest.Save();if(!chest.m_nview.GetZDO().GetByteArray(ZDOVars.s_items).SequenceEqual(image.Data))throw new IOException("Native chest save did not retain payment.");WriteKeys(chest,zdo=>zdo.Set(key,"1"));}
            if(pair.After.Any(e=>e.Id=="player:"+actor&&!EntryConfirmed(source,e))){
                unsavedPlayerReceipts.Add(source);Actor.m_customData[key]="1";var profile=Game.instance.GetPlayerProfile();profile.SavePlayerData(Actor);if(!profile.Save())throw new IOException("Native character save failed.");unsavedPlayerReceipts.Remove(source);
            }
            if(!ReceiptConfirmed(source))throw new IOException("Native payment receipt is incomplete.");
        }
        internal bool Permitted(string source,float radius){
            if(!IsBound(source)||!Actor||Actor.IsDead()||Actor.IsTeleporting())return false;
            NativeInventoryGuards.Applying=true;try{return bindings[source].Before.Where(e=>e.Id.StartsWith("chest:",StringComparison.Ordinal)).All(e=>{
                var chest=Chest(e.Id);return UnityEngine.Vector3.Distance(Actor.transform.position,chest.transform.position)<=radius&&ContainerScan.IsUsable(chest,ContainerUse.Reach);
            });}finally{NativeInventoryGuards.Applying=false;}
        }
        /// <summary>
        /// Burrow's own keys on a chest's ZDO (hold, receipt, identity) leave its items unchanged. When the chest had read the
        /// ZDO's latest data, its read marker moves along, as TidySweep.WriteMemory does: otherwise the game re-reads the same
        /// items, and a reload during a hold re-creates every item (durabilities a hundredth off), so the payment's exact image
        /// no longer matches and its commit is refused.
        /// </summary>
        internal static void WriteKeys(Container chest,Action<ZDO> write){
            var zdo=chest.m_nview.GetZDO();uint before=zdo.DataRevision;
            write(zdo);
            if(chest.m_nview.IsOwner()&&chest.m_lastRevision==before)chest.m_lastRevision=zdo.DataRevision;
        }
        internal void Hold(string source,bool decided){
            if(!bindings.TryGetValue(source,out var pair))return;
            foreach(var image in pair.Before.Where(e=>e.Id.StartsWith("chest:",StringComparison.Ordinal)&&!EntryConfirmed(source,e))){var chest=Chest(image.Id);if(!chest||!chest.m_nview.IsOwner())continue;WriteKeys(chest,zdo=>{zdo.Set(HoldKey,source);zdo.Set(HoldUntil,decided?0L:DateTime.UtcNow.AddSeconds(10).Ticks);});}
        }
        internal void Release(string source){
            if(!bindings.TryGetValue(source,out var pair))return;
            foreach(var image in pair.Before.Where(e=>e.Id.StartsWith("chest:",StringComparison.Ordinal))){var chest=Chest(image.Id);if(chest&&chest.m_nview.IsOwner()&&chest.m_nview.GetZDO().GetString(HoldKey)==source)WriteKeys(chest,zdo=>{zdo.Set(HoldKey,"");zdo.Set(HoldUntil,0L);});}
        }
    }
}
