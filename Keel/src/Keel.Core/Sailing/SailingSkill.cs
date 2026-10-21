// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Sailing skill's CS1 identity and Keel's network names (design B1, 3, 9.3). The GrindstoneSkills
// names are facts used only to read and import old data.
namespace ExtraPotions.Keel.Sailing
{
    /// <summary>
    /// Sailing registers through Crucible's skill registry CS1 under the product-independent identity Crucible's
    /// GrindstoneSkillsMap assigns to Keel, and imports the GrindstoneSkills level once per character.
    /// </summary>
    public static class SailingSkill
    {
        public const string Name = "Sailing";
        public const string Identity = "meadworks_sailing";

        /// <summary>The game's <c>Skills.SkillType</c> number of Sailing (pinned; checked against CS1 at run time).</summary>
        public const int GameType = 1690349047;

        public const string ImportFrom = "grindstone_sailing";

        public static int ImportType => SkillHash.CustomType(ImportFrom);

        /// <summary>CS1 default publish key on the player ZDO (identity plus <c>_level</c>).</summary>
        public const string PublishKey = Identity + "_level";

        /// <summary>The level key GrindstoneSkills publishes, read only while it runs or for import.</summary>
        public const string SourcePublishKey = "grindstone_sailing_level";

        /// <summary>C05 key of the skill name (CS1 <c>textKey</c>); the description key appends <c>_description</c>.</summary>
        public const string TextKey = "skill_sailing";

        public const string Description = "Health of the ships you build, speed of the ships you steer, and how far you explore at sea.";

        /// <summary>The Sailing icon: GrindstoneSkills' <c>skill_sailing.png</c> (AI-generated, GPL-3.0-or-later), adopted
        /// unchanged under the ValheimMods notice (user decision 2026-10-08). Repository path of the exact upstream bytes.</summary>
        public const string IconFile = "assets/skill_sailing.png";

        /// <summary>Logical name of the embedded icon in Keel.dll.</summary>
        public const string IconResource = "Keel.skill_sailing.png";
    }

    /// <summary>ZDO keys and RPC names (design section 3); GrindstoneSkills' keys are read-only aliases.</summary>
    public static class SailingKeys
    {
        /// <summary>float on a ship's ZDO: the Sailing level of its builder, written once when it is placed.</summary>
        public const string ShipwrightLevel = KeelCore.ZdoPrefix + "shipwright";

        /// <summary>Vector3 on a ship's ZDO, written by its owner: the flat direction the called wind blows toward.</summary>
        public const string WindCallDirection = KeelCore.ZdoPrefix + "wind_dir";

        /// <summary>long on a ship's ZDO, written by its owner: world time (ticks) when the called wind ends.</summary>
        public const string WindCallUntil = KeelCore.ZdoPrefix + "wind_until";

        public const string RpcWindCall = "keel_WindCall";
        public const string RpcLookout = "keel_Lookout";

        /// <summary>GrindstoneSkills' shipwright key, read when <see cref="ShipwrightLevel"/> is absent (no write).</summary>
        public const string SourceShipwrightLevel = "grindstone_shipwright";
    }
}