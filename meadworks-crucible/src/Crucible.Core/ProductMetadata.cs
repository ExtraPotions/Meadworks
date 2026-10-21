using System;
using System.Text.RegularExpressions;

namespace ExtraPotions.Meadworks.Crucible {
    // ServerOnly is appended so existing numeric values stay stable; see PeerClasses for the peer-class mapping.
    public enum MultiplayerPolicy { ClientOnly, Optional, RequiredOnAllPeers, ServerOnly }
    public sealed class ProductMetadata {
        public string Id { get; }
        public string Name { get; }
        public Version Version { get; }
        public Version MinimumApi { get; }
        public Version MaximumApi { get; }
        public MultiplayerPolicy Multiplayer { get; }
        public string Family { get; }
        public ProductMetadata(string id,string name,Version version,Version minimumApi,Version maximumApi,MultiplayerPolicy multiplayer,string family="Foundation") {
            Validation.Id(id); if(string.IsNullOrWhiteSpace(name)||name.Length>128||name.IndexOfAny(new[]{'\r','\n'})>=0)throw new ArgumentException("Invalid product name.");
            Validation.Version(version);Validation.Version(minimumApi);Validation.Version(maximumApi);
            if(minimumApi>maximumApi)throw new ArgumentException("Reversed API interval.");
            if(!Enum.IsDefined(typeof(MultiplayerPolicy),multiplayer))throw new ArgumentException("Invalid multiplayer policy.");
            if(string.IsNullOrWhiteSpace(family)||family.Length>64||family.IndexOfAny(new[]{'\r','\n'})>=0)throw new ArgumentException("Invalid product family.");
            Id=id;Name=name;Version=version;MinimumApi=minimumApi;MaximumApi=maximumApi;Multiplayer=multiplayer;Family=family;
        }
    }
    internal static class Validation {
        // One instance: the static Regex.IsMatch builds a cache key string per call, and capability lookups validate every frame.
        static readonly Regex IdPattern=new Regex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9_]*)+$");
        internal static bool IsId(string id)=>id!=null&&id.Length<=128&&IdPattern.IsMatch(id);
        internal static void Id(string id) { if(!IsId(id))throw new ArgumentException("Expected a lowercase namespaced ID."); }
        internal static void Version(Version version) { if(version==null||version.Major<0||version.Minor<0)throw new ArgumentException("Expected numeric major.minor version."); }
    }
}
