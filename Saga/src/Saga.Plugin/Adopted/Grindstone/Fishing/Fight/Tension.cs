// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Fight/Tension.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Line tension, on the angler's client, every step a fish is on the line (<see cref="FloatScope"/>). The game makes a
    /// hooked fish thrash now and then (Fish.Escape: 0.5 to 3 s plus 1.5 s per level, longer for later fish); reeling
    /// then goes at half speed and costs its escape pull. Here, reeling while it thrashes also builds tension (Tension
    /// Build, less for a skilled angler), reeling a resting fish lets it ease slowly and not reeling lets it ease fast.
    /// At full tension the line snaps the way the game snaps it: the fish goes free and the float is gone. So the fight
    /// has a rhythm: ease off while it thrashes, reel while it rests. The bar under the crosshair
    /// (<see cref="TensionBar"/>) shows it. The hook itself starts a thrash while the angler is still reeling, so tension
    /// only builds from <see cref="ReactSeconds"/> after the hook: time to see the bar and let go.
    /// </summary>
    public static class Tension
    {
        /// <summary>How much slower tension eases while you reel a resting fish than while you let the line be.</summary>
        private const float ReelingEaseShare = 0.2f;

        /// <summary>Seconds after the hook in which reeling into the first thrash builds no tension.</summary>
        public const float ReactSeconds = 0.75f;

        /// <summary>Updates the tension; true when the line snapped (the game's step must not run).</summary>
        public static bool Step(FishingFloat fishingFloat, FloatFight fight, Player angler, Fish fish, float dt)
        {
            fight.Thrashing = fish.IsEscaping();
            if (!FishingFightSettings.TensionEnabled.Value)
            {
                fight.Tension = 0f;
                return false;
            }
            bool reeling = angler.IsBlocking() && angler.HaveStamina();
            bool thrashing = fight.Thrashing && Time.time - fight.HookedAt >= ReactSeconds;
            fight.Tension = Mathf.Clamp01(fight.Tension + Change(reeling, thrashing, fight.Level) * dt);
            TensionBar.Show(fight);
            if (fight.Tension < 1f)
                return false;
            Snap(fishingFloat, fish);
            return true;
        }

        /// <summary>The share of the line's strength gained (or lost, below 0) per second.</summary>
        public static float Change(bool reeling, bool thrashing, float level)
        {
            if (reeling && thrashing)
                return FishSkill.Percent(FishSkill.Between(FishingFightSettings.TensionBuildAt0.Value, FishingFightSettings.TensionBuildAt100.Value, level));
            float ease = FishSkill.Percent(FishingFightSettings.TensionEase.Value);
            return reeling ? -ease * ReelingEaseShare : -ease;
        }

        /// <summary>The game's own line break: its message, the fish released, the float gone with the break effect.</summary>
        private static void Snap(FishingFloat fishingFloat, Fish fish)
        {
            fishingFloat.Message("$msg_fishing_linebroke", prioritized: true);
            fish.OnHooked(null);
            fishingFloat.m_lineBreakEffect.Create(fishingFloat.transform.position, Quaternion.identity);
            fishingFloat.m_nview.Destroy();
            TensionBar.Hide();
        }
    }
}
