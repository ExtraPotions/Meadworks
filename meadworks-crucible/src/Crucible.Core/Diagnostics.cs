using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
namespace ExtraPotions.Meadworks.Crucible {
    public sealed class DiagnosticSnapshot {
        /// <summary>From the Core assembly's build metadata (Directory.Build.props Version), never hard-coded.</summary>
        public string CrucibleVersion=>AssemblyVersions.Of(typeof(DiagnosticSnapshot).Assembly);
        public string ApiVersion=>ProductRegistry.ApiVersion.ToString();
        public string RemoteCapabilities=>"unknown";
        public IReadOnlyList<ProductMetadata> Products { get; }
        public IReadOnlyList<CapabilityMetadata> Capabilities { get; }
        public IReadOnlyList<UiDescriptor> Ui { get; }
        public RequestDiagnostics Requests { get; }
        internal DiagnosticSnapshot(IReadOnlyList<ProductMetadata> products,IReadOnlyList<CapabilityMetadata> capabilities,IReadOnlyList<UiDescriptor> ui,RequestDiagnostics requests){Products=products;Capabilities=capabilities;Ui=ui;Requests=requests;}
    }

    public static class AssemblyVersions {
        /// <summary>The informational version without "+commit" build metadata, else the assembly version as major.minor.build.</summary>
        public static string Of(Assembly assembly){
            if(assembly==null)throw new ArgumentNullException(nameof(assembly));
            var informational=assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if(!string.IsNullOrWhiteSpace(informational)){var cut=informational!.IndexOf('+');return cut>=0?informational.Substring(0,cut):informational;}
            var version=assembly.GetName().Version;
            return version==null?"unknown":version.ToString(Math.Max(2,Math.Min(3,version.Build<0?2:3)));
        }
    }

    /// <summary>Line formats of the read-only <c>meadworks</c> console command (C11).</summary>
    public static class DiagnosticsText {
        public static string Header(DiagnosticSnapshot snapshot,string network){
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            return "Crucible "+snapshot.CrucibleVersion+" | API "+snapshot.ApiVersion+" | network "+network+" | capability format "+CapabilityCodec.CurrentFormat.ToString(CultureInfo.InvariantCulture);
        }
        public static string Authority(SessionRole role,bool authorityReady,bool isAdmin,ServerCrucible server,bool waiting,double timeoutSeconds)=>
            "Authority: "+role+" | server Crucible "+server+(waiting?" (waiting up to "+timeoutSeconds.ToString("0.##",CultureInfo.InvariantCulture)+" s for Hello)":"")+" | authority ready "+authorityReady+" | admin "+isAdmin;
        /// <summary>Peer class and gate state of one product: activity, then the decision for server settings and for cheats.</summary>
        public static string Product(ProductMetadata product,AuthorityFacts facts){
            if(product==null)throw new ArgumentNullException(nameof(product));
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            var active=AuthorityGate.ProductActive(facts,out var reason);
            return product.Id+" "+product.Version+" | "+product.Multiplayer+" | peer class "+facts.PeerClass+" | server copy "+facts.ServerProduct+" | "+(active?"active":"off: "+reason)
                +" | settings "+State(AuthorityGate.Decide(ConfigScope.ServerAuthoritative,facts))+" | cheats "+State(AuthorityGate.Decide(ConfigScope.Cheat,facts));
        }
        static string State(AuthorityDecision decision)=>(decision.Allowed?"Allowed":"Blocked:"+decision.Reason)+(decision.Source==ConfigValueSource.Locked?" (safe value)":"");
    }
}
