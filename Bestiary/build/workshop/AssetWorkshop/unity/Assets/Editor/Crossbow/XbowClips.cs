namespace Workshop.Crossbow
{
    /// <summary>
    /// The crossbowman's two clips, which the mod puts in place of the skeleton archer's: the game's Skeleton controller
    /// plays its bow shot as two states, bow_idle (the Draugr's "Bow Aim Idle 01", left at 24% of its length) then
    /// attack_bow ("Bow Aim Recoil" at 1.2x speed, left at 87%), blending 0.25 s into each and out of the second.
    ///
    ///   ecp_xbow_aim, in bow_idle: raises the crossbow in the left fist to the right shoulder, the right hand takes the
    ///     stock's wrist, the head lies on the stock, and it holds the aim until the shot state takes over at
    ///     <see cref="AimHold"/>.
    ///   ecp_xbow_fire, in attack_bow: holds, shoots (the game's OnAttackTrigger at <see cref="Fire"/>), kicks, lowers the
    ///     crossbow, the right hand hooks the string and draws it into the nut, takes a bolt from the quiver on the right
    ///     hip and lays it in the groove, and it carries the crossbow again by <see cref="Done"/>, where the state hands
    ///     back to the idle.
    ///
    /// Times are the clips' own seconds. The mod times the string and the bolts by them (EliteCreaturesPack Crossbow/
    /// XbowRig): let go from Fire, on the fingers from StringGrab, in the nut from Spanned; a bolt in the fingers from
    /// BoltGrab to Lay, in the groove from Lay to the next Fire. The pinch is the right hand's.
    /// </summary>
    public static class XbowClips
    {
        public const string AimName = "ecp_xbow_aim";
        public const string FireName = "ecp_xbow_fire";
        public const string AimReplaces = "Bow Aim Idle 01";
        public const string FireReplaces = "Bow Aim Recoil";
        public const float AimExit = 0.23824464f;     // the Skeleton controller's exit times
        public const float FireExit = 0.8684211f;
        public const float FireSpeed = 1.2f;          // attack_bow's state speed

        public const float AimHold = 1.05f;
        public static readonly float AimLength = AimHold / AimExit;
        public const float Fire = 0.32f;
        // The reload, from the settled aim at 0.56 to the carry, is played at half pace (2026-09-29, at the user's
        // request): a key at t after 0.56 now sits at 0.56 + 2 x (t - 0.56) (StringGrab to Done were 1.04, 1.34, 1.66,
        // 2.08 and 2.62). EliteCreaturesPack Crossbow/XbowRig mirrors these.
        public const float StringGrab = 1.52f;
        public const float Spanned = 2.12f;
        public const float BoltGrab = 2.76f;
        public const float Lay = 3.60f;
        public const float Done = 4.68f;
        public static readonly float FireLength = Done / FireExit;

        public static XbowKey[] AimKeys() => new[]
        {
            Carried(0f),
            new XbowKey(0.24f) { Bow = Bow.Raised, Right = Hand.Wrist, Yaw = 18f, Cheek = 0.3f, Grip = 0.9f },
            Aimed(0.46f, 1f), Aimed(0.75f, 1f, 1.5f), Aimed(AimHold, 1f), Aimed(AimLength, 1f),
        };

        public static XbowKey[] FireKeys() => new[]
        {
            Aimed(0f, 1f), Aimed(0.30f, 1f),
            new XbowKey(0.38f) { Bow = Bow.Kick, Right = Hand.Wrist, Yaw = 31f, Cheek = 0.6f, Grip = 0.9f },
            Aimed(0.56f, 0.6f),
            Low(1.12f, Hand.Wrist, 18f, 0.8f), Low(StringGrab, Hand.StringGrab, 26f, 0.6f),
            Low(Spanned, Hand.Spanned, 24f, 0.75f), Low(2.44f, Hand.ToQuiver, 16f, 0.1f),
            Low(BoltGrab, Hand.InQuiver, 20f, 0.75f), Low(3.12f, Hand.BoltOut, 22f, 0.75f),
            Low(Lay, Hand.Lay, 26f, 0.75f), Low(3.88f, Hand.Pat, 22f, -0.2f),
            Carried(Done), Carried(FireLength),
        };

        /// <summary>
        /// The players' reload with the Bone Crossbow (the mod plays it in place of the game's Arbalest reload, "Reload
        /// Crossbow", while a Bone Crossbow is in hand; the user: "may need an animation just for this bow when players
        /// use it"): the crossbowman's own reload from the shot's clip, from the carry: lower the crossbow, hook the
        /// string and draw it into the nut, take a bolt from the right hip, lay it and pat it home, carry. The game's
        /// reload state plays at 1.4x and ends when the reload time is up. The keys are the crossbowman's (StringGrab to
        /// Done shifted by <see cref="PlayerShift"/>), paced (<see cref="PlayerPace"/>) so the Bone Crossbow's reload
        /// (<see cref="PlayerReload"/>, its `Reload Time` default) ends <see cref="ReloadEnds"/> into them at full pace:
        /// just after the pat, as the hand goes back to the carry, where the user lined the reload bar up in game (1.3 s
        /// at half pace); the game's blend finishes the carry. On 2026-09-30 the reload went 3, 2, 1.5, 1.3, then 2.3 s.
        /// The Crossbows skill shortens the reload down to half, and the game then cuts more of the clip's end. Its
        /// done state ("Reload done") holds the carry (<see cref="PlayerDoneName"/>) for 0.1 s only: the game's own is 0.9 s,
        /// and while that state plays (tagged minoraction_fast) the player can neither shoot nor block (the user: "after
        /// the reload animation i stand there for a little bit"). Its one event, the reload click, is at 0 s.
        /// </summary>
        public const string PlayerReloadName = "ecp_xbow_player_reload", PlayerDoneName = "ecp_xbow_player_reload_done";
        public const float PlayerReload = 2.3f, ReloadEnds = 3.64f;
        public const float PlayerPace = PlayerReload * 1.4f / ReloadEnds;
        public const float PlayerReloadLength = 3f * 1.4f * PlayerPace, PlayerDoneLength = 0.1f;
        private const float PlayerShift = 0.4f - 1.12f;

        /// <summary>
        /// A crossbowman's reload time (seconds into its fire clip) as the player reload's own seconds: moved 0.72 s
        /// earlier, then paced (EliteCreaturesPack XbowPlayerRig and XbowPlayerHand mirror the results).
        /// </summary>
        public static float PlayerTime(float crossbowman) => (crossbowman + PlayerShift) * PlayerPace;

        public static XbowKey[] PlayerReloadKeys() => new[]
        {
            Carried(0f),
            Low(PlayerTime(1.12f), Hand.Wrist, 18f, 0.8f), Low(PlayerTime(StringGrab), Hand.StringGrab, 26f, 0.6f),
            Low(PlayerTime(Spanned), Hand.Spanned, 24f, 0.75f), Low(PlayerTime(2.44f), Hand.ToQuiver, 16f, 0.1f),
            Low(PlayerTime(BoltGrab), Hand.InQuiver, 20f, 0.75f), Low(PlayerTime(3.12f), Hand.BoltOut, 22f, 0.75f),
            Low(PlayerTime(Lay), Hand.Lay, 26f, 0.75f), Low(PlayerTime(3.88f), Hand.Pat, 22f, -0.2f),
            Carried(PlayerTime(Done)), Carried(PlayerReloadLength),
        };

        public static XbowKey[] PlayerDoneKeys() => new[] { Carried(0f), Carried(PlayerDoneLength) };

        /// <summary>The carry clips' own pose on the idle's first frame, so the shot starts and ends where they are.</summary>
        public static XbowKey Carried(float time) => new XbowKey(time) { Bow = Bow.Carry, Right = Hand.Wrist, Grip = 0.9f };

        private static XbowKey Aimed(float time, float cheek, float sway = 0f) =>
            new XbowKey(time) { Bow = Bow.Aim, Right = Hand.Wrist, Yaw = 32f + sway, Cheek = cheek, Grip = 0.9f };

        private static XbowKey Low(float time, Hand right, float down, float grip) =>
            new XbowKey(time) { Bow = Bow.Lowered, Right = right, Yaw = 10f, Lean = 25f, Down = down, Grip = grip };
    }
}
