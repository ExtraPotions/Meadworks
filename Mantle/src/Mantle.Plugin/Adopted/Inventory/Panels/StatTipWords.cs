using Mantle.Core;

namespace Mantle.Panels
{
    /// <summary>The $mantle_tip_ words of the stat sheet's breakdowns (<see cref="StatTips"/>, <see cref="GearTips"/>).</summary>
    public static class StatTipWords
    {
        public static string Base { get; private set; }
        public static string Other { get; private set; }
        public static string Nothing { get; private set; }
        public static string Effect { get; private set; }
        public static string CarryWeight { get; private set; }
        public static string World { get; private set; }
        public static string Heaviest { get; private set; }
        public static string MissingHealth { get; private set; }
        public static string ParryArmour { get; private set; }
        public static string Skill { get; private set; }
        public static string Capped { get; private set; }

        public static void Register()
        {
            Base = Language.Add("mantle_tip_base", "Base");
            Other = Language.Add("mantle_tip_other", "Other effects");
            Nothing = Language.Add("mantle_tip_nothing", "Nothing you wear or carry gives this");
            Effect = Language.Add("mantle_tip_effect", "An effect");
            CarryWeight = "$se_max_carryweight"; // the game's own word: Max carry weight / Encombrement max / Carga máxima
            World = Language.Add("mantle_tip_world", "World modifier");
            Heaviest = Language.Add("mantle_tip_heaviest", "Heaviest");
            MissingHealth = Language.Add("mantle_tip_missinghealth", "Missing health");
            ParryArmour = Language.Add("mantle_tip_parryarmor", "Block armor on a parry");
            Skill = Language.Add("mantle_tip_skill", "Skill");
            Capped = Language.Add("mantle_tip_capped", "Over the cap: the game applies less than the sum");
        }
    }
}
