// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// What the target's owner applies for the attacker of the hit it is resolving (Character.RPC_Damage returns at once
    /// on any other peer). The owner cannot see the attacker's gear, so it reads the attacker's player ZDO, which the
    /// attacker's own client writes: Lingering Wounds' total (<c>ecf_dot</c>, <see cref="PlayerStats"/>, on rebuild) and
    /// the penetration of the weapon it last swung (<c>ecf_pen</c>, <see cref="Penetration"/>, at the swing's start), each
    /// clamped to the running rules. A creature, or a player without the keys, reads 0. Set for the duration of the
    /// RPC only; a hit resolved inside another (Bramblehide's returned damage on a creature this peer owns) keeps its
    /// own values and restores the outer ones. Works on a dedicated server: it needs no local player. Entered and left
    /// by <see cref="IncomingDamageDispatch"/>, which looks the attacker's ZDO up once for every feature.
    /// </summary>
    internal static class HitOwnerContext
    {
        public struct Saved
        {
            public float DotScale;
            public float ResistBypass;
        }

        /// <summary>How much longer the attacker's burning, poison and frost last (1 = as the game has it).</summary>
        public static float DotScale { get; private set; } = 1f;

        /// <summary>The share of each resistance the attacker's hit bypasses (0 = none).</summary>
        public static float ResistBypass { get; private set; }

        /// <summary>The values for a hit the target's owner resolves; <paramref name="attacker"/> is null off the owner.</summary>
        public static Saved Enter(ZDO? attacker)
        {
            Saved outer = new Saved { DotScale = DotScale, ResistBypass = ResistBypass };
            bool fromPlayer = attacker != null && ItemEffects.Enabled;
            DotScale = fromPlayer ? 1f + PlayerStats.OfZdo(attacker, PlayerStats.Dot) : 1f;
            ResistBypass = fromPlayer ? Penetration.OfZdo(attacker) : 0f;
            return outer;
        }

        public static void Exit(Saved outer)
        {
            DotScale = Math.Max(1f, outer.DotScale);
            ResistBypass = Math.Max(0f, outer.ResistBypass);
        }

        public static void Reset()
        {
            DotScale = 1f;
            ResistBypass = 0f;
        }
    }
}
