// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Fishing/Core/FloatFight.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Everything GrindstoneSkills tracks about one cast, on the angler's client only: added to the float when it lands
    /// (<see cref="FloatSetup"/>), so it lives and dies with the float and no other client ever has one. A float without
    /// it (cast by someone else, or before Fishing was turned on) is left entirely to the game.
    /// </summary>
    public sealed class FloatFight : MonoBehaviour
    {
        // The cast.
        /// <summary>The angler's Fishing level at the cast.</summary>
        public float Level;
        /// <summary>The float touched the water and the senses have read it.</summary>
        public bool Landed;
        /// <summary>Seconds in the water without a fish on the line, towards a snag.</summary>
        public float WaterTime;
        public bool SnagRolled;
        public bool Snagged;
        /// <summary>The biome the float was in when it snagged: the snag's table.</summary>
        public Heightmap.Biome SnagBiome;

        // The nibble.
        /// <summary>The angler was already reeling when the last nibble came: no perfect strike.</summary>
        public bool ReelingAtNibble;

        // The fish on the line; reset by Hooked.
        public Fish Fish;
        /// <summary>Time.time of the hook.</summary>
        public float HookedAt;
        /// <summary>Line tension, 0..1; the line snaps at 1.</summary>
        public float Tension;
        public bool Thrashing;
        /// <summary>Thrashes the fish has started since it was hooked (a cancelled one does not count).</summary>
        public int Thrashes;
        /// <summary>A perfect strike: the fish's first thrash, which the hook starts, is skipped.</summary>
        public bool PerfectPending;
        /// <summary>The fish is spent: no more thrashes, and the line comes in faster.</summary>
        public bool Spent;
        public float GraceUntil;
        public bool GraceUsed;

        public static FloatFight Of(FishingFloat fishingFloat) =>
            fishingFloat == null ? null : fishingFloat.GetComponent<FloatFight>();

        public static FloatFight Attach(FishingFloat fishingFloat, float level)
        {
            FloatFight fight = fishingFloat.gameObject.AddComponent<FloatFight>();
            fight.Level = level;
            return fight;
        }

        public bool InGrace => GraceUntil > Time.time;

        /// <summary>A new fish is on the line; <paramref name="perfect"/> when the strike was.</summary>
        public void Hooked(Fish fish, bool perfect)
        {
            Fish = fish;
            HookedAt = Time.time;
            Tension = 0f;
            Thrashing = false;
            Thrashes = 0;
            PerfectPending = perfect;
            Spent = false;
            GraceUntil = 0f;
            GraceUsed = false;
            Snagged = false;
        }

        /// <summary>No fish on the line (lost, landed or not yet hooked).</summary>
        public void Unhooked()
        {
            if (Fish == null && Tension <= 0f)
                return;
            Fish = null;
            Tension = 0f;
            Thrashing = false;
            GraceUntil = 0f;
        }
    }
}
