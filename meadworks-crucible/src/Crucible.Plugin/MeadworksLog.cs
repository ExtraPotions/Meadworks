using BepInEx.Logging;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// A product's log, filtered by the suite-wide level and its per-product override (<see cref="MeadworksLogging"/>)
    /// with repeat suppression. Quiet by default: errors and warnings only.
    /// </summary>
    public sealed class MeadworksLog {
        readonly ManualLogSource logger;readonly string prefix;
        public string Product { get; }
        MeadworksLog(ManualLogSource logger,string product){this.logger=logger;Product=product;prefix="[Meadworks:"+product+"] ";}
        public static MeadworksLog For(ManualLogSource logger,string product)=>new MeadworksLog(logger,product);
        public bool IsEnabled(LogSeverity severity)=>MeadworksLogging.Enabled(Product,severity);
        /// <summary>Normal and above.</summary>
        public void Info(string message)=>MeadworksLogging.Write(logger,Product,prefix,LogSeverity.Info,message);
        /// <summary>Always written (repeats suppressed).</summary>
        public void Warning(string message)=>MeadworksLogging.Write(logger,Product,prefix,LogSeverity.Warning,message);
        /// <summary>Always written (repeats suppressed).</summary>
        public void Error(object error)=>MeadworksLogging.Write(logger,Product,prefix,LogSeverity.Error,error?.ToString()??"");
        /// <summary>Verbose and above.</summary>
        public void Verbose(string message)=>MeadworksLogging.Write(logger,Product,prefix,LogSeverity.Verbose,message);
        /// <summary>Debug only.</summary>
        public void Debug(string message)=>MeadworksLogging.Write(logger,Product,prefix,LogSeverity.Debug,message);
    }
}
