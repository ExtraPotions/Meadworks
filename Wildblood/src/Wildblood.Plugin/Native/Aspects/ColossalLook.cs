// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// What a Colossal shockwave looks and sounds like, on every machine holding the boss and never on a dedicated server:
    /// the Elder's own stomp - a flat wave rolling out along the ground, a ring of dust, flung rock and a shake of the
    /// camera - grown so the wave's edge lands on the shockwave's radius, so a player can read at a glance whether it
    /// reached them, with the deep crunch of splitting rock. It is a cosmetic clone, so a player's effect density thins
    /// or removes the look; the sound plays whatever the density.
    /// </summary>
    internal static class ColossalLook
    {
        private const string LookName = "vfx_gdking_stomp";
        private const string SoundName = "sfx_gdking_rock_destroyed";

        // Stand-ins should a game update rename the effects: the nearest ground slam, the first keyword first.
        private static readonly string[] LookKeywords = { "stomp", "groundslam", "slam", "shockwave" };
        private static readonly string[] SoundKeywords = { "rock_destroyed", "stomp", "slam", "impact" };

        /// <summary>The Elder's stomp wave is 14 m across as the game draws it, so its edge is 7 m out.</summary>
        private const float AuthoredWaveRadius = 7f;

        /// <summary>
        /// The radius <see cref="CosmeticClone.FlashWhole"/> treats an effect as drawn for (CosmeticClone's baseline): it
        /// grows an effect by radius / this, so scaling by this / the wave's own radius puts the wave's edge on the radius.
        /// </summary>
        private const float FlashBaseline = 4f;

        public static void Draw(Vector3 point, float radius)
        {
            if (ZNet.instance == null || ZNet.instance.IsDedicated())
            {
                return;
            }
            GameObject? look = EffectResolver.Resolve(LookName, LookKeywords);
            CosmeticClone.FlashWhole(look, point, radius, FlashBaseline / AuthoredWaveRadius);
            CosmeticClone.Sound(EffectResolver.ResolveSound(SoundName, SoundKeywords), point);
        }
    }
}
