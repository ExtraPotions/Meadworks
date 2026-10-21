using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Durable per-object token. Valheim reassigns ZDOIDs when a world loads, so products that must recognize
    /// the same object across sessions store one of these on the object's ZDO instead of keeping its ZDOID.
    /// </summary>
    public readonly struct InstanceToken:IEquatable<InstanceToken> {
        static readonly Regex Shape=new Regex("^[0-9a-f]{32}$",RegexOptions.CultureInvariant);
        public string Value { get; }
        InstanceToken(string value){Value=value;}
        public static InstanceToken New()=>From(Guid.NewGuid());
        public static InstanceToken From(Guid guid){if(guid==Guid.Empty)throw new InvalidOperationException("Instance token GUID must not be empty.");return new InstanceToken(guid.ToString("N"));}
        public static bool IsValid(string? value)=>value!=null&&Shape.IsMatch(value)&&value!=EmptyText;
        public static bool TryParse(string? value,out InstanceToken token){token=default;if(!IsValid(value))return false;token=new InstanceToken(value!);return true;}
        public static InstanceToken Parse(string value)=>TryParse(value,out var token)?token:throw new FormatException("Invalid instance token.");
        static readonly string EmptyText=new string('0',32);
        public bool Equals(InstanceToken other)=>string.Equals(Value,other.Value,StringComparison.Ordinal);
        public override bool Equals(object? obj)=>obj is InstanceToken other&&Equals(other);
        public override int GetHashCode()=>Value==null?0:StringComparer.Ordinal.GetHashCode(Value);
        public static bool operator==(InstanceToken left,InstanceToken right)=>left.Equals(right);
        public static bool operator!=(InstanceToken left,InstanceToken right)=>!left.Equals(right);
        public override string ToString()=>Value??"";
    }

    /// <summary>Minimal string storage over one object (a ZDO in game, a dictionary in tests).</summary>
    public interface IInstanceIdentityStore {
        /// <summary>Whether this peer may write the object now (ZDO owner).</summary>
        bool CanWrite { get; }
        /// <summary>Returns the stored value, or an empty string when absent (ZDO.GetString semantics).</summary>
        string Read(string key);
        void Write(string key,string value);
    }

    public enum InstanceIdentityStatus { Existing, Assigned, NotOwner, Invalid }

    public sealed class InstanceIdentityResult {
        public InstanceIdentityStatus Status { get; }
        public InstanceToken? Token { get; }
        internal InstanceIdentityResult(InstanceIdentityStatus status,InstanceToken? token){Status=status;Token=token;}
        public bool HasToken=>Token!=null;
    }

    public static class InstanceIdentity {
        static readonly Regex Segment=new Regex("^[a-z][a-z0-9_]*$",RegexOptions.CultureInvariant);
        static readonly Regex Key=new Regex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$",RegexOptions.CultureInvariant);
        /// <summary>Default ZDO key for a product, e.g. "wildroot.instance" (the key Wildroot already writes).</summary>
        public static string KeyFor(string product){if(product==null||!Segment.IsMatch(product))throw new ArgumentException("Invalid product identity namespace.");return product+".instance";}
        public static bool IsValidKey(string? key)=>key!=null&&Key.IsMatch(key);
        static void RequireKey(string key){if(!IsValidKey(key))throw new ArgumentException("Invalid instance identity key.");}

        public static InstanceToken? Read(IInstanceIdentityStore store,string key){
            if(store==null)throw new ArgumentNullException(nameof(store));RequireKey(key);
            return InstanceToken.TryParse(store.Read(key),out var token)?token:(InstanceToken?)null;
        }

        /// <summary>
        /// Returns the stored token, or assigns one when this peer owns the object and none is stored.
        /// A malformed stored value is reported as Invalid and left untouched unless <paramref name="repairInvalid"/> is set.
        /// </summary>
        public static InstanceIdentityResult Ensure(IInstanceIdentityStore store,string key,Func<Guid>? generator=null,bool repairInvalid=false){
            if(store==null)throw new ArgumentNullException(nameof(store));RequireKey(key);
            var raw=store.Read(key)??"";
            if(InstanceToken.TryParse(raw,out var existing))return new InstanceIdentityResult(InstanceIdentityStatus.Existing,existing);
            bool corrupt=raw.Length>0;
            if(corrupt&&!repairInvalid)return new InstanceIdentityResult(InstanceIdentityStatus.Invalid,null);
            if(!store.CanWrite)return new InstanceIdentityResult(corrupt?InstanceIdentityStatus.Invalid:InstanceIdentityStatus.NotOwner,null);
            var token=InstanceToken.From((generator??Guid.NewGuid)());
            store.Write(key,token.Value);
            return new InstanceIdentityResult(InstanceIdentityStatus.Assigned,token);
        }

        /// <summary>Finds the single source carrying <paramref name="token"/>; returns null when absent or ambiguous.</summary>
        public static T? Resolve<T>(IEnumerable<T> sources,InstanceToken token,Func<T,InstanceToken?> identity) where T:struct{
            T? found=null;
            foreach(var source in sources){if(identity(source)!=token)continue;if(found!=null)return null;found=source;}
            return found;
        }
        public static T? ResolveObject<T>(IEnumerable<T> sources,InstanceToken token,Func<T,InstanceToken?> identity) where T:class{
            T? found=null;
            foreach(var source in sources){if(identity(source)!=token)continue;if(found!=null)return null;found=source;}
            return found;
        }

        /// <summary>Tokens carried by more than one source (copied ZDOs), each reported once in first-seen order.</summary>
        public static IReadOnlyList<InstanceToken> Duplicates<T>(IEnumerable<T> sources,Func<T,InstanceToken?> identity){
            var seen=new HashSet<InstanceToken>();var reported=new HashSet<InstanceToken>();var result=new List<InstanceToken>();
            foreach(var source in sources){var token=identity(source);if(token==null)continue;if(!seen.Add(token.Value)&&reported.Add(token.Value))result.Add(token.Value);}
            return result.AsReadOnly();
        }
    }

    /// <summary>World-scoped token for identities persisted outside the world save (e.g. Burrow container indexes).</summary>
    public readonly struct InstanceReference:IEquatable<InstanceReference> {
        public long World { get; }
        public InstanceToken Token { get; }
        public InstanceReference(long world,InstanceToken token){if(world==0)throw new ArgumentOutOfRangeException(nameof(world),"World id must be non-zero.");if(!InstanceToken.IsValid(token.Value))throw new ArgumentException("Invalid instance token.",nameof(token));World=world;Token=token;}
        public bool BelongsTo(long world)=>World==world;
        public override string ToString()=>World.ToString(CultureInfo.InvariantCulture)+":"+Token.Value;
        public static bool TryParse(string? text,out InstanceReference reference){
            reference=default;if(text==null)return false;var split=text.IndexOf(':');if(split<=0)return false;
            var worldText=text.Substring(0,split);
            if(!long.TryParse(worldText,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var world)||world==0||world.ToString(CultureInfo.InvariantCulture)!=worldText)return false;
            if(!InstanceToken.TryParse(text.Substring(split+1),out var token))return false;
            reference=new InstanceReference(world,token);return true;
        }
        public bool Equals(InstanceReference other)=>World==other.World&&Token==other.Token;
        public override bool Equals(object? obj)=>obj is InstanceReference other&&Equals(other);
        public override int GetHashCode()=>(World.GetHashCode()*397)^Token.GetHashCode();
        public static bool operator==(InstanceReference left,InstanceReference right)=>left.Equals(right);
        public static bool operator!=(InstanceReference left,InstanceReference right)=>!left.Equals(right);
    }
}
