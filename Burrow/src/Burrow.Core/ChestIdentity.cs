using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtraPotions.Burrow {
    /// <summary>World-scoped native chest identity and retained runtime aliases.</summary>
    public sealed class ChestIdentity {
        public const int MaximumAliases=512,MaximumBytes=32768;
        readonly string[] aliases;
        public long World {get;}
        public string Token {get;}
        public IReadOnlyList<string> Aliases=>Array.AsReadOnly(aliases);
        ChestIdentity(long world,string token,string[] aliases){World=world;Token=token;this.aliases=aliases;}
        public static ChestIdentity Create(long world,string nativeId)=>new ChestIdentity(world,"persistent:"+Guid.NewGuid().ToString("N"),new[]{nativeId}).Validate();
        ChestIdentity Validate(){
            if(World==0||Token==null||!Token.StartsWith("persistent:",StringComparison.Ordinal)||!Guid.TryParseExact(Token.Substring(11),"N",out _)||aliases.Length<1||aliases.Length>MaximumAliases||aliases.Distinct(StringComparer.Ordinal).Count()!=aliases.Length||aliases.Any(a=>!NativeId(a)))throw new InvalidDataException("Invalid native chest identity.");
            return this;
        }
        static bool NativeId(string value){
            var parts=value?.Split(':');return parts!=null&&parts.Length==2&&long.TryParse(parts[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out var creator)&&uint.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out var serial)&&serial!=0&&value==creator.ToString(CultureInfo.InvariantCulture)+":"+serial.ToString(CultureInfo.InvariantCulture);
        }
        public bool Matches(string value)=>Token==value||Array.IndexOf(aliases,value)>=0;
        public ChestIdentity Remember(string nativeId){if(Array.IndexOf(aliases,nativeId)>=0)return this;return new ChestIdentity(World,Token,aliases.Concat(new[]{nativeId}).ToArray()).Validate();}
        public byte[] Encode(){
            Validate();using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(1);writer.Write(World);writer.Write(Token);writer.Write(aliases.Length);foreach(var alias in aliases)writer.Write(alias);}
                byte[] body=stream.ToArray();using(var sha=SHA256.Create())stream.Write(sha.ComputeHash(body),0,32);
                if(stream.Length>MaximumBytes)throw new InvalidDataException("Chest identity exceeds bound.");return stream.ToArray();
            }
        }
        public static ChestIdentity Read(byte[] bytes,long world){
            if(bytes==null||bytes.Length<49||bytes.Length>MaximumBytes)throw new InvalidDataException("Chest identity byte bound.");
            byte[] body=bytes.Take(bytes.Length-32).ToArray();using(var sha=SHA256.Create())if(!sha.ComputeHash(body).SequenceEqual(bytes.Skip(body.Length)))throw new InvalidDataException("Chest identity checksum.");
            using(var stream=new MemoryStream(body,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)){
                if(reader.ReadInt32()!=1||reader.ReadInt64()!=world)throw new InvalidDataException("Chest identity world or version.");
                string token=reader.ReadString();int count=reader.ReadInt32();if(count<1||count>MaximumAliases)throw new InvalidDataException("Chest alias bound.");
                var aliases=new string[count];for(int i=0;i<count;i++)aliases[i]=reader.ReadString();
                if(stream.Position!=stream.Length)throw new InvalidDataException("Chest identity trailing bytes.");return new ChestIdentity(world,token,aliases).Validate();
            }
        }
        public static T Resolve<T>(IEnumerable<T> sources,string requested,Func<T,string> nativeId,Func<T,ChestIdentity?> identity,bool requirePersistent=true) where T:class {
            T? found=null;ChestIdentity? persisted=null;
            foreach(var source in sources){
                var value=identity(source);if(nativeId(source)!=requested&&value?.Matches(requested)!=true)continue;
                if(found!=null)return null!;found=source;persisted=value;
            }
            return found!=null&&(!requirePersistent||persisted?.Matches(requested)==true)?found:null!;
        }
    }
}
