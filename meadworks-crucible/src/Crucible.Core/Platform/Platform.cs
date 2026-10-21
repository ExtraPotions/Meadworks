using System;
using System.Runtime.InteropServices;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Whether this process can show UI. A dedicated server (Windows or Linux) is headless.</summary>
    public enum HostSurface { Graphical, Headless }

    /// <summary>Where a player-facing notice goes.</summary>
    public enum NoticeChannel { Hud, Log }

    /// <summary>
    /// Pure headless decisions (C10). The plugin feeds the Unity facts: Jotunn's <c>GUIManager.IsHeadless()</c> is a
    /// null graphics device; batch mode is also headless. UI, input and HUD messages only run on a graphical host.
    /// </summary>
    public static class HeadlessGuard {
        public static HostSurface Detect(bool nullGraphicsDevice,bool batchMode)=>nullGraphicsDevice||batchMode?HostSurface.Headless:HostSurface.Graphical;
        public static bool AllowsUi(HostSurface surface)=>Known(surface)==HostSurface.Graphical;
        public static bool AllowsInput(HostSurface surface)=>Known(surface)==HostSurface.Graphical;
        /// <summary>HUD only on a graphical host whose HUD exists; otherwise the log.</summary>
        public static NoticeChannel Channel(HostSurface surface,bool hudReady)=>AllowsUi(surface)&&hudReady?NoticeChannel.Hud:NoticeChannel.Log;
        public static string Refusal(string feature){
            if(string.IsNullOrWhiteSpace(feature))throw new ArgumentException("Expected a feature name.",nameof(feature));
            return feature+" needs a graphical client; this process is headless.";
        }
        static HostSurface Known(HostSurface surface)=>Enum.IsDefined(typeof(HostSurface),surface)?surface:throw new ArgumentException("Unknown host surface.",nameof(surface));
    }

    /// <summary>File-system rules of the platform a path belongs to. Windows and Linux are the Meadworks targets.</summary>
    public enum PathPlatform { Windows, Unix }

    /// <summary>
    /// Path text rules that hold on Windows and Linux (C10). Windows accepts both separators and ignores case; Linux
    /// only splits on '/', treats '\' as a file-name character and compares case-sensitively. Callers pass full paths
    /// (<c>Path.GetFullPath</c>); these helpers never touch the disk.
    /// </summary>
    public static class PortablePaths {
        public static PathPlatform Current { get; }=RuntimeInformation.IsOSPlatform(OSPlatform.Windows)?PathPlatform.Windows:PathPlatform.Unix;

        public static bool IgnoresCase(PathPlatform platform)=>Known(platform)==PathPlatform.Windows;
        public static StringComparison Comparison(PathPlatform platform)=>IgnoresCase(platform)?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        public static StringComparison Comparison()=>Comparison(Current);

        /// <summary>Removes trailing separators but keeps a root ("/", "C:\").</summary>
        public static string TrimEnd(string? path,PathPlatform platform){
            if(string.IsNullOrEmpty(path))return "";
            var p=path!;int root=RootLength(p,platform);int end=p.Length;
            while(end>root&&IsSeparator(p[end-1],platform))end--;
            if(end==0)return p.Substring(0,1);
            return p.Substring(0,end);
        }
        public static string TrimEnd(string? path)=>TrimEnd(path,Current);

        /// <summary>Same location after trimming trailing separators; on Windows '/' equals '\' and case is ignored.</summary>
        public static bool Same(string? a,string? b,PathPlatform platform){
            if(string.IsNullOrEmpty(a)||string.IsNullOrEmpty(b))return false;
            return string.Equals(Canonical(a!,platform),Canonical(b!,platform),Comparison(platform));
        }
        public static bool Same(string? a,string? b)=>Same(a,b,Current);

        /// <summary>Last path segment, ignoring a trailing separator; empty for roots and missing paths.</summary>
        public static string Leaf(string? path,PathPlatform platform){
            var trimmed=TrimEnd(path,platform);
            if(trimmed.Length==0||trimmed.Length<=RootLength(trimmed,platform))return "";
            for(int i=trimmed.Length-1;i>=0;i--)if(IsSeparator(trimmed[i],platform))return trimmed.Substring(i+1);
            return trimmed;
        }
        public static string Leaf(string? path)=>Leaf(path,Current);

        public static bool LeafIs(string? path,string name,PathPlatform platform)=>!string.IsNullOrEmpty(name)&&string.Equals(Leaf(path,platform),name,Comparison(platform));
        public static bool LeafIs(string? path,string name)=>LeafIs(path,name,Current);

        static string Canonical(string path,PathPlatform platform){
            var trimmed=TrimEnd(path,platform);
            return platform==PathPlatform.Windows?trimmed.Replace('/','\\'):trimmed;
        }
        static bool IsSeparator(char c,PathPlatform platform)=>c=='/'||(platform==PathPlatform.Windows&&c=='\\');
        static int RootLength(string p,PathPlatform platform){
            if(Known(platform)==PathPlatform.Unix)return p.Length>0&&p[0]=='/'?1:0;
            if(p.Length>=3&&char.IsLetter(p[0])&&p[1]==':'&&IsSeparator(p[2],platform))return 3;
            if(p.Length>=2&&char.IsLetter(p[0])&&p[1]==':')return 2;
            return p.Length>0&&IsSeparator(p[0],platform)&&!(p.Length>1&&IsSeparator(p[1],platform))?1:0;
        }
        static PathPlatform Known(PathPlatform platform)=>Enum.IsDefined(typeof(PathPlatform),platform)?platform:throw new ArgumentException("Unknown path platform.",nameof(platform));
    }
}
