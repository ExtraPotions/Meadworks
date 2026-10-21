// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;
namespace ExtraPotions.Saga.Powers {
    public readonly struct PowerCommand {
        public readonly long Player;public readonly string Boss;public readonly int Count;public readonly bool Reset;
        public PowerCommand(long player,string boss,int count,bool reset){if(player==0||string.IsNullOrWhiteSpace(boss)||boss.Length>64||boss.IndexOfAny(new[]{'\r','\n',',',':'})>=0||count<0||count>1000000||!reset&&count==0)throw new ArgumentException("Invalid power command");Player=player;Boss=boss;Count=count;Reset=reset;}
        public byte[] Encode(){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write((byte)1);writer.Write(Player);writer.Write(Boss);writer.Write(Count);writer.Write(Reset);writer.Flush();return stream.ToArray();}}
        public static PowerCommand Decode(byte[] bytes){if(bytes==null||bytes.Length>256)throw new FormatException("Power command size");using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){if(reader.ReadByte()!=1)throw new FormatException("Power command version");var result=new PowerCommand(reader.ReadInt64(),reader.ReadString(),reader.ReadInt32(),reader.ReadBoolean());if(stream.Position!=stream.Length)throw new FormatException("Trailing power command bytes");return result;}}
    }
    public enum PowerRoute { Unavailable, ThisMachine, Owner }
    /// <summary>Where the authority sends an admin's power command. A host holds the avatar objects of the players around it; a dedicated server holds none and knows a player only by the ZDO the player's machine owns.</summary>
    public static class PowerCommandRoute {
        public static PowerRoute Choose(bool avatarHere,bool avatarIsMine,bool zdoKnown)=>avatarHere?(avatarIsMine?PowerRoute.ThisMachine:PowerRoute.Owner):zdoKnown?PowerRoute.Owner:PowerRoute.Unavailable;
    }
}
