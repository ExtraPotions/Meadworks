using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    public enum FontKind { Game, OperatingSystem, Builtin }
    public readonly struct FontChoice {
        public FontKind Kind { get; }
        /// <summary>Game font search name, installed OS family name (as installed), or builtin resource name.</summary>
        public string Name { get; }
        public FontChoice(FontKind kind,string name){Kind=kind;Name=name;}
        public override string ToString()=>Kind+":"+Name;
    }

    /// <summary>
    /// Pure font selection per language. Linux families (Noto, DejaVu, Liberation, WenQuanYi, Nanum) are preferred
    /// so the same choice works on Linux clients, then Windows names. The game font only covers Latin scripts.
    /// </summary>
    public static class FontResolver {
        /// <summary>Substring of Valheim's bundled Latin UI font name.</summary>
        public const string GameFont="AveriaSerifLibre";
        /// <summary>Unity builtin fallback resource.</summary>
        public const string BuiltinFont="LegacyRuntime.ttf";
        static readonly string[] Latin={"Noto Sans","DejaVu Sans","Liberation Sans","Arial","Segoe UI"};
        static readonly string[] Korean={"Noto Sans CJK KR","Noto Sans KR","NanumGothic","Malgun Gothic"};
        static readonly string[] Chinese={"Noto Sans CJK SC","Noto Sans SC","WenQuanYi Micro Hei","WenQuanYi Zen Hei","Microsoft YaHei UI","Microsoft YaHei"};

        public static bool UsesGameFont(string? language){var canonical=MeadworksLanguages.Normalize(language);return canonical=="English"||canonical=="Spanish"||canonical=="French"||canonical=="German";}
        public static IReadOnlyList<string> Preferences(string? language){
            switch(MeadworksLanguages.Normalize(language)){
                case "Korean":return Array.AsReadOnly(Korean);
                case "Chinese":return Array.AsReadOnly(Chinese);
                default:return Array.AsReadOnly(Latin);
            }
        }
        public static FontChoice Select(string? language,IEnumerable<string> installedFonts,bool gameFontAvailable){
            if(gameFontAvailable&&UsesGameFont(language))return new FontChoice(FontKind.Game,GameFont);
            var installed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            if(installedFonts!=null)foreach(var name in installedFonts)if(!string.IsNullOrEmpty(name)&&!installed.ContainsKey(name))installed.Add(name,name);
            foreach(var preferred in Preferences(language))if(installed.TryGetValue(preferred,out var actual))return new FontChoice(FontKind.OperatingSystem,actual);
            return new FontChoice(FontKind.Builtin,BuiltinFont);
        }
    }
}
