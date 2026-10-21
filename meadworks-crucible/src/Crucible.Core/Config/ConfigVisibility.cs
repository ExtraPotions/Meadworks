using System;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Whether configuration UIs list a C01 entry.</summary>
    public enum ConfigVisibility {
        /// <summary>Listed by configuration UIs (the default for every player-facing setting).</summary>
        Shown,
        /// <summary>A product-managed entry (synchronized carrier, import marker) that configuration UIs do not list.</summary>
        Hidden,
    }

    /// <summary>
    /// The C01 hidden bind convention. Hidden entries carry a tag object named <c>ConfigurationManagerAttributes</c> with
    /// <c>Browsable = false</c> and <c>ReadOnly = true</c>: BepInEx ConfigurationManager skips non-browsable entries, and
    /// Almanac reads the same tag by type short name and member name (<c>AttributeReader.ReadManager</c>), so
    /// <c>Presentation.Decide</c> returns <c>HiddenNotBrowsable</c> and its settings window, profiles, bulk reset and
    /// session undo skip the entry. Hidden entries keep full C01 ownership metadata. Cheats are never hidden, because a
    /// hidden cheat would escape the C02/C13 cheat review.
    /// </summary>
    public static class ConfigVisibilityRules {
        /// <summary>The <c>Browsable</c> attribute value: false for hidden entries, unset (manager default: listed) otherwise.</summary>
        public static bool? Browsable(ConfigVisibility visibility)=>visibility==ConfigVisibility.Hidden?false:(bool?)null;

        /// <summary>The <c>ReadOnly</c> attribute value: a hidden entry is product-managed, so managers never edit it.</summary>
        public static bool? ReadOnly(ConfigVisibility visibility)=>visibility==ConfigVisibility.Hidden?true:(bool?)null;

        public static bool Allowed(ConfigScope scope,ConfigVisibility visibility){
            if(!Enum.IsDefined(typeof(ConfigScope),scope)||!Enum.IsDefined(typeof(ConfigVisibility),visibility))return false;
            return visibility==ConfigVisibility.Shown||scope!=ConfigScope.Cheat;
        }

        public static void Require(ConfigScope scope,ConfigVisibility visibility){
            if(!Enum.IsDefined(typeof(ConfigScope),scope))throw new ArgumentException("Unknown configuration scope.",nameof(scope));
            if(!Enum.IsDefined(typeof(ConfigVisibility),visibility))throw new ArgumentException("Unknown configuration visibility.",nameof(visibility));
            if(!Allowed(scope,visibility))throw new ArgumentException("A cheat cannot be bound hidden; cheats stay visible for review.",nameof(visibility));
        }
    }
}
