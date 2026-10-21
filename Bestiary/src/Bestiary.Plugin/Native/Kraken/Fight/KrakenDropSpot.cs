// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// OWNER, while a kraken dies: every item dropped through the game's <c>ItemDrop.DropItem</c> in its death (Elite
    /// Crafting's runes and rolled gear, which it drops at the dying creature's middle, a loot mod's extras) lands where the
    /// kraken's own loot does (<see cref="KrakenDeath.LootSpot"/>: on the held ship's deck, or just over the water), not
    /// where its body was, which may be deep under the ship. The death patch opens the window before any other mod's part
    /// of the death and closes it after all of them (<see cref="KrakenDeathPatch"/>); it never outlives the frame.
    /// The game's own loot comes another way and is aimed by <see cref="KrakenDeath.AimLoot"/>.
    /// </summary>
    public static class KrakenDropSpot
    {
        private const float Scatter = 0.5f;   // the game's own drop area round a creature's loot spot

        private static Vector3 spot;
        private static int frame = -1;

        public static void Open(Vector3 at) => (spot, frame) = (at, Time.frameCount);

        public static void Close() => frame = -1;

        /// <summary>An item about to be dropped: moved to the spot, a little scattered and never below it, while a death is open.</summary>
        public static void Redirect(ref Vector3 position)
        {
            if (frame != Time.frameCount)
            {
                return;
            }
            Vector3 scatter = Random.insideUnitSphere * Scatter;
            scatter.y = Mathf.Abs(scatter.y);
            position = spot + scatter;
        }
    }
}
