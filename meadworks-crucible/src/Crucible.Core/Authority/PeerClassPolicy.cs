using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>One product as the join handshake sees it: id, display name, major.minor and peer class.</summary>
    public sealed class PeerProduct {
        public string Id { get; }
        public string Name { get; }
        /// <summary>Major.minor; null for an entry decoded from a v1 frame, which carried no product version.</summary>
        public Version? Version { get; }
        public PeerClass PeerClass { get; }
        public bool Legacy=>Version==null;
        public string VersionText=>Version==null?"unknown version":Version.Major.ToString(CultureInfo.InvariantCulture)+"."+Version.Minor.ToString(CultureInfo.InvariantCulture);
        public PeerProduct(string id,string name,Version? version,PeerClass peerClass){
            Validation.Id(id);
            if(string.IsNullOrWhiteSpace(name)||name.Length>128||name.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new ArgumentException("Invalid product name.",nameof(name));
            if(version!=null&&(version.Major<0||version.Minor<0||version.Build>=0||version.Revision>=0))throw new ArgumentException("Wire versions are major.minor.",nameof(version));
            if(!Enum.IsDefined(typeof(PeerClass),peerClass))throw new ArgumentException("Unknown peer class.",nameof(peerClass));
            Id=id;Name=name;Version=version;PeerClass=peerClass;
        }
        public static PeerProduct From(ProductMetadata product){
            if(product==null)throw new ArgumentNullException(nameof(product));
            return new PeerProduct(product.Id,product.Name,new Version(product.Version.Major,product.Version.Minor),PeerClasses.Of(product));
        }
    }

    public sealed class CapabilitySet {
        /// <summary>1 for the shipped Crucible 0.3 layout, 2 for product major.minor and peer class.</summary>
        public int FormatVersion { get; }
        public IReadOnlyList<PeerProduct> Products { get; }
        internal CapabilitySet(int format,IReadOnlyList<PeerProduct> products){FormatVersion=format;Products=products;}
    }

    /// <summary>
    /// Capability frame payloads. v1 (Crucible 0.3): int32 count, then per product a BinaryWriter string id and the
    /// protocol version 1.0 as two int32. v2: int32 marker -2 (no v1 count is negative), int32 count, then per product
    /// id, name, int32 major, int32 minor and one peer-class byte. Decoding accepts both; newer formats are reported as
    /// unsupported so the player is told to update Crucible.
    /// </summary>
    public static class CapabilityCodec {
        public const int CurrentFormat=2;
        const int MaxBytes=49152,MaxProducts=256;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);

        public static byte[] Encode(IEnumerable<PeerProduct> products){
            var list=Distinct(products);
            if(list.Any(p=>p.Legacy))throw new ArgumentException("Legacy entries have no version to encode.",nameof(products));
            using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,Utf8,true)){
                    writer.Write(-CurrentFormat);writer.Write(list.Count);
                    foreach(var p in list){writer.Write(p.Id);writer.Write(p.Name);writer.Write(p.Version!.Major);writer.Write(p.Version.Minor);writer.Write((byte)p.PeerClass);}
                }
                if(stream.Length>MaxBytes)throw new ArgumentException("Capability frame exceeds bounds.",nameof(products));
                return stream.ToArray();
            }
        }

        /// <summary>The v1 layout, byte for byte as Crucible 0.3 writes it.</summary>
        public static byte[] EncodeV1(IEnumerable<string> productIds){
            var ids=(productIds??throw new ArgumentNullException(nameof(productIds))).ToList();
            if(ids.Count>MaxProducts)throw new ArgumentException("Too many products.",nameof(productIds));
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){
                writer.Write(ids.Count);foreach(var id in ids){writer.Write(id);writer.Write(1);writer.Write(0);}writer.Flush();return stream.ToArray();
            }
        }

        public static CapabilitySet Decode(byte[] bytes){
            if(bytes==null||bytes.Length>MaxBytes||bytes.Length<4)throw new InvalidDataException("Capability frame exceeds bounds.");
            try{
                using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Utf8)){
                    var head=reader.ReadInt32();CapabilitySet set;
                    if(head>=0)set=new CapabilitySet(1,ReadV1(reader,head));
                    else if(head==-CurrentFormat)set=new CapabilitySet(2,ReadV2(reader));
                    else throw new NotSupportedException("Capability format "+(-(long)head).ToString(CultureInfo.InvariantCulture)+" is newer than this Crucible understands; update Crucible.");
                    if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing capability data.");
                    return set;
                }
            }catch(Exception e)when(e is EndOfStreamException||e is ArgumentException||e is DecoderFallbackException||e is FormatException){throw new InvalidDataException("Malformed capability frame.",e);}
        }

        static IReadOnlyList<PeerProduct> ReadV1(BinaryReader reader,int count){
            if(count>MaxProducts)throw new InvalidDataException("Too many products.");
            var list=new List<PeerProduct>(count);var ids=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<count;i++){
                var id=reader.ReadString();var major=reader.ReadInt32();var minor=reader.ReadInt32();
                if(major!=1||minor!=0)throw new InvalidDataException("Protocol mismatch.");
                if(!ids.Add(id))throw new InvalidDataException("Duplicate product.");
                list.Add(new PeerProduct(id,id,null,PeerClass.RequiredOnAllPeers));
            }
            return list.AsReadOnly();
        }

        static IReadOnlyList<PeerProduct> ReadV2(BinaryReader reader){
            var count=reader.ReadInt32();if(count<0||count>MaxProducts)throw new InvalidDataException("Invalid product count.");
            var list=new List<PeerProduct>(count);var ids=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<count;i++){
                var id=reader.ReadString();var name=reader.ReadString();var major=reader.ReadInt32();var minor=reader.ReadInt32();var peer=reader.ReadByte();
                if(major<0||minor<0)throw new InvalidDataException("Invalid version.");
                if(!Enum.IsDefined(typeof(PeerClass),(int)peer))throw new InvalidDataException("Unknown peer class.");
                if(!ids.Add(id))throw new InvalidDataException("Duplicate product.");
                list.Add(new PeerProduct(id,name,new Version(major,minor),(PeerClass)peer));
            }
            return list.AsReadOnly();
        }

        internal static List<PeerProduct> Distinct(IEnumerable<PeerProduct> products){
            var list=(products??throw new ArgumentNullException(nameof(products))).ToList();
            if(list.Any(p=>p==null))throw new ArgumentException("Null product.",nameof(products));
            if(list.Count>MaxProducts)throw new ArgumentException("Too many products.",nameof(products));
            if(list.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=list.Count)throw new ArgumentException("Duplicate product id.",nameof(products));
            return list.OrderBy(p=>p.Id,StringComparer.Ordinal).ToList();
        }
    }

    public enum PeerProductOutcome {
        /// <summary>Both sides run the same major.minor.</summary>
        Match,
        /// <summary>One side sent a v1 frame; the version cannot be compared here (Jotunn still checks it).</summary>
        VersionUnverified,
        /// <summary>Required product the client lacks: refused.</summary>
        MissingOnClient,
        /// <summary>Required product with another major.minor: refused.</summary>
        VersionMismatch,
        /// <summary>Required product the server lacks: turned off on the client for the session.</summary>
        MissingOnServer,
        /// <summary>Client-optional product the server lacks: runs on own settings, cheats locked.</summary>
        ClientOnly,
        /// <summary>Client-optional product with another major.minor: runs on own settings until versions match.</summary>
        Unsynced,
        /// <summary>Server-only product on the client: stays idle.</summary>
        ServerOnlyIgnored,
    }

    public sealed class PeerProductResult {
        public string Id { get; }
        public string Name { get; }
        public PeerProductOutcome Outcome { get; }
        public PeerProduct? Server { get; }
        public PeerProduct? Client { get; }
        /// <summary>The line shown to the player, or null when nothing needs saying.</summary>
        public string? Message { get; }
        internal PeerProductResult(PeerProduct? server,PeerProduct? client,PeerProductOutcome outcome,string? message){var p=server??client!;Id=p.Id;Name=p.Name;Server=server;Client=client;Outcome=outcome;Message=message;}
        public bool Refuses=>Outcome==PeerProductOutcome.MissingOnClient||Outcome==PeerProductOutcome.VersionMismatch;
    }

    public sealed class PeerClassVerdict {
        public IReadOnlyList<PeerProductResult> Products { get; }
        public bool Admit=>RefusalLines.Count==0;
        public IReadOnlyList<string> RefusalLines { get; }
        public IReadOnlyList<string> Notices { get; }
        /// <summary>Required products the server lacks; the client turns them off for the session.</summary>
        public IReadOnlyList<string> DisabledForSession { get; }
        internal PeerClassVerdict(List<PeerProductResult> products){
            Products=products.AsReadOnly();
            RefusalLines=products.Where(p=>p.Refuses).Select(p=>p.Message!).ToList().AsReadOnly();
            Notices=products.Where(p=>!p.Refuses&&p.Message!=null).Select(p=>p.Message!).ToList().AsReadOnly();
            DisabledForSession=products.Where(p=>p.Outcome==PeerProductOutcome.MissingOnServer).Select(p=>p.Id).ToList().AsReadOnly();
        }
        /// <summary>What the authority gate should assume about the server's copy of a product.</summary>
        public ServerProduct ServerStateOf(string productId){
            var result=Products.FirstOrDefault(p=>p.Id==productId);if(result==null)return ServerProduct.Unknown;
            switch(result.Outcome){
                case PeerProductOutcome.MissingOnServer:case PeerProductOutcome.ClientOnly:case PeerProductOutcome.Unsynced:return ServerProduct.Missing;
                case PeerProductOutcome.MissingOnClient:return ServerProduct.Unknown;
                default:return ServerProduct.Present;
            }
        }
        /// <summary>Charter's refusal wording: one line per mismatch, then the code both logs carry.</summary>
        public string RefusalMessage(string code){
            if(code==null||!Regex.IsMatch(code,"^[a-z0-9]{6}$"))throw new ArgumentException("Refusal codes are six characters a-z0-9.",nameof(code));
            return string.Join("\n",RefusalLines)+"\nRefusal code "+code+"; the same code is in the server's log.";
        }
    }

    /// <summary>
    /// Roster peer-class rules for one server and one client product set (major.minor must match). Required products
    /// the client lacks or runs at another major.minor refuse the join with what to install; required products the
    /// server lacks turn off on the client for the session; client-optional products always join; server-only
    /// products are ignored on clients. Results are ordered by product id.
    /// </summary>
    public static class PeerClassPolicy {
        public static PeerClassVerdict Evaluate(IEnumerable<PeerProduct> server,IEnumerable<PeerProduct> client){
            var servers=CapabilityCodec.Distinct(server??throw new ArgumentNullException(nameof(server)));
            var clients=CapabilityCodec.Distinct(client??throw new ArgumentNullException(nameof(client)));
            var results=new List<PeerProductResult>();
            foreach(var id in servers.Select(p=>p.Id).Union(clients.Select(p=>p.Id),StringComparer.Ordinal).OrderBy(i=>i,StringComparer.Ordinal)){
                var s=servers.FirstOrDefault(p=>p.Id==id);var c=clients.FirstOrDefault(p=>p.Id==id);
                var result=s!=null?ServerSide(s,c):ClientSide(c!);
                if(result!=null)results.Add(result);
            }
            return new PeerClassVerdict(results);
        }

        static PeerProductResult? ServerSide(PeerProduct s,PeerProduct? c){
            if(s.PeerClass==PeerClass.ServerOnly)return c==null?null:new PeerProductResult(s,c,PeerProductOutcome.ServerOnlyIgnored,null);
            if(c==null)return s.PeerClass==PeerClass.RequiredOnAllPeers?new PeerProductResult(s,null,PeerProductOutcome.MissingOnClient,"This server requires "+s.Name+" "+s.VersionText+". Install "+s.Name+" "+s.VersionText+" to join."):null;
            if(c.PeerClass==PeerClass.ServerOnly)return new PeerProductResult(s,c,PeerProductOutcome.ServerOnlyIgnored,null);
            if(s.Legacy||c.Legacy)return new PeerProductResult(s,c,PeerProductOutcome.VersionUnverified,null);
            if(s.Version==c.Version)return new PeerProductResult(s,c,PeerProductOutcome.Match,null);
            var have="This server runs "+s.Name+" "+s.VersionText+"; you have "+c.VersionText+". ";
            if(s.PeerClass==PeerClass.ClientOptional)return new PeerProductResult(s,c,PeerProductOutcome.Unsynced,have+s.Name+" uses your own settings until the versions match.");
            return new PeerProductResult(s,c,PeerProductOutcome.VersionMismatch,have+(c.Version<s.Version?"Update "+s.Name+" to "+s.VersionText+" to join.":"Install "+s.Name+" "+s.VersionText+" to join, or ask the host to update."));
        }

        static PeerProductResult ClientSide(PeerProduct c){
            switch(c.PeerClass){
                case PeerClass.RequiredOnAllPeers:return new PeerProductResult(null,c,PeerProductOutcome.MissingOnServer,"This server does not run "+c.Name+"; "+c.Name+" is turned off for this session.");
                case PeerClass.ClientOptional:return new PeerProductResult(null,c,PeerProductOutcome.ClientOnly,null);
                default:return new PeerProductResult(null,c,PeerProductOutcome.ServerOnlyIgnored,null);
            }
        }
    }

    public static class RefusalCodes {
        const string Alphabet="abcdefghijklmnopqrstuvwxyz0123456789";
        /// <summary>Six characters a-z0-9 naming one refusal in the server's and the player's log.</summary>
        public static string New(){
            var bytes=new byte[6];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
            var chars=new char[6];for(int i=0;i<6;i++)chars[i]=Alphabet[bytes[i]%Alphabet.Length];
            return new string(chars);
        }
    }
}
