using System;
using System.Linq;
namespace Wayfarer.Core {
    /// <summary>The local Language setting. The words themselves live in assets/localization/*.json (Crucible C05 catalog).</summary>
    public static class WayfarerLanguages {
        // Korean is appended so existing selector order is unchanged; the set equals Crucible's seven Meadworks languages.
        public static readonly string[] Languages={"English","Spanish","Chinese","Russian","French","German","Korean"};
        /// <summary>Values of the local General.Language setting, in selector order (Auto follows the game).</summary>
        public static string[] Settings=>new[]{"Auto"}.Concat(Languages).ToArray();
        public static string Normalize(string language) {
            if(language.IndexOf("Chinese",StringComparison.OrdinalIgnoreCase)>=0)return "Chinese";
            return Languages.FirstOrDefault(l=>string.Equals(l,language,StringComparison.OrdinalIgnoreCase))??"English";
        }
        /// <summary>Catalog key naming a selector value in the interface language, for example language_korean.</summary>
        public static string NameKey(string setting)=>"language_"+(string.Equals(setting,"Auto",StringComparison.OrdinalIgnoreCase)?"auto":Normalize(setting).ToLowerInvariant());
    }
}
