using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Wildroot {
 public enum ExtraResourceOrigin {None,LocalFile,Server}
 public enum CatalogMatch {Unknown,Same,Different}
 /// <summary>
 /// A client's catalog briefly differs from the server's while an administrator's change arrives one setting at a time, which
 /// needs nothing from the player. A difference is reported once, and only after the same pair of fingerprints has lasted
 /// <see cref="SettleSeconds"/>; a real mismatch (another Wildroot version or prefab mod) lasts and is reported.
 /// </summary>
 public sealed class CatalogMismatchGate {
  public const double SettleSeconds=10;
  string pending="",warned="";double since;
  /// <summary>True while a difference waits to settle; the caller observes again once <see cref="SettleSeconds"/> have passed.</summary>
  public bool Pending=>pending.Length>0&&!string.Equals(pending,warned,StringComparison.Ordinal);
  public double Since=>since;
  /// <summary>Returns the "server/local" pair to report, or null.</summary>
  public string? Observe(CatalogMatch match,string? server,string? local,double now){
   if(match!=CatalogMatch.Different){pending="";warned="";return null;}
   var key=server+"/"+local;
   if(!string.Equals(key,pending,StringComparison.Ordinal)){pending=key;since=now;return null;}
   if(now-since<SettleSeconds||string.Equals(key,warned,StringComparison.Ordinal))return null;
   warned=key;return key;
  }
 }
 /// <summary>
 /// Distribution rules. The built-in catalog and every catalog setting are server-authoritative Crucible
 /// settings (C01, carried by the Jotunn bridge), so peers derive the same catalog from the same values. The one input
 /// that is not a setting, the extra-resource file, travels the same way: the authority publishes its file text in a
 /// hidden server-authoritative setting, and remote clients build from that text instead of their own file. The server
 /// also publishes a fingerprint of its effective catalog so a client can tell when its own build differs (for example
 /// a prefab mod the server has and the client lacks).
 /// </summary>
 public static class CatalogDistribution {
  /// <summary>Upper bound for the shared extra-resource text; a larger file stays local to the authority.</summary>
  public const int MaxSharedLength=65536;
  public static ExtraResourceOrigin Origin(SessionRole role,bool enabled)=>!enabled?ExtraResourceOrigin.None:role==SessionRole.RemoteClient?ExtraResourceOrigin.Server:ExtraResourceOrigin.LocalFile;
  /// <summary>Roles whose machine serves other peers and therefore publishes extras and the fingerprint.</summary>
  public static bool Publishes(SessionRole role)=>role==SessionRole.Host||role==SessionRole.DedicatedServer;
  /// <summary>The text to publish, empty for none, or null when it exceeds <see cref="MaxSharedLength"/>.</summary>
  public static string? Shareable(string? text){text??="";return text.Length>MaxSharedLength?null:text;}
  /// <summary>Order-independent SHA-256 (first 16 hex digits) over every definition fact a peer acts on.</summary>
  public static string Fingerprint(IEnumerable<PlantDefinition> definitions){
   if(definitions==null)throw new ArgumentNullException(nameof(definitions));
   var lines=definitions.Select(Line).OrderBy(l=>l,StringComparer.Ordinal);
   using var sha=SHA256.Create();var hash=sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n",lines)));
   var text=new StringBuilder(16);for(int i=0;i<8;i++)text.Append(hash[i].ToString("x2",CultureInfo.InvariantCulture));return text.ToString();
  }
  public static CatalogMatch Compare(string? server,string? local)=>string.IsNullOrEmpty(server)||string.IsNullOrEmpty(local)?CatalogMatch.Unknown:string.Equals(server,local,StringComparison.Ordinal)?CatalogMatch.Same:CatalogMatch.Different;
  static string Line(PlantDefinition d){
   string N(double v)=>v.ToString("R",CultureInfo.InvariantCulture);
   return string.Join("|",d.Id,d.Prefab,d.GrownPrefab,d.Kind.ToString(),string.Join(",",d.Costs.Select(c=>c.ItemId+":"+c.Amount.ToString(CultureInfo.InvariantCulture))),d.HarvestItem,d.Yield.ToString(CultureInfo.InvariantCulture),N(d.GrowMin),N(d.GrowMax),N(d.RespawnSeconds),N(d.Radius),N(d.ScaleMin),N(d.ScaleMax),d.Enabled?"1":"0",d.Free?"1":"0");
  }
 }

 public sealed class RecordedValue {
  public object Handle{get;}public string Member{get;}public object? Original{get;}
  internal RecordedValue(object handle,string member,object? original){Handle=handle;Member=member;Original=original;}
 }
 /// <summary>
 /// Snapshot of native prefab state: the first original value of every (object, member) Wildroot changes, kept
 /// until restore. Restore hands them back newest first and forgets them, so disabling, a vanilla server or shutdown
 /// returns the game's own values and a later enable captures again.
 /// </summary>
 public sealed class MutationLedger {
  sealed class Key:IEquatable<Key> {
   readonly object handle;readonly string member;public Key(object handle,string member){this.handle=handle;this.member=member;}
   public bool Equals(Key? other)=>other!=null&&ReferenceEquals(handle,other.handle)&&string.Equals(member,other.member,StringComparison.Ordinal);
   public override bool Equals(object? obj)=>Equals(obj as Key);
   public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(handle)*31+StringComparer.Ordinal.GetHashCode(member);
  }
  readonly Dictionary<Key,RecordedValue> index=new Dictionary<Key,RecordedValue>();readonly List<RecordedValue> order=new List<RecordedValue>();
  public int Count=>order.Count;
  /// <returns>True when this is the first capture of the member since the last restore.</returns>
  public bool Capture(object handle,string member,object? original){
   if(handle==null)throw new ArgumentNullException(nameof(handle));if(string.IsNullOrEmpty(member))throw new ArgumentException("Expected a member name.",nameof(member));
   var key=new Key(handle,member);if(index.ContainsKey(key))return false;
   var value=new RecordedValue(handle,member,original);index.Add(key,value);order.Add(value);return true;
  }
  public RecordedValue? Original(object handle,string member)=>handle!=null&&member!=null&&index.TryGetValue(new Key(handle,member),out var value)?value:null;
  public IReadOnlyList<RecordedValue> Restore(){var result=order.AsEnumerable().Reverse().ToList();order.Clear();index.Clear();return result.AsReadOnly();}
 }

 /// <summary>Which pieces stay on the cultivator when Wildroot steps back: the game's own, minus everything Wildroot manages that the game does not list.</summary>
 public static class CultivatorMembership {
  public static IReadOnlyList<string> Restore(IEnumerable<string> current,IEnumerable<string> native,IEnumerable<string> managed){
   var nativeSet=new HashSet<string>(native??throw new ArgumentNullException(nameof(native)),StringComparer.Ordinal);var managedSet=new HashSet<string>(managed??throw new ArgumentNullException(nameof(managed)),StringComparer.Ordinal);
   var result=new List<string>();
   foreach(var name in current??throw new ArgumentNullException(nameof(current)))if((!managedSet.Contains(name)||nativeSet.Contains(name))&&!result.Contains(name))result.Add(name);
   foreach(var name in native)if(!result.Contains(name))result.Add(name);
   return result.AsReadOnly();
  }
 }
}
