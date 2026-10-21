// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/FishingFightSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 51: the fight on the line. Line tension builds while you reel against a thrashing fish and snaps the line
    /// when full; the strike window after a nibble grows with the angler's level, and a strike right on the nibble is a
    /// perfect strike; every thrash is shorter than the last until the fish is spent; from the grace level, running out
    /// of stamina lets the fish run instead of losing it. Values at 0 and at 100 grow linearly with the angler's Fishing
    /// level in between. Synced.
    /// </summary>
    public static class FishingFightSettings
    {
        public const string Section = FishingSettings.FightSection;

        public static NativeSetting<bool> TensionEnabled { get; private set; }
        public static NativeSetting<float> TensionBuildAt0 { get; private set; }
        public static NativeSetting<float> TensionBuildAt100 { get; private set; }
        public static NativeSetting<float> TensionEase { get; private set; }
        public static NativeSetting<float> StrikeWindowAt0 { get; private set; }
        public static NativeSetting<float> StrikeWindowAt100 { get; private set; }
        public static NativeSetting<float> PerfectStrikeWindow { get; private set; }
        public static NativeSetting<float> TiringPerThrash { get; private set; }
        public static NativeSetting<int> ThrashesToTire { get; private set; }
        public static NativeSetting<float> SpentReelSpeed { get; private set; }
        public static NativeSetting<float> GraceLevel { get; private set; }
        public static NativeSetting<float> GraceSeconds { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindTension(config);
            BindStrike(config);
            BindTiring(config);
        }

        private static void BindTension(NativeConfiguration config)
        {
            TensionEnabled = config.Bind(Section, "Line Tension", true,
                "Reeling while a hooked fish thrashes builds tension on the line, shown in a bar under the crosshair; when it is full the line snaps. Ease off while it thrashes, reel while it rests. Off, the line only breaks as in the game.");
            TensionBuildAt0 = config.Bind(Section, "Tension Build At 0", 60f,
                "Percent of the line's strength a level 0 angler builds per second of reeling against a thrashing fish.",
                acceptableValues: new AcceptableValueRange<float>(1f, 500f));
            TensionBuildAt100 = config.Bind(Section, "Tension Build At 100", 30f,
                "The same for a level 100 angler.", acceptableValues: new AcceptableValueRange<float>(1f, 500f));
            TensionEase = config.Bind(Section, "Tension Ease", 50f,
                "Percent of the line's strength the tension drops per second while you are not reeling. It drops a fifth as fast while you reel a fish that is not thrashing.",
                acceptableValues: new AcceptableValueRange<float>(1f, 500f));
        }

        private static void BindStrike(NativeConfiguration config)
        {
            StrikeWindowAt0 = config.Bind(Section, "Strike Window At 0", 0.5f,
                "Seconds after a nibble in which reeling sets the hook, for a level 0 angler. The game's own is 0.5.",
                acceptableValues: new AcceptableValueRange<float>(0.1f, 5f));
            StrikeWindowAt100 = config.Bind(Section, "Strike Window At 100", 1f,
                "The same for a level 100 angler.", acceptableValues: new AcceptableValueRange<float>(0.1f, 5f));
            PerfectStrikeWindow = config.Bind(Section, "Perfect Strike Window", 0.2f,
                "Seconds after a nibble in which starting to reel is a perfect strike: the fish skips its first thrash. Reeling already when it nibbles never counts. 0 turns perfect strikes off.",
                acceptableValues: Settings.UpTo(2f));
        }

        private static void BindTiring(NativeConfiguration config)
        {
            TiringPerThrash = config.Bind(Section, "Tiring Per Thrash", 15f,
                "Percent shorter each thrash of a hooked fish is than its first: the third thrash lasts 70% as long. Never below 20%.",
                acceptableValues: Settings.UpTo(50f));
            ThrashesToTire = config.Bind(Section, "Thrashes To Tire", 4,
                "Thrashes after which a hooked fish is spent: it thrashes no more and comes in faster. Legendary fish take the Legendary Thrashes setting instead. 0 turns tiring out off.",
                acceptableValues: new AcceptableValueRange<int>(0, 50));
            SpentReelSpeed = config.Bind(Section, "Spent Reel Speed", 50f,
                "Percent faster the line comes in once the fish is spent.", acceptableValues: Settings.UpTo(300f));
            GraceLevel = config.Bind(Section, "Grace Level", 75f,
                "Fishing level from which running out of stamina with a fish on the line does not lose it at once: the fish takes line while you catch your breath, once per fish. 0 gives it to everyone; above 100 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
            GraceSeconds = config.Bind(Section, "Grace Seconds", 4f,
                "How long the grace lasts. It ends early once a quarter of your stamina is back; still out of stamina at the end, the fish gets away.",
                acceptableValues: new AcceptableValueRange<float>(1f, 30f));
        }
    }
}
