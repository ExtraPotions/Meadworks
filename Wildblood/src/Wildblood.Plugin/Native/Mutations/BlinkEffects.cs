// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What a machine holding a Blinking creature sees and hears, drawn from the blink message. The tell is the `tell
    /// effect` at the destination plus a chime played there: the spot is behind the target, which for a player is at or
    /// under their own camera, so the marker alone is easy to miss - the chime tells them where to turn. The blink is
    /// the `blink effect` twice, a puff where it vanishes and one where it reappears. Each is a local cosmetic clone
    /// (<see cref="CosmeticClone"/>), so the player's effect density applies, and each is sized to the creature's body.
    /// </summary>
    internal static class BlinkEffects
    {
        private static readonly string[] Marker = { "ping", "spawn", "glow", "spark" };
        private static readonly string[] Puff = { "ghost", "spawn", "puff", "smoke", "poof" };
        private static readonly string[] Chime = { "wishboneping", "ping", "ghost" };

        public static void Tell(string effect, string sound, Vector3 dest, float radius)
        {
            CosmeticClone.Flash(EffectResolver.Resolve(effect, Marker, "Blinking tell effect"), dest, radius);
            CosmeticClone.Sound(EffectResolver.ResolveSound(sound, Chime, "Blinking tell sound"), dest);
        }

        public static void Blink(string effect, Vector3 from, Vector3 to, float radius)
        {
            GameObject? prefab = EffectResolver.Resolve(effect, Puff, "Blinking blink effect");
            CosmeticClone.Flash(prefab, from, radius);
            CosmeticClone.Flash(prefab, to, radius);
        }

        /// <summary>
        /// The size the effects are drawn at, from the body's height: a vanilla effect is authored for a radius of 4 m,
        /// so a greydwarf-sized body draws it about as authored, a troll up to two and a half times as big.
        /// </summary>
        public static float Radius(Character creature) => Mathf.Clamp(BlinkSpot.Height(creature) * 2f, 3f, 10f);

        /// <summary>A dedicated server has no screen or speakers: it draws nothing and hides nothing.</summary>
        public static bool Headless() => ZNet.instance != null && ZNet.instance.IsDedicated();
    }
}
