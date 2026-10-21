// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>Where a skeleton's blow puts its damage: all of it in one kind, or half slash and half pierce (the dagger).</summary>
    public enum Blow
    {
        Slash,
        Pierce,
        Blunt,
        SlashPierce,
    }

    /// <summary>
    /// How an arsenal skeleton swings its melee weapon: one blow, never a combo. The Skeleton controller's state it
    /// plays ("attack", or "attack_mace" the poison skeleton's mace uses), the player clip that takes the place of the
    /// Skeleton's own sword swing in that state (null: the Skeleton's own), how the damage lands against the damage of
    /// the sword of the skeleton it replaces (<see cref="Factor"/>), how far and wide it reaches, and the seconds
    /// between blows at the least.
    /// </summary>
    public sealed class Swing
    {
        public readonly string Animation;
        public readonly string? Clip;
        public readonly Blow Blow;
        public readonly float Factor, Reach, Angle, Interval;

        public Swing(string animation, string? clip, Blow blow, float factor, float reach, float angle, float interval)
        {
            (Animation, Clip, Blow, Factor, Reach, Angle, Interval) = (animation, clip, blow, factor, reach, angle, interval);
        }
    }

    /// <summary>
    /// One of the seven weapons of the skeleton arsenal (AssetWorkshop assets/ecp_skel_*: bone, each with vertebrae worked
    /// in), the skeleton that carries it and the players' item made after it. The creature is a copy of one of the
    /// game's skeletons (<see cref="ArsenalKind"/>); the players' item is a copy of the bronze-age weapon of its class
    /// (<see cref="GameItem"/>) wearing the bone model, a little weaker (<see cref="ArsenalItems"/>). The bow
    /// has no <see cref="Swing"/>: it shoots as the skeleton archer does.
    /// </summary>
    public sealed class ArsenalWeapon
    {
        public static readonly ArsenalWeapon Bow = new ArsenalWeapon("Bow", "Bowman", "BowFineWood", null,
            "ECP_Spine:1, BoneFragments:12:6");

        public static readonly ArsenalWeapon[] All =
        {
            new ArsenalWeapon("Dagger", "Cutthroat", "KnifeCopper",
                new Swing("attack", "knife_slash0", Blow.SlashPierce, 0.8f, 1.5f, 60f, 2f), "BoneFragments:8:4"),
            new ArsenalWeapon("Sword", "Swordsman", "SwordBronze",
                new Swing("attack", null, Blow.Slash, 1f, 1.8f, 90f, 3f), "ECP_Spine:1, BoneFragments:10:5"),
            new ArsenalWeapon("Axe", "Axeman", "AxeBronze",
                new Swing("attack", "axe_swing", Blow.Slash, 1.1f, 1.9f, 90f, 3f), "ECP_Spine:1, BoneFragments:10:5"),
            new ArsenalWeapon("Mace", "Bonebreaker", "MaceBronze",
                new Swing("attack_mace", null, Blow.Blunt, 1.15f, 2.2f, 70f, 3.5f), "ECP_Spine:1, BoneFragments:12:6"),
            new ArsenalWeapon("Spear", "Spearman", "SpearBronze",
                new Swing("attack", "Javelin Stab", Blow.Pierce, 1f, 2.3f, 40f, 3f), "ECP_Spine:1, BoneFragments:10:5"),
            new ArsenalWeapon("Atgeir", "Halberdier", "AtgeirBronze",
                new Swing("attack", "2Hand-Spear-Attack1", Blow.Pierce, 1.2f, 2.8f, 60f, 4f), "ECP_Spine:2, BoneFragments:16:8"),
            Bow,
        };

        /// <summary>The weapon's name in settings ("Dagger"), and the skeleton's title ("Cutthroat").</summary>
        public readonly string Key, Title;

        /// <summary>The bronze-age weapon of the class the players' item copies: its handling, skill, sounds and numbers.</summary>
        public readonly string GameItem;

        /// <summary>How the skeleton swings it; null for the bow.</summary>
        public readonly Swing? Swing;

        /// <summary>The players' recipe (item:amount[:per upgrade], comma separated).</summary>
        public readonly string Recipe;

        private ArsenalWeapon(string key, string title, string gameItem, Swing? swing, string recipe)
        {
            (Key, Title, GameItem, Swing, Recipe) = (key, title, gameItem, swing, recipe);
        }

        public bool IsBow => Swing == null;

        /// <summary>Held in both hands: no shield beside it (the bow is held in the left).</summary>
        public bool TwoHanded => Key == "Atgeir";

        /// <summary>The players' item prefab ("ECP_BoneDagger") and its translation key ("ecp_bonedagger").</summary>
        public string Item => "ECP_Bone" + Key;

        public string Word => "ecp_bone" + Key.ToLowerInvariant();

        /// <summary>The creature's translation key ("ecp_skeletoncutthroat").</summary>
        public string EnemyWord => "ecp_skeleton" + Title.ToLowerInvariant();

        /// <summary>The bundle's model the skeleton holds, the one the players hold (the bow sits differently in a player's hand), and the icon.</summary>
        public string Model => "ecp_skel_" + Key.ToLowerInvariant();

        public string PlayerModel => IsBow ? Model + "_player" : Model;

        public string Icon => PlayerModel + "_icon";
    }
}
