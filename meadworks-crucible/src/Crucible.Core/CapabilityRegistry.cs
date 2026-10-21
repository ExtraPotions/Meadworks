using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    public sealed class CapabilityMetadata {
        public string Id { get; }
        public Version Version { get; }
        public string Owner { get; }
        internal CapabilityMetadata(string id,Version version,string owner){Id=id;Version=version;Owner=owner;}
    }
    public sealed class CapabilityRegistry {
        sealed class Entry {
            internal readonly ProductRegistration Owner;
            internal readonly CapabilityMetadata Metadata;
            internal readonly object Service;
            internal readonly Type Contract;
            internal Entry(ProductRegistration owner,string id,Version version,object service,Type contract){Owner=owner;Metadata=new CapabilityMetadata(id,version,owner.Metadata.Id);Service=service;Contract=contract;}
        }
        readonly List<Entry> entries=new List<Entry>();
        /// <summary>Changes on every capability added or removed; a consumer caches a resolution and revalidates it by comparing this number, without allocating.</summary>
        public int Generation { get; private set; }
        internal CapabilityRegistry(ProductRegistry registry) { }
        internal void Add<T>(ProductRegistration owner,string id,Version version,T service) where T:class {
            owner.EnsureActive();Validation.Id(id);Validation.Version(version);
            // First-party capabilities use meadworks.*; another mod publishes under its own product id (com.example.mod.name).
            if(!id.StartsWith("meadworks.",StringComparison.Ordinal)&&!id.StartsWith(owner.Metadata.Id+".",StringComparison.Ordinal))throw new ArgumentException("Capabilities use the meadworks namespace or the owning product id as prefix.");
            if(service==null)throw new ArgumentNullException(nameof(service));
            if(entries.Any(e=>ReferenceEquals(e.Owner,owner)&&e.Metadata.Id==id))throw new InvalidOperationException("Owner already provides this capability.");
            entries.Add(new Entry(owner,id,version,service,typeof(T)));Generation++;
        }
        /// <summary>Optional lookup: a malformed id or version simply resolves nothing.</summary>
        public bool TryResolve<T>(string id,Version minimumVersion,out T? service) where T:class {
            service=null;if(!Validation.IsId(id)||minimumVersion==null||minimumVersion.Major<0||minimumVersion.Minor<0)return false;
            // A loop, not LINQ: products resolve optional capabilities every frame.
            Entry? found=null;
            for(int i=0;i<entries.Count;i++){
                var e=entries[i];
                if(e.Metadata.Id!=id||e.Metadata.Version.Major!=minimumVersion.Major||e.Metadata.Version<minimumVersion||e.Contract!=typeof(T))continue;
                if(found!=null)return false;
                found=e;
            }
            if(found==null)return false;service=(T)found.Service;return true;
        }
        /// <summary>Every provider of a per-product capability (one entry per owner, compatible version and exact type), ordered by owner.</summary>
        public IReadOnlyList<KeyValuePair<string,T>> ResolveEach<T>(string id,Version minimumVersion) where T:class {
            Validation.Id(id);Validation.Version(minimumVersion);
            return Array.AsReadOnly(entries.Where(e=>e.Metadata.Id==id&&e.Metadata.Version.Major==minimumVersion.Major&&e.Metadata.Version>=minimumVersion&&e.Contract==typeof(T)).OrderBy(e=>e.Metadata.Owner,StringComparer.Ordinal).Select(e=>new KeyValuePair<string,T>(e.Metadata.Owner,(T)e.Service)).ToArray());
        }
        public IReadOnlyList<CapabilityMetadata> Providers(string id){Validation.Id(id);return Array.AsReadOnly(entries.Where(e=>e.Metadata.Id==id).OrderBy(e=>e.Metadata.Owner,StringComparer.Ordinal).Select(e=>e.Metadata).ToArray());}
        internal IReadOnlyList<CapabilityMetadata> All()=>Array.AsReadOnly(entries.OrderBy(e=>e.Metadata.Id,StringComparer.Ordinal).ThenBy(e=>e.Metadata.Owner,StringComparer.Ordinal).Select(e=>e.Metadata).ToArray());
        /// <summary>True when any product provides this capability id (any version or type); no allocation.</summary>
        public bool Any(string id){if(id==null)return false;for(int i=0;i<entries.Count;i++)if(entries[i].Metadata.Id==id)return true;return false;}
        internal void Remove(ProductRegistration owner){if(entries.RemoveAll(e=>ReferenceEquals(e.Owner,owner))>0)Generation++;}
    }
}
