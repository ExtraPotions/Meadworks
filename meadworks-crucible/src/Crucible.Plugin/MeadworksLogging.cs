using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// C06 logging service: suite-wide verbosity with per-product overrides, repeat suppression for every
    /// <see cref="MeadworksLog"/>, the <c>meadworks log</c> console verb and the optional per-mod console filter for all
    /// BepInEx sources. Settings live in Crucible's config (Local scope). Lines are written outside the service lock,
    /// so BepInEx listeners never run while it is held.
    /// </summary>
    public static partial class MeadworksLogging {
        sealed class Line {internal readonly ManualLogSource Logger;internal readonly LogSeverity Severity;internal readonly string Text;internal Line(ManualLogSource logger,LogSeverity severity,string text){Logger=logger;Severity=severity;Text=text;}}
        sealed class Origin {internal readonly ManualLogSource Logger;internal readonly string Prefix;internal readonly LogSeverity Severity;internal Origin(ManualLogSource logger,string prefix,LogSeverity severity){Logger=logger;Prefix=prefix;Severity=severity;}}

        static readonly object gate=new object();
        static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        static readonly List<ILogListener> installed=new List<ILogListener>();
        static long hiddenNoise;static bool stopped;
        static RepeatSuppressor repeats=new RepeatSuppressor();
        static ConfigEntry<LogVerbosity>? level;static ConfigEntry<string>? overrides,filterRules;static ConfigEntry<bool>? filterEnabled,filterDisk,showAll;static ConfigEntry<float>? window;
        static bool persisting;static double nextFlush;

        public static LogPolicy Policy { get; }=new LogPolicy();
        public static ConsoleFilter ConsoleFilter { get; }=new ConsoleFilter();

        internal static void Bind(ConfigFile config){
            stopped=false;StartCounting();
            level=MeadworksConfig.Bind(config,"Logging","Level",LogVerbosity.Quiet,"MeadWorks log level: Quiet (errors and warnings), Normal, Verbose or Debug. Also: console 'meadworks log <level> [product]'. Crucible owns this local installation setting.");
            overrides=MeadworksConfig.Bind(config,"Logging","ProductOverrides","","Per-product levels, for example 'Wildroot=Debug,Burrow=Normal'. Crucible owns this local installation setting.");
            window=MeadworksConfig.Bind(config,"Logging","RepeatWindowSeconds",60f,new ConfigDescription("Identical lines inside this window are written once and counted; 0 writes every line. Crucible owns this local installation setting.",new AcceptableValueRange<float>(0f,3600f)));
            filterEnabled=MeadworksConfig.Bind(config,"Logging","ConsoleFilterEnabled",false,"Filter the BepInEx console for all installed mods by the rules below. Crucible owns this local installation setting.");
            filterRules=MeadworksConfig.Bind(config,"Logging","ConsoleFilterRules","","Per-source console levels: 'Source=All|Info|Warning|Error|None', separated by ';'. '*' sets the level for unlisted sources. Crucible owns this local installation setting.");
            filterDisk=MeadworksConfig.Bind(config,"Logging","ConsoleFilterAppliesToDiskLog",false,"Also apply the console filter rules to BepInEx's LogOutput.log. Crucible owns this local installation setting.");
            showAll=MeadworksConfig.Bind(config,"Logging","ShowAllConsoleMessages",false,"Show every message on the BepInEx console. By default Crucible hides a short, exact list of known-harmless vanilla and Unity messages (world-generation hints, missing audio or video shaders on a dedicated server and similar); LogOutput.log always keeps everything. Crucible owns this local installation setting.");
            config.SettingChanged+=(_,args)=>{if(!persisting&&args.ChangedSetting.Definition.Section=="Logging")Load();};
            Load();
            if(!showAll.Value&&Plugin.Log!=null)Plugin.Log.LogInfo("Crucible: hiding "+KnownConsoleNoise.Embedded.Rules.Count+" known-harmless vanilla console messages (full log in LogOutput.log)");
        }

        /// <summary>Console lines hidden as known-harmless vanilla messages this session.</summary>
        public static long HiddenNoiseCount=>System.Threading.Interlocked.Read(ref hiddenNoise);

        static void Load(){
            List<string> rejected;
            lock(gate){
                Policy.Level=Enum.IsDefined(typeof(LogVerbosity),level!.Value)?level.Value:LogVerbosity.Quiet;
                rejected=Policy.LoadOverrides(overrides!.Value).ToList();
                rejected.AddRange(ConsoleFilter.Load(filterRules!.Value));
                ConsoleFilter.Enabled=filterEnabled!.Value;
                var seconds=Math.Max(0f,Math.Min(3600f,window!.Value));
                if(repeats.WindowSeconds!=seconds)repeats=new RepeatSuppressor(seconds);
            }
            ApplyFilter();
            if(rejected.Count>0&&Plugin.Log!=null)Plugin.Log.LogWarning("[Meadworks:Crucible] Ignored invalid logging entries: "+string.Join(", ",rejected));
        }

        public static bool Enabled(string product,LogSeverity severity){lock(gate)return Policy.Allows(product,severity);}

        internal static void Write(ManualLogSource logger,string product,string prefix,LogSeverity severity,string message){
            var lines=new List<Line>(2);
            lock(gate){
                if(!Policy.Allows(product,severity))return;
                var verdict=repeats.Admit(product+"|"+severity+"|"+message,clock.Elapsed.TotalSeconds,new Origin(logger,prefix,severity));
                if(!verdict.Emit)return;
                if(verdict.SuppressedBefore>0)lines.Add(new Line(logger,severity,prefix+RepeatSuppressor.Summary(verdict.SuppressedBefore,repeats.WindowSeconds)));
                lines.Add(new Line(logger,severity,prefix+message));
            }
            Emit(lines);
        }

        /// <summary>Writes the counts of windows that ended without a further identical line. Called from Plugin.Update.</summary>
        internal static void Update(){
            var now=clock.Elapsed.TotalSeconds;if(now<nextFlush)return;nextFlush=now+1;
            var lines=new List<Line>();
            lock(gate)foreach(var summary in repeats.Flush(now))if(summary.Tag is Origin origin)lines.Add(new Line(origin.Logger,origin.Severity,origin.Prefix+RepeatSuppressor.Summary(summary.Count,repeats.WindowSeconds)));
            Emit(lines);
        }

        static void Emit(List<Line> lines){
            foreach(var line in lines)switch(line.Severity){
                case LogSeverity.Error:line.Logger.LogError(line.Text);break;
                case LogSeverity.Warning:line.Logger.LogWarning(line.Text);break;
                case LogSeverity.Debug:line.Logger.LogDebug(line.Text);break;
                default:line.Logger.LogInfo(line.Text);break;
            }
        }

        /// <summary>The <c>meadworks log ...</c> verb; arguments exclude "log". Changes are saved to Crucible's config.</summary>
        internal static IReadOnlyList<string> Command(IReadOnlyList<string> args){
            LogCommandResult result;
            lock(gate)result=LogCommand.Run(Policy,ConsoleFilter,args);
            if(result.Changed){Persist();ApplyFilter();}
            return result.Lines;
        }

        static void Persist(){
            if(level==null)return;
            persisting=true;
            try{lock(gate){level.Value=Policy.Level;overrides!.Value=Policy.FormatOverrides();filterEnabled!.Value=ConsoleFilter.Enabled;filterRules!.Value=ConsoleFilter.Format();}}
            finally{persisting=false;}
        }

        /// <summary>
        /// Wraps (or unwraps) BepInEx's console listener in the known-noise filter (unless ShowAllConsoleMessages) and, when the
        /// per-mod console filter is on, in that filter too; the disk listener is wrapped only by the per-mod filter when configured.
        /// </summary>
        static void ApplyFilter(){
            bool enabled,disk=filterDisk?.Value??false,noise=!(showAll?.Value??false)&&!stopped;lock(gate)enabled=ConsoleFilter.Enabled;
            var listeners=BepInEx.Logging.Logger.Listeners;
            foreach(var wrapper in installed){listeners.Remove(wrapper);listeners.Add(Unwrap(wrapper));}
            installed.Clear();
            if(!enabled&&!noise)return;
            foreach(var listener in listeners.ToArray()){
                bool console=listener is ConsoleLogListener;
                if(!console&&!(enabled&&disk&&listener is DiskLogListener))continue;
                ILogListener wrapped=listener;
                if(console&&noise)wrapped=new NoiseListener(wrapped);
                if(enabled)wrapped=new FilteringListener(wrapped);
                listeners.Remove(listener);listeners.Add(wrapped);installed.Add(wrapped);
            }
        }

        static ILogListener Unwrap(ILogListener listener){
            while(true){
                if(listener is FilteringListener f)listener=f.Inner;
                else if(listener is NoiseListener n)listener=n.Inner;
                else return listener;
            }
        }

        internal static void Shutdown(){stopped=true;lock(gate)ConsoleFilter.Enabled=false;ApplyFilter();StopCounting();}

        static LogSeverity Map(LogLevel level){
            if((level&(LogLevel.Fatal|LogLevel.Error))!=0)return LogSeverity.Error;
            if((level&LogLevel.Warning)!=0)return LogSeverity.Warning;
            if((level&(LogLevel.Message|LogLevel.Info))!=0)return LogSeverity.Info;
            return LogSeverity.Debug;
        }

        /// <summary>Drops the exact known-harmless vanilla messages from the console only; the disk listener never sees this wrapper.</summary>
        sealed class NoiseListener:ILogListener {
            internal readonly ILogListener Inner;
            internal NoiseListener(ILogListener inner){Inner=inner;}
            public void LogEvent(object sender,LogEventArgs eventArgs){
                if(KnownConsoleNoise.Embedded.IsKnown(eventArgs.Source?.SourceName,NoiseLevel(eventArgs.Level),eventArgs.Data as string)){System.Threading.Interlocked.Increment(ref hiddenNoise);return;}
                Inner.LogEvent(sender,eventArgs);
            }
            public void Dispose()=>Inner.Dispose();
        }

        static NoiseSeverity NoiseLevel(LogLevel level){
            if((level&LogLevel.Fatal)!=0)return NoiseSeverity.Fatal;
            if((level&LogLevel.Error)!=0)return NoiseSeverity.Error;
            if((level&LogLevel.Warning)!=0)return NoiseSeverity.Warning;
            return NoiseSeverity.Info;
        }

        sealed class FilteringListener:ILogListener {
            internal readonly ILogListener Inner;
            internal FilteringListener(ILogListener inner){Inner=inner;}
            public void LogEvent(object sender,LogEventArgs eventArgs){
                bool allowed;lock(gate)allowed=ConsoleFilter.Allows(eventArgs.Source?.SourceName,Map(eventArgs.Level));
                if(allowed)Inner.LogEvent(sender,eventArgs);
            }
            public void Dispose()=>Inner.Dispose();
        }
    }
}
