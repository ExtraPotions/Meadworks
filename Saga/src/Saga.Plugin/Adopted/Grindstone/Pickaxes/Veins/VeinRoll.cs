// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Veins/VeinRoll.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Globalization;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The dice of a rich vein, thrown the same way on every machine with nothing stored: the world seed and the
    /// deposit's position give a value in 0..1, and the vein chances turn that into 0 to 3 stars.
    /// <list type="bullet">
    /// <item><b>Position:</b> x and z, each rounded to 0.5 m; y is ignored. An intact deposit and the fractured rock it
    /// turns into stand at the same position (Destructible.Destroy instantiates the "_frac" at its own position), and
    /// every machine reads the position from the same ZDO, so they all agree; the rounding only absorbs float noise.</item>
    /// <item><b>Seed:</b> the world's World.m_seed. The server (dedicated or hosting) has it from the world file; a client
    /// receives the same int in ZNet's peer info when it joins and builds its WorldGenerator from it.</item>
    /// <item><b>Hash:</b> the game's string.GetStableHashCode (the same on every machine and every run, unlike .NET's
    /// GetHashCode), of a key written with the invariant culture so a server and a client with different locales write
    /// the same text. Neighbouring positions differ only in the key's last digits, which moves the stable hash by a few
    /// low bits, so the hash is avalanched (the MurmurHash3 finalizer) before its top 24 bits become the value.</item>
    /// </list>
    /// </summary>
    public static class VeinRoll
    {
        /// <summary>The highest vein, as many stars as a dish can have.</summary>
        public const int MaxStars = 3;

        /// <summary>Steps per metre the position is rounded to: 0.5 m.</summary>
        private const float StepsPerMetre = 2f;

        /// <summary>Keeps these dice apart from any other position hash.</summary>
        private const string Salt = "saga.vein";

        /// <summary>The loaded world's seed; 0 before a world is loaded.</summary>
        public static int Seed()
        {
            World world = ZNet.World;
            if (world != null)
                return world.m_seed;
            return WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed() : 0;
        }

        /// <summary>The value, 0 to just below 1, of a deposit standing at this position in the loaded world.</summary>
        public static float At(Vector3 position) => At(Seed(), position);

        /// <summary>The value, 0 to just below 1, of a deposit standing at this position in a world with this seed.</summary>
        public static float At(int seed, Vector3 position)
        {
            string key = Salt + ":" + Text(seed) + ":" + Text(Step(position.x)) + ":" + Text(Step(position.z));
            uint mixed = Avalanche(unchecked((uint)key.GetStableHashCode()));
            return (mixed >> 8) / 16777216f;
        }

        /// <summary>
        /// The stars a value gives, with the vein chances as they are now: 3 below "Vein Chance 3 Stars", 2 below that plus
        /// "Vein Chance 2 Stars", 1 below all three chances together, else 0.
        /// </summary>
        public static int StarsFor(float value)
        {
            float below = 0f;
            for (int stars = MaxStars; stars >= 1; stars--)
            {
                below += Mathf.Max(0f, Chance(stars)) / 100f;
                if (value < below)
                    return stars;
            }
            return 0;
        }

        private static float Chance(int stars)
        {
            switch (stars)
            {
                case 3:
                    return VeinSettings.Chance3.Value;
                case 2:
                    return VeinSettings.Chance2.Value;
                default:
                    return VeinSettings.Chance1.Value;
            }
        }

        private static int Step(float metres) => Mathf.RoundToInt(metres * StepsPerMetre);

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>The MurmurHash3 32-bit finalizer: every input bit flips about half the output bits.</summary>
        private static uint Avalanche(uint hash)
        {
            unchecked
            {
                hash ^= hash >> 16;
                hash *= 0x85EBCA6Bu;
                hash ^= hash >> 13;
                hash *= 0xC2B2AE35u;
                hash ^= hash >> 16;
                return hash;
            }
        }
    }
}
