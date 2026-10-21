// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace ExtraPotions.Wildblood.Bosses
{
    public sealed class BossProgressMessage
    {
        public const int MaximumBytes=512;
        static readonly Encoding Utf8=new UTF8Encoding(false,true);
        public bool Complete { get; }
        public bool Terminal { get; }
        public long World { get; }
        public long SourceUser { get; }
        public uint SourceId { get; }
        public string Boss { get; }
        public string Token { get; }
        public string FlightKey=>World.ToString(CultureInfo.InvariantCulture)+"/"+SourceUser.ToString(CultureInfo.InvariantCulture)+"/"+SourceId.ToString(CultureInfo.InvariantCulture)+"/"+Boss+"/"+Token+"/"+(Complete?"1":"0")+(Terminal?"1":"0");
        public BossProgressMessage(bool complete,bool terminal,long world,long sourceUser,uint sourceId,string boss,string token)
        {
            if(world==0||(sourceUser==0&&sourceId==0)||string.IsNullOrEmpty(boss)||boss.Length>32||boss.IndexOfAny(new[]{'\0','\r','\n'})>=0||!BossProgression.ValidToken(token))throw new FormatException("Invalid boss progress fact.");
            Complete=complete;Terminal=terminal;World=world;SourceUser=sourceUser;SourceId=sourceId;Boss=boss;Token=token;
        }
        public byte[] Encode()
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Utf8,true)){
                writer.Write((byte)1);writer.Write((byte)(Complete?1:0));writer.Write((byte)(Terminal?1:0));writer.Write(World);writer.Write(SourceUser);writer.Write(SourceId);
                Text(writer,Boss,128);Text(writer,Token,32);writer.Flush();var bytes=stream.ToArray();if(bytes.Length>MaximumBytes)throw new FormatException("Boss progress packet too large.");return bytes;
            }
        }
        static void Text(BinaryWriter writer,string value,int maximum){var bytes=Utf8.GetBytes(value);if(bytes.Length>maximum)throw new FormatException("Boss progress text too long.");writer.Write((ushort)bytes.Length);writer.Write(bytes);}
        static string Text(BinaryReader reader,int maximum){int length=reader.ReadUInt16();if(length>maximum)throw new FormatException("Boss progress text too long.");var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new FormatException("Truncated boss progress text.");return Utf8.GetString(bytes);}
        static bool Flag(BinaryReader reader){byte flag=reader.ReadByte();if(flag>1)throw new FormatException("Invalid boss progress flag.");return flag==1;}
        public static BossProgressMessage Decode(byte[] bytes)
        {
            if(bytes==null||bytes.Length>MaximumBytes)throw new FormatException("Boss progress packet too large.");
            try{using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Utf8,true)){
                if(reader.ReadByte()!=1)throw new FormatException("Unknown boss progress packet.");
                bool complete=Flag(reader),terminal=Flag(reader);long world=reader.ReadInt64(),user=reader.ReadInt64();uint id=reader.ReadUInt32();
                var result=new BossProgressMessage(complete,terminal,world,user,id,Text(reader,128),Text(reader,32));
                if(stream.Position!=stream.Length)throw new FormatException("Trailing boss progress data.");return result;
            }}catch(EndOfStreamException error){throw new FormatException("Truncated boss progress packet.",error);}catch(DecoderFallbackException error){throw new FormatException("Invalid boss progress UTF8.",error);}
        }
        // The caller supplies AuthorityContext.SenderId from Crucible's actual connection, never packet data or ActorId.
        public static bool TryAuthenticatedOwner(string sender,out long owner)=>long.TryParse(sender,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out owner)&&owner!=0&&owner.ToString(CultureInfo.InvariantCulture)==sender;
    }
    public enum BossProgressFlightStart { Started,Existing,Full }
    public sealed class BossProgressFlightGate
    {
        public const int Capacity=32;
        readonly Dictionary<string,long> active=new Dictionary<string,long>(StringComparer.Ordinal);
        long sequence;
        public int Count=>active.Count;
        public BossProgressFlightStart Start(BossProgressMessage message,out long lease)
        {
            if(active.TryGetValue(message.FlightKey,out lease))return BossProgressFlightStart.Existing;
            if(active.Count>=Capacity){lease=0;return BossProgressFlightStart.Full;}
            lease=checked(++sequence);active.Add(message.FlightKey,lease);return BossProgressFlightStart.Started;
        }
        public bool Finish(BossProgressMessage message,long lease){if(!active.TryGetValue(message.FlightKey,out long current)||current!=lease)return false;active.Remove(message.FlightKey);return true;}
        public void Reset()=>active.Clear();
    }
}
