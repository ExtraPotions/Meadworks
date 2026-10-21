// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Echo/Echo.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Echo, the level 50 milestone, on the miner's own client: from "Echo Level" a swing that hits rock finds the
    /// nearest loaded ore deposit within "Echo Radius" (<see cref="EchoScan"/>), plays the Wishbone's ping 4 m from the
    /// player towards it and floats "&lt;name&gt;, 32 m" there. Everything is local: a local-only copy of the ping
    /// (<see cref="WishbonePing"/>, a 3D sound, so it comes from the deposit's direction) and the game's local floating
    /// text, so only the miner hears and sees it and nothing is sent. The name is the deposit's display name
    /// (<see cref="Rock.DisplayName"/>, its ore's for a deposit without a name of its own).
    /// <list type="bullet">
    /// <item><b>Cooldown:</b> "Echo Cooldown" seconds, the miner's own, in memory. Only a ping starts it: a swing with
    /// nothing in range is silent and leaves the Echo ready, so the first swing that has a deposit in range pings at
    /// once. Such a swing only looks through the deposits the last walk over the loaded objects found; a new walk
    /// happens every few seconds or after a few metres (<see cref="EchoScan"/>).</item>
    /// <item><b>The text ignores "Show Callouts":</b> it is the feature itself (the only way to learn which deposit and
    /// how far), not a callout about something that happened; "Echo Level" above 100 turns the Echo off.</item>
    /// </list>
    /// </summary>
    public static class Echo
    {
        /// <summary>How far from the player, towards the deposit, the ping sounds and the text floats, in metres.</summary>
        private const float PingDistance = 4f;

        private static float lastPing = float.NegativeInfinity;

        /// <summary>
        /// Called by <see cref="MineSwing"/> on the miner's own client, once per swing that hit rock, when the game raises
        /// Pickaxes for it, after the wear perk. <paramref name="rocks"/> lists the rocks the swing hit (never empty);
        /// they are skipped, and so is the fractured form an intact deposit among them just turned into.
        /// </summary>
        public static void OnSwingHitRock(Attack attack, IReadOnlyList<Rock> rocks)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !Ready())
                return;
            Rock deposit = EchoScan.Nearest(player.transform.position, VeinSettings.EchoRadius.Value, rocks);
            if (deposit == null)
                return;
            lastPing = Time.time;
            Ping(player, deposit);
        }

        /// <summary>The local player reached "Echo Level" and their cooldown is over.</summary>
        private static bool Ready() =>
            PickSkill.Active
            && PickSkill.Reached(PickSkill.Local(), VeinSettings.EchoLevel.Value)
            && Time.time - lastPing >= VeinSettings.EchoCooldown.Value;

        /// <summary>The ping and "&lt;name&gt;, N m" at a spot 4 m from the player's chest towards the deposit (at the deposit when it is nearer).</summary>
        private static void Ping(Player player, Rock deposit)
        {
            Vector3 chest = player.GetCenterPoint();
            Vector3 spot = chest + Vector3.ClampMagnitude(deposit.Position - chest, PingDistance);
            WishbonePing.Play(spot);
            int metres = Mathf.RoundToInt(Vector3.Distance(player.transform.position, deposit.Position));
            FloatingText.Show(spot, SagaText.Format("gs_7d4a83c54abcca2a", deposit.DisplayName, metres));
        }
    }
}
