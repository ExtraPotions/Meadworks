using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using Jotunn.Entities;
namespace ExtraPotions.Meadworks.Crucible {
    public sealed class DiagnosticsCommand:ConsoleCommand {
        public override string Name=>"meadworks";
        public override string Help=>"Meadworks diagnostics (read-only); 'meadworks log <level> [product]' and 'meadworks log filter ...' set logging.";
        public override void Run(string[] args){
            if(args!=null&&args.Length>0&&string.Equals(args[0],"log",StringComparison.OrdinalIgnoreCase)){
                foreach(var line in MeadworksLogging.Command(args.Skip(1).ToArray()))global::Console.instance?.Print(line);
                return;
            }
            if(!Plugin.IsReady){global::Console.instance?.Print("Crucible is unavailable.");return;}
            var snapshot=Plugin.Services.Diagnostics();var session=Plugin.Session;var presence=Plugin.ServerPresence;
            global::Console.instance?.Print(DiagnosticsText.Header(snapshot,PeerProtocol.Version.ToString())+" | plugin "+Plugin.Version);
            global::Console.instance?.Print(DiagnosticsText.Authority(session.Role,session.AuthorityReady,session.IsAdmin,MeadworksAuthority.Server,presence.Waiting,presence.TimeoutSeconds)+" | peer-class handshake "+(Plugin.PeerClassHandshake?"on":"off"));
            if(ZNet.instance!=null&&ZNet.instance.IsServer()){var admission=MeadworksAdmission.Current();global::Console.instance?.Print(admission.Summary+" | peers: "+MeadworksAdmission.Peers.Describe());}
            global::Console.instance?.Print(snapshot.Requests.ToString());
            global::Console.instance?.Print(MeadworksMischief.Diagnostics());
            foreach(var p in snapshot.Products){
                global::Console.instance?.Print(DiagnosticsText.Product(p,MeadworksAuthority.Facts(p.Id)));
                var aside=MeadworksStepAside.Decide(p.Id);
                if(aside.Yield)global::Console.instance?.Print("  steps aside: "+string.Join(", ",aside.Present.Select(s=>s.Name)));
                foreach(var module in Plugin.Services.StepAside.Modules(p.Id))global::Console.instance?.Print("  module "+module.ModuleId+" steps aside: "+module.Source.Name);
                if(Plugin.Services.Capabilities.TryResolve<Func<IReadOnlyList<string>>>("meadworks.diagnostics."+p.Id,new System.Version(1,0),out var describe)){
                    try{foreach(var line in describe!().Take(64))global::Console.instance?.Print("  "+line);}
                    catch(Exception error){global::Console.instance?.Print("  product diagnostics unavailable: "+error.GetType().Name);}
                }
            }
            global::Console.instance?.Print("log level "+MeadworksLogging.Policy.Level+(MeadworksLogging.Policy.Overrides.Count>0?" | overrides "+MeadworksLogging.Policy.FormatOverrides():"")+" | console filter "+(MeadworksLogging.ConsoleFilter.Enabled?"on":"off"));
            foreach(var line in MeadworksBuildHashes.Describe())global::Console.instance?.Print(line);
            foreach(var c in snapshot.Capabilities)global::Console.instance?.Print(c.Id+" "+c.Version+" | "+c.Owner);
            foreach(var dependency in Chainloader.PluginInfos.Values.OrderBy(p=>p.Metadata.GUID,StringComparer.Ordinal))global::Console.instance?.Print("loaded: "+dependency.Metadata.GUID+" "+dependency.Metadata.Version);
        }
    }
}
