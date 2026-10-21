// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Relentless ignores sneaking. The game hides a crouching player from monsters in three ways, all read off the
    /// player while a monster's AI thinks: its stealth factor shrinks how far the monster sees it and how near it must
    /// come before the monster turns alert; its crouch lowers the point the monster needs a clear line to, so low cover
    /// hides it; and moving crouched makes no footstep noise to hear. While a Relentless creature's AI is thinking
    /// (<see cref="Active"/>, opened and closed around its update by the Relentless senses patches) all three answer as
    /// if the player stood upright: full stealth factor, not crouching, and moving crouched as loud as walking. Every
    /// other creature, and the player's own stealth display, sees the real values. This runs where the AI runs, on the
    /// creature's owner, against the player's crouch pose, velocity and noise the game already shares with every peer.
    /// </summary>
    public static class RelentlessSenses
    {
        /// <summary>The noise range the game gives a player who moves upright without running.</summary>
        private const float WalkNoise = 15f;

        /// <summary>The game's own threshold for "moving", 0.1 m/s, squared.</summary>
        private const float MovingSqr = 0.01f;

        /// <summary>True while a Relentless creature's AI is thinking; every player it senses then counts as standing.</summary>
        public static bool Active { get; private set; }

        public static void Enter(BaseAI ai) => Active = RelentlessHunters.Has(ai);

        public static void Exit() => Active = false;

        /// <summary>
        /// A player's noise as the creature hears it: moving while crouched makes the game's walking noise. Any louder
        /// noise (running, fighting, chopping) is the game's own. Noise-dampening gear is not applied to the substitute,
        /// since a remote player's status effects are simulated only on that player's own machine.
        /// </summary>
        public static float StandingNoise(Player player, float noise)
        {
            if (noise >= WalkNoise || player.GetVelocity().sqrMagnitude < MovingSqr || !Crouched(player))
            {
                return noise;
            }
            return WalkNoise;
        }

        // The game's own crouch test, asked with the scope lifted - inside it every player answers "standing".
        private static bool Crouched(Player player)
        {
            Active = false;
            try
            {
                return player.IsCrouching();
            }
            finally
            {
                Active = true;
            }
        }
    }
}
