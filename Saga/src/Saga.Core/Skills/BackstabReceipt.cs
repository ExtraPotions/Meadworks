// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace ExtraPotions.Saga.Skills {
    public sealed class BackstabReceipt {
        public const int MaxBytes=1024;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        public long World{get;}public string Epoch{get;}public long VictimUser{get;}public uint VictimId{get;}public int Prefab{get;}public long Owner{get;}public long AttackerUser{get;}public uint AttackerId{get;}public long Player{get;}public string Event{get;}
        public BackstabReceipt(long world,string epoch,long victimUser,uint victimId,int prefab,long owner,long attackerUser,uint attackerId,long player,string id){
            if(world==0||string.IsNullOrWhiteSpace(epoch)||epoch.Length>128||Utf8.GetByteCount(epoch)>512||epoch.IndexOfAny(new[]{'\0','\r','\n'})>=0||victimUser==0||victimId==0||prefab==0||owner==0||attackerUser==0||attackerId==0||player==0||!Guid.TryParseExact(id,"N",out var guid)||guid==Guid.Empty||guid.ToString("N")!=id)throw new FormatException("Invalid backstab receipt.");
            World=world;Epoch=epoch;VictimUser=victimUser;VictimId=victimId;Prefab=prefab;Owner=owner;AttackerUser=attackerUser;AttackerId=attackerId;Player=player;Event=id;
        }
        public bool Same(BackstabReceipt other)=>other!=null&&World==other.World&&Epoch==other.Epoch&&VictimUser==other.VictimUser&&VictimId==other.VictimId&&Prefab==other.Prefab&&Owner==other.Owner&&AttackerUser==other.AttackerUser&&AttackerId==other.AttackerId&&Player==other.Player&&Event==other.Event;
        public byte[] Encode(){using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Utf8,true)){writer.Write((byte)1);writer.Write(World);var epoch=Utf8.GetBytes(Epoch);writer.Write((ushort)epoch.Length);writer.Write(epoch);writer.Write(VictimUser);writer.Write(VictimId);writer.Write(Prefab);writer.Write(Owner);writer.Write(AttackerUser);writer.Write(AttackerId);writer.Write(Player);writer.Write(Guid.ParseExact(Event,"N").ToByteArray());}return stream.ToArray();}}
        public static BackstabReceipt Decode(byte[] bytes){
            if(bytes==null||bytes.Length>MaxBytes)throw new FormatException("Backstab packet size.");
            try{using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Utf8)){
                if(reader.ReadByte()!=1)throw new FormatException("Backstab packet version.");long world=reader.ReadInt64();int length=reader.ReadUInt16();if(length<1||length>512||length>stream.Length-stream.Position)throw new FormatException("Backstab epoch size.");string epoch=Utf8.GetString(reader.ReadBytes(length));long vu=reader.ReadInt64();uint vi=reader.ReadUInt32();int prefab=reader.ReadInt32();long owner=reader.ReadInt64(),au=reader.ReadInt64();uint ai=reader.ReadUInt32();long player=reader.ReadInt64();var guidBytes=reader.ReadBytes(16);if(guidBytes.Length!=16||stream.Position!=stream.Length)throw new FormatException("Backstab packet boundary.");return new BackstabReceipt(world,epoch,vu,vi,prefab,owner,au,ai,player,new Guid(guidBytes).ToString("N"));
            }}catch(EndOfStreamException error){throw new FormatException("Truncated backstab packet.",error);}catch(DecoderFallbackException error){throw new FormatException("Invalid backstab UTF8.",error);}
        }
    }
    public enum BackstabAdmission { Accepted, Duplicate, Conflict, WrongSession, InvalidAuthority, RateLimited, Capacity }
    public sealed class BackstabReceipts {
        public const int Capacity=4096,PerSecond=64;
        sealed class Entry {internal BackstabReceipt Receipt=null!;internal byte Stages;}
        readonly Dictionary<string,Entry> receipts=new Dictionary<string,Entry>(StringComparer.Ordinal);
        long world;string epoch="";double window=double.NaN;int calls;
        public int Count=>receipts.Count;
        public void Session(long currentWorld,string currentEpoch){if(world==currentWorld&&epoch==currentEpoch)return;world=currentWorld;epoch=currentEpoch;receipts.Clear();window=double.NaN;calls=0;}
        public BackstabAdmission Admit(BackstabReceipt receipt,long currentWorld,string currentEpoch,bool verified,double now,bool recipient=false){
            if(currentWorld==0||string.IsNullOrEmpty(currentEpoch)||receipt.World!=currentWorld||receipt.Epoch!=currentEpoch)return BackstabAdmission.WrongSession;
            Session(currentWorld,currentEpoch);
            if(double.IsNaN(now)||double.IsInfinity(now)||(!double.IsNaN(window)&&now<window))return BackstabAdmission.InvalidAuthority;
            if(double.IsNaN(window)||now-window>=1){window=now;calls=0;}if(calls>=PerSecond)return BackstabAdmission.RateLimited;calls++;
            if(!verified)return BackstabAdmission.InvalidAuthority;
            byte stage=recipient?(byte)2:(byte)1;
            if(receipts.TryGetValue(receipt.Event,out var prior)){if(!prior.Receipt.Same(receipt))return BackstabAdmission.Conflict;if((prior.Stages&stage)!=0)return BackstabAdmission.Duplicate;prior.Stages|=stage;return BackstabAdmission.Accepted;}
            if(receipts.Count>=Capacity)return BackstabAdmission.Capacity;
            receipts.Add(receipt.Event,new Entry{Receipt=receipt,Stages=stage});return BackstabAdmission.Accepted;
        }
        public bool Contains(BackstabReceipt receipt)=>receipts.TryGetValue(receipt.Event,out var prior)&&(prior.Stages&1)!=0&&prior.Receipt.Same(receipt);
    }
}
