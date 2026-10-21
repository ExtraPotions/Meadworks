// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Where a wave comes from (features/raids.md section 4.3): one spot on the ring 40 to 60 m around the host, tried in
    /// twelve directions at three distances. A spot in water or lava, or on ground this machine has not loaded, never;
    /// of the rest the best by the game's own spawn tests, the most important first: outside the base (the game's
    /// player-base areas, which its own spawns keep out of), nothing overhead (no roof, floor, tree or rock: the game's
    /// blocked test, so never under a roof or beneath the base), a different side from the last wave (a quarter turn or
    /// more), ground level enough to stand on, and a way to the host that crosses no building before the base's heart (one
    /// ray). Ties go to chance. Once a wave, so the few rays it costs are nothing. Each raider then arrives a few metres
    /// around the spot (<see cref="Around"/>).
    /// </summary>
    internal static class WaveRing
    {
        private const int Directions = 12;
        private const float MinTurnDegrees = 90f;
        private const float WetMargin = 0.5f;
        private const float SpreadMetres = 5f;

        /// <summary>Buildings this close to the host are the base's heart: a wave walks into them, not around them.</summary>
        private const float HeartMetres = 20f;

        /// <summary>The game's own steepest spawn ground (35 degrees), as the up-component of the ground's normal.</summary>
        private static readonly float FlatNormal = Mathf.Cos(35f * Mathf.Deg2Rad);

        private static readonly float[] Distances =
            { RaidTable.SpawnRingMin, (RaidTable.SpawnRingMin + RaidTable.SpawnRingMax) / 2f, RaidTable.SpawnRingMax };

        private static readonly int PieceMask = LayerMask.GetMask("piece");

        /// <summary>
        /// The wave's spot on the ground and its direction from the host in degrees. <paramref name="last"/> is the last
        /// wave's direction, or null for the first. Where nothing on the ring is dry and loaded, the host's own spot.
        /// </summary>
        public static Vector3 Pick(Vector3 host, float? last, out float direction)
        {
            float start = Random.Range(0f, 360f);
            Candidate best = new Candidate(host, start, -1);
            int ties = 0;
            for (int i = 0; ZoneSystem.instance != null && i < Directions * Distances.Length; i++)
            {
                Candidate next = Try(host, start + i % Directions * (360f / Directions), Distances[i / Directions], last);
                if (next.Score > best.Score)
                {
                    best = next;
                    ties = 1;
                }
                else if (next.Score >= 0 && next.Score == best.Score && Random.Range(0, ++ties) == 0)
                {
                    best = next; // an even chance among equals, without keeping them all
                }
            }
            direction = best.Angle;
            return best.Spot;
        }

        /// <summary>Where one raider arrives: a few metres around the wave's spot on dry, open ground, raised by the
        /// creature's own ground offset from its spawn entry; the spot itself when a few tries find none.</summary>
        public static Vector3 Around(Vector3 spot, float groundOffset)
        {
            for (int i = 0; ZoneSystem.instance != null && i < 4; i++)
            {
                Vector2 step = Random.insideUnitCircle * SpreadMetres;
                Vector3 at = spot + new Vector3(step.x, 0f, step.y);
                if (Dry(ref at, out _) && !ZoneSystem.instance.IsBlocked(at))
                {
                    return at + Vector3.up * groundOffset;
                }
            }
            return spot + Vector3.up * groundOffset;
        }

        private static Candidate Try(Vector3 host, float angle, float distance, float? last)
        {
            angle = Mathf.Repeat(angle, 360f);
            Vector3 spot = host + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
            int score = Dry(ref spot, out Vector3 normal) ? Score(host, spot, normal, angle, last) : -1;
            return new Candidate(spot, angle, score);
        }

        // On loaded terrain, above the water with a margin, and not in lava. Moves the point onto the ground.
        private static bool Dry(ref Vector3 point, out Vector3 normal)
        {
            ZoneSystem.instance.GetGroundData(ref point, out normal, out _, out _, out Heightmap hmap);
            return hmap != null && point.y >= ZoneSystem.instance.m_waterLevel + WetMargin && !hmap.IsLava(point);
        }

        private static int Score(Vector3 host, Vector3 spot, Vector3 normal, float angle, float? last)
        {
            int score = EffectArea.IsPointInsideArea(spot, EffectArea.Type.PlayerBase) == null ? 16 : 0;
            score += ZoneSystem.instance.IsBlocked(spot) ? 0 : 8;
            score += last == null || Mathf.Abs(Mathf.DeltaAngle(angle, last.Value)) >= MinTurnDegrees ? 4 : 0;
            score += normal.y >= FlatNormal ? 2 : 0;
            return score + (WayClear(spot, host) ? 1 : 0);
        }

        // One ray at chest height from the spot toward the host, stopping at the base's heart: no building piece on it.
        private static bool WayClear(Vector3 spot, Vector3 host)
        {
            Vector3 from = spot + Vector3.up * 1.5f;
            Vector3 way = host + Vector3.up * 1.5f - from;
            float length = way.magnitude - HeartMetres;
            return length <= 0f || !Physics.Raycast(from, way.normalized, length, PieceMask);
        }

        /// <summary>One spot tried: where, which way from the host, and its score (-1: wet, lava or not loaded).</summary>
        private readonly struct Candidate
        {
            public Candidate(Vector3 spot, float angle, int score)
            {
                Spot = spot;
                Angle = angle;
                Score = score;
            }

            public Vector3 Spot { get; }

            public float Angle { get; }

            public int Score { get; }
        }
    }
}
