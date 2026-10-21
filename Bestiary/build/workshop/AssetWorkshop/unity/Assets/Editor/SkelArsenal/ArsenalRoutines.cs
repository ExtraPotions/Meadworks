namespace Workshop.SkelArsenal
{
    /// <summary>One state of an attack: the clip (a path under the reference export's Assets, without .anim), the state's
    /// speed, and the transition out of it (exit time, normalized, and blend: seconds, or normalized when not fixed).</summary>
    public sealed class Step
    {
        public string Clip;
        public float Speed = 1f, Exit = 1f, Blend = 0.2f;
        public bool FixedBlend = true;
    }

    /// <summary>One weapon of the skeleton arsenal shown by one Skeleton: its prefab, its hand, and its attack.</summary>
    public sealed class Routine
    {
        public string Weapon, Label;
        public bool LeftHand;
        public float Enter = 0.25f;     // the blend from the idle into the first state, seconds
        public Step[] Steps;
    }

    /// <summary>
    /// The attack each weapon plays, taken from the game's own controllers (states, clips, speeds and transition times
    /// read from Player_animator.controller and Skeleton_animator.controller in the reference export): the Skeleton's own
    /// sword swing (its attack_axe state, which skeleton_sword's "attack" triggers), mace (attack_mace, Skeleton_Poison's)
    /// and bow shot (bow_idle, then attack_bow); for the weapons skeletons never carry, the player's primary attack with
    /// that weapon (knife_stab's three slashes, spear_poke, atgeir_attack's three, swing_axe's three). A combo's states
    /// follow each other at 92% with the blend the player's controller uses for the next hit.
    /// </summary>
    public static class ArsenalRoutines
    {
        private const string Old = "Characters/Player/model/old_PlayerCharacter/";
        private const string Axe = "Characters/Player/model/axe_anim/";
        private const string Polearm = "3rd party/RPG Character Animation Pack/Animations/2Hand-Spear/";
        private const string Draugr = "Characters/Draugr/model/";
        private const float Chain = 0.92f;

        public static readonly Routine[] All =
        {
            new Routine { Weapon = "ecp_skel_dagger", Label = "Dagger", Enter = 0.1f, Steps = new[]
            {
                new Step { Clip = Old + "knife_slash0", Speed = 1.2f, Exit = Chain, Blend = 0.1f },
                new Step { Clip = Old + "knife_slash1", Speed = 1.2f, Exit = Chain, Blend = 0.1f },
                new Step { Clip = Old + "knife_slash2", Speed = 1.2f, Exit = 1f, Blend = 0.2f },
            } },
            new Routine { Weapon = "ecp_skel_sword", Label = "Sword", Steps = new[]
            {
                new Step { Clip = Draugr + "Standing Melee Attack Horizontal", Speed = 1.2f, Exit = 0.8358865f, Blend = 0.2554553f, FixedBlend = false },
            } },
            new Routine { Weapon = "ecp_skel_axe", Label = "Axe", Enter = 0.2f, Steps = new[]
            {
                new Step { Clip = Axe + "axe_swing", Speed = 1f, Exit = Chain, Blend = 0.2f },
                new Step { Clip = Axe + "Axe combo 2", Speed = 1.4f, Exit = Chain, Blend = 0.2f },
                new Step { Clip = Axe + "Axe combo 3", Speed = 1.2f, Exit = 1f, Blend = 0.2f },
            } },
            new Routine { Weapon = "ecp_skel_mace", Label = "Mace", Steps = new[]
            {
                new Step { Clip = "Characters/Skeleton/model/Model/Skeleton_mace", Speed = 1f, Exit = 0.9f, Blend = 0.25f },
            } },
            new Routine { Weapon = "ecp_skel_spear", Label = "Spear", Enter = 0.2f, Steps = new[]
            {
                new Step { Clip = Old + "Javelin Stab", Speed = 1f, Exit = 1.3f, Blend = 0.3f },
            } },
            new Routine { Weapon = "ecp_skel_atgeir", Label = "Atgeir", Enter = 0.1f, Steps = new[]
            {
                new Step { Clip = Polearm + "2Hand-Spear-Attack1", Speed = 1f, Exit = Chain, Blend = 0.1f },
                new Step { Clip = Polearm + "2Hand-Spear-Attack9", Speed = 1f, Exit = Chain, Blend = 0.1f },
                new Step { Clip = Polearm + "2Hand-Spear-Attack3", Speed = 1f, Exit = 1f, Blend = 0.3f },
            } },
            new Routine { Weapon = "ecp_skel_bow", Label = "Bow", LeftHand = true, Steps = new[]
            {
                new Step { Clip = Draugr + "Bow Aim Idle 01", Speed = 1f, Exit = 0.23824464f, Blend = 0.2499997f },
                new Step { Clip = Draugr + "Bow Aim Recoil", Speed = 1.2f, Exit = 0.8684211f, Blend = 0.25f },
            } },
        };
    }
}
