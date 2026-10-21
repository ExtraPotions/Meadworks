// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Local journal correlation only; native ask/reply transport fields remain unchanged.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace ExtraPotions.Wildblood.Transfers;

public sealed class TheftRequest
{
    public string Root { get; }
    public long World { get; }
    public long Actor { get; }
    public long SourceUser { get; }
    public uint SourceId { get; }
    public long Owner { get; }
    public int Counter { get; }
    public int X { get; }
    public int Y { get; }
    public string Session { get; }
    public string Fingerprint { get; }
    public TheftRequest(string root,long world,long actor,long sourceUser,uint sourceId,long owner,int counter,int x,int y,string session,string fingerprint)
    { Root=root;World=world;Actor=actor;SourceUser=sourceUser;SourceId=sourceId;Owner=owner;Counter=counter;X=x;Y=y;Session=session;Fingerprint=fingerprint; }
}
public enum TheftDisposition { Approved, Denied, Recovery }
public sealed class TheftReceipt
{
    public string Root { get; }
    public string Fingerprint { get; }
    public TheftDisposition Disposition { get; }
    public TheftReceipt(string root,string fingerprint,TheftDisposition disposition) {Root=root;Fingerprint=fingerprint;Disposition=disposition;}
    public bool Matches(TheftRequest request) => request != null && Root==request.Root && Fingerprint==TheftMetadata.Hash(TheftMetadata.Encode(request));
    public static TheftReceipt For(TheftRequest request,bool banked) => new(request.Root,TheftMetadata.Hash(TheftMetadata.Encode(request)),banked?TheftDisposition.Approved:TheftDisposition.Denied);
}
public static class TheftMetadata
{
    public const int Maximum=2048;
    public static string Hash(byte[] value) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
    static bool Hex(string value,int length)=>value!=null&&value.Length==length&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    public static byte[] Encode(TheftRequest request)
    {
        if(request==null||!Hex(request.Root,32)||request.World==0||request.Actor==0||request.Owner==0||request.Counter<=0
            ||request.SourceUser==0||request.SourceId==0||request.X<0||request.Y<0||request.X>=256||request.Y>=256
            ||string.IsNullOrEmpty(request.Session)||request.Session.Length>128||request.Session.Any(char.IsControl)||!Hex(request.Fingerprint,64))
            throw new InvalidDataException("Theft correlation invalid.");
        using(var stream=new MemoryStream()) {using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)) {
            writer.Write(1);writer.Write(request.Root);writer.Write(request.World);writer.Write(request.Actor);writer.Write(request.SourceUser);
            writer.Write(request.SourceId);writer.Write(request.Owner);writer.Write(request.Counter);writer.Write(request.X);writer.Write(request.Y);
            writer.Write(request.Session);writer.Write(request.Fingerprint);
        }return stream.ToArray();}
    }
    public static TheftRequest Decode(byte[] value)
    {
        if(value==null||value.Length>Maximum)throw new InvalidDataException("Theft correlation bound.");
        using(var stream=new MemoryStream(value,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)) {
            if(reader.ReadInt32()!=1)throw new InvalidDataException("Theft correlation version.");
            var request=new TheftRequest(reader.ReadString(),reader.ReadInt64(),reader.ReadInt64(),reader.ReadInt64(),reader.ReadUInt32(),reader.ReadInt64(),reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32(),reader.ReadString(),reader.ReadString());
            if(stream.Position!=stream.Length||!Encode(request).SequenceEqual(value))throw new InvalidDataException("Theft correlation trailing data.");return request;
        }
    }
    public static byte[] Encode(TheftReceipt receipt)
    {
        if(receipt==null||!Hex(receipt.Root,32)||!Hex(receipt.Fingerprint,64)||!Enum.IsDefined(typeof(TheftDisposition),receipt.Disposition))throw new InvalidDataException("Theft outcome invalid.");
        using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(1);writer.Write(receipt.Root);writer.Write(receipt.Fingerprint);writer.Write((int)receipt.Disposition);}return stream.ToArray();}
    }
    public static TheftReceipt Receipt(byte[] value)
    {
        if(value==null||value.Length>Maximum)throw new InvalidDataException("Theft outcome bound.");
        using(var stream=new MemoryStream(value,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)) {
            if(reader.ReadInt32()!=1)throw new InvalidDataException("Theft outcome version.");
            var receipt=new TheftReceipt(reader.ReadString(),reader.ReadString(),(TheftDisposition)reader.ReadInt32());
            if(stream.Position!=stream.Length||!Encode(receipt).SequenceEqual(value))throw new InvalidDataException("Theft outcome trailing data.");return receipt;
        }
    }
}
