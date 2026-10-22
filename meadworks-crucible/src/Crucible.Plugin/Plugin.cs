using System;
using BepInEx;
using Jotunn.Managers;
using Jotunn.Utils;
namespace ExtraPotions.Meadworks.Crucible {
    [BepInPlugin(Guid,Name,Version)]
    [BepInDependency(Jotunn.Main.ModGuid,"2.30.2")]
    // VersionCheckOnly (server-only mode, docs/server-only-mode.md): Jotunn compares versions whenever both sides run
    // Crucible but no longer refuses a client without it. Under Network.AdmissionMode Strict the silent-peer refusal
    // (NativePeerJoin) refuses that client instead; AllowVanilla admits it when every installed product allows it.
    [NetworkCompatibility(CompatibilityLevel.VersionCheckOnly,VersionStrictness.Minor)]
    public sealed class Plugin:BaseUnityPlugin {
        public const string Guid="com.meadworks.crucible";
        public const string Name="ExtraPotions Meadworks: Crucible";
        public const string Version="0.3.27";
        public static ProductRegistry Services { get; private set; }=null!;
        public static bool IsReady { get; private set; }
        internal static BepInEx.Logging.ManualLogSource? Log { get; private set; }
        /// <summary>C04 feature flag (config Network.PeerClassHandshake), shown by the diagnostics command.</summary>
        public static bool PeerClassHandshake { get; private set; }
        public static SessionSnapshot Session=>Services.Sessions.Current;
        public static RequestRouter Requests=>Services.Requests;
        public static NativeRequestClient RequestClient{get;private set;}=null!;
        public static NativePeerCommandClient PeerCommandClient{get;private set;}=null!;
        /// <summary>C02 vanilla-server detection: Absent when the server sends no Crucible Hello within the timeout.</summary>
        public static ServerPresenceTimer ServerPresence{get;private set;}=new ServerPresenceTimer();
        ProductRegistration? registration;
        NativeSessionAdapter? sessions;NativeRequestTransport? transport;RequestDispatcher? dispatcher;
        NativePeerCommandTransport? peerCommands;
        MeadworksTranslations? blockedText;
        void Awake(){
            Log=Logger;
            SyncedConfigGuard.Install();
            MeadworksConfig.EnsureSchema(Config,1);
            MeadworksLogging.Bind(Config);
            MeadworksMischief.Bind(Config,Logger);
            MeadworksStepAside.Bind(Config);
            MeadworksSkills.Install(Config,Logger);
            MeadworksAchievementGuard.Install(Config,Logger);
            Services=new ProductRegistry();Services.Operations.RemoteProtocol=PeerProtocol.Version;
            MeadworksSettingTiers.Install(Services,Logger);
            registration=Services.Register(new ProductMetadata(Guid,Name,new System.Version(Version),new System.Version(0,1),new System.Version(0,1),MultiplayerPolicy.RequiredOnAllPeers));
            var helloTimeout=MeadworksConfig.Bind(Config,"Authority","VanillaServerTimeoutSeconds",10f,"Seconds a client waits for the server's Crucible Hello before treating the server as vanilla (cheats locked, required products off).",ConfigScope.Local,new BepInEx.Configuration.AcceptableValueRange<float>(2f,120f));
            ServerPresence=new ServerPresenceTimer(Math.Max(2f,Math.Min(120f,helloTimeout.Value)));
            helloTimeout.SettingChanged+=(_,_)=>ServerPresence.TimeoutSeconds=Math.Max(2f,Math.Min(120f,helloTimeout.Value));
            sessions=new NativeSessionAdapter(Services);dispatcher=new RequestDispatcher();transport=new NativeRequestTransport(Services,sessions,dispatcher,ServerPresence);RequestClient=new NativeRequestClient(Services,sessions,transport);
            PeerClassHandshake=transport.PeerClassHandshake=MeadworksConfig.Bind(Config,"Network","PeerClassHandshake",true,"Checks MeadWorks versions when a player joins: the server turns away clients missing a required MeadWorks mod or running a different major.minor version, and a client turns off required mods the server lacks. On by default; turn it off only to diagnose join problems. Restart the game after changing this setting.").Value;
            MeadworksAdmission.Bind(Config);MeadworksAdmission.Register(registration);MeadworksBuildHashes.Register(registration);
            if(PeerClassHandshake)NativePeerJoin.Install(Services);
            // Pre-spawn refusal of clients without Meadworks: on every game, whatever Network.PeerClassHandshake says.
            NativePeerJoin.InstallPeerInfoGate();
            Services.Requests.ValidateLiveAuthority=sessions.Validate;
            peerCommands=new NativePeerCommandTransport(Services,dispatcher);PeerCommandClient=new NativePeerCommandClient(Services,peerCommands);
            Services.PeerCommandRequests.ValidateLiveAuthority=peerCommands.Validate;
            CommandManager.Instance.AddConsoleCommand(new DiagnosticsCommand());
            try{blockedText=MeadworksTranslations.Register(Info.Metadata,BlockedOptionText.Default,log:Logger);}catch(Exception error){Logger.LogWarning("Blocked-option texts stay built-in; translation overrides unavailable: "+error.GetType().Name);}
            var built=AssemblyVersions.Of(typeof(Plugin).Assembly);if(built!=Version)Logger.LogWarning($"Crucible plugin constant {Version} differs from build version {built}.");
            IsReady=true;MeadworksWorldSettings.Install();Logger.LogInfo($"Crucible {Version} | ExtraPotions | expDARE | API {ProductRegistry.ApiVersion} | network {PeerProtocol.Version} ready");
        }
        void Update(){if(!IsReady)return;sessions!.Update();transport!.Update();NativePeerJoin.Update();peerCommands!.Update();dispatcher!.Update();RequestClient.Update();PeerCommandClient.Update();Services.Requests.Drain();Services.PeerCommandRequests.Drain();MeadworksLogging.Update();blockedText?.Update();MeadworksBlockedOptions.Update(UnityEngine.Time.unscaledTime,UnityEngine.Time.unscaledDeltaTime);MeadworksSkills.Update(UnityEngine.Time.deltaTime);MeadworksAchievementGuard.Update();}
        void OnDestroy(){IsReady=false;MeadworksSettingTiers.Uninstall();SyncedConfigGuard.Uninstall();MeadworksWorldSettings.Shutdown();NativePeerJoin.Uninstall();MeadworksSkills.Uninstall();MeadworksAchievementGuard.Uninstall();MeadworksBlockedOptions.Shutdown();blockedText?.Dispose();MeadworksLogging.Shutdown();PeerCommandClient?.Dispose();RequestClient?.Dispose();peerCommands?.Dispose();transport?.Dispose();dispatcher?.Dispose();sessions?.Dispose();Services?.Dispose();registration=null;}
    }
}
