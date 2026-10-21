using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// What a server tells a client it refuses at join (C04): the refusal code both logs carry and one line per
    /// reason. Wire format: int32 format 1, code (BinaryWriter string), int32 line count, lines; bounded.
    /// </summary>
    public sealed class RefusalNotice {
        public const int Format=1,MaxLines=64,MaxLineLength=512,MaxBytes=32768;
        static readonly Regex CodePattern=new Regex("^[a-z0-9]{6}$",RegexOptions.CultureInvariant);
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        public string Code { get; }
        public IReadOnlyList<string> Lines { get; }
        /// <summary>Charter's wording: the lines, then the code the server's log carries too.</summary>
        public string Message=>string.Join("\n",Lines)+"\nRefusal code "+Code+"; the same code is in the server's log.";

        public RefusalNotice(string code,IEnumerable<string> lines){
            if(code==null||!CodePattern.IsMatch(code))throw new ArgumentException("Refusal codes are six characters a-z0-9.",nameof(code));
            var list=(lines??throw new ArgumentNullException(nameof(lines))).ToList();
            if(list.Count==0||list.Count>MaxLines)throw new ArgumentException("A refusal names 1 to "+MaxLines+" reasons.",nameof(lines));
            foreach(var line in list)if(string.IsNullOrWhiteSpace(line)||line.Length>MaxLineLength||line.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new ArgumentException("Refusal lines are single non-empty lines of at most "+MaxLineLength+" characters.",nameof(lines));
            Code=code;Lines=list.AsReadOnly();
        }

        public static RefusalNotice From(PeerClassVerdict verdict,string code){
            if(verdict==null)throw new ArgumentNullException(nameof(verdict));
            if(verdict.Admit)throw new ArgumentException("An admitted verdict has no refusal notice.",nameof(verdict));
            return new RefusalNotice(code,verdict.RefusalLines);
        }

        public byte[] Encode(){
            using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,Utf8,true)){writer.Write(Format);writer.Write(Code);writer.Write(Lines.Count);foreach(var line in Lines)writer.Write(line);}
                if(stream.Length>MaxBytes)throw new InvalidOperationException("Refusal notice exceeds bounds.");
                return stream.ToArray();
            }
        }

        public static RefusalNotice Decode(byte[] bytes){
            if(bytes==null||bytes.Length<4||bytes.Length>MaxBytes)throw new InvalidDataException("Refusal notice exceeds bounds.");
            try{
                using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Utf8)){
                    var format=reader.ReadInt32();
                    if(format>Format)throw new NotSupportedException("Refusal notice format "+format+" is newer than this Crucible understands; update Crucible.");
                    if(format!=Format)throw new InvalidDataException("Unknown refusal notice format.");
                    var code=reader.ReadString();var count=reader.ReadInt32();
                    if(count<1||count>MaxLines)throw new InvalidDataException("Invalid refusal line count.");
                    var lines=new List<string>(count);for(int i=0;i<count;i++)lines.Add(reader.ReadString());
                    if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing refusal data.");
                    return new RefusalNotice(code,lines);
                }
            }catch(Exception e)when(e is EndOfStreamException||e is ArgumentException||e is DecoderFallbackException||e is FormatException){throw new InvalidDataException("Malformed refusal notice.",e);}
        }
    }

    /// <summary>The client's side of the join greeting.</summary>
    public sealed class PeerJoinClient {
        /// <summary>The client's own verdict on the server's set; null when the greeting could not be read.</summary>
        public PeerClassVerdict? Verdict { get; }
        /// <summary>The client's v2 set to send back; null while the handshake is off or the greeting was unreadable.</summary>
        public byte[]? Answer { get; }
        /// <summary>Why the greeting could not be used, for the log.</summary>
        public string? Problem { get; }
        internal PeerJoinClient(PeerClassVerdict? verdict,byte[]? answer,string? problem){Verdict=verdict;Answer=answer;Problem=problem;}
    }

    /// <summary>The server's side of the join greeting.</summary>
    public sealed class PeerJoinServer {
        public bool Admit=>Notice==null;
        public PeerClassVerdict? Verdict { get; }
        /// <summary>The complete v2 product list supplied by an admitted peer, or null when it was unavailable.</summary>
        public IReadOnlyList<PeerProduct>? ClientProducts { get; }
        /// <summary>The notice to send before disconnecting a refused client.</summary>
        public RefusalNotice? Notice { get; }
        /// <summary>A note for the server log when the answer was admitted unverified.</summary>
        public string? Problem { get; }
        internal PeerJoinServer(PeerClassVerdict? verdict,RefusalNotice? notice,string? problem,IReadOnlyList<PeerProduct>? clientProducts=null){Verdict=verdict;Notice=notice;Problem=problem;ClientProducts=clientProducts;}
    }

    /// <summary>
    /// C04 at <c>ZNet.RPC_PeerInfo</c> (Charter family-check pattern): when the game accepts a peer, the server greets
    /// it with its v2 product set; the client evaluates the set (turning off required products the server lacks,
    /// recording its own refusal) and, while the handshake is on, answers with its own set; the server refuses a client
    /// missing a required product or running another major.minor with a <see cref="RefusalNotice"/> and disconnects it.
    /// A client that never answers (handshake off, Crucible 0.3) is admitted unverified, as v1 always was.
    /// </summary>
    public static class PeerJoin {
        public const string UnreadableAnswer="Crucible could not read this client's product list; update Crucible on both sides.";

        public static PeerJoinClient Client(IEnumerable<PeerProduct> local,byte[] greeting,bool handshakeOn){
            if(local==null)throw new ArgumentNullException(nameof(local));
            CapabilitySet server;
            try{server=CapabilityCodec.Decode(greeting??throw new ArgumentNullException(nameof(greeting)));}
            catch(NotSupportedException e){return new PeerJoinClient(null,null,e.Message);}
            catch(InvalidDataException e){return new PeerJoinClient(null,null,"The server's product list could not be read: "+e.Message);}
            var products=local.ToList();
            var verdict=PeerClassPolicy.Evaluate(server.Products,products);
            return new PeerJoinClient(verdict,handshakeOn?CapabilityCodec.Encode(products):null,null);
        }

        public static PeerJoinServer Server(IEnumerable<PeerProduct> local,byte[] answer,Func<string> newCode){
            if(local==null)throw new ArgumentNullException(nameof(local));
            if(answer==null)throw new ArgumentNullException(nameof(answer));
            if(newCode==null)throw new ArgumentNullException(nameof(newCode));
            CapabilitySet client;
            try{client=CapabilityCodec.Decode(answer);}
            catch(NotSupportedException e){return new PeerJoinServer(null,null,"Client answered with a newer product list format; versions unverified ("+e.Message+")");}
            catch(InvalidDataException){return new PeerJoinServer(null,new RefusalNotice(newCode(),new[]{UnreadableAnswer}),null);}
            if(client.FormatVersion<2)return new PeerJoinServer(null,null,null);
            var verdict=PeerClassPolicy.Evaluate(local,client.Products);
            return verdict.Admit?new PeerJoinServer(verdict,null,null,client.Products):new PeerJoinServer(verdict,RefusalNotice.From(verdict,newCode()),null);
        }
    }
}
