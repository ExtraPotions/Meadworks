// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.15.0, GPL-3.0-or-later.
// Adapted from ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1:
// GrindstoneSkills/GrindstoneSkills/Sailing/WindCall/WindCallInput.cs (Unlockable, Refusal, cooldown),
// Sailing/SailingAbilities.cs (Unlocked), Sailing/WindCall/WindCallState.cs (end time, direction check, Compass),
// Sailing/WindCall/WindCallReceive.cs (Announcement), Sailing/Lookout/LookoutReveal.cs (IsTarget),
// Sailing/Lookout/LookoutRing.cs (Spread radius).
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: ShipCall accepts a networked Wind Call only from the player steering that ship, and a Lookout
// pulse only from a player aboard it. Namespace Keel.Sailing; Unity-free; the
// cooldown is an object per ability and player instead of a static field; refusals are reasons for Keel's C05 texts
// instead of English strings; the ZDO reads and writes stay in the plugin; the announcement choice, the lookout target
// test and the ring radius are pure functions of their inputs so they are testable.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Keel.Localization;

namespace ExtraPotions.Keel.Sailing
{
    public enum AbilityRefusal { None, NeedsLevel, CoolingDown }

    /// <summary>Wind Call (level 25) and Lookout (level 50): unlock levels and refusals.</summary>
    public static class AbilityRules
    {
        /// <summary>The ability exists at all: Sailing on and its level at most 100 (above 100 turns it off).</summary>
        public static bool Unlockable(bool sailingActive, float unlockLevel) => sailingActive && unlockLevel <= SailingRules.MaxLevel;

        /// <summary>The player has it: unlockable and their level reaches the unlock level (0 gives it to everyone).</summary>
        public static bool Unlocked(bool sailingActive, float unlockLevel, float level) => Unlockable(sailingActive, unlockLevel) && level >= unlockLevel;

        /// <summary>Why the player cannot use it now; <paramref name="secondsLeft"/> is the whole seconds of cooldown left.</summary>
        public static AbilityRefusal Refuse(float level, float unlockLevel, AbilityCooldown cooldown, float now, out int secondsLeft)
        {
            if (cooldown == null) throw new ArgumentNullException(nameof(cooldown));
            secondsLeft = 0;
            if (level < unlockLevel) return AbilityRefusal.NeedsLevel;
            var wait = cooldown.Left(now);
            if (wait <= 0f) return AbilityRefusal.None;
            secondsLeft = (int)Math.Ceiling(wait);
            return AbilityRefusal.CoolingDown;
        }
    }

    /// <summary>One player's cooldown of one ability, on that player's own client (game time in seconds).</summary>
    public sealed class AbilityCooldown
    {
        private float readyAt;

        /// <summary>Seconds until it may be used again; 0 when ready.</summary>
        public float Left(float now) => Math.Max(0f, readyAt - now);

        public void Start(float now, float seconds) => readyAt = now + Math.Max(0f, float.IsNaN(seconds) ? 0f : seconds);
    }

    /// <summary>
    /// A ship's called wind, kept in the ship's ZDO so every client aboard reads the same one: the flat direction it
    /// blows toward and the world time (ticks) it ends. Only the ship's owner writes it.
    /// </summary>
    public static class WindCallState
    {
        private static readonly string[] points = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        public static IReadOnlyList<string> Points { get; } = Array.AsReadOnly(points);

        /// <summary>The end time of a wind called now for <paramref name="seconds"/>.</summary>
        public static long Until(long nowTicks, float seconds) => nowTicks + (long)(Math.Max(0f, seconds) * TimeSpan.TicksPerSecond);

        /// <summary>A called wind blows now: its end is later than now and its direction is a real one.</summary>
        public static bool Blows(long untilTicks, long nowTicks, float x, float z) => untilTicks > nowTicks && x * x + z * z > 0.5f;

        /// <summary>
        /// Whether a wind change already under way must be turned toward the called wind. The game ignores new wind targets
        /// until a change finishes, and it advances a change by one physics step per rendered frame, so a call made during a
        /// native change would wait several seconds, longer at a low frame rate.
        /// </summary>
        public static bool Retarget(bool changing, float targetX, float targetZ, float calledX, float calledZ)
        {
            if (!changing || calledX * calledX + calledZ * calledZ <= 0.5f) return false;
            var target = Math.Sqrt(targetX * targetX + targetZ * targetZ);
            var called = Math.Sqrt(calledX * calledX + calledZ * calledZ);
            if (target < 1e-6) return true;
            return (targetX * calledX + targetZ * calledZ) / (target * called) < 0.999;
        }

        /// <summary>Which of the three Wind Call texts a client aboard shows: its own call, a named crewmate's, or nobody's.</summary>
        public static WindAnnouncement Announce(long caller, long local, bool callerAboard)
        {
            if (caller == local) return WindAnnouncement.Self;
            return callerAboard ? WindAnnouncement.ByName : WindAnnouncement.Everyone;
        }

        /// <summary>The C05 key of an announcement: self and everyone take (point, seconds), by-name takes (name, point, seconds).</summary>
        public static string KeyOf(WindAnnouncement announcement)
        {
            switch (announcement)
            {
                case WindAnnouncement.Self: return KeelTextKeys.WindCalledSelf;
                case WindAnnouncement.ByName: return KeelTextKeys.WindCalledBy;
                default: return KeelTextKeys.WindCalled;
            }
        }

        /// <summary>The compass point a flat direction points to, the world's +z being north as on the map: "north-east".</summary>
        public static string Compass(float x, float z)
        {
            var degrees = Math.Atan2(x, z) * 180.0 / Math.PI;
            degrees -= Math.Floor(degrees / 360.0) * 360.0;
            return points[(int)Math.Round(degrees / 45.0) % points.Length];
        }
    }

    public enum WindAnnouncement { Self, ByName, Everyone }

    /// <summary>Who may apply a ship RPC. The caller id is the resolved peer, not the number in the packet.</summary>
    public static class ShipCall
    {
        public static bool MayCallWind(long senderPlayerId, bool aboard, bool controlling) =>
            senderPlayerId != 0L && aboard && controlling;

        public static bool MayPulseLookout(long senderPlayerId, bool aboard) =>
            senderPlayerId != 0L && aboard;
    }

    /// <summary>The lookout pulse: which creatures it reveals and how its ring spreads.</summary>
    public static class LookoutRules
    {
        public const float RingStartRadius = 3f;
        public const float RingSeconds = 2f;

        /// <summary>
        /// A creature the pulse reveals: not a player, alive, not a boss (its tag is the boss bar), its hud not hidden, an
        /// enemy of the player (tamed creatures are not), within the radius on the flat.
        /// </summary>
        public static bool IsTarget(bool isPlayer, bool isDead, bool isBoss, bool hideHud, bool isEnemy, float dx, float dz, float radius)
        {
            if (isPlayer || isDead || isBoss || hideHud || !isEnemy) return false;
            return dx * dx + dz * dz <= radius * radius;
        }

        /// <summary>The ring's radius at fraction t of its life: fast at first, easing out at the edge.</summary>
        public static float RingRadius(float t, float radius)
        {
            var end = Math.Max(RingStartRadius, radius);
            var x = Math.Min(1f, Math.Max(0f, float.IsNaN(t) ? 0f : t));
            var eased = 1f - (1f - x) * (1f - x);
            return RingStartRadius + (end - RingStartRadius) * eased;
        }
    }
}
