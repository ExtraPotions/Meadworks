using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    // Unity-facing consumers use these services on the main thread.
    public sealed class ProductRegistry:IDisposable {
        public static readonly Version ApiVersion=new Version(0,1);
        bool disposed;
        internal readonly Dictionary<string,ProductRegistration> Entries=new Dictionary<string,ProductRegistration>(StringComparer.Ordinal);
        public CapabilityRegistry Capabilities { get; }
        public SessionTracker Sessions { get; }=new SessionTracker();
        public OperationRegistry Operations { get; }=new OperationRegistry();
        public PeerCommandRegistry PeerCommands { get; }=new PeerCommandRegistry();
        public PeerCommandRouter PeerCommandRequests { get; }
        /// <summary>Source mods each product replaces; a product yields when one is installed (C09 step-aside).</summary>
        public StepAsideRegistry StepAside { get; }=new StepAsideRegistry();
        public RequestRouter Requests { get; }
        public ProductRegistry() { Capabilities=new CapabilityRegistry(this);var clock=System.Diagnostics.Stopwatch.StartNew();Requests=new RequestRouter(Operations,Sessions,()=>clock.Elapsed.TotalSeconds);PeerCommandRequests=new PeerCommandRouter(PeerCommands,Sessions,()=>clock.Elapsed.TotalSeconds); }
        public ProductRegistration Register(ProductMetadata metadata) {
            if(disposed)throw new ObjectDisposedException(nameof(ProductRegistry));
            if(metadata==null)throw new ArgumentNullException(nameof(metadata));
            if(ApiVersion<metadata.MinimumApi||ApiVersion>metadata.MaximumApi)throw new NotSupportedException("Consumer API interval excludes Crucible API "+ApiVersion+".");
            if(Entries.ContainsKey(metadata.Id))throw new InvalidOperationException("Product is already registered.");
            var registration=new ProductRegistration(this,metadata);Entries.Add(metadata.Id,registration);return registration;
        }
        /// <summary>O(1) lookup of a registered product's metadata; no snapshot, sort or allocation.</summary>
        public bool TryGetMetadata(string id,out ProductMetadata metadata){if(id!=null&&Entries.TryGetValue(id,out var registration)){metadata=registration.Metadata;return true;}metadata=null!;return false;}
        public IReadOnlyList<ProductMetadata> Snapshot()=>Array.AsReadOnly(Entries.Values.Select(r=>r.Metadata).OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray());
        public void Dispose(){if(disposed)return;disposed=true;Requests.Stop();PeerCommandRequests.Stop();foreach(var registration in Entries.Values.ToArray())registration.Dispose();Operations.Dispose();PeerCommands.Dispose();Sessions.Reset();}
        public DiagnosticSnapshot Diagnostics()=>new DiagnosticSnapshot(Snapshot(),Capabilities.All(),Array.AsReadOnly(Entries.Values.Where(r=>r.Ui!=null).OrderBy(r=>r.Metadata.Id,StringComparer.Ordinal).Select(r=>r.Ui!).ToArray()),Requests.Diagnostics());
        internal void Remove(ProductRegistration registration) { if(Entries.TryGetValue(registration.Metadata.Id,out var current)&&ReferenceEquals(current,registration)){Capabilities.Remove(registration);Entries.Remove(registration.Metadata.Id);} }
    }
    public sealed class ProductRegistration:IDisposable {
        readonly ProductRegistry registry;
        bool disposed;
        internal event Action? Disposed;
        public ProductMetadata Metadata { get; }
        public bool IsActive=>!disposed;
        public UiDescriptor? Ui { get; private set; }
        internal ProductRegistration(ProductRegistry registry,ProductMetadata metadata){this.registry=registry;Metadata=metadata;}
        internal void EnsureActive() { if(disposed||!registry.Entries.TryGetValue(Metadata.Id,out var active)||!ReferenceEquals(active,this))throw new ObjectDisposedException(Metadata.Id); }
        public void AddCapability<T>(string id,Version version,T service) where T:class { EnsureActive();registry.Capabilities.Add(this,id,version,service); }
        public void AddUi(UiDescriptor descriptor){EnsureActive();Ui=descriptor??throw new ArgumentNullException(nameof(descriptor));}
        public void Dispose(){if(disposed)return;disposed=true;registry.Remove(this);Ui=null;var callbacks=Disposed;Disposed=null;if(callbacks!=null)foreach(Action callback in callbacks.GetInvocationList())try{callback();}catch{ }}
    }
}
