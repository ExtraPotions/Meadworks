// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace ExtraPotions.Saga.Conversion {
    /// <summary>Bounded Saga callouts carry catalog keys and arguments so every receiver chooses its own language.</summary>
    public static class CalloutText {
        public const string Prefix="~saga1:";
        public static string Encode(string key,params string[] arguments){
            if(!ValidKey(key)||arguments.Length>8)throw new ArgumentException("Invalid Saga callout.");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){
                writer.Write(key);writer.Write((byte)arguments.Length);foreach(var argument in arguments){if(argument==null||argument.Length>512)throw new ArgumentException("Callout argument exceeds its limit.");writer.Write(argument);}writer.Flush();if(stream.Length>4096)throw new ArgumentException("Callout exceeds its limit.");return Prefix+Convert.ToBase64String(stream.ToArray());
            }
        }
        public static bool TryDecode(string text,out string key,out IReadOnlyList<string> arguments){
            key="";arguments=Array.Empty<string>();if(text==null||!text.StartsWith(Prefix,StringComparison.Ordinal)||text.Length>Prefix.Length+5464)return false;
            try {var bytes=Convert.FromBase64String(text.Substring(Prefix.Length));if(bytes.Length>4096)return false;using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)){
                var parsedKey=reader.ReadString();if(!ValidKey(parsedKey))return false;var count=reader.ReadByte();if(count>8)return false;var values=new string[count];for(var n=0;n<count;n++){values[n]=reader.ReadString();if(values[n].Length>512)return false;}if(stream.Position!=stream.Length)return false;key=parsedKey;arguments=Array.AsReadOnly(values);return true;
            }}catch(Exception exception)when(exception is FormatException||exception is IOException||exception is ArgumentException){return false;}
        }
        static bool ValidKey(string key){if(string.IsNullOrEmpty(key)||key.Length>80)return false;foreach(var c in key)if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'))return false;return true;}
    }
}
