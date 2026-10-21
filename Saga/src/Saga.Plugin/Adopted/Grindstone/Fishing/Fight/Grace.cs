// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Fight/Grace.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Catching your breath. In the game a fish on the line drains stamina every step (1 per second, a fifth of that at
    /// level 100), so stamina never comes back during a fight, and the fish is lost the moment it runs out. From Grace
    /// Level, the first time a fish would be lost that way the angler gets Grace Seconds instead: the game's step is
    /// skipped, so nothing drains and stamina starts to come back (after the game's usual 1 s pause), while the fish
    /// takes line (1.5 m per second, up to the line's full length) and pulls the float along. The grace ends early once a
    /// quarter of the angler's stamina is back; still at 0 when it runs out, the game's step loses the fish as usual.
    /// Attacking or drawing a bow ends it, so the game's step lets the fish go and removes the float as it always does.
    /// Once per fish. On the angler's client, from <see cref="FloatScope"/>.
    /// </summary>
    public static class Grace
    {
        private const float LinePerSecond = 1.5f;
        private const float RecoveredShare = 0.25f;

        /// <summary>
        /// Holds the fish through the grace; true while it does (the game's step must not run). An angler who attacks or
        /// draws a bow ends it: the game's step then lets the fish go and removes the float, as it always does then.
        /// </summary>
        public static bool Step(FishingFloat fishingFloat, FloatFight fight, Player angler, Fish fish, float dt)
        {
            if (angler.InAttack() || angler.IsDrawingBow())
                return false;
            if (fight.InGrace)
            {
                if (angler.GetStamina() >= angler.GetMaxStamina() * RecoveredShare)
                {
                    fight.GraceUntil = 0f;
                    return false;
                }
                Hold(fishingFloat, angler, fish, dt);
                TensionBar.Show(fight);
                return true;
            }
            if (angler.HaveStamina() || fight.GraceUsed || !FishSkill.Reached(fight.Level, FishingFightSettings.GraceLevel.Value))
                return false;
            Begin(fishingFloat, fight);
            Hold(fishingFloat, angler, fish, dt);
            return true;
        }

        private static void Begin(FishingFloat fishingFloat, FloatFight fight)
        {
            fight.GraceUsed = true;
            fight.GraceUntil = Time.time + Mathf.Max(1f, FishingFightSettings.GraceSeconds.Value);
            fight.Tension = 0f;
            fishingFloat.Message(SagaText.Text("gs_d5567dc3e12bc610"), prioritized: true);
            TensionBar.Show(fight);
        }

        /// <summary>The fish runs with the float and the line pays out, as the game's physics would with a slack line.</summary>
        private static void Hold(FishingFloat fishingFloat, Player angler, Fish fish, float dt)
        {
            Transform rodTop = fishingFloat.GetRodTop(angler);
            if (rodTop == null)
                return;
            fishingFloat.m_lineLength = Mathf.Min(fishingFloat.m_maxDistance - 1f, fishingFloat.m_lineLength + LinePerSecond * dt);
            Utils.Pull(fishingFloat.m_body, fish.transform.position, 0.5f, fishingFloat.m_moveForce, 0.5f, 0.3f);
            Utils.Pull(fishingFloat.m_body, rodTop.position, fishingFloat.m_lineLength, fishingFloat.m_moveForce, 1f, 0.3f);
        }
    }
}
